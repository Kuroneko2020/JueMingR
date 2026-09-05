using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace JueMingR.TerrariaHost.F5
{
    // Twelve tabs and one keyboard, fixed offline-exported alpha masks. Owns one GPU
    // atlas; no Legacy assembly, filesystem asset lookup, SVG parser or font icon.
    internal sealed class F5IconAtlas : IDisposable
    {
        internal const int KeyboardIndex = 12;
        private const int Size = 72, Count = 13;
        private Texture2D texture;
        private readonly Rectangle[] bounds = new Rectangle[Count];

        internal void Ensure(GraphicsDevice device)
        {
            // Reuse only on the owning device; publish a new atlas after its upload succeeds.
            if (texture != null && !texture.IsDisposed && ReferenceEquals(texture.GraphicsDevice, device)) return;
            Dispose();
            var data = new Color[Size * Size * Count];
            using (var stream = typeof(F5IconAtlas).Assembly.GetManifestResourceStream("JueMingR.F5.TabIcons.alpha"))
            {
                if (stream == null || stream.Length != data.Length)
                    throw new InvalidOperationException("F5 icon resource is missing or invalid.");
                for (int icon = 0; icon < Count; icon++)
                {
                    int left = Size, top = Size, right = 0, bottom = 0;
                    for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
                    {
                        int alpha = stream.ReadByte();
                        if (alpha < 0) throw new InvalidOperationException("F5 icon resource is truncated.");
                        data[(icon * Size + y) * Size + x] = new Color(alpha, alpha, alpha, alpha);
                        if (alpha == 0) continue;
                        left = Math.Min(left, x); top = Math.Min(top, y);
                        right = Math.Max(right, x + 1); bottom = Math.Max(bottom, y + 1);
                    }
                    if (right <= left || bottom <= top) throw new InvalidOperationException("F5 icon is empty.");
                    bounds[icon] = new Rectangle(left, icon * Size + top, right - left, bottom - top);
                }
            }
            var candidate = new Texture2D(device, Size, Size * Count);
            try { candidate.SetData(data); texture = candidate; }
            catch { candidate.Dispose(); throw; }
        }

        internal void Draw(SpriteBatch batch, int index, F5Rect box, Color color)
        {
            Rectangle source = bounds[index];
            // Bounds center the ink; the full source domain sets its size. Fitting
            // cropped ink to the slot would enlarge both artwork and stroke width.
            float scale = Math.Min(box.Width, box.Height) / Size;
            var position = new Vector2(box.X + (box.Width - source.Width * scale) / 2,
                box.Y + (box.Height - source.Height * scale) / 2);
            batch.Draw(texture, position, source, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }

        public void Dispose() { if (texture != null) { texture.Dispose(); texture = null; } }
    }
}
