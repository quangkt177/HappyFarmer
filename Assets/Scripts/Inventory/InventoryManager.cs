using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyFarmer.Inventory
{
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        public const int HotbarSize = 8;
        public const int BackpackSize = 16;
        public const int TotalSlots = HotbarSize + BackpackSize;

        [Header("Database & Preloaded Sprites")]
        [SerializeField] private List<InventoryItemData> itemDatabase = new List<InventoryItemData>();
        [SerializeField] private List<Sprite> preloadedSprites = new List<Sprite>();

        [Header("Runtime Inventory Slots (0..7 Hotbar, 8..23 Backpack)")]
        [SerializeField] private List<InventorySlot> slots = new List<InventorySlot>();

        [SerializeField] private int selectedHotbarIndex;

        private readonly Dictionary<string, InventoryItemData> itemLookup = new Dictionary<string, InventoryItemData>();
        private readonly Dictionary<string, Sprite> spriteLookup = new Dictionary<string, Sprite>();
        private readonly Dictionary<Vector2Int, CropPlot> plantedCrops = new Dictionary<Vector2Int, CropPlot>();

        public event Action OnInventoryChanged;
        public event Action<int> OnSelectedHotbarChanged;
        public event Action<string> OnNotification;

        public int SelectedHotbarIndex => selectedHotbarIndex;
        public Transform PlayerTransform { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeSpritesAndDatabase();
            InitializeSlots();
        }

        private void Start()
        {
            FindPlayer();
            SpawnStarterWorldPickups();
            OnInventoryChanged?.Invoke();
            OnSelectedHotbarChanged?.Invoke(selectedHotbarIndex);
        }

        private void Update()
        {
            if (PlayerTransform == null)
            {
                FindPlayer();
            }

            HandleHotbarHotkeys();
            HandleWorldInteractionKeys();
        }

        private void FindPlayer()
        {
            var playerObj = GameObject.Find("Player");
            if (playerObj != null)
            {
                PlayerTransform = playerObj.transform;
            }
        }

        public void SetPreloadedSprites(List<Sprite> sprites)
        {
            preloadedSprites = sprites ?? new List<Sprite>();
        }

        private void InitializeSpritesAndDatabase()
        {
            spriteLookup.Clear();

            // 1. Từ danh sách được serialize sẵn trong Scene
            if (preloadedSprites != null)
            {
                foreach (var s in preloadedSprites)
                {
                    if (s != null && !spriteLookup.ContainsKey(s.name))
                        spriteLookup[s.name] = s;
                }
            }

            // 2. Từ các Sprite đang được load trong bộ nhớ
            foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (s != null && !spriteLookup.ContainsKey(s.name))
                    spriteLookup[s.name] = s;
            }

#if UNITY_EDITOR
            // 3. Trong Editor: đảm bảo luôn tìm đủ toàn bộ Sprite trong Assets/Sprites
            var guids = UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites" });
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Sprite sp && !spriteLookup.ContainsKey(sp.name))
                    {
                        spriteLookup[sp.name] = sp;
                    }
                }
            }
#endif

            if (itemDatabase == null || itemDatabase.Count == 0)
            {
                BuildDefaultDatabase();
            }

            itemLookup.Clear();
            foreach (var item in itemDatabase)
            {
                if (item == null || string.IsNullOrEmpty(item.id)) continue;
                if (item.icon == null && !string.IsNullOrEmpty(item.spriteName))
                {
                    spriteLookup.TryGetValue(item.spriteName, out item.icon);
                }
                itemLookup[item.id] = item;
            }
        }

        private void BuildDefaultDatabase()
        {
            itemDatabase = new List<InventoryItemData>
            {
                CreateItem("seed_carrot", "Hạt Cà Rốt",
                    "Hạt giống cà rốt ngọt giòn. Đứng trên đất và bấm [E] hoặc nút [Sử dụng] để gieo trồng.",
                    ItemCategory.Seed, "seed_carrot", "carrot", "produce_carrot"),
                CreateItem("seed_strawberry", "Hạt Dâu Tây",
                    "Hạt giống dâu tây đỏ mọng. Gieo xuống đất bằng phím [E] và chờ thu hoạch.",
                    ItemCategory.Seed, "seed_strawberry", "strawberry", "produce_strawberry"),
                CreateItem("seed_pumpkin", "Hạt Bí Ngô",
                    "Hạt giống bí ngô vàng ươm đặc trưng của vùng Little Dreamyland.",
                    ItemCategory.Seed, "seed_pumpkin", "pumpkin", "produce_pumpkin"),
                CreateItem("seed_cauliflower", "Hạt Súp Lơ",
                    "Hạt giống súp lơ trắng tươi mát, dễ trồng trên đất nông trại.",
                    ItemCategory.Seed, "seed_cauliflower", "cauliflower", "produce_cauliflower"),

                CreateItem("produce_carrot", "Cà Rốt Tươi",
                    "Củ cà rốt giòn ngọt vừa thu hoạch. Có thể dùng ngay để hồi sức hoặc cất trong kho.",
                    ItemCategory.Produce, "produce_carrot"),
                CreateItem("produce_strawberry", "Dâu Tây Đỏ",
                    "Trái dâu tây chín mọng thơm lừng, đặc sản được yêu thích nhất nông trại.",
                    ItemCategory.Produce, "produce_strawberry"),
                CreateItem("produce_pumpkin", "Bí Ngô Vàng",
                    "Quả bí ngô tròn đầy đặn, nông sản giá trị cao.",
                    ItemCategory.Produce, "produce_pumpkin"),
                CreateItem("produce_cauliflower", "Súp Lơ Trắng",
                    "Bông súp lơ trắng tinh khôi, xanh tươi từ thửa ruộng nhà.",
                    ItemCategory.Produce, "produce_cauliflower"),

                CreateItem("mushroom", "Nấm Rừng",
                    "Nấm mọc tự nhiên quanh bìa rừng và các bụi cây ẩm ướt.",
                    ItemCategory.Forage, "mushroom"),
                CreateItem("flowers_white", "Hoa Cúc Trắng",
                    "Bông hoa dại trắng tinh khôi hái được trên đồng cỏ.",
                    ItemCategory.Forage, "flowers_white"),
                CreateItem("flowers_yellow", "Hoa Cúc Vàng",
                    "Bông hoa cúc vàng rực rỡ mang lại niềm vui cho người nông dân.",
                    ItemCategory.Forage, "flowers_yellow"),

                CreateItem("log", "Khúc Gỗ Sồi",
                    "Gỗ tự nhiên thu thập khi phát quang nông trại, dùng làm vật liệu xây dựng.",
                    ItemCategory.Resource, "log"),
                CreateItem("rock_small", "Đá Cuội Nhỏ",
                    "Viên đá cuội nhặt từ các thửa đất hoang quanh nông trại.",
                    ItemCategory.Resource, "rock_small")
            };
        }

        private InventoryItemData CreateItem(
            string id,
            string displayName,
            string description,
            ItemCategory category,
            string spriteName,
            string cropKey = "",
            string harvestItemId = "")
        {
            spriteLookup.TryGetValue(spriteName, out var icon);
            return new InventoryItemData
            {
                id = id,
                displayName = displayName,
                description = description,
                category = category,
                spriteName = spriteName,
                icon = icon,
                maxStack = 99,
                cropKey = cropKey,
                harvestItemId = harvestItemId
            };
        }

        private void InitializeSlots()
        {
            bool needsStarterItems = slots == null || slots.Count == 0;
            if (slots == null) slots = new List<InventorySlot>(TotalSlots);

            while (slots.Count < TotalSlots)
            {
                slots.Add(new InventorySlot());
            }

            bool allEmpty = true;
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty)
                {
                    allEmpty = false;
                    break;
                }
            }

            if (needsStarterItems || allEmpty)
            {
                PopulateStarterInventory();
            }
        }

        public void PopulateStarterInventory()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].Clear();
            }

            // Vật phẩm khởi đầu trên thanh Hotbar (0..7)
            slots[0].Set("seed_carrot", 6);
            slots[1].Set("seed_strawberry", 5);
            slots[2].Set("seed_pumpkin", 4);
            slots[3].Set("seed_cauliflower", 4);
            slots[4].Set("produce_carrot", 3);
            slots[5].Set("mushroom", 2);

            // Vật phẩm khởi đầu trong Túi đồ Backpack (8..23)
            slots[8].Set("produce_strawberry", 4);
            slots[9].Set("produce_pumpkin", 2);
            slots[10].Set("log", 8);
            slots[11].Set("rock_small", 6);
            slots[12].Set("flowers_white", 3);
            slots[13].Set("flowers_yellow", 2);
        }

        private void SpawnStarterWorldPickups()
        {
            // Tạo một vài vật phẩm có sẵn trên đường đi quanh nông trại để người chơi đi dạo nhặt thử
            var pickupsRoot = GameObject.Find("WorldPickups");
            if (pickupsRoot != null) return;

            pickupsRoot = new GameObject("WorldPickups");
            CreatePickup(pickupsRoot.transform, "mushroom", 1, new Vector3(8.5f, 6.4f, 0f), 0f);
            CreatePickup(pickupsRoot.transform, "flowers_yellow", 1, new Vector3(12.2f, 7.2f, 0f), 0f);
            CreatePickup(pickupsRoot.transform, "seed_carrot", 2, new Vector3(9.6f, 8.5f, 0f), 0f);
            CreatePickup(pickupsRoot.transform, "log", 2, new Vector3(6.2f, 5.2f, 0f), 0f);
            CreatePickup(pickupsRoot.transform, "produce_strawberry", 1, new Vector3(23.5f, 10.2f, 0f), 0f);
        }

        private void HandleHotbarHotkeys()
        {
            // Phím số 1..8 chọn ô Hotbar
            for (int i = 0; i < HotbarSize; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    SelectHotbarSlot(i);
                }
            }

            // Con lăn chuột chuyển đổi ô Hotbar
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                int dir = scroll > 0f ? -1 : 1;
                int next = (selectedHotbarIndex + dir + HotbarSize) % HotbarSize;
                SelectHotbarSlot(next);
            }
        }

        private void HandleWorldInteractionKeys()
        {
            // Phím [E] để Thu hoạch cây chín / Nhặt vật phẩm / Sử dụng vật phẩm đang cầm
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (!TryHarvestNearbyCrop() && !TryForageNearbySceneProp())
                {
                    UseSlotItem(selectedHotbarIndex);
                }
            }

            // Phím [Q] để thả 1 vật phẩm từ ô đang chọn trên Hotbar
            if (Input.GetKeyDown(KeyCode.Q))
            {
                DropSlotItem(selectedHotbarIndex, 1);
            }
        }

        public void SelectHotbarSlot(int index)
        {
            index = Mathf.Clamp(index, 0, HotbarSize - 1);
            if (selectedHotbarIndex == index)
            {
                OnSelectedHotbarChanged?.Invoke(selectedHotbarIndex);
                return;
            }

            selectedHotbarIndex = index;
            OnSelectedHotbarChanged?.Invoke(selectedHotbarIndex);
        }

        public InventorySlot GetSlot(int index)
        {
            if (index < 0 || index >= slots.Count) return null;
            return slots[index];
        }

        public InventoryItemData GetItemData(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            itemLookup.TryGetValue(id, out var data);
            return data;
        }

        public Sprite GetSprite(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return null;
            spriteLookup.TryGetValue(spriteName, out var sp);
            return sp;
        }

        public bool AddItem(string itemId, int amount = 1, bool showNotification = true)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return false;
            var data = GetItemData(itemId);
            if (data == null) return false;

            int remaining = amount;

            // 1. Cộng dồn vào các ô đã có sẵn cùng loại vật phẩm
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                var slot = slots[i];
                if (!slot.IsEmpty && slot.itemId == itemId && slot.count < data.maxStack)
                {
                    int canAdd = Mathf.Min(remaining, data.maxStack - slot.count);
                    slot.count += canAdd;
                    remaining -= canAdd;
                }
            }

            // 2. Điền vào các ô còn trống
            for (int i = 0; i < slots.Count && remaining > 0; i++)
            {
                var slot = slots[i];
                if (slot.IsEmpty)
                {
                    int canAdd = Mathf.Min(remaining, data.maxStack);
                    slot.Set(itemId, canAdd);
                    remaining -= canAdd;
                }
            }

            int added = amount - remaining;
            if (added > 0)
            {
                OnInventoryChanged?.Invoke();
                if (showNotification)
                {
                    Notify($"Đã nhận +{added} {data.displayName}");
                }
                return true;
            }

            if (showNotification)
            {
                Notify("Túi đồ đã đầy!");
            }
            return false;
        }

        public bool RemoveItemAt(int slotIndex, int amount = 1)
        {
            var slot = GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty || amount <= 0) return false;

            slot.count -= amount;
            if (slot.count <= 0)
            {
                slot.Clear();
            }

            OnInventoryChanged?.Invoke();
            return true;
        }

        public void SwapOrMergeSlots(int indexA, int indexB)
        {
            if (indexA == indexB) return;
            var slotA = GetSlot(indexA);
            var slotB = GetSlot(indexB);
            if (slotA == null || slotB == null) return;

            if (slotA.IsEmpty && slotB.IsEmpty) return;

            // Nếu cùng loại vật phẩm -> gộp stack
            if (!slotA.IsEmpty && !slotB.IsEmpty && slotA.itemId == slotB.itemId)
            {
                var data = GetItemData(slotB.itemId);
                int maxStack = data != null ? data.maxStack : 99;
                if (slotB.count < maxStack)
                {
                    int transfer = Mathf.Min(slotA.count, maxStack - slotB.count);
                    slotB.count += transfer;
                    slotA.count -= transfer;
                    if (slotA.count <= 0) slotA.Clear();
                    OnInventoryChanged?.Invoke();
                    return;
                }
            }

            // Hoán đổi 2 ô
            string tempId = slotA.itemId;
            int tempCount = slotA.count;

            slotA.Set(slotB.itemId, slotB.count);
            slotB.Set(tempId, tempCount);

            OnInventoryChanged?.Invoke();
        }

        public void QuickMoveSlot(int slotIndex)
        {
            var source = GetSlot(slotIndex);
            if (source == null || source.IsEmpty) return;

            int rangeStart = slotIndex < HotbarSize ? HotbarSize : 0;
            int rangeEnd = slotIndex < HotbarSize ? TotalSlots : HotbarSize;

            var data = GetItemData(source.itemId);
            int maxStack = data != null ? data.maxStack : 99;

            // 1. Ưu tiên gộp vào ô cùng loại trong vùng đích
            for (int i = rangeStart; i < rangeEnd && !source.IsEmpty; i++)
            {
                var target = slots[i];
                if (!target.IsEmpty && target.itemId == source.itemId && target.count < maxStack)
                {
                    int move = Mathf.Min(source.count, maxStack - target.count);
                    target.count += move;
                    source.count -= move;
                    if (source.count <= 0) source.Clear();
                }
            }

            // 2. Chuyển vào ô trống đầu tiên trong vùng đích
            for (int i = rangeStart; i < rangeEnd && !source.IsEmpty; i++)
            {
                var target = slots[i];
                if (target.IsEmpty)
                {
                    target.Set(source.itemId, source.count);
                    source.Clear();
                    break;
                }
            }

            OnInventoryChanged?.Invoke();
        }

        public void SortBackpack()
        {
            // Gom các stack cùng loại trong Backpack (8..23) trước khi sắp xếp
            var merged = new Dictionary<string, int>();
            for (int i = HotbarSize; i < TotalSlots; i++)
            {
                var s = slots[i];
                if (!s.IsEmpty)
                {
                    if (!merged.ContainsKey(s.itemId)) merged[s.itemId] = 0;
                    merged[s.itemId] += s.count;
                    s.Clear();
                }
            }

            var sortedEntries = new List<InventorySlot>();
            foreach (var kvp in merged)
            {
                var data = GetItemData(kvp.Key);
                int maxStack = data != null ? data.maxStack : 99;
                int remaining = kvp.Value;
                while (remaining > 0)
                {
                    int take = Mathf.Min(remaining, maxStack);
                    sortedEntries.Add(new InventorySlot { itemId = kvp.Key, count = take });
                    remaining -= take;
                }
            }

            sortedEntries.Sort((a, b) =>
            {
                var da = GetItemData(a.itemId);
                var db = GetItemData(b.itemId);
                if (da == null || db == null) return string.CompareOrdinal(a.itemId, b.itemId);
                int catCmp = da.category.CompareTo(db.category);
                if (catCmp != 0) return catCmp;
                return string.Compare(da.displayName, db.displayName, StringComparison.CurrentCulture);
            });

            for (int i = 0; i < sortedEntries.Count && (HotbarSize + i) < TotalSlots; i++)
            {
                slots[HotbarSize + i].Set(sortedEntries[i].itemId, sortedEntries[i].count);
            }

            OnInventoryChanged?.Invoke();
            Notify("Đã sắp xếp túi đồ gọn gàng!");
        }

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
                    if (TryPlantSeed(data))
                    {
                        RemoveItemAt(slotIndex, 1);
                        Notify($"Đã gieo {data.displayName}! Cây đang lớn...");
                        return true;
                    }
                    return false;

                case ItemCategory.Produce:
                case ItemCategory.Forage:
                    RemoveItemAt(slotIndex, 1);
                    Notify($"Đã sử dụng {data.displayName}! (+Năng lượng nông trại)");
                    return true;

                case ItemCategory.Resource:
                    DropSlotItem(slotIndex, 1);
                    return true;
            }

            return false;
        }

        public bool DropSlotItem(int slotIndex, int amount = 1)
        {
            var slot = GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty) return false;

            var data = GetItemData(slot.itemId);
            if (data == null) return false;

            int dropCount = Mathf.Min(amount, slot.count);
            Vector3 dropPos = PlayerTransform != null
                ? PlayerTransform.position + new Vector3(UnityEngine.Random.Range(-0.55f, 0.55f), -0.35f, 0f)
                : Vector3.zero;

            var pickupsRoot = GameObject.Find("WorldPickups");
            if (pickupsRoot == null) pickupsRoot = new GameObject("WorldPickups");

            CreatePickup(pickupsRoot.transform, slot.itemId, dropCount, dropPos, 1.1f);
            RemoveItemAt(slotIndex, dropCount);
            Notify($"Đã thả {dropCount} {data.displayName} xuống đất.");
            return true;
        }

        private void CreatePickup(Transform parent, string itemId, int count, Vector3 position, float cooldown)
        {
            var data = GetItemData(itemId);
            if (data == null) return;

            var go = new GameObject($"Pickup_{itemId}");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = position;

            var pickup = go.AddComponent<WorldItemPickup>();
            pickup.Initialize(itemId, count, data.icon, cooldown);
        }

        private bool TryPlantSeed(InventoryItemData seedData)
        {
            if (PlayerTransform == null)
            {
                FindPlayer();
                if (PlayerTransform == null) return false;
            }

            Vector2Int cell = new Vector2Int(
                Mathf.FloorToInt(PlayerTransform.position.x),
                Mathf.FloorToInt(PlayerTransform.position.y)
            );

            if (plantedCrops.TryGetValue(cell, out var existingPlot) && existingPlot != null)
            {
                if (existingPlot.IsMature)
                {
                    existingPlot.TryHarvest(this);
                }
                else
                {
                    Notify("Ô đất này đã có cây đang phát triển!");
                }
                return false;
            }

            string key = string.IsNullOrEmpty(seedData.cropKey) ? "carrot" : seedData.cropKey;
            var stages = new Sprite[5];
            for (int i = 0; i < 5; i++)
            {
                stages[i] = GetSprite($"stage{i}_{key}");
            }

            var wetSoil = GetSprite("soil_wet") ?? GetSprite("soil_dry");
            var plotObj = new GameObject($"Crop_{key}_{cell.x}_{cell.y}");
            plotObj.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);

            var plot = plotObj.AddComponent<CropPlot>();
            plot.Initialize(key, seedData.harvestItemId, wetSoil, stages);
            plantedCrops[cell] = plot;
            return true;
        }

        private bool TryHarvestNearbyCrop()
        {
            if (PlayerTransform == null) return false;

            CropPlot closest = null;
            float bestDist = 1.35f;

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

            if (closest != null)
            {
                return closest.TryHarvest(this);
            }

            return false;
        }

        private bool TryForageNearbySceneProp()
        {
            if (PlayerTransform == null) return false;

            // Cho phép hái nấm / nhặt đá nhỏ / hoa trên bản đồ khi đứng gần và bấm [E]
            string[] forageableNames = { "mushroom", "rock_small", "flowers_white", "flowers_yellow" };
            var allRenderers = FindObjectsOfType<SpriteRenderer>();

            SpriteRenderer bestTarget = null;
            string matchedItemId = null;
            float bestDist = 1.25f;

            foreach (var sr in allRenderers)
            {
                if (sr == null || sr.gameObject == PlayerTransform.gameObject) continue;
                if (sr.GetComponent<WorldItemPickup>() != null) continue;

                string objName = sr.gameObject.name;
                for (int i = 0; i < forageableNames.Length; i++)
                {
                    if (objName == forageableNames[i])
                    {
                        float d = Vector2.Distance(PlayerTransform.position, sr.transform.position);
                        if (d <= bestDist)
                        {
                            bestDist = d;
                            bestTarget = sr;
                            matchedItemId = forageableNames[i];
                        }
                        break;
                    }
                }
            }

            if (bestTarget != null && !string.IsNullOrEmpty(matchedItemId))
            {
                if (AddItem(matchedItemId, 1))
                {
                    Destroy(bestTarget.gameObject);
                    return true;
                }
            }

            return false;
        }

        public void Notify(string message)
        {
            OnNotification?.Invoke(message);
        }
    }
}
