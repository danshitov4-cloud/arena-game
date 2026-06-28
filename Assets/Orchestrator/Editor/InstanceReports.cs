using System;
using System.Linq;
using UnityEngine;
using Unity.Plastic.Newtonsoft.Json.Linq;

namespace Orchestrator.Editor
{
    public static class InstanceReports
    {
        // command: scene.report.instancesByType
        // args: { "typeName": "SortByY", "max": 200 }
        public static object InstancesByType(JToken args)
        {
            string typeName = (string?)args?["typeName"] ?? "";
            int max = (int?)args?["max"] ?? 200;

            if (string.IsNullOrWhiteSpace(typeName))
                throw new ArgumentException("args.typeName is required");

            var monos = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(m => m != null)
                .ToArray();

            bool Match(Type t) =>
                string.Equals(t.Name, typeName, StringComparison.Ordinal) ||
                string.Equals(t.FullName, typeName, StringComparison.Ordinal);

            var matches = monos
                .Where(m => Match(m.GetType()))
                .Take(max)
                .Select(m => new
                {
                    componentInstanceId = m.GetInstanceID(),
                    gameObjectInstanceId = m.gameObject.GetInstanceID(),
                    name = m.gameObject.name,
                    // ИЗМЕНЕНО: SceneUtils вместо локального GetHierarchyPath
                    path = SceneUtils.GetHierarchyPath(m.transform),
                    enabled = m.enabled
                })
                .ToArray();

            return new
            {
                typeName,
                count = matches.Length,
                items = matches
            };
        }

        // УДАЛЁН: локальный GetHierarchyPath — теперь используй SceneUtils.GetHierarchyPath
    }
}