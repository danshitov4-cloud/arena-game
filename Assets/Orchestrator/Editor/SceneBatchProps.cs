using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class SceneBatchProps
    {
        public static object ApplyIfDiffComponentProperties(JToken args)
        {
            bool dryRun = (bool?)args?["dryRun"] ?? false;
            bool apply = (bool?)args?["apply"] ?? true;

            var diff = DiffComponentProperties(args);

            var diffToken = JToken.FromObject(diff);
            bool ok = (bool?)diffToken["ok"] ?? false;

            if (!ok)
                return new { ok = false, stage = "diff", diff };

            int changedComponents = (int?)diffToken["changedComponents"] ?? 0;

            if (changedComponents == 0 || !apply || dryRun)
            {
                return new
                {
                    ok = true,
                    stage = "diffOnly",
                    changedComponents,
                    applied = false,
                    reason = (!apply ? "apply=false" : (dryRun ? "dryRun=true" : "no diffs")),
                    diff
                };
            }

            var applyResult = SetComponentProperties(args);

            return new
            {
                ok = true,
                stage = "applied",
                changedComponents,
                applied = true,
                diff,
                apply = applyResult
            };
        }

        public static object DiffComponentProperties(JToken args)
        {
            var queryToken = args?["query"] ?? throw new ArgumentException("args.query is required");
            string componentTypeName = ((string?)args?["componentType"] ?? "").Trim();
            var setObj = args?["set"] as JObject;

            int sampleLimit = (int?)args?["sampleLimit"] ?? 30;
            bool includeUnchanged = (bool?)args?["includeUnchanged"] ?? false;

            if (string.IsNullOrWhiteSpace(componentTypeName))
                throw new ArgumentException("args.componentType is required");
            if (setObj == null)
                throw new ArgumentException("args.set is required (object with member:value)");
            if (sampleLimit < 0) sampleLimit = 0;
            if (sampleLimit > 200) sampleLimit = 200;

            var compType = SceneQuery.ResolveType(componentTypeName);
            if (compType == null || !typeof(Component).IsAssignableFrom(compType))
                throw new InvalidOperationException($"Component type not found or not a Component: {componentTypeName}");

            // »«Ã≈Õ≈ÕŒ: SceneUtils ‚ÏÂÒÚÓ SceneBatch_GetObjectsFromQuery
            var gos = SceneUtils.GetObjectsFromQuery(queryToken);

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var members = new System.Collections.Generic.List<(string name, PropertyInfo? prop, FieldInfo? field, object value, Type targetType)>();
            var failedMembers = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var kv in setObj.Properties())
            {
                string memberName = kv.Name;

                var prop = compType.GetProperty(memberName, flags);
                var field = compType.GetField(memberName, flags);

                if (prop == null && field == null)
                {
                    failedMembers[memberName] = "member not found";
                    continue;
                }

                var targetType = prop != null ? prop.PropertyType : field!.FieldType;

                try
                {
                    object typedValue = ConvertJsonToType(kv.Value, targetType);
                    members.Add((memberName, prop, field, typedValue, targetType));
                }
                catch (Exception ex)
                {
                    failedMembers[memberName] = "convert failed: " + ex.Message;
                }
            }

            if (members.Count == 0)
                return new { ok = false, error = "No valid members to diff.", failedMembers };

            int matchedObjects = gos.Length;
            int matchedComponents = 0;
            int changedComponents = 0;
            int changedPairs = 0;
            int skippedObjects = 0;

            var perMember = new System.Collections.Generic.Dictionary<string, (int checkedCount, int diffCount)>(StringComparer.Ordinal);
            foreach (var m in members)
                perMember[m.name] = (0, 0);

            var examples = new System.Collections.Generic.List<object>();

            foreach (var go in gos)
            {
                if (go == null) continue;

                var comps = go.GetComponents(compType);
                if (comps == null || comps.Length == 0) { skippedObjects++; continue; }

                foreach (var c in comps)
                {
                    if (c == null) continue;
                    matchedComponents++;

                    bool componentWouldChange = false;
                    var diffsForThisComponent = new System.Collections.Generic.List<object>();

                    foreach (var m in members)
                    {
                        object? cur = null;
                        bool canRead = true;

                        try
                        {
                            cur = m.prop != null ? m.prop.GetValue(c) : m.field!.GetValue(c);
                        }
                        catch
                        {
                            canRead = false;
                        }

                        var st = perMember[m.name];
                        st.checkedCount++;
                        perMember[m.name] = st;

                        bool differs = !canRead || !EqualsSmart(cur, m.value);

                        if (differs)
                        {
                            componentWouldChange = true;
                            changedPairs++;

                            var st2 = perMember[m.name];
                            st2.diffCount++;
                            perMember[m.name] = st2;

                            if (sampleLimit > 0 && diffsForThisComponent.Count < 20)
                            {
                                diffsForThisComponent.Add(new
                                {
                                    member = m.name,
                                    current = cur,
                                    desired = m.value
                                });
                            }
                        }
                        else if (includeUnchanged && sampleLimit > 0 && diffsForThisComponent.Count < 20)
                        {
                            diffsForThisComponent.Add(new
                            {
                                member = m.name,
                                current = cur,
                                desired = m.value,
                                same = true
                            });
                        }
                    }

                    if (componentWouldChange) changedComponents++;

                    if (sampleLimit > 0 && examples.Count < sampleLimit && (componentWouldChange || includeUnchanged))
                    {
                        examples.Add(new
                        {
                            gameObject = go.name,
                            // »«Ã≈Õ≈ÕŒ: SceneUtils ‚ÏÂÒÚÓ ÎÓÍ‡Î¸ÌÓÈ GetPath
                            path = SceneUtils.GetHierarchyPath(go.transform),
                            gameObjectInstanceId = go.GetInstanceID(),
                            componentInstanceId = c.GetInstanceID(),
                            wouldChange = componentWouldChange,
                            diffs = diffsForThisComponent.ToArray()
                        });
                    }
                }
            }

            var perMemberArr = perMember
                .Select(kv => new
                {
                    member = kv.Key,
                    checkedCount = kv.Value.checkedCount,
                    diffCount = kv.Value.diffCount
                })
                .OrderByDescending(x => x.diffCount)
                .ToArray();

            return new
            {
                ok = true,
                componentType = compType.FullName,
                matchedObjects,
                matchedComponents,
                changedComponents,
                changedPairs,
                skippedObjects,
                perMember = perMemberArr,
                examples,
                failedMembers
            };
        }

        public static object SetComponentProperties(JToken args)
        {
            bool dryRun = (bool?)args?["dryRun"] ?? false;

            var queryToken = args?["query"] ?? throw new ArgumentException("args.query is required");
            string componentTypeName = ((string?)args?["componentType"] ?? "").Trim();
            var setObj = args?["set"] as JObject;

            if (string.IsNullOrWhiteSpace(componentTypeName))
                throw new ArgumentException("args.componentType is required");
            if (setObj == null)
                throw new ArgumentException("args.set is required (object with member:value)");

            var compType = SceneQuery.ResolveType(componentTypeName);
            if (compType == null || !typeof(Component).IsAssignableFrom(compType))
                throw new InvalidOperationException($"Component type not found or not a Component: {componentTypeName}");

            // »«Ã≈Õ≈ÕŒ: SceneUtils ‚ÏÂÒÚÓ SceneBatch_GetObjectsFromQuery
            var gos = SceneUtils.GetObjectsFromQuery(queryToken);

            int matchedObjects = gos.Length;
            int matchedComponents = 0;
            int changedTotal = 0;
            int skippedObjects = 0;

            var appliedMembers = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var failedMembers = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var members = new System.Collections.Generic.List<(string name, PropertyInfo? prop, FieldInfo? field, object value)>();

            foreach (var kv in setObj.Properties())
            {
                string memberName = kv.Name;

                var prop = compType.GetProperty(memberName, flags);
                var field = compType.GetField(memberName, flags);

                if (prop == null && field == null)
                {
                    failedMembers[memberName] = "member not found";
                    continue;
                }

                var targetType = prop != null ? prop.PropertyType : field!.FieldType;

                try
                {
                    object typedValue = ConvertJsonToType(kv.Value, targetType);
                    members.Add((memberName, prop, field, typedValue));
                }
                catch (Exception ex)
                {
                    failedMembers[memberName] = "convert failed: " + ex.Message;
                }
            }

            if (members.Count == 0)
                return new { ok = false, error = "No valid members to set.", failedMembers };

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName($"Batch SetProperties {compType.Name}");

                try
                {
                    foreach (var go in gos)
                    {
                        if (go == null) continue;

                        var comps = go.GetComponents(compType);
                        if (comps == null || comps.Length == 0) { skippedObjects++; continue; }

                        matchedComponents += comps.Length;

                        foreach (var c in comps)
                        {
                            if (c == null) continue;

                            Undo.RecordObject(c, "Set Component Properties");

                            foreach (var m in members)
                            {
                                if (!TrySetMember(c, m.prop, m.field, m.value, out bool didChange))
                                    continue;

                                appliedMembers.Add(m.name);
                                if (didChange)
                                {
                                    changedTotal++;
                                    EditorUtility.SetDirty(c);
                                }
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
                    if (go == null) continue;

                    var comps = go.GetComponents(compType);
                    if (comps == null || comps.Length == 0) { skippedObjects++; continue; }

                    matchedComponents += comps.Length;

                    foreach (var c in comps)
                    {
                        if (c == null) continue;

                        foreach (var m in members)
                        {
                            if (TryWouldChange(c, m.prop, m.field, m.value))
                            {
                                appliedMembers.Add(m.name);
                                changedTotal++;
                            }
                        }
                    }
                }
            }

            return new
            {
                ok = true,
                dryRun,
                componentType = compType.FullName,
                matchedObjects,
                matchedComponents,
                changed = changedTotal,
                skippedObjects,
                appliedMembers = appliedMembers.ToArray(),
                failedMembers
            };
        }

        public static object GetComponentProperty(JToken args)
        {
            var queryToken = args?["query"] ?? throw new ArgumentException("args.query is required");
            string componentTypeName = ((string?)args?["componentType"] ?? "").Trim();
            string memberName = ((string?)args?["member"] ?? "").Trim();

            if (string.IsNullOrWhiteSpace(componentTypeName))
                throw new ArgumentException("args.componentType is required");
            if (string.IsNullOrWhiteSpace(memberName))
                throw new ArgumentException("args.member is required");

            var compType = SceneQuery.ResolveType(componentTypeName);
            if (compType == null || !typeof(Component).IsAssignableFrom(compType))
                throw new InvalidOperationException($"Component type not found or not a Component: {componentTypeName}");

            // »«Ã≈Õ≈ÕŒ: SceneUtils ‚ÏÂÒÚÓ SceneBatch_GetObjectsFromQuery
            var gos = SceneUtils.GetObjectsFromQuery(queryToken);

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var prop = compType.GetProperty(memberName, flags);
            var field = compType.GetField(memberName, flags);

            if (prop == null && field == null)
                throw new InvalidOperationException($"Member not found: {compType.Name}.{memberName}");

            var items = new System.Collections.Generic.List<object>();
            int matchedComponents = 0;

            foreach (var go in gos)
            {
                if (go == null) continue;

                var comps = go.GetComponents(compType);
                if (comps == null || comps.Length == 0) continue;

                foreach (var c in comps)
                {
                    matchedComponents++;
                    object? v = null;
                    try
                    {
                        v = prop != null ? prop.GetValue(c) : field!.GetValue(c);
                    }
                    catch { }

                    items.Add(new
                    {
                        gameObject = go.name,
                        gameObjectInstanceId = go.GetInstanceID(),
                        componentInstanceId = c.GetInstanceID(),
                        value = v
                    });
                }
            }

            return new
            {
                ok = true,
                componentType = compType.FullName,
                member = memberName,
                matchedObjects = gos.Length,
                matchedComponents,
                items = items.Take(50).ToArray(),
                truncated = items.Count > 50
            };
        }

        public static object SetComponentProperty(JToken args)
        {
            bool dryRun = (bool?)args?["dryRun"] ?? false;

            var queryToken = args?["query"] ?? throw new ArgumentException("args.query is required");
            string componentTypeName = ((string?)args?["componentType"] ?? "").Trim();
            string memberName = ((string?)args?["member"] ?? "").Trim();
            var valueToken = args?["value"];

            if (string.IsNullOrWhiteSpace(componentTypeName))
                throw new ArgumentException("args.componentType is required");
            if (string.IsNullOrWhiteSpace(memberName))
                throw new ArgumentException("args.member is required");
            if (valueToken == null)
                throw new ArgumentException("args.value is required");

            var compType = SceneQuery.ResolveType(componentTypeName);
            if (compType == null || !typeof(Component).IsAssignableFrom(compType))
                throw new InvalidOperationException($"Component type not found or not a Component: {componentTypeName}");

            // »«Ã≈Õ≈ÕŒ: SceneUtils ‚ÏÂÒÚÓ SceneBatch_GetObjectsFromQuery
            var gos = SceneUtils.GetObjectsFromQuery(queryToken);

            int matchedObjects = gos.Length;
            int matchedComponents = 0;
            int changed = 0;
            int skipped = 0;

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var prop = compType.GetProperty(memberName, flags);
            var field = compType.GetField(memberName, flags);

            if (prop == null && field == null)
                throw new InvalidOperationException($"Member not found: {compType.Name}.{memberName}");

            Type targetType = prop != null ? prop.PropertyType : field!.FieldType;
            object typedValue = ConvertJsonToType(valueToken, targetType);

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName($"Batch Set {compType.Name}.{memberName}");

                try
                {
                    foreach (var go in gos)
                    {
                        if (go == null) continue;

                        var comps = go.GetComponents(compType);
                        if (comps == null || comps.Length == 0) { skipped++; continue; }

                        matchedComponents += comps.Length;

                        foreach (var c in comps)
                        {
                            if (c == null) continue;

                            if (!TrySetMember(c, prop, field, typedValue, out bool didChange))
                                continue;

                            Undo.RecordObject(c, "Set Component Member");
                            TrySetMember(c, prop, field, typedValue, out didChange);

                            if (didChange)
                            {
                                changed++;
                                EditorUtility.SetDirty(c);
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
                    if (go == null) continue;

                    var comps = go.GetComponents(compType);
                    if (comps == null || comps.Length == 0) { skipped++; continue; }

                    matchedComponents += comps.Length;

                    foreach (var c in comps)
                    {
                        if (c == null) continue;
                        if (TryWouldChange(c, prop, field, typedValue))
                            changed++;
                    }
                }
            }

            return new
            {
                ok = true,
                dryRun,
                componentType = compType.FullName,
                member = memberName,
                value = JToken.FromObject(valueToken),
                matchedObjects,
                matchedComponents,
                changed,
                skipped
            };
        }

        public static object RemoveComponent(JToken args)
        {
            bool dryRun = (bool?)args?["dryRun"] ?? false;

            var queryToken = args?["query"] ?? throw new ArgumentException("args.query is required");
            string componentTypeName = ((string?)args?["componentType"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(componentTypeName))
                throw new ArgumentException("args.componentType is required");

            var compType = SceneQuery.ResolveType(componentTypeName);
            if (compType == null || !typeof(Component).IsAssignableFrom(compType))
                throw new InvalidOperationException($"Component type not found or not a Component: {componentTypeName}");

            // »«Ã≈Õ≈ÕŒ: SceneUtils ‚ÏÂÒÚÓ SceneBatch_GetObjectsFromQuery
            var gos = SceneUtils.GetObjectsFromQuery(queryToken);

            int matchedObjects = gos.Length;
            int removed = 0;
            int skipped = 0;

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName($"Batch Remove {compType.Name}");

                try
                {
                    foreach (var go in gos)
                    {
                        if (go == null) continue;

                        var comps = go.GetComponents(compType);
                        if (comps == null || comps.Length == 0) { skipped++; continue; }

                        foreach (var c in comps)
                        {
                            if (c == null) continue;
                            Undo.DestroyObjectImmediate(c);
                            removed++;
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
                    if (go == null) continue;

                    var comps = go.GetComponents(compType);
                    if (comps == null || comps.Length == 0) { skipped++; continue; }
                    removed += comps.Length;
                }
            }

            return new
            {
                ok = true,
                dryRun,
                componentType = compType.FullName,
                matchedObjects,
                removed,
                skipped
            };
        }

        // ---------- helpers ----------

        private static bool TrySetMember(Component c, PropertyInfo? prop, FieldInfo? field, object value, out bool didChange)
        {
            didChange = false;
            try
            {
                if (prop != null)
                {
                    if (!prop.CanWrite) return false;
                    object? cur = prop.GetValue(c);
                    didChange = !EqualsSmart(cur, value);
                    if (didChange) prop.SetValue(c, value);
                    return true;
                }

                if (field != null)
                {
                    object? cur = field.GetValue(c);
                    didChange = !EqualsSmart(cur, value);
                    if (didChange) field.SetValue(c, value);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool TryWouldChange(Component c, PropertyInfo? prop, FieldInfo? field, object value)
        {
            try
            {
                object? cur = prop != null ? prop.GetValue(c) : field!.GetValue(c);
                return !EqualsSmart(cur, value);
            }
            catch { return false; }
        }

        private static bool EqualsSmart(object? a, object b)
        {
            if (a == null) return false;
            if (a.Equals(b)) return true;

            if (a is float af && b is float bf) return Mathf.Abs(af - bf) < 1e-6f;
            if (a is double ad && b is double bd) return Math.Abs(ad - bd) < 1e-9;

            if (a is Vector3 av3 && b is Vector3 bv3) return (av3 - bv3).sqrMagnitude < 1e-10f;
            if (a is Vector2 av2 && b is Vector2 bv2) return (av2 - bv2).sqrMagnitude < 1e-10f;
            if (a is Color ac && b is Color bc)
                return Mathf.Abs(ac.r - bc.r) < 1e-5f && Mathf.Abs(ac.g - bc.g) < 1e-5f &&
                       Mathf.Abs(ac.b - bc.b) < 1e-5f && Mathf.Abs(ac.a - bc.a) < 1e-5f;

            return false;
        }

        private static object ConvertJsonToType(JToken token, Type targetType)
        {
            var nt = Nullable.GetUnderlyingType(targetType);
            if (nt != null)
            {
                if (token.Type == JTokenType.Null) return null!;
                targetType = nt;
            }

            if (targetType == typeof(string)) return token.Type == JTokenType.Null ? "" : token.ToString();
            if (targetType == typeof(bool)) return token.Value<bool>();
            if (targetType == typeof(int)) return token.Value<int>();
            if (targetType == typeof(float)) return token.Value<float>();
            if (targetType == typeof(double)) return token.Value<double>();

            if (targetType.IsEnum)
            {
                if (token.Type == JTokenType.String) return Enum.Parse(targetType, token.Value<string>()!, ignoreCase: true);
                return Enum.ToObject(targetType, token.Value<int>());
            }

            if (targetType == typeof(Vector2))
                return new Vector2((float?)token["x"] ?? 0f, (float?)token["y"] ?? 0f);

            if (targetType == typeof(Vector3))
                return new Vector3((float?)token["x"] ?? 0f, (float?)token["y"] ?? 0f, (float?)token["z"] ?? 0f);

            if (targetType == typeof(Color))
                return new Color((float?)token["r"] ?? 0f, (float?)token["g"] ?? 0f, (float?)token["b"] ?? 0f, (float?)token["a"] ?? 1f);

            try
            {
                return token.ToObject(targetType)!;
            }
            catch
            {
                return Convert.ChangeType(token.ToString(), targetType, CultureInfo.InvariantCulture);
            }
        }

        // ”ƒ¿À®Õ: SceneBatch_GetObjectsFromQuery ó ÚÂÔÂ¸ ËÒÔÓÎ¸ÁÛÈ SceneUtils.GetObjectsFromQuery
    }
}