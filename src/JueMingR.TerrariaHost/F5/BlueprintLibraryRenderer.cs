using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.F5
{
    // Layout prepares every glyph and rectangle. Draw only projects cached
    // geometry and borrowed skins; it never opens assets or measures names.
    internal sealed class BlueprintLibraryRenderer : IDisposable
    {
        private DynamicSpriteFont font;
        private Texture2D pixel, surface;
        private RasterizerState clipped;
        internal int Generation { get; private set; }
        internal F5Rect LastClip { get; private set; }
        internal GraphicsDevice Device { get { return pixel?.GraphicsDevice; } }
        internal bool Refresh()
        {
            var nextFont = FontAssets.MouseText?.Value; var nextPixel = TextureAssets.MagicPixel?.Value;
            var nextSurface = TextureAssets.InventoryBack?.Value;
            if (!ReferenceEquals(font, nextFont) || !ReferenceEquals(pixel, nextPixel) || !ReferenceEquals(surface, nextSurface)) Generation++;
            font = nextFont; pixel = nextPixel; surface = nextSurface;
            return font != null && pixel != null && !pixel.IsDisposed;
        }
        internal void Draw(IList<BlueprintLibraryPart> parts, F5Rect viewport, Matrix matrix, float scroll,
            BlueprintLibraryPart hovered, BlueprintLibraryPart armed, BlueprintLibraryPreviewCache cache, SingleLineEditView edit)
        {
            SpriteBatch batch = Main.spriteBatch;
            if (batch == null) throw new InvalidOperationException("blueprint-batch-unavailable");
            var device = batch.GraphicsDevice; var oldScissor = device.ScissorRectangle;
            var oldRasterizer = device.RasterizerState; var oldBlend = device.BlendState;
            var oldDepth = device.DepthStencilState; var oldSampler = device.SamplerStates[0];
            if (clipped == null) clipped = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
            bool began = false; batch.End();
            try
            {
                device.ScissorRectangle = F5ControlRenderer.ContentClip(viewport, matrix, oldScissor);
                LastClip = F5ControlRenderer.LogicalClip(device.ScissorRectangle, matrix);
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, clipped, null, matrix); began = true;
                foreach (var part in parts)
                {
                    var source = part.Element; var rect = source.Rect.Offset(viewport.X, viewport.Y - scroll);
                    if (rect.Bottom <= viewport.Y || rect.Y >= viewport.Bottom) continue;
                    if (source.Kind == F5ElementKind.Panel)
                    {
                        UiSurface.Panel(batch, pixel, rect, surface, new Color(232, 232, 232));
                        if (part.Card && part.Selected) RoundedSelection(batch, new F5Rect(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2));
                    }
                    else if (source.Kind == F5ElementKind.Image)
                    {
                        UiSurface.Panel(batch, pixel, rect, surface, new Color(190, 190, 190));
                        var texture = cache.Get(part.PreviewKey);
                        if (texture != null)
                        {
                            float factor = Math.Min((rect.Width - 10) / texture.Width, (rect.Height - 10) / texture.Height);
                            float width = texture.Width * factor, height = texture.Height * factor;
                            var target = new F5Rect(rect.X + (rect.Width - width) / 2, rect.Y + (rect.Height - height) / 2, width, height);
                            Border(batch, target, new Color(86, 112, 150, 92));
                            batch.Draw(texture, new Vector2(target.X, target.Y), null, Color.White, 0, Vector2.Zero, factor, SpriteEffects.None, 0);
                        }
                        else Text(batch, source.Text, rect, source.TextSize, source.TextScale, Color.LightGray, true);
                    }
                    else if (source.Kind == F5ElementKind.Button || source.Kind == F5ElementKind.Field)
                    {
                        if (part.Command == BlueprintLibraryCommand.Name)
                        {
                            UiSurface.Panel(batch, pixel, rect, surface, !part.Enabled ? new Color(165, 165, 165) : ReferenceEquals(part, armed) ? new Color(185, 185, 185) : ReferenceEquals(part, hovered) ? Color.White : new Color(220, 220, 220), fractionalSurface: true);
                            if (!part.Editing) Text(batch, source.Text, new F5Rect(rect.X + 7, rect.Y + 4, rect.Width - 14, rect.Height - 8), source.TextSize, source.TextScale, part.Enabled ? new Color(230, 232, 224) : Color.Gray, false);
                        }
                        else F5ControlRenderer.Button(batch, pixel, surface, font, source, ReferenceEquals(part, hovered), part.Enabled,
                            null, viewport.X, viewport.Y - scroll, true, ReferenceEquals(part, armed));
                        if (part.Editing && edit != null)
                        {
                            float y = rect.Y + (rect.Height - edit.Size.Height) / 2;
                            if (edit.SelectionRight > edit.SelectionLeft) Fill(batch, new F5Rect(rect.X + 5 + edit.SelectionLeft, rect.Y + 3,
                                edit.SelectionRight - edit.SelectionLeft, rect.Height - 6), Color.CornflowerBlue * .45f);
                            Text(batch, edit.Text, new F5Rect(rect.X + 5, rect.Y, rect.Width - 10, rect.Height), edit.Size, .70f, Color.White, false);
                            if ((Environment.TickCount & 1023) < 512) Fill(batch, new F5Rect(rect.X + 5 + edit.Caret, y, 1, Math.Min(rect.Height - 4, edit.Size.Height)), Color.White);
                        }
                    }
                    else Text(batch, source.Text, rect, source.TextSize, source.TextScale, part.Ink, false);
                }
            }
            finally
            {
                try { if (began) batch.End(); }
                finally
                {
                    device.ScissorRectangle = oldScissor; device.RasterizerState = oldRasterizer; device.BlendState = oldBlend;
                    device.DepthStencilState = oldDepth; device.SamplerStates[0] = oldSampler;
                    batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix);
                }
            }
        }
        private void Text(SpriteBatch batch, string text, F5Rect rect, F5Size size, float scale, Color color, bool center)
        {
            if (string.IsNullOrEmpty(text)) return;
            Utils.DrawBorderStringFourWay(batch, font, text, rect.X + (center ? (rect.Width - size.Width) / 2 : 0) - size.OffsetX,
                rect.Y + (rect.Height - size.Height) / 2 - size.OffsetY, color, Color.Black, Vector2.Zero, scale);
        }
        private void Fill(SpriteBatch batch, F5Rect r, Color color)
        { if (r.Width > 0 && r.Height > 0) batch.Draw(pixel, new Vector2(r.X, r.Y), new Rectangle(0, 0, 1, 1), color, 0, Vector2.Zero, new Vector2(r.Width, r.Height), SpriteEffects.None, 0); }
        private void RoundedSelection(SpriteBatch batch, F5Rect r)
        {
            var color = new Color(86, 112, 72, 58);
            Fill(batch, new F5Rect(r.X, r.Y + 5, r.Width, r.Height - 10), color);
            for (int y = 0; y < 5; y++)
            { int inset = y == 0 ? 3 : y < 2 ? 1 : 0; Fill(batch, new F5Rect(r.X + inset, r.Y + y, r.Width - inset * 2, 1), color); Fill(batch, new F5Rect(r.X + inset, r.Bottom - y - 1, r.Width - inset * 2, 1), color); }
        }
        private void Border(SpriteBatch batch, F5Rect r, Color color)
        { Fill(batch, new F5Rect(r.X - 1, r.Y - 1, r.Width + 2, 1), color); Fill(batch, new F5Rect(r.X - 1, r.Bottom, r.Width + 2, 1), color); Fill(batch, new F5Rect(r.X - 1, r.Y, 1, r.Height), color); Fill(batch, new F5Rect(r.Right, r.Y, 1, r.Height), color); }
        public void Dispose() { if (clipped != null) { clipped.Dispose(); clipped = null; } }
    }
}
