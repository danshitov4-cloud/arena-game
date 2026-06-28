using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class SceneGameObjectCommands
    {
        // command: scene.setActiveByQuery
        public static object SetActiveByQuery(JToken args)
        {
            if (args?["query"] == null) throw new ArgumentException("args.query is required");
            bool dryRun = (bool?)args?["dryRun"] ?? false;

            if (args?["active"] == null) throw new ArgumentException("args.active is required");
            bool active = (bool)args["active"]!;

            int max = (int?)args?["max"] ?? 2000;
            max = Mathf.Clamp(max, 1, 20000);

            // ֵֵַָּֽֽ־: SceneUtils גלוסעמ SceneBatchProps.SceneBatch_GetObjectsFromQuery
            var gos = SceneUtils.GetObjectsFromQuery(args["query"]!);
            if (gos.Length > max) gos = gos.Take(max).ToArray();

            int matched = gos.Length;
            int changed = 0;
            int skipped = 0;

            var samples = new List<object>(10);

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator SetActiveByQuery");

                try
                {
                    foreach (var go in gos)
                    {
                        if (go == null) { skipped++; continue; }

                        bool current = go.activeSelf;
                        if (current == active) continue;

                        Undo.RecordObject(go, "SetActive");
                        go.SetActive(active);
                        changed++;
                        EditorUtility.SetDirty(go);

                        if (samples.Count < 10)
                            samples.Add(new { name = go.name, id = go.GetInstanceID(), from = current, to = active });
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

                    bool current = go.activeSelf;
                    if (current != active) changed++;

                    if (samples.Count < 10)
                        samples.Add(new { name = go.name, id = go.GetInstanceID(), from = current, to = active });
                }
            }

            return new
            {
                ok = true,
                dryRun,
                active,
                matchedObjects = matched,
                changedObjects = changed,
                skippedObjects = skipped,
                samples
            };
        }

        // command: scene.deleteByQuery
        public static object DeleteByQuery(JToken args)
        {
            if (args?["query"] == null) throw new ArgumentException("args.query is required");
            bool dryRun = (bool?)args?["dryRun"] ?? false;

            int max = (int?)args?["max"] ?? 2000;
            max = Mathf.Clamp(max, 1, 20000);

            // ֵֵַָּֽֽ־: SceneUtils גלוסעמ SceneBatchProps.SceneBatch_GetObjectsFromQuery
            var gos = SceneUtils.GetObjectsFromQuery(args["query"]!);
            if (gos.Length > max) gos = gos.Take(max).ToArray();

            // ֵֵַָּֽֽ־: SceneUtils.GetDepth גלוסעמ כמךאכüםמדמ GetDepth
            var ordered = gos
                .Where(g => g != null)
                .OrderByDescending(g => SceneUtils.GetDepth(g!.transform))
                .ToArray();

            int matched = ordered.Length;
            int deleted = 0;

            var samples = new List<object>(10);

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator DeleteByQuery");

                try
                {
                    foreach (var go in ordered)
                    {
                        if (go == null) continue;

                        if (samples.Count < 10)
                            // ֵֵַָּֽֽ־: SceneUtils.GetHierarchyPath גלוסעמ כמךאכüםמדמ GetPath
                            samples.Add(new { name = go.name, id = go.GetInstanceID(), path = SceneUtils.GetHierarchyPath(go.transform) });

                        Undo.DestroyObjectImmediate(go);
                        deleted++;
                    }
                }
                finally
                {
                    Undo.CollapseUndoOperations(group);
                }
            }
            else
            {
                foreach (var go in ordered)
                {
                    if (go == null) continue;
                    deleted++;

                    if (samples.Count < 10)
                        samples.Add(new { name = go.name, id = go.GetInstanceID(), path = SceneUtils.GetHierarchyPath(go.transform) });
                }
            }

            return new
            {
                ok = true,
                dryRun,
                matchedObjects = matched,
                deletedObjects = deleted,
                samples
            };
        }

        // command: scene.destroyChildrenOf
        public static object DestroyChildrenOf(JToken args)
        {
            bool dryRun = (bool?)args?["dryRun"] ?? false;
            bool recursive = (bool?)args?["recursive"] ?? true;

            int maxParents = (int?)args?["maxParents"] ?? 200;
            maxParents = Mathf.Clamp(maxParents, 1, 5000);

            int maxChildren = (int?)args?["maxChildren"] ?? 20000;
            maxChildren = Mathf.Clamp(maxChildren, 1, 200000);

            var parents = ResolveParents(args, maxParents);

            int parentCount = parents.Length;
            int matchedChildren = 0;
            int deletedChildren = 0;

            var samples = new List<object>(10);

            var toDelete = new List<GameObject>(Math.Min(512, maxChildren));

            foreach (var p in parents)
            {
                if (p == null) continue;

                foreach (Transform child in p)
                {
                    if (child == null) continue;

                    if (recursive)
                        CollectRecursive(child, toDelete, maxChildren);
                    else
                        AddIfRoom(child.gameObject, toDelete, maxChildren);
                }
            }

            matchedChildren = toDelete.Count;

            // ֵֵַָּֽֽ־: SceneUtils.GetDepth גלוסעמ כמךאכüםמדמ GetDepth
            var ordered = toDelete
                .Where(g => g != null)
                .OrderByDescending(g => SceneUtils.GetDepth(g!.transform))
                .ToArray();

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator DestroyChildrenOf");

                try
                {
                    foreach (var go in ordered)
                    {
                        if (go == null) continue;

                        if (samples.Count < 10)
                            // ֵֵַָּֽֽ־: SceneUtils.GetHierarchyPath גלוסעמ כמךאכüםמדמ GetPath
                            samples.Add(new { name = go.name, id = go.GetInstanceID(), path = SceneUtils.GetHierarchyPath(go.transform) });

                        Undo.DestroyObjectImmediate(go);
                        deletedChildren++;
                    }
                }
                finally
                {
                    Undo.CollapseUndoOperations(group);
                }
            }
            else
            {
                foreach (var go in ordered)
                {
                    if (go == null) continue;

                    if (samples.Count < 10)
                        samples.Add(new { name = go.name, id = go.GetInstanceID(), path = SceneUtils.GetHierarchyPath(go.transform) });

                    deletedChildren++;
                }
            }

            return new
            {
                ok = true,
                dryRun,
                parentCount,
                recursive,
                matchedChildren,
                deletedChildren,
                samples
            };
        }

        // ---------------- helpers ----------------

        private static Transform[] ResolveParents(JToken args, int maxParents)
        {
            int parentId = (int?)args?["parentInstanceId"] ?? 0;

            if (parentId != 0)
            {
                var obj = EditorUtility.InstanceIDToObject(parentId);
                if (obj is GameObject go) return new[] { go.transform };
                if (obj is Transform t) return new[] { t };
                throw new ArgumentException("parentInstanceId must reference GameObject or Transform");
            }

            if (args?["parentQuery"] != null)
            {
                // ֵֵַָּֽֽ־: SceneUtils גלוסעמ SceneBatchProps.SceneBatch_GetObjectsFromQuery
                var gos = SceneUtils.GetObjectsFromQuery(args["parentQuery"]!);
                if (gos.Length > maxParents) gos = gos.Take(maxParents).ToArray();
                return gos.Where(g => g != null).Select(g => g!.transform).ToArray();
            }

            throw new ArgumentException("Provide args.parentInstanceId OR args.parentQuery");
        }

        private static void CollectRecursive(Transform root, List<GameObject> list, int max)
        {
            if (root == null) return;
            AddIfRoom(root.gameObject, list, max);

            for (int i = 0; i < root.childCount; i++)
            {
                if (list.Count >= max) return;
                var ch = root.GetChild(i);
                if (ch != null) CollectRecursive(ch, list, max);
            }
        }

        private static void AddIfRoom(GameObject go, List<GameObject> list, int max)
        {
            if (go == null) return;
            if (list.Count >= max) return;
            list.Add(go);
        }

        // ׃ְִֵֻֽÛ: כמךאכüםûו GetDepth ט GetPath — עוןונü טסןמכüחףי SceneUtils
    }
}