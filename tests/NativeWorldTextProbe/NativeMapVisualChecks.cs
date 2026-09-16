using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingR.Features.MapMarkers;
using JueMingR.Features.Exploration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Map;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeMapVisualChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static void Run(ProbeGraphics graphics, string output, bool alignmentOnly = false)
        {
            Directory.CreateDirectory(output); graphics.LoadMarkerTextures(); graphics.LoadDeathTexture();
            Main.gameMenu = Main.dedServ = Main.hideUI = Main.mapFullscreen = false; Main.netMode = Main.myPlayer = 0;
            Main.screenWidth = 960; Main.screenHeight = 640; typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, Matrix.Identity);
            Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(graphics.GraphicsDevice); Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, 960, 640));
            Main.player[0] = new Player { active = true, position = new Vector2(1600, 800), gravDir = 1 };
            string root = Path.Combine(Terraria.Program.SavePath, "map-visual");
            Main.ActivePlayerFileData = new Terraria.IO.PlayerFileData(Path.Combine(root, "fixture.plr"), false) { Player = Main.player[0] };
            Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(root, "fixture.wld"), false) { UniqueId = Guid.NewGuid() };
            Main.maxTilesX = 4200; Main.maxTilesY = 1200; Main.Map = new WorldMap(4200, 1200);
            Main.dedServ = true; try { System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Terraria.Graphics.Capture.CaptureManager).TypeHandle); } finally { Main.dedServ = false; }
            var assembly = Assembly.LoadFrom(Path.Combine(Program.Repository, "artifacts/build/Debug/work/bin/JueMingR.TerrariaHost/x86/Debug/net472/JueMingR.TerrariaHost.dll"));
            object context = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker").GetNestedType("PostfixContext", Flags), Flags, null,
                new object[] { "map-markers-exploration-" + new string('9', 40), Path.Combine(root, "evidence.txt"), root }, null);
            try
            {
                Call(context, "InstallInformationSources"); Call(context, "InitializeRuntime", true); var host = Get(context, "MapFeatures"); var library = (MarkerLibrary)Get(host, "Markers");
                Until(() => { Call(context, "UpdateRuntime"); return library.Loaded && (bool)Get(host, "ControlsEnabled") && GetOptional(host, "Counter") != null; });
                var shell = Get(context, "Shell"); var renderer = Get(shell, "renderer"); var popup = Get(shell, "MapPopup"); Call(renderer, "RefreshResources");
                var prepare = popup.GetType().GetMethod("Prepare", Flags); var measure = Delegate.CreateDelegate(prepare.GetParameters()[3].ParameterType, renderer, renderer.GetType().GetMethod("PopupMeasure", Flags));
                Action<float, float> layout = (w, h) => prepare.Invoke(popup, new object[] { w, h, Get(renderer, "FontIdentity"), measure, Get(renderer, "SkinGeneration") });
                if (alignmentOnly) { AlignmentImages(graphics, output, context, host, shell, renderer, popup, layout); return; }
                Call(popup, "Open", false); layout(960, 640); graphics.Image(Path.Combine(output, "map-empty.png"), () => Call(renderer, "DrawMapPopup", popup), Matrix.Identity);
                int[] icons = { 8, 48, 50, 224, 171, 393, 966, 29 };
                for (int i = 0; i < 11; i++) { long op = library.Create(new MarkerRecord((i + 1).ToString("x32"), 200 + i * 40.125, 200.375, icons[i % 8], i == 0 ? "营地家的第一处标记点" : "标记" + i)); Until(() => { library.Poll(); return library.LastOperation == op; }); }
                layout(960, 640); graphics.Image(Path.Combine(output, "map-management.png"), () => Call(renderer, "DrawMapPopup", popup), Matrix.Identity);
                Require(((System.Collections.ICollection)Get(popup, "Icons")).Count == 10, "normal full page displays all ten markers");
                Call(popup, "Execute", 2, null, 0f, 0f); layout(960, 640);
                graphics.Image(Path.Combine(output, "map-second-page.png"), () => Call(renderer, "DrawMapPopup", popup), Matrix.Identity);
                Call(popup, "Execute", 1, null, 0f, 0f); layout(960, 640);
                var workspace = (MarkerWorkspace)Get(host, "Workspace"); workspace.Poll(); workspace.BeginEdit(library.Saved.Records[0].Id); layout(960, 640);
                graphics.Image(Path.Combine(output, "map-name-edit.png"), () => Call(renderer, "DrawMapPopup", popup), Matrix.Identity); workspace.CancelEdit();
                layout(640, 220); graphics.Image(Path.Combine(output, "map-management-small.png"), () => Call(renderer, "DrawMapPopup", popup), Matrix.Identity, 640, 220);
                Set(popup, "scroll", 10); Set(popup, "dirty", true); layout(640, 220);
                graphics.Image(Path.Combine(output, "map-management-small-tail.png"), () => Call(renderer, "DrawMapPopup", popup), Matrix.Identity, 640, 220);
                Call(popup, "Open", true); layout(960, 640); graphics.Image(Path.Combine(output, "exploration-controls.png"), () => Call(renderer, "DrawMapPopup", popup), Matrix.Identity);
                Set(popup, "Hovered", ((System.Collections.Generic.List<int>)Get(popup, "Commands")).IndexOf(24)); layout(960, 640);
                graphics.Image(Path.Combine(output, "exploration-dynamic-help.png"), () => Call(renderer, "DrawMapPopup", popup), Matrix.Identity);
                Set(popup, "Hovered", -1); layout(960, 640);
                long now = 1000; Set(host, "milliseconds", (Func<long>)(() => now));
                Action update = () => { now += 334; Call(context, "UpdateRuntime"); };
                // These previews own only a map, not a populated live Tile world.
                // SetTile is the real map store; UpdateLighting may invoke native
                // UpdateType and clear a cell whose world Tile is absent.
                var lit = new MapTile { Light = 255 };
                for (int x = 100; x < 520; x++) for (int y = 100; y < 860; y++) Main.Map.SetTile(x, y, ref lit);
                Call(host, "Recount");
                Call(host, "PauseScan", true); update(); layout(960, 640);
                graphics.Image(Path.Combine(output, "exploration-paused.png"), () => Call(renderer, "DrawMapPopup", popup), Matrix.Identity);
                Call(host, "PauseScan", false); Set(host, "FastScan", true);
                Until(() => { update(); return !((ExplorationCounter)Get(host, "Counter")).Scanning; }); layout(960, 640);
                Require((string)Get(host, "ExplorationText") == "已揭示 6.33%" && (string)Get(host, "ScanText") == "上次结果", "actual completed count exposes ratio and honest historical context without a completion notice: " + Get(host, "ExplorationText") + " / " + Get(host, "ScanText"));
                graphics.Image(Path.Combine(output, "exploration-complete.png"), () => Call(renderer, "DrawMapPopup", popup), Matrix.Identity);
                Call(host, "SetDynamic", true); Until(() => { update(); return ((ExplorationCounter)Get(host, "Counter")).Current; });
                for (int x = 640; x < 1920; x += 64) Main.Map.SetTile(x, 960, ref lit);
                update(); Require(((ExplorationCounter)Get(host, "Counter")).Pending, "actual changed blocks still awaiting the bounded dynamic budget"); layout(960, 640);
                graphics.Image(Path.Combine(output, "exploration-updating.png"), () => Call(renderer, "DrawMapPopup", popup), Matrix.Identity);
                Until(() => { update(); return ((ExplorationCounter)Get(host, "Counter")).Current; }); layout(960, 640);
                graphics.Image(Path.Combine(output, "exploration-current.png"), () => Call(renderer, "DrawMapPopup", popup), Matrix.Identity);
                Call(popup, "Suspend"); var state = Get(shell, "State"); Set(state, "Ready", true); Call(state, "Navigate", 2); Call(state, "RestoreVisible");
                Call(renderer, "Prepare", state, 960f, 640f, 1f); Call(renderer, "PrepareMapValue");
                graphics.Image(Path.Combine(output, "map-f5-page.png"), () => Call(renderer, "Draw", state, Matrix.Identity, false, false), Matrix.Identity);
                Call(popup, "Suspend"); Call(host, "SetMarkers", true); Main.mapFullscreen = true;
                var layer = Get(host, "Layer"); var frameType = assembly.GetType("JueMingR.TerrariaHost.Map.MapView");
                object frame = Activator.CreateInstance(frameType, Flags, null, new object[] { Vector2.Zero, Vector2.Zero, 1f, 1f, 255 }, null);
                graphics.Image(Path.Combine(output, "map-native-icons.png"), () => Call(layer, "DrawIcons", frame), Matrix.Identity);
                Set(layer, "picking", true); Set(layer, "pointX", 220.25); Set(layer, "pointY", 150.75); Set(layer, "anchor", new Vector2(900, 580));
                graphics.Image(Path.Combine(output, "map-picker-clamped.png"), () => { Call(layer, "DrawIcons", frame); Main.spriteBatch.End(); Call(layer, "Overlay", frame); Main.spriteBatch.Begin(); }, Matrix.Identity);
                var saved = Terraria.GameContent.TextureAssets.Item[8]; Terraria.GameContent.TextureAssets.Item[8] = null;
                try { graphics.Image(Path.Combine(output, "map-resource-fallback.png"), () => Call(layer, "DrawIcons", frame), Matrix.Identity); Require(library.Saved.Records[0].Icon == 8, "fallback never rewrites actual style identity"); }
                finally { Terraria.GameContent.TextureAssets.Item[8] = saved; }
                Set(layer, "picking", false); PixelAnchors(graphics, library, layer, frameType);
                var deathHost = Get(context, "DeathRecords"); var deaths = (JueMingR.Features.DeathHistory.DeathHistory)Get(deathHost, "History");
                Until(() => { Call(context, "UpdateRuntime"); return deaths.Snapshot.Known; });
                for (int i = 0; i < 8; i++)
                {
                    var moment = DateTimeOffset.UtcNow.AddSeconds(i);
                    var fact = new JueMingR.Platform.DeathHistory.DeathFact(JueMingR.Platform.DeathHistory.DeathEventId.Create(moment, Guid.NewGuid()), moment.Offset, true, (200 + i * 40) * 16, 270 * 16, "隔离共存死亡点");
                    Until(() => { Call(context, "UpdateRuntime"); return deaths.Accept(fact); });
                }
                Call(deathHost, "SetEnabled", true); Until(() => { Call(context, "UpdateRuntime"); return deaths.Snapshot.Markers.Count == 8; });
                var drawing = Get(layer, "drawing").GetType(); var oldIcons = Main.MapIcons; Main.MapIcons = new MapIconOverlay();
                try
                {
                    graphics.Image(Path.Combine(output, "map-death-coexistence.png"), () => {
                        drawing.GetMethod("BeginMap", Flags).Invoke(null, null);
                        try { string text = "native"; Main.MapIcons.Draw(Vector2.Zero, Vector2.Zero, null, 1, 1, 255, ref text); }
                        finally { drawing.GetMethod("EndMap", Flags).Invoke(null, new object[] { null }); }
                    }, Matrix.Identity);
                }
                finally { Main.MapIcons = oldIcons; }
                Console.WriteLine("PASS: actual native marker resources, fallback, compact/short management, editor and exploration controls rendered; inspect generated PNGs separately.");
            }
            finally { Main.gameMenu = true; Call(context, "UpdateRuntime"); StopContext(context); Main.gameMenu = false; }
        }
        private static void AlignmentImages(ProbeGraphics graphics, string output, object context, object host, object shell, object renderer, object popup, Action<float, float> layout)
        {
            long now = 1000; Set(host, "milliseconds", (Func<long>)(() => now));
            var lit = new MapTile { Light = 255 };
            for (int x = 100; x < 520; x++) for (int y = 100; y < 860; y++) Main.Map.SetTile(x, y, ref lit);
            Call(host, "SetDynamic", true); Set(host, "FastScan", true);
            Until(() => { now += 334; Call(context, "UpdateRuntime"); return ((ExplorationCounter)Get(host, "Counter")).Current; });
            Require((string)Get(host, "ExplorationText") == "已揭示 6.33%" && (string)Get(host, "ScanText") == "", "alignment preview uses a real current result");
            Call(popup, "Open", true); layout(344, 292);
            graphics.Image(Path.Combine(output, "exploration-centered.png"), () => Call(renderer, "DrawMapPopup", popup), Matrix.Identity, 344, 292);
            Call(popup, "Suspend"); var state = Get(shell, "State"); Set(state, "Ready", true); Call(state, "Navigate", 2); Call(state, "RestoreVisible");
            Call(renderer, "Prepare", state, 960f, 640f, 1f); Call(renderer, "PrepareMapValue");
            var page = Get(state, "Layout"); var view = Get(page, "Viewport");
            var value = ((System.Collections.IEnumerable)Get(page, "Elements")).Cast<object>().Single(e => Get(e, "Command").ToString() == "ExplorationValue");
            var rect = Get(value, "Rect");
            float rowX = (float)Get(state, "X") + (float)Get(view, "X"), rowY = (float)Get(state, "Y") + (float)Get(view, "Y") + (float)Get(rect, "Y") - (float)Get(state, "Scroll");
            var matrix = Matrix.CreateTranslation(4 - rowX, 4 - rowY, 0);
            graphics.Image(Path.Combine(output, "map-row-right-aligned.png"), () => Call(renderer, "Draw", state, matrix, false, false), Matrix.Identity, 530, (int)Math.Ceiling((float)Get(rect, "Height")) + 8);
            Console.WriteLine("PASS: two focused actual-font alignment previews rendered from the current Host result.");
        }
        private static void PixelAnchors(ProbeGraphics graphics, MarkerLibrary library, object layer, Type frameType)
        {
            var original = library.Saved; int cases = 0;
            try
            {
                foreach (int id in new[] { 8, 48, 50, 224, 171, 393, 966, 29 })
                foreach (float zoom in new[] { 960f / 4200 * .599f, 2.5f, 31.18f }) foreach (float ui in new[] { .75f, 1f, 1.5f })
                {
                    var position = new Vector2(500.25f, 300.5f); var offset = new Vector2(480 - 10 * zoom, 320 - 10 * zoom);
                    object frame = Activator.CreateInstance(frameType, Flags, null, new object[] { position, offset, zoom, ui, 255 }, null);
                    object[] point = { 451f, 301f, 4200, 1200, 0d, 0d }; Require((bool)CallWithArguments(frame, "TryPoint", point), "visual input point accepted");
                    var document = new MarkerDocument(original.Pair, 4200, 1200, 1, new[] { new MarkerRecord(new string('1', 32), (double)point[4], (double)point[5], id, "像素") });
                    // The CPU input suite separately exercises disk admission and
                    // acknowledgement; this checks exact codec reload + real draw.
                    var restored = MarkerCodec.Decode(MarkerCodec.Encode(document), original.Pair, 4200, 1200); Set(library, "Saved", restored);
                    var actual = graphics.Pixels(() => Call(layer, "DrawIcons", frame), Matrix.Identity);
                    var texture = Terraria.GameContent.TextureAssets.Item[id].Value; var record = restored.Records[0];
                    Require(Main.itemAnimations[id] == null, "native preview item uses whole source frame");
                    var native = new MapOverlayDrawContext(position, offset, null, zoom, 28 * ui / Math.Max(texture.Width, texture.Height), 1);
                    var expected = graphics.Pixels(() => native.Draw(texture, new Vector2((float)record.X, (float)record.Y), new Terraria.DataStructures.SpriteFrame(1, 1), Terraria.UI.Alignment.Center), Matrix.Identity);
                    var a = Bounds(actual); var b = Bounds(expected);
                    Require(a.Width > 0 && a.Height > 0 && Math.Abs(a.Left - b.Left) <= 1 && Math.Abs(a.Top - b.Top) <= 1 && Math.Abs(a.Right - b.Right) <= 1 && Math.Abs(a.Bottom - b.Bottom) <= 1, "actual XNB pixel extent agrees with independent native centered draw within one rendered pixel"); cases++;
                }
                Console.WriteLine("PASS: " + cases + " actual-device XNB pixel comparisons after continuous-point codec reload; native map context is the independent draw reference.");
            }
            finally { Set(library, "Saved", original); }
        }
        private static object CallWithArguments(object value, string name, object[] args) { return value.GetType().GetMethod(name, Flags).Invoke(value, args); }
        private static Rectangle Bounds(Color[] pixels)
        { int l = 960, t = 640, r = -1, b = -1; for (int y = 0; y < 640; y++) for (int x = 0; x < 960; x++) if (pixels[y * 960 + x].A != 0) { l = Math.Min(l, x); r = Math.Max(r, x); t = Math.Min(t, y); b = Math.Max(b, y); } return new Rectangle(l, t, Math.Max(0, r - l + 1), Math.Max(0, b - t + 1)); }
        private static void Until(Func<bool> done) { var watch = System.Diagnostics.Stopwatch.StartNew(); while (!done()) { if (watch.ElapsedMilliseconds > 5000) throw new TimeoutException("map visual data"); Thread.Sleep(2); } }
    }
}
