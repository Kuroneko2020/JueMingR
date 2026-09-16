using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.MapMarkers;
using JueMingR.Features.Exploration;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.Graphics.Capture;
using Terraria.Map;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeMapMarkerChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static Vector2 nativePosition, nativeOrigin;
        private static float nativeScale;
        internal static void Run(string output)
        {
            Main.dedServ = true; try { System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(CaptureManager).TypeHandle); } finally { Main.dedServ = false; }
            string root = Path.Combine(Terraria.Program.SavePath, "map-composition"); Directory.CreateDirectory(root);
            string config = Program.ProductionConfiguration;
            var assembly = Assembly.LoadFrom(Path.Combine(Program.Repository, "artifacts/build/" + config + "/work/bin/JueMingR.TerrariaHost/x86/" + config + "/net472/JueMingR.TerrariaHost.dll"));
            Require((typeof(ExplorationCounter).GetField("CellReads", Flags) == null) == (config == "Release"), "cost probe production Features configuration matches Host");
            Main.gameMenu = Main.hideUI = Main.mapFullscreen = Main.inFancyUI = Main.onlyDrawFancyUI = Main.ingameOptionsWindow = false;
            Main.netMode = Main.myPlayer = 0; Main.maxTilesX = 256; Main.maxTilesY = 128; Main.Map = new WorldMap(256, 128);
            Main.screenWidth = 960; Main.screenHeight = 640; Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null); Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, 960, 640));
            Main.player[0] = new Player { active = true, name = "map fixture", position = new Vector2(1600, 800), gravDir = 1 };
            Main.ActivePlayerFileData = new Terraria.IO.PlayerFileData(Path.Combine(root, "fixture.plr"), false) { Player = Main.player[0] };
            Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(root, "fixture.wld"), false) { UniqueId = Guid.NewGuid() };
            typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, Matrix.Identity); FiniteCostChecks.SetCpuFont(8);
            var worker = assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker", true);
            object context = Activator.CreateInstance(worker.GetNestedType("PostfixContext", Flags), Flags, null, new object[] { "map-markers-exploration-" + new string('9', 40), Path.Combine(root, "evidence.txt"), root }, null);
            try
            {
                Call(context, "InstallInformationSources"); Call(context, "InitializeRuntime", true);
                object host = Get(context, "MapFeatures"); var library = (MarkerLibrary)Get(host, "Markers");
                Until(() => { Call(context, "UpdateRuntime"); return (bool)Get(host, "ControlsEnabled") && library.Loaded && GetOptional(host, "Counter") != null; });
                Require(GetOptional(context, "DeathRecords") != null && GetOptional(context, "Guidance") != null && GetOptional(context, "WorldObjects") != null && GetOptional(context, "items") != null && GetOptional(context, "notes") != null, "complete map profile retains accepted feature chain");
                Require(!(bool)Get(host, "MarkersEnabled") && !(bool)Get(host, "DynamicEnabled"), "both ordinary defaults off");
                var bindings = (JueMingR.Platform.Hotkeys.HotkeyBindings)Get(Get(Get(context, "Shell"), "hotkeys"), "Bindings"); Until(() => { bindings.Poll(); return bindings.Loaded; }); Require(bindings.Get("map-markers.toggle") == null, "common marker hotkey initially unbound");
                Geometry(assembly);
                NativeMapDrawOracle.Run(host);
                Inputs(worker, context, host);
                NativeMapFeedbackChecks.Run(host);
                HostCounting(context, host, root);
                NativeExplorationCosts.Run(context, host, output);
                NativeMapCoexistenceChecks.Run(context, host);
                var retired = EndAndCapture(context, host); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                Require(!retired[0].IsAlive && !retired[1].IsAlive, "large current counter and map are released after session exit");
            }
            finally { Main.gameMenu = true; Call(context, "UpdateRuntime"); StopContext(context); Main.gameMenu = false; }
            Console.WriteLine("PASS: complete map profile, native geometry oracle, actual InputPostfix ownership, reliable asset commit, locate and Host counting lifecycle.");
        }
        private static void Geometry(Assembly assembly)
        {
            var isolation = new Harmony("JueMingR.Tests.MapAnchor");
            var original = typeof(MapOverlayDrawContext).GetMethod("Draw", new[] { typeof(Texture2D), typeof(Vector2), typeof(SpriteFrame), typeof(Alignment) });
            var view = assembly.GetType("JueMingR.TerrariaHost.Map.MapView", true);
            try
            {
                isolation.Patch(original, transpiler: new HarmonyMethod(typeof(NativeMapMarkerChecks).GetMethod(nameof(GeometrySink), Flags)));
                foreach (float zoom in new[] { .5f, 1f, 2.5f, 12.3f, 31.18f }) foreach (float ui in new[] { .75f, 1f, 1.5f })
                {
                    var center = new Vector2(123.25f, 60.5f); var offset = new Vector2(480 - 10 * zoom, 320 - 10 * zoom);
                    object frame = Activator.CreateInstance(view, Flags, null, new object[] { center, offset, zoom, ui, 255 }, null);
                    object[] args = { 451f, 301f, 256, 128, 0d, 0d }; Require((bool)view.GetMethod("TryPoint", Flags).Invoke(frame, args), "interior pointer accepted");
                    double x = (double)args[4], y = (double)args[5];
                    var native = new MapOverlayDrawContext(center, offset, null, zoom, ui, 1f);
                    native.Draw(null, new Vector2((float)x, (float)y), new SpriteFrame(1, 1), Alignment.Center);
                    var projected = (Vector2)Call(frame, "Project", x, y);
                    Require(Vector2.Distance(nativePosition, new Vector2(451, 301)) <= 1f && Vector2.Distance(nativePosition, projected) <= 1f && nativeOrigin == new Vector2(16, 16) && nativeScale == ui, "independent original arithmetic and center anchor <=1 rendered pixel; no tile snap or second UI scaling");
                    var encoded = MarkerCodec.Encode(new MarkerDocument(new string('a', 64), 256, 128, 0, new[] { new MarkerRecord(new string('1', 32), x, y, 8, "测试") }));
                    var restored = MarkerCodec.Decode(encoded, new string('a', 64), 256, 128).Records[0]; Require(restored.X == x && restored.Y == y, "reload preserves continuous anchor exactly");
                }
            }
            finally { isolation.Unpatch(original, HarmonyPatchType.All, isolation.Id); }
        }
        private static IEnumerable<CodeInstruction> GeometrySink(IEnumerable<CodeInstruction> input)
        {
            int rect = 0, draw = 0; var code = input.ToList();
            foreach (var instruction in code)
            {
                var method = instruction.operand as MethodInfo; if (method == null) continue;
                if (method.DeclaringType == typeof(SpriteFrame) && method.Name == "GetSourceRectangle") { instruction.opcode = OpCodes.Call; instruction.operand = typeof(NativeMapMarkerChecks).GetMethod(nameof(Source), Flags); rect++; }
                if (method.DeclaringType == typeof(SpriteBatch) && method.Name == "Draw") { instruction.opcode = OpCodes.Call; instruction.operand = typeof(NativeMapMarkerChecks).GetMethod(nameof(Draw), Flags); draw++; }
            }
            Require(rect == 1 && draw == 1, "oracle only replaces one texture extent and terminal GPU draw"); return code;
        }
        private static Rectangle Source(ref SpriteFrame frame, Texture2D texture) { return new Rectangle(0, 0, 32, 32); }
        private static void Draw(SpriteBatch batch, Texture2D texture, Vector2 position, Rectangle? source, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float depth)
        { nativePosition = position; nativeOrigin = origin; nativeScale = scale; }
        private static void Inputs(Type worker, object context, object host)
        {
            var input = Get(context, "Input"); var shell = Get(context, "Shell"); var layer = Get(host, "Layer"); var library = (MarkerLibrary)Get(host, "Markers");
            var state = Get(shell, "State"); Set(state, "Ready", true); Call(Get(shell, "renderer"), "RefreshResources");
            bool focused = true;
            Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(1))); Set(input, "foregroundWindow", (Func<IntPtr>)(() => focused ? new IntPtr(1) : IntPtr.Zero));
            object oldContext = worker.GetField("postfixContext", Flags).GetValue(null); int oldCommitted = (int)worker.GetField("hookCommitted", Flags).GetValue(null);
            worker.GetField("postfixContext", Flags).SetValue(null, context); worker.GetField("hookCommitted", Flags).SetValue(null, 1);
            PlayerInput.Triggers.Initialize(); FocusHelper.IsSelectedApplication = true; Main.blockInput = false;
            typeof(PlayerInput).GetField("_originalScreenWidth", Flags).SetValue(null, 960); typeof(PlayerInput).GetField("_originalScreenHeight", Flags).SetValue(null, 640);
            typeof(PlayerInput).GetField("RawMouseScale", Flags).SetValue(null, Vector2.One);
            var viewType = layer.GetType().Assembly.GetType("JueMingR.TerrariaHost.Map.MapView", true);
            Action draw = () => Call(layer, "Completed", Activator.CreateInstance(viewType, Flags, null, new object[] { new Vector2(128, 64), new Vector2(480, 320), 2f, 1f, 255 }, null));
            Action<int, int, bool, bool, Keys, string> frame = (x, y, left, right, key, request) =>
            {
                Call(input, "BeginUpdate"); Call(shell, "BeforeInput");
                PlayerInput.MouseX = x; PlayerInput.MouseY = y; PlayerInput.MouseInfo = new MouseState(x, y, 0, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, right ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released);
                PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft = left; PlayerInput.Triggers.Current.MouseRight = right;
                if (request != null) PlayerInput.Triggers.Current.KeyStatus[request] = true;
                PlayerInput.Triggers.Update(); Main.mouseLeft = left; Main.mouseRight = right; Main.mouseX = x; Main.mouseY = y; FocusHelper.IsSelectedApplication = focused;
                Call(input, "AfterMapping"); Main.keyState = key == Keys.None ? new KeyboardState() : new KeyboardState(key);
                worker.GetMethod("InputPostfix", Flags).Invoke(null, null);
                Require(!(bool)Get(shell, "Failed"), "actual shell stays available during map input");
            };
            Action neutral = () => frame(0, 0, false, false, Keys.None, null);
            Action pick = () => { neutral(); neutral(); Main.mapFullscreen = true; Call(host, "SetMarkers", true); draw(); frame(451, 301, false, true, Keys.None, null); frame(451, 301, false, false, Keys.None, null); Require((bool)Get(layer, "Picking"), "fresh right click owns picker"); };
            Func<Vector2> option = () => { Array options = (Array)Get(layer, "options"); object r = options.GetValue(0); return new Vector2((float)Get(r, "X") + 20, (float)Get(r, "Y") + 20); };
            try
            {
                var bindings = (JueMingR.Platform.Hotkeys.HotkeyBindings)Get(Get(shell, "hotkeys"), "Bindings");
                JueMingR.Platform.Hotkeys.HotkeyChord chord; string reason; long command;
                Require(JueMingR.Platform.Hotkeys.HotkeyChord.TryCreate((int)Keys.F6, JueMingR.Platform.Hotkeys.HotkeyModifiers.None, out chord, out reason) && bindings.TrySet("map-markers.toggle", chord, null, out command, out reason), "map common binding admitted");
                Until(() => { bindings.Poll(); return !bindings.Busy; });
                pick(); frame(451, 301, false, false, Keys.F6, null);
                Require(!(bool)Get(host, "MarkersEnabled") && !(bool)Get(layer, "Picking"), "actual full-map common hotkey disables marker picker"); neutral();
                pick(); frame(451, 301, false, false, Keys.Escape, "Inventory"); PlayerInput.Triggers.Current.CopyInto(Main.LocalPlayer);
                Require(!(bool)Get(layer, "Picking") && !Main.LocalPlayer.controlInv && Main.mapFullscreen, "picker Esc cancels only picker and cannot enter native Inventory"); neutral();
                foreach (string takeover in new[] { "MapFull", "ToggleCameraMode", "capture", "map", "world", "focus" })
                {
                    pick(); var p = option(); frame((int)p.X, (int)p.Y, true, false, Keys.None, null);
                    var world = Main.ActiveWorldFileData; var map = Main.Map; object capture = Get(CaptureManager.Instance, "_interface");
                    if (takeover == "capture") Set(capture, "Active", true);
                    if (takeover == "map") Main.Map = new WorldMap(256, 128);
                    if (takeover == "world") Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(rootDummy, "other.wld"), false) { UniqueId = Guid.NewGuid() };
                    if (takeover == "focus") focused = false;
                    frame((int)p.X, (int)p.Y, false, false, Keys.None, takeover == "MapFull" || takeover == "ToggleCameraMode" ? takeover : null);
                    Require(!library.Busy && library.Saved.Records.Count == 0 && !(bool)Get(layer, "Picking"), "same-sample takeover cannot commit old selection: " + takeover);
                    Set(capture, "Active", false); Main.Map = map; Main.ActiveWorldFileData = world; focused = true; neutral(); neutral();
                    if (takeover == "world") { Call(context, "UpdateRuntime"); Until(() => { Call(context, "UpdateRuntime"); return library.Loaded && GetOptional(host, "Counter") != null; }); }
                }
                pick(); var position = option(); frame((int)position.X, (int)position.Y, true, false, Keys.None, null); frame((int)position.X, (int)position.Y, false, false, Keys.None, null);
                Until(() => { Call(context, "UpdateRuntime"); return !library.Busy; });
                Require(library.Saved.Records.Count == 1 && library.Saved.Records[0].X == 113.5 && library.Saved.Records[0].Y == 54.5, "actual shared physical input commits frozen continuous point once");
                var record = library.Saved.Records[0]; Main.mapFullscreenScale = float.NaN; neutral(); Require((bool)Call(host, "Locate", record.Id), "locate command accepted"); neutral();
                Require(Main.mapFullscreen && Main.mapFullscreenScale == 2.5f && Main.mapFullscreenPos == new Vector2(113.5f, 54.5f), "locate uses native default for invalid zoom and never snaps the marker");
                Require(Main.LocalPlayer.position == new Vector2(1600, 800), "locate never moves the player");
            }
            finally { worker.GetField("postfixContext", Flags).SetValue(null, oldContext); worker.GetField("hookCommitted", Flags).SetValue(null, oldCommitted); Main.mapFullscreen = false; }
        }
        private static readonly string rootDummy = Path.GetTempPath();
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static WeakReference[] EndAndCapture(object context, object host)
        { var refs = new[] { new WeakReference(Get(host, "Counter")), new WeakReference(Main.Map) }; Main.gameMenu = true; Call(context, "UpdateRuntime"); Main.Map = null; Main.tile = null; return refs; }
        private static void HostCounting(object context, object host, string root)
        {
            long now = 0; Set(host, "milliseconds", (Func<long>)(() => now));
            Call(host, "SetDynamic", true); Call(host, "Recount"); Call(host, "PauseScan", false);
            for (int i = 0; i < 100; i++) { now += 17; Call(context, "UpdateRuntime"); }
            var counter = (ExplorationCounter)Get(host, "Counter"); Require(counter.Current && counter.Count == 0, "host initial all-dark result is established");
            Call(host, "Recount"); Call(host, "PauseScan", true); var tile = new MapTile { Light = 255 }; Main.Map.SetTile(1, 1, ref tile);
            for (int i = 0; i < 2000; i++) { now += 17; Call(context, "UpdateRuntime"); }
            Require(counter.Count == 1 && counter.Paused && counter.Scanning, "actual Host maintains baseline while manual scan is paused");
            var history = (ExplorationHistory)Get(host, "history"); Until(() => { Call(context, "UpdateRuntime"); return history.Saved; });
            Require(File.Exists(Path.Combine(root, "JueMingRData/records/exploration/" + Get(host, "Pair") + ".json")), "actual full Host saves a throttled complete summary");
            Call(host, "SetDynamic", false); Main.gameMenu = true; Call(context, "UpdateRuntime"); Require(GetOptional(host, "Counter") == null, "session exit releases current map counter");
            Main.gameMenu = false; Until(() => { Call(context, "UpdateRuntime"); return GetOptional(host, "Counter") != null; }); counter = (ExplorationCounter)Get(host, "Counter");
            Require(!counter.Scanning && !counter.Current && !(bool)Get(host, "FastScan") && !counter.Paused, "off plus persisted history is historical and restores no scan speed/pause");
            Main.maxTilesX = 300; Main.maxTilesY = 140; Main.Map = new WorldMap(300, 140); Call(host, "SetDynamic", true);
            Until(() => { Call(context, "UpdateRuntime"); var c = GetOptional(host, "Counter") as ExplorationCounter; return c != null && c.Current; });
            counter = (ExplorationCounter)Get(host, "Counter"); Require(counter.Total == 42000 && counter.Count == 0, "same world object with changed logical dimensions rebuilds valid memory result");
        }
        private static void Until(Func<bool> value)
        { var clock = System.Diagnostics.Stopwatch.StartNew(); while (!value()) { if (clock.ElapsedMilliseconds > 5000) throw new TimeoutException("map native state"); Thread.Sleep(2); } }
    }
}
