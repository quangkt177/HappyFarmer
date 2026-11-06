using UnityEngine;

namespace HappyFarmer.Gameplay
{
    public class AnimalController : MonoBehaviour
    {
        public enum AnimalType { Chicken, Cow }

        [Header("Animal Settings")]
        [SerializeField] private AnimalType animalType = AnimalType.Chicken;
        [SerializeField] private float moveSpeed = 1f;
        [SerializeField] private float idleDurationMin = 2f;
        [SerializeField] private float idleDurationMax = 5f;
        [SerializeField] private float wanderRadius = 2.5f;

        [Header("Sprites")]
        [SerializeField] private Sprite[] idleFrames;

        private SpriteRenderer spriteRenderer;
        private Rigidbody2D rb;
        private Vector2 originPosition;
        private Vector2 targetPosition;
        private bool isMoving;
        private float stateTimer;
        private float animTimer;
        private int animFrame;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            rb = GetComponent<Rigidbody2D>();
            originPosition = transform.position;
            stateTimer = Random.Range(idleDurationMin, idleDurationMax);
        }

        public void Setup(AnimalType type, float radius = 1.5f, float speed = 0.8f)
        {
            animalType = type;
            wanderRadius = radius;
            moveSpeed = speed;
        }

        public void SetFrames(Sprite[] frames)
        {
            idleFrames = frames;
            if (idleFrames != null && idleFrames.Length > 0 && spriteRenderer != null)
            {
                spriteRenderer.sprite = idleFrames[0];
            }
        }

        private void Update()
        {
            stateTimer -= Time.deltaTime;

            if (stateTimer <= 0f)
            {
                if (isMoving)
                {
                    // Stop moving, enter idle
                    isMoving = false;
                    if (rb != null) rb.velocity = Vector2.zero;
                    stateTimer = Random.Range(idleDurationMin, idleDurationMax);
                }
                else
                {
                    // 50% chance to wander slightly, 50% chance to stay idle
                    if (Random.value < 0.5f)
                    {
                        Vector2 randomOffset = Random.insideUnitCircle * wanderRadius;
                        targetPosition = originPosition + randomOffset;
                        isMoving = true;
                        stateTimer = Random.Range(1.5f, 3f);
                    }
                    else
                    {
                        stateTimer = Random.Range(idleDurationMin, idleDurationMax);
                    }
                }
            }

            if (isMoving && rb != null)
            {
                Vector2 currentPos = transform.position;
                Vector2 dir = (targetPosition - currentPos);
                if (dir.sqrMagnitude > 0.05f)
                {
                    dir.Normalize();
                    rb.velocity = dir * moveSpeed;
                    if (dir.x != 0f)
                    {
                        spriteRenderer.flipX = dir.x < 0f;
                    }
                }
                else
                {
                    isMoving = false;
                    rb.velocity = Vector2.zero;
                }
            }

            // Animate idle/peck/chew
            if (idleFrames != null && idleFrames.Length > 0)
            {
                float frameRate = animalType == AnimalType.Chicken ? 0.12f : 0.18f;
                animTimer += Time.deltaTime;
                if (animTimer >= frameRate)
                {
                    animTimer = 0f;
                    animFrame = (animFrame + 1) % idleFrames.Length;
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.sprite = idleFrames[animFrame];
                    }
                }
            }
        }
    }
}
