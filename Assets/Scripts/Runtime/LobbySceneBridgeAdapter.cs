using System;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Fusion;
using Mimic.Bridge;
using Mimic.Bridge.Model;
using Mimic.Gameplay.Lobby;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mimic.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class LobbySceneBridgeAdapter : MonoBehaviour, IMatchPort, ILobbyChatPort
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
        private LobbyRoomContext _roomContext;
        private string _currentRoomId;
        private bool _initialRuntimeReadySent;

        private void Awake()
        {
            _roomContext = new LobbyRoomContext(maxMessageHistory);
            EnsurePlayerSpawner();
            ResolvePrefab();
            BindHandlers();
            VillagePlayerSync.OnLobbyChatNetworkMessage += HandleNetworkLobbyChatMessage;
        }

        private void Start()
        {
            TrySendInitialRuntimeReady();
        }

        private void OnDestroy()
        {
            VillagePlayerSync.OnLobbyChatNetworkMessage -= HandleNetworkLobbyChatMessage;
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
            _matchHandler?.BindPort(this);
            _lobbyChatHandler?.BindPort(this);
        }

        private void UnbindHandlers()
        {
            _matchHandler?.ClearPort(this);
            _lobbyChatHandler?.ClearPort(this);
        }

        public void R2U_MatchManager_CreateRoom_REQ(
            Request.R2U.MatchManagerCreateRoom data,
            Action<Acknowledge.U2R.MatchManagerCreateRoom> onSuccess,
            Action<string> onError)
        {
            var hostPlayerId = data?.HostPlayer?.PlayerId;
            var safeHostId = string.IsNullOrWhiteSpace(hostPlayerId) ? "host" : hostPlayerId;
            var safeHostNickname = string.IsNullOrWhiteSpace(data?.HostPlayer?.PlayerNickname)
                ? safeHostId
                : data.HostPlayer.PlayerNickname.Trim();
            var roomCode = NormalizeRoomCode(data?.RoomCode);
            Debug.Log($"[LobbySceneBridgeAdapter] Handle CreateRoom hostPlayerId={safeHostId} roomCode={roomCode}");

            if (playerSpawner == null)
            {
                Debug.LogWarning("[LobbySceneBridgeAdapter] CreateRoom failed: PlayerSpawner is missing in LobbyScene.");
                onError?.Invoke(Util.ToBridgeError("NOT_INITIALIZED", "PlayerSpawner is missing in LobbyScene.", true));
                return;
            }

            if (string.IsNullOrWhiteSpace(roomCode))
            {
                Debug.LogWarning("[LobbySceneBridgeAdapter] CreateRoom failed: roomCode is required.");
                onError?.Invoke(Util.ToBridgeError("INVALID_ARGUMENT", "roomCode is required.", false));
                return;
            }

            if (IsValidInviteCode(roomCode) == false)
            {
                Debug.LogWarning($"[LobbySceneBridgeAdapter] CreateRoom failed: invalid roomCode={roomCode}");
                onError?.Invoke(Util.ToBridgeError("INVALID_ARGUMENT", "roomCode must be 5 uppercase letters or digits.", false));
                return;
            }

            var maxPlayerCount = Mathf.Max(1, data.MaxPlayerCount);

            if (_roomContext.TryRegisterRoom(roomCode, roomCode, maxPlayerCount, safeHostId, safeHostNickname, out var roomState) == false)
            {
                Debug.LogWarning($"[LobbySceneBridgeAdapter] CreateRoom failed: duplicate roomCode={roomCode}");
                onError?.Invoke(Util.ToBridgeError(
                    "INVALID_ARGUMENT",
                    "A room with the same roomCode already exists.",
                    false,
                    new { roomCode }));
                return;
            }

            var roomId = roomCode;
            var inviteCode = roomCode;
            _currentRoomId = roomId;

            StartRoomAsync(
                roomId,
                allowSessionCreation: true,
                started =>
                {
                    if (started)
                    {
                        Debug.Log($"[LobbySceneBridgeAdapter] CreateRoom success roomId={roomId} inviteCode={inviteCode}");
                        onSuccess?.Invoke(new Acknowledge.U2R.MatchManagerCreateRoom(
                            true,
                            roomId,
                            inviteCode,
                            roomState.JoinedPlayerCount,
                            _roomContext.GetParticipantsSnapshot(roomId)));
                        return;
                    }

                    Debug.LogWarning($"[LobbySceneBridgeAdapter] CreateRoom failed to start session roomId={roomId}");
                    // StartRoomInternalAsync already reports error via onError.
                },
                onError: onError);
        }

        public void R2U_MatchManager_JoinRoomByInviteCode_REQ(
            Request.R2U.MatchManagerJoinRoomByInviteCode data,
            Action<Acknowledge.U2R.MatchManagerJoinRoomByInviteCode> onSuccess,
            Action<string> onError)
        {
            var inviteCode = NormalizeInviteCode(data?.InviteCode);
            var playerId = string.IsNullOrWhiteSpace(data?.Player?.PlayerId) ? null : data.Player.PlayerId.Trim();
            var playerNickname = string.IsNullOrWhiteSpace(data?.Player?.PlayerNickname)
                ? playerId
                : data.Player.PlayerNickname.Trim();
            if (string.IsNullOrWhiteSpace(inviteCode))
            {
                onError?.Invoke(Util.ToBridgeError("INVALID_ARGUMENT", "InviteCode is required.", false));
                return;
            }

            if (IsValidInviteCode(inviteCode) == false)
            {
                Debug.LogWarning($"[LobbySceneBridgeAdapter] JoinRoom rejected: invalid inviteCode={inviteCode}");
                onError?.Invoke(Util.ToBridgeError("INVALID_ARGUMENT", "Invite code must be 5 uppercase letters or digits.", false));
                return;
            }

            if (string.IsNullOrWhiteSpace(playerId))
            {
                onError?.Invoke(Util.ToBridgeError("INVALID_ARGUMENT", "playerId is required.", false));
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
                onError?.Invoke(Util.ToBridgeError(
                    "ROOM_FULL",
                    "Room is full.",
                    false,
                    new { roomId = targetRoomId }));
                return;
            }

            _currentRoomId = targetRoomId;
            StartRoomAsync(
                targetRoomId,
                allowSessionCreation: false,
                onSuccess: started =>
                {
                    if (started)
                    {
                        var roomState = _roomContext.GetOrCreateRoom(targetRoomId, inviteCode, int.MaxValue, null);
                        roomState.MarkJoined(playerId, playerNickname);
                        onSuccess?.Invoke(new Acknowledge.U2R.MatchManagerJoinRoomByInviteCode(
                            true,
                            targetRoomId,
                            roomState.JoinedPlayerCount,
                            _roomContext.GetParticipantsSnapshot(targetRoomId)));
                        return;
                    }

                    Debug.LogWarning($"[LobbySceneBridgeAdapter] JoinRoom failed to start session roomId={targetRoomId} playerId={playerId}");
                    // StartRoomInternalAsync already reports error via onError.
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
                onError?.Invoke(Util.ToBridgeError("INVALID_ARGUMENT", "RoomId is required.", false));
                return;
            }

            _currentRoomId = roomId;
            StartRoomAsync(
                roomId,
                allowSessionCreation: false,
                onSuccess: started =>
                {
                    if (started)
                    {
                        onSuccess?.Invoke(new Acknowledge.U2R.MatchManagerRejoinRoom(true, roomId, "WAITING"));
                        return;
                    }

                    // StartRoomInternalAsync already reports error via onError.
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
                onError?.Invoke(Util.ToBridgeError("INVALID_ARGUMENT", "No room id specified.", false));
                return;
            }

            if (playerSpawner == null)
            {
                onError?.Invoke(Util.ToBridgeError("NOT_INITIALIZED", "PlayerSpawner is missing in LobbyScene.", true));
                return;
            }

            if (playerSpawner.IsStarted == false)
            {
                onError?.Invoke(Util.ToBridgeError("NOT_INITIALIZED", "Lobby room is not initialized yet. Create or join room first.", true));
                return;
            }

            if (IsRoomRuntimeReady(roomId) == false)
            {
                onError?.Invoke(Util.ToBridgeError("RUNTIME_NOT_READY", "RuntimeReady was not emitted for this room yet.", true));
                return;
            }

            if (data?.Sender == null || data?.Message == null)
            {
                onError?.Invoke(Util.ToBridgeError("INVALID_ARGUMENT", "sender and message are required.", false));
                return;
            }

            var senderPlayerId = data.Sender.PlayerId;
            var senderNickname = string.IsNullOrWhiteSpace(data.Sender.PlayerNickname)
                ? senderPlayerId
                : data.Sender.PlayerNickname.Trim();
            var messageText = data.Message.MessageText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(senderPlayerId) || messageText.Length == 0)
            {
                onError?.Invoke(Util.ToBridgeError("INVALID_ARGUMENT", "sender.playerId and message.messageText are required.", false));
                return;
            }

            var recordedAt = string.IsNullOrWhiteSpace(data.Message.CreatedAt)
                ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
                : data.Message.CreatedAt;
            var messageId = string.IsNullOrWhiteSpace(data.Message.MessageId)
                ? BuildMessageId()
                : data.Message.MessageId;
            var localSync = VillagePlayerSync.LocalPlayer;
            if (localSync != null)
            {
                localSync.PublishLobbyChat(
                    roomId,
                    senderPlayerId,
                    senderNickname,
                    messageText,
                    messageId,
                    recordedAt);
            }
            else
            {
                // Fallback path when no local network player is bound yet.
                EmitLobbyChatMessageReceived(
                    roomId,
                    senderPlayerId,
                    senderNickname,
                    messageText,
                    messageId,
                    recordedAt);
            }

            var ackMessage = new ChatMessage(messageId, senderPlayerId, senderNickname, messageText, recordedAt);
            onSuccess?.Invoke(new Acknowledge.U2R.LobbyChatManagerSubmitMessage(true, roomId, data.ClientMessageId, ackMessage));
        }

        private void StartRoomAsync(
            string roomId,
            bool allowSessionCreation,
            Action<bool> onSuccess = null,
            Action<string> onError = null)
        {
            StartRoomInternalAsync(roomId, allowSessionCreation, onSuccess, onError).Forget();
        }

        private async UniTaskVoid StartRoomInternalAsync(
            string roomId,
            bool allowSessionCreation,
            Action<bool> onSuccess,
            Action<string> onError)
        {
            if (playerSpawner == null)
            {
                Debug.LogWarning("[LobbySceneBridgeAdapter] StartSession failed: PlayerSpawner is missing in LobbyScene.");
                onError?.Invoke(Util.ToBridgeError("NOT_INITIALIZED", "PlayerSpawner is missing in LobbyScene.", true));
                return;
            }

            try
            {
                Debug.Log($"[LobbySceneBridgeAdapter] StartSession roomId={roomId}");
                playerSpawner.Configure(roomId, defaultPlayerPrefab, roomGameMode);
                var success = await playerSpawner.StartOrJoinSessionAsync(
                    roomId,
                    roomGameMode,
                    defaultPlayerPrefab,
                    allowSessionCreation,
                    this.GetCancellationTokenOnDestroy());
                if (success == false)
                {
                    Debug.LogWarning($"[LobbySceneBridgeAdapter] StartSession failed roomId={roomId}");
                    _roomContext.ClearRuntimeReady(roomId);
                    onError?.Invoke(Util.ToBridgeError(
                        allowSessionCreation ? "INTERNAL_ERROR" : "ROOM_NOT_FOUND",
                        $"Failed to create/join session '{roomId}'.",
                        allowSessionCreation,
                        new { roomId }));
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
                onError?.Invoke(Util.ToBridgeError(
                    "INTERNAL_ERROR",
                    exception.Message,
                    true,
                    new { roomId }));
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

        private void HandleNetworkLobbyChatMessage(VillagePlayerSync.LobbyChatNetworkMessage message)
        {
            if (string.IsNullOrWhiteSpace(message.RoomId))
            {
                return;
            }

            EmitLobbyChatMessageReceived(
                message.RoomId,
                message.SenderPlayerId,
                message.SenderPlayerNickname,
                message.MessageText,
                message.MessageId,
                message.CreatedAt);
        }

        private void EmitLobbyChatMessageReceived(
            string roomId,
            string senderPlayerId,
            string senderNickname,
            string messageText,
            string messageId,
            string createdAt)
        {
            var chatMessage = new ChatMessage(messageId, senderPlayerId, senderNickname, messageText, createdAt);

            var history = _roomContext.GetOrCreateHistory(roomId);
            history.Add(chatMessage);
            _roomContext.TrimHistory(history);

            _lobbyChatHandler?.MessageReceived(roomId, chatMessage);
        }
    }
}
