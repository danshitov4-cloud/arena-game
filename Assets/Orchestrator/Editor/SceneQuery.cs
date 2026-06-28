using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class SceneQuery
    {
        public static object Query(JToken args)
        {
            var q = ParseArgs(args);
            var found = Find(q);

            return new
            {
                ok = true,
                query = q,
                count = found.Count,
                items = found.Select(x => new
                {
                    name = x.name,
                    instanceId = x.GetInstanceID(),
                    // ИЗМЕНЕНО: SceneUtils вместо локального GetPath
                    path = SceneUtils.GetHierarchyPath(x.transform),
                    activeSelf = x.activeSelf,
                    activeInHierarchy = x.activeInHierarchy,
                    tag = x.tag,
                    layer = x.layer
                }).ToArray()
            };
        }

        public static object SelectByQuery(JToken args)
        {
            var q = ParseArgs(args);
            var found = Find(q);

            Selection.objects = found.Cast<UnityEngine.Object>().ToArray();

            return new
            {
                ok = true,
                query = q,
                selectedCount = found.Count,
                selected = found.Select(x => new
                {
                    name = x.name,
                    instanceId = x.GetInstanceID(),
                    path = SceneUtils.GetHierarchyPath(x.transform)
                }).ToArray()
            };
        }

        // ---------- internals ----------

        private sealed class QueryArgs
        {
            public string nameContains = "";
            public bool caseSensitive;
            public string? tag;
            public int? layer;
            public string? hasComponent;
            public bool includeInactive = true;
            public int max = 200;
        }

        private static QueryArgs ParseArgs(JToken args)
        {
            var q = new QueryArgs
            {
                nameContains = ((string?)args?["nameContains"] ?? "").Trim(),
                caseSensitive = (bool?)args?["caseSensitive"] ?? false,
                tag = ((string?)args?["tag"])?.Trim(),
                layer = (int?)args?["layer"],
                hasComponent = ((string?)args?["hasComponent"])?.Trim(),
                includeInactive = (bool?)args?["includeInactive"] ?? true,
                max = (int?)args?["max"] ?? 200
            };

            if (q.max < 1) q.max = 1;
            if (q.max > 5000) q.max = 5000;

            return q;
        }

        private static List<GameObject> Find(QueryArgs q)
        {
            var gos = UnityEngine.Object.FindObjectsByType<GameObject>(
                q.includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            StringComparison cmp = q.caseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            Type? requiredType = null;
            if (!string.IsNullOrWhiteSpace(q.hasComponent))
                requiredType = ResolveType(q.hasComponent!);

            var result = new List<GameObject>(Math.Min(q.max, 256));

            foreach (var go in gos)
            {
                if (go == null) continue;

                if (!string.IsNullOrWhiteSpace(q.nameContains) &&
                    go.name.IndexOf(q.nameContains, cmp) < 0)
                    continue;

                if (!string.IsNullOrWhiteSpace(q.tag))
                {
                    try { if (!go.CompareTag(q.tag)) continue; }
                    catch { continue; }
                }

                if (q.layer.HasValue && go.layer != q.layer.Value)
                    continue;

                if (requiredType != null && go.GetComponent(requiredType) == null)
                    continue;

                result.Add(go);
                if (result.Count >= q.max) break;
            }

            return result;
        }

        internal static Type? ResolveType(string typeName)
        {
            var t = Type.GetType(typeName, throwOnError: false);

            if (t == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = asm.GetType(typeName, throwOnError: false);
                    if (t != null) break;
                }
            }

            if (t == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch { continue; }

                    t = types.FirstOrDefault(x => x.Name.Equals(typeName, StringComparison.Ordinal));
                    if (t != null) break;
                }
            }

            return t;
        }

        // УДАЛЁН: локальный GetPath — теперь используй SceneUtils.GetHierarchyPath
    }
}