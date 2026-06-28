using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class ProjectScriptsPatchCommands
    {
        // command: project.scripts.read
        // args:
        // {
        //   "path": "Assets/Scripts/TestRunner.cs", // required
        //   "maxChars": 20000                      // optional, default 20000
        // }
        public static object Read(JToken args)
        {
            var o = args as JObject;
            string path = ((string?)o?["path"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("args.path is required");
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("args.path must start with 'Assets/'");

            int maxChars = (int?)o?["maxChars"] ?? 20000;
            if (maxChars < 100) maxChars = 100;
            if (maxChars > 500000) maxChars = 500000;

            string abs = ToAbs(path);
            if (!File.Exists(abs))
                return new { ok = false, error = "File not found", path };

            string text = File.ReadAllText(abs, Encoding.UTF8);
            bool truncated = text.Length > maxChars;
            if (truncated) text = text.Substring(0, maxChars);

            return new { ok = true, path, length = new FileInfo(abs).Length, truncated, text };
        }

        // command: project.scripts.patch
        // args:
        // {
        //   "path": "Assets/Scripts/TestRunner.cs",  // required
        //   "operations": [
        //      {
        //         "op": "replace",
        //         "find": "old",
        //         "replace": "new",
        //         "count": 1                          // optional, default 1, -1 = all
        //      },
        //      {
        //         "op": "insertBetweenMarkers",
        //         "start": "// <ORCH:REGION>",
        //         "end":   "// </ORCH:REGION>",
        //         "content": "line1\nline2",
        //         "createIfMissing": true,            // default true
        //         "whereIfCreate": "end"              // start|end
        //      }
        //   ],
        //   "overwrite": true,                       // default true
        //   "dryRun": false
        // }
        public static object Patch(JToken args)
        {
            var o = args as JObject;
            if (o == null) throw new ArgumentException("args must be an object");

            string path = ((string?)o["path"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("args.path is required");
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("args.path must start with 'Assets/'");

            bool dryRun = (bool?)o["dryRun"] ?? false;
            bool overwrite = (bool?)o["overwrite"] ?? true;

            var ops = o["operations"] as JArray;
            if (ops == null || ops.Count == 0)
                throw new ArgumentException("args.operations is required (non-empty array)");

            string abs = ToAbs(path);
            if (!File.Exists(abs))
                return new { ok = false, error = "File not found", path };

            string original = File.ReadAllText(abs, Encoding.UTF8);
            string text = original;

            var reports = new List<object>();

            foreach (var opTok in ops)
            {
                if (opTok is not JObject opObj) continue;
                string op = ((string?)opObj["op"] ?? "").Trim();

                if (op.Equals("replace", StringComparison.OrdinalIgnoreCase))
                {
                    string find = (string?)opObj["find"] ?? "";
                    string repl = (string?)opObj["replace"] ?? "";
                    int count = (int?)opObj["count"] ?? 1;

                    int beforeLen = text.Length;
                    int replaced = 0;

                    if (string.IsNullOrEmpty(find))
                    {
                        reports.Add(new { op = "replace", ok = false, error = "find is empty" });
                        continue;
                    }

                    if (count == -1)
                    {
                        replaced = CountOccurrences(text, find);
                        text = text.Replace(find, repl);
                    }
                    else
                    {
                        // replace first N occurrences
                        int remaining = Math.Max(0, count);
                        int idx = 0;
                        var sb = new StringBuilder(text.Length);

                        while (remaining > 0)
                        {
                            int pos = text.IndexOf(find, idx, StringComparison.Ordinal);
                            if (pos < 0) break;

                            sb.Append(text, idx, pos - idx);
                            sb.Append(repl);
                            idx = pos + find.Length;
                            remaining--;
                            replaced++;
                        }

                        sb.Append(text, idx, text.Length - idx);
                        text = sb.ToString();
                    }

                    reports.Add(new
                    {
                        op = "replace",
                        ok = true,
                        find,
                        replaceLen = repl.Length,
                        replaced,
                        changed = text.Length != beforeLen
                    });

                    continue;
                }

                if (op.Equals("insertBetweenMarkers", StringComparison.OrdinalIgnoreCase))
                {
                    string start = ((string?)opObj["start"] ?? "").Trim();
                    string end = ((string?)opObj["end"] ?? "").Trim();
                    string content = (string?)opObj["content"] ?? "";

                    bool createIfMissing = (bool?)opObj["createIfMissing"] ?? true;
                    string whereIfCreate = ((string?)opObj["whereIfCreate"] ?? "end").Trim().ToLowerInvariant();
                    if (whereIfCreate != "start" && whereIfCreate != "end") whereIfCreate = "end";

                    if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end))
                    {
                        reports.Add(new { op = "insertBetweenMarkers", ok = false, error = "start/end required" });
                        continue;
                    }

                    int s = text.IndexOf(start, StringComparison.Ordinal);
                    int e = text.IndexOf(end, StringComparison.Ordinal);

                    bool created = false;

                    if (s < 0 || e < 0 || e < s)
                    {
                        if (!createIfMissing)
                        {
                            reports.Add(new { op = "insertBetweenMarkers", ok = false, error = "markers not found" });
                            continue;
                        }

                        // create markers
                        string block = start + "\n" + end + "\n";
                        text = whereIfCreate == "start" ? (block + text) : (text + "\n" + block);
                        created = true;

                        // re-find
                        s = text.IndexOf(start, StringComparison.Ordinal);
                        e = text.IndexOf(end, StringComparison.Ordinal);
                        if (s < 0 || e < 0 || e < s)
                        {
                            reports.Add(new { op = "insertBetweenMarkers", ok = false, error = "failed to create markers" });
                            continue;
                        }
                    }

                    int insertPos = s + start.Length;
                    // keep exactly one newline after start
                    if (insertPos < text.Length && text[insertPos] == '\r') insertPos++;
                    if (insertPos < text.Length && text[insertPos] == '\n') insertPos++;

                    // remove current content between markers (until before end)
                    int endPos = e;
                    // remove trailing newline just before end marker
                    int removeStart = insertPos;
                    int removeLen = Math.Max(0, endPos - removeStart);

                    var sb2 = new StringBuilder(text.Length + content.Length + 32);
                    sb2.Append(text, 0, removeStart);
                    sb2.Append(content);
                    if (!content.EndsWith("\n")) sb2.Append("\n");
                    sb2.Append(text, endPos, text.Length - endPos);

                    text = sb2.ToString();

                    reports.Add(new
                    {
                        op = "insertBetweenMarkers",
                        ok = true,
                        createdMarkers = created,
                        start,
                        end,
                        contentChars = content.Length
                    });

                    continue;
                }

                reports.Add(new { op, ok = false, error = "Unknown op" });
            }

            bool changedAll = !string.Equals(original, text, StringComparison.Ordinal);

            if (dryRun)
            {
                return new
                {
                    ok = true,
                    dryRun = true,
                    path,
                    changed = changedAll,
                    operations = reports,
                    preview = MakePreview(original, text, 1200)
                };
            }

            if (changedAll && overwrite)
            {
                File.WriteAllText(abs, text, Encoding.UTF8);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
            }

            return new
            {
                ok = true,
                dryRun = false,
                path,
                changed = changedAll,
                operations = reports
            };
        }

        // -------- helpers --------

        private static string ToAbs(string assetsPath)
            => Path.Combine(Directory.GetCurrentDirectory(), assetsPath.Replace('/', Path.DirectorySeparatorChar));

        private static int CountOccurrences(string text, string find)
        {
            int count = 0, idx = 0;
            while (true)
            {
                int pos = text.IndexOf(find, idx, StringComparison.Ordinal);
                if (pos < 0) break;
                count++;
                idx = pos + find.Length;
            }
            return count;
        }

        private static object MakePreview(string before, string after, int max)
        {
            // простой preview: первые N символов до/после
            string b = before.Length > max ? before.Substring(0, max) : before;
            string a = after.Length > max ? after.Substring(0, max) : after;
            return new { before = b, after = a, truncated = before.Length > max || after.Length > max };
        }
    }
}


