using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Orchestrator.Editor
{
    // Простые DTO, чтобы Newtonsoft не ловил self-referencing loop
    [Serializable]
    public sealed class ArgSpec
    {
        public string type = "string";         // "string" | "int" | "float" | "bool" | "vec3" | "color" | "object" | "array"
        public bool optional = true;
        public object? @default = null;        // число/строка/bool или null
        public string? description = null;
        public string[]? allowed = null;       // для enum-подобных
    }

    [Serializable]
    public sealed class CommandSpec
    {
        public string command = "";
        public string summary = "";
        public string owner = "";
        public string risk = "safe";           // safe | info | risky
        public Dictionary<string, ArgSpec> args = new();
        public object[]? examples = null;      // массив примеров запросов
        public string? returns = null;         // коротко что возвращает
    }

    public static class HelpRegistry
    {
        public const string SchemaVersion = "0.1.0";

        private static readonly Dictionary<string, CommandSpec> _specs =
            new Dictionary<string, CommandSpec>(StringComparer.OrdinalIgnoreCase);

        public static void Register(CommandSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (string.IsNullOrWhiteSpace(spec.command)) throw new ArgumentException("spec.command required");
            _specs[spec.command.Trim()] = spec;
        }

        public static CommandSpec? Get(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            _specs.TryGetValue(command.Trim(), out var spec);
            return spec;
        }

        public static string[] ListCommands()
            => _specs.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

        public static CommandSpec[] GetAll(string? prefix = null)
        {
            IEnumerable<CommandSpec> q = _specs.Values;

            if (!string.IsNullOrWhiteSpace(prefix))
                q = q.Where(s => s.command.StartsWith(prefix.Trim(), StringComparison.OrdinalIgnoreCase));

            return q.OrderBy(s => s.command, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        // ВАЖНО: один раз заполнить реестр. Можно расширять постепенно.
        [UnityEditor.InitializeOnLoadMethod]
        private static void Init()
        {
            if (_specs.Count > 0) return;

            // --- help.* ---
            Register(new CommandSpec
            {
                command = "help.commands",
                summary = "Lists all available commands.",
                owner = "HelpCommands",
                risk = "safe",
                args = new Dictionary<string, ArgSpec>(),
                returns = "commands[]",
                examples = new object[] {
                    new { id="hc", command="help.commands", args=(object?)null, dryRun=false }
                }
            });

            Register(new CommandSpec
            {
                command = "help.command",
                summary = "Returns schema for a single command.",
                owner = "HelpCommands",
                risk = "safe",
                args = new Dictionary<string, ArgSpec>
                {
                    ["command"] = new ArgSpec { type = "string", optional = false, description = "Command name, e.g. scene.batch.offsetTransform" }
                },
                returns = "CommandSpec",
                examples = new object[] {
                    new { id="h1", command="help.command", args=new { command="scene.batch.offsetTransform" }, dryRun=false }
                }
            });

            Register(new CommandSpec
            {
                command = "help.schema",
                summary = "Returns full schema for all commands (optionally filtered by prefix).",
                owner = "HelpCommands",
                risk = "safe",
                args = new Dictionary<string, ArgSpec>
                {
                    ["prefix"] = new ArgSpec { type = "string", optional = true, description = "Optional filter, e.g. 'scene.' or 'materials.'" }
                },
                returns = "CommandSpec[]",
                examples = new object[] {
                    new { id="hs1", command="help.schema", args=(object?)null, dryRun=false },
                    new { id="hs2", command="help.schema", args=new { prefix="materials." }, dryRun=false },
                }
            });

            // --- scene.batch.offsetTransform ---
            Register(new CommandSpec
            {
                command = "scene.batch.offsetTransform",
                summary = "Offsets position/rotation/scale for objects selected by query.",
                owner = "SceneBatchTransforms",
                risk = "risky",
                args = new Dictionary<string, ArgSpec>
                {
                    ["query"] = new ArgSpec { type = "object", optional = false, description = "scene.query args object" },
                    ["space"] = new ArgSpec { type = "string", optional = true, @default = "world", allowed = new[] { "world", "local" } },
                    ["positionDelta"] = new ArgSpec { type = "vec3", optional = true, description = "Delta added to position/localPosition" },
                    ["rotationDeltaEuler"] = new ArgSpec { type = "vec3", optional = true, description = "Delta rotation in degrees" },
                    ["scaleMul"] = new ArgSpec { type = "vec3", optional = true, description = "Multiply localScale by (x,y,z)" },
                    ["scaleDelta"] = new ArgSpec { type = "vec3", optional = true, description = "Add to localScale" },
                    ["max"] = new ArgSpec { type = "int", optional = true, @default = 2000 },
                    ["dryRun"] = new ArgSpec { type = "bool", optional = true, @default = false },
                },
                returns = "matchedObjects/changedObjects + samples",
                examples = new object[] {
                    new {
                        id="off1",
                        command="scene.batch.offsetTransform",
                        args=new {
                            query=new { nameContains="Building", max=500, includeInactive=true },
                            space="world",
                            positionDelta=new { x=0, y=2, z=0 },
                            dryRun=true
                        },
                        dryRun=false
                    }
                }
            });

            // --- scene.batch.placePrefabGrid ---
            Register(new CommandSpec
            {
                command = "scene.batch.placePrefabGrid",
                summary = "Instantiates a prefab in a rows x cols grid, optionally creates/uses parent.",
                owner = "SceneBatchTransforms",
                risk = "risky",
                args = new Dictionary<string, ArgSpec>
                {
                    ["assetPath"] = new ArgSpec { type = "string", optional = false, description = "Assets/.../X.prefab" },
                    ["rows"] = new ArgSpec { type = "int", optional = false },
                    ["cols"] = new ArgSpec { type = "int", optional = false },
                    ["plane"] = new ArgSpec { type = "string", optional = true, @default = "xz", allowed = new[] { "xz", "xy", "yz" } },
                    ["origin"] = new ArgSpec { type = "vec3", optional = true },
                    ["spacing"] = new ArgSpec { type = "vec3", optional = true },
                    ["rotationEuler"] = new ArgSpec { type = "vec3", optional = true },
                    ["scale"] = new ArgSpec { type = "vec3", optional = true },
                    ["centered"] = new ArgSpec { type = "bool", optional = true, @default = false },
                    ["nameTemplate"] = new ArgSpec { type = "string", optional = true, @default = "Grid_{r}_{c}" },

                    ["parentInstanceId"] = new ArgSpec { type = "int", optional = true, description = "Existing parent instance id" },
                    ["parentName"] = new ArgSpec { type = "string", optional = true, description = "Find or create parent by name" },
                    ["createParentIfMissing"] = new ArgSpec { type = "bool", optional = true, @default = true },
                    ["makeParentNameUnique"] = new ArgSpec { type = "bool", optional = true, @default = true },

                    ["maxTotal"] = new ArgSpec { type = "int", optional = true, @default = 5000 },
                    ["dryRun"] = new ArgSpec { type = "bool", optional = true, @default = false },
                },
                returns = "createdCount + samples + parent info",
                examples = new object[] {
                    new {
                        id="grid1",
                        command="scene.batch.placePrefabGrid",
                        args=new {
                            assetPath="Assets/prefabs/bullding/Building_Base.prefab",
                            rows=3, cols=4,
                            plane="xz",
                            origin=new { x=0, y=0, z=0 },
                            spacing=new { x=2, y=0, z=2 },
                            centered=true,
                            parentName="BuildingsGrid",
                            createParentIfMissing=true,
                            dryRun=true
                        },
                        dryRun=false
                    }
                }
            });

            // Дальше ты просто ДОБАВЛЯЕШЬ Register(...) для новых команд по мере роста проекта.
        }
    }
}