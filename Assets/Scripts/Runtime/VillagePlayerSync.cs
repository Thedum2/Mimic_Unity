using System.Collections.Generic;
using System.Linq;
using Fusion;
using Mimic.Gameplay.Network.Spawn;
using UnityEngine;

namespace Mimic.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(VillageCharacterMovement))]
    [DefaultExecutionOrder(-2500)]
    public sealed class VillagePlayerSync : NetworkBehaviour
    {
        public static VillagePlayerSync LocalPlayer { get; private set; }

        [SerializeField] private bool isLocalPlayer;
        [SerializeField] private string playerId = "player_01";
        [SerializeField] private string sessionRoomId = "room-preview";
        [SerializeField] private string sessionMatchId = "match-preview";
        [SerializeField] private float localSendInterval = 0.12f;
        [SerializeField] private float interpolationSpeed = 12f;
        [SerializeField] private float noDataTimeoutSeconds = 1.5f;
        [SerializeField] private int spawnAnchorHoldFrames = 12;

        private static readonly Dictionary<string, VillagePlayerSync> PlayersById = new();
        private static readonly List<VillagePlayerSync> AllPlayers = new();

        [Networked]
        private Vector3 NetworkPosition { get; set; }

        [Networked]
        private Vector3 NetworkForward { get; set; }

        [Networked]
        private float NetworkSpeed { get; set; }

        [Networked]
        private float NetworkUpdateTime { get; set; }

        private const float DirectionDeadZone = 0.0001f;
        private const float FloatEqualityTolerance = 0.0001f;
        private const float MinSpawnUpdateTime = 0.0001f;
        private const float InitialSpawnRepairWindowSeconds = 2f;

        private VillageCharacterMovement _movement;
        private float _lastRemoteDataTime = -999f;
        private float _lastRemoteKnownTimestamp = -999f;
        private float _nextPublishTime;
        private float _nextSpawnSeedAttemptTime;
        private int _spawnSeedAttemptCount;
        private int _spawnAnchorFrameBudget;
        private float _spawnRepairWindowEndsAt;
        private string _currentPlayerId;
        private bool _hasNetworkSpawnState;
        private bool _hasSpawnAnchor;
        private Vector3 _spawnAnchorPosition;
        private Quaternion _spawnAnchorRotation = Quaternion.identity;
        private const float SpawnSeedRetryInterval = 0.08f;
        private const int MaxSpawnSeedAttempts = 40;

        public string PlayerId => playerId;
        public bool IsLocalPlayer => IsNetworkRunning ? IsLocallyOwned : isLocalPlayer;
        private bool IsNetworkRunning => Runner != null && Runner.IsRunning;
        private bool IsLocallyOwned => HasInputAuthority || IsLocalRunnerAuthority();

        private void Awake()
        {
            _movement = GetComponent<VillageCharacterMovement>();
        }

        private void OnEnable()
        {
            Register();
            ApplyAuthorityMode();
        }

        private void OnDisable()
        {
            Unregister();
        }

        public override void Spawned()
        {
            if (IsNetworkRunning == false)
            {
                return;
            }

            if (HasStateAuthority || HasInputAuthority || IsLocalRunnerAuthority())
            {
                CacheSpawnAnchorFromAuthority();
                if (!_hasNetworkSpawnState)
                {
                    TrySeedSpawnFromLocalTransform();
                }

                _debugLogged = false;
                EnsureLocalOwnership();
                StartSpawnRepairWindow();
                TryBindCameraTarget();
                return;
            }

            _debugLogged = false;
            EnsureLocalOwnership();
            StartSpawnRepairWindow();
            TryBindCameraTarget();
        }

        public void EnsureLocalOwnership()
        {
            RefreshLocalPlayerCache();
            ApplyAuthorityMode();
            TryBindCameraTarget();
        }

        public override void FixedUpdateNetwork()
        {
            if (!IsNetworkRunning)
            {
                return;
            }

            RepairInvalidInitialSpawnPosition();
            EnforceSpawnAnchor();
            ApplyAuthorityMode();
            EnsureSpawnStateInitialized();

            if (HasStateAuthority || IsLocalRunnerAuthority())
            {
                TryBindCameraTarget();

                if (!_hasNetworkSpawnState)
                {
                    return;
                }

                PublishLocalState();
            }
            else
            {
                _movement?.SetManualInputEnabled(false);
            }
        }

        public override void Render()
        {
            if (_debugLogged == false && IsNetworkRunning)
            {
                Debug.Log(
                    $"[VillagePlayerSync] Render enter name={name}, netPos={NetworkPosition}, updateTime={NetworkUpdateTime}, hasSpawnState={_hasNetworkSpawnState}, stateAuth={HasStateAuthority}, inputAuth={HasInputAuthority}, localPlayer={IsLocalPlayer}");
                _debugLogged = true;
            }

            EnforceSpawnAnchor();

            if (!IsNetworkRunning || IsLocallyOwned)
            {
                if (IsLocallyOwned && _hasNetworkSpawnState == false)
                {
                    if (TryApplyNetworkSpawnState())
                    {
                        _hasNetworkSpawnState = true;
                    }
                }

                TryBindCameraTarget();
                return;
            }

            if (_hasNetworkSpawnState == false && TryResolveSpawnFromAuthority(out var startupPosition, out var startupRotation))
            {
                if ((transform.position - startupPosition).sqrMagnitude > FloatEqualityTolerance ||
                    (transform.rotation.eulerAngles - startupRotation.eulerAngles).sqrMagnitude > FloatEqualityTolerance)
                {
                    transform.position = startupPosition;
                    transform.rotation = startupRotation;
                }
            }

            TryApplyNetworkSpawnState();

            if (!HasChangedNetworkTimestamp())
            {
                if (Time.time - _lastRemoteDataTime > noDataTimeoutSeconds)
                {
                    return;
                }
            }
            else
            {
                _lastRemoteDataTime = Time.time;
            }

            var desiredRotation = GetForwardLookRotation(transform.rotation, NetworkForward);
            var t = 1f - Mathf.Exp(-interpolationSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, NetworkPosition, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);
        }

        private bool HasChangedNetworkTimestamp()
        {
            if (NetworkUpdateTime < MinSpawnUpdateTime)
            {
                return false;
            }

            if (Mathf.Abs(NetworkUpdateTime - _lastRemoteKnownTimestamp) <= FloatEqualityTolerance)
            {
                return false;
            }

            _lastRemoteKnownTimestamp = NetworkUpdateTime;
            return true;
        }

        private bool _debugLogged;

        private void StartSpawnRepairWindow()
        {
            _spawnRepairWindowEndsAt = Time.time + InitialSpawnRepairWindowSeconds;
        }

        private void EnforceSpawnAnchor()
        {
            if (!_hasSpawnAnchor || _spawnAnchorFrameBudget <= 0 || !HasStateAuthority && !HasInputAuthority)
            {
                return;
            }

            if ((transform.position - _spawnAnchorPosition).sqrMagnitude > FloatEqualityTolerance ||
                Quaternion.Angle(transform.rotation, _spawnAnchorRotation) > 0.05f)
            {
                transform.SetPositionAndRotation(_spawnAnchorPosition, _spawnAnchorRotation);
            }

            _spawnAnchorFrameBudget--;
        }

        private void RepairInvalidInitialSpawnPosition()
        {
            if (Time.time > _spawnRepairWindowEndsAt || _hasNetworkSpawnState || (HasInputAuthority == false && HasStateAuthority == false))
            {
                return;
            }

            if (IsLikelyInvalidOrigin(transform.position) == false)
            {
                _spawnRepairWindowEndsAt = Time.time;
                return;
            }

            if (!TryResolveSpawnFromAuthority(out var spawnPosition, out var spawnRotation))
            {
                return;
            }

            if (IsLikelyInvalidOrigin(spawnPosition))
            {
                return;
            }

            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            if (HasStateAuthority || HasInputAuthority)
            {
                ApplyNetworkSnapshot(
                    spawnPosition,
                    spawnRotation * Vector3.forward,
                    0f,
                    Mathf.Max((float)Runner.SimulationTime, MinSpawnUpdateTime));
            }

            _spawnRepairWindowEndsAt = Time.time;
        }

        private bool IsLikelyInvalidOrigin(Vector3 position)
        {
            return position.sqrMagnitude <= FloatEqualityTolerance;
        }

        private bool CacheSpawnAnchorFromAuthority()
        {
            if (TryResolveSpawnFromAuthority(out var spawnPosition, out var spawnRotation))
            {
                _spawnAnchorPosition = spawnPosition;
                _spawnAnchorRotation = spawnRotation;
                _hasSpawnAnchor = true;
                _spawnAnchorFrameBudget = spawnAnchorHoldFrames;
                return true;
            }

            return false;
        }

        private bool TryApplyNetworkSpawnState()
        {
            if (_hasNetworkSpawnState || HasStateAuthority)
            {
                return false;
            }

            if (NetworkUpdateTime < MinSpawnUpdateTime)
            {
                return false;
            }

            if (HasChangedNetworkTimestamp() == false)
            {
                if (_lastRemoteKnownTimestamp > -900f)
                {
                    return false;
                }

                if (NetworkPosition.sqrMagnitude <= FloatEqualityTolerance)
                {
                    return false;
                }

                _lastRemoteKnownTimestamp = NetworkUpdateTime;
            }

            var desiredRotation = GetForwardLookRotation(transform.rotation, NetworkForward);
            transform.position = NetworkPosition;
            transform.rotation = desiredRotation;
            _hasNetworkSpawnState = true;
            _lastRemoteDataTime = Time.time;
            return true;
        }

        private void EnsureSpawnStateInitialized()
        {
            if (_hasNetworkSpawnState)
            {
                return;
            }

            if (Time.time < _nextSpawnSeedAttemptTime)
            {
                return;
            }

            _nextSpawnSeedAttemptTime = Time.time + SpawnSeedRetryInterval;

            TryApplyNetworkSpawnState();
            if (_hasNetworkSpawnState)
            {
                _spawnSeedAttemptCount = 0;
                return;
            }

            if (!HasStateAuthority && !HasInputAuthority && !IsLocalRunnerAuthority())
            {
                return;
            }

            if (TrySeedSpawnFromLocalTransform())
            {
                _spawnSeedAttemptCount = 0;
                CacheSpawnAnchorFromAuthority();
                return;
            }

            _spawnSeedAttemptCount++;
            if (_spawnSeedAttemptCount < MaxSpawnSeedAttempts)
            {
                return;
            }

            if (transform.position.sqrMagnitude > FloatEqualityTolerance)
            {
                ApplyNetworkSnapshot(transform.position, transform.forward, 0f, Mathf.Max((float)Runner.SimulationTime, MinSpawnUpdateTime));
                _hasNetworkSpawnState = true;
                _lastRemoteDataTime = Time.time;
            }
        }

        private bool TrySeedSpawnFromLocalTransform()
        {
            if (_hasNetworkSpawnState)
            {
                return true;
            }

            if (!HasInputAuthority && !HasStateAuthority && !IsLocalRunnerAuthority())
            {
                return false;
            }

            var hasSpawn = TryResolveSpawnFromAuthority(out var spawnPosition, out var spawnRotation);
            if (hasSpawn)
            {
                if ((transform.position - spawnPosition).sqrMagnitude > FloatEqualityTolerance ||
                    (transform.rotation.eulerAngles - spawnRotation.eulerAngles).sqrMagnitude > FloatEqualityTolerance)
                {
                    transform.SetPositionAndRotation(spawnPosition, spawnRotation);
                }

                ApplyNetworkSnapshot(spawnPosition, spawnRotation * Vector3.forward, 0f,
                    Mathf.Max((float)Runner.SimulationTime, MinSpawnUpdateTime));
                _hasNetworkSpawnState = true;
                _lastRemoteDataTime = Time.time;
                _hasSpawnAnchor = true;
                _spawnAnchorPosition = spawnPosition;
                _spawnAnchorRotation = spawnRotation;
                _spawnAnchorFrameBudget = spawnAnchorHoldFrames;
                Debug.Log(
                    $"[VillagePlayerSync] Seeded spawn for {name} from authority player {Object?.InputAuthority}, pos={spawnPosition}, rot={spawnRotation.eulerAngles}");
                return true;
            }

            if (HasStateAuthority && transform.position.sqrMagnitude > FloatEqualityTolerance)
            {
                ApplyNetworkSnapshot(transform.position, transform.forward, 0f, Mathf.Max((float)Runner.SimulationTime, MinSpawnUpdateTime));
                _hasNetworkSpawnState = true;
                _lastRemoteDataTime = Time.time;
                Debug.Log($"[VillagePlayerSync] Seed fallback state for {name}, pos={transform.position}");
                return true;
            }

            return false;
        }

        private bool TryResolveSpawnFromAuthority(out Vector3 spawnPosition, out Quaternion spawnRotation)
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;

            if (Object != null &&
                Object.InputAuthority != PlayerRef.None &&
                PlayerSpawnPointResolver.TryGetSpawnTransform(Object.InputAuthority, out var cachedPosition, out var cachedRotation))
            {
                spawnPosition = cachedPosition;
                spawnRotation = cachedRotation;
                return true;
            }

            if (Runner != null &&
                Runner.LocalPlayer != PlayerRef.None &&
                PlayerSpawnPointResolver.TryGetSpawnTransform(Runner.LocalPlayer, out var localPosition, out var localRotation))
            {
                spawnPosition = localPosition;
                spawnRotation = localRotation;
                return true;
            }

            return false;
        }

        private void Register()
        {
            _currentPlayerId = ResolvePlayerId();
            if (!PlayersById.ContainsKey(_currentPlayerId))
            {
                PlayersById[_currentPlayerId] = this;
            }

            if (!AllPlayers.Contains(this))
            {
                AllPlayers.Add(this);
            }

            RefreshLocalPlayerCache();
        }

        private void Unregister()
        {
            if (LocalPlayer == this)
            {
                LocalPlayer = null;
            }

            PlayersById.Remove(ResolvePlayerId());
            AllPlayers.Remove(this);
        }

        public SnapshotInfo CreateSnapshot()
        {
            var isMoving = Mathf.Abs(_movement?.LastSpeed ?? 0f) > DirectionDeadZone;
            var forward = (_movement != null && _movement.LastSpeed > 0f)
                ? _movement.Forward
                : (NetworkForward.sqrMagnitude > DirectionDeadZone ? NetworkForward : transform.forward);
            var speed = HasStateAuthority || HasInputAuthority
                ? (_movement != null ? _movement.LastSpeed : 0f)
                : NetworkSpeed;

            return new SnapshotInfo
            {
                PlayerId = ResolvePlayerId(),
                DisplayName = gameObject.name,
                IsLocalPlayer = IsLocalPlayer,
                IsMoving = isMoving,
                Speed = speed,
                Position = NetworkPosition,
                Forward = forward.normalized
            };
        }

        public void SetPlayerId(string newPlayerId)
        {
            if (IsNetworkRunning && Runner.IsRunning)
            {
                Unregister();
            }

            playerId = string.IsNullOrWhiteSpace(newPlayerId)
                ? ResolvePlayerId()
                : newPlayerId;
            Register();
        }

        public void ApplySpawnState(Vector3 position, Quaternion rotation, float updateTime, float speed = 0f, Vector3? forward = null)
        {
            if (!IsNetworkRunning || !HasStateAuthority)
            {
                return;
            }

            var spawnForward = forward ?? rotation * Vector3.forward;
            var safeUpdateTime = Mathf.Max(updateTime, MinSpawnUpdateTime);
            ApplyNetworkSnapshot(position, spawnForward, speed, safeUpdateTime);
            _hasNetworkSpawnState = true;
            _spawnSeedAttemptCount = 0;
            _lastRemoteDataTime = Time.time;
            _hasSpawnAnchor = true;
            _spawnAnchorPosition = position;
            _spawnAnchorRotation = rotation;
            _spawnAnchorFrameBudget = spawnAnchorHoldFrames;
            transform.position = position;
            transform.rotation = rotation;
            Debug.Log(
                $"[VillagePlayerSync] ApplySpawnState name={name}, authorityState={HasStateAuthority}, inputAuth={HasInputAuthority}, pos={position}, rot={rotation.eulerAngles}, update={safeUpdateTime}, updateTimeNow={NetworkUpdateTime}");
        }
        

        private string ResolvePlayerId()
        {
            if (!string.IsNullOrWhiteSpace(_currentPlayerId))
            {
                return _currentPlayerId;
            }

            if (string.IsNullOrWhiteSpace(playerId) || playerId == "player_01")
            {
                _currentPlayerId = $"player_{gameObject.GetInstanceID()}";
                return _currentPlayerId;
            }

            _currentPlayerId = playerId;
            return _currentPlayerId;
        }

        private void ApplyAuthorityMode()
        {
            if (_movement == null)
            {
                return;
            }

            var controlledLocally = IsLocalPlayer;
            _movement.SetLocalPlayer(controlledLocally);
            _movement.SetManualInputEnabled(controlledLocally);
            RefreshLocalPlayerCache();
        }

        private void PublishLocalState()
        {
            if (localSendInterval > 0f && Time.time < _nextPublishTime)
            {
                return;
            }

            _nextPublishTime = Time.time + localSendInterval;

            if (_movement == null)
            {
                return;
            }

            var forward = _movement.Forward.sqrMagnitude > DirectionDeadZone
                ? _movement.Forward
                : Vector3.forward;
            var speed = _movement.LastSpeed;

            var updateTime = Time.time;

            if (HasStateAuthority)
            {
                ApplyNetworkSnapshot(transform.position, forward, speed, updateTime);
                return;
            }

            if (HasInputAuthority)
            {
                RpcPublishLocalSnapshot(transform.position, forward, speed, updateTime);
            }
        }

        private void ApplyNetworkSnapshot(Vector3 position, Vector3 forward, float speed, float sequence)
        {
            NetworkPosition = position;
            NetworkForward = forward;
            NetworkSpeed = speed;
            NetworkUpdateTime = sequence;
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, InvokeLocal = false, Channel = RpcChannel.Unreliable)]
        private void RpcPublishLocalSnapshot(Vector3 position, Vector3 forward, float speed, float sequence)
        {
            ApplyNetworkSnapshot(position, forward, speed, sequence);
        }

        private static Quaternion GetForwardLookRotation(Quaternion fallbackRotation, Vector3 forward)
        {
            if (forward.sqrMagnitude <= DirectionDeadZone)
            {
                return fallbackRotation;
            }

            return Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        public static IEnumerable<VillagePlayerSync> GetAllPlayers() => AllPlayers;

        public struct SnapshotInfo
        {
            public string PlayerId { get; set; }
            public string DisplayName { get; set; }
            public bool IsLocalPlayer { get; set; }
            public bool IsMoving { get; set; }
            public float Speed { get; set; }
            public Vector3 Position { get; set; }
            public Vector3 Forward { get; set; }
        }

        private void RefreshLocalPlayerCache()
        {
            if (HasInputAuthority)
            {
                LocalPlayer = this;
                return;
            }

            if (Runner != null && Runner.LocalPlayer != PlayerRef.None &&
                Object != null &&
                Object.InputAuthority == Runner.LocalPlayer)
            {
                LocalPlayer = this;
                return;
            }

            if (LocalPlayer != this)
            {
                return;
            }

            if (!IsNetworkRunning)
            {
                LocalPlayer = null;
                return;
            }

            LocalPlayer = null;
            var inputAuthorityPlayer = UnityEngine.Object.FindObjectsOfType<VillagePlayerSync>()
                .FirstOrDefault(x => x != null && x.Runner != null && x.Runner.IsRunning && x.HasInputAuthority);
            if (inputAuthorityPlayer != null)
            {
                LocalPlayer = inputAuthorityPlayer;
            }
        }

        private void TryBindCameraTarget()
        {
            if (HasInputAuthority == false && IsLocalRunnerAuthority() == false)
            {
                return;
            }

            if (IsLocalRunnerAuthority())
            {
                _movement?.SetLocalPlayer(true);
            }

            var camera = UnityEngine.Object.FindObjectOfType<IsometricFollowCamera>();
            if (camera != null)
            {
                camera.SetTarget(transform);
            }
        }

        private bool IsLocalRunnerAuthority()
        {
            if (Object == null || Runner == null)
            {
                return false;
            }

            return Runner.LocalPlayer != PlayerRef.None && Object.InputAuthority == Runner.LocalPlayer;
        }
    }
}
