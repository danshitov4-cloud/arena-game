using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class SceneReferenceCommands
    {

        // command: scene.ref.setFieldByQuerySelf
        // args:
        // {
        //   "query": { ... },                       // required
        //   "componentType": "SortByY",             // required: на каких компонентах ставим поле
        //   "member": "sg",                         // required: поле/свойство
        //
        //   "selfComponentType": "UnityEngine.Rendering.SortingGroup", // required: какой компонент взять с этого же GO
        //   "allComponents": false,                 // optional (если на одном GO несколько SortByY)
        //   "max": 5000,                            // safety
        //   "dryRun": false
        // }
        public static object SetFieldByQuerySelf(JToken args)
        {
            if (args?["query"] == null) throw new ArgumentException("args.query is required");

            string compTypeName = ((string?)args?["componentType"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(compTypeName)) throw new ArgumentException("args.componentType is required");

            string member = ((string?)args?["member"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(member)) throw new ArgumentException("args.member is required");

            string selfTypeName = ((string?)args?["selfComponentType"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(selfTypeName)) throw new ArgumentException("args.selfComponentType is required");

            bool dryRun = (bool?)args?["dryRun"] ?? false;
            bool allComponents = (bool?)args?["allComponents"] ?? false;

            int max = (int?)args?["max"] ?? 5000;
            if (max < 1) max = 1;
            if (max > 20000) max = 20000;

            Type? hostType = SceneQuery.ResolveType(compTypeName);
            if (hostType == null)
                return new { ok = false, error = $"componentType not found: {compTypeName}" };

            Type? selfType = SceneQuery.ResolveType(selfTypeName);
            if (selfType == null || !typeof(Component).IsAssignableFrom(selfType))
                return new { ok = false, error = $"selfComponentType not found or not a Component: {selfTypeName}" };

            var gos = SceneUtils.GetObjectsFromQuery(args["query"]!);
            if (gos.Length > max) gos = gos.Take(max).ToArray();

            int matchedObjects = gos.Length;
            int matchedComponents = 0;
            int changed = 0;
            int skipped = 0;

            var samples = new List<object>(10);

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator Set Self Reference");

                try
                {
                    foreach (var go in gos)
                    {
                        if (go == null) { skipped++; continue; }

                        var selfComp = go.GetComponent(selfType);
                        if (selfComp == null) { skipped++; continue; }

                        var comps = go.GetComponents(hostType);
                        if (comps == null || comps.Length == 0) { skipped++; continue; }

                        var list = allComponents ? comps : new[] { comps[0] };

                        foreach (var c in list)
                        {
                            if (c == null) continue;
                            matchedComponents++;

                            bool did = TrySetMemberToObjectReference((Component)c, member, selfComp, dryRun: false, out _);
                            if (did) { changed++; EditorUtility.SetDirty((Component)c); }
                            else { skipped++; }

                            if (samples.Count < 10)
                                samples.Add(new
                                {
                                    go = go.name,
                                    goId = go.GetInstanceID(),
                                    component = c.GetType().FullName,
                                    member,
                                    selfComponent = selfComp.GetType().FullName
                                });
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
                    if (go == null) { skipped++; continue; }

                    var selfComp = go.GetComponent(selfType);
                    if (selfComp == null) { skipped++; continue; }

                    var comps = go.GetComponents(hostType);
                    if (comps == null || comps.Length == 0) { skipped++; continue; }

                    var list = allComponents ? comps : new[] { comps[0] };

                    foreach (var c in list)
                    {
                        if (c == null) continue;
                        matchedComponents++;

                        bool would = TrySetMemberToObjectReference((Component)c, member, selfComp, dryRun: true, out _);
                        if (would) changed++;

                        if (samples.Count < 10)
                            samples.Add(new
                            {
                                go = go.name,
                                goId = go.GetInstanceID(),
                                component = c.GetType().FullName,
                                member,
                                wouldAssign = selfComp.GetType().FullName
                            });
                    }
                }
            }

            return new
            {
                ok = true,
                dryRun,
                queryMatchedObjects = matchedObjects,
                matchedComponents,
                changedCount = changed,
                skippedCount = skipped,
                samples
            };
        }

        // helper: ставим UnityEngine.Object ссылку в поле/prop/serialized property
        private static bool TrySetMemberToObjectReference(Component comp, string memberName, UnityEngine.Object value, bool dryRun, out string? whySkipped)
        {
            whySkipped = null;

            // 1) SerializedObject (работает даже для private [SerializeField])
            try
            {
                var so = new SerializedObject(comp);
                var sp = so.FindProperty(memberName);
                if (sp != null && sp.propertyType == SerializedPropertyType.ObjectReference)
                {
                    bool diff = sp.objectReferenceValue != value;
                    if (!dryRun)
                    {
                        Undo.RecordObject(comp, "Set Self Reference");
                        sp.objectReferenceValue = value;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    return diff;
                }
            }
            catch { }

            // 2) Reflection fallback
            var type = comp.GetType();

            var f = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null)
            {
                if (!typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType))
                {
                    whySkipped = $"Field '{memberName}' is not UnityEngine.Object reference.";
                    return false;
                }

                var current = f.GetValue(comp) as UnityEngine.Object;
                bool diff = current != value;

                if (!dryRun)
                {
                    Undo.RecordObject(comp, "Set Self Reference");
                    f.SetValue(comp, value);
                }

                return diff;
            }

            var p = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite)
            {
                if (!typeof(UnityEngine.Object).IsAssignableFrom(p.PropertyType))
                {
                    whySkipped = $"Property '{memberName}' is not UnityEngine.Object reference.";
                    return false;
                }

                var current = p.GetValue(comp) as UnityEngine.Object;
                bool diff = current != value;

                if (!dryRun)
                {
                    Undo.RecordObject(comp, "Set Self Reference");
                    p.SetValue(comp, value);
                }

                return diff;
            }

            whySkipped = $"Member '{memberName}' not found (SerializedProperty/Field/Property).";
            return false;
        }

        // command: scene.ref.describe
        // args:
        // {
        //   "componentType": "BuildingView",   // required
        //   "max": 200                         // safety
        // }
        public static object Describe(JToken args)
        {
            string compTypeName = ((string?)args?["componentType"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(compTypeName))
                throw new ArgumentException("args.componentType is required");

            int max = (int?)args?["max"] ?? 200;
            if (max < 1) max = 1;
            if (max > 2000) max = 2000;

            Type? t = SceneQuery.ResolveType(compTypeName);
            if (t == null)
                return new { ok = false, error = $"Type not found: {compTypeName}" };

            var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType))
                .Select(f => new
                {
                    name = f.Name,
                    fieldType = f.FieldType.FullName,
                    isPublic = f.IsPublic,
                    isSerializeField = f.GetCustomAttributes(typeof(SerializeField), true).Any()
                })
                .Take(max)
                .ToArray();

            var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(p => p.CanWrite && typeof(UnityEngine.Object).IsAssignableFrom(p.PropertyType))
                .Select(p => new
                {
                    name = p.Name,
                    propType = p.PropertyType.FullName,
                    hasSetter = p.SetMethod != null,
                    isPublicSetter = p.SetMethod != null && p.SetMethod.IsPublic
                })
                .Take(max)
                .ToArray();

            return new
            {
                ok = true,
                componentType = t.FullName,
                referenceFields = fields,
                referenceProperties = props
            };
        }

        // command: scene.ref.setFieldByQuery
        // Назначает ссылку в поле/свойство у компонента на объектах по query.
        //
        // args:
        // {
        //   "query": { ... },                 // required: какие объекты меняем
        //   "componentType": "MyNS.MyComp",   // required: на каком компоненте ставим
        //   "member": "target",              // required: имя поля или свойства (лучше для сериализуемых: поле)
        //
        //   "target": {
        //       "by": "instanceId|path|queryFirst",  // required
        //       "instanceId": 123,                  // if by=instanceId
        //       "path": "GridRoot/Player",          // if by=path (точный путь в иерархии)
        //       "query": { ... },                   // if by=queryFirst
        //
        //       "mode": "auto|gameObject|transform|component",  // optional, default auto
        //       "componentType": "UnityEngine.Transform"        // if mode=component (какой компонент брать с target GO)
        //   },
        //
        //   "allComponents": false,          // optional: если на GO несколько compType — менять все/только первый
        //   "max": 5000,                     // safety
        //   "dryRun": false
        // }
        public static object SetFieldByQuery(JToken args)
        {
            if (args?["query"] == null) throw new ArgumentException("args.query is required");
            string compTypeName = ((string?)args?["componentType"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(compTypeName)) throw new ArgumentException("args.componentType is required");

            string member = ((string?)args?["member"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(member)) throw new ArgumentException("args.member is required");

            var targetSpec = args?["target"] as JObject;
            if (targetSpec == null) throw new ArgumentException("args.target is required (object)");

            bool dryRun = (bool?)args?["dryRun"] ?? false;
            bool allComponents = (bool?)args?["allComponents"] ?? false;

            int max = (int?)args?["max"] ?? 5000;
            if (max < 1) max = 1;
            if (max > 20000) max = 20000;

            // 1) Resolve source objects
            var gos = SceneUtils.GetObjectsFromQuery(args["query"]!);
            if (gos.Length > max) gos = gos.Take(max).ToArray();

            // 2) Resolve target GameObject
            GameObject? targetGo = ResolveTargetGameObject(targetSpec);
            if (targetGo == null)
                return new { ok = false, error = "Target GameObject not found (args.target)" };

            // 3) Decide what we assign (GameObject/Transform/Component)
            UnityEngine.Object? assignObj = ResolveAssignableObject(targetSpec, targetGo);
            if (assignObj == null)
                return new { ok = false, error = "Unable to resolve assignable object from target (mode/componentType mismatch?)" };

            // 4) Resolve component type for sources
            Type? srcCompType = SceneQuery.ResolveType(compTypeName);
            if (srcCompType == null)
                return new { ok = false, error = $"componentType not found: {compTypeName}" };

            int matchedObjects = gos.Length;
            int matchedComponents = 0;
            int changed = 0;
            int skipped = 0;

            var samples = new List<object>(10);

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator SetFieldByQuery");

                try
                {
                    foreach (var go in gos)
                    {
                        if (go == null) { skipped++; continue; }

                        var comps = go.GetComponents(srcCompType);
                        if (comps == null || comps.Length == 0) { skipped++; continue; }

                        var list = allComponents ? comps : new[] { comps[0] };

                        foreach (var c in list)
                        {
                            if (c == null) continue;
                            matchedComponents++;

                            bool did = TrySetMember(c, member, assignObj, dryRun: false, out string? whySkipped);
                            if (did)
                            {
                                changed++;
                                EditorUtility.SetDirty(c);
                            }
                            else
                            {
                                if (!string.IsNullOrWhiteSpace(whySkipped)) skipped++;
                            }

                            if (samples.Count < 10)
                            {
                                samples.Add(new
                                {
                                    go = go.name,
                                    goId = go.GetInstanceID(),
                                    component = c.GetType().FullName,
                                    member,
                                    assigned = assignObj.name,
                                    assignedType = assignObj.GetType().FullName
                                });
                            }
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
                    if (go == null) { skipped++; continue; }

                    var comps = go.GetComponents(srcCompType);
                    if (comps == null || comps.Length == 0) { skipped++; continue; }

                    var list = allComponents ? comps : new[] { comps[0] };

                    foreach (var c in list)
                    {
                        if (c == null) continue;
                        matchedComponents++;

                        bool would = TrySetMember(c, member, assignObj, dryRun: true, out _);
                        if (would) changed++;

                        if (samples.Count < 10)
                        {
                            samples.Add(new
                            {
                                go = go.name,
                                goId = go.GetInstanceID(),
                                component = c.GetType().FullName,
                                member,
                                wouldAssign = assignObj.name,
                                assignedType = assignObj.GetType().FullName
                            });
                        }
                    }
                }
            }

            return new
            {
                ok = true,
                dryRun,
                queryMatchedObjects = matchedObjects,
                matchedComponents,
                changedCount = changed,
                skippedCount = skipped,
                target = new
                {
                    gameObject = targetGo.name,
                    targetInstanceId = targetGo.GetInstanceID(),
                    assigned = assignObj.name,
                    assignedType = assignObj.GetType().FullName
                },
                samples
            };
        }

        // ---------------- internals ----------------

        private static GameObject? ResolveTargetGameObject(JObject targetSpec)
        {
            string by = ((string?)targetSpec["by"] ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(by)) throw new ArgumentException("args.target.by is required");

            return by switch
            {
                "instanceid" => ResolveByInstanceId((int?)targetSpec["instanceId"]),
                "path" => ResolveByPath(((string?)targetSpec["path"] ?? "").Trim()),
                "queryfirst" => ResolveByQueryFirst(targetSpec["query"]),
                _ => throw new ArgumentException("args.target.by must be instanceId|path|queryFirst")
            };
        }

        private static GameObject? ResolveByInstanceId(int? id)
        {
            if (!id.HasValue || id.Value == 0) throw new ArgumentException("args.target.instanceId is required");
            var obj = EditorUtility.InstanceIDToObject(id.Value);
            if (obj is GameObject go) return go;
            if (obj is Component c) return c.gameObject;
            return null;
        }

        private static GameObject? ResolveByQueryFirst(JToken? query)
        {
            if (query == null) throw new ArgumentException("args.target.query is required for by=queryFirst");
            var gos = SceneUtils.GetObjectsFromQuery(query);
            return gos.FirstOrDefault(g => g != null);
        }

        private static GameObject? ResolveByPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("args.target.path is required for by=path");

            // path like "GridRoot/Player"
            var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;

            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            GameObject? current = roots.FirstOrDefault(r => r.name == parts[0]);
            if (current == null) return null;

            for (int i = 1; i < parts.Length; i++)
            {
                var tr = current.transform.Find(parts[i]);
                if (tr == null) return null;
                current = tr.gameObject;
            }

            return current;
        }

        private static UnityEngine.Object? ResolveAssignableObject(JObject targetSpec, GameObject targetGo)
        {
            string mode = ((string?)targetSpec["mode"] ?? "auto").Trim().ToLowerInvariant();
            string compType = ((string?)targetSpec["componentType"] ?? "").Trim();

            if (mode == "auto")
            {
                // если указан componentType -> component, иначе -> transform
                mode = string.IsNullOrWhiteSpace(compType) ? "transform" : "component";
            }

            return mode switch
            {
                "gameobject" => targetGo,
                "transform" => targetGo.transform,
                "component" => ResolveTargetComponent(targetGo, compType),
                _ => throw new ArgumentException("args.target.mode must be auto|gameObject|transform|component")
            };
        }

        private static UnityEngine.Object? ResolveTargetComponent(GameObject targetGo, string compTypeName)
        {
            if (string.IsNullOrWhiteSpace(compTypeName))
                throw new ArgumentException("args.target.componentType is required when mode=component");

            Type? t = SceneQuery.ResolveType(compTypeName);
            if (t == null) throw new ArgumentException($"target.componentType not found: {compTypeName}");

            var c = targetGo.GetComponent(t);
            return c as UnityEngine.Object;
        }

        private static bool TrySetMember(Component comp, string memberName, UnityEngine.Object value, bool dryRun, out string? whySkipped)
        {
            whySkipped = null;

            // 1) Prefer SerializedObject (works for [SerializeField] private fields too)
            try
            {
                var so = new SerializedObject(comp);
                var sp = so.FindProperty(memberName);
                if (sp != null && sp.propertyType == SerializedPropertyType.ObjectReference)
                {
                    // already same?
                    bool diff = sp.objectReferenceValue != value;

                    if (!dryRun)
                    {
                        Undo.RecordObject(comp, "Set Reference Field");
                        sp.objectReferenceValue = value;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }

                    return diff;
                }
            }
            catch
            {
                // ignore and fallback to reflection
            }

            // 2) Reflection fallback for public fields/properties
            var type = comp.GetType();

            // Field
            var f = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null)
            {
                if (!typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType))
                {
                    whySkipped = $"Field '{memberName}' is not UnityEngine.Object reference.";
                    return false;
                }

                var current = f.GetValue(comp) as UnityEngine.Object;
                bool diff = current != value;

                if (!dryRun)
                {
                    Undo.RecordObject(comp, "Set Reference Field");
                    f.SetValue(comp, value);
                }

                return diff;
            }

            // Property
            var p = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite)
            {
                if (!typeof(UnityEngine.Object).IsAssignableFrom(p.PropertyType))
                {
                    whySkipped = $"Property '{memberName}' is not UnityEngine.Object reference.";
                    return false;
                }

                var current = p.GetValue(comp) as UnityEngine.Object;
                bool diff = current != value;

                if (!dryRun)
                {
                    Undo.RecordObject(comp, "Set Reference Property");
                    p.SetValue(comp, value);
                }

                return diff;
            }

            whySkipped = $"Member '{memberName}' not found (SerializedProperty/Field/Property).";
            return false;
        }
    }
}
