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

            public class ConversationParticipant
            {
                [JsonProperty("playerId")] public string PlayerId { get; set; }
                [JsonProperty("displayName")] public string DisplayName { get; set; }

                public ConversationParticipant(string playerId, string displayName)
                {
                    PlayerId = playerId;
                    DisplayName = displayName;
                }
            }

            public class ConversationManagerOpenConversation
            {
                [JsonProperty("conversationId")] public string ConversationId { get; set; }
                [JsonProperty("participants")] public List<ConversationParticipant> Participants { get; set; }
                [JsonProperty("observerPlayerIds")] public List<string> ObserverPlayerIds { get; set; }
                [JsonProperty("status")] public string Status { get; set; }

                public ConversationManagerOpenConversation(string conversationId, List<ConversationParticipant> participants, List<string> observerPlayerIds, string status)
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
                [JsonProperty("speakerDisplayName")] public string SpeakerDisplayName { get; set; }
                [JsonProperty("messageText")] public string MessageText { get; set; }
                [JsonProperty("createdAt")] public string CreatedAt { get; set; }

                public ConversationHistoryMessage(string messageId, string speakerPlayerId, string speakerDisplayName, string messageText, string createdAt)
                {
                    MessageId = messageId;
                    SpeakerPlayerId = speakerPlayerId;
                    SpeakerDisplayName = speakerDisplayName;
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

            public class LobbyChatManagerMessage
            {
                [JsonProperty("messageId")] public string MessageId { get; set; }
                [JsonProperty("senderPlayerId")] public string SenderPlayerId { get; set; }
                [JsonProperty("senderDisplayName")] public string SenderDisplayName { get; set; }
                [JsonProperty("messageText")] public string MessageText { get; set; }
                [JsonProperty("messageType")] public string MessageType { get; set; }
                [JsonProperty("createdAt")] public string CreatedAt { get; set; }

                public LobbyChatManagerMessage(
                    string messageId,
                    string senderPlayerId,
                    string senderDisplayName,
                    string messageText,
                    string messageType,
                    string createdAt)
                {
                    MessageId = messageId;
                    SenderPlayerId = senderPlayerId;
                    SenderDisplayName = senderDisplayName;
                    MessageText = messageText;
                    MessageType = messageType;
                    CreatedAt = createdAt;
                }
            }

            public class LobbyChatManagerMessageReceived
            {
                [JsonProperty("roomId")] public string RoomId { get; set; }
                [JsonProperty("message")] public LobbyChatManagerMessage Message { get; set; }

                public LobbyChatManagerMessageReceived(string roomId, LobbyChatManagerMessage message)
                {
                    RoomId = roomId;
                    Message = message;
                }
            }

            public class LobbyChatManagerHistoryUpdated
            {
                [JsonProperty("roomId")] public string RoomId { get; set; }
                [JsonProperty("messages")] public List<LobbyChatManagerMessage> Messages { get; set; }

                public LobbyChatManagerHistoryUpdated(string roomId, List<LobbyChatManagerMessage> messages)
                {
                    RoomId = roomId;
                    Messages = messages;
                }
            }

            public class LobbyChatManagerSystemMessage
            {
                [JsonProperty("roomId")] public string RoomId { get; set; }
                [JsonProperty("eventType")] public string EventType { get; set; }
                [JsonProperty("targetPlayerId")] public string TargetPlayerId { get; set; }
                [JsonProperty("targetDisplayName")] public string TargetDisplayName { get; set; }
                [JsonProperty("messageText")] public string MessageText { get; set; }
                [JsonProperty("createdAt")] public string CreatedAt { get; set; }

                public LobbyChatManagerSystemMessage(
                    string roomId,
                    string eventType,
                    string targetPlayerId,
                    string targetDisplayName,
                    string messageText,
                    string createdAt)
                {
                    RoomId = roomId;
                    EventType = eventType;
                    TargetPlayerId = targetPlayerId;
                    TargetDisplayName = targetDisplayName;
                    MessageText = messageText;
                    CreatedAt = createdAt;
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

            public class PlayerTransformSnapshot
            {
                [JsonProperty("playerId")] public string PlayerId { get; set; }
                [JsonProperty("displayName")] public string DisplayName { get; set; }
                [JsonProperty("isLocalPlayer")] public bool IsLocalPlayer { get; set; }
                [JsonProperty("moving")] public bool Moving { get; set; }
                [JsonProperty("speed")] public float Speed { get; set; }
                [JsonProperty("positionX")] public float PositionX { get; set; }
                [JsonProperty("positionY")] public float PositionY { get; set; }
                [JsonProperty("positionZ")] public float PositionZ { get; set; }
                [JsonProperty("forwardX")] public float ForwardX { get; set; }
                [JsonProperty("forwardY")] public float ForwardY { get; set; }
                [JsonProperty("forwardZ")] public float ForwardZ { get; set; }

                public PlayerTransformSnapshot(
                    string playerId,
                    string displayName,
                    bool isLocalPlayer,
                    bool moving,
                    float speed,
                    float positionX,
                    float positionY,
                    float positionZ,
                    float forwardX,
                    float forwardY,
                    float forwardZ)
                {
                    PlayerId = playerId;
                    DisplayName = displayName;
                    IsLocalPlayer = isLocalPlayer;
                    Moving = moving;
                    Speed = speed;
                    PositionX = positionX;
                    PositionY = positionY;
                    PositionZ = positionZ;
                    ForwardX = forwardX;
                    ForwardY = forwardY;
                    ForwardZ = forwardZ;
                }
            }

            public class PlayerManagerWorldStateChanged
            {
                [JsonProperty("roomId")] public string RoomId { get; set; }
                [JsonProperty("matchId")] public string MatchId { get; set; }
                [JsonProperty("localPlayerId")] public string LocalPlayerId { get; set; }
                [JsonProperty("tick")] public int Tick { get; set; }
                [JsonProperty("players")] public List<PlayerTransformSnapshot> Players { get; set; }

                public PlayerManagerWorldStateChanged(string roomId, string matchId, string localPlayerId, int tick, List<PlayerTransformSnapshot> players)
                {
                    RoomId = roomId;
                    MatchId = matchId;
                    LocalPlayerId = localPlayerId;
                    Tick = tick;
                    Players = players;
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
