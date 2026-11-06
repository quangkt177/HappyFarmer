using UnityEngine;
using UnityEngine.SceneManagement;

namespace HappyFarmer.Gameplay
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class ScenePortal : MonoBehaviour
    {
        [Header("Destination")]
        [SerializeField] private string destinationScene = "PastureScene";
        [SerializeField] private Vector2 targetSpawnPosition = new Vector2(3f, 7.5f);

        [Header("Portal Visuals")]
        [SerializeField] private string portalLabel = "To Pasture";

        public static bool HasPendingSpawn = false;
        public static Vector2 NextSpawnPosition = Vector2.zero;

        private bool isTransitioning = false;

        private void Start()
        {
            var collider = GetComponent<BoxCollider2D>();
            collider.isTrigger = true;

            // If player just arrived in this scene, apply pending spawn position
            if (HasPendingSpawn)
            {
                var player = GameObject.FindWithTag("Player");
                if (player == null) player = GameObject.Find("Player");
                if (player != null)
                {
                    player.transform.position = new Vector3(NextSpawnPosition.x, NextSpawnPosition.y, 0f);
                }
                HasPendingSpawn = false;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isTransitioning) return;

            if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
            {
                isTransitioning = true;
                HasPendingSpawn = true;
                NextSpawnPosition = targetSpawnPosition;
                Debug.Log($"[HappyFarmer] Portal triggered: Loading scene '{destinationScene}' with spawn at {targetSpawnPosition}");
                SceneManager.LoadScene(destinationScene);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            var col = GetComponent<BoxCollider2D>();
            if (col != null)
            {
                Gizmos.DrawCube(transform.position + (Vector3)col.offset, col.size);
            }
        }
    }
}
