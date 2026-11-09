using System;
using System.Collections.Generic;
using HappyFarmer.Gameplay;
using HappyFarmer.Inventory;
using HappyFarmer.Economy;
using UnityEngine;

namespace HappyFarmer.Shop
{
    public enum ShopKind
    {
        Farm,       // Sạp nông sản: mua hạt giống, bán nông sản
        Pasture     // Sạp gia súc: mua gà con / bê con, bán gà / bò trưởng thành
    }

    public class ShopOffer
    {
        public string title;
        public string detail;
        public Sprite icon;
        public string priceText;
        public string actionLabel;
        public Func<bool> action;
        public string secondaryLabel;
        public Func<bool> secondaryAction;
    }

    public class ShopStall : MonoBehaviour
    {
        [SerializeField] private ShopKind kind = ShopKind.Farm;
        [SerializeField] private string shopTitle = "Sạp Nông Sản";
        [SerializeField] private float interactRadius = 1.5f;
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        private Collider2D area;

        public string ShopTitle => shopTitle;

        public void Setup(ShopKind shopKind, string title)
        {
            kind = shopKind;
            shopTitle = title;
        }

        private void Awake()
        {
            area = GetComponent<Collider2D>();
        }

        private void Update()
        {
            var ui = ShopUI.Instance;
            var manager = InventoryManager.Instance;
            if (ui == null || manager == null || manager.PlayerTransform == null) return;

            Vector2 playerPos = manager.PlayerTransform.position;
            Vector2 closest = area != null ? area.ClosestPoint(playerPos) : (Vector2)transform.position;
            bool isNear = Vector2.Distance(playerPos, closest) <= interactRadius;

            if (!isNear)
            {
                ui.HidePrompt(this);
                if (ui.CurrentStall == this) ui.Close();
                return;
            }

            if (ui.CurrentStall == this) return;

            ui.ShowPrompt(this, $"[{interactKey}] Mở {shopTitle}");
            if (Input.GetKeyDown(interactKey)) ui.Open(this);
        }

        // =====================================================================
        // DANH SÁCH HÀNG
        // =====================================================================

        public List<ShopOffer> GetOffers(bool isBuyTab)
        {
            var manager = InventoryManager.Instance;
            if (manager == null) return new List<ShopOffer>();

            if (kind == ShopKind.Farm)
                return isBuyTab ? GetSeedOffers(manager) : GetProduceOffers(manager);
            return isBuyTab ? GetBabyAnimalOffers(manager) : GetAdultAnimalOffers(manager);
        }

        private static List<ShopOffer> GetSeedOffers(InventoryManager manager)
        {
            var offers = new List<ShopOffer>();
            var econ = EconomyManager.Instance;
            var db = econ != null ? econ.Database : manager.EconomyDatabase;
            if (db == null || db.items == null) return offers;

            foreach (var item in db.items)
            {
                if (item == null || !item.CanBuy) continue;
                string id = item.id;
                offers.Add(new ShopOffer
                {
                    title = item.displayName,
                    detail = $"Đang có: {CountItem(manager, id)}",
                    icon = item.icon,
                    priceText = econ != null ? econ.FormatCurrency(item.buyPrice) : manager.FormatCurrency(item.buyPrice),
                    actionLabel = "Mua 1",
                    action = () => BuyItem(manager, id, 1),
                    secondaryLabel = "Mua 5",
                    secondaryAction = () => BuyItem(manager, id, 5)
                });
            }
            return offers;
        }

        private static List<ShopOffer> GetProduceOffers(InventoryManager manager)
        {
            var offers = new List<ShopOffer>();
            var econ = EconomyManager.Instance;
            var db = econ != null ? econ.Database : manager.EconomyDatabase;
            if (db == null || db.items == null) return offers;

            foreach (var item in db.items)
            {
                if (item == null || !item.CanSell || item.category == ItemCategory.Seed) continue;
                int owned = CountItem(manager, item.id);
                if (owned <= 0) continue;

                string id = item.id;
                offers.Add(new ShopOffer
                {
                    title = item.displayName,
                    detail = $"{item.GetCategoryDisplayName()}  |  Đang có: {owned}",
                    icon = item.icon,
                    priceText = econ != null ? econ.FormatCurrency(item.sellPrice) : manager.FormatCurrency(item.sellPrice),
                    actionLabel = "Bán 1",
                    action = () => SellItem(manager, id, 1),
                    secondaryLabel = "Bán Hết",
                    secondaryAction = () => SellItem(manager, id, int.MaxValue)
                });
            }
            return offers;
        }

        private static List<ShopOffer> GetBabyAnimalOffers(InventoryManager manager)
        {
            var econ = EconomyManager.Instance;
            var db = econ != null ? econ.Database : manager.EconomyDatabase;
            if (db == null) return new List<ShopOffer>();
            return new List<ShopOffer>
            {
                BabyOffer(manager, AnimalGrowth.Species.Chicken, "Gà Con", db.chickBuyPrice, db.maxChickens),
                BabyOffer(manager, AnimalGrowth.Species.Cow, "Bê Con", db.calfBuyPrice, db.maxCows)
            };
        }

        private static List<ShopOffer> GetAdultAnimalOffers(InventoryManager manager)
        {
            var econ = EconomyManager.Instance;
            var db = econ != null ? econ.Database : manager.EconomyDatabase;
            if (db == null) return new List<ShopOffer>();
            return new List<ShopOffer>
            {
                AdultOffer(manager, AnimalGrowth.Species.Chicken, "Gà Mái", db.chickenSellPrice),
                AdultOffer(manager, AnimalGrowth.Species.Cow, "Bò Sữa", db.cowSellPrice)
            };
        }

        private static ShopOffer BabyOffer(InventoryManager manager, AnimalGrowth.Species species, string name, int price, int maxCount)
        {
            int total = AnimalMarket.CountAnimals(species);
            var econ = EconomyManager.Instance;
            return new ShopOffer
            {
                title = name,
                detail = $"Thả vào chuồng  |  Trong chuồng: {total}/{maxCount}",
                icon = AnimalMarket.GetIcon(species),
                priceText = econ != null ? econ.FormatCurrency(price) : manager.FormatCurrency(price),
                actionLabel = "Mua",
                action = () => BuyAnimal(manager, species, name, price, maxCount)
            };
        }

        private static ShopOffer AdultOffer(InventoryManager manager, AnimalGrowth.Species species, string name, int price)
        {
            int adults = AnimalMarket.CountAnimals(species, AnimalGrowth.GrowthStage.Adult);
            var econ = EconomyManager.Instance;
            return new ShopOffer
            {
                title = name,
                detail = $"Chỉ bán con đã lớn  |  Đang có: {adults}",
                icon = AnimalMarket.GetIcon(species),
                priceText = econ != null ? econ.FormatCurrency(price) : manager.FormatCurrency(price),
                actionLabel = "Bán 1",
                action = () => SellAnimal(manager, species, name, price)
            };
        }

        // =====================================================================
        // GIAO DỊCH
        // =====================================================================

        private static int CountItem(InventoryManager manager, string itemId)
        {
            int total = 0;
            for (int i = 0; i < manager.TotalSlotCount; i++)
            {
                var slot = manager.GetSlot(i);
                if (slot != null && !slot.IsEmpty && slot.itemId == itemId) total += slot.count;
            }
            return total;
        }

        private static bool BuyItem(InventoryManager manager, string itemId, int amount)
        {
            var data = manager.GetItemData(itemId);
            if (data == null || !data.CanBuy) return false;

            int cost = data.buyPrice * amount;
            var econ = EconomyManager.Instance;
            if (econ != null && !econ.CanAfford(cost))
            {
                manager.Notify($"Không đủ {econ.CurrencyName}! Cần {econ.FormatCurrency(cost)}.");
                return false;
            }
            if (!manager.CanFitItem(itemId, amount))
            {
                manager.Notify("Túi đồ đã đầy, không thể mua thêm!");
                return false;
            }

            if (econ != null)
            {
                econ.TrySpendCurrency(cost);
            }
            else
            {
                manager.TrySpendCurrency(cost);
            }
            manager.AddItem(itemId, amount, false);
            string formattedCost = econ != null ? econ.FormatCurrency(cost) : manager.FormatCurrency(cost);
            manager.Notify($"Đã mua {amount} {data.displayName} (-{formattedCost})");
            return true;
        }

        private static bool SellItem(InventoryManager manager, string itemId, int amount)
        {
            var econ = EconomyManager.Instance;
            if (econ != null)
            {
                return econ.SellItemById(itemId, amount);
            }

            var data = manager.GetItemData(itemId);
            if (data == null || !data.CanSell) return false;

            int sold = 0;
            for (int i = 0; i < manager.TotalSlotCount && sold < amount; i++)
            {
                var slot = manager.GetSlot(i);
                if (slot == null || slot.IsEmpty || slot.itemId != itemId) continue;

                int take = Mathf.Min(slot.count, amount - sold);
                manager.RemoveItemAt(i, take);
                sold += take;
            }
            if (sold == 0) return false;

            int earned = sold * data.sellPrice;
            manager.AddCurrency(earned);
            manager.Notify($"Đã bán {sold} {data.displayName} (+{manager.FormatCurrency(earned)})");
            return true;
        }

        private static bool BuyAnimal(InventoryManager manager, AnimalGrowth.Species species, string name, int price, int maxCount)
        {
            if (AnimalMarket.CountAnimals(species) >= maxCount)
            {
                manager.Notify($"Chuồng đã đủ {maxCount} con, không thả thêm {name} được!");
                return false;
            }
            var econ = EconomyManager.Instance;
            if (econ != null && !econ.CanAfford(price))
            {
                manager.Notify($"Không đủ {econ.CurrencyName}! Cần {econ.FormatCurrency(price)}.");
                return false;
            }
            if (!AnimalMarket.BuyBaby(species))
            {
                manager.Notify($"Không tìm thấy chuồng cho {name} trên map này.");
                return false;
            }

            if (econ != null)
            {
                econ.TrySpendCurrency(price);
            }
            else
            {
                manager.TrySpendCurrency(price);
            }
            string formattedPrice = econ != null ? econ.FormatCurrency(price) : manager.FormatCurrency(price);
            manager.Notify($"Đã mua 1 {name} (-{formattedPrice}), đã thả vào chuồng!");
            return true;
        }

        private static bool SellAnimal(InventoryManager manager, AnimalGrowth.Species species, string name, int price)
        {
            if (!AnimalMarket.SellOneAdult(species))
            {
                manager.Notify($"Chưa có {name} nào đủ lớn để bán.");
                return false;
            }

            var econ = EconomyManager.Instance;
            if (econ != null)
            {
                econ.AddCurrency(price);
            }
            else
            {
                manager.AddCurrency(price);
            }
            string formattedPrice = econ != null ? econ.FormatCurrency(price) : manager.FormatCurrency(price);
            manager.Notify($"Đã bán 1 {name} (+{formattedPrice})");
            return true;
        }
    }
}
