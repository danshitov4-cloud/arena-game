using Unity.Plastic.Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEditor;

namespace Orchestrator.Editor
{
    public static class OptimizerRestore
    {
        // command: scene.optimize.restore
        // args:
        // { "restoreAll": true, "dryRun": false }
        // или
        // { "actionIds": ["test-disable-SortByY","test-disable-BuildingView"], "dryRun": true }
        public static object Restore(JToken args)
        {
            bool restoreAll = (bool?)args?["restoreAll"] ?? true;
            bool dryRun = (bool?)args?["dryRun"] ?? false;

            // optional: actionIds[]
            var actionIdsToken = args?["actionIds"];
            HashSet<string>? actionIds = null;
            if (actionIdsToken != null && actionIdsToken.Type == JTokenType.Array)
            {
                actionIds = actionIdsToken
                    .Select(x => (string)x)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToHashSet(StringComparer.Ordinal);
            }

            // План пересчитываем на лету (MVP)
            dynamic plan = OptimizerPlan.BuildPlan();
            var actions = new List<dynamic>();
            foreach (var a in plan.actions) actions.Add(a);

            // Берём только те actions, которые реально выключают что-то через batchSetEnabledByType
            // и у которых enabled=false
            var candidates = actions
                .Where(a => a.command != null && (string)a.command == "scene.batchSetEnabledByType")
                .Where(a => a.args != null && a.args.enabled == false)
                .ToList();

            if (!restoreAll && (actionIds == null || actionIds.Count == 0))
                throw new ArgumentException("When restoreAll=false you must provide args.actionIds[]");

            if (!restoreAll)
                candidates = candidates.Where(a => actionIds!.Contains((string)a.id)).ToList();

            var restored = new List<object>();
            var skipped = new List<object>();

            // Один Undo на весь restore
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Orchestrator Restore Optimize Plan");

            try
            {
                foreach (var a in candidates)
                {
                    string id = (string)a.id;
                    string cmd = (string)a.command;

                    if (!IsAllowed(cmd))
                    {
                        skipped.Add(new { id, command = cmd, reason = "not allowed" });
                        continue;
                    }

                    // Берём исходные args и делаем enabled=true
                    JObject baseArgs = JObject.FromObject(a.args);
                    baseArgs["enabled"] = true;

                    if (dryRun)
                    {
                        restored.Add(new { id, command = cmd, wouldRestore = true, args = baseArgs });
                    }
                    else
                    {
                        var res = BatchCommands.BatchSetEnabledByType(baseArgs);
                        restored.Add(new { id, command = cmd, result = res });
                    }
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }

            return new
            {
                requested = new
                {
                    restoreAll,
                    dryRun,
                    actionIds = actionIds?.ToArray()
                },
                restoredCount = restored.Count,
                skippedCount = skipped.Count,
                restored,
                skipped
            };
        }

        private static bool IsAllowed(string cmd)
            => cmd == "scene.batchSetEnabledByType";
    }
}