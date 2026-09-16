using System;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace JueMingR.TerrariaHost.Map
{
    internal sealed class MarkerIcons
    {
        internal static readonly int[] Ids = { 8, 48, 50, 224, 171, 393, 966, 29 };
        private readonly Texture2D[] textures = new Texture2D[8];
        private readonly object[] assets = new object[8], devices = new object[8];
        private readonly bool[] failed = new bool[8];
        internal bool Draw(SpriteBatch batch, int id, Vector2 center, float size, float alpha, out F5Rect bounds)
        {
            bounds = default(F5Rect); int index = Array.IndexOf(Ids, id); if (index < 0) return false;
            var asset = TextureAssets.Item[id]; var device = batch.GraphicsDevice;
            if (!ReferenceEquals(assets[index], asset) || !ReferenceEquals(devices[index], device))
            { textures[index] = null; failed[index] = false; assets[index] = asset; devices[index] = device; }
            var texture = textures[index];
            if (!failed[index]) try
            {
                if (texture == null || texture.IsDisposed || !ReferenceEquals(texture.GraphicsDevice, device) || !ReferenceEquals(texture, asset?.Value))
                { Main.instance.LoadItem(id); texture = TextureAssets.Item[id]?.Value; assets[index] = TextureAssets.Item[id]; textures[index] = texture; }
                if (texture == null || texture.IsDisposed || !ReferenceEquals(texture.GraphicsDevice, device)) failed[index] = true;
            }
            catch { failed[index] = true; textures[index] = null; }
            // One recognizable native-pixel fallback for every unavailable
            // style. Failure is latched per asset/device generation; never
            // allocate, dispose or repeatedly reload Terraria-owned textures.
            if (failed[index]) return Fallback(batch, center, size, alpha, out bounds);
            Rectangle frame = Main.itemAnimations[id] == null ? texture.Bounds : Main.itemAnimations[id].GetFrame(texture);
            float scale = Math.Min(size / frame.Width, size / frame.Height);
            bounds = new F5Rect(center.X - frame.Width * scale / 2, center.Y - frame.Height * scale / 2, frame.Width * scale, frame.Height * scale);
            batch.Draw(texture, center, frame, Color.White * alpha, 0, new Vector2(frame.Width / 2f, frame.Height / 2f), scale, SpriteEffects.None, 0); return true;
        }
        private static bool Fallback(SpriteBatch batch, Vector2 center, float size, float alpha, out F5Rect bounds)
        {
            bounds = new F5Rect(center.X - size / 2, center.Y - size / 2, size, size);
            var pixel = TextureAssets.MagicPixel?.Value; if (pixel == null || pixel.IsDisposed) return false;
            batch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), Color.DarkSlateGray * alpha, 0, new Vector2(.5f), size, SpriteEffects.None, 0);
            batch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), Color.Gold * alpha, 0, new Vector2(.5f), new Vector2(size * .7f, 3), SpriteEffects.None, 0);
            batch.Draw(pixel, center, new Rectangle(0, 0, 1, 1), Color.Gold * alpha, 0, new Vector2(.5f), new Vector2(3, size * .7f), SpriteEffects.None, 0); return true;
        }
    }
}
