using HappyFarmer.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HappyFarmer.Inventory
{
    public static class InventoryBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInventoryInScene()
        {
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

            if (Object.FindObjectOfType<InventoryUI>() == null)
            {
                CreateInventoryCanvas();
            }
        }

        public static GameObject CreateInventoryCanvas()
        {
            var canvasObj = new GameObject("InventoryCanvas");
            int uiLayer = LayerMask.NameToLayer("UI");
            canvasObj.layer = uiLayer >= 0 ? uiLayer : 5;

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var ui = canvasObj.AddComponent<InventoryUI>();
            ui.BuildUIHierarchy();

            return canvasObj;
        }
    }
}
