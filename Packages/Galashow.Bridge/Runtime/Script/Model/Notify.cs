using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mimic.Bridge.Model
{
    public class Notify
    {
        public class R2U
        {
        }

        public class U2R
        {
            public class RoundResult
            {
                [JsonProperty("survivorsUserIdx")] public List<int> SurvivorsUserIdx { get; set; }
                [JsonProperty("eliminatedUserIdx")] public List<int> EliminatedUserIdx { get; set; }
                [JsonProperty("totalParticipants")] public int TotalParticipants { get; set; }
                [JsonProperty("remainingPlayers")] public int RemainingPlayers { get; set; }
                [JsonProperty("totalPlayTime")] public float TotalPlayTime { get; set; }
            }

            public class MatchManagerRuntimeReady
            {
                [JsonProperty("roomId")] public string RoomId { get; set; }
                [JsonProperty("unityReady")] public bool UnityReady { get; set; }
                [JsonProperty("sceneName")] public string SceneName { get; set; }

                public MatchManagerRuntimeReady(string roomId, bool unityReady, string sceneName)
                {
                    RoomId = roomId;
                    UnityReady = unityReady;
                    SceneName = sceneName;
                }
            }

            public class MatchManagerStartMatch
            {
                [JsonProperty("roomId")] public string RoomId { get; set; }
                [JsonProperty("matchId")] public string MatchId { get; set; }
                [JsonProperty("phase")] public string Phase { get; set; }
                [JsonProperty("playerCount")] public int PlayerCount { get; set; }

                public MatchManagerStartMatch(string roomId, string matchId, string phase, int playerCount)
                {
                    RoomId = roomId;
                    MatchId = matchId;
                    Phase = phase;
                    PlayerCount = playerCount;
                }
            }

            public class RoundManagerAssignedTopic
            {
                [JsonProperty("round")] public int Round { get; set; }
                [JsonProperty("playerId")] public string PlayerId { get; set; }
                [JsonProperty("topicId")] public string TopicId { get; set; }
                [JsonProperty("topicText")] public string TopicText { get; set; }

                public RoundManagerAssignedTopic(int round, string playerId, string topicId, string topicText)
                {
                    Round = round;
                    PlayerId = playerId;
                    TopicId = topicId;
                    TopicText = topicText;
                }
            }

            public class ConversationManagerOpenConversation
            {
                [JsonProperty("conversationId")] public string ConversationId { get; set; }
                [JsonProperty("participants")] public List<PlayerBase> Participants { get; set; }
                [JsonProperty("observerPlayerIds")] public List<string> ObserverPlayerIds { get; set; }
                [JsonProperty("status")] public string Status { get; set; }

                public ConversationManagerOpenConversation(
                    string conversationId,
                    List<PlayerBase> participants,
                    List<string> observerPlayerIds,
                    string status)
                {
                    ConversationId = conversationId;
                    Participants = participants;
                    ObserverPlayerIds = observerPlayerIds;
                    Status = status;
                }
            }

            public class ConversationLastMessage
            {
                [JsonProperty("messageId")] public string MessageId { get; set; }
                [JsonProperty("speakerPlayerId")] public string SpeakerPlayerId { get; set; }
                [JsonProperty("messageText")] public string MessageText { get; set; }
                [JsonProperty("topicUsed")] public bool TopicUsed { get; set; }

                public ConversationLastMessage(string messageId, string speakerPlayerId, string messageText, bool topicUsed)
                {
                    MessageId = messageId;
                    SpeakerPlayerId = speakerPlayerId;
                    MessageText = messageText;
                    TopicUsed = topicUsed;
                }
            }

            public class ConversationManagerSceneUpdated
            {
                [JsonProperty("conversationId")] public string ConversationId { get; set; }
                [JsonProperty("status")] public string Status { get; set; }
                [JsonProperty("participants")] public List<string> Participants { get; set; }
                [JsonProperty("observerPlayerIds")] public List<string> ObserverPlayerIds { get; set; }
                [JsonProperty("lastMessage")] public ConversationLastMessage LastMessage { get; set; }

                public ConversationManagerSceneUpdated(string conversationId, string status, List<string> participants, List<string> observerPlayerIds, ConversationLastMessage lastMessage)
                {
                    ConversationId = conversationId;
                    Status = status;
                    Participants = participants;
                    ObserverPlayerIds = observerPlayerIds;
                    LastMessage = lastMessage;
                }
            }

            public class ConversationHistoryMessage
            {
                [JsonProperty("messageId")] public string MessageId { get; set; }
                [JsonProperty("speakerPlayerId")] public string SpeakerPlayerId { get; set; }
                [JsonProperty("speakerPlayerNickname")] public string SpeakerPlayerNickname { get; set; }
                [JsonProperty("messageText")] public string MessageText { get; set; }
                [JsonProperty("createdAt")] public string CreatedAt { get; set; }

                public ConversationHistoryMessage(string messageId, string speakerPlayerId, string speakerPlayerNickname, string messageText, string createdAt)
                {
                    MessageId = messageId;
                    SpeakerPlayerId = speakerPlayerId;
                    SpeakerPlayerNickname = speakerPlayerNickname;
                    MessageText = messageText;
                    CreatedAt = createdAt;
                }
            }

            public class ConversationManagerHistoryUpdated
            {
                [JsonProperty("conversationId")] public string ConversationId { get; set; }
                [JsonProperty("visibleToPlayerId")] public string VisibleToPlayerId { get; set; }
                [JsonProperty("messages")] public List<ConversationHistoryMessage> Messages { get; set; }

                public ConversationManagerHistoryUpdated(string conversationId, string visibleToPlayerId, List<ConversationHistoryMessage> messages)
                {
                    ConversationId = conversationId;
                    VisibleToPlayerId = visibleToPlayerId;
                    Messages = messages;
                }
            }

            public class LobbyChatManagerMessageReceived
            {
                [JsonProperty("roomId")] public string RoomId { get; set; }
                [JsonProperty("message")] public ChatMessage Message { get; set; }

                public LobbyChatManagerMessageReceived(string roomId, ChatMessage message)
                {
                    RoomId = roomId;
                    Message = message;
                }
            }

            public class LobbyChatManagerHistoryUpdated
            {
                [JsonProperty("roomId")] public string RoomId { get; set; }
                [JsonProperty("messages")] public List<ChatMessage> Messages { get; set; }

                public LobbyChatManagerHistoryUpdated(string roomId, List<ChatMessage> messages)
                {
                    RoomId = roomId;
                    Messages = messages;
                }
            }

            public class PlayerManagerSurvivorStateChanged
            {
                [JsonProperty("alivePlayerIds")] public List<string> AlivePlayerIds { get; set; }
                [JsonProperty("eliminatedPlayerIds")] public List<string> EliminatedPlayerIds { get; set; }

                public PlayerManagerSurvivorStateChanged(List<string> alivePlayerIds, List<string> eliminatedPlayerIds)
                {
                    AlivePlayerIds = alivePlayerIds;
                    EliminatedPlayerIds = eliminatedPlayerIds;
                }
            }

            public class MatchManagerEndMatch
            {
                [JsonProperty("matchId")] public string MatchId { get; set; }
                [JsonProperty("winnerPlayerId")] public string WinnerPlayerId { get; set; }
                [JsonProperty("resultType")] public string ResultType { get; set; }
                [JsonProperty("endedAt")] public string EndedAt { get; set; }

                public MatchManagerEndMatch(string matchId, string winnerPlayerId, string resultType, string endedAt)
                {
                    MatchId = matchId;
                    WinnerPlayerId = winnerPlayerId;
                    ResultType = resultType;
                    EndedAt = endedAt;
                }
            }
        }
    }
}
