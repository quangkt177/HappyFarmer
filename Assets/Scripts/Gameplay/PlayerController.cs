using System.Collections.Generic;
using UnityEngine;

namespace HappyFarmer.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 4f;

        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer;
        private Vector2 movement;
        
        // Directions: 0: Up, 1: Right, 2: Down, 3: Left
        private int currentDirection = 2; // Default facing down
        private float animTimer;
        private int animFrame;

        // Sprites cache: [clipType, direction, frame]
        private Sprite[][] idleClips;
        private Sprite[][] runClips;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            LoadBunnySprites();
        }

        private void LoadBunnySprites()
        {
            // Rows in sprite sheet: 0 = Up, 1 = Right, 2 = Left, 3 = Down
            // We want directions: 0 = Up, 1 = Right, 2 = Down, 3 = Left
            int[] sheetRows = { 0, 1, 3, 2 };

            var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
            var spriteDict = new Dictionary<string, Sprite>();
            foreach (var s in allSprites)
            {
                if (!spriteDict.ContainsKey(s.name))
                    spriteDict[s.name] = s;
            }

            idleClips = new Sprite[4][];
            runClips = new Sprite[4][];

            for (int dir = 0; dir < 4; dir++)
            {
                int row = sheetRows[dir];

                // Idle: 5 frames
                idleClips[dir] = new Sprite[5];
                for (int f = 0; f < 5; f++)
                {
                    spriteDict.TryGetValue($"bunny_idle_{row}_{f}", out idleClips[dir][f]);
                }

                // Run: 8 frames
                runClips[dir] = new Sprite[8];
                for (int f = 0; f < 8; f++)
                {
                    spriteDict.TryGetValue($"bunny_run_{row}_{f}", out runClips[dir][f]);
                }
            }
        }

        private void Update()
        {
            movement.x = Input.GetAxisRaw("Horizontal");
            movement.y = Input.GetAxisRaw("Vertical");

            if (movement != Vector2.zero)
            {
                movement.Normalize();
                if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y))
                {
                    currentDirection = movement.x > 0 ? 1 : 3; // Right or Left
                }
                else
                {
                    currentDirection = movement.y > 0 ? 0 : 2; // Up or Down
                }
            }

            Animate();
        }

        private void FixedUpdate()
        {
            rb.velocity = movement * moveSpeed;
        }

        private void Animate()
        {
            if (idleClips == null || runClips == null || spriteRenderer == null) return;

            bool isMoving = movement.sqrMagnitude > 0.01f;
            float frameDuration = isMoving ? 0.08f : 0.16f;
            int maxFrames = isMoving ? 8 : 5;

            animTimer += Time.deltaTime;
            if (animTimer >= frameDuration)
            {
                animTimer = 0f;
                animFrame = (animFrame + 1) % maxFrames;
            }

            Sprite currentSprite = null;
            if (isMoving)
            {
                if (runClips[currentDirection] != null && animFrame < runClips[currentDirection].Length)
                    currentSprite = runClips[currentDirection][animFrame];
            }
            else
            {
                if (idleClips[currentDirection] != null && animFrame < idleClips[currentDirection].Length)
                    currentSprite = idleClips[currentDirection][animFrame];
            }

            if (currentSprite != null)
            {
                spriteRenderer.sprite = currentSprite;
            }
        }
    }
}
