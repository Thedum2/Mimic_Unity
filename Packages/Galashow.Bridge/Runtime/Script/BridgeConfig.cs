using System;

namespace Mimic.Bridge
{
    [Serializable]
    public enum MessageType
    {
        REQ,
        ACK,
        NTY
    }

    [Serializable]
    public enum MessageDirection
    {
        R2U,
        U2R
    }

    [Serializable]
    public class Message
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

    public sealed class AcknowledgeResponse
    {
        public string Route { get; }
        public object Data { get; }

        public AcknowledgeResponse(object data, string route = null)
        {
            Data = data;
            Route = route;
        }
    }

    [Serializable]
    public class PendingRequest
    {
        public string requestId;
        public string route;
        public DateTime sentTime;
        public float timeoutSeconds;
        public Action<Message> onSuccess;
        public Action<string> onError;
        public Action onTimeout;
    }
}
