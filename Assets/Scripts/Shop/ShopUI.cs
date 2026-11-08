using System.Collections.Generic;
using HappyFarmer.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace HappyFarmer.Shop
{
    public class ShopUI : MonoBehaviour
    {
        public static ShopUI Instance { get; private set; }

        [SerializeField] private KeyCode closeKey = KeyCode.Escape;
        [SerializeField] private KeyCode toggleKey = KeyCode.E;

        private static readonly Color PanelColor = new Color(0.44f, 0.27f, 0.15f, 0.98f);
        private static readonly Color PaperColor = new Color(0.95f, 0.88f, 0.74f, 1f);
        private static readonly Color RowColor = new Color(0.89f, 0.80f, 0.63f, 1f);
        private static readonly Color DarkText = new Color(0.30f, 0.17f, 0.08f, 1f);
        private static readonly Color LightText = new Color(1f, 0.96f, 0.84f, 1f);
        private static readonly Color BuyColor = new Color(0.30f, 0.55f, 0.24f, 1f);
        private static readonly Color SellColor = new Color(0.72f, 0.42f, 0.14f, 1f);
        private static readonly Color TabIdleColor = new Color(0.58f, 0.40f, 0.24f, 1f);
        private static readonly Color TabActiveColor = new Color(0.86f, 0.62f, 0.20f, 1f);

        private Font font;
        private GameObject promptBox;
        private Text promptText;
        private Object promptOwner;

        private GameObject modalRoot;
        private Text titleText;
        private Text balanceText;
        private Text emptyText;
        private Image buyTabImage;
        private Image sellTabImage;
        private Transform listContent;

        private bool isBuyTab = true;
        private int openedFrame;
        private InventoryManager subscribedManager;

        public ShopStall CurrentStall { get; private set; }

        public static ShopUI Create()
        {
            var canvasObj = new GameObject("ShopCanvas");
            int uiLayer = LayerMask.NameToLayer("UI");
            canvasObj.layer = uiLayer >= 0 ? uiLayer : 5;

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 110;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            return canvasObj.AddComponent<ShopUI>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            BuildPrompt();
            BuildModal();
            promptBox.SetActive(false);
            modalRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (CurrentStall == null || Time.frameCount == openedFrame) return;
            if (Input.GetKeyDown(closeKey) || Input.GetKeyDown(toggleKey)) Close();
        }

        // =====================================================================
        // PROMPT ([E] Mở ...)
        // =====================================================================

        public void ShowPrompt(Object owner, string text)
        {
            promptOwner = owner;
            promptText.text = text;
            promptBox.SetActive(true);
        }

        public void HidePrompt(Object owner)
        {
            if (promptOwner != owner) return;
            promptOwner = null;
            promptBox.SetActive(false);
        }

        // =====================================================================
        // MỞ / ĐÓNG SHOP
        // =====================================================================

        public void Open(ShopStall stall)
        {
            CurrentStall = stall;
            openedFrame = Time.frameCount;
            isBuyTab = true;
            titleText.text = stall.ShopTitle;
            HidePrompt(stall);
            modalRoot.SetActive(true);
            Subscribe();
            Refresh();
        }

        public void Close()
        {
            CurrentStall = null;
            modalRoot.SetActive(false);
            Unsubscribe();
        }

        private void SelectTab(bool buyTab)
        {
            isBuyTab = buyTab;
            Refresh();
        }

        private void Subscribe()
        {
            var manager = InventoryManager.Instance;
            if (manager == null || manager == subscribedManager) return;
            Unsubscribe();
            subscribedManager = manager;
            manager.OnInventoryChanged += Refresh;
            manager.OnBalanceChanged += OnBalanceChanged;
        }

        private void Unsubscribe()
        {
            if (subscribedManager == null) return;
            subscribedManager.OnInventoryChanged -= Refresh;
            subscribedManager.OnBalanceChanged -= OnBalanceChanged;
            subscribedManager = null;
        }

        private void OnBalanceChanged(int _) => Refresh();

        private void Refresh()
        {
            if (CurrentStall == null) return;
            var manager = InventoryManager.Instance;

            buyTabImage.color = isBuyTab ? TabActiveColor : TabIdleColor;
            sellTabImage.color = isBuyTab ? TabIdleColor : TabActiveColor;
            balanceText.text = manager != null ? $"Ví: {manager.FormatCurrency(manager.CurrentBalance)}" : string.Empty;

            for (int i = listContent.childCount - 1; i >= 0; i--)
            {
                var child = listContent.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            List<ShopOffer> offers = CurrentStall.GetOffers(isBuyTab);
            foreach (var offer in offers) BuildRow(offer);

            emptyText.gameObject.SetActive(offers.Count == 0);
            emptyText.text = isBuyTab ? "Sạp chưa có hàng để bán." : "Bạn chưa có gì để bán ở sạp này.";
        }

        // =====================================================================
        // DỰNG GIAO DIỆN
        // =====================================================================

        private void BuildPrompt()
        {
            promptBox = CreateBox("InteractPrompt", transform, new Color(0.36f, 0.22f, 0.13f, 0.94f), false, out var rect);
            SetAnchor(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 156f), new Vector2(460f, 40f));
            promptText = CreateText("PromptText", promptBox.transform, 17, FontStyle.Bold, LightText, TextAnchor.MiddleCenter);
            StretchFull(promptText.rectTransform, 10f, 4f);
        }

        private void BuildModal()
        {
            // Nền mờ chặn click xuống game
            modalRoot = CreateBox("ShopModal", transform, new Color(0f, 0f, 0f, 0.45f), true, out var dimRect);
            StretchFull(dimRect, 0f, 0f);

            var panel = CreateBox("ShopPanel", modalRoot.transform, PanelColor, true, out var panelRect);
            SetAnchor(panelRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(660f, 480f));

            titleText = CreateText("Title", panel.transform, 24, FontStyle.Bold, LightText, TextAnchor.MiddleLeft);
            SetTopRow(titleText.rectTransform, 20f, -260f, 0f, 48f);

            balanceText = CreateText("Balance", panel.transform, 18, FontStyle.Bold, new Color(1f, 0.86f, 0.35f, 1f), TextAnchor.MiddleRight);
            SetTopRow(balanceText.rectTransform, 300f, -70f, 0f, 48f);

            var closeButton = CreateButton("CloseButton", panel.transform, "X", new Color(0.62f, 0.20f, 0.14f, 1f), 18, out _);
            var closeRect = (RectTransform)closeButton.transform;
            SetAnchor(closeRect, Vector2.one, Vector2.one, Vector2.one, new Vector2(-12f, -10f), new Vector2(40f, 34f));
            closeButton.onClick.AddListener(Close);

            var buyTab = CreateButton("BuyTab", panel.transform, "Mua", TabActiveColor, 17, out _);
            SetAnchor((RectTransform)buyTab.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -56f), new Vector2(140f, 36f));
            buyTab.onClick.AddListener(() => SelectTab(true));
            buyTabImage = buyTab.GetComponent<Image>();

            var sellTab = CreateButton("SellTab", panel.transform, "Bán", TabIdleColor, 17, out _);
            SetAnchor((RectTransform)sellTab.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(168f, -56f), new Vector2(140f, 36f));
            sellTab.onClick.AddListener(() => SelectTab(false));
            sellTabImage = sellTab.GetComponent<Image>();

            var paper = CreateBox("ListPaper", panel.transform, PaperColor, true, out var paperRect);
            paperRect.anchorMin = Vector2.zero;
            paperRect.anchorMax = Vector2.one;
            paperRect.offsetMin = new Vector2(16f, 16f);
            paperRect.offsetMax = new Vector2(-16f, -100f);

            BuildScrollList(paper.transform);

            emptyText = CreateText("EmptyText", paper.transform, 17, FontStyle.Italic, DarkText, TextAnchor.MiddleCenter);
            StretchFull(emptyText.rectTransform, 20f, 20f);
        }

        private void BuildScrollList(Transform parent)
        {
            var viewport = CreateBox("Viewport", parent, Color.white, true, out var viewportRect);
            StretchFull(viewportRect, 8f, 8f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var content = CreateUIObject("Content", viewport.transform, out var contentRect);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = parent.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            listContent = content.transform;
        }

        private void BuildRow(ShopOffer offer)
        {
            var row = CreateBox("Row_" + offer.title, listContent, RowColor, true, out _);
            row.AddComponent<LayoutElement>().preferredHeight = 60f;

            CreateBox("Icon", row.transform, Color.white, false, out var iconRect, out var iconImage);
            SetAnchor(iconRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(44f, 44f));
            iconImage.sprite = offer.icon;
            iconImage.preserveAspect = true;
            iconImage.enabled = offer.icon != null;

            var title = CreateText("Title", row.transform, 17, FontStyle.Bold, DarkText, TextAnchor.LowerLeft, offer.title);
            SetRowText(title.rectTransform, 0.5f, 1f);

            var detail = CreateText("Detail", row.transform, 13, FontStyle.Normal, new Color(0.42f, 0.27f, 0.15f, 1f), TextAnchor.UpperLeft, offer.detail);
            SetRowText(detail.rectTransform, 0f, 0.5f);

            var price = CreateText("Price", row.transform, 16, FontStyle.Bold, new Color(0.60f, 0.38f, 0.05f, 1f), TextAnchor.MiddleRight, offer.priceText);
            SetAnchor(price.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-206f, 0f), new Vector2(96f, 40f));

            Color actionColor = isBuyTab ? BuyColor : SellColor;
            var main = CreateButton("ActionButton", row.transform, offer.actionLabel, actionColor, 15, out _);
            SetAnchor((RectTransform)main.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-104f, 0f), new Vector2(92f, 38f));
            main.onClick.AddListener(() => RunOffer(offer.action));

            if (offer.secondaryAction != null)
            {
                var second = CreateButton("SecondaryButton", row.transform, offer.secondaryLabel, actionColor, 15, out _);
                SetAnchor((RectTransform)second.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(92f, 38f));
                second.onClick.AddListener(() => RunOffer(offer.secondaryAction));
            }
        }

        private void RunOffer(System.Func<bool> action)
        {
            action?.Invoke();
            Refresh();
        }

        // =====================================================================
        // UI HELPERS
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

        private static GameObject CreateBox(string name, Transform parent, Color color, bool raycast, out RectTransform rect, out Image img)
        {
            var go = CreateUIObject(name, parent, out rect);
            img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return go;
        }

        private Text CreateText(string name, Transform parent, int size, FontStyle style, Color color, TextAnchor align, string initialText = "")
        {
            var go = CreateUIObject(name, parent, out _);
            var txt = go.AddComponent<Text>();
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

        private Button CreateButton(string name, Transform parent, string label, Color color, int fontSize, out Text labelText)
        {
            var go = CreateBox(name, parent, color, true, out _, out var img);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            labelText = CreateText("Label", go.transform, fontSize, FontStyle.Bold, LightText, TextAnchor.MiddleCenter, label);
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
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padX, padY);
            rect.offsetMax = new Vector2(-padX, -padY);
        }

        // Dòng chữ trên cùng của panel, neo theo mép trái/phải
        private static void SetTopRow(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(right, -top);
        }

        // Chữ trong một dòng hàng: chừa chỗ cho icon bên trái, giá và nút bên phải
        private static void SetRowText(RectTransform rect, float minY, float maxY)
        {
            rect.anchorMin = new Vector2(0f, minY);
            rect.anchorMax = new Vector2(1f, maxY);
            rect.offsetMin = new Vector2(66f, 2f);
            rect.offsetMax = new Vector2(-310f, -2f);
        }
    }
}
