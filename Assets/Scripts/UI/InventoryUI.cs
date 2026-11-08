using System.Collections.Generic;
using HappyFarmer.Inventory;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HappyFarmer.UI
{
    public class InventoryUI : MonoBehaviour
    {
        [Header("UI Configuration")]
        [SerializeField] private KeyCode toggleKey = KeyCode.I;
        [SerializeField] private KeyCode alternateToggleKey = KeyCode.Tab;
        [SerializeField] private int backpackGridColumns = 4;
        [SerializeField] private float toastDuration = 2.8f;

        [Header("Main Containers")]
        [SerializeField] private GameObject hotbarRoot;
        [SerializeField] private GameObject backpackModalRoot;
        [SerializeField] private RectTransform hotbarSlotsContainer;
        [SerializeField] private RectTransform backpackSlotsContainer;

        [Header("Economy HUD & Coin Display")]
        [SerializeField] private GameObject hudCoinBadge;
        [SerializeField] private UnityEngine.UI.Image hudCoinIcon;
        [SerializeField] private UnityEngine.UI.Text hudCoinText;
        [SerializeField] private UnityEngine.UI.Image modalCoinIcon;
        [SerializeField] private UnityEngine.UI.Text modalCoinText;

        [Header("Hotbar Info & Controls")]
        [SerializeField] private UnityEngine.UI.Text activeItemLabel;
        [SerializeField] private UnityEngine.UI.Button bagToggleButton;
        [SerializeField] private UnityEngine.UI.Image bagButtonIcon;

        [Header("Backpack Details Panel")]
        [SerializeField] private UnityEngine.UI.Image detailIconImage;
        [SerializeField] private UnityEngine.UI.Text detailNameText;
        [SerializeField] private UnityEngine.UI.Text detailCategoryText;
        [SerializeField] private UnityEngine.UI.Text detailCountText;
        [SerializeField] private UnityEngine.UI.Text detailPriceText;
        [SerializeField] private UnityEngine.UI.Text detailDescriptionText;

        [Header("Action & Economy Buttons")]
        [SerializeField] private UnityEngine.UI.Button dropButton;
        [SerializeField] private UnityEngine.UI.Button sortButton;
        [SerializeField] private UnityEngine.UI.Button sellOneButton;
        [SerializeField] private UnityEngine.UI.Text sellOneButtonText;
        [SerializeField] private UnityEngine.UI.Button sellAllButton;
        [SerializeField] private UnityEngine.UI.Text sellAllButtonText;
        [SerializeField] private UnityEngine.UI.Button closeBackpackButton;

        [Header("Toast & Drag Ghost")]
        [SerializeField] private GameObject toastBannerObject;
        [SerializeField] private UnityEngine.UI.Text toastText;
        [SerializeField] private RectTransform dragGhostRect;
        [SerializeField] private UnityEngine.UI.Image dragGhostImage;

        [SerializeField] private List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();

        private int inspectedSlotIndex;
        private int draggedSlotIndex = -1;
        private float toastTimer;

        private Sprite woodPanelSprite;
        private Sprite innerPaperSprite;
        private Sprite slotBoxSprite;
        private Sprite selectionOutlineSprite;
        private Sprite buttonSprite;
        private Sprite coinSprite;

        private void Awake()
        {
            CleanObsoleteButtons();
            ConfigureCrispCanvas();
            GenerateThemeSprites();
            EnsureUIHierarchy();
            BindEvents();
        }

        private void ConfigureCrispCanvas()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.pixelPerfect = true;
            }

            var scaler = GetComponent<UnityEngine.UI.CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
                scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            foreach (var txt in GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                if (txt != null)
                {
                    txt.alignByGeometry = false;
                }
            }
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

        private void ConnectToManager()
        {
            var manager = InventoryManager.Instance ?? FindObjectOfType<InventoryManager>();
            if (manager == null) return;

            if (slotUIs.Count != manager.TotalSlotCount)
            {
                BuildUIHierarchy();
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

        private static void BindButton(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action)
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



        public void ToggleBackpack()
        {
            SetBackpackOpen(backpackModalRoot == null || !backpackModalRoot.activeSelf);
        }

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
        // SLOT POINTER & DRAG EVENTS
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
        // UI REFRESH
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
            var manager = InventoryManager.Instance;
            if (manager == null) return;

            string formatted = manager.FormatCurrency(manager.CurrentBalance);
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

        private static void SetButtonState(UnityEngine.UI.Button btn, UnityEngine.UI.Text label, bool interactable, string text)
        {
            if (btn != null) btn.interactable = interactable;
            if (label != null) label.text = text;
        }

        // =====================================================================
        // DYNAMIC UI HIERARCHY BUILDER
        // =====================================================================

                private void CleanObsoleteButtons()
        {
            var allTransforms = GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                if (t != null && (t.gameObject.name == "UseButton" || t.gameObject.name == "BuyOneButton"))
                {
                    t.gameObject.SetActive(false);
                    t.SetParent(null);
                    if (Application.isPlaying)
                    {
                        Destroy(t.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(t.gameObject);
                    }
                }
            }
        }

public void EnsureUIHierarchy()
        {
            CleanObsoleteButtons();
            if (hotbarRoot != null &&
                backpackModalRoot != null &&
                slotUIs.Count == InventoryManager.TotalSlots &&
                sellOneButton != null &&
                hudCoinText != null)
            {
                if (backpackSlotsContainer != null && backpackSlotsContainer.parent is RectTransform leftPanelRect && leftPanelRect.anchorMax.x > 0.01f)
                {
                    leftPanelRect.anchorMin = new Vector2(0f, 0f);
                    leftPanelRect.anchorMax = new Vector2(0f, 1f);
                    leftPanelRect.pivot = new Vector2(0f, 0.5f);
                    leftPanelRect.offsetMin = new Vector2(14f, 14f);
                    leftPanelRect.offsetMax = new Vector2(394f, -68f);
                }
                ApplyProceduralSpritesToExistingUI();
                return;
            }
            BuildUIHierarchy();
        }

        public void BuildUIHierarchy()
        {
            if (Application.isPlaying) GenerateThemeSprites();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    child.SetActive(false);
                    child.transform.SetParent(null, false);
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
            slotUIs.Clear();

            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf")
                        ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            BuildHudCoinBadge(font);
            BuildToastBanner(font);
            BuildHotbarSection(font);
            BuildBackpackModalSection(font);
            BuildDragGhost();

            if (Application.isPlaying) ApplyProceduralSpritesToExistingUI();
        }

        private void BuildHudCoinBadge(Font font)
        {
            hudCoinBadge = CreateBox("HudCoinBadge", transform, new Color(0.44f, 0.27f, 0.15f, 0.96f), false, out var rect);
            SetAnchor(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(190f, 48f));

            CreateBox("CoinIcon", hudCoinBadge.transform, new Color(1f, 0.84f, 0.18f, 1f), false, out var iconRect, out hudCoinIcon);
            hudCoinIcon.preserveAspect = true;
            SetAnchor(iconRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(28f, 28f));

            hudCoinText = CreateText("HudCoinText", hudCoinBadge.transform, font, 19, FontStyle.Bold,
                new Color(1f, 0.94f, 0.65f, 1f), TextAnchor.MiddleRight, "100 Xu");
            SetStretchOffsets(hudCoinText.rectTransform, 46f, 4f, -14f, -4f);
        }

        private void BuildToastBanner(Font font)
        {
            toastBannerObject = CreateBox("ToastBanner", transform, new Color(0.36f, 0.22f, 0.13f, 0.96f), false, out var rect);
            SetAnchor(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(540f, 46f));

            toastText = CreateText("ToastText", toastBannerObject.transform, font, 18, FontStyle.Bold,
                new Color(1f, 0.96f, 0.84f, 1f), TextAnchor.MiddleCenter);
            StretchFull(toastText.rectTransform, 14f, 6f);
        }

        private void BuildHotbarSection(Font font)
        {
            int hotbarCount = InventoryManager.HotbarSize;

            hotbarRoot = CreateUIObject("HotbarSection", transform, out var rootRect);
            SetAnchor(rootRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(720f, 128f));

            var ribbonObj = CreateBox("ActiveItemRibbon", hotbarRoot.transform, new Color(0.42f, 0.26f, 0.15f, 0.94f), false, out var ribbonRect);
            SetAnchor(ribbonRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(640f, 34f));

            activeItemLabel = CreateText("ActiveItemLabel", ribbonObj.transform, font, 16, FontStyle.Bold,
                new Color(0.99f, 0.94f, 0.80f, 1f), TextAnchor.MiddleCenter);
            StretchFull(activeItemLabel.rectTransform, 12f, 4f);

            var barFrame = CreateBox("HotbarWoodFrame", hotbarRoot.transform, new Color(0.54f, 0.34f, 0.20f, 1f), true, out var barRect);
            SetAnchor(barRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-46f, 0f), new Vector2(616f, 84f));

            var innerBar = CreateBox("HotbarInner", barFrame.transform, new Color(0.75f, 0.58f, 0.38f, 1f), false, out var innerBarRect);
            StretchFull(innerBarRect, 8f, 8f);

            var slotsHolder = CreateUIObject("HotbarSlots", innerBar.transform, out hotbarSlotsContainer);
            StretchFull(hotbarSlotsContainer, 6f, 4f);

            var hlg = slotsHolder.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.spacing = 6f;
            hlg.padding = new RectOffset(4, 4, 2, 2);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            for (int i = 0; i < hotbarCount; i++)
            {
                slotUIs.Add(CreateSlotElement($"HotbarSlot_{i + 1}", hotbarSlotsContainer, font, i, true));
            }

            var bagBtnObj = CreateBox("BagButton", hotbarRoot.transform, new Color(0.62f, 0.39f, 0.22f, 1f), true, out var bagBtnRect, out var bagBtnImg);
            SetAnchor(bagBtnRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(314f, 0f), new Vector2(88f, 84f));
            bagToggleButton = bagBtnObj.AddComponent<UnityEngine.UI.Button>();
            bagToggleButton.targetGraphic = bagBtnImg;

            CreateBox("CrateIcon", bagBtnObj.transform, Color.white, false, out var iconRect, out bagButtonIcon);
            bagButtonIcon.preserveAspect = true;
            SetAnchor(iconRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(40f, 40f));

#if UNITY_EDITOR
            bagButtonIcon.sprite = FindEditorSprite("crate_open") ?? FindEditorSprite("crate");
#endif

            var bagLabel = CreateText("BagLabel", bagBtnObj.transform, font, 14, FontStyle.Bold,
                new Color(1f, 0.95f, 0.82f, 1f), TextAnchor.LowerCenter, $"TÚI ĐỒ [{toggleKey}]");
            StretchFull(bagLabel.rectTransform, 4f, 6f);
        }

        private void BuildBackpackModalSection(Font font)
        {
            int hotbarCount = InventoryManager.HotbarSize;
            int backpackCount = InventoryManager.BackpackSize;

            backpackModalRoot = CreateBox("BackpackModal", transform, new Color(0.08f, 0.05f, 0.03f, 0.48f), true, out var modalRect);
            StretchFull(modalRect, 0f, 0f);

            var windowObj = CreateBox("InventoryWindow", backpackModalRoot.transform, new Color(0.51f, 0.32f, 0.18f, 1f), true, out var winRect);
            SetAnchor(winRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(800f, 500f));

            var parchmentObj = CreateBox("ParchmentBody", windowObj.transform, new Color(0.95f, 0.89f, 0.76f, 1f), false, out var parchmentRect);
            StretchFull(parchmentRect, 14f, 14f);

            // Header Banner
            var headerObj = CreateBox("HeaderBanner", parchmentObj.transform, new Color(0.44f, 0.27f, 0.15f, 1f), false, out var headerRect);
            SetAnchor(headerRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(-20f, 52f));

            var titleText = CreateText("TitleText", headerObj.transform, font, 21, FontStyle.Bold,
                new Color(1f, 0.94f, 0.78f, 1f), TextAnchor.MiddleLeft, "TÚI ĐỒ & QUẦY BÁN HÀNG NÔNG TRẠI");
            SetStretchOffsets(titleText.rectTransform, 18f, 6f, -240f, -6f);

            var modalCoinBox = CreateBox("ModalCoinBox", headerObj.transform, new Color(0.28f, 0.16f, 0.08f, 0.95f), false, out var modalCoinRect);
            SetAnchor(modalCoinRect, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-58f, 0f), new Vector2(170f, 36f));

            CreateBox("CoinIcon", modalCoinBox.transform, new Color(1f, 0.84f, 0.18f, 1f), false, out var modalCoinIconRect, out modalCoinIcon);
            modalCoinIcon.preserveAspect = true;
            SetAnchor(modalCoinIconRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(22f, 22f));

            modalCoinText = CreateText("ModalCoinText", modalCoinBox.transform, font, 16, FontStyle.Bold,
                new Color(1f, 0.92f, 0.55f, 1f), TextAnchor.MiddleRight, "100 Xu");
            SetStretchOffsets(modalCoinText.rectTransform, 36f, 2f, -10f, -2f);

            closeBackpackButton = CreateAnchoredButton("CloseButton", headerObj.transform, font, "X",
                new Color(0.78f, 0.31f, 0.27f, 1f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(38f, 36f), 18);

            // Left Column: Backpack Slots Grid
            var leftPanel = CreateBox("BackpackGridPanel", parchmentObj.transform, new Color(0.82f, 0.69f, 0.49f, 1f), false, out var leftRect);
            leftRect.anchorMin = new Vector2(0f, 0f);
            leftRect.anchorMax = new Vector2(0f, 1f);
            leftRect.pivot = new Vector2(0f, 0.5f);
            leftRect.offsetMin = new Vector2(14f, 14f);
            leftRect.offsetMax = new Vector2(394f, -68f);

            var gridTitle = CreateText("GridSubtitle", leftPanel.transform, font, 14, FontStyle.Bold,
                new Color(0.32f, 0.19f, 0.10f, 1f), TextAnchor.MiddleLeft,
                $"NGĂN CHỨA ĐỒ ({backpackCount} Ô)  •  Chuột phải: Chuyển nhanh");
            SetAnchor(gridTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -6f), new Vector2(-20f, 24f));

            var gridContainerObj = CreateUIObject("SlotsGrid", leftPanel.transform, out backpackSlotsContainer);
            SetStretchOffsets(backpackSlotsContainer, 14f, 12f, -14f, -34f);

            var grid = gridContainerObj.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.cellSize = new Vector2(78f, 76f);
            grid.spacing = new Vector2(10f, 10f);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, backpackGridColumns);

            for (int i = 0; i < backpackCount; i++)
            {
                slotUIs.Add(CreateSlotElement($"BackpackSlot_{i + 1}", backpackSlotsContainer, font, hotbarCount + i, false));
            }

            // Right Column: Item Details & Economy Actions
            var rightPanel = CreateBox("ItemDetailsPanel", parchmentObj.transform, new Color(0.89f, 0.80f, 0.63f, 1f), false, out var rightRect);
            SetStretchOffsets(rightRect, 406f, 14f, -14f, -68f);

            var previewBox = CreateBox("IconPreviewBox", rightPanel.transform, new Color(0.96f, 0.90f, 0.76f, 1f), false, out var previewRect);
            SetAnchor(previewRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -14f), new Vector2(84f, 84f));

            CreateBox("DetailIcon", previewBox.transform, Color.white, false, out var detailIconRect, out detailIconImage);
            detailIconImage.preserveAspect = true;
            StretchFull(detailIconRect, 12f, 12f);

            detailNameText = CreateTopStretchText("DetailName", rightPanel.transform, font, 20, FontStyle.Bold,
                new Color(0.28f, 0.16f, 0.08f, 1f), 110f, -42f, -14f, -14f);
            detailCategoryText = CreateTopStretchText("DetailCategory", rightPanel.transform, font, 14, FontStyle.Italic,
                new Color(0.45f, 0.30f, 0.16f, 1f), 110f, -64f, -14f, -42f);
            detailCountText = CreateTopStretchText("DetailCount", rightPanel.transform, font, 14, FontStyle.Bold,
                new Color(0.25f, 0.45f, 0.18f, 1f), 110f, -86f, -14f, -64f);

            var priceBanner = CreateBox("PriceBanner", rightPanel.transform, new Color(0.38f, 0.24f, 0.12f, 0.95f), false, out var priceBannerRect);
            priceBannerRect.anchorMin = new Vector2(0f, 1f);
            priceBannerRect.anchorMax = new Vector2(1f, 1f);
            priceBannerRect.pivot = new Vector2(0.5f, 1f);
            priceBannerRect.offsetMin = new Vector2(16f, -138f);
            priceBannerRect.offsetMax = new Vector2(-16f, -104f);

            detailPriceText = CreateText("DetailPriceText", priceBanner.transform, font, 14, FontStyle.Bold,
                new Color(1f, 0.91f, 0.50f, 1f), TextAnchor.MiddleCenter);
            StretchFull(detailPriceText.rectTransform, 10f, 4f);

            var descBox = CreateBox("DescriptionBox", rightPanel.transform, new Color(0.95f, 0.89f, 0.77f, 1f), false, out var descBoxRect);
            SetStretchOffsets(descBoxRect, 16f, 112f, -16f, -146f);

            detailDescriptionText = CreateText("DetailDescription", descBox.transform, font, 14, FontStyle.Normal,
                new Color(0.30f, 0.19f, 0.11f, 1f), TextAnchor.UpperLeft);
            detailDescriptionText.lineSpacing = 1.15f;
            StretchFull(detailDescriptionText.rectTransform, 12f, 8f);

            var economyBar = CreateButtonRow("EconomyButtonsRow", rightPanel.transform, 60f, 102f, 6f);
            sellOneButton = CreateLayoutButton("SellOneButton", economyBar, font, "Bán 1",
                new Color(0.82f, 0.56f, 0.14f, 1f), out sellOneButtonText);
            sellAllButton = CreateLayoutButton("SellAllButton", economyBar, font, "Bán Hết",
                new Color(0.76f, 0.39f, 0.14f, 1f), out sellAllButtonText);

            var actionsBar = CreateButtonRow("ActionButtonsRow", rightPanel.transform, 12f, 52f, 8f);
            dropButton = CreateLayoutButton("DropButton", actionsBar, font, "Thả 1 Món",
                new Color(0.68f, 0.43f, 0.22f, 1f), out _);
            sortButton = CreateLayoutButton("SortButton", actionsBar, font, "Sắp Xếp",
                new Color(0.46f, 0.34f, 0.22f, 1f), out _);
        }

        private void BuildDragGhost()
        {
            var ghostObj = CreateBox("DragGhostIcon", transform, new Color(1f, 1f, 1f, 0.9f), false, out dragGhostRect, out dragGhostImage);
            dragGhostImage.preserveAspect = true;
            SetAnchor(dragGhostRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52f, 52f));
            ghostObj.SetActive(false);
        }

        private InventorySlotUI CreateSlotElement(string name, Transform parent, Font font, int index, bool isHotbar)
        {
            Color bgCol = isHotbar ? new Color(0.89f, 0.79f, 0.61f, 1f) : new Color(0.86f, 0.75f, 0.57f, 1f);
            var slotObj = CreateBox(name, parent, bgCol, true, out _, out var bgImg);

            CreateBox("SelectionBorder", slotObj.transform, new Color(0.98f, 0.76f, 0.18f, 1f), false, out var borderRect, out var borderImg);
            StretchFull(borderRect, 0f, 0f);
            borderImg.enabled = false;

            CreateBox("Icon", slotObj.transform, Color.white, false, out var iconRect, out var iconImg);
            StretchFull(iconRect, 10f, 10f);
            iconImg.preserveAspect = true;
            iconImg.enabled = false;

            var keyText = CreateText("KeybindText", slotObj.transform, font, 14, FontStyle.Bold,
                new Color(0.42f, 0.27f, 0.15f, 0.9f), TextAnchor.UpperLeft, isHotbar ? (index + 1).ToString() : string.Empty);
            StretchFull(keyText.rectTransform, 6f, 4f);
            keyText.gameObject.SetActive(isHotbar);

            var countTxt = CreateText("CountText", slotObj.transform, font, 16, FontStyle.Bold,
                new Color(0.22f, 0.12f, 0.05f, 1f), TextAnchor.LowerRight);
            StretchFull(countTxt.rectTransform, 6f, 4f);
            var outline = countTxt.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(0.98f, 0.94f, 0.84f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);

            var slotUI = slotObj.AddComponent<InventorySlotUI>();
            slotUI.Setup(this, index, isHotbar, bgImg, borderImg, iconImg, countTxt, keyText);
            return slotUI;
        }

        // =====================================================================
        // COMPACT UI HELPERS
        // =====================================================================

        private static GameObject CreateUIObject(string name, Transform parent, out RectTransform rect)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : 5;
            go.transform.SetParent(parent, false);
            rect = go.GetComponent<RectTransform>();
            return go;
        }

        private static GameObject CreateBox(string name, Transform parent, Color color, bool raycast, out RectTransform rect)
        {
            return CreateBox(name, parent, color, raycast, out rect, out _);
        }

        private static GameObject CreateBox(string name, Transform parent, Color color, bool raycast, out RectTransform rect, out UnityEngine.UI.Image img)
        {
            var go = CreateUIObject(name, parent, out rect);
            img = go.AddComponent<UnityEngine.UI.Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return go;
        }

        private static UnityEngine.UI.Text CreateText(string name, Transform parent, Font font, int size, FontStyle style, Color color, TextAnchor align, string initialText = "")
        {
            var go = CreateUIObject(name, parent, out _);
            var txt = go.AddComponent<UnityEngine.UI.Text>();
            txt.font = font;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = align;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            txt.text = initialText;
            return txt;
        }

        private static UnityEngine.UI.Text CreateTopStretchText(string name, Transform parent, Font font, int size, FontStyle style, Color color, float left, float bottom, float right, float top)
        {
            var txt = CreateText(name, parent, font, size, style, color, TextAnchor.UpperLeft);
            txt.rectTransform.anchorMin = new Vector2(0f, 1f);
            txt.rectTransform.anchorMax = new Vector2(1f, 1f);
            txt.rectTransform.pivot = new Vector2(0f, 1f);
            txt.rectTransform.offsetMin = new Vector2(left, bottom);
            txt.rectTransform.offsetMax = new Vector2(right, top);
            return txt;
        }

        private static Transform CreateButtonRow(string name, Transform parent, float bottomY, float topY, float spacing)
        {
            var rowObj = CreateUIObject(name, parent, out var rect);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(16f, bottomY);
            rect.offsetMax = new Vector2(-16f, topY);

            var hlg = rowObj.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            return rowObj.transform;
        }

        private static UnityEngine.UI.Button CreateAnchoredButton(string name, Transform parent, Font font, string label, Color bgColor, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize)
        {
            var btnObj = CreateBox(name, parent, bgColor, true, out var rect, out var img);
            SetAnchor(rect, anchor, anchor, anchor, pos, size);
            var btn = btnObj.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;

            var txt = CreateText("Label", btnObj.transform, font, fontSize, FontStyle.Bold, new Color(1f, 0.97f, 0.88f, 1f), TextAnchor.MiddleCenter, label);
            StretchFull(txt.rectTransform, 4f, 2f);
            return btn;
        }

        private static UnityEngine.UI.Button CreateLayoutButton(string name, Transform parent, Font font, string label, Color bgColor, out UnityEngine.UI.Text labelText)
        {
            var btnObj = CreateBox(name, parent, bgColor, true, out _, out var img);
            var btn = btnObj.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = img;

            labelText = CreateText("Label", btnObj.transform, font, 15, FontStyle.Bold, new Color(1f, 0.97f, 0.88f, 1f), TextAnchor.MiddleCenter, label);
            StretchFull(labelText.rectTransform, 4f, 2f);
            return btn;
        }

        private static void SetAnchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        private static void StretchFull(RectTransform rect, float padX, float padY)
        {
            SetStretchOffsets(rect, padX, padY, -padX, -padY);
        }

        private static void SetStretchOffsets(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        // =====================================================================
        // PROCEDURAL PIXEL THEME SPRITES (RUNTIME 9-SLICE)
        // =====================================================================

        private void ApplyProceduralSpritesToExistingUI()
        {
            foreach (var img in GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (img == null) continue;
                string n = img.gameObject.name;

                if (n == "BackpackModal" || n == "Icon" || n == "DetailIcon" || n == "CrateIcon" || n == "DragGhostIcon")
                    continue;

                if (n == "CoinIcon")
                {
                    img.sprite = coinSprite;
                    img.type = UnityEngine.UI.Image.Type.Simple;
                    img.color = Color.white;
                }
                else if (n == "SelectionBorder")
                {
                    img.sprite = selectionOutlineSprite;
                    img.type = UnityEngine.UI.Image.Type.Sliced;
                }
                else if (n.StartsWith("HotbarSlot_") || n.StartsWith("BackpackSlot_") || n == "IconPreviewBox")
                {
                    img.sprite = slotBoxSprite;
                    img.type = UnityEngine.UI.Image.Type.Sliced;
                }
                else if (n == "HotbarInner" || n == "ParchmentBody" || n == "BackpackGridPanel" || n == "ItemDetailsPanel" || n == "DescriptionBox")
                {
                    img.sprite = innerPaperSprite;
                    img.type = UnityEngine.UI.Image.Type.Sliced;
                }
                else if (n.EndsWith("Button"))
                {
                    img.sprite = buttonSprite;
                    img.type = UnityEngine.UI.Image.Type.Sliced;
                }
                else
                {
                    img.sprite = woodPanelSprite;
                    img.type = UnityEngine.UI.Image.Type.Sliced;
                }
            }
        }

        private void GenerateThemeSprites()
        {
            if (woodPanelSprite == null)
                woodPanelSprite = CreateBeveledBoxSprite("WoodPanel9Slice", 16, new Color(0.24f, 0.13f, 0.06f, 1f), new Color(0.86f, 0.68f, 0.48f, 1f));
            if (innerPaperSprite == null)
                innerPaperSprite = CreateBeveledBoxSprite("PaperPanel9Slice", 16, new Color(0.48f, 0.31f, 0.17f, 1f), new Color(0.95f, 0.88f, 0.74f, 1f));
            if (slotBoxSprite == null)
                slotBoxSprite = CreateBeveledBoxSprite("SlotBox9Slice", 16, new Color(0.38f, 0.23f, 0.12f, 1f), new Color(0.72f, 0.55f, 0.36f, 1f));
            if (buttonSprite == null)
                buttonSprite = CreateBeveledBoxSprite("Button9Slice", 16, new Color(0.20f, 0.11f, 0.05f, 1f), new Color(0.92f, 0.84f, 0.68f, 1f));
            if (selectionOutlineSprite == null)
                selectionOutlineSprite = CreateOutlineBoxSprite("SelectionOutline9Slice", 16, 3);
            if (coinSprite == null)
                coinSprite = CreatePixelCoinSprite("PixelGoldCoin", 16);
        }

        private static Sprite CreatePixelCoinSprite(string name, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            Color outline = new Color(0.42f, 0.24f, 0.05f, 1f);
            Color goldDark = new Color(0.88f, 0.62f, 0.10f, 1f);
            Color goldMid = new Color(0.98f, 0.80f, 0.18f, 1f);
            Color goldBright = new Color(1f, 0.95f, 0.55f, 1f);

            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center, dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > 6.6f) tex.SetPixel(x, y, Color.clear);
                    else if (dist > 5.4f) tex.SetPixel(x, y, outline);
                    else if ((x == 7 || x == 8) && y >= 5 && y <= 10) tex.SetPixel(x, y, goldBright);
                    else if (dx + dy < -2f) tex.SetPixel(x, y, goldBright);
                    else if (dx + dy > 2.5f) tex.SetPixel(x, y, goldDark);
                    else tex.SetPixel(x, y, goldMid);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateBeveledBoxSprite(string name, int size, Color outerBorder, Color innerHighlight)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if ((x == 0 || x == size - 1) && (y == 0 || y == size - 1))
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }
                    bool isOuter = x == 0 || x == size - 1 || y == 0 || y == size - 1 || ((x == 1 || x == size - 2) && (y == 1 || y == size - 2));
                    bool isInner = x == 1 || x == size - 2 || y == 1 || y == size - 2;
                    tex.SetPixel(x, y, isOuter ? outerBorder : (isInner ? innerHighlight : Color.white));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
        }

        private static Sprite CreateOutlineBoxSprite(string name, int size, int thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isEdge = x < thickness || x >= size - thickness || y < thickness || y >= size - thickness;
                    tex.SetPixel(x, y, isEdge ? Color.white : Color.clear);
                }
            }
            tex.Apply();
            int b = thickness + 1;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

#if UNITY_EDITOR
        private static Sprite FindEditorSprite(string spriteName)
        {
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites" }))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Sprite s && s.name == spriteName) return s;
                }
            }
            return null;
        }
#endif
    }
}
