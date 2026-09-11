using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.WorldTargets;
using JueMingR.Platform.WorldTargets;
using JueMingR.TerrariaHost.WorldTargets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Terraria
{
    internal static class WorldTargetGraphicsChecks
    {
        internal static void Logical()
        {
            WorldTargetObservationChecks.Prepare(); Main.screenPosition = new Vector2(100, 200);
            var target = new WorldTarget { X = 300, Y = 350, Width = 56, Height = 46 };
            foreach (float gravity in new[] { 1f, -1f }) foreach (long ticks in new[] { 0L, 6250000L, 17500000L, 35000000L })
            {
                Vector2 center = WorldTargetWorldLayer.Screen(target.CenterX, target.CenterY, gravity);
                var animation = new WorldTargetAnimation(ticks, gravity);
                for (int i = 0; i < 3; i++)
                {
                    var pose = WorldTargetArrows.At(target, animation, i); Vector2 p = WorldTargetWorldLayer.Screen(pose.X, pose.Y, gravity);
                    Vector2 direction = new Vector2(pose.DirectionX, pose.DirectionY * gravity), difference = center - p;
                    Check(Math.Abs(difference.X * direction.Y - difference.Y * direction.X) < .001 && Vector2.Dot(difference, direction) > 0, "production mirrored position and direction agree");
                    Check(Vector2.Distance(center, p) + pose.Length * .6f <= WorldTargetArrows.Extent(target) + 1, "complete raster arrow and bob are inside clip extent");
                }
            }
            var world = new WorldTargetWorldLayer(new List<WorldTarget>(), () => true, () => WorldTargetSettings.Default);
            Check(world.Draw() && world.ResourceCreations == 0, "empty actual draw consumer creates no resource and needs no device");
            Console.WriteLine("PASS: production arrow screen transform, inward direction, full clipping extent and zero-target resource gate; no device.");
        }
        internal static void Run()
        {
            using (var graphics = new F5FixtureGraphics())
            {
                WorldTargetObservationChecks.Prepare(); Main.mapFullscreen = Main.hideUI = false;
                var settings = WorldTargetSettings.Default.WithEnabled(WorldTargetKind.LifeCrystal, true).WithEnabled(WorldTargetKind.ChilletEgg, true);
                var targets = new List<WorldTarget> {
                    new WorldTarget { Kind = WorldTargetKind.LifeCrystal, X = 100, Y = 200, Width = 32, Height = 32 },
                    new WorldTarget { Kind = WorldTargetKind.LifeCrystal, X = 132, Y = 200, Width = 32, Height = 32 },
                    new WorldTarget { Kind = WorldTargetKind.ChilletEgg, X = 400, Y = 200, Width = 36, Height = 38 } };
                var world = new WorldTargetWorldLayer(targets, () => true, () => settings);
                using (var output = new RenderTarget2D(graphics.Device, 800, 600))
                {
                    graphics.Device.SetRenderTarget(output); graphics.Device.Clear(Color.Black);
                    Main.spriteBatch.Begin(); var clip = graphics.Device.ScissorRectangle;
                    foreach (long ticks in new[] { 0L, 6250000L, 17500000L, 35000000L })
                    { Main.gameTimeCache = new GameTime(TimeSpan.FromTicks(ticks), TimeSpan.FromSeconds(1.0 / 60)); Check(world.Draw() && world.LastDrawn == 9, "each complete target has exactly three actual arrow draws"); }
                    Check(world.ResourceCreations == 1, "one reused arrow texture across targets and times");
                    Main.hideUI = true; world.Draw(); Check(world.LastDrawn == 0, "native hidden UI gate"); Main.hideUI = false;
                    Main.LocalPlayer.gravDir = -1; world.Draw(); Check(world.LastDrawn == 9, "actual inverted draw remains available");
                    Check(graphics.Device.ScissorRectangle == clip, "caller scissor preserved");
                    Main.spriteBatch.End();
                    // Deliberately invalid caller batch tests the local exception
                    // boundary, not an environment deferral or silent PASS.
                    Check(world.Draw() && world.Failure != null, "draw exception cannot abort later native UI");
                    Main.spriteBatch.Begin(); Main.spriteBatch.Draw(GameContent.TextureAssets.MagicPixel.Value, new Rectangle(0, 0, 4, 4), Color.Red); Main.spriteBatch.End();
                    graphics.Device.SetRenderTarget(null); var pixels = new Color[800 * 600]; output.GetData(pixels);
                    Check(pixels[801].R == 255, "following batch still draws after local failure");
                }
                world.Clear(); Check(typeof(WorldTargetWorldLayer).GetField("arrow", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(world) == null, "owned texture retired on clear");
            }
            Console.WriteLine("PASS: actual arrow XNA draws, multi-time/adjacent objects, resource reuse/exit, gates and preserved following UI.");
        }
        private static void Check(bool value, string message) { WorldTargetObservationChecks.Check(value, message); }
    }
}
