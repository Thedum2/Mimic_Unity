using System;
using System.Collections.Generic;
using Mimic.Bridge.Model;

namespace Mimic.Bridge
{
    public sealed class ConversationHandler : BaseMessageHandler
    {
        private IConversationPort _port;

        public ConversationHandler() : base("ConversationManager") { }

        public void BindPort(IConversationPort port)
        {
            _port = port;
        }

        public void ClearPort(IConversationPort port)
        {
            if (_port == port)
            {
                _port = null;
            }
        }

        public void OpenConversation(string conversationId, List<PlayerBase> participants, List<string> observerPlayerIds, string status)
        {
            NTY("OpenConversation", new Notify.U2R.ConversationManagerOpenConversation(conversationId, participants, observerPlayerIds, status));
        }

        public void SceneUpdated(string conversationId, string status, List<string> participants, List<string> observerPlayerIds, Notify.U2R.ConversationLastMessage lastMessage)
        {
            NTY("SceneUpdated", new Notify.U2R.ConversationManagerSceneUpdated(conversationId, status, participants, observerPlayerIds, lastMessage));
        }

        public void HistoryUpdated(string conversationId, string visibleToPlayerId, List<Notify.U2R.ConversationHistoryMessage> messages)
        {
            NTY("HistoryUpdated", new Notify.U2R.ConversationManagerHistoryUpdated(conversationId, visibleToPlayerId, messages));
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
                    onError?.Invoke($"[ConversationHandler] Unknown REQ action '{action}'");
                    break;
            }
        }

        public override void HandleNotify(Message message)
        {
            var (_, action) = Util.ParseRoute(message.route);
            BridgeLog.Debug($"[ConversationHandler] Unknown NTY action '{action}'");
        }

        private void HandleSubmitMessage(Message message, Action<AcknowledgeResponse> onSuccess, Action<string> onError)
        {
            if (!Util.TryTo<Request.R2U.ConversationManagerSubmitMessage>(message.data, out var request, out var error))
            {
                onError?.Invoke($"bad payload: {error}");
                return;
            }

            if (_port == null)
            {
                onError?.Invoke("No IConversationPort registered");
                return;
            }

            _port.R2U_ConversationManager_SubmitMessage_REQ(
                request,
                data => onSuccess?.Invoke(SuccessForAction("MessageAccepted", data)),
                onError);
        }
    }
}
