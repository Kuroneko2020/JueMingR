using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using JueMingR.TerrariaHost.F5;
namespace JueMingR.TerrariaHost.About
{
    // Own only these two textures. A failed decode is latched until a real device
    // generation change; ordinary page re-entry cannot turn failure into a loop.
    internal sealed class SponsorImages : IDisposable
    {
        private GraphicsDevice device;
        private readonly Texture2D[] textures = new Texture2D[2];
        private readonly bool[] attempted = new bool[2];
        internal int LoadAttempts { get; private set; }
        internal bool Failed { get; private set; }
        internal void Draw(SpriteBatch batch, Texture2D pixel, F5Rect rect, string name, float scale)
        {
            if (!ReferenceEquals(device, batch.GraphicsDevice)) { Dispose(); device = batch.GraphicsDevice; Array.Clear(attempted, 0, 2); Failed = false; }
            int index = name == "wechat.png" ? 0 : 1;
            if (textures[index] != null && textures[index].IsDisposed) { textures[index] = null; attempted[index] = false; }
            if (!attempted[index])
            {
                attempted[index] = true; LoadAttempts++;
                try
                {
                    using (Stream stream = typeof(SponsorImages).Assembly.GetManifestResourceStream("JueMingR.About." + name))
                    { if (stream == null) throw new InvalidDataException(); textures[index] = Texture2D.FromStream(device, stream); }
                }
                catch { Failed = true; }
            }
            var texture = textures[index];
            if (texture != null)
                batch.Draw(texture, new Vector2(rect.X + 6 * scale, rect.Y + 6 * scale), null, Color.White, 0, Vector2.Zero,
                    new Vector2((rect.Width - 12 * scale) / texture.Width, (rect.Height - 12 * scale) / texture.Height), SpriteEffects.None, 0);
        }
        public void Dispose()
        {
            for (int i = 0; i < 2; i++)
                if (textures[i] != null) { textures[i].Dispose(); textures[i] = null; attempted[i] = false; }
            // Failed attempts retain their latch on the same device.
        }
    }
}
