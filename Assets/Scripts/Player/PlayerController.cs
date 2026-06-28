using UnityEngine;
using UnityEngine.InputSystem;

namespace ArenaGame
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Parts")]
        [SerializeField] private Transform lowerBody;
        [SerializeField] private Transform upperBody;
        [SerializeField] private Transform firePoint;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float rotationSpeed = 540f;

        [Header("Combat")]
        [SerializeField] private int maxHealth = 3;
        [SerializeField] private int maxAmmo = 60;
        [SerializeField] private float fireDelay = 0.25f;
        [SerializeField] private int bulletDamage = 1;
        [SerializeField] private GameObject bulletPrefab;

        private CharacterController _controller;
        private Vector3 _velocity;
        private int _currentHealth;
        private int _currentAmmo;
        private float _nextFireTime;
        private Camera _camera;

        public int CurrentHealth => _currentHealth;
        public int CurrentAmmo => _currentAmmo;
        public int MaxAmmo => maxAmmo;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _currentHealth = maxHealth;
            _currentAmmo = maxAmmo;
            _camera = Camera.main;
        }

        private void Start()
        {
            UIManager.Instance?.UpdateHP(_currentHealth);
            UIManager.Instance?.UpdateAmmo(_currentAmmo, maxAmmo);
        }

        private void Update()
        {
            if (_camera == null) _camera = Camera.main;
            HandleGravity();
            Move();
            Aim();
        }

        private void Move()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            float h = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float v = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            Vector3 dir = new Vector3(h, 0f, v).normalized;

            if (dir.sqrMagnitude > 0.001f && lowerBody != null)
            {
                lowerBody.rotation = Quaternion.RotateTowards(
                    lowerBody.rotation,
                    Quaternion.LookRotation(dir),
                    rotationSpeed * Time.deltaTime);
            }

            _controller.Move(dir * moveSpeed * Time.deltaTime);
        }

        private void Aim()
        {
            if (_camera == null || upperBody == null) return;
            var mouse = Mouse.current;
            if (mouse == null) return;

            Ray ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
            Plane floor = new Plane(Vector3.up, Vector3.zero);
            if (!floor.Raycast(ray, out float dist)) return;

            Vector3 aimDir = ray.GetPoint(dist) - transform.position;
            aimDir.y = 0f;
            if (aimDir.sqrMagnitude < 0.001f) return;

            upperBody.rotation = Quaternion.RotateTowards(
                upperBody.rotation,
                Quaternion.LookRotation(aimDir.normalized),
                rotationSpeed * Time.deltaTime);
        }

        private void HandleGravity()
        {
            if (_controller.isGrounded && _velocity.y < 0f)
                _velocity.y = -2f;

            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private void Shoot(Vector2 screenPos)
        {
            if (_currentAmmo <= 0 || bulletPrefab == null || _camera == null) return;

            Ray ray = _camera.ScreenPointToRay(screenPos);
            Plane floor = new Plane(Vector3.up, Vector3.zero);
            if (!floor.Raycast(ray, out float dist)) return;

            Vector3 direction = ray.GetPoint(dist) - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return;
            direction.Normalize();

            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + direction * 0.6f;
            GameObject bulletObject = Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(direction));
            bulletObject.GetComponent<Bullet>()?.Initialize(direction, bulletDamage);

            _currentAmmo--;
            _nextFireTime = Time.time + fireDelay;
            UIManager.Instance?.UpdateAmmo(_currentAmmo, maxAmmo);
        }

        public void UpgradeFireRate(float multiplier)
        {
            fireDelay = Mathf.Max(0.05f, fireDelay * multiplier);
        }

        public void SetBulletDamage(int damage)
        {
            bulletDamage = Mathf.Max(1, damage);
        }

        public void Heal(int amount)
        {
            _currentHealth = Mathf.Min(maxHealth, _currentHealth + Mathf.Max(0, amount));
            UIManager.Instance?.UpdateHP(_currentHealth);
        }

        public void TakeDamage(int amount)
        {
            _currentHealth = Mathf.Max(0, _currentHealth - Mathf.Max(0, amount));
            UIManager.Instance?.UpdateHP(_currentHealth);
            if (_currentHealth <= 0) Die();
        }

        public void OnMove(InputValue value) { }
        public void OnJump(InputValue value) { }

        public void OnAttack(InputValue value)
        {
            if (!value.isPressed || Time.time < _nextFireTime) return;
            var mouse = Mouse.current;
            if (mouse == null) return;
            Shoot(mouse.position.ReadValue());
        }

        public void Die()
        {
            GameManager.Instance?.GameOver();
        }
    }
}
