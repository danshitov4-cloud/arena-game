using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Plastic.Newtonsoft.Json.Linq;
using System.IO;
using Unity.Plastic.Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using System.Text;

namespace Orchestrator.Editor
{

    public static class HelpSchema
    {
        private const string DefaultMarkdownPath = "Assets/Orchestrator/ORCHESTRATOR_SPEC.md";

        public static object ExportMarkdown(JToken args)
        {
            EnsureInited();

            var o = args as JObject;

            // куда сохранять
            string path = ((string?)o?["path"] ?? DefaultMarkdownPath).Trim();
            bool includeExamples = (bool?)o?["includeExamples"] ?? true;
            bool includeOnlyDescribed = (bool?)o?["onlyDescribed"] ?? true;
            string prefix = ((string?)o?["prefix"] ?? "").Trim(); // можно "scene." или "materials."
            bool groupByPrefix = (bool?)o?["groupByPrefix"] ?? true;

            // список команд
            var dispatcher = BuildDispatcherCommands().Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

            IEnumerable<string> list;
            if (includeOnlyDescribed)
                list = _docs.Keys;
            else
                list = dispatcher;

            if (!string.IsNullOrWhiteSpace(prefix))
                list = list.Where(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            var commands = list
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            // диагностика missing/extra (в документ тоже можно вставить)
            var described = _docs.Keys.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var missingInHelp = dispatcher.Except(described, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
            var extraInHelp = described.Except(dispatcher, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();

            var sb = new StringBuilder(64 * 1024);

            sb.AppendLine("# Unity Orchestrator Spec");
            sb.AppendLine();
            sb.AppendLine($"- SchemaVersion: `{SchemaVersion}`");
            sb.AppendLine($"- GeneratedAtUtc: `{DateTime.UtcNow:O}`");
            sb.AppendLine();

            sb.AppendLine("## Endpoint");
            sb.AppendLine("- POST `http://127.0.0.1:5137/command`");
            sb.AppendLine("- JSON body: `{ id, command, args, dryRun }`");
            sb.AppendLine();

            sb.AppendLine("## Notes");
            sb.AppendLine("- `dryRun=true` where possible to preview changes.");
            sb.AppendLine("- Use snapshots/workflows for rollback.");
            sb.AppendLine();

            sb.AppendLine("## Coverage report");
            sb.AppendLine($"- Dispatcher commands: **{dispatcher.Length}**");
            sb.AppendLine($"- Described commands: **{described.Length}**");
            sb.AppendLine($"- Missing in help: **{missingInHelp.Length}**");
            sb.AppendLine($"- Extra in help: **{extraInHelp.Length}**");
            sb.AppendLine();

            if (missingInHelp.Length > 0)
            {
                sb.AppendLine("### Missing in help");
                foreach (var c in missingInHelp) sb.AppendLine($"- `{c}`");
                sb.AppendLine();
            }

            if (extraInHelp.Length > 0)
            {
                sb.AppendLine("### Extra in help (not in dispatcher list)");
                foreach (var c in extraInHelp) sb.AppendLine($"- `{c}`");
                sb.AppendLine();
            }

            // Группировка по префиксу (scene./materials./help./etc.)
            if (groupByPrefix)
            {
                var groups = commands.GroupBy(GetTopGroup, StringComparer.OrdinalIgnoreCase)
                                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

                foreach (var g in groups)
                {
                    sb.AppendLine($"## {g.Key}");
                    sb.AppendLine();

                    foreach (var cmd in g)
                        AppendCommandSection(sb, cmd, includeExamples);

                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine("## Commands");
                sb.AppendLine();
                foreach (var cmd in commands)
                    AppendCommandSection(sb, cmd, includeExamples);
            }

            // запись файла
            EnsureDirForFile(path);
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();

            return new
            {
                ok = true,
                saved = true,
                path,
                bytes = sb.Length,
                commandCount = commands.Length,
                includeExamples,
                onlyDescribed = includeOnlyDescribed,
                prefix = string.IsNullOrWhiteSpace(prefix) ? null : prefix
            };

            static string GetTopGroup(string cmd)
            {
                // "scene.batch.xxx" -> "scene.batch"
                // "scene.xxx" -> "scene"
                // "materials.snapshot.xxx" -> "materials.snapshot"
                var parts = cmd.Split('.');
                if (parts.Length >= 2) return parts[0] + "." + parts[1];
                return parts[0];
            }

            void AppendCommandSection(StringBuilder s, string cmd, bool withExamples)
            {
                if (!_docs.TryGetValue(cmd, out var d))
                {
                    s.AppendLine($"### `{cmd}`");
                    s.AppendLine();
                    s.AppendLine("_No description available in HelpSchema._");
                    s.AppendLine();
                    return;
                }

                s.AppendLine($"### `{d.command}`");
                s.AppendLine();
                if (!string.IsNullOrWhiteSpace(d.summary))
                    s.AppendLine(d.summary);
                s.AppendLine();
                s.AppendLine($"- **Risk:** `{d.risk}`");
                if (!string.IsNullOrWhiteSpace(d.owner))
                    s.AppendLine($"- **Owner:** `{d.owner}`");
                s.AppendLine();

                // args
                s.AppendLine("#### Args");
                if (d.args == null || d.args.Count == 0)
                {
                    s.AppendLine("- (none)");
                }
                else
                {
                    foreach (var kv in d.args.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        // выводим компактно как JSON для читаемости
                        string argJson;
                        try
                        {
                            argJson = Unity.Plastic.Newtonsoft.Json.JsonConvert.SerializeObject(kv.Value);
                        }
                        catch
                        {
                            argJson = kv.Value?.ToString() ?? "";
                        }
                        s.AppendLine($"- `{kv.Key}`: `{argJson}`");
                    }
                }
                s.AppendLine();

                // example
                if (withExamples && !string.IsNullOrWhiteSpace(d.exampleJson))
                {
                    s.AppendLine("#### Example");
                    s.AppendLine("```json");
                    s.AppendLine(d.exampleJson.Trim());
                    s.AppendLine("```");
                    s.AppendLine();
                }
            }
        }

        // HelpSchema.cs — замените ваш текущий Dump на этот
        // Поддерживает:
        // - prefix: "scene." / "materials."
        // - command: "scene.batch.offsetTransform" (только одна команда)
        // - keysOnly: true (только список команд)
        // - onlyDescribed: true/false
        // - includeSkeleton: true/false
        // - includeExamples: true/false
        // - maxCommands: ограничить количество (по умолчанию 200)
        private const string DefaultHelpPath = "ProjectSettings/OrchestratorHelpSchema.json";

        public static object Open(JToken args)
        {
            var o = args as JObject;

            string path = ((string?)o?["path"] ?? DefaultHelpPath).Trim();
            string mode = ((string?)o?["mode"] ?? "reveal").Trim().ToLowerInvariant();
            // mode: "reveal" (по умолчанию) | "openurl" | "folder"

            // Нормализуем относительный путь в абсолютный (Unity любит абсолютный для reveal)
            string abs = Path.GetFullPath(path);

            if (mode == "folder")
            {
                string dir = Directory.Exists(abs) ? abs : Path.GetDirectoryName(abs);
                if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                    return new { ok = false, error = $"Folder not found for path: {abs}", path = abs };

                EditorUtility.RevealInFinder(dir);
                return new { ok = true, action = "reveal-folder", path = dir };
            }

            if (!File.Exists(abs))
                return new { ok = false, error = $"File not found: {abs}", path = abs };

            if (mode == "openurl")
            {
                // иногда откроет файл в дефолтном редакторе
                Application.OpenURL("file://" + abs.Replace("\\", "/"));
                return new { ok = true, action = "openurl", path = abs };
            }

            // mode == "reveal" (самый надёжный)
            EditorUtility.RevealInFinder(abs);
            return new { ok = true, action = "reveal", path = abs };
        }


        public static object Save(JToken args)
        {
            EnsureInited();

            var o = args as JObject;

            // Параметры сохранения
            string path = ((string?)o?["path"] ?? DefaultHelpPath).Trim();
            bool pretty = (bool?)o?["pretty"] ?? true;

            // Какие данные дампить (по умолчанию: всё)
            bool onlyDescribed = (bool?)o?["onlyDescribed"] ?? false;
            bool includeSkeleton = (bool?)o?["includeSkeleton"] ?? true;
            bool includeExamples = (bool?)o?["includeExamples"] ?? true;

            // Собираем объект дампа (тот же, что help.dump)
            var dumpObj = Dump(new JObject
            {
                ["onlyDescribed"] = onlyDescribed,
                ["includeSkeleton"] = includeSkeleton,
                ["includeExamples"] = includeExamples,
                ["maxCommands"] = (int?)o?["maxCommands"] ?? 5000
            });

            // Превращаем в JSON строку
            var formatting = pretty ? Formatting.Indented : Formatting.None;
            string json = JsonConvert.SerializeObject(dumpObj, formatting);

            // Гарантируем, что директория существует
            EnsureDirForFile(path);

            File.WriteAllText(path, json);

            // Обновим ассет-базу (на всякий)
            AssetDatabase.Refresh();

            return new
            {
                ok = true,
                saved = true,
                path,
                bytes = json.Length,
                onlyDescribed,
                includeSkeleton,
                includeExamples
            };
        }

        public static object Load(JToken args)
        {
            var o = args as JObject;
            string path = ((string?)o?["path"] ?? DefaultHelpPath).Trim();

            if (!File.Exists(path))
                return new { ok = true, loaded = false, path, reason = "file not found" };

            string json = File.ReadAllText(path);

            // Попробуем вытащить минимум полей, не падая
            try
            {
                var root = JObject.Parse(json);

                string schemaVersion = (string?)root["schemaVersion"] ?? "";
                int dispatcherCount = (int?)root["dispatcherCount"] ?? -1;
                int describedCount = (int?)root["describedCount"] ?? -1;

                // commands может быть map-объектом; считаем количество ключей
                int commandCount = 0;
                if (root["commands"] is JObject cmdsObj)
                    commandCount = cmdsObj.Properties().Count();

                return new
                {
                    ok = true,
                    loaded = true,
                    path,
                    schemaVersion,
                    dispatcherCount,
                    describedCount,
                    commandCount
                };
            }
            catch (Exception ex)
            {
                // файл есть, но формат неожиданный
                return new
                {
                    ok = false,
                    loaded = false,
                    path,
                    error = ex.GetType().Name + ": " + ex.Message
                };
            }
        }

        // helper
        private static void EnsureDirForFile(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        public static object Dump(JToken args)
        {
            EnsureInited();

            var o = args as JObject;

            bool onlyDescribed = (bool?)o?["onlyDescribed"] ?? false;
            bool includeSkeleton = (bool?)o?["includeSkeleton"] ?? false;
            bool includeExamples = (bool?)o?["includeExamples"] ?? true;

            string prefix = ((string?)o?["prefix"] ?? "").Trim();
            string oneCmd = ((string?)o?["command"] ?? "").Trim();

            bool keysOnly = (bool?)o?["keysOnly"] ?? false;

            int maxCommands = (int?)o?["maxCommands"] ?? 200;
            if (maxCommands < 1) maxCommands = 1;
            if (maxCommands > 5000) maxCommands = 5000;

            var dispatcher = BuildDispatcherCommands().Distinct().OrderBy(x => x).ToArray();
            var described = _docs.Keys.Distinct().OrderBy(x => x).ToArray();

            var missing = dispatcher.Except(described).OrderBy(x => x).ToArray();
            var extra = described.Except(dispatcher).OrderBy(x => x).ToArray();

            // базовый список команд для выдачи
            IEnumerable<string> list = onlyDescribed ? described : dispatcher;

            // фильтры
            if (!string.IsNullOrEmpty(prefix))
                list = list.Where(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(oneCmd))
                list = list.Where(c => string.Equals(c, oneCmd, StringComparison.OrdinalIgnoreCase));

            var listArr = list
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Take(maxCommands)
                .ToArray();

            if (keysOnly)
            {
                return new
                {
                    ok = true,
                    schemaVersion = SchemaVersion,
                    keysOnly = true,
                    onlyDescribed,
                    prefix = string.IsNullOrEmpty(prefix) ? null : prefix,
                    command = string.IsNullOrEmpty(oneCmd) ? null : oneCmd,
                    count = listArr.Length,
                    commands = listArr
                };
            }

            // собрать map command->doc (или skeleton)
            var map = new Dictionary<string, object>(StringComparer.Ordinal);

            foreach (var cmd in listArr)
            {
                if (_docs.TryGetValue(cmd, out var d))
                {
                    map[cmd] = new
                    {
                        summary = d.summary,
                        risk = d.risk,
                        owner = d.owner,
                        args = d.args,
                        example = includeExamples && !string.IsNullOrWhiteSpace(d.exampleJson) ? d.exampleJson : null
                    };
                }
                else if (includeSkeleton)
                {
                    map[cmd] = new
                    {
                        summary = "",
                        risk = "info",
                        owner = "",
                        args = new Dictionary<string, object>(),
                        example = (string?)null
                    };
                }
                // иначе — пропускаем неописанные
            }

            return new
            {
                ok = true,
                schemaVersion = SchemaVersion,

                // параметры выдачи
                onlyDescribed,
                includeSkeleton,
                includeExamples,
                prefix = string.IsNullOrEmpty(prefix) ? null : prefix,
                command = string.IsNullOrEmpty(oneCmd) ? null : oneCmd,
                maxCommands,

                // диагностика
                dispatcherCount = dispatcher.Length,
                describedCount = described.Length,
                missingInHelpCount = missing.Length,
                extraInHelpCount = extra.Length,
                missingInHelp = missing,
                extraInHelp = extra,

                // payload
                count = map.Count,
                commands = map
            };
        }


        public const string SchemaVersion = "0.2.0";

        // --- Внутреннее описание команды ---
        private sealed class CommandDoc
        {
            public string command = "";
            public string summary = "";
            public string risk = "info";       // info|risky
            public string owner = "";          // имя класса-исполнителя
            public Dictionary<string, object> args = new(); // schema args
            public string exampleJson = "";    // optional
        }

        // Реестр описаний (только help-описания)
        private static readonly Dictionary<string, CommandDoc> _docs =
            new Dictionary<string, CommandDoc>(StringComparer.Ordinal);

        private static bool _inited;

        // ---------- ПУБЛИЧНЫЕ API ДЛЯ /help.* ----------

        // help.commands
        public static object GetCommands(JToken args)
        {
            EnsureInited();

            var all = BuildDispatcherCommands().Distinct().OrderBy(x => x).ToArray();

            return new
            {
                ok = true,
                schemaVersion = SchemaVersion,
                commands = all
            };
        }

        // help.command  args: { "command":"..." }
        public static object GetCommand(JToken args)
        {
            EnsureInited();

            var o = AsObject(args);
            string cmd = ((string?)o?["command"] ?? "").Trim();

            if (string.IsNullOrWhiteSpace(cmd))
                return new { ok = false, schemaVersion = SchemaVersion, error = "args.command is required" };

            if (_docs.TryGetValue(cmd, out var d))
            {
                return new
                {
                    ok = true,
                    schemaVersion = SchemaVersion,
                    command = d.command,
                    summary = d.summary,
                    risk = d.risk,
                    owner = d.owner,
                    args = d.args,
                    example = string.IsNullOrWhiteSpace(d.exampleJson) ? null : d.exampleJson
                };
            }

            return new
            {
                ok = false,
                schemaVersion = SchemaVersion,
                error = $"Command not described in help: {cmd}",
                knownDescribed = _docs.Keys.OrderBy(x => x).ToArray()
            };
        }

        // help.schema  args: { "includeSkeleton": true }
        public static object GetSchema(JToken args)
        {
            EnsureInited();

            var o = AsObject(args);
            bool includeSkeleton = (bool?)o?["includeSkeleton"] ?? false;

            var dispatcher = BuildDispatcherCommands().Distinct().OrderBy(x => x).ToArray();
            var described = _docs.Keys.Distinct().OrderBy(x => x).ToArray();

            var missing = dispatcher.Except(described).OrderBy(x => x).ToArray();
            var extra = described.Except(dispatcher).OrderBy(x => x).ToArray();

            Dictionary<string, object> skeleton = null;
            if (includeSkeleton)
            {
                skeleton = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var cmd in missing)
                {
                    skeleton[cmd] = new
                    {
                        summary = "",
                        risk = "info",
                        owner = "",
                        args = new Dictionary<string, object>()
                    };
                }
            }

            return new
            {
                ok = true,
                schemaVersion = SchemaVersion,
                dispatcherCount = dispatcher.Length,
                describedCount = described.Length,
                missingInHelpCount = missing.Length,
                extraInHelpCount = extra.Length,
                missingInHelp = missing,
                extraInHelp = extra,
                skeleton = skeleton
            };
        }

        // ---------- HELPERS ----------

        private static JObject AsObject(JToken token)
        {
            // args может быть: null, JValue, JObject, JArray...
            // нам нужен JObject (или null)
            return token as JObject;
        }

        private static void Describe(
            string command,
            string summary,
            string risk,
            string owner,
            Dictionary<string, object> args,
            string exampleJson = "")
        {
            _docs[command] = new CommandDoc
            {
                command = command ?? "",
                summary = summary ?? "",
                risk = string.IsNullOrWhiteSpace(risk) ? "info" : risk,
                owner = owner ?? "",
                args = args ?? new Dictionary<string, object>(),
                exampleJson = exampleJson ?? ""
            };
        }

        // Маленький DSL для аргументов
        private static class Arg
        {
            public static object String(bool required = false, bool optional = false, string @default = null, string[] allowed = null, string example = null)
                => new
                {
                    type = "string",
                    required = required && !optional,
                    optional = optional || !required,
                    @default,
                    allowed,
                    example
                };

            public static object Int(bool required = false, bool optional = false, int? @default = null, int? min = null, int? max = null, string example = null)
                => new
                {
                    type = "int",
                    required = required && !optional,
                    optional = optional || !required,
                    @default,
                    min,
                    max,
                    example
                };

            public static object Float(bool required = false, bool optional = false, float? @default = null, float? min = null, float? max = null, string example = null)
                => new
                {
                    type = "float",
                    required = required && !optional,
                    optional = optional || !required,
                    @default,
                    min,
                    max,
                    example
                };

            public static object Bool(bool required = false, bool optional = false, bool? @default = null, string example = null)
                => new
                {
                    type = "bool",
                    required = required && !optional,
                    optional = optional || !required,
                    @default,
                    example
                };

            public static object Object(bool required = false, bool optional = false, string example = null)
                => new
                {
                    type = "object",
                    required = required && !optional,
                    optional = optional || !required,
                    example
                };

            public static object Vec3(bool required = false, bool optional = false, object @default = null, string example = null)
                => new
                {
                    type = "vec3",
                    required = required && !optional,
                    optional = optional || !required,
                    @default,
                    example
                };
        }

        // ---------- INIT: тут добавляем описания команд ----------

        private static void EnsureInited()
        {
            if (_inited) return;
            _inited = true;


            // -------------------- scene.query --------------------
            Describe(
                "scene.query",
                summary: "Finds GameObjects by query filters (name/tag/layer/component). Returns list (truncated by max).",
                risk: "info",
                owner: "SceneQuery",
                args: new Dictionary<string, object>
                {
                    ["nameContains"] = Arg.String(optional: true, example: "Building"),
                    ["caseSensitive"] = Arg.Bool(optional: true, @default: false),
                    ["tag"] = Arg.String(optional: true, example: "Player"),
                    ["layer"] = Arg.Int(optional: true, example: "0"),
                    ["hasComponent"] = Arg.String(optional: true, example: "Rigidbody2D / UnityEngine.Camera / Light2D"),
                    ["includeInactive"] = Arg.Bool(optional: true, @default: true),
                    ["max"] = Arg.Int(optional: true, @default: 200, min: 1, max: 5000),
                }
            );

            // -------------------- scene.selectByQuery --------------------
            Describe(
                "scene.selectByQuery",
                summary: "Selects objects found by scene.query (Editor selection).",
                risk: "risky",
                owner: "SceneQuery",
                args: new Dictionary<string, object>
                {
                    ["nameContains"] = Arg.String(optional: true, example: "Building"),
                    ["caseSensitive"] = Arg.Bool(optional: true, @default: false),
                    ["tag"] = Arg.String(optional: true, example: "Player"),
                    ["layer"] = Arg.Int(optional: true, example: "0"),
                    ["hasComponent"] = Arg.String(optional: true, example: "Rigidbody2D"),
                    ["includeInactive"] = Arg.Bool(optional: true, @default: true),
                    ["max"] = Arg.Int(optional: true, @default: 200, min: 1, max: 5000),
                    ["ping"] = Arg.Bool(optional: true, @default: true, example: "true"),
                }
            );

            // -------------------- scene.snapshot.take --------------------
            Describe(
                "scene.snapshot.take",
                summary: "Takes a scene snapshot (stores enabled state / tracked stuff inside orchestrator snapshot system).",
                risk: "risky",
                owner: "SnapshotCommands",
                args: new Dictionary<string, object>
                {
                    ["name"] = Arg.String(optional: true, example: "before-opt"),
                    // если у тебя есть includeTypes — оставляем как object, чтобы не ломать схему
                    ["includeTypes"] = Arg.Object(optional: true, example: "[\"SortByY\",\"BuildingView\"]"),
                    ["dryRun"] = Arg.Bool(optional: true, @default: false),
                }
            );

            // -------------------- scene.snapshot.restore --------------------
            Describe(
                "scene.snapshot.restore",
                summary: "Restores scene snapshot by id.",
                risk: "risky",
                owner: "SnapshotCommands",
                args: new Dictionary<string, object>
                {
                    ["id"] = Arg.String(required: true, example: "f579d549714c49a6b28c1a17ce4fcefb"),
                    ["dryRun"] = Arg.Bool(optional: true, @default: false),
                }
            );

            // -------------------- scene.snapshot.list --------------------
            Describe(
                "scene.snapshot.list",
                summary: "Lists saved scene snapshots (most recent first).",
                risk: "info",
                owner: "SnapshotCommands",
                args: new Dictionary<string, object>
                {
                    ["max"] = Arg.Int(optional: true, @default: 50, min: 1, max: 5000),
                }
            );

            // -------------------- scene.batch.setComponentProperty --------------------
            Describe(
                "scene.batch.setComponentProperty",
                summary: "Sets one component member/property for all objects matching query.",
                risk: "risky",
                owner: "SceneBatchProps",
                args: new Dictionary<string, object>
                {
                    ["query"] = Arg.Object(required: true, example: "{nameContains:\"Building\", max:500, includeInactive:true}"),
                    ["componentType"] = Arg.String(required: true, example: "UnityEngine.Camera / Rigidbody / UnityEngine.Rendering.Universal.Light2D"),
                    ["member"] = Arg.String(required: true, example: "orthographicSize / intensity / isKinematic"),
                    ["value"] = Arg.Object(required: true, example: "8  (or true/false or {x:0,y:1,z:0})"),
                    ["dryRun"] = Arg.Bool(optional: true, @default: false),
                }
            );

            // -------------------- scene.batch.setComponentProperties --------------------
            Describe(
                "scene.batch.setComponentProperties",
                summary: "Sets multiple members at once (set:{...}) for all objects matching query.",
                risk: "risky",
                owner: "SceneBatchProps",
                args: new Dictionary<string, object>
                {
                    ["query"] = Arg.Object(required: true, example: "{hasComponent:\"Light2D\", max:2000, includeInactive:true}"),
                    ["componentType"] = Arg.String(required: true, example: "UnityEngine.Rendering.Universal.Light2D"),
                    ["set"] = Arg.Object(required: true, example: "{intensity:3.0, color:{r:1,g:0.8,b:0.6,a:1}}"),
                    ["dryRun"] = Arg.Bool(optional: true, @default: false),
                }
            );

            // -------------------- scene.batch.diffComponentProperties --------------------
            Describe(
                "scene.batch.diffComponentProperties",
                summary: "Checks which objects differ from desired values (set:{...}). Returns diff stats + samples.",
                risk: "info",
                owner: "SceneBatchProps",
                args: new Dictionary<string, object>
                {
                    ["query"] = Arg.Object(required: true, example: "{nameContains:\"Building\", max:500, includeInactive:true}"),
                    ["componentType"] = Arg.String(required: true, example: "Rigidbody"),
                    ["set"] = Arg.Object(required: true, example: "{isKinematic:true, useGravity:false}"),
                    ["sampleLimit"] = Arg.Int(optional: true, @default: 20, min: 0, max: 200),
                    ["dryRun"] = Arg.Bool(optional: true, @default: false),
                }
            );

            // -------------------- scene.batch.applyIfDiffComponentProperties --------------------
            Describe(
                "scene.batch.applyIfDiffComponentProperties",
                summary: "Runs diff; if differences exist, applies set:{...} (optionally only when apply=true).",
                risk: "risky",
                owner: "SceneBatchProps",
                args: new Dictionary<string, object>
                {
                    ["query"] = Arg.Object(required: true, example: "{nameContains:\"Building\", max:500, includeInactive:true}"),
                    ["componentType"] = Arg.String(required: true, example: "Rigidbody"),
                    ["set"] = Arg.Object(required: true, example: "{isKinematic:true, useGravity:false}"),
                    ["sampleLimit"] = Arg.Int(optional: true, @default: 20, min: 0, max: 200),
                    ["apply"] = Arg.Bool(optional: true, @default: true),
                    ["dryRun"] = Arg.Bool(optional: true, @default: false),
                }
            );

            // -------------------- scene.batch.snapToGrid --------------------
            Describe(
                "scene.batch.snapToGrid",
                summary: "Snaps objects (from query) to grid (position rounding).",
                risk: "risky",
                owner: "SceneBatchTransforms",
                args: new Dictionary<string, object>
                {
                    ["query"] = Arg.Object(required: true, example: "{nameContains:\"Building\", max:500, includeInactive:true}"),
                    // у разных реализаций могут быть разные поля — оставляем универсально
                    ["gridSize"] = Arg.Vec3(optional: true, @default: new { x = 1, y = 1, z = 1 }, example: "{x:1,y:1,z:1}"),
                    ["origin"] = Arg.Vec3(optional: true, @default: new { x = 0, y = 0, z = 0 }),
                    ["space"] = Arg.String(optional: true, @default: "world", allowed: new[] { "world", "local" }),
                    ["dryRun"] = Arg.Bool(optional: true, @default: false),
                }
            );

            // -------------------- materials.snapshot.take --------------------
            Describe(
                "materials.snapshot.take",
                summary: "Captures material properties for top-used materials in scoped renderers (by nameContains).",
                risk: "risky",
                owner: "MaterialSnapshotCommands",
                args: new Dictionary<string, object>
                {
                    ["name"] = Arg.String(optional: true, example: "mat-before-red"),
                    ["nameContains"] = Arg.String(required: true, example: "Building"),
                    ["top"] = Arg.Int(optional: true, @default: 3, min: 1, max: 200),
                    ["useShared"] = Arg.Bool(optional: true, @default: true),
                    ["properties"] = Arg.Object(optional: true, example: "[\"_Color\",\"_BaseColor\",\"_EmissionColor\",\"_ZWrite\"]"),
                    ["includeAllColorAliases"] = Arg.Bool(optional: true, @default: true),
                    ["dryRun"] = Arg.Bool(optional: true, @default: false),
                }
            );

            // -------------------- materials.snapshot.restore --------------------
            Describe(
                "materials.snapshot.restore",
                summary: "Restores material properties from material snapshot by id.",
                risk: "risky",
                owner: "MaterialSnapshotCommands",
                args: new Dictionary<string, object>
                {
                    ["id"] = Arg.String(required: true, example: "8db038d7e9e744359088d87345f7b7c7"),
                    ["dryRun"] = Arg.Bool(optional: true, @default: false),
                }
            ); 

            // --- help.* (чтобы они не попадали в missingInHelp) ---
            Describe("help.commands",
                summary: "Lists all commands available in dispatcher.",
                risk: "info",
                owner: "HelpSchema",
                args: new Dictionary<string, object>());

            Describe("help.command",
                summary: "Returns schema for a single command.",
                risk: "info",
                owner: "HelpSchema",
                args: new Dictionary<string, object>
                {
                    ["command"] = Arg.String(required: true, example: "scene.batch.offsetTransform"),
                });

            Describe("help.schema",
                summary: "Compares dispatcher commands vs described commands. Can return skeleton for missing.",
                risk: "info",
                owner: "HelpSchema",
                args: new Dictionary<string, object>
                {
                    ["includeSkeleton"] = Arg.Bool(optional: true, @default: false),
                });

            // --- ping ---
            Describe("ping",
                summary: "Health check. Returns unity version, project name, active scene.",
                risk: "info",
                owner: "CommandDispatcher",
                args: new Dictionary<string, object>());

            // --- примеры, которые у тебя уже есть ---
            Describe("scene.batch.offsetTransform",
                summary: "Offsets position/rotation/scale for objects selected by query.",
                risk: "risky",
                owner: "SceneBatchTransforms",
                args: new Dictionary<string, object>
                {
                    ["query"] = Arg.Object(required: true, example: "scene.query args object"),
                    ["space"] = Arg.String(optional: true, @default: "world", allowed: new[] { "world", "local" }),
                    ["positionDelta"] = Arg.Vec3(optional: true, @default: new { x = 0, y = 0, z = 0 }),
                    ["rotationDeltaEuler"] = Arg.Vec3(optional: true, @default: new { x = 0, y = 0, z = 0 }),
                    ["scaleMul"] = Arg.Vec3(optional: true, @default: new { x = 1, y = 1, z = 1 }),
                    ["scaleDelta"] = Arg.Vec3(optional: true, @default: new { x = 0, y = 0, z = 0 }),
                    ["max"] = Arg.Int(optional: true, @default: 2000, min: 1, max: 20000),
                    ["dryRun"] = Arg.Bool(optional: true, @default: false),
                });

            Describe("scene.batch.placePrefabGrid",
                summary: "Creates a grid of prefab instances (optionally under a parent).",
                risk: "risky",
                owner: "SceneBatchTransforms",
                args: new Dictionary<string, object>
                {
                    ["assetPath"] = Arg.String(required: true, example: "Assets/Prefabs/Tree.prefab"),
                    ["rows"] = Arg.Int(required: true, min: 1),
                    ["cols"] = Arg.Int(required: true, min: 1),
                    ["plane"] = Arg.String(optional: true, @default: "xz", allowed: new[] { "xz", "xy", "yz" }),
                    ["origin"] = Arg.Vec3(optional: true, @default: new { x = 0, y = 0, z = 0 }),
                    ["spacing"] = Arg.Vec3(optional: true, @default: new { x = 1, y = 0, z = 1 }),
                    ["rotationEuler"] = Arg.Vec3(optional: true, @default: new { x = 0, y = 0, z = 0 }),
                    ["scale"] = Arg.Vec3(optional: true, @default: new { x = 1, y = 1, z = 1 }),
                    ["centered"] = Arg.Bool(optional: true, @default: false),
                    ["nameTemplate"] = Arg.String(optional: true, @default: "Grid_{r}_{c}"),

                    ["parentInstanceId"] = Arg.Int(optional: true, @default: 0),
                    ["parentName"] = Arg.String(optional: true, example: "GridRoot"),
                    ["createParentIfMissing"] = Arg.Bool(optional: true, @default: true),
                    ["makeParentNameUnique"] = Arg.Bool(optional: true, @default: true),

                    ["maxTotal"] = Arg.Int(optional: true, @default: 5000, min: 1, max: 50000),
                    ["dryRun"] = Arg.Bool(optional: true, @default: false),
                });

            // ВАЖНО: дальше просто добавляешь Describe(...) для других команд из missingInHelp.
        }

        // ---------- ВРЕМЕННО: список команд из dispatcher ----------
        // Сейчас это ручной список. Позже можно сделать автоматический реестр.
        private static string[] BuildDispatcherCommands()
        {
            return CommandDispatcher.GetRegisteredCommands();
        }
    }
    }

