using System;
using System.Linq;
using UnityEngine;

namespace PacmanGame
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class GhostAnimator : MonoBehaviour
    {
        [SerializeField] private string resourcePath = "Pacman/ghost_sheet";
        [SerializeField] private float framesPerSecond = 8f;
        public Color ghostColor = Color.white;

        private static readonly int[] Sequence = { 0, 1, 2, 3, 2, 1 };

        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private int sequenceIndex;
        private float timer;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            LoadFrames();
            ApplyFrame(0);
        }

        private void OnEnable()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (frames == null || frames.Length == 0) LoadFrames();
            ApplyFrame(0);
        }

        private void LateUpdate()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (frames == null || frames.Length == 0) LoadFrames();
            if (frames == null || frames.Length == 0) return;

            spriteRenderer.color = ghostColor;

            timer += Time.deltaTime * framesPerSecond;
            while (timer >= 1f)
            {
                timer -= 1f;
                sequenceIndex = (sequenceIndex + 1) % Sequence.Length;
            }
            ApplyFrame(Sequence[sequenceIndex]);
        }

        private void LoadFrames()
        {
            Sprite[] loaded = Resources.LoadAll<Sprite>(resourcePath)
                .Where(s => s != null)
                .OrderBy(s => s.name, StringComparer.Ordinal)
                .ToArray();

            if (loaded.Length >= 2)
            {
                frames = loaded;
                return;
            }

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                Debug.LogWarning($"GhostAnimator: не удалось загрузить Resources/{resourcePath}");
                frames = Array.Empty<Sprite>();
                return;
            }

            const int frameCount = 4;
            int frameWidth = texture.width / frameCount;
            int frameHeight = texture.height;
            frames = new Sprite[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                frames[i] = Sprite.Create(
                    texture,
                    new Rect(i * frameWidth, 0, frameWidth, frameHeight),
                    new Vector2(0.5f, 0.5f),
                    frameHeight);
                frames[i].name = $"ghost_runtime_{i}";
            }
        }

        private void ApplyFrame(int index)
        {
            if (spriteRenderer == null || frames == null || frames.Length == 0) return;
            index = Mathf.Clamp(index, 0, frames.Length - 1);
            if (frames[index] != null) spriteRenderer.sprite = frames[index];
        }
    }
}
