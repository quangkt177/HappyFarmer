using System.Collections.Generic;
using HappyFarmer.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HappyFarmer.UI
{
    public class InventoryUI : MonoBehaviour
    {
        [Header("Main Containers")]
        [SerializeField] private GameObject hotbarRoot;
        [SerializeField] private GameObject backpackModalRoot;
        [SerializeField] private RectTransform hotbarSlotsContainer;
        [SerializeField] private RectTransform backpackSlotsContainer;

        [Header("Hotbar Info & Controls")]
        [SerializeField] private UnityEngine.UI.Text activeItemLabel;
        [SerializeField] private UnityEngine.UI.Button bagToggleButton;
        [SerializeField] private UnityEngine.UI.Image bagButtonIcon;

        [Header("Backpack Details Panel")]
        [SerializeField] private UnityEngine.UI.Image detailIconImage;
        [SerializeField] private UnityEngine.UI.Text detailNameText;
        [SerializeField] private UnityEngine.UI.Text detailCategoryText;
        [SerializeField] private UnityEngine.UI.Text detailCountText;
        [SerializeField] private UnityEngine.UI.Text detailDescriptionText;
        [SerializeField] private UnityEngine.UI.Button useButton;
        [SerializeField] private UnityEngine.UI.Text useButtonText;
        [SerializeField] private UnityEngine.UI.Button dropButton;
        [SerializeField] private UnityEngine.UI.Button sortButton;
        [SerializeField] private UnityEngine.UI.Button closeBackpackButton;

        [Header("Toast Notification")]
        [SerializeField] private GameObject toastBannerObject;
        [SerializeField] private UnityEngine.UI.Text toastText;

        [Header("Drag Ghost")]
        [SerializeField] private RectTransform dragGhostRect;
        [SerializeField] private UnityEngine.UI.Image dragGhostImage;

        [SerializeField] private List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();

        private int inspectedSlotIndex = 0;
        private int draggedSlotIndex = -1;
        private float toastTimer;

        private Sprite woodPanelSprite;
        private Sprite innerPaperSprite;
        private Sprite slotBoxSprite;
        private Sprite selectionOutlineSprite;
        private Sprite buttonSprite;

        private void Awake()
        {
            GenerateThemeSprites();
            EnsureUIHierarchy();
            BindEvents();
        }

        private void Start()
        {
            ConnectToManager();
            SetBackpackOpen(false);
            if (toastBannerObject != null) toastBannerObject.SetActive(false);
            if (dragGhostRect != null) dragGhostRect.gameObject.SetActive(false);
            RefreshAll();
        }

        private void OnDestroy()
        {
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryChanged -= RefreshAll;
                InventoryManager.Instance.OnSelectedHotbarChanged -= OnHotbarSelectionChanged;
                InventoryManager.Instance.OnNotification -= ShowToast;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.B))
            {
                ToggleBackpack();
            }
            else if (Input.GetKeyDown(KeyCode.Escape) && backpackModalRoot != null && backpackModalRoot.activeSelf)
            {
                SetBackpackOpen(false);
            }

            if (toastTimer > 0f)
            {
                toastTimer -= Time.deltaTime;
                if (toastTimer <= 0f && toastBannerObject != null)
                {
                    toastBannerObject.SetActive(false);
                }
            }
        }

        private void ConnectToManager()
        {
            var manager = InventoryManager.Instance;
            if (manager == null)
            {
                manager = FindObjectOfType<InventoryManager>();
            }

            if (manager != null)
            {
                manager.OnInventoryChanged -= RefreshAll;
                manager.OnSelectedHotbarChanged -= OnHotbarSelectionChanged;
                manager.OnNotification -= ShowToast;

                manager.OnInventoryChanged += RefreshAll;
                manager.OnSelectedHotbarChanged += OnHotbarSelectionChanged;
                manager.OnNotification += ShowToast;

                inspectedSlotIndex = manager.SelectedHotbarIndex;

                if (bagButtonIcon != null)
                {
                    var crateSprite = manager.GetSprite("crate_open") ?? manager.GetSprite("crate");
                    if (crateSprite != null)
                    {
                        bagButtonIcon.sprite = crateSprite;
                        bagButtonIcon.enabled = true;
                    }
                }
            }
        }

        private void BindEvents()
        {
            if (bagToggleButton != null)
            {
                bagToggleButton.onClick.RemoveAllListeners();
                bagToggleButton.onClick.AddListener(ToggleBackpack);
            }

            if (closeBackpackButton != null)
            {
                closeBackpackButton.onClick.RemoveAllListeners();
                closeBackpackButton.onClick.AddListener(() => SetBackpackOpen(false));
            }

            if (useButton != null)
            {
                useButton.onClick.RemoveAllListeners();
                useButton.onClick.AddListener(OnUseClicked);
            }

            if (dropButton != null)
            {
                dropButton.onClick.RemoveAllListeners();
                dropButton.onClick.AddListener(OnDropClicked);
            }

            if (sortButton != null)
            {
                sortButton.onClick.RemoveAllListeners();
                sortButton.onClick.AddListener(OnSortClicked);
            }

            for (int i = 0; i < slotUIs.Count; i++)
            {
                if (slotUIs[i] != null)
                {
                    slotUIs[i].BindRuntime(this, i, i < InventoryManager.HotbarSize);
                }
            }
        }

        public void ToggleBackpack()
        {
            bool nextState = backpackModalRoot == null || !backpackModalRoot.activeSelf;
            SetBackpackOpen(nextState);
        }

        public void SetBackpackOpen(bool open)
        {
            if (backpackModalRoot != null)
            {
                backpackModalRoot.SetActive(open);
            }

            if (!open && dragGhostRect != null)
            {
                dragGhostRect.gameObject.SetActive(false);
                draggedSlotIndex = -1;
            }

            RefreshAll();
        }

        private void OnHotbarSelectionChanged(int hotbarIndex)
        {
            if (backpackModalRoot == null || !backpackModalRoot.activeSelf)
            {
                inspectedSlotIndex = hotbarIndex;
            }
            RefreshAll();
        }

        public void OnSlotLeftClicked(int slotIndex, bool isHotbar)
        {
            var manager = InventoryManager.Instance;
            if (manager == null) return;

            bool isBagOpen = backpackModalRoot != null && backpackModalRoot.activeSelf;

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                manager.QuickMoveSlot(slotIndex);
                inspectedSlotIndex = slotIndex;
                RefreshAll();
                return;
            }

            if (isHotbar)
            {
                manager.SelectHotbarSlot(slotIndex);
            }

            inspectedSlotIndex = slotIndex;
            RefreshAll();
        }

        public void OnSlotRightClicked(int slotIndex)
        {
            var manager = InventoryManager.Instance;
            if (manager == null) return;

            manager.QuickMoveSlot(slotIndex);
            inspectedSlotIndex = slotIndex;
            RefreshAll();
        }

        public void OnSlotBeginDrag(int slotIndex, PointerEventData eventData)
        {
            var manager = InventoryManager.Instance;
            if (manager == null) return;

            var slot = manager.GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty) return;

            var data = manager.GetItemData(slot.itemId);
            if (data == null || data.icon == null) return;

            draggedSlotIndex = slotIndex;
            if (dragGhostRect != null && dragGhostImage != null)
            {
                dragGhostImage.sprite = data.icon;
                dragGhostRect.gameObject.SetActive(true);
                dragGhostRect.SetAsLastSibling();
                dragGhostRect.position = eventData.position;
            }
        }

        public void OnSlotDrag(PointerEventData eventData)
        {
            if (draggedSlotIndex < 0 || dragGhostRect == null) return;
            dragGhostRect.position = eventData.position;
        }

        public void OnSlotEndDrag()
        {
            draggedSlotIndex = -1;
            if (dragGhostRect != null)
            {
                dragGhostRect.gameObject.SetActive(false);
            }
        }

        public void OnSlotDrop(int targetSlotIndex)
        {
            if (draggedSlotIndex < 0) return;

            var manager = InventoryManager.Instance;
            if (manager != null && draggedSlotIndex != targetSlotIndex)
            {
                manager.SwapOrMergeSlots(draggedSlotIndex, targetSlotIndex);
                inspectedSlotIndex = targetSlotIndex;
                if (targetSlotIndex < InventoryManager.HotbarSize)
                {
                    manager.SelectHotbarSlot(targetSlotIndex);
                }
            }

            OnSlotEndDrag();
            RefreshAll();
        }

        private void OnUseClicked()
        {
            var manager = InventoryManager.Instance;
            if (manager == null) return;
            manager.UseSlotItem(inspectedSlotIndex);
            RefreshAll();
        }

        private void OnDropClicked()
        {
            var manager = InventoryManager.Instance;
            if (manager == null) return;
            manager.DropSlotItem(inspectedSlotIndex, 1);
            RefreshAll();
        }

        private void OnSortClicked()
        {
            var manager = InventoryManager.Instance;
            if (manager == null) return;
            manager.SortBackpack();
            RefreshAll();
        }

        public void ShowToast(string message)
        {
            if (toastBannerObject == null || toastText == null) return;
            toastText.text = message;
            toastBannerObject.SetActive(true);
            toastTimer = 2.8f;
        }

        public void RefreshAll()
        {
            var manager = InventoryManager.Instance;
            if (manager == null) return;

            bool isBagOpen = backpackModalRoot != null && backpackModalRoot.activeSelf;

            for (int i = 0; i < slotUIs.Count && i < InventoryManager.TotalSlots; i++)
            {
                var slotUI = slotUIs[i];
                if (slotUI == null) continue;

                var slot = manager.GetSlot(i);
                Sprite icon = null;
                int count = 0;

                if (slot != null && !slot.IsEmpty)
                {
                    var data = manager.GetItemData(slot.itemId);
                    if (data != null)
                    {
                        icon = data.icon;
                        count = slot.count;
                    }
                }

                bool isSelected = isBagOpen
                    ? (i == inspectedSlotIndex)
                    : (i == manager.SelectedHotbarIndex);

                slotUI.Refresh(icon, count, isSelected);
            }

            RefreshHotbarHeader(manager);
            RefreshDetailsPanel(manager);
        }

        private void RefreshHotbarHeader(InventoryManager manager)
        {
            if (activeItemLabel == null) return;

            var slot = manager.GetSlot(manager.SelectedHotbarIndex);
            if (slot == null || slot.IsEmpty)
            {
                activeItemLabel.text = "Tay không  •  [E] Thu hoạch / Hái lượm   [I] Mở Túi Đồ";
                return;
            }

            var data = manager.GetItemData(slot.itemId);
            if (data == null)
            {
                activeItemLabel.text = "Tay không  •  [I] Mở Túi Đồ";
                return;
            }

            string actionHint = data.category == ItemCategory.Seed
                ? "[E] Gieo hạt"
                : (data.category == ItemCategory.Produce || data.category == ItemCategory.Forage)
                    ? "[E] Sử dụng"
                    : "[E] Đặt ra đất";

            activeItemLabel.text = $"{data.displayName} (x{slot.count})   •   {actionHint}   [Q] Thả   [I] Túi Đồ";
        }

        private void RefreshDetailsPanel(InventoryManager manager)
        {
            if (detailNameText == null) return;

            var slot = manager.GetSlot(inspectedSlotIndex);
            var data = (slot != null && !slot.IsEmpty) ? manager.GetItemData(slot.itemId) : null;

            if (data == null)
            {
                if (detailIconImage != null)
                {
                    detailIconImage.sprite = null;
                    detailIconImage.enabled = false;
                }
                detailNameText.text = "Ô Trống";
                if (detailCategoryText != null) detailCategoryText.text = "Chưa có vật phẩm";
                if (detailCountText != null) detailCountText.text = "Số lượng: 0";
                if (detailDescriptionText != null)
                {
                    detailDescriptionText.text =
                        "Hãy chọn một vật phẩm trong túi đồ hoặc thanh công cụ để xem thông tin.\n\n" +
                        "• Kéo & Thả chuột trái để đổi chỗ vật phẩm.\n" +
                        "• Nhấp Chuột Phải (hoặc Shift+Click) để chuyển nhanh giữa Thanh Công Cụ và Túi Đồ.";
                }
                if (useButton != null) useButton.interactable = false;
                if (dropButton != null) dropButton.interactable = false;
                if (useButtonText != null) useButtonText.text = "Sử dụng";
                return;
            }

            if (detailIconImage != null)
            {
                detailIconImage.sprite = data.icon;
                detailIconImage.color = Color.white;
                detailIconImage.enabled = data.icon != null;
            }

            detailNameText.text = data.displayName;
            if (detailCategoryText != null)
                detailCategoryText.text = $"Phân loại: {data.GetCategoryDisplayName()}";
            if (detailCountText != null)
                detailCountText.text = $"Số lượng: {slot.count} / {data.maxStack}";
            if (detailDescriptionText != null)
                detailDescriptionText.text = data.description;

            if (useButton != null) useButton.interactable = true;
            if (dropButton != null) dropButton.interactable = true;

            if (useButtonText != null)
            {
                useButtonText.text = data.category == ItemCategory.Seed
                    ? "Gieo Hạt"
                    : (data.category == ItemCategory.Produce || data.category == ItemCategory.Forage)
                        ? "Sử Dụng"
                        : "Đặt Ra";
            }
        }

        // =====================================================================
        // XÂY DỰNG CẤU TRÚC UI THEO PHONG CÁCH NÔNG TRẠI GỖ ẤM ÁP (uGUI)
        // =====================================================================

        public void EnsureUIHierarchy()
        {
            if (hotbarRoot != null && backpackModalRoot != null && slotUIs.Count == InventoryManager.TotalSlots)
            {
                ApplyProceduralSpritesToExistingUI();
                return;
            }

            BuildUIHierarchy();
        }

        public void BuildUIHierarchy()
        {
            GenerateThemeSprites();

            // Xóa các con cũ nếu đang rebuild
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                    Destroy(transform.GetChild(i).gameObject);
                else
                    DestroyImmediate(transform.GetChild(i).gameObject);
            }
            slotUIs.Clear();

            Font font = GetDefaultFont();

            // 1. Toast Notification (Góc trên giữa màn hình)
            BuildToastBanner(font);

            // 2. Thanh Hotbar bên dưới màn hình (8 ô + Nút Mở Túi Đồ)
            BuildHotbarSection(font);

            // 3. Cửa sổ Túi Đồ Nông Trại (Chính giữa màn hình)
            BuildBackpackModalSection(font);

            // 4. Icon kéo thả (Drag Ghost)
            BuildDragGhost();
        }

        private void BuildToastBanner(Font font)
        {
            toastBannerObject = CreateUIObject("ToastBanner", transform);
            var rect = toastBannerObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -24f);
            rect.sizeDelta = new Vector2(520f, 46f);

            var bg = toastBannerObject.AddComponent<UnityEngine.UI.Image>();
            bg.sprite = woodPanelSprite;
            bg.type = UnityEngine.UI.Image.Type.Sliced;
            bg.color = new Color(0.36f, 0.22f, 0.13f, 0.96f);
            bg.raycastTarget = false;

            toastText = CreateText("ToastText", toastBannerObject.transform, font, 18, FontStyle.Bold,
                new Color(1f, 0.96f, 0.84f, 1f), TextAnchor.MiddleCenter);
            StretchFull(toastText.rectTransform, 14f, 6f);
        }

        private void BuildHotbarSection(Font font)
        {
            hotbarRoot = CreateUIObject("HotbarSection", transform);
            var rootRect = hotbarRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, 16f);
            rootRect.sizeDelta = new Vector2(720f, 128f);

            // Dải băng gỗ hiển thị tên vật phẩm đang cầm
            var ribbonObj = CreateUIObject("ActiveItemRibbon", hotbarRoot.transform);
            var ribbonRect = ribbonObj.GetComponent<RectTransform>();
            ribbonRect.anchorMin = new Vector2(0.5f, 1f);
            ribbonRect.anchorMax = new Vector2(0.5f, 1f);
            ribbonRect.pivot = new Vector2(0.5f, 1f);
            ribbonRect.anchoredPosition = new Vector2(0f, 0f);
            ribbonRect.sizeDelta = new Vector2(620f, 34f);

            var ribbonImg = ribbonObj.AddComponent<UnityEngine.UI.Image>();
            ribbonImg.sprite = woodPanelSprite;
            ribbonImg.type = UnityEngine.UI.Image.Type.Sliced;
            ribbonImg.color = new Color(0.42f, 0.26f, 0.15f, 0.94f);
            ribbonImg.raycastTarget = false;

            activeItemLabel = CreateText("ActiveItemLabel", ribbonObj.transform, font, 15, FontStyle.Bold,
                new Color(0.99f, 0.94f, 0.80f, 1f), TextAnchor.MiddleCenter);
            StretchFull(activeItemLabel.rectTransform, 12f, 4f);

            // Khung gỗ chính của Hotbar
            var barFrame = CreateUIObject("HotbarWoodFrame", hotbarRoot.transform);
            var barRect = barFrame.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(-46f, 0f);
            barRect.sizeDelta = new Vector2(616f, 84f);

            var barBg = barFrame.AddComponent<UnityEngine.UI.Image>();
            barBg.sprite = woodPanelSprite;
            barBg.type = UnityEngine.UI.Image.Type.Sliced;
            barBg.color = new Color(0.54f, 0.34f, 0.20f, 1f);
            barBg.raycastTarget = true;

            var innerBar = CreateUIObject("HotbarInner", barFrame.transform);
            var innerBarRect = innerBar.GetComponent<RectTransform>();
            StretchFull(innerBarRect, 8f, 8f);
            var innerBarImg = innerBar.AddComponent<UnityEngine.UI.Image>();
            innerBarImg.sprite = innerPaperSprite;
            innerBarImg.type = UnityEngine.UI.Image.Type.Sliced;
            innerBarImg.color = new Color(0.75f, 0.58f, 0.38f, 1f);
            innerBarImg.raycastTarget = false;

            var slotsHolder = CreateUIObject("HotbarSlots", innerBar.transform);
            hotbarSlotsContainer = slotsHolder.GetComponent<RectTransform>();
            StretchFull(hotbarSlotsContainer, 6f, 4f);

            var hlg = slotsHolder.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.spacing = 6f;
            hlg.padding = new RectOffset(4, 4, 2, 2);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            for (int i = 0; i < InventoryManager.HotbarSize; i++)
            {
                var slotUI = CreateSlotElement($"HotbarSlot_{i + 1}", hotbarSlotsContainer, font, i, true);
                slotUIs.Add(slotUI);
            }

            // Nút Túi Đồ bên phải Hotbar
            var bagBtnObj = CreateUIObject("BagButton", hotbarRoot.transform);
            var bagBtnRect = bagBtnObj.GetComponent<RectTransform>();
            bagBtnRect.anchorMin = new Vector2(0.5f, 0f);
            bagBtnRect.anchorMax = new Vector2(0.5f, 0f);
            bagBtnRect.pivot = new Vector2(0.5f, 0f);
            bagBtnRect.anchoredPosition = new Vector2(314f, 0f);
            bagBtnRect.sizeDelta = new Vector2(88f, 84f);

            var bagBtnImg = bagBtnObj.AddComponent<UnityEngine.UI.Image>();
            bagBtnImg.sprite = woodPanelSprite;
            bagBtnImg.type = UnityEngine.UI.Image.Type.Sliced;
            bagBtnImg.color = new Color(0.62f, 0.39f, 0.22f, 1f);
            bagBtnImg.raycastTarget = true;

            bagToggleButton = bagBtnObj.AddComponent<UnityEngine.UI.Button>();
            bagToggleButton.targetGraphic = bagBtnImg;

            var iconObj = CreateUIObject("CrateIcon", bagBtnObj.transform);
            var iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 10f);
            iconRect.sizeDelta = new Vector2(40f, 40f);

            bagButtonIcon = iconObj.AddComponent<UnityEngine.UI.Image>();
            bagButtonIcon.preserveAspect = true;
            bagButtonIcon.raycastTarget = false;

#if UNITY_EDITOR
            var cratePreview = FindEditorSprite("crate_open") ?? FindEditorSprite("crate");
            if (cratePreview != null)
            {
                bagButtonIcon.sprite = cratePreview;
            }
#endif

            var bagLabel = CreateText("BagLabel", bagBtnObj.transform, font, 13, FontStyle.Bold,
                new Color(1f, 0.95f, 0.82f, 1f), TextAnchor.LowerCenter);
            StretchFull(bagLabel.rectTransform, 4f, 6f);
            bagLabel.text = "TÚI ĐỒ [I]";
        }

        private void BuildBackpackModalSection(Font font)
        {
            backpackModalRoot = CreateUIObject("BackpackModal", transform);
            var modalRect = backpackModalRoot.GetComponent<RectTransform>();
            modalRect.anchorMin = Vector2.zero;
            modalRect.anchorMax = Vector2.one;
            modalRect.offsetMin = Vector2.zero;
            modalRect.offsetMax = Vector2.zero;

            // Lớp nền mờ nhẹ phía sau
            var dimBg = backpackModalRoot.AddComponent<UnityEngine.UI.Image>();
            dimBg.color = new Color(0.08f, 0.05f, 0.03f, 0.48f);
            dimBg.raycastTarget = true;

            // Khung bảng gỗ chính
            var windowObj = CreateUIObject("InventoryWindow", backpackModalRoot.transform);
            var winRect = windowObj.GetComponent<RectTransform>();
            winRect.anchorMin = new Vector2(0.5f, 0.5f);
            winRect.anchorMax = new Vector2(0.5f, 0.5f);
            winRect.pivot = new Vector2(0.5f, 0.5f);
            winRect.anchoredPosition = new Vector2(0f, 38f);
            winRect.sizeDelta = new Vector2(760f, 470f);

            var winBg = windowObj.AddComponent<UnityEngine.UI.Image>();
            winBg.sprite = woodPanelSprite;
            winBg.type = UnityEngine.UI.Image.Type.Sliced;
            winBg.color = new Color(0.51f, 0.32f, 0.18f, 1f);
            winBg.raycastTarget = true;

            // Nền giấy da kem bên trong
            var parchmentObj = CreateUIObject("ParchmentBody", windowObj.transform);
            var parchmentRect = parchmentObj.GetComponent<RectTransform>();
            StretchFull(parchmentRect, 14f, 14f);
            var parchmentImg = parchmentObj.AddComponent<UnityEngine.UI.Image>();
            parchmentImg.sprite = innerPaperSprite;
            parchmentImg.type = UnityEngine.UI.Image.Type.Sliced;
            parchmentImg.color = new Color(0.95f, 0.89f, 0.76f, 1f);
            parchmentImg.raycastTarget = false;

            // Thanh tiêu đề gỗ phía trên
            var headerObj = CreateUIObject("HeaderBanner", parchmentObj.transform);
            var headerRect = headerObj.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0f, -8f);
            headerRect.sizeDelta = new Vector2(-20f, 52f);

            var headerImg = headerObj.AddComponent<UnityEngine.UI.Image>();
            headerImg.sprite = woodPanelSprite;
            headerImg.type = UnityEngine.UI.Image.Type.Sliced;
            headerImg.color = new Color(0.44f, 0.27f, 0.15f, 1f);
            headerImg.raycastTarget = false;

            var titleText = CreateText("TitleText", headerObj.transform, font, 22, FontStyle.Bold,
                new Color(1f, 0.94f, 0.78f, 1f), TextAnchor.MiddleLeft);
            StretchFull(titleText.rectTransform, 20f, 6f);
            titleText.text = "TÚI ĐỒ NÔNG TRẠI  —  LITTLE DREAMYLAND";

            // Nút Đóng [X]
            closeBackpackButton = CreateStyledButton(
                "CloseButton",
                headerObj.transform,
                font,
                "X",
                new Color(0.78f, 0.31f, 0.27f, 1f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-10f, 0f),
                new Vector2(38f, 36f),
                18);

            // Cột trái: Lưới 16 ô (4x4)
            var leftPanel = CreateUIObject("BackpackGridPanel", parchmentObj.transform);
            var leftRect = leftPanel.GetComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0f, 0f);
            leftRect.anchorMax = new Vector2(0f, 1f);
            leftRect.pivot = new Vector2(0f, 0.5f);
            leftRect.offsetMin = new Vector2(14f, 14f);
            leftRect.offsetMax = new Vector2(394f, -68f);

            var leftBg = leftPanel.AddComponent<UnityEngine.UI.Image>();
            leftBg.sprite = innerPaperSprite;
            leftBg.type = UnityEngine.UI.Image.Type.Sliced;
            leftBg.color = new Color(0.82f, 0.69f, 0.49f, 1f);
            leftBg.raycastTarget = false;

            var gridTitle = CreateText("GridSubtitle", leftPanel.transform, font, 14, FontStyle.Bold,
                new Color(0.32f, 0.19f, 0.10f, 1f), TextAnchor.MiddleLeft);
            gridTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            gridTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            gridTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            gridTitle.rectTransform.anchoredPosition = new Vector2(0f, -6f);
            gridTitle.rectTransform.sizeDelta = new Vector2(-20f, 24f);
            gridTitle.text = "NGĂN CHỨA ĐỒ (16 Ô)  •  Chuột phải: Chuyển nhanh";

            var gridContainerObj = CreateUIObject("SlotsGrid", leftPanel.transform);
            backpackSlotsContainer = gridContainerObj.GetComponent<RectTransform>();
            backpackSlotsContainer.anchorMin = Vector2.zero;
            backpackSlotsContainer.anchorMax = Vector2.one;
            backpackSlotsContainer.offsetMin = new Vector2(14f, 12f);
            backpackSlotsContainer.offsetMax = new Vector2(-14f, -34f);

            var grid = gridContainerObj.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.cellSize = new Vector2(78f, 72f);
            grid.spacing = new Vector2(10f, 8f);
            grid.startCorner = UnityEngine.UI.GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            for (int i = 0; i < InventoryManager.BackpackSize; i++)
            {
                int slotIdx = InventoryManager.HotbarSize + i;
                var slotUI = CreateSlotElement($"BackpackSlot_{i + 1}", backpackSlotsContainer, font, slotIdx, false);
                slotUIs.Add(slotUI);
            }

            // Cột phải: Chi tiết vật phẩm & Nút thao tác
            var rightPanel = CreateUIObject("ItemDetailsPanel", parchmentObj.transform);
            var rightRect = rightPanel.GetComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(0f, 0f);
            rightRect.anchorMax = new Vector2(1f, 1f);
            rightRect.pivot = new Vector2(1f, 0.5f);
            rightRect.offsetMin = new Vector2(406f, 14f);
            rightRect.offsetMax = new Vector2(-14f, -68f);

            var rightBg = rightPanel.AddComponent<UnityEngine.UI.Image>();
            rightBg.sprite = innerPaperSprite;
            rightBg.type = UnityEngine.UI.Image.Type.Sliced;
            rightBg.color = new Color(0.89f, 0.80f, 0.63f, 1f);
            rightBg.raycastTarget = false;

            // Khung hiển thị Icon lớn + Tên vật phẩm
            var previewBox = CreateUIObject("IconPreviewBox", rightPanel.transform);
            var previewRect = previewBox.GetComponent<RectTransform>();
            previewRect.anchorMin = new Vector2(0f, 1f);
            previewRect.anchorMax = new Vector2(0f, 1f);
            previewRect.pivot = new Vector2(0f, 1f);
            previewRect.anchoredPosition = new Vector2(16f, -16f);
            previewRect.sizeDelta = new Vector2(84f, 84f);

            var previewBg = previewBox.AddComponent<UnityEngine.UI.Image>();
            previewBg.sprite = slotBoxSprite;
            previewBg.type = UnityEngine.UI.Image.Type.Sliced;
            previewBg.color = new Color(0.96f, 0.90f, 0.76f, 1f);
            previewBg.raycastTarget = false;

            var detailIconObj = CreateUIObject("DetailIcon", previewBox.transform);
            var detailIconRect = detailIconObj.GetComponent<RectTransform>();
            StretchFull(detailIconRect, 12f, 12f);
            detailIconImage = detailIconObj.AddComponent<UnityEngine.UI.Image>();
            detailIconImage.preserveAspect = true;
            detailIconImage.raycastTarget = false;

            detailNameText = CreateText("DetailName", rightPanel.transform, font, 20, FontStyle.Bold,
                new Color(0.28f, 0.16f, 0.08f, 1f), TextAnchor.UpperLeft);
            detailNameText.rectTransform.anchorMin = new Vector2(0f, 1f);
            detailNameText.rectTransform.anchorMax = new Vector2(1f, 1f);
            detailNameText.rectTransform.pivot = new Vector2(0f, 1f);
            detailNameText.rectTransform.offsetMin = new Vector2(112f, -46f);
            detailNameText.rectTransform.offsetMax = new Vector2(-14f, -16f);
            detailNameText.text = "Hạt Cà Rốt";

            detailCategoryText = CreateText("DetailCategory", rightPanel.transform, font, 14, FontStyle.Italic,
                new Color(0.45f, 0.30f, 0.16f, 1f), TextAnchor.UpperLeft);
            detailCategoryText.rectTransform.anchorMin = new Vector2(0f, 1f);
            detailCategoryText.rectTransform.anchorMax = new Vector2(1f, 1f);
            detailCategoryText.rectTransform.pivot = new Vector2(0f, 1f);
            detailCategoryText.rectTransform.offsetMin = new Vector2(112f, -72f);
            detailCategoryText.rectTransform.offsetMax = new Vector2(-14f, -48f);
            detailCategoryText.text = "Phân loại: Hạt giống";

            detailCountText = CreateText("DetailCount", rightPanel.transform, font, 14, FontStyle.Bold,
                new Color(0.25f, 0.45f, 0.18f, 1f), TextAnchor.UpperLeft);
            detailCountText.rectTransform.anchorMin = new Vector2(0f, 1f);
            detailCountText.rectTransform.anchorMax = new Vector2(1f, 1f);
            detailCountText.rectTransform.pivot = new Vector2(0f, 1f);
            detailCountText.rectTransform.offsetMin = new Vector2(112f, -98f);
            detailCountText.rectTransform.offsetMax = new Vector2(-14f, -74f);
            detailCountText.text = "Số lượng: 6 / 99";

            // Khung mô tả chi tiết
            var descBox = CreateUIObject("DescriptionBox", rightPanel.transform);
            var descBoxRect = descBox.GetComponent<RectTransform>();
            descBoxRect.anchorMin = new Vector2(0f, 0f);
            descBoxRect.anchorMax = new Vector2(1f, 1f);
            descBoxRect.offsetMin = new Vector2(16f, 68f);
            descBoxRect.offsetMax = new Vector2(-16f, -112f);

            var descBg = descBox.AddComponent<UnityEngine.UI.Image>();
            descBg.sprite = innerPaperSprite;
            descBg.type = UnityEngine.UI.Image.Type.Sliced;
            descBg.color = new Color(0.95f, 0.89f, 0.77f, 1f);
            descBg.raycastTarget = false;

            detailDescriptionText = CreateText("DetailDescription", descBox.transform, font, 15, FontStyle.Normal,
                new Color(0.30f, 0.19f, 0.11f, 1f), TextAnchor.UpperLeft);
            StretchFull(detailDescriptionText.rectTransform, 12f, 10f);
            detailDescriptionText.lineSpacing = 1.15f;

            // Hàng nút bấm chức năng bên dưới cột phải
            var actionsBar = CreateUIObject("ActionButtonsRow", rightPanel.transform);
            var actionsRect = actionsBar.GetComponent<RectTransform>();
            actionsRect.anchorMin = new Vector2(0f, 0f);
            actionsRect.anchorMax = new Vector2(1f, 0f);
            actionsRect.pivot = new Vector2(0.5f, 0f);
            actionsRect.offsetMin = new Vector2(16f, 14f);
            actionsRect.offsetMax = new Vector2(-16f, 56f);

            var actionsHlg = actionsBar.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            actionsHlg.spacing = 8f;
            actionsHlg.childAlignment = TextAnchor.MiddleCenter;
            actionsHlg.childControlWidth = true;
            actionsHlg.childControlHeight = true;
            actionsHlg.childForceExpandWidth = true;
            actionsHlg.childForceExpandHeight = true;

            useButton = CreateLayoutButton("UseButton", actionsBar.transform, font, "Gieo Hạt",
                new Color(0.35f, 0.62f, 0.25f, 1f), out useButtonText);
            dropButton = CreateLayoutButton("DropButton", actionsBar.transform, font, "Thả 1 Món",
                new Color(0.76f, 0.47f, 0.21f, 1f), out _);
            sortButton = CreateLayoutButton("SortButton", actionsBar.transform, font, "Sắp Xếp",
                new Color(0.46f, 0.34f, 0.22f, 1f), out _);
        }

        private void BuildDragGhost()
        {
            var ghostObj = CreateUIObject("DragGhostIcon", transform);
            dragGhostRect = ghostObj.GetComponent<RectTransform>();
            dragGhostRect.anchorMin = new Vector2(0.5f, 0.5f);
            dragGhostRect.anchorMax = new Vector2(0.5f, 0.5f);
            dragGhostRect.pivot = new Vector2(0.5f, 0.5f);
            dragGhostRect.sizeDelta = new Vector2(52f, 52f);

            dragGhostImage = ghostObj.AddComponent<UnityEngine.UI.Image>();
            dragGhostImage.preserveAspect = true;
            dragGhostImage.raycastTarget = false;
            dragGhostImage.color = new Color(1f, 1f, 1f, 0.9f);
            ghostObj.SetActive(false);
        }

        private InventorySlotUI CreateSlotElement(
            string name,
            Transform parent,
            Font font,
            int index,
            bool isHotbar)
        {
            var slotObj = CreateUIObject(name, parent);
            var bgImg = slotObj.AddComponent<UnityEngine.UI.Image>();
            bgImg.sprite = slotBoxSprite;
            bgImg.type = UnityEngine.UI.Image.Type.Sliced;
            bgImg.color = isHotbar
                ? new Color(0.89f, 0.79f, 0.61f, 1f)
                : new Color(0.86f, 0.75f, 0.57f, 1f);
            bgImg.raycastTarget = true;

            // Viền chọn màu vàng kim
            var borderObj = CreateUIObject("SelectionBorder", slotObj.transform);
            StretchFull(borderObj.GetComponent<RectTransform>(), 0f, 0f);
            var borderImg = borderObj.AddComponent<UnityEngine.UI.Image>();
            borderImg.sprite = selectionOutlineSprite;
            borderImg.type = UnityEngine.UI.Image.Type.Sliced;
            borderImg.color = new Color(0.98f, 0.76f, 0.18f, 1f);
            borderImg.raycastTarget = false;
            borderImg.enabled = false;

            // Icon vật phẩm
            var iconObj = CreateUIObject("Icon", slotObj.transform);
            StretchFull(iconObj.GetComponent<RectTransform>(), 10f, 10f);
            var iconImg = iconObj.AddComponent<UnityEngine.UI.Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconImg.enabled = false;

            // Số thứ tự phím nóng 1..8 ở góc trên trái (chỉ cho Hotbar)
            var keyText = CreateText("KeybindText", slotObj.transform, font, 12, FontStyle.Bold,
                new Color(0.42f, 0.27f, 0.15f, 0.9f), TextAnchor.UpperLeft);
            StretchFull(keyText.rectTransform, 6f, 4f);
            keyText.text = isHotbar ? (index + 1).ToString() : string.Empty;
            keyText.gameObject.SetActive(isHotbar);

            // Số lượng cộng dồn ở góc dưới phải
            var countTxt = CreateText("CountText", slotObj.transform, font, 14, FontStyle.Bold,
                new Color(0.22f, 0.12f, 0.05f, 1f), TextAnchor.LowerRight);
            StretchFull(countTxt.rectTransform, 6f, 4f);
            var outline = countTxt.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0.98f, 0.94f, 0.84f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);

            var slotUI = slotObj.AddComponent<InventorySlotUI>();
            slotUI.Setup(this, index, isHotbar, bgImg, borderImg, iconImg, countTxt, keyText);
            return slotUI;
        }

        private UnityEngine.UI.Button CreateStyledButton(
            string name,
            Transform parent,
            Font font,
            string label,
            Color bgColor,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 size,
            int fontSize)
        {
            var btnObj = CreateUIObject(name, parent);
            var rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            var img = btnObj.AddComponent<UnityEngine.UI.Image>();
            img.sprite = buttonSprite;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = bgColor;
            img.raycastTarget = true;

            var btn = btnObj.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;

            var txt = CreateText("Label", btnObj.transform, font, fontSize, FontStyle.Bold,
                new Color(1f, 0.97f, 0.88f, 1f), TextAnchor.MiddleCenter);
            StretchFull(txt.rectTransform, 4f, 2f);
            txt.text = label;

            return btn;
        }

        private UnityEngine.UI.Button CreateLayoutButton(
            string name,
            Transform parent,
            Font font,
            string label,
            Color bgColor,
            out UnityEngine.UI.Text labelText)
        {
            var btnObj = CreateUIObject(name, parent);
            var img = btnObj.AddComponent<UnityEngine.UI.Image>();
            img.sprite = buttonSprite;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.color = bgColor;
            img.raycastTarget = true;

            var btn = btnObj.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;

            labelText = CreateText("Label", btnObj.transform, font, 15, FontStyle.Bold,
                new Color(1f, 0.97f, 0.88f, 1f), TextAnchor.MiddleCenter);
            StretchFull(labelText.rectTransform, 6f, 4f);
            labelText.text = label;

            return btn;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : 5;
            go.transform.SetParent(parent, false);
            return go;
        }

        private static UnityEngine.UI.Text CreateText(
            string name,
            Transform parent,
            Font font,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor alignment)
        {
            var go = CreateUIObject(name, parent);
            var txt = go.AddComponent<UnityEngine.UI.Text>();
            txt.font = font;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        private static void StretchFull(RectTransform rect, float padX, float padY)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padX, padY);
            rect.offsetMax = new Vector2(-padX, -padY);
        }

        private static Font GetDefaultFont()
        {
            var font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return font;
        }

        private void ApplyProceduralSpritesToExistingUI()
        {
            // Gán lại 9-slice procedural sprites khi vào Play Mode nếu các Image trong Scene đang dùng màu thuần
            var allImages = GetComponentsInChildren<UnityEngine.UI.Image>(true);
            foreach (var img in allImages)
            {
                if (img == null || img.sprite != null) continue;
                string n = img.gameObject.name;
                if (n == "SelectionBorder")
                {
                    img.sprite = selectionOutlineSprite;
                    img.type = UnityEngine.UI.Image.Type.Sliced;
                }
                else if (n.StartsWith("HotbarSlot_") || n.StartsWith("BackpackSlot_") || n == "IconPreviewBox")
                {
                    img.sprite = slotBoxSprite;
                    img.type = UnityEngine.UI.Image.Type.Sliced;
                }
                else if (n.EndsWith("Button"))
                {
                    img.sprite = buttonSprite;
                    img.type = UnityEngine.UI.Image.Type.Sliced;
                }
                else if (n != "BackpackModal" && n != "Icon" && n != "DetailIcon" && n != "CrateIcon" && n != "DragGhostIcon")
                {
                    img.sprite = woodPanelSprite;
                    img.type = UnityEngine.UI.Image.Type.Sliced;
                }
            }
        }

        private void GenerateThemeSprites()
        {
            if (woodPanelSprite == null)
            {
                woodPanelSprite = CreateBeveledBoxSprite(
                    "WoodPanel9Slice",
                    16,
                    new Color(0.24f, 0.13f, 0.06f, 1f), // Viền ngoài gỗ sẫm
                    new Color(0.86f, 0.68f, 0.48f, 1f), // Viền sáng trong
                    Color.white);
            }

            if (innerPaperSprite == null)
            {
                innerPaperSprite = CreateBeveledBoxSprite(
                    "PaperPanel9Slice",
                    16,
                    new Color(0.48f, 0.31f, 0.17f, 1f),
                    new Color(0.95f, 0.88f, 0.74f, 1f),
                    Color.white);
            }

            if (slotBoxSprite == null)
            {
                slotBoxSprite = CreateBeveledBoxSprite(
                    "SlotBox9Slice",
                    16,
                    new Color(0.38f, 0.23f, 0.12f, 1f),
                    new Color(0.72f, 0.55f, 0.36f, 1f),
                    Color.white);
            }

            if (buttonSprite == null)
            {
                buttonSprite = CreateBeveledBoxSprite(
                    "Button9Slice",
                    16,
                    new Color(0.20f, 0.11f, 0.05f, 1f),
                    new Color(0.92f, 0.84f, 0.68f, 1f),
                    Color.white);
            }

            if (selectionOutlineSprite == null)
            {
                selectionOutlineSprite = CreateOutlineBoxSprite("SelectionOutline9Slice", 16, 3);
            }
        }

        private static Sprite CreateBeveledBoxSprite(
            string name,
            int size,
            Color outerBorder,
            Color innerHighlight,
            Color fill)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = name;
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isCornerCut = (x == 0 || x == size - 1) && (y == 0 || y == size - 1);
                    if (isCornerCut)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    bool isOuter = x == 0 || x == size - 1 || y == 0 || y == size - 1 ||
                                   ((x == 1 || x == size - 2) && (y == 1 || y == size - 2));
                    bool isInner = x == 1 || x == size - 2 || y == 1 || y == size - 2;

                    if (isOuter)
                        tex.SetPixel(x, y, outerBorder);
                    else if (isInner)
                        tex.SetPixel(x, y, innerHighlight);
                    else
                        tex.SetPixel(x, y, fill);
                }
            }

            tex.Apply();
            return Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(4, 4, 4, 4));
        }

        private static Sprite CreateOutlineBoxSprite(string name, int size, int thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = name;
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isEdge = x < thickness || x >= size - thickness || y < thickness || y >= size - thickness;
                    tex.SetPixel(x, y, isEdge ? Color.white : Color.clear);
                }
            }

            tex.Apply();
            return Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(thickness + 1, thickness + 1, thickness + 1, thickness + 1));
        }

#if UNITY_EDITOR
        private static Sprite FindEditorSprite(string spriteName)
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites" });
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Sprite s && s.name == spriteName)
                        return s;
                }
            }
            return null;
        }
#endif
    }
}
