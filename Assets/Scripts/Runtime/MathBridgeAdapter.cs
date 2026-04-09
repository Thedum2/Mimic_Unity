    using System;
using Mimic.Bridge;
using Mimic.Bridge.Model;
using UnityEngine;

namespace Mimic.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class MathBridgeAdapter : MonoBehaviour, IMathPort
    {
        private MathHandler _mathHandler;

        private void Awake()
        {
            _mathHandler = BridgeManager.Instance.GetHandler<MathHandler>("MathManager");
            _mathHandler?.BindPort(this);
        }

        private void OnDestroy()
        {
            _mathHandler?.ClearPort(this);
        }

        public void R2U_MathManager_Add_REQ(
            Request.R2U.MathAdd data,
            Action<Acknowledge.U2R.MathAdd> onSuccess,
            Action<string> onError)
        {
            if (data == null)
            {
                onError?.Invoke("request payload is null.");
                return;
            }

            var sum = data.A + data.B;
            onSuccess?.Invoke(new Acknowledge.U2R.MathAdd(true, sum));
        }
    }
}
