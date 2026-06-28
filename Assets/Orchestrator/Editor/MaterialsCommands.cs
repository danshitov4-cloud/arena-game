using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class MaterialsCommands
    {
        // command: materials.report
        public static object Report(JToken args)
        {
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

            var usage = new Dictionary<Material, int>();
            int totalSlots = 0;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;

                totalSlots += mats.Length;

                foreach (var m in mats)
                {
                    if (m == null) continue;
                    usage.TryGetValue(m, out int c);
                    usage[m] = c + 1;
                }
            }

            var top = usage
                .OrderByDescending(kv => kv.Value)
                .Take(20)
                .Select(kv => new
                {
                    name = kv.Key.name,
                    instanceId = kv.Key.GetInstanceID(),
                    shader = kv.Key.shader != null ? kv.Key.shader.name : "",
                    usedByRenderers = kv.Value,
                    assetPath = AssetDatabase.GetAssetPath(kv.Key)
                })
                .ToArray();

            var topBySlots = renderers
                .Where(r => r != null && r.sharedMaterials != null)
                .Select(r => new
                {
                    name = r.gameObject.name,
                    instanceId = r.gameObject.GetInstanceID(),
                    materialSlots = r.sharedMaterials.Length
                })
                .OrderByDescending(x => x.materialSlots)
                .Take(20)
                .ToArray();

            return new
            {
                summary = new
                {
                    rendererCount = renderers.Length,
                    totalMaterialSlots = totalSlots,
                    uniqueMaterials = usage.Count
                },
                topMaterials = top,
                topObjectsByMaterialSlots = topBySlots
            };
        }

        // command: materials.findUsage
        public static object FindUsage(JToken args)
        {
            string needle = (string?)args?["materialNameContains"] ?? "";
            int max = (int?)args?["max"] ?? 200;
            if (string.IsNullOrWhiteSpace(needle))
                throw new ArgumentException("args.materialNameContains is required");

            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var items = new List<object>();

            foreach (var r in renderers)
            {
                if (items.Count >= max) break;
                if (r == null) continue;

                var mats = r.sharedMaterials;
                if (mats == null) continue;

                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;

                    if (m.name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        items.Add(new
                        {
                            gameObject = new
                            {
                                name = r.gameObject.name,
                                instanceId = r.gameObject.GetInstanceID(),
                                // ИЗМЕНЕНО: SceneUtils вместо локального GetHierarchyPath
                                path = SceneUtils.GetHierarchyPath(r.transform)
                            },
                            rendererType = r.GetType().FullName,
                            materialSlot = i,
                            material = new
                            {
                                name = m.name,
                                instanceId = m.GetInstanceID(),
                                shader = m.shader != null ? m.shader.name : "",
                                assetPath = AssetDatabase.GetAssetPath(m)
                            }
                        });
                        break;
                    }
                }
            }

            return new
            {
                query = new { materialNameContains = needle, max },
                count = items.Count,
                items
            };
        }

        // command: materials.setColorByObjectNameContains
        public static object SetColorByObjectNameContains(JToken args)
        {
            string nameContains = (string?)args?["nameContains"] ?? "";
            if (string.IsNullOrWhiteSpace(nameContains))
                throw new ArgumentException("args.nameContains is required");

            bool useShared = (bool?)args?["useShared"] ?? true;

            var c = args?["color"] ?? throw new ArgumentException("args.color is required");
            float r = (float?)c["r"] ?? 1f;
            float g = (float?)c["g"] ?? 1f;
            float b = (float?)c["b"] ?? 1f;
            float a = (float?)c["a"] ?? 1f;
            var color = new Color(r, g, b, a);

            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

            int matchedRenderers = 0;
            int changedMaterials = 0;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Orchestrator Set Color By Name (Hierarchy)");

            try
            {
                foreach (var rend in renderers)
                {
                    if (rend == null) continue;

                    // ИЗМЕНЕНО: SceneUtils вместо локального NameInHierarchyContains
                    if (!SceneUtils.NameInHierarchyContains(rend.transform, nameContains))
                        continue;

                    matchedRenderers++;

                    var mats = useShared ? rend.sharedMaterials : rend.materials;
                    if (mats == null) continue;

                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i];
                        if (m == null) continue;

                        Undo.RecordObject(m, "Set Material Color");

                        if (m.HasProperty("_BaseColor"))
                            m.SetColor("_BaseColor", color);
                        else if (m.HasProperty("_Color"))
                            m.SetColor("_Color", color);
                        else
                            continue;

                        EditorUtility.SetDirty(m);
                        changedMaterials++;
                    }

                    if (useShared) rend.sharedMaterials = mats;
                    else rend.materials = mats;

                    EditorUtility.SetDirty(rend);
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }

            return new
            {
                query = new { nameContains, useShared, color = new { r, g, b, a } },
                matchedRenderers,
                changedMaterials
            };
        }

        // command: materials.replaceByObjectNameContains
        public static object ReplaceByObjectNameContains(JToken args)
        {
            string nameContains = (string?)args?["nameContains"] ?? "";
            string newPath = (string?)args?["newMaterialAssetPath"] ?? "";
            bool useShared = (bool?)args?["useShared"] ?? true;

            if (string.IsNullOrWhiteSpace(nameContains))
                throw new ArgumentException("args.nameContains is required");
            if (string.IsNullOrWhiteSpace(newPath))
                throw new ArgumentException("args.newMaterialAssetPath is required");

            var newMat = AssetDatabase.LoadAssetAtPath<Material>(newPath);
            if (newMat == null)
                throw new InvalidOperationException($"Material not found: {newPath}");

            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

            int matchedRenderers = 0;
            int replacedSlots = 0;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Orchestrator Replace Material By Name");

            try
            {
                foreach (var rend in renderers)
                {
                    if (rend == null) continue;
                    if (rend.gameObject.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    matchedRenderers++;

                    var mats = useShared ? rend.sharedMaterials : rend.materials;
                    if (mats == null) continue;

                    bool any = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;
                        mats[i] = newMat;
                        replacedSlots++;
                        any = true;
                    }

                    if (any)
                    {
                        Undo.RecordObject(rend, "Replace Materials");
                        if (useShared) rend.sharedMaterials = mats;
                        else rend.materials = mats;
                        EditorUtility.SetDirty(rend);
                    }
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }

            return new
            {
                query = new { nameContains, newMaterialAssetPath = newPath, useShared },
                matchedRenderers,
                replacedSlots
            };
        }

        // УДАЛЕНЫ: локальные GetHierarchyPath и NameInHierarchyContains — теперь используй SceneUtils
    }
}