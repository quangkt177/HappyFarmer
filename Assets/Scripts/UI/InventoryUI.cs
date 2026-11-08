using System.Collections.Generic;
using HappyFarmer.Inventory;
using HappyFarmer.Economy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HappyFarmer.UI
{
    public class InventoryUI : MonoBehaviour
    {
        [Header("Controls & Timing")]
        [SerializeField] private KeyCode toggleKey = KeyCode.I;
        [SerializeField] private KeyCode alternateToggleKey = KeyCode.Tab;
        [SerializeField] private float toastDuration = 2.8f;

        [Header("Hierarchy Containers")]
        [SerializeField] private GameObject hotbarRoot;
        [SerializeField] private GameObject backpackModalRoot;
        [SerializeField] private RectTransform hotbarSlotsContainer;
        [SerializeField] private RectTransform backpackSlotsContainer;

        [Header("Currency Displays")]
        [SerializeField] private GameObject hudCoinBadge;
        [SerializeField] private Text hudCoinText;
        [SerializeField] private Text modalCoinText;

        [Header("Hotbar Info & Controls")]
        [SerializeField] private Text activeItemLabel;
        [SerializeField] private Button bagToggleButton;
        [SerializeField] private Image bagButtonIcon;

        [Header("Item Details Panel")]
        [SerializeField] private Image detailIconImage;
        [SerializeField] private Text detailNameText;
        [SerializeField] private Text detailCategoryText;
        [SerializeField] private Text detailCountText;
        [SerializeField] private Text detailPriceText;
        [SerializeField] private Text detailDescriptionText;

        [Header("Action Buttons")]
        [SerializeField] private Button dropButton;
        [SerializeField] private Button sortButton;
        [SerializeField] private Button sellOneButton;
        [SerializeField] private Text sellOneButtonText;
        [SerializeField] private Button sellAllButton;
        [SerializeField] private Text sellAllButtonText;
        [SerializeField] private Button closeBackpackButton;

        [Header("Toast & Drag Ghost")]
        [SerializeField] private GameObject toastBannerObject;
        [SerializeField] private Text toastText;
        [SerializeField] private RectTransform dragGhostRect;
        [SerializeField] private Image dragGhostImage;

        [SerializeField] private List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();

        private int inspectedSlotIndex;
        private int draggedSlotIndex = -1;
        private float toastTimer;

        public KeyCode ToggleKey => toggleKey;

        private void Awake()
        {
            ConfigureCrispCanvas();
            BindExistingHierarchy();
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
            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.OnBalanceChanged -= OnBalanceChanged;
            }
            if (InventoryManager.Instance == null) return;
            InventoryManager.Instance.OnInventoryChanged -= RefreshAll;
            InventoryManager.Instance.OnSelectedHotbarChanged -= OnHotbarSelectionChanged;
            InventoryManager.Instance.OnBalanceChanged -= OnBalanceChanged;
            InventoryManager.Instance.OnNotification -= ShowToast;
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey) || Input.GetKeyDown(alternateToggleKey) || Input.GetKeyDown(KeyCode.B))
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

        private void ConfigureCrispCanvas()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.pixelPerfect = true;

            var scaler = GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        private void ConnectToManager()
        {
            var manager = InventoryManager.Instance ?? FindObjectOfType<InventoryManager>();
            if (manager == null) return;

            if (slotUIs.Count != manager.TotalSlotCount)
            {
                BindExistingHierarchy();
                BindEvents();
            }

            manager.OnInventoryChanged -= RefreshAll;
            manager.OnSelectedHotbarChanged -= OnHotbarSelectionChanged;
            manager.OnBalanceChanged -= OnBalanceChanged;
            manager.OnNotification -= ShowToast;

            manager.OnInventoryChanged += RefreshAll;
            manager.OnSelectedHotbarChanged += OnHotbarSelectionChanged;
            manager.OnBalanceChanged += OnBalanceChanged;
            manager.OnNotification += ShowToast;

            if (EconomyManager.Instance != null)
            {
                EconomyManager.Instance.OnBalanceChanged -= OnBalanceChanged;
                EconomyManager.Instance.OnBalanceChanged += OnBalanceChanged;
            }

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

        private void BindEvents()
        {
            BindButton(bagToggleButton, ToggleBackpack);
            BindButton(closeBackpackButton, () => SetBackpackOpen(false));
            BindButton(dropButton, () => ExecuteAndRefresh(m => m.DropSlotItem(inspectedSlotIndex, 1)));
            BindButton(sortButton, () => ExecuteAndRefresh(m => m.SortBackpack()));
            BindButton(sellOneButton, () => ExecuteAndRefresh(m => m.SellSlotItem(inspectedSlotIndex, 1)));
            BindButton(sellAllButton, () => ExecuteAndRefresh(m => m.SellAllInSlot(inspectedSlotIndex)));

            int hotbarCount = InventoryManager.HotbarSize;
            for (int i = 0; i < slotUIs.Count; i++)
            {
                if (slotUIs[i] != null)
                {
                    slotUIs[i].BindRuntime(this, i, i < hotbarCount);
                }
            }
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void ExecuteAndRefresh(System.Action<InventoryManager> action)
        {
            var manager = InventoryManager.Instance;
            if (manager == null) return;
            action(manager);
            RefreshAll();
        }

        public void ToggleBackpack() => SetBackpackOpen(backpackModalRoot == null || !backpackModalRoot.activeSelf);

        public void SetBackpackOpen(bool open)
        {
            if (backpackModalRoot != null) backpackModalRoot.SetActive(open);

            var manager = InventoryManager.Instance;
            if (manager != null)
            {
                var current = manager.GetSlot(inspectedSlotIndex);
                if (!open || current == null || current.IsEmpty)
                {
                    inspectedSlotIndex = manager.SelectedHotbarIndex;
                }
            }

            if (!open) OnSlotEndDrag();
            RefreshAll();
        }

        private void OnHotbarSelectionChanged(int hotbarIndex)
        {
            inspectedSlotIndex = hotbarIndex;
            RefreshAll();
        }

        private void OnBalanceChanged(int _)
        {
            RefreshCurrencyUI();
            if (InventoryManager.Instance != null) RefreshDetailsPanel(InventoryManager.Instance);
        }

        // =====================================================================
        // SLOT INTERACTIONS & DRAG-AND-DROP
        // =====================================================================

        public void OnSlotLeftClicked(int slotIndex, bool isHotbar)
        {
            var manager = InventoryManager.Instance;
            if (manager == null) return;

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                inspectedSlotIndex = manager.QuickMoveSlot(slotIndex);
                RefreshAll();
                return;
            }

            if (isHotbar) manager.SelectHotbarSlot(slotIndex);
            inspectedSlotIndex = slotIndex;
            RefreshAll();
        }

        public void OnSlotRightClicked(int slotIndex)
        {
            var manager = InventoryManager.Instance;
            if (manager == null) return;
            inspectedSlotIndex = manager.QuickMoveSlot(slotIndex);
            RefreshAll();
        }

        public void OnSlotBeginDrag(int slotIndex, PointerEventData eventData)
        {
            var manager = InventoryManager.Instance;
            var slot = manager != null ? manager.GetSlot(slotIndex) : null;
            var data = (slot != null && !slot.IsEmpty) ? manager.GetItemData(slot.itemId) : null;
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
            if (draggedSlotIndex >= 0 && dragGhostRect != null)
            {
                dragGhostRect.position = eventData.position;
            }
        }

        public void OnSlotEndDrag()
        {
            draggedSlotIndex = -1;
            if (dragGhostRect != null) dragGhostRect.gameObject.SetActive(false);
        }

        public void OnSlotDrop(int targetSlotIndex)
        {
            var manager = InventoryManager.Instance;
            if (draggedSlotIndex >= 0 && manager != null && draggedSlotIndex != targetSlotIndex)
            {
                manager.SwapOrMergeSlots(draggedSlotIndex, targetSlotIndex);
                inspectedSlotIndex = targetSlotIndex;
                if (targetSlotIndex < manager.HotbarSlotCount)
                {
                    manager.SelectHotbarSlot(targetSlotIndex);
                }
            }
            OnSlotEndDrag();
            RefreshAll();
        }

        // =====================================================================
        // REFRESH & DISPLAY
        // =====================================================================

        public void ShowToast(string message)
        {
            if (toastBannerObject == null || toastText == null) return;
            toastText.text = message;
            toastBannerObject.SetActive(true);
            toastTimer = toastDuration;
        }

        public void RefreshAll()
        {
            var manager = InventoryManager.Instance;
            if (manager == null) return;

            bool isBagOpen = backpackModalRoot != null && backpackModalRoot.activeSelf;

            for (int i = 0; i < slotUIs.Count && i < manager.TotalSlotCount; i++)
            {
                var slotUI = slotUIs[i];
                if (slotUI == null) continue;

                var slot = manager.GetSlot(i);
                var data = (slot != null && !slot.IsEmpty) ? manager.GetItemData(slot.itemId) : null;
                bool isSelected = i == (isBagOpen ? inspectedSlotIndex : manager.SelectedHotbarIndex);

                slotUI.Refresh(data?.icon, data != null ? slot.count : 0, isSelected);
            }

            RefreshCurrencyUI();
            RefreshHotbarHeader(manager);
            RefreshDetailsPanel(manager);
        }

        private void RefreshCurrencyUI()
        {
            var econ = EconomyManager.Instance;
            var manager = InventoryManager.Instance;
            if (econ == null && manager == null) return;

            string formatted = econ != null 
                ? econ.FormatCurrency(econ.CurrentBalance) 
                : manager.FormatCurrency(manager.CurrentBalance);

            if (hudCoinText != null) hudCoinText.text = formatted;
            if (modalCoinText != null) modalCoinText.text = formatted;
        }

        private void RefreshHotbarHeader(InventoryManager manager)
        {
            if (activeItemLabel == null) return;

            var slot = manager.GetSlot(manager.SelectedHotbarIndex);
            var data = (slot != null && !slot.IsEmpty) ? manager.GetItemData(slot.itemId) : null;

            if (data == null)
            {
                activeItemLabel.text = $"Tay không   •   [{toggleKey}] Mở Túi Đồ & Bán Hàng   [Q] Thả món đồ";
                return;
            }

            string priceInfo = data.CanSell ? $"  |  Giá bán: {manager.FormatCurrency(data.sellPrice)}" : string.Empty;
            activeItemLabel.text = $"{data.displayName} (x{slot.count}){priceInfo}   •   [{toggleKey}] Túi Đồ   [Q] Thả món đồ";
        }

        private void RefreshDetailsPanel(InventoryManager manager)
        {
            if (detailNameText == null) return;

            var slot = manager.GetSlot(inspectedSlotIndex);
            var data = (slot != null && !slot.IsEmpty) ? manager.GetItemData(slot.itemId) : null;

            if (data == null)
            {
                if (detailIconImage != null) detailIconImage.enabled = false;
                detailNameText.text = "Ô Trống";
                if (detailCategoryText != null) detailCategoryText.text = "Chưa có vật phẩm";
                if (detailCountText != null) detailCountText.text = "Số lượng: 0";
                if (detailPriceText != null) detailPriceText.text = "Mệnh giá: —";
                if (detailDescriptionText != null)
                {
                    detailDescriptionText.text =
                        "Hãy chọn một vật phẩm trong túi đồ hoặc thanh công cụ để xem mệnh giá và bán hàng.\n\n" +
                        "• Chọn vật phẩm rồi bấm [Bán 1] hoặc [Bán Hết] để nhận Xu ngay tại Túi Đồ.\n" +
                        "• Kéo & Thả chuột trái để đổi chỗ, Chuột Phải để chuyển nhanh.";
                }

                if (dropButton != null) dropButton.interactable = false;
                SetButtonState(sellOneButton, sellOneButtonText, false, "Bán 1");
                SetButtonState(sellAllButton, sellAllButtonText, false, "Bán Hết");
                return;
            }

            if (detailIconImage != null)
            {
                detailIconImage.sprite = data.icon;
                detailIconImage.color = Color.white;
                detailIconImage.enabled = data.icon != null;
            }

            detailNameText.text = data.displayName;
            if (detailCategoryText != null) detailCategoryText.text = $"Phân loại: {data.GetCategoryDisplayName()}";
            if (detailCountText != null) detailCountText.text = $"Số lượng: {slot.count} / {data.maxStack}";
            if (detailDescriptionText != null) detailDescriptionText.text = data.description;

            int stackValue = slot.count * data.sellPrice;
            if (detailPriceText != null)
            {
                if (data.CanSell)
                    detailPriceText.text = $"Giá bán: {manager.FormatCurrency(data.sellPrice)} / món  (Tổng: {manager.FormatCurrency(stackValue)})";
                else
                    detailPriceText.text = "Không thể bán";
            }

            if (dropButton != null) dropButton.interactable = true;

            SetButtonState(sellOneButton, sellOneButtonText, data.CanSell,
                data.CanSell ? $"Bán 1 (+{manager.FormatCurrency(data.sellPrice)})" : "Không Bán");
            SetButtonState(sellAllButton, sellAllButtonText, data.CanSell,
                data.CanSell ? $"Bán Hết (+{manager.FormatCurrency(stackValue)})" : "Bán Hết");
        }

        private static void SetButtonState(Button btn, Text label, bool interactable, string text)
        {
            if (btn != null) btn.interactable = interactable;
            if (label != null) label.text = text;
        }

        // =====================================================================
        // SCENE CANVAS BINDINGS (NO BUILDER BY CODE)
        // =====================================================================

        public void EnsureUIHierarchy() => BindExistingHierarchy();
        public void BuildUIHierarchy() => BindExistingHierarchy();

        public void BindExistingHierarchy()
        {
            if (hotbarRoot == null) hotbarRoot = transform.Find("HotbarSection")?.gameObject;
            if (backpackModalRoot == null) backpackModalRoot = transform.Find("BackpackModal")?.gameObject;
            if (hudCoinBadge == null) hudCoinBadge = transform.Find("HudCoinBadge")?.gameObject;
            if (hudCoinText == null && hudCoinBadge != null) hudCoinText = hudCoinBadge.GetComponentInChildren<Text>(true);
            if (toastBannerObject == null) toastBannerObject = transform.Find("ToastBanner")?.gameObject;
            if (toastText == null && toastBannerObject != null) toastText = toastBannerObject.GetComponentInChildren<Text>(true);
            if (dragGhostRect == null) dragGhostRect = transform.Find("DragGhostIcon")?.GetComponent<RectTransform>();
            if (dragGhostImage == null && dragGhostRect != null) dragGhostImage = dragGhostRect.GetComponent<Image>();

            if (hotbarRoot != null)
            {
                if (activeItemLabel == null) activeItemLabel = hotbarRoot.transform.Find("ActiveItemRibbon/ActiveItemLabel")?.GetComponent<Text>();
                if (bagToggleButton == null) bagToggleButton = hotbarRoot.transform.Find("BagButton")?.GetComponent<Button>();
                if (bagButtonIcon == null) bagButtonIcon = hotbarRoot.transform.Find("BagButton/CrateIcon")?.GetComponent<Image>();
                if (hotbarSlotsContainer == null) hotbarSlotsContainer = hotbarRoot.transform.Find("HotbarWoodFrame/HotbarInner/HotbarSlots")?.GetComponent<RectTransform>();
            }

            if (backpackModalRoot != null)
            {
                if (closeBackpackButton == null) closeBackpackButton = backpackModalRoot.transform.Find("InventoryWindow/ParchmentBody/HeaderBanner/CloseButton")?.GetComponent<Button>();
                if (modalCoinText == null) modalCoinText = backpackModalRoot.transform.Find("InventoryWindow/ParchmentBody/HeaderBanner/ModalCoinBox/ModalCoinText")?.GetComponent<Text>();
                if (backpackSlotsContainer == null) backpackSlotsContainer = backpackModalRoot.transform.Find("InventoryWindow/ParchmentBody/BackpackGridPanel/SlotsGrid")?.GetComponent<RectTransform>();

                var details = backpackModalRoot.transform.Find("InventoryWindow/ParchmentBody/ItemDetailsPanel");
                if (details != null)
                {
                    if (detailIconImage == null) detailIconImage = details.Find("IconPreviewBox/DetailIcon")?.GetComponent<Image>();
                    if (detailNameText == null) detailNameText = details.Find("DetailName")?.GetComponent<Text>();
                    if (detailCategoryText == null) detailCategoryText = details.Find("DetailCategory")?.GetComponent<Text>();
                    if (detailCountText == null) detailCountText = details.Find("DetailCount")?.GetComponent<Text>();
                    if (detailPriceText == null) detailPriceText = details.Find("PriceBanner/DetailPriceText")?.GetComponent<Text>();
                    if (detailDescriptionText == null) detailDescriptionText = details.Find("DescriptionBox/DetailDescription")?.GetComponent<Text>();

                    var econRow = details.Find("EconomyButtonsRow");
                    if (econRow != null)
                    {
                        if (sellOneButton == null) sellOneButton = econRow.Find("SellOneButton")?.GetComponent<Button>();
                        if (sellOneButtonText == null && sellOneButton != null) sellOneButtonText = sellOneButton.GetComponentInChildren<Text>(true);
                        if (sellAllButton == null) sellAllButton = econRow.Find("SellAllButton")?.GetComponent<Button>();
                        if (sellAllButtonText == null && sellAllButton != null) sellAllButtonText = sellAllButton.GetComponentInChildren<Text>(true);
                    }

                    var actRow = details.Find("ActionButtonsRow");
                    if (actRow != null)
                    {
                        if (dropButton == null) dropButton = actRow.Find("DropButton")?.GetComponent<Button>();
                        if (sortButton == null) sortButton = actRow.Find("SortButton")?.GetComponent<Button>();
                    }
                }
            }

            if (slotUIs == null) slotUIs = new List<InventorySlotUI>();
            if (slotUIs.Count != InventoryManager.TotalSlots)
            {
                slotUIs.Clear();
                if (hotbarSlotsContainer != null)
                {
                    for (int i = 0; i < InventoryManager.HotbarSize; i++)
                    {
                        var slot = hotbarSlotsContainer.Find($"HotbarSlot_{i + 1}")?.GetComponent<InventorySlotUI>();
                        if (slot != null) slotUIs.Add(slot);
                    }
                }
                if (backpackSlotsContainer != null)
                {
                    for (int i = 0; i < InventoryManager.BackpackSize; i++)
                    {
                        var slot = backpackSlotsContainer.Find($"BackpackSlot_{i + 1}")?.GetComponent<InventorySlotUI>();
                        if (slot != null) slotUIs.Add(slot);
                    }
                }
            }
        }
    }
}
