using System;
using System.Text.RegularExpressions;
using Fusion;
using Mimic.Bridge;
using Mimic.Bridge.Model;
using Mimic.Gameplay.Lobby;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mimic.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class LobbySceneBridgeAdapter : MonoBehaviour, IMatchPort, ILobbyChatPort, IMathPort
    {
        private const int DefaultMaxMessageHistory = 60;
        private static readonly Regex InviteCodePattern = new("^[A-Z0-9]{5}$", RegexOptions.Compiled);

        [SerializeField] private VillagePlayerSpawner playerSpawner;
        [SerializeField] private NetworkObject defaultPlayerPrefab;
        [SerializeField] private GameMode roomGameMode = GameMode.Shared;
        [SerializeField] private int maxMessageHistory = DefaultMaxMessageHistory;
        [SerializeField] private bool emitRuntimeReady = true;

        private MatchHandler _matchHandler;
        private LobbyChatHandler _lobbyChatHandler;
        private MathHandler _mathHandler;
        private LobbyRoomContext _roomContext;
        private string _currentRoomId;
        private bool _initialRuntimeReadySent;

        private void Awake()
        {
            _roomContext = new LobbyRoomContext(maxMessageHistory);
            EnsurePlayerSpawner();
            ResolvePrefab();
            BindHandlers();
        }

        private void Start()
        {
            TrySendInitialRuntimeReady();
        }

        private void OnDestroy()
        {
            UnbindHandlers();
        }

        private void EnsurePlayerSpawner()
        {
            if (playerSpawner == null)
            {
                playerSpawner = FindObjectOfType<VillagePlayerSpawner>();
            }
        }

        private void ResolvePrefab()
        {
            if (playerSpawner == null || defaultPlayerPrefab == null)
            {
                return;
            }

            playerSpawner.Configure(null, defaultPlayerPrefab, roomGameMode);
        }

        private void BindHandlers()
        {
            var bridge = BridgeManager.Instance;
            _matchHandler = bridge.GetHandler<MatchHandler>("MatchManager");
            _lobbyChatHandler = bridge.GetHandler<LobbyChatHandler>("LobbyChatManager");
            _mathHandler = bridge.GetHandler<MathHandler>("MathManager");
            _matchHandler?.BindPort(this);
            _lobbyChatHandler?.BindPort(this);
            _mathHandler?.BindPort(this);
        }

        private void UnbindHandlers()
        {
            _matchHandler?.ClearPort(this);
            _lobbyChatHandler?.ClearPort(this);
            _mathHandler?.ClearPort(this);
        }

        public void R2U_MatchManager_CreateRoom_REQ(
            Request.R2U.MatchManagerCreateRoom data,
            Action<Acknowledge.U2R.MatchManagerCreateRoom> onSuccess,
            Action<string> onError)
        {
            var safeHost = string.IsNullOrWhiteSpace(data?.HostPlayerId) ? "host" : data.HostPlayerId;
            var roomCode = NormalizeRoomCode(data?.RoomCode);
            Debug.Log($"[LobbySceneBridgeAdapter] Handle CreateRoom hostPlayerId={safeHost} roomCode={roomCode}");

            if (playerSpawner == null)
            {
                Debug.LogWarning("[LobbySceneBridgeAdapter] CreateRoom failed: PlayerSpawner is missing in LobbyScene.");
                onError?.Invoke("PlayerSpawner is missing in LobbyScene.");
                return;
            }

            if (string.IsNullOrWhiteSpace(roomCode))
            {
                Debug.LogWarning("[LobbySceneBridgeAdapter] CreateRoom failed: roomCode is required.");
                onError?.Invoke("roomCode is required.");
                return;
            }

            if (IsValidInviteCode(roomCode) == false)
            {
                Debug.LogWarning($"[LobbySceneBridgeAdapter] CreateRoom failed: invalid roomCode={roomCode}");
                onError?.Invoke("roomCode must be 5 uppercase letters or digits.");
                return;
            }

            var maxPlayerCount = Mathf.Max(1, data.MaxPlayerCount);

            if (_roomContext.TryRegisterRoom(roomCode, roomCode, maxPlayerCount, safeHost, out _) == false)
            {
                Debug.LogWarning($"[LobbySceneBridgeAdapter] CreateRoom failed: duplicate roomCode={roomCode}");
                onError?.Invoke("A room with the same roomCode already exists.");
                return;
            }

            var roomId = roomCode;
            var inviteCode = roomCode;
            _currentRoomId = roomId;

            StartRoomAsync(
                roomId,
                started =>
                {
                    if (started)
                    {
                        Debug.Log($"[LobbySceneBridgeAdapter] CreateRoom success roomId={roomId} inviteCode={inviteCode}");
                        onSuccess?.Invoke(new Acknowledge.U2R.MatchManagerCreateRoom(true, roomId, inviteCode));
                        return;
                    }

                    Debug.LogWarning($"[LobbySceneBridgeAdapter] CreateRoom failed to start session roomId={roomId}");
                    onError?.Invoke($"Failed to create session '{roomId}'.");
                },
                onError: onError);
        }

        public void R2U_MatchManager_JoinRoomByInviteCode_REQ(
            Request.R2U.MatchManagerJoinRoomByInviteCode data,
            Action<Acknowledge.U2R.MatchManagerJoinRoomByInviteCode> onSuccess,
            Action<string> onError)
        {
            var inviteCode = NormalizeInviteCode(data?.InviteCode);
            var playerId = string.IsNullOrWhiteSpace(data?.PlayerId) ? null : data.PlayerId.Trim();
            if (string.IsNullOrWhiteSpace(inviteCode))
            {
                onError?.Invoke("InviteCode is required.");
                return;
            }

            if (IsValidInviteCode(inviteCode) == false)
            {
                Debug.LogWarning($"[LobbySceneBridgeAdapter] JoinRoom rejected: invalid inviteCode={inviteCode}");
                onError?.Invoke("Invite code must be 5 uppercase letters or digits.");
                return;
            }

            if (string.IsNullOrWhiteSpace(playerId))
            {
                onError?.Invoke("playerId is required.");
                return;
            }

            var targetRoomId = _roomContext.ResolveRoomId(inviteCode, inviteCode);
            if (string.Equals(targetRoomId, inviteCode, StringComparison.Ordinal) == false)
            {
                Debug.Log($"[LobbySceneBridgeAdapter] JoinRoom resolved invite={inviteCode} -> room={targetRoomId}");
            }

            if (_roomContext.CanJoin(targetRoomId, playerId) == false)
            {
                Debug.LogWarning($"[LobbySceneBridgeAdapter] JoinRoom rejected: room full roomId={targetRoomId} playerId={playerId}");
                onError?.Invoke("Room is full.");
                return;
            }

            _currentRoomId = targetRoomId;
            StartRoomAsync(
                targetRoomId,
                onSuccess: started =>
                {
                if (started)
                {
                    var roomState = _roomContext.GetOrCreateRoom(targetRoomId, inviteCode, int.MaxValue, null);
                    roomState.MarkJoined(playerId);
                    onSuccess?.Invoke(new Acknowledge.U2R.MatchManagerJoinRoomByInviteCode(true, targetRoomId, roomState.JoinedPlayerCount));
                    return;
                }

                    Debug.LogWarning($"[LobbySceneBridgeAdapter] JoinRoom failed to start session roomId={targetRoomId} playerId={playerId}");
                    onError?.Invoke($"Failed to join session '{targetRoomId}'.");
                },
                onError: onError);
        }

        public void R2U_MatchManager_RejoinRoom_REQ(
            Request.R2U.MatchManagerRejoinRoom data,
            Action<Acknowledge.U2R.MatchManagerRejoinRoom> onSuccess,
            Action<string> onError)
        {
            var roomId = ResolveRoomIdFromRequest(data);
            if (string.IsNullOrWhiteSpace(roomId))
            {
                onError?.Invoke("RoomId is required.");
                return;
            }

            _currentRoomId = roomId;
            StartRoomAsync(
                roomId,
                onSuccess: started =>
                {
                    if (started)
                    {
                        onSuccess?.Invoke(new Acknowledge.U2R.MatchManagerRejoinRoom(true, roomId, "WAITING"));
                        return;
                    }

                    onError?.Invoke($"Failed to rejoin session '{roomId}'.");
                },
                onError: onError);
        }

        public void R2U_LobbyChatManager_SubmitMessage_REQ(
            Request.R2U.LobbyChatManagerSubmitMessage data,
            Action<Acknowledge.U2R.LobbyChatManagerSubmitMessage> onSuccess,
            Action<string> onError)
        {
            var roomId = ResolveMessageRoom(data?.RoomId);
            if (string.IsNullOrWhiteSpace(roomId))
            {
                onError?.Invoke("No room id specified.");
                return;
            }

            if (playerSpawner == null)
            {
                onError?.Invoke("PlayerSpawner is missing in LobbyScene.");
                return;
            }

            if (playerSpawner.IsStarted == false)
            {
                onError?.Invoke("Lobby room is not initialized yet. Create or join room first.");
                return;
            }

            if (IsRoomRuntimeReady(roomId) == false)
            {
                onError?.Invoke("RuntimeReady was not emitted for this room yet.");
                return;
            }

            if (string.IsNullOrWhiteSpace(data.SenderPlayerId) || string.IsNullOrWhiteSpace(data.MessageText))
            {
                onError?.Invoke("senderPlayerId and messageText are required.");
                return;
            }

            var recordedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var messageId = BuildMessageId();
            var chatMessage = new Notify.U2R.LobbyChatManagerMessage(
                messageId,
                data.SenderPlayerId,
                data.SenderDisplayName,
                data.MessageText,
                "USER",
                recordedAt);

            _lobbyChatHandler?.MessageReceived(roomId, chatMessage);

            onSuccess?.Invoke(new Acknowledge.U2R.LobbyChatManagerSubmitMessage(true, roomId, data.ClientMessageId, messageId, recordedAt));
        }

        public void R2U_MathManager_Add_REQ(
            Request.R2U.MathAdd data,
            Action<Acknowledge.U2R.MathAdd> onSuccess,
            Action<string> onError)
        {
            if (data == null)
            {
                onError?.Invoke("request payload is null.");
                return;
            }

            var sum = data.A + data.B;
            onSuccess?.Invoke(new Acknowledge.U2R.MathAdd(true, sum));
        }

        private void StartRoomAsync(
            string roomId,
            Action<bool> onSuccess = null,
            Action<string> onError = null)
        {
            StartRoomInternalAsync(roomId, onSuccess, onError);
        }

        private async void StartRoomInternalAsync(
            string roomId,
            Action<bool> onSuccess,
            Action<string> onError)
        {
            if (playerSpawner == null)
            {
                Debug.LogWarning("[LobbySceneBridgeAdapter] StartSession failed: PlayerSpawner is missing in LobbyScene.");
                onError?.Invoke("PlayerSpawner is missing in LobbyScene.");
                return;
            }

            try
            {
                Debug.Log($"[LobbySceneBridgeAdapter] StartSession roomId={roomId}");
                playerSpawner.Configure(roomId, defaultPlayerPrefab, roomGameMode);
                var success = await playerSpawner.StartOrJoinSessionAsync(roomId, roomGameMode, defaultPlayerPrefab);
                if (success == false)
                {
                    Debug.LogWarning($"[LobbySceneBridgeAdapter] StartSession failed roomId={roomId}");
                    _roomContext.ClearRuntimeReady(roomId);
                    onError?.Invoke($"Failed to create/join session '{roomId}'.");
                    onSuccess?.Invoke(false);
                    return;
                }

                Debug.Log($"[LobbySceneBridgeAdapter] StartSession success roomId={roomId}");

                _roomContext.SetRuntimeReady(roomId);
                onSuccess?.Invoke(true);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[LobbySceneBridgeAdapter] StartSession exception roomId={roomId} error={exception}");
                _roomContext.ClearRuntimeReady(roomId);
                onError?.Invoke(exception.Message);
                onSuccess?.Invoke(false);
            }
        }

        private void SendRuntimeReady(string roomId)
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                sceneName = "LobbyScene";
            }

            _matchHandler.RuntimeReady(roomId, true, sceneName);
        }

        private void TrySendInitialRuntimeReady()
        {
            if (_initialRuntimeReadySent || emitRuntimeReady == false || _matchHandler == null)
            {
                return;
            }

            var initialRoomId = string.IsNullOrWhiteSpace(_currentRoomId) ? string.Empty : _currentRoomId;
            SendRuntimeReady(initialRoomId);
            _initialRuntimeReadySent = true;
        }

        private bool IsRoomRuntimeReady(string roomId)
        {
            return _roomContext.IsRuntimeReady(roomId);
        }

        private string ResolveMessageRoom(string roomId)
        {
            if (!string.IsNullOrWhiteSpace(roomId))
            {
                return roomId;
            }

            return _currentRoomId;
        }

        private string ResolveRoomIdFromRequest(Request.R2U.MatchManagerRejoinRoom data)
        {
            if (data == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(data.RoomId))
            {
                return data.RoomId;
            }

            if (!string.IsNullOrWhiteSpace(data.SessionId))
            {
                return data.SessionId;
            }

            if (!string.IsNullOrWhiteSpace(_currentRoomId))
            {
                return _currentRoomId;
            }

            return null;
        }

        private string NormalizeInviteCode(string inviteCode)
        {
            return string.IsNullOrWhiteSpace(inviteCode) ? null : inviteCode.Trim().ToUpperInvariant();
        }

        private string NormalizeRoomCode(string roomCode)
        {
            return string.IsNullOrWhiteSpace(roomCode) ? null : roomCode.Trim().ToUpperInvariant();
        }

        private string BuildMessageId()
        {
            return $"msg_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}";
        }

        private static bool IsValidInviteCode(string inviteCode)
        {
            return string.IsNullOrWhiteSpace(inviteCode) == false && InviteCodePattern.IsMatch(inviteCode);
        }
    }
}
