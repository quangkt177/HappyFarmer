using HappyFarmer.Inventory;
using HappyFarmer.Economy;
using UnityEngine;

namespace HappyFarmer.Shop
{
    // Khu đất bị cỏ dại và đá phủ kín; trả Xu ở tấm biển để dọn sạch và mở ruộng
    public class ParcelUnlock : MonoBehaviour
    {
        [SerializeField] private string displayName = "Khu Đất Mới";
        [SerializeField] private int fallbackPrice = 150;
        [SerializeField] private float interactRadius = 1.4f;
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        private Transform sign;

        public void Setup(string parcelDisplayName)
        {
            displayName = parcelDisplayName;
        }

        private int Price
        {
            get
            {
                var econ = EconomyManager.Instance;
                if (econ != null) return econ.GetParcelPrice(gameObject.name, fallbackPrice);
                var db = InventoryManager.Instance != null ? InventoryManager.Instance.EconomyDatabase : null;
                return db != null ? db.GetParcelPrice(gameObject.name, fallbackPrice) : fallbackPrice;
            }
        }

        private void Start()
        {
            sign = transform.Find("sign");
            if (FarmSession.UnlockedParcels.Contains(gameObject.name)) ClearOvergrowth();
        }

        private void Update()
        {
            var ui = ShopUI.Instance;
            var manager = InventoryManager.Instance;
            if (ui == null || manager == null || manager.PlayerTransform == null) return;

            Vector2 anchor = sign != null ? sign.position : transform.position;
            if (Vector2.Distance(manager.PlayerTransform.position, anchor) > interactRadius)
            {
                ui.HidePrompt(this);
                return;
            }

            int price = Price;
            ui.ShowPrompt(this, $"[{interactKey}] Mở {displayName} ({manager.FormatCurrency(price)})");
            if (Input.GetKeyDown(interactKey)) TryUnlock(manager, price);
        }

        private void TryUnlock(InventoryManager manager, int price)
        {
            var econ = EconomyManager.Instance;
            bool success = econ != null ? econ.TrySpendCurrency(price) : manager.TrySpendCurrency(price);
            string curName = econ != null ? econ.CurrencyName : manager.CurrencyName;
            string curFmt = econ != null ? econ.FormatCurrency(price) : manager.FormatCurrency(price);

            if (!success)
            {
                manager.Notify($"Không đủ {curName}! Cần {curFmt} để mở {displayName}.");
                return;
            }

            FarmSession.UnlockedParcels.Add(gameObject.name);
            ClearOvergrowth();
            manager.Notify($"Đã mở {displayName} (-{curFmt})!");
        }

        private void ClearOvergrowth()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            if (ShopUI.Instance != null) ShopUI.Instance.HidePrompt(this);
            enabled = false;
        }
    }
}
