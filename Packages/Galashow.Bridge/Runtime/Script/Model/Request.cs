using Newtonsoft.Json;

namespace Mimic.Bridge.Model
{
    public class PlayerBase
    {
        [JsonProperty("playerId")] public string PlayerId { get; set; }
        [JsonProperty("playerNickname")] public string PlayerNickname { get; set; }
        [JsonProperty("isHost")] public bool IsHost { get; set; }

        public PlayerBase()
        {
        }

        public PlayerBase(string playerId, string playerNickname, bool isHost = false)
        {
            PlayerId = playerId;
            PlayerNickname = playerNickname;
            IsHost = isHost;
        }
    }

    public class ChatMessage
    {
        [JsonProperty("messageId")] public string MessageId { get; set; }
        [JsonProperty("senderPlayerId")] public string SenderPlayerId { get; set; }
        [JsonProperty("senderPlayerNickname")] public string SenderPlayerNickname { get; set; }
        [JsonProperty("messageText")] public string MessageText { get; set; }
        [JsonProperty("createdAt")] public string CreatedAt { get; set; }

        public ChatMessage()
        {
        }

        public ChatMessage(string messageId, string senderPlayerId, string senderPlayerNickname, string messageText, string createdAt)
        {
            MessageId = messageId;
            SenderPlayerId = senderPlayerId;
            SenderPlayerNickname = senderPlayerNickname;
            MessageText = messageText;
            CreatedAt = createdAt;
        }
    }

    public class Request
    {
        public class R2U
        {
            public class MatchManagerCreateRoom
            {
                [JsonProperty("hostPlayer")] public PlayerBase HostPlayer { get; set; }
                [JsonProperty("roomCode")] public string RoomCode { get; set; }
                [JsonProperty("maxPlayerCount")] public int MaxPlayerCount { get; set; }
                [JsonProperty("region")] public string Region { get; set; }
                [JsonProperty("isPrivate")] public bool IsPrivate { get; set; }
            }

            public class MatchManagerJoinRoomByInviteCode
            {
                [JsonProperty("player")] public PlayerBase Player { get; set; }
                [JsonProperty("inviteCode")] public string InviteCode { get; set; }
            }

            public class MatchManagerRejoinRoom
            {
                [JsonProperty("player")] public PlayerBase Player { get; set; }
                [JsonProperty("roomId")] public string RoomId { get; set; }
                [JsonProperty("sessionId")] public string SessionId { get; set; }
            }

            public class ConversationManagerSubmitMessage
            {
                [JsonProperty("conversationId")] public string ConversationId { get; set; }
                [JsonProperty("speakerPlayerId")] public string SpeakerPlayerId { get; set; }
                [JsonProperty("messageText")] public string MessageText { get; set; }
                [JsonProperty("topicId")] public string TopicId { get; set; }
            }

            public class LobbyChatManagerSubmitMessage
            {
                [JsonProperty("roomId")] public string RoomId { get; set; }
                [JsonProperty("sender")] public PlayerBase Sender { get; set; }
                [JsonProperty("message")] public ChatMessage Message { get; set; }
                [JsonProperty("clientMessageId")] public string ClientMessageId { get; set; }
            }

        }
    }
}
