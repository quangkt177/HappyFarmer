using UnityEngine;

namespace HappyFarmer.Gameplay
{
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        [SerializeField] private float smoothSpeed = 5f;
        [SerializeField] private Vector2 minBounds = new Vector2(8f, 6f);
        [SerializeField] private Vector2 maxBounds = new Vector2(26f, 16f);

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desiredPosition = new Vector3(target.position.x, target.position.y, transform.position.z);
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, minBounds.x, maxBounds.x);
            desiredPosition.y = Mathf.Clamp(desiredPosition.y, minBounds.y, maxBounds.y);

            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        }
    }
}
