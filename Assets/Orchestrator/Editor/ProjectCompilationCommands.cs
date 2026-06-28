using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class ProjectCompilationCommands
    {
        // command: project.compilation.wait
        // args:
        // {
        //   "timeoutMs": 30000,        // default 30000
        //   "pollMs": 200,            // default 200
        //   "max": 200,               // default 200 (сколько сообщений вернуть)
        //   "includeWarnings": true,  // default true
        //   "clearBefore": false      // default false: если true Ч очищаем накопленные сообщени€ перед ожиданием
        // }
        public static object Wait(JToken args)
        {
            var o = args as JObject;

            int timeoutMs = (int?)o?["timeoutMs"] ?? 30000;
            if (timeoutMs < 100) timeoutMs = 100;
            if (timeoutMs > 600000) timeoutMs = 600000; // 10 минут max

            int pollMs = (int?)o?["pollMs"] ?? 200;
            if (pollMs < 50) pollMs = 50;
            if (pollMs > 2000) pollMs = 2000;

            int max = (int?)o?["max"] ?? 200;
            if (max < 1) max = 1;
            if (max > 5000) max = 5000;

            bool includeWarnings = (bool?)o?["includeWarnings"] ?? true;
            bool clearBefore = (bool?)o?["clearBefore"] ?? false;

            if (clearBefore)
            {
                _messages.Clear();
                _lastErrorCount = 0;
                _lastWarningCount = 0;
            }

            double start = EditorApplication.timeSinceStartup;

            // ¬ажно: чтобы Unity реально УтиковалаФ и компил€ци€ завершалась, ждЄм через Sleep + проверку флага.
            while (EditorApplication.isCompiling)
            {
                if ((EditorApplication.timeSinceStartup - start) * 1000.0 >= timeoutMs)
                {
                    // timeout Ч вернЄм статус + что уже есть
                    return new
                    {
                        ok = true,
                        timedOut = true,
                        waitedMs = (int)((EditorApplication.timeSinceStartup - start) * 1000.0),
                        status = Status(null),
                        errors = Errors(new JObject
                        {
                            ["max"] = max,
                            ["includeWarnings"] = includeWarnings
                        })
                    };
                }

                System.Threading.Thread.Sleep(pollMs);
            }

            // finished
            return new
            {
                ok = true,
                timedOut = false,
                waitedMs = (int)((EditorApplication.timeSinceStartup - start) * 1000.0),
                status = Status(null),
                errors = Errors(new JObject
                {
                    ["max"] = max,
                    ["includeWarnings"] = includeWarnings
                })
            };
        }

        private sealed class Msg
        {
            public string assembly = "";
            public string type = "";   // error|warning
            public string message = "";
            public string file = "";
            public int line;
            public int column;
            public string timeUtc = "";
        }

        private static readonly List<Msg> _messages = new List<Msg>(512);
        private static DateTime _lastEventUtc = DateTime.MinValue;
        private static int _lastErrorCount;
        private static int _lastWarningCount;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            _lastEventUtc = DateTime.UtcNow;

            // удалим старые сообщени€ по этой сборке (чтобы список не разрасталс€ одинаковыми)
            _messages.RemoveAll(m => string.Equals(m.assembly, assemblyPath, StringComparison.OrdinalIgnoreCase));

            int e = 0, w = 0;

            foreach (var m in messages)
            {
                if (m.type == CompilerMessageType.Error) e++;
                else if (m.type == CompilerMessageType.Warning) w++;

                _messages.Add(new Msg
                {
                    assembly = assemblyPath,
                    type = m.type == CompilerMessageType.Error ? "error" : "warning",
                    message = m.message ?? "",
                    file = m.file ?? "",
                    line = m.line,
                    column = m.column,
                    timeUtc = _lastEventUtc.ToString("o")
                });
            }

            // пересчЄт общих счЄтчиков
            _lastErrorCount = _messages.Count(x => x.type == "error");
            _lastWarningCount = _messages.Count(x => x.type == "warning");

            // safety: ограничим пам€ть
            if (_messages.Count > 5000)
                _messages.RemoveRange(0, _messages.Count - 5000);
        }

        // command: project.compilation.status
        // args: { }
        public static object Status(JToken args)
        {
            return new
            {
                ok = true,
                isCompiling = EditorApplication.isCompiling,
                lastEventUtc = _lastEventUtc == DateTime.MinValue ? null : _lastEventUtc.ToString("o"),
                errors = _lastErrorCount,
                warnings = _lastWarningCount,
                messageCount = _messages.Count
            };
        }

        // command: project.compilation.errors
        // args:
        // {
        //   "max": 200,            // default 200
        //   "includeWarnings": false,  // default false
        //   "assemblyContains": "Assembly-CSharp", // optional filter
        //   "fileContains": "Assets/",             // optional filter
        //   "clear": false         // default false: если true Ч очистить сохранЄнные сообщени€ и вернуть пусто
        // }
        public static object Errors(JToken args)
        {
            var o = args as JObject;
            int max = (int?)o?["max"] ?? 200;
            if (max < 1) max = 1;
            if (max > 5000) max = 5000;

            bool includeWarnings = (bool?)o?["includeWarnings"] ?? false;

            string assemblyContains = ((string?)o?["assemblyContains"] ?? "").Trim();
            string fileContains = ((string?)o?["fileContains"] ?? "").Trim();

            bool clear = (bool?)o?["clear"] ?? false;
            if (clear)
            {
                _messages.Clear();
                _lastErrorCount = 0;
                _lastWarningCount = 0;
                return new { ok = true, cleared = true, items = Array.Empty<object>() };
            }

            IEnumerable<Msg> q = _messages;

            q = includeWarnings ? q : q.Where(x => x.type == "error");

            if (!string.IsNullOrWhiteSpace(assemblyContains))
                q = q.Where(x => x.assembly.IndexOf(assemblyContains, StringComparison.OrdinalIgnoreCase) >= 0);

            if (!string.IsNullOrWhiteSpace(fileContains))
                q = q.Where(x => x.file.IndexOf(fileContains, StringComparison.OrdinalIgnoreCase) >= 0);

            var items = q
                .OrderByDescending(x => x.timeUtc)
                .Take(max)
                .Select(x => new
                {
                    x.type,
                    x.message,
                    x.file,
                    x.line,
                    x.column,
                    x.assembly,
                    x.timeUtc
                })
                .ToArray();

            return new
            {
                ok = true,
                isCompiling = EditorApplication.isCompiling,
                errors = _lastErrorCount,
                warnings = _lastWarningCount,
                returned = items.Length,
                items
            };
        }
    }
}

