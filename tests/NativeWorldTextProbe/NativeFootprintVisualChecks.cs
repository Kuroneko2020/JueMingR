using System;
using System.IO;
using System.Reflection;
using JueMingR.Features.Footprints;
using JueMingR.Platform.Footprints;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeFootprintVisualChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static void Run(ProbeGraphics graphics, string output)
        {
            Directory.CreateDirectory(output);
            Main.gameMenu = Main.dedServ = Main.hideUI = Main.mapFullscreen = false; Main.netMode = Main.myPlayer = 0;
            Main.screenWidth = 960; Main.screenHeight = 640; typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, Matrix.Identity);
            Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(graphics.GraphicsDevice); Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, 960, 640));
            Main.player[0] = new Player { active = true, position = new Vector2(1600, 800), gravDir = 1 };
            string root = Path.Combine(Terraria.Program.SavePath, "footprint-visual");
            Main.ActivePlayerFileData = new Terraria.IO.PlayerFileData(Path.Combine(root, "fixture.plr"), false) { Player = Main.player[0] };
            Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(root, "fixture.wld"), false) { UniqueId = Guid.NewGuid() };
            Main.maxTilesX = 4200; Main.maxTilesY = 1200; Main.Map = new Terraria.Map.WorldMap(4200, 1200);
            Main.dedServ = true; try { System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Terraria.Graphics.Capture.CaptureManager).TypeHandle); } finally { Main.dedServ = false; }
            var assembly = Assembly.LoadFrom(Path.Combine(Program.Repository, "artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll"));
            object context = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker").GetNestedType("PostfixContext", Flags), Flags, null,
                new object[] { "footprints-" + new string('8', 40), Path.Combine(root, "evidence.txt"), root }, null);
            try
            {
                Call(context, "InstallInformationSources"); Call(context, "InitializeRuntime", true); var host = Get(context, "Footprints");
                Until(() => { Call(context, "UpdateRuntime"); return GetOptional(host, "Recorder") != null; });
                var shell = Get(context, "Shell"); var renderer = Get(shell, "renderer"); var popup = Get(shell, "FootprintPopup"); Call(renderer, "RefreshResources");
                var prepare = popup.GetType().GetMethod("Prepare", Flags); var measure = Delegate.CreateDelegate(prepare.GetParameters()[3].ParameterType, renderer, renderer.GetType().GetMethod("PopupMeasure", Flags));
                Action<float, float> layout = (w, h) => prepare.Invoke(popup, new object[] { w, h, Get(renderer, "FontIdentity"), measure, Get(renderer, "SkinGeneration") });
                Call(popup, "Open", 2); layout(960, 640);
                graphics.Image(Path.Combine(output, "footprints-config.png"), () => Call(renderer, "DrawFootprintPopup", popup), Matrix.Identity);
                layout(640, 220); graphics.Image(Path.Combine(output, "footprints-config-small.png"), () => Call(renderer, "DrawFootprintPopup", popup), Matrix.Identity, 640, 220);
                var panel = Get(popup, "Panel"); Require((float)Get(panel, "Bottom") <= 220, "small configuration stays inside viewport");
                var confirmation = (FootprintClearConfirmation)Get(popup, "confirmation"); string generation = (string)Get(host, "Generation");
                for (int stage = 1; stage <= 2; stage++)
                {
                    confirmation.Presented(); confirmation.Press(generation); confirmation.Release(generation); Set(popup, "dirty", true); layout(640, 220);
                    graphics.Image(Path.Combine(output, "footprints-confirm-" + stage + ".png"), () => Call(renderer, "DrawFootprintPopup", popup), Matrix.Identity, 640, 220);
                }
                Call(popup, "Close"); Call(host, "SetDisplay", true); Main.mapFullscreen = true;
                var recorder = (FootprintRecorder)Get(host, "Recorder");
                recorder.Observe(100.25f, 100.25f, FootprintPosition.Valid, true); recorder.Observe(300.25f, 100.25f, FootprintPosition.Valid, false);
                recorder.Observe(300.25f, 250.25f, FootprintPosition.Valid, false); recorder.Observe(450.25f, 250.25f, FootprintPosition.Valid, true);
                recorder.Observe(550.25f, 350.25f, FootprintPosition.Valid, false, 43416000);
                var layer = Get(host, "Layer"); Call(layer, "Update");
                var frameType = assembly.GetType("JueMingR.TerrariaHost.Map.MapView");
                object frame = Activator.CreateInstance(frameType, Flags, null, new object[] { Vector2.Zero, Vector2.Zero, 1f, 1f, 255 }, null);
                var pixels = graphics.Pixels(() => Call(layer, "DrawRoutes", frame), Matrix.Identity);
                int greenRows = 0; for (int y = 0; y < 640; y++) if (pixels[y * 960 + 200].G > 100) greenRows++;
                Require(greenRows == 1, "actual MagicPixel draws horizontal route at exactly one screen pixel");
                Require(pixels[150 * 960 + 200].A == 0 && pixels[250 * 960 + 400].A == 0, "no full-texture rectangle or cross-segment chord");
                long projections = (long)Get(layer, "ProjectedPoints");
                for (int i = 0; i < 20; i++) graphics.Pixels(() => Call(layer, "DrawRoutes", frame), Matrix.Identity);
                Require((long)Get(layer, "ProjectedPoints") == projections, "steady native draw never reprojects fixed geometry");
                graphics.Image(Path.Combine(output, "footprints-map-201h.png"), () => { Call(layer, "DrawRoutes", frame); Main.spriteBatch.End(); Call(layer, "DrawBar", frame); Main.spriteBatch.Begin(); }, Matrix.Identity);
                var playback = (FootprintPlayback)Get(layer, "Playback"); playback.Seek(2, recorder.End);
                graphics.Image(Path.Combine(output, "footprints-paused.png"), () => { Call(layer, "DrawRoutes", frame); Main.spriteBatch.End(); Call(layer, "DrawBar", frame); Main.spriteBatch.Begin(); }, Matrix.Identity);
                Console.WriteLine("PASS: original XNB footprint config/three-stage labels/compact viewport, 201h timeline and real one-pixel route pixels; inspect PNG layout separately.");
            }
            finally { Main.gameMenu = true; Call(context, "UpdateRuntime"); StopContext(context); Main.gameMenu = false; }
        }
        private static void Until(Func<bool> predicate) { var clock = System.Diagnostics.Stopwatch.StartNew(); while (!predicate()) { if (clock.ElapsedMilliseconds > 10000) throw new TimeoutException("footprint visual ready"); System.Threading.Thread.Sleep(5); } }
    }
}
