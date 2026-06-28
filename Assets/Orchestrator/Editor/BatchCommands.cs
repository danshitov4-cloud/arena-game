using Unity.Plastic.Newtonsoft.Json.Linq;
using System;
using System.Linq;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Orchestrator.Editor
{
    public static class BatchCommands
    {
        // command: scene.batchSetEnabledByType
        // args: { "typeName": "SortByY", "enabled": false, "max": 1000 }
        public static object BatchSetEnabledByType(JToken args)
        {
            string typeName = (string?)args?["typeName"] ?? "";
            bool enabled = (bool?)args?["enabled"] ?? true;
            int max = (int?)args?["max"] ?? 10000;

            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException("args.typeName is required");

            var monos = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(m => m != null)
                .ToArray();

            int changed = 0;
            int matched = 0;

            // Группируем Undo одним шагом
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Orchestrator Batch SetEnabled {typeName} -> {enabled}");

            foreach (var m in monos)
            {
                if (matched >= max) break;

                var t = m.GetType();
                if (!TypeMatches(t, typeName)) continue;

                matched++;

                if (m.enabled != enabled)
                {
                    Undo.RecordObject(m, "Set Enabled");
                    m.enabled = enabled;
                    EditorUtility.SetDirty(m);
                    changed++;
                }
            }

            Undo.CollapseUndoOperations(group);

            return new
            {
                typeName,
                enabled,
                matched,
                changed
            };
        }

        private static bool TypeMatches(Type t, string typeName)
        {
            // принимаем:
            // "SortByY" (краткое имя)
            // "Namespace.SortByY" (полное)
            return string.Equals(t.Name, typeName, StringComparison.Ordinal)
                || string.Equals(t.FullName, typeName, StringComparison.Ordinal);
        }
    }
}