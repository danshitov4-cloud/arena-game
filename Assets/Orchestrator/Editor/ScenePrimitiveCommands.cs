using System;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class ScenePrimitiveCommands
    {
        // command: scene.createPrimitive
        // args:
        // {
        //   "name": "MyObject",
        //   "primitiveType": "Cube",        // Cube | Sphere | Capsule | Cylinder | Plane | Quad
        //   "position": {"x":0,"y":0,"z":0},
        //   "scale":    {"x":1,"y":1,"z":1},
        //   "color":    {"r":1,"g":0,"b":0,"a":1},  // optional
        //   "dryRun": false
        // }
        public static object CreatePrimitive(JToken args)
        {
            bool dryRun = (bool?)args?["dryRun"] ?? false;

            string name = ((string?)args?["name"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("args.name is required");

            string typeStr = ((string?)args?["primitiveType"] ?? "Cube").Trim();
            if (!Enum.TryParse<PrimitiveType>(typeStr, true, out var primitiveType))
                throw new ArgumentException($"Unknown primitiveType: {typeStr}. Use: Cube, Sphere, Capsule, Cylinder, Plane, Quad");

            if (dryRun)
            {
                return new { ok = true, dryRun = true, name, primitiveType = typeStr };
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Orchestrator CreatePrimitive");

            var go = GameObject.CreatePrimitive(primitiveType);
            go.name = name;
            Undo.RegisterCreatedObjectUndo(go, "CreatePrimitive");

            // position
            var posToken = args?["position"];
            if (posToken != null)
            {
                go.transform.position = new Vector3(
                    (float?)posToken["x"] ?? 0f,
                    (float?)posToken["y"] ?? 0f,
                    (float?)posToken["z"] ?? 0f);
            }

            // scale
            var scaleToken = args?["scale"];
            if (scaleToken != null)
            {
                go.transform.localScale = new Vector3(
                    (float?)scaleToken["x"] ?? 1f,
                    (float?)scaleToken["y"] ?? 1f,
                    (float?)scaleToken["z"] ?? 1f);
            }

            // color — instanced material so it doesn't affect other objects
            var colorToken = args?["color"];
            if (colorToken != null)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(renderer.sharedMaterial)
                    {
                        color = new Color(
                            (float?)colorToken["r"] ?? 1f,
                            (float?)colorToken["g"] ?? 1f,
                            (float?)colorToken["b"] ?? 1f,
                            (float?)colorToken["a"] ?? 1f)
                    };
                    renderer.material = mat;
                }
            }

            EditorUtility.SetDirty(go);

            return new
            {
                ok = true,
                dryRun = false,
                name = go.name,
                instanceId = go.GetInstanceID(),
                primitiveType = typeStr,
                position = new { x = go.transform.position.x, y = go.transform.position.y, z = go.transform.position.z },
                scale = new { x = go.transform.localScale.x, y = go.transform.localScale.y, z = go.transform.localScale.z }
            };
        }

        // command: scene.savePrefab
        // args:
        // {
        //   "query": { "nameContains": "TriangleEnemy" },   // finds scene object
        //   "path": "Assets/Prefabs/TriangleEnemy.prefab",  // save path
        //   "removeFromScene": true                         // optional, default true
        //   "dryRun": false
        // }
        public static object SavePrefab(JToken args)
        {
            bool dryRun = (bool?)args?["dryRun"] ?? false;

            string path = ((string?)args?["path"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("args.path is required");
            if (!path.EndsWith(".prefab")) path += ".prefab";

            if (args?["query"] == null) throw new ArgumentException("args.query is required");

            bool removeFromScene = (bool?)args?["removeFromScene"] ?? true;

            var gos = SceneUtils.GetObjectsFromQuery(args["query"]!);
            if (gos.Length == 0) return new { ok = false, error = "No objects matched query" };

            var go = gos[0];
            if (go == null) return new { ok = false, error = "Matched object is null" };

            if (dryRun)
            {
                return new { ok = true, dryRun = true, name = go.name, path, removeFromScene };
            }

            // Ensure directory exists
            string dir = System.IO.Path.GetDirectoryName(path)!;
            if (!AssetDatabase.IsValidFolder(dir))
            {
                string[] parts = dir.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            bool saved = prefab != null;

            if (removeFromScene)
                Undo.DestroyObjectImmediate(go);

            AssetDatabase.SaveAssets();

            return new
            {
                ok = saved,
                dryRun = false,
                savedTo = path,
                prefabName = prefab != null ? prefab.name : null,
                removedFromScene = removeFromScene && saved
            };
        }
    }
}
