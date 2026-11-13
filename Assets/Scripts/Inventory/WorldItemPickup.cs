using UnityEngine;

namespace HappyFarmer.Inventory
{
    public class WorldItemPickup : MonoBehaviour
    {
        public string itemId;
        public int amount = 1;
        public float pickupRadius = 0.85f;
        public float bobSpeed = 3.2f;
        public float bobHeight = 0.08f;

        private Vector3 basePosition;
        private float spawnTime;
        private float pickupCooldown = 0.4f;
        private SpriteRenderer spriteRenderer;

        public void Initialize(string id, int count, Sprite icon, float cooldown = 0.4f)
        {
            itemId = id;
            amount = Mathf.Max(1, count);
            pickupCooldown = cooldown;
            basePosition = transform.position;
            spawnTime = Time.time;

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            spriteRenderer.sprite = icon;
            spriteRenderer.sortingLayerName = "World";
            spriteRenderer.sortingOrder = 5;
        }

        private void Start()
        {
            basePosition = transform.position;
            if (spawnTime <= 0f) spawnTime = Time.time;
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            // Hiệu ứng nhấp nhô nhẹ cho vật phẩm rơi trên đất
            float offset = Mathf.Sin((Time.time - spawnTime) * bobSpeed) * bobHeight;
            transform.position = new Vector3(basePosition.x, basePosition.y + Mathf.Max(0f, offset), basePosition.z);

            if (Time.time - spawnTime < pickupCooldown) return;

            var manager = InventoryManager.Instance;
            if (manager == null || manager.PlayerTransform == null) return;

            float dist = Vector2.Distance(basePosition, manager.PlayerTransform.position);
            if (dist <= pickupRadius)
            {
                if (manager.AddItem(itemId, amount))
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
