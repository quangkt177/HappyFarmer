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
        [Header("Thông tin cơ bản")]
        public string id;
        public string displayName;
        [TextArea(2, 4)]
        public string description;
        public ItemCategory category;
        public string spriteName;
        public Sprite icon;
        [Min(1)]
        public int maxStack = 99;

        [Header("Kinh tế (Economy)")]
        public bool canBeSold = true;
        [Min(0)]
        public int sellPrice = 10;
        public bool canBeBought;
        [Min(0)]
        public int buyPrice;

        [Header("Trồng trọt (Chỉ dùng cho Seed)")]
        public string cropKey;
        public string harvestItemId;
        [Min(0.5f)]
        public float secondsPerStage = 3.5f;
        [Min(1)]
        public int minHarvestYield = 2;
        [Min(1)]
        public int maxHarvestYield = 3;

        public bool CanSell => canBeSold && sellPrice > 0;
        public bool CanBuy => canBeBought && buyPrice > 0;

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

        public string GetActionLabel()
        {
            switch (category)
            {
                case ItemCategory.Seed: return "Gieo Hạt";
                case ItemCategory.Produce:
                case ItemCategory.Forage: return "Sử Dụng";
                default: return "Đặt Ra";
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
            if (string.IsNullOrEmpty(id) || amount <= 0)
            {
                Clear();
                return;
            }
            itemId = id;
            count = amount;
        }

        public int Add(int amount, int maxStack)
        {
            if (amount <= 0) return 0;
            int space = Mathf.Max(0, maxStack - count);
            int take = Mathf.Min(amount, space);
            count += take;
            return take;
        }

        public int Remove(int amount)
        {
            if (amount <= 0 || IsEmpty) return 0;
            int take = Mathf.Min(amount, count);
            count -= take;
            if (count <= 0) Clear();
            return take;
        }

        public void Clear()
        {
            itemId = string.Empty;
            count = 0;
        }
    }
}
