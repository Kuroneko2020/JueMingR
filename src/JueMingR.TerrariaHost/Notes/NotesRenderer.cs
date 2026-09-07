using System;
using JueMingR.Features.Notes;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Notes
{
    internal sealed class NotesRenderer : IDisposable
    {
        private DynamicSpriteFont font;
        private Texture2D pixel;
        private RasterizerState clipped;
        private SpriteBatch batch;
        private Matrix matrix;
        private Rectangle outerClip;
        internal object FontIdentity { get { return font; } }
        internal bool Refresh()
        {
            font = FontAssets.MouseText == null ? null : FontAssets.MouseText.Value;
            pixel = TextureAssets.MagicPixel == null ? null : TextureAssets.MagicPixel.Value;
            return font != null && pixel != null && !pixel.IsDisposed;
        }
        internal NotesTextLayout Layout(string text, float width, float scale)
        { return new NotesTextLayout(text, width, element => font.MeasureString(element).X * scale); }
        internal void Pass(Matrix transform, F5Rect? clip, Action draw)
        {
            SpriteBatch target = Main.spriteBatch; if (target == null) throw new InvalidOperationException("Notes batch unavailable.");
            GraphicsDevice device = target.GraphicsDevice;
            Rectangle oldScissor = device.ScissorRectangle; RasterizerState oldRasterizer = device.RasterizerState;
            BlendState oldBlend = device.BlendState; DepthStencilState oldDepth = device.DepthStencilState; SamplerState oldSampler = device.SamplerStates[0];
            Matrix original = Main.UIScaleMatrix;
            if (clipped == null) clipped = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true };
            bool began = false; target.End();
            try
            {
                batch = target; matrix = transform; outerClip = clip.HasValue ? Rectangle.Intersect(oldScissor, ScreenRect(clip.Value)) : oldScissor;
                device.ScissorRectangle = outerClip;
                Begin(); began = true; draw();
            }
            finally
            {
                try { if (began) target.End(); }
                finally
                {
                    batch = null; device.ScissorRectangle = oldScissor; device.RasterizerState = oldRasterizer;
                    device.BlendState = oldBlend; device.DepthStencilState = oldDepth; device.SamplerStates[0] = oldSampler;
                    target.Begin(SpriteSortMode.Deferred, null, null, null, null, null, original);
                }
            }
        }
        private void Begin()
        { batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, clipped, null, matrix); }
        private Rectangle ScreenRect(F5Rect rect)
        {
            Vector2 a = Vector2.Transform(new Vector2(rect.X, rect.Y), matrix), b = Vector2.Transform(new Vector2(rect.Right, rect.Bottom), matrix);
            return new Rectangle((int)Math.Ceiling(a.X), (int)Math.Ceiling(a.Y), Math.Max(0, (int)Math.Floor(b.X) - (int)Math.Ceiling(a.X)), Math.Max(0, (int)Math.Floor(b.Y) - (int)Math.Ceiling(a.Y)));
        }
        internal void Fill(F5Rect rect, Color color)
        { batch.Draw(pixel, new Rectangle((int)rect.X, (int)rect.Y, Math.Max(1, (int)rect.Width), Math.Max(1, (int)rect.Height)), color); }
        internal void Label(string text, float x, float y, float scale, Color color)
        {
            // Literal font path: neither this nor TextView enters ChatManager.
            batch.DrawString(font, text, new Vector2(x + 1, y + 1), Color.Black * 0.9f, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            batch.DrawString(font, text, new Vector2(x, y), color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }
        internal void Button(F5Rect rect, string text, bool hover, Color? color = null)
        {
            Fill(rect, hover ? new Color(84, 102, 134, 240) : new Color(47, 63, 91, 235));
            Label(text, rect.X + 5, rect.Y + 3, 0.65f, color ?? Color.White);
        }
        internal void TextView(NotesTextLayout layout, F5Rect rect, float scroll, float scale, float lineHeight, Color color,
            NoteEditor editor = null, string composition = "")
        {
            if (layout == null) return;
            batch.End(); batch.GraphicsDevice.ScissorRectangle = Rectangle.Intersect(outerClip, ScreenRect(rect)); Begin();
            try
            {
                int first = Math.Max(0, (int)(scroll / lineHeight)), last = Math.Min(layout.Lines.Count - 1, (int)((scroll + rect.Height) / lineHeight));
                for (int lineIndex = first; lineIndex <= last; lineIndex++)
                {
                    NotesTextLine line = layout.Lines[lineIndex]; float x = rect.X, y = rect.Y + lineIndex * lineHeight - scroll;
                    for (int i = line.First; i < line.Last; i++)
                    { Label(layout.Element(i), x, y, scale, color); x += layout.Advance(i); }
                }
                if (editor != null && layout.Text == editor.Text)
                {
                    float x = rect.X + layout.CaretX(editor.Caret), y = rect.Y + layout.LineOf(editor.Caret) * lineHeight - scroll;
                    if ((Environment.TickCount & 1023) < 512) Fill(new F5Rect(x, y, 1, lineHeight - 2), Color.White);
                    if (!String.IsNullOrEmpty(composition))
                    {
                        float width = Math.Min(rect.Width, font.MeasureString(composition).X * scale + 8);
                        float cx = Math.Max(rect.X, Math.Min(rect.Right - width, x));
                        Fill(new F5Rect(cx, y, width, lineHeight), new Color(20, 28, 40, 255)); Label(composition, cx + 2, y, scale, Color.Gold);
                    }
                    // Native candidate layer uses current UI coordinates after all
                    // interface layers. Convert through screen from our frozen matrix.
                    if (y + lineHeight > rect.Y && y < rect.Bottom)
                    {
                        Vector2 screen = Vector2.Transform(new Vector2(x, Math.Min(rect.Bottom, y + lineHeight)), matrix);
                        Vector2 anchor = Vector2.Transform(screen, Matrix.Invert(Main.UIScaleMatrix));
                        Main.instance.SetIMEPanelAnchor(new Vector2(anchor.X, anchor.Y + 32), 0);
                    }
                }
            }
            finally { batch.End(); batch.GraphicsDevice.ScissorRectangle = outerClip; Begin(); }
        }
        public void Dispose()
        {
            if (clipped != null) { clipped.Dispose(); clipped = null; }
            // Shared vanilla fonts/textures are borrowed, never disposed here.
            font = null; pixel = null;
        }
    }
}
