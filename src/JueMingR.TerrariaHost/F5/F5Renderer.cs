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
        private RasterizerState clipped;
        internal int SkinGeneration { get; private set; }

        internal F5Renderer() { measure = Measure; }

        internal bool RefreshResources()
        {
            font = FontAssets.MouseText == null ? null : FontAssets.MouseText.Value;
            Texture2D nextBackground = TextureAssets.SettingsPanel == null ? null : TextureAssets.SettingsPanel.Value;
            Texture2D nextRow = TextureAssets.InventoryBack13 == null ? null : TextureAssets.InventoryBack13.Value;
            Texture2D nextButton = TextureAssets.InventoryBack == null ? null : TextureAssets.InventoryBack.Value;
            Texture2D nextPixel = TextureAssets.MagicPixel == null ? null : TextureAssets.MagicPixel.Value;
            if (!ReferenceEquals(background, nextBackground) || !ReferenceEquals(row, nextRow) ||
                !ReferenceEquals(button, nextButton) || !ReferenceEquals(pixel, nextPixel)) SkinGeneration++;
            background = nextBackground; row = nextRow; button = nextButton; pixel = nextPixel;
            return font != null && pixel != null && !pixel.IsDisposed;
        }

        internal void Prepare(F5Interaction state, float width, float height, float scale)
        { state.Layout.Ensure(width, height, scale, state.Page, font, measure); state.ClampScroll(); }

        private F5Size Measure(string text)
        { Vector2 size = font.MeasureString(text); return new F5Size(size.X, size.Y); }

        internal void Draw(F5Interaction state, Matrix matrix, bool biomeEnabled)
        {
            SpriteBatch batch = Main.spriteBatch;
            if (batch == null) throw new InvalidOperationException("F5 SpriteBatch is unavailable.");
            GraphicsDevice device = batch.GraphicsDevice;
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
                    if (element.Kind == F5ElementKind.Panel) Panel(batch, rect, row, Color.White);
                    else if (element.Kind == F5ElementKind.Text)
                        Text(batch, element.Text, new Vector2(rect.X, rect.Y), element.TextScale,
                            element.Accent ? Color.LightGreen : Color.White);
                    else if (element.Kind == F5ElementKind.BiomeStatus)
                        Text(batch, biomeEnabled ? "当前：已开启" : "当前：已关闭", new Vector2(rect.X, rect.Y), element.TextScale, Color.White);
                    else
                    {
                        bool enabled = element.Command != F5Command.None;
                        bool selected = enabled && (element.Command == F5Command.EnableBiome) == biomeEnabled;
                        bool hovered = rect.Contains(state.PointerX, state.PointerY) && view.Contains(state.PointerX, state.PointerY);
                        Panel(batch, rect, button, enabled ? Color.White : new Color(140, 140, 140));
                        Text(batch, element.Text, new Vector2(rect.X + (rect.Width - element.TextSize.Width) / 2,
                            rect.Y + (rect.Height - element.TextSize.Height) / 2), element.TextScale,
                            selected ? Color.LightGreen : enabled && hovered ? Color.Gold : enabled ? Color.White : Color.Silver);
                        if (selected) Fill(batch, new F5Rect(rect.X + 4, rect.Bottom - 3, rect.Width - 8, 2), Color.LightGreen);
                    }
                }
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

        private void DrawChrome(SpriteBatch batch, F5Interaction state)
        {
            F5Layout layout = state.Layout;
            Panel(batch, layout.Window.Offset(state.X, state.Y), background, Color.White);
            Panel(batch, layout.Title.Offset(state.X, state.Y), row, Color.White);
            Text(batch, "JueMingR", new Vector2(state.X + 14, state.Y + 7), 0.75f, Color.White);
            for (int i = 0; i < F5Layout.Pages.Length; i++)
            {
                F5Rect nav = layout.Navigation(i).Offset(state.X, state.Y);
                bool selected = state.Page == i;
                Panel(batch, nav, button, selected ? Color.White : new Color(175, 175, 175));
                F5Size size = layout.NavigationSize(i);
                Text(batch, F5Layout.Pages[i], new Vector2(nav.X + (nav.Width - size.Width) / 2,
                    nav.Y + (nav.Height - size.Height) / 2), 0.75f, selected ? Color.Gold : Color.White);
                if (selected) Fill(batch, new F5Rect(nav.X + 4, nav.Bottom - 3, nav.Width - 8, 2), Color.Gold);
            }
            Panel(batch, layout.ContentPanel.Offset(state.X, state.Y), row, Color.White);
            Fill(batch, layout.ScrollTrack.Offset(state.X, state.Y), new Color(30, 30, 40));
            Fill(batch, layout.ScrollThumb(state.Scroll).Offset(state.X, state.Y), Color.Silver);
        }

        private void Panel(SpriteBatch batch, F5Rect rect, Texture2D texture, Color tint)
        {
            if (texture == null || texture.IsDisposed || texture.Width <= 20 || texture.Height <= 20 || rect.Width < 20 || rect.Height < 20)
            { Fill(batch, rect, new Color(46, 50, 76)); return; }
            Utils.DrawSplicedPanel(batch, texture, (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height,
                10, 10, 10, 10, tint);
        }

        private void Fill(SpriteBatch batch, F5Rect rect, Color color)
        { batch.Draw(pixel, new Rectangle((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height), color); }

        private void Text(SpriteBatch batch, string text, Vector2 position, float scale, Color color)
        { Utils.DrawBorderStringFourWay(batch, font, text, position.X, position.Y, color, Color.Black, Vector2.Zero, scale); }

        public void Dispose() { if (clipped != null) { clipped.Dispose(); clipped = null; } }
    }
}
