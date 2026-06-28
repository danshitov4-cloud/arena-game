using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class SceneFileCommands
    {
        public static object Open(JToken args)
        {
            string path = (string?)args?["path"] ?? "";
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("args.path is required (e.g. \"Assets/ИГРА.unity\")");

            bool additive = (bool?)args?["additive"] ?? false;
            var mode = additive ? OpenSceneMode.Additive : OpenSceneMode.Single;

            var scene = EditorSceneManager.OpenScene(path, mode);
            if (!scene.IsValid())
                throw new InvalidOperationException($"Failed to open scene: {path}");

            return new
            {
                opened = new
                {
                    name    = scene.name,
                    path    = scene.path,
                    isDirty = scene.isDirty
                }
            };
        }

        public static object Save(JToken args)
        {
            bool all = (bool?)args?["all"] ?? true;

            bool saved = all
                ? EditorSceneManager.SaveOpenScenes()
                : EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

            return new { saved, path = SceneManager.GetActiveScene().path };
        }

        public static object SaveAs(JToken args)
        {
            string newPath = (string?)args?["path"] ?? "";
            if (string.IsNullOrWhiteSpace(newPath))
                throw new ArgumentException("args.path is required");

            var scene = SceneManager.GetActiveScene();
            bool saved = EditorSceneManager.SaveScene(scene, newPath);
            if (!saved)
                throw new InvalidOperationException($"Failed to save scene to: {newPath}");

            return new { saved, path = newPath };
        }

        // command: scene.new
        // args: { "path": "Assets/3D game.unity", "setup": "default"|"empty", "dryRun": false }
        // setup="default" → Main Camera + Directional Light (3D ready)
        // setup="empty"   → blank scene
        public static object New(JToken args)
        {
            string path = ((string?)args?["path"] ?? "").Trim().Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("args.path is required (e.g. \"Assets/3D game.unity\")");
            if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                path += ".unity";

            string setupStr = ((string?)args?["setup"] ?? "default").Trim().ToLower();
            var setup = setupStr == "empty"
                ? NewSceneSetup.EmptyScene
                : NewSceneSetup.DefaultGameObjects;

            bool dryRun = (bool?)args?["dryRun"] ?? false;
            if (dryRun)
                return new { ok = true, dryRun = true, path, setup = setupStr };

            var scene = EditorSceneManager.NewScene(setup, NewSceneMode.Single);
            bool saved = EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.Refresh();

            return new { ok = true, path, setup = setupStr, saved, sceneName = scene.name };
        }

        public static object GetActive(JToken _)
        {
            var scene = SceneManager.GetActiveScene();
            return new
            {
                name       = scene.name,
                path       = scene.path,
                isDirty    = scene.isDirty,
                isLoaded   = scene.isLoaded,
                rootCount  = scene.rootCount,
                buildIndex = scene.buildIndex
            };
        }
    }
}
