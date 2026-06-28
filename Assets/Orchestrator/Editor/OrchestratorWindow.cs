using UnityEditor;
using UnityEngine;

namespace Orchestrator.Editor
{
    public class OrchestratorWindow : EditorWindow
    {
        private int _port = 5137;

        [MenuItem("Tools/AI Orchestrator")]
        public static void Open()
        {
            GetWindow<OrchestratorWindow>("AI Orchestrator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Unity Orchestrator Server", EditorStyles.boldLabel);
            _port = EditorGUILayout.IntField("Port", _port);

            GUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !OrchestratorServer.IsRunning;
                if (GUILayout.Button("Start Server"))
                    OrchestratorServer.Start(_port);

                GUI.enabled = OrchestratorServer.IsRunning;
                if (GUILayout.Button("Stop Server"))
                    OrchestratorServer.Stop();

                GUI.enabled = true;
            }

            GUILayout.Space(8);
            EditorGUILayout.HelpBox(
                $"Status: {(OrchestratorServer.IsRunning ? "RUNNING" : "STOPPED")}\n" +
                $"Endpoint: http://127.0.0.1:{_port}/command",
                MessageType.Info);
        }
    }
}