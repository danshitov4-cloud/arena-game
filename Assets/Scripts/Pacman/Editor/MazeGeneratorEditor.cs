using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PacmanGame.Editor
{
    [CustomEditor(typeof(MazeGenerator))]
    public class MazeGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            GUILayout.Space(10);

            var gen = (MazeGenerator)target;

            if (GUILayout.Button("Generate Maze (Edit Mode)", GUILayout.Height(36)))
            {
                gen.GenerateMaze();
                EditorUtility.SetDirty(gen);
                EditorSceneManager.MarkSceneDirty(gen.gameObject.scene);
            }

            GUILayout.Space(4);

            GUI.color = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("Clear Maze", GUILayout.Height(24)))
            {
                ClearChildren("Maze");
                ClearChildren("Ghosts");
                EditorUtility.SetDirty(gen);
                EditorSceneManager.MarkSceneDirty(gen.gameObject.scene);
            }
            GUI.color = Color.white;
        }

        private static void ClearChildren(string parentName)
        {
            var parent = GameObject.Find(parentName);
            if (parent == null) return;
            for (int i = parent.transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(parent.transform.GetChild(i).gameObject);
        }
    }
}
