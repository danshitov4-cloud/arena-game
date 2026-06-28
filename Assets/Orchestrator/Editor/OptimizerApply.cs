using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Plastic.Newtonsoft.Json.Linq;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEditor;

namespace Orchestrator.Editor
{
    public static class OptimizerApply
    {
        // command: scene.optimize.apply
        // args:
        // { "applyAllWithCommand": true }
        // или
        // { "actionId": "test-disable-SortByY" }
        public static object Apply(JToken args)
        {
            bool applyAll = (bool?)args?["applyAllWithCommand"] ?? false;
            string actionId = (string?)args?["actionId"] ?? "";
            bool dryRun = (bool?)args?["dryRun"] ?? false;
            var actionIdsToken = args?["actionIds"];
            var actionIds = actionIdsToken != null && actionIdsToken.Type == JTokenType.Array
                ? actionIdsToken.Select(x => (string)x).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal)
                : null;
            dynamic plan = OptimizerPlan.BuildPlan();
            var actions = new List<dynamic>();
            foreach (var a in plan.actions) actions.Add(a);

            List<dynamic> toApply;

            if (applyAll)
            {
                toApply = actions.Where(a => a.command != null).ToList();
            }
            else if (actionIds != null && actionIds.Count > 0)
            {
                toApply = actions.Where(a => a.command != null && actionIds.Contains((string)a.id)).ToList();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(actionId))
                    throw new ArgumentException("args.actionId or args.actionIds is required when applyAllWithCommand=false");

                toApply = actions.Where(a => a.command != null && (string)a.id == actionId).ToList();
            }

            var applied = new List<object>();
            var skipped = new List<object>();

            // ќдин Undo дл€ всего применени€
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Orchestrator Apply Optimize Plan");

            try
            {
                foreach (var a in toApply)
                {
                    string cmd = (string)a.command;

                    if (!IsAllowed(cmd))
                    {
                        skipped.Add(new { id = (string)a.id, command = cmd, reason = "not allowed" });
                        continue;
                    }

                    JToken cmdArgs = a.args == null ? new JObject() : JObject.FromObject(a.args);

                    if (cmd == "scene.batchSetEnabledByType")
                    {
                        if (dryRun)
                        {
                            applied.Add(new
                            {
                                id = (string)a.id,
                                command = cmd,
                                wouldApply = true,
                                args = cmdArgs
                            });
                        }
                        else
                        {
                            var res = BatchCommands.BatchSetEnabledByType(cmdArgs);
                            applied.Add(new { id = (string)a.id, command = cmd, result = res });
                        }
                    }
                    else
                    {
                        skipped.Add(new { id = (string)a.id, command = cmd, reason = "not implemented" });
                    }
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }

            return new
            {
                requested = new { applyAllWithCommand = applyAll, actionId, dryRun },
                appliedCount = applied.Count,
                skippedCount = skipped.Count,
                applied,
                skipped
            };
        }

        private static bool IsAllowed(string cmd)
        {
            // allowlist Ч расширим позже
            return cmd == "scene.batchSetEnabledByType";
        }
    }
}