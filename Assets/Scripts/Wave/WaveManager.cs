using System.Collections.Generic;
using UnityEngine;

namespace ArenaGame
{
    public class WaveManager : MonoBehaviour
    {
        private const float WaveDuration = 180f;

        [Header("Wave State")]
        public int currentWave;
        public float waveTimer = WaveDuration;

        [Header("Spawn Settings")]
        [SerializeField] private float spawnInterval = 5f;
        [SerializeField] private float spawnRadius = 15f;
        [SerializeField] private GameObject triangleEnemyPrefab;
        [SerializeField] private GameObject rectEnemyPrefab;
        [SerializeField] private Transform player;
        [SerializeField] private UpgradeSystem upgradeSystem;

        private static readonly int[] EnemyCountPerWave = { 0, 5, 10, 20 };

        private readonly List<GameObject> _spawnedEnemies = new List<GameObject>();
        private int _targetEnemyCount;
        private int _spawnedEnemyCount;
        private float _spawnCountdown;
        private bool _waveActive;

        public int EnemiesRemaining => _spawnedEnemies.Count;
        public bool IsWaveActive => _waveActive;

        private void Update()
        {
            if (!_waveActive)
                return;

            waveTimer = Mathf.Max(0f, waveTimer - Time.deltaTime);
            _spawnCountdown -= Time.deltaTime;

            if (_spawnedEnemyCount < _targetEnemyCount && _spawnCountdown <= 0f)
            {
                SpawnEnemy();
                _spawnCountdown = spawnInterval;
            }

            _spawnedEnemies.RemoveAll(enemy => enemy == null);

            bool allSpawnedEnemiesDefeated =
                _spawnedEnemyCount >= _targetEnemyCount && _spawnedEnemies.Count == 0;

            if (waveTimer <= 0f || allSpawnedEnemiesDefeated)
                EndWave();
        }

        public void StartWave()
        {
            StartWave(currentWave + 1);
        }

        public void StartWave(int wave)
        {
            if (wave > 3)
            {
                GameManager.Instance?.Victory();
                return;
            }

            if (player == null)
            {
                PlayerController playerController = FindFirstObjectByType<PlayerController>();
                if (playerController != null) player = playerController.transform;
            }

            if (upgradeSystem == null)
                upgradeSystem = FindFirstObjectByType<UpgradeSystem>();

            currentWave = Mathf.Clamp(wave, 1, 3);
            waveTimer = WaveDuration;
            _targetEnemyCount = GetEnemyCount(currentWave);
            _spawnedEnemyCount = 0;
            _spawnCountdown = 0f;
            _spawnedEnemies.Clear();
            _waveActive = true;

            if (GameManager.Instance != null)
                GameManager.Instance.wave = currentWave;

            UIManager.Instance?.ShowUpgradePanel(false);
        }

        public void EndWave()
        {
            if (!_waveActive)
                return;

            _waveActive = false;

            foreach (GameObject enemy in _spawnedEnemies)
            {
                if (enemy != null)
                    Destroy(enemy);
            }
            _spawnedEnemies.Clear();

            if (currentWave >= 3)
            {
                GameManager.Instance?.Victory();
                return;
            }

            if (upgradeSystem == null)
                upgradeSystem = FindFirstObjectByType<UpgradeSystem>();

            upgradeSystem?.ShowUpgradeChoice();
        }

        private void SpawnEnemy()
        {
            GameObject prefab = ChooseEnemyPrefab();
            if (prefab == null || player == null)
                return;

            float spawnY = prefab.GetComponent<EnemyBase>()?.floorOffset ?? 0.5f;
            Vector3 pos = RandomSpawnPosition();
            pos.y = spawnY;

            GameObject enemy = Instantiate(prefab, pos, Quaternion.identity);
            _spawnedEnemies.Add(enemy);
            _spawnedEnemyCount++;
        }

        private GameObject ChooseEnemyPrefab()
        {
            if (currentWave == 1)
                return triangleEnemyPrefab;

            float triangleChance = currentWave == 2 ? 0.7f : 0.5f;
            GameObject chosen = Random.value < triangleChance
                ? triangleEnemyPrefab
                : rectEnemyPrefab;

            if (chosen == null)
                chosen = triangleEnemyPrefab != null ? triangleEnemyPrefab : rectEnemyPrefab;

            return chosen;
        }

        private int GetEnemyCount(int wave)
        {
            int index = Mathf.Clamp(wave, 1, EnemyCountPerWave.Length - 1);
            return EnemyCountPerWave[index];
        }

        private Vector3 RandomSpawnPosition()
        {
            const float arenaHalf = 13.5f;
            Vector2 direction = Random.insideUnitCircle.normalized;
            float distance = Random.Range(spawnRadius * 0.6f, spawnRadius);
            Vector3 pos = player.position + new Vector3(direction.x, 0f, direction.y) * distance;
            pos.x = Mathf.Clamp(pos.x, -arenaHalf, arenaHalf);
            pos.z = Mathf.Clamp(pos.z, -arenaHalf, arenaHalf);
            return pos;
        }
    }
}