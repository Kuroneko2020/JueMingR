using System;
using System.Collections.Generic;
using JueMingR.Features.EntityLabels;
using JueMingR.TerrariaHost.EntityLabels;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria
{
    internal static class EntityWorldChecks
    {
        internal static void Logical()
        {
            Main.LocalPlayer = new Player { active = true }; Main.screenPosition = new Vector2(100, 200); Main.screenHeight = 600;
            var label = new EntityLabel { Y = 300, Height = 40 };
            Check(EntityWorldLayer.ScreenTop(label, 30) == 64, "normal text above interpolated top edge");
            Main.LocalPlayer.gravDir = -1;
            Check(EntityWorldLayer.ScreenTop(label, 30) == 424, "inverted gravity mirrors object edge while preserving upright text above it");
            Main.LocalPlayer.gravDir = 1;
            Console.WriteLine("PASS: actual world projection normal/inverted-gravity anchor, without creating a graphics device.");
        }
        internal static void Run()
        {
            using (var graphics = new F5FixtureGraphics())
            {
                Main.LocalPlayer = new Player { active = true, dead = true }; Main.gameMenu = Main.dedServ = Main.mapFullscreen = Main.hideUI = false;
                Main.netMode = 1; Main.screenPosition = new Vector2(100, 200); Main.screenWidth = 800; Main.screenHeight = 600;
                Main.GameViewMatrix.ZoomMatrix = Matrix.Identity;
                var labels = new List<EntityLabel> { new EntityLabel { Name = "敌怪显名", Health = "20/100", SourceSlot = 2,
                    X = 500, Y = 500, Rgb = 0xCD5C5C, NameSize = 70, HealthSize = 57 } };
                var world = new EntityWorldLayer(labels, () => true);
                using (var target = new RenderTarget2D(graphics.Device, 800, 600))
                {
                    graphics.Device.SetRenderTarget(target); graphics.Device.Clear(Color.Black);
                    Main.spriteBatch.Begin();
                    var scissor = graphics.Device.ScissorRectangle;
                    Check(world.Draw() && world.LastDrawn == 1, "dead client world label draws under caller batch");
                    int measured = world.MeasurementCount;
                    for (int i = 0; i < 20; i++) world.Draw();
                    Check(world.MeasurementCount == measured, "unchanged text never remeasures each draw");
                    Main.mapFullscreen = true; world.Draw(); Check(world.LastDrawn == 0, "fullscreen map has no world projection"); Main.mapFullscreen = false;
                    Main.GameViewMatrix.ZoomMatrix = Matrix.CreateScale(1.5f); world.Draw();
                    Check(world.MeasurementCount == measured, "camera zoom does not invalidate logical font metrics");
                    Main.GameViewMatrix.ZoomMatrix = Matrix.Identity;
                    GameContent.FontAssets.MouseText = graphics.Asset("changed-label-font", graphics.CreateFont(12, 24, -3, 5)); world.Draw();
                    Check(world.MeasurementCount == measured + 2, "font identity replacement remeasures both lines");
                    Utils.ThrowF5Text = true; Check(world.Draw() && world.Failure != null, "text exception is contained without halting following layers"); Utils.ThrowF5Text = false;
                    Check(graphics.Device.ScissorRectangle == scissor, "world labels preserve caller scissor");
                    Main.spriteBatch.Draw(GameContent.TextureAssets.MagicPixel.Value, new Rectangle(0, 0, 4, 4), new Rectangle(0, 0, 1, 1), Color.Red);
                    Main.spriteBatch.End(); graphics.Device.SetRenderTarget(null);
                    var pixels = new Color[800 * 600]; target.GetData(pixels); Check(pixels[801].R == 255, "following caller draw still succeeds after label failure");
                }
                world.Clear(); Check(world.CachedEntries == 0, "world exit releases cached text and font identity");
            }
            Console.WriteLine("PASS: entity world labels use actual XNA font/draw, cached metrics, modal gates and preserved caller batch.");
        }
        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
