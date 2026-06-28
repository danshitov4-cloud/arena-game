using Unity.Plastic.Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using System.IO;
using JsonConvert = Unity.Plastic.Newtonsoft.Json.JsonConvert;
using Formatting = Unity.Plastic.Newtonsoft.Json.Formatting;

namespace Orchestrator.Editor
{
    public static class SnapshotCommands
    {
        public static object Latest(JToken args)
        {
            var last = SnapshotStore.Snapshots.Values
                .OrderByDescending(s => s.CreatedUtc)
                .FirstOrDefault();

            if (last == null)
                return new { found = false };

            return new
            {
                found = true,
                snapshot = new
                {
                    id = last.Id,
                    name = last.Name,
                    createdUtc = last.CreatedUtc,
                    entryCount = last.Entries.Count
                }
            };
        }

        // command: scene.snapshot.restoreLatest
        // args: { "dryRun": false }
        public static object RestoreLatest(JToken args)
        {
            bool dryRun = (bool?)args?["dryRun"] ?? false;

            var last = SnapshotStore.Snapshots.Values
                .OrderByDescending(s => s.CreatedUtc)
                .FirstOrDefault();

            if (last == null)
                throw new InvalidOperationException("No snapshots available.");

            // Переиспользуем Restore по id
            var restoreArgs = new JObject
            {
                ["id"] = last.Id,
                ["dryRun"] = dryRun
            };

            return Restore(restoreArgs);
        }
        public static object Save(JToken args)
        {
            SaveToDisk();
            return new { saved = true, path = SavePath, count = SnapshotStore.Snapshots.Count };
        }

        public static object Load(JToken args)
        {
            LoadFromDisk();
            return new { loaded = true, path = SavePath, count = SnapshotStore.Snapshots.Count };
        }
        private const string SavePath = "ProjectSettings/OrchestratorSnapshots.json";

        [InitializeOnLoadMethod]
        private static void AutoLoadOnEditorStart()
        {
            try { LoadFromDisk(); }
            catch (Exception ex) { Debug.LogWarning("Snapshot auto-load failed: " + ex.Message); }
        }
        private static void SaveToDisk()
        {
            var dto = new SnapshotFileDto
            {
                version = 1,
                snapshots = SnapshotStore.Snapshots.Values
                    .OrderByDescending(s => s.CreatedUtc)
                    .ToList()
            };

            var json = JsonConvert.SerializeObject(dto, Formatting.Indented);
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);
            File.WriteAllText(SavePath, json);
        }

        private static void LoadFromDisk()
        {
            if (!File.Exists(SavePath))
                return;

            var json = File.ReadAllText(SavePath);
            var dto = JsonConvert.DeserializeObject<SnapshotFileDto>(json);

            SnapshotStore.Snapshots.Clear();

            if (dto?.snapshots == null) return;

            foreach (var s in dto.snapshots)
            {
                if (!string.IsNullOrWhiteSpace(s.Id))
                    SnapshotStore.Snapshots[s.Id] = s;
            }
        }

        // DTO для файла
        [Serializable]
        private sealed class SnapshotFileDto
        {
            public int version;
            public List<Snapshot> snapshots = new();
        }
        // --------- Public Commands ---------

        // command: scene.snapshot.take
        // args: { "name": "before-test", "includeTypes": ["SortByY","BuildingView"] }  (includeTypes optional)
        public static object Take(JToken args)
        {
            string name = (string?)args?["name"] ?? $"snapshot-{DateTime.Now:yyyyMMdd-HHmmss}";
            var includeTypes = ReadStringArray(args?["includeTypes"]); // optional

            var entries = CaptureEntries(includeTypes);

            var id = Guid.NewGuid().ToString("N");
            SnapshotStore.Snapshots[id] = new Snapshot
            {
                Id = id,
                Name = name,
                CreatedUtc = DateTime.UtcNow,
                Entries = entries
            };
            SaveToDisk();

            return new
            {
                snapshot = new
                {
                    id,
                    name,
                    createdUtc = SnapshotStore.Snapshots[id].CreatedUtc,
                    entryCount = entries.Count
                }
            };
        }

        // command: scene.snapshot.restore
        // args: { "id": "<snapshotId>", "dryRun": false }
        public static object Restore(JToken args)
        {
            string id = (string?)args?["id"] ?? "";
            bool dryRun = (bool?)args?["dryRun"] ?? false;

            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("args.id is required");

            if (!SnapshotStore.Snapshots.TryGetValue(id, out var snap))
                throw new InvalidOperationException($"Snapshot not found: {id}");

            int matched = 0;
            int changed = 0;
            int missing = 0;

            // Один Undo на весь restore
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Orchestrator Restore Snapshot: {snap.Name}");

            try
            {
                foreach (var e in snap.Entries)
                {
                    var obj = EditorUtility.InstanceIDToObject(e.ComponentInstanceId);
                    if (obj is not Component comp)
                    {
                        missing++;
                        continue;
                    }

                    if (!TryGetEnabled(comp, out bool current))
                        continue;

                    matched++;

                    if (current != e.Enabled)
                    {
                        if (!dryRun)
                        {
                            Undo.RecordObject(comp, "Restore Enabled");
                            SetEnabled(comp, e.Enabled);
                            EditorUtility.SetDirty(comp);
                        }
                        changed++;
                    }
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }

            return new
            {
                snapshot = new { id = snap.Id, name = snap.Name },
                dryRun,
                matched,
                changed,
                missing
            };
        }

        // command: scene.snapshot.list
        public static object List(JToken args)
        {
            var items = SnapshotStore.Snapshots.Values
                .OrderByDescending(s => s.CreatedUtc)
                .Select(s => new
                {
                    id = s.Id,
                    name = s.Name,
                    createdUtc = s.CreatedUtc,
                    entryCount = s.Entries.Count
                })
                .ToArray();

            return new { count = items.Length, items };
        }

        // command: scene.snapshot.delete
        // args: { "id": "<snapshotId>" }
        public static object Delete(JToken args)
        {
            string id = (string?)args?["id"] ?? "";
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("args.id is required");

            bool ok = SnapshotStore.Snapshots.Remove(id);
            if (ok) SaveToDisk();
            return new { deleted = ok, id };
        }

        // --------- Capture Helpers ---------

        private static List<SnapshotEntry> CaptureEntries(HashSet<string>? includeTypes)
        {
            // Берём все компоненты, которые имеют enabled (Behaviour/Renderer/Collider)
            var comps = UnityEngine.Object.FindObjectsByType<Component>(FindObjectsSortMode.None);

            var list = new List<SnapshotEntry>(capacity: 1024);

            foreach (var c in comps)
            {
                if (c == null) continue;

                // Фильтр по типам (если задан)
                if (includeTypes != null && includeTypes.Count > 0)
                {
                    var t = c.GetType();
                    if (!includeTypes.Contains(t.Name) && !includeTypes.Contains(t.FullName ?? t.Name))
                        continue;
                }

                if (!TryGetEnabled(c, out bool enabled))
                    continue;

                list.Add(new SnapshotEntry
                {
                    ComponentInstanceId = c.GetInstanceID(),
                    Type = c.GetType().FullName ?? c.GetType().Name,
                    Enabled = enabled
                });
            }

            return list;
        }

        private static bool TryGetEnabled(Component comp, out bool enabled)
        {
            switch (comp)
            {
                case Behaviour b:
                    enabled = b.enabled;
                    return true;

                case Renderer r:
                    enabled = r.enabled;
                    return true;

                case Collider col:
                    enabled = col.enabled;
                    return true;

                default:
                    enabled = false;
                    return false;
            }
        }

        private static void SetEnabled(Component comp, bool value)
        {
            switch (comp)
            {
                case Behaviour b: b.enabled = value; break;
                case Renderer r: r.enabled = value; break;
                case Collider col: col.enabled = value; break;
                default: break;
            }
        }

        private static HashSet<string>? ReadStringArray(JToken? token)
        {
            if (token == null || token.Type != JTokenType.Array) return null;

            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var it in token)
            {
                var s = (string?)it;
                if (!string.IsNullOrWhiteSpace(s)) set.Add(s);
            }
            return set;
        }

        // --------- Storage Types ---------

        private static class SnapshotStore
        {
            public static readonly Dictionary<string,  Snapshot> Snapshots = new();
        }

        [Serializable]
        public sealed class Snapshot
        {
            public string Id = "";
            public string Name = "";
            public DateTime CreatedUtc;
            public List<SnapshotEntry> Entries = new();
        }

        [Serializable]
        public sealed class SnapshotEntry
        {
            public int ComponentInstanceId;
            public string Type = "";
            public bool Enabled;
        }
    }
}