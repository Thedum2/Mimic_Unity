using System;
using Mimic.Bridge.Model;

namespace Mimic.Bridge
{
    public sealed class MathHandler : BaseMessageHandler
    {
        private IMathPort _port;

        public MathHandler() : base("MathManager") { }

        public void BindPort(IMathPort port)
        {
            _port = port;
        }

        public void ClearPort(IMathPort port)
        {
            if (_port == port)
            {
                _port = null;
            }
        }

        public override void HandleRequest(Message message, Action<AcknowledgeResponse> onSuccess, Action<string> onError)
        {
            var (_, action) = Util.ParseRoute(message.route);
            switch (action)
            {
                case "Add":
                    HandleAdd(message, onSuccess, onError);
                    break;
                default:
                    onError?.Invoke($"[MathHandler] Unknown REQ action '{action}'");
                    break;
            }
        }

        public override void HandleNotify(Message message)
        {
            var (_, action) = Util.ParseRoute(message.route);
            BridgeLog.Debug($"[MathHandler] Unknown NTY action '{action}'");
        }

        private void HandleAdd(Message message, Action<AcknowledgeResponse> onSuccess, Action<string> onError)
        {
            if (!Util.TryTo<Request.R2U.MathAdd>(message.data, out var request, out var error))
            {
                onError?.Invoke($"bad payload: {error}");
                return;
            }

            if (_port == null)
            {
                onError?.Invoke("No IMathPort registered");
                return;
            }

            _port.R2U_MathManager_Add_REQ(
                request,
                data => onSuccess?.Invoke(Success(data)),
                onError);
        }
    }
}
