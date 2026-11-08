using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyFarmer.Inventory
{
    [Serializable]
    public class StarterSlotConfig
    {
        [Min(0)] public int slotIndex;
        public string itemId;
        [Min(1)] public int count = 1;
    }

    [Serializable]
    public class ForagePropConfig
    {
        [Tooltip("Tên GameObject trên bản đồ (ví dụ: mushroom, bush, rock_small)")]
        public string sceneObjectName;
        [Tooltip("ID vật phẩm nhận được khi thu thập")]
        public string rewardItemId;
        [Min(1)] public int amount = 1;
    }

    [Serializable]
    public class WorldPickupSpawnConfig
    {
        public string itemId;
        [Min(1)] public int count = 1;
        public Vector2 position;
    }

    [Serializable]
    public class ParcelPriceConfig
    {
        [Tooltip("Tên GameObject khu đất trên bản đồ (ví dụ: Parcel East)")]
        public string parcelName;
        [Min(0)] public int price = 150;
    }

    [CreateAssetMenu(fileName = "EconomyDatabase", menuName = "Happy Farmer/Economy & Item Database", order = 1)]
    public class EconomyDatabaseSO : ScriptableObject
    {
        [Header("Cấu hình Sức chứa Túi đồ (Capacity)")]
        [Min(1)] public int hotbarSize = 8;
        [Min(4)] public int backpackSize = 16;

        [Header("Thiết lập Tiền tệ (Currency)")]
        public string currencyName = "Xu Vàng";
        public string currencySymbol = "Xu";
        [Min(0)] public int startingBalance = 100;

        [Header("Danh mục Vật phẩm & Mệnh giá (Item Catalog)")]
        public List<InventoryItemData> items = new List<InventoryItemData>();

        [Header("Vật phẩm khởi đầu (Starter Slots)")]
        public List<StarterSlotConfig> starterSlots = new List<StarterSlotConfig>();

        [Header("Thu thập Vật thể trên Bản đồ (Forage Rules)")]
        public List<ForagePropConfig> forageRules = new List<ForagePropConfig>();

        [Header("Vật phẩm rơi sẵn trên Bản đồ (Initial Pickups)")]
        public List<WorldPickupSpawnConfig> initialWorldPickups = new List<WorldPickupSpawnConfig>();

        [Header("Sạp Gia Súc (Pasture Stall)")]
        [Min(0)] public int chickBuyPrice = 40;
        [Min(0)] public int calfBuyPrice = 120;
        [Min(0)] public int chickenSellPrice = 70;
        [Min(0)] public int cowSellPrice = 220;
        [Min(1)] public int maxChickens = 12;
        [Min(1)] public int maxCows = 8;

        [Header("Giá mở Khu Đất (Parcels)")]
        public List<ParcelPriceConfig> parcelPrices = new List<ParcelPriceConfig>();

        public int TotalSlots => Mathf.Max(1, hotbarSize) + Mathf.Max(1, backpackSize);

        public string FormatCurrency(int amount)
        {
            string sym = string.IsNullOrEmpty(currencySymbol) ? "Xu" : currencySymbol;
            return $"{amount:N0} {sym}";
        }

        public int GetParcelPrice(string parcelName, int fallback)
        {
            if (parcelPrices == null) return fallback;
            foreach (var cfg in parcelPrices)
            {
                if (cfg != null && cfg.parcelName == parcelName) return cfg.price;
            }
            return fallback;
        }

        public void PopulateDefaultDataIfEmpty(Func<string, Sprite> spriteResolver = null)
        {
            if (items == null) items = new List<InventoryItemData>();
            if (items.Count == 0)
            {
                items = new List<InventoryItemData>
                {
                    // Hạt giống
                    CreateEntry("seed_carrot", "Hạt Cà Rốt",
                        "Hạt giống cà rốt ngọt giòn. Đứng trên ô đất ruộng và bấm [E] hoặc nút [Gieo Hạt] để trồng.",
                        ItemCategory.Seed, "seed_carrot", sellPrice: 5, canBuy: true, buyPrice: 10,
                        cropKey: "carrot", harvestId: "produce_carrot", spriteResolver: spriteResolver),

                    CreateEntry("seed_cauliflower", "Hạt Súp Lơ",
                        "Hạt giống súp lơ trắng tươi mát, dễ trồng trên đất nông trại.",
                        ItemCategory.Seed, "seed_cauliflower", sellPrice: 8, canBuy: true, buyPrice: 15,
                        cropKey: "cauliflower", harvestId: "produce_cauliflower", spriteResolver: spriteResolver),

                    CreateEntry("seed_strawberry", "Hạt Dâu Tây",
                        "Hạt giống dâu tây đỏ mọng. Gieo xuống đất bằng phím [E] và chờ thu hoạch.",
                        ItemCategory.Seed, "seed_strawberry", sellPrice: 10, canBuy: true, buyPrice: 20,
                        cropKey: "strawberry", harvestId: "produce_strawberry", spriteResolver: spriteResolver),

                    CreateEntry("seed_pumpkin", "Hạt Bí Ngô",
                        "Hạt giống bí ngô vàng ươm đặc trưng của vùng Little Dreamyland.",
                        ItemCategory.Seed, "seed_pumpkin", sellPrice: 15, canBuy: true, buyPrice: 30,
                        cropKey: "pumpkin", harvestId: "produce_pumpkin", spriteResolver: spriteResolver),

                    // Nông sản thu hoạch
                    CreateEntry("produce_carrot", "Cà Rốt Tươi",
                        "Củ cà rốt giòn ngọt vừa thu hoạch. Có thể bán lấy Xu hoặc sử dụng ngay.",
                        ItemCategory.Produce, "produce_carrot", sellPrice: 18, canBuy: false, buyPrice: 0,
                        spriteResolver: spriteResolver),

                    CreateEntry("produce_cauliflower", "Súp Lơ Trắng",
                        "Bông súp lơ trắng tinh khôi, xanh tươi từ thửa ruộng nhà.",
                        ItemCategory.Produce, "produce_cauliflower", sellPrice: 26, canBuy: false, buyPrice: 0,
                        spriteResolver: spriteResolver),

                    CreateEntry("produce_strawberry", "Dâu Tây Đỏ",
                        "Trái dâu tây chín mọng thơm lừng, đặc sản được yêu thích nhất nông trại.",
                        ItemCategory.Produce, "produce_strawberry", sellPrice: 35, canBuy: false, buyPrice: 0,
                        spriteResolver: spriteResolver),

                    CreateEntry("produce_pumpkin", "Bí Ngô Vàng",
                        "Quả bí ngô tròn đầy đặn, nông sản giá trị cao của nông trại.",
                        ItemCategory.Produce, "produce_pumpkin", sellPrice: 50, canBuy: false, buyPrice: 0,
                        spriteResolver: spriteResolver),

                    // Hái lượm
                    CreateEntry("mushroom", "Nấm Rừng",
                        "Nấm mọc tự nhiên quanh bìa rừng và các bụi cây ẩm ướt.",
                        ItemCategory.Forage, "mushroom", sellPrice: 15, canBuy: false, buyPrice: 0,
                        spriteResolver: spriteResolver),

                    CreateEntry("flowers_white", "Hoa Cúc Trắng",
                        "Bông hoa dại trắng tinh khôi hái được trên đồng cỏ.",
                        ItemCategory.Forage, "flowers_white", sellPrice: 12, canBuy: false, buyPrice: 0,
                        spriteResolver: spriteResolver),

                    CreateEntry("flowers_yellow", "Hoa Cúc Vàng",
                        "Bông hoa cúc vàng rực rỡ mang lại niềm vui cho người nông dân.",
                        ItemCategory.Forage, "flowers_yellow", sellPrice: 12, canBuy: false, buyPrice: 0,
                        spriteResolver: spriteResolver),

                    // Nguyên liệu
                    CreateEntry("log", "Khúc Gỗ Sồi",
                        "Gỗ tự nhiên thu thập khi phát quang bụi cây trên nông trại.",
                        ItemCategory.Resource, "log", sellPrice: 8, canBuy: false, buyPrice: 0,
                        spriteResolver: spriteResolver),

                    CreateEntry("rock_small", "Đá Cuội Nhỏ",
                        "Viên đá cuội nhặt từ các thửa đất hoang quanh nông trại.",
                        ItemCategory.Resource, "rock_small", sellPrice: 6, canBuy: false, buyPrice: 0,
                        spriteResolver: spriteResolver)
                };
            }

            if (starterSlots == null || starterSlots.Count == 0)
            {
                starterSlots = new List<StarterSlotConfig>
                {
                    new StarterSlotConfig { slotIndex = 0, itemId = "seed_carrot", count = 6 },
                    new StarterSlotConfig { slotIndex = 1, itemId = "seed_strawberry", count = 5 },
                    new StarterSlotConfig { slotIndex = 2, itemId = "seed_pumpkin", count = 4 },
                    new StarterSlotConfig { slotIndex = 3, itemId = "seed_cauliflower", count = 4 },
                    new StarterSlotConfig { slotIndex = 4, itemId = "produce_carrot", count = 3 },
                    new StarterSlotConfig { slotIndex = 5, itemId = "mushroom", count = 2 },

                    new StarterSlotConfig { slotIndex = 8, itemId = "produce_strawberry", count = 4 },
                    new StarterSlotConfig { slotIndex = 9, itemId = "produce_pumpkin", count = 2 },
                    new StarterSlotConfig { slotIndex = 10, itemId = "log", count = 8 },
                    new StarterSlotConfig { slotIndex = 11, itemId = "rock_small", count = 6 },
                    new StarterSlotConfig { slotIndex = 12, itemId = "flowers_white", count = 3 },
                    new StarterSlotConfig { slotIndex = 13, itemId = "flowers_yellow", count = 2 }
                };
            }

            if (forageRules == null || forageRules.Count == 0)
            {
                forageRules = new List<ForagePropConfig>
                {
                    new ForagePropConfig { sceneObjectName = "mushroom", rewardItemId = "mushroom", amount = 1 },
                    new ForagePropConfig { sceneObjectName = "rock_small", rewardItemId = "rock_small", amount = 1 },
                    new ForagePropConfig { sceneObjectName = "flowers_white", rewardItemId = "flowers_white", amount = 1 },
                    new ForagePropConfig { sceneObjectName = "flowers_yellow", rewardItemId = "flowers_yellow", amount = 1 },
                    new ForagePropConfig { sceneObjectName = "log", rewardItemId = "log", amount = 1 },
                    new ForagePropConfig { sceneObjectName = "bush", rewardItemId = "log", amount = 1 },
                    new ForagePropConfig { sceneObjectName = "bush_round", rewardItemId = "log", amount = 1 }
                };
            }

            if (initialWorldPickups == null || initialWorldPickups.Count == 0)
            {
                initialWorldPickups = new List<WorldPickupSpawnConfig>
                {
                    new WorldPickupSpawnConfig { itemId = "mushroom", count = 1, position = new Vector2(8.5f, 6.4f) },
                    new WorldPickupSpawnConfig { itemId = "flowers_yellow", count = 1, position = new Vector2(12.2f, 7.2f) },
                    new WorldPickupSpawnConfig { itemId = "seed_carrot", count = 2, position = new Vector2(9.6f, 8.5f) },
                    new WorldPickupSpawnConfig { itemId = "log", count = 2, position = new Vector2(6.2f, 5.2f) },
                    new WorldPickupSpawnConfig { itemId = "produce_strawberry", count = 1, position = new Vector2(23.5f, 10.2f) }
                };
            }

            if (parcelPrices == null || parcelPrices.Count == 0)
            {
                parcelPrices = new List<ParcelPriceConfig>
                {
                    new ParcelPriceConfig { parcelName = "Parcel East", price = 150 },
                    new ParcelPriceConfig { parcelName = "Parcel South", price = 300 }
                };
            }
        }

        private static InventoryItemData CreateEntry(
            string id,
            string displayName,
            string description,
            ItemCategory category,
            string spriteName,
            int sellPrice,
            bool canBuy,
            int buyPrice,
            string cropKey = "",
            string harvestId = "",
            Func<string, Sprite> spriteResolver = null)
        {
            return new InventoryItemData
            {
                id = id,
                displayName = displayName,
                description = description,
                category = category,
                spriteName = spriteName,
                icon = spriteResolver != null ? spriteResolver(spriteName) : null,
                maxStack = 99,
                canBeSold = sellPrice > 0,
                sellPrice = sellPrice,
                canBeBought = canBuy,
                buyPrice = buyPrice,
                cropKey = cropKey,
                harvestItemId = harvestId,
                secondsPerStage = 3.5f,
                minHarvestYield = 2,
                maxHarvestYield = 3
            };
        }
    }
}
