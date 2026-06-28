using System;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class HelpCommands
    {
        // command: help.commands
        public static object Commands(JToken args)
        {
            var allDispatcher = CommandDispatcher.GetRegisteredCommands();
            System.Array.Sort(allDispatcher, StringComparer.OrdinalIgnoreCase);

            return new
            {
                ok = true,
                schemaVersion = HelpRegistry.SchemaVersion,
                total = allDispatcher.Length,
                commands = allDispatcher
            };
        }

        // command: help.command
        // args: { "command":"..." }
        public static object Command(JToken args)
        {
            string cmd = ((string?)args?["command"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(cmd))
                return new { ok = false, error = "args.command is required" };

            var spec = HelpRegistry.Get(cmd);
            if (spec == null)
                return new { ok = false, error = $"Unknown command: {cmd}" };

            return new
            {
                ok = true,
                schemaVersion = HelpRegistry.SchemaVersion,
                command = spec
            };
        }

        // command: help.schema
        // args: { "prefix":"scene." } (optional)
        public static object Schema(JToken args)
        {
            string? prefix = ((string?)args?["prefix"])?.Trim();
            var all = HelpRegistry.GetAll(prefix);

            return new
            {
                ok = true,
                schemaVersion = HelpRegistry.SchemaVersion,
                prefix = string.IsNullOrWhiteSpace(prefix) ? null : prefix,
                count = all.Length,
                commands = all
            };
        }
    }
}