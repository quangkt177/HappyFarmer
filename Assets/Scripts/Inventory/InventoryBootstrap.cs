using HappyFarmer.UI;
using HappyFarmer.Economy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace HappyFarmer.Inventory
{
    public static class InventoryBootstrap
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

            EnsureInventoryInScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureInventoryInScene();
        }

        public static void EnsureInventoryInScene()
        {
            if (Object.FindObjectOfType<EconomyManager>() == null)
            {
                var econObj = new GameObject("EconomyManager");
                econObj.AddComponent<EconomyManager>();
            }

            if (Object.FindObjectOfType<InventoryManager>() == null)
            {
                var managerObj = new GameObject("InventoryManager");
                managerObj.AddComponent<InventoryManager>();
            }

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<StandaloneInputModule>();
            }

            var existingUi = Object.FindObjectOfType<InventoryUI>();
            if (existingUi != null && !existingUi.HasValidHierarchy)
            {
                // Nếu là Canvas rỗng chưa có UI hierarchy (như ở một số scene cũ), xóa đi để nạp từ Prefab hoàn chỉnh
                Object.DestroyImmediate(existingUi.gameObject);
                existingUi = null;
            }

            if (existingUi == null)
            {
                CreateInventoryCanvas();
            }
            else
            {
                existingUi.EnsureUIHierarchy();
            }
        }

        public static GameObject CreateInventoryCanvas()
        {
            var prefab = Resources.Load<GameObject>("InventoryCanvas")
                         ?? Resources.Load<GameObject>("Prefabs/InventoryCanvas");

#if UNITY_EDITOR
            if (prefab == null)
            {
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/InventoryCanvas.prefab")
                         ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/InventoryCanvas.prefab");
            }
#endif

            if (prefab != null)
            {
                var instance = Object.Instantiate(prefab);
                instance.name = "InventoryCanvas";
                var canvasUi = instance.GetComponent<InventoryUI>();
                if (canvasUi != null) canvasUi.EnsureUIHierarchy();
                return instance;
            }

            Debug.LogWarning("[InventoryBootstrap] Không tìm thấy Prefab InventoryCanvas.");
            return null;
        }
    }
}
