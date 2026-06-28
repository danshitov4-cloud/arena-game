using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class AssetReferenceCommands
    {
        // command: asset.ref.setFieldByQuery
        // args:
        // {
        //   "query": { ... },                  // required: какие объекты меняем
        //   "componentType": "BuildingView",   // required
        //   "member": "someAssetField",        // required: поле/свойство
        //
        //   "assetPath": "Assets/...",         // required
        //   "assetType": "auto|Material|Sprite|AudioClip|GameObject|ScriptableObject", // optional, default auto
        //
        //   "allComponents": false,            // optional
        //   "max": 5000,                       // safety
        //   "dryRun": false
        // }
        public static object SetFieldByQuery(JToken args)
        {
            if (args?["query"] == null) throw new ArgumentException("args.query is required");

            string compTypeName = ((string?)args?["componentType"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(compTypeName)) throw new ArgumentException("args.componentType is required");

            string member = ((string?)args?["member"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(member)) throw new ArgumentException("args.member is required");

            string assetPath = ((string?)args?["assetPath"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(assetPath)) throw new ArgumentException("args.assetPath is required");

            string assetType = ((string?)args?["assetType"] ?? "auto").Trim();

            bool dryRun = (bool?)args?["dryRun"] ?? false;
            bool allComponents = (bool?)args?["allComponents"] ?? false;

            int max = (int?)args?["max"] ?? 5000;
            if (max < 1) max = 1;
            if (max > 20000) max = 20000;

            // resolve component type
            Type? srcCompType = SceneQuery.ResolveType(compTypeName);
            if (srcCompType == null)
                return new { ok = false, error = $"componentType not found: {compTypeName}" };

            // load asset
            UnityEngine.Object? asset = LoadAsset(assetPath, assetType);
            if (asset == null)
                return new { ok = false, error = $"Asset not found or type mismatch: {assetPath}", assetPath, assetType };

            // targets
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
                Undo.SetCurrentGroupName("Orchestrator Set Asset Reference");

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

                            bool did = TrySetMember(c, member, asset, dryRun: false, out _);
                            if (did) { changed++; EditorUtility.SetDirty(c); }
                            else { skipped++; }

                            if (samples.Count < 10)
                                samples.Add(new
                                {
                                    go = go.name,
                                    goId = go.GetInstanceID(),
                                    component = c.GetType().FullName,
                                    member,
                                    asset = asset.name,
                                    assetType = asset.GetType().FullName
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

                    var comps = go.GetComponents(srcCompType);
                    if (comps == null || comps.Length == 0) { skipped++; continue; }

                    var list = allComponents ? comps : new[] { comps[0] };

                    foreach (var c in list)
                    {
                        if (c == null) continue;
                        matchedComponents++;

                        bool would = TrySetMember(c, member, asset, dryRun: true, out _);
                        if (would) changed++;

                        if (samples.Count < 10)
                            samples.Add(new
                            {
                                go = go.name,
                                goId = go.GetInstanceID(),
                                component = c.GetType().FullName,
                                member,
                                wouldAssign = asset.name,
                                assetType = asset.GetType().FullName
                            });
                    }
                }
            }

            return new
            {
                ok = true,
                dryRun,
                assetPath,
                loadedAsset = new { name = asset.name, type = asset.GetType().FullName },
                queryMatchedObjects = matchedObjects,
                matchedComponents,
                changedCount = changed,
                skippedCount = skipped,
                samples
            };
        }

        private static UnityEngine.Object? LoadAsset(string assetPath, string assetType)
        {
            assetType = (assetType ?? "auto").Trim();

            // auto: пробуем как Object
            if (assetType.Equals("auto", StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

            // simple named types
            Type? t = assetType switch
            {
                "Material" => typeof(Material),
                "Sprite" => typeof(Sprite),
                "AudioClip" => typeof(AudioClip),
                "GameObject" => typeof(GameObject),
                "ScriptableObject" => typeof(ScriptableObject),
                _ => SceneQuery.ResolveType(assetType) // если дадут полное имя типа
            };

            if (t == null) return null;

            return AssetDatabase.LoadAssetAtPath(assetPath, t);
        }

        private static bool TrySetMember(Component comp, string memberName, UnityEngine.Object value, bool dryRun, out string? whySkipped)
        {
            whySkipped = null;

            // 1) SerializedObject (для [SerializeField] тоже)
            try
            {
                var so = new SerializedObject(comp);
                var sp = so.FindProperty(memberName);
                if (sp != null && sp.propertyType == SerializedPropertyType.ObjectReference)
                {
                    bool diff = sp.objectReferenceValue != value;

                    if (!dryRun)
                    {
                        Undo.RecordObject(comp, "Set Asset Reference");
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
                    Undo.RecordObject(comp, "Set Asset Reference");
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
                    Undo.RecordObject(comp, "Set Asset Reference");
                    p.SetValue(comp, value);
                }

                return diff;
            }

            whySkipped = $"Member '{memberName}' not found (SerializedProperty/Field/Property).";
            return false;
        }
    }
}
