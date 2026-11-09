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
            try
            {
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
                        NotesVisualChecks.Run(graphics, outputDirectory);
                        CheckIconScale(graphics, outputDirectory);
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
                        Render(graphics, renderer, outputDirectory, "default-information-off", 9, 1, 0, false);
                        Render(graphics, renderer, outputDirectory, "default-scale050", 9, .5f, 0, false, fractional: true);
                        Render(graphics, renderer, outputDirectory, "default-scale135-fractional-scroll", 9, 1.35f, 120, fractional: true);
                        CheckDeviceRows(graphics, font, inventory, outputDirectory, "actual-font");
                        Render(graphics, renderer, outputDirectory, "default-information-unavailable", 9, 1, 0, false, true);
                        Render(graphics, renderer, outputDirectory, "name-chest", 9, 1, 0, hintTarget: "宝箱显名");
                        Render(graphics, renderer, outputDirectory, "name-infection", 9, 1, 0, hintTarget: "世界感染");
                        Render(graphics, renderer, outputDirectory, "hint-keyboard", 9, 1, 0, hintTarget: "keyboard");
                        Render(graphics, renderer, outputDirectory, "hint-unavailable", 9, 1, 0, false, true, "unavailable");
                        Render(graphics, renderer, outputDirectory, "default-fishing", 7, 1, 0);
                        // Synthetic 24-high glyphs exercise the tall-font row
                        // reserve; this is geometry stress, not a font-pack claim.
                        GameContent.FontAssets.MouseText = graphics.Asset("synthetic-fractional-font", graphics.CreateFont(10, 24));
                        Render(graphics, renderer, outputDirectory, "synthetic-fractional-font-information", 9, 1, 0);
                        CheckDeviceRows(graphics, GameContent.FontAssets.MouseText.Value, inventory, outputDirectory, "glyph24");
                        GameContent.FontAssets.MouseText = graphics.Asset("actual-mouse-font", font);
                        Render(graphics, renderer, outputDirectory, "default-scale150", 9, 1.5f, 240);
                        CheckSkinAndLifetime(graphics, renderer, inventory, outputDirectory);
                        Console.WriteLine("PASS: production F5 offline previews with local XNB font and surface; owner visual acceptance remains pending.");
                    }
                }
            }
            catch (Exception error) { Console.Error.WriteLine(error.ToString()); throw; }
        }

        private static T Read<T>(XnbReader reader, string path) where T : class
        { using (var stream = File.OpenRead(path)) return reader.FromStream<T>(stream); }

        private static void CheckDeviceRows(F5FixtureGraphics graphics, DynamicSpriteFont font, Texture2D skin, string output, string name)
        {
            var metrics = new UiTextMetrics();
            var raw = metrics.Measure(font, "关闭");
            var size = new F5Size(raw.Width * .7f + 4, raw.Height * .7f + 4, raw.OffsetX * .7f - 2, raw.OffsetY * .7f - 2);
            foreach (float scale in new[] { .5f, 1f, 1.35f })
            {
                var matrix = Matrix.CreateScale(scale); matrix.M41 = .37f; matrix.M42 = .61f;
                Main.UIScaleMatrix = matrix;
                var element = new F5Element(F5ElementKind.Button,
                    new F5Rect(28.3f, 30.7f, Math.Max(100, size.Width + 20), Math.Max(30, size.Height + 12)),
                    "关闭", size, .7f, F5Command.DisableBiome);
                var line = F5Layout.ButtonUnderline(element, matrix);
                int left = (int)Math.Round(line.X * scale + matrix.M41), right = (int)Math.Round(line.Right * scale + matrix.M41);
                int top = (int)Math.Round(line.Y * scale + matrix.M42), bottom = (int)Math.Round(line.Bottom * scale + matrix.M42);
                F5InputChecks.Check(bottom - top == 2 && right > left, "fully visible line spans exactly two integer device rows");
                using (var baseline = new RenderTarget2D(graphics.Device, 420, 150))
                using (var target = new RenderTarget2D(graphics.Device, 420, 150))
                using (var rasterizer = new RasterizerState { CullMode = CullMode.None, ScissorTestEnable = true })
                {
                    Action<RenderTarget2D, Color?, Rectangle> draw = (image, color, clip) =>
                    {
                        Rectangle priorClip = graphics.Device.ScissorRectangle;
                        graphics.Device.SetRenderTarget(image); graphics.Device.Clear(new Color(80, 100, 110));
                        graphics.Device.ScissorRectangle = clip;
                        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                            DepthStencilState.None, rasterizer, null, matrix);
                        F5ControlRenderer.Button(Main.spriteBatch, GameContent.TextureAssets.MagicPixel.Value, skin, font,
                            element, false, true, color, transform: matrix);
                        Main.spriteBatch.End(); graphics.Device.SetRenderTarget(null); graphics.Device.ScissorRectangle = priorClip;
                    };
                    foreach (bool clipped in new[] { false, true })
                    {
                        var clip = new Rectangle(0, clipped ? top + 1 : 0, 420, clipped ? 149 - top : 150);
                        draw(baseline, null, clip);
                        var before = new Color[420 * 150]; baseline.GetData(before);
                        foreach (Color color in new[] { Color.LightGreen, Color.IndianRed })
                        {
                            draw(target, color, clip); var after = new Color[420 * 150]; target.GetData(after);
                            int changed = 0;
                            for (int y = 0; y < 150; y++) for (int x = 0; x < 420; x++)
                            {
                                bool onLine = x >= left && x < right && y >= top && y < bottom && clip.Contains(x, y);
                                F5InputChecks.Check(onLine ? after[y * 420 + x] == color : after[y * 420 + x] == before[y * 420 + x],
                                    "production button adds only the exact snapped line; text, skin, border and normal clipping match the undecorated render");
                                if (onLine) changed++;
                            }
                            F5InputChecks.Check(changed == (right - left) * (clipped ? 1 : 2), "viewport crops one row only when the line crosses its edge");
                            SaveTexture(target, Path.Combine(output, "underline-" + name + "-" + scale.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) +
                                (color == Color.IndianRed ? "-off" : "-on") + (clipped ? "-clipped" : "") + ".png"));
                        }
                    }
                }
            }
            Console.WriteLine("PASS: production button pixels at native minimum/normal/fractional scales; exact two rows, glyph/skin preservation and viewport clipping: " + name);
        }

        private static void CheckIconScale(F5FixtureGraphics graphics, string output)
        {
            using (var atlas = new F5IconAtlas())
            using (var target = new RenderTarget2D(graphics.Device, 520, 100))
            {
                atlas.Ensure(graphics.Device);
                graphics.Device.SetRenderTarget(target); graphics.Device.Clear(Color.Transparent);
                Main.spriteBatch.Begin();
                for (int scale = 1; scale <= 2; scale++) for (int index = 0; index < 13; index++)
                {
                    float side = (index == 12 ? 14 : 18) * scale;
                    atlas.Draw(Main.spriteBatch, index, new F5Rect(12 + index * 38 + (36 - side) / 2,
                        (scale == 1 ? 4 : 50) + (36 - side) / 2, side, side), Color.White);
                }
                Main.spriteBatch.End(); graphics.Device.SetRenderTarget(null);
                var pixels = new Color[target.Width * target.Height]; target.GetData(pixels);
                for (int scale = 1; scale <= 2; scale++) for (int index = 0; index < 13; index++)
                {
                    int left = 12 + index * 38, top = scale == 1 ? 4 : 50;
                    int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1, soft = 0;
                    for (int y = top; y < top + 36; y++) for (int x = left; x < left + 36; x++)
                    {
                        byte alpha = pixels[y * target.Width + x].A;
                        if (alpha <= 16) continue;
                        minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                        if (alpha < 240) soft++;
                    }
                    F5InputChecks.Check(maxX >= 0 && soft > 0, "each icon remains visible with sampled soft edges");
                    F5InputChecks.Check(maxX - minX + 1 <= (index == 12 ? 11 : 15) * scale &&
                        maxY - minY + 1 <= (index == 12 ? 8 : 15) * scale,
                        "icons retain source padding instead of enlarging cropped artwork to fill the slot");
                    F5InputChecks.Check(Math.Abs((minX + maxX + 1) / 2f - left - 18) <= 0.5f &&
                        Math.Abs((minY + maxY + 1) / 2f - top - 18) <= 0.5f,
                        "small artwork is centered inside its unchanged slot");
                }
                SaveTexture(target, Path.Combine(output, "icons-scale100-and200.png"));
            }
            Console.WriteLine("PASS: small source-proportional tab and keyboard artwork at 100/200 percent.");
        }

        private static void CheckSkinAndLifetime(F5FixtureGraphics graphics, F5Renderer renderer, Texture2D shared, string output)
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
                Render(graphics, renderer, output, "synthetic-dark-information", 9, 1, 0);
                Render(graphics, renderer, output, "synthetic-dark-information-off", 9, 1, 0, false);
            }
            Console.WriteLine("PASS: skin replacement, rounded fill and GPU ownership.");
        }

        private static void Render(F5FixtureGraphics graphics, F5Renderer renderer, string output,
            string name, int page, float scale, int wheel, bool enabled = true, bool failed = false, string hintTarget = null, bool fractional = false)
        {
            var matrix = Matrix.CreateScale(scale, scale, 1);
            if (fractional) { matrix.M41 = .37f; matrix.M42 = .61f; }
            Main.UIScaleMatrix = matrix;
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
            if (fractional) state.ScrollTo(state.Scroll + .43f);
            if (hintTarget != null)
                foreach (var element in state.Layout.Elements)
                    if (element.Text == hintTarget || hintTarget == "keyboard" && element.HotkeyTarget != null ||
                        hintTarget == "unavailable" && element.Command == F5Command.EnableBiome)
                    {
                        state.ScrollTo(element.Rect.Y - 100);
                        var view = state.Layout.Viewport.Offset(state.X, state.Y);
                        input.X = view.X + element.Rect.X + 2; input.Y = view.Y + element.Rect.Y - state.Scroll + 2;
                        state.Update(input); break;
                    }
            using (var target = new RenderTarget2D(graphics.Device, 1920, 1080))
            {
                graphics.Device.SetRenderTarget(target);
                graphics.Device.Clear(new Color(80, 100, 110));
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Main.UIScaleMatrix);
                int titles = Utils.RefinedTitleDraws, errors = Utils.FunctionColorErrors;
                int status = Utils.NormalStatusDraws, keys = Utils.KeyboardTextDraws;
                renderer.Draw(state, Main.UIScaleMatrix, enabled, failed);
                renderer.DrawHints(state, Main.UIScaleMatrix, null, false, failed);
                if (hintTarget != null) F5InputChecks.Check(renderer.HintLayout.Visible, "real information preview consumes the shared hint draw path");
                Main.spriteBatch.End(); graphics.Device.SetRenderTarget(null);
                CheckNavigationRows(target, state, matrix);
                F5InputChecks.Check(Utils.RefinedTitleDraws == titles + 1 && Utils.FunctionColorErrors == errors &&
                    Utils.NormalStatusDraws == status && Utils.KeyboardTextDraws == keys,
                    "actual renderer uses the Chinese display title, neutral function text and no normal-status/key text");
                if (page == 9 && scale == 1 && hintTarget == null) CheckRefinedPixels(target, state, enabled, failed);
                SaveTexture(target, Path.Combine(output, name + ".png"));
            }
        }

        private static void CheckNavigationRows(RenderTarget2D target, F5Interaction state, Matrix matrix)
        {
            F5Rect line = state.Layout.NavigationUnderline(state.Page, matrix, state.X, state.Y);
            int left = (int)Math.Round(line.X * matrix.M11 + matrix.M41), right = (int)Math.Round(line.Right * matrix.M11 + matrix.M41);
            int top = (int)Math.Round(line.Y * matrix.M22 + matrix.M42), bottom = (int)Math.Round(line.Bottom * matrix.M22 + matrix.M42);
            F5InputChecks.Check(bottom - top == 2, "selected gold navigation uses the same two device rows");
            var pixels = new Color[target.Width * target.Height]; target.GetData(pixels);
            for (int y = top; y < bottom; y++) for (int x = left; x < right; x++)
                F5InputChecks.Check(pixels[y * target.Width + x] == Color.Gold,
                    "actual production chrome fills both complete snapped navigation rows");
        }

        private static void CheckRefinedPixels(RenderTarget2D target, F5Interaction state, bool enabled, bool failed)
        {
            var pixels = new Color[target.Width * target.Height]; target.GetData(pixels);
            Texture2D skin = GameContent.TextureAssets.InventoryBack.Value;
            var source = new Color[skin.Width * skin.Height]; skin.GetData(source);
            F5Element selected = null;
            foreach (F5Element element in state.Layout.Elements)
                if (element.Command == (enabled ? F5Command.EnableBiome : F5Command.DisableBiome)) selected = element;
            F5InputChecks.Check(selected != null, "information page retains both named real controls");
            F5Rect view = state.Layout.Viewport.Offset(state.X, state.Y);
            F5Rect surface = F5Layout.ButtonSurface(selected).Offset(view.X, view.Y - state.Scroll);
            int green = 0, red = 0, minX = int.MaxValue, maxX = 0;
            for (int y = 0; y < target.Height; y++) for (int x = 0; x < target.Width; x++)
            {
                Color value = pixels[y * target.Width + x];
                bool isGreen = value == Color.LightGreen, isRed = value == Color.IndianRed;
                if (!isGreen && !isRed) continue;
                if (isGreen) green++; else red++;
                F5InputChecks.Check(!failed && surface.Contains(x + 0.5f, y + 0.5f),
                    "state-colored pixels belong only to the button named for the real state, never the function label or other button");
                CheckSkinInterior(source, skin, surface, x, y, true);
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
            }
            F5InputChecks.Check(failed ? green == 0 && red == 0 : enabled ? green > 0 && red == 0 : red > 0 && green == 0,
                "actual framebuffer has only enable green, only disable red, or no false failed state");
            if (!failed) F5InputChecks.Check(maxX - minX + 1 < surface.Width - 12,
                "state line is visibly shorter than its button");
            F5Rect nav = state.Layout.Navigation(state.Page).Offset(state.X, state.Y);
            F5Rect underline = state.Layout.NavigationUnderline(state.Page, Main.UIScaleMatrix, state.X, state.Y);
            int underlinePixels = 0;
            for (int y = (int)Math.Floor(underline.Y); y < Math.Ceiling(underline.Bottom); y++)
                for (int x = (int)Math.Floor(underline.X); x < Math.Ceiling(underline.Right); x++)
                    if (pixels[y * target.Width + x] == Color.Gold)
                    { CheckSkinInterior(source, skin, nav, x, y); underlinePixels++; }
            F5InputChecks.Check(underlinePixels > 0, "selected navigation underline is actually rendered inside the skin");
            foreach (F5Element element in state.Layout.Elements)
            {
                if (element.Kind != F5ElementKind.Hotkey) continue;
                F5Rect slot = element.Rect.Offset(view.X, view.Y - state.Scroll);
                if (slot.Y < view.Y || slot.Bottom > view.Bottom) continue;
                int y = (int)slot.Y + 2, x = (int)(slot.X + slot.Width / 2);
                F5InputChecks.Check(pixels[y * target.Width + x] == pixels[y * target.Width + (int)slot.X - 2],
                    "empty space around the small keyboard has the row surface, not an extra button background/frame");
            }
        }

        private static void CheckSkinInterior(Color[] pixels, Texture2D skin, F5Rect surface, int x, int y,
            bool fractionalSurface = false)
        {
            float left = fractionalSurface ? surface.X : (int)surface.X;
            float top = fractionalSurface ? surface.Y : (int)surface.Y;
            float width = fractionalSurface ? surface.Width : (int)surface.Width;
            float height = fractionalSurface ? surface.Height : (int)surface.Height;
            int sx = SourceCoordinate(x + 0.5f - left, width, skin.Width);
            int sy = SourceCoordinate(y + 0.5f - top, height, skin.Height);
            Color original = pixels[sy * skin.Width + sx];
            F5InputChecks.Check(original.A > 200 && original.R + original.G + original.B > 30,
                "underline pixels must cover the actual opaque skin interior, not transparent corners or its black rim");
        }

        private static int SourceCoordinate(float point, float destinationSize, int sourceSize)
        {
            float value = point < 10 ? point : point >= destinationSize - 10 ? sourceSize - destinationSize + point :
                10 + (point - 10) * (sourceSize - 20) / (destinationSize - 20);
            return Math.Max(0, Math.Min(sourceSize - 1, (int)value));
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
