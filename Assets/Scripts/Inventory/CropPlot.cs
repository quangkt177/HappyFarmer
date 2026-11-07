using UnityEngine;

namespace HappyFarmer.Inventory
{
    public class CropPlot : MonoBehaviour
    {
        public string cropKey;
        public string harvestItemId;
        public int currentStage;
        public float secondsPerStage = 3.5f;
        public int minYield = 2;
        public int maxYield = 3;

        private float stageTimer;
        private Sprite[] stageSprites;
        private SpriteRenderer cropRenderer;

        public bool IsMature => stageSprites != null && stageSprites.Length > 0 && currentStage >= stageSprites.Length - 1;

        public void Initialize(InventoryItemData seedData, Sprite soilSprite, Sprite[] stages)
        {
            cropKey = seedData.cropKey;
            harvestItemId = seedData.harvestItemId;
            secondsPerStage = Mathf.Max(0.5f, seedData.secondsPerStage);
            minYield = Mathf.Max(1, seedData.minHarvestYield);
            maxYield = Mathf.Max(minYield, seedData.maxHarvestYield);
            stageSprites = stages;
            currentStage = 0;
            stageTimer = 0f;

            var soilObj = new GameObject("WetSoil");
            soilObj.transform.SetParent(transform, false);
            var soilRenderer = soilObj.AddComponent<SpriteRenderer>();
            soilRenderer.sprite = soilSprite;
            soilRenderer.sortingLayerName = "Ground";
            soilRenderer.sortingOrder = 3;

            var cropObj = new GameObject("CropSprite");
            cropObj.transform.SetParent(transform, false);
            cropObj.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            cropRenderer = cropObj.AddComponent<SpriteRenderer>();
            cropRenderer.sortingLayerName = "World";
            cropRenderer.sortingOrder = 3;
            cropRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

            UpdateVisual();
        }

        private void Update()
        {
            if (IsMature || stageSprites == null || stageSprites.Length == 0) return;

            stageTimer += Time.deltaTime;
            if (stageTimer < secondsPerStage) return;

            stageTimer = 0f;
            currentStage = Mathf.Min(currentStage + 1, stageSprites.Length - 1);
            UpdateVisual();

            if (IsMature && InventoryManager.Instance != null)
            {
                var item = InventoryManager.Instance.GetItemData(harvestItemId);
                string cropName = item != null ? item.displayName : cropKey;
                InventoryManager.Instance.Notify($"{cropName} đã chín! Bấm [E] gần cây để thu hoạch.");
            }
        }

        private void UpdateVisual()
        {
            if (cropRenderer != null && stageSprites != null && currentStage < stageSprites.Length)
            {
                cropRenderer.sprite = stageSprites[currentStage];
            }
        }

        public bool TryHarvest(InventoryManager manager)
        {
            if (!IsMature || manager == null) return false;

            int yieldAmount = Random.Range(minYield, maxYield + 1);
            if (!manager.AddItem(harvestItemId, yieldAmount)) return false;

            Destroy(gameObject);
            return true;
        }
    }
}
