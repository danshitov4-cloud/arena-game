using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class PrefabInstantiateCommands
    {
        // command: prefab.instantiate
        public static object Instantiate(JToken args)
        {
            var o = args as JObject;
            if (o == null) throw new ArgumentException("args must be an object");

            string assetPath = ((string?)o["assetPath"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("args.assetPath is required (e.g. Assets/Prefabs/X.prefab)");

            bool dryRun = (bool?)o["dryRun"] ?? false;
            string name = ((string?)o["name"] ?? "").Trim();
            bool select = (bool?)o["select"] ?? false;
            bool worldPositionStays = (bool?)o["worldPositionStays"] ?? true;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
                return new { ok = false, error = $"Prefab not found at assetPath: {assetPath}", assetPath };

            Transform? parent = null;
            if (!dryRun)
                parent = ResolveParent(o, createIfMissingAllowed: true);

            bool hasPos = o["position"] is JObject;
            bool hasRot = o["rotationEuler"] is JObject;
            bool hasScale = o["scale"] is JObject;

            // ИЗМЕНЕНО: SceneUtils вместо локального ReadVec3
            Vector3 pos = SceneUtils.ReadVec3(o["position"], Vector3.zero);
            Vector3 rotEuler = SceneUtils.ReadVec3(o["rotationEuler"], Vector3.zero);
            Vector3 scale = SceneUtils.ReadVec3(o["scale"], Vector3.one);

            if (dryRun)
            {
                return new
                {
                    ok = true,
                    dryRun = true,
                    assetPath,
                    prefabName = prefab.name,
                    wouldCreate = new
                    {
                        name = string.IsNullOrWhiteSpace(name) ? prefab.name : name,
                        parent = o["parentInstanceId"] != null || o["parentName"] != null ? "provided" : null,
                        // ИЗМЕНЕНО: SceneUtils.Vec3ToObj вместо локального ToObj
                        position = hasPos ? SceneUtils.Vec3ToObj(pos) : null,
                        rotationEuler = hasRot ? SceneUtils.Vec3ToObj(rotEuler) : null,
                        scale = hasScale ? SceneUtils.Vec3ToObj(scale) : null,
                        select
                    }
                };
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Orchestrator Prefab Instantiate");

            GameObject? instance = null;

            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null)
                    return new { ok = false, error = "InstantiatePrefab returned null", assetPath };

                Undo.RegisterCreatedObjectUndo(instance, "Instantiate Prefab");

                if (parent != null)
                    instance.transform.SetParent(parent, worldPositionStays);

                if (!string.IsNullOrWhiteSpace(name))
                    instance.name = name;

                if (hasPos) instance.transform.position = pos;
                if (hasRot) instance.transform.rotation = Quaternion.Euler(rotEuler);
                if (hasScale) instance.transform.localScale = scale;

                EditorUtility.SetDirty(instance);

                if (select)
                    Selection.activeGameObject = instance;
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }

            return new
            {
                ok = true,
                dryRun = false,
                created = new
                {
                    name = instance != null ? instance.name : "",
                    instanceId = instance != null ? instance.GetInstanceID() : 0,
                    assetPath,
                    parent = parent != null ? parent.name : null,
                    position = instance != null ? SceneUtils.Vec3ToObj(instance.transform.position) : null,
                    rotationEuler = instance != null ? SceneUtils.Vec3ToObj(instance.transform.rotation.eulerAngles) : null,
                    scale = instance != null ? SceneUtils.Vec3ToObj(instance.transform.localScale) : null
                }
            };
        }

        private static Transform? ResolveParent(JObject o, bool createIfMissingAllowed)
        {
            int parentId = (int?)o["parentInstanceId"] ?? 0;
            string parentName = ((string?)o["parentName"] ?? "").Trim();
            bool createParentIfMissing = (bool?)o["createParentIfMissing"] ?? true;

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
                // ИЗМЕНЕНО: SceneUtils вместо локального FindExactByName
                var existing = SceneUtils.FindExactByName(parentName);
                if (existing != null) return existing.transform;

                if (!createIfMissingAllowed || !createParentIfMissing)
                    throw new InvalidOperationException($"Parent '{parentName}' not found.");

                var created = new GameObject(parentName);
                Undo.RegisterCreatedObjectUndo(created, "Create Parent (prefab.instantiate)");
                return created.transform;
            }

            return null;
        }

        // УДАЛЕНЫ: локальные FindExactByName, ReadVec3, ToObj — теперь используй SceneUtils
    }
}