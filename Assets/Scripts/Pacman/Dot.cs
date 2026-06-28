using UnityEngine;

namespace PacmanGame
{
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D))]
    public class Dot : MonoBehaviour
    {
        [SerializeField] private int points = 10;
        private bool collected;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (collected || other.GetComponent<PacmanController>() == null) return;
            collected = true;
            GameManager.Instance?.CollectDot(points);
            Destroy(gameObject);
        }
    }
}