using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HappyFarmer.EditorTools
{
    public static class AnimalSpriteGenerator
    {
        private const string OutputDir = "Assets/Sprites/Animals";

        // LittleDreamyland Color Palette
        private static readonly Color C_Outline   = HexColor("533A38"); // Dark brown outline
        private static readonly Color C_ChickBody  = HexColor("FDE06C"); // Warm chick yellow
        private static readonly Color C_ChickShade = HexColor("E3BA4B"); // Golden yellow shade
        private static readonly Color C_ChickBeak  = HexColor("D39D5B"); // Orange beak/feet
        private static readonly Color C_ChickBlush = HexColor("D06171"); // Soft pink cheek
        private static readonly Color C_White      = HexColor("FFFFFF");
        private static readonly Color C_Clear      = new Color(0, 0, 0, 0);

        private static readonly Color C_CowBody    = HexColor("EAD2E4"); // Cream/pink cow body
        private static readonly Color C_CowSpot    = HexColor("C65885"); // Pink cow spot
        private static readonly Color C_CowNose    = HexColor("E67E9F"); // Bright pink nose
        private static readonly Color C_CowHoof    = HexColor("74542C"); // Brown hoof

        private static readonly Color C_EggShell   = HexColor("ECDDC9"); // Egg cream
        private static readonly Color C_EggShade   = HexColor("C2AD8E"); // Egg shadow

        [MenuItem("Happy Farmer/5. Generate Baby Animals & Egg Sprites", false, 5)]
        public static void GenerateAllSprites()
        {
            if (!Directory.Exists(OutputDir))
            {
                Directory.CreateDirectory(OutputDir);
            }

            GenerateEggSheet();
            GenerateChickSheet();
            GenerateCalfSheet();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HappyFarmer] SUCCESS: Baby Chick, Calf and Egg sprites generated!");
        }

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }

        // ==========================================
        // 1. EGG SHEET (16x16, 4 frames: Idle, Wobble L, Wobble R, Crack)
        // ==========================================
        private static void GenerateEggSheet()
        {
            int frameW = 16, frameH = 16, frames = 4;
            var tex = new Texture2D(frameW * frames, frameH, TextureFormat.RGBA32, false);
            FillClear(tex);

            for (int f = 0; f < frames; f++)
            {
                int ox = f * frameW;
                int tilt = f == 1 ? -1 : f == 2 ? 1 : 0;
                bool crack = f == 3;

                // Draw egg shape (approx 8x10 pixels centered)
                DrawEgg(tex, ox + 8 + tilt, 7, crack);
            }

            tex.Apply();
            var path = $"{OutputDir}/Egg_Hatching.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            ConfigureMultiSprite(path, frameW, frameH, 1, frames, "egg", new Vector2(0.5f, 0.2f));
        }

        private static void DrawEgg(Texture2D tex, int cx, int cy, bool crack)
        {
            // Egg profile (radius X per Y from bottom to top)
            int[] rx = { 3, 4, 4, 4, 3, 3, 2, 1 };
            int baseY = cy - 3;

            for (int dy = 0; dy < rx.Length; dy++)
            {
                int y = baseY + dy;
                int r = rx[dy];

                // Outline
                SetPixelSafe(tex, cx - r, y, C_Outline);
                SetPixelSafe(tex, cx + r, y, C_Outline);

                // Fill
                for (int x = cx - r + 1; x < cx + r; x++)
                {
                    Color c = (x > cx) ? C_EggShade : C_EggShell;
                    if (dy == rx.Length - 1 && x == cx) c = C_White; // highlight
                    SetPixelSafe(tex, x, y, c);
                }
            }

            // Top and bottom outline
            SetPixelSafe(tex, cx, baseY - 1, C_Outline);
            SetPixelSafe(tex, cx - 1, baseY - 1, C_Outline);
            SetPixelSafe(tex, cx + 1, baseY - 1, C_Outline);
            SetPixelSafe(tex, cx, baseY + rx.Length, C_Outline);

            // Crack mark
            if (crack)
            {
                SetPixelSafe(tex, cx, cy, C_Outline);
                SetPixelSafe(tex, cx + 1, cy + 1, C_Outline);
                SetPixelSafe(tex, cx, cy + 2, C_Outline);
                SetPixelSafe(tex, cx - 1, cy + 1, C_ChickBody); // Little yellow feather peeking!
            }
        }

        // ==========================================
        // 2. CHICK SHEET (24x24, 4 rows x 4 frames)
        // ==========================================
        private static void GenerateChickSheet()
        {
            int frameW = 24, frameH = 24, cols = 4, rows = 4;
            var tex = new Texture2D(frameW * cols, frameH * rows, TextureFormat.RGBA32, false);
            FillClear(tex);

            // Row 0: Up, Row 1: Right, Row 2: Down, Row 3: Left
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int ox = c * frameW;
                    int oy = (rows - 1 - r) * frameH;
                    DrawChickFrame(tex, ox + 12, oy + 8, r, c);
                }
            }

            tex.Apply();
            var path = $"{OutputDir}/Chick_Idle.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            ConfigureMultiSprite(path, frameW, frameH, rows, cols, "chick_idle", new Vector2(0.5f, 0.25f));
        }

        private static void DrawChickFrame(Texture2D tex, int cx, int cy, int direction, int frame)
        {
            int dip = (frame == 1 || frame == 2) ? -1 : 0;
            int hop = (frame == 3) ? 1 : 0;
            int y = cy + dip + hop;

            // Feet
            SetPixelSafe(tex, cx - 2, cy - 3, C_ChickBeak);
            SetPixelSafe(tex, cx + 2, cy - 3, C_ChickBeak);

            // Body circle (radius 5)
            for (int dy = -4; dy <= 4; dy++)
            {
                int span = (dy == -4 || dy == 4) ? 2 : (dy == -3 || dy == 3) ? 4 : 5;
                for (int dx = -span; dx <= span; dx++)
                {
                    bool isBorder = (dx == -span || dx == span || dy == -4 || dy == 4);
                    if (isBorder)
                    {
                        SetPixelSafe(tex, cx + dx, y + dy, C_Outline);
                    }
                    else
                    {
                        Color fill = (dx > 1 || dy < -1) ? C_ChickShade : C_ChickBody;
                        SetPixelSafe(tex, cx + dx, y + dy, fill);
                    }
                }
            }

            // Direction features
            if (direction == 2) // Facing Down
            {
                // Eyes
                SetPixelSafe(tex, cx - 2, y + 1, C_Outline);
                SetPixelSafe(tex, cx + 2, y + 1, C_Outline);
                // Beak
                SetPixelSafe(tex, cx, y - 1, C_ChickBeak);
                SetPixelSafe(tex, cx - 1, y - 1, C_ChickBeak);
                // Cheeks
                SetPixelSafe(tex, cx - 3, y, C_ChickBlush);
                SetPixelSafe(tex, cx + 3, y, C_ChickBlush);
            }
            else if (direction == 1) // Facing Right
            {
                SetPixelSafe(tex, cx + 2, y + 1, C_Outline);
                SetPixelSafe(tex, cx + 5, y - (frame == 2 ? 2 : 0), C_ChickBeak);
                SetPixelSafe(tex, cx + 1, y, C_ChickBlush);
            }
            else if (direction == 3) // Facing Left
            {
                SetPixelSafe(tex, cx - 2, y + 1, C_Outline);
                SetPixelSafe(tex, cx - 5, y - (frame == 2 ? 2 : 0), C_ChickBeak);
                SetPixelSafe(tex, cx - 1, y, C_ChickBlush);
            }
            else // Facing Up
            {
                // Fluff on back
                SetPixelSafe(tex, cx, y + 3, C_ChickShade);
            }

            // Tiny wing
            if (frame == 3) // Flapping
            {
                SetPixelSafe(tex, cx - 6, y + 2, C_ChickBody);
                SetPixelSafe(tex, cx + 6, y + 2, C_ChickBody);
            }
        }

        // ==========================================
        // 3. CALF SHEET (32x32, 4 rows x 4 frames)
        // ==========================================
        private static void GenerateCalfSheet()
        {
            int frameW = 32, frameH = 32, cols = 4, rows = 4;
            var tex = new Texture2D(frameW * cols, frameH * rows, TextureFormat.RGBA32, false);
            FillClear(tex);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int ox = c * frameW;
                    int oy = (rows - 1 - r) * frameH;
                    DrawCalfFrame(tex, ox + 16, oy + 10, r, c);
                }
            }

            tex.Apply();
            var path = $"{OutputDir}/Calf_Idle.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            ConfigureMultiSprite(path, frameW, frameH, rows, cols, "calf_idle", new Vector2(0.5f, 0.2f));
        }

        private static void DrawCalfFrame(Texture2D tex, int cx, int cy, int direction, int frame)
        {
            int chew = (frame == 2) ? 1 : 0;
            int tail = (frame == 1) ? 1 : 0;

            // Legs & Hooves (4 cute short legs)
            for (int lx = -6; lx <= 6; lx += 4)
            {
                SetPixelSafe(tex, cx + lx, cy - 4, C_CowHoof);
                SetPixelSafe(tex, cx + lx, cy - 3, C_Outline);
            }

            // Body box (approx 14 wide x 8 high)
            int bw = 7, bh = 4;
            for (int dy = -bh; dy <= bh; dy++)
            {
                for (int dx = -bw; dx <= bw; dx++)
                {
                    bool isBorder = (dx == -bw || dx == bw || dy == -bh || dy == bh);
                    if (isBorder)
                    {
                        SetPixelSafe(tex, cx + dx, cy + dy, C_Outline);
                    }
                    else
                    {
                        // Pink spots on body
                        bool isSpot = (dx > 0 && dx < 5 && dy > -2 && dy < 3);
                        SetPixelSafe(tex, cx + dx, cy + dy, isSpot ? C_CowSpot : C_CowBody);
                    }
                }
            }

            // Head (rounded, big and cute on baby calf)
            int hx = (direction == 1) ? cx + 5 : (direction == 3) ? cx - 5 : cx;
            int hy = cy + 4;
            int hw = 5, hh = 5;

            for (int dy = -hh; dy <= hh; dy++)
            {
                for (int dx = -hw; dx <= hw; dx++)
                {
                    if (dx == -hw || dx == hw || dy == -hh || dy == hh)
                    {
                        SetPixelSafe(tex, hx + dx, hy + dy, C_Outline);
                    }
                    else
                    {
                        SetPixelSafe(tex, hx + dx, hy + dy, C_CowBody);
                    }
                }
            }

            // Ears
            SetPixelSafe(tex, hx - 6, hy + 3, C_CowNose);
            SetPixelSafe(tex, hx + 6, hy + 3, C_CowNose);

            // Face details
            if (direction == 2) // Facing Down
            {
                // Big eyes
                SetPixelSafe(tex, hx - 2, hy + 1, C_Outline);
                SetPixelSafe(tex, hx + 2, hy + 1, C_Outline);
                // Pink cute muzzle
                for (int mx = -2; mx <= 2; mx++)
                {
                    SetPixelSafe(tex, hx + mx, hy - 2 + chew, C_CowNose);
                    SetPixelSafe(tex, hx + mx, hy - 3 + chew, C_Outline);
                }
            }
            else if (direction == 1) // Facing Right
            {
                SetPixelSafe(tex, hx + 2, hy + 1, C_Outline);
                SetPixelSafe(tex, hx + 5, hy - 2 + chew, C_CowNose);
            }
            else if (direction == 3) // Facing Left
            {
                SetPixelSafe(tex, hx - 2, hy + 1, C_Outline);
                SetPixelSafe(tex, hx - 5, hy - 2 + chew, C_CowNose);
            }

            // Tail
            SetPixelSafe(tex, cx - 8, cy + tail, C_CowSpot);
        }

        private static void FillClear(Texture2D tex)
        {
            var clear = new Color[tex.width * tex.height];
            for (int i = 0; i < clear.Length; i++) clear[i] = C_Clear;
            tex.SetPixels(clear);
        }

        private static void SetPixelSafe(Texture2D tex, int x, int y, Color color)
        {
            if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
            {
                tex.SetPixel(x, y, color);
            }
        }

        private static void ConfigureMultiSprite(string path, int frameW, int frameH, int rows, int cols, string prefix, Vector2 pivot)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;

            var sheets = new List<SpriteMetaData>();
            int totalH = frameH * rows;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    sheets.Add(new SpriteMetaData
                    {
                        name = (rows == 1) ? $"{prefix}_{c}" : $"{prefix}_{r}_{c}",
                        rect = new Rect(c * frameW, totalH - (r + 1) * frameH, frameW, frameH),
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
            importer.spritesheet = sheets.ToArray();
            importer.SaveAndReimport();
        }
    }
}
