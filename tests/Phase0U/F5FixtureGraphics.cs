using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Content.Sources;
using ReLogic.Graphics;

namespace Terraria
{
    // A hidden native test window owns a real XNA device. No game executable,
    // game content, presentation, keyboard capture or real mouse movement is used.
    internal sealed class F5FixtureGraphics : IDisposable, IContentSource
    {
        private IntPtr window;
        internal readonly GraphicsDevice Device;
        private readonly Texture2D texture;
        internal F5FixtureGraphics()
        {
            window = CreateWindowEx(0, "STATIC", "Phase 0-U fixture", 0, 0, 0, 1920, 1080,
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (window == IntPtr.Zero) throw new InvalidOperationException("Hidden fixture window creation failed.");
            Device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.Reach,
                new PresentationParameters { DeviceWindowHandle = window, BackBufferWidth = 1920, BackBufferHeight = 1080,
                    BackBufferFormat = SurfaceFormat.Color, DepthStencilFormat = DepthFormat.None, IsFullScreen = false });
            texture = new Texture2D(Device, 32, 32);
            var colors = new Color[1024];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.White;
            texture.SetData(colors);
            Main.spriteBatch = new SpriteBatch(Device);
            GameContent.FontAssets.MouseText = Asset("fixture-font", CreateFont(10, 20));
            GameContent.TextureAssets.MagicPixel = Asset("fixture-pixel", texture);
            GameContent.TextureAssets.SettingsPanel = Asset("fixture-panel", texture);
            GameContent.TextureAssets.InventoryBack = Asset("fixture-button", texture);
            GameContent.TextureAssets.InventoryBack13 = Asset("fixture-row", texture);
        }

        internal DynamicSpriteFont CreateFont(int width, int height)
        {
            var font = new DynamicSpriteFont(0, height, '?');
            Type pageType = typeof(DynamicSpriteFont).Assembly.GetType("ReLogic.Graphics.FontPage", true);
            object page = Activator.CreateInstance(pageType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { texture, new List<Rectangle> { new Rectangle(0, 0, width, height) },
                    new List<Rectangle> { new Rectangle(0, 0, width, height) }, new List<char> { '?' },
                    new List<Vector3> { new Vector3(0, width, 0) } }, null);
            Array pages = Array.CreateInstance(pageType, 1); pages.SetValue(page, 0);
            typeof(DynamicSpriteFont).GetMethod("SetPages", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(font, new object[] { pages });
            return font;
        }

        internal Asset<T> Asset<T>(string name, T value) where T : class
        {
            var asset = (Asset<T>)Activator.CreateInstance(typeof(Asset<T>), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { name }, null);
            typeof(Asset<T>).GetMethod("SubmitLoadedContent", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(asset, new object[] { value, this });
            return asset;
        }

        public void Dispose()
        {
            Main.spriteBatch.Dispose(); Main.spriteBatch = null;
            texture.Dispose(); Device.Dispose();
            if (window != IntPtr.Zero) { DestroyWindow(window); window = IntPtr.Zero; }
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

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(int extended, string className, string title, int style,
            int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr module, IntPtr parameter);
        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr window);
    }

    public static partial class Utils
    {
        internal static bool ThrowF5Text;
        internal static int F5TextDraws;
        public static void DrawSplicedPanel(SpriteBatch batch, Texture2D texture, int x, int y, int width, int height,
            int left, int right, int top, int bottom, Color color)
        { batch.Draw(texture, new Rectangle(x, y, width, height), color); }
        public static void DrawBorderStringFourWay(SpriteBatch batch, DynamicSpriteFont font, string text,
            float x, float y, Color textColor, Color borderColor, Vector2 origin, float scale = 1)
        {
            // Fault after headers, inside the clipped content batch.
            if (ThrowF5Text && text == "敌怪显名") throw new InvalidOperationException("Controlled F5 content draw failure.");
            F5TextDraws++;
            batch.DrawString(font, text, new Vector2(x, y), textColor, 0, origin, scale, SpriteEffects.None, 0);
        }
    }
}
