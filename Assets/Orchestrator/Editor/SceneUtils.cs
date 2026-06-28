using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    /// <summary>
    /// Общие утилиты. Заменяет дублирующийся код из ~16 файлов.
    /// </summary>
    public static class SceneUtils
    {
        /// <summary>Возвращает полный путь объекта в иерархии: "Root/Parent/Child"</summary>
        public static string GetHierarchyPath(Transform t)
        {
            if (t == null) return "";
            var stack = new Stack<string>(16);
            while (t != null)
            {
                stack.Push(t.name);
                t = t.parent;
            }
            return string.Join("/", stack);
        }

        /// <summary>
        /// Проверяет содержит ли имя объекта или любого его родителя подстроку needle.
        /// </summary>
        public static bool NameInHierarchyContains(Transform t, string needle)
        {
            while (t != null)
            {
                if (t.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                t = t.parent;
            }
            return false;
        }

        /// <summary>
        /// Находит GameObject'ы в сцене по query-объекту.
        /// Поддерживает: nameContains, caseSensitive, tag, layer, hasComponent, includeInactive, max.
        /// </summary>
        public static GameObject[] GetObjectsFromQuery(JToken query)
        {
            string nameContains = ((string?)query?["nameContains"] ?? "").Trim();
            bool caseSensitive = (bool?)query?["caseSensitive"] ?? false;
            string? tag = ((string?)query?["tag"])?.Trim();
            int? layer = (int?)query?["layer"];
            string? hasComp = ((string?)query?["hasComponent"])?.Trim();
            bool includeInactive = (bool?)query?["includeInactive"] ?? true;
            int max = (int?)query?["max"] ?? 200;
            if (max < 1) max = 1;
            if (max > 5000) max = 5000;

            StringComparison cmp = caseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            Type? requiredType = null;
            if (!string.IsNullOrWhiteSpace(hasComp))
                requiredType = ResolveType(hasComp!);

            var gos = UnityEngine.Object.FindObjectsByType<GameObject>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            var result = new List<GameObject>(Math.Min(max, 256));

            foreach (var go in gos)
            {
                if (go == null) continue;

                if (!string.IsNullOrWhiteSpace(nameContains) &&
                    go.name.IndexOf(nameContains, cmp) < 0)
                    continue;

                if (!string.IsNullOrWhiteSpace(tag))
                {
                    try { if (!go.CompareTag(tag)) continue; }
                    catch { continue; }
                }

                if (layer.HasValue && go.layer != layer.Value)
                    continue;

                if (requiredType != null && go.GetComponent(requiredType) == null)
                    continue;

                result.Add(go);
                if (result.Count >= max) break;
            }

            return result.ToArray();
        }

        /// <summary>Читает Vector3 из JToken {x,y,z}. Возвращает def если token null.</summary>
        public static Vector3 ReadVec3(JToken? token, Vector3 def)
        {
            if (token == null || token.Type == JTokenType.Null)
                return def;
            float x = (float?)token["x"] ?? def.x;
            float y = (float?)token["y"] ?? def.y;
            float z = (float?)token["z"] ?? def.z;
            return new Vector3(x, y, z);
        }

        /// <summary>Конвертирует Vector3 в анонимный объект {x,y,z} для JSON.</summary>
        public static object Vec3ToObj(Vector3 v) => new { x = v.x, y = v.y, z = v.z };

        /// <summary>Находит первый GameObject с точным совпадением имени (включая inactive).</summary>
        public static GameObject? FindExactByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var all = UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in all)
                if (go != null && go.name == name) return go;
            return null;
        }

        /// <summary>Возвращает глубину объекта в иерархии (0 = корень).</summary>
        public static int GetDepth(Transform t)
        {
            int d = 0;
            while (t != null && t.parent != null) { d++; t = t.parent; }
            return d;
        }

        /// <summary>
        /// Резолвит тип по имени: полное имя, короткое имя (Rigidbody), или namespace.Name.
        /// Дублирует SceneQuery.ResolveType чтобы не зависеть от internal метода.
        /// </summary>
        public static Type? ResolveType(string typeName)
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

                    t = System.Linq.Enumerable.FirstOrDefault(types,
                        x => x.Name.Equals(typeName, StringComparison.Ordinal));
                    if (t != null) break;
                }
            }

            return t;
        }
    }
}