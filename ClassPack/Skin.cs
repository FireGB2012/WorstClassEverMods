using System.Collections.Generic;
using UnityEngine;

namespace ClassPack
{
    /// <summary>
    /// The look of every window the mod draws, copied from the game's own menus (its results
    /// window): cream paper panels with a brown line and round corners, white cells with a
    /// thin line, green round buttons with white text, an orange title with a dark outline,
    /// and white outlined text on the playing screen. One place, so all windows match.
    /// </summary>
    internal static class Skin
    {
        public static readonly Color32 Cream = new Color32(248, 240, 221, 255);
        public static readonly Color32 CreamDark = new Color32(237, 225, 196, 255);
        public static readonly Color32 Brown = new Color32(78, 58, 40, 255);
        public static readonly Color32 Line = new Color32(126, 100, 72, 255);
        public static readonly Color32 Faded = new Color32(140, 118, 94, 255);
        public static readonly Color32 Green = new Color32(118, 196, 78, 255);
        public static readonly Color32 GreenLight = new Color32(140, 214, 98, 255);
        public static readonly Color32 GreenDark = new Color32(66, 136, 44, 255);
        public static readonly Color32 Orange = new Color32(242, 140, 40, 255);
        public static readonly Color32 White = new Color32(255, 255, 255, 255);
        public static readonly Color32 Hover = new Color32(255, 246, 204, 255);
        public static readonly Color32 Outline = new Color32(48, 36, 26, 255);

        public static Font Font { get; private set; }

        public static GUIStyle Panel, Card, Title, Label, Small, Note, Hint, Button, GreenButton, Cell, Track, Thumb;

        private static bool _ready;

        public static void Ensure()
        {
            if (_ready)
            {
                return;
            }
            _ready = true;
            try
            {
                // A round, heavy font close to the one in the game's menus.
                Font = Font.CreateDynamicFontFromOSFont(new[] { "Arial Rounded MT Bold", "Segoe UI Semibold", "Verdana", "Arial" }, 18);
            }
            catch (System.Exception)
            {
                Font = null;
            }

            Panel = Box(Cream, Brown, 20, 4);
            Card = Box(CreamDark, Line, 12, 2);
            Title = Text(28, Orange, TextAnchor.MiddleLeft, false);
            Label = Text(15, Brown, TextAnchor.UpperLeft, true);
            Small = Text(12, Faded, TextAnchor.UpperLeft, true);
            Note = Text(14, new Color32(204, 98, 24, 255), TextAnchor.UpperLeft, true);
            Hint = Text(17, White, TextAnchor.MiddleCenter, false);

            Button = Pressable(White, Hover, Line, 10, 2, Brown, 14);
            GreenButton = Pressable(Green, GreenLight, GreenDark, 13, 3, White, 16);

            // A cell is a choice in a row: white, and green once it is picked.
            Cell = Pressable(White, Hover, Line, 9, 2, Brown, 13);
            Cell.fixedHeight = 30f;
            Cell.margin = new RectOffset(2, 2, 2, 2);
            Texture2D picked = Round(Green, GreenDark, 9, 2);
            Cell.onNormal.background = picked;
            Cell.onHover.background = picked;
            Cell.onActive.background = picked;
            Cell.onNormal.textColor = White;
            Cell.onHover.textColor = White;
            Cell.onActive.textColor = White;

            Track = Box(CreamDark, Line, 7, 2);
            Track.fixedHeight = 16f;
            Track.margin = new RectOffset(4, 4, 6, 6);
            Thumb = Box(Green, GreenDark, 7, 2);
            Thumb.fixedHeight = 16f;
            Thumb.fixedWidth = 22f;
        }

        private static GUIStyle Text(int size, Color32 color, TextAnchor anchor, bool wrap)
        {
            var style = new GUIStyle { font = Font, fontSize = size, alignment = anchor, wordWrap = wrap, richText = true };
            style.normal.textColor = color;
            return style;
        }

        /// <summary>A round-cornered box in one colour with a line around it, as a style that stretches cleanly.</summary>
        public static GUIStyle Box(Color32 fill, Color32 border, int radius, int line)
        {
            var style = new GUIStyle { font = Font };
            style.normal.background = Round(fill, border, radius, line);
            style.border = new RectOffset(radius + 1, radius + 1, radius + 1, radius + 1);
            return style;
        }

        public static GUIStyle Pressable(Color32 fill, Color32 over, Color32 border, int radius, int line, Color32 text, int size)
        {
            GUIStyle style = Box(fill, border, radius, line);
            Texture2D lit = Round(over, border, radius, line);
            style.hover.background = lit;
            style.active.background = lit;
            style.fontSize = size;
            style.alignment = TextAnchor.MiddleCenter;
            style.padding = new RectOffset(10, 10, 2, 2);
            style.margin = new RectOffset(3, 3, 3, 3);
            style.normal.textColor = text;
            style.hover.textColor = text;
            style.active.textColor = text;
            return style;
        }

        /// <summary>Paints a small round-cornered square that IMGUI stretches by its corners.</summary>
        public static Texture2D Round(Color32 fill, Color32 border, int radius, int line)
        {
            int size = radius * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            float half = size * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // How far this pixel is outside the round-cornered square (negative: inside).
                    float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - (half - radius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - (half - radius), 0f);
                    float outside = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    float edge = Mathf.Clamp01(0.5f - outside);
                    float inner = Mathf.Clamp01(0.5f - (outside + line));
                    Color color = Color.Lerp(border, fill, inner);
                    color.a *= edge;
                    pixels[y * size + x] = color;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>Text with a dark line around it, like the game's titles and on-screen text.</summary>
        public static void Outlined(Rect at, string text, GUIStyle style, Color32 color, Color32 outline, float thick)
        {
            Color old = style.normal.textColor;
            style.normal.textColor = outline;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx != 0 || dy != 0)
                    {
                        GUI.Label(new Rect(at.x + dx * thick, at.y + dy * thick, at.width, at.height), text, style);
                    }
                }
            }
            style.normal.textColor = color;
            GUI.Label(at, text, style);
            style.normal.textColor = old;
        }
    }
}
