using System;
using System.IO;
using JueMingR.Features.WorldObjectText;
using JueMingR.Platform.WorldObjectText;
using JueMingR.TerrariaHost.World;
using JueMingR.TerrariaHost.WorldObjectText;
using Microsoft.Xna.Framework;
using Terraria;

namespace NativeWorldTextProbe
{
    internal static class NativeSignAnchorChecks
    {
        internal static void Run(ProbeGraphics graphics, WorldTileObservation world, WorldObjectHostObservation source, string output)
        {
            // All three sign materials and native mounting styles 0..4:
            // standing, hanging, left, right, wall. Text is the owner's sample.
            int[] types = { 55, 425, 573 };
            foreach (bool inverted in new[] { false, true })
            {
                Main.tile = new Tile[256, 128]; Main.sign = new Sign[32000]; source.EndSession();
                Main.screenPosition = Vector2.Zero; Main.LocalPlayer.position = new Vector2(480, 320);
                Main.LocalPlayer.gravDir = inverted ? -1 : 1; Main.GameViewMatrix.Zoom = new Vector2(1.25f);
                for (int row = 0; row < types.Length; row++) for (int style = 0; style < 5; style++)
                {
                    int x = 10 + style * 10, y = 12 + row * 9;
                    NativeWorldChecks.Put(x, y, types[row], style, 2);
                    Main.sign[row * 5 + style] = new Sign { x = x, y = y, text = "药水/食物/材料" };
                }
                var settings = WorldObjectSettings.Default.WithMode(WorldObjectKind.Sign, WorldObjectMode.All);
                var discovery = new WorldObjectDiscovery(source); var layer = new WorldObjectTextWorldLayer(discovery, () => true);
                discovery.SetPresentationGate(layer.MayPresent, layer.IsPrepared);
                for (int tick = 0; tick < 100; tick++) { world.BeginTick(); discovery.Update(settings, null); layer.Prepare(settings); }
                graphics.Scene(layer, Path.Combine(output, "native-sign-mountings-" + (inverted ? "inverted" : "normal") + ".png"));
                Color[] pixels = graphics.WorldPixels(layer);
                Require(layer.Failure == null && layer.LastDrawn == 15, "all materials and mountings reach actual Draw");
                for (int row = 0; row < types.Length; row++) for (int style = 0; style < 5; style++)
                {
                    int x = 10 + style * 10, y = 12 + row * 9;
                    var board = graphics.ObjectArtBounds(types[row], style, 2, true);
                    float top = inverted ? 640 - y * 16 - board.Bottom : y * 16 + board.Top;
                    var expected = Vector2.Transform(new Vector2(x * 16 + 16, top), Main.GameViewMatrix.ZoomMatrix);
                    // Rows/columns are far enough apart to isolate every label,
                    // while the 40-pixel headroom also captures the old gap.
                    int bottom = -1;
                    for (int py = Math.Max(0, (int)expected.Y - 40); py < Math.Min(640, (int)expected.Y + 24); py++)
                        for (int px = Math.Max(0, (int)expected.X - 70); px < Math.Min(960, (int)expected.X + 70); px++)
                            if (pixels[py * 960 + px].A != 0) bottom = Math.Max(bottom, py);
                    float gap = expected.Y - bottom - 1;
                    Require(bottom >= 0 && gap >= -3 && gap <= 2, "body touches signboard, excluding chains/posts: type=" + types[row] + ", style=" + style + ", inverted=" + inverted + ", gap=" + gap);
                }
                layer.Clear(); discovery.Clear();
            }
            Main.LocalPlayer.gravDir = 1; Main.GameViewMatrix.Zoom = Vector2.One;
            Console.WriteLine("PASS: actual XNB signboard pixels for 3 materials x 5 mounting styles x both gravities; owner's text at default size/125% zoom, chain/post excluded.");
        }
        private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException("Native sign anchor: " + reason); }
    }
}
