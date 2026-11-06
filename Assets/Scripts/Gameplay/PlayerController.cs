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
        
        // Directions: 0 = Down, 1 = Up, 2 = Left, 3 = Right
        private int currentDirection = 0; // Default facing down

        private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
        private static readonly int DirectionHash = Animator.StringToHash("direction");

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
                // Prioritize dominant axis for 4-directional animations
                if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y))
                {
                    // 3: Right, 2: Left
                    currentDirection = movement.x > 0 ? 3 : 2;
                }
                else
                {
                    // 1: Up, 0: Down
                    currentDirection = movement.y > 0 ? 1 : 0;
                }
            }
        }

        private void UpdateAnimation()
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                bool isMoving = movement.sqrMagnitude > 0.01f;
                animator.SetBool(IsMovingHash, isMoving);
                animator.SetInteger(DirectionHash, currentDirection);
            }
        }

        public int CurrentDirection => currentDirection;
        public bool IsMoving => movement.sqrMagnitude > 0.01f;
    }
}
