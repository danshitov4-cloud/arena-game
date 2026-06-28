using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class ProjectAssetsInspectCommands
    {
        // command: project.assets.inspect
        // args:
        // {
        //   "assetPath": "Assets/Prefabs/X.prefab", // required
        //   "includeDependencies": true,           // default true
        //   "includeSubAssets": true,              // default true
        //   "depsRecursive": true,                 // default true
        //   "depsMax": 200,                        // default 200
        //   "subAssetsMax": 50                     // default 50
        // }
        public static object Inspect(JToken args)
        {
            var o = args as JObject;
            string assetPath = ((string?)o?["assetPath"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("args.assetPath is required");

            bool includeDependencies = (bool?)o?["includeDependencies"] ?? true;
            bool depsRecursive = (bool?)o?["depsRecursive"] ?? true;
            int depsMax = (int?)o?["depsMax"] ?? 200;
            if (depsMax < 0) depsMax = 0;
            if (depsMax > 5000) depsMax = 5000;

            bool includeSubAssets = (bool?)o?["includeSubAssets"] ?? true;
            int subAssetsMax = (int?)o?["subAssetsMax"] ?? 50;
            if (subAssetsMax < 0) subAssetsMax = 0;
            if (subAssetsMax > 2000) subAssetsMax = 2000;

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(guid))
                return new { ok = false, error = $"Asset not found: {assetPath}", assetPath };

            var main = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (main == null)
                return new { ok = false, error = $"LoadMainAssetAtPath returned null: {assetPath}", assetPath, guid };

            long? fileSizeBytes = null;
            string fullPath = ToFullPath(assetPath);
            if (!string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath))
                fileSizeBytes = new FileInfo(fullPath).Length;

            object[]? subAssets = null;
            if (includeSubAssets)
            {
                var subs = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)
                    .Where(x => x != null)
                    .Take(subAssetsMax)
                    .Select(x => new { name = x.name, type = x.GetType().FullName })
                    .Cast<object>()
                    .ToArray();

                subAssets = subs;
            }

            string[]? deps = null;
            if (includeDependencies)
            {
                var all = AssetDatabase.GetDependencies(assetPath, recursive: depsRecursive)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct()
                    .OrderBy(p => p)
                    .ToArray();

                // часто возвращает сам assetPath Ч оставим, но ограничим max
                deps = depsMax == 0 ? Array.Empty<string>() : all.Take(depsMax).ToArray();
            }

            // небольша€ Уумна€Ф часть дл€ Material
            object? materialInfo = null;
            if (main is Material mat)
            {
                materialInfo = new
                {
                    shader = mat.shader != null ? mat.shader.name : null,
                    renderQueue = mat.renderQueue,
                    hasColor = mat.HasProperty("_Color") || mat.HasProperty("_BaseColor")
                };
            }

            // небольша€ Уумна€Ф часть дл€ Prefab
            object? prefabInfo = null;
            if (main is GameObject)
            {
                // Ёто может быть prefab, но не гарантировано. ѕроверим.
                var ptype = PrefabUtility.GetPrefabAssetType(main);
                prefabInfo = new
                {
                    prefabAssetType = ptype.ToString()
                };
            }

            return new
            {
                ok = true,
                assetPath,
                guid,
                main = new
                {
                    name = main.name,
                    type = main.GetType().FullName
                },
                fileSizeBytes,
                subAssets,
                dependencies = deps,
                materialInfo,
                prefabInfo
            };
        }

        private static string ToFullPath(string assetPath)
        {
            // Assets/... -> <project>/Assets/...
            // Packages/... обычно не файл в проекте (не гарантируем)
            if (assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace('/', Path.DirectorySeparatorChar));

            return ""; // не пытаемс€ угадывать Packages
        }
    }
}

