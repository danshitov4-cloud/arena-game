using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class SceneDeleteCommands
    {
        // command: scene.deleteByName
        // args: { "name":"GridRoot", "includeInactive":true, "max":50, "dryRun":false }
        public static object DeleteByName(JToken args)
        {
            var o = args as JObject ?? new JObject();

            string name = ((string?)o["name"] ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("args.name is required");

            bool includeInactive = (bool?)o["includeInactive"] ?? true;
            bool dryRun = (bool?)o["dryRun"] ?? false;

            int max = (int?)o["max"] ?? 50;
            if (max < 1) max = 1;
            if (max > 5000) max = 5000;

            var all = UnityEngine.Object.FindObjectsByType<GameObject>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            var targets = all.Where(go => go != null && go.name == name).Take(max).ToArray();

            int deleted = 0;

            if (!dryRun)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Orchestrator DeleteByName");

                try
                {
                    foreach (var go in targets)
                    {
                        if (go == null) continue;
                        Undo.DestroyObjectImmediate(go);
                        deleted++;
                    }
                }
                finally
                {
                    Undo.CollapseUndoOperations(group);
                }
            }

            return new
            {
                ok = true,
                dryRun,
                name,
                matched = targets.Length,
                deleted
            };
        }
    }
}
