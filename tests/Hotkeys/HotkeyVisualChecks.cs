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
                        var views = new[] {
                            View(null, HotkeyFeedbackKind.Ready),
                            View("LeftShift+LeftAlt+X", HotkeyFeedbackKind.Ready),
                            View("LeftShift+LeftAlt+X", HotkeyFeedbackKind.Saved, "已保存", "LShift 与原版「快捷火把」重合，可能同时触发。"),
                            new HotkeyPopupView("自动丢弃", Parse("K"), null, HotkeyModifiers.RightControl | HotkeyModifiers.RightShift, new HotkeyFeedback(HotkeyFeedbackKind.Capturing, "等待主键"), true, true, true),
                            View("K", HotkeyFeedbackKind.Rejected, "此组合已用于「自动堆叠」。"),
                            View("K", HotkeyFeedbackKind.Failed, "保存失败，原绑定仍有效"),
                            View("RightControl+RightShift+RightAlt+MediaPreviousTrack", HotkeyFeedbackKind.Ready)
                        };
                        for (int i = 0; i < views.Length; i++)
                        {
                            GameContent.TextureAssets.InventoryBack = graphics.Asset("actual-hotkey-surface", i == 5 ? replacement : surface);
                            renderer.RefreshResources();
                            float scale = i == 6 ? 1.5f : 1;
                            Main.UIScaleMatrix = Matrix.CreateScale(scale, scale, 1);
                            var context = new F5Interaction(); context.Ready = true;
                            context.Update(new F5Input { Width = 1280, Height = 720, Scale = scale, Active = true, Focused = true, F5 = true });
                            renderer.Prepare(context, 1280, 720, scale);
                            popup.Layout.ResetAnchor();
                            popup.Layout.Build(1280 / scale, 720 / scale, renderer.FontIdentity, new F5Rect(560, 220, 22, 30), views[i], renderer.PopupMeasure);
                            CheckGeometry(popup.Layout);
                            if (i == 6)
                            {
                                var hoverInput = new HostInputState(() => new IntPtr(1), () => new IntPtr(1));
                                // Set the hover through the production popup input path.
                                var field = typeof(HotkeyPopup).GetField("input", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                                hoverInput = (HostInputState)field.GetValue(popup);
                                hoverInput.BeginUpdate(); GameInput.PlayerInput.MouseInfo = new Microsoft.Xna.Framework.Input.MouseState();
                                hoverInput.AfterMapping(); Main.keyState = new Microsoft.Xna.Framework.Input.KeyboardState(); hoverInput.AfterKeyboardRefresh();
                                var help = popup.Layout.Buttons[popup.Layout.Index(HotkeyPopupCommand.Help)].Rect.Offset(popup.Layout.Panel.X, popup.Layout.Panel.Y);
                                popup.Process(true, 0, help.X + 4, help.Y + 4, true);
                                if (!popup.HelpVisible) throw new Exception("Help hover was not presented.");
                            }
                            using (var target = new RenderTarget2D(graphics.Device, 1280, 720))
                            {
                                graphics.Device.SetRenderTarget(target); graphics.Device.Clear(new Color(32, 40, 48));
                                Main.spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix);
                                renderer.Draw(context, Main.UIScaleMatrix, false, false);
                                var scissor = graphics.Device.ScissorRectangle;
                                renderer.DrawPopup(popup);
                                if (graphics.Device.ScissorRectangle != scissor) throw new Exception("Popup changed caller clipping state.");
                                Main.spriteBatch.Draw(GameContent.TextureAssets.MagicPixel.Value, new Rectangle(0, 0, 4, 4), Color.Red);
                                Main.spriteBatch.End(); graphics.Device.SetRenderTarget(null);
                                var pixels = new Color[1280 * 720]; target.GetData(pixels);
                                if (pixels[1281].R != 255) throw new Exception("Popup damaged subsequent sprite batch drawing.");
                                using (var file = File.Create(Path.Combine(output, "popup-state" + i + ".png"))) target.SaveAsPng(file, 1280, 720);
                            }
                        }
                        GameContent.FontAssets.MouseText = graphics.Asset("synthetic-offset-hotkey-font", graphics.CreateFont(12, 24, -3, 5)); renderer.RefreshResources();
                        var longView = View("LeftControl+RightControl+RightShift+OemPlus", HotkeyFeedbackKind.Saved, "已保存", "原版动作的长名称用于验证完整换行和边界，可能同时触发。");
                        popup.Layout.ResetAnchor();
                        popup.Layout.Build(640, 480, renderer.FontIdentity, new F5Rect(630, 450, 22, 30), longView, renderer.PopupMeasure);
                        CheckGeometry(popup.Layout);
                        int generation = popup.Layout.Generation;
                        for (int i = 0; i < 1000; i++) popup.Layout.Build(640, 480, renderer.FontIdentity, new F5Rect(630, 450, 22, 30), longView, renderer.PopupMeasure);
                        if (popup.Layout.Generation != generation) throw new Exception("Unchanged popup rebuilds each frame.");
                    }
                }
                Main.UIScaleMatrix = Matrix.Identity;
            }
            Console.WriteLine("PASS: seven actual-XNB popup previews in current R context, two vanilla surfaces, 100/150% scale and synthetic offset geometry. Third-party skin/hardware not implied.");
        }
        private static HotkeyChord Parse(string text) { HotkeyChord chord; string reason; if (!HotkeyChord.TryParse(text, out chord, out reason)) throw new Exception(reason); return chord; }
        private static HotkeyPopupView View(string text, HotkeyFeedbackKind kind, string summary = null, string warning = null)
        { return new HotkeyPopupView("自动丢弃", text == null ? null : Parse(text), null, HotkeyModifiers.None, new HotkeyFeedback(kind, summary, advisory: warning), kind != HotkeyFeedbackKind.Saving, false, true); }
        private static void CheckGeometry(HotkeyPopupLayout layout)
        {
            if (layout.Panel.X < 12 || layout.Panel.Y < 12 || layout.Panel.Right > layout.Width - 11.9f || layout.Panel.Bottom > layout.Height - 11.9f) throw new Exception("Popup outside viewport.");
            foreach (var text in layout.Text)
                if (text.Rect.X < 0 || text.Rect.Right > layout.Panel.Width || text.Rect.Bottom > layout.FooterTop) throw new Exception("Full readable popup text overlaps controls/clipping.");
            for (int i = 0; i < layout.Buttons.Count; i++)
            {
                var r = layout.Buttons[i].Rect.Offset(layout.Panel.X, layout.Panel.Y);
                if (layout.Hit(r.X + r.Width / 2, r.Y + r.Height / 2) != (layout.Enabled[i] ? layout.Commands[i] : HotkeyPopupCommand.None)) throw new Exception("Popup paint and hit geometry disagree.");
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
