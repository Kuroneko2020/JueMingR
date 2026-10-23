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
        internal Vector2? LastImeAnchor { get; private set; }
        internal GraphicsDevice Device { get { return pixel?.GraphicsDevice; } }
        internal bool ResourcesCurrent { get { return font != null && pixel != null && !pixel.IsDisposed &&
            ReferenceEquals(font, FontAssets.MouseText?.Value) && ReferenceEquals(pixel, TextureAssets.MagicPixel?.Value) &&
            ReferenceEquals(surface, TextureAssets.InventoryBack?.Value); } }
        internal bool Refresh()
        {
            var nextFont = FontAssets.MouseText?.Value; var nextPixel = TextureAssets.MagicPixel?.Value;
            var nextSurface = TextureAssets.InventoryBack?.Value;
            if (!ReferenceEquals(font, nextFont) || !ReferenceEquals(pixel, nextPixel) || !ReferenceEquals(surface, nextSurface)) Generation++;
            font = nextFont; pixel = nextPixel; surface = nextSurface;
            return font != null && pixel != null && !pixel.IsDisposed;
        }
        internal void Draw(IList<BlueprintLibraryPart> parts, F5Rect viewport, Matrix matrix, float scroll,
            BlueprintLibraryPart hovered, BlueprintLibraryPart armed, BlueprintLibraryPreviewCache cache, SingleLineEditView edit, bool ownsText)
        {
            SpriteBatch batch = Main.spriteBatch;
            if (batch == null) throw new InvalidOperationException("blueprint-batch-unavailable");
            var device = batch.GraphicsDevice; var oldScissor = device.ScissorRectangle;
            var oldRasterizer = device.RasterizerState; var oldBlend = device.BlendState;
            var oldDepth = device.DepthStencilState; var oldSampler = device.SamplerStates[0];
            if (clipped == null) clipped = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
            LastImeAnchor = null;
            bool began = false; batch.End();
            try
            {
                device.ScissorRectangle = F5ControlRenderer.ContentClip(viewport, matrix, oldScissor);
                LastClip = F5ControlRenderer.LogicalClip(device.ScissorRectangle, matrix);
                // Font atlases need linear filtering at fractional text/UI scale.
                // Finite summary textures get their own point-filtered pass below.
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, clipped, null, matrix); began = true;
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
                        if (cache.Get(part.PreviewKey) == null) Text(batch, source.Text, rect, source.TextSize, source.TextScale, Color.LightGray, true);
                    }
                    else if (source.Kind == F5ElementKind.Button || source.Kind == F5ElementKind.Field)
                    {
                        if (part.Command == BlueprintLibraryCommand.Name)
                        {
                            UiSurface.Panel(batch, pixel, rect, surface, !part.Enabled ? new Color(165, 165, 165) : ReferenceEquals(part, armed) ? new Color(185, 185, 185) : ReferenceEquals(part, hovered) ? Color.White : new Color(220, 220, 220), fractionalSurface: true);
                            if (!part.Editing) Text(batch, source.Text, new F5Rect(rect.X + 7, rect.Y + 4, rect.Width - 14, rect.Height - 8), source.TextSize, source.TextScale, part.Enabled ? new Color(230, 232, 224) : Color.Gray, false);
                        }
                        else
                        {
                            UiSurface.Panel(batch, pixel, F5Layout.ButtonSurface(source).Offset(viewport.X, viewport.Y - scroll), surface,
                                part.Enabled && ReferenceEquals(part, armed) ? new Color(185, 185, 185) : part.Enabled && ReferenceEquals(part, hovered) ? Color.White : !part.Enabled ? new Color(165, 165, 165) : new Color(220, 220, 220), fractionalSurface: true);
                            Text(batch, source.Text, rect, source.TextSize, source.TextScale, part.Enabled ? Color.White : Color.Gray, true);
                        }
                        if (part.Editing && edit != null)
                        {
                            float y = rect.Y + (rect.Height - edit.Size.Height) / 2;
                            if (edit.SelectionRight > edit.SelectionLeft) Fill(batch, new F5Rect(rect.X + 5 + edit.SelectionLeft, rect.Y + 3,
                                edit.SelectionRight - edit.SelectionLeft, rect.Height - 6), Color.CornflowerBlue * .45f);
                            Text(batch, edit.Text, new F5Rect(rect.X + 5, rect.Y, rect.Width - 10, rect.Height), edit.Size, .70f, Color.White, false);
                            if ((Environment.TickCount & 1023) < 512) Fill(batch, new F5Rect(rect.X + 5 + edit.Caret, y, 1, Math.Min(rect.Height - 4, edit.Size.Height)), Color.White);
                            Vector2 anchor;
                            if (ownsText && TryImeAnchor(rect, edit.Caret, edit.Size.Height, LastClip, matrix, Main.UIScaleMatrix, out anchor))
                            {
                                LastImeAnchor = anchor;
                                Main.instance.SetIMEPanelAnchor(anchor, 0);
                            }
                        }
                    }
                    else Text(batch, source.Text, rect, source.TextSize, source.TextScale, part.Ink, false);
                }
                if (hovered != null && hovered.Hint != null)
                {
                    var rect = hovered.Element.Rect.Offset(viewport.X, viewport.Y - scroll);
                    Text(batch, hovered.Hint, new F5Rect(Math.Max(viewport.X, Math.Min(rect.X, viewport.Right - hovered.HintSize.Width)), Math.Max(viewport.Y, rect.Y - hovered.HintSize.Height - 3), hovered.HintSize.Width, hovered.HintSize.Height), hovered.HintSize, .62f, Color.LightGoldenrodYellow, false);
                }
                batch.End(); began = false;
                // Draw in physical integer pixels. The worker image fits the
                // requested budget, so every aggregated source column survives
                // final consumption (including sparse columns skipped by 256→231).
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, clipped, null, Matrix.Identity); began = true;
                foreach (var part in parts)
                {
                    if (part.Element.Kind != F5ElementKind.Image) continue;
                    var rect = part.Element.Rect.Offset(viewport.X, viewport.Y - scroll);
                    var texture = cache.Get(part.PreviewKey);
                    if (texture == null || rect.Bottom <= viewport.Y || rect.Y >= viewport.Bottom) continue;
                    var target = PreviewTarget(rect, matrix, texture.Width, texture.Height);
                    Border(batch, new F5Rect(target.X, target.Y, target.Width, target.Height), new Color(86, 112, 150, 92));
                    batch.Draw(texture, target, Color.White);
                }
            }
            finally
            {
                try { if (began) batch.End(); }
                finally
                {
                    device.ScissorRectangle = oldScissor; device.RasterizerState = oldRasterizer; device.BlendState = oldBlend;
                    device.DepthStencilState = oldDepth; device.SamplerStates[0] = oldSampler;
                    // This layer resumes the caller's established deferred UI
                    // contract, with its actual states even on an exceptional draw.
                    batch.Begin(SpriteSortMode.Deferred, oldBlend, oldSampler, oldDepth, oldRasterizer, null, Main.UIScaleMatrix);
                }
            }
        }
        private void Text(SpriteBatch batch, string text, F5Rect rect, F5Size size, float scale, Color color, bool center)
        {
            if (string.IsNullOrEmpty(text)) return;
            var origin = new Vector2(rect.X + (center ? (rect.Width - size.Width) / 2 : 0) - size.OffsetX,
                rect.Y + (rect.Height - size.Height) / 2 - size.OffsetY);
            // Vanilla's ±2 logical outline overwhelms these small labels. Scale
            // the local outline with the glyph; Measure uses the same expansion.
            float border = Outline(scale);
            batch.DrawString(font, text, origin + new Vector2(-border, 0), Color.Black, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            batch.DrawString(font, text, origin + new Vector2(border, 0), Color.Black, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            batch.DrawString(font, text, origin + new Vector2(0, -border), Color.Black, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            batch.DrawString(font, text, origin + new Vector2(0, border), Color.Black, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            batch.DrawString(font, text, origin, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }
        internal static float Outline(float scale) { return scale; }
        internal static Rectangle PreviewTarget(F5Rect rect, Matrix matrix, int width, int height)
        {
            var a = Vector2.Transform(new Vector2(rect.X + 5, rect.Y + 5), matrix);
            var b = Vector2.Transform(new Vector2(rect.Right - 5, rect.Bottom - 5), matrix);
            int availableWidth = Math.Max(1, (int)Math.Floor(b.X - a.X)), availableHeight = Math.Max(1, (int)Math.Floor(b.Y - a.Y));
            // Old-size textures are never assigned to a new size key. A bounded
            // image may only be enlarged here, never point-sampled down again.
            float factor = Math.Max(1, Math.Min((float)availableWidth / width, (float)availableHeight / height));
            int targetWidth = Math.Max(width, (int)Math.Floor(width * factor)), targetHeight = Math.Max(height, (int)Math.Floor(height * factor));
            return new Rectangle((int)Math.Ceiling(a.X + (availableWidth - targetWidth) / 2f),
                (int)Math.Ceiling(a.Y + (availableHeight - targetHeight) / 2f), targetWidth, targetHeight);
        }
        internal static bool TryImeAnchor(F5Rect field, float caret, float textHeight, F5Rect clip, Matrix frozen, Matrix currentUi, out Vector2 anchor)
        {
            anchor = default(Vector2);
            float x = field.X + 5 + caret, top = field.Y + (field.Height - textHeight) / 2;
            // The caret and its baseline must be inside this frame's final
            // scissor, not merely inside an offscreen field's old logical box.
            float bottom = Math.Min(field.Bottom - 2, top + textHeight);
            if (x < field.X + 5 || x + 1 > field.Right - 4 || x < clip.X || x + 1 > clip.Right || top < clip.Y || bottom > clip.Bottom || bottom <= top) return false;
            var physical = Vector2.Transform(new Vector2(x, bottom), frozen);
            var logical = Vector2.Transform(physical, Matrix.Invert(currentUi));
            // Terraria 1.4.5.8 DrawIMEPanel subtracts 32 UI logical units.
            anchor = new Vector2(logical.X, logical.Y + 32); return true;
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
