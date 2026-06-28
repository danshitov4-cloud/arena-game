using UnityEngine;
using UnityEngine.UI;

namespace ArenaGame
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Sources")]
        [SerializeField] private PlayerController player;
        [SerializeField] private WaveManager waveManager;

        [Header("HUD")]
        [SerializeField] private Text ammoText;
        [SerializeField] private Text hpText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text waveText;

        [Header("Panels")]
        [SerializeField] private GameObject upgradePanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject victoryPanel;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ShowUpgradePanel(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (victoryPanel != null) victoryPanel.SetActive(false);
        }

        private void Update()
        {
            if (player == null)
                player = FindFirstObjectByType<PlayerController>();
            if (waveManager == null)
                waveManager = FindFirstObjectByType<WaveManager>();

            if (player != null)
            {
                UpdateAmmo(player.CurrentAmmo, player.MaxAmmo);
                UpdateHP(player.CurrentHealth);
            }

            if (waveManager != null)
            {
                UpdateTimer(waveManager.waveTimer);
                UpdateWave(waveManager.currentWave);
            }
        }

        public void UpdateAmmo(int current, int max)
        {
            if (ammoText != null)
                ammoText.text = $"Ammo: {current}/{max}";
        }

        public void UpdateHP(int current)
        {
            if (hpText != null)
                hpText.text = $"HP: {current}";
        }

        public void UpdateTimer(float seconds)
        {
            if (timerText == null) return;

            int safeSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
            int minutes = safeSeconds / 60;
            int remainder = safeSeconds % 60;
            timerText.text = $"Time: {minutes:00}:{remainder:00}";
        }

        public void UpdateWave(int wave)
        {
            if (waveText != null)
                waveText.text = $"Wave: {wave}/3";
        }

        public void ShowUpgradePanel(bool show)
        {
            if (upgradePanel != null)
                upgradePanel.SetActive(show);
        }

        public void ShowGameOver()
        {
            if (gameOverPanel != null)
                gameOverPanel.SetActive(true);
        }

        public void ShowVictory()
        {
            if (victoryPanel != null)
                victoryPanel.SetActive(true);
        }
    }
}