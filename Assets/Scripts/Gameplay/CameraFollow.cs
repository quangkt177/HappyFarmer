using UnityEngine;

namespace HappyFarmer.Gameplay
{
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        [SerializeField] private float smoothSpeed = 8f;

        [Header("Map Bounds (in world units)")]
        [SerializeField] private Vector2 mapMin = new Vector2(0f, 0f);
        [SerializeField] private Vector2 mapMax = new Vector2(34f, 22f);

        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            if (cam == null) cam = GetComponent<Camera>();

            // Calculate camera viewport extents in world units
            float vertExtent = cam.orthographicSize;
            float horzExtent = vertExtent * cam.aspect;

            // Calculate min and max camera position so viewport never leaves map
            float minX = mapMin.x + horzExtent;
            float maxX = mapMax.x - horzExtent;
            float minY = mapMin.y + vertExtent;
            float maxY = mapMax.y - vertExtent;

            Vector3 desiredPosition = transform.position;

            // Clamp X: if map is narrower than camera view, center on map
            if (minX >= maxX)
            {
                desiredPosition.x = (mapMin.x + mapMax.x) * 0.5f;
            }
            else
            {
                desiredPosition.x = Mathf.Clamp(target.position.x, minX, maxX);
            }

            // Clamp Y: if map is shorter than camera view, center on map
            if (minY >= maxY)
            {
                desiredPosition.y = (mapMin.y + mapMax.y) * 0.5f;
            }
            else
            {
                desiredPosition.y = Mathf.Clamp(target.position.y, minY, maxY);
            }

            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        }
    }
}
