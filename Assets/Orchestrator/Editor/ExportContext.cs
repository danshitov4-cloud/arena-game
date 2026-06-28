using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using ZipCompressionLevel = System.IO.Compression.CompressionLevel;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class ExportContextCommands
    {
        private const string DefaultZipPath = "ProjectSettings/OrchestratorContext.zip";

        // command: project.exportContext
        // args (optional):
        // {
        //   "zipPath": "ProjectSettings/OrchestratorContext.zip",
        //   "include": ["path1","path2"],       // override default list
        //   "add": ["extra1","extra2"],          // add extra paths
        //   "exclude": ["pathToSkip"],           // exclude some paths
        //   "includeProjectSettingsSchema": true // include ProjectSettings/OrchestratorHelpSchema.json
        // }
        public static object Export(JToken args)
        {
            var o = args as JObject;

            string zipPath = ((string?)o?["zipPath"] ?? DefaultZipPath).Trim();
            if (string.IsNullOrWhiteSpace(zipPath)) zipPath = DefaultZipPath;

            bool includeProjectSettingsSchema = (bool?)o?["includeProjectSettingsSchema"] ?? true;

            // ---- default include list ----
            var files = new List<string>
            {
                "Assets/Orchestrator/Editor/CommandDispatcher.cs",
                "Assets/Orchestrator/Editor/HelpSchema.cs",
                "Assets/Orchestrator/ORCHESTRATOR_SPEC.md",
            };

            if (includeProjectSettingsSchema)
                files.Add("ProjectSettings/OrchestratorHelpSchema.json");

            // override include (если передали include — используем только его)
            if (o?["include"] is JArray includeArr && includeArr.Count > 0)
            {
                files = includeArr.Select(x => ((string?)x ?? "").Trim())
                                  .Where(s => !string.IsNullOrWhiteSpace(s))
                                  .ToList();
            }

            // add extras
            if (o?["add"] is JArray addArr && addArr.Count > 0)
            {
                foreach (var t in addArr)
                {
                    var p = ((string?)t ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(p)) files.Add(p);
                }
            }

            // exclude
            var exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (o?["exclude"] is JArray exArr && exArr.Count > 0)
            {
                foreach (var t in exArr)
                {
                    var p = ((string?)t ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(p)) exclude.Add(Norm(p));
                }
            }

            files = files.Where(p => !exclude.Contains(Norm(p)))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .ToList();

            // normalize to absolute and check exist
            var included = new List<object>();
            var missing = new List<object>();

            string zipAbs = Path.GetFullPath(zipPath);
            EnsureDirForFile(zipAbs);

            // recreate zip
            if (File.Exists(zipAbs)) File.Delete(zipAbs);

            using (var zip = ZipFile.Open(zipAbs, ZipArchiveMode.Create))
            {
                foreach (var rel in files)
                {
                    if (string.IsNullOrWhiteSpace(rel)) continue;

                    string abs = Path.GetFullPath(rel);
                    if (!File.Exists(abs))
                    {
                        missing.Add(new { path = rel });
                        continue;
                    }

                    // entry path inside zip should be relative (pretty)
                    string entryName = rel.Replace("\\", "/");


                    zip.CreateEntryFromFile(abs, entryName, ZipCompressionLevel.Optimal);


                    included.Add(new
                    {
                        path = rel,
                        bytes = new FileInfo(abs).Length
                    });
                }
            }

            AssetDatabase.Refresh();

            return new
            {
                ok = true,
                zipPath = zipPath,
                zipAbsolute = zipAbs,
                includedCount = included.Count,
                missingCount = missing.Count,
                included,
                missing
            };
        }

        private static string Norm(string p) => p.Replace('\\', '/').Trim();

        private static void EnsureDirForFile(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
