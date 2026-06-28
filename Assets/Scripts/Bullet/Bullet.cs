using UnityEngine;

namespace ArenaGame
{
    [RequireComponent(typeof(Rigidbody))]
    public class Bullet : MonoBehaviour
    {
        public int damage = 1;
        public float speed = 10f;

        [SerializeField] private float lifetime = 3f;

        private Rigidbody _rb;
        private Vector3 _direction;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;

            var mr = GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
                if (sh != null)
                {
                    var mat = new Material(sh);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.yellow);
                    else if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.yellow);
                    mr.material = mat;
                }
            }
        }

        private void Start()
        {
            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null)
            {
                var bulletCol = GetComponent<Collider>();
                foreach (var col in playerGO.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(bulletCol, col);
            }

            Vector3 dir = _direction.sqrMagnitude > 0.001f ? _direction : transform.forward;
            _rb.linearVelocity = dir * speed;
            Invoke(nameof(Expire), lifetime);
        }

        public void Initialize(Vector3 direction, int newDamage)
        {
            damage = newDamage;
            direction.y = 0f;
            _direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.TryGetComponent<EnemyBase>(out var enemy))
                enemy.TakeDamage(damage);
            Expire();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<EnemyBase>(out var enemy))
            {
                enemy.TakeDamage(damage);
                Expire();
            }
        }

        private void Expire()
        {
            CancelInvoke(nameof(Expire));
            Destroy(gameObject);
        }
    }
}
