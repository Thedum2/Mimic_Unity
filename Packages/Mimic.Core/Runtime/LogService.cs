using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mimic.Core
{
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warn = 3,
        Error = 4,
        Fatal = 5,
        Off = 6
    }

    [Serializable]
    public readonly struct LogEvent
    {
        public readonly long Timestamp;
        public readonly LogLevel Level;
        public readonly string Tag;
        public readonly string Message;
        public readonly string StackTrace;
        public readonly string Exception;

        public LogEvent(long timestamp, LogLevel level, string tag, string message, string stackTrace, Exception exception)
        {
            Timestamp = timestamp;
            Level = level;
            Tag = tag ?? string.Empty;
            Message = message ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
            Exception = exception != null ? exception.GetType().Name + ": " + exception.Message : string.Empty;
        }

        public override string ToString()
        {
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(Timestamp).ToLocalTime().ToString("HH:mm:ss.fff");
            var head = $"[{dt}] [{Level}]" + (string.IsNullOrEmpty(Tag) ? string.Empty : $" [{Tag}]");
            if (!string.IsNullOrEmpty(Exception)) return $"{head} {Message}\nException: {Exception}\n{StackTrace}";
            if (!string.IsNullOrEmpty(StackTrace)) return $"{head} {Message}\n{StackTrace}";
            return $"{head} {Message}";
        }
    }

    public interface ILogSink
    {
        void Write(in LogEvent e, UnityEngine.Object context = null);
    }

    public sealed class UnityConsoleSink : ILogSink
    {
        public bool UseContextObject = true;

        public void Write(in LogEvent e, UnityEngine.Object context = null)
        {
            var msg = e.ToString();
            var ctx = UseContextObject ? context : null;
            switch (e.Level)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                case LogLevel.Info:
                    if (ctx) Debug.Log(msg, ctx); else Debug.Log(msg);
                    break;
                case LogLevel.Warn:
                    if (ctx) Debug.LogWarning(msg, ctx); else Debug.LogWarning(msg);
                    break;
                case LogLevel.Error:
                case LogLevel.Fatal:
                    if (ctx) Debug.LogError(msg, ctx); else Debug.LogError(msg);
                    break;
            }
        }
    }

    public sealed class MemoryRingSink : ILogSink
    {
        private readonly int capacity;
        private readonly LogEvent[] buffer;
        private int count;
        private int head;
        private readonly object sync = new();

        public MemoryRingSink(int capacity = 1024)
        {
            this.capacity = Mathf.Max(32, capacity);
            buffer = new LogEvent[this.capacity];
        }

        public void Write(in LogEvent e, UnityEngine.Object context = null)
        {
            lock (sync)
            {
                buffer[head] = e;
                head = (head + 1) % capacity;
                if (count < capacity) count++;
            }
        }

        public List<LogEvent> GetRecent(int max)
        {
            lock (sync)
            {
                var take = Mathf.Clamp(max, 0, count);
                var list = new List<LogEvent>(take);
                var index = (head - 1 + capacity) % capacity;
                for (var i = 0; i < take; i++)
                {
                    list.Add(buffer[index]);
                    index = (index - 1 + capacity) % capacity;
                }
                list.Reverse();
                return list;
            }
        }

        public string ExportAsText(int max = 1000)
        {
            return string.Join("\n", GetRecent(max));
        }
    }

    [DefaultExecutionOrder(-5002)]
    public class LogService : PersistentMonoSingleton<LogService>
    {
        [Header("Config")]
        [SerializeField] private LogLevel minimumLevel = LogLevel.Info;
        [SerializeField] private bool useUnityConsole = true;
        [SerializeField] private int memoryCapacity = 1024;

        private readonly List<ILogSink> sinks = new();
        private MemoryRingSink memorySink;

        protected override void OnInitializing()
        {
            base.OnInitializing();
            memorySink = new MemoryRingSink(memoryCapacity);
            sinks.Add(memorySink);
            if (useUnityConsole)
            {
                sinks.Add(new UnityConsoleSink());
            }
        }

        public void AddSink(ILogSink sink)
        {
            if (sink != null && !sinks.Contains(sink)) sinks.Add(sink);
        }

        public void RemoveSink(ILogSink sink)
        {
            if (sink != null) sinks.Remove(sink);
        }

        public void Log(LogLevel level, string message, string tag = null, UnityEngine.Object context = null, Exception ex = null, string stackTrace = null)
        {
            if (level < minimumLevel || minimumLevel == LogLevel.Off) return;
            var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var ev = new LogEvent(ts, level, tag, message, stackTrace, ex);
            foreach (var sink in sinks)
            {
                sink.Write(ev, context);
            }
        }

        public void Log(LogLevel level, Func<string> messageFactory, string tag = null, UnityEngine.Object context = null, Exception ex = null, string stackTrace = null)
        {
            if (level < minimumLevel || minimumLevel == LogLevel.Off) return;
            string msg = null;
            try { msg = messageFactory?.Invoke(); }
            catch (Exception mfEx) { msg = $"<messageFactory threw> {mfEx.Message}"; ex ??= mfEx; }
            Log(level, msg, tag, context, ex, stackTrace);
        }

        public IReadOnlyList<LogEvent> GetRecent(int max = 200) => memorySink.GetRecent(max);
        public string DumpText(int max = 1000) => memorySink.ExportAsText(max);
    }

    public static class GLog
    {
        private static string CallerToTag(string provided, [CallerMemberName] string member = null, [CallerFilePath] string file = null)
        {
            if (!string.IsNullOrEmpty(provided)) return provided;
            if (!string.IsNullOrEmpty(file))
            {
                try
                {
                    var name = System.IO.Path.GetFileNameWithoutExtension(file);
                    return string.IsNullOrEmpty(member) ? name : $"{name}.{member}";
                }
                catch { }
            }

            return member ?? string.Empty;
        }

        public static void Trace(string msg, string tag = null, UnityEngine.Object ctx = null) => LogService.Instance.Log(LogLevel.Trace, msg, CallerToTag(tag), ctx);
        public static void Debug(string msg, string tag = null, UnityEngine.Object ctx = null) => LogService.Instance.Log(LogLevel.Debug, msg, CallerToTag(tag), ctx);
        public static void Info(string msg, string tag = null, UnityEngine.Object ctx = null) => LogService.Instance.Log(LogLevel.Info, msg, CallerToTag(tag), ctx);
        public static void Warn(string msg, string tag = null, UnityEngine.Object ctx = null) => LogService.Instance.Log(LogLevel.Warn, msg, CallerToTag(tag), ctx);
        public static void Error(string msg, string tag = null, UnityEngine.Object ctx = null, Exception ex = null) => LogService.Instance.Log(LogLevel.Error, msg, CallerToTag(tag), ctx, ex);
        public static void Fatal(string msg, string tag = null, UnityEngine.Object ctx = null, Exception ex = null) => LogService.Instance.Log(LogLevel.Fatal, msg, CallerToTag(tag), ctx, ex);

        public static void Trace(Func<string> f, string tag = null, UnityEngine.Object ctx = null) => LogService.Instance.Log(LogLevel.Trace, f, CallerToTag(tag), ctx);
        public static void Debug(Func<string> f, string tag = null, UnityEngine.Object ctx = null) => LogService.Instance.Log(LogLevel.Debug, f, CallerToTag(tag), ctx);
        public static void Info(Func<string> f, string tag = null, UnityEngine.Object ctx = null) => LogService.Instance.Log(LogLevel.Info, f, CallerToTag(tag), ctx);
        public static void Warn(Func<string> f, string tag = null, UnityEngine.Object ctx = null) => LogService.Instance.Log(LogLevel.Warn, f, CallerToTag(tag), ctx);
        public static void Error(Func<string> f, string tag = null, UnityEngine.Object ctx = null, Exception ex = null) => LogService.Instance.Log(LogLevel.Error, f, CallerToTag(tag), ctx, ex);
        public static void Fatal(Func<string> f, string tag = null, UnityEngine.Object ctx = null, Exception ex = null) => LogService.Instance.Log(LogLevel.Fatal, f, CallerToTag(tag), ctx, ex);
    }
}
