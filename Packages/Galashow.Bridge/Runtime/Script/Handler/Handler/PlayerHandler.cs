using System.Collections.Generic;
using Mimic.Bridge.Model;

namespace Mimic.Bridge
{
    public sealed class PlayerHandler : BaseMessageHandler
    {
        public PlayerHandler() : base("PlayerManager") { }

        public void SurvivorStateChanged(List<string> alivePlayerIds, List<string> eliminatedPlayerIds)
        {
            NTY("SurvivorStateChanged", new Notify.U2R.PlayerManagerSurvivorStateChanged(alivePlayerIds, eliminatedPlayerIds));
        }

        public override void HandleRequest(Message message, System.Action<AcknowledgeResponse> onSuccess, System.Action<string> onError)
        {
            var (_, action) = Util.ParseRoute(message.route);
            onError?.Invoke($"[PlayerHandler] Unknown REQ action '{action}'");
        }

        public override void HandleNotify(Message message)
        {
            var (_, action) = Util.ParseRoute(message.route);
            BridgeLog.Debug($"[PlayerHandler] Unknown NTY action '{action}'");
        }
    }
}
