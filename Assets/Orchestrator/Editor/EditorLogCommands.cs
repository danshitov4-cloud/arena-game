using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class EditorLogCommands
    {
        private sealed class Entry
        {
            public string type = "";       // "error" | "warning" | "log" | "exception"
            public string message = "";
            public string stackTrace = "";
            public string timeUtc = "";
        }

        private static readonly List<Entry> _buffer = new List<Entry>(1024);
        private static readonly object _lock = new object();
        private const int MaxBuffer = 2000;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        private static void OnLog(string message, string stackTrace, LogType logType)
        {
            string type = logType switch
            {
                LogType.Error     => "error",
                LogType.Assert    => "error",
                LogType.Exception => "exception",
                LogType.Warning   => "warning",
                _                 => "log"
            };

            var entry = new Entry
            {
                type       = type,
                message    = message ?? "",
                stackTrace = stackTrace ?? "",
                timeUtc    = DateTime.UtcNow.ToString("o")
            };

            lock (_lock)
            {
                _buffer.Add(entry);
                if (_buffer.Count > MaxBuffer)
                    _buffer.RemoveRange(0, _buffer.Count - MaxBuffer);
            }
        }

        // command: editor.log.read
        // args:
        // {
        //   "max": 50,               // default 50, max 500
        //   "type": "all",           // "all" | "error" | "warning" | "log" | "exception"
        //   "contains": "",          // optional substring filter on message
        //   "last": true,            // true = newest first (default), false = oldest first
        //   "clear": false           // clear buffer after reading
        // }
        public static object Read(JToken args)
        {
            var o = args as JObject;

            int max = (int?)o?["max"] ?? 50;
            if (max < 1)   max = 1;
            if (max > 500) max = 500;

            string typeFilter = ((string?)o?["type"] ?? "all").Trim().ToLowerInvariant();
            string contains   = ((string?)o?["contains"] ?? "").Trim();
            bool newestFirst  = (bool?)o?["last"] ?? true;
            bool clear        = (bool?)o?["clear"] ?? false;

            List<Entry> snapshot;
            lock (_lock)
            {
                snapshot = new List<Entry>(_buffer);
                if (clear) _buffer.Clear();
            }

            IEnumerable<Entry> q = snapshot;

            if (typeFilter != "all")
                q = q.Where(e => e.type == typeFilter);

            if (!string.IsNullOrWhiteSpace(contains))
                q = q.Where(e => e.message.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0);

            if (newestFirst)
                q = q.Reverse();

            var items = q.Take(max).Select(e => new
            {
                e.type,
                e.message,
                e.stackTrace,
                e.timeUtc
            }).ToArray();

            int totalErrors   = snapshot.Count(e => e.type is "error" or "exception");
            int totalWarnings = snapshot.Count(e => e.type == "warning");

            return new
            {
                ok = true,
                totalInBuffer = snapshot.Count,
                totalErrors,
                totalWarnings,
                cleared = clear,
                returned = items.Length,
                items
            };
        }
    }
}
