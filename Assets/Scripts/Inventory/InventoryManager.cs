using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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
        [SerializeField] private KeyCode interactKey = KeyCode.E;
        [SerializeField] private KeyCode dropKey = KeyCode.Q;
        [SerializeField] private float interactRadius = 1.3f;
        [SerializeField] private string blockedTilePrefix = "path_";

        [Header("Runtime State")]
        [SerializeField] private int currentBalance = -1;
        [SerializeField] private int selectedHotbarIndex;
        [SerializeField] private List<InventorySlot> slots = new List<InventorySlot>();

        private readonly Dictionary<string, InventoryItemData> itemLookup = new Dictionary<string, InventoryItemData>();
        private readonly Dictionary<string, Sprite> spriteLookup = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, ForagePropConfig> forageLookup = new Dictionary<string, ForagePropConfig>();
        private readonly Dictionary<Vector2Int, CropPlot> plantedCrops = new Dictionary<Vector2Int, CropPlot>();

        private Tilemap pathsTilemap;

        public event Action OnInventoryChanged;
        public event Action<int> OnSelectedHotbarChanged;
        public event Action<int> OnBalanceChanged;
        public event Action<string> OnNotification;

        public int HotbarSlotCount => economyDatabase != null ? Mathf.Max(1, economyDatabase.hotbarSize) : DefaultHotbarSize;
        public int BackpackSlotCount => economyDatabase != null ? Mathf.Max(1, economyDatabase.backpackSize) : DefaultBackpackSize;
        public int TotalSlotCount => HotbarSlotCount + BackpackSlotCount;
        public int SelectedHotbarIndex => selectedHotbarIndex;
        public int CurrentBalance => currentBalance;
        public EconomyDatabaseSO EconomyDatabase => economyDatabase;
        public string CurrencyName => economyDatabase != null && !string.IsNullOrEmpty(economyDatabase.currencyName)
            ? economyDatabase.currencyName
            : "Xu Vàng";

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
            InitializeBalanceAndSlots();
        }

        private void Start()
        {
            ResolveSceneReferences();
            SpawnConfiguredWorldPickups();
            OnInventoryChanged?.Invoke();
            OnSelectedHotbarChanged?.Invoke(selectedHotbarIndex);
            OnBalanceChanged?.Invoke(currentBalance);
        }

        private void Update()
        {
            if (PlayerTransform == null) ResolveSceneReferences();
            HandleHotbarInput();
            HandleWorldInput();
        }

        public void SetEconomyDatabase(EconomyDatabaseSO db) => economyDatabase = db;
        public void SetPreloadedSprites(List<Sprite> sprites) => preloadedSprites = sprites ?? new List<Sprite>();
        public void Notify(string message) => OnNotification?.Invoke(message);

        // =====================================================================
        // INITIALIZATION (DATA-DRIVEN)
        // =====================================================================

        private void ResolveSceneReferences()
        {
            if (PlayerTransform == null)
            {
                var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
                if (player != null) PlayerTransform = player.transform;
            }

            if (pathsTilemap == null)
            {
                var pathsObj = GameObject.Find("Grid/Paths") ?? GameObject.Find("Paths");
                if (pathsObj != null) pathsTilemap = pathsObj.GetComponent<Tilemap>();
            }
        }

        private void LoadSprites()
        {
            spriteLookup.Clear();

            if (preloadedSprites != null)
            {
                foreach (var s in preloadedSprites)
                {
                    if (s != null && !spriteLookup.ContainsKey(s.name))
                        spriteLookup[s.name] = s;
                }
            }

            foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (s != null && !spriteLookup.ContainsKey(s.name))
                    spriteLookup[s.name] = s;
            }

#if UNITY_EDITOR
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites" }))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Sprite sp && !spriteLookup.ContainsKey(sp.name))
                        spriteLookup[sp.name] = sp;
                }
            }
#endif
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

            forageLookup.Clear();
            if (economyDatabase.forageRules != null)
            {
                foreach (var rule in economyDatabase.forageRules)
                {
                    if (rule != null && !string.IsNullOrEmpty(rule.sceneObjectName))
                    {
                        forageLookup[rule.sceneObjectName] = rule;
                    }
                }
            }
        }

        private void InitializeBalanceAndSlots()
        {
            if (currentBalance < 0)
            {
                currentBalance = economyDatabase != null ? economyDatabase.startingBalance : 100;
            }

            if (slots == null) slots = new List<InventorySlot>(TotalSlotCount);
            while (slots.Count < TotalSlotCount)
            {
                slots.Add(new InventorySlot());
            }

            bool isAllEmpty = true;
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty) { isAllEmpty = false; break; }
            }

            if (isAllEmpty && economyDatabase != null && economyDatabase.starterSlots != null)
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
            if (economyDatabase == null || economyDatabase.initialWorldPickups == null) return;
            if (GameObject.Find("WorldPickups") != null) return;

            var root = new GameObject("WorldPickups").transform;
            foreach (var cfg in economyDatabase.initialWorldPickups)
            {
                if (cfg == null || string.IsNullOrEmpty(cfg.itemId)) continue;
                SpawnPickupObject(root, cfg.itemId, cfg.count, cfg.position, 0f);
            }
        }

        // =====================================================================
        // QUERIES & CURRENCY
        // =====================================================================

        public InventorySlot GetSlot(int index) => (index >= 0 && index < slots.Count) ? slots[index] : null;

        public InventoryItemData GetItemData(string id) =>
            (!string.IsNullOrEmpty(id) && itemLookup.TryGetValue(id, out var data)) ? data : null;

        public Sprite GetSprite(string name) =>
            (!string.IsNullOrEmpty(name) && spriteLookup.TryGetValue(name, out var sp)) ? sp : null;

        public string FormatCurrency(int amount) =>
            economyDatabase != null ? economyDatabase.FormatCurrency(amount) : $"{amount:N0} Xu";

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

        public bool AddItem(string itemId, int amount = 1, bool showNotification = true)
        {
            var data = GetItemData(itemId);
            if (data == null || amount <= 0) return false;

            int remaining = amount;

            // 1. Cộng dồn vào stack đang có
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                var s = slots[i];
                if (!s.IsEmpty && s.itemId == itemId && s.count < data.maxStack)
                {
                    int add = Mathf.Min(remaining, data.maxStack - s.count);
                    s.count += add;
                    remaining -= add;
                }
            }

            // 2. Đặt vào ô trống
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                if (slots[i].IsEmpty)
                {
                    int add = Mathf.Min(remaining, data.maxStack);
                    slots[i].Set(itemId, add);
                    remaining -= add;
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

            slot.Set(slot.itemId, slot.count - amount);
            OnInventoryChanged?.Invoke();
            return true;
        }

        public void SwapOrMergeSlots(int indexA, int indexB)
        {
            if (indexA == indexB) return;
            var slotA = GetSlot(indexA);
            var slotB = GetSlot(indexB);
            if (slotA == null || slotB == null || (slotA.IsEmpty && slotB.IsEmpty)) return;

            if (!slotA.IsEmpty && slotA.itemId == slotB.itemId)
            {
                int maxStack = GetItemData(slotB.itemId)?.maxStack ?? 99;
                int move = Mathf.Min(slotA.count, maxStack - slotB.count);
                if (move > 0)
                {
                    slotB.count += move;
                    slotA.Set(slotA.itemId, slotA.count - move);
                    OnInventoryChanged?.Invoke();
                    return;
                }
            }

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

            int rangeStart = slotIndex < HotbarSlotCount ? HotbarSlotCount : 0;
            int rangeEnd = slotIndex < HotbarSlotCount ? TotalSlotCount : HotbarSlotCount;
            int maxStack = GetItemData(source.itemId)?.maxStack ?? 99;
            int targetIdx = slotIndex;

            for (int i = rangeStart; i < rangeEnd && !source.IsEmpty; i++)
            {
                var target = slots[i];
                if (!target.IsEmpty && target.itemId == source.itemId && target.count < maxStack)
                {
                    int move = Mathf.Min(source.count, maxStack - target.count);
                    target.count += move;
                    source.Set(source.itemId, source.count - move);
                    targetIdx = i;
                }
            }

            for (int i = rangeStart; i < rangeEnd && !source.IsEmpty; i++)
            {
                if (slots[i].IsEmpty)
                {
                    slots[i].Set(source.itemId, source.count);
                    source.Clear();
                    targetIdx = i;
                    break;
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
        // ECONOMY (SELL & BUY IN INVENTORY)
        // =====================================================================

        public bool SellSlotItem(int slotIndex, int amount = 1)
        {
            var slot = GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty || amount <= 0) return false;

            var data = GetItemData(slot.itemId);
            if (data == null || !data.CanSell)
            {
                Notify($"{data?.displayName ?? "Vật phẩm"} không thể bán!");
                return false;
            }

            int count = Mathf.Min(amount, slot.count);
            int earned = count * data.sellPrice;

            RemoveItemAt(slotIndex, count);
            AddCurrency(earned);
            Notify($"Đã bán {count} {data.displayName} (+{FormatCurrency(earned)})!");
            return true;
        }

        public bool SellAllInSlot(int slotIndex)
        {
            var slot = GetSlot(slotIndex);
            return slot != null && !slot.IsEmpty && SellSlotItem(slotIndex, slot.count);
        }

        public bool BuyItem(string itemId, int amount = 1)
        {
            var data = GetItemData(itemId);
            if (data == null || !data.CanBuy || amount <= 0) return false;

            int cost = data.buyPrice * amount;
            if (currentBalance < cost)
            {
                Notify($"Không đủ {CurrencyName}! Cần {FormatCurrency(cost)}.");
                return false;
            }

            if (!CanFitItem(itemId, amount))
            {
                Notify("Túi đồ đã đầy, không thể mua thêm!");
                return false;
            }

            if (!TrySpendCurrency(cost)) return false;

            AddItem(itemId, amount, false);
            Notify($"Đã mua +{amount} {data.displayName} (-{FormatCurrency(cost)})!");
            return true;
        }

        // =====================================================================
        // ITEM USAGE & WORLD INTERACTION
        // =====================================================================

        public bool UseSlotItem(int slotIndex)
        {
            var slot = GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty)
            {
                Notify("Ô đang chọn chưa có vật phẩm.");
                return false;
            }

            var data = GetItemData(slot.itemId);
            if (data == null) return false;

            switch (data.category)
            {
                case ItemCategory.Seed:
                    if (!TryPlantSeed(data)) return false;
                    RemoveItemAt(slotIndex, 1);
                    Notify($"Đã gieo {data.displayName}! Cây đang lớn...");
                    return true;

                case ItemCategory.Produce:
                case ItemCategory.Forage:
                    RemoveItemAt(slotIndex, 1);
                    Notify($"Đã sử dụng {data.displayName}!");
                    return true;

                default:
                    return DropSlotItem(slotIndex, 1);
            }
        }

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
        }

        private void HandleWorldInput()
        {
            if (Input.GetKeyDown(interactKey))
            {
                if (!TryHarvestNearbyCrop() && !TryForageNearbySceneProp())
                {
                    UseSlotItem(selectedHotbarIndex);
                }
            }
            if (Input.GetKeyDown(dropKey))
            {
                DropSlotItem(selectedHotbarIndex, 1);
            }
        }

        private bool TryPlantSeed(InventoryItemData seedData)
        {
            ResolveSceneReferences();
            if (PlayerTransform == null) return false;

            var cell = new Vector2Int(
                Mathf.FloorToInt(PlayerTransform.position.x),
                Mathf.FloorToInt(PlayerTransform.position.y));

            if (pathsTilemap != null)
            {
                var tile = pathsTilemap.GetTile(new Vector3Int(cell.x, cell.y, 0));
                if (tile != null && !string.IsNullOrEmpty(blockedTilePrefix) && tile.name.StartsWith(blockedTilePrefix))
                {
                    Notify("Không thể gieo hạt trên đường đi! Hãy đứng trên ô đất ruộng.");
                    return false;
                }
            }

            if (plantedCrops.TryGetValue(cell, out var existing) && existing != null)
            {
                if (existing.IsMature) existing.TryHarvest(this);
                else Notify("Ô đất này đã có cây đang phát triển!");
                return false;
            }

            string key = string.IsNullOrEmpty(seedData.cropKey) ? "carrot" : seedData.cropKey;
            var stages = ResolveCropStageSprites(key);
            var wetSoil = GetSprite("soil_wet") ?? GetSprite("soil_dry");

            var plotObj = new GameObject($"Crop_{key}_{cell.x}_{cell.y}");
            plotObj.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

            var plot = plotObj.AddComponent<CropPlot>();
            plot.Initialize(seedData, wetSoil, stages);
            plantedCrops[cell] = plot;
            return true;
        }

        private Sprite[] ResolveCropStageSprites(string cropKey)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < 10; i++)
            {
                var sp = GetSprite($"stage{i}_{cropKey}");
                if (sp == null) break;
                list.Add(sp);
            }
            return list.ToArray();
        }

        private bool TryHarvestNearbyCrop()
        {
            if (PlayerTransform == null) return false;

            CropPlot closest = null;
            float bestDist = interactRadius;

            foreach (var kvp in plantedCrops)
            {
                var plot = kvp.Value;
                if (plot == null || !plot.IsMature) continue;

                float d = Vector2.Distance(PlayerTransform.position, plot.transform.position);
                if (d <= bestDist)
                {
                    bestDist = d;
                    closest = plot;
                }
            }

            return closest != null && closest.TryHarvest(this);
        }

        private bool TryForageNearbySceneProp()
        {
            if (PlayerTransform == null || forageLookup.Count == 0) return false;

            SpriteRenderer bestTarget = null;
            ForagePropConfig matchedRule = null;
            float bestDist = interactRadius;

            foreach (var sr in FindObjectsOfType<SpriteRenderer>())
            {
                if (sr == null || sr.gameObject == PlayerTransform.gameObject) continue;
                if (sr.GetComponent<WorldItemPickup>() != null) continue;

                if (forageLookup.TryGetValue(sr.gameObject.name, out var rule))
                {
                    float d = Vector2.Distance(PlayerTransform.position, sr.transform.position);
                    if (d <= bestDist)
                    {
                        bestDist = d;
                        bestTarget = sr;
                        matchedRule = rule;
                    }
                }
            }

            if (bestTarget == null || matchedRule == null) return false;

            if (AddItem(matchedRule.rewardItemId, matchedRule.amount))
            {
                Destroy(bestTarget.gameObject);
                return true;
            }
            return false;
        }
    }
}
