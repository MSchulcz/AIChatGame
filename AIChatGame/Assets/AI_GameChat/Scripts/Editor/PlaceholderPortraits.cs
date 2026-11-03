using UnityEngine;

namespace AIGameChat.EditorTools
{
    /// <summary>
    /// Рисует простые вертикальные портреты-заглушки (384×576, 2:3) для каждой эмоции,
    /// чтобы игра работала сразу. Замените PNG своими рисунками того же размера.
    /// </summary>
    public static class PlaceholderPortraits
    {
        public const int Width = 384;
        public const int Height = 576;

        static readonly Color32 Skin = new Color32(255, 222, 194, 255);
        static readonly Color32 Dark = new Color32(30, 25, 33, 255);
        static readonly Color32 White = new Color32(250, 250, 250, 255);
        static readonly Color32 Pink = new Color32(255, 150, 160, 255);
        static readonly Color32 Mouth = new Color32(150, 40, 55, 255);
        static readonly Color32 Drop = new Color32(150, 210, 255, 255);

        public static byte[] RenderPng(Color body, Color hair, Emotion emotion)
        {
            var px = new Color32[Width * Height]; // по умолчанию прозрачный
            Color32 bodyColor = body;
            Color32 hairColor = hair;

            // Тело и шея
            FillRoundedRect(px, 60, 0, 324, 250, 70, bodyColor);
            FillRect(px, 160, 225, 224, 300, new Color32(240, 205, 178, 255));

            // Волосы сзади, голова, чёлка
            FillDisc(px, 192, 400, 135, hairColor);
            FillDisc(px, 192, 370, 118, Skin);
            for (int y = 445; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int dx = x - 192, dy = y - 400;
                    if (dx * dx + dy * dy <= 135 * 135) Set(px, x, y, hairColor);
                }
            }

            switch (emotion)
            {
                case Emotion.Happy:
                    Arc(px, 147, 378, 16, 20, 160, 6, Dark);
                    Arc(px, 237, 378, 16, 20, 160, 6, Dark);
                    Line(px, 122, 424, 172, 428, 6, Dark);
                    Line(px, 212, 428, 262, 424, 6, Dark);
                    Arc(px, 192, 332, 42, 205, 335, 6, Dark);
                    break;

                case Emotion.Sad:
                    Eyes(px, 10);
                    Line(px, 122, 410, 172, 426, 6, Dark);
                    Line(px, 262, 410, 212, 426, 6, Dark);
                    Arc(px, 192, 285, 40, 25, 155, 6, Dark);
                    break;

                case Emotion.Angry:
                    Eyes(px, 9);
                    Line(px, 122, 430, 172, 410, 8, Dark);
                    Line(px, 262, 430, 212, 410, 8, Dark);
                    Line(px, 170, 305, 214, 305, 7, Dark);
                    break;

                case Emotion.Surprised:
                    FillDisc(px, 147, 380, 20, White); FillDisc(px, 147, 380, 9, Dark);
                    FillDisc(px, 237, 380, 20, White); FillDisc(px, 237, 380, 9, Dark);
                    Arc(px, 147, 410, 26, 30, 150, 5, Dark);
                    Arc(px, 237, 410, 26, 30, 150, 5, Dark);
                    FillDisc(px, 192, 312, 22, Dark);
                    FillDisc(px, 192, 312, 15, Mouth);
                    break;

                case Emotion.Scared:
                    FillDisc(px, 147, 380, 19, White); FillDisc(px, 147, 380, 5, Dark);
                    FillDisc(px, 237, 380, 19, White); FillDisc(px, 237, 380, 5, Dark);
                    Line(px, 122, 414, 172, 432, 5, Dark);
                    Line(px, 262, 414, 212, 432, 5, Dark);
                    Line(px, 160, 310, 176, 300, 5, Dark);
                    Line(px, 176, 300, 192, 312, 5, Dark);
                    Line(px, 192, 312, 208, 300, 5, Dark);
                    Line(px, 208, 300, 224, 310, 5, Dark);
                    FillDisc(px, 300, 420, 10, Drop);
                    break;

                case Emotion.Embarrassed:
                    Arc(px, 147, 378, 14, 20, 160, 6, Dark);
                    Arc(px, 237, 378, 14, 20, 160, 6, Dark);
                    FillDisc(px, 120, 335, 26, Pink);
                    FillDisc(px, 264, 335, 26, Pink);
                    Line(px, 176, 308, 188, 302, 5, Dark);
                    Line(px, 188, 302, 200, 308, 5, Dark);
                    Line(px, 200, 308, 208, 302, 5, Dark);
                    break;

                default: // Neutral
                    Eyes(px, 10);
                    Line(px, 122, 418, 172, 418, 6, Dark);
                    Line(px, 212, 418, 262, 418, 6, Dark);
                    Line(px, 172, 310, 212, 310, 6, Dark);
                    break;
            }

            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            return png;
        }

        static void Eyes(Color32[] px, int radius)
        {
            FillDisc(px, 147, 380, radius, Dark);
            FillDisc(px, 237, 380, radius, Dark);
        }

        static void Set(Color32[] px, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            px[y * Width + x] = c;
        }

        static void FillRect(Color32[] px, int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Set(px, x, y, c);
        }

        static void FillDisc(Color32[] px, int cx, int cy, int r, Color32 c)
        {
            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= r2) Set(px, x, y, c);
                }
        }

        static void FillRoundedRect(Color32[] px, int x0, int y0, int x1, int y1, int radius, Color32 c)
        {
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    int cx = Mathf.Clamp(x, x0 + radius, x1 - radius);
                    int cy = Mathf.Clamp(y, y0 + radius, y1 - radius);
                    int dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= radius * radius) Set(px, x, y, c);
                }
            }
        }

        static void Line(Color32[] px, int x0, int y0, int x1, int y1, int thickness, Color32 c)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(new Vector2(x0, y0), new Vector2(x1, y1)));
            if (steps < 1) steps = 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                FillDisc(px, Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), thickness / 2, c);
            }
        }

        static void Arc(Color32[] px, int cx, int cy, int r, float fromDeg, float toDeg, int thickness, Color32 c)
        {
            const int steps = 48;
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, (float)i / steps) * Mathf.Deg2Rad;
                FillDisc(px, cx + Mathf.RoundToInt(r * Mathf.Cos(a)), cy + Mathf.RoundToInt(r * Mathf.Sin(a)), thickness / 2, c);
            }
        }
    }
}
