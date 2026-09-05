using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed class F5Renderer : IDisposable
    {
        private DynamicSpriteFont font;
        private Texture2D background, row, button, pixel;
        private readonly Func<string, F5Size> measure;
        private readonly DynamicSpriteFont.DrawCharacter collectGlyph;
        private float glyphLeft, glyphTop, glyphRight, glyphBottom;
        private RasterizerState clipped;
        private Texture2D roundCap;
        private readonly F5IconAtlas icons = new F5IconAtlas();
        private static readonly int[] OuterCornerInsets = { 4, 2, 1, 1, 0, 0, 0, 0, 0, 0 };
        internal int SkinGeneration { get; private set; }

        internal F5Renderer() { measure = Measure; collectGlyph = CollectGlyph; }

        internal bool RefreshResources()
        {
            font = FontAssets.MouseText == null ? null : FontAssets.MouseText.Value;
            Texture2D nextButton = TextureAssets.InventoryBack == null ? null : TextureAssets.InventoryBack.Value;
            // InventoryBack already carries the current skin's color. SettingsPanel
            // is a horizontal strip; InventoryBack13 requires a vanilla blue tint.
            Texture2D nextBackground = nextButton, nextRow = nextButton;
            Texture2D nextPixel = TextureAssets.MagicPixel == null ? null : TextureAssets.MagicPixel.Value;
            if (!ReferenceEquals(background, nextBackground) || !ReferenceEquals(row, nextRow) ||
                !ReferenceEquals(button, nextButton) || !ReferenceEquals(pixel, nextPixel)) SkinGeneration++;
            background = nextBackground; row = nextRow; button = nextButton; pixel = nextPixel;
            return font != null && pixel != null && !pixel.IsDisposed;
        }

        internal void Prepare(F5Interaction state, float width, float height, float scale)
        { state.Layout.Ensure(width, height, scale, state.Page, font, measure); state.ClampScroll(); }

        private F5Size Measure(string text)
        {
            glyphLeft = glyphTop = float.PositiveInfinity;
            glyphRight = glyphBottom = float.NegativeInfinity;
            // Public ReLogic traversal applies the same fallback, kerning, spacing
            // and glyph cropping offsets as DrawString. No texture readback.
            font.DrawCustomFast(collectGlyph, text, Vector2.Zero, Vector2.One);
            if (float.IsPositiveInfinity(glyphLeft)) return new F5Size(0, font.LineSpacing);
            return new F5Size(glyphRight - glyphLeft, glyphBottom - glyphTop, glyphLeft, glyphTop);
        }

        private void CollectGlyph(Texture2D texture, Vector2 position, Rectangle source, Vector2 scale)
        {
            if (source.Width <= 0 || source.Height <= 0) return;
            glyphLeft = Math.Min(glyphLeft, position.X); glyphTop = Math.Min(glyphTop, position.Y);
            glyphRight = Math.Max(glyphRight, position.X + source.Width * scale.X);
            glyphBottom = Math.Max(glyphBottom, position.Y + source.Height * scale.Y);
        }

        internal void Draw(F5Interaction state, Matrix matrix, bool biomeEnabled, bool biomeFailed)
        {
            SpriteBatch batch = Main.spriteBatch;
            if (batch == null) throw new InvalidOperationException("F5 SpriteBatch is unavailable.");
            GraphicsDevice device = batch.GraphicsDevice;
            icons.Ensure(device);
            EnsureRoundCap(device);
            F5Layout layout = state.Layout;
            Matrix layerMatrix = Main.UIScaleMatrix;
            // GameInterfaceLayer has begun this batch. Restore both its batch
            // contract and the actual graphics state even if a content draw fails.
            Rectangle oldScissor = device.ScissorRectangle;
            RasterizerState oldRasterizer = device.RasterizerState;
            BlendState oldBlend = device.BlendState;
            DepthStencilState oldDepth = device.DepthStencilState;
            SamplerState oldSampler = device.SamplerStates[0];
            if (clipped == null) clipped = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
            bool contentBatch = false;
            batch.End();
            try
            {
                batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, matrix);
                contentBatch = true;
                DrawChrome(batch, state);
                batch.End();
                contentBatch = false;
                F5Rect view = layout.Viewport.Offset(state.X, state.Y);
                Vector2 top = Vector2.Transform(new Vector2(view.X, view.Y), matrix);
                Vector2 bottom = Vector2.Transform(new Vector2(view.Right, view.Bottom), matrix);
                Rectangle clip = new Rectangle((int)Math.Ceiling(top.X), (int)Math.Ceiling(top.Y),
                    Math.Max(0, (int)Math.Floor(bottom.X) - (int)Math.Ceiling(top.X)),
                    Math.Max(0, (int)Math.Floor(bottom.Y) - (int)Math.Ceiling(top.Y)));
                device.ScissorRectangle = Rectangle.Intersect(oldScissor, clip);
                batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                    DepthStencilState.None, clipped, null, matrix);
                contentBatch = true;
                for (int i = 0; i < layout.Elements.Count; i++)
                {
                    F5Element element = layout.Elements[i];
                    F5Rect rect = element.Rect.Offset(view.X, view.Y - state.Scroll);
                    if (rect.Bottom <= view.Y || rect.Y >= view.Bottom) continue;
                    if (element.Kind == F5ElementKind.Panel) Panel(batch, rect, row, new Color(232, 232, 232));
                    else if (element.Kind == F5ElementKind.Field) Panel(batch, rect, row, new Color(180, 180, 180));
                    else if (element.Kind == F5ElementKind.Text)
                        Text(batch, element.Text, new Vector2(rect.X, rect.Y), element.TextScale,
                            element.Accent ? Color.LightGreen : Color.White, element.TextSize);
                    else if (element.Kind == F5ElementKind.BiomeStatus)
                        Text(batch, biomeFailed ? "当前：不可用" : biomeEnabled ? "当前：已开启" : "当前：已关闭",
                            new Vector2(rect.X, rect.Y), element.TextScale, Color.White,
                            layout.BiomeStatusSize(biomeFailed, biomeEnabled));
                    else
                    {
                        bool enabled = element.Command != F5Command.None && !biomeFailed;
                        bool selected = enabled && (element.Command == F5Command.EnableBiome) == biomeEnabled;
                        bool hovered = rect.Contains(state.PointerX, state.PointerY) && view.Contains(state.PointerX, state.PointerY);
                        Panel(batch, rect, button, enabled && hovered ? Color.White : new Color(220, 220, 220));
                        Text(batch, element.Text, new Vector2(rect.X + (rect.Width - element.TextSize.Width) / 2,
                            rect.Y + (rect.Height - element.TextSize.Height) / 2), element.TextScale,
                            selected ? Color.LightGreen : enabled && hovered ? Color.Gold : Color.White, element.TextSize);
                        if (selected) Fill(batch, new F5Rect(rect.X + 4, rect.Bottom - 3, rect.Width - 8, 2), Color.LightGreen);
                    }
                }
                batch.End();
                contentBatch = false;
                device.ScissorRectangle = oldScissor;
                batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, matrix);
                contentBatch = true;
                DrawHint(batch, state, biomeFailed);
            }
            finally
            {
                try { if (contentBatch) batch.End(); }
                finally
                {
                    device.ScissorRectangle = oldScissor;
                    device.RasterizerState = oldRasterizer;
                    device.BlendState = oldBlend;
                    device.DepthStencilState = oldDepth;
                    device.SamplerStates[0] = oldSampler;
                    batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, layerMatrix);
                }
            }
        }

        private void DrawHint(SpriteBatch batch, F5Interaction state, bool biomeFailed)
        {
            if (!state.OwnsPointer) return;
            F5Element hover = state.HitButton(state.PointerX - state.X, state.PointerY - state.Y);
            if (hover == null) return;
            int index = F5Layout.HintIndex(hover, biomeFailed);
            if (index < 0) return;
            F5Size size = state.Layout.HintSize(index);
            float x = Math.Max(state.X + 8, Math.Min(state.X + state.Layout.Window.Width - size.Width - 24, state.PointerX + 14));
            float y = Math.Max(state.Y + 8, Math.Min(state.Y + state.Layout.Window.Height - size.Height - 24, state.PointerY + 18));
            Panel(batch, new F5Rect(x, y, size.Width + 16, size.Height + 16), background, Color.White);
            Text(batch, F5Layout.HintText(index), new Vector2(x + 8, y + 8), 0.65f, Color.White, size);
        }

        private void DrawChrome(SpriteBatch batch, F5Interaction state)
        {
            F5Layout layout = state.Layout;
            Panel(batch, layout.Window.Offset(state.X, state.Y), background, new Color(235, 235, 235), true);
            Text(batch, "JueMingR", new Vector2(state.X + 14,
                state.Y + layout.Title.Y + (layout.Title.Height - layout.TitleSize.Height) / 2),
                0.75f, Color.White, layout.TitleSize);
            for (int i = 0; i < F5Layout.Pages.Length; i++)
            {
                F5Rect nav = layout.Navigation(i).Offset(state.X, state.Y);
                bool selected = state.Page == i;
                bool hovered = nav.Contains(state.PointerX, state.PointerY);
                Panel(batch, nav, button, selected || hovered ? Color.White : new Color(220, 220, 220));
                F5Size size = layout.NavigationSize(i);
                Color foreground = selected ? Color.Gold : Color.White;
                icons.Draw(batch, i, layout.NavigationIcon(i).Offset(state.X, state.Y), foreground);
                F5Rect label = layout.NavigationLabel(i).Offset(state.X, state.Y);
                Text(batch, F5Layout.Pages[i], new Vector2(label.X, label.Y), 0.75f, foreground, size);
                if (selected) Fill(batch, new F5Rect(nav.X + 4, nav.Bottom - 3, nav.Width - 8, 2), Color.Gold);
            }
            Panel(batch, layout.ContentPanel.Offset(state.X, state.Y), row, new Color(205, 205, 205));
            if (layout.MaxScroll > 0)
            {
                RoundBar(batch, layout.ScrollTrackVisual.Offset(state.X, state.Y), Color.Black * 0.4f);
                bool hover = layout.ScrollTrack.Offset(state.X, state.Y).Contains(state.PointerX, state.PointerY);
                RoundBar(batch, layout.ScrollThumbVisual(state.Scroll).Offset(state.X, state.Y),
                    Color.White * (hover || state.DraggingScroll ? 0.85f : 0.6f));
            }
        }

        private void EnsureRoundCap(GraphicsDevice device)
        {
            if (roundCap != null && !roundCap.IsDisposed && ReferenceEquals(roundCap.GraphicsDevice, device)) return;
            if (roundCap != null) roundCap.Dispose();
            var data = new Color[64];
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
            {
                float distance = (float)Math.Sqrt((x - 3.5f) * (x - 3.5f) + (y - 3.5f) * (y - 3.5f));
                data[y * 8 + x] = Color.White * Math.Max(0, Math.Min(1, 4 - distance));
            }
            var candidate = new Texture2D(device, 8, 8);
            try { candidate.SetData(data); roundCap = candidate; }
            catch { candidate.Dispose(); roundCap = null; throw; }
        }

        private void RoundBar(SpriteBatch batch, F5Rect rect, Color color)
        {
            float radius = rect.Width / 2;
            batch.Draw(roundCap, new Vector2(rect.X, rect.Y), new Rectangle(0, 0, 8, 4), color,
                0, Vector2.Zero, rect.Width / 8, SpriteEffects.None, 0);
            batch.Draw(pixel, new Vector2(rect.X, rect.Y + radius), new Rectangle(0, 0, 1, 1), color,
                0, Vector2.Zero, new Vector2(rect.Width, Math.Max(0, rect.Height - 2 * radius)), SpriteEffects.None, 0);
            batch.Draw(roundCap, new Vector2(rect.X, rect.Bottom - radius), new Rectangle(0, 4, 8, 4), color,
                0, Vector2.Zero, rect.Width / 8, SpriteEffects.None, 0);
        }

        private void Panel(SpriteBatch batch, F5Rect rect, Texture2D texture, Color tint, bool roundedOuter = false)
        {
            if (texture == null || texture.IsDisposed || texture.Width <= 20 || texture.Height <= 20 || rect.Width < 20 || rect.Height < 20)
            {
                Color fallback = new Color(46, 50, 76);
                if (!roundedOuter) Fill(batch, rect, fallback);
                else
                {
                    Fill(batch, new F5Rect(rect.X, rect.Y + 4, rect.Width, rect.Height - 8), fallback);
                    for (int band = 0; band < 4; band++)
                    {
                        int inset = OuterCornerInsets[band];
                        Fill(batch, new F5Rect(rect.X + inset, rect.Y + band, rect.Width - 2 * inset, 1), fallback);
                        Fill(batch, new F5Rect(rect.X + inset, rect.Bottom - band - 1, rect.Width - 2 * inset, 1), fallback);
                    }
                }
                return;
            }
            if (roundedOuter)
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

        private void Fill(SpriteBatch batch, F5Rect rect, Color color)
        { batch.Draw(pixel, new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height), color); }

        private void Text(SpriteBatch batch, string text, Vector2 position, float scale, Color color, F5Size size)
        { Utils.DrawBorderStringFourWay(batch, font, text, position.X - size.OffsetX, position.Y - size.OffsetY,
            color, Color.Black, Vector2.Zero, scale); }

        public void Dispose()
        {
            if (clipped != null) { clipped.Dispose(); clipped = null; }
            if (roundCap != null) { roundCap.Dispose(); roundCap = null; }
            icons.Dispose();
        }
    }
}
