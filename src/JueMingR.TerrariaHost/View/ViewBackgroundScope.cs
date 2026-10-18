using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;

namespace JueMingR.TerrariaHost.View
{
    // Only the native cave-background / obstruction layer uses this domain.
    // Its old loops require width/height/position together; the real viewport,
    // camera matrix and all surrounding R/native consumers keep their domains.
    internal sealed class ViewBackgroundScope : IDisposable
    {
        private readonly int width, height;
        private readonly Vector2 position;
        private bool disposed;
        internal readonly Matrix Matrix;
        internal ViewBackgroundScope()
        {
            width = Main.screenWidth; height = Main.screenHeight; position = Main.screenPosition;
            Vector2 start = Main.Camera.ScaledPosition, size = Main.Camera.ScaledSize;
            Matrix = Microsoft.Xna.Framework.Matrix.CreateTranslation(new Vector3(start - position, 0)) * Main.GameViewMatrix.TransformationMatrix;
            Main.spriteBatch.End();
            try
            {
                Main.screenPosition = start; Main.screenWidth = (int)Math.Ceiling(size.X); Main.screenHeight = (int)Math.Ceiling(size.Y);
                Begin(Matrix);
            }
            catch
            {
                Main.screenPosition = position; Main.screenWidth = width; Main.screenHeight = height;
                Begin(Main.GameViewMatrix.TransformationMatrix); throw;
            }
        }
        private static void Begin(Matrix matrix)
        { Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, matrix); }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            try { Main.spriteBatch.End(); }
            finally
            {
                Main.screenPosition = position; Main.screenWidth = width; Main.screenHeight = height;
                Begin(Main.GameViewMatrix.TransformationMatrix);
            }
        }
    }
}
