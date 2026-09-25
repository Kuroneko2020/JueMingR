using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using JueMingR.TerrariaHost.F5;
namespace JueMingR.TerrariaHost.About
{
    // Local reproduction of the authorized Z page's static pixel ornaments.
    // Skin readback occurs once per borrowed texture identity; no owned GPU asset,
    // animation or per-frame palette sampling is introduced.
    internal sealed class AboutPainter
    {
        internal static readonly Color Gold = new Color(255, 238, 204);
        internal static readonly Color Muted = new Color(204, 216, 236);
        private Texture2D source;
        private Color panel, content, row, border;
        internal int PaletteReads { get; private set; }
        internal AboutPainter() { SetPalette(new Color(75, 98, 154)); }
        private void SetPalette(Color value)
        { panel = Mix(value, new Color(15, 18, 30), .08); content = Mix(value, new Color(8, 10, 22), .14); row = Mix(value, new Color(10, 13, 28), .20); border = Mix(value, Color.White, .30); }
        private static Color Mix(Color a, Color b, double amount)
        { return new Color((int)(a.R + (b.R - a.R) * amount), (int)(a.G + (b.G - a.G) * amount), (int)(a.B + (b.B - a.B) * amount)); }
        internal void Prepare(Texture2D skin)
        {
            if (ReferenceEquals(source, skin)) return;
            source = skin; SetPalette(new Color(75, 98, 154));
            if (skin == null || skin.IsDisposed || (long)skin.Width * skin.Height > 1048576) return;
            try
            {
                PaletteReads++; var pixels = new Color[skin.Width * skin.Height]; skin.GetData(pixels);
                long r = 0, g = 0, b = 0, weight = 0;
                int x0 = skin.Width / 4, x1 = skin.Width - x0, y0 = skin.Height / 4, y1 = skin.Height - y0;
                for (int y = y0; y < y1; y += Math.Max(1, (y1 - y0) / 12))
                    for (int x = x0; x < x1; x += Math.Max(1, (x1 - x0) / 12)) Accumulate(pixels[y * skin.Width + x], ref r, ref g, ref b, ref weight);
                if (weight == 0) for (int i = 0; i < pixels.Length; i += Math.Max(1, pixels.Length / 256)) Accumulate(pixels[i], ref r, ref g, ref b, ref weight);
                if (weight > 0) SetPalette(new Color((int)(r / weight), (int)(g / weight), (int)(b / weight)));
            }
            catch { /* Keep fallback for this resource generation, without retrying each frame. */ }
        }
        private static void Accumulate(Color c, ref long r, ref long g, ref long b, ref long weight)
        { if (c.A < 32) return; r += c.R * c.A; g += c.G * c.A; b += c.B * c.A; weight += c.A; }
        internal void Card(SpriteBatch batch, Texture2D pixel, F5Rect rect, string style, float scale)
        {
            var c = new Canvas(batch, pixel, rect.X, rect.Y, scale); float w = rect.Width / scale, h = rect.Height / scale;
            if (style == "backdrop")
            {
                c.Round(0, 0, w, h, 8, content, 218); c.Round(2, 2, w - 4, h - 4, 6, row, 76);
                c.Dots(18, 20, w - 36, border, 54); c.Dots(18, h - 20, w - 36, border, 54); c.Stars(14, 14, w - 28, h - 28, 28, border, 64); return;
            }
            int alpha = style == "header" ? 238 : style == "portrait" ? 232 : style == "info" ? 228 : style == "footer" ? 218 : 224;
            c.Round(0, 0, w, h, 8, border, Math.Min(236, alpha)); c.Round(2, 2, w - 4, h - 4, 6, content, Math.Min(232, alpha)); c.Round(5, 5, w - 10, h - 10, 4, row, 70);
            if (style == "header")
            {
                c.Corners(0, 0, w, h, 14, border, 166); c.Stars(12, 12, w - 24, h - 24, 14, border, 96);
                c.Spark(82, 28, 3, new Color(255, 226, 148), 220); c.Spark(w - 86, 30, 3, new Color(255, 226, 148), 220);
                c.Dots(26, h / 2 + 2, 128, border, 112); c.Dots(w - 154, h / 2 + 2, 128, border, 112);
                float x = (w - 214) / 2, y = (h - 42) / 2;
                c.Round(x, y, 214, 42, 6, new Color(68, 72, 156), 92); c.Round(x + 1, y + 1, 212, 40, 5, new Color(52, 58, 144), 84);
                c.Fill(x + 16, y + 9, 4, 4, new Color(255, 226, 156), 220); c.Fill(x + 194, y + 29, 4, 4, new Color(255, 226, 156), 220);
                c.Spark(x + 12, y + 19, 2, new Color(255, 226, 156), 218); c.Spark(x + 202, y + 19, 2, new Color(255, 226, 156), 218);
            }
            else if (style == "portrait")
            {
                c.Corners(0, 0, w, h, 14, border, 166); c.Round(12, 12, w - 24, h - 24, 6, panel, 104);
                c.Border(18, 18, w - 36, h - 36, 1, border, 94); c.Stars(22, 22, w - 44, h - 44, 18, border, 78);
                c.Dots(34, 36, w - 68, border, 82); c.Dots(34, h - 36, w - 68, border, 82); c.Dots(38, h / 2 + 34, w - 76, border, 74);
                c.Spark(46, 46, 2, border, 126); c.Spark(w - 46, h - 46, 2, border, 126);
            }
            else if (style == "info") { Icon(c, 18, 11, "info"); c.Dots(18, 31, w - 36, border, 104); }
            else if (style == "feedback") { Icon(c, 39, 9, "message"); c.Dots(34, 31, w - 68, border, 96); }
            else if (style == "qr")
            {
                Icon(c, 15, 9, "heart"); c.Dots(38, 34, w - 56, border, 96);
                float size = Math.Max(76, Math.Min(96, Math.Min((w - 42) / 2, h - 66)));
                c.Fill(w / 2, 52, 1, Math.Max(30, size - 20), new Color(176, 192, 226), 78);
            }
            else if (style == "footer")
            {
                c.Corners(0, 0, w, h, 10, border, 166); c.Stars(14, 14, w - 28, h - 28, 14, border, 90);
                c.Dots(30, 27, 56, border, 104); c.Dots(w - 86, 27, 56, border, 104);
                c.Spark(70, 27, 2, border, 146); c.Spark(w - 70, 27, 2, border, 146);
            }
        }
        internal void FeedbackButton(SpriteBatch batch, Texture2D pixel, F5Rect rect, bool hovered)
        {
            float scale = rect.Width / 198; var c = new Canvas(batch, pixel, rect.X, rect.Y, scale);
            c.Round(0, 0, 198, 26, 6, panel, hovered ? 158 : 122); c.Round(1, 1, 196, 24, 5, row, 68); c.Border(1, 1, 196, 24, 1, border, hovered ? 214 : 152);
            c.Fill(8, 21, 182, 1, Color.White, hovered ? 58 : 36); c.Spark(18, 13, 2, new Color(255, 226, 150), hovered ? 240 : 190); c.Spark(180, 13, 2, new Color(255, 226, 150), hovered ? 240 : 190);
        }
        internal void QrFrame(SpriteBatch batch, Texture2D pixel, F5Rect rect, float scale, bool wechat)
        {
            var c = new Canvas(batch, pixel, rect.X, rect.Y, scale); float size = rect.Width / scale;
            c.Fill(0, 0, size, size, Color.White, 255);
            c.Border(-1, -1, size + 2, size + 2, 1, wechat ? new Color(64, 190, 108) : new Color(64, 156, 235), 255);
        }
        private void Icon(Canvas c, float x, float y, string kind)
        {
            c.Round(x, y, 20, 20, 4, panel, 180); c.Border(x, y, 20, 20, 1, border, 220); c.Fill(x + 3, y + 3, 14, 1, Color.White, 54);
            Color tint = kind == "heart" ? new Color(255, 148, 164) : new Color(214, 194, 246);
            if (kind == "heart") { c.Fill(x + 6, y + 6, 3, 3, tint, 230); c.Fill(x + 11, y + 6, 3, 3, tint, 230); c.Fill(x + 5, y + 9, 10, 3, tint, 230); c.Fill(x + 7, y + 12, 6, 3, tint, 230); c.Fill(x + 9, y + 15, 2, 2, tint, 230); }
            else if (kind == "message") { c.Border(x + 4, y + 4, 12, 10, 2, tint, 220); c.Fill(x + 7, y + 13, 5, 2, tint, 220); }
            else { c.Border(x + 5, y + 4, 10, 12, 2, tint, 220); c.Fill(x + 8, y + 8, 4, 2, tint, 220); c.Fill(x + 8, y + 12, 4, 2, tint, 180); }
        }
        private struct Canvas
        {
            private readonly SpriteBatch batch; private readonly Texture2D pixel; private readonly float ox, oy, scale;
            internal Canvas(SpriteBatch batch, Texture2D pixel, float x, float y, float scale) { this.batch = batch; this.pixel = pixel; ox = x; oy = y; this.scale = scale; }
            internal void Fill(float x, float y, float w, float h, Color tint, int alpha)
            { if (w > 0 && h > 0) batch.Draw(pixel, new Vector2(ox + x * scale, oy + y * scale), new Rectangle(0, 0, 1, 1), tint * (alpha / 255f), 0, Vector2.Zero, new Vector2(w * scale, h * scale), SpriteEffects.None, 0); }
            internal void Round(float x, float y, float w, float h, int radius, Color tint, int alpha)
            {
                Fill(x, y + radius, w, h - radius * 2, tint, alpha);
                for (int i = 0; i < radius; i++) { float inset = (float)Math.Ceiling(radius - Math.Sqrt(radius * radius - (radius - i - .5) * (radius - i - .5))); Fill(x + inset, y + i, w - inset * 2, 1, tint, alpha); Fill(x + inset, y + h - i - 1, w - inset * 2, 1, tint, alpha); }
            }
            internal void Border(float x, float y, float w, float h, float thickness, Color tint, int alpha)
            { Fill(x, y, w, thickness, tint, alpha); Fill(x, y + h - thickness, w, thickness, tint, alpha); Fill(x, y + thickness, thickness, h - thickness * 2, tint, alpha); Fill(x + w - thickness, y + thickness, thickness, h - thickness * 2, tint, alpha); }
            internal void Dots(float x, float y, float width, Color tint, int alpha)
            { for (float dx = 0; dx < width; dx += 8) Fill(x + dx, y, Math.Min(3, width - dx), 1, tint, alpha); }
            internal void Spark(float x, float y, int size, Color tint, int alpha)
            { Fill(x - size, y, size * 2 + 1, 1, tint, alpha); Fill(x, y - size, 1, size * 2 + 1, tint, alpha); }
            internal void Corners(float x, float y, float w, float h, float length, Color tint, int alpha)
            {
                for (int i = 0; i < 4; i++) { bool right = i % 2 != 0, bottom = i / 2 != 0; float dx = right ? x + w - 12 : x + 10, dy = bottom ? y + h - 12 : y + 10; Fill(right ? dx + 2 - length : dx, dy, length, 2, tint, alpha); Fill(dx, bottom ? dy + 2 - length : dy, 2, length, tint, alpha); }
            }
            internal void Stars(float x, float y, float w, float h, int count, Color border, int alpha)
            {
                for (int i = 0; i < count; i++)
                {
                    uint hash; unchecked { hash = 2166136261u; hash = (hash ^ (uint)x) * 16777619u; hash = (hash ^ (uint)y) * 16777619u; hash = (hash ^ (uint)w) * 16777619u; hash = (hash ^ (uint)h) * 16777619u; hash = (hash ^ (uint)(i + 1)) * 16777619u; }
                    float sx = x + hash % (uint)Math.Max(1, w), sy = y + (hash >> 8) % (uint)Math.Max(1, h); int size = (int)((hash >> 17) & 1) + 1, tint = (int)((hash >> 19) & 3), a = alpha + (int)((hash >> 21) & 31);
                    Color color = tint == 1 ? new Color(255, 226, 156) : tint == 2 ? new Color(198, 214, 255) : tint == 3 ? new Color(216, 178, 255) : border;
                    Fill(sx, sy, size, size, color, a); if ((hash & 7) == 0) Spark(sx + size / 2, sy + size / 2, 1, color, Math.Min(255, a + 26));
                }
            }
        }
    }
}
