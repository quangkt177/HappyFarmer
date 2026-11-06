using UnityEngine;

namespace HappyFarmer.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 4f;

        private Rigidbody2D rb;
        private Animator animator;

        private Vector2 movement;
        private Vector2 lastDirection = Vector2.down; // Default facing down

        // Animator Parameter Hashes
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int LastMoveXHash = Animator.StringToHash("LastMoveX");
        private static readonly int LastMoveYHash = Animator.StringToHash("LastMoveY");

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
        }

        private void Update()
        {
            HandleInput();
            UpdateAnimation();
        }

        private void FixedUpdate()
        {
            // Normalize movement so diagonal speed is not faster
            Vector2 targetVelocity = movement.normalized * moveSpeed;
            rb.velocity = targetVelocity;
        }

        private void HandleInput()
        {
            movement.x = Input.GetAxisRaw("Horizontal");
            movement.y = Input.GetAxisRaw("Vertical");

            if (movement != Vector2.zero)
            {
                // Snap lastDirection to 4 cardinal directions for cleaner facing
                if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y))
                {
                    lastDirection = new Vector2(Mathf.Sign(movement.x), 0f);
                }
                else
                {
                    lastDirection = new Vector2(0f, Mathf.Sign(movement.y));
                }
            }
        }

        private void UpdateAnimation()
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                bool isMoving = movement.sqrMagnitude > 0.01f;

                animator.SetFloat(SpeedHash, isMoving ? movement.magnitude : 0f);

                if (isMoving)
                {
                    animator.SetFloat(MoveXHash, lastDirection.x);
                    animator.SetFloat(MoveYHash, lastDirection.y);
                }

                animator.SetFloat(LastMoveXHash, lastDirection.x);
                animator.SetFloat(LastMoveYHash, lastDirection.y);
            }
        }

        public Vector2 LastDirection => lastDirection;
        public bool IsMoving => movement.sqrMagnitude > 0.01f;
    }
}
