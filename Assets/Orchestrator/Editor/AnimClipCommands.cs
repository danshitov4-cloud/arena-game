using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class AnimClipCommands
    {
        // anim.clip.create
        // {
        //   "assetPath": "Assets/Anim/Pacman_Walk.anim",
        //   "fps": 8,
        //   "loop": true,
        //   "overwrite": false,
        //   "sprites": ["Assets/Sprites/Pacman/pacman_sheet_0", "...pacman_sheet_1", ...]
        // }
        public static object Create(JToken args)
        {
            var o = args as JObject ?? new JObject();

            string assetPath = ((string?)o["assetPath"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("args.assetPath is required (e.g. \"Assets/Anim/Walk.anim\")");

            if (o["sprites"] is not JArray spritesArr || spritesArr.Count == 0)
                throw new ArgumentException("args.sprites must be a non-empty array of sprite sub-asset paths");

            float fps      = (float?)o["fps"] ?? 8f;
            bool loop      = (bool?)o["loop"] ?? true;
            bool overwrite = (bool?)o["overwrite"] ?? false;
            bool dryRun    = (bool?)o["dryRun"] ?? false;

            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (existing != null && !overwrite)
                return new { ok = true, created = false, alreadyExists = true, assetPath };

            // Load sprite assets — they live as sub-assets inside the PNG
            var sprites = spritesArr
                .Select(t =>
                {
                    string sp = ((string?)t)?.Trim() ?? "";
                    // "Assets/Sprites/Pacman/pacman_sheet.png/pacman_sheet_0" style
                    // or "Assets/Sprites/Pacman/pacman_sheet_0" — try direct first
                    var spr = AssetDatabase.LoadAssetAtPath<Sprite>(sp);
                    if (spr != null) return spr;

                    // Try loading all sub-assets from the PNG and matching by name
                    int lastSlash = sp.LastIndexOf('/');
                    if (lastSlash > 0)
                    {
                        string parentPath = sp[..lastSlash];
                        string spriteName = sp[(lastSlash + 1)..];
                        var all = AssetDatabase.LoadAllAssetsAtPath(parentPath);
                        spr = all.OfType<Sprite>().FirstOrDefault(s => s.name == spriteName);
                    }
                    return spr;
                })
                .Where(s => s != null)
                .ToArray();

            if (sprites.Length == 0)
                throw new InvalidOperationException("No sprites could be loaded. Check paths and run project.sprites.slice first.");

            if (dryRun)
                return new { ok = true, dryRun = true, assetPath, fps, loop, spriteCount = sprites.Length };

            // Ensure output folder exists
            string dir = System.IO.Path.GetDirectoryName(assetPath)?.Replace("\\", "/") ?? "Assets";
            ProjectAssetsFolderCommands.EnsureFolders(dir);

            if (existing != null && overwrite)
                AssetDatabase.DeleteAsset(assetPath);

            var clip = new AnimationClip { frameRate = fps };

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

            var keyframes = sprites.Select((spr, i) => new ObjectReferenceKeyframe
            {
                time  = i / fps,
                value = spr
            }).ToArray();

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            AssetDatabase.CreateAsset(clip, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return new { ok = true, created = true, assetPath, fps, loop, spriteCount = sprites.Length };
        }

        // anim.controller.addState
        // {
        //   "controllerPath": "Assets/Anim/Player.controller",
        //   "stateName": "Walk",
        //   "clipPath": "Assets/Anim/Pacman_Walk.anim",
        //   "isDefault": true,
        //   "dryRun": false
        // }
        public static object AddState(JToken args)
        {
            var o = args as JObject ?? new JObject();

            string controllerPath = ((string?)o["controllerPath"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(controllerPath))
                throw new ArgumentException("args.controllerPath is required");

            string stateName = ((string?)o["stateName"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(stateName))
                throw new ArgumentException("args.stateName is required");

            string clipPath = ((string?)o["clipPath"] ?? "").Trim();
            bool isDefault  = (bool?)o["isDefault"] ?? false;
            bool dryRun     = (bool?)o["dryRun"] ?? false;

            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (ctrl == null)
                throw new InvalidOperationException($"AnimatorController not found: {controllerPath}");

            AnimationClip? clip = null;
            if (!string.IsNullOrWhiteSpace(clipPath))
            {
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                    throw new InvalidOperationException($"AnimationClip not found: {clipPath}");
            }

            var sm = ctrl.layers[0].stateMachine;
            bool stateExists = sm.states.Any(s => s.state.name == stateName);

            if (dryRun)
                return new { ok = true, dryRun = true, controllerPath, stateName, clipPath, isDefault, stateExists };

            AnimatorState state;
            if (stateExists)
                state = sm.states.First(s => s.state.name == stateName).state;
            else
                state = sm.AddState(stateName);

            if (clip != null)
                state.motion = clip;

            if (isDefault)
                sm.defaultState = state;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            return new { ok = true, controllerPath, stateName, clipPath, isDefault, stateCreated = !stateExists };
        }
    }
}
