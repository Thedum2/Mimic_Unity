using System;
using Newtonsoft.Json.Linq;

namespace Mimic.Bridge
{
    public static class Util
    {
        public static (string routeName, string action) ParseRoute(string route)
        {
            if (string.IsNullOrEmpty(route))
            {
                return (string.Empty, string.Empty);
            }

            var parts = route.Split('_', 2);
            return parts.Length >= 2 ? (parts[0], parts[1]) : (route, string.Empty);
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
