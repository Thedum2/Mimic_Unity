using System;
using Mimic.Bridge.Model;

namespace Mimic.Bridge
{
    public sealed class LobbyChatHandler : BaseMessageHandler
    {
        private ILobbyChatPort _port;

        public LobbyChatHandler() : base("LobbyChatManager") { }

        public void BindPort(ILobbyChatPort port)
        {
            _port = port;
        }

        public void ClearPort(ILobbyChatPort port)
        {
            if (_port == port)
            {
                _port = null;
            }
        }

        public void MessageReceived(string roomId, Notify.U2R.LobbyChatManagerMessage message)
        {
            NTY("MessageReceived", new Notify.U2R.LobbyChatManagerMessageReceived(roomId, message));
        }

        public void HistoryUpdated(string roomId, System.Collections.Generic.List<Notify.U2R.LobbyChatManagerMessage> messages)
        {
            NTY("HistoryUpdated", new Notify.U2R.LobbyChatManagerHistoryUpdated(roomId, messages));
        }

        public void SystemMessage(
            string roomId,
            string eventType,
            string targetPlayerId,
            string targetDisplayName,
            string messageText,
            string createdAt)
        {
            NTY(
                "SystemMessage",
                new Notify.U2R.LobbyChatManagerSystemMessage(roomId, eventType, targetPlayerId, targetDisplayName, messageText, createdAt));
        }

        public override void HandleRequest(Message message, Action<AcknowledgeResponse> onSuccess, Action<string> onError)
        {
            var (_, action) = Util.ParseRoute(message.route);
            switch (action)
            {
                case "SubmitMessage":
                    HandleSubmitMessage(message, onSuccess, onError);
                    break;
                default:
                    onError?.Invoke($"[LobbyChatHandler] Unknown REQ action '{action}'");
                    break;
            }
        }

        public override void HandleNotify(Message message)
        {
            var (_, action) = Util.ParseRoute(message.route);
            BridgeLog.Debug($"[LobbyChatHandler] Unknown NTY action '{action}'");
        }

        private void HandleSubmitMessage(Message message, Action<AcknowledgeResponse> onSuccess, Action<string> onError)
        {
            if (!Util.TryTo<Request.R2U.LobbyChatManagerSubmitMessage>(message.data, out var request, out var error))
            {
                onError?.Invoke($"bad payload: {error}");
                return;
            }

            if (_port == null)
            {
                onError?.Invoke("No ILobbyChatPort registered");
                return;
            }

            _port.R2U_LobbyChatManager_SubmitMessage_REQ(
                request,
                data => onSuccess?.Invoke(Success(data)),
                onError);
        }
    }
}
