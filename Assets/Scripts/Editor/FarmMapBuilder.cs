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
    public static class FarmMapBuilder
    {
        private const string ScenePath = "Assets/Scenes/FarmMap.unity";
        private const string PastureScenePath = "Assets/Scenes/PastureScene.unity";
        private const string GroundLayer = "Ground";
        private const string WorldLayer = "World";
        private const string UiLayer = "UI";
        private const int MapWidth = 34;
        private const int MapHeight = 22;

        static FarmMapBuilder()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.Log("[HappyFarmer] Building Farm Map scene...");
                BuildEverything();
            }
        }

        [MenuItem("Happy Farmer/1. Rebuild Farm Map (Reset & Build)", false, 1)]
        public static void BuildEverything()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Tiles");

            ConfigureProject();
            var sprites = LoadAllSprites();
            var tiles = LoadAllTiles(sprites);
            CreateTilePalette(tiles);
            BuildScene(sprites, tiles);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HappyFarmer] SUCCESS: Farm Map scene successfully built at " + ScenePath);
        }

        [MenuItem("Happy Farmer/2. Open Farm Map Scene", false, 2)]
        public static void OpenFarmMapScene()
        {
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
            else
            {
                BuildEverything();
            }
        }

        [MenuItem("Happy Farmer/3. Create Tile Palette", false, 3)]
        public static void MenuCreateTilePalette()
        {
            var sprites = LoadAllSprites();
            var tiles = LoadAllTiles(sprites);
            CreateTilePalette(tiles, true);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void ConfigureProject()
        {
            var tagManagerObjs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerObjs != null && tagManagerObjs.Length > 0)
            {
                var tagManager = new SerializedObject(tagManagerObjs[0]);
                var sortingLayers = tagManager.FindProperty("m_SortingLayers");
                if (sortingLayers != null)
                {
                    AddSortingLayer(sortingLayers, GroundLayer, 1000001);
                    AddSortingLayer(sortingLayers, WorldLayer, 1000002);
                    AddSortingLayer(sortingLayers, UiLayer, 1000003);
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            var graphicsObjs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphicsObjs != null && graphicsObjs.Length > 0)
            {
                var graphics = new SerializedObject(graphicsObjs[0]);
                var sortMode = graphics.FindProperty("m_TransparencySortMode");
                if (sortMode != null) sortMode.intValue = (int)TransparencySortMode.CustomAxis;
                var sortAxis = graphics.FindProperty("m_TransparencySortAxis");
                if (sortAxis != null) sortAxis.vector3Value = Vector3.up;
                graphics.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene(PastureScenePath, true)
            };
        }

        private static void AddSortingLayer(SerializedProperty layers, string name, int uniqueId)
        {
            for (var i = 0; i < layers.arraySize; i++)
            {
                if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name) return;
            }

            layers.InsertArrayElementAtIndex(layers.arraySize);
            var layer = layers.GetArrayElementAtIndex(layers.arraySize - 1);
            layer.FindPropertyRelative("name").stringValue = name;
            layer.FindPropertyRelative("uniqueID").intValue = uniqueId;
            layer.FindPropertyRelative("locked").boolValue = false;
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

            string[] tileNames = {
                "grass", "dirt_tl", "dirt_t", "dirt_tr", "dirt_l", "dirt_c", "dirt_r", "dirt_bl", "dirt_b", "dirt_br",
                "path_h_l", "path_h", "path_h_r", "path_v_t", "path_v", "path_v_b"
            };

            foreach (var name in tileNames)
            {
                if (!tiles.ContainsKey(name) && sprites.ContainsKey(name))
                {
                    var tile = ScriptableObject.CreateInstance<Tile>();
                    tile.sprite = sprites[name];
                    tile.colliderType = Tile.ColliderType.None;
                    var path = $"Assets/Tiles/{name}.asset";
                    AssetDatabase.CreateAsset(tile, path);
                    tiles[name] = tile;
                }
            }

            return tiles;
        }

        private static void CreateTilePalette(Dictionary<string, TileBase> tiles, bool overwrite = false)
        {
            var palettePath = "Assets/Tiles/FarmPalette.prefab";
            if (!overwrite && File.Exists(palettePath)) return;

            var paletteObj = new GameObject("FarmPalette");
            paletteObj.AddComponent<Grid>();
            var layer = new GameObject("Layer1");
            layer.transform.SetParent(paletteObj.transform, false);
            var tilemap = layer.AddComponent<Tilemap>();
            layer.AddComponent<TilemapRenderer>();

            if (tiles.TryGetValue("grass", out var grassTile))
                tilemap.SetTile(new Vector3Int(0, 0, 0), grassTile);

            string[,] dirtGrid = {
                { "dirt_tl", "dirt_t", "dirt_tr" },
                { "dirt_l",  "dirt_c", "dirt_r"  },
                { "dirt_bl", "dirt_b", "dirt_br" }
            };
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    var name = dirtGrid[r, c];
                    if (tiles.TryGetValue(name, out var tile))
                        tilemap.SetTile(new Vector3Int(c + 2, 2 - r, 0), tile);
                }
            }

            if (tiles.TryGetValue("path_h_l", out var phl)) tilemap.SetTile(new Vector3Int(6, 2, 0), phl);
            if (tiles.TryGetValue("path_h", out var ph))     tilemap.SetTile(new Vector3Int(7, 2, 0), ph);
            if (tiles.TryGetValue("path_h_r", out var phr)) tilemap.SetTile(new Vector3Int(8, 2, 0), phr);

            if (tiles.TryGetValue("path_v_t", out var pvt)) tilemap.SetTile(new Vector3Int(6, 1, 0), pvt);
            if (tiles.TryGetValue("path_v", out var pv))     tilemap.SetTile(new Vector3Int(6, 0, 0), pv);
            if (tiles.TryGetValue("path_v_b", out var pvb)) tilemap.SetTile(new Vector3Int(6, -1, 0), pvb);

            PrefabUtility.SaveAsPrefabAsset(paletteObj, palettePath);
            Object.DestroyImmediate(paletteObj);
        }

        private static void BuildScene(Dictionary<string, Sprite> sprites, Dictionary<string, TileBase> tiles)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Grid
            var gridObject = new GameObject("Grid");
            gridObject.AddComponent<Grid>();

            // Ground Tilemap: Fill well beyond borders with grass
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

            // Paths & Fields Tilemap
            var pathsObj = new GameObject("Paths");
            pathsObj.transform.SetParent(gridObject.transform, false);
            var pathsTilemap = pathsObj.AddComponent<Tilemap>();
            var pathsRenderer = pathsObj.AddComponent<TilemapRenderer>();
            pathsRenderer.sortingLayerName = GroundLayer;
            pathsRenderer.sortingOrder = 1;

            // Main horizontal path extends all the way to right edge (x=33) for pasture exit!
            PaintHorizontalPath(pathsTilemap, tiles, 3, 33, 7);
            PaintHorizontalPath(pathsTilemap, tiles, 7, 11, 13);
            PaintVerticalPath(pathsTilemap, tiles, 11, 8, 12);
            PaintVerticalPath(pathsTilemap, tiles, 25, 8, 10);

            var fields = new[] {
                new RectInt(5, 8, 5, 4),
                new RectInt(13, 8, 6, 4),
                new RectInt(13, 3, 8, 4)
            };
            foreach (var field in fields)
            {
                PaintField(pathsTilemap, tiles, field);
            }

            // 2. World Root
            var world = new GameObject("World").transform;
            BuildBoundaries(world);
            BuildScenery(world, sprites, fields);
            BuildHouse(world, sprites, 4, 14);
            BuildStall(world, sprites, 24, 11);
            BuildParcels(world, sprites);

            // 3. Portal to PastureScene at right edge
            BuildPortal(world, sprites, new Vector2(32f, 7f), "PastureScene", new Vector2(3f, 11f), "To Pasture");

            // 4. Player
            BuildPlayer(world, sprites);

            // 5. Main Camera
            BuildCamera();

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

            AddWall(bounds, new Vector2(MapWidth / 2f, 0.5f), new Vector2(MapWidth + 10f, 3f));
            AddWall(bounds, new Vector2(MapWidth / 2f, MapHeight - 0.5f), new Vector2(MapWidth + 10f, 3f));
            AddWall(bounds, new Vector2(0.5f, MapHeight / 2f), new Vector2(3f, MapHeight + 10f));

            // Right wall has opening at y=6..8 for path to pasture
            AddWall(bounds, new Vector2(MapWidth - 0.5f, 2.5f), new Vector2(3f, 6f));
            AddWall(bounds, new Vector2(MapWidth - 0.5f, 15f), new Vector2(3f, 13f));
        }

        private static void AddWall(GameObject owner, Vector2 center, Vector2 size)
        {
            var wall = owner.AddComponent<BoxCollider2D>();
            wall.offset = center;
            wall.size = size;
        }

        private static void BuildScenery(Transform parent, Dictionary<string, Sprite> sprites, RectInt[] fields)
        {
            var scenery = new GameObject("Scenery").transform;
            scenery.SetParent(parent, false);

            var random = new System.Random(5);
            // Top and bottom borders
            for (var x = 0; x < MapWidth + 1; x += 2)
            {
                Prop(scenery, sprites, random.Next(3) == 0 ? "pine" : "tree", x + 0.5f, 19.4f + (float)random.NextDouble() * 0.6f, false);
                Prop(scenery, sprites, random.Next(3) == 0 ? "pine" : "tree", x + 0.5f, -0.4f + (float)random.NextDouble() * 0.8f, false);
            }

            // Left and right borders (leave opening on right at y=7)
            for (var y = 1; y < MapHeight; y += 2)
            {
                Prop(scenery, sprites, random.Next(3) == 0 ? "pine" : "tree", 0.6f, y + 0.4f, false);
                if (y < 6 || y > 8)
                {
                    Prop(scenery, sprites, random.Next(3) == 0 ? "pine" : "tree", MapWidth - 0.6f, y + 0.4f, false);
                }
            }

            // Seal top-right corner specifically
            Prop(scenery, sprites, "tree", MapWidth - 0.6f, 19.8f, false);
            Prop(scenery, sprites, "pine", MapWidth - 0.6f, 21.2f, false);

            // Floor flower/grass details
            for (var i = 0; i < 90; i++)
            {
                var x = random.Next(3, MapWidth - 3);
                var y = random.Next(3, MapHeight - 4);
                if (!IsOpenGrass(x, y, fields)) continue;
                var detailKey = $"detail_{random.Next(40)}";
                if (sprites.ContainsKey(detailKey))
                {
                    Decal(scenery, sprites, detailKey, x + 0.5f, y + 0.5f);
                }
            }

            // Scene props
            Prop(scenery, sprites, "well", 13.5f, 14f, true);
            Prop(scenery, sprites, "bench", 16.5f, 14f, true);
            Prop(scenery, sprites, "flowers_white", 3.5f, 14f, true);
            Prop(scenery, sprites, "flowers_yellow", 11.5f, 14f, true);
            Prop(scenery, sprites, "lamp", 12.5f, 12.5f, true);
            Prop(scenery, sprites, "lamp", 28.5f, 8.5f, true);
            Prop(scenery, sprites, "notice_board", 21f, 8.5f, true);
            Prop(scenery, sprites, "pond", 27f, 3f, true);
            Prop(scenery, sprites, "log", 4.5f, 4f, true);
            Prop(scenery, sprites, "stump", 7.5f, 3.5f, true);
            Prop(scenery, sprites, "rock", 10.5f, 4.5f, true);
            Prop(scenery, sprites, "bush", 22.5f, 15.5f, true);
            Prop(scenery, sprites, "bush_round", 19.5f, 16.5f, true);
            Prop(scenery, sprites, "mushroom", 29.5f, 15.5f, false);
            Prop(scenery, sprites, "mushroom", 3.5f, 3f, false);
        }

        private static bool IsOpenGrass(int x, int y, RectInt[] fields)
        {
            if (y >= 6 && y <= 8) return false;
            if (y >= 12 && y <= 14 && x <= 13) return false;
            if (x >= 10 && x <= 12 && y <= 14) return false;
            if (x >= 22 && x <= 28 && y >= 7 && y <= 15) return false;
            if (x <= 11 && y >= 13) return false;

            var cell = new Vector2Int(x, y);
            return !fields.Any(field => new RectInt(field.x - 1, field.y - 1, field.width + 2, field.height + 2).Contains(cell));
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
                collider.size = new Vector2(renderer.sprite.bounds.size.x * 0.8f, 0.5f);
                collider.offset = new Vector2(0f, 0.25f);
            }

            return prop;
        }

        private static GameObject Composite(Transform parent, string name, float x, float y)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(x, y, 0f);
            var group = root.AddComponent<SortingGroup>();
            group.sortingLayerName = WorldLayer;
            return root;
        }

        private static void Piece(GameObject root, Dictionary<string, Sprite> sprites, string spriteName, float x, float y, int order)
        {
            if (!sprites.TryGetValue(spriteName, out var sprite)) return;
            var piece = new GameObject(spriteName);
            piece.transform.SetParent(root.transform, false);
            piece.transform.localPosition = new Vector3(x, y, 0f);
            var renderer = piece.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = WorldLayer;
            renderer.sortingOrder = order;
        }

        private static void BuildHouse(Transform parent, Dictionary<string, Sprite> sprites, int x, int y)
        {
            var house = Composite(parent, "House", x, y);
            for (var column = 0; column < 6; column++)
            {
                for (var row = 0; row < 2; row++) Piece(house, sprites, "brick", column + 0.5f, row + 0.5f, 0);
            }

            Piece(house, sprites, "window", 1.5f, 1.5f, 1);
            Piece(house, sprites, "window", 4.5f, 1.5f, 1);
            Piece(house, sprites, "door", 3.5f, 0.5f, 1);
            for (var gable = 0; gable < 3; gable++) Piece(house, sprites, "gable", gable * 2 + 1f, 1.9f, 2);

            var collider = house.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(6f, 2f);
            collider.offset = new Vector2(3f, 1f);
        }

        private static void BuildStall(Transform parent, Dictionary<string, Sprite> sprites, int x, int y)
        {
            var stall = Composite(parent, "Shop Stall", x, y);
            for (var column = 0; column < 3; column++)
            {
                Piece(stall, sprites, "brick_dark", column + 0.5f, 1.5f, 0);
                Piece(stall, sprites, "stall_front", column + 0.5f, 0.5f, 1);
                Piece(stall, sprites, "stall_awning", column + 0.5f, 2.05f, 3);
                Piece(stall, sprites, "stall_awning", column + 0.5f, 2.4f, 3);
            }

            Piece(stall, sprites, "stall_post", 0.5f, 0f, 2);
            Piece(stall, sprites, "stall_post", 2.5f, 0f, 2);
            Piece(stall, sprites, "crate", 0.5f, 0.75f, 4);
            Piece(stall, sprites, "crate_open", 1.5f, 0.75f, 4);
            Piece(stall, sprites, "barrel", 2.5f, 0.75f, 4);

            var collider = stall.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(3f, 2f);
            collider.offset = new Vector2(1.5f, 1f);
        }

        private static void BuildParcels(Transform parent, Dictionary<string, Sprite> sprites)
        {
            var parcelsGroup = new GameObject("Parcels").transform;
            parcelsGroup.SetParent(parent, false);

            Prop(parcelsGroup, sprites, "sign", 10.5f, 8.5f, false);

            var eastParcel = new GameObject("Parcel East").transform;
            eastParcel.SetParent(parcelsGroup, false);
            Prop(eastParcel, sprites, "sign", 12.5f, 8.5f, false);

            var random = new System.Random(3);
            var overgrowth = new[] { "bush", "bush_round", "rock_small", "bush", "mushroom" };
            for (var x = 14; x <= 17; x++)
            {
                for (var y = 9; y <= 10; y++)
                {
                    Prop(eastParcel, sprites, overgrowth[random.Next(overgrowth.Length)], x + 0.5f, y + 0.1f, false);
                }
            }

            var southParcel = new GameObject("Parcel South").transform;
            southParcel.SetParent(parcelsGroup, false);
            Prop(southParcel, sprites, "sign", 12.5f, 3.5f, false);

            for (var x = 14; x <= 19; x++)
            {
                for (var y = 4; y <= 5; y++)
                {
                    Prop(southParcel, sprites, overgrowth[random.Next(overgrowth.Length)], x + 0.5f, y + 0.1f, false);
                }
            }
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

        private static void BuildPlayer(Transform parent, Dictionary<string, Sprite> sprites)
        {
            var player = new GameObject("Player");
            player.transform.SetParent(parent, false);
            player.transform.position = new Vector3(11f, 7.5f, 0f);
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

            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(player.transform, false);
            shadow.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var shadowRenderer = shadow.AddComponent<SpriteRenderer>();
            var shadowPath = "Assets/Sprites/LittleDreamyland/Sprites/Sprite_Shadow.png";
            var shadowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(shadowPath);
            if (shadowSprite != null) shadowRenderer.sprite = shadowSprite;
            shadowRenderer.sortingLayerName = GroundLayer;
            shadowRenderer.sortingOrder = 10;
            shadowRenderer.color = new Color(1f, 1f, 1f, 0.45f);

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

            var follow = cameraObj.AddComponent<CameraFollow>();
            follow.target = GameObject.Find("Player")?.transform;
        }
    }
}
