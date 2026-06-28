using System.Collections.Generic;
using UnityEngine;

namespace PacmanGame
{
    public class MazeGenerator : MonoBehaviour
    {
        private static readonly int[,] Layout = new int[21, 21]
        {
            { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 },
            { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 },
            { 1, 0, 1, 1, 1, 0, 1, 1, 1, 0, 1, 0, 1, 1, 1, 0, 1, 1, 1, 0, 1 },
            { 1, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 1 },
            { 1, 0, 1, 0, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 0, 1, 0, 1 },
            { 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1 },
            { 1, 1, 1, 0, 1, 0, 1, 1, 1, 0, 1, 0, 1, 1, 1, 0, 1, 0, 1, 1, 1 },
            { 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1 },
            { 1, 0, 1, 1, 1, 0, 1, 0, 1, 1, 1, 1, 1, 0, 1, 0, 1, 1, 1, 0, 1 },
            { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1 },
            { 1, 1, 1, 0, 1, 0, 1, 1, 1, 0, 0, 0, 1, 1, 1, 0, 1, 0, 1, 1, 1 },
            { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1 },
            { 1, 0, 1, 1, 1, 0, 1, 0, 1, 1, 1, 1, 1, 0, 1, 0, 1, 1, 1, 0, 1 },
            { 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1 },
            { 1, 1, 1, 0, 1, 0, 1, 1, 1, 0, 1, 0, 1, 1, 1, 0, 1, 0, 1, 1, 1 },
            { 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1 },
            { 1, 0, 1, 0, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 0, 1, 0, 1 },
            { 1, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 1 },
            { 1, 0, 1, 1, 1, 0, 1, 1, 1, 0, 1, 0, 1, 1, 1, 0, 1, 1, 1, 0, 1 },
            { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 },
            { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }
        };

        private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
        private Transform mazeRoot;
        private Transform playerRoot;
        private Transform ghostsRoot;

        private void Start()
        {
            GenerateMaze();
        }

        [ContextMenu("Generate Maze")]
        public void GenerateMaze()
        {
            mazeRoot = GameObject.Find("Maze")?.transform ?? transform;
            playerRoot = GameObject.Find("Player")?.transform;
            ghostsRoot = GameObject.Find("Ghosts")?.transform;
            if (playerRoot == null || ghostsRoot == null)
            {
                Debug.LogError("Pacman scene roots Player or Ghosts are missing.");
                return;
            }

            ClearGeneratedChildren();
            Sprite wallSprite = CreateSprite(Color.blue);
            Sprite dotSprite = CreateSprite(Color.white);
            Sprite playerSprite = CreateSprite(Color.yellow);
            Sprite ghostSprite = CreateSprite(Color.red);

            int dotCount = 0;
            var reserved = new HashSet<Vector2Int>
            {
                new Vector2Int(1, 1), new Vector2Int(19, 19), new Vector2Int(11, 19),
                new Vector2Int(19, 1), new Vector2Int(1, 19)
            };

            for (int y = 0; y < 21; y++)
            {
                for (int x = 0; x < 21; x++)
                {
                    if (Layout[y, x] == 1) CreateWall(x, y, wallSprite);
                    else if (!reserved.Contains(new Vector2Int(x, y)))
                    {
                        CreateDot(x, y, dotSprite);
                        dotCount++;
                    }
                }
            }

            ConfigurePlayer(playerSprite, new Vector2(1, 1));
            ConfigureGhosts(ghostSprite);
            FitCameraToMaze();
            GameManager.Instance?.RegisterDots(dotCount);
            Debug.Log($"Pacman maze generated: 224 walls, {dotCount} dots, 4 ghosts.");
        }

        private void ClearGeneratedChildren()
        {
            for (int i = mazeRoot.childCount - 1; i >= 0; i--) SafeDestroy(mazeRoot.GetChild(i).gameObject);
            for (int i = ghostsRoot.childCount - 1; i >= 0; i--) SafeDestroy(ghostsRoot.GetChild(i).gameObject);
        }

        private void CreateWall(int x, int y, Sprite sprite)
        {
            var wall = new GameObject($"Wall_{x}_{y}");
            wall.transform.SetParent(mazeRoot, false);
            wall.transform.position = new Vector3(x, y, 0f);
            var renderer = wall.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.blue;
            wall.AddComponent<BoxCollider2D>();
            wall.AddComponent<MazeCell>().Configure(MazeCellType.Wall);
        }

        private void CreateDot(int x, int y, Sprite sprite)
        {
            var dot = new GameObject($"Dot_{x}_{y}");
            dot.transform.SetParent(mazeRoot, false);
            dot.transform.position = new Vector3(x, y, -0.1f);
            dot.transform.localScale = Vector3.one * 0.18f;
            var renderer = dot.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            var collider = dot.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            dot.AddComponent<Dot>();
        }

        private void ConfigurePlayer(Sprite sprite, Vector2 position)
        {
            playerRoot.position = position;
            playerRoot.localScale = Vector3.one * 0.8f;

            var renderer = GetOrAdd<SpriteRenderer>(playerRoot.gameObject);
            renderer.sprite = sprite;
            renderer.color = Color.white;

            var unityAnimator = playerRoot.GetComponent<Animator>();
            if (unityAnimator != null) unityAnimator.enabled = false;

            GetOrAdd<PacmanMouthAnimator>(playerRoot.gameObject);

            var collider = GetOrAdd<CircleCollider2D>(playerRoot.gameObject);
            collider.isTrigger = true;
            collider.radius = 0.48f;
            var body = GetOrAdd<Rigidbody2D>(playerRoot.gameObject);
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            GetOrAdd<PacmanController>(playerRoot.gameObject);
        }

        private void ConfigureGhosts(Sprite sprite)
        {
            Vector2[] positions = { new Vector2(19, 19), new Vector2(11, 19), new Vector2(19, 1), new Vector2(1, 19) };
            Color[] ghostColors =
            {
                Color.red,
                new Color(1f, 0.72f, 0.8f),
                Color.cyan,
                new Color(1f, 0.6f, 0f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var ghost = new GameObject($"Ghost_{i + 1}");
                ghost.transform.SetParent(ghostsRoot, false);
                ghost.transform.position = positions[i];
                ghost.transform.localScale = Vector3.one * 0.8f;
                var renderer = ghost.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = ghostColors[i];
                var collider = ghost.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.radius = 0.48f;
                var body = ghost.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                ghost.AddComponent<GhostAI>();
                var anim = ghost.AddComponent<GhostAnimator>();
                if (anim != null) anim.ghostColor = ghostColors[i];
            }
        }

        private static void FitCameraToMaze()
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return;

            // Maze is 21×21 cells (0..20). Add 1 unit total padding → half-extent = 11.
            const float halfExtent = 11f;
            float aspect = (float)Screen.width / Screen.height;

            // Fit whichever dimension is tighter so the full maze always stays visible.
            cam.orthographicSize = Mathf.Max(halfExtent, halfExtent / aspect);
        }

        private Sprite CreateSprite(Color color)
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color[16 * 16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels(pixels);
            texture.Apply();
            generatedTextures.Add(texture);
            return Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private void OnDestroy()
        {
            foreach (var texture in generatedTextures) if (texture != null) SafeDestroy(texture);
        }

        private static void SafeDestroy(Object obj)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(obj);
            else
#endif
                Destroy(obj);
        }
    }
}