using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using JueMingR.Features.EntityLabels;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.EntityLabels;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content.Readers;
using ReLogic.Graphics;

namespace Terraria
{
    // Explicit graphics entry: no substitute bitmap or swallowed device error.
    // Generated images require human/model viewing; file creation alone is not UI acceptance.
    internal static class EntityVisualChecks
    {
        internal static void Run(string content, string output)
        {
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-EntityVisual-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); Directory.CreateDirectory(output);
            var runtime = new SingleFeatureRuntime(new Probe(), new Idle());
            var host = new HostEntityLabels(root, runtime) { LayersReady = true }; runtime.AddFeature(host); runtime.Update(0);
            var document = (PreferenceDocument<EntityLabelSettings>)typeof(HostEntityLabels).GetField("preferences", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host);
            try
            {
                if (!SpinWait.SpinUntil(() => host.Preferences.IsLoaded, 3000)) throw new Exception("isolated visual settings not ready");
                using (var graphics = new F5FixtureGraphics())
                using (var renderer = new F5Renderer())
                {
                    var services = new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService), new GraphicsService(graphics.Device));
                    using (var reader = new XnbReader(services))
                    using (var skin = Read<Texture2D>(reader, Path.Combine(content, "Images", "Inventory_Back.xnb")))
                    using (var alternate = Read<Texture2D>(reader, Path.Combine(content, "Images", "Inventory_Back13.xnb")))
                    {
                        var font = Read<DynamicSpriteFont>(reader, Path.Combine(content, "Fonts", "Mouse_Text.xnb"));
                        renderer.EntityControls = new EntityLabelControls(host);
                        for (int scene = 0; scene < 4; scene++)
                        {
                            float scale = scene == 3 ? 1.5f : 1; int width = scene == 3 ? 960 : 1280, height = 720;
                            Main.UIScaleMatrix = Matrix.CreateScale(scale, scale, 1);
                            Main.LocalPlayer = new Player { active = true }; Main.gameMenu = false; Main.netMode = 0;
                            Main.screenPosition = Vector2.Zero; Main.screenWidth = width; Main.screenHeight = height;
                            GameContent.FontAssets.MouseText = graphics.Asset("entity-visual-font", scene == 3 ? graphics.CreateFont(16, 30, -3, 5) : font);
                            GameContent.TextureAssets.InventoryBack = graphics.Asset("entity-visual-skin", scene == 3 ? alternate : skin);
                            renderer.RefreshResources();
                            var state = new F5Interaction { Ready = true };
                            state.Update(new F5Input { Width = width, Height = height, Scale = scale, Active = true, Focused = true, F5 = true });
                            typeof(F5Interaction).GetProperty("Page", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(state, 9, null);
                            renderer.Prepare(state, width, height, scale);
                            var popup = new StylePopup(host, new HostInputState(() => new IntPtr(1), () => new IntPtr(1)));
                            var kind = (EntityLabelKind)(scene % 3); var anchor = new F5Rect(300, 110, 45, 32);
                            popup.Click(kind, anchor, 9);
                            if (scene == 1) { popup.Editor.PreviewHsl(0, 240); popup.Editor.BeginHex(); popup.Editor.Insert("12"); }
                            if (scene == 2) { popup.Editor.BeginHex(); popup.Editor.Insert("123"); popup.Editor.CommitHex(); }
                            popup.Prepare(width / scale, height / scale, scale, renderer.FontIdentity, renderer.PopupMeasure, renderer.SkinGeneration, anchor);
                            if (!popup.Visible || popup.Layout.Panel.Right > width / scale || popup.Layout.Panel.Bottom > height / scale) throw new Exception("style panel out of viewport");
                            foreach (var text in popup.Layout.Text) if (text.Rect.Right > popup.Layout.Panel.Width - 4 || text.Rect.Bottom > popup.Layout.Panel.Height) throw new Exception("style text clipped");
                            var labels = new List<EntityLabel> {
                                new EntityLabel { SourceSlot = 0, Name = "长名称 · 史莱姆", Health = "125/150", X = 180, Y = 90, Height = 32, Rgb = 0xCD5C5C, NameSize = scene == 3 ? 180 : 90, HealthSize = scene == 3 ? 167 : 77 },
                                new EntityLabel { SourceSlot = 1, Name = "金色动物", X = width - 170, Y = height - 70, Rgb = 0xFFD700, NameSize = scene == 3 ? 50 : 90, HealthSize = 50 }
                            };
                            var world = new EntityWorldLayer(labels, () => true);
                            using (var target = new RenderTarget2D(graphics.Device, width, height))
                            {
                                graphics.Device.SetRenderTarget(target); graphics.Device.Clear(new Color(32, 40, 48));
                                Main.GameViewMatrix.ZoomMatrix = Matrix.Identity;
                                Main.spriteBatch.Begin(); world.Draw(); Main.spriteBatch.End();
                                if (world.Failure != null || world.LastDrawn != 2) throw new Exception("actual visual world draw failed");
                                Main.spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix);
                                renderer.Draw(state, Main.UIScaleMatrix, true, false); renderer.DrawStylePopup(popup);
                                Main.spriteBatch.Draw(GameContent.TextureAssets.MagicPixel.Value, new Rectangle(0, 0, 4, 4), new Rectangle(0, 0, 1, 1), Color.Red);
                                Main.spriteBatch.End(); graphics.Device.SetRenderTarget(null);
                                var pixels = new Color[width * height]; target.GetData(pixels); if (pixels[width + 1].R != 255) throw new Exception("following batch draw corrupted");
                                using (var file = new FileStream(Path.Combine(output, "entity-scene-" + scene + ".png"), FileMode.CreateNew)) target.SaveAsPng(file, width, height);
                            }
                            popup.Close();
                        }
                    }
                }
            }
            finally
            {
                bool stopped = document.Stop(3000); string full = Path.GetFullPath(root);
                if (!stopped) throw new Exception("visual worker still active; isolated root retained");
                if (!Path.GetDirectoryName(full).Equals(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("JueMingR-EntityVisual-", StringComparison.Ordinal)) throw new Exception("unexpected visual root");
                Directory.Delete(full, true);
            }
            Console.WriteLine("PASS: four actual-resource entity style/world previews generated; viewing/real-machine acceptance is separate.");
        }
        private static T Read<T>(XnbReader reader, string path) where T : class { using (var stream = File.OpenRead(path)) return reader.FromStream<T>(stream); }
        private sealed class Probe : IGameSessionProbe { public bool IsSessionActive { get { return true; } } }
        private sealed class Idle : IRuntimeFeature { public bool Enabled { get { return false; } } public void OnSessionStarted() { } public void OnSessionEnded() { } public void Update(ulong tick) { } public void FailClosed() { } }
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
