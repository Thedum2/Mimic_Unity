using Newtonsoft.Json;

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

                public MatchManagerCreateRoom(bool result, string roomId, string inviteCode)
                {
                    Result = result;
                    RoomId = roomId;
                    InviteCode = inviteCode;
                }
            }

            public class MatchManagerJoinRoomByInviteCode
            {
                [JsonProperty("result")] public bool Result { get; set; }
                [JsonProperty("roomId")] public string RoomId { get; set; }
                [JsonProperty("joinedPlayerCount")] public int JoinedPlayerCount { get; set; }

                public MatchManagerJoinRoomByInviteCode(bool result, string roomId, int joinedPlayerCount)
                {
                    Result = result;
                    RoomId = roomId;
                    JoinedPlayerCount = joinedPlayerCount;
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
                [JsonProperty("messageId")] public string MessageId { get; set; }
                [JsonProperty("recordedAt")] public string RecordedAt { get; set; }

                public LobbyChatManagerSubmitMessage(
                    bool result,
                    string roomId,
                    string clientMessageId,
                    string messageId,
                    string recordedAt)
                {
                    Result = result;
                    RoomId = roomId;
                    ClientMessageId = clientMessageId;
                    MessageId = messageId;
                    RecordedAt = recordedAt;
                }
            }

            public class MathAdd
            {
                [JsonProperty("result")] public bool Result { get; set; }
                [JsonProperty("sum")] public int Sum { get; set; }

                public MathAdd(bool result, int sum)
                {
                    Result = result;
                    Sum = sum;
                }
            }
        }
    }
}
