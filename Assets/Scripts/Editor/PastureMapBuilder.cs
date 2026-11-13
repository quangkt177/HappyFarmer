using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using HappyFarmer.Gameplay;

namespace HappyFarmer.EditorTools
{
    [InitializeOnLoad]
    public static class PastureMapBuilder
    {
        private const string ScenePath = "Assets/Scenes/PastureScene.unity";
        private const string FarmScenePath = "Assets/Scenes/FarmMap.unity";
        private const string GroundLayer = "Ground";
        private const string WorldLayer = "World";
        private const int MapWidth = 34;
        private const int MapHeight = 22;

        static PastureMapBuilder()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            const string flagPath = "Temp/pasture_with_stall_v1.flag";
            if (File.Exists(flagPath)) return;

            bool needProps = !Directory.Exists("Assets/Sprites/Barns") ||
                             Directory.GetFiles("Assets/Sprites/Barns", "*.png").Length < 5;

            if (needProps)
            {
                Debug.Log("[HappyFarmer] Step 1: Generating barn pixel-art props...");
                BarnPropGenerator.GenerateAllBarnProps();
                AnimalSpriteGenerator.GenerateAllSprites();
                SliceAnimals();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorApplication.delayCall += OnEditorReady;
                return;
            }

            File.WriteAllText(flagPath, "done");
            Debug.Log("[HappyFarmer] Step 2: Building PastureScene with exact reference coops & pastures...");
            BuildPastureEverything();
        }

        [MenuItem("Happy Farmer/4. Build Pasture Scene (Animals & Pens)", false, 4)]
        public static void BuildPastureEverything()
        {
            EnsureFolder("Assets/Scenes");

            // 1. Generate props matching user reference images
            BarnPropGenerator.GenerateAllBarnProps();
            AnimalSpriteGenerator.GenerateAllSprites();

            // 2. Slice animal sprites
            SliceAnimals();

            // 3. Load all sprites and tiles
            var sprites = LoadAllSprites();
            var tiles = LoadAllTiles(sprites);

            // 4. Build PastureScene exactly matching Image 1 & 2
            BuildPastureScene(sprites, tiles);

            // 5. Update Build Settings
            UpdateBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HappyFarmer] SUCCESS: PastureScene built matching user reference images!");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void UpdateBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(FarmScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true)
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void SliceAnimals()
        {
            var chickenPath = "Assets/Sprites/LittleDreamyland/Sprites/Animals/CHICKEN/Chicken_Idle.png";
            SliceSheet48x48(chickenPath, "chicken_idle", new Vector2(0.5f, 0.25f));

            var cowPath = "Assets/Sprites/LittleDreamyland/Sprites/Animals/COW/Cow_Idle.png";
            SliceSheet48x48(cowPath, "cow_idle", new Vector2(0.5f, 0.2f));

            AssetDatabase.Refresh();
        }

        private static void SliceSheet48x48(string path, string prefix, Vector2 pivot)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;

            var bytes = File.ReadAllBytes(path);
            int width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
            int height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];

            int cols = width / 48;
            int rows = height / 48;

            var list = new List<SpriteMetaData>();
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    list.Add(new SpriteMetaData
                    {
                        name = $"{prefix}_{r}_{c}",
                        rect = new Rect(c * 48, height - (r + 1) * 48, 48, 48),
                        alignment = (int)SpriteAlignment.Custom,
                        pivot = pivot
                    });
                }
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.spritesheet = list.ToArray();
            importer.SaveAndReimport();
        }

        private static Dictionary<string, Sprite> LoadAllSprites()
        {
            var sprites = new Dictionary<string, Sprite>();
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Sprite sprite && !sprites.ContainsKey(sprite.name))
                    {
                        sprites[sprite.name] = sprite;
                    }
                }
            }
            return sprites;
        }

        private static Dictionary<string, TileBase> LoadAllTiles(Dictionary<string, Sprite> sprites)
        {
            var tiles = new Dictionary<string, TileBase>();
            var guids = AssetDatabase.FindAssets("t:TileBase", new[] { "Assets/Tiles" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                if (tile != null && !tiles.ContainsKey(tile.name))
                {
                    tiles[tile.name] = tile;
                }
            }
            return tiles;
        }

        private static void BuildPastureScene(Dictionary<string, Sprite> sprites, Dictionary<string, TileBase> tiles)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Grid
            var gridObject = new GameObject("Grid");
            gridObject.AddComponent<Grid>();

            // Ground Tilemap (Lush grass filled with margin -15 to +15)
            var groundObj = new GameObject("Ground");
            groundObj.transform.SetParent(gridObject.transform, false);
            var groundTilemap = groundObj.AddComponent<Tilemap>();
            var groundRenderer = groundObj.AddComponent<TilemapRenderer>();
            groundRenderer.sortingLayerName = GroundLayer;
            groundRenderer.sortingOrder = 0;

            if (tiles.TryGetValue("grass", out var grassTile))
            {
                for (var x = -15; x < MapWidth + 15; x++)
                {
                    for (var y = -15; y < MapHeight + 15; y++)
                    {
                        groundTilemap.SetTile(new Vector3Int(x, y, 0), grassTile);
                    }
                }
            }

            // Paths & Pen Floors
            var pathsObj = new GameObject("Paths");
            pathsObj.transform.SetParent(gridObject.transform, false);
            var pathsTilemap = pathsObj.AddComponent<Tilemap>();
            var pathsRenderer = pathsObj.AddComponent<TilemapRenderer>();
            pathsRenderer.sortingLayerName = GroundLayer;
            pathsRenderer.sortingOrder = 1;

            // Main Entrance Road from West: y=11, x=0 to 18
            PaintHorizontalPath(pathsTilemap, tiles, 0, 18, 11);
            // Path leading up to Chicken Pen gate (x=9, y=12 to 14)
            PaintVerticalPath(pathsTilemap, tiles, 9, 12, 14);
            // Path leading down to Cow Pasture gate (x=20, y=9 to 11)
            PaintVerticalPath(pathsTilemap, tiles, 20, 9, 11);
            // Path leading to Pasture Stall (x=16, y=11 to 12)
            PaintVerticalPath(pathsTilemap, tiles, 16, 11, 12);

            // Chicken Pen Dirt Ground: 8 wide x 5 high -> Rect(5, 14, 9, 6)
            PaintField(pathsTilemap, tiles, new RectInt(5, 14, 9, 6));

            // 2. World Root
            var world = new GameObject("World").transform;
            BuildBoundaries(world);
            BuildScenery(world, sprites);

            // 3. Exact Chicken Pen matching Image 1
            BuildExactChickenPen(world, sprites);

            // 4. Exact Cow Pasture matching Image 2
            BuildExactCowPasture(world, sprites);

            // 5. Pasture Stall (Gian hàng nông sản gà & bò)
            BuildPastureStall(world, sprites);

            // 6. Portal returning to FarmMap
            BuildPortal(world, sprites, new Vector2(1f, 11f), "FarmMap", new Vector2(29.5f, 7f), "To Farm");

            // 7. Player
            BuildPlayer(world, sprites, new Vector2(3f, 11f));

            // 8. Main Camera
            BuildCamera();

            // 9. Inventory System & UI
            FarmMapBuilder.BuildInventorySystem(sprites);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void PaintHorizontalPath(Tilemap tilemap, Dictionary<string, TileBase> tiles, int fromX, int toX, int y)
        {
            for (var x = fromX; x <= toX; x++)
            {
                var name = x == fromX ? "path_h_l" : x == toX ? "path_h_r" : "path_h";
                if (tiles.TryGetValue(name, out var tile))
                    tilemap.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }

        private static void PaintVerticalPath(Tilemap tilemap, Dictionary<string, TileBase> tiles, int x, int fromY, int toY)
        {
            for (var y = fromY; y <= toY; y++)
            {
                var name = y == toY ? "path_v_t" : y == fromY ? "path_v_b" : "path_v";
                if (tiles.TryGetValue(name, out var tile))
                    tilemap.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }

        private static void PaintField(Tilemap tilemap, Dictionary<string, TileBase> tiles, RectInt area)
        {
            for (var x = area.xMin; x < area.xMax; x++)
            {
                for (var y = area.yMin; y < area.yMax; y++)
                {
                    var col = x == area.xMin ? "l" : x == area.xMax - 1 ? "r" : string.Empty;
                    var row = y == area.yMin ? "b" : y == area.yMax - 1 ? "t" : string.Empty;
                    var name = row + col;
                    var tileKey = name.Length == 0 ? "dirt_c" : "dirt_" + name;
                    if (tiles.TryGetValue(tileKey, out var tile))
                        tilemap.SetTile(new Vector3Int(x, y, 0), tile);
                }
            }
        }

        private static void BuildBoundaries(Transform parent)
        {
            var bounds = new GameObject("Boundaries");
            bounds.transform.SetParent(parent, false);

            AddWall(bounds, new Vector2(MapWidth / 2f, MapHeight - 0.5f), new Vector2(MapWidth + 10f, 3f));
            AddWall(bounds, new Vector2(MapWidth / 2f, 0.5f), new Vector2(MapWidth + 10f, 3f));
            AddWall(bounds, new Vector2(MapWidth - 0.5f, MapHeight / 2f), new Vector2(3f, MapHeight + 10f));

            AddWall(bounds, new Vector2(0.5f, 5.5f), new Vector2(3f, 11f));
            AddWall(bounds, new Vector2(0.5f, 17.5f), new Vector2(3f, 11f));
        }

        private static void AddWall(GameObject owner, Vector2 center, Vector2 size)
        {
            var wall = owner.AddComponent<BoxCollider2D>();
            wall.offset = center;
            wall.size = size;
        }

        private static void BuildScenery(Transform parent, Dictionary<string, Sprite> sprites)
        {
            var scenery = new GameObject("Scenery").transform;
            scenery.SetParent(parent, false);

            var random = new System.Random(7);

            for (var x = 0; x < MapWidth + 1; x += 2)
            {
                Prop(scenery, sprites, random.Next(3) == 0 ? "pine" : "tree", x + 0.5f, 19.4f + (float)random.NextDouble() * 0.6f, false);
                Prop(scenery, sprites, random.Next(3) == 0 ? "pine" : "tree", x + 0.5f, -0.4f + (float)random.NextDouble() * 0.8f, false);
            }
            for (var y = 1; y < MapHeight; y += 2)
            {
                Prop(scenery, sprites, random.Next(3) == 0 ? "pine" : "tree", MapWidth - 0.6f, y + 0.4f, false);
            }
            for (var y = 1; y <= 9; y += 2)
            {
                Prop(scenery, sprites, random.Next(3) == 0 ? "pine" : "tree", 0.6f, y + 0.4f, false);
            }
            for (var y = 13; y < MapHeight; y += 2)
            {
                Prop(scenery, sprites, random.Next(3) == 0 ? "pine" : "tree", 0.6f, y + 0.4f, false);
            }

            Prop(scenery, sprites, "lamp", 3.5f, 12.5f, true);
            Prop(scenery, sprites, "lamp", 17.5f, 12.5f, true);
            Prop(scenery, sprites, "notice_board", 5.5f, 12.5f, true);
        }

        // =========================================================================
        // EXACT CHICKEN PEN MATCHING USER IMAGE 1
        // =========================================================================
        private static void BuildExactChickenPen(Transform parent, Dictionary<string, Sprite> sprites)
        {
            var pen = new GameObject("Chicken Pen (Image 1)").transform;
            pen.SetParent(parent, false);

            int minX = 5, maxX = 13;
            int minY = 14, maxY = 19;
            int gateX = 9;

            // 1. Round-post fence perimeter
            // Top fence (y = maxY)
            for (int x = minX; x <= maxX; x++)
            {
                Prop(pen, sprites, "round_fence_post_h", x + 0.5f, maxY + 0.5f, true);
            }

            // Bottom fence (y = minY) with gate at center
            for (int x = minX; x <= maxX; x++)
            {
                if (x == gateX)
                {
                    Prop(pen, sprites, "round_fence_gate", x + 0.5f, minY + 0.5f, false);
                }
                else
                {
                    Prop(pen, sprites, "round_fence_post_h", x + 0.5f, minY + 0.5f, true);
                }
            }

            // Left & right fences
            for (int y = minY + 1; y < maxY; y++)
            {
                Prop(pen, sprites, "round_fence_v", minX + 0.5f, y + 0.5f, true);
                Prop(pen, sprites, "round_fence_v", maxX + 0.5f, y + 0.5f, true);
            }

            // 2. Small Chicken Coop (Red tile roof, cream wall, arched wood door) - Top Center
            Prop(pen, sprites, "Small_Chicken_Coop", 9.5f, 18.2f, true);

            // 3. Wooden Crates / Chests - Top Left
            Prop(pen, sprites, "crate_open", 6.5f, 18.0f, true);
            Prop(pen, sprites, "crate", 7.5f, 18.0f, true);

            // 4. Round Wooden Water Tub with blue water - Top Right
            Prop(pen, sprites, "Water_Tub_Round", 12.0f, 18.0f, true);

            // 5. Chickens matching positions in Image 1:
            // 5 chickens: top-left, top-right, bottom-left, bottom-center, bottom-right
            var eggFrames = GetFrames(sprites, "egg", 4);
            var chickFrames = GetFrames(sprites, "chick_idle_2", 4);
            if (chickFrames.Length == 0) chickFrames = GetFrames(sprites, "chick_idle_0", 4);
            var adultFrames = GetFrames(sprites, "chicken_idle_2", 8);
            if (adultFrames.Length == 0) adultFrames = GetFrames(sprites, "chicken_idle_0", 8);

            // Position 1: Near crates (x=7.5, y=16.8) - Baby Chick
            SpawnChickenWithGrowth(pen, AnimalGrowth.GrowthStage.Baby, eggFrames, chickFrames, adultFrames, 15f, 7.5f, 16.8f);

            // Position 2: Near water tub (x=11.5, y=16.8) - Adult Chicken
            SpawnChickenWithGrowth(pen, AnimalGrowth.GrowthStage.Adult, eggFrames, chickFrames, adultFrames, 20f, 11.5f, 16.8f);

            // Position 3: Bottom left (x=6.8, y=15.5) - Adult Chicken
            SpawnChickenWithGrowth(pen, AnimalGrowth.GrowthStage.Adult, eggFrames, chickFrames, adultFrames, 20f, 6.8f, 15.5f);

            // Position 4: Bottom center (x=9.5, y=15.5) - Egg Nest
            SpawnChickenWithGrowth(pen, AnimalGrowth.GrowthStage.Egg, eggFrames, chickFrames, adultFrames, 12f, 9.5f, 15.5f);

            // Position 5: Bottom right (x=12.2, y=15.5) - Adult Chicken
            SpawnChickenWithGrowth(pen, AnimalGrowth.GrowthStage.Adult, eggFrames, chickFrames, adultFrames, 20f, 12.2f, 15.5f);
        }

        // =========================================================================
        // EXACT COW PASTURE MATCHING USER IMAGE 2
        // =========================================================================
        private static void BuildExactCowPasture(Transform parent, Dictionary<string, Sprite> sprites)
        {
            var pasture = new GameObject("Cow Pasture (Image 2)").transform;
            pasture.SetParent(parent, false);

            int minX = 14, maxX = 25;
            int minY = 3, maxY = 9;
            int gateX = 20;

            // 1. Round-post fence perimeter
            // Top fence (y = maxY)
            for (int x = minX; x <= maxX; x++)
            {
                Prop(pasture, sprites, "round_fence_post_h", x + 0.5f, maxY + 0.5f, true);
            }

            // Bottom fence (y = minY) with gate at center
            for (int x = minX; x <= maxX; x++)
            {
                if (x == gateX)
                {
                    Prop(pasture, sprites, "round_fence_gate", x + 0.5f, minY + 0.5f, false);
                }
                else
                {
                    Prop(pasture, sprites, "round_fence_post_h", x + 0.5f, minY + 0.5f, true);
                }
            }

            // Left & right fences
            for (int y = minY + 1; y < maxY; y++)
            {
                Prop(pasture, sprites, "round_fence_v", minX + 0.5f, y + 0.5f, true);
                Prop(pasture, sprites, "round_fence_v", maxX + 0.5f, y + 0.5f, true);
            }

            // 2. Rectangular Water Trough with blue water - Top Left
            Prop(pasture, sprites, "Water_Trough_Rect", 17.5f, 8.2f, true);

            // 3. Two Golden Conical Haystacks - Top Right
            Prop(pasture, sprites, "Haystack", 22.0f, 8.0f, true);
            Prop(pasture, sprites, "Haystack", 23.5f, 8.0f, true);

            // 4. Wooden Feed Tubs Stack - Middle Right
            Prop(pasture, sprites, "Feed_Tubs_Stack", 24.2f, 5.8f, true);

            // 5. Floor grass decals inside pasture (matching Image 2)
            Decal(pasture, sprites, "detail_5", 19.5f, 7.5f);
            Decal(pasture, sprites, "detail_11", 16.5f, 4.5f);
            Decal(pasture, sprites, "detail_25", 21.5f, 4.2f);

            // 6. Cows matching positions in Image 2:
            // 3 cows: top-left, center, middle-right
            var calfFrames = GetFrames(sprites, "calf_idle_2", 4);
            if (calfFrames.Length == 0) calfFrames = GetFrames(sprites, "calf_idle_0", 4);
            var cowFrames = GetFrames(sprites, "cow_idle_2", 8);
            if (cowFrames.Length == 0) cowFrames = GetFrames(sprites, "cow_idle_0", 8);

            // Cow 1: Upper-left under water trough (x=17.5, y=6.2)
            SpawnCowWithGrowth(pasture, AnimalGrowth.GrowthStage.Baby, calfFrames, cowFrames, 18f, 17.5f, 6.2f);

            // Cow 2: Center-bottom (x=19.5, y=4.8)
            SpawnCowWithGrowth(pasture, AnimalGrowth.GrowthStage.Adult, calfFrames, cowFrames, 25f, 19.5f, 4.8f);

            // Cow 3: Lower-right near feed tubs (x=22.8, y=4.8)
            SpawnCowWithGrowth(pasture, AnimalGrowth.GrowthStage.Adult, calfFrames, cowFrames, 25f, 22.8f, 4.8f);
        }

        // =========================================================================
        // PASTURE SHOP STALL (GIAN HÀNG NÔNG SẢN GÀ & BÒ)
        // =========================================================================
        private static void BuildPastureStall(Transform parent, Dictionary<string, Sprite> sprites)
        {
            var stallObj = new GameObject("Pasture Stall");
            stallObj.transform.SetParent(parent, false);
            stallObj.transform.position = new Vector3(16.0f, 13.2f, 0f);

            var renderer = stallObj.AddComponent<SpriteRenderer>();
            if (!sprites.TryGetValue("Pasture_Stall", out var stallSprite))
            {
                stallSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Barns/Pasture_Stall.png");
            }
            if (stallSprite != null)
            {
                renderer.sprite = stallSprite;
            }
            renderer.sortingLayerName = WorldLayer;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;

            // Box collider covering the wooden counter so player can stand right in front
            var collider = stallObj.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(2.4f, 0.7f);
            collider.offset = new Vector2(0f, 0.45f);

            // Surrounding props & lights
            Prop(parent, sprites, "lamp", 14.2f, 13.0f, true);
            Prop(parent, sprites, "flowers_white", 17.8f, 12.8f, false);
            Decal(parent, sprites, "detail_25", 14.8f, 11.5f);
            Decal(parent, sprites, "detail_5", 17.2f, 11.5f);
        }

        private static void SpawnChickenWithGrowth(Transform parent, AnimalGrowth.GrowthStage stage, Sprite[] egg, Sprite[] chick, Sprite[] adult, float growTime, float x, float y)
        {
            var go = new GameObject(stage == AnimalGrowth.GrowthStage.Egg ? "Egg Nest" : stage == AnimalGrowth.GrowthStage.Baby ? "Baby Chick" : "Adult Chicken");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = WorldLayer;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.5f, 0.4f);
            col.offset = new Vector2(0f, 0.2f);

            var controller = go.AddComponent<AnimalController>();
            controller.Setup(AnimalController.AnimalType.Chicken, 1.2f, 0.7f);
            var growth = go.AddComponent<AnimalGrowth>();
            growth.Setup(AnimalGrowth.Species.Chicken, stage, egg, chick, adult, growTime);
        }

        private static void SpawnCowWithGrowth(Transform parent, AnimalGrowth.GrowthStage stage, Sprite[] calf, Sprite[] adult, float growTime, float x, float y)
        {
            var go = new GameObject(stage == AnimalGrowth.GrowthStage.Baby ? "Baby Calf" : "Adult Cow");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = WorldLayer;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.2f, 0.6f);
            col.offset = new Vector2(0f, 0.3f);

            var controller = go.AddComponent<AnimalController>();
            controller.Setup(AnimalController.AnimalType.Cow, 1.5f, 0.5f);
            var growth = go.AddComponent<AnimalGrowth>();
            growth.Setup(AnimalGrowth.Species.Cow, stage, null, calf, adult, growTime);
        }

        private static Sprite[] GetFrames(Dictionary<string, Sprite> sprites, string prefix, int count)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < count; i++)
            {
                if (sprites.TryGetValue($"{prefix}_{i}", out var sprite))
                {
                    list.Add(sprite);
                }
            }
            return list.ToArray();
        }

        private static void BuildPortal(Transform parent, Dictionary<string, Sprite> sprites, Vector2 pos, string destScene, Vector2 destSpawn, string label)
        {
            var portalObj = new GameObject($"Portal ({label})");
            portalObj.transform.SetParent(parent, false);
            portalObj.transform.position = new Vector3(pos.x, pos.y, 0f);

            var sign = portalObj.AddComponent<SpriteRenderer>();
            if (sprites.TryGetValue("sign", out var signSprite)) sign.sprite = signSprite;
            sign.sortingLayerName = WorldLayer;

            var collider = portalObj.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.5f, 2.5f);
            collider.offset = new Vector2(0f, 0f);

            var portal = portalObj.AddComponent<ScenePortal>();
            var serialized = new SerializedObject(portal);
            serialized.FindProperty("destinationScene").stringValue = destScene;
            serialized.FindProperty("targetSpawnPosition").vector2Value = destSpawn;
            serialized.FindProperty("portalLabel").stringValue = label;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildPlayer(Transform parent, Dictionary<string, Sprite> sprites, Vector2 spawnPos)
        {
            var player = new GameObject("Player");
            player.transform.SetParent(parent, false);
            player.transform.position = new Vector3(spawnPos.x, spawnPos.y, 0f);
            player.tag = "Player";

            var renderer = player.AddComponent<SpriteRenderer>();
            if (sprites.TryGetValue("bunny_idle_3_0", out var bunnySprite))
            {
                renderer.sprite = bunnySprite;
            }
            renderer.sortingLayerName = WorldLayer;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;

            var rb = player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = player.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.6f, 0.35f);
            col.offset = new Vector2(0f, 0.15f);

            // Animator component
            var anim = player.AddComponent<Animator>();
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Player/Bunny.controller");
            if (controller != null) anim.runtimeAnimatorController = controller;

            player.AddComponent<PlayerController>();
        }

        private static void BuildCamera()
        {
            var cameraObj = new GameObject("Main Camera");
            var camera = cameraObj.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7.5f;
            camera.backgroundColor = new Color(0.53f, 0.64f, 0.28f, 1f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObj.transform.position = new Vector3(17f, 11f, -10f);
            cameraObj.tag = "MainCamera";
            cameraObj.AddComponent<AudioListener>();
        }

        private static GameObject Prop(Transform parent, Dictionary<string, Sprite> sprites, string spriteName, float x, float y, bool solid)
        {
            var prop = new GameObject(spriteName);
            prop.transform.SetParent(parent, false);
            prop.transform.position = new Vector3(x, y, 0f);
            var renderer = prop.AddComponent<SpriteRenderer>();
            if (sprites.TryGetValue(spriteName, out var sprite))
            {
                renderer.sprite = sprite;
            }
            renderer.sortingLayerName = WorldLayer;
            renderer.spriteSortPoint = SpriteSortPoint.Pivot;

            if (solid && renderer.sprite != null)
            {
                var collider = prop.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(renderer.sprite.bounds.size.x * 0.9f, 0.4f);
                collider.offset = new Vector2(0f, 0.2f);
            }

            return prop;
        }

        private static void Decal(Transform parent, Dictionary<string, Sprite> sprites, string spriteName, float x, float y)
        {
            if (!sprites.TryGetValue(spriteName, out var sprite)) return;
            var decal = new GameObject(spriteName);
            decal.transform.SetParent(parent, false);
            decal.transform.position = new Vector3(x, y, 0f);
            var renderer = decal.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = GroundLayer;
            renderer.sortingOrder = 2;
        }
    }
}
