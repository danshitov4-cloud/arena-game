using UnityEngine;
using UnityEngine.InputSystem;

namespace PacmanGame
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public int Score { get; private set; }
        public int RemainingDots { get; private set; }
        public bool GameEnded { get; private set; }
        private string endMessage = "";

        private void Awake()
        {
            Instance = this;
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void RegisterDots(int count)
        {
            RemainingDots = count;
            Score = 0;
            GameEnded = false;
            endMessage = "";
            Time.timeScale = 1f;
        }

        public void CollectDot(int points)
        {
            if (GameEnded) return;
            Score += points;
            RemainingDots = Mathf.Max(0, RemainingDots - 1);
            if (RemainingDots == 0) Win();
        }

        public void Win()
        {
            if (GameEnded) return;
            GameEnded = true;
            endMessage = "YOU WIN!";
            Time.timeScale = 0f;
        }

        public void Lose()
        {
            if (GameEnded) return;
            GameEnded = true;
            endMessage = "GAME OVER";
            Time.timeScale = 0f;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            bool restart = keyboard != null
                ? keyboard.rKey.wasPressedThisFrame
                : Input.GetKeyDown(KeyCode.R);

            if (restart) RestartCurrentMaze();
        }

        public void RestartCurrentMaze()
        {
            Time.timeScale = 1f;
            GameEnded = false;
            endMessage = "";
            Score = 0;

            var generator = FindFirstObjectByType<MazeGenerator>();
            if (generator != null)
            {
                generator.GenerateMaze();
                return;
            }

            Debug.LogWarning("Pacman restart requested, but MazeGenerator was not found in the scene.");
        }

        private void OnGUI()
        {
            var label = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            label.normal.textColor = Color.white;
            GUI.Label(new Rect(20, 15, 500, 40), $"SCORE: {Score}    DOTS: {RemainingDots}", label);
            var center = new GUIStyle(label) { fontSize = 44, alignment = TextAnchor.MiddleCenter };
            center.normal.textColor = endMessage == "YOU WIN!" ? Color.yellow : Color.red;
            GUI.Label(new Rect(Screen.width / 2f - 250, Screen.height / 2f - 60, 500, 70), endMessage, center);
            var hint = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            GUI.Label(new Rect(Screen.width / 2f - 250, Screen.height / 2f + 10, 500, 40), "Press R to restart", hint);
        }
    }
}