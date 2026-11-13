using System;
using UnityEngine;
using HappyFarmer.Inventory;

namespace HappyFarmer.Economy
{
    public class EconomyManager : MonoBehaviour
    {
        private static EconomyManager _instance;

        public static EconomyManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<EconomyManager>();
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [SerializeField] private EconomyDatabaseSO economyDatabase;
        [SerializeField] private int currentBalance = -1;
        private static int carriedBalance = -1;

        public event Action<int> OnBalanceChanged;

        public int CurrentBalance => currentBalance;
        public EconomyDatabaseSO Database => economyDatabase;
        public string CurrencyName => economyDatabase != null && !string.IsNullOrEmpty(economyDatabase.currencyName)
            ? economyDatabase.currencyName
            : "Xu Vàng";
        public string CurrencySymbol => economyDatabase != null && !string.IsNullOrEmpty(economyDatabase.currencySymbol)
            ? economyDatabase.currencySymbol
            : "Xu";

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            LoadDatabase();
            InitializeBalance();
        }

        private void Start()
        {
            OnBalanceChanged?.Invoke(currentBalance);
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            carriedBalance = currentBalance;
            _instance = null;
        }

        public void SetEconomyDatabase(EconomyDatabaseSO db)
        {
            economyDatabase = db;
        }

        private void LoadDatabase()
        {
            if (economyDatabase == null)
            {
                economyDatabase = Resources.Load<EconomyDatabaseSO>("EconomyDatabase");
            }
            if (economyDatabase == null)
            {
                economyDatabase = ScriptableObject.CreateInstance<EconomyDatabaseSO>();
                economyDatabase.PopulateDefaultDataIfEmpty();
            }
        }

        private void InitializeBalance()
        {
            if (carriedBalance >= 0)
            {
                currentBalance = carriedBalance;
            }
            else if (currentBalance < 0)
            {
                currentBalance = economyDatabase != null ? economyDatabase.startingBalance : 100;
            }
        }

        // =====================================================================
        // CURRENCY TRANSACTIONS & FORMATTING
        // =====================================================================

        public string FormatCurrency(int amount)
        {
            return economyDatabase != null ? economyDatabase.FormatCurrency(amount) : $"{amount:N0} {CurrencySymbol}";
        }

        public bool CanAfford(int amount)
        {
            return amount >= 0 && currentBalance >= amount;
        }

        public void AddCurrency(int amount)
        {
            if (amount <= 0) return;
            currentBalance += amount;
            OnBalanceChanged?.Invoke(currentBalance);
        }

        public bool TrySpendCurrency(int amount)
        {
            if (amount < 0 || currentBalance < amount) return false;
            currentBalance -= amount;
            OnBalanceChanged?.Invoke(currentBalance);
            return true;
        }

        public int GetParcelPrice(string parcelName, int fallback = 150)
        {
            return economyDatabase != null ? economyDatabase.GetParcelPrice(parcelName, fallback) : fallback;
        }

        // =====================================================================
        // ITEM SELLING / INVENTORY INTEGRATION
        // =====================================================================

        public bool SellSlotItem(int slotIndex, int amount = 1)
        {
            var inv = InventoryManager.Instance;
            if (inv == null) return false;

            var slot = inv.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty || amount <= 0) return false;

            var data = inv.GetItemData(slot.itemId);
            if (data == null || !data.CanSell)
            {
                inv.Notify($"{data?.displayName ?? "Vật phẩm"} không thể bán!");
                return false;
            }

            int count = Mathf.Min(amount, slot.count);
            int earned = count * data.sellPrice;

            inv.RemoveItemAt(slotIndex, count);
            AddCurrency(earned);
            inv.Notify($"Đã bán {count} {data.displayName} (+{FormatCurrency(earned)})!");
            return true;
        }

        public bool SellAllInSlot(int slotIndex)
        {
            var inv = InventoryManager.Instance;
            if (inv == null) return false;

            var slot = inv.GetSlot(slotIndex);
            return slot != null && !slot.IsEmpty && SellSlotItem(slotIndex, slot.count);
        }

        public bool SellItemById(string itemId, int amount)
        {
            var inv = InventoryManager.Instance;
            if (inv == null) return false;

            var data = inv.GetItemData(itemId);
            if (data == null || !data.CanSell) return false;

            int sold = 0;
            for (int i = 0; i < inv.TotalSlotCount && sold < amount; i++)
            {
                var slot = inv.GetSlot(i);
                if (slot == null || slot.IsEmpty || slot.itemId != itemId) continue;

                int take = Mathf.Min(slot.count, amount - sold);
                inv.RemoveItemAt(i, take);
                sold += take;
            }
            if (sold == 0) return false;

            int earned = sold * data.sellPrice;
            AddCurrency(earned);
            inv.Notify($"Đã bán {sold} {data.displayName} (+{FormatCurrency(earned)})");
            return true;
        }
    }
}
