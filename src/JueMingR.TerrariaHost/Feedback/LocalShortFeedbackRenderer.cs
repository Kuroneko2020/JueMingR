using System;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;

namespace JueMingR.TerrariaHost.Feedback
{
    internal sealed class LocalShortFeedbackRenderer
    {
        private readonly UiTextMetrics metrics = new UiTextMetrics();
        private DynamicSpriteFont font;
        private string text;
        private float requestedScale, width, height, scale;
        private F5Size size;
        internal Vector2 NativeSize { get; private set; }
        internal float Height { get { return size.Height * scale + 8; } }
#if DEBUG
        internal int Layouts { get; private set; }
#endif
        internal void Prepare(DynamicSpriteFont current, string content, float requested, float viewWidth, float viewHeight)
        {
            if (ReferenceEquals(font, current) && text == content && requestedScale == requested && width == viewWidth && height == viewHeight) return;
            font = current; text = content; requestedScale = requested; width = viewWidth; height = viewHeight;
            size = metrics.Measure(font, text); NativeSize = font.MeasureString(text);
            // Keep the final status word. Fit the complete bounded switch name,
            // rather than ellipsizing away the actual result on a small viewport.
            scale = Math.Min(requested, Math.Min((width - 32) / Math.Max(1, size.Width), (height - 48) / (4 * Math.Max(1, size.Height + 8))));
            scale = Math.Max(.1f, scale);
#if DEBUG
            Layouts++;
#endif
        }
        internal void Draw(SpriteBatch batch, Vector2 topCenter, Matrix inverse, Color color)
        {
            Vector2 p = new Vector2(topCenter.X - size.Width * scale / 2 - size.OffsetX * scale, topCenter.Y - size.OffsetY * scale);
            Vector2 drawScale = new Vector2(scale * inverse.M11, scale * inverse.M22);
            Color outline = Color.Black * (color.A / 255f);
            batch.DrawString(font, text, Vector2.Transform(p + new Vector2(-2, 0), inverse), outline, 0, Vector2.Zero, drawScale, SpriteEffects.None, 0);
            batch.DrawString(font, text, Vector2.Transform(p + new Vector2(2, 0), inverse), outline, 0, Vector2.Zero, drawScale, SpriteEffects.None, 0);
            batch.DrawString(font, text, Vector2.Transform(p + new Vector2(0, -2), inverse), outline, 0, Vector2.Zero, drawScale, SpriteEffects.None, 0);
            batch.DrawString(font, text, Vector2.Transform(p + new Vector2(0, 2), inverse), outline, 0, Vector2.Zero, drawScale, SpriteEffects.None, 0);
            batch.DrawString(font, text, Vector2.Transform(p, inverse), color, 0, Vector2.Zero, drawScale, SpriteEffects.None, 0);
        }
    }
}
