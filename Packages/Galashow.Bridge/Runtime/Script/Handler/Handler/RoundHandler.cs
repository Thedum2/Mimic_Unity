using Mimic.Bridge.Model;

namespace Mimic.Bridge
{
    public sealed class RoundHandler : BaseMessageHandler
    {
        public RoundHandler() : base("RoundManager") { }

        public void AssignedTopic(int round, string playerId, string topicId, string topicText)
        {
            NTY("AssignedTopic", new Notify.U2R.RoundManagerAssignedTopic(round, playerId, topicId, topicText));
        }

        public override void HandleRequest(Message message, System.Action<AcknowledgeResponse> onSuccess, System.Action<string> onError)
        {
            var (_, action) = Util.ParseRoute(message.route);
            onError?.Invoke($"[RoundHandler] Unknown REQ action '{action}'");
        }

        public override void HandleNotify(Message message)
        {
            var (_, action) = Util.ParseRoute(message.route);
            BridgeLog.Debug($"[RoundHandler] Unknown NTY action '{action}'");
        }
    }
}
