using System;
using System.Reflection;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Effects;

namespace JueMingR.TerrariaHost.View
{
    // Expanded world layers draw directly with the actual camera. Scene filters
    // still consume a physical-sized sky/scene/swap through their original
    // BeginCapture/EndCapture, without reinstating cropped tile-layer targets.
    internal sealed class ViewSceneCapture : IDisposable
    {
        private GraphicsDevice device;
        private RenderTarget2D sky, scene, swap;
        private RenderTarget2D savedSky, savedScene, savedSwap;
        private bool attached;
        private static readonly FieldInfo capturing = typeof(FilterManager).GetField("_captureThisFrame", BindingFlags.Instance | BindingFlags.NonPublic);
        internal int Generation { get; private set; }
        internal bool Attached { get { return attached; } }
        internal void Attach()
        {
            var current = Main.instance.GraphicsDevice;
            if (!ReferenceEquals(device, current))
            {
                Dispose(); device = current;
                device.DeviceResetting += Reset; device.Disposing += Reset;
            }
            var size = current.PresentationParameters;
            if (scene == null || scene.IsDisposed || scene.IsContentLost || scene.Width != size.BackBufferWidth || scene.Height != size.BackBufferHeight)
            {
                RenderTarget2D newSky = null, newScene = null, newSwap = null;
                try
                {
                    newSky = Create(current); newScene = Create(current); newSwap = Create(current);
                }
                catch { newSwap?.Dispose(); newScene?.Dispose(); newSky?.Dispose(); throw; }
                Release(); sky = newSky; scene = newScene; swap = newSwap; Generation++;
            }
            savedSky = Main.skyTarget; savedScene = Main.screenTarget; savedSwap = Main.screenTargetSwap;
            Main.skyTarget = sky; Main.screenTarget = scene; Main.screenTargetSwap = swap; attached = true;
        }
        private static RenderTarget2D Create(GraphicsDevice value)
        {
            var p = value.PresentationParameters;
            return new RenderTarget2D(value, p.BackBufferWidth, p.BackBufferHeight, false, p.BackBufferFormat, DepthFormat.None);
        }
        internal void Detach(bool aborted)
        {
            if (!attached) return;
            // A failed owned layer may precede EndCapture. Its private lease
            // must not survive into a later ordinary/native capture.
            if (aborted) capturing.SetValue(Filters.Scene, false);
            Main.skyTarget = savedSky; Main.screenTarget = savedScene; Main.screenTargetSwap = savedSwap;
            savedSky = savedScene = savedSwap = null; attached = false;
        }
        private void Reset(object sender, EventArgs args) { Detach(true); Release(); }
        private void Release()
        { swap?.Dispose(); scene?.Dispose(); sky?.Dispose(); swap = scene = sky = null; }
        public void Dispose()
        {
            Detach(true); Release();
            if (device != null) { device.DeviceResetting -= Reset; device.Disposing -= Reset; device = null; }
        }
    }
    // Only failures introduced by VIEW-owned preparation cross this boundary;
    // unrelated original draw exceptions retain their original propagation.
    internal sealed class ViewRenderFailure : Exception
    { internal ViewRenderFailure(Exception inner) : base("VIEW owned rendering preparation failed.", inner) { } }
}
