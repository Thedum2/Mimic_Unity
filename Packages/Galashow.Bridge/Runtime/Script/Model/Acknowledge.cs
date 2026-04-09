using Newtonsoft.Json;
using System.Collections.Generic;

namespace Mimic.Bridge.Model
{
    public class Acknowledge
    {
        public class U2R
        {
            public class MatchManagerCreateRoom
            {
                [JsonProperty("result")] public bool Result { get; set; }
                [JsonProperty("roomId")] public string RoomId { get; set; }
                [JsonProperty("inviteCode")] public string InviteCode { get; set; }
                [JsonProperty("joinedPlayerCount")] public int JoinedPlayerCount { get; set; }
                [JsonProperty("participants")] public List<PlayerBase> Participants { get; set; }

                public MatchManagerCreateRoom(
                    bool result,
                    string roomId,
                    string inviteCode,
                    int joinedPlayerCount,
                    List<PlayerBase> participants)
                {
                    Result = result;
                    RoomId = roomId;
                    InviteCode = inviteCode;
                    JoinedPlayerCount = joinedPlayerCount;
                    Participants = participants ?? new List<PlayerBase>();
                }
            }

            public class MatchManagerJoinRoomByInviteCode
            {
                [JsonProperty("result")] public bool Result { get; set; }
                [JsonProperty("roomId")] public string RoomId { get; set; }
                [JsonProperty("joinedPlayerCount")] public int JoinedPlayerCount { get; set; }
                [JsonProperty("participants")] public List<PlayerBase> Participants { get; set; }

                public MatchManagerJoinRoomByInviteCode(
                    bool result,
                    string roomId,
                    int joinedPlayerCount,
                    List<PlayerBase> participants)
                {
                    Result = result;
                    RoomId = roomId;
                    JoinedPlayerCount = joinedPlayerCount;
                    Participants = participants ?? new List<PlayerBase>();
                }
            }

            public class MatchManagerRejoinRoom
            {
                [JsonProperty("result")] public bool Result { get; set; }
                [JsonProperty("roomId")] public string RoomId { get; set; }
                [JsonProperty("matchStatus")] public string MatchStatus { get; set; }

                public MatchManagerRejoinRoom(bool result, string roomId, string matchStatus)
                {
                    Result = result;
                    RoomId = roomId;
                    MatchStatus = matchStatus;
                }
            }

            public class ConversationManagerMessageAccepted
            {
                [JsonProperty("result")] public bool Result { get; set; }
                [JsonProperty("conversationId")] public string ConversationId { get; set; }
                [JsonProperty("messageId")] public string MessageId { get; set; }
                [JsonProperty("recordedAt")] public string RecordedAt { get; set; }

                public ConversationManagerMessageAccepted(bool result, string conversationId, string messageId, string recordedAt)
                {
                    Result = result;
                    ConversationId = conversationId;
                    MessageId = messageId;
                    RecordedAt = recordedAt;
                }
            }

            public class LobbyChatManagerSubmitMessage
            {
                [JsonProperty("result")] public bool Result { get; set; }
                [JsonProperty("roomId")] public string RoomId { get; set; }
                [JsonProperty("clientMessageId")] public string ClientMessageId { get; set; }
                [JsonProperty("message")] public ChatMessage Message { get; set; }

                public LobbyChatManagerSubmitMessage(
                    bool result,
                    string roomId,
                    string clientMessageId,
                    ChatMessage message)
                {
                    Result = result;
                    RoomId = roomId;
                    ClientMessageId = clientMessageId;
                    Message = message;
                }
            }

        }
    }
}
