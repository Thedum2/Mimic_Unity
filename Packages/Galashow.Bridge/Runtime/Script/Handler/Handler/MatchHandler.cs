using System;
using Mimic.Bridge.Model;

namespace Mimic.Bridge
{
    public sealed class MatchHandler : BaseMessageHandler
    {
        private IMatchPort _port;

        public MatchHandler() : base("MatchManager") { }

        public void BindPort(IMatchPort port)
        {
            _port = port;
        }

        public void ClearPort(IMatchPort port)
        {
            if (_port == port)
            {
                _port = null;
            }
        }

        public void RuntimeReady(string roomId, bool unityReady, string sceneName)
        {
            NTY("RuntimeReady", new Notify.U2R.MatchManagerRuntimeReady(roomId, unityReady, sceneName));
        }

        public void StartMatch(string roomId, string matchId, string phase, int playerCount)
        {
            NTY("StartMatch", new Notify.U2R.MatchManagerStartMatch(roomId, matchId, phase, playerCount));
        }

        public void EndMatch(string matchId, string winnerPlayerId, string resultType, string endedAt)
        {
            NTY("EndMatch", new Notify.U2R.MatchManagerEndMatch(matchId, winnerPlayerId, resultType, endedAt));
        }

        public override void HandleRequest(Message message, Action<AcknowledgeResponse> onSuccess, Action<string> onError)
        {
            var (_, action) = Util.ParseRoute(message.route);
            switch (action)
            {
                case "CreateRoom":
                    HandleCreateRoom(message, onSuccess, onError);
                    break;
                case "JoinRoomByInviteCode":
                    HandleJoinRoomByInviteCode(message, onSuccess, onError);
                    break;
                case "RejoinRoom":
                    HandleRejoinRoom(message, onSuccess, onError);
                    break;
                default:
                    onError?.Invoke($"[MatchHandler] Unknown REQ action '{action}'");
                    break;
            }
        }

        public override void HandleNotify(Message message)
        {
            var (_, action) = Util.ParseRoute(message.route);
            BridgeLog.Debug($"[MatchHandler] Unknown NTY action '{action}'");
        }

        private void HandleCreateRoom(Message message, Action<AcknowledgeResponse> onSuccess, Action<string> onError)
        {
            if (!Util.TryTo<Request.R2U.MatchManagerCreateRoom>(message.data, out var request, out var error))
            {
                onError?.Invoke($"bad payload: {error}");
                return;
            }

            if (_port == null)
            {
                onError?.Invoke("No IMatchPort registered");
                return;
            }

            _port.R2U_MatchManager_CreateRoom_REQ(request, data => onSuccess?.Invoke(Success(data)), onError);
        }

        private void HandleJoinRoomByInviteCode(Message message, Action<AcknowledgeResponse> onSuccess, Action<string> onError)
        {
            if (!Util.TryTo<Request.R2U.MatchManagerJoinRoomByInviteCode>(message.data, out var request, out var error))
            {
                onError?.Invoke($"bad payload: {error}");
                return;
            }

            if (_port == null)
            {
                onError?.Invoke("No IMatchPort registered");
                return;
            }

            _port.R2U_MatchManager_JoinRoomByInviteCode_REQ(request, data => onSuccess?.Invoke(Success(data)), onError);
        }

        private void HandleRejoinRoom(Message message, Action<AcknowledgeResponse> onSuccess, Action<string> onError)
        {
            if (!Util.TryTo<Request.R2U.MatchManagerRejoinRoom>(message.data, out var request, out var error))
            {
                onError?.Invoke($"bad payload: {error}");
                return;
            }

            if (_port == null)
            {
                onError?.Invoke("No IMatchPort registered");
                return;
            }

            _port.R2U_MatchManager_RejoinRoom_REQ(request, data => onSuccess?.Invoke(Success(data)), onError);
        }
    }
}
