using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class MaterialSnapshotCommands
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            Converters = new List<JsonConverter> { new ColorRgbaConverter() },
            ReferenceLoopHandling = ReferenceLoopHandling.Error
        };

        private const string SavePath = "ProjectSettings/OrchestratorMaterialSnapshots.json";

        private static readonly Dictionary<string, MaterialSnapshot> _snapshots =
            new Dictionary<string, MaterialSnapshot>(StringComparer.OrdinalIgnoreCase);

        private static bool AutoPersistEnabled => true;

        [InitializeOnLoadMethod]
        private static void AutoInit()
        {
            if (!AutoPersistEnabled) return;

            try { LoadInternal(merge: true); } catch { }

            AssemblyReloadEvents.beforeAssemblyReload -= AutoSaveInternal;
            AssemblyReloadEvents.beforeAssemblyReload += AutoSaveInternal;

            EditorApplication.quitting -= AutoSaveInternal;
            EditorApplication.quitting += AutoSaveInternal;
        }

        private static void AutoSaveInternal()
        {
            if (!AutoPersistEnabled) return;
            try { SaveInternal(); } catch { }
        }

        private static void EnsureProjectSettingsDir()
        {
            if (!Directory.Exists("ProjectSettings"))
                Directory.CreateDirectory("ProjectSettings");
        }

        public static object Save(JToken args) => SaveInternal();

        public static object Load(JToken args)
        {
            bool merge = (bool?)args?["merge"] ?? true;
            return LoadInternal(merge);
        }

        private static object SaveInternal()
        {
            EnsureProjectSettingsDir();

            var payload = new
            {
                version = 1,
                savedUtc = DateTime.UtcNow.ToString("o"),
                count = _snapshots.Count,
                snapshots = _snapshots.Values
                    .OrderByDescending(s => s.createdUtc)
                    .ToArray()
            };

            File.WriteAllText(SavePath, JsonConvert.SerializeObject(payload, JsonSettings), Encoding.UTF8);
            return new { ok = true, saved = true, path = SavePath, count = _snapshots.Count };
        }

        private static object LoadInternal(bool merge)
        {
            if (!File.Exists(SavePath))
                return new { ok = true, loaded = false, path = SavePath, reason = "file not found" };

            var root = JObject.Parse(File.ReadAllText(SavePath, Encoding.UTF8));
            var arr = root["snapshots"] as JArray;

            if (arr == null)
                return new { ok = false, error = "Invalid file format: snapshots missing", path = SavePath };

            if (!merge) _snapshots.Clear();

            int added = 0;
            foreach (var token in arr)
            {
                var serializer = JsonSerializer.Create(JsonSettings);
                var snap = token.ToObject<MaterialSnapshot>(serializer);
                if (snap == null || string.IsNullOrWhiteSpace(snap.id)) continue;

                snap.id = NormalizeId(snap.id);
                _snapshots[snap.id] = snap;
                added++;
            }

            return new { ok = true, loaded = true, path = SavePath, merge, added, total = _snapshots.Count };
        }

        // command: materials.snapshot.take
        public static object Take(JToken args)
        {
            string name = (string?)args?["name"] ?? $"mat-snap-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            string nameContains = ((string?)args?["nameContains"] ?? "").Trim();
            int top = (int?)args?["top"] ?? 3;
            bool useShared = (bool?)args?["useShared"] ?? true;
            bool includeAllColorAliases = (bool?)args?["includeAllColorAliases"] ?? true;

            if (string.IsNullOrWhiteSpace(nameContains))
                throw new ArgumentException("args.nameContains is required");

            if (top < 1) top = 1;

            var requestedProps = new HashSet<string>(StringComparer.Ordinal);
            if (args?["properties"] is JArray propsArr)
            {
                foreach (var p in propsArr)
                {
                    var s = ((string?)p ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(s)) requestedProps.Add(s);
                }
            }
            if (requestedProps.Count == 0)
            {
                requestedProps.Add("_Color");
                requestedProps.Add("_BaseColor");
                requestedProps.Add("_RendererColor");
                requestedProps.Add("_EmissionColor");
                requestedProps.Add("_ZWrite");
            }

            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            var usage = new Dictionary<Material, int>();

            foreach (var r in renderers)
            {
                if (r == null) continue;
                // ИЗМЕНЕНО: SceneUtils вместо локального NameInHierarchyContains
                if (!SceneUtils.NameInHierarchyContains(r.transform, nameContains)) continue;

                var mats = r.sharedMaterials;
                if (mats == null) continue;

                foreach (var m in mats)
                {
                    if (m == null) continue;
                    usage.TryGetValue(m, out int c);
                    usage[m] = c + 1;
                }
            }

            var selected = usage
                .OrderByDescending(kv => kv.Value)
                .Take(top)
                .Select(kv => kv.Key)
                .ToArray();

            if (selected.Length == 0)
                return new { ok = false, error = "No materials found in scoped renderers.", query = new { name, nameContains, top, useShared } };

            var entries = new List<SnapshotEntry>();
            foreach (var mat in selected)
            {
                if (mat == null) continue;

                var propsToSave = new HashSet<string>(requestedProps, StringComparer.Ordinal);
                if (includeAllColorAliases) { propsToSave.Add("_BaseColor"); propsToSave.Add("_Color"); propsToSave.Add("_RendererColor"); }

                foreach (var prop in propsToSave)
                {
                    if (!mat.HasProperty(prop)) continue;
                    var type = DetectPropertyType(prop);
                    if (type == PropType.Unsupported) continue;
                    var entry = SnapshotEntry.Capture(mat, prop, type);
                    if (entry != null) entries.Add(entry.Value);
                }
            }

            string id = NormalizeId(Guid.NewGuid().ToString("N"));
            var snap = new MaterialSnapshot { id = id, name = name, createdUtc = DateTime.UtcNow, nameContains = nameContains, top = top, useShared = useShared, entries = entries };
            _snapshots[id] = snap;

            if (AutoPersistEnabled) { try { SaveInternal(); } catch { } }

            return new
            {
                ok = true,
                snapshot = new
                {
                    id,
                    name,
                    createdUtc = snap.createdUtc.ToString("o"),
                    entryCount = entries.Count,
                    selectedMaterialCount = selected.Length,
                    selectedMaterials = selected.Select(m => new
                    {
                        name = m.name,
                        instanceId = m.GetInstanceID(),
                        shader = m.shader != null ? m.shader.name : "",
                        assetPath = AssetDatabase.GetAssetPath(m)
                    }).ToArray()
                }
            };
        }

        // command: materials.snapshot.restore
        public static object Restore(JToken args)
        {
            string id = NormalizeId((string?)args?["id"]);
            bool dryRun = (bool?)args?["dryRun"] ?? false;

            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("args.id is required");

            if (!_snapshots.TryGetValue(id, out var snap))
            {
                string sampleKeys = string.Join(", ", _snapshots.Keys.Take(5));
                throw new InvalidOperationException($"MaterialSnapshot not found: '{id}'. Total={_snapshots.Count}. SampleKeys=[{sampleKeys}]");
            }

            int matched = 0, changed = 0, missing = 0;

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator MaterialSnapshot Restore");

                try
                {
                    foreach (var e in snap.entries)
                    {
                        var obj = EditorUtility.InstanceIDToObject(e.materialInstanceId);
                        if (obj is not Material mat || mat == null) { missing++; continue; }

                        matched++;
                        if (!mat.HasProperty(e.property)) { missing++; continue; }

                        Undo.RecordObject(mat, "Restore Material Property");

                        bool didChange = e.ApplyTo(mat, dryRun: false);
                        if (didChange) { changed++; EditorUtility.SetDirty(mat); }
                    }
                }
                finally { Undo.CollapseUndoOperations(group); }
            }
            else
            {
                foreach (var e in snap.entries)
                {
                    var obj = EditorUtility.InstanceIDToObject(e.materialInstanceId);
                    if (obj is not Material mat || mat == null) { missing++; continue; }
                    matched++;
                    if (!mat.HasProperty(e.property)) { missing++; continue; }
                    if (e.ApplyTo(mat, dryRun: true)) changed++;
                }
            }

            return new { ok = true, snapshot = new { id = snap.id, name = snap.name, createdUtc = snap.createdUtc.ToString("o") }, dryRun, matched, changed, missing };
        }

        // command: materials.snapshot.list
        public static object List(JToken args)
        {
            int max = (int?)args?["max"] ?? 50;
            var items = _snapshots.Values
                .OrderByDescending(s => s.createdUtc)
                .Take(Math.Max(1, max))
                .Select(s => new { id = s.id, name = s.name, createdUtc = s.createdUtc.ToString("o"), entryCount = s.entries.Count, nameContains = s.nameContains, top = s.top })
                .ToArray();

            return new { ok = true, count = items.Length, items };
        }

        // command: materials.snapshot.latest
        public static object Latest(JToken args)
        {
            var latest = _snapshots.Values.OrderByDescending(s => s.createdUtc).FirstOrDefault();
            if (latest == null) return new { ok = true, found = false };

            return new { ok = true, found = true, snapshot = new { id = latest.id, name = latest.name, createdUtc = latest.createdUtc.ToString("o"), entryCount = latest.entries.Count } };
        }

        // УДАЛЁН: локальный NameInHierarchyContains — теперь используй SceneUtils.NameInHierarchyContains

        private static string NormalizeId(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Trim();
            var sb = new StringBuilder(s.Length);
            foreach (char ch in s)
            {
                bool isHex = (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
                if (isHex) sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        internal enum PropType { Unsupported, Color, Float }

        private static PropType DetectPropertyType(string prop)
        {
            if (prop == "_Color" || prop == "_BaseColor" || prop == "_RendererColor" || prop == "_EmissionColor")
                return PropType.Color;
            if (prop == "_ZWrite" || prop == "_EnableExternalAlpha" || prop == "_Cutoff" || prop == "_Metallic" || prop == "_Smoothness")
                return PropType.Float;
            return PropType.Float;
        }

        internal readonly struct SnapshotEntry
        {
            public readonly int materialInstanceId;
            public readonly string property;
            public readonly PropType type;
            public readonly Color color;
            public readonly float f;

            private SnapshotEntry(int id, string prop, PropType type, Color c, float f)
            {
                materialInstanceId = id; property = prop; this.type = type; color = c; this.f = f;
            }

            internal static SnapshotEntry? Capture(Material mat, string prop, PropType type)
            {
                int id = mat.GetInstanceID();
                try
                {
                    if (type == PropType.Color) { var c = mat.GetColor(prop); return new SnapshotEntry(id, prop, type, c, 0f); }
                    var v = mat.GetFloat(prop);
                    return new SnapshotEntry(id, prop, type, default, v);
                }
                catch { return null; }
            }

            public bool ApplyTo(Material mat, bool dryRun)
            {
                try
                {
                    if (type == PropType.Color) { var current = mat.GetColor(property); bool diff = !Approximately(current, color); if (!dryRun) mat.SetColor(property, color); return diff; }
                    var currentF = mat.GetFloat(property); bool diffF = Math.Abs(currentF - f) > 1e-6f; if (!dryRun) mat.SetFloat(property, f); return diffF;
                }
                catch { return false; }
            }

            private static bool Approximately(Color a, Color b) =>
                Mathf.Abs(a.r - b.r) < 1e-5f && Mathf.Abs(a.g - b.g) < 1e-5f &&
                Mathf.Abs(a.b - b.b) < 1e-5f && Mathf.Abs(a.a - b.a) < 1e-5f;
        }

        internal sealed class MaterialSnapshot
        {
            public string id = ""; public string name = ""; public DateTime createdUtc;
            public string nameContains = ""; public int top; public bool useShared;
            public List<SnapshotEntry> entries = new();
        }
    }

    internal sealed class ColorRgbaConverter : JsonConverter
    {
        public override bool CanConvert(System.Type objectType) => objectType == typeof(Color);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var c = (Color)value;
            writer.WriteStartObject();
            writer.WritePropertyName("r"); writer.WriteValue(c.r);
            writer.WritePropertyName("g"); writer.WriteValue(c.g);
            writer.WritePropertyName("b"); writer.WriteValue(c.b);
            writer.WritePropertyName("a"); writer.WriteValue(c.a);
            writer.WriteEndObject();
        }

        public override object ReadJson(JsonReader reader, System.Type objectType, object existingValue, JsonSerializer serializer)
        {
            var o = JObject.Load(reader);
            return new Color((float?)o["r"] ?? 0f, (float?)o["g"] ?? 0f, (float?)o["b"] ?? 0f, (float?)o["a"] ?? 1f);
        }
    }
}