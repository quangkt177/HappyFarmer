using UnityEngine;

namespace HappyFarmer.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        public enum ToolType
        {
            Hoe = 0,
            WateringCan = 1,
            Scythe = 2
        }

        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 4f;

        [Header("Tool Settings")]
        [SerializeField] private ToolType selectedTool = ToolType.Hoe;

        private Rigidbody2D rb;
        private Animator animator;

        private Vector2 movement;
        private Vector2 lastDirection = Vector2.down; // Default facing down

        private bool isActing;
        private float actionTimer;
        private const float ActionDuration = 0.75f; // 9 frames @ 12 FPS

        // Animator Parameter Hashes
        private static readonly int MoveXHash = Animator.StringToHash("MoveX");
        private static readonly int MoveYHash = Animator.StringToHash("MoveY");
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int LastMoveXHash = Animator.StringToHash("LastMoveX");
        private static readonly int LastMoveYHash = Animator.StringToHash("LastMoveY");
        private static readonly int HoeHash = Animator.StringToHash("Hoe");
        private static readonly int WaterHash = Animator.StringToHash("Water");
        private static readonly int ScytheHash = Animator.StringToHash("Scythe");

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
        }

        private void Update()
        {
            if (isActing)
            {
                actionTimer -= Time.deltaTime;
                if (actionTimer <= 0f)
                {
                    isActing = false;
                }
                return;
            }

            HandleActionInput();
            if (!isActing)
            {
                HandleMovementInput();
                UpdateAnimation();
            }
        }

        private void FixedUpdate()
        {
            if (isActing)
            {
                rb.velocity = Vector2.zero;
                return;
            }

            // Normalize movement so diagonal speed is not faster
            Vector2 targetVelocity = movement.normalized * moveSpeed;
            rb.velocity = targetVelocity;
        }

        private void HandleMovementInput()
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

        private void HandleActionInput()
        {
            // Direct hotkeys: 1/J = Hoe, 2/K = Water, 3/L = Scythe
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.J))
            {
                selectedTool = ToolType.Hoe;
                PerformAction(ToolType.Hoe);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.K))
            {
                selectedTool = ToolType.WateringCan;
                PerformAction(ToolType.WateringCan);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3) || Input.GetKeyDown(KeyCode.L))
            {
                selectedTool = ToolType.Scythe;
                PerformAction(ToolType.Scythe);
            }
            // Use currently selected tool with Space or Left Mouse Click
            else if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            {
                PerformAction(selectedTool);
            }
        }

        public void PerformAction(ToolType tool)
        {
            if (isActing) return;

            isActing = true;
            actionTimer = ActionDuration;
            movement = Vector2.zero;
            rb.velocity = Vector2.zero;

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetFloat(SpeedHash, 0f);
                animator.SetFloat(LastMoveXHash, lastDirection.x);
                animator.SetFloat(LastMoveYHash, lastDirection.y);

                switch (tool)
                {
                    case ToolType.Hoe:
                        animator.SetTrigger(HoeHash);
                        break;
                    case ToolType.WateringCan:
                        animator.SetTrigger(WaterHash);
                        break;
                    case ToolType.Scythe:
                        animator.SetTrigger(ScytheHash);
                        break;
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
        public bool IsActing => isActing;
        public ToolType SelectedTool => selectedTool;
    }
}
