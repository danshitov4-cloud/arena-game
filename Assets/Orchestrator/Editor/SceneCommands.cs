using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class SceneCommands
    {
        public static object Find(JToken args)
        {
            string nameContains = (string?)args?["nameContains"] ?? "";
            bool caseSensitive = (bool?)args?["caseSensitive"] ?? false;
            int max = (int?)args?["max"] ?? 50;

            if (string.IsNullOrWhiteSpace(nameContains))
                throw new ArgumentException("args.nameContains is required");

            var comp = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            var all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            var matches = all
                .Where(go => go != null && go.name?.IndexOf(nameContains, comp) >= 0)
                .Take(max)
                .Select(go => new
                {
                    name = go.name,
                    instanceId = go.GetInstanceID(),
                    // ИЗМЕНЕНО: SceneUtils вместо локального GetHierarchyPath
                    path = SceneUtils.GetHierarchyPath(go.transform)
                })
                .ToArray();

            return new
            {
                query = new { nameContains, caseSensitive, max },
                count = matches.Length,
                items = matches
            };
        }

        public static object Select(JToken args)
        {
            int instanceId = (int?)args?["instanceId"] ?? 0;
            bool ping = (bool?)args?["ping"] ?? true;

            if (instanceId == 0)
                throw new ArgumentException("args.instanceId is required");

            var obj = EditorUtility.InstanceIDToObject(instanceId);
            if (obj == null)
                throw new InvalidOperationException($"No object for instanceId={instanceId}");

            Selection.activeObject = obj;
            if (ping) EditorGUIUtility.PingObject(obj);

            return new
            {
                selected = new
                {
                    instanceId,
                    name = obj.name,
                    type = obj.GetType().Name
                }
            };
        }

        public static object GetComponents(JToken args)
        {
            int instanceId = (int?)args?["instanceId"] ?? 0;
            if (instanceId == 0)
                throw new ArgumentException("args.instanceId is required");

            var obj = EditorUtility.InstanceIDToObject(instanceId);

            GameObject go = obj as GameObject;
            if (go == null && obj is Component c) go = c.gameObject;
            if (go == null) throw new InvalidOperationException($"No GameObject for instanceId={instanceId}");

            var comps = go.GetComponents<Component>();

            var items = comps.Select(comp =>
            {
                if (comp == null)
                {
                    return new
                    {
                        type = "<Missing Script>",
                        componentInstanceId = 0,
                        props = (object?)null
                    };
                }

                return new
                {
                    type = comp.GetType().FullName,
                    componentInstanceId = comp.GetInstanceID(),
                    props = (object?)GetKnownProps(comp)
                };
            }).ToArray();

            return new
            {
                target = new { name = go.name, instanceId = go.GetInstanceID() },
                componentCount = comps.Length,
                components = items
            };
        }

        private static object? GetKnownProps(Component comp)
        {
            if (comp is Transform t)
            {
                var p = t.position;
                var r = t.eulerAngles;
                var s = t.localScale;
                return new
                {
                    position = new { x = p.x, y = p.y, z = p.z },
                    rotationEuler = new { x = r.x, y = r.y, z = r.z },
                    scale = new { x = s.x, y = s.y, z = s.z }
                };
            }

            if (comp is Behaviour b)
                return new { enabled = b.enabled };

            if (comp is Renderer rend)
                return new
                {
                    enabled = rend.enabled,
                    shadowCastingMode = rend.shadowCastingMode.ToString(),
                    receiveShadows = rend.receiveShadows,
                    materialCount = rend.sharedMaterials?.Length ?? 0
                };

            if (comp is Light light)
                return new
                {
                    enabled = light.enabled,
                    type = light.type.ToString(),
                    intensity = light.intensity,
                    shadows = light.shadows.ToString()
                };

            if (comp is Collider col)
                return new { enabled = col.enabled, isTrigger = col.isTrigger };

            if (comp is Rigidbody rb)
                return new { isKinematic = rb.isKinematic, useGravity = rb.useGravity, mass = rb.mass };

            return null;
        }

        // УДАЛЁН: локальный GetHierarchyPath — теперь используй SceneUtils.GetHierarchyPath
    }
}