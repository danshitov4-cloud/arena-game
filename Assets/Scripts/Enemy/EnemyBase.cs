using UnityEngine;

namespace ArenaGame
{
    public abstract class EnemyBase : MonoBehaviour
    {
        [Header("Stats")]
        public int hp = 1;
        public float speed = 3f;

        [Header("Combat")]
        public int contactDamage = 1;

        [Header("Spawn")]
        public float floorOffset = 0.5f;

        protected bool _isDead;
        protected Transform _playerTransform;
        private Rigidbody _rb;

        protected virtual void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null) _playerTransform = playerGO.transform;
        }

        protected virtual void Update() { }

        protected virtual void FixedUpdate() => MoveTowardsPlayer();

        public virtual void TakeDamage(int damage)
        {
            if (_isDead) return;
            hp -= damage;
            if (hp <= 0) Die();
        }

        protected virtual void Die()
        {
            _isDead = true;
            Destroy(gameObject);
        }

        protected void MoveTowardsPlayer()
        {
            if (_playerTransform == null || _isDead) return;
            Vector3 target = new Vector3(_playerTransform.position.x, transform.position.y, _playerTransform.position.z);
            Vector3 dir = target - transform.position;
            if (dir.sqrMagnitude < 0.001f) return;

            _rb.linearVelocity = dir.normalized * speed;
            _rb.MoveRotation(Quaternion.LookRotation(dir.normalized));
        }
    }
}
