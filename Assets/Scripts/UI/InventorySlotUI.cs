using UnityEngine;
using UnityEngine.EventSystems;

namespace HappyFarmer.UI
{
    public class InventorySlotUI : MonoBehaviour,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IDropHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] private int slotIndex;
        [SerializeField] private bool isHotbarSlot;
        [SerializeField] private UnityEngine.UI.Image backgroundImage;
        [SerializeField] private UnityEngine.UI.Image selectionBorderImage;
        [SerializeField] private UnityEngine.UI.Image iconImage;
        [SerializeField] private UnityEngine.UI.Text countText;
        [SerializeField] private UnityEngine.UI.Text keybindText;

        private InventoryUI ownerUI;
        private bool isHovered;
        private bool isSelected;

        public int SlotIndex => slotIndex;

        public void Setup(
            InventoryUI ui,
            int index,
            bool hotbar,
            UnityEngine.UI.Image bg,
            UnityEngine.UI.Image border,
            UnityEngine.UI.Image icon,
            UnityEngine.UI.Text count,
            UnityEngine.UI.Text keybind)
        {
            ownerUI = ui;
            slotIndex = index;
            isHotbarSlot = hotbar;
            backgroundImage = bg;
            selectionBorderImage = border;
            iconImage = icon;
            countText = count;
            keybindText = keybind;

            if (keybindText != null)
            {
                keybindText.text = hotbar ? (index + 1).ToString() : string.Empty;
                keybindText.gameObject.SetActive(hotbar);
            }
        }

        public void BindRuntime(InventoryUI ui, int index, bool hotbar)
        {
            ownerUI = ui;
            slotIndex = index;
            isHotbarSlot = hotbar;

            if (backgroundImage == null)
                backgroundImage = GetComponent<UnityEngine.UI.Image>();
            if (selectionBorderImage == null)
                selectionBorderImage = transform.Find("SelectionBorder")?.GetComponent<UnityEngine.UI.Image>();
            if (iconImage == null)
                iconImage = transform.Find("Icon")?.GetComponent<UnityEngine.UI.Image>();
            if (countText == null)
                countText = transform.Find("CountText")?.GetComponent<UnityEngine.UI.Text>();
            if (keybindText == null)
                keybindText = transform.Find("KeybindText")?.GetComponent<UnityEngine.UI.Text>();
        }

        public void Refresh(Sprite icon, int count, bool selected)
        {
            isSelected = selected;

            if (iconImage != null)
            {
                if (icon != null && count > 0)
                {
                    iconImage.sprite = icon;
                    iconImage.color = Color.white;
                    iconImage.enabled = true;
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.enabled = false;
                }
            }

            if (countText != null)
            {
                if (count > 1)
                {
                    countText.text = count.ToString();
                    countText.enabled = true;
                }
                else
                {
                    countText.text = string.Empty;
                    countText.enabled = false;
                }
            }

            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            if (selectionBorderImage != null)
            {
                selectionBorderImage.enabled = isSelected || isHovered;
                if (isSelected)
                {
                    selectionBorderImage.color = new Color(0.98f, 0.76f, 0.18f, 1f); // Vàng kim nổi bật
                }
                else if (isHovered)
                {
                    selectionBorderImage.color = new Color(1f, 0.92f, 0.65f, 0.75f); // Sáng nhẹ khi di chuột
                }
            }

            if (backgroundImage != null)
            {
                if (isSelected)
                {
                    backgroundImage.color = new Color(0.98f, 0.91f, 0.74f, 1f);
                }
                else if (isHovered)
                {
                    backgroundImage.color = new Color(0.93f, 0.84f, 0.67f, 1f);
                }
                else
                {
                    backgroundImage.color = isHotbarSlot
                        ? new Color(0.89f, 0.79f, 0.61f, 1f)
                        : new Color(0.86f, 0.75f, 0.57f, 1f);
                }
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (ownerUI == null) return;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                ownerUI.OnSlotRightClicked(slotIndex);
            }
            else if (eventData.button == PointerEventData.InputButton.Left)
            {
                ownerUI.OnSlotLeftClicked(slotIndex, isHotbarSlot);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (ownerUI == null || eventData.button != PointerEventData.InputButton.Left) return;
            ownerUI.OnSlotBeginDrag(slotIndex, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (ownerUI == null || eventData.button != PointerEventData.InputButton.Left) return;
            ownerUI.OnSlotDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (ownerUI == null) return;
            ownerUI.OnSlotEndDrag();
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (ownerUI == null) return;
            ownerUI.OnSlotDrop(slotIndex);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            UpdateVisualState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            UpdateVisualState();
        }
    }
}
