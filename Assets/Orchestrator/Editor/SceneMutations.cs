using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class SceneMutations
    {
        public static object CreateEmpty(JToken args)
        {
            string name = (string?)args?["name"] ?? "New Empty";
            int parentId = (int?)args?["parentInstanceId"] ?? 0;

            float x = (float?)args?["position"]?["x"] ?? 0f;
            float y = (float?)args?["position"]?["y"] ?? 0f;
            float z = (float?)args?["position"]?["z"] ?? 0f;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Orchestrator Create Empty");

            if (parentId != 0)
            {
                var parentObj = EditorUtility.InstanceIDToObject(parentId) as GameObject;
                if (parentObj != null)
                    go.transform.SetParent(parentObj.transform, worldPositionStays: true);
            }

            go.transform.position = new Vector3(x, y, z);

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);

            return new { created = new { name = go.name, instanceId = go.GetInstanceID() } };
        }

        public static object AddPrefab(JToken args)
        {
            string assetPath = (string?)args?["assetPath"] ?? "";
            string guid = (string?)args?["guid"] ?? "";

            if (string.IsNullOrWhiteSpace(assetPath))
            {
                if (string.IsNullOrWhiteSpace(guid))
                    throw new ArgumentException("args.assetPath or args.guid is required");
                assetPath = AssetDatabase.GUIDToAssetPath(guid);
            }

            if (string.IsNullOrWhiteSpace(assetPath) || !File.Exists(assetPath))
                throw new FileNotFoundException($"Prefab not found at path: {assetPath}");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
                throw new InvalidOperationException($"Asset is not a prefab GameObject: {assetPath}");

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null)
                throw new InvalidOperationException("Failed to instantiate prefab");

            Undo.RegisterCreatedObjectUndo(instance, "Orchestrator Add Prefab");

            int parentId = (int?)args?["parentInstanceId"] ?? 0;
            if (parentId != 0)
            {
                var parentObj = EditorUtility.InstanceIDToObject(parentId) as GameObject;
                if (parentObj != null)
                    instance.transform.SetParent(parentObj.transform, worldPositionStays: true);
            }

            string nameOverride = (string?)args?["nameOverride"] ?? "";
            if (!string.IsNullOrWhiteSpace(nameOverride))
                instance.name = nameOverride;

            // »«Ã≈Õ≈ÕŒ: SceneUtils.ReadVec3 ‚ÏÂÒÚÓ inline Ô‡ÒËÌ„‡
            var p = args?["position"];
            if (p != null && p.Type == JTokenType.Object)
                instance.transform.position = SceneUtils.ReadVec3(p, instance.transform.position);

            var r = args?["rotationEuler"];
            if (r != null && r.Type == JTokenType.Object)
                instance.transform.eulerAngles = SceneUtils.ReadVec3(r, instance.transform.eulerAngles);

            var s = args?["scale"];
            if (s != null && s.Type == JTokenType.Object)
                instance.transform.localScale = SceneUtils.ReadVec3(s, instance.transform.localScale);

            Selection.activeGameObject = instance;
            EditorGUIUtility.PingObject(instance);

            return new { created = new { name = instance.name, instanceId = instance.GetInstanceID(), assetPath } };
        }

        public static object SetTransform(JToken args)
        {
            int instanceId = (int?)args?["instanceId"] ?? 0;
            if (instanceId == 0) throw new ArgumentException("args.instanceId is required");

            var obj = EditorUtility.InstanceIDToObject(instanceId);
            if (obj == null) throw new InvalidOperationException($"No object for instanceId={instanceId}");

            GameObject go = obj as GameObject;
            if (go == null && obj is Component c) go = c.gameObject;
            if (go == null) throw new InvalidOperationException("instanceId is not a GameObject/Component");

            Undo.RecordObject(go.transform, "Orchestrator Set Transform");

            // »«Ã≈Õ≈ÕŒ: SceneUtils.ReadVec3 ‚ÏÂÒÚÓ inline Ô‡ÒËÌ„‡
            var p = args?["position"];
            if (p != null && p.Type == JTokenType.Object)
                go.transform.position = SceneUtils.ReadVec3(p, go.transform.position);

            var r = args?["rotationEuler"];
            if (r != null && r.Type == JTokenType.Object)
                go.transform.eulerAngles = SceneUtils.ReadVec3(r, go.transform.eulerAngles);

            var s = args?["scale"];
            if (s != null && s.Type == JTokenType.Object)
                go.transform.localScale = SceneUtils.ReadVec3(s, go.transform.localScale);

            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);

            return new
            {
                updated = new
                {
                    name = go.name,
                    instanceId = go.GetInstanceID(),
                    position = new { x = go.transform.position.x, y = go.transform.position.y, z = go.transform.position.z },
                    rotationEuler = new { x = go.transform.eulerAngles.x, y = go.transform.eulerAngles.y, z = go.transform.eulerAngles.z },
                    scale = new { x = go.transform.localScale.x, y = go.transform.localScale.y, z = go.transform.localScale.z }
                }
            };
        }
    }
}