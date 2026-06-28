using System.Collections.Generic;
using UnityEngine;

namespace PacmanGame
{
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D))]
    public class GhostAI : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4.5f;
        private Vector2 direction = Vector2.left;
        private Vector3 targetPosition;
        private static readonly Vector2[] Directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };

        private void Start()
        {
            transform.position = new Vector3(Mathf.Round(transform.position.x), Mathf.Round(transform.position.y), 0f);
            targetPosition = transform.position;
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.GameEnded) return;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            if ((transform.position - targetPosition).sqrMagnitude > 0.0001f) return;

            transform.position = targetPosition;
            direction = ChooseDirection();
            targetPosition += (Vector3)direction;
        }

        private Vector2 ChooseDirection()
        {
            var available = new List<Vector2>();
            foreach (var candidate in Directions)
            {
                if (CanMove(candidate)) available.Add(candidate);
            }

            if (available.Count > 1) available.Remove(-direction);
            if (available.Count == 0) return -direction;
            return available[Random.Range(0, available.Count)];
        }

        private bool CanMove(Vector2 candidate)
        {
            Vector2 checkPoint = (Vector2)targetPosition + candidate;
            foreach (var hit in Physics2D.OverlapCircleAll(checkPoint, 0.35f))
            {
                var cell = hit.GetComponent<MazeCell>();
                if (cell != null && cell.IsWall) return false;
            }
            return true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<PacmanController>() != null) GameManager.Instance?.Lose();
        }
    }
}