using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class PrefabReplaceCommands
    {
        // command: prefab.replaceByQuery
        public static object ReplaceByQuery(JToken args)
        {
            var o = args as JObject ?? new JObject();

            if (o["query"] == null) throw new ArgumentException("args.query is required");

            string prefabAssetPath = ((string?)o["prefabAssetPath"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(prefabAssetPath))
                throw new ArgumentException("args.prefabAssetPath is required (e.g. Assets/Prefabs/X.prefab)");

            bool keepTransform = (bool?)o["keepTransform"] ?? true;
            bool keepName = (bool?)o["keepName"] ?? true;
            bool keepParent = (bool?)o["keepParent"] ?? true;
            bool deleteOriginal = (bool?)o["deleteOriginal"] ?? true;
            bool dryRun = (bool?)o["dryRun"] ?? false;

            int max = (int?)o["max"] ?? 2000;
            if (max < 1) max = 1;
            if (max > 20000) max = 20000;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
            if (prefab == null)
                return new { ok = false, error = $"Prefab not found at path: {prefabAssetPath}" };

            // ÈÇÌÅÍÅÍÎ: SceneUtils âìåñòî SceneBatchProps.SceneBatch_GetObjectsFromQuery
            var targets = SceneUtils.GetObjectsFromQuery(o["query"]);
            if (targets.Length > max) targets = targets.Take(max).ToArray();

            int matched = targets.Length;
            int replaced = 0;
            int skipped = 0;

            var samples = new List<object>();

            if (dryRun)
            {
                foreach (var go in targets)
                {
                    if (go == null) { skipped++; continue; }
                    replaced++;
                    if (samples.Count < 10)
                        samples.Add(new
                        {
                            oldName = go.name,
                            oldInstanceId = go.GetInstanceID(),
                            // ÈÇÌÅÍÅÍÎ: SceneUtils âìåñòî ëîêàëüíîãî GetHierarchyPath
                            path = SceneUtils.GetHierarchyPath(go.transform),
                            wouldCreatePrefab = prefabAssetPath
                        });
                }

                return new
                {
                    ok = true,
                    dryRun = true,
                    prefabAssetPath,
                    matchedObjects = matched,
                    wouldReplace = replaced,
                    skippedObjects = skipped,
                    samples
                };
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Orchestrator Prefab ReplaceByQuery");

            try
            {
                foreach (var oldGo in targets)
                {
                    if (oldGo == null) { skipped++; continue; }
                    var oldT = oldGo.transform;
                    if (oldT == null) { skipped++; continue; }

                    Transform parent = keepParent ? oldT.parent : null;
                    Vector3 pos = oldT.position;
                    Quaternion rot = oldT.rotation;
                    Vector3 scale = oldT.localScale;

                    var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    if (instance == null) { skipped++; continue; }

                    Undo.RegisterCreatedObjectUndo(instance, "Create Replacement Prefab");

                    if (parent != null)
                        instance.transform.SetParent(parent, worldPositionStays: true);

                    if (keepTransform)
                    {
                        instance.transform.position = pos;
                        instance.transform.rotation = rot;
                        instance.transform.localScale = scale;
                    }

                    if (keepName)
                        instance.name = oldGo.name;

                    if (deleteOriginal)
                        Undo.DestroyObjectImmediate(oldGo);

                    replaced++;

                    if (samples.Count < 10)
                        samples.Add(new
                        {
                            oldName = oldGo.name,
                            newName = instance.name,
                            newInstanceId = instance.GetInstanceID(),
                            parent = parent != null ? parent.name : null
                        });
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
                prefabAssetPath,
                matchedObjects = matched,
                replacedObjects = replaced,
                skippedObjects = skipped,
                deleteOriginal,
                keepTransform,
                keepName,
                keepParent,
                samples
            };
        }

        // ÓÄÀË¨Í: ëîêàëüíûé GetHierarchyPath — òåïåðü èñïîëüçóé SceneUtils.GetHierarchyPath
    }
}