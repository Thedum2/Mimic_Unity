using System;
using Newtonsoft.Json;
using UnityEngine;
using Mimic.Bridge;

namespace Mimic.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class LobbyBridgeDebugRequestSender : MonoBehaviour
    {
        [Header("Debug Identity")]
        [SerializeField] private string playerId = "player_001";

        [Header("Create Room")]
        [SerializeField] private string roomCode = "A1B2C";
        [SerializeField] private int maxPlayerCount = 5;
        [SerializeField] private string region = "KR";
        [SerializeField] private bool isPrivate = true;
        [SerializeField] private string hostPlayerNickname = "host";

        [Header("Join Room")]
        [SerializeField] private string inviteCode = "A1B2C";

        [Header("Rejoin Room")]
        [SerializeField] private string rejoinRoomId = "room_player_001";
        [SerializeField] private string rejoinSessionId = "room_player_001";

        [Header("Lobby Chat")]
        [SerializeField] private string chatRoomId = "";
        [SerializeField] private string senderPlayerNickname = "Host";
        [SerializeField] private string messageText = "Hello lobby";
        [SerializeField] private string clientMessageId = "client_msg_001";

        [ContextMenu("Debug/Send CreateRoom REQ")]
        public void SendCreateRoomRequest()
        {
            SendRequest(
                "MatchManager_CreateRoom",
                new
                {
                    hostPlayer = new
                    {
                        playerId,
                        playerNickname = hostPlayerNickname,
                        isHost = true
                    },
                    roomCode,
                    maxPlayerCount,
                    region,
                    isPrivate
                });
        }

        [ContextMenu("Debug/Send JoinRoomByInviteCode REQ")]
        public void SendJoinRoomRequest()
        {
            SendRequest(
                "MatchManager_JoinRoomByInviteCode",
                new
                {
                    player = new
                    {
                        playerId,
                        playerNickname = hostPlayerNickname
                    },
                    inviteCode
                });
        }

        [ContextMenu("Debug/Send RejoinRoom REQ")]
        public void SendRejoinRoomRequest()
        {
            SendRequest(
                "MatchManager_RejoinRoom",
                new
                {
                    player = new
                    {
                        playerId,
                        playerNickname = hostPlayerNickname
                    },
                    roomId = rejoinRoomId,
                    sessionId = rejoinSessionId
                });
        }

        [ContextMenu("Debug/Send LobbyChat SubmitMessage REQ")]
        public void SendLobbyChatRequest()
        {
            SendRequest(
                "LobbyChatManager_SubmitMessage",
                new
                {
                    roomId = chatRoomId,
                    sender = new
                    {
                        playerId,
                        playerNickname = senderPlayerNickname,
                        isHost = false
                    },
                    message = new
                    {
                        senderPlayerId = playerId,
                        senderPlayerNickname = senderPlayerNickname,
                        messageText,
                        messageId = clientMessageId,
                        createdAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
                    },
                    clientMessageId
                });
        }

        private void SendRequest(string route, object data)
        {
            var payload = new DebugBridgeMessage
            {
                ok = true,
                type = "REQ",
                from = "R",
                to = "U",
                route = route,
                id = Guid.NewGuid().ToString(),
                data = data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var json = JsonConvert.SerializeObject(payload);
            BridgeManager.Instance.ReceiveMessage(json);
        }

        [Serializable]
        private sealed class DebugBridgeMessage
        {
            public bool ok;
            public string type;
            public string from;
            public string to;
            public string route;
            public string id;
            public object data;
            public long timestamp;
        }
    }
}
