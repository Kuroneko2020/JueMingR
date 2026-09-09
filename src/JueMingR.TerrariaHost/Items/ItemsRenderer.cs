using System;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Items
{
    // Uses the F5 surface and real glyph bounds. Only the scissor state below
    // is owned; game and resource-pack textures/fonts are borrowed each frame.
    internal sealed class ItemsRenderer : IDisposable
    {
        private readonly UiTextMetrics metrics = new UiTextMetrics();
        private DynamicSpriteFont font;
        private Texture2D pixel, surface;
        private RasterizerState clipped;
        private SpriteBatch batch;
        internal float RowHeight { get; set; }
        internal int Generation { get; private set; }
        private readonly System.Collections.Generic.Dictionary<string, F5Size> sizes = new System.Collections.Generic.Dictionary<string, F5Size>();
        private F5Size Measure(string text)
        {
            F5Size size;
            if (!sizes.TryGetValue(text, out size))
            { if (sizes.Count >= 512) sizes.Clear(); sizes[text] = size = metrics.Measure(font, text); }
            return size;
        }
        internal bool Refresh()
        {
            var nextFont = FontAssets.MouseText?.Value; var nextPixel = TextureAssets.MagicPixel?.Value; var nextSurface = TextureAssets.InventoryBack?.Value;
            if (!ReferenceEquals(font, nextFont) || !ReferenceEquals(pixel, nextPixel) || !ReferenceEquals(surface, nextSurface))
            { Generation++; sizes.Clear(); }
            font = nextFont; pixel = nextPixel; surface = nextSurface;
            if (font == null || pixel == null || pixel.IsDisposed) return false;
            return true;
        }
        internal void Pass(Matrix matrix, F5Rect clip, Action draw)
        {
            SpriteBatch target = Main.spriteBatch;
            if (target == null) throw new InvalidOperationException("items-batch-unavailable");
            GraphicsDevice device = target.GraphicsDevice;
            Rectangle oldScissor = device.ScissorRectangle;
            RasterizerState oldRasterizer = device.RasterizerState; BlendState oldBlend = device.BlendState;
            DepthStencilState oldDepth = device.DepthStencilState; SamplerState oldSampler = device.SamplerStates[0];
            if (clipped == null) clipped = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
            Vector2 a = Vector2.Transform(new Vector2(clip.X, clip.Y), matrix), b = Vector2.Transform(new Vector2(clip.Right, clip.Bottom), matrix);
            var rectangle = new Rectangle((int)Math.Ceiling(a.X), (int)Math.Ceiling(a.Y), Math.Max(0, (int)Math.Floor(b.X) - (int)Math.Ceiling(a.X)), Math.Max(0, (int)Math.Floor(b.Y) - (int)Math.Ceiling(a.Y)));
            bool began = false; target.End();
            try
            {
                device.ScissorRectangle = Rectangle.Intersect(oldScissor, rectangle);
                target.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, clipped, null, matrix);
                began = true; batch = target; draw();
            }
            finally
            {
                try { if (began) target.End(); }
                finally
                {
                    batch = null; device.ScissorRectangle = oldScissor; device.RasterizerState = oldRasterizer; device.BlendState = oldBlend;
                    device.DepthStencilState = oldDepth; device.SamplerStates[0] = oldSampler;
                    target.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix);
                }
            }
        }
        internal void Panel(F5Rect rect) { F5ControlRenderer.Panel(batch, pixel, surface, rect); }
        internal void Label(F5Element element) { F5ControlRenderer.Text(batch, font, element, Color.White); }
        internal void Button(F5Element element, bool selected, bool enabled, bool off, bool hovered)
        { F5ControlRenderer.Button(batch, pixel, surface, font, element, hovered, enabled, selected ? (Color?)(off ? Color.IndianRed : Color.LightGreen) : null, subduedWhenDisabled: true); }
        internal void Text(string text, F5Rect rect, Color color, float scale = .7f)
        {
            if (string.IsNullOrEmpty(text)) return;
            F5Size size = Measure(text);
            // Clip labels by measured glyphs, retaining the established readable
            // scale. Never shrink a resource-pack font to hide a geometry bug.
            if (size.Width * scale > rect.Width - 4)
            {
                int low = 0, high = text.Length;
                while (low < high) { int mid = (low + high + 1) / 2; if (Measure(text.Substring(0, mid) + "…").Width * scale <= rect.Width - 4) low = mid; else high = mid - 1; }
                text = text.Substring(0, low) + "…"; size = Measure(text);
            }
            Utils.DrawBorderStringFourWay(batch, font, text, rect.X + 2 - size.OffsetX * scale,
                rect.Y + (rect.Height - size.Height * scale) / 2 - size.OffsetY * scale, color, Color.Black, Vector2.Zero, scale);
        }
        internal void Item(int type, F5Rect rect)
        {
            if (type <= 0 || type >= TextureAssets.Item.Length) return;
            Main.instance.LoadItem(type);
            Texture2D texture = TextureAssets.Item[type]?.Value;
            if (texture == null || texture.IsDisposed) return;
            Rectangle frame = Main.itemAnimations[type] == null ? texture.Bounds : Main.itemAnimations[type].GetFrame(texture);
            float scale = Math.Min(1, Math.Min((rect.Width - 4) / frame.Width, (rect.Height - 4) / frame.Height));
            batch.Draw(texture, new Vector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2), frame, Color.White, 0,
                new Vector2(frame.Width / 2f, frame.Height / 2f), scale, SpriteEffects.None, 0);
        }
        internal void Selection(F5Rect r)
        {
            var color = Color.LightGreen;
            // One 6x6 rounded dot carries selection without tinting the item.
            Fill(new F5Rect(r.Right - 8, r.Y + 3, 2, 1), color);
            Fill(new F5Rect(r.Right - 9, r.Y + 4, 4, 1), color);
            Fill(new F5Rect(r.Right - 10, r.Y + 5, 6, 2), color);
            Fill(new F5Rect(r.Right - 9, r.Y + 7, 4, 1), color);
            Fill(new F5Rect(r.Right - 8, r.Y + 8, 2, 1), color);
        }
        internal void Cross(F5Rect r, bool enabled)
        {
            // Only the drawing is inset; the layout retains its 18x18 hit area.
            Fill(new F5Rect(r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6), Color.Black * .5f);
            var color = enabled ? Color.LightGray : Color.Gray;
            Stroke(new Vector2(r.X + 6, r.Y + 6), new Vector2(r.Right - 6, r.Bottom - 6), color);
            Stroke(new Vector2(r.Right - 6, r.Y + 6), new Vector2(r.X + 6, r.Bottom - 6), color);
        }
        // Scale is relative to the sampled source, not a destination size.
        // MagicPixel can be a tall texture (and can be replaced by a pack), so
        // both primitives must sample one texel before applying logical lengths.
        private void Fill(F5Rect r, Color color)
        { batch.Draw(pixel, new Vector2(r.X, r.Y), new Rectangle(0, 0, 1, 1), color, 0, Vector2.Zero, new Vector2(r.Width, r.Height), SpriteEffects.None, 0); }
        private void Stroke(Vector2 a, Vector2 b, Color color)
        { Vector2 d = b - a; batch.Draw(pixel, a, new Rectangle(0, 0, 1, 1), color, (float)Math.Atan2(d.Y, d.X), new Vector2(0, .5f), new Vector2(d.Length(), 1.5f), SpriteEffects.None, 0); }
        public void Dispose() { if (clipped != null) { clipped.Dispose(); clipped = null; } }
    }
}
