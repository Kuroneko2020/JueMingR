using System;
using System.Collections.Generic;
using System.IO;
using JueMingR.Features.WorldTargets;
using JueMingR.Platform.WorldTargets;
using JueMingR.TerrariaHost.WorldTargets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content.Readers;
using ReLogic.Graphics;

namespace Terraria
{
    // Explicit, isolated preview of the production draw consumer and local native
    // textures. No game/world/save is loaded; the synthetic arrangement is fixed.
    internal static class WorldTargetVisualChecks
    {
        internal static void Run(string content, string output)
        {
            Directory.CreateDirectory(output);
            using (var graphics = new F5FixtureGraphics())
            {
                var services = new GameServiceContainer(); services.AddService(typeof(IGraphicsDeviceService), new GraphicsService(graphics.Device));
                var textures = new List<Texture2D>();
                using (var reader = new XnbReader(services))
                try
                {
                    foreach (int id in new[] { 12, 236, 639, 751, 752 })
                    {
                        using (var stream = File.OpenRead(Path.Combine(content, "Images", "Tiles_" + id + ".xnb")))
                            textures.Add(reader.FromStream<Texture2D>(stream));
                        Console.WriteLine("Native target texture " + id + ": " + textures[textures.Count - 1].Width + "x" + textures[textures.Count - 1].Height);
                    }
                    DynamicSpriteFont font;
                    using (var stream = File.OpenRead(Path.Combine(content, "Fonts", "Mouse_Text.xnb"))) font = reader.FromStream<DynamicSpriteFont>(stream);
                    WorldTargetObservationChecks.Prepare(); Main.screenWidth = 960; Main.screenHeight = 480;
                    var targets = new List<WorldTarget>(); var settings = WorldTargetSettings.Default;
                    for (int kind = 0; kind < 5; kind++)
                    {
                        settings = settings.WithEnabled((WorldTargetKind)kind, true);
                        targets.Add(Target((WorldTargetKind)kind, 6 + kind * 11, 8));
                        targets.Add(Target((WorldTargetKind)kind, 6 + kind * 11, 20));
                        targets.Add(Target((WorldTargetKind)kind, 8 + kind * 11, 20));
                    }
                    var world = new WorldTargetWorldLayer(targets, () => true, () => settings);
                    try
                    {
                        using (var canvas = new RenderTarget2D(graphics.Device, 960, 480))
                        for (int frame = 0; frame <= 71; frame++)
                        {
                            graphics.Device.SetRenderTarget(canvas); graphics.Device.Clear(new Color(30, 43, 47));
                            Main.gameTimeCache = new GameTime(TimeSpan.FromMilliseconds(frame * 100), TimeSpan.FromMilliseconds(100));
                            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
                            Main.spriteBatch.DrawString(font, "Selected native static frames / production arrows / " + (frame / 10.0).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " s", new Vector2(20, 20), Color.White);
                            Main.spriteBatch.DrawString(font, "Adjacent independent objects", new Vector2(20, 240), Color.White);
                            foreach (var target in targets) DrawNative(textures[(int)target.Kind], target);
                            world.Draw();
                            Main.spriteBatch.End(); graphics.Device.SetRenderTarget(null);
                            if (world.Failure != null || world.LastDrawn != targets.Count * 3 || world.ResourceCreations != 1)
                                throw new InvalidOperationException("Production animation preview draw/resource failure.");
                            using (var stream = new FileStream(Path.Combine(output, "frame-" + frame.ToString("D3") + ".png"), FileMode.CreateNew)) canvas.SaveAsPng(stream, 960, 480);
                        }
                    }
                    finally { world.Clear(); }
                }
                finally { foreach (var texture in textures) texture.Dispose(); }
            }
            Console.WriteLine("PASS: 72 native-texture frames from the production arrow renderer, including adjacent objects and one full orbit. Viewing/owner acceptance remain separate.");
        }
        private static WorldTarget Target(WorldTargetKind kind, int x, int y)
        {
            var target = new WorldTarget { Kind = kind, TileX = x, TileY = y, X = x * 16, Y = y * 16, Width = 32, Height = 32 };
            if (kind == WorldTargetKind.SleepingDigtoise) { target.X -= 9; target.Y -= 8; target.Width = 56; target.Height = 46; }
            if (kind == WorldTargetKind.ChilletEgg) { target.X -= 2; target.Y += 2; target.Width = 36; target.Height = 38; }
            return target;
        }
        private static void DrawNative(Texture2D texture, WorldTarget target)
        {
            if (target.Kind == WorldTargetKind.ChilletEgg)
            {
                // The actual single-frame asset is 38x36, independently of the
                // established 36x38 world observation/display envelope. Show
                // the complete image instead of clipping it to that envelope.
                if (texture.Width != 38 || texture.Height != 36) throw new InvalidOperationException("Unexpected native egg preview asset.");
                Main.spriteBatch.Draw(texture, new Vector2(target.X, target.Y), new Rectangle(0, 0, 38, 36), Color.White);
            }
            else if (target.Kind == WorldTargetKind.SleepingDigtoise)
                Main.spriteBatch.Draw(texture, new Vector2(target.X, target.Y), new Rectangle(0, 0, (int)target.Width, (int)target.Height), Color.White);
            else
                for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++)
                    Main.spriteBatch.Draw(texture, new Vector2(target.X + x * 16, target.Y + y * 16), new Rectangle(x * 18, y * 18, 16, 16), Color.White);
        }
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
