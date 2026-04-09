using System;
using Newtonsoft.Json;
using UnityEngine;
using Mimic.Bridge;

namespace Mimic.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class MathBridgeSampleRequestSender : MonoBehaviour
    {
        [SerializeField] private int a = 3;
        [SerializeField] private int b = 2;
        [SerializeField] private bool sendOnStart = false;

        private string _pendingRequestId;

        private void OnEnable()
        {
            BridgeManager.OnMessageReceived += OnBridgeMessageReceived;
        }

        private void OnDisable()
        {
            BridgeManager.OnMessageReceived -= OnBridgeMessageReceived;
        }

        private void Start()
        {
            if (sendOnStart)
            {
                SendMathAddRequest();
            }
        }

        [ContextMenu("Debug/Send Math Add REQ (3+2=5 sample)")]
        public void SendMathAddRequest()
        {
            var payload = new DebugBridgeMessage
            {
                ok = true,
                type = "REQ",
                from = "R",
                to = "U",
                route = "MathManager_Add",
                id = Guid.NewGuid().ToString(),
                data = new
                {
                    a,
                    b
                },
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            _pendingRequestId = payload.id;
            var json = JsonConvert.SerializeObject(payload);
            BridgeManager.Instance.ReceiveMessage(json);
            Debug.Log($"[MathSample] send {json}");
        }

        [ContextMenu("Debug/Send Math Add 3+2 REQ")]
        public void SendMathAddPresetRequest()
        {
            a = 3;
            b = 2;
            SendMathAddRequest();
        }

        private void OnBridgeMessageReceived(Message message)
        {
            if (string.IsNullOrEmpty(_pendingRequestId) || _pendingRequestId != message.id)
            {
                return;
            }

            if (string.Equals(message.type, "ACK", StringComparison.OrdinalIgnoreCase) == false)
            {
                return;
            }

            var data = JsonConvert.SerializeObject(message.data);
            Debug.Log($"[MathSample] ack: {data}");
            _pendingRequestId = null;
        }

        [Serializable]
        private sealed class DebugBridgeMessage
        {
            public bool ok;
            public string type;
            public string from;
            public string to;
            public string route;
            public string id;
            public object data;
            public long timestamp;
        }
    }
}
