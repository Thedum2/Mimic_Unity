using System;
using System.Runtime.InteropServices;
using Mimic.Core;
using Newtonsoft.Json;

namespace Mimic.Bridge
{
    public static class WebGLBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] public static extern void SendMessageToReact(string jsonMessage);
        [DllImport("__Internal")] public static extern int IsReactBridgeReady();
        [DllImport("__Internal")] public static extern void InitializeReactBridge();
        [DllImport("__Internal")] public static extern void InitializeReactBridgeRuntime();
#else
        public static void SendMessageToReact(string jsonMessage) { }

        public static int IsReactBridgeReady() => 1;
        public static void InitializeReactBridge() { }
        public static void InitializeReactBridgeRuntime() { }
#endif

        public static void Init()
        {
            try
            {
                InitializeReactBridgeRuntime();
                InitializeReactBridge();
                GLog.Debug("[WebGLBridge] Initialization invoked");
            }
            catch (Exception e)
            {
                GLog.Warn($"[WebGLBridge] Init failed: {e}");
            }
        }

        public static void Send(Message message)
        {
            if (message == null)
            {
                GLog.Warn("[WebGLBridge] Cannot send null message to React");
                return;
            }

            Send(JsonConvert.SerializeObject(message));
        }

        public static void Send(string jsonMessage)
        {
            if (string.IsNullOrEmpty(jsonMessage))
            {
                GLog.Warn("[WebGLBridge] Cannot send empty message to React");
                return;
            }

            try
            {
                SendMessageToReact(jsonMessage);
            }
            catch (Exception e)
            {
                GLog.Error($"[WebGLBridge] Native send failed: {e}");
            }
        }
    }
}
