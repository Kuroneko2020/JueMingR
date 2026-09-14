using System;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;

namespace JueMingR.TerrariaHost.Guidance
{
    internal sealed class GuidanceText
    {
        private readonly UiTextMetrics metrics = new UiTextMetrics();
        private readonly string[] lines = new string[3];
        private readonly F5Size[] sizes = new F5Size[3];
        private DynamicSpriteFont font;
        private string content;
        private float scale, maxWidth;
        private int count;
        private bool prepared;
        internal void Invalidate() { prepared = false; }
        internal float Width { get; private set; }
        internal float Height { get; private set; }
#if DEBUG
        internal int Layouts { get; private set; }
#endif
        internal void Prepare(DynamicSpriteFont current, string text, float size, float width)
        {
            if (prepared && ReferenceEquals(font, current) && content == text && scale == size && maxWidth == width) return;
            prepared = false;
            font = current; content = text; scale = size; maxWidth = width; count = 0; Width = Height = 0;
            if (font == null || string.IsNullOrEmpty(text) || width < 24) { prepared = true; return; }
#if DEBUG
            Layouts++;
#endif
            foreach (string original in text.Split('\n'))
            {
                if (count == 3) break;
                string line = original.Length > 128 ? original.Substring(0, 128) + "…" : original;
                var measured = metrics.Measure(font, line);
                if (measured.Width * scale + 4 > width)
                {
                    int low = 0, high = line.Length;
                    while (low < high)
                    { int mid = (low + high + 1) / 2; if (metrics.Measure(font, line.Substring(0, mid) + "…").Width * scale + 4 <= width) low = mid; else high = mid - 1; }
                    if (low > 0 && char.IsHighSurrogate(line[low - 1])) low--;
                    line = line.Substring(0, low) + "…"; measured = metrics.Measure(font, line);
                }
                lines[count] = line; sizes[count++] = measured;
                Width = Math.Max(Width, measured.Width * scale + 4); Height += measured.Height * scale + 4;
            }
            prepared = true;
        }
        internal Vector2 Clamp(Vector2 center, float width, float height)
        { return new Vector2(Math.Max(8, Math.Min(width - Width - 8, center.X - Width / 2)), Math.Max(8, Math.Min(height - Height - 8, center.Y - Height / 2))); }
        internal void Draw(SpriteBatch batch, Vector2 physicalTopLeft, Matrix inverse, Color color)
        {
            if (count == 0) return;
            var drawScale = new Vector2(scale * inverse.M11, scale * inverse.M22);
            float y = physicalTopLeft.Y;
            for (int i = 0; i < count; i++)
            {
                Vector2 physical = new Vector2(physicalTopLeft.X + (Width - sizes[i].Width * scale) / 2 - sizes[i].OffsetX * scale, y + 2 - sizes[i].OffsetY * scale);
                batch.DrawString(font, lines[i], Vector2.Transform(physical + new Vector2(2), inverse), new Color(12, 14, 20, 210) * (color.A / 255f), 0, Vector2.Zero, drawScale, SpriteEffects.None, 0);
                batch.DrawString(font, lines[i], Vector2.Transform(physical, inverse), color, 0, Vector2.Zero, drawScale, SpriteEffects.None, 0);
                y += sizes[i].Height * scale + 4;
            }
        }
    }
}
