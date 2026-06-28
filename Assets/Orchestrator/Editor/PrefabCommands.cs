using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class PrefabCommands
    {
        // command: prefab.applyOverridesByQuery
        // args:
        // {
        //   "query": { ... },      // required
        //   "max": 2000,           // safety
        //   "dryRun": false
        // }
        public static object ApplyOverridesByQuery(JToken args)
        {
            if (args?["query"] == null) throw new ArgumentException("args.query is required");
            bool dryRun = (bool?)args?["dryRun"] ?? false;

            int max = (int?)args?["max"] ?? 2000;
            if (max < 1) max = 1;
            if (max > 20000) max = 20000;

            var gos = SceneUtils.GetObjectsFromQuery(args["query"]!);
            if (gos.Length > max) gos = gos.Take(max).ToArray();

            int matched = gos.Length;
            int applied = 0;
            int skipped = 0;

            var samples = new List<object>(10);

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator Prefab Apply Overrides");

                try
                {
                    foreach (var go in gos)
                    {
                        if (go == null) { skipped++; continue; }

                        // Берём ближайший корень префаб-инстанса
                        var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                        if (root == null) { skipped++; continue; }

                        var status = PrefabUtility.GetPrefabInstanceStatus(root);
                        if (status != PrefabInstanceStatus.Connected)
                        {
                            skipped++;
                            continue;
                        }

                        // Есть ли вообще overrides?
                        if (!PrefabUtility.HasPrefabInstanceAnyOverrides(root, false))
                        {
                            skipped++;
                            continue;
                        }

                        // Применяем overrides в prefab asset
                        PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);
                        applied++;

                        if (samples.Count < 10)
                        {
                            samples.Add(new
                            {
                                name = root.name,
                                instanceId = root.GetInstanceID(),
                                action = "applied"
                            });
                        }
                    }
                }
                finally
                {
                    Undo.CollapseUndoOperations(group);
                }
            }
            else
            {
                foreach (var go in gos)
                {
                    if (go == null) { skipped++; continue; }
                    var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                    if (root == null) { skipped++; continue; }

                    var status = PrefabUtility.GetPrefabInstanceStatus(root);
                    if (status != PrefabInstanceStatus.Connected) { skipped++; continue; }

                    if (!PrefabUtility.HasPrefabInstanceAnyOverrides(root, false)) { skipped++; continue; }

                    applied++;
                    if (samples.Count < 10)
                        samples.Add(new { name = root.name, instanceId = root.GetInstanceID(), action = "wouldApply" });
                }
            }

            return new
            {
                ok = true,
                dryRun,
                matchedObjects = matched,
                appliedCount = applied,
                skippedCount = skipped,
                samples
            };
        }

        // command: prefab.revertOverridesByQuery
        // args:
        // {
        //   "query": { ... },      // required
        //   "max": 2000,           // safety
        //   "dryRun": false
        // }
        public static object RevertOverridesByQuery(JToken args)
        {
            if (args?["query"] == null) throw new ArgumentException("args.query is required");
            bool dryRun = (bool?)args?["dryRun"] ?? false;

            int max = (int?)args?["max"] ?? 2000;
            if (max < 1) max = 1;
            if (max > 20000) max = 20000;

            var gos = SceneUtils.GetObjectsFromQuery(args["query"]!);
            if (gos.Length > max) gos = gos.Take(max).ToArray();

            int matched = gos.Length;
            int reverted = 0;
            int skipped = 0;

            var samples = new List<object>(10);

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator Prefab Revert Overrides");

                try
                {
                    foreach (var go in gos)
                    {
                        if (go == null) { skipped++; continue; }

                        var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                        if (root == null) { skipped++; continue; }

                        var status = PrefabUtility.GetPrefabInstanceStatus(root);
                        if (status != PrefabInstanceStatus.Connected)
                        {
                            skipped++;
                            continue;
                        }

                        if (!PrefabUtility.HasPrefabInstanceAnyOverrides(root, false))
                        {
                            skipped++;
                            continue;
                        }

                        PrefabUtility.RevertPrefabInstance(root, InteractionMode.UserAction);
                        reverted++;

                        if (samples.Count < 10)
                            samples.Add(new { name = root.name, instanceId = root.GetInstanceID(), action = "reverted" });
                    }
                }
                finally
                {
                    Undo.CollapseUndoOperations(group);
                }
            }
            else
            {
                foreach (var go in gos)
                {
                    if (go == null) { skipped++; continue; }

                    var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                    if (root == null) { skipped++; continue; }

                    var status = PrefabUtility.GetPrefabInstanceStatus(root);
                    if (status != PrefabInstanceStatus.Connected) { skipped++; continue; }

                    if (!PrefabUtility.HasPrefabInstanceAnyOverrides(root, false)) { skipped++; continue; }

                    reverted++;
                    if (samples.Count < 10)
                        samples.Add(new { name = root.name, instanceId = root.GetInstanceID(), action = "wouldRevert" });
                }
            }

            return new
            {
                ok = true,
                dryRun,
                matchedObjects = matched,
                revertedCount = reverted,
                skippedCount = skipped,
                samples
            };
        }

        // command: prefab.unpackByQuery
        // args:
        // {
        //   "query": { ... },                 // required
        //   "mode": "outmost" | "completely", // default outmost
        //   "max": 2000,                      // safety
        //   "dryRun": false
        // }
        public static object UnpackByQuery(JToken args)
        {
            if (args?["query"] == null) throw new ArgumentException("args.query is required");
            bool dryRun = (bool?)args?["dryRun"] ?? false;

            string mode = ((string?)args?["mode"] ?? "outmost").Trim().ToLowerInvariant();
            var unpackMode = mode == "completely"
                ? PrefabUnpackMode.Completely
                : PrefabUnpackMode.OutermostRoot;

            int max = (int?)args?["max"] ?? 2000;
            if (max < 1) max = 1;
            if (max > 20000) max = 20000;

            var gos = SceneUtils.GetObjectsFromQuery(args["query"]!);
            if (gos.Length > max) gos = gos.Take(max).ToArray();

            int matched = gos.Length;
            int unpacked = 0;
            int skipped = 0;

            var samples = new List<object>(10);

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator Prefab Unpack");

                try
                {
                    foreach (var go in gos)
                    {
                        if (go == null) { skipped++; continue; }

                        var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                        if (root == null) { skipped++; continue; }

                        var status = PrefabUtility.GetPrefabInstanceStatus(root);
                        if (status == PrefabInstanceStatus.NotAPrefab)
                        {
                            skipped++;
                            continue;
                        }

                        PrefabUtility.UnpackPrefabInstance(root, unpackMode, InteractionMode.UserAction);
                        unpacked++;

                        if (samples.Count < 10)
                            samples.Add(new { name = root.name, instanceId = root.GetInstanceID(), action = "unpacked", mode });
                    }
                }
                finally
                {
                    Undo.CollapseUndoOperations(group);
                }
            }
            else
            {
                foreach (var go in gos)
                {
                    if (go == null) { skipped++; continue; }
                    var root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
                    if (root == null) { skipped++; continue; }

                    var status = PrefabUtility.GetPrefabInstanceStatus(root);
                    if (status == PrefabInstanceStatus.NotAPrefab) { skipped++; continue; }

                    unpacked++;
                    if (samples.Count < 10)
                        samples.Add(new { name = root.name, instanceId = root.GetInstanceID(), action = "wouldUnpack", mode });
                }
            }

            return new
            {
                ok = true,
                dryRun,
                mode,
                matchedObjects = matched,
                unpackedCount = unpacked,
                skippedCount = skipped,
                samples
            };
        }
    }
}

