using System;
using System.IO;
using System.Linq;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace Terraria
{
    // Exercise production SpriteBatch calls, not a copy of rectangle arithmetic.
    // The clip is deliberately much larger than the markers: clipping must not
    // hide an oversized primitive and make the regression appear to pass.
    internal static class ItemDecorationChecks
    {
        internal static void Run(F5FixtureGraphics graphics, string output)
        {
            CheckIconSize(graphics);
            var original = TextureAssets.MagicPixel;
            using (var one = White(graphics.Device, 1, 1))
            using (var tall = White(graphics.Device, 1, 1000))
            using (var square = White(graphics.Device, 32, 32))
            using (var renderer = new ItemsRenderer())
            {
                try
                {
                    foreach (float scale in new[] { 1f, 1.5f })
                    {
                        Color[] baseline = null;
                        foreach (Texture2D texture in new[] { one, tall, square })
                        {
                            TextureAssets.MagicPixel = graphics.Asset("marker-pixel", texture); renderer.Refresh();
                            Color[] pixels = Draw(graphics, renderer, scale, output == null ? null : Path.Combine(output,
                                "markers-" + texture.Width + "x" + texture.Height + "-scale" + (scale == 1 ? "1" : "1_5") + ".png"));
                            if (baseline == null) baseline = pixels;
                            else Check(baseline.SequenceEqual(pixels), "marker pixels must be independent of the borrowed texture dimensions");
                        }
                    }
                    renderer.Dispose();
                    Check(!one.IsDisposed && !tall.IsDisposed && !square.IsDisposed, "renderer does not dispose borrowed pixel textures");
                }
                finally { TextureAssets.MagicPixel = original; }
            }
            Console.WriteLine("PASS: production selection/cross pixels bounded and identical for 1x1, 1x1000, 32x32 textures, resource replacement, and 1/1.5 UI scale.");
        }
        internal static Color[] Draw(F5FixtureGraphics graphics, ItemsRenderer renderer, float scale, string path)
        {
            var boxes = new[] { new F5Rect(24, 24, 47, 48), new F5Rect(84, 24, 47, 48), new F5Rect(150, 24, 18, 18), new F5Rect(180, 24, 18, 18) };
            var regions = new[] {
                new F5Rect(boxes[0].Right - 11, boxes[0].Y + 2, 8, 8), new F5Rect(boxes[1].Right - 11, boxes[1].Y + 2, 8, 8),
                new F5Rect(boxes[2].X + 3, boxes[2].Y + 3, 12, 12), new F5Rect(boxes[3].X + 3, boxes[3].Y + 3, 12, 12) };
            var transform = Matrix.CreateScale(scale) * Matrix.CreateTranslation(9, 7, 0);
            using (var target = new RenderTarget2D(graphics.Device, 384, 256))
            {
                graphics.Device.SetRenderTarget(target); graphics.Device.Clear(Color.Transparent); Main.spriteBatch.Begin();
                renderer.Pass(transform, new F5Rect(0, 0, 240, 160), () =>
                { renderer.Selection(boxes[0]); renderer.Selection(boxes[1]); renderer.Cross(boxes[2], true); renderer.Cross(boxes[3], false); });
                Main.spriteBatch.End(); graphics.Device.SetRenderTarget(null);
                var pixels = new Color[384 * 256]; target.GetData(pixels);
                if (path != null) { Directory.CreateDirectory(Path.GetDirectoryName(path)); using (var file = File.Create(path)) target.SaveAsPng(file, 384, 256); }
                int ink = 0;
                for (int y = 0; y < 256; y++) for (int x = 0; x < 384; x++)
                {
                    if (pixels[y * 384 + x].A == 0) continue;
                    ink++;
                    Check(regions.Any(b => x >= b.X * scale + 8 && x < b.Right * scale + 10 && y >= b.Y * scale + 6 && y < b.Bottom * scale + 8),
                        "decoration escaped the small dot/reduced cross region at " + x + "," + y);
                }
                Check(ink > 150, "selection and cross actually produce pixels");
                for (int j = 0; j < 2; j++)
                {
                    Color center = pixels[(int)((boxes[j].Y + 6) * scale + 7) * 384 + (int)((boxes[j].Right - 7) * scale + 9)];
                    Check(center.A > 0 && center.G > center.R, "each selected card has a visible green dot");
                }
                for (int j = 2; j < 4; j++)
                    Check(pixels[(int)((boxes[j].Y + 9) * scale + 7) * 384 + (int)((boxes[j].X + 9) * scale + 9)].R > 80,
                        "each reduced cross remains visible above its backing");
                Check(pixels[(int)(48 * scale + 7) * 384 + (int)(48 * scale + 9)].A == 0, "selection leaves the item center untinted");
                return pixels;
            }
        }
        private static void CheckIconSize(F5FixtureGraphics graphics)
        {
            var original = TextureAssets.Item[8];
            using (var texture = White(graphics.Device, 100, 20))
            using (var renderer = new ItemsRenderer())
            {
                try
                {
                    TextureAssets.Item[8] = graphics.Asset("wide-grid-item", texture); renderer.Refresh();
                    foreach (bool candidate in new[] { false, true })
                    {
                        var before = new F5Rect(40, 40, candidate ? 48 : 50, candidate ? 48 : 34);
                        // Align centers exactly for a pixel comparison: only the
                        // button width changes, not item fitting or sampling.
                        var button = new F5Rect(before.X + (before.Width - 47) / 2, before.Y, 47, before.Height);
                        Color[] expected = DrawIcon(graphics, renderer, before);
                        Color[] actual = DrawIcon(graphics, renderer, ItemsLayout.IconBounds(button, candidate));
                        Check(expected.Any(c => c.A != 0) && expected.SequenceEqual(actual), "narrower button preserves original wide-item pixels");
                    }
                }
                finally { TextureAssets.Item[8] = original; }
            }
            Console.WriteLine("PASS: narrower list/candidate buttons preserve original wide-item pixels.");
        }
        private static Color[] DrawIcon(F5FixtureGraphics graphics, ItemsRenderer renderer, F5Rect rect)
        {
            using (var target = new RenderTarget2D(graphics.Device, 128, 96))
            {
                graphics.Device.SetRenderTarget(target); graphics.Device.Clear(Color.Transparent); Main.spriteBatch.Begin();
                renderer.Pass(Matrix.Identity, new F5Rect(0, 0, 128, 96), () => renderer.Item(8, rect));
                Main.spriteBatch.End(); graphics.Device.SetRenderTarget(null);
                var pixels = new Color[128 * 96]; target.GetData(pixels); return pixels;
            }
        }
        private static Texture2D White(GraphicsDevice device, int width, int height)
        { var texture = new Texture2D(device, width, height); texture.SetData(Enumerable.Repeat(Color.White, width * height).ToArray()); return texture; }
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("ITEM DECORATION CHECK FAILED: " + message); }
    }
}
