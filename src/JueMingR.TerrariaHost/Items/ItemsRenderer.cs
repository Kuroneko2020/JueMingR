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
        internal float RowHeight { get; private set; }
        internal bool Refresh()
        {
            font = FontAssets.MouseText?.Value; pixel = TextureAssets.MagicPixel?.Value; surface = TextureAssets.InventoryBack?.Value;
            if (font == null || pixel == null || pixel.IsDisposed) return false;
            F5Size size = metrics.Measure(font, "自动出售 未绑定 Ag");
            RowHeight = Math.Max(34, size.Height * .75f + 12); return true;
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
        internal void Panel(F5Rect rect) { UiSurface.Panel(batch, pixel, rect, surface, new Color(232, 232, 232)); }
        internal void Text(string text, F5Rect rect, Color color, float scale = .7f)
        {
            if (string.IsNullOrEmpty(text)) return;
            F5Size size = metrics.Measure(font, text);
            // Clip labels by measured glyphs, retaining the established readable
            // scale. Never shrink a resource-pack font to hide a geometry bug.
            if (size.Width * scale > rect.Width - 4)
            {
                int low = 0, high = text.Length;
                while (low < high) { int mid = (low + high + 1) / 2; if (metrics.Measure(font, text.Substring(0, mid) + "…").Width * scale <= rect.Width - 4) low = mid; else high = mid - 1; }
                text = text.Substring(0, low) + "…"; size = metrics.Measure(font, text);
            }
            Utils.DrawBorderStringFourWay(batch, font, text, rect.X + 2 - size.OffsetX * scale,
                rect.Y + (rect.Height - size.Height * scale) / 2 - size.OffsetY * scale, color, Color.Black, Vector2.Zero, scale);
        }
        internal void Button(F5Rect rect, string text, bool selected, bool enabled = true)
        {
            UiSurface.Panel(batch, pixel, rect, surface, selected ? Color.White : new Color(205, 205, 215), fractionalSurface: true);
            Text(text, rect, enabled ? selected ? Color.LightGreen : Color.White : Color.Gray);
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
        public void Dispose() { if (clipped != null) { clipped.Dispose(); clipped = null; } }
    }
}
