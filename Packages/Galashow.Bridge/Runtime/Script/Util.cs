using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mimic.Bridge
{
    public static class Util
    {
        public static string ToBridgeError(
            string code,
            string message,
            bool retryable = false,
            object details = null)
        {
            var payload = new BridgeErrorPayload
            {
                code = string.IsNullOrWhiteSpace(code) ? "INTERNAL_ERROR" : code,
                message = string.IsNullOrWhiteSpace(message) ? "Unknown bridge error." : message,
                retryable = retryable,
                details = details
            };

            return JsonConvert.SerializeObject(payload);
        }

        public static bool TryParseBridgeError(string raw, out BridgeErrorPayload payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            try
            {
                var parsed = JsonConvert.DeserializeObject<BridgeErrorPayload>(raw);
                if (parsed == null ||
                    string.IsNullOrWhiteSpace(parsed.code) ||
                    string.IsNullOrWhiteSpace(parsed.message))
                {
                    return false;
                }

                payload = parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static (string routeName, string action) ParseRoute(string route)
        {
            if (string.IsNullOrWhiteSpace(route))
            {
                return (string.Empty, string.Empty);
            }

            var normalizedRoute = route.Trim();
            const string bridgeRoutePrefix = "BridgeManager_";
            if (normalizedRoute.StartsWith(bridgeRoutePrefix, StringComparison.Ordinal))
            {
                normalizedRoute = normalizedRoute[bridgeRoutePrefix.Length..];
            }

            var parts = normalizedRoute.Split('_', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return (string.Empty, string.Empty);
            }

            var index = 0;
            if (parts[index] is "R2U" or "U2R")
            {
                index++;
            }

            if (index >= parts.Length)
            {
                return (string.Empty, string.Empty);
            }

            var manager = parts[index];
            index++;

            if (index >= parts.Length)
            {
                return (manager, string.Empty);
            }

            var action = string.Join("_", parts[index..]);
            var suffixIndex = action.LastIndexOf('_');
            if (suffixIndex > 0)
            {
                var tail = action[(suffixIndex + 1)..];
                if (tail is "REQ" or "ACK" or "NTY")
                {
                    action = action[..suffixIndex];
                }
            }

            return (manager, action);
        }

        public static bool TryTo<T>(object raw, out T model, out string error)
        {
            try
            {
                if (raw is T typed)
                {
                    model = typed;
                    error = null;
                    return true;
                }

                var token = raw as JToken ?? JToken.FromObject(raw);
                model = token.ToObject<T>();
                error = null;
                return true;
            }
            catch (Exception e)
            {
                model = default;
                error = e.Message;
                return false;
            }
        }
    }
}
