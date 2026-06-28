using UnityEngine;
using UnityEngine.InputSystem;

namespace PacmanGame
{
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D))]
    public class PacmanController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 7f;
        private Vector2 desiredDirection = Vector2.zero;
        private Vector2 currentDirection = Vector2.zero;
        private Vector3 targetPosition;

        private void Start()
        {
            transform.position = Snap(transform.position);
            targetPosition = transform.position;
        }

        private void Update()
        {
            ReadInput();
            if (GameManager.Instance != null && GameManager.Instance.GameEnded) return;

            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            if ((transform.position - targetPosition).sqrMagnitude > 0.0001f) return;

            transform.position = targetPosition;
            if (CanMove(desiredDirection)) currentDirection = desiredDirection;
            if (CanMove(currentDirection)) targetPosition += (Vector3)currentDirection;
        }

        private void ReadInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)         desiredDirection = Vector2.up;
                else if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)  desiredDirection = Vector2.down;
                else if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)  desiredDirection = Vector2.left;
                else if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) desiredDirection = Vector2.right;
            }
            else
            {
                if      (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    desiredDirection = Vector2.up;
                else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  desiredDirection = Vector2.down;
                else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  desiredDirection = Vector2.left;
                else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) desiredDirection = Vector2.right;
            }
        }

        private bool CanMove(Vector2 direction)
        {
            if (direction == Vector2.zero) return false;
            Vector2 checkPoint = (Vector2)targetPosition + direction;
            var hits = Physics2D.OverlapCircleAll(checkPoint, 0.35f);
            foreach (var hit in hits)
            {
                var cell = hit.GetComponent<MazeCell>();
                if (cell != null && cell.IsWall) return false;
            }
            return true;
        }

        private static Vector3 Snap(Vector3 value) => new Vector3(Mathf.Round(value.x), Mathf.Round(value.y), 0f);

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<GhostAI>() != null) GameManager.Instance?.Lose();
        }
    }
}