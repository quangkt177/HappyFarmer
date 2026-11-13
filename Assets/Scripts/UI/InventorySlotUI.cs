using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image selectionBorderImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private Text countText;
        [SerializeField] private Text keybindText;

        private InventoryUI ownerUI;
        private bool isHovered;
        private bool isSelected;

        public int SlotIndex => slotIndex;

        public void BindRuntime(InventoryUI ui, int index, bool hotbar)
        {
            ownerUI = ui;
            slotIndex = index;
            isHotbarSlot = hotbar;

            if (backgroundImage == null) backgroundImage = GetComponent<Image>();
            if (selectionBorderImage == null) selectionBorderImage = transform.Find("SelectionBorder")?.GetComponent<Image>();
            if (iconImage == null) iconImage = transform.Find("Icon")?.GetComponent<Image>();
            if (countText == null) countText = transform.Find("CountText")?.GetComponent<Text>();
            if (keybindText == null) keybindText = transform.Find("KeybindText")?.GetComponent<Text>();

            if (keybindText != null)
            {
                keybindText.text = hotbar ? (index + 1).ToString() : string.Empty;
                keybindText.gameObject.SetActive(hotbar);
            }
        }

        public void Refresh(Sprite icon, int count, bool selected)
        {
            isSelected = selected;

            if (iconImage != null)
            {
                bool hasItem = icon != null && count > 0;
                iconImage.sprite = hasItem ? icon : null;
                iconImage.color = Color.white;
                iconImage.enabled = hasItem;
            }

            if (countText != null)
            {
                countText.text = count > 1 ? count.ToString() : string.Empty;
                countText.enabled = count > 1;
            }

            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            if (selectionBorderImage != null)
            {
                selectionBorderImage.enabled = isSelected || isHovered;
                selectionBorderImage.color = isSelected
                    ? new Color(0.98f, 0.76f, 0.18f, 1f)   // Vàng kim nổi bật
                    : new Color(1f, 0.92f, 0.65f, 0.75f);  // Sáng nhẹ khi hover
            }

            if (backgroundImage != null)
            {
                if (isSelected) backgroundImage.color = new Color(0.98f, 0.91f, 0.74f, 1f);
                else if (isHovered) backgroundImage.color = new Color(0.93f, 0.84f, 0.67f, 1f);
                else backgroundImage.color = isHotbarSlot
                    ? new Color(0.89f, 0.79f, 0.61f, 1f)
                    : new Color(0.86f, 0.75f, 0.57f, 1f);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (ownerUI == null) return;
            if (eventData.button == PointerEventData.InputButton.Right)
                ownerUI.OnSlotRightClicked(slotIndex);
            else if (eventData.button == PointerEventData.InputButton.Left)
                ownerUI.OnSlotLeftClicked(slotIndex, isHotbarSlot);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (ownerUI != null && eventData.button == PointerEventData.InputButton.Left)
                ownerUI.OnSlotBeginDrag(slotIndex, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (ownerUI != null && eventData.button == PointerEventData.InputButton.Left)
                ownerUI.OnSlotDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData) => ownerUI?.OnSlotEndDrag();

        public void OnDrop(PointerEventData eventData) => ownerUI?.OnSlotDrop(slotIndex);

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
