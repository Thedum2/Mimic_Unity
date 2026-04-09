namespace Mimic.Bridge
{
    public interface IBridgeSender
    {
        void Request(string route, object data = null,
            System.Action<Message> onSuccess = null,
            System.Action<string> onError = null,
            System.Action onTimeout = null,
            float timeoutSeconds = -1f);

        void Notify(string route, object data = null);
    }
}
