using UnityEngine;

namespace HappyFarmer.Inventory
{
    public class CropPlot : MonoBehaviour
    {
        public string cropKey;
        public string harvestItemId;
        public int currentStage;
        public float secondsPerStage = 3.5f;

        private float stageTimer;
        private Sprite[] stageSprites;
        private SpriteRenderer cropRenderer;
        private SpriteRenderer soilRenderer;

        public bool IsMature => stageSprites != null && currentStage >= stageSprites.Length - 1;

        public void Initialize(string key, string resultItemId, Sprite soilWetSprite, Sprite[] stages)
        {
            cropKey = key;
            harvestItemId = resultItemId;
            stageSprites = stages;
            currentStage = 0;
            stageTimer = 0f;

            // Lớp đất ướt bên dưới
            var soilObj = new GameObject("WetSoil");
            soilObj.transform.SetParent(transform, false);
            soilObj.transform.localPosition = Vector3.zero;
            soilRenderer = soilObj.AddComponent<SpriteRenderer>();
            soilRenderer.sprite = soilWetSprite;
            soilRenderer.sortingLayerName = "Ground";
            soilRenderer.sortingOrder = 3;

            // Lớp cây trồng bên trên
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
            if (stageSprites == null || stageSprites.Length == 0) return;

            if (!IsMature)
            {
                stageTimer += Time.deltaTime;
                if (stageTimer >= secondsPerStage)
                {
                    stageTimer = 0f;
                    currentStage = Mathf.Min(currentStage + 1, stageSprites.Length - 1);
                    UpdateVisual();

                    if (IsMature && InventoryManager.Instance != null)
                    {
                        var itemData = InventoryManager.Instance.GetItemData(harvestItemId);
                        string cropName = itemData != null ? itemData.displayName : cropKey;
                        InventoryManager.Instance.Notify($"{cropName} đã chín! Bấm [E] gần cây để thu hoạch.");
                    }
                }
            }
        }

        private void UpdateVisual()
        {
            if (cropRenderer != null && stageSprites != null && currentStage >= 0 && currentStage < stageSprites.Length)
            {
                cropRenderer.sprite = stageSprites[currentStage];
            }
        }

        public bool TryHarvest(InventoryManager manager)
        {
            if (!IsMature || manager == null) return false;

            int yieldAmount = Random.Range(2, 4);
            if (manager.AddItem(harvestItemId, yieldAmount))
            {
                Destroy(gameObject);
                return true;
            }
            return false;
        }
    }
}
