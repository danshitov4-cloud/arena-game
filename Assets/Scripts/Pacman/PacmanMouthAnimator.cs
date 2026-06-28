using System;
using System.Linq;
using UnityEngine;

namespace PacmanGame
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class PacmanMouthAnimator : MonoBehaviour
    {
        [SerializeField] private string resourcePath = "Pacman/pacman_sheet";
        [SerializeField] private float framesPerSecond = 12f;
        [SerializeField] private bool animateWhenIdle = true;

        private static readonly int[] Sequence = { 0, 1, 2, 3, 2, 1 };

        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private int sequenceIndex;
        private float timer;
        private Vector3 lastPosition;
        private Vector2 facing = Vector2.right;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.color = Color.white;
            LoadFrames();
            ApplyFrame(0);
            lastPosition = transform.position;
        }

        private void OnEnable()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (frames == null || frames.Length == 0) LoadFrames();
            ApplyFrame(0);
            lastPosition = transform.position;
        }

        private void LateUpdate()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (frames == null || frames.Length == 0) LoadFrames();
            if (frames == null || frames.Length == 0) return;

            spriteRenderer.color = Color.white;

            Vector3 delta = transform.position - lastPosition;
            bool moved = delta.sqrMagnitude > 0.00001f;
            if (moved)
            {
                if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)) facing = delta.x >= 0f ? Vector2.right : Vector2.left;
                else facing = delta.y >= 0f ? Vector2.up : Vector2.down;
                RotateToFacing();
            }

            if (animateWhenIdle || moved)
            {
                timer += Time.deltaTime * framesPerSecond;
                while (timer >= 1f)
                {
                    timer -= 1f;
                    sequenceIndex = (sequenceIndex + 1) % Sequence.Length;
                }
                ApplyFrame(Sequence[sequenceIndex]);
            }
            else
            {
                sequenceIndex = 0;
                timer = 0f;
                ApplyFrame(0);
            }

            lastPosition = transform.position;
        }

        private void LoadFrames()
        {
            Sprite[] loadedSprites = Resources.LoadAll<Sprite>(resourcePath)
                .Where(s => s != null)
                .OrderBy(s => s.name, StringComparer.Ordinal)
                .ToArray();

            if (loadedSprites.Length >= 4)
            {
                frames = loadedSprites.Take(4).ToArray();
                return;
            }

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                Debug.LogWarning($"PacmanMouthAnimator could not load Resources/{resourcePath}.");
                frames = Array.Empty<Sprite>();
                return;
            }

            int frameWidth = texture.width / 4;
            int frameHeight = texture.height;
            frames = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                frames[i] = Sprite.Create(
                    texture,
                    new Rect(i * frameWidth, 0, frameWidth, frameHeight),
                    new Vector2(0.5f, 0.5f),
                    frameHeight);
                frames[i].name = $"pacman_runtime_{i}";
            }
        }

        private void ApplyFrame(int index)
        {
            if (spriteRenderer == null || frames == null || frames.Length == 0) return;
            index = Mathf.Clamp(index, 0, frames.Length - 1);
            if (frames[index] != null) spriteRenderer.sprite = frames[index];
        }

        private void RotateToFacing()
        {
            float z = 0f;
            if (facing == Vector2.up) z = 90f;
            else if (facing == Vector2.left) z = 180f;
            else if (facing == Vector2.down) z = -90f;
            transform.rotation = Quaternion.Euler(0f, 0f, z);
        }
    }
}