using System;
using System.IO;
using JueMingR.Infrastructure.Settings;
using JueMingR.Platform.Hotkeys;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Hotkeys;
using JueMingR.TerrariaHost.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content.Readers;
using ReLogic.Graphics;

namespace Terraria
{
    internal static class HotkeyVisualChecks
    {
        internal static void Run(string content, string output)
        {
            Directory.CreateDirectory(output);
            using (var graphics = new F5FixtureGraphics())
            using (var renderer = new F5Renderer())
            {
                var services = new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService), new GraphicsService(graphics.Device));
                using (var reader = new XnbReader(services))
                using (var surface = Read<Texture2D>(reader, Path.Combine(content, "Images", "Inventory_Back.xnb")))
                using (var replacement = Read<Texture2D>(reader, Path.Combine(content, "Images", "Inventory_Back13.xnb")))
                {
                    var font = Read<DynamicSpriteFont>(reader, Path.Combine(content, "Fonts", "Mouse_Text.xnb"));
                    GameContent.FontAssets.MouseText = graphics.Asset("actual-hotkey-font", font);
                    var registry = new HotkeyRegistry(); registry.Register(new HotkeyAction("test.visual", "自动丢弃", HotkeyContext.Gameplay, () => true, () => { }));
                    using (var owner = new HotkeyBindings(registry, new FilePreferenceStorage(Path.Combine(output, "visual-probe.json"))))
                    {
                        var popup = new HotkeyPopup(owner, registry, new HostInputState(() => new IntPtr(1), () => new IntPtr(1)));
                        popup.Click("test.visual", new F5Rect(650, 10, 22, 30), 1, 0, 100);
                        popup.Click("test.visual", new F5Rect(650, 10, 22, 30), 1, 0, 200);
                        string[] statuses = { "请选择开始录入。保存后若再改原版键位，不会自动检查或禁用。", "等待主键：RCtrl+RShift+RAlt+", "已保存并生效。 提醒：「LeftControl」也用于原版「SmartCursor」，触发时可能同时执行。", "正在保存；成功前仍使用旧绑定。 提醒：无法读取当前原版键盘配置，未能核对按键重合。", "此次保存失败，旧绑定继续有效。可以重新录入后再试。", "已保存并生效。" };
                        for (int skin = 0; skin < 2; skin++)
                        {
                            GameContent.TextureAssets.InventoryBack = graphics.Asset("actual-hotkey-surface", skin == 0 ? surface : replacement);
                            renderer.RefreshResources();
                            for (int scaleIndex = 0; scaleIndex < 2; scaleIndex++)
                            {
                                float scale = scaleIndex == 0 ? 1 : 1.5f; Main.UIScaleMatrix = Matrix.CreateScale(scale, scale, 1);
                                for (int i = 0; i < statuses.Length; i++)
                                {
                                    popup.Layout.Build(800 / scale, 600 / scale, renderer.FontIdentity, new F5Rect(480 / scale, 8, 22, 30), "自动丢弃",
                                        i == 0 || i == 5 ? "未绑定" : "RCtrl+RShift+RAlt+MediaPreviousTrack", statuses[i], i == 1, renderer.PopupMeasure);
                                    CheckGeometry(popup.Layout);
                                    using (var target = new RenderTarget2D(graphics.Device, 800, 600))
                                    {
                                        graphics.Device.SetRenderTarget(target); graphics.Device.Clear(Color.Transparent);
                                        Main.spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix);
                                        var scissor = graphics.Device.ScissorRectangle;
                                        renderer.DrawPopup(popup);
                                        if (graphics.Device.ScissorRectangle != scissor) throw new Exception("Popup changed caller clipping state.");
                                        Main.spriteBatch.Draw(GameContent.TextureAssets.MagicPixel.Value, new Rectangle(0, 0, 4, 4), Color.Red);
                                        Main.spriteBatch.End(); graphics.Device.SetRenderTarget(null);
                                        var pixels = new Color[800 * 600]; target.GetData(pixels);
                                        if (pixels[801].R != 255) throw new Exception("Popup damaged subsequent sprite batch drawing.");
                                        using (var file = File.Create(Path.Combine(output, "hotkeys-surface" + skin + "-scale" + scaleIndex + "-state" + i + ".png"))) target.SaveAsPng(file, 800, 600);
                                    }
                                }
                            }
                        }
                        // Deliberately offset glyph origins: use real layout and
                        // rendering metrics, without claiming this is a font pack.
                        GameContent.FontAssets.MouseText = graphics.Asset("synthetic-offset-hotkey-font", graphics.CreateFont(12, 24, -3, 5)); renderer.RefreshResources();
                        popup.Layout.Build(800, 600, renderer.FontIdentity, new F5Rect(790, 570, 22, 30), "群系显示", "LCtrl+RCtrl+RShift+OemPlus", statuses[2], false, renderer.PopupMeasure);
                        CheckGeometry(popup.Layout);
                        int generation = popup.Layout.Generation;
                        for (int i = 0; i < 1000; i++) popup.Layout.Build(800, 600, renderer.FontIdentity, new F5Rect(790, 570, 22, 30), "群系显示", "LCtrl+RCtrl+RShift+OemPlus", statuses[2], false, renderer.PopupMeasure);
                        if (popup.Layout.Generation != generation) throw new Exception("Unchanged popup rebuilds each frame.");
                    }
                }
                Main.UIScaleMatrix = Matrix.Identity;
            }
            Console.WriteLine("PASS: 24 actual-XNB hotkey popup previews, two local vanilla surfaces, 100/150% scale, full feedback wrapping and synthetic offset metrics. Third-party skin/hardware not implied.");
        }
        private static void CheckGeometry(HotkeyPopupLayout layout)
        {
            if (layout.Panel.X < 12 || layout.Panel.Y < 12 || layout.Panel.Right > layout.Width - 11.9f || layout.Panel.Bottom > layout.Height - 11.9f) throw new Exception("Popup outside viewport.");
            foreach (var text in layout.Text)
                if (text.Rect.X < 0 || text.Rect.Right > layout.Panel.Width || text.Rect.Bottom > layout.Buttons[0].Rect.Y) throw new Exception("Full readable popup text overlaps controls/clipping.");
            for (int i = 0; i < layout.Buttons.Count; i++)
            {
                var r = layout.Buttons[i].Rect.Offset(layout.Panel.X, layout.Panel.Y);
                if (layout.Hit(r.X + r.Width / 2, r.Y + r.Height / 2) != i) throw new Exception("Popup paint and hit geometry disagree.");
            }
        }
        private static T Read<T>(XnbReader reader, string path) where T : class { using (var stream = File.OpenRead(path)) return reader.FromStream<T>(stream); }
        private sealed class GraphicsService : IGraphicsDeviceService
        {
            public GraphicsDevice GraphicsDevice { get; }
            internal GraphicsService(GraphicsDevice device) { GraphicsDevice = device; }
            public event EventHandler<EventArgs> DeviceCreated { add { } remove { } }
            public event EventHandler<EventArgs> DeviceDisposing { add { } remove { } }
            public event EventHandler<EventArgs> DeviceReset { add { } remove { } }
            public event EventHandler<EventArgs> DeviceResetting { add { } remove { } }
        }
    }
}
