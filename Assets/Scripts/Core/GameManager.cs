using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ArenaGame
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Systems")]
        [SerializeField] private WaveManager waveManager;
        [SerializeField] private UpgradeSystem upgradeSystem;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private PlayerController player;

        [Header("State")]
        public GameState currentState = GameState.Menu;
        public int score;
        public int wave;

        [Header("Settings")]
        [SerializeField] private int startingLives = 3;

        private int _lives;

        public WaveManager WaveManager => waveManager;
        public UpgradeSystem UpgradeSystem => upgradeSystem;
        public UIManager UIManager => uiManager;
        public PlayerController Player => player;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Time.timeScale = 1f;
            ResolveReferences();
        }

        private void Start()
        {
            StartGame();
        }

        private void Update()
        {
            if (currentState != GameState.GameOver && currentState != GameState.Victory)
                return;

            bool restartPressed = Keyboard.current != null
                ? Keyboard.current.rKey.wasPressedThisFrame
                : Input.GetKeyDown(KeyCode.R);

            if (restartPressed)
                RestartGame();
        }

        public void StartGame()
        {
            ResolveReferences();
            Time.timeScale = 1f;
            currentState = GameState.Playing;
            score = 0;
            wave = 1;
            _lives = startingLives;
            uiManager?.ShowUpgradePanel(false);
            waveManager?.StartWave(1);
        }

        public void GameOver()
        {
            if (currentState == GameState.GameOver || currentState == GameState.Victory)
                return;

            currentState = GameState.GameOver;
            Time.timeScale = 0f;
            uiManager?.ShowGameOver();
        }

        public void Victory()
        {
            if (currentState == GameState.Victory)
                return;

            currentState = GameState.Victory;
            Time.timeScale = 0f;
            uiManager?.ShowVictory();
        }

        public void RestartGame()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void AddScore(int points)
        {
            score += Mathf.Max(0, points);
        }

        public void LoseLife()
        {
            _lives = Mathf.Max(0, _lives - 1);
            if (_lives == 0)
                GameOver();
        }

        private void ResolveReferences()
        {
            if (waveManager == null) waveManager = FindFirstObjectByType<WaveManager>();
            if (upgradeSystem == null) upgradeSystem = FindFirstObjectByType<UpgradeSystem>();
            if (uiManager == null) uiManager = FindFirstObjectByType<UIManager>();
            if (player == null) player = FindFirstObjectByType<PlayerController>();
        }
    }

    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        GameOver,
        Victory
    }
}