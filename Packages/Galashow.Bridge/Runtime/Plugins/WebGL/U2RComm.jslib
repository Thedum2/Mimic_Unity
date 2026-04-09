mergeInto(LibraryManager.library, {
  SendMessageToReact: function (jsonPtr) {
    try {
      var json = UTF8ToString(jsonPtr);
      if (typeof window !== "undefined") {
        if (typeof window.dispatchReactUnityEvent === "function") {
          window.dispatchReactUnityEvent("onUnityMessage", json);
        } else {
          window.__reactBridgeQueue = window.__reactBridgeQueue || [];
          window.__reactBridgeQueue.push({ name: "onUnityMessage", payload: json });
        }
      }
    } catch (e) {
      console.warn("[ReactBridge] SendMessageToReact failed:", e);
    }
  },

  IsReactBridgeReady: function () {
    try {
      return (typeof window !== "undefined" && typeof window.dispatchReactUnityEvent === "function") ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },

  InitializeReactBridge: function () {
    try {
      if (typeof window !== "undefined") {
        window.__reactBridgeQueue = window.__reactBridgeQueue || [];
      }
    } catch (e) {
      console.warn("[ReactBridge] InitializeReactBridge failed:", e);
    }
  },

  InitializeReactBridgeRuntime: function () {
    try {
      if (typeof window === "undefined") return;
      var q = window.__reactBridgeQueue;
      if (typeof window.dispatchReactUnityEvent === "function" && Array.isArray(q) && q.length > 0) {
        var buf = q.slice();
        q.length = 0;
        for (var i = 0; i < buf.length; i++) {
          var evt = buf[i];
          try { window.dispatchReactUnityEvent(evt.name, evt.payload); }
          catch (e) { console.warn("[ReactBridge] flush failed:", e); }
        }
      }
    } catch (e) {
      console.warn("[ReactBridge] InitializeReactBridgeRuntime failed:", e);
    }
  }
});
