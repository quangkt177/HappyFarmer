using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HappyFarmer.EditorTools
{
    public static class BarnPropGenerator
    {
        private const string OutputDir = "Assets/Sprites/Barns";

        // Palette
        private static readonly Color C_Outline    = HexColor("4A3026"); // Dark brown outline
        private static readonly Color C_Wood        = HexColor("A07048"); // Warm wood body
        private static readonly Color C_WoodLight   = HexColor("BE8C5C"); // Wood highlight
        private static readonly Color C_WoodDark    = HexColor("7A5032"); // Wood shade
        private static readonly Color C_WoodDeep    = HexColor("5C3B24"); // Deep wood shade

        private static readonly Color C_RoofRed     = HexColor("D64D52"); // Barn red roof
        private static readonly Color C_RoofLight   = HexColor("EB6B6F"); // Roof highlight
        private static readonly Color C_RoofDark    = HexColor("9E3238"); // Roof shadow
        private static readonly Color C_WallCream   = HexColor("EADBCE"); // Coop wall cream
        private static readonly Color C_WallShade   = HexColor("C9B6A3"); // Wall shadow

        private static readonly Color C_WaterLight  = HexColor("72C5EE"); // Clear blue water
        private static readonly Color C_WaterDeep   = HexColor("4292C7"); // Water shadow
        private static readonly Color C_WaterRim    = HexColor("8ED8FC"); // Water shine

        private static readonly Color C_HayYellow   = HexColor("FCE068"); // Bright golden straw
        private static readonly Color C_HayGold     = HexColor("E5BF42"); // Mid straw
        private static readonly Color C_HayDark     = HexColor("B5882B"); // Deep straw shade

        private static readonly Color C_Iron        = HexColor("737B85"); // Metal hoop
        private static readonly Color C_Clear       = new Color(0, 0, 0, 0);

        [MenuItem("Happy Farmer/6. Generate Barn & Coop Props (Hay, Troughs, Fences)", false, 6)]
        public static void GenerateAllBarnProps()
        {
            if (!Directory.Exists(OutputDir))
            {
                Directory.CreateDirectory(OutputDir);
            }

            GenerateFenceSprites();
            GenerateChickenCoop();
            GenerateWaterTroughs();
            GenerateHaystack();
            GenerateFeedTubs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HappyFarmer] SUCCESS: Barn and Coop pixel-art props generated!");
        }

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }

        private static void FillClear(Texture2D tex)
        {
            var clear = new Color[tex.width * tex.height];
            for (int i = 0; i < clear.Length; i++) clear[i] = C_Clear;
            tex.SetPixels(clear);
        }

        private static void Set(Texture2D tex, int x, int y, Color c)
        {
            if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
            {
                tex.SetPixel(x, y, c);
            }
        }

        // ==========================================
        // 1. ROUND-POST FENCES & GATE
        // ==========================================
        private static void GenerateFenceSprites()
        {
            // 64x64 sheet with:
            // 0,0: Horizontal rail with post (16x20)
            // 16,0: Post only (16x20)
            // 32,0: Gate (20x20)
            // 0,20: Vertical fence (16x20)
            var tex = new Texture2D(64, 48, TextureFormat.RGBA32, false);
            FillClear(tex);

            // A) Horizontal Fence Segment with Round Post (16x24 at x=0, y=0)
            DrawRoundPost(tex, 8, 2, 18);
            DrawDoubleRails(tex, 0, 16, 6, 12);

            // B) Horizontal Rail only (16x24 at x=16, y=0)
            DrawDoubleRails(tex, 16, 32, 6, 12);

            // C) Wooden Gate (24x24 at x=32, y=0)
            DrawRoundPost(tex, 34, 2, 18);
            DrawRoundPost(tex, 54, 2, 18);
            DrawGateDoor(tex, 35, 53, 5, 13);

            // D) Vertical Fence Post with Rail (16x24 at x=0, y=24)
            DrawRoundPost(tex, 8, 26, 42);
            DrawVerticalRail(tex, 8, 24, 48);

            tex.Apply();
            var path = $"{OutputDir}/Round_Fence_Set.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            ConfigureFenceImporter(path);
        }

        private static void DrawRoundPost(Texture2D tex, int cx, int by, int ty)
        {
            int h = ty - by;
            // Round cap
            Set(tex, cx - 1, ty, C_WoodLight);
            Set(tex, cx,     ty, C_WoodLight);
            Set(tex, cx - 2, ty - 1, C_Outline);
            Set(tex, cx + 1, ty - 1, C_Outline);
            Set(tex, cx - 1, ty + 1, C_Outline);
            Set(tex, cx,     ty + 1, C_Outline);

            // Post shaft (width 4: cx-2 to cx+1)
            for (int y = by; y < ty; y++)
            {
                Set(tex, cx - 2, y, C_Outline);
                Set(tex, cx - 1, y, C_WoodLight);
                Set(tex, cx,     y, C_Wood);
                Set(tex, cx + 1, y, C_Outline);
            }
            Set(tex, cx - 1, by - 1, C_Outline);
            Set(tex, cx,     by - 1, C_Outline);
        }

        private static void DrawDoubleRails(Texture2D tex, int x1, int x2, int y1, int y2)
        {
            // Top rail (2px thick at y2)
            for (int x = x1; x < x2; x++)
            {
                Set(tex, x, y2 + 1, C_Outline);
                Set(tex, x, y2,     C_WoodLight);
                Set(tex, x, y2 - 1, C_WoodDark);
                Set(tex, x, y2 - 2, C_Outline);
            }
            // Bottom rail (2px thick at y1)
            for (int x = x1; x < x2; x++)
            {
                Set(tex, x, y1 + 1, C_Outline);
                Set(tex, x, y1,     C_WoodLight);
                Set(tex, x, y1 - 1, C_WoodDark);
                Set(tex, x, y1 - 2, C_Outline);
            }
        }

        private static void DrawVerticalRail(Texture2D tex, int cx, int y1, int y2)
        {
            for (int y = y1; y < y2; y++)
            {
                Set(tex, cx - 1, y, C_WoodDark);
                Set(tex, cx,     y, C_WoodDeep);
            }
        }

        private static void DrawGateDoor(Texture2D tex, int x1, int x2, int y1, int y2)
        {
            // Two horizontal gate bars
            for (int x = x1; x <= x2; x++)
            {
                Set(tex, x, y2, C_WoodLight);
                Set(tex, x, y2 - 1, C_Outline);
                Set(tex, x, y1, C_WoodLight);
                Set(tex, x, y1 - 1, C_Outline);
            }
            // Diagonal brace
            int len = x2 - x1;
            for (int i = 0; i <= len; i++)
            {
                int x = x1 + i;
                int y = y1 + (i * (y2 - y1)) / len;
                Set(tex, x, y, C_Wood);
                Set(tex, x, y - 1, C_Outline);
            }
        }

        private static void ConfigureFenceImporter(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;

            var sheets = new SpriteMetaData[]
            {
                new SpriteMetaData { name = "round_fence_post_h", rect = new Rect(0, 0, 16, 24),  alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.5f, 0f) },
                new SpriteMetaData { name = "round_fence_rail_h", rect = new Rect(16, 0, 16, 24), alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.5f, 0f) },
                new SpriteMetaData { name = "round_fence_gate",   rect = new Rect(32, 0, 24, 24), alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.5f, 0f) },
                new SpriteMetaData { name = "round_fence_v",      rect = new Rect(0, 24, 16, 24), alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(0.5f, 0f) },
            };

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.spritesheet = sheets;
            importer.SaveAndReimport();
        }

        // ==========================================
        // 2. SMALL CHICKEN COOP (Exact match to Image 1)
        // ==========================================
        private static void GenerateChickenCoop()
        {
            int w = 32, h = 36;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            FillClear(tex);

            int cx = 16;
            // Pitched red roof (from y=14 to y=34)
            int roofBaseY = 14;
            int roofTopY = 34;

            for (int y = roofBaseY; y <= roofTopY; y++)
            {
                float progress = (float)(y - roofBaseY) / (roofTopY - roofBaseY);
                int halfW = (int)Mathf.Lerp(15, 0, progress);

                for (int x = cx - halfW; x <= cx + halfW; x++)
                {
                    bool isBorder = (x == cx - halfW || x == cx + halfW || y == roofTopY);
                    if (isBorder)
                    {
                        Set(tex, x, y, C_Outline);
                    }
                    else
                    {
                        // Roof tiles texture
                        bool shingle = ((x + y) % 4 == 0);
                        Set(tex, x, y, shingle ? C_RoofLight : (x > cx) ? C_RoofDark : C_RoofRed);
                    }
                }
            }

            // Cream walls (from y=2 to y=14, width 22: cx-11 to cx+10)
            int wallL = cx - 11, wallR = cx + 10;
            for (int y = 2; y <= roofBaseY; y++)
            {
                for (int x = wallL; x <= wallR; x++)
                {
                    bool isBorder = (x == wallL || x == wallR || y == 2);
                    if (isBorder)
                    {
                        Set(tex, x, y, C_Outline);
                    }
                    else
                    {
                        Set(tex, x, y, (x > cx) ? C_WallShade : C_WallCream);
                    }
                }
            }

            // Arched dark wood door in center (width 8: cx-4 to cx+3, height 8: y=2 to 10)
            for (int y = 2; y <= 10; y++)
            {
                int dw = (y == 10) ? 2 : (y == 9) ? 3 : 4;
                for (int x = cx - dw; x < cx + dw; x++)
                {
                    bool isBorder = (x == cx - dw || x == cx + dw - 1 || y == 10);
                    Set(tex, x, y, isBorder ? C_Outline : C_WoodDark);
                }
            }

            tex.Apply();
            var path = $"{OutputDir}/Small_Chicken_Coop.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            ConfigureSingleSprite(path, new Vector2(0.5f, 0.1f));
        }

        // ==========================================
        // 3. WATER TROUGHS (Round tub & Rect trough)
        // ==========================================
        private static void GenerateWaterTroughs()
        {
            // A) Round Water Tub (18x20, Image 1)
            int tw = 20, th = 22;
            var tubTex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
            FillClear(tubTex);
            DrawRoundWaterTub(tubTex, 10, 2);
            tubTex.Apply();

            var tubPath = $"{OutputDir}/Water_Tub_Round.png";
            File.WriteAllBytes(tubPath, tubTex.EncodeToPNG());
            Object.DestroyImmediate(tubTex);
            AssetDatabase.ImportAsset(tubPath);
            ConfigureSingleSprite(tubPath, new Vector2(0.5f, 0.1f));

            // B) Rectangular Water Trough (34x20, Image 2)
            int rw = 36, rh = 20;
            var rectTex = new Texture2D(rw, rh, TextureFormat.RGBA32, false);
            FillClear(rectTex);
            DrawRectWaterTrough(rectTex, 18, 3);
            rectTex.Apply();

            var rectPath = $"{OutputDir}/Water_Trough_Rect.png";
            File.WriteAllBytes(rectPath, rectTex.EncodeToPNG());
            Object.DestroyImmediate(rectTex);
            AssetDatabase.ImportAsset(rectPath);
            ConfigureSingleSprite(rectPath, new Vector2(0.5f, 0.15f));
        }

        private static void DrawRoundWaterTub(Texture2D tex, int cx, int by)
        {
            // Barrel shape (ellipse)
            int rw = 7, h = 14;
            int ty = by + h;

            for (int y = by; y <= ty; y++)
            {
                int span = (y == by || y == ty) ? 5 : (y == by + 1 || y == ty - 1) ? 6 : 7;
                for (int x = cx - span; x <= cx + span; x++)
                {
                    bool isBorder = (x == cx - span || x == cx + span || y == by);
                    if (isBorder)
                    {
                        Set(tex, x, y, C_Outline);
                    }
                    else
                    {
                        // Iron bands at y=by+4 and y=by+10
                        bool isBand = (y == by + 4 || y == by + 10);
                        Set(tex, x, y, isBand ? C_Iron : (x > cx + 1) ? C_WoodDark : (x < cx - 2) ? C_WoodLight : C_Wood);
                    }
                }
            }

            // Water surface inside top opening (ellipse)
            for (int dy = -2; dy <= 2; dy++)
            {
                int span = (dy == -2 || dy == 2) ? 4 : 6;
                for (int dx = -span; dx <= span; dx++)
                {
                    bool isRim = (dx == -span || dx == span || dy == 2 || dy == -2);
                    Set(tex, cx + dx, ty + dy, isRim ? C_Outline : (dx < -1) ? C_WaterRim : (dx > 2) ? C_WaterDeep : C_WaterLight);
                }
            }
        }

        private static void DrawRectWaterTrough(Texture2D tex, int cx, int by)
        {
            int w = 15, h = 12;
            int x1 = cx - w, x2 = cx + w;
            int y1 = by, y2 = by + h;

            // Outer wooden/stone box
            for (int y = y1; y <= y2; y++)
            {
                for (int x = x1; x <= x2; x++)
                {
                    bool isBorder = (x == x1 || x == x2 || y == y1 || y == y2);
                    Set(tex, x, y, isBorder ? C_Outline : C_Wood);
                }
            }

            // Water interior
            for (int y = y1 + 3; y <= y2 - 2; y++)
            {
                for (int x = x1 + 2; x <= x2 - 2; x++)
                {
                    Color wc = (y == y2 - 2) ? C_WaterRim : (y == y1 + 3) ? C_WaterDeep : C_WaterLight;
                    Set(tex, x, y, wc);
                }
            }
        }

        // ==========================================
        // 4. HAYSTACK (Exact match to Image 2)
        // ==========================================
        private static void GenerateHaystack()
        {
            int w = 26, h = 30;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            FillClear(tex);

            int cx = 13;
            // Conical straw mound
            int[] widths = { 2, 4, 6, 7, 8, 9, 10, 11, 11, 12, 12, 12, 11, 11, 10, 8, 4 };
            int baseY = 2;

            for (int i = 0; i < widths.Length; i++)
            {
                int y = baseY + (widths.Length - 1 - i);
                int span = widths[i];

                for (int x = cx - span; x <= cx + span; x++)
                {
                    bool isBorder = (x == cx - span || x == cx + span || i == 0 || i == widths.Length - 1);
                    if (isBorder)
                    {
                        Set(tex, x, y, C_Outline);
                    }
                    else
                    {
                        // Straw texture lines
                        bool strand = ((x * 3 + y * 2) % 5 == 0);
                        Color c = strand ? C_HayYellow : (x > cx + 1) ? C_HayDark : C_HayGold;
                        Set(tex, x, y, c);
                    }
                }
            }

            // Cute tuft on peak
            Set(tex, cx,     baseY + widths.Length,     C_HayYellow);
            Set(tex, cx,     baseY + widths.Length + 1, C_Outline);
            Set(tex, cx - 1, baseY + widths.Length,     C_Outline);
            Set(tex, cx + 1, baseY + widths.Length,     C_Outline);

            tex.Apply();
            var path = $"{OutputDir}/Haystack.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            ConfigureSingleSprite(path, new Vector2(0.5f, 0.1f));
        }

        // ==========================================
        // 5. WOOD FEED TUBS STACK (Image 2)
        // ==========================================
        private static void GenerateFeedTubs()
        {
            int w = 20, h = 26;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            FillClear(tex);

            int cx = 10;
            // Bottom tub
            DrawRoundTubSmall(tex, cx, 2, 9);
            // Top tub stacked
            DrawRoundTubSmall(tex, cx, 11, 9);

            tex.Apply();
            var path = $"{OutputDir}/Feed_Tubs_Stack.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            ConfigureSingleSprite(path, new Vector2(0.5f, 0.1f));
        }

        private static void DrawRoundTubSmall(Texture2D tex, int cx, int by, int r)
        {
            int h = 9;
            int ty = by + h;
            for (int y = by; y <= ty; y++)
            {
                int span = (y == by || y == ty) ? r - 2 : r;
                for (int x = cx - span; x <= cx + span; x++)
                {
                    bool isBorder = (x == cx - span || x == cx + span || y == by);
                    if (isBorder)
                    {
                        Set(tex, x, y, C_Outline);
                    }
                    else
                    {
                        Set(tex, x, y, (x > cx + 1) ? C_WoodDark : C_Wood);
                    }
                }
            }
            // Rim
            for (int x = cx - r + 1; x <= cx + r - 1; x++)
            {
                Set(tex, x, ty, C_WoodLight);
            }
        }

        private static void ConfigureSingleSprite(string path, Vector2 pivot)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.spritePivot = pivot;
            importer.SaveAndReimport();
        }
    }
}
