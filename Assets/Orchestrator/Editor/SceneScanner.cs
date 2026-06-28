using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Orchestrator.Editor
{
    public static class SceneScanner
    {
        public static object ScanActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();

            var allGos = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            var monos = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

            int nullScripts = monos.Count(m => m == null);

            int rendererCount = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length;
            int lightCount = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length;
            int colliderCount = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length;

            int componentCount = 0;
            foreach (var go in allGos)
                componentCount += go.GetComponents<Component>().Length;

            return new
            {
                scene = scene.name,
                rootCount = roots.Length,
                gameObjectCount = allGos.Length,
                componentCount,
                monoBehaviourCount = monos.Length,
                nullScriptCount = nullScripts,
                rendererCount,
                lightCount,
                colliderCount
            };
        }
    }
}