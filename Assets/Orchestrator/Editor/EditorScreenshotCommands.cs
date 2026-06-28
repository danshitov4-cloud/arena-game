using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class EditorScreenshotCommands
    {
        // command: editor.screenshot
        // args:
        // {
        //   "source": "game"|"scene",  // default "game"
        //   "width":  640,             // default 640
        //   "height": 480,             // default 480
        //   "superSampling": 1         // 1..4, default 1
        // }
        // Returns: { ok, source, width, height, path }
        // Reads the path with the Read tool to view the screenshot.
        public static object Capture(JToken args)
        {
            string source = ((string?)args?["source"] ?? "game").Trim().ToLower();
            int width    = Math.Max(64, Math.Min(1920, (int?)args?["width"]  ?? 640));
            int height   = Math.Max(64, Math.Min(1080, (int?)args?["height"] ?? 480));
            int ss       = Math.Max(1,  Math.Min(4,    (int?)args?["superSampling"] ?? 1));

            Camera cam = PickCamera(source);
            if (cam == null)
                return new { ok = false, error = $"No camera found for source='{source}'" };

            int renderW = width  * ss;
            int renderH = height * ss;

            var rt      = new RenderTexture(renderW, renderH, 24, RenderTextureFormat.ARGB32);
            var prevRT  = cam.targetTexture;
            var prevAct = RenderTexture.active;

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(renderW, renderH, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, renderW, renderH), 0, 0);
            tex.Apply();
            RenderTexture.active = prevAct;
            cam.targetTexture    = prevRT;
            UnityEngine.Object.DestroyImmediate(rt);

            // Downscale if superSampling > 1
            if (ss > 1)
            {
                var scaled = ScaleTexture(tex, width, height);
                UnityEngine.Object.DestroyImmediate(tex);
                tex = scaled;
            }

            string outPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "ProjectSettings", "OrchestratorScreenshot.png"));
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            return new { ok = true, source, width, height, superSampling = ss, path = outPath };
        }

        private static Camera PickCamera(string source)
        {
            if (source == "scene")
            {
                var sv = SceneView.lastActiveSceneView;
                if (sv?.camera != null) return sv.camera;
            }

            // Game camera: first active camera in scene
            var cam = Camera.main;
            if (cam != null) return cam;

            // Fallback: any camera
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindAnyObjectByType<Camera>();
#else
            return UnityEngine.Object.FindObjectOfType<Camera>();
#endif
        }

        private static Texture2D ScaleTexture(Texture2D src, int targetW, int targetH)
        {
            var rt  = RenderTexture.GetTemporary(targetW, targetH, 0);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var dst = new Texture2D(targetW, targetH, TextureFormat.RGBA32, false);
            dst.ReadPixels(new Rect(0, 0, targetW, targetH), 0, 0);
            dst.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return dst;
        }
    }
}
