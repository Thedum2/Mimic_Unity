using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using Mimic.Gameplay.Network.Spawn;
using UnityEngine;

namespace Mimic.Gameplay
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-3200)]
    public sealed class VillagePlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private NetworkRunner runner;
        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private string roomName = "room-preview";
        [SerializeField] private GameMode gameMode = GameMode.Shared;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private Vector3 fallbackSpawnPosition = new(0f, 1f, 0f);
        [SerializeField] private Quaternion fallbackSpawnRotation = Quaternion.identity;
        [SerializeField] private bool autoStartOnAwake = false;
        [SerializeField] private float startSessionTimeoutSeconds = 12f;

        private readonly PlayerSpawnCoordinator _spawnCoordinator = new();
        private INetworkSceneManager _sceneManager;
        private INetworkObjectProvider _objectProvider;
        private bool _started;
        private bool _isStarting;
        private bool _hasPendingStart;
        private UniTask<bool> _pendingStart;
        private string _requestedRoomName;

        public string RoomName => roomName;
        public bool IsStarted => _started;

        private void Start()
        {
            if (autoStartOnAwake)
            {
                StartOrJoinSessionAsync(cancellationToken: this.GetCancellationTokenOnDestroy()).Forget();
            }
        }

        private void OnDestroy()
        {
            if (runner != null)
            {
                runner.RemoveCallbacks(this);
                if (_started && runner.IsRunning)
                {
                    runner.Shutdown();
                }
            }
        }

        public void Configure(string targetRoomName, NetworkObject playerPrefabOverride = null, GameMode? gameModeOverride = null)
        {
            if (!string.IsNullOrWhiteSpace(targetRoomName))
            {
                roomName = targetRoomName;
            }

            if (playerPrefabOverride != null)
            {
                playerPrefab = playerPrefabOverride;
            }

            if (gameModeOverride.HasValue)
            {
                gameMode = gameModeOverride.Value;
            }
        }

        public UniTask<bool> StartOrJoinSessionAsync(
            string sessionName = null,
            GameMode? gameModeOverride = null,
            NetworkObject playerPrefabOverride = null,
            bool allowSessionCreation = true,
            CancellationToken cancellationToken = default)
        {
            Configure(sessionName, playerPrefabOverride, gameModeOverride);
            var destroyToken = this.GetCancellationTokenOnDestroy();
            var effectiveToken = cancellationToken.CanBeCanceled ? cancellationToken : destroyToken;

            if (_hasPendingStart)
            {
                return _pendingStart;
            }

            if (string.IsNullOrWhiteSpace(roomName))
            {
                Debug.LogError("VillagePlayerSpawner: roomName is required.");
                return UniTask.FromResult(false);
            }

            if (_started)
            {
                var shouldStartDifferentRoom = string.Equals(_requestedRoomName, roomName, StringComparison.Ordinal) == false;
                if (shouldStartDifferentRoom == false)
                {
                    if (_isStarting)
                    {
                        return _hasPendingStart ? _pendingStart : UniTask.FromResult(false);
                    }

                    return UniTask.FromResult(true);
                }

                if (_isStarting)
                {
                    return _hasPendingStart ? _pendingStart : UniTask.FromResult(false);
                }
            }

            _pendingStart = StartOrJoinSessionInternalAsync(allowSessionCreation, effectiveToken).Preserve();
            _hasPendingStart = true;
            return _pendingStart;
        }

        public void SetRequestedRoomName(string targetRoomName)
        {
            if (string.IsNullOrWhiteSpace(targetRoomName))
            {
                return;
            }

            roomName = targetRoomName;
        }

        private async UniTask<bool> StartOrJoinSessionInternalAsync(bool allowSessionCreation, CancellationToken cancellationToken)
        {
            if (_isStarting)
            {
                return false;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("VillagePlayerSpawner: Player prefab is required.");
                return false;
            }

            ConfigureRunner();
            if (_started && runner != null && runner.IsRunning)
            {
                if (string.Equals(_requestedRoomName, roomName, StringComparison.Ordinal) == false)
                {
                    _requestedRoomName = null;
                    _started = false;
                    runner.Shutdown();
                    await AwaitRunnerShutdownAsync(cancellationToken);
                    if (runner.IsRunning)
                    {
                        Debug.LogWarning(
                            $"VillagePlayerSpawner: Timeout waiting for runner shutdown before joining room '{roomName}'. Attempting StartGame anyway.");
                    }

                    _spawnCoordinator.Clear();
                    PlayerSpawnPointResolver.Clear();
                }
            }

            EnsureRunnerHelpers();
            ResolveSpawnPointsIfNeeded();
            CacheSceneFallbackSpawn();

            _isStarting = true;
            runner.RemoveCallbacks(this);
            runner.AddCallbacks(this);

            try
            {
                Debug.Log($"VillagePlayerSpawner: StartGame starting (mode={gameMode}, room={roomName}).");
                var startTask = runner.StartGame(new StartGameArgs
                {
                    GameMode = gameMode,
                    SessionName = roomName,
                    EnableClientSessionCreation = allowSessionCreation,
                    Scene = new NetworkSceneInfo(),
                    SceneManager = _sceneManager,
                    ObjectProvider = _objectProvider
                });
                var timeoutSeconds = Mathf.Max(1f, startSessionTimeoutSeconds);
                StartGameResult result;
                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeoutCts.CancelAfterSlim(TimeSpan.FromSeconds(timeoutSeconds));
                    try
                    {
                        result = await startTask.AsUniTask().AttachExternalCancellation(timeoutCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return false;
                        }

                        Debug.LogError(
                            $"VillagePlayerSpawner: StartGame timed out after {timeoutSeconds:0.0}s for room '{roomName}'.");
                        if (runner != null && runner.IsRunning)
                        {
                            runner.Shutdown();
                            await AwaitRunnerShutdownAsync(cancellationToken);
                        }

                        return false;
                    }
                }

                _requestedRoomName = roomName;
                _started = true;
                return result.Ok;
            }
            catch (Exception exception)
            {
                Debug.LogError($"VillagePlayerSpawner: Failed to start session '{roomName}'. {exception}");
                return false;
            }
            finally
            {
                _isStarting = false;
                _hasPendingStart = false;
                _pendingStart = default;
            }
        }

        private async UniTask AwaitRunnerShutdownAsync(CancellationToken cancellationToken)
        {
            var timeout = DateTime.UtcNow.AddSeconds(2.5);
            while (runner != null && runner.IsRunning && DateTime.UtcNow < timeout)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await UniTask.Delay(50, cancellationToken: cancellationToken);
            }
        }

        private void ConfigureRunner()
        {
            if (runner != null)
            {
                return;
            }

            runner = GetComponent<NetworkRunner>();
            if (runner != null)
            {
                return;
            }

            var runnerObject = new GameObject("NetworkRunner");
            runnerObject.transform.SetParent(transform);
            runner = runnerObject.AddComponent<NetworkRunner>();
        }

        private void EnsureRunnerHelpers()
        {
            _sceneManager = runner.GetComponent<INetworkSceneManager>();
            if (_sceneManager == null)
            {
                _sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            }

            _objectProvider = runner.GetComponent<INetworkObjectProvider>();
            if (_objectProvider == null)
            {
                _objectProvider = runner.gameObject.AddComponent<NetworkObjectProviderDefault>();
            }
        }

        private void CacheSceneFallbackSpawn()
        {
            PlayerSpawnPointResolver.SetSceneFallbackSpawn(fallbackSpawnPosition, fallbackSpawnRotation);
        }

        private void SpawnPlayerIfNeeded(NetworkRunner runningRunner, PlayerRef player)
        {
            if (runningRunner == null)
            {
                return;
            }

            if (_spawnCoordinator.TrySpawn(
                runningRunner,
                player,
                playerPrefab,
                _ => PlayerSpawnPointResolver.ResolveAndCacheSpawnTransform(player, spawnPoints, fallbackSpawnPosition, fallbackSpawnRotation),
                CanSpawnForPlayer,
                out var spawnedPlayer,
                out var position,
                out var rotation) == false)
            {
                return;
            }

            if (spawnedPlayer == null)
            {
                Debug.LogWarning($"VillagePlayerSpawner: Failed to spawn player for PlayerRef {player.PlayerId}.");
                return;
            }

            var spawnTime = Mathf.Max((float)runningRunner.SimulationTime, 0.0001f);
            spawnedPlayer.transform.SetPositionAndRotation(position, rotation);
            var sync = spawnedPlayer.GetComponent<VillagePlayerSync>();
            if (sync != null)
            {
                sync.ApplySpawnState(position, rotation, spawnTime);
                sync.SetPlayerId($"player_{player.PlayerId}");
            }

            Debug.Log($"VillagePlayerSpawner: Spawned player {player.PlayerId} at {position} with rotation {rotation.eulerAngles} on mode={runningRunner.GameMode}, stateAuth={spawnedPlayer.HasStateAuthority}.");
        }

        private bool HasSpawnedPlayerObject(NetworkRunner runningRunner, PlayerRef player)
        {
            return _spawnCoordinator.HasSpawnedById(runningRunner, player);
        }

        private static bool CanSpawnForPlayer(NetworkRunner runningRunner, PlayerRef player)
        {
            if (runningRunner == null)
            {
                return false;
            }

            // In Shared mode, each client should spawn only its own avatar.
            // Letting both shared master and local peer spawn the same player can produce duplicates.
            if (runningRunner.GameMode == GameMode.Shared)
            {
                return runningRunner.LocalPlayer == player;
            }

            // In host/server style modes, the server is authoritative for player spawning.
            return runningRunner.IsServer;
        }

        private void ResolveSpawnPointsIfNeeded()
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                return;
            }

            var candidates = FindObjectsOfType<Transform>();
            var found = new List<Transform>(candidates.Length);
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate == null || candidate == transform)
                {
                    continue;
                }

                if (candidate.name.IndexOf("spawnpoint", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found.Add(candidate);
                }
            }

            if (found.Count > 0)
            {
                spawnPoints = found.ToArray();
                Debug.Log($"VillagePlayerSpawner: Auto-resolved {spawnPoints.Length} spawn point(s) by object name.");
            }
            else
            {
                Debug.LogWarning("VillagePlayerSpawner: SpawnPoints are not configured and no object named 'SpawnPoint' was found.");
            }
        }

        public void OnReliableDataProgress(NetworkRunner r, PlayerRef p, ReliableKey k, float progress)
        {
        }

        void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner r)
        {
            if (r.LocalPlayer == PlayerRef.None)
            {
                return;
            }

            if (CanSpawnForPlayer(r, r.LocalPlayer) == false)
            {
                return;
            }

            if (HasSpawnedPlayerObject(r, r.LocalPlayer))
            {
                return;
            }

            PlayerSpawnPointResolver.ResolveAndCacheSpawnTransform(r.LocalPlayer, spawnPoints, fallbackSpawnPosition, fallbackSpawnRotation);
            SpawnPlayerIfNeeded(r, r.LocalPlayer);
        }

        void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner r, NetworkObject obj, PlayerRef p)
        {
        }

        void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner r, NetworkObject obj, PlayerRef p)
        {
            if (obj == null)
            {
                return;
            }

            var sync = obj.GetComponent<VillagePlayerSync>();
            if (sync == null)
            {
                return;
            }

            sync.EnsureLocalOwnership();
        }
        void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner r, PlayerRef p)
        {
            Debug.Log($"VillagePlayerSpawner: Player joined id={p.PlayerId}, mode={r.GameMode}, isServer={r.IsServer}, isSharedMaster={r.IsSharedModeMasterClient}, local={r.LocalPlayer}.");
            PlayerSpawnPointResolver.ResolveAndCacheSpawnTransform(p, spawnPoints, fallbackSpawnPosition, fallbackSpawnRotation);
            if (CanSpawnForPlayer(r, p))
            {
                SpawnPlayerIfNeeded(r, p);
            }

            var playerObject = r.GetPlayerObject(p);
            if (playerObject == null)
            {
                return;
            }

            var sync = playerObject.GetComponent<VillagePlayerSync>();
            sync?.EnsureLocalOwnership();
        }
        void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner r, PlayerRef p)
        {
            _spawnCoordinator.RemovePlayer(p);
        }
        void INetworkRunnerCallbacks.OnInput(NetworkRunner r, NetworkInput i)
        {
        }
        void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner r, PlayerRef p, NetworkInput i)
        {
        }
        void INetworkRunnerCallbacks.OnShutdown(NetworkRunner r, ShutdownReason reason)
        {
            _spawnCoordinator.Clear();
            PlayerSpawnPointResolver.Clear();
            _started = false;
        }
        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner r)
        {
        }
        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason)
        {
        }
        void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
        }
        void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner r, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
        }
        void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner r, SimulationMessagePtr message)
        {
        }
        void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner r, List<SessionInfo> sessionList)
        {
        }
        void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data)
        {
        }
        void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner r, HostMigrationToken hostMigrationToken)
        {
        }
        void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner r, PlayerRef p, ReliableKey key, ArraySegment<byte> data)
        {
        }
        void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner r)
        {
        }
    }
}
