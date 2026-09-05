using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content.Readers;
using ReLogic.Graphics;
using JueMingR.TerrariaHost.F5;

namespace Terraria
{
    // Explicit local asset paths only. This never starts the real Terraria entry
    // point or touches player data. Images exercise the linked production renderer.
    internal static class F5VisualChecks
    {
        internal static void Run(string contentDirectory, string outputDirectory)
        {
            if (!Directory.Exists(contentDirectory)) throw new DirectoryNotFoundException(contentDirectory);
            Directory.CreateDirectory(outputDirectory);
            using (var graphics = new F5FixtureGraphics())
            using (var renderer = new F5Renderer())
            {
                var services = new GameServiceContainer();
                services.AddService(typeof(IGraphicsDeviceService), new GraphicsService(graphics.Device));
                using (var reader = new XnbReader(services))
                using (var inventory = Read<Texture2D>(reader, Path.Combine(contentDirectory, "Images/Inventory_Back.xnb")))
                {
                    var font = Read<DynamicSpriteFont>(reader, Path.Combine(contentDirectory, "Fonts/Mouse_Text.xnb"));
                    GameContent.FontAssets.MouseText = graphics.Asset("actual-mouse-font", font);
                    GameContent.TextureAssets.InventoryBack = graphics.Asset("actual-inventory-back", inventory);
                    using (var probe = new RenderTarget2D(graphics.Device, 700, 140))
                    {
                        graphics.Device.SetRenderTarget(probe); graphics.Device.Clear(new Color(63, 65, 151));
                        Main.spriteBatch.Begin();
                        Main.spriteBatch.DrawString(font, "JueMingR 敌怪显名 配置 开启 关闭", new Vector2(20, 10), Color.White,
                            0, Vector2.Zero, 0.75f, SpriteEffects.None, 0);
                        Utils.DrawBorderStringFourWay(Main.spriteBatch, font, "JueMingR 敌怪显名 配置 开启 关闭", 20, 70,
                            Color.White, Color.Black, Vector2.Zero, 0.75f);
                        Main.spriteBatch.End(); graphics.Device.SetRenderTarget(null);
                        SaveTexture(probe, Path.Combine(outputDirectory, "font-single-vs-fourway.png"));
                    }
                    SaveTexture(inventory, Path.Combine(outputDirectory, "actual-inventory-back.png"));
                    Render(graphics, renderer, outputDirectory, "default-information", 9, 1, 0);
                    Render(graphics, renderer, outputDirectory, "default-fishing", 7, 1, 0);
                    Render(graphics, renderer, outputDirectory, "default-scale150", 9, 1.5f, 240);
                    CheckSkinAndLifetime(graphics, renderer, inventory);
                    Console.WriteLine("PASS: production F5 offline previews with local XNB font and surface; owner visual acceptance remains pending.");
                }
            }
        }

        private static T Read<T>(XnbReader reader, string path) where T : class
        { using (var stream = File.OpenRead(path)) return reader.FromStream<T>(stream); }

        private static void CheckSkinAndLifetime(F5FixtureGraphics graphics, F5Renderer renderer, Texture2D shared)
        {
            var state = new F5Interaction { Ready = true };
            state.Update(new F5Input { Width = 1280, Height = 1080, Scale = 1, Active = true, Focused = true, F5 = true });
            renderer.Prepare(state, 1280, 1080, 1);
            int generation = state.Layout.Generation, measurements = state.Layout.MeasurementCount;
            using (var replacement = new Texture2D(graphics.Device, 48, 48))
            using (var target = new RenderTarget2D(graphics.Device, 1280, 1080))
            {
                var colors = new Color[48 * 48];
                for (int i = 0; i < colors.Length; i++) colors[i] = new Color(22, 36, 28);
                replacement.SetData(colors);
                GameContent.TextureAssets.InventoryBack = graphics.Asset("synthetic-square-dark-skin", replacement);
                renderer.RefreshResources(); renderer.Prepare(state, 1280, 1080, 1);
                F5InputChecks.Check(state.Layout.Generation == generation && state.Layout.MeasurementCount == measurements,
                    "color/texture-size replacement must not remeasure or reflow");
                Main.UIScaleMatrix = Matrix.Identity;
                graphics.Device.SetRenderTarget(target); graphics.Device.Clear(Color.CornflowerBlue);
                Main.spriteBatch.Begin(); renderer.Draw(state, Matrix.Identity, true, false); Main.spriteBatch.End();
                graphics.Device.SetRenderTarget(null);
                var pixels = new Color[1280 * 1080]; target.GetData(pixels);
                Color corner = pixels[(int)state.Y * 1280 + (int)state.X];
                Color surface = pixels[((int)state.Y + 12) * 1280 + (int)state.X + 200];
                F5InputChecks.Check(corner == Color.CornflowerBlue, "square skin cannot leak a rectangular fill under the rounded outer edge");
                F5InputChecks.Check(surface.G > surface.B && surface.B > surface.R && surface.G < 40,
                    "replacement surface color must survive without a fixed blue or black theme");
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                object atlas = typeof(F5Renderer).GetField("icons", flags).GetValue(renderer);
                var textureField = typeof(F5IconAtlas).GetField("texture", flags);
                var first = (Texture2D)textureField.GetValue(atlas);
                var cap = (Texture2D)typeof(F5Renderer).GetField("roundCap", flags).GetValue(renderer);
                for (int i = 0; i < 3; i++)
                {
                    renderer.RefreshResources(); renderer.Prepare(state, 1280, 1080, 1);
                    Main.spriteBatch.Begin(); renderer.Draw(state, Matrix.Identity, true, false); Main.spriteBatch.End();
                }
                F5InputChecks.Check(ReferenceEquals(first, textureField.GetValue(atlas)) &&
                    ReferenceEquals(cap, typeof(F5Renderer).GetField("roundCap", flags).GetValue(renderer)),
                    "steady frames reuse owned GPU assets");
                renderer.Dispose();
                F5InputChecks.Check(first.IsDisposed && cap.IsDisposed && !shared.IsDisposed && !replacement.IsDisposed,
                    "renderer disposal releases only its own atlas/caps, preserving host resources");
            }
            Console.WriteLine("PASS: skin replacement, rounded fill and GPU ownership.");
        }

        private static void Render(F5FixtureGraphics graphics, F5Renderer renderer, string output,
            string name, int page, float scale, int wheel)
        {
            Main.UIScaleMatrix = Matrix.CreateScale(scale, scale, 1);
            var state = new F5Interaction { Ready = true };
            var input = new F5Input { Width = 1280, Height = 1080, Scale = scale, Active = true,
                Focused = true, F5 = true, X = 1900, Y = 1040 };
            state.Update(input);
            renderer.RefreshResources(); renderer.Prepare(state, input.Width, input.Height, input.Scale);
            F5Rect nav = state.Layout.Navigation(page);
            input.F5 = false; input.Left = true; input.X = state.X + nav.X + 5; input.Y = state.Y + nav.Y + 5;
            state.Update(input); renderer.Prepare(state, input.Width, input.Height, input.Scale);
            input.Left = false; input.Wheel = -wheel; input.Y = state.Y + 160;
            state.Update(input); input.Wheel = 0;
            using (var target = new RenderTarget2D(graphics.Device, 1920, 1080))
            {
                graphics.Device.SetRenderTarget(target);
                graphics.Device.Clear(new Color(80, 100, 110));
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix);
                renderer.Draw(state, Main.UIScaleMatrix, true, false);
                Main.spriteBatch.End(); graphics.Device.SetRenderTarget(null);
                SaveTexture(target, Path.Combine(output, name + ".png"));
            }
        }

        private static void SaveTexture(Texture2D texture, string path)
        { using (var stream = File.Create(path)) texture.SaveAsPng(stream, texture.Width, texture.Height); }

        private sealed class GraphicsService : IGraphicsDeviceService
        {
            internal GraphicsService(GraphicsDevice device) { GraphicsDevice = device; }
            public GraphicsDevice GraphicsDevice { get; private set; }
            public event EventHandler<EventArgs> DeviceCreated { add { } remove { } }
            public event EventHandler<EventArgs> DeviceDisposing { add { } remove { } }
            public event EventHandler<EventArgs> DeviceReset { add { } remove { } }
            public event EventHandler<EventArgs> DeviceResetting { add { } remove { } }
        }
    }
}
