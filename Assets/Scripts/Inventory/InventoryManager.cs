using System;
using System.Collections.Generic;
using UnityEngine;
using HappyFarmer.Economy;

namespace HappyFarmer.Inventory
{
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        public const int DefaultHotbarSize = 8;
        public const int DefaultBackpackSize = 16;

        public static int HotbarSize => Instance != null ? Instance.HotbarSlotCount : DefaultHotbarSize;
        public static int BackpackSize => Instance != null ? Instance.BackpackSlotCount : DefaultBackpackSize;
        public static int TotalSlots => Instance != null ? Instance.TotalSlotCount : (DefaultHotbarSize + DefaultBackpackSize);

        [Header("Data Source (ScriptableObject)")]
        [SerializeField] private EconomyDatabaseSO economyDatabase;
        [SerializeField] private List<Sprite> preloadedSprites = new List<Sprite>();

        [Header("Controls & Interaction Settings")]
        [SerializeField] private KeyCode dropKey = KeyCode.Q;

        [Header("Runtime State")]
        [SerializeField] private int selectedHotbarIndex;
        [SerializeField] private List<InventorySlot> slots = new List<InventorySlot>();

        private readonly Dictionary<string, InventoryItemData> itemLookup = new Dictionary<string, InventoryItemData>();
        private readonly Dictionary<string, Sprite> spriteLookup = new Dictionary<string, Sprite>();

        // Giữ lại dữ liệu túi đồ khi chuyển scene
        private static List<InventorySlot> carriedSlots;

        public event Action OnInventoryChanged;
        public event Action<int> OnSelectedHotbarChanged;
        public event Action<int> OnBalanceChanged;
        public event Action<string> OnNotification;

        public int HotbarSlotCount => economyDatabase != null ? Mathf.Max(1, economyDatabase.hotbarSize) : DefaultHotbarSize;
        public int BackpackSlotCount => economyDatabase != null ? Mathf.Max(1, economyDatabase.backpackSize) : DefaultBackpackSize;
        public int TotalSlotCount => HotbarSlotCount + BackpackSlotCount;
        public int SelectedHotbarIndex => selectedHotbarIndex;
        public InventorySlot ActiveSlot => GetSlot(selectedHotbarIndex);
        public InventoryItemData ActiveItemData => GetItemData(ActiveSlot?.itemId);

        public int CurrentBalance => EconomyManager.Instance != null ? EconomyManager.Instance.CurrentBalance : 0;
        public EconomyDatabaseSO EconomyDatabase => economyDatabase;
        public string CurrencyName => EconomyManager.Instance != null ? EconomyManager.Instance.CurrencyName : "Xu Vàng";
        public Transform PlayerTransform { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            LoadSprites();
            LoadDatabase();
            InitializeSlots();

            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.OnBalanceChanged += HandleBalanceChanged;
            }
        }

        private void HandleBalanceChanged(int balance) => OnBalanceChanged?.Invoke(balance);

        private void Start()
        {
            ResolveSceneReferences();
            SpawnConfiguredWorldPickups();
            OnInventoryChanged?.Invoke();
            OnSelectedHotbarChanged?.Invoke(selectedHotbarIndex);
            OnBalanceChanged?.Invoke(CurrentBalance);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.OnBalanceChanged -= HandleBalanceChanged;
            }
            carriedSlots = slots;
            Instance = null;
        }

        private void Update()
        {
            if (PlayerTransform == null) ResolveSceneReferences();
            HandleHotbarInput();
        }

        public void SetEconomyDatabase(EconomyDatabaseSO db)
        {
            economyDatabase = db;
            EconomyManager.Instance?.SetEconomyDatabase(db);
        }

        public void SetPreloadedSprites(List<Sprite> sprites) => preloadedSprites = sprites ?? new List<Sprite>();
        public void Notify(string message) => OnNotification?.Invoke(message);

        // =====================================================================
        // INITIALIZATION & DATA LOADING
        // =====================================================================

        private void ResolveSceneReferences()
        {
            if (PlayerTransform != null) return;
            var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
            if (player != null) PlayerTransform = player.transform;
        }

        private void LoadSprites()
        {
            spriteLookup.Clear();
            void AddSprite(Sprite s)
            {
                if (s != null && !spriteLookup.ContainsKey(s.name))
                    spriteLookup[s.name] = s;
            }

            if (preloadedSprites != null) preloadedSprites.ForEach(AddSprite);
            foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>()) AddSprite(s);

#if UNITY_EDITOR
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites" }))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Sprite sp) AddSprite(sp);
                }
            }
#endif
        }

        private void LoadDatabase()
        {
            if (economyDatabase == null)
            {
                economyDatabase = Resources.Load<EconomyDatabaseSO>("EconomyDatabase") 
                    ?? ScriptableObject.CreateInstance<EconomyDatabaseSO>();
            }

            economyDatabase.PopulateDefaultDataIfEmpty(GetSprite);

            itemLookup.Clear();
            foreach (var item in economyDatabase.items)
            {
                if (item == null || string.IsNullOrEmpty(item.id)) continue;
                if (item.icon == null && !string.IsNullOrEmpty(item.spriteName))
                {
                    item.icon = GetSprite(item.spriteName);
                }
                itemLookup[item.id] = item;
            }

            EnsureVipProduceItems();
        }

        /// <summary>
        /// Tự động sinh dữ liệu vật phẩm nông sản VIP (Bạc, Vàng, Bạch Kim) nếu chưa có trong database
        /// </summary>
        private void EnsureVipProduceItems()
        {
            var vipTiers = new[]
            {
                new { Suffix = "silver", Name = "Bạc", Multiplier = 1.5f },
                new { Suffix = "gold", Name = "Vàng", Multiplier = 2.0f },
                new { Suffix = "platinum", Name = "Bạch Kim", Multiplier = 3.0f }
            };

            var baseProduceList = new List<InventoryItemData>();
            foreach (var item in itemLookup.Values)
            {
                if (item != null && item.category == ItemCategory.Produce &&
                    !item.id.EndsWith("_silver") && !item.id.EndsWith("_gold") && !item.id.EndsWith("_platinum"))
                {
                    baseProduceList.Add(item);
                }
            }

            foreach (var baseItem in baseProduceList)
            {
                foreach (var tier in vipTiers)
                {
                    string vipId = $"{baseItem.id}_{tier.Suffix}";
                    if (!itemLookup.ContainsKey(vipId))
                    {
                        string spriteName = $"{baseItem.spriteName}_{tier.Suffix}";
                        Sprite sp = GetSprite(spriteName);

                        var vipItem = new InventoryItemData
                        {
                            id = vipId,
                            displayName = $"{baseItem.displayName} ({tier.Name})",
                            description = $"Nông sản phẩm cấp {tier.Name} hảo hạng. Chất lượng vượt trội, bán được giá cao ({tier.Multiplier}x).",
                            category = ItemCategory.Produce,
                            spriteName = spriteName,
                            icon = sp,
                            canBeSold = true,
                            sellPrice = Mathf.RoundToInt(baseItem.sellPrice * tier.Multiplier),
                            canBeBought = false,
                            buyPrice = 0,
                            maxStack = baseItem.maxStack
                        };

                        itemLookup[vipId] = vipItem;

                        if (economyDatabase != null && economyDatabase.items != null &&
                            !economyDatabase.items.Exists(i => i != null && i.id == vipId))
                        {
                            economyDatabase.items.Add(vipItem);
                        }
                    }
                }
            }
        }

        private void InitializeSlots()
        {
            if (carriedSlots != null) slots = carriedSlots;
            if (slots == null) slots = new List<InventorySlot>(TotalSlotCount);

            while (slots.Count < TotalSlotCount) slots.Add(new InventorySlot());

            if (slots.TrueForAll(s => s.IsEmpty) && carriedSlots == null && economyDatabase?.starterSlots != null)
            {
                foreach (var starter in economyDatabase.starterSlots)
                {
                    if (starter != null && starter.slotIndex >= 0 && starter.slotIndex < slots.Count)
                    {
                        slots[starter.slotIndex].Set(starter.itemId, starter.count);
                    }
                }
            }
        }

        private void SpawnConfiguredWorldPickups()
        {
            if (economyDatabase?.initialWorldPickups == null || GameObject.Find("WorldPickups") != null) return;

            var root = new GameObject("WorldPickups").transform;
            foreach (var cfg in economyDatabase.initialWorldPickups)
            {
                if (cfg != null && !string.IsNullOrEmpty(cfg.itemId))
                {
                    SpawnPickupObject(root, cfg.itemId, cfg.count, cfg.position, 0f);
                }
            }
        }

        // =====================================================================
        // QUERIES & CURRENCY FORWARDING
        // =====================================================================

        public InventorySlot GetSlot(int index) => (index >= 0 && index < slots.Count) ? slots[index] : null;

        public InventoryItemData GetItemData(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (itemLookup.TryGetValue(id, out var data)) return data;

            // Nếu là vật phẩm VIP chưa có trong itemLookup, tự động sinh và nạp
            if (id.EndsWith("_silver") || id.EndsWith("_gold") || id.EndsWith("_platinum"))
            {
                EnsureVipProduceItems();
                if (itemLookup.TryGetValue(id, out data)) return data;
            }

            return null;
        }

        public Sprite GetSprite(string name) =>
            (!string.IsNullOrEmpty(name) && spriteLookup.TryGetValue(name, out var sp)) ? sp : null;

        public string FormatCurrency(int amount) =>
            EconomyManager.Instance != null ? EconomyManager.Instance.FormatCurrency(amount) : $"{amount:N0} Xu";

        public void AddCurrency(int amount) => EconomyManager.Instance?.AddCurrency(amount);

        public bool TrySpendCurrency(int amount) =>
            EconomyManager.Instance != null && EconomyManager.Instance.TrySpendCurrency(amount);

        public void SelectHotbarSlot(int index)
        {
            selectedHotbarIndex = Mathf.Clamp(index, 0, HotbarSlotCount - 1);
            OnSelectedHotbarChanged?.Invoke(selectedHotbarIndex);
        }

        // =====================================================================
        // INVENTORY CORE OPERATIONS
        // =====================================================================

        public bool CanFitItem(string itemId, int amount)
        {
            var data = GetItemData(itemId);
            if (data == null || amount <= 0) return false;

            int available = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s.IsEmpty) available += data.maxStack;
                else if (s.itemId == itemId && s.count < data.maxStack) available += (data.maxStack - s.count);
                if (available >= amount) return true;
            }
            return false;
        }

        public int CountItem(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            int total = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].itemId == itemId) total += slots[i].count;
            }
            return total;
        }

        public bool HasItem(string itemId, int amount = 1) => CountItem(itemId) >= amount;

        public bool AddItem(string itemId, int amount = 1, bool showNotification = true)
        {
            var data = GetItemData(itemId);
            if (data == null || amount <= 0) return false;

            int remaining = amount;

            // 1. Cộng dồn vào các stack đã có
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                if (!slots[i].IsEmpty && slots[i].itemId == itemId)
                    remaining -= slots[i].Add(remaining, data.maxStack);
            }

            // 2. Đặt phần còn lại vào ô trống
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                if (slots[i].IsEmpty)
                {
                    slots[i].itemId = itemId;
                    remaining -= slots[i].Add(remaining, data.maxStack);
                }
            }

            int added = amount - remaining;
            if (added <= 0)
            {
                if (showNotification) Notify("Túi đồ đã đầy!");
                return false;
            }

            OnInventoryChanged?.Invoke();
            if (showNotification) Notify($"Đã nhận +{added} {data.displayName}");
            return true;
        }

        public bool RemoveItemAt(int slotIndex, int amount = 1)
        {
            var slot = GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty || amount <= 0) return false;

            slot.Remove(amount);
            OnInventoryChanged?.Invoke();
            return true;
        }

        public int RemoveItem(string itemId, int amount = 1)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return 0;

            int remaining = amount;
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                if (slots[i].itemId == itemId)
                    remaining -= slots[i].Remove(remaining);
            }

            int removed = amount - remaining;
            if (removed > 0) OnInventoryChanged?.Invoke();
            return removed;
        }

        public void SwapOrMergeSlots(int indexA, int indexB)
        {
            if (indexA == indexB) return;
            var slotA = GetSlot(indexA);
            var slotB = GetSlot(indexB);
            if (slotA == null || slotB == null || (slotA.IsEmpty && slotB.IsEmpty)) return;

            // Nếu cùng item thì gộp sang B
            if (!slotA.IsEmpty && slotA.itemId == slotB.itemId)
            {
                int maxStack = GetItemData(slotB.itemId)?.maxStack ?? 99;
                int moved = slotB.Add(slotA.count, maxStack);
                if (moved > 0)
                {
                    slotA.Remove(moved);
                    OnInventoryChanged?.Invoke();
                    return;
                }
            }

            // Hoán đổi vị trí
            string tempId = slotA.itemId;
            int tempCount = slotA.count;
            slotA.Set(slotB.itemId, slotB.count);
            slotB.Set(tempId, tempCount);
            OnInventoryChanged?.Invoke();
        }

        public int QuickMoveSlot(int slotIndex)
        {
            var source = GetSlot(slotIndex);
            if (source == null || source.IsEmpty) return slotIndex;

            int start = slotIndex < HotbarSlotCount ? HotbarSlotCount : 0;
            int end = slotIndex < HotbarSlotCount ? TotalSlotCount : HotbarSlotCount;
            int maxStack = GetItemData(source.itemId)?.maxStack ?? 99;
            int targetIdx = slotIndex;

            // 1. Gộp vào slot cùng loại ở vùng đích
            for (int i = start; i < end && !source.IsEmpty; i++)
            {
                if (!slots[i].IsEmpty && slots[i].itemId == source.itemId)
                {
                    int moved = slots[i].Add(source.count, maxStack);
                    if (moved > 0)
                    {
                        source.Remove(moved);
                        targetIdx = i;
                    }
                }
            }

            // 2. Điền phần còn lại vào ô trống đầu tiên ở vùng đích
            if (!source.IsEmpty)
            {
                for (int i = start; i < end; i++)
                {
                    if (slots[i].IsEmpty)
                    {
                        slots[i].Set(source.itemId, source.count);
                        source.Clear();
                        targetIdx = i;
                        break;
                    }
                }
            }

            OnInventoryChanged?.Invoke();
            return targetIdx;
        }

        public void SortBackpack()
        {
            var totals = new Dictionary<string, int>();
            for (int i = HotbarSlotCount; i < TotalSlotCount; i++)
            {
                var s = slots[i];
                if (s.IsEmpty) continue;
                totals[s.itemId] = (totals.TryGetValue(s.itemId, out int cur) ? cur : 0) + s.count;
                s.Clear();
            }

            var sorted = new List<InventorySlot>();
            foreach (var kvp in totals)
            {
                int maxStack = GetItemData(kvp.Key)?.maxStack ?? 99;
                int rem = kvp.Value;
                while (rem > 0)
                {
                    int take = Mathf.Min(rem, maxStack);
                    sorted.Add(new InventorySlot { itemId = kvp.Key, count = take });
                    rem -= take;
                }
            }

            sorted.Sort((a, b) =>
            {
                var da = GetItemData(a.itemId);
                var db = GetItemData(b.itemId);
                if (da == null || db == null) return string.CompareOrdinal(a.itemId, b.itemId);
                int c = da.category.CompareTo(db.category);
                return c != 0 ? c : string.Compare(da.displayName, db.displayName, StringComparison.CurrentCulture);
            });

            for (int i = 0; i < sorted.Count && (HotbarSlotCount + i) < TotalSlotCount; i++)
            {
                slots[HotbarSlotCount + i].Set(sorted[i].itemId, sorted[i].count);
            }

            OnInventoryChanged?.Invoke();
            Notify("Đã sắp xếp túi đồ gọn gàng!");
        }

        // =====================================================================
        // ECONOMY (FORWARD TO ECONOMY MANAGER)
        // =====================================================================

        public bool SellSlotItem(int slotIndex, int amount = 1) =>
            EconomyManager.Instance != null && EconomyManager.Instance.SellSlotItem(slotIndex, amount);

        public bool SellAllInSlot(int slotIndex) =>
            EconomyManager.Instance != null && EconomyManager.Instance.SellAllInSlot(slotIndex);

        // =====================================================================
        // ITEM USAGE & WORLD INTERACTION
        // =====================================================================

        public bool DropSlotItem(int slotIndex, int amount = 1)
        {
            var slot = GetSlot(slotIndex);
            var data = slot != null ? GetItemData(slot.itemId) : null;
            if (data == null || slot.IsEmpty) return false;

            int count = Mathf.Min(amount, slot.count);
            Vector3 dropPos = PlayerTransform != null
                ? PlayerTransform.position + new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), -0.35f, 0f)
                : Vector3.zero;

            var root = GameObject.Find("WorldPickups") ?? new GameObject("WorldPickups");
            SpawnPickupObject(root.transform, slot.itemId, count, dropPos, 1.1f);
            RemoveItemAt(slotIndex, count);
            Notify($"Đã thả {count} {data.displayName} xuống đất.");
            return true;
        }

        private void SpawnPickupObject(Transform parent, string itemId, int count, Vector3 pos, float cooldown)
        {
            var data = GetItemData(itemId);
            if (data == null) return;

            var go = new GameObject($"Pickup_{itemId}");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.AddComponent<WorldItemPickup>().Initialize(itemId, count, data.icon, cooldown);
        }

        private void HandleHotbarInput()
        {
            int count = Mathf.Min(HotbarSlotCount, 9);
            for (int i = 0; i < count; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    SelectHotbarSlot(i);
                }
            }

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                int dir = scroll > 0f ? -1 : 1;
                SelectHotbarSlot((selectedHotbarIndex + dir + HotbarSlotCount) % HotbarSlotCount);
            }

            if (Input.GetKeyDown(dropKey))
            {
                DropSlotItem(selectedHotbarIndex, 1);
            }
        }
    }
}
