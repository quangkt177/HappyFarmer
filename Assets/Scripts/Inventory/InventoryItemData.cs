using System;
using UnityEngine;

namespace HappyFarmer.Inventory
{
    public enum ItemCategory
    {
        Seed,       // Hạt giống
        Produce,    // Nông sản thu hoạch
        Forage,     // Vật phẩm hái lượm
        Resource    // Nguyên liệu tự nhiên
    }

    [Serializable]
    public class InventoryItemData
    {
        public string id;
        public string displayName;
        [TextArea(2, 4)]
        public string description;
        public ItemCategory category;
        public string spriteName;
        public Sprite icon;
        public int maxStack = 99;

        // Dùng cho hạt giống: tên cây trồng (vd: "carrot", "strawberry", "pumpkin", "cauliflower")
        public string cropKey;
        public string harvestItemId;

        public string GetCategoryDisplayName()
        {
            switch (category)
            {
                case ItemCategory.Seed: return "Hạt giống";
                case ItemCategory.Produce: return "Nông sản";
                case ItemCategory.Forage: return "Hái lượm";
                case ItemCategory.Resource: return "Nguyên liệu";
                default: return "Vật phẩm";
            }
        }
    }

    [Serializable]
    public class InventorySlot
    {
        public string itemId;
        public int count;

        public bool IsEmpty => string.IsNullOrEmpty(itemId) || count <= 0;

        public void Set(string id, int amount)
        {
            itemId = id;
            count = amount;
            if (count <= 0)
            {
                Clear();
            }
        }

        public void Clear()
        {
            itemId = string.Empty;
            count = 0;
        }

        public InventorySlot Clone()
        {
            return new InventorySlot
            {
                itemId = this.itemId,
                count = this.count
            };
        }
    }
}
