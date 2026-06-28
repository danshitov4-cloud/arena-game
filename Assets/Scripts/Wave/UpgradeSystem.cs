using UnityEngine;
using UnityEngine.UI;

namespace ArenaGame
{
    public enum UpgradeType
    {
        FireRate,
        Damage,
        HP
    }

    public class UpgradeSystem : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private GameObject upgradePanel;
        [SerializeField] private Button[] upgradeButtons;

        private UpgradeType[] _currentChoices = new UpgradeType[3];
        private PlayerController _player;
        private int _bulletDamageLevel = 1;

        private void Awake()
        {
            if (upgradePanel != null) upgradePanel.SetActive(false);
        }

        public void ShowUpgradeChoice()
        {
            ResolvePlayer();
            _currentChoices = PickRandomChoices();

            if (upgradePanel != null) upgradePanel.SetActive(true);
            Time.timeScale = 0f;

            for (int i = 0; i < upgradeButtons.Length && i < _currentChoices.Length; i++)
            {
                int captured = i;
                Button btn = upgradeButtons[i];
                if (btn == null) continue;

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => ApplyUpgrade(_currentChoices[captured]));

                Text label = btn.GetComponentInChildren<Text>();
                if (label != null) label.text = DescribeUpgrade(_currentChoices[i]);
            }
        }

        public void ApplyUpgrade(UpgradeType type)
        {
            ResolvePlayer();

            if (_player != null)
            {
                switch (type)
                {
                    case UpgradeType.FireRate:
                        _player.UpgradeFireRate(0.75f);
                        break;
                    case UpgradeType.Damage:
                        _bulletDamageLevel++;
                        _player.SetBulletDamage(_bulletDamageLevel);
                        break;
                    case UpgradeType.HP:
                        _player.Heal(1);
                        break;
                }
            }

            HidePanel();
            GameManager.Instance?.WaveManager?.StartWave();
        }

        private void HidePanel()
        {
            if (upgradePanel != null) upgradePanel.SetActive(false);
            Time.timeScale = 1f;
        }

        private UpgradeType[] PickRandomChoices()
        {
            UpgradeType[] pool = { UpgradeType.FireRate, UpgradeType.Damage, UpgradeType.HP };
            for (int i = pool.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            return pool;
        }

        private static string DescribeUpgrade(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.FireRate: return "Fire Rate +25%";
                case UpgradeType.Damage:   return "Bullet Damage +1";
                case UpgradeType.HP:       return "Restore 1 HP";
                default:                   return type.ToString();
            }
        }

        private void ResolvePlayer()
        {
            if (_player == null) _player = FindFirstObjectByType<PlayerController>();
        }
    }
}
