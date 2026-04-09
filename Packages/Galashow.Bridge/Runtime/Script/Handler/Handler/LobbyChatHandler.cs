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

        public void MessageReceived(string roomId, ChatMessage message)
        {
            NTY("MessageReceived", new Notify.U2R.LobbyChatManagerMessageReceived(roomId, message));
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
                    onError?.Invoke(Util.ToBridgeError("INVALID_ARGUMENT", $"[LobbyChatHandler] Unknown REQ action '{action}'", false));
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
                onError?.Invoke(Util.ToBridgeError("INVALID_ARGUMENT", $"bad payload: {error}", false));
                return;
            }

            if (_port == null)
            {
                onError?.Invoke(Util.ToBridgeError("NOT_INITIALIZED", "No ILobbyChatPort registered", true));
                return;
            }

            _port.R2U_LobbyChatManager_SubmitMessage_REQ(
                request,
                data => onSuccess?.Invoke(Success(data)),
                onError);
        }
    }
}
