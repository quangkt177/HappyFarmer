using System;
using UnityEngine;
using HappyFarmer.Inventory;

namespace HappyFarmer.Gameplay
{
    /// <summary>
    /// Đại diện cho một cây trồng tại ô đất.
    /// Quản lý các giai đoạn phát triển (Stage 0 -> Stage 4).
    /// Chỉ lớn lên khi đất được tưới ướt; chuyển giai đoạn xong thì đất khô lại (cần tưới tiếp).
    /// </summary>
    public class CropPlant : MonoBehaviour
    {
        [Header("Crop Info")]
        [SerializeField] private string cropKey = "carrot";
        [SerializeField] private string cropDisplayName = "Cà Rốt";
        [SerializeField] private string harvestItemId = "produce_carrot";
        [Tooltip("Thời gian (giây) cần để cây hoàn thành một giai đoạn khi đất được tưới ướt (đã chỉnh chậm xuống)")]
        [SerializeField] private float secondsPerStage = 12f;
        [SerializeField] private int minYield = 1;
        [SerializeField] private int maxYield = 3;

        public enum CropQuality
        {
            Spoiled,
            Normal,
            Silver,
            Gold,
            Platinum
        }

        [Header("Harvest Quality & Spoil Rates (%)")]
        [Tooltip("Tỉ lệ nông sản bị hỏng khi thu hoạch (0-100%). Khi bị hỏng sẽ hiện thông báo lên màn hình và không nhận được nông sản.")]
        [Range(0f, 100f)] [SerializeField] private float spoiledChance = 10f;

        [Tooltip("Tỉ lệ nhận được nông sản Platinum (giá x3)")]
        [Range(0f, 100f)] [SerializeField] private float platinumChance = 5f;

        [Tooltip("Tỉ lệ nhận được nông sản Vàng (giá x2)")]
        [Range(0f, 100f)] [SerializeField] private float goldChance = 15f;

        [Tooltip("Tỉ lệ nhận được nông sản Bạc (giá x1.5)")]
        [Range(0f, 100f)] [SerializeField] private float silverChance = 25f;

        [Header("Growth State")]
        [SerializeField] private int currentStage = 0;
        [SerializeField] private bool isMature = false;
        private float stageTimer = 0f;
        private const int MaxStage = 4;

        [Header("Wilt State")]
        [Tooltip("Cây có đang bị héo hay không")]
        [SerializeField] private bool isWilted = false;
        [Tooltip("Thời gian (giây) đất bị khô ở các giai đoạn 1-3 trước khi cây bị héo")]
        [SerializeField] private float dryWiltDuration = 20f;
        [Tooltip("Thời gian (giây) cây đã chín ở giai đoạn 4 mà không thu hoạch kịp trước khi bị héo")]
        [SerializeField] private float matureWiltDuration = 25f;

        private float dryTimer = 0f;
        private float matureTimer = 0f;

        [Header("Watering & Soil State")]
        [Tooltip("Trạng thái đất đã được tưới nước (ướt). Cây chỉ lớn khi đất đang ướt.")]
        [SerializeField] private bool isSoilWet = false;

        [Header("Interaction")]
        [SerializeField] private float harvestRadius = 1.3f;
        [SerializeField] private KeyCode harvestKey = KeyCode.E;

        private SpriteRenderer spriteRenderer;
        private SpriteRenderer soilRenderer;
        private Sprite soilWetSprite;

        private Action<CropPlant> onHarvestedCallback;
        private Transform playerTransform;

        public bool IsMature => isMature;
        public bool IsWilted => isWilted;
        public bool IsSoilWet => isSoilWet;
        public string CropDisplayName => cropDisplayName;
        public int CurrentStage => currentStage;
        public string CropKey => cropKey;
        public float SecondsPerStage => secondsPerStage;
        public float DryTimer => dryTimer;
        public float DryWiltDuration => dryWiltDuration;
        public float MatureTimer => matureTimer;
        public float MatureWiltDuration => matureWiltDuration;

        public void Initialize(InventoryItemData seedData, Action<CropPlant> onHarvested)
        {
            if (seedData != null)
            {
                cropKey = !string.IsNullOrEmpty(seedData.cropKey) ? seedData.cropKey : "carrot";
                cropDisplayName = !string.IsNullOrEmpty(seedData.displayName) ? seedData.displayName : "Cây trồng";
                harvestItemId = !string.IsNullOrEmpty(seedData.harvestItemId) ? seedData.harvestItemId : "produce_" + cropKey;
                // Tốc độ sinh trưởng chậm xuống (mặc định tối thiểu 10s hoặc tỉ lệ theo database)
                secondsPerStage = Mathf.Max(10f, seedData.secondsPerStage * 3f);
                minYield = Mathf.Max(1, seedData.minHarvestYield);
                maxYield = Mathf.Max(minYield, seedData.maxHarvestYield);
            }

            onHarvestedCallback = onHarvested;
            currentStage = 0;
            isMature = false;
            isWilted = false;
            stageTimer = 0f;
            dryTimer = 0f;
            matureTimer = 0f;
            isSoilWet = false;

            // Thiết lập thời gian héo cân đối theo thời gian phát triển
            dryWiltDuration = Mathf.Max(15f, secondsPerStage * 1.8f);
            matureWiltDuration = Mathf.Max(20f, secondsPerStage * 2.2f);

            EnsureSpriteRenderer();
            EnsureSoilRenderer();
            SetSoilWet(false);
            UpdateVisual();
        }

        private void Awake()
        {
            EnsureSpriteRenderer();
            EnsureSoilRenderer();
        }

        private void Start()
        {
            ResolvePlayer();
            EnsureSoilRenderer();
            SetSoilWet(isSoilWet);
            UpdateVisual();
        }

        private void Update()
        {
            // Cây đã héo thì dừng mọi quá trình phát triển
            if (isWilted) return;

            // Xử lý tăng trưởng từng giai đoạn
            if (!isMature)
            {
                // CHỈ CHUYỂN ĐỔI TRẠNG THÁI KHI ĐƯỢC TƯỚI NƯỚC (ĐẤT ƯỚT)
                if (isSoilWet)
                {
                    dryTimer = 0f;
                    stageTimer += Time.deltaTime;
                    if (stageTimer >= secondsPerStage)
                    {
                        stageTimer = 0f;
                        AdvanceStage();
                    }
                }
                else
                {
                    // Đất đang khô: Nếu ở giai đoạn 1, 2, 3 mà không tưới nước kịp thời -> Cây bị héo!
                    if (currentStage >= 1 && currentStage < MaxStage)
                    {
                        dryTimer += Time.deltaTime;
                        if (dryTimer >= dryWiltDuration)
                        {
                            WiltCrop(false);
                        }
                    }
                }
            }
            else
            {
                // Khi đã chín (Giai đoạn 4), nếu không thu hoạch kịp thời -> Cây bị héo!
                matureTimer += Time.deltaTime;
                if (matureTimer >= matureWiltDuration)
                {
                    WiltCrop(true);
                    return;
                }

                // Khi đã chín, lắng nghe tương tác thu hoạch bằng phím E
                CheckHarvestInteraction();
            }
        }

        private void EnsureSpriteRenderer()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                }
                spriteRenderer.sortingLayerName = "World";
                spriteRenderer.sortingOrder = 3;
            }

            var col = GetComponent<BoxCollider2D>();
            if (col == null)
            {
                col = gameObject.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = new Vector2(0.8f, 0.8f);
            }
        }

        private Sprite GetSoilWetSprite()
        {
            if (soilWetSprite != null) return soilWetSprite;

            if (InventoryManager.Instance != null)
            {
                soilWetSprite = InventoryManager.Instance.GetSprite("soil_wet");
            }

            if (soilWetSprite == null)
            {
                foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (s.name.Equals("soil_wet", StringComparison.OrdinalIgnoreCase))
                    {
                        soilWetSprite = s;
                        break;
                    }
                }
            }

            return soilWetSprite;
        }

        private void EnsureSoilRenderer()
        {
            if (soilRenderer != null) return;

            Transform soilChild = transform.Find("SoilOverlay");
            if (soilChild == null)
            {
                GameObject soilObj = new GameObject("SoilOverlay");
                soilObj.transform.SetParent(transform, false);
                soilObj.transform.localPosition = Vector3.zero;
                soilChild = soilObj.transform;
            }

            soilRenderer = soilChild.GetComponent<SpriteRenderer>();
            if (soilRenderer == null)
            {
                soilRenderer = soilChild.gameObject.AddComponent<SpriteRenderer>();
            }

            // Hiển thị ở layer Ground (order 5) để nằm TRÊN tilemap đất và DƯỚI sprite cây
            soilRenderer.sortingLayerName = "Ground";
            soilRenderer.sortingOrder = 5;
            soilRenderer.sprite = GetSoilWetSprite();
            soilChild.gameObject.SetActive(isSoilWet);
        }

        /// <summary>
        /// Bật/tắt trạng thái đất ướt (hiển thị sprite soil_wet trên ô đất)
        /// </summary>
        public void SetSoilWet(bool wet)
        {
            isSoilWet = wet;
            EnsureSoilRenderer();
            if (soilRenderer != null)
            {
                if (wet && soilRenderer.sprite == null)
                {
                    soilRenderer.sprite = GetSoilWetSprite();
                }
                soilRenderer.gameObject.SetActive(wet);
            }
        }

        /// <summary>
        /// Tưới nước cho cây. Trả về true nếu tưới thành công.
        /// </summary>
        public bool TryWater()
        {
            if (isWilted)
            {
                Debug.LogWarning($"[Tưới Nước] <b>{cropDisplayName}</b> đã bị héo khô rồi, không thể tưới! Hãy dùng <b>Liềm (Phím 3 / L)</b> để dọn.");
                return false;
            }

            if (isMature)
            {
                Debug.Log($"[Tưới Nước] <b>{cropDisplayName}</b> đã chín hoàn toàn rồi, hãy thu hoạch!");
                return false;
            }

            if (isSoilWet)
            {
                Debug.Log($"[Tưới Nước] Đất quanh <b>{cropDisplayName}</b> đã ướt sẵn rồi, không cần tưới thêm.");
                return false;
            }

            SetSoilWet(true);
            dryTimer = 0f;
            Debug.Log($"<color=#2196F3><b>[Tưới Nước]</b></color> Đã tưới nước cho <b>{cropDisplayName}</b> (Đất đã ướt)! Cây bắt đầu phát triển.");
            return true;
        }

        private void ResolvePlayer()
        {
            if (playerTransform == null)
            {
                if (InventoryManager.Instance != null && InventoryManager.Instance.PlayerTransform != null)
                {
                    playerTransform = InventoryManager.Instance.PlayerTransform;
                }
                else
                {
                    var playerObj = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
                    if (playerObj != null) playerTransform = playerObj.transform;
                }
            }
        }

        /// <summary>
        /// Chuyển giai đoạn phát triển và XOÁ TRẠNG THÁI ƯỚT CỦA ĐẤT
        /// </summary>
        private void AdvanceStage()
        {
            if (currentStage < MaxStage)
            {
                currentStage++;
                UpdateVisual();

                // CHUYỂN ĐỔI XONG -> XOÁ TRẠNG THÁI ƯỚT CỦA ĐẤT
                SetSoilWet(false);

                // Reset bộ đếm đất khô cho giai đoạn mới
                dryTimer = 0f;

                if (currentStage >= MaxStage)
                {
                    isMature = true;
                    matureTimer = 0f;
                    Debug.Log($"<color=#FF9800><b>[Cây Trồng]</b></color> <b>{cropDisplayName}</b> đã chín hoàn toàn! Lại gần bấm <b>[{harvestKey}]</b> để thu hoạch (nếu không thu hoạch kịp trong {matureWiltDuration:F0}s cây sẽ bị héo).");
                }
                else
                {
                    Debug.Log($"<color=#4CAF50><b>[Cây Trồng]</b></color> <b>{cropDisplayName}</b> đã lớn lên <b>Giai đoạn {currentStage}/{MaxStage}</b>! Đất đã khô lại, hãy tưới nước trong {dryWiltDuration:F0}s để cây tiếp tục lớn (nếu không tưới sẽ bị héo).");
                }
            }
        }

        /// <summary>
        /// Chuyển cây sang trạng thái héo khô khi không tưới ở stage 1-3 hoặc không thu hoạch kịp ở stage 4
        /// </summary>
        public void WiltCrop(bool isMatureTimeout)
        {
            if (isWilted) return;

            isWilted = true;
            isSoilWet = false;
            SetSoilWet(false);

            UpdateVisual();

            string reason = isMatureTimeout
                ? "không thu hoạch kịp thời khi đã chín (Giai đoạn 4)"
                : $"đất bị khô quá lâu không được tưới nước (Giai đoạn {currentStage})";

            Debug.Log($"<color=#FF5722><b>[Cây Héo]</b></color> <b>{cropDisplayName}</b> đã bị héo do {reason}! Hãy dùng <b>Liềm (Phím 3 / L)</b> để dọn dẹp.");
        }

        /// <summary>
        /// Dọn dẹp / cắt bỏ cây bị héo bằng Liềm
        /// </summary>
        public void ClearWiltedCrop()
        {
            Debug.Log($"<color=#795548><b>[Dọn Cây Héo]</b></color> Đã dùng <b>Liềm</b> dọn sạch <b>{cropDisplayName}</b> bị héo! Ô đất đã sạch sẽ sẵn sàng trồng mới.");
            onHarvestedCallback?.Invoke(this);
            Destroy(gameObject);
        }

        private void UpdateVisual()
        {
            if (spriteRenderer == null) EnsureSpriteRenderer();

            // Nếu cây bị héo: stage{wiltStage}_{cropKey}_wilted (stage 1 đến 4)
            // Nếu cây bình thường: stage{currentStage}_{cropKey}
            string spriteName;
            if (isWilted)
            {
                int wiltStage = Mathf.Clamp(currentStage, 1, 4);
                spriteName = $"stage{wiltStage}_{cropKey}_wilted";
            }
            else
            {
                spriteName = $"stage{currentStage}_{cropKey}";
            }

            Sprite stageSprite = LoadCropSprite(spriteName);

            if (stageSprite != null)
            {
                spriteRenderer.sprite = stageSprite;
            }
            else
            {
                Debug.LogWarning($"[CropPlant] Không tìm thấy sprite cho giai đoạn: '{spriteName}'");
            }
        }

        private Sprite LoadCropSprite(string spriteName)
        {
            Sprite sp = null;
            if (InventoryManager.Instance != null)
            {
                sp = InventoryManager.Instance.GetSprite(spriteName);
            }

            // Xử lý lệch tên pumpkin vs pumkin trong asset sprite sheet
            if (sp == null && spriteName.Contains("pumpkin"))
            {
                string altName = spriteName.Replace("pumpkin", "pumkin");
                if (InventoryManager.Instance != null)
                {
                    sp = InventoryManager.Instance.GetSprite(altName);
                }
            }
            if (sp == null && spriteName.Contains("pumkin"))
            {
                string altName = spriteName.Replace("pumkin", "pumpkin");
                if (InventoryManager.Instance != null)
                {
                    sp = InventoryManager.Instance.GetSprite(altName);
                }
            }

            if (sp == null)
            {
                // Fallback tìm trong toàn bộ sprite đã nạp trong bộ nhớ
                foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    if (s.name.Equals(spriteName, StringComparison.OrdinalIgnoreCase))
                    {
                        sp = s;
                        break;
                    }
                    if (spriteName.Contains("pumpkin") && s.name.Equals(spriteName.Replace("pumpkin", "pumkin"), StringComparison.OrdinalIgnoreCase))
                    {
                        sp = s;
                        break;
                    }
                }
            }

            return sp;
        }

        private void CheckHarvestInteraction()
        {
            if (isWilted) return;

            if (playerTransform == null)
            {
                ResolvePlayer();
                if (playerTransform == null) return;
            }

            // Nếu người chơi đang cầm cuốc hoặc liềm thì không tự thu hoạch ở đây để nhường thao tác công cụ
            if (playerTransform != null)
            {
                var pc = playerTransform.GetComponent<PlayerController>();
                if (pc != null && (pc.SelectedTool == PlayerController.ToolType.Hoe || pc.SelectedTool == PlayerController.ToolType.Scythe)) return;
            }

            float dist = Vector2.Distance(playerTransform.position, transform.position);
            bool isNear = dist <= harvestRadius;

            if (isNear && Input.GetKeyDown(harvestKey))
            {
                Harvest();
            }
        }

        /// <summary>
        /// Phá hủy / nhổ bỏ cây trồng bằng cuốc (không thu hoạch nông sản)
        /// </summary>
        public void DestroyCrop()
        {
            Debug.Log($"<color=#E91E63><b>[Cuốc Đất]</b></color> Đã dùng cuốc phá bỏ <b>{cropDisplayName}</b>! Ô đất đã trống trở lại.");
            onHarvestedCallback?.Invoke(this);
            Destroy(gameObject);
        }

        public void Harvest()
        {
            if (isWilted)
            {
                Debug.LogWarning($"[Thu Hoạch] <b>{cropDisplayName}</b> đã bị héo khô, không thể thu hoạch! Hãy dùng <b>Liềm (Phím 3 / L)</b> để dọn.");
                return;
            }

            var inv = InventoryManager.Instance;
            if (inv == null) return;

            // 1. Kiểm tra tỉ lệ nông sản bị hỏng (Spoiled)
            float roll = UnityEngine.Random.Range(0f, 100f);
            if (roll < spoiledChance)
            {
                inv.Notify("Nông sản bị hỏng!");
                Debug.LogWarning($"<color=#E53935><b>[Nông Sản Bị Hỏng]</b></color> Thật không may! <b>{cropDisplayName}</b> đã bị hư hỏng nên không thu hoạch được nông sản nào.");

                onHarvestedCallback?.Invoke(this);
                Destroy(gameObject);
                return;
            }

            // 2. Xác định phẩm cấp nông sản VIP (Platinum, Vàng, Bạc, Thường)
            float qualityRoll = UnityEngine.Random.Range(0f, 100f);
            CropQuality quality = CropQuality.Normal;
            string targetHarvestId = harvestItemId;
            string tierSuffix = "";

            if (qualityRoll < platinumChance)
            {
                quality = CropQuality.Platinum;
                tierSuffix = "_platinum";
            }
            else if (qualityRoll < platinumChance + goldChance)
            {
                quality = CropQuality.Gold;
                tierSuffix = "_gold";
            }
            else if (qualityRoll < platinumChance + goldChance + silverChance)
            {
                quality = CropQuality.Silver;
                tierSuffix = "_silver";
            }

            if (!string.IsNullOrEmpty(tierSuffix))
            {
                string candidateId = harvestItemId + tierSuffix;
                if (inv.GetItemData(candidateId) != null)
                {
                    targetHarvestId = candidateId;
                }
            }

            int yieldAmount = UnityEngine.Random.Range(minYield, maxYield + 1);
            bool added = inv.AddItem(targetHarvestId, yieldAmount, true);

            if (added)
            {
                var itemData = inv.GetItemData(targetHarvestId);
                string nameToShow = itemData != null ? itemData.displayName : cropDisplayName;
                int price = itemData != null ? itemData.sellPrice : 0;

                string qualityTag = quality switch
                {
                    CropQuality.Platinum => "<color=#00E5FF><b>[VIP BẠCH KIM]</b></color> ",
                    CropQuality.Gold => "<color=#FFD700><b>[VIP VÀNG]</b></color> ",
                    CropQuality.Silver => "<color=#C0C0C0><b>[VIP BẠC]</b></color> ",
                    _ => ""
                };

                Debug.Log($"<color=#4CAF50><b>[Thu Hoạch]</b></color> {qualityTag}Đã thu hoạch thành công: <b>{nameToShow}</b> x{yieldAmount} (Giá bán: {price} Xu) vào túi đồ!");
            }
            else
            {
                Debug.LogWarning("[Thu Hoạch] Túi đồ đã đầy, không thể nhận nông sản!");
                return;
            }

            onHarvestedCallback?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
