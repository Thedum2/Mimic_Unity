using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mimic.Core;
using Newtonsoft.Json;
using UnityEngine;

namespace Mimic.Bridge
{
    [DefaultExecutionOrder(-5001)]
    public class BridgeManager : PersistentMonoSingleton<BridgeManager>
    {
        [Header("Settings")]
        [SerializeField] private float defaultTimeoutSeconds = 10f;
        [SerializeField] private string bridgeGameObjectName = "BridgeManager";

        private readonly Dictionary<string, IMessageHandler> _messageHandlers = new();
        private readonly Dictionary<string, PendingRequest> _pendingRequests = new();

        public static Action<Message> OnMessageReceived;
        public static Action<Message> OnMessageSent;

        private int _idIndex;
        private IBridgeSender _sender;
        private MainHandler _mainHandler;

        protected override void OnInitializing()
        {
            base.OnInitializing();
            gameObject.name = bridgeGameObjectName;
        }

        protected override void OnInitialized()
        {
            _sender = new BridgeSender(this);
            _mainHandler = new MainHandler();
            _mainHandler.Initialize();
            RegisterDefaultHandlers();
            StartCoroutine(CheckTimeouts());
            WebGLBridge.Init();
        }

        public void RegisterHandler(IMessageHandler handler)
        {
            if (handler == null) return;

            var route = handler.GetRoute();
            _messageHandlers[route] = handler;
            if (handler is BaseMessageHandler baseHandler)
            {
                baseHandler.__BindSender(_sender);
            }
        }

        public void UnregisterHandler(string route)
        {
            if (!string.IsNullOrEmpty(route))
            {
                _messageHandlers.Remove(route);
            }
        }

        private void RegisterDefaultHandlers()
        {
            RegisterHandler(_mainHandler.MatchHandler);
            RegisterHandler(_mainHandler.RoundHandler);
            RegisterHandler(_mainHandler.ConversationHandler);
            RegisterHandler(_mainHandler.LobbyChatHandler);
            RegisterHandler(_mainHandler.PlayerHandler);
        }

        public void ReceiveMessage(string jsonMessage)
        {
            try
            {
                if (string.IsNullOrEmpty(jsonMessage))
                {
                    GLog.Warn("[Bridge] Received empty message");
                    return;
                }

                var message = JsonConvert.DeserializeObject<Message>(jsonMessage);
                if (message == null)
                {
                    GLog.Error("[Bridge] Failed to deserialize message");
                    return;
                }

                var route = string.IsNullOrWhiteSpace(message.route) ? "Unknown" : message.route;
                var messageType = string.IsNullOrWhiteSpace(message.type) ? "UNKNOWN" : message.type.ToUpperInvariant();
                Debug.Log($"[INTERFACE] R2U {route} {messageType} {jsonMessage}");
                HandleIncomingMessage(message);
            }
            catch (Exception e)
            {
                GLog.Error($"[Bridge] Parse error: {e.Message}");
            }
        }

        private void HandleIncomingMessage(Message message)
        {
            var (routeName, _) = Util.ParseRoute(message.route);
            OnMessageReceived?.Invoke(message);

            switch (message.type?.ToUpperInvariant())
            {
                case "REQ":
                    HandleRequest(routeName, message);
                    break;
                case "ACK":
                    HandleAcknowledge(message);
                    break;
                case "NTY":
                    HandleNotify(routeName, message);
                    break;
                default:
                    GLog.Warn($"[Bridge] Unknown message type: {message.type}");
                    break;
            }
        }

        private void HandleRequest(string route, Message message)
        {
            if (_messageHandlers.TryGetValue(route, out var handler))
            {
                handler.HandleRequest(
                    message,
                    onSuccess: response =>
                    {
                        var acknowledgeRoute = string.IsNullOrEmpty(response?.Route) ? message.route : response.Route;
                        SendAcknowledge(message.id, acknowledgeRoute, true, response?.Data);
                    },
                    onError: error => SendAcknowledge(message.id, message.route, false, new { error })
                );
            }
            else
            {
                GLog.Warn($"[Bridge] No handler for route: {message.route}");
                SendAcknowledge(message.id, message.route, false, new { error = "No handler registered" });
            }
        }

        private void HandleAcknowledge(Message message)
        {
            if (_pendingRequests.Remove(message.id, out var pending))
            {
                if (message.ok)
                {
                    pending.onSuccess?.Invoke(message);
                }
                else
                {
                    pending.onError?.Invoke("ACK returned error");
                }
            }
            else
            {
                GLog.Debug($"[Bridge] Unknown ACK: {message.id}");
            }
        }

        private void HandleNotify(string route, Message message)
        {
            if (_messageHandlers.TryGetValue(route, out var handler))
            {
                handler.HandleNotify(message);
            }
            else
            {
                GLog.Debug($"[Bridge] No handler for NTY: {message.route}");
            }
        }

        private void SendRequestInternal(string route, object data = null,
            Action<Message> onSuccess = null,
            Action<string> onError = null,
            Action onTimeout = null,
            float timeoutSeconds = -1f)
        {
            if (timeoutSeconds < 0f)
            {
                timeoutSeconds = defaultTimeoutSeconds;
            }

            var message = CreateMessage(MessageType.REQ, MessageDirection.U2R, route, data, true);

            _pendingRequests[message.id] = new PendingRequest
            {
                requestId = message.id,
                sentTime = DateTime.UtcNow,
                timeoutSeconds = timeoutSeconds,
                onSuccess = onSuccess,
                onError = onError,
                onTimeout = onTimeout
            };

            SendMessageToReactInternal(message);
        }

        private void SendNotifyInternal(string route, object data = null)
        {
            var message = CreateMessage(MessageType.NTY, MessageDirection.U2R, route, data, true);
            SendMessageToReactInternal(message);
        }

        private void SendAcknowledge(string requestId, string route, bool success, object data)
        {
            var message = CreateMessage(MessageType.ACK, MessageDirection.U2R, route, data, success, requestId);
            SendMessageToReactInternal(message);
        }

        private void SendMessageToReactInternal(Message message)
        {
            try
            {
                var json = JsonConvert.SerializeObject(message);
                var route = string.IsNullOrWhiteSpace(message?.route) ? "Unknown" : message.route;
                var messageType = string.IsNullOrWhiteSpace(message?.type) ? "UNKNOWN" : message.type.ToUpperInvariant();
                Debug.Log($"[INTERFACE] U2R {route} {messageType} {json}");
                WebGLBridge.Send(json);
                OnMessageSent?.Invoke(message);
            }
            catch (Exception e)
            {
                GLog.Error($"[Bridge] Send failed: {e.Message}");
            }
        }

        private Message CreateMessage(MessageType type, MessageDirection direction, string route, object data, bool ok, string customId = null)
        {
            return new Message
            {
                ok = ok,
                type = type.ToString(),
                route = route,
                id = customId ?? GenerateMessageId(direction),
                data = data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            };
        }

        private string GenerateMessageId(MessageDirection direction)
        {
            var prefix = direction == MessageDirection.U2R ? "u2r" : "r2u";
            var uuid = Guid.NewGuid().ToString();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var currentId = _idIndex++;
            return $"{prefix}_{uuid}_{timestamp}_{currentId}";
        }

        private IEnumerator CheckTimeouts()
        {
            var wait = new WaitForSeconds(1f);
            while (true)
            {
                yield return wait;
                var currentTime = DateTime.UtcNow;
                var expiredRequests = _pendingRequests.Values.Where(req => (currentTime - req.sentTime).TotalSeconds > req.timeoutSeconds).ToList();
                foreach (var expired in expiredRequests)
                {
                    if (_pendingRequests.Remove(expired.requestId, out _))
                    {
                        expired.onTimeout?.Invoke();
                        GLog.Warn($"[Bridge] Request timeout: {expired.requestId}");
                    }
                }
            }
        }

        public T GetHandler<T>(string route) where T : class, IMessageHandler
        {
            return _messageHandlers.TryGetValue(route, out var handler) ? handler as T : null;
        }

        public bool IsConnected()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return WebGLBridge.IsReactBridgeReady() == 1;
#else
            return true;
#endif
        }

        public int GetPendingRequestCount() => _pendingRequests.Count;

        public void ClearAllPendingRequests()
        {
            var requests = _pendingRequests.Values.ToList();
            _pendingRequests.Clear();
            foreach (var request in requests)
            {
                request.onTimeout?.Invoke();
            }
        }

        public override void ClearSingleton()
        {
            _messageHandlers.Clear();
            _pendingRequests.Clear();
            StopAllCoroutines();
        }

        private sealed class BridgeSender : IBridgeSender
        {
            private readonly BridgeManager _manager;

            public BridgeSender(BridgeManager manager)
            {
                _manager = manager;
            }

            public void Request(string route, object data = null, Action<Message> onSuccess = null, Action<string> onError = null, Action onTimeout = null, float timeoutSeconds = -1f)
            {
                _manager.SendRequestInternal(route, data, onSuccess, onError, onTimeout, timeoutSeconds);
            }

            public void Notify(string route, object data = null)
            {
                _manager.SendNotifyInternal(route, data);
            }
        }
    }
}
