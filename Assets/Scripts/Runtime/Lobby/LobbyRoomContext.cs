using System;
using System.Collections.Generic;
using Mimic.Bridge.Model;

namespace Mimic.Gameplay.Lobby
{
    internal sealed class LobbyRoomContext
    {
        private readonly Dictionary<string, string> _inviteToRoomId = new();
        private readonly Dictionary<string, RoomState> _roomStates = new();
        private readonly Dictionary<string, List<ChatMessage>> _messageHistoryByRoom = new();
        private readonly HashSet<string> _runtimeReadyRooms = new();
        private readonly int _maxMessageHistory;

        public LobbyRoomContext(int maxMessageHistory)
        {
            _maxMessageHistory = MathfClampMax(maxMessageHistory, 60);
        }

        public bool TryRegisterRoom(
            string roomId,
            string inviteCode,
            int maxPlayerCount,
            string hostPlayerId,
            string hostPlayerNickname,
            out RoomState roomState)
        {
            roomState = null;
            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(inviteCode))
            {
                return false;
            }

            if (_inviteToRoomId.ContainsKey(inviteCode))
            {
                return false;
            }

            roomState = GetOrCreateRoom(roomId, inviteCode, maxPlayerCount, hostPlayerId, hostPlayerNickname);
            _inviteToRoomId[inviteCode] = roomId;
            return true;
        }

        public bool HasRoom(string roomId)
        {
            return string.IsNullOrWhiteSpace(roomId) == false && _roomStates.ContainsKey(roomId);
        }

        public string ResolveRoomId(string inviteCode, string fallbackInviteCode)
        {
            if (string.IsNullOrWhiteSpace(inviteCode))
            {
                return fallbackInviteCode;
            }

            return _inviteToRoomId.TryGetValue(inviteCode, out var roomId) ? roomId : inviteCode;
        }

        public bool CanJoin(string roomId, string playerId)
        {
            if (_roomStates.TryGetValue(roomId, out var roomState))
            {
                return roomState.CanJoin(playerId);
            }

            return true;
        }

        public RoomState GetOrCreateRoom(
            string roomId,
            string inviteCode,
            int maxPlayerCount,
            string hostPlayerId = null,
            string hostPlayerNickname = null)
        {
            if (_roomStates.TryGetValue(roomId, out var roomState))
            {
                return roomState;
            }

            roomState = new RoomState(roomId, inviteCode, maxPlayerCount, hostPlayerId, hostPlayerNickname);
            _roomStates[roomId] = roomState;
            if (_messageHistoryByRoom.ContainsKey(roomId) == false)
            {
                _messageHistoryByRoom[roomId] = new List<ChatMessage>();
            }

            return roomState;
        }

        public void MarkJoined(string roomId, string playerId, string playerNickname = null)
        {
            if (_roomStates.TryGetValue(roomId, out var roomState))
            {
                roomState.MarkJoined(playerId, playerNickname);
            }
        }

        public List<PlayerBase> GetParticipantsSnapshot(string roomId)
        {
            if (_roomStates.TryGetValue(roomId, out var roomState) == false)
            {
                return new List<PlayerBase>();
            }

            return roomState.GetParticipantsSnapshot();
        }

        public List<ChatMessage> GetOrCreateHistory(string roomId)
        {
            if (_messageHistoryByRoom.TryGetValue(roomId, out var history))
            {
                return history;
            }

            var created = new List<ChatMessage>();
            _messageHistoryByRoom[roomId] = created;
            return created;
        }

        public void TrimHistory(List<ChatMessage> history)
        {
            if (_maxMessageHistory <= 0 || history.Count <= _maxMessageHistory)
            {
                return;
            }

            var trimCount = history.Count - _maxMessageHistory;
            if (trimCount > 0)
            {
                history.RemoveRange(0, trimCount);
            }
        }

        public bool IsRuntimeReady(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return _runtimeReadyRooms.Count > 0;
            }

            return _runtimeReadyRooms.Contains(roomId);
        }

        public void SetRuntimeReady(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return;
            }

            _runtimeReadyRooms.Add(roomId);
        }

        public void ClearRuntimeReady(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return;
            }

            _runtimeReadyRooms.Remove(roomId);
        }

        private static int MathfClampMax(int value, int fallback)
        {
            return Math.Max(1, value > 0 ? value : fallback);
        }
    }

    internal sealed class RoomState
    {
        private readonly HashSet<string> _joinedPlayerIds = new();
        private readonly Dictionary<string, PlayerBase> _participantsByPlayerId = new();

        public string RoomId { get; }
        public string InviteCode { get; }
        public int MaxPlayerCount { get; }
        public int JoinedPlayerCount => _joinedPlayerIds.Count;

        public RoomState(
            string roomId,
            string inviteCode,
            int maxPlayerCount,
            string hostPlayerId,
            string hostPlayerNickname)
        {
            RoomId = roomId;
            InviteCode = inviteCode;
            MaxPlayerCount = maxPlayerCount;

            if (string.IsNullOrWhiteSpace(hostPlayerId) == false)
            {
                MarkJoined(hostPlayerId, hostPlayerNickname, true);
            }
        }

        public bool CanJoin(string playerId)
        {
            if (_joinedPlayerIds.Contains(playerId))
            {
                return true;
            }

            return _joinedPlayerIds.Count < MaxPlayerCount;
        }

        public void MarkJoined(string playerId, string playerNickname = null, bool isHost = false)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            _joinedPlayerIds.Add(playerId);
            var normalizedName = string.IsNullOrWhiteSpace(playerNickname) ? playerId : playerNickname.Trim();

            if (_participantsByPlayerId.TryGetValue(playerId, out var existing))
            {
                existing.PlayerNickname = normalizedName;
                existing.IsHost = existing.IsHost || isHost;
                return;
            }

            _participantsByPlayerId[playerId] = new PlayerBase(
                playerId,
                normalizedName,
                isHost);
        }

        public List<PlayerBase> GetParticipantsSnapshot()
        {
            return new List<PlayerBase>(_participantsByPlayerId.Values);
        }
    }
}
