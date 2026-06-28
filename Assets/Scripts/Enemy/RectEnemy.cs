using UnityEngine;

namespace ArenaGame
{
    public class RectEnemy : EnemyBase
    {
        protected override void Awake()
        {
            hp = 6;
            speed = 2f;
            base.Awake();
            if (TryGetComponent<Renderer>(out var r))
                r.material.color = new Color(0.9f, 0.1f, 0.1f);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.TryGetComponent<PlayerController>(out var player))
                player.TakeDamage(contactDamage);
        }
    }
}
