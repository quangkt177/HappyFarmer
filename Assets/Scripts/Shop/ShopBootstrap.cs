using UnityEngine;
using UnityEngine.SceneManagement;

namespace HappyFarmer.Shop
{
    // Gắn logic shop / mở ruộng vào các object có sẵn do FarmMapBuilder và PastureMapBuilder dựng
    public static class ShopBootstrap
    {
        private static bool isSubscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitOnPlayMode()
        {
            if (!isSubscribed)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                SceneManager.sceneLoaded += OnSceneLoaded;
                isSubscribed = true;
            }

            SetupScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SetupScene();
        }

        public static void SetupScene()
        {
            if (Object.FindObjectOfType<ShopUI>() == null) ShopUI.Create();

            AttachStall("Shop Stall", ShopKind.Farm, "Sạp Nông Sản");
            AttachStall("Pasture Stall", ShopKind.Pasture, "Sạp Gia Súc");
            AttachParcel("Parcel East", "Khu Đất Phía Đông");
            AttachParcel("Parcel South", "Khu Đất Phía Nam");

            AnimalMarket.RestoreSceneAnimals();
        }

        private static void AttachStall(string objectName, ShopKind kind, string title)
        {
            var go = GameObject.Find(objectName);
            if (go == null || go.GetComponent<ShopStall>() != null) return;
            go.AddComponent<ShopStall>().Setup(kind, title);
        }

        private static void AttachParcel(string objectName, string title)
        {
            var go = GameObject.Find(objectName);
            if (go == null || go.GetComponent<ParcelUnlock>() != null) return;
            go.AddComponent<ParcelUnlock>().Setup(title);
        }
    }
}
