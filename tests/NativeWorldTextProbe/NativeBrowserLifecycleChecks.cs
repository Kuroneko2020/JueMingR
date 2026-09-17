using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingR.Platform.Hotkeys;
using JueMingR.Features.ItemBrowser;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Actual composition, input owners and native XNB resources. Only the world
    // and save directory are isolated; production locate/close/clear callbacks
    // remain attached throughout the regression.
    internal static class NativeBrowserLifecycleChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        internal static void Run(ProbeGraphics graphics, string output, Assembly assembly)
        {
            string root = Path.Combine(Terraria.Program.SavePath, "browser-lifecycle"); Directory.CreateDirectory(root);
            typeof(NativeChestScanChecks).GetMethod("Reset", Flags).Invoke(null, null);
            typeof(NativeChestScanChecks).GetMethod("Place", Flags).Invoke(null, new object[] { 0, 20, 20, 21, 99 });
            Main.gameMenu = Main.hideUI = Main.mapFullscreen = Main.inFancyUI = Main.onlyDrawFancyUI = Main.ingameOptionsWindow = Main.drawingPlayerChat = Main.blockInput = false;
            Main.netMode = Main.myPlayer = 0; Main.LocalPlayer.active = true; Main.LocalPlayer.chest = -1; Main.LocalPlayer.position = new Vector2(300, 300); Main.LocalPlayer.gravDir = 1;
            Main.LocalPlayer.mouseInterface = Main.mouseText = false;
            Main.screenPosition = Vector2.Zero; Main.screenWidth = 960; Main.screenHeight = 640;
            Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null); Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, 960, 640));
            Main.Map = new Terraria.Map.WorldMap(Main.maxTilesX, Main.maxTilesY);
            Main.ActivePlayerFileData = new Terraria.IO.PlayerFileData(Path.Combine(root, "fixture.plr"), false) { Player = Main.LocalPlayer };
            Main.ActiveWorldFileData = new Terraria.IO.WorldFileData(Path.Combine(root, "fixture.wld"), false) { UniqueId = Guid.NewGuid() };
            typeof(Main).GetField("_uiScaleMatrix", Flags).SetValue(null, Matrix.Identity);
            typeof(PlayerInput).GetField("_originalScreenWidth", Flags).SetValue(null, 960); typeof(PlayerInput).GetField("_originalScreenHeight", Flags).SetValue(null, 640);
            typeof(PlayerInput).GetField("RawMouseScale", Flags).SetValue(null, Vector2.One); PlayerInput.Triggers.Initialize();
            object context = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Phase0SHarmonyWorker").GetNestedType("PostfixContext", Flags), Flags, null,
                new object[] { "item-browser-" + new string('9', 40), Path.Combine(root, "evidence.txt"), root }, null);
            try
            {
                Call(context, "InstallInformationSources"); Call(context, "InitializeRuntime", true);
                object shell = Get(context, "Shell"), input = Get(context, "Input"), state = Get(shell, "State"), browser = Get(context, "Browser");
                object page = Get(shell, "Browser"), knowledge = Get(browser, "Knowledge"), locator = Get(browser, "Locator");
                Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(1))); Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(1)));
                Set(shell, "LayersReady", true); FocusHelper.IsSelectedApplication = true;
                var wait = System.Diagnostics.Stopwatch.StartNew();
                while (!(bool)Get(Get(shell, "preferences"), "UiLoaded"))
                { Call(context, "UpdateRuntime"); if (wait.ElapsedMilliseconds > 10000) throw new TimeoutException("lifecycle settings"); Thread.Sleep(5); }
                for (int i = 0; i < 1800; i++) Call(knowledge, "Step", true);
                var workspace = (BrowserWorkspace)Get(knowledge, "Workspace"); var catalog = (BrowserCatalog)Get(Get(knowledge, "Native"), "Catalog");
                graphics.LoadItemTextures(catalog.Search("", 0, false).Take(64)); graphics.LoadItemTextures(new[] { 9 });
                graphics.LoadItemTextures(catalog.Search("wood", 16, true).Take(64));
                workspace.Query = "wood"; workspace.Category = 16; workspace.SortByName = true;
                Call(Get(page, "query"), "Insert", "wood");
                Set(page, "locatorView", 2); Call(Get(page, "locator"), "Insert", "#9");
                Action<int, int, bool, bool> frame = (x, y, down, f5) =>
                {
                    Call(input, "BeginUpdate"); Call(shell, "BeforeInput"); Require(!(bool)Get(shell, "Failed"), "lifecycle BeforeInput remains healthy");
                    PlayerInput.MouseInfo = new MouseState(x, y, 0, down ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                    PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft = down; PlayerInput.Triggers.Update(); Main.mouseLeft = down;
                    Call(input, "AfterMapping"); Main.keyState = f5 ? new KeyboardState(Keys.F5) : new KeyboardState(); Call(input, "AfterKeyboardRefresh");
                    Call(Get(browser, "Targets"), "ProcessInput"); Call(shell, "ProcessInput"); Require(!(bool)Get(shell, "Failed"), "lifecycle ProcessInput remains healthy");
                    Call(context, "UpdateRuntime"); Call(context, "UpdateShell"); Require(!(bool)Get(shell, "Failed"), "lifecycle AfterUpdate remains healthy");
                    graphics.Pixels(() => Call(shell, "DrawLayer"), Matrix.Identity); Require(!(bool)Get(shell, "Failed"), "lifecycle Draw remains healthy");
                };
                Func<int, Point> point = command =>
                {
                    object part = ((IEnumerable)Get(page, "Parts")).Cast<object>().Single(p => (int)Get(p, "Command") == command);
                    object rect = Get(Get(part, "Element"), "Rect");
                    return new Point((int)((float)Get(rect, "X") + (float)Get(rect, "Width") / 2), (int)((float)Get(rect, "Y") + (float)Get(rect, "Height") / 2));
                };
                Action<int> click = command => { Point p = point(command); frame(p.X, p.Y, false, false); frame(p.X, p.Y, true, false); frame(p.X, p.Y, false, false); };
                frame(0, 0, false, false); frame(0, 0, false, false); Call(state, "Navigate", 3);
                frame(0, 0, false, true); frame(0, 0, false, false);
                Require((bool)Get(state, "Visible"), "real F5 key opens browser before locate");
                click(17);
                for (int i = 0; i < 100 && (bool)Get(locator, "scanning"); i++) frame(0, 0, false, false);
                Require(((IList)Get(locator, "Results")).Count == 1 && !(bool)Get(state, "Visible"), "real locate click finds chest and production callback closes F5");
                Require(graphics.Pixels(() => Call(locator, "Draw"), Matrix.Identity).Any(c => c.A > 0 && c.G > c.R), "located chest has world highlight");
                frame(0, 0, false, false); frame(0, 0, false, true); frame(0, 0, false, false);
                Require((bool)Get(state, "Visible") && (int)Get(state, "Page") == 3, "real F5 key reopens the same query page after successful locate");
                Require(((IEnumerable)Get(page, "Parts")).Cast<object>().Any(p => (int)Get(p, "Command") == 27), "real pre-clear page contains locator candidates");
                click(18); frame(0, 0, false, false);
                Require(((IList)Get(locator, "Results")).Count == 0 && !(bool)Get(locator, "scanning") && ((IList)Get(locator, "Details")).Count == 0, "real clear click retires results and details");
                Require(!graphics.Pixels(() => Call(locator, "Draw"), Matrix.Identity).Any(c => c.A > 0), "cleared locator emits no world pixels");
                Require(!(bool)Get(shell, "Failed") && (bool)Get(state, "Visible"), "clear leaves F5 usable");
                Require(workspace.Query == "" && workspace.Category == 0 && !workspace.SortByName && workspace.Selected == 0 && !workspace.Detail && workspace.CatalogOffset == 0 &&
                    (string)Get(Get(page, "query"), "Text") == "" && (string)Get(Get(page, "locator"), "Text") == "" && ((int[])Get(page, "locatorCandidates")).Length == 0 && (int)Get(page, "locatorView") == 0,
                    "real clear restores both drafts and the complete initial directory state");
                Require(((IEnumerable)Get(page, "Parts")).Cast<object>().Any(p => (int)Get(p, "Command") == 19) && !((IEnumerable)Get(page, "Parts")).Cast<object>().Any(p => (int)Get(p, "Command") == 27), "actual post-clear layout shows catalog icons without locator candidates");
                graphics.Image(Path.Combine(output, "browser-after-clear.png"), () => Call(shell, "DrawLayer"), Matrix.Identity, 960, 640);
                click(5); Require(workspace.Query == "wood" && workspace.Category == 16 && workspace.SortByName && (string)Get(Get(page, "locator"), "Text") == "", "actual Back restores browsing without resurrecting locator input");
                click(6); Require(workspace.Query == "" && workspace.Category == 0 && !workspace.SortByName, "actual Forward restores the cleared catalog");
                Call(state, "Navigate", 2); frame(0, 0, false, false);
                graphics.Image(Path.Combine(output, "announcement-bindings.png"), () => Call(shell, "DrawLayer"), Matrix.Identity, 960, 640);
                var bindings = (HotkeyBindings)Get(Get(shell, "hotkeys"), "Bindings");
                foreach (var chordCase in new[] { Tuple.Create("LeftControl+K", "bound"), Tuple.Create("LeftControl+LeftShift+LeftAlt+Mouse5", "long") })
                {
                    HotkeyChord chord; string reason; long command;
                    Require(HotkeyChord.TryParse(chordCase.Item1, out chord, out reason) && bindings.TrySet("announcement.send", chord, null, out command, out reason), "visual chord uses the real shared binding owner");
                    wait.Restart(); while (bindings.Busy) { bindings.Poll(); if (wait.ElapsedMilliseconds > 10000) throw new TimeoutException("visual chord save"); Thread.Sleep(5); }
                    Require(bindings.CompletionSucceeded, "visual chord saved in isolated directory"); frame(0, 0, false, false);
                    var renderer = Get(shell, "renderer"); var size = Get(renderer, "sendValueSize");
                    Require((float)Get(size, "Width") <= 108 && (string)Get(Get(renderer, "AnnouncementControls"), "SendBindingText") == chord.DisplayText, "actual native binding label fits its box while retaining full chord");
                    graphics.Image(Path.Combine(output, "announcement-bindings-" + chordCase.Item2 + ".png"), () => Call(shell, "DrawLayer"), Matrix.Identity, 960, 640);
                }
                Console.WriteLine("PASS: production composition and physical locate click -> automatic close -> F5 reopen -> clear click; world pixels disappear and F5 stays available.");
            }
            finally { var browser = GetOptional(context, "Browser"); if (browser != null) ((IDisposable)browser).Dispose(); StopContext(context); }
        }
    }
}
