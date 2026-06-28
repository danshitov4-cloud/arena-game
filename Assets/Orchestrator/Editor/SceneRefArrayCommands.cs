using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class SceneRefArrayCommands
    {
        // command: scene.ref.setArrayFieldByQuery
        // args:
        // {
        //   "query": {...},                        // объекты, на которых меняем поле
        //   "componentType": "MyNamespace.MyComp", // или "UnityEngine.Transform" не нужно, нужен компонент
        //   "fieldName": "waypoints",             // поле или свойство
        //   "targets": {                          // чем заполняем
        //      "by": "query" | "paths" | "instanceIds",
        //      "query": {...},                    // если by=query
        //      "paths": ["Root/A","Root/B"],      // если by=paths
        //      "instanceIds": [123,456]           // если by=instanceIds
        //   },
        //   "allowDuplicates": false,             // default false
        //   "dryRun": false
        // }
        public static object SetArrayFieldByQuery(JToken args)
        {
            var o = args as JObject ?? new JObject();

            if (o["query"] == null) throw new ArgumentException("args.query is required");
            string componentTypeName = ((string?)o["componentType"] ?? "").Trim();
            string fieldName = ((string?)o["fieldName"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(componentTypeName)) throw new ArgumentException("args.componentType is required");
            if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentException("args.fieldName is required");

            bool dryRun = (bool?)o["dryRun"] ?? false;
            bool allowDuplicates = (bool?)o["allowDuplicates"] ?? false;

            var targetsSpec = o["targets"] as JObject;
            if (targetsSpec == null) throw new ArgumentException("args.targets is required");

            string by = ((string?)targetsSpec["by"] ?? "query").Trim().ToLowerInvariant();

            // 1) Находим объекты, которые будем менять
            var receiverGos = SceneUtils.GetObjectsFromQuery(o["query"]);

            // 2) Находим объекты-источники ссылок
            var sourceGos = ResolveSourceObjects(targetsSpec, by);

            // 3) Получаем тип компонента, на котором ищем поле
            var compType = ResolveType(componentTypeName);
            if (compType == null)
                return new { ok = false, error = $"Cannot resolve componentType: {componentTypeName}" };

            int matchedReceivers = receiverGos.Length;
            int changed = 0;
            int skipped = 0;

            // образцы
            var samples = new List<object>();

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator Set Array Field By Query");

                try
                {
                    foreach (var go in receiverGos)
                    {
                        if (go == null) { skipped++; continue; }

                        var comp = go.GetComponent(compType);
                        if (comp == null) { skipped++; continue; }

                        if (!TrySet(comp, fieldName, sourceGos, allowDuplicates, out string? err, dryRun: false))
                        {
                            skipped++;
                            if (samples.Count < 10)
                                samples.Add(new { receiver = go.name, error = err ?? "set failed" });
                            continue;
                        }

                        changed++;
                        EditorUtility.SetDirty(comp);

                        if (samples.Count < 10)
                            samples.Add(new
                            {
                                receiver = go.name,
                                receiverId = go.GetInstanceID(),
                                componentType = compType.FullName,
                                fieldName,
                                sourcesCount = sourceGos.Length
                            });
                    }
                }
                finally
                {
                    Undo.CollapseUndoOperations(group);
                }
            }
            else
            {
                foreach (var go in receiverGos)
                {
                    if (go == null) { skipped++; continue; }
                    var comp = go.GetComponent(compType);
                    if (comp == null) { skipped++; continue; }

                    if (TrySet(comp, fieldName, sourceGos, allowDuplicates, out _, dryRun: true))
                        changed++;
                }
            }

            return new
            {
                ok = true,
                dryRun,
                componentType = compType.FullName,
                fieldName,
                receivers = new { matched = matchedReceivers, changed, skipped },
                sources = new
                {
                    by,
                    count = sourceGos.Length,
                    sample = sourceGos.Take(10).Select(g => g != null ? g.name : "null").ToArray()
                },
                samples
            };
        }

        // ---------- helpers ----------

        private static GameObject[] ResolveSourceObjects(JObject targetsSpec, string by)
        {
            if (by == "query")
            {
                if (targetsSpec["query"] == null) throw new ArgumentException("targets.query is required when targets.by=query");
                return SceneUtils.GetObjectsFromQuery(targetsSpec["query"]);
            }

            if (by == "paths")
            {
                if (targetsSpec["paths"] is not JArray arr) throw new ArgumentException("targets.paths must be array when by=paths");
                var list = new List<GameObject>();
                foreach (var t in arr)
                {
                    var p = ((string?)t ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(p)) continue;
                    var go = FindByPath(p);
                    if (go != null) list.Add(go);
                }
                return list.ToArray();
            }

            if (by == "instanceids")
            {
                if (targetsSpec["instanceIds"] is not JArray arr) throw new ArgumentException("targets.instanceIds must be array when by=instanceIds");
                var list = new List<GameObject>();
                foreach (var t in arr)
                {
                    int id = (int?)t ?? 0;
                    if (id == 0) continue;
                    var obj = EditorUtility.InstanceIDToObject(id);
                    if (obj is GameObject g) list.Add(g);
                    else if (obj is Component c && c != null) list.Add(c.gameObject);
                    else if (obj is Transform tr && tr != null) list.Add(tr.gameObject);
                }
                return list.ToArray();
            }

            throw new ArgumentException("targets.by must be query|paths|instanceIds");
        }

        private static bool TrySet(Component comp, string fieldName, GameObject[] sourceGos, bool allowDuplicates, out string? error, bool dryRun)
        {
            error = null;

            var t = comp.GetType();

            // поле?
            var fi = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fi != null)
                return TryAssignToMember(comp, fi.FieldType, v => { if (!dryRun) { Undo.RecordObject(comp, "Set field"); fi.SetValue(comp, v); } }, sourceGos, allowDuplicates, out error);

            // свойство?
            var pi = t.GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pi != null && pi.CanWrite)
                return TryAssignToMember(comp, pi.PropertyType, v => { if (!dryRun) { Undo.RecordObject(comp, "Set property"); pi.SetValue(comp, v); } }, sourceGos, allowDuplicates, out error);

            error = $"Field/Property not found: {fieldName}";
            return false;
        }

        private static bool TryAssignToMember(Component comp, Type memberType, Action<object?> assign, GameObject[] sourceGos, bool allowDuplicates, out string? error)
        {
            error = null;

            // 1) Массив?
            if (memberType.IsArray)
            {
                var elemType = memberType.GetElementType();
                if (elemType == null) { error = "Array element type is null"; return false; }

                var values = ConvertSources(comp, elemType, sourceGos, allowDuplicates);
                var arr = Array.CreateInstance(elemType, values.Count);
                for (int i = 0; i < values.Count; i++) arr.SetValue(values[i], i);

                assign(arr);
                return true;
            }

            // 2) List<T> ?
            if (IsGenericList(memberType, out var elem))
            {
                var values = ConvertSources(comp, elem!, sourceGos, allowDuplicates);

                var list = (IList)Activator.CreateInstance(memberType)!;
                foreach (var v in values) list.Add(v);

                assign(list);
                return true;
            }

            error = $"Member must be array or List<T>, got: {memberType.FullName}";
            return false;
        }

        private static List<object?> ConvertSources(Component receiverComp, Type elemType, GameObject[] sourceGos, bool allowDuplicates)
        {
            var list = new List<object?>();
            var seen = new HashSet<int>();

            foreach (var go in sourceGos)
            {
                if (go == null) continue;

                object? v = null;

                if (elemType == typeof(GameObject)) v = go;
                else if (elemType == typeof(Transform)) v = go.transform;
                else if (typeof(Component).IsAssignableFrom(elemType))
                    v = go.GetComponent(elemType);

                if (v == null) continue;

                if (!allowDuplicates)
                {
                    int key = (v is UnityEngine.Object uo) ? uo.GetInstanceID() : v.GetHashCode();
                    if (!seen.Add(key)) continue;
                }

                list.Add(v);
            }

            return list;
        }

        private static bool IsGenericList(Type t, out Type? elemType)
        {
            elemType = null;
            if (!t.IsGenericType) return false;
            var def = t.GetGenericTypeDefinition();
            if (def != typeof(List<>)) return false;
            elemType = t.GetGenericArguments()[0];
            return true;
        }

        private static Type? ResolveType(string name)
        {
            // 1) прямой Type.GetType
            var t = Type.GetType(name, throwOnError: false);
            if (t != null) return t;

            // 2) поиск по домену
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var tt = asm.GetType(name, throwOnError: false);
                    if (tt != null) return tt;
                }
                catch { }
            }

            // 3) если короткое имя "Rigidbody" и т.п.
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? found = null;
                try
                {
                    found = asm.GetTypes().FirstOrDefault(x => x.Name == name);
                }
                catch { }
                if (found != null) return found;
            }

            return null;
        }

        private static GameObject? FindByPath(string path)
        {
            // "Root/Child/Sub"
            var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;

            var all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in all)
            {
                if (go == null) continue;
                if (go.name != parts[0]) continue;

                var t = go.transform;
                bool ok = true;

                for (int i = 1; i < parts.Length; i++)
                {
                    var child = t.Find(parts[i]);
                    if (child == null) { ok = false; break; }
                    t = child;
                }

                if (ok) return t.gameObject;
            }

            return null;
        }
    }
}
