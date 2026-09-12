using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Runtime.Serialization;
using JueMingR.TerrariaHost.WorldObjectText;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content.Readers;
using ReLogic.Graphics;
using ReLogic.Content;
using ReLogic.Content.Sources;

namespace NativeWorldTextProbe
{
    internal sealed class ProbeGraphics : IDisposable, IGraphicsDeviceService, IContentSource
    {
        private IntPtr window;
        private readonly SpriteBatch batch;
        private readonly XnbReader reader;
        private RenderTarget2D costCanvas;
        private readonly string contentDirectory;
        private readonly Dictionary<int, Texture2D> tiles = new Dictionary<int, Texture2D>();
        public GraphicsDevice GraphicsDevice { get; private set; }
        internal DynamicSpriteFont Font { get; }
        internal ProbeGraphics(string content)
        {
            contentDirectory = content;
            window = CreateWindowEx(0, "STATIC", "Native text probe", 0, 0, 0, 960, 640, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (window == IntPtr.Zero) throw new InvalidOperationException("hidden-test-window-unavailable");
            GraphicsDevice = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.Reach, new PresentationParameters {
                DeviceWindowHandle = window, BackBufferWidth = 960, BackBufferHeight = 640, BackBufferFormat = SurfaceFormat.Color, DepthStencilFormat = DepthFormat.None, IsFullScreen = false });
            batch = new SpriteBatch(GraphicsDevice); Terraria.Main.spriteBatch = batch;
            var services = new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService), this); reader = new XnbReader(services);
            using (var stream = File.OpenRead(Path.Combine(content, "Fonts", "Mouse_Text.xnb"))) Font = reader.FromStream<DynamicSpriteFont>(stream);
            Terraria.Main.instance = (Terraria.Main)FormatterServices.GetUninitializedObject(typeof(Terraria.Main));
            // Supply only the already-created device service. Calling the real
            // Main/Game constructor or game Initialize/Run is outside this probe.
            typeof(Game).GetField("graphicsDeviceService", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Terraria.Main.instance, this);
            GC.SuppressFinalize(Terraria.Main.instance);
            Terraria.Main.netMode = 0; Terraria.Main.dedServ = false;
            Terraria.GameContent.FontAssets.MouseText = Loaded("probe-native-font", Font);
            Terraria.GameContent.FontAssets.ItemStack = Loaded("probe-native-stack", Font);
            using (var stream = File.OpenRead(Path.Combine(content, "Images", "Item_8.xnb"))) Terraria.GameContent.TextureAssets.Item[8] = Loaded("Images/Item_8", reader.FromStream<Texture2D>(stream));
            using (var stream = File.OpenRead(Path.Combine(content, "Images", "Inventory_Back.xnb"))) Terraria.GameContent.TextureAssets.InventoryBack = Loaded("Images/Inventory_Back", reader.FromStream<Texture2D>(stream));
            using (var stream = File.OpenRead(Path.Combine(content, "Images", "MagicPixel.xnb"))) Terraria.GameContent.TextureAssets.MagicPixel = Loaded("Images/MagicPixel", reader.FromStream<Texture2D>(stream));
        }
        internal void Preview(string output, params NativeWorldTextLayout[] layouts)
        {
            Directory.CreateDirectory(output);
            using (var canvas = new RenderTarget2D(GraphicsDevice, 960, 640))
            {
                GraphicsDevice.SetRenderTarget(canvas); GraphicsDevice.Clear(new Color(30, 43, 47));
                batch.Begin();
                for (int i = 0; i < layouts.Length; i++) { layouts[i].ApplyColor(0xE6C16A); layouts[i].Draw(batch, new Vector2(30 + i % 3 * 310, 50 + i / 3 * 300)); }
                batch.End(); GraphicsDevice.SetRenderTarget(null);
                using (var stream = new FileStream(Path.Combine(output, "native-text-layout.png"), FileMode.CreateNew)) canvas.SaveAsPng(stream, 960, 640);
            }
        }
        internal void DrawWorld(WorldObjectTextWorldLayer layer, string output)
        {
            using (var canvas = new RenderTarget2D(GraphicsDevice, 960, 640))
            {
                GraphicsDevice.SetRenderTarget(canvas); GraphicsDevice.Clear(new Color(30, 43, 47));
                batch.Begin(); layer.Draw(); batch.End(); GraphicsDevice.SetRenderTarget(null);
                using (var stream = new FileStream(output, FileMode.CreateNew)) canvas.SaveAsPng(stream, 960, 640);
            }
        }
        internal void DrawFrame(WorldObjectTextWorldLayer layer)
        {
            if (costCanvas == null) costCanvas = new RenderTarget2D(GraphicsDevice, 960, 640);
            GraphicsDevice.SetRenderTarget(costCanvas); GraphicsDevice.Clear(new Color(30, 43, 47));
            batch.Begin(); layer.Draw(); batch.End(); GraphicsDevice.SetRenderTarget(null);
        }
        internal void Image(string output, Action draw, Matrix matrix)
        {
            using (var canvas = new RenderTarget2D(GraphicsDevice, 960, 640))
            {
                GraphicsDevice.SetRenderTarget(canvas); GraphicsDevice.Clear(new Color(30, 43, 47));
                batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, matrix); draw(); batch.End(); GraphicsDevice.SetRenderTarget(null);
                using (var stream = new FileStream(output, FileMode.CreateNew)) canvas.SaveAsPng(stream, 960, 640);
            }
        }
        internal void Scene(WorldObjectTextWorldLayer layer, string output)
        {
            Image(output, () =>
            {
                for (int y = 0; y < 50; y++) for (int x = 0; x < 75; x++)
                {
                    var tile = Terraria.Main.tile[x, y]; if (tile == null || !tile.active()) continue;
                    Texture2D texture;
                    if (!tiles.TryGetValue(tile.type, out texture))
                    { using (var stream = File.OpenRead(Path.Combine(contentDirectory, "Images", "Tiles_" + tile.type + ".xnb"))) texture = reader.FromStream<Texture2D>(stream); tiles.Add(tile.type, texture); }
                    var position = new Vector2(x * 16, y * 16) - Terraria.Main.screenPosition;
                    if (Terraria.Main.LocalPlayer.gravDir == -1) position.Y = Terraria.Main.screenHeight - position.Y - 16;
                    batch.Draw(texture, position, new Rectangle(tile.frameX, tile.frameY, 16, 16), Color.White, 0, Vector2.Zero, 1,
                        Terraria.Main.LocalPlayer.gravDir == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None, 0);
                }
                layer.Draw();
            }, Terraria.Main.GameViewMatrix.ZoomMatrix);
        }
        public void Dispose() { foreach (var texture in tiles.Values) texture.Dispose(); costCanvas?.Dispose(); reader?.Dispose(); batch?.Dispose(); GraphicsDevice?.Dispose(); if (window != IntPtr.Zero) { DestroyWindow(window); window = IntPtr.Zero; } }
        private Asset<T> Loaded<T>(string name, T value) where T : class
        {
            var asset = (Asset<T>)Activator.CreateInstance(typeof(Asset<T>), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { name }, null);
            typeof(Asset<T>).GetMethod("SubmitLoadedContent", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(asset, new object[] { value, this });
            if (asset.State != AssetState.Loaded) throw new InvalidOperationException("native-probe-asset-not-loaded");
            return asset;
        }
        public IContentValidator ContentValidator { get; set; }
        public string FileWatcherPath { get { return null; } }
        public bool HasAsset(string name) { return false; }
        public List<string> GetAllAssetsStartingWith(string name) { return new List<string>(); }
        public string GetExtension(string name) { return null; }
        public Stream OpenStream(string name) { throw new NotSupportedException(); }
        public void RejectAsset(string name, IRejectionReason reason) { }
        public void ClearRejections() { }
        public bool TryGetRejections(List<string> reasons) { return false; }
        public void Refresh() { }
        public event EventHandler<EventArgs> DeviceCreated { add { } remove { } }
        public event EventHandler<EventArgs> DeviceDisposing { add { } remove { } }
        public event EventHandler<EventArgs> DeviceReset { add { } remove { } }
        public event EventHandler<EventArgs> DeviceResetting { add { } remove { } }
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(int extended, string className, string title, int style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr module, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
    }
}
