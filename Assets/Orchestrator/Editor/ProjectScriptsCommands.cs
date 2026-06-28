using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class ProjectScriptsCommands
    {
        // command: project.scripts.create
        // args:
        // {
        //   "path": "Assets/Scripts/PlayerController.cs", // required
        //   "template": "MonoBehaviour|ScriptableObject|Plain", // default MonoBehaviour
        //   "className": "PlayerController",             // optional (если пусто Ч из имени файла)
        //   "namespace": "Game",                         // optional
        //   "overwrite": false,                          // default false
        //   "dryRun": false
        // }
        public static object Create(JToken args)
        {
            var o = args as JObject;
            if (o == null) throw new ArgumentException("args must be an object");

            string path = ((string?)o["path"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("args.path is required");
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("args.path must start with 'Assets/'");

            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                path += ".cs";

            string template = ((string?)o["template"] ?? "MonoBehaviour").Trim();
            string ns = ((string?)o["namespace"] ?? "").Trim();
            bool overwrite = (bool?)o["overwrite"] ?? false;
            bool dryRun = (bool?)o["dryRun"] ?? false;

            string className = ((string?)o["className"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(className))
                className = Path.GetFileNameWithoutExtension(path);

            if (!IsValidIdentifier(className))
                throw new ArgumentException($"Invalid className: '{className}'");

            string dir = Path.GetDirectoryName(path) ?? "Assets";
            string absDir = Path.Combine(Directory.GetCurrentDirectory(), dir);
            string absPath = Path.Combine(Directory.GetCurrentDirectory(), path);

            bool exists = File.Exists(absPath);
            if (exists && !overwrite)
                return new { ok = false, error = "File already exists. Set overwrite=true to replace.", path };

            string code = BuildCode(template, className, ns);

            if (dryRun)
            {
                return new
                {
                    ok = true,
                    dryRun = true,
                    path,
                    className,
                    @namespace = string.IsNullOrWhiteSpace(ns) ? null : ns,
                    template,
                    wouldOverwrite = exists,
                    preview = code.Length > 800 ? code.Substring(0, 800) + "\n..." : code
                };
            }

            Directory.CreateDirectory(absDir);
            File.WriteAllText(absPath, code);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            return new
            {
                ok = true,
                dryRun = false,
                path,
                className,
                @namespace = string.IsNullOrWhiteSpace(ns) ? null : ns,
                template,
                overwritten = exists
            };
        }

        private static string BuildCode(string template, string className, string ns)
        {
            string header = "using UnityEngine;\n";

            string body = template switch
            {
                "ScriptableObject" => $@"
[CreateAssetMenu(menuName = ""{className}"")]
public class {className} : ScriptableObject
{{
}}
".Trim(),

                "Plain" => $@"
public class {className}
{{
}}
".Trim(),

                _ => $@"
public class {className} : MonoBehaviour
{{
    // Start is called before the first frame update
    void Start()
    {{
    }}

    // Update is called once per frame
    void Update()
    {{
    }}
}}
".Trim()
            };

            if (string.IsNullOrWhiteSpace(ns))
                return header + "\n" + body + "\n";

            return header + "\n" + $"namespace {ns}\n{{\n" + Indent(body, 4) + "\n}\n";
        }

        private static string Indent(string s, int spaces)
        {
            var pad = new string(' ', spaces);
            var lines = s.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = pad + lines[i];
            return string.Join("\n", lines);
        }

        private static bool IsValidIdentifier(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            if (!(char.IsLetter(s[0]) || s[0] == '_')) return false;
            for (int i = 1; i < s.Length; i++)
                if (!(char.IsLetterOrDigit(s[i]) || s[i] == '_')) return false;
            return true;
        }
    }
}
