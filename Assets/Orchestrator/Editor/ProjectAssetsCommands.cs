using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class ProjectAssetsCommands
    {
        // command: project.assets.find
        // args:
        // {
        //   "nameContains": "Building",      // required
        //   "type": "Prefab|Material|AudioClip|Sprite|ScriptableObject|Scene|Any", // default Any
        //   "folder": "Assets",             // optional: Assets/... or Packages/...
        //   "max": 20                       // default 20
        // }
        public static object Find(JToken args)
        {
            var o = args as JObject ?? new JObject();

            string nameContains = ((string?)o["nameContains"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(nameContains))
                throw new ArgumentException("args.nameContains is required");

            string type = ((string?)o["type"] ?? "Any").Trim();
            string folder = ((string?)o["folder"] ?? "").Trim();

            int max = (int?)o["max"] ?? 20;
            if (max < 1) max = 1;
            if (max > 200) max = 200;

            string filter = type switch
            {
                "Prefab" => "t:Prefab",
                "Material" => "t:Material",
                "AudioClip" => "t:AudioClip",
                "Sprite" => "t:Sprite",
                "ScriptableObject" => "t:ScriptableObject",
                "Scene" => "t:Scene",
                _ => "" // Any
            };

            string query = string.IsNullOrEmpty(filter)
                ? nameContains
                : $"{nameContains} {filter}";

            string[] searchIn = string.IsNullOrWhiteSpace(folder) ? Array.Empty<string>() : new[] { folder };

            string[] guids = searchIn.Length == 0
                ? AssetDatabase.FindAssets(query)
                : AssetDatabase.FindAssets(query, searchIn);

            var items = guids
                .Select(g =>
                {
                    string path = AssetDatabase.GUIDToAssetPath(g);
                    var obj = AssetDatabase.LoadMainAssetAtPath(path);
                    return new
                    {
                        guid = g,
                        path,
                        name = obj != null ? obj.name : System.IO.Path.GetFileNameWithoutExtension(path),
                        assetType = obj != null ? obj.GetType().FullName : "unknown"
                    };
                })
                .Where(x => x.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0
                            || x.path.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(max)
                .ToArray();

            return new
            {
                ok = true,
                query = new { nameContains, type, folder, max },
                found = items.Length,
                items
            };
        }
    }
}

