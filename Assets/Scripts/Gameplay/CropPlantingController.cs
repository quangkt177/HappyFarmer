using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using HappyFarmer.Inventory;

namespace HappyFarmer.Gameplay
{
    /// <summary>
    /// Điều khiển việc tương tác và trồng hạt giống trên các ô đất (mặc định: dirt_c).
    /// Hiển thị viền đỏ bao quanh ô đất đang được nhắm tới, không dùng UI banner.
    /// Hỗ trợ cả Tilemap lẫn GameObject ô đất có Collider.
    /// Có thể tự động sinh cây mầm (CropPlant) lớn dần và thu hoạch bằng phím E.
    /// </summary>
    public class CropPlantingController : MonoBehaviour
    {
        [SerializeField] private Transform playerTransform;
        [SerializeField] private GameObject cropPlantPrefab;

        private Tilemap groundTilemap;

        private string targetDirtName = "dirt_c";
        private bool showSelectionHighlight = true;
        private Color highlightColor = Color.red;
        private int borderThickness = 2;
        private string highlightSortingLayer = "World";
        private int highlightSortingOrder = 10;
        private KeyCode plantKey = KeyCode.E;
        private float interactRadius = 1.3f;
        private float checkDistance = 0.8f;
        private bool consumeSeedOnPlant = true;
        private bool preventDuplicatePlanting = true;
        private bool spawnCropVisual = true;


        // Lưu trữ các tọa độ ô đất đã gieo hạt
        private readonly HashSet<Vector3Int> plantedTilePositions = new HashSet<Vector3Int>();
        private readonly HashSet<int> plantedObjectIds = new HashSet<int>();

        private PlayerController playerController;

        // Đối tượng vẽ viền đỏ
        private GameObject highlightObj;
        private SpriteRenderer highlightRenderer;
        private Texture2D highlightTexture;
        private Sprite highlightSprite;

        private void Awake()
        {
            ResolveReferences();
            EnsureHighlightObject();
        }

        private void Start()
        {
            ResolveReferences();
            EnsureHighlightObject();
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                ResolveReferences();
                if (playerTransform == null) return;
            }

            // Kiểm tra xem có ô đất dirt_c ở gần không
            bool hasDirt = TryFindNearbyDirt(out Vector3Int tileCell, out GameObject dirtObject, out Vector3 worldPos, out string dirtLabel);

            // Cập nhật vị trí viền đỏ quanh ô đất được chọn
            UpdateSelectionHighlight(hasDirt, worldPos);

            // Xử lý tưới nước khi người chơi dùng bình tưới (Phím 2, K, Space hoặc Click chuột)
            HandleWateringInput(hasDirt, worldPos);

            // Xử lý dùng liềm dọn cây bị héo (Phím 3, L, Space hoặc Click chuột khi cầm liềm)
            HandleScytheInput(hasDirt, worldPos, tileCell, dirtObject);

            // Xử lý phang cuốc vào cây để phá cây (Phím 1, J, Space hoặc Click chuột khi cầm cuốc)
            HandleHoeInput(hasDirt, worldPos, tileCell, dirtObject);

            // Bấm E để trồng hạt giống hoặc tương tác với cây
            if (hasDirt && Input.GetKeyDown(plantKey))
            {
                HandlePlantOrInteract(tileCell, dirtObject, worldPos, dirtLabel);
            }
        }

        private void OnDisable()
        {
            if (highlightObj != null)
            {
                highlightObj.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (highlightObj != null) Destroy(highlightObj);
            if (highlightTexture != null) Destroy(highlightTexture);
            if (highlightSprite != null) Destroy(highlightSprite);
        }



        /// <summary>
        /// Tạo GameObject và Sprite viền vuông rỗng tự động bằng code (không cần tạo asset ngoài)
        /// </summary>
        private void EnsureHighlightObject()
        {
            if (highlightObj != null) return;

            highlightObj = new GameObject("DirtSelectionBorder");
            highlightObj.transform.SetParent(transform, false);
            highlightObj.hideFlags = HideFlags.DontSave;

            highlightRenderer = highlightObj.AddComponent<SpriteRenderer>();
            highlightRenderer.sortingLayerName = highlightSortingLayer;
            highlightRenderer.sortingOrder = highlightSortingOrder;

            CreateHighlightSprite();
            highlightObj.SetActive(false);
        }

        /// <summary>
        /// Tạo texture viền vuông rỗng (32x32) với độ dày borderThickness
        /// </summary>
        private void CreateHighlightSprite()
        {
            int size = 32;
            int thickness = Mathf.Clamp(borderThickness, 1, 6);

            if (highlightTexture != null) Destroy(highlightTexture);
            if (highlightSprite != null) Destroy(highlightSprite);

            highlightTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            highlightTexture.filterMode = FilterMode.Point;
            highlightTexture.wrapMode = TextureWrapMode.Clamp;

            Color transparent = new Color(0, 0, 0, 0);
            Color solidWhite = Color.white;

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    bool isBorder = x < thickness || x >= size - thickness ||
                                    y < thickness || y >= size - thickness;
                    highlightTexture.SetPixel(x, y, isBorder ? solidWhite : transparent);
                }
            }
            highlightTexture.Apply();

            highlightSprite = Sprite.Create(
                highlightTexture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                size
            );

            if (highlightRenderer != null)
            {
                highlightRenderer.sprite = highlightSprite;
                highlightRenderer.color = highlightColor;
            }
        }

        /// <summary>
        /// Hiển thị hoặc ẩn viền đỏ quanh tọa độ ô đất đang nhắm tới
        /// </summary>
        private void UpdateSelectionHighlight(bool hasDirt, Vector3 worldPos)
        {
            if (!showSelectionHighlight)
            {
                if (highlightObj != null && highlightObj.activeSelf)
                {
                    highlightObj.SetActive(false);
                }
                return;
            }

            EnsureHighlightObject();

            if (hasDirt)
            {
                highlightObj.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);
                if (groundTilemap != null)
                {
                    highlightObj.transform.localScale = groundTilemap.cellSize;
                }
                else
                {
                    highlightObj.transform.localScale = Vector3.one;
                }

                highlightRenderer.color = highlightColor;

                if (!highlightObj.activeSelf)
                {
                    highlightObj.SetActive(true);
                }
            }
            else
            {
                if (highlightObj.activeSelf)
                {
                    highlightObj.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Tự động tìm kiếm Player, PlayerController và Tilemap nếu người dùng chưa gán trong Inspector
        /// </summary>
        private void ResolveReferences()
        {
            if (playerTransform == null || string.IsNullOrEmpty(playerTransform.gameObject.scene.name))
            {
                if (GetComponent<PlayerController>() != null)
                {
                    playerTransform = transform;
                }
                else if (InventoryManager.Instance != null && InventoryManager.Instance.PlayerTransform != null)
                {
                    playerTransform = InventoryManager.Instance.PlayerTransform;
                }
                else
                {
                    var playerObj = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
                    if (playerObj != null)
                    {
                        playerTransform = playerObj.transform;
                    }
                }
            }

            if (playerTransform != null && playerController == null)
            {
                playerController = playerTransform.GetComponent<PlayerController>();
            }

            if (groundTilemap == null)
            {
                // Ưu tiên tìm GameObject có tên "Paths" (nơi FarmMapBuilder vẽ các ô đất ruộng)
                var pathsObj = GameObject.Find("Paths");
                if (pathsObj != null)
                {
                    groundTilemap = pathsObj.GetComponent<Tilemap>();
                }

                // Nếu không có, tìm bất kỳ Tilemap nào trong Scene
                if (groundTilemap == null)
                {
                    groundTilemap = FindObjectOfType<Tilemap>();
                }
            }
        }

        /// <summary>
        /// Tìm ô đất dirt_c gần người chơi (ưu tiên hướng nhìn, sau đó dưới chân, sau đó xung quanh)
        /// </summary>
        private bool TryFindNearbyDirt(out Vector3Int tileCell, out GameObject dirtObject, out Vector3 worldPos, out string dirtLabel)
        {
            tileCell = Vector3Int.zero;
            dirtObject = null;
            worldPos = Vector3.zero;
            dirtLabel = string.Empty;

            if (playerTransform == null) return false;

            // 1. Kiểm tra trên Tilemap trước
            if (groundTilemap != null)
            {
                Vector2 facingDir = playerController != null ? playerController.LastDirection : Vector2.down;
                if (facingDir == Vector2.zero) facingDir = Vector2.down;

                // 1.1. Kiểm tra ô ngay trước mặt người chơi
                Vector3 frontPos = playerTransform.position + (Vector3)(facingDir.normalized * checkDistance);
                Vector3Int frontCell = groundTilemap.WorldToCell(frontPos);
                Vector3 frontCenter = groundTilemap.GetCellCenterWorld(frontCell);
                if (IsMatchingTile(frontCell) && IsTileEmptyOrCrop(frontCell, frontCenter))
                {
                    tileCell = frontCell;
                    worldPos = frontCenter;
                    dirtLabel = $"ô đất {targetDirtName} tại tọa độ ô [{frontCell.x}, {frontCell.y}]";
                    return true;
                }

                // 1.2. Kiểm tra ô ngay dưới chân người chơi
                Vector3Int feetCell = groundTilemap.WorldToCell(playerTransform.position);
                Vector3 feetCenter = groundTilemap.GetCellCenterWorld(feetCell);
                if (IsMatchingTile(feetCell) && IsTileEmptyOrCrop(feetCell, feetCenter))
                {
                    tileCell = feetCell;
                    worldPos = feetCenter;
                    dirtLabel = $"ô đất {targetDirtName} dưới chân [{feetCell.x}, {feetCell.y}]";
                    return true;
                }

                // 1.3. Quét các ô xung quanh trong phạm vi interactRadius
                Vector3Int centerCell = groundTilemap.WorldToCell(playerTransform.position);
                int cellRadius = Mathf.CeilToInt(interactRadius);
                float minDistance = float.MaxValue;
                bool foundTile = false;

                for (int x = -cellRadius; x <= cellRadius; x++)
                {
                    for (int y = -cellRadius; y <= cellRadius; y++)
                    {
                        Vector3Int testCell = new Vector3Int(centerCell.x + x, centerCell.y + y, 0);
                        Vector3 centerWorld = groundTilemap.GetCellCenterWorld(testCell);
                        if (IsMatchingTile(testCell) && IsTileEmptyOrCrop(testCell, centerWorld))
                        {
                            float dist = Vector2.Distance(playerTransform.position, centerWorld);
                            if (dist <= interactRadius && dist < minDistance)
                            {
                                minDistance = dist;
                                tileCell = testCell;
                                worldPos = centerWorld;
                                dirtLabel = $"ô đất {targetDirtName} gần nhất [{testCell.x}, {testCell.y}]";
                                foundTile = true;
                            }
                        }
                    }
                }

                if (foundTile) return true;
            }

            // 2. Kiểm tra nếu có GameObject ô đất với Collider trong Scene
            Collider2D[] hits = Physics2D.OverlapCircleAll(playerTransform.position, interactRadius);
            float nearestDist = float.MaxValue;
            foreach (var hit in hits)
            {
                if (hit == null || hit.transform == playerTransform) continue;

                // Kiểm tra theo tên GameObject (không dùng CompareTag để tránh Unity in lỗi Tag: dirt_c is not defined)
                bool isMatch = hit.name.IndexOf(targetDirtName, System.StringComparison.OrdinalIgnoreCase) >= 0;

                if (isMatch && IsTileEmptyOrCrop(Vector3Int.zero, hit.transform.position))
                {
                    float dist = Vector2.Distance(playerTransform.position, hit.transform.position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        dirtObject = hit.gameObject;
                        worldPos = hit.transform.position;
                        dirtLabel = $"GameObject ô đất '{hit.name}'";
                    }
                }
            }

            return dirtObject != null;
        }

        /// <summary>
        /// Kiểm tra Tile tại ô cell có đúng là loại đất cần tìm (dirt_c) hay không
        /// </summary>
        private bool IsMatchingTile(Vector3Int cell)
        {
            if (groundTilemap == null) return false;
            TileBase tile = groundTilemap.GetTile(cell);
            if (tile == null) return false;

            // Kiểm tra tên của TileBase asset (ví dụ dirt_c)
            if (tile.name.Equals(targetDirtName, System.StringComparison.OrdinalIgnoreCase) ||
                tile.name.Contains(targetDirtName))
            {
                return true;
            }

            // Kiểm tra tên Sprite bên trong Tile (nếu tile là Tile thông thường)
            if (tile is Tile unityTile && unityTile.sprite != null)
            {
                if (unityTile.sprite.name.Equals(targetDirtName, System.StringComparison.OrdinalIgnoreCase) ||
                    unityTile.sprite.name.Contains(targetDirtName))
                {
                    return true;
                }
            }

            return false;
        }

        private Transform cachedParcels;

        private Transform GetParcelsTransform()
        {
            if (cachedParcels == null)
            {
                var p = GameObject.Find("Parcels") ?? GameObject.Find("World/Parcels");
                if (p != null) cachedParcels = p.transform;
            }
            return cachedParcels;
        }

        /// <summary>
        /// Kiểm tra xem ô đất tại cellPos có hợp lệ để hiển thị viền đỏ và gieo trồng hay không.
        /// Hợp lệ khi:
        /// 1. Ô đất hoàn toàn TRỐNG (sẵn sàng gieo hạt, không bị vật cản che khuất).
        /// 2. HOẶC trên ô đất đang có CROP PLANT.
        /// </summary>
        private bool IsTileEmptyOrCrop(Vector3Int cellPos, Vector3 cellCenter)
        {
            // 1. Nếu ô này đã được ghi nhận có CropPlant theo danh sách quản lý -> Hợp lệ!
            if (cellPos != Vector3Int.zero && plantedTilePositions.Contains(cellPos))
            {
                return true;
            }

            // 2. Quét các Collider2D tại vị trí ô đất
            Collider2D[] allColliders = Physics2D.OverlapBoxAll(cellCenter, new Vector2(0.6f, 0.6f), 0f);
            foreach (var col in allColliders)
            {
                if (col == null) continue;

                // Nếu là CropPlant -> Hợp lệ!
                if (col.GetComponent<CropPlant>() != null || col.GetComponentInParent<CropPlant>() != null || col.name.StartsWith("Crop_"))
                {
                    return true;
                }
            }

            // Kiểm tra thêm theo vị trí nếu CropPlant tạo từ prefab chưa lưu collider
            Collider2D circleHit = Physics2D.OverlapCircle(cellCenter, 0.4f);
            if (circleHit != null && (circleHit.GetComponent<CropPlant>() != null || circleHit.GetComponentInParent<CropPlant>() != null || circleHit.name.StartsWith("Crop_")))
            {
                return true;
            }

            // 3. Kiểm tra xem ô đất có bị chặn bởi VẬT CẢN (Obstacle) không
            foreach (var col in allColliders)
            {
                if (col == null) continue;

                // BỎ QUA NGƯỜI CHƠI (tuyệt đối không nhầm Player là vật cản!)
                if (IsPlayerCollider(col)) continue;

                // Bỏ qua khung viền đỏ
                if (highlightObj != null && col.gameObject == highlightObj) continue;

                // Bỏ qua các trigger tương tác (nếu không phải vật cản đặc)
                if (col.isTrigger) continue;

                // Nếu là một Collider rắn (solid) -> Đây là vật cản (đá, tường, nhà...)!
                return false;
            }

            // 4. Kiểm tra các khu đất Parcel chưa mở (Parcels chứa bụi rậm, đá chưa dọn)
            Transform parcelsTrans = GetParcelsTransform();
            if (parcelsTrans != null)
            {
                for (int p = 0; p < parcelsTrans.childCount; p++)
                {
                    Transform parcel = parcelsTrans.GetChild(p);
                    // Chỉ kiểm tra khi khu đất Parcel chưa mở
                    var unlock = parcel.GetComponent<Shop.ParcelUnlock>();
                    if (unlock != null && unlock.enabled && parcel.gameObject.activeInHierarchy)
                    {
                        for (int i = 0; i < parcel.childCount; i++)
                        {
                            Transform prop = parcel.GetChild(i);
                            if (prop.name.Equals("sign", System.StringComparison.OrdinalIgnoreCase)) continue;

                            if (Mathf.Abs(prop.position.x - cellCenter.x) < 0.45f &&
                                Mathf.Abs(prop.position.y - cellCenter.y) < 0.45f)
                            {
                                return false; // Ô đất đang bị bụi cây/đá của Parcel chưa mở che!
                            }
                        }
                    }
                }
            }

            // Nếu không có bất kỳ vật cản nào -> Ô ĐẤT HOÀN TOÀN TRỐNG! Hợp lệ vẽ viền đỏ!
            return true;
        }

        private bool IsPlayerCollider(Collider2D col)
        {
            if (col == null) return false;
            if (col.transform == transform || col.transform.IsChildOf(transform)) return true;
            if (playerTransform != null && (col.transform == playerTransform || col.transform.IsChildOf(playerTransform))) return true;
            if (col.CompareTag("Player")) return true;
            if (col.GetComponent<PlayerController>() != null || col.GetComponentInParent<PlayerController>() != null) return true;
            return false;
        }

        /// <summary>
        /// Xử lý thao tác tưới nước bằng bình tưới (Phím 2, K, Space hoặc Click chuột trái)
        /// </summary>
        private void HandleWateringInput(bool hasDirt, Vector3 targetWorldPos)
        {
            if (!hasDirt) return;

            bool isWateringKey = Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2) || Input.GetKeyDown(KeyCode.K);
            bool isUseToolKey = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
            bool isWateringTool = playerController != null && playerController.SelectedTool == PlayerController.ToolType.WateringCan;

            if (isWateringKey || (isUseToolKey && isWateringTool))
            {
                CropPlant crop = GetCropAtPosition(targetWorldPos);
                if (crop != null && !crop.IsMature)
                {
                    if (crop.TryWater())
                    {
                        if (playerController != null && !playerController.IsActing)
                        {
                            playerController.PerformAction(PlayerController.ToolType.WateringCan);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Xử lý thao tác dùng liềm dọn cây bị héo (Phím 3, L, Space hoặc Click chuột trái khi cầm liềm)
        /// </summary>
        private void HandleScytheInput(bool hasDirt, Vector3 targetWorldPos, Vector3Int tileCell, GameObject dirtObject)
        {
            bool isScytheKey = Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3) || Input.GetKeyDown(KeyCode.L);
            bool isUseToolKey = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
            bool isScytheTool = playerController != null && playerController.SelectedTool == PlayerController.ToolType.Scythe;

            if (isScytheKey || (isUseToolKey && isScytheTool))
            {
                CropPlant crop = hasDirt ? GetCropAtPosition(targetWorldPos) : null;
                if (crop == null)
                {
                    crop = FindNearbyCrop();
                }

                if (crop != null)
                {
                    if (crop.IsWilted)
                    {
                        Vector3 cropPos = crop.transform.position;
                        crop.ClearWiltedCrop();

                        if (groundTilemap != null)
                        {
                            plantedTilePositions.Remove(groundTilemap.WorldToCell(cropPos));
                        }
                        if (tileCell != Vector3Int.zero)
                        {
                            plantedTilePositions.Remove(tileCell);
                        }
                        if (dirtObject != null)
                        {
                            plantedObjectIds.Remove(dirtObject.GetInstanceID());
                        }

                        if (playerController != null && !playerController.IsActing)
                        {
                            playerController.PerformAction(PlayerController.ToolType.Scythe);
                        }
                    }
                    else
                    {
                        Debug.Log($"[Liềm] Cây <b>{crop.CropDisplayName}</b> vẫn còn tươi tốt, không cần dùng liềm! Hãy dùng Cuốc (phím 1) nếu muốn phá bỏ.");
                    }
                }
            }
        }

        /// <summary>
        /// Xử lý thao tác dùng cuốc phang vào cây để phá cây (Phím 1, J, Space hoặc Click chuột trái khi cầm cuốc)
        /// </summary>
        private void HandleHoeInput(bool hasDirt, Vector3 targetWorldPos, Vector3Int tileCell, GameObject dirtObject)
        {
            bool isHoeKey = Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.J);
            bool isUseToolKey = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
            bool isHoeTool = playerController != null && playerController.SelectedTool == PlayerController.ToolType.Hoe;

            if (isHoeKey || (isUseToolKey && isHoeTool))
            {
                CropPlant crop = hasDirt ? GetCropAtPosition(targetWorldPos) : null;
                if (crop == null)
                {
                    crop = FindNearbyCrop();
                }

                if (crop != null)
                {
                    // Cây bị héo thì không thể dùng cuốc, mà phải dùng liềm
                    if (crop.IsWilted)
                    {
                        Debug.LogWarning($"[Cuốc Đất] <b>{crop.CropDisplayName}</b> đã bị héo khô! Bạn cần dùng <b>Liềm (Phím 3 / L)</b> để dọn dẹp.");
                        return;
                    }

                    Vector3 cropPos = crop.transform.position;
                    crop.DestroyCrop();

                    if (groundTilemap != null)
                    {
                        plantedTilePositions.Remove(groundTilemap.WorldToCell(cropPos));
                    }
                    if (tileCell != Vector3Int.zero)
                    {
                        plantedTilePositions.Remove(tileCell);
                    }
                    if (dirtObject != null)
                    {
                        plantedObjectIds.Remove(dirtObject.GetInstanceID());
                    }

                    if (playerController != null && !playerController.IsActing)
                    {
                        playerController.PerformAction(PlayerController.ToolType.Hoe);
                    }
                }
            }
        }

        private CropPlant FindNearbyCrop()
        {
            if (playerTransform == null) return null;
            Vector2 facingDir = playerController != null ? playerController.LastDirection : Vector2.down;
            if (facingDir == Vector2.zero) facingDir = Vector2.down;
            Vector3 frontPos = playerTransform.position + (Vector3)(facingDir.normalized * checkDistance);

            CropPlant crop = GetCropAtPosition(frontPos);
            if (crop != null) return crop;

            return GetCropAtPosition(playerTransform.position);
        }

        /// <summary>
        /// Xử lý khi bấm phím E: Nếu đã có cây thì thu hoạch/tưới/cuốc/liềm/xem thông tin, nếu ô trống thì gieo hạt
        /// </summary>
        private void HandlePlantOrInteract(Vector3Int tileCell, GameObject dirtObject, Vector3 worldPos, string dirtLabel)
        {
            CropPlant existingCrop = GetCropAtPosition(worldPos);
            if (existingCrop != null)
            {
                // 1. Cây đã bị héo
                if (existingCrop.IsWilted)
                {
                    if (playerController != null && playerController.SelectedTool == PlayerController.ToolType.Scythe)
                    {
                        // Đang cầm liềm mà bấm E -> Dọn cây héo
                        Vector3 cropPos = existingCrop.transform.position;
                        existingCrop.ClearWiltedCrop();

                        if (groundTilemap != null)
                        {
                            plantedTilePositions.Remove(groundTilemap.WorldToCell(cropPos));
                        }
                        if (tileCell != Vector3Int.zero)
                        {
                            plantedTilePositions.Remove(tileCell);
                        }
                        if (dirtObject != null)
                        {
                            plantedObjectIds.Remove(dirtObject.GetInstanceID());
                        }

                        if (!playerController.IsActing)
                        {
                            playerController.PerformAction(PlayerController.ToolType.Scythe);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[Cây Héo] <b>{existingCrop.CropDisplayName}</b> đã bị héo khô! Hãy đổi sang <b>Liềm (Phím 3 / L)</b> để dọn dẹp ô đất.");
                    }
                    return;
                }

                // 2. Nếu đang cầm Cuốc mà bấm E vào cây -> Dùng cuốc phá cây
                if (playerController != null && playerController.SelectedTool == PlayerController.ToolType.Hoe)
                {
                    Vector3 cropPos = existingCrop.transform.position;
                    existingCrop.DestroyCrop();

                    if (groundTilemap != null)
                    {
                        plantedTilePositions.Remove(groundTilemap.WorldToCell(cropPos));
                    }
                    if (tileCell != Vector3Int.zero)
                    {
                        plantedTilePositions.Remove(tileCell);
                    }
                    if (dirtObject != null)
                    {
                        plantedObjectIds.Remove(dirtObject.GetInstanceID());
                    }

                    if (playerController != null && !playerController.IsActing)
                    {
                        playerController.PerformAction(PlayerController.ToolType.Hoe);
                    }
                    return;
                }

                // 3. Nếu cây đã chín -> Thu hoạch
                if (existingCrop.IsMature)
                {
                    existingCrop.Harvest();
                }
                // 4. Nếu đang cầm bình tưới mà bấm E -> Tưới nước
                else if (playerController != null && playerController.SelectedTool == PlayerController.ToolType.WateringCan)
                {
                    if (existingCrop.TryWater())
                    {
                        playerController.PerformAction(PlayerController.ToolType.WateringCan);
                    }
                }
                // 5. Ngược lại -> Xem thông tin cây trồng
                else
                {
                    string soilStatus = existingCrop.IsSoilWet ? "<color=#2196F3>Đất đang ướt (cây đang lớn)</color>" : "<color=#FF9800>Đất đang khô (cần tưới nước để lớn tiếp)</color>";
                    float remainingDry = Mathf.Max(0f, existingCrop.DryWiltDuration - existingCrop.DryTimer);
                    string wiltWarning = (!existingCrop.IsSoilWet && existingCrop.CurrentStage >= 1)
                        ? $" - <color=#FF5722>Cảnh báo: Còn {remainingDry:F0}s trước khi bị héo!</color>"
                        : "";
                    Debug.Log($"[Cây Trồng] <b>{existingCrop.CropDisplayName}</b> (Giai đoạn {existingCrop.CurrentStage}/4) - Trạng thái: {soilStatus}{wiltWarning}");
                }
                return;
            }

            // Nếu chưa có cây -> Gieo hạt giống
            PlantCurrentSeed(tileCell, dirtObject, worldPos, dirtLabel);
        }

        /// <summary>
        /// Tìm CropPlant tại vị trí ô đất
        /// </summary>
        private CropPlant GetCropAtPosition(Vector3 worldPos)
        {
            Collider2D[] colliders = Physics2D.OverlapBoxAll(worldPos, new Vector2(0.7f, 0.7f), 0f);
            foreach (var col in colliders)
            {
                if (col == null) continue;
                var crop = col.GetComponent<CropPlant>() ?? col.GetComponentInParent<CropPlant>();
                if (crop != null) return crop;
            }

            // Fallback tìm theo khoảng cách gần nhất
            foreach (var crop in FindObjectsOfType<CropPlant>())
            {
                if (Vector2.Distance(crop.transform.position, worldPos) < 0.6f)
                {
                    return crop;
                }
            }

            return null;
        }

        /// <summary>
        /// Xử lý logic gieo hạt khi người chơi bấm E
        /// </summary>
        private void PlantCurrentSeed(Vector3Int tileCell, GameObject dirtObject, Vector3 worldPos, string dirtLabel)
        {
            var inv = InventoryManager.Instance;
            var activeItem = inv != null ? inv.ActiveItemData : null;
            var activeSlot = inv != null ? inv.ActiveSlot : null;

            // Kiểm tra xem đã trồng ô này chưa nếu bật cờ preventDuplicatePlanting
            if (preventDuplicatePlanting)
            {
                if (dirtObject != null && plantedObjectIds.Contains(dirtObject.GetInstanceID()))
                {
                    Debug.LogWarning($"[HappyFarmer] {dirtLabel} đã được gieo hạt rồi!");
                    return;
                }
                if (groundTilemap != null && plantedTilePositions.Contains(tileCell))
                {
                    Debug.LogWarning($"[HappyFarmer] {dirtLabel} đã được gieo hạt rồi!");
                    return;
                }
            }

            // Kiểm tra người chơi có đang cầm hạt giống không
            bool isSeed = activeItem != null && 
                          (activeItem.category == ItemCategory.Seed || activeItem.id.StartsWith("seed_"));

            if (!isSeed)
            {
                if (activeItem == null)
                {
                    Debug.LogWarning($"[HappyFarmer] Bạn đang đứng gần {dirtLabel} nhưng chưa chọn vật phẩm nào! Hãy chọn hạt giống trên thanh hotbar để trồng.");
                }
                else
                {
                    Debug.LogWarning($"[HappyFarmer] Vật phẩm đang chọn là '{activeItem.displayName}', không phải hạt giống! Không thể gieo trồng.");
                }
                return;
            }

            // Ghi Log trồng hạt giống theo yêu cầu (chỉ ghi log Console, không dùng banner)
            Debug.Log($"<color=#4CAF50><b>[HappyFarmer - Trồng Cây]</b></color> Đang trồng: <b>{activeItem.displayName}</b> (ID: {activeItem.id}, Cây: {activeItem.cropKey}) vào {dirtLabel} tại vị trí World {worldPos}!");

            // Đánh dấu ô đã trồng
            if (dirtObject != null)
            {
                plantedObjectIds.Add(dirtObject.GetInstanceID());
            }
            else
            {
                plantedTilePositions.Add(tileCell);
            }

            // Sinh cây trồng hiển thị quá trình lớn lên nếu bật tính năng
            if (spawnCropVisual)
            {
                SpawnCrop(activeItem, worldPos, tileCell, dirtObject);
            }

            // Trừ số lượng hạt giống nếu bật tùy chọn
            if (consumeSeedOnPlant && inv != null)
            {
                inv.RemoveItemAt(inv.SelectedHotbarIndex, 1);
            }
        }

        /// <summary>
        /// Tạo cây trồng tại ô đất vừa gieo hạt
        /// </summary>
        private void SpawnCrop(InventoryItemData seedData, Vector3 worldPos, Vector3Int tileCell, GameObject dirtObject)
        {
            GameObject cropObj;
            if (cropPlantPrefab != null)
            {
                cropObj = Instantiate(cropPlantPrefab, new Vector3(worldPos.x, worldPos.y, 0f), Quaternion.identity);
            }
            else
            {
                return;
            }

            var crop = cropObj.GetComponent<CropPlant>();
            if (crop == null)
            {
                crop = cropObj.AddComponent<CropPlant>();
            }

            crop.Initialize(seedData, (harvestedCrop) =>
            {
                // Khi thu hoạch xong, giải phóng ô đất để người chơi có thể trồng lại
                if (groundTilemap != null)
                {
                    plantedTilePositions.Remove(tileCell);
                }
                if (dirtObject != null)
                {
                    plantedObjectIds.Remove(dirtObject.GetInstanceID());
                }
            });
        }

        private void OnDrawGizmosSelected()
        {
            Transform origin = playerTransform != null ? playerTransform : transform;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(origin.position, interactRadius);

            Vector2 dir = playerController != null ? playerController.LastDirection : Vector2.down;
            if (dir == Vector2.zero) dir = Vector2.down;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(origin.position, origin.position + (Vector3)(dir.normalized * checkDistance));
        }
    }
}
