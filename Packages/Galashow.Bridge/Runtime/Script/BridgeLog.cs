using UnityEngine;

namespace Mimic.Bridge
{
    internal static class BridgeLog
    {
        public static void Info(string message) => Debug(message);
        public static void Warn(string message) => Debug(message);
        public static void Error(string message) => Debug(message);
        public static void Debug(string message)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log(message);
#endif
        }
    }
}
