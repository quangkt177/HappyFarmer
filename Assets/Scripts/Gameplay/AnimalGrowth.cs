using System.Collections;
using UnityEngine;

namespace HappyFarmer.Gameplay
{
    public class AnimalGrowth : MonoBehaviour
    {
        public enum Species { Chicken, Cow }
        public enum GrowthStage { Egg, Baby, Adult }

        [Header("Animal Info")]
        [SerializeField] private Species species = Species.Chicken;
        [SerializeField] private GrowthStage currentStage = GrowthStage.Baby;

        [Header("Growth Timers")]
        [Tooltip("Seconds required to reach the next stage")]
        [SerializeField] private float secondsPerStage = 15f;
        [SerializeField] private bool autoGrow = true;

        [Header("Sprites for Stages")]
        [SerializeField] private Sprite[] eggFrames;
        [SerializeField] private Sprite[] babyFrames;
        [SerializeField] private Sprite[] adultFrames;

        private float growthTimer;
        private AnimalController controller;
        private SpriteRenderer spriteRenderer;
        private bool isTransitioning;

        public Species AnimalSpecies => species;
        public GrowthStage CurrentStage => currentStage;
        public float GrowthPercent => Mathf.Clamp01(growthTimer / secondsPerStage);

        private void Awake()
        {
            controller = GetComponent<AnimalController>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            ApplyCurrentStageVisuals();
        }

        public void Setup(Species s, GrowthStage stage, Sprite[] egg, Sprite[] baby, Sprite[] adult, float stageDuration = 15f)
        {
            species = s;
            currentStage = stage;
            eggFrames = egg;
            babyFrames = baby;
            adultFrames = adult;
            secondsPerStage = stageDuration;
            ApplyCurrentStageVisuals();
        }

        private void Update()
        {
            if (!autoGrow || isTransitioning || currentStage == GrowthStage.Adult) return;

            growthTimer += Time.deltaTime;
            if (growthTimer >= secondsPerStage)
            {
                GrowToNextStage();
            }

            // Press E when player is near to feed
            CheckPlayerFeeding();
        }

        public void Feed()
        {
            if (currentStage == GrowthStage.Adult) return;

            // Feeding speeds up growth
            growthTimer += 5f;
            StartCoroutine(LoveAnimationRoutine());
            Debug.Log($"[HappyFarmer] Fed {species}! Growth progress: {(int)(GrowthPercent * 100)}%");

            if (growthTimer >= secondsPerStage)
            {
                GrowToNextStage();
            }
        }

        private void CheckPlayerFeeding()
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    float dist = Vector2.Distance(player.transform.position, transform.position);
                    if (dist < 1.8f)
                    {
                        Feed();
                    }
                }
            }
        }

        public void GrowToNextStage()
        {
            if (isTransitioning) return;

            if (species == Species.Chicken)
            {
                if (currentStage == GrowthStage.Egg)
                {
                    StartCoroutine(EvolveRoutine(GrowthStage.Baby, "🐣 Trứng đã nở thành Gà con!"));
                }
                else if (currentStage == GrowthStage.Baby)
                {
                    StartCoroutine(EvolveRoutine(GrowthStage.Adult, "🐔 Gà con đã lớn thành Gà mái!"));
                }
            }
            else if (species == Species.Cow)
            {
                if (currentStage == GrowthStage.Baby)
                {
                    StartCoroutine(EvolveRoutine(GrowthStage.Adult, "🐮 Bê con đã lớn thành Bò sữa!"));
                }
            }
        }

        private IEnumerator EvolveRoutine(GrowthStage nextStage, string message)
        {
            isTransitioning = true;
            growthTimer = 0f;

            // 1. Squash & Stretch anticipation
            Vector3 originalScale = transform.localScale;
            for (float t = 0; t < 0.25f; t += Time.deltaTime)
            {
                float progress = t / 0.25f;
                transform.localScale = new Vector3(originalScale.x * (1f + 0.3f * Mathf.Sin(progress * Mathf.PI)),
                                                   originalScale.y * (1f - 0.2f * Mathf.Sin(progress * Mathf.PI)), 1f);
                yield return null;
            }

            // 2. Big Growth Pop!
            currentStage = nextStage;
            ApplyCurrentStageVisuals();
            Debug.Log($"[HappyFarmer] {message}");

            for (float t = 0; t < 0.35f; t += Time.deltaTime)
            {
                float progress = t / 0.35f;
                float pop = Mathf.Sin(progress * Mathf.PI);
                transform.localScale = originalScale * (1f + pop * 0.4f);
                yield return null;
            }

            transform.localScale = originalScale;
            isTransitioning = false;
        }

        private IEnumerator LoveAnimationRoutine()
        {
            Vector3 orig = transform.localScale;
            for (float t = 0; t < 0.2f; t += Time.deltaTime)
            {
                transform.localScale = orig * 1.2f;
                yield return null;
            }
            transform.localScale = orig;
        }

        private void ApplyCurrentStageVisuals()
        {
            if (controller == null) controller = GetComponent<AnimalController>();
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

            Sprite[] activeFrames = null;
            switch (currentStage)
            {
                case GrowthStage.Egg:
                    activeFrames = eggFrames;
                    if (controller != null) controller.enabled = false; // Eggs don't wander
                    break;
                case GrowthStage.Baby:
                    activeFrames = babyFrames;
                    if (controller != null) controller.enabled = true;
                    break;
                case GrowthStage.Adult:
                    activeFrames = adultFrames;
                    if (controller != null) controller.enabled = true;
                    break;
            }

            if (controller != null && activeFrames != null && activeFrames.Length > 0)
            {
                controller.SetFrames(activeFrames);
            }
            else if (spriteRenderer != null && activeFrames != null && activeFrames.Length > 0)
            {
                spriteRenderer.sprite = activeFrames[0];
            }
        }
    }
}
