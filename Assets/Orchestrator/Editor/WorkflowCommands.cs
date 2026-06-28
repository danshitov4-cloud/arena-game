// Assets/Orchestrator/Editor/WorkflowCommands.cs
// ЕДИНСТВЕННЫЙ workflow-класс. Без SceneWorkflow.
// Поддерживает autoRestore в двух режимах:
// 1) restoreMode = "snapshot"      -> SnapshotCommands.Restore(snapshotId)
// 2) restoreMode = "deleteParent"  -> SceneDeleteCommands.DeleteByName(parentName)

using System;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class WorkflowCommands
    {
        // --- scheduled restore state ---
        private struct ScheduledRestore
        {
            public bool active;

            // mode: "snapshot" | "deleteParent"
            public string mode;

            // snapshot mode
            public string snapshotId;

            // deleteParent mode
            public string parentName;

            public double dueTime;
            public string reason;
        }

        private static ScheduledRestore? _scheduled;
        private static object? _lastWorkflowResult;

        // command: scene.workflow.last
        public static object Last(JToken args)
        {
            return new
            {
                ok = true,
                found = _lastWorkflowResult != null,
                workflow = _lastWorkflowResult
            };
        }

        // command: scene.workflow.cancelRestore
        public static object CancelScheduledRestore(JToken args)
        {
            if (_scheduled.HasValue && _scheduled.Value.active)
            {
                EditorApplication.update -= TickScheduledRestore;

                var s = _scheduled.Value;
                _scheduled = null;

                return new
                {
                    ok = true,
                    cancelled = true,
                    mode = s.mode,
                    snapshotId = s.snapshotId,
                    parentName = s.parentName,
                    dueTime = s.dueTime,
                    reason = s.reason
                };
            }

            return new { ok = true, cancelled = false };
        }

        // command: scene.workflow.restoreNow
        // args: { "mode":"snapshot|deleteParent", "snapshotId":"...", "parentName":"GridRoot" }
        public static object RestoreNow(JToken args)
        {
            var o = args as JObject ?? new JObject();
            string mode = ((string?)o["mode"] ?? "snapshot").Trim();

            if (mode.Equals("deleteParent", StringComparison.OrdinalIgnoreCase))
            {
                string parentName = ((string?)o["parentName"] ?? "").Trim();
                if (string.IsNullOrWhiteSpace(parentName))
                    return new { ok = false, error = "restoreNow deleteParent requires args.parentName" };

                var del = SceneDeleteCommands.DeleteByName(new JObject
                {
                    ["name"] = parentName,
                    ["includeInactive"] = true,
                    ["dryRun"] = false
                });

                return new { ok = true, mode = "deleteParent", parentName, result = del };
            }
            else
            {
                string snapshotId = ((string?)o["snapshotId"] ?? "").Trim();
                if (string.IsNullOrWhiteSpace(snapshotId))
                    return new { ok = false, error = "restoreNow snapshot requires args.snapshotId" };

                var res = SnapshotCommands.Restore(new JObject
                {
                    ["id"] = snapshotId,
                    ["dryRun"] = false
                });

                return new { ok = true, mode = "snapshot", snapshotId, result = res };
            }
        }

        // command: scene.workflow.run
        // args:
        // {
        //   "snapshotName": "before-x",
        //   "command": "scene.batch.placePrefabGrid",
        //   "commandArgs": { ... },
        //   "autoRestore": true,
        //   "restoreAfterSeconds": 30,
        //   "dryRun": false,
        //
        //   // NEW:
        //   "restoreMode": "snapshot" | "deleteParent",
        //   "parentName": "GridRoot" // required for deleteParent (or commandArgs.parentName)
        // }
        public static object Run(JToken args)
        {
            var o = args as JObject;
            if (o == null) throw new ArgumentException("args must be an object");

            string snapshotName = ((string?)o["snapshotName"] ?? $"wf-{DateTime.UtcNow:yyyyMMdd-HHmmss}").Trim();
            string command = ((string?)o["command"] ?? "").Trim();
            var commandArgs = o["commandArgs"];

            bool autoRestore = (bool?)o["autoRestore"] ?? false;
            double restoreAfterSeconds = (double?)o["restoreAfterSeconds"] ?? 0;
            bool dryRun = (bool?)o["dryRun"] ?? false;

            // NEW: restore mode
            string restoreMode = ((string?)o["restoreMode"] ?? "snapshot").Trim(); // snapshot|deleteParent
            string parentNameForRestore = ((string?)o["parentName"] ?? "").Trim();

            if (string.IsNullOrWhiteSpace(command))
                return new { ok = false, stage = "validate", error = "args.command is required" };

            // 1) TAKE SNAPSHOT (сценовый)
            object takeObj;
            try
            {
                var takeArgs = new JObject
                {
                    ["name"] = snapshotName,
                    ["dryRun"] = dryRun
                };

                takeObj = SnapshotCommands.Take(takeArgs);
            }
            catch (Exception ex)
            {
                return new { ok = false, stage = "take", error = ex.Message };
            }

            string snapshotId = ExtractSnapshotId(takeObj);

            if (string.IsNullOrWhiteSpace(snapshotId))
            {
                return new
                {
                    ok = false,
                    stage = "take",
                    error = "snapshotId missing",
                    take = JToken.FromObject(takeObj)
                };
            }

            // 2) APPLY COMMAND
            object applyObj;
            try
            {
                var req = new CommandRequest
                {
                    id = "wf_apply",
                    command = command,
                    args = commandArgs,
                    dryRun = dryRun
                };

                var resp = CommandDispatcher.Execute(req);

                applyObj = new
                {
                    ok = resp.ok,
                    error = resp.error,
                    result = resp.result
                };

                if (!resp.ok)
                {
                    return new
                    {
                        ok = false,
                        stage = "apply",
                        error = resp.error ?? "apply failed",
                        snapshotId,
                        take = JToken.FromObject(takeObj),
                        apply = applyObj
                    };
                }
            }
            catch (Exception ex)
            {
                return new
                {
                    ok = false,
                    stage = "apply",
                    error = ex.Message,
                    snapshotId,
                    take = JToken.FromObject(takeObj)
                };
            }

            // 3) OPTIONAL: schedule restore
            object scheduledObj = new { willRestore = false };

            if (autoRestore && !dryRun)
            {
                if (restoreAfterSeconds < 0) restoreAfterSeconds = 0;

                if (restoreMode.Equals("deleteParent", StringComparison.OrdinalIgnoreCase))
                {
                    // parentName можно передать явно в workflow args, либо взять из commandArgs.parentName
                    if (string.IsNullOrWhiteSpace(parentNameForRestore))
                        parentNameForRestore = ((string?)((commandArgs as JObject)?["parentName"] ?? "")).Trim();

                    if (string.IsNullOrWhiteSpace(parentNameForRestore))
                    {
                        return new
                        {
                            ok = false,
                            stage = "schedule",
                            error = "restoreMode=deleteParent requires args.parentName (or commandArgs.parentName)",
                            snapshotId,
                            take = JToken.FromObject(takeObj),
                            apply = applyObj
                        };
                    }

                    ScheduleRestore_DeleteParent(parentNameForRestore, restoreAfterSeconds, "scene.workflow.run autoRestore deleteParent");
                    scheduledObj = new { willRestore = true, restoreAfterSeconds, mode = "deleteParent", parentName = parentNameForRestore };
                }
                else
                {
                    ScheduleRestore_Snapshot(snapshotId, restoreAfterSeconds, "scene.workflow.run autoRestore snapshot");
                    scheduledObj = new { willRestore = true, restoreAfterSeconds, mode = "snapshot", snapshotId };
                }
            }

            var result = new
            {
                ok = true,
                workflow = new
                {
                    snapshotName,
                    snapshotId,
                    command,
                    dryRun,
                    autoRestore,
                    restoreAfterSeconds,
                    restoreMode = restoreMode
                },
                take = JToken.FromObject(takeObj),
                apply = applyObj,
                scheduled = scheduledObj
            };

            _lastWorkflowResult = result;
            return result;
        }

        // ---------- internals ----------

        private static string ExtractSnapshotId(object takeObj)
        {
            try
            {
                var jt = JToken.FromObject(takeObj);

                // 1) { snapshot: { id: "..." } }
                var id1 = (string?)jt["snapshot"]?["id"];
                if (!string.IsNullOrWhiteSpace(id1)) return id1.Trim();

                // 2) { snapshotId: "..." }
                var id2 = (string?)jt["snapshotId"];
                if (!string.IsNullOrWhiteSpace(id2)) return id2.Trim();

                // 3) { result: { snapshot: { id } } } (если кто-то завернул)
                var id3 = (string?)jt["result"]?["snapshot"]?["id"];
                if (!string.IsNullOrWhiteSpace(id3)) return id3.Trim();

                var id4 = (string?)jt["result"]?["snapshotId"];
                if (!string.IsNullOrWhiteSpace(id4)) return id4.Trim();

                // 4) На всякий: если где-то другой регистр
                var id5 = (string?)jt["Result"]?["Snapshot"]?["Id"];
                if (!string.IsNullOrWhiteSpace(id5)) return id5.Trim();
            }
            catch { }

            return "";
        }

        private static void ScheduleRestore_Snapshot(string snapshotId, double afterSeconds, string reason)
        {
            EditorApplication.update -= TickScheduledRestore;

            _scheduled = new ScheduledRestore
            {
                active = true,
                mode = "snapshot",
                snapshotId = snapshotId,
                parentName = "",
                dueTime = EditorApplication.timeSinceStartup + Math.Max(0, afterSeconds),
                reason = reason ?? ""
            };

            EditorApplication.update += TickScheduledRestore;
        }

        private static void ScheduleRestore_DeleteParent(string parentName, double afterSeconds, string reason)
        {
            EditorApplication.update -= TickScheduledRestore;

            _scheduled = new ScheduledRestore
            {
                active = true,
                mode = "deleteParent",
                snapshotId = "",
                parentName = parentName,
                dueTime = EditorApplication.timeSinceStartup + Math.Max(0, afterSeconds),
                reason = reason ?? ""
            };

            EditorApplication.update += TickScheduledRestore;
        }

        private static void TickScheduledRestore()
        {
            if (!_scheduled.HasValue || !_scheduled.Value.active)
            {
                EditorApplication.update -= TickScheduledRestore;
                return;
            }

            var s = _scheduled.Value;
            if (EditorApplication.timeSinceStartup < s.dueTime)
                return;

            _scheduled = null;
            EditorApplication.update -= TickScheduledRestore;

            try
            {
                if (string.Equals(s.mode, "deleteParent", StringComparison.OrdinalIgnoreCase))
                {
                    SceneDeleteCommands.DeleteByName(new JObject
                    {
                        ["name"] = s.parentName,
                        ["includeInactive"] = true,
                        ["dryRun"] = false
                    });
                }
                else
                {
                    SnapshotCommands.Restore(new JObject
                    {
                        ["id"] = s.snapshotId,
                        ["dryRun"] = false
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Orchestrator] Scheduled restore failed: {ex.Message}");
            }
        }
    }
}
