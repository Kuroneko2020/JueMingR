using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using ReLogic.Graphics;
using Terraria;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed class UiTextMetrics
    {
        private readonly DynamicSpriteFont.DrawCharacter collect;
        private float left, top, right, bottom;
        internal UiTextMetrics() { collect = Collect; }
        internal F5Size Measure(DynamicSpriteFont font, string text)
        {
            left = top = float.PositiveInfinity; right = bottom = float.NegativeInfinity;
            font.DrawCustomFast(collect, text, Vector2.Zero, Vector2.One);
            return float.IsPositiveInfinity(left) ? new F5Size(0, font.LineSpacing) : new F5Size(right - left, bottom - top, left, top);
        }
        private void Collect(Texture2D texture, Vector2 position, Rectangle source, Vector2 scale)
        {
            if (source.Width <= 0 || source.Height <= 0) return;
            left = Math.Min(left, position.X); top = Math.Min(top, position.Y);
            right = Math.Max(right, position.X + source.Width * scale.X); bottom = Math.Max(bottom, position.Y + source.Height * scale.Y);
        }
    }
    // F5 and Notes borrow the same current skin and use one surface implementation.
    // This helper owns no assets; skin replacement must never dispose shared textures.
    internal static class UiSurface
    {
        private static readonly int[] OuterCornerInsets = { 4, 2, 1, 1, 0, 0, 0, 0, 0, 0 };
        private static void Decoration(SpriteBatch batch, Texture2D pixel, F5Rect rect, Color color)
        { batch.Draw(pixel, new Vector2(rect.X, rect.Y), new Rectangle(0, 0, 1, 1), color,
            0, Vector2.Zero, new Vector2(rect.Width, rect.Height), SpriteEffects.None, 0); }
        internal static void Panel(SpriteBatch batch, Texture2D pixel, F5Rect rect, Texture2D texture, Color tint,
            bool roundedOuter = false, bool fractionalSurface = false)
        {
            if (texture == null || texture.IsDisposed || texture.Width <= 20 || texture.Height <= 20 || rect.Width < 20 || rect.Height < 20)
            {
                Color fallback = new Color(46, 50, 76) * (tint.A / 255f);
                if (fractionalSurface) Decoration(batch, pixel, rect, fallback);
                else if (!roundedOuter) Fill(batch, pixel, rect, fallback);
                else
                {
                    Fill(batch, pixel, new F5Rect(rect.X, rect.Y + 4, rect.Width, rect.Height - 8), fallback);
                    for (int band = 0; band < 4; band++)
                    {
                        int inset = OuterCornerInsets[band];
                        Fill(batch, pixel, new F5Rect(rect.X + inset, rect.Y + band, rect.Width - 2 * inset, 1), fallback);
                        Fill(batch, pixel, new F5Rect(rect.X + inset, rect.Bottom - band - 1, rect.Width - 2 * inset, 1), fallback);
                    }
                }
                return;
            }
            if (roundedOuter || fractionalSurface)
            {
                // Clip both texture fill and frame to the same six-unit outer
                // silhouette, including a skin whose source corners are square.
                // Only four 10x10 corner slices need bounded one-unit bands.
                for (int rowIndex = 0; rowIndex < 3; rowIndex++) for (int column = 0; column < 3; column++)
                {
                    int sx = column == 0 ? 0 : column == 1 ? 10 : texture.Width - 10;
                    int sy = rowIndex == 0 ? 0 : rowIndex == 1 ? 10 : texture.Height - 10;
                    int sw = column == 1 ? texture.Width - 20 : 10;
                    int sh = rowIndex == 1 ? texture.Height - 20 : 10;
                    if (fractionalSurface)
                    {
                        // Function surfaces share the exact glyph-derived center
                        // and bottom edge used by their labels and state marks.
                        float x = rect.X + (column == 0 ? 0 : column == 1 ? 10 : rect.Width - 10);
                        float y = rect.Y + (rowIndex == 0 ? 0 : rowIndex == 1 ? 10 : rect.Height - 10);
                        float width = column == 1 ? rect.Width - 20 : 10;
                        float height = rowIndex == 1 ? rect.Height - 20 : 10;
                        batch.Draw(texture, new Vector2(x, y), new Rectangle(sx, sy, sw, sh), tint,
                            0, Vector2.Zero, new Vector2(width / sw, height / sh), SpriteEffects.None, 0);
                        continue;
                    }
                    int dx = (int)rect.X + (column == 0 ? 0 : column == 1 ? 10 : (int)rect.Width - 10);
                    int dy = (int)rect.Y + (rowIndex == 0 ? 0 : rowIndex == 1 ? 10 : (int)rect.Height - 10);
                    int dw = column == 1 ? (int)rect.Width - 20 : 10;
                    int dh = rowIndex == 1 ? (int)rect.Height - 20 : 10;
                    if (column == 1 || rowIndex == 1)
                        batch.Draw(texture, new Rectangle(dx, dy, dw, dh), new Rectangle(sx, sy, sw, sh), tint);
                    else for (int band = 0; band < 10; band++)
                    {
                        int edgeDistance = rowIndex == 0 ? band : 9 - band;
                        int inset = OuterCornerInsets[edgeDistance];
                        int left = column == 0 ? inset : 0;
                        batch.Draw(texture, new Rectangle(dx + left, dy + band, 10 - inset, 1),
                            new Rectangle(sx + left, sy + band, 10 - inset, 1), tint);
                    }
                }
                return;
            }
            Utils.DrawSplicedPanel(batch, texture, (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height,
                10, 10, 10, 10, tint);
        }

        private static void Fill(SpriteBatch batch, Texture2D pixel, F5Rect rect, Color color)
        { batch.Draw(pixel, new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height), color); }

    }
}
