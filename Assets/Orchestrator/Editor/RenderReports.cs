using System.Linq;
using UnityEngine;

namespace Orchestrator.Editor
{
    public static class RenderReports
    {
        // command: scene.report.renderers
        public static object Renderers()
        {
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

            int total = renderers.Length;
            int enabledCount = renderers.Count(r => r != null && r.enabled);

            int castShadows = renderers.Count(r =>
                r != null && r.enabled && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off);

            int receiveShadows = renderers.Count(r =>
                r != null && r.enabled && r.receiveShadows);

            int totalMaterialRefs = renderers.Sum(r => r != null && r.sharedMaterials != null ? r.sharedMaterials.Length : 0);

            // уникальные материалы (приблизительно)
            var uniqueMats = renderers
                .Where(r => r != null && r.sharedMaterials != null)
                .SelectMany(r => r.sharedMaterials)
                .Where(m => m != null)
                .Distinct()
                .Count();

            // Топ-10 по количеству материалов
            var topByMaterials = renderers
                .Where(r => r != null && r.sharedMaterials != null)
                .Select(r => new
                {
                    name = r.gameObject.name,
                    instanceId = r.gameObject.GetInstanceID(),
                    materialCount = r.sharedMaterials.Length,
                    shadowCastingMode = r.shadowCastingMode.ToString(),
                    receiveShadows = r.receiveShadows
                })
                .OrderByDescending(x => x.materialCount)
                .Take(10)
                .ToArray();

            return new
            {
                summary = new
                {
                    totalRenderers = total,
                    enabledRenderers = enabledCount,
                    castShadows,
                    receiveShadows,
                    totalMaterialRefs,
                    uniqueMaterials = uniqueMats
                },
                topByMaterials
            };
        }
    }
}
