using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Orchestrator.Editor
{
    public static class SceneReports
    {
        // command: scene.report.updates
        // args: (пока не нужны) {}
        public static object Updates()
        {
            // Все MonoBehaviour в сценах (в Editor)
            var monos = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

            // В Unity бывают "Missing Script" (null элементы в массиве)
            int missingScripts = monos.Count(m => m == null);

            var groups = monos
                .Where(m => m != null)
                .GroupBy(m => m.GetType())
                .Select(g =>
                {
                    var t = g.Key;

                    bool hasUpdate = HasMethod(t, "Update");
                    bool hasFixedUpdate = HasMethod(t, "FixedUpdate");
                    bool hasLateUpdate = HasMethod(t, "LateUpdate");

                    // интересуют только те, у кого есть хотя бы один update-метод
                    bool any = hasUpdate || hasFixedUpdate || hasLateUpdate;

                    return new
                    {
                        type = t.FullName,
                        count = g.Count(),
                        hasUpdate,
                        hasFixedUpdate,
                        hasLateUpdate,
                        any
                    };
                })
                .Where(x => x.any)
                .OrderByDescending(x => x.count)
                .ThenBy(x => x.type)
                .ToArray();

            int totalMono = monos.Length;

            return new
            {
                summary = new
                {
                    totalMonoBehaviours = totalMono,
                    missingScripts,
                    typesWithUpdates = groups.Length
                },
                items = groups
            };
        }

        private static bool HasMethod(Type type, string name)
        {
            // Ищем даже private методы (Unity вызывает Update даже если он private)
            // DeclaredOnly = проверяем именно этот тип, а не базовые классы.
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            return type.GetMethod(name, flags) != null;
        }
    }
}