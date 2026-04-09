using Newtonsoft.Json;

namespace Mimic.Bridge.Model
{
    public class Request
    {
        public class R2U
        {
            public class MatchManagerCreateRoom
            {
                [JsonProperty("hostPlayerId")] public string HostPlayerId { get; set; }
                [JsonProperty("hostPlayerName")] public string HostPlayerName { get; set; }
                [JsonProperty("roomCode")] public string RoomCode { get; set; }
                [JsonProperty("maxPlayerCount")] public int MaxPlayerCount { get; set; }
                [JsonProperty("region")] public string Region { get; set; }
                [JsonProperty("isPrivate")] public bool IsPrivate { get; set; }
            }

            public class MatchManagerJoinRoomByInviteCode
            {
                [JsonProperty("playerId")] public string PlayerId { get; set; }
                [JsonProperty("playerName")] public string PlayerName { get; set; }
                [JsonProperty("inviteCode")] public string InviteCode { get; set; }
            }

            public class MatchManagerRejoinRoom
            {
                [JsonProperty("playerId")] public string PlayerId { get; set; }
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
                [JsonProperty("senderPlayerId")] public string SenderPlayerId { get; set; }
                [JsonProperty("senderDisplayName")] public string SenderDisplayName { get; set; }
                [JsonProperty("messageText")] public string MessageText { get; set; }
                [JsonProperty("clientMessageId")] public string ClientMessageId { get; set; }
            }
        }
    }
}
