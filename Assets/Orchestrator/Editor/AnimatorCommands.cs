using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class AnimatorCommands
    {
        // command: anim.controller.create
        // args: { "assetPath":"Assets/Anim/Player.controller", "overwrite":false, "dryRun":false }
        public static object CreateController(JToken args)
        {
            var o = args as JObject ?? new JObject();
            string assetPath = ((string?)o["assetPath"] ?? "").Trim();
            bool overwrite = (bool?)o["overwrite"] ?? false;
            bool dryRun = (bool?)o["dryRun"] ?? false;

            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("args.assetPath is required (e.g. Assets/Anim/Player.controller)");

            if (!assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("assetPath must start with 'Assets/'");

            string dir = System.IO.Path.GetDirectoryName(assetPath)?.Replace("\\", "/") ?? "Assets";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                if (dryRun)
                    return new { ok = true, created = false, dryRun = true, wouldCreateFolders = true, assetPath };

                EnsureFolders(dir);
            }

            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
            if (existing != null && !overwrite)
                return new { ok = true, created = false, exists = true, assetPath };

            if (dryRun)
                return new { ok = true, created = true, dryRun = true, assetPath };

            if (existing != null && overwrite)
                AssetDatabase.DeleteAsset(assetPath);

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(assetPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new
            {
                ok = true,
                created = true,
                assetPath,
                controller = new { name = ctrl.name }
            };
        }

        // command: anim.controller.addParameters
        // args:
        // {
        //   "assetPath":"Assets/Anim/Player.controller",
        //   "parameters":[
        //     {"name":"Speed","type":"float","default":0},
        //     {"name":"IsGrounded","type":"bool","default":true},
        //     {"name":"Jump","type":"trigger"}
        //   ],
        //   "dryRun":false
        // }
        public static object AddParameters(JToken args)
        {
            var o = args as JObject ?? new JObject();
            string assetPath = ((string?)o["assetPath"] ?? "").Trim();
            bool dryRun = (bool?)o["dryRun"] ?? false;

            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("args.assetPath is required");

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
            if (ctrl == null)
                return new { ok = false, error = $"AnimatorController not found: {assetPath}" };

            if (o["parameters"] is not JArray arr || arr.Count == 0)
                return new { ok = false, error = "args.parameters must be a non-empty array" };

            var existing = new HashSet<string>(ctrl.parameters.Select(p => p.name), StringComparer.Ordinal);
            int added = 0;
            var addedNames = new List<string>();

            foreach (var pTok in arr)
            {
                string name = ((string?)pTok?["name"] ?? "").Trim();
                string type = ((string?)pTok?["type"] ?? "").Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (existing.Contains(name)) continue;

                var pt = type switch
                {
                    "float" => AnimatorControllerParameterType.Float,
                    "int" => AnimatorControllerParameterType.Int,
                    "bool" => AnimatorControllerParameterType.Bool,
                    "trigger" => AnimatorControllerParameterType.Trigger,
                    _ => (AnimatorControllerParameterType?)null
                };

                if (pt == null) continue;

                if (!dryRun)
                {
                    var param = new AnimatorControllerParameter { name = name, type = pt.Value };

                    if (pt == AnimatorControllerParameterType.Float)
                        param.defaultFloat = (float?)pTok?["default"] ?? 0f;
                    else if (pt == AnimatorControllerParameterType.Int)
                        param.defaultInt = (int?)pTok?["default"] ?? 0;
                    else if (pt == AnimatorControllerParameterType.Bool)
                        param.defaultBool = (bool?)pTok?["default"] ?? false;

                    ctrl.AddParameter(param);
                }

                existing.Add(name);
                added++;
                addedNames.Add(name);
            }

            if (!dryRun)
            {
                // ИСПРАВЛЕНО: SetDirty вынесен из цикла — один вызов после всех параметров
                EditorUtility.SetDirty(ctrl);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return new
            {
                ok = true,
                assetPath,
                dryRun,
                addedCount = added,
                added = addedNames
            };
        }

        // command: anim.assignControllerByQuery
        // args: { "query":{...}, "controllerAssetPath":"Assets/Anim/Player.controller", "addAnimatorIfMissing":true, "dryRun":false }
        public static object AssignControllerByQuery(JToken args)
        {
            var o = args as JObject ?? new JObject();
            bool dryRun = (bool?)o["dryRun"] ?? false;

            if (o["query"] == null)
                throw new ArgumentException("args.query is required");

            string controllerAssetPath = ((string?)o["controllerAssetPath"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(controllerAssetPath))
                throw new ArgumentException("args.controllerAssetPath is required");

            bool addAnimatorIfMissing = (bool?)o["addAnimatorIfMissing"] ?? true;

            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerAssetPath);
            if (ctrl == null)
                return new { ok = false, error = $"Controller not found: {controllerAssetPath}" };

            // ИЗМЕНЕНО: SceneUtils вместо SceneBatchProps.SceneBatch_GetObjectsFromQuery
            var gos = SceneUtils.GetObjectsFromQuery(o["query"]);

            int matched = gos.Length;
            int changed = 0;
            int skipped = 0;

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator Assign AnimatorController");

                try
                {
                    foreach (var go in gos)
                    {
                        if (go == null) { skipped++; continue; }

                        var animator = go.GetComponent<Animator>();
                        if (animator == null && addAnimatorIfMissing)
                        {
                            Undo.AddComponent<Animator>(go);
                            animator = go.GetComponent<Animator>();
                        }

                        if (animator == null) { skipped++; continue; }

                        if (animator.runtimeAnimatorController == ctrl) continue;

                        Undo.RecordObject(animator, "Assign AnimatorController");
                        animator.runtimeAnimatorController = ctrl;
                        EditorUtility.SetDirty(animator);
                        changed++;
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
                    var animator = go.GetComponent<Animator>();
                    if (animator == null && !addAnimatorIfMissing) { skipped++; continue; }
                    if (animator != null && animator.runtimeAnimatorController == ctrl) continue;
                    changed++;
                }
            }

            return new
            {
                ok = true,
                dryRun,
                controllerAssetPath,
                matchedObjects = matched,
                changedObjects = changed,
                skippedObjects = skipped
            };
        }

        // command: anim.report
        // args: { "assetPath":"Assets/Anim/Player.controller" }
        public static object Report(JToken args)
        {
            var o = args as JObject ?? new JObject();
            string assetPath = ((string?)o["assetPath"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("args.assetPath is required");

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
            if (ctrl == null)
                return new { ok = false, error = $"AnimatorController not found: {assetPath}" };

            return new
            {
                ok = true,
                assetPath,
                layers = ctrl.layers.Select(l => l.name).ToArray(),
                parameters = ctrl.parameters.Select(p => new { name = p.name, type = p.type.ToString() }).ToArray()
            };
        }

        private static void EnsureFolders(string dir)
        {
            string[] parts = dir.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !parts[0].Equals("Assets", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Folder must be under Assets/");

            string current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}