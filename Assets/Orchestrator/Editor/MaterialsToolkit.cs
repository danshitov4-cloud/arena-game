using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class MaterialsToolkit
    {
        public static object PresetsApplyAutoTarget(JToken args)
        {
            string preset = ((string?)args?["preset"] ?? "").Trim();
            string target = ((string?)args?["target"] ?? "").Trim();
            int top = (int?)args?["top"] ?? 3;
            bool useShared = (bool?)args?["useShared"] ?? true;
            bool dryRun = (bool?)args?["dryRun"] ?? false;
            bool autoRestore = (bool?)args?["autoRestore"] ?? false;

            string snapshotName = (string?)args?["snapshotName"]
                                  ?? $"mat-before-{preset}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

            if (string.IsNullOrWhiteSpace(preset))
                throw new ArgumentException("args.preset is required");
            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("args.target is required");

            var wfArgs = new JObject
            {
                ["snapshotName"] = snapshotName,
                ["preset"] = preset,
                ["nameContains"] = target,
                ["top"] = top,
                ["useShared"] = useShared,
                ["dryRun"] = dryRun,
                ["autoRestore"] = autoRestore
            };

            var wfRes = WorkflowTestPresetAutoTargetV2(wfArgs);

            return new
            {
                ok = true,
                alias = "materials.presets.applyAutoTarget",
                forwardedTo = "materials.workflow.testPresetAutoTargetV2",
                args = new { preset, target, top, useShared, dryRun, autoRestore, snapshotName },
                workflow = wfRes
            };
        }

        public static object ListPresets(JToken args)
        {
            var items = new object[]
            {
                new { name="highlight", description="Подсветка: ставит цвет (желтоватый). Пытается поставить эмиссию, если есть _EmissionColor.", changes=new[]{"color:auto(_BaseColor/_Color/_RendererColor)","_EmissionColor(if exists)"} },
                new { name="red",       description="Красный маркер: ставит красный цвет.",                                                        changes=new[]{"color:auto(_BaseColor/_Color/_RendererColor)"} },
                new { name="ghost",     description="Призрак: уменьшает альфу у цвета (делает прозрачнее).",                                       changes=new[]{"color.a:auto(_BaseColor/_Color/_RendererColor)"} },
                new { name="reset",     description="Сброс: белый цвет + эмиссия в 0 (если есть _EmissionColor).",                                changes=new[]{"color:auto(...)=white","_EmissionColor(if exists)=black"} }
            };

            return new { ok = true, count = items.Length, presets = items };
        }

        public static object WorkflowTestPresetAutoTargetV2(JToken args)
        {
            string snapshotName = (string?)args?["snapshotName"] ?? "mat-before";
            bool autoRestore = (bool?)args?["autoRestore"] ?? false;

            var takeArgs = new JObject
            {
                ["name"] = snapshotName,
                ["nameContains"] = args?["nameContains"] ?? throw new ArgumentException("args.nameContains is required"),
                ["top"] = (int?)args?["top"] ?? 3,
                ["useShared"] = (bool?)args?["useShared"] ?? true,
                ["includeAllColorAliases"] = true
            };

            var takeRes = MaterialSnapshotCommands.Take(takeArgs);
            var takeObj = JObject.FromObject(takeRes);

            if (!(bool?)takeObj["ok"] ?? false)
                return new { ok = false, error = "material snapshot take failed", take = takeRes };

            string matSnapshotId = (string?)takeObj["snapshot"]?["id"] ?? "";

            var applyArgs = new JObject
            {
                ["preset"] = args?["preset"] ?? throw new ArgumentException("args.preset is required"),
                ["nameContains"] = args?["nameContains"]!,
                ["top"] = (int?)args?["top"] ?? 3,
                ["useShared"] = (bool?)args?["useShared"] ?? true,
                ["dryRun"] = (bool?)args?["dryRun"] ?? false
            };

            var applyRes = ApplyPresetAutoTarget(applyArgs);

            object? restoreRes = null;
            if (autoRestore)
            {
                restoreRes = MaterialSnapshotCommands.Restore(new JObject
                {
                    ["id"] = matSnapshotId,
                    ["dryRun"] = false
                });
            }

            return new
            {
                ok = true,
                workflow = new { snapshotName, materialSnapshotId = matSnapshotId, autoRestore },
                take = takeRes,
                apply = applyRes,
                restore = restoreRes
            };
        }

        public static object WorkflowTestPresetAutoTarget(JToken args)
        {
            string snapshotName = (string?)args?["snapshotName"] ?? "before-preset";
            bool autoRestore = (bool?)args?["autoRestore"] ?? false;

            var takeRes = SnapshotCommands.Take(new JObject { ["name"] = snapshotName });
            var takeObj = JObject.FromObject(takeRes);

            string snapshotId =
                (string?)takeObj["snapshot"]?["id"]
                ?? (string?)takeObj["id"]
                ?? "";

            var applyArgs = new JObject
            {
                ["preset"] = args?["preset"] ?? throw new ArgumentException("args.preset is required"),
                ["nameContains"] = args?["nameContains"] ?? throw new ArgumentException("args.nameContains is required"),
                ["top"] = (int?)args?["top"] ?? 3,
                ["useShared"] = (bool?)args?["useShared"] ?? true,
                ["dryRun"] = (bool?)args?["dryRun"] ?? false
            };

            var applyRes = ApplyPresetAutoTarget(applyArgs);

            object? restoreRes = null;
            if (autoRestore)
            {
                if (string.IsNullOrWhiteSpace(snapshotId))
                    throw new InvalidOperationException("SnapshotId is empty - cannot restore.");

                restoreRes = SnapshotCommands.Restore(new JObject
                {
                    ["id"] = snapshotId,
                    ["dryRun"] = false
                });
            }

            return new
            {
                workflow = new { snapshotName, snapshotId, autoRestore },
                take = takeRes,
                apply = applyRes,
                restore = restoreRes
            };
        }

        public static object ApplyPresetAutoTarget(JToken args)
        {
            string presetName = ((string?)args?["preset"] ?? "").Trim();
            string nameContains = ((string?)args?["nameContains"] ?? "").Trim();
            int top = (int?)args?["top"] ?? 3;
            bool useShared = (bool?)args?["useShared"] ?? true;
            bool dryRun = (bool?)args?["dryRun"] ?? false;

            if (string.IsNullOrWhiteSpace(presetName)) throw new ArgumentException("args.preset is required");
            if (string.IsNullOrWhiteSpace(nameContains)) throw new ArgumentException("args.nameContains is required");
            if (top < 1) top = 1;

            var preset = PresetSpec.FromName(presetName);
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

            var usage = new Dictionary<Material, int>();
            int scopedRendererCount = 0;
            int totalSlots = 0;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                // ИЗМЕНЕНО: SceneUtils вместо локального NameInHierarchyContains
                if (!SceneUtils.NameInHierarchyContains(r.transform, nameContains)) continue;

                scopedRendererCount++;
                var shared = r.sharedMaterials;
                if (shared == null) continue;
                totalSlots += shared.Length;

                foreach (var m in shared)
                {
                    if (m == null) continue;
                    usage.TryGetValue(m, out int c);
                    usage[m] = c + 1;
                }
            }

            var selected = usage
                .OrderByDescending(kv => kv.Value)
                .Take(top)
                .Select(kv => new SelectedMat(kv.Key, kv.Value))
                .ToArray();

            if (selected.Length == 0)
                return new { ok = false, error = "No materials found in scoped renderers.", query = new { preset = preset.name, nameContains, top, useShared, dryRun }, scopedRendererCount, totalSlots };

            int matchedRenderers = 0;
            int matchedSlots = 0;
            int changedSlots = 0;

            var changedProps = new Dictionary<string, int>(StringComparer.Ordinal);
            var skippedProps = new Dictionary<string, int>(StringComparer.Ordinal);

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator Materials ApplyPresetAutoTarget");

                try
                {
                    foreach (var r in renderers)
                    {
                        if (r == null) continue;
                        if (!SceneUtils.NameInHierarchyContains(r.transform, nameContains)) continue;

                        var shared = r.sharedMaterials;
                        if (shared == null) continue;

                        Material[] edit = useShared ? shared : r.materials;
                        if (edit == null) continue;

                        bool anyMatched = false;
                        bool anyChanged = false;

                        for (int i = 0; i < shared.Length && i < edit.Length; i++)
                        {
                            var s = shared[i];
                            if (s == null || !IsSelected(selected, s)) continue;

                            anyMatched = true;
                            matchedSlots++;

                            var mToEdit = edit[i];
                            if (mToEdit == null) continue;

                            Undo.RecordObject(mToEdit, "Material ApplyPresetAutoTarget");

                            var r1 = ApplyPresetToMaterial(mToEdit, preset, dryRun: false);
                            if (r1.anyChanged) { changedSlots++; anyChanged = true; }

                            CountDict(changedProps, r1.applied);
                            CountDict(skippedProps, r1.skipped);
                        }

                        if (anyMatched) matchedRenderers++;

                        if (anyChanged)
                        {
                            Undo.RecordObject(r, "Renderer Materials Update");
                            if (useShared) r.sharedMaterials = edit;
                            else r.materials = edit;
                            EditorUtility.SetDirty(r);
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
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    if (!SceneUtils.NameInHierarchyContains(r.transform, nameContains)) continue;

                    var shared = r.sharedMaterials;
                    if (shared == null) continue;

                    Material[] edit = useShared ? shared : r.materials;
                    if (edit == null) continue;

                    bool anyMatched = false;

                    for (int i = 0; i < shared.Length && i < edit.Length; i++)
                    {
                        var s = shared[i];
                        if (s == null || !IsSelected(selected, s)) continue;

                        anyMatched = true;
                        matchedSlots++;

                        var mToEdit = edit[i];
                        if (mToEdit == null) continue;

                        var r1 = ApplyPresetToMaterial(mToEdit, preset, dryRun: true);
                        if (r1.anyChanged) changedSlots++;

                        CountDict(changedProps, r1.applied);
                        CountDict(skippedProps, r1.skipped);
                    }

                    if (anyMatched) matchedRenderers++;
                }
            }

            return new
            {
                ok = true,
                preset = preset.name,
                dryRun,
                query = new { nameContains, top, useShared },
                scopedRendererCount,
                totalSlots,
                selectedMaterials = selected.Select(x => new
                {
                    name = x.mat.name,
                    instanceId = x.mat.GetInstanceID(),
                    shader = x.mat.shader != null ? x.mat.shader.name : "",
                    assetPath = AssetDatabase.GetAssetPath(x.mat),
                    usedByRenderersInScope = x.usedBy
                }).ToArray(),
                matchedRenderers,
                matchedSlots,
                changedSlots,
                changedProps,
                skippedProps
            };

            static bool IsSelected(SelectedMat[] sel, Material m)
            {
                for (int i = 0; i < sel.Length; i++)
                    if (ReferenceEquals(sel[i].mat, m)) return true;
                return false;
            }
        }

        private readonly struct SelectedMat
        {
            public readonly Material mat;
            public readonly int usedBy;
            public SelectedMat(Material mat, int usedBy) { this.mat = mat; this.usedBy = usedBy; }
        }

        public static object ApplyPreset(JToken args)
        {
            string scope = (string?)args?["scope"] ?? "scene";
            string preset = ((string?)args?["preset"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(preset)) throw new ArgumentException("args.preset is required");

            bool dryRun = (bool?)args?["dryRun"] ?? false;
            bool useShared = (bool?)args?["useShared"] ?? true;
            string? nameContains = (string?)args?["nameContains"];

            var p = PresetSpec.FromName(preset);

            if (string.Equals(scope, "asset", StringComparison.OrdinalIgnoreCase))
            {
                var mat = ResolveMaterial(args);
                var res = ApplyPresetToMaterial(mat, p, dryRun);
                return new { scope = "asset", preset = p.name, dryRun, material = DescribeMat(mat), applied = res.applied, skipped = res.skipped };
            }

            string materialNameContains = (string?)args?["materialNameContains"] ?? "";
            if (string.IsNullOrWhiteSpace(materialNameContains))
                throw new ArgumentException("args.materialNameContains is required for scope=scene");

            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            int matchedRenderers = 0;
            int matchedMaterials = 0;
            int changedMaterials = 0;

            var changedProps = new Dictionary<string, int>(StringComparer.Ordinal);
            var skippedProps = new Dictionary<string, int>(StringComparer.Ordinal);

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator Materials ApplyPreset");

                try
                {
                    foreach (var rend in renderers)
                    {
                        if (rend == null) continue;
                        // ИЗМЕНЕНО: SceneUtils вместо локального NameInHierarchyContains
                        if (!string.IsNullOrWhiteSpace(nameContains) && !SceneUtils.NameInHierarchyContains(rend.transform, nameContains)) continue;

                        var mats = useShared ? rend.sharedMaterials : rend.materials;
                        if (mats == null) continue;

                        bool anyMatched = false;
                        bool anyChanged = false;

                        for (int i = 0; i < mats.Length; i++)
                        {
                            var m = mats[i];
                            if (m == null) continue;
                            if (m.name.IndexOf(materialNameContains, StringComparison.OrdinalIgnoreCase) < 0) continue;

                            anyMatched = true;
                            matchedMaterials++;

                            Undo.RecordObject(m, "Material ApplyPreset");

                            var r = ApplyPresetToMaterial(m, p, dryRun: false);
                            if (r.anyChanged) { changedMaterials++; anyChanged = true; }
                            CountDict(changedProps, r.applied);
                            CountDict(skippedProps, r.skipped);
                        }

                        if (anyMatched) matchedRenderers++;
                        if (anyChanged)
                        {
                            if (useShared) rend.sharedMaterials = mats;
                            else rend.materials = mats;
                            EditorUtility.SetDirty(rend);
                        }
                    }
                }
                finally { Undo.CollapseUndoOperations(group); }
            }
            else
            {
                foreach (var rend in renderers)
                {
                    if (rend == null) continue;
                    if (!string.IsNullOrWhiteSpace(nameContains) && !SceneUtils.NameInHierarchyContains(rend.transform, nameContains)) continue;

                    var mats = useShared ? rend.sharedMaterials : rend.materials;
                    if (mats == null) continue;

                    bool anyMatched = false;

                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i];
                        if (m == null) continue;
                        if (m.name.IndexOf(materialNameContains, StringComparison.OrdinalIgnoreCase) < 0) continue;

                        anyMatched = true;
                        matchedMaterials++;

                        var r = ApplyPresetToMaterial(m, p, dryRun: true);
                        if (r.anyChanged) changedMaterials++;
                        CountDict(changedProps, r.applied);
                        CountDict(skippedProps, r.skipped);
                    }

                    if (anyMatched) matchedRenderers++;
                }
            }

            return new { scope = "scene", preset = p.name, dryRun, query = new { materialNameContains, nameContains, useShared }, matchedRenderers, matchedMaterials, changedMaterials, changedProps, skippedProps };
        }

        private static void CountDict(Dictionary<string, int> target, IEnumerable<string> keys)
        {
            foreach (var k in keys) { target.TryGetValue(k, out int c); target[k] = c + 1; }
        }

        private readonly struct PresetSpec
        {
            public readonly string name;
            public readonly bool setColor; public readonly Color color;
            public readonly bool setEmission; public readonly Color emissionColor;
            public readonly bool setMetallic; public readonly float metallic;
            public readonly bool setSmoothness; public readonly float smoothness;
            public readonly bool setAlphaOnly; public readonly float alpha;

            private PresetSpec(string name, bool setColor, Color color, bool setEmission, Color emissionColor, bool setMetallic, float metallic, bool setSmoothness, float smoothness, bool setAlphaOnly, float alpha)
            {
                this.name = name; this.setColor = setColor; this.color = color;
                this.setEmission = setEmission; this.emissionColor = emissionColor;
                this.setMetallic = setMetallic; this.metallic = metallic;
                this.setSmoothness = setSmoothness; this.smoothness = smoothness;
                this.setAlphaOnly = setAlphaOnly; this.alpha = alpha;
            }

            public static PresetSpec FromName(string preset) => preset.Trim().ToLowerInvariant() switch
            {
                "highlight" => new PresetSpec("highlight", true, new Color(1f, 0.92f, 0.2f, 1f), true, new Color(1f, 0.7f, 0.1f, 1f), false, 0f, false, 0f, false, 1f),
                "red" => new PresetSpec("red", true, new Color(1f, 0.1f, 0.1f, 1f), false, default, false, 0f, false, 0f, false, 1f),
                "ghost" => new PresetSpec("ghost", false, default, false, default, false, 0f, false, 0f, true, 0.25f),
                "reset" => new PresetSpec("reset", true, new Color(1f, 1f, 1f, 1f), true, new Color(0f, 0f, 0f, 1f), false, 0f, false, 0f, false, 1f),
                _ => throw new InvalidOperationException($"Unknown preset: {preset}. Allowed: highlight, red, ghost, reset")
            };
        }

        private static (bool anyChanged, List<string> applied, List<string> skipped) ApplyPresetToMaterial(Material m, PresetSpec p, bool dryRun)
        {
            var applied = new List<string>();
            var skipped = new List<string>();
            bool any = false;

            if (p.setColor)
            {
                string prop = PickColorProp(m);
                if (string.IsNullOrWhiteSpace(prop)) skipped.Add("color");
                else { if (!dryRun) { m.SetColor(prop, p.color); EditorUtility.SetDirty(m); } applied.Add(prop); any = true; }
            }

            if (p.setAlphaOnly)
            {
                string prop = PickColorProp(m);
                if (string.IsNullOrWhiteSpace(prop)) skipped.Add("alpha");
                else { var c = m.GetColor(prop); c.a = p.alpha; if (!dryRun) { m.SetColor(prop, c); EditorUtility.SetDirty(m); } applied.Add(prop + ".a"); any = true; }
            }

            if (p.setEmission)
            {
                if (!m.HasProperty("_EmissionColor")) skipped.Add("_EmissionColor");
                else { if (!dryRun) { m.SetColor("_EmissionColor", p.emissionColor); EditorUtility.SetDirty(m); } applied.Add("_EmissionColor"); any = true; }
            }

            if (p.setMetallic)
            {
                if (!m.HasProperty("_Metallic")) skipped.Add("_Metallic");
                else { if (!dryRun) { m.SetFloat("_Metallic", p.metallic); EditorUtility.SetDirty(m); } applied.Add("_Metallic"); any = true; }
            }

            if (p.setSmoothness)
            {
                if (!m.HasProperty("_Smoothness")) skipped.Add("_Smoothness");
                else { if (!dryRun) { m.SetFloat("_Smoothness", p.smoothness); EditorUtility.SetDirty(m); } applied.Add("_Smoothness"); any = true; }
            }

            return (any, applied, skipped);
        }

        public static object SetColorAuto(JToken args)
        {
            var colorTok = args?["color"] ?? throw new ArgumentException("args.color is required");
            float r = (float?)colorTok["r"] ?? 1f;
            float g = (float?)colorTok["g"] ?? 1f;
            float b = (float?)colorTok["b"] ?? 1f;
            float a = (float?)colorTok["a"] ?? 1f;

            string scope = (string?)args?["scope"] ?? "scene";

            if (string.Equals(scope, "asset", StringComparison.OrdinalIgnoreCase))
            {
                var mat = ResolveMaterial(args);
                string prop = PickColorProp(mat);
                if (prop == "") return new { scope = "asset", material = DescribeMat(mat), ok = false, error = "No color property found" };

                return SetProperty(new JObject { ["scope"] = "asset", ["materialInstanceId"] = mat.GetInstanceID(), ["property"] = prop, ["valueColor"] = new JObject { ["r"] = r, ["g"] = g, ["b"] = b, ["a"] = a } });
            }

            string materialNameContains = (string?)args?["materialNameContains"] ?? "";
            if (string.IsNullOrWhiteSpace(materialNameContains))
                throw new ArgumentException("args.materialNameContains is required for scope=scene");

            bool useShared = (bool?)args?["useShared"] ?? true;
            string? nameContains = (string?)args?["nameContains"];

            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            Material? sampleMat = null;

            foreach (var rend in renderers)
            {
                if (rend == null) continue;
                // ИЗМЕНЕНО: SceneUtils вместо локального NameInHierarchyContains
                if (!string.IsNullOrWhiteSpace(nameContains) && !SceneUtils.NameInHierarchyContains(rend.transform, nameContains)) continue;

                var mats = useShared ? rend.sharedMaterials : rend.materials;
                if (mats == null) continue;

                foreach (var m in mats)
                {
                    if (m == null) continue;
                    if (m.name.IndexOf(materialNameContains, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    sampleMat = m;
                    break;
                }
                if (sampleMat != null) break;
            }

            if (sampleMat == null)
                return new { scope = "scene", ok = false, error = "No matching material found in scene for materialNameContains (and optional nameContains)." };

            string chosenProp = PickColorProp(sampleMat);
            if (string.IsNullOrWhiteSpace(chosenProp))
                return new { scope = "scene", ok = false, error = "Matching material has no _BaseColor/_Color/_RendererColor." };

            var args2 = new JObject { ["scope"] = "scene", ["materialNameContains"] = materialNameContains, ["useShared"] = useShared, ["property"] = chosenProp, ["valueColor"] = new JObject { ["r"] = r, ["g"] = g, ["b"] = b, ["a"] = a } };
            if (!string.IsNullOrWhiteSpace(nameContains)) args2["nameContains"] = nameContains;

            return new { ok = true, chosenProperty = chosenProp, result = SetProperty(args2) };
        }

        private static string PickColorProp(Material m)
        {
            if (m.HasProperty("_BaseColor")) return "_BaseColor";
            if (m.HasProperty("_Color")) return "_Color";
            if (m.HasProperty("_RendererColor")) return "_RendererColor";
            return "";
        }

        public static object ReportRich(JToken args)
        {
            int top = (int?)args?["top"] ?? 20;
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

            var usage = new Dictionary<Material, int>();
            int totalSlots = 0;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                totalSlots += mats.Length;
                foreach (var m in mats) { if (m == null) continue; usage.TryGetValue(m, out int c); usage[m] = c + 1; }
            }

            var topMats = usage
                .OrderByDescending(kv => kv.Value)
                .Take(Math.Max(1, top))
                .Select(kv =>
                {
                    var m = kv.Key;
                    return new
                    {
                        name = m.name,
                        instanceId = m.GetInstanceID(),
                        shader = m.shader != null ? m.shader.name : "",
                        usedByRenderers = kv.Value,
                        assetPath = AssetDatabase.GetAssetPath(m),
                        capabilities = new
                        {
                            has_BaseColor = m.HasProperty("_BaseColor"),
                            has_Color = m.HasProperty("_Color"),
                            has_RendererColor = m.HasProperty("_RendererColor"),
                            has_Metallic = m.HasProperty("_Metallic"),
                            has_Smoothness = m.HasProperty("_Smoothness"),
                            has_EmissionColor = m.HasProperty("_EmissionColor"),
                            has_MainTex = m.HasProperty("_MainTex"),
                            has_BaseMap = m.HasProperty("_BaseMap"),
                            has_MaskTex = m.HasProperty("_MaskTex"),
                            has_NormalMap = m.HasProperty("_NormalMap"),
                            has_BumpMap = m.HasProperty("_BumpMap"),
                            has_AlphaTex = m.HasProperty("_AlphaTex"),
                        },
                        sample = new
                        {
                            color = GetFirstColor(m),
                            metallic = m.HasProperty("_Metallic") ? (double)m.GetFloat("_Metallic") : (double?)null,
                            smoothness = m.HasProperty("_Smoothness") ? (double)m.GetFloat("_Smoothness") : (double?)null,
                            emissionColor = m.HasProperty("_EmissionColor") ? ToJson(m.GetColor("_EmissionColor")) : null
                        }
                    };
                }).ToArray();

            return new { summary = new { rendererCount = renderers.Length, totalMaterialSlots = totalSlots, uniqueMaterials = usage.Count }, top = topMats.Length, topMaterials = topMats };
        }

        private static object? GetFirstColor(Material m)
        {
            if (m.HasProperty("_BaseColor")) return ToJson(m.GetColor("_BaseColor"));
            if (m.HasProperty("_Color")) return ToJson(m.GetColor("_Color"));
            if (m.HasProperty("_RendererColor")) return ToJson(m.GetColor("_RendererColor"));
            return null;
        }

        public static object Inspect(JToken args)
        {
            Material mat = ResolveMaterial(args);
            var props = new List<object>();
            int count = mat.shader != null ? mat.shader.GetPropertyCount() : 0;

            for (int i = 0; i < count; i++)
            {
                var name = mat.shader.GetPropertyName(i);
                var type = mat.shader.GetPropertyType(i).ToString();
                object? value = null;

                try
                {
                    var pt = mat.shader.GetPropertyType(i);
                    switch (pt)
                    {
                        case UnityEngine.Rendering.ShaderPropertyType.Color:
                            if (mat.HasProperty(name)) value = ToJson(mat.GetColor(name)); break;
                        case UnityEngine.Rendering.ShaderPropertyType.Float:
                        case UnityEngine.Rendering.ShaderPropertyType.Range:
                            if (mat.HasProperty(name)) value = mat.GetFloat(name); break;
                        case UnityEngine.Rendering.ShaderPropertyType.Texture:
                            if (mat.HasProperty(name)) { var tex = mat.GetTexture(name); value = tex == null ? null : new { name = tex.name, instanceId = tex.GetInstanceID(), assetPath = AssetDatabase.GetAssetPath(tex) }; }
                            break;
                    }
                }
                catch { }

                props.Add(new { name, type, value });
            }

            return new { material = new { name = mat.name, instanceId = mat.GetInstanceID(), assetPath = AssetDatabase.GetAssetPath(mat), shader = mat.shader != null ? mat.shader.name : "" }, propertyCount = props.Count, properties = props };
        }

        public static object SetProperty(JToken args)
        {
            string scope = (string?)args?["scope"] ?? "scene";
            bool useShared = (bool?)args?["useShared"] ?? true;
            string prop = (string?)args?["property"] ?? "";
            if (string.IsNullOrWhiteSpace(prop)) throw new ArgumentException("args.property is required");
            if (!IsAllowedProperty(prop)) throw new InvalidOperationException($"Property '{prop}' is not allowed in MVP.");

            bool hasColor = args?["valueColor"] != null;
            bool hasFloat = args?["valueFloat"] != null;
            bool hasBool = args?["valueBool"] != null;
            bool hasTex = args?["valueTextureAssetPath"] != null;
            if ((hasColor ? 1 : 0) + (hasFloat ? 1 : 0) + (hasBool ? 1 : 0) + (hasTex ? 1 : 0) != 1)
                throw new ArgumentException("Provide exactly one of: valueColor, valueFloat, valueBool, valueTextureAssetPath");

            if (string.Equals(scope, "asset", StringComparison.OrdinalIgnoreCase))
            {
                var mat = ResolveMaterial(args);
                int changed = ApplyToMaterial(mat, prop, args);
                return new { scope = "asset", material = DescribeMat(mat), changedMaterials = changed };
            }

            string matNameContains = (string?)args?["materialNameContains"] ?? "";
            if (string.IsNullOrWhiteSpace(matNameContains)) throw new ArgumentException("args.materialNameContains is required for scope=scene");

            string? nameContains = (string?)args?["nameContains"];
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            int matchedRenderers = 0, matchedMaterials = 0, changedMaterials = 0;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Orchestrator Materials SetProperty");

            try
            {
                foreach (var rend in renderers)
                {
                    if (rend == null) continue;
                    // ИЗМЕНЕНО: SceneUtils вместо локального NameInHierarchyContains
                    if (!string.IsNullOrWhiteSpace(nameContains) && !SceneUtils.NameInHierarchyContains(rend.transform, nameContains)) continue;

                    var mats = useShared ? rend.sharedMaterials : rend.materials;
                    if (mats == null) continue;

                    bool any = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i];
                        if (m == null) continue;
                        if (m.name.IndexOf(matNameContains, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        matchedMaterials++;
                        Undo.RecordObject(m, "Material SetProperty");
                        int ch = ApplyToMaterial(m, prop, args);
                        if (ch > 0) { changedMaterials += ch; any = true; }
                    }
                    if (matchedMaterials > 0) matchedRenderers++;
                    if (any) { if (useShared) rend.sharedMaterials = mats; else rend.materials = mats; EditorUtility.SetDirty(rend); }
                }
            }
            finally { Undo.CollapseUndoOperations(group); }

            return new { scope = "scene", query = new { materialNameContains = matNameContains, nameContains, useShared, property = prop }, matchedRenderers, matchedMaterials, changedMaterials };
        }

        public static object SetColorByMaterialNameContains(JToken args)
        {
            args["scope"] = "scene";
            args["property"] = "_BaseColor";
            args["valueColor"] = args["color"] ?? throw new ArgumentException("args.color is required");
            args["materialNameContains"] = args["materialNameContains"] ?? throw new ArgumentException("args.materialNameContains is required");
            return SetProperty(args);
        }

        public static object ReplaceByMaterialNameContains(JToken args)
        {
            string matNameContains = (string?)args?["materialNameContains"] ?? "";
            string newPath = (string?)args?["newMaterialAssetPath"] ?? "";
            bool useShared = (bool?)args?["useShared"] ?? true;
            string? nameContains = (string?)args?["nameContains"];

            if (string.IsNullOrWhiteSpace(matNameContains)) throw new ArgumentException("args.materialNameContains is required");
            if (string.IsNullOrWhiteSpace(newPath)) throw new ArgumentException("args.newMaterialAssetPath is required");

            var newMat = AssetDatabase.LoadAssetAtPath<Material>(newPath);
            if (newMat == null) throw new InvalidOperationException($"Material not found: {newPath}");

            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            int matchedRenderers = 0, replacedSlots = 0;

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Orchestrator Replace Material By MaterialName");

            try
            {
                foreach (var rend in renderers)
                {
                    if (rend == null) continue;
                    // ИЗМЕНЕНО: SceneUtils вместо локального NameInHierarchyContains
                    if (!string.IsNullOrWhiteSpace(nameContains) && !SceneUtils.NameInHierarchyContains(rend.transform, nameContains)) continue;

                    var mats = useShared ? rend.sharedMaterials : rend.materials;
                    if (mats == null) continue;

                    bool any = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i];
                        if (m == null) continue;
                        if (m.name.IndexOf(matNameContains, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        mats[i] = newMat; replacedSlots++; any = true;
                    }

                    if (any) { matchedRenderers++; Undo.RecordObject(rend, "Replace Materials"); if (useShared) rend.sharedMaterials = mats; else rend.materials = mats; EditorUtility.SetDirty(rend); }
                }
            }
            finally { Undo.CollapseUndoOperations(group); }

            return new { query = new { materialNameContains = matNameContains, newMaterialAssetPath = newPath, nameContains, useShared }, matchedRenderers, replacedSlots };
        }

        private static Material ResolveMaterial(JToken args)
        {
            int matId = (int?)args?["materialInstanceId"] ?? 0;
            string assetPath = (string?)args?["materialAssetPath"] ?? "";

            if (matId != 0) { var obj = EditorUtility.InstanceIDToObject(matId); if (obj is Material m) return m; throw new InvalidOperationException($"No Material for instanceId={matId}"); }
            if (!string.IsNullOrWhiteSpace(assetPath)) { var m = AssetDatabase.LoadAssetAtPath<Material>(assetPath); if (m != null) return m; throw new InvalidOperationException($"Material not found: {assetPath}"); }
            throw new ArgumentException("Provide args.materialInstanceId or args.materialAssetPath");
        }

        private static object DescribeMat(Material mat) => new { name = mat.name, instanceId = mat.GetInstanceID(), assetPath = AssetDatabase.GetAssetPath(mat), shader = mat.shader != null ? mat.shader.name : "" };

        private static bool IsAllowedProperty(string prop) =>
            prop == "_BaseColor" || prop == "_Color" || prop == "_Metallic" ||
            prop == "_Smoothness" || prop == "_EmissionColor" || prop == "_Cutoff";

        private static int ApplyToMaterial(Material m, string prop, JToken args)
        {
            if (prop == "_BaseColor" && !m.HasProperty("_BaseColor") && m.HasProperty("_Color")) prop = "_Color";
            if (prop == "_Color" && !m.HasProperty("_Color") && m.HasProperty("_BaseColor")) prop = "_BaseColor";
            if (!m.HasProperty(prop)) return 0;

            if (args?["valueColor"] != null) { var c = args["valueColor"]!; m.SetColor(prop, new Color((float?)c["r"] ?? 1f, (float?)c["g"] ?? 1f, (float?)c["b"] ?? 1f, (float?)c["a"] ?? 1f)); EditorUtility.SetDirty(m); return 1; }
            if (args?["valueFloat"] != null) { m.SetFloat(prop, (float?)args["valueFloat"] ?? 0f); EditorUtility.SetDirty(m); return 1; }
            if (args?["valueBool"] != null) { m.SetFloat(prop, ((bool?)args["valueBool"] ?? false) ? 1f : 0f); EditorUtility.SetDirty(m); return 1; }
            if (args?["valueTextureAssetPath"] != null)
            {
                string p = (string?)args["valueTextureAssetPath"] ?? "";
                if (string.IsNullOrWhiteSpace(p)) throw new ArgumentException("valueTextureAssetPath empty");
                var tex = AssetDatabase.LoadAssetAtPath<Texture>(p);
                if (tex == null) throw new InvalidOperationException($"Texture not found: {p}");
                m.SetTexture(prop, tex); EditorUtility.SetDirty(m); return 1;
            }
            return 0;
        }

        private static object ToJson(Color c) => new { r = c.r, g = c.g, b = c.b, a = c.a };

        // УДАЛЁН: локальный NameInHierarchyContains — теперь используй SceneUtils.NameInHierarchyContains
    }
}