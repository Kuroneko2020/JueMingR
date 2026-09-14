using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;

namespace JueMingR.TerrariaHost.F5
{
    internal static class F5ControlRenderer
    {
        // Content passes and name hit testing must agree on the last visible
        // pixel, including fractional UI scales and a narrower caller scissor.
        internal static Rectangle ContentClip(F5Rect view, Matrix matrix, Rectangle parent)
        {
            Vector2 a = Vector2.Transform(new Vector2(view.X, view.Y), matrix);
            Vector2 b = Vector2.Transform(new Vector2(view.Right, view.Bottom), matrix);
            var clip = new Rectangle((int)System.Math.Ceiling(a.X), (int)System.Math.Ceiling(a.Y),
                System.Math.Max(0, (int)System.Math.Floor(b.X) - (int)System.Math.Ceiling(a.X)),
                System.Math.Max(0, (int)System.Math.Floor(b.Y) - (int)System.Math.Ceiling(a.Y)));
            return Rectangle.Intersect(parent, clip);
        }
        internal static F5Rect LogicalClip(Rectangle clip, Matrix matrix)
        {
            var inverse = Matrix.Invert(matrix);
            var a = Vector2.Transform(new Vector2(clip.Left, clip.Top), inverse);
            var b = Vector2.Transform(new Vector2(clip.Right, clip.Bottom), inverse);
            return new F5Rect(a.X, a.Y, b.X - a.X, b.Y - a.Y);
        }
        internal static void Panel(SpriteBatch batch, Texture2D pixel, Texture2D skin, F5Rect rect)
        { UiSurface.Panel(batch, pixel, rect, skin, new Color(232, 232, 232)); }
        internal static void Text(SpriteBatch batch, DynamicSpriteFont font, F5Element element, Color color)
        { Utils.DrawBorderStringFourWay(batch, font, element.Text, element.Rect.X - element.TextSize.OffsetX,
            element.Rect.Y - element.TextSize.OffsetY, color, Color.Black, Vector2.Zero, element.TextScale); }
        // Selection is a preference projection, independent of current execution
        // conditions. The owner supplies a real failure separately from selection.
        internal static void Button(SpriteBatch batch, Texture2D pixel, Texture2D skin, DynamicSpriteFont font,
            F5Element element, bool hovered, bool enabled, Color? selected, float x = 0, float y = 0, bool subduedWhenDisabled = false, bool pressed = false)
        {
            UiSurface.Panel(batch, pixel, F5Layout.ButtonSurface(element).Offset(x, y), skin,
                enabled && pressed ? new Color(185, 185, 185) : enabled && hovered ? Color.White : subduedWhenDisabled && !enabled ? new Color(165, 165, 165) : new Color(220, 220, 220), fractionalSurface: true);
            var label = F5Layout.ButtonLabel(element).Offset(x, y);
            Utils.DrawBorderStringFourWay(batch, font, element.Text, label.X - element.TextSize.OffsetX,
                label.Y - element.TextSize.OffsetY, subduedWhenDisabled && !enabled ? Color.Gray : Color.White, Color.Black, Vector2.Zero, element.TextScale);
            if (!selected.HasValue) return;
            F5Rect line = F5Layout.ButtonUnderline(element).Offset(x, y);
            batch.Draw(pixel, new Vector2(line.X, line.Y), new Rectangle(0, 0, 1, 1), selected.Value,
                0, Vector2.Zero, new Vector2(line.Width, line.Height), SpriteEffects.None, 0);
        }
    }
}
