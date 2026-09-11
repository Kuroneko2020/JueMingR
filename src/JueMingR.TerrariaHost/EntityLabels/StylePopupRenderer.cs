using System;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;

namespace JueMingR.TerrariaHost.EntityLabels
{
    // Borrows the current F5 resources and caller-owned UI batch. Gradients are
    // finite color bands cached by draft identity/revision, never GPU resources.
    internal sealed class StylePopupRenderer
    {
        private readonly Color[,] bands = new Color[3, 32];
        private StyleEditor cached;
        private int revision = -1;
        internal void Draw(StylePopup popup, SpriteBatch batch, DynamicSpriteFont font, Texture2D skin, Texture2D pixel)
        {
            if (!popup.Visible) return;
            var editor = popup.Editor; var layout = popup.Layout;
            if (!ReferenceEquals(cached, editor) || revision != editor.Revision)
            {
                for (int i = 0; i < 32; i++)
                {
                    double fraction = i / 31d;
                    bands[0, i] = Rgb(StyleEditor.HslToRgb(fraction * 360, 100, 50));
                    bands[1, i] = Rgb(StyleEditor.HslToRgb(editor.Hue, fraction * 100, editor.Lightness));
                    bands[2, i] = Rgb(StyleEditor.HslToRgb(editor.Hue, editor.Saturation, fraction * 100));
                }
                cached = editor; revision = editor.Revision;
            }
            UiSurface.Panel(batch, pixel, layout.Panel, skin, Color.White, true);
            for (int i = 0; i < layout.Text.Count; i++)
            {
                var line = layout.Text[i];
                Text(batch, font, line.Text, line.Rect.Offset(layout.Panel.X, layout.Panel.Y), line.TextSize,
                    line.TextScale, i >= layout.ErrorStartIndex ? Color.LightCoral : Color.White);
            }
            Fill(batch, pixel, layout.Preview.Offset(layout.Panel.X, layout.Panel.Y), Rgb(editor.Rgb));
            var field = layout.HexField.Offset(layout.Panel.X, layout.Panel.Y);
            UiSurface.Panel(batch, pixel, field, skin, new Color(180, 180, 180), true);
            if (popup.TextInput.Editing && layout.Selection.Width > 0)
                Fill(batch, pixel, layout.Selection.Offset(layout.Panel.X, layout.Panel.Y), new Color(90, 130, 190));
            Text(batch, font, editor.Hex, new F5Rect(field.X + 8, field.Y + (field.Height - layout.HexSize.Height) / 2, 0, 0), layout.HexSize, .7f, Color.White);
            if (popup.TextInput.Editing)
                Fill(batch, pixel, new F5Rect(layout.Panel.X + layout.CaretX, field.Y + 5, 1, field.Height - 10), Color.White);
            for (int axis = 0; axis < 3; axis++)
            {
                var track = layout.Sliders[axis].Offset(layout.Panel.X, layout.Panel.Y);
                float top = track.Y + (track.Height - 14) / 2;
                for (int i = 0; i < 32; i++)
                {
                    float left = track.X + track.Width * i / 32;
                    Fill(batch, pixel, new F5Rect(left, top, track.Width * (i + 1) / 32 + track.X - left, 14), bands[axis, i]);
                }
                double value = axis == 0 ? editor.Hue / 360 : axis == 1 ? editor.Saturation / 100 : editor.Lightness / 100;
                float thumb = track.X + (float)value * track.Width;
                Fill(batch, pixel, new F5Rect(thumb - 3, top - 3, 6, 20), Color.Black);
                Fill(batch, pixel, new F5Rect(thumb - 1, top - 2, 2, 18), Color.White);
            }
            for (int i = 0; i < layout.Buttons.Count; i++)
            {
                var command = layout.Commands[i];
                F5ControlRenderer.Button(batch, pixel, skin, font, layout.Buttons[i], popup.Hovered == command,
                    StylePopupLayout.Enabled(command, popup.NameSize), null, layout.Panel.X, layout.Panel.Y, true,
                    popup.Pressed == command && popup.Hovered == command);
            }
        }
        private static Color Rgb(int rgb) { return new Color((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }
        private static void Fill(SpriteBatch batch, Texture2D pixel, F5Rect rect, Color color)
        { batch.Draw(pixel, new Vector2(rect.X, rect.Y), new Rectangle(0, 0, 1, 1), color, 0, Vector2.Zero, new Vector2(rect.Width, rect.Height), SpriteEffects.None, 0); }
        private static void Text(SpriteBatch batch, DynamicSpriteFont font, string text, F5Rect rect, F5Size size, float scale, Color color)
        { Utils.DrawBorderStringFourWay(batch, font, text, rect.X - size.OffsetX, rect.Y - size.OffsetY, color, Color.Black, Vector2.Zero, scale); }
    }
}
