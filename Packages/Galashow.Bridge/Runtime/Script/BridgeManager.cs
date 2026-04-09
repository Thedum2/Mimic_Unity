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
        private const string ReactEndpoint = "R";
        private const string UnityEndpoint = "U";
        [Header("Settings")]
        [SerializeField] private float defaultTimeoutSeconds = 5f;
        [SerializeField] private string bridgeGameObjectName = "BridgeManager";

        private readonly Dictionary<string, IMessageHandler> _messageHandlers = new();
        private readonly Dictionary<string, PendingRequest> _pendingRequests = new();
        private readonly Dictionary<string, Queue<PendingRequest>> _pendingRequestsByRoute = new();

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
            RegisterHandler(_mainHandler.LobbyChatHandler);
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
                GLog.Error($"[Bridge] Parse error: {e.Message}\nPayload: {jsonMessage}\n{e}");
            }
        }

        private void HandleIncomingMessage(Message message)
        {
            NormalizeBridgeDirection(message);
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
            var routeForResponse = NormalizeRouteForMessage(message.route);
            if (_messageHandlers.TryGetValue(route, out var handler))
            {
                handler.HandleRequest(
                    message,
                    onSuccess: response =>
                    {
                        var acknowledgeRoute = string.IsNullOrEmpty(response?.Route)
                            ? routeForResponse
                            : NormalizeRouteForMessage(response.Route);
                        SendAcknowledge(message.id, acknowledgeRoute, true, response?.Data);
                    },
                    onError: error => SendAcknowledge(message.id, routeForResponse, false, BuildErrorAcknowledgePayload(routeForResponse, error))
                );
            }
            else
            {
                GLog.Warn($"[Bridge] No handler for route: {message.route}");
                var error = Util.ToBridgeError("NOT_INITIALIZED", "No handler registered.", true, new { route = routeForResponse });
                SendAcknowledge(message.id, routeForResponse, false, BuildErrorAcknowledgePayload(routeForResponse, error));
            }
        }

        private void HandleAcknowledge(Message message)
        {
            var route = message.route;
            if (TryResolvePendingRequest(message.id, route, out var pending))
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
            // Accept lobby chat submit as R2U NTY as well, and respond via ACK.
            if (string.Equals(message.from, ReactEndpoint, StringComparison.Ordinal))
            {
                var (_, action) = Util.ParseRoute(message.route);
                if (string.Equals(route, "LobbyChatManager", StringComparison.Ordinal) &&
                    string.Equals(action, "SubmitMessage", StringComparison.Ordinal))
                {
                    HandleRequest(route, message);
                    return;
                }
            }

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
                route = NormalizeRouteForMessage(route),
                sentTime = DateTime.UtcNow,
                timeoutSeconds = timeoutSeconds,
                onSuccess = onSuccess,
                onError = onError,
                onTimeout = onTimeout
            };
            EnqueuePendingRequest(_pendingRequests[message.id]);

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

        private static ErrorAcknowledgePayload BuildErrorAcknowledgePayload(string route, string rawError)
        {
            if (Util.TryParseBridgeError(rawError, out var parsedError))
            {
                return new ErrorAcknowledgePayload(parsedError);
            }

            var normalizedError = string.IsNullOrWhiteSpace(rawError)
                ? "Unknown bridge error."
                : rawError.Trim();
            var lower = normalizedError.ToLowerInvariant();

            var code = "INTERNAL_ERROR";
            var retryable = true;

            if (lower.Contains("required") ||
                lower.Contains("invalid") ||
                lower.Contains("must be") ||
                lower.Contains("bad payload"))
            {
                code = "INVALID_ARGUMENT";
                retryable = false;
            }
            else if (lower.Contains("room is full"))
            {
                code = "ROOM_FULL";
                retryable = false;
            }
            else if (lower.Contains("room not found"))
            {
                code = "ROOM_NOT_FOUND";
                retryable = false;
            }
            else if (lower.Contains("runtimeready") || lower.Contains("runtime not ready"))
            {
                code = "RUNTIME_NOT_READY";
                retryable = true;
            }
            else if ((lower.Contains("no i") && lower.Contains("registered")) ||
                     lower.Contains("missing in lobbyscene") ||
                     lower.Contains("not initialized"))
            {
                code = "NOT_INITIALIZED";
                retryable = true;
            }
            else if (lower.Contains("timeout"))
            {
                code = "TIMEOUT";
                retryable = true;
            }
            else if ((route == "MatchManager_JoinRoomByInviteCode" || route == "MatchManager_RejoinRoom") &&
                     (lower.Contains("failed to join session") || lower.Contains("failed to rejoin session")))
            {
                code = "ROOM_NOT_FOUND";
                retryable = false;
            }

            return new ErrorAcknowledgePayload(new BridgeErrorPayload
            {
                code = code,
                message = normalizedError,
                retryable = retryable,
                details = new { route }
            });
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
            var normalizedRoute = NormalizeRouteForMessage(route);
            var (from, to) = GetEnvelopeDirection(direction);
            return new Message
            {
                ok = ok,
                type = type.ToString(),
                from = from,
                to = to,
                route = normalizedRoute,
                id = customId ?? GenerateMessageId(direction),
                data = data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        private string GenerateMessageId(MessageDirection direction)
        {
            return Guid.NewGuid().ToString();
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
                        RemovePendingRequestByRoute(expired);
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
            _pendingRequestsByRoute.Clear();
            foreach (var request in requests)
            {
                request.onTimeout?.Invoke();
            }
        }

        public override void ClearSingleton()
        {
            _messageHandlers.Clear();
            _pendingRequests.Clear();
            _pendingRequestsByRoute.Clear();
            StopAllCoroutines();
        }

        private void NormalizeBridgeDirection(Message message)
        {
            if (string.IsNullOrWhiteSpace(message.type))
            {
                return;
            }

            var type = message.type.ToUpperInvariant();
            var expectedFrom = type == "REQ" ? ReactEndpoint : type is "ACK" or "NTY" ? UnityEndpoint : string.Empty;
            var expectedTo = type == "REQ" ? UnityEndpoint : type is "ACK" or "NTY" ? ReactEndpoint : string.Empty;

            if (string.IsNullOrWhiteSpace(message.from))
            {
                message.from = expectedFrom;
            }

            if (string.IsNullOrWhiteSpace(message.to))
            {
                message.to = expectedTo;
            }

            if (!string.IsNullOrWhiteSpace(expectedFrom) &&
                !string.Equals(message.from, expectedFrom, StringComparison.Ordinal))
            {
                GLog.Warn($"[Bridge] Unexpected sender '{message.from}' for type '{type}'. Expected '{expectedFrom}'.");
            }
        }

        private bool TryResolvePendingRequest(string requestId, string route, out PendingRequest pending)
        {
            if (!string.IsNullOrWhiteSpace(requestId) && _pendingRequests.Remove(requestId, out pending))
            {
                RemovePendingRequestByRoute(pending);
                return true;
            }

            pending = TryDequeuePendingByRoute(route);
            return pending != null;
        }

        private PendingRequest TryDequeuePendingByRoute(string route)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                return null;
            }

            var normalizedRoute = NormalizeRouteForMessage(route);
            if (_pendingRequestsByRoute.TryGetValue(normalizedRoute, out var queue) == false || queue.Count == 0)
            {
                return null;
            }

            var pending = queue.Dequeue();
            if (pending is not null)
            {
                _pendingRequests.Remove(pending.requestId, out _);
            }

            if (queue.Count == 0)
            {
                _pendingRequestsByRoute.Remove(normalizedRoute);
            }

            return pending;
        }

        private void EnqueuePendingRequest(PendingRequest pending)
        {
            if (pending == null || string.IsNullOrWhiteSpace(pending.route))
            {
                return;
            }

            if (_pendingRequestsByRoute.TryGetValue(pending.route, out var queue))
            {
                queue.Enqueue(pending);
                return;
            }

            _pendingRequestsByRoute[pending.route] = new Queue<PendingRequest>(new[] { pending });
        }

        private void RemovePendingRequestByRoute(PendingRequest pending)
        {
            if (pending == null || string.IsNullOrWhiteSpace(pending.route))
            {
                return;
            }

            if (_pendingRequestsByRoute.TryGetValue(pending.route, out var queue) == false || queue.Count == 0)
            {
                return;
            }

            var normalizedRoute = NormalizeRouteForMessage(pending.route);
            var remain = queue.Where(request => request.requestId != pending.requestId).ToList();
            if (remain.Count == 0)
            {
                _pendingRequestsByRoute.Remove(normalizedRoute);
                return;
            }

            _pendingRequestsByRoute[normalizedRoute] = new Queue<PendingRequest>(remain);
        }

        private string NormalizeRouteForMessage(string route)
        {
            var (manager, action) = Util.ParseRoute(route);
            if (string.IsNullOrWhiteSpace(manager))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(action))
            {
                return manager;
            }

            return $"{manager}_{action}";
        }

        private static (string from, string to) GetEnvelopeDirection(MessageDirection direction)
        {
            return direction switch
            {
                MessageDirection.U2R => (UnityEndpoint, ReactEndpoint),
                MessageDirection.R2U => (ReactEndpoint, UnityEndpoint),
                _ => (string.Empty, string.Empty)
            };
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
