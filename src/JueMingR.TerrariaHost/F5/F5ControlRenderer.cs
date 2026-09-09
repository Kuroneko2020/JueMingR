using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;

namespace JueMingR.TerrariaHost.F5
{
    internal static class F5ControlRenderer
    {
        internal static void Panel(SpriteBatch batch, Texture2D pixel, Texture2D skin, F5Rect rect)
        { UiSurface.Panel(batch, pixel, rect, skin, new Color(232, 232, 232)); }
        internal static void Text(SpriteBatch batch, DynamicSpriteFont font, F5Element element, Color color)
        { Utils.DrawBorderStringFourWay(batch, font, element.Text, element.Rect.X - element.TextSize.OffsetX,
            element.Rect.Y - element.TextSize.OffsetY, color, Color.Black, Vector2.Zero, element.TextScale); }
        // Selection is a preference projection, independent of current execution
        // conditions. The owner supplies a real failure separately from selection.
        internal static void Button(SpriteBatch batch, Texture2D pixel, Texture2D skin, DynamicSpriteFont font,
            F5Element element, bool hovered, bool enabled, Color? selected, float x = 0, float y = 0)
        {
            UiSurface.Panel(batch, pixel, F5Layout.ButtonSurface(element).Offset(x, y), skin,
                enabled && hovered ? Color.White : new Color(220, 220, 220), fractionalSurface: true);
            var label = F5Layout.ButtonLabel(element).Offset(x, y);
            Utils.DrawBorderStringFourWay(batch, font, element.Text, label.X - element.TextSize.OffsetX,
                label.Y - element.TextSize.OffsetY, Color.White, Color.Black, Vector2.Zero, element.TextScale);
            if (!selected.HasValue) return;
            F5Rect line = F5Layout.ButtonUnderline(element).Offset(x, y);
            batch.Draw(pixel, new Vector2(line.X, line.Y), new Rectangle(0, 0, 1, 1), selected.Value,
                0, Vector2.Zero, new Vector2(line.Width, line.Height), SpriteEffects.None, 0);
        }
    }
}
