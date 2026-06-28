using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class SceneBatch
    {
        // command: scene.batch.addComponent
        // args:
        // {
        //   "query": { ... как в scene.query ... },
        //   "componentType": "Rigidbody",
        //   "dryRun": false
        // }
        public static object AddComponent(JToken args)
        {
            bool dryRun = (bool?)args?["dryRun"] ?? false;

            var queryToken = args?["query"];
            if (queryToken == null) throw new ArgumentException("args.query is required");

            string typeName = ((string?)args?["componentType"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(typeName)) throw new ArgumentException("args.componentType is required");

            var type = SceneQuery.ResolveType(typeName);
            if (type == null || !typeof(Component).IsAssignableFrom(type))
                throw new InvalidOperationException($"Component type not found or not a Component: {typeName}");

            // ИЗМЕНЕНО: SceneUtils вместо локального GetObjectsFromQuery
            var gos = SceneUtils.GetObjectsFromQuery(queryToken);

            int matched = gos.Length;
            int added = 0;
            int skipped = 0;

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName($"Batch AddComponent {type.Name}");

                try
                {
                    foreach (var go in gos)
                    {
                        if (go == null) continue;

                        if (go.GetComponent(type) != null)
                        {
                            skipped++;
                            continue;
                        }

                        Undo.AddComponent(go, type);
                        added++;
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
                    if (go == null) continue;
                    if (go.GetComponent(type) != null) skipped++;
                    else added++;
                }
            }

            return new
            {
                ok = true,
                dryRun,
                componentType = type.FullName,
                matched,
                added,
                skipped
            };
        }

        // command: scene.batch.setComponentEnabled
        // args:
        // {
        //   "query": { ... },
        //   "componentType": "SortByY",
        //   "enabled": false,
        //   "dryRun": false
        // }
        public static object SetComponentEnabled(JToken args)
        {
            bool dryRun = (bool?)args?["dryRun"] ?? false;

            var queryToken = args?["query"];
            if (queryToken == null) throw new ArgumentException("args.query is required");

            string typeName = ((string?)args?["componentType"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(typeName)) throw new ArgumentException("args.componentType is required");

            bool enabled = (bool?)args?["enabled"] ?? true;

            var type = SceneQuery.ResolveType(typeName);
            if (type == null || !typeof(Behaviour).IsAssignableFrom(type))
                throw new InvalidOperationException($"Type must be a Behaviour (has .enabled): {typeName}");

            // ИЗМЕНЕНО: SceneUtils вместо локального GetObjectsFromQuery
            var gos = SceneUtils.GetObjectsFromQuery(queryToken);

            int matchedObjects = gos.Length;
            int matchedComponents = 0;
            int changed = 0;

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName($"Batch SetEnabled {type.Name}={enabled}");

                try
                {
                    foreach (var go in gos)
                    {
                        if (go == null) continue;

                        var comps = go.GetComponents(type).OfType<Behaviour>().ToArray();
                        if (comps.Length == 0) continue;

                        matchedComponents += comps.Length;

                        foreach (var b in comps)
                        {
                            if (b.enabled == enabled) continue;
                            Undo.RecordObject(b, "Set Behaviour Enabled");
                            b.enabled = enabled;
                            EditorUtility.SetDirty(b);
                            changed++;
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
                    if (go == null) continue;

                    var comps = go.GetComponents(type).OfType<Behaviour>().ToArray();
                    matchedComponents += comps.Length;

                    foreach (var b in comps)
                        if (b.enabled != enabled) changed++;
                }
            }

            return new
            {
                ok = true,
                dryRun,
                componentType = type.FullName,
                enabled,
                matchedObjects,
                matchedComponents,
                changed
            };
        }

        // УДАЛЁН: приватный GetObjectsFromQuery — теперь используй SceneUtils.GetObjectsFromQuery
    }
}