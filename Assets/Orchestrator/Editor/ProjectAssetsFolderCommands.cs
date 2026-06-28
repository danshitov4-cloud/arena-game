using System;
using System.IO;
using UnityEditor;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class ProjectAssetsFolderCommands
    {
        public static object CreateFolder(JToken args)
        {
            string path = ((string?)args?["path"] ?? "").Trim().Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("args.path is required (e.g. \"Assets/Sprites/Pacman\")");
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("path must start with 'Assets/'");

            bool dryRun = (bool?)args?["dryRun"] ?? false;
            bool alreadyExists = AssetDatabase.IsValidFolder(path);

            if (dryRun)
                return new { ok = true, dryRun = true, path, alreadyExists };

            if (alreadyExists)
                return new { ok = true, created = false, alreadyExists = true, path };

            EnsureFolders(path);
            AssetDatabase.Refresh();

            return new { ok = true, created = true, path };
        }

        public static object Refresh(JToken args)
        {
            string path = ((string?)args?["path"] ?? "").Trim();

            if (!string.IsNullOrWhiteSpace(path))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            else
                AssetDatabase.Refresh();

            return new { ok = true, refreshed = string.IsNullOrWhiteSpace(path) ? "all" : path };
        }

        // project.assets.importFile
        // { "source": "C:/Users/User/Desktop/pacman_sheet.png",
        //   "destination": "Assets/Sprites/Pacman/pacman_sheet.png",
        //   "overwrite": false, "dryRun": false }
        public static object ImportFile(JToken args)
        {
            string source = ((string?)args?["source"] ?? "").Trim();
            string dest   = ((string?)args?["destination"] ?? "").Trim().Replace("\\", "/");
            bool overwrite = (bool?)args?["overwrite"] ?? false;
            bool dryRun    = (bool?)args?["dryRun"] ?? false;

            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("args.source is required — absolute path to the external file");
            if (string.IsNullOrWhiteSpace(dest))
                throw new ArgumentException("args.destination is required (e.g. \"Assets/Sprites/Pacman/pacman_sheet.png\")");
            if (!dest.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("destination must start with 'Assets/'");
            if (!File.Exists(source))
                throw new FileNotFoundException($"Source file not found: {source}");

            bool destExists = File.Exists(dest);
            if (destExists && !overwrite)
                return new { ok = true, copied = false, alreadyExists = true, destination = dest };

            if (dryRun)
                return new { ok = true, dryRun = true, source, destination = dest, overwrite };

            string destDir = Path.GetDirectoryName(dest)?.Replace("\\", "/") ?? "Assets";
            EnsureFolders(destDir);

            File.Copy(source, dest, overwrite);
            AssetDatabase.ImportAsset(dest, ImportAssetOptions.ForceUpdate);

            return new { ok = true, copied = true, source, destination = dest };
        }

        internal static void EnsureFolders(string dir)
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
