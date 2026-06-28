using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class SceneHierarchyCommands
    {
        // command: scene.batch.duplicateByQuery
        public static object DuplicateByQuery(JToken args)
        {
            if (args?["query"] == null) throw new ArgumentException("args.query is required");

            bool dryRun = (bool?)args?["dryRun"] ?? false;

            int copiesPerObject = (int?)args?["copiesPerObject"] ?? 1;
            if (copiesPerObject < 1) copiesPerObject = 1;
            if (copiesPerObject > 1000) copiesPerObject = 1000;

            string nameTemplate = ((string?)args?["nameTemplate"] ?? "{name}_copy_{i1}").Trim();
            if (string.IsNullOrWhiteSpace(nameTemplate)) nameTemplate = "{name}_copy_{i1}";

            string parentMode = ((string?)args?["parentMode"] ?? "same").Trim().ToLowerInvariant();
            if (parentMode != "same" && parentMode != "keepworld" && parentMode != "set")
                throw new ArgumentException("args.parentMode must be 'same' or 'keepWorld' or 'set'");

            string parentName = ((string?)args?["parentName"] ?? "").Trim();

            int max = (int?)args?["max"] ?? 2000;
            if (max < 1) max = 1;
            if (max > 20000) max = 20000;

            int maxCreated = (int?)args?["maxCreated"] ?? 20000;
            if (maxCreated < 1) maxCreated = 1;
            if (maxCreated > 200000) maxCreated = 200000;

            // »«Ã≈Õ≈ÕŒ: SceneUtils ‚ÏÂÒÚÓ ÎÓÍ‡Î¸ÌÓ„Ó ReadVec3
            Vector3 offset = SceneUtils.ReadVec3(args?["worldPositionOffset"], Vector3.zero);

            // »«Ã≈Õ≈ÕŒ: SceneUtils ‚ÏÂÒÚÓ SceneBatchProps.SceneBatch_GetObjectsFromQuery
            var src = SceneUtils.GetObjectsFromQuery(args["query"]!);
            if (src.Length > max) src = src.Take(max).ToArray();

            int matched = src.Length;
            int wouldCreate = checked(matched * copiesPerObject);
            if (wouldCreate > maxCreated)
                return new { ok = false, error = $"Would create {wouldCreate} objects > maxCreated={maxCreated}" };

            Transform? forcedParent = null;
            if (parentMode == "set")
            {
                if (string.IsNullOrWhiteSpace(parentName))
                    throw new ArgumentException("args.parentName is required when parentMode='set'");

                // »«Ã≈Õ≈ÕŒ: SceneUtils ‚ÏÂÒÚÓ ÎÓÍ‡Î¸ÌÓ„Ó FindExactByName
                var pgo = SceneUtils.FindExactByName(parentName);
                if (pgo == null)
                {
                    if (dryRun) throw new InvalidOperationException($"Parent '{parentName}' not found.");
                    pgo = new GameObject(parentName);
                    Undo.RegisterCreatedObjectUndo(pgo, "Create Parent (DuplicateByQuery)");
                }
                forcedParent = pgo.transform;
            }

            int created = 0;
            var samples = new List<object>(10);

            string FormatName(string srcName, int i0, int i1)
                => nameTemplate
                    .Replace("{name}", srcName)
                    .Replace("{i}", i0.ToString())
                    .Replace("{i1}", i1.ToString());

            if (dryRun)
            {
                int idx = 0;
                foreach (var go in src)
                {
                    if (go == null) continue;
                    for (int c = 0; c < copiesPerObject; c++)
                    {
                        idx++;
                        created++;
                        if (samples.Count < 10)
                            samples.Add(new { source = go.name, newName = FormatName(go.name, idx - 1, idx) });
                    }
                }

                return new
                {
                    ok = true,
                    dryRun = true,
                    matchedObjects = matched,
                    copiesPerObject,
                    wouldCreate = created,
                    parentMode,
                    parentName = forcedParent != null ? forcedParent.name : null,
                    worldPositionOffset = new { x = offset.x, y = offset.y, z = offset.z },
                    samples
                };
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Orchestrator DuplicateByQuery");

            try
            {
                int idx = 0;

                foreach (var go in src)
                {
                    if (go == null) continue;

                    for (int c = 0; c < copiesPerObject; c++)
                    {
                        idx++;
                        var clone = UnityEngine.Object.Instantiate(go);
                        if (clone == null) continue;

                        Undo.RegisterCreatedObjectUndo(clone, "Duplicate GameObject");

                        if (parentMode == "same")
                            clone.transform.SetParent(go.transform.parent, worldPositionStays: true);
                        else if (parentMode == "keepworld")
                        {
                            // leave as is
                        }
                        else
                            clone.transform.SetParent(forcedParent, worldPositionStays: true);

                        if (offset != Vector3.zero)
                            clone.transform.position += offset;

                        clone.name = FormatName(go.name, idx - 1, idx);
                        created++;

                        if (samples.Count < 10)
                            samples.Add(new { source = go.name, newName = clone.name, id = clone.GetInstanceID() });

                        if (created >= maxCreated) break;
                    }

                    if (created >= maxCreated) break;
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }

            return new
            {
                ok = true,
                dryRun = false,
                matchedObjects = matched,
                copiesPerObject,
                createdObjects = created,
                parentMode,
                parentName = forcedParent != null ? forcedParent.name : null,
                worldPositionOffset = new { x = offset.x, y = offset.y, z = offset.z },
                samples
            };
        }

        // command: scene.batch.renameByQuery
        public static object RenameByQuery(JToken args)
        {
            if (args?["query"] == null) throw new ArgumentException("args.query is required");

            string template = ((string?)args?["template"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(template)) throw new ArgumentException("args.template is required");

            bool dryRun = (bool?)args?["dryRun"] ?? false;

            int startIndex = (int?)args?["startIndex"] ?? 1;
            int zeroPad = (int?)args?["zeroPad"] ?? 0;
            if (zeroPad < 0) zeroPad = 0;
            if (zeroPad > 10) zeroPad = 10;

            int max = (int?)args?["max"] ?? 5000;
            if (max < 1) max = 1;
            if (max > 20000) max = 20000;

            // »«Ã≈Õ≈ÕŒ: SceneUtils ‚ÏÂÒÚÓ SceneBatchProps.SceneBatch_GetObjectsFromQuery
            var gos = SceneUtils.GetObjectsFromQuery(args["query"]!);
            if (gos.Length > max) gos = gos.Take(max).ToArray();

            int matched = gos.Length;
            int changed = 0;
            int skipped = 0;

            var samples = new List<object>(10);

            string Pad(int n)
                => zeroPad <= 0 ? n.ToString() : n.ToString().PadLeft(zeroPad, '0');

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator RenameByQuery");

                try
                {
                    for (int idx = 0; idx < gos.Length; idx++)
                    {
                        var go = gos[idx];
                        if (go == null) { skipped++; continue; }

                        string oldName = go.name;
                        string newName = template
                            .Replace("{i}", Pad(idx))
                            .Replace("{i1}", Pad(startIndex + idx))
                            .Replace("{name}", oldName)
                            .Trim();

                        if (string.IsNullOrWhiteSpace(newName)) { skipped++; continue; }
                        if (newName == oldName) continue;

                        Undo.RecordObject(go, "Rename GameObject");
                        go.name = newName;
                        changed++;
                        EditorUtility.SetDirty(go);

                        if (samples.Count < 10)
                            samples.Add(new { oldName, newName, id = go.GetInstanceID() });
                    }
                }
                finally
                {
                    Undo.CollapseUndoOperations(group);
                }
            }
            else
            {
                for (int idx = 0; idx < gos.Length; idx++)
                {
                    var go = gos[idx];
                    if (go == null) { skipped++; continue; }

                    string oldName = go.name;
                    string newName = template
                        .Replace("{i}", Pad(idx))
                        .Replace("{i1}", Pad(startIndex + idx))
                        .Replace("{name}", oldName)
                        .Trim();

                    if (string.IsNullOrWhiteSpace(newName)) { skipped++; continue; }
                    if (newName != oldName) changed++;

                    if (samples.Count < 10)
                        samples.Add(new { oldName, newName, id = go.GetInstanceID() });
                }
            }

            return new
            {
                ok = true,
                dryRun,
                template,
                startIndex,
                zeroPad,
                matchedObjects = matched,
                changedObjects = changed,
                skippedObjects = skipped,
                samples
            };
        }

        // command: scene.batch.setParentByQuery
        public static object SetParentByQuery(JToken args)
        {
            if (args?["query"] == null) throw new ArgumentException("args.query is required");

            bool dryRun = (bool?)args?["dryRun"] ?? false;
            bool worldPositionStays = (bool?)args?["worldPositionStays"] ?? true;

            int max = (int?)args?["max"] ?? 5000;
            if (max < 1) max = 1;
            if (max > 20000) max = 20000;

            Transform parent = ResolveParent(args, dryRun);

            // »«Ã≈Õ≈ÕŒ: SceneUtils ‚ÏÂÒÚÓ SceneBatchProps.SceneBatch_GetObjectsFromQuery
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
                Undo.SetCurrentGroupName("Orchestrator SetParentByQuery");

                try
                {
                    foreach (var go in gos)
                    {
                        if (go == null) { skipped++; continue; }
                        var t = go.transform;
                        if (t == null) { skipped++; continue; }

                        if (t == parent) { skipped++; continue; }
                        if (t.parent == parent) continue;

                        Undo.RecordObject(t, "Set Parent");
                        t.SetParent(parent, worldPositionStays);
                        changed++;
                        EditorUtility.SetDirty(t);

                        if (samples.Count < 10)
                            samples.Add(new { name = go.name, id = go.GetInstanceID(), newParent = parent.name, worldPositionStays });
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
                    var t = go.transform;
                    if (t == null) { skipped++; continue; }

                    if (t.parent != parent) changed++;

                    if (samples.Count < 10)
                        samples.Add(new { name = go.name, id = go.GetInstanceID(), wouldParent = parent.name });
                }
            }

            return new
            {
                ok = true,
                dryRun,
                worldPositionStays,
                parent = new { name = parent.name, instanceId = parent.gameObject.GetInstanceID() },
                matchedObjects = matched,
                changedObjects = changed,
                skippedObjects = skipped,
                samples
            };
        }

        private static Transform ResolveParent(JToken args, bool dryRun)
        {
            int parentId = (int?)args?["parentInstanceId"] ?? 0;
            string parentName = ((string?)args?["parentName"] ?? "").Trim();
            bool createParentIfMissing = (bool?)args?["createParentIfMissing"] ?? true;

            if (parentId != 0 && !string.IsNullOrWhiteSpace(parentName))
                throw new ArgumentException("Provide either parentInstanceId OR parentName, not both.");

            if (parentId != 0)
            {
                var obj = EditorUtility.InstanceIDToObject(parentId);
                if (obj is GameObject go) return go.transform;
                if (obj is Transform t) return t;
                throw new ArgumentException("parentInstanceId must reference GameObject or Transform.");
            }

            if (!string.IsNullOrWhiteSpace(parentName))
            {
                // »«Ã≈Õ≈ÕŒ: SceneUtils ‚ÏÂÒÚÓ ÎÓÍ‡Î¸ÌÓ„Ó FindExactByName
                var existing = SceneUtils.FindExactByName(parentName);
                if (existing != null) return existing.transform;

                if (dryRun || !createParentIfMissing)
                    throw new InvalidOperationException($"Parent '{parentName}' not found.");

                var created = new GameObject(parentName);
                Undo.RegisterCreatedObjectUndo(created, "Create Parent");
                return created.transform;
            }

            throw new ArgumentException("Provide args.parentInstanceId OR args.parentName.");
        }

        // ”ƒ¿À≈Õ€: ÎÓÍ‡Î¸Ì˚Â ReadVec3 Ë FindExactByName ó ÚÂÔÂ¸ ËÒÔÓÎ¸ÁÛÈ SceneUtils
    }
}