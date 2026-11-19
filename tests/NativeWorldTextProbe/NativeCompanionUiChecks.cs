using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using JueMingR.Platform.Hotkeys;
using JueMingR.Features.Notes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Uses the actual window, archived profiles and native Player/Item projections.
    // Layout-only preparation does not initialize graphics, run Draw or operate a
    // game. These assertions do not stand in for owner UI/IME/FPS acceptance.
    internal static class NativeCompanionUiChecks
    {
        internal static void Run(object shell)
        {
            var host = Get(shell, "Companions");
            var profiles = ((IEnumerable)Get(host, "Profiles")).Cast<object>().ToArray();
            Require(profiles.Length >= 2, "UI fixture must first persist two actual companion profiles through the production archive");
            var window = Get(shell, "CompanionUi"); var state = Get(shell, "State"); var layout = Get(state, "Layout");
            var screen = new Vector2(1280, 900); var matrix = Matrix.Identity;
            var origin = Vector2.Zero;
            Action prepare = () => Call(window, "PrepareLayout", matrix, screen);
            Action<Vector2, bool, bool, int> sample = (point, left, focused, wheel) =>
            {
                NativeToolsUiChecks.Mouse(point, left);
                Call(window, "ProcessLayoutInput", true, new KeyboardState(), point, matrix, screen, focused, wheel);
            };
            Action<string> click = key =>
            {
                var control = Controls(window).Single(c => ((string)Get(c, "Key")).StartsWith(key + "/", StringComparison.Ordinal));
                var point = NativeToolsUiChecks.Point(Get(control, "Rect"));
                sample(point, false, true, 0); sample(point, true, true, 0); sample(point, false, true, 0);
            };
            try
            {
                NativeToolsUiChecks.Mouse(origin, false);
                Call(window, "Toggle", Get(layout, "Viewport")); prepare();
                Require((bool)Get(window, "Visible"), "real manager opens");
                string[] tabs = Controls(window).Select(c => (string)Get(c, "Text")).Where(t => t == "背包" || t == "配置" || t == "聊天").ToArray();
                Require(tabs.SequenceEqual(new[] { "背包", "配置" }), "first batch exposes exactly the two actual tabs");
                var list = Get(window, "listView"); var plus = Get(Controls(window).Single(c => (string)Get(c, "Text") == "＋"), "Rect");
                Require((float)Get(plus, "Y") >= (float)Get(list, "Bottom"), "create plus remains below the persistent left list");
                int actors = ((IDictionary)Get(host, "actors")).Count;
                Guid other = (Guid)Get(profiles[1], "Id");
                click("profile:" + other);
                Require((Guid)Get(window, "selected") == other && ((IDictionary)Get(host, "actors")).Count == actors,
                    "selecting another real profile neither joins it nor changes any actor");
                int builds = (int)Get(window, "LayoutBuilds"), measurements = (int)Get(layout, "MeasurementCount");
                for (int i = 0; i < 120; i++) prepare();
                Require((int)Get(window, "LayoutBuilds") == builds && (int)Get(layout, "MeasurementCount") == measurements,
                    "120 stable real window prepares rebuild no controls and measure no text");
                var oldPanel = Get(window, "panel");
                var titlePoint = NativeToolsUiChecks.Point(Get(window, "titleBar"));
                sample(titlePoint, false, true, 0); sample(titlePoint, true, true, 0);
                sample(titlePoint + new Vector2(24, 8), true, true, 0); sample(titlePoint + new Vector2(24, 8), false, true, 0);
                Require((float)Get(Get(window, "panel"), "X") == (float)Get(oldPanel, "X") + 24 &&
                    (float)Get(Get(window, "panel"), "Y") == (float)Get(oldPanel, "Y") + 8 &&
                    (int)Get(window, "LayoutBuilds") == builds && (int)Get(layout, "MeasurementCount") == measurements,
                    "real title drag translates prepared geometry without rebuilding cells or remeasuring text");
                // Force actual overflow. At the taller fixture size the full
                // inventory fits, so a consumed wheel cannot move its offset.
                screen = new Vector2(1280, 480); prepare();
                int ownerOffset = (int)Get(window, "ownerOffset");
                var beforeList = Get(window, "listView"); var body = Get(window, "companionView");
                var bodyPoint = NativeToolsUiChecks.Point(body);
                Require((int)Get(window, "bodyMaximum") > 0, "right inventory viewport is actually shorter than its slot content");
                sample(bodyPoint, false, true, -120);
                Require((int)Get(window, "bodyOffset") > 0 && (int)Get(window, "ownerOffset") == ownerOffset &&
                    Get(window, "listView").Equals(beforeList) && (bool)Get(window, "ConsumeWheel"),
                    "actual right inventory scroll consumes the wheel while retaining the left list geometry");
                click("configuration");
                var configList = Get(window, "listView");
                Require((int)Get(window, "page") == 1 && (Guid)Get(window, "selected") == other &&
                    (float)Get(configList, "X") == (float)Get(beforeList, "X") && (float)Get(configList, "Width") == (float)Get(beforeList, "Width"),
                    "configuration keeps the same selected character in the left rail while its content determines panel height");
                click("config-tab:Character");
                screen=new Vector2(1280,360);prepare();body=Get(window,"configurationViewport");bodyPoint=NativeToolsUiChecks.Point(body);
                Action scrollBottom = () =>
                {
                    while ((int)Get(window, "bodyOffset") < (int)Get(window, "bodyMaximum"))
                    {
                        int before = (int)Get(window, "bodyOffset"); sample(bodyPoint, false, true, -120);
                        Require((int)Get(window, "bodyOffset") > before, "configuration scroll advances toward its actual body maximum");
                    }
                };
                scrollBottom(); click("request-reset"); scrollBottom();
                int expandedOffset = (int)Get(window, "bodyOffset");
                long epoch = ((JueMingR.Platform.Companions.CompanionProfile)profiles[1]).DataEpoch;
                click("cancel-reset");
                Require(!(bool)Get(window, "confirmingReset") && (int)Get(window, "bodyOffset") < expandedOffset &&
                    (int)Get(window, "bodyOffset") == (int)Get(window, "bodyMaximum"),
                    "cancelling expanded reset confirmation shrinks content and clamps the actual offset without resetting a character");
                var request = Controls(window).Single(c => ((string)Get(c, "Key")).StartsWith("request-reset/", StringComparison.Ordinal));
                var requestRect = Get(request, "Rect");
                Require((float)Get(requestRect,"Y")>=(float)Get(body,"Y")&&(float)Get(requestRect,"Bottom")<=(float)Get(body,"Bottom"),
                    "same preparation reprojects the collapsed last control wholly inside the configuration content clip at its clamped offset");
                prepare();
                request = Controls(window).Single(c => ((string)Get(c, "Key")).StartsWith("request-reset/", StringComparison.Ordinal));
                Require(Get(request, "Rect").Equals(requestRect) && ((IEnumerable)Get(host, "Profiles")).Cast<JueMingR.Platform.Companions.CompanionProfile>().Single(p => p.Id == other).DataEpoch == epoch,
                    "the next stable frame retains the corrected geometry and cancellation never performs reset");
                screen=new Vector2(1280,900);prepare();
                click("inventory");
                var config = Controls(window).Single(c => ((string)Get(c, "Key")).StartsWith("configuration/", StringComparison.Ordinal));
                var oldPoint = NativeToolsUiChecks.Point(Get(config, "Rect"));
                sample(oldPoint, false, true, 0); sample(oldPoint, true, true, 0);
                matrix = Matrix.CreateScale(1.5f); screen = new Vector2(1280, 720);
                sample(oldPoint, false, true, 0);
                Require((int)Get(window, "page") == 0, "a release after native UI-matrix/viewport change cannot execute the old tab press");
                prepare();
                config = Controls(window).Single(c => ((string)Get(c, "Key")).StartsWith("configuration/", StringComparison.Ordinal));
                var point2 = NativeToolsUiChecks.Point(Get(config, "Rect"));
                sample(point2, true, true, 0); sample(point2, false, false, 0);
                Require(!(bool)Get(window, "Visible") && (int)Get(window, "page") == 0, "focus loss closes the view without submitting its armed tab");
                sample(origin, false, true, 0);
                Call(window, "Toggle", Get(layout, "Viewport")); prepare();
                var close = Controls(window).Single(c => ((string)Get(c, "Key")).StartsWith("close/", StringComparison.Ordinal));
                var closePoint = NativeToolsUiChecks.Point(Get(close, "Rect"));
                sample(closePoint, false, true, 0); sample(closePoint, true, true, 0); sample(closePoint, false, true, 0);
                Require(!(bool)Get(window, "Visible") && (bool)Get(window, "ConsumeLeft"), "closing retains its actual mouse release for the shared shell consumer");
                sample(origin, false, true, 0);
                Require(!(bool)Get(window, "ConsumeLeft"), "a later trusted neutral sample retires only the owned physical tail");
                builds = (int)Get(window, "LayoutBuilds"); measurements = (int)Get(layout, "MeasurementCount");
                for (int i = 0; i < 120; i++) prepare();
                Require((int)Get(window, "LayoutBuilds") == builds && (int)Get(layout, "MeasurementCount") == measurements,
                    "closed window performs no layout or font work");
                Console.WriteLine("PASS companion actual two-page layout, native profile selection, stable/closed work, independent scroll, changed matrix/focus gesture retirement and real close release tail; no Draw, Windows IME, owner input or FPS claim.");
            }
            finally { Call(window, "Close"); NativeToolsUiChecks.Mouse(origin, false); }
        }
        internal static void FullInput(object context, ProbeGraphics graphics, string output)
        {
            // This leaf now reaches actual AI Spawn after native creation.
            // Reuse the normal finite NativeBody startup before any ownership
            // snapshots; null unused player slots are a headless fixture state.
            NativeCombatAimChecks.Initialize();
            Main.spawnTileX=60;Main.spawnTileY=58;Main.leftWorld=Main.topWorld=0;Main.rightWorld=Main.maxTilesX*16;Main.bottomWorld=Main.maxTilesY*16;
            for(int x=2;x<118;x++){Main.tile[x,60].active(true);Main.tile[x,60].type=TileID.Stone;}
            new HarmonyLib.Harmony("JueMingR.Tests.QuickItemOutlets").Unpatch(HarmonyLib.AccessTools.Method(typeof(Player),"Spawn"),HarmonyLib.HarmonyPatchType.Prefix,"JueMingR.Tests.QuickItemOutlets");
            NativeCompanionCoreLifecycleChecks.PrepareHuman();
            graphics.LoadCompanionAssets();
            graphics.LoadItemTextures(Main.LocalPlayer.inventory.Where(i=>i!=null&&!i.IsAir).Select(i=>i.type).Distinct());
            const string actionId = "companions.manage";
            var shell = Get(context, "Shell"); var state = Get(shell, "State"); var window = Get(shell, "CompanionUi"); var input = Get(context, "Input");
            var bindings = (HotkeyBindings)Get(Get(shell, "hotkeys"), "Bindings"); var popup = Get(shell, "HotkeyPopup");
            var previousForeground = Get(input, "foregroundWindow"); var previousGameWindow = Get(input, "gameWindow");
            var text = Get(window, "textInput"); var imeField = text.GetType().GetField("ime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var previousIme = imeField.GetValue(text); bool focused = true;
            Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(1))); Set(input, "foregroundWindow", (Func<IntPtr>)(() => focused ? new IntPtr(1) : new IntPtr(2)));
            imeField.SetValue(text, new InputIme(imeField.FieldType).GetTransparentProxy());
            Action<Vector2, bool, Keys[]> frame = (point, left, keys) => NativeToolsUiChecks.UiFrame(context, point, left, keys);
            var neutral = new Keys[0]; var chordKeys = new[] { Keys.LeftControl, Keys.F6 };
            Action release = () => frame(Vector2.Zero, false, neutral);
            Action pulse = () => { release(); frame(Vector2.Zero, false, chordKeys); release(); };
            Func<string, object> control = prefix => Controls(window).Single(c => ((string)Get(c, "Key")).StartsWith(prefix, StringComparison.Ordinal));
            try
            {
                Require(!((IEnumerable)Get(Get(shell, "Companions"), "Profiles")).Cast<object>().Any(),
                    "independent-window input fixture starts with a clean archive; portrait/resource gameplay belongs to the separate real-profile checks");
                Main.screenWidth = 960; Main.screenHeight = 760; Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions(); Main.UIScale = 1;
                Call(shell, "CloseAndSubmitPosition"); release(); release();
                NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); bindings.Poll(); return bindings.Loaded && !bindings.Busy; });
                Require(bindings.Get(actionId) == null, "new management action has no implicit default key");
                frame(Vector2.Zero, false, new[] { Keys.F5 }); release(); NativeToolsUiChecks.Nav(context, 1);
                object entry = null;
                while (true)
                {
                    var misc = Get(shell, "MiscUi"); var view = Get(misc, "view");
                    entry = NativeToolsUiChecks.Controls(misc).Single(c => (string)GetOptional(Get(c, "Element"), "HotkeyTarget") == actionId);
                    var rect = Get(entry, "Rect");
                    if ((float)Get(rect, "Y") >= (float)Get(view, "Y") && (float)Get(rect, "Bottom") <= (float)Get(view, "Bottom")) break;
                    float before = (float)Get(state, "Scroll");
                    NativeToolsUiChecks.UiFrame(context, NativeToolsUiChecks.Point(view), false, neutral, -120);
                    Require((float)Get(state, "Scroll") > before, "new standard keyboard entry is reachable through the actual Misc scroll");
                }
                var keyPoint = NativeToolsUiChecks.Point(Get(entry, "Rect"));
                NativeToolsUiChecks.Click(context, keyPoint); NativeToolsUiChecks.Click(context, keyPoint);
                Require((bool)Get(popup, "Visible") && (string)Get(popup, "Target") == actionId, "new keyboard icon opens the existing recorder for its exact stable action");
                NativeToolsUiChecks.PopupClick(context, popup, "Record"); release(); frame(Vector2.Zero, false, chordKeys); release();
                NativeQuickItemChecks.Until(() => { bindings.Poll(); Call(context, "UpdateShell"); return !bindings.Busy; });
                Require(bindings.CompletionSucceeded && bindings.Get(actionId)?.MainKey == (int)Keys.F6, "real recorder saves the new key through the existing reliable binding owner");
                var registry = (HotkeyRegistry)Get(Get(shell, "hotkeys"), "Registry");
                string otherAction = registry.Actions.First(a => a.Id != actionId && a.Context == HotkeyContext.Gameplay).Id;
                long duplicateCommand; string duplicateReason;
                Require(!bindings.TrySet(otherAction, bindings.Get(actionId), null, out duplicateCommand, out duplicateReason) && !string.IsNullOrEmpty(duplicateReason),
                    "the actual shared binding owner rejects a duplicate management chord without altering the accepted binding");
                NativeToolsUiChecks.PopupClick(context, popup, "Close"); release();
                var manage = Get(Get(shell, "MiscUi"), "companionControl");
                NativeToolsUiChecks.Click(context, NativeToolsUiChecks.Point(Get(manage, "Rect")));
                Require(!(bool)Get(state, "Visible") && (bool)Get(window, "Visible") && (bool)Get(window, "Independent") &&
                    ReferenceEquals(window, Get(shell, "CompanionUi")),
                    "the actual Misc management release closes F5 and opens the same independent companion window");
                typeof(Player).GetMethod("ResetControls", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Main.LocalPlayer, null);
                Terraria.GameInput.PlayerInput.Triggers.Current.CopyInto(Main.LocalPlayer);
                Require(!Main.LocalPlayer.controlUseItem && !Main.LocalPlayer.controlUseTile,
                    "manual parent-to-independent handoff consumes its actual release before native world input");
                var manualPanel = Get(window, "panel"); var manualTitle = NativeToolsUiChecks.Point(Get(window, "titleBar"));
                frame(manualTitle, false, neutral); frame(manualTitle, true, neutral);
                frame(manualTitle + new Vector2(18, 6), true, neutral); frame(manualTitle + new Vector2(18, 6), false, neutral);
                Require((float)Get(Get(window, "panel"), "X") >= (float)Get(manualPanel, "X") &&
                    (float)Get(Get(window, "panel"), "Y") >= (float)Get(manualPanel, "Y") && !(bool)Get(window,"Dragging") && !(bool)Get(state, "Visible"),
                    "the manually opened independent window accepts the title drag and clamps at screen edges while F5 stays closed");
                pulse();
                Require(!(bool)Get(window, "Visible") && !(bool)Get(state, "Visible"), "the existing management hotkey closes the manually opened window without reopening F5");
                NotesCapture(context, chordKeys);
                pulse();
                Require((bool)Get(window, "Visible") && (bool)Get(window, "Independent") && !(bool)Get(state, "Visible"),
                    "a trusted new edge opens only the independent management window");
                frame(Vector2.Zero, false, chordKeys); frame(Vector2.Zero, false, chordKeys);
                Require(!(bool)Get(window, "Visible"), "the first later new edge closes the independent window");
                release(); pulse();
                frame(Vector2.Zero, false, chordKeys); frame(Vector2.Zero, false, chordKeys);
                Require(!(bool)Get(window, "Visible"), "holding a close edge never reopens the window");
                release(); pulse();
                var panel = Get(window, "panel");
                int cornerX = (int)(float)Get(panel, "X") + 3, cornerY = (int)(float)Get(panel, "Y") + 3;
                // UiSurface preserves the native skin's transparent 10px corner
                // slices. Presence is tested inside the stretched surface, not
                // at a corner whose transparency is valid original artwork.
                int sampleX = (int)(float)Get(panel, "X") + 16, sampleY = (int)(float)Get(panel, "Y") + 16;
                Console.WriteLine("COMPANION DRAW before visible=" + Get(window, "Visible") + " independent=" + Get(window, "Independent") +
                    " ready=" + Get(window, "ready") + " preparedGeneration=" + Get(window, "preparedGeneration") + " generation=" + Get(window, "generation") +
                    " resources=" + Get(Get(window, "renderer"), "ResourcesCurrent") + " shellVisible=" + Get(state, "Visible") +
                    " shellReady=" + Get(state, "Ready") + " layersReady=" + Get(shell, "LayersReady") + " failed=" + Get(shell, "Failed") +
                    " panel=" + Get(panel, "X") + "," + Get(panel, "Y") + "," + Get(panel, "Width") + "," + Get(panel, "Height") +
                    " nativeMatrix=" + Main.UIScaleMatrix + " preparedMatrix=" + Get(window, "matrix") + " sample=" + sampleX + "," + sampleY);
                // Exercise the same prepared production window directly first:
                // Shell deliberately fails closed and discards the draw exception.
                // This isolated call preserves its original native stack on failure.
                graphics.Pixels(()=>Call(window,"Draw"),Matrix.Identity,960,760);
                NativeCompanionUiV3Checks.InspectBody(window);
                var pixels = graphics.Pixels(() => Call(shell, "DrawLayer"), Matrix.Identity, 960, 760);
                // Save exactly this DrawLayer result before asserting. A second
                // draw after a fail-closed transition could erase the first image.
                string drawPath = Path.Combine(output, "companion-independent-f5-closed.png");
                using (var image = new Microsoft.Xna.Framework.Graphics.Texture2D(graphics.GraphicsDevice, 960, 760))
                { image.SetData(pixels); using (var stream = File.Create(drawPath)) image.SaveAsPng(stream, 960, 760); }
                Console.WriteLine("COMPANION DRAW after visible=" + Get(window, "Visible") + " ready=" + Get(window, "ready") +
                    " shellVisible=" + Get(state, "Visible") + " failed=" + Get(shell, "Failed") + " samplePixel=" + pixels[sampleY * 960 + sampleX] +
                    " corner3Pixel=" + pixels[cornerY * 960 + cornerX] + " nontransparent=" + pixels.Count(p => p.A != 0) + " artifact=" + drawPath);
                Require(pixels[sampleY * 960 + sampleX].A > 0 && !(bool)Get(state, "Visible"), "actual DrawLayer paints the independent panel with F5 still closed");
                var title = NativeToolsUiChecks.Point(Get(window, "titleBar"));
                frame(title, false, neutral); frame(title, true, neutral); frame(title + new Vector2(26, 9), true, neutral);
                frame(title + new Vector2(26, 9), false, neutral);
                typeof(Player).GetMethod("ResetControls", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Main.LocalPlayer, null);
                Terraria.GameInput.PlayerInput.Triggers.Current.CopyInto(Main.LocalPlayer);
                Require((bool)Get(window, "Visible") && !(bool)Get(window, "Dragging") && !Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft,
                    "full shared input chain retains the real title release without a native world-use pulse");
                Require(!Main.LocalPlayer.controlUseItem && !Main.LocalPlayer.controlUseTile, "the original native CopyInto consumer receives no world use from the UI title gesture");
                var accepted = Get(window, "panel"); title = NativeToolsUiChecks.Point(Get(window, "titleBar"));
                frame(title, true, neutral); frame(title - new Vector2(30, 5), true, neutral);
                focused = false; frame(title, true, neutral);
                Require(!(bool)Get(window, "Visible"), "sampled focus loss retires the uncommitted drag");
                focused = true; frame(title, true, chordKeys); frame(title, false, chordKeys); release(); release();
                Require(!(bool)Get(window, "Visible"), "reactivation and held key tails do not reopen the window");
                pulse();
                Require((float)Get(Get(window, "panel"), "X") == (float)Get(accepted, "X") && (float)Get(Get(window, "panel"), "Y") == (float)Get(accepted, "Y"),
                    "interrupted drag restores the last accepted temporary position");
                Main.screenWidth = 1440; Main.screenHeight = 900; Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions();
                release(); title = NativeToolsUiChecks.Point(Get(window, "titleBar")); accepted=Get(window,"panel");
                frame(title, true, neutral);frame(title+new Vector2(76,32),true,neutral);
                Require(!Get(window,"panel").Equals(accepted),"scale fixture has a visible unconfirmed drag offset");
                Main.UIScale = 1.5f;
                Require(Math.Abs(Main.UIScale - 1.5f) < .001f, "scale check uses the actual supported native 150 percent setting");
                frame(title, true, neutral); frame(title, false, neutral);
                var scaledPanel = Get(window, "panel"); var transform = Main.UIScaleMatrix;
                Require(!(bool)Get(window, "Dragging") && (float)Get(scaledPanel, "X") >= 12 && (float)Get(scaledPanel, "Y") >= 12 &&
                    (float)Get(scaledPanel, "Right") <= 1440 / transform.M11 - 12 + .01f &&
                    (float)Get(scaledPanel, "Bottom") <= 900 / transform.M22 - 12 + .01f,
                    "viewport/scale change cancels the old grab and clamps the entire independent panel in the new logical domain");
                float acceptedX=Math.Max(12,Math.Min((float)Get(accepted,"X"),1440/transform.M11-(float)Get(scaledPanel,"Width")-12));
                float acceptedY=Math.Max(12,Math.Min((float)Get(accepted,"Y"),900/transform.M22-(float)Get(scaledPanel,"Height")-12));
                Require(Math.Abs((float)Get(scaledPanel,"X")-acceptedX)<.01f && Math.Abs((float)Get(scaledPanel,"Y")-acceptedY)<.01f,
                    "scale clamps the last accepted coordinates and discards the uncommitted offset");
                graphics.Image(Path.Combine(output, "companion-independent-native-150.png"), () => Call(shell, "DrawLayer"), transform, 1440, 900);
                Main.screenWidth = 960; Main.screenHeight = 760; Terraria.GameInput.PlayerInput.CacheOriginalScreenDimensions(); Main.UIScale = 1; release();
                var plus = NativeToolsUiChecks.Point(Get(control("create/"), "Rect"));
                int worldMode=Main.GameMode;
                try {Main.GameMode=3;NativeToolsUiChecks.Click(context,plus);NativeCompanionNativeCreationChecks.CancelEntry(context,graphics,output);}
                finally {Main.GameMode=worldMode;}
                release();pulse();
                Main.drawingPlayerChat = true; release(); frame(Vector2.Zero, false, chordKeys); Main.drawingPlayerChat = false;
                frame(Vector2.Zero, false, chordKeys); release();
                Require(!(bool)Get(window, "Visible"), "native chat and its held edge cannot open a management window later");
                frame(Vector2.Zero, false, new[] { Keys.W, Keys.LeftControl, Keys.F6 });
                var physical = (HotkeyInput)Get(input, "Hotkeys");
                Require((bool)Get(window, "Visible") && !physical.IsSuppressed((int)Keys.W), "UI open does not globally suppress unrelated held movement");
                release(); pulse();
                frame(Vector2.Zero, false, new[] { Keys.F5 }); release(); NativeToolsUiChecks.Nav(context, 1);
                entry = NativeToolsUiChecks.Controls(Get(shell, "MiscUi")).Single(c => (string)GetOptional(Get(c, "Element"), "HotkeyTarget") == actionId);
                keyPoint = NativeToolsUiChecks.Point(Get(entry, "Rect")); NativeToolsUiChecks.Click(context, keyPoint); NativeToolsUiChecks.Click(context, keyPoint);
                NativeToolsUiChecks.PopupClick(context, popup, "Clear");
                NativeQuickItemChecks.Until(() => { bindings.Poll(); Call(context, "UpdateShell"); return !bindings.Busy; });
                Require(bindings.CompletionSucceeded && bindings.Get(actionId) == null, "the same public binding popup reliably clears the independent action");
                NativeToolsUiChecks.PopupClick(context, popup, "Close"); release(); frame(Vector2.Zero, false, new[] { Keys.F5 }); release(); pulse();
                Require(!(bool)Get(window, "Visible") && !(bool)Get(state, "Visible"), "cleared key no longer opens either management or F5");
                NativeMouseBinding(context,bindings,window);
                NativeCompanionUiV3Checks.Draw(context,graphics,output);
                Console.WriteLine("PASS actual shared keyboard recorder/save, independent closed-F5 open/close/DrawLayer, title gesture/native mouse consumption, focus-neutral rearm and text/chat boundaries. OS IME and FPS remain unverified.");
            }
            finally
            {
                Main.drawingPlayerChat = false; Call(window, "Close"); Call(shell, "CloseAndSubmitPosition");
                imeField.SetValue(text, previousIme); Set(input, "foregroundWindow", previousForeground); Set(input, "gameWindow", previousGameWindow);
                NativeToolsUiChecks.Mouse(Vector2.Zero, false);
            }
        }
        private static void NativeMouseBinding(object context,HotkeyBindings bindings,object window)
        {
            const string action="companions.manage";
            Require(HotkeyChord.TryCreate(256,HotkeyModifiers.LeftControl,out var mouseChord,out _) &&
                bindings.TrySet(action,mouseChord,null,out _,out _),"the normal registry accepts Ctrl+Mouse1");
            NativeQuickItemChecks.Until(()=>{NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);return !bindings.Busy;});
            var player=Main.LocalPlayer;bool inventory=Main.playerInventory, mouseInterface=player.mouseInterface,lastInterface=player.lastMouseInterface;
            try
            {
                Main.playerInventory=true;player.mouseInterface=player.lastMouseInterface=false;
                NativeToolsUiChecks.UiFrame(context,new Vector2(30,30),true,Keys.LeftControl);
                Require(!(bool)Get(window,"Visible") && Main.mouseLeft &&
                    Terraria.GameInput.PlayerInput.Triggers.Current.MouseLeft,"fresh Ctrl+Mouse1 in native inventory reaches its vanilla click instead of the manager");
                NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);
                Main.playerInventory=false;player.mouseInterface=true;
                NativeToolsUiChecks.UiFrame(context,new Vector2(30,30),true,Keys.LeftControl);
                Require(!(bool)Get(window,"Visible") && Main.mouseLeft,"another native UI's pointer claim wins without an inventory flag");
                NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);
                player.mouseInterface=player.lastMouseInterface=false;
                NativeToolsUiChecks.UiFrame(context,Vector2.Zero,true,Keys.LeftControl);
                Require((bool)Get(window,"Visible"),"the same mouse chord still opens in the unobstructed world");
            }
            finally
            {
                Call(window,"Close");Main.playerInventory=inventory;player.mouseInterface=mouseInterface;player.lastMouseInterface=lastInterface;
                NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);
                Require(bindings.TrySet(action,null,null,out _,out _),"remove the temporary mouse binding");
                NativeQuickItemChecks.Until(()=>{NativeToolsUiChecks.UiFrame(context,Vector2.Zero,false);return !bindings.Busy;});
            }
        }
        private static void NotesCapture(object context, Keys[] chordKeys)
        {
            var shell = Get(context, "Shell"); var window = Get(shell, "CompanionUi"); var notes = Get(shell, "notes");
            var pins = Get(notes, "pins"); var workspace = (NotesWorkspace)Get(notes, "workspace"); var feature = workspace.Feature;
            var neutral = new Keys[0];
            Action<Vector2, bool, Keys[]> frame = (point, left, keys) => NativeToolsUiChecks.UiFrame(context, point, left, keys);
            Note note = null; Exception firstFailure = null; string stage = "load-notes-owner";
            try
            {
                NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); Call(context, "UpdateShell"); return feature.Loaded && !feature.Busy; });
                var originalIds = feature.Saved.Notes.Select(n => n.Id).ToArray();
                stage = "create-temporary-note";
                // The running Host polls Workspace, which owns completion IDs.
                // Establish and remove fixture notes through that same owner;
                // a direct Feature submit would publish an unowned completion.
                Require(workspace.Request(new NotesAction(NotesActionKind.Create)), "actual notes workspace accepts the isolated note creation");
                NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); Call(context, "UpdateShell"); return !feature.Busy; });
                note = feature.Saved.Notes.Single(n => !originalIds.Contains(n.Id));
                stage = "pin-temporary-note";
                Require(workspace.Request(new NotesAction(NotesActionKind.Pin, note.Id, x: 20, y: 20)), "actual notes workspace accepts the isolated pin action");
                NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); Call(context, "UpdateShell"); return !feature.Busy && feature.Saved.Find(note.Id).Pinned; });
                var pin = ((IEnumerable)Get(pins, "Pins")).Cast<object>().Single(p => ((Note)Get(p, "Note")).Id == note.Id);
                stage = "capture-and-drag-note";
                var point = NativeToolsUiChecks.Point(Get(pin, "Drag"));
                frame(point, false, neutral); frame(point, true, neutral);
                Require((bool)Get(notes, "CapturesPointer") && GetOptional(pins, "drag") != null, "real pinned-note toolbar owns the physical Mouse1 drag");
                var movedPoint = point + new Vector2(35, 20); frame(movedPoint, true, neutral);
                var moved = Get(pin, "Rect");
                frame(movedPoint, true, chordKeys);
                Require(!(bool)Get(window, "Visible") && GetOptional(pins, "drag") != null && Get(pin, "Rect").Equals(moved),
                    "management edge during note capture does not open, cancel or roll back the actual note drag");
                frame(movedPoint, true, neutral);
                stage = "release-tail-and-held-key";
                frame(movedPoint, false, chordKeys);
                Require(!(bool)Get(window, "Visible") && (bool)Get(notes, "ConsumeLeft"),
                    "a fresh management edge on the owned real release sample stays consumed until that physical tail is retired");
                frame(movedPoint, false, chordKeys);
                Require(!(bool)Get(window, "Visible"), "a blocked management edge is never replayed after the note releases capture");
                NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); Call(context, "UpdateShell"); return !feature.Busy; });
                Require(feature.Saved.Find(note.Id).X == (int)(float)Get(moved, "X") && feature.Saved.Find(note.Id).Y == (int)(float)Get(moved, "Y"),
                    "the actual note drag reaches its original reliable position writer without manager interference");
                stage = "ordinary-hover-new-edge";
                frame(movedPoint, false, neutral);
                Require((bool)Get(notes, "OwnsPointer") && !(bool)Get(notes, "CapturesPointer"), "the same real note remains ordinary hover after trusted release");
                frame(movedPoint, false, chordKeys);
                Require((bool)Get(window, "Visible") && !(bool)Get(Get(shell, "State"), "Visible"),
                    "ordinary note hover still admits a later trusted keyboard edge without opening F5");
                frame(Vector2.Zero, false, neutral); frame(Vector2.Zero, false, chordKeys); frame(Vector2.Zero, false, neutral);
                Require(!(bool)Get(window, "Visible"), "independent management closes on a later new edge after the note regression");
                Console.WriteLine("PASS existing note capture/release-tail priority, no blocked-key replay, preserved native note position commit and keyboard management action over ordinary note hover.");
            }
            catch (Exception error)
            {
                firstFailure = error;
                Console.Error.WriteLine("COMPANION NOTES first-failure stage=" + stage + " note=" + (note?.Id ?? "<not-created>") +
                    " workspacePending=" + GetOptional(workspace, "pendingId") + " featureBusy=" + feature.Busy + Environment.NewLine + error);
                throw;
            }
            finally
            {
                try
                {
                    Call(window, "Close"); frame(Vector2.Zero, false, neutral);
                    NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return !feature.Busy; });
                    if (note != null && feature.Saved.Find(note.Id) != null)
                    {
                        Require(workspace.Request(new NotesAction(NotesActionKind.ConfirmDelete, note.Id)), "actual notes workspace accepts cleanup confirmation for only its temporary note");
                        Require(workspace.Request(new NotesAction(NotesActionKind.Delete, note.Id)), "actual notes workspace owns the temporary note cleanup commit");
                        NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); Call(context, "UpdateShell"); return !feature.Busy; });
                        Require(feature.Saved.Find(note.Id) == null, "temporary pinned note is removed by its acknowledged workspace command");
                    }
                }
                catch (Exception cleanupError)
                {
                    Console.Error.WriteLine("COMPANION NOTES cleanup-failure after-stage=" + stage + " firstFailure=" + (firstFailure != null) +
                        " note=" + (note?.Id ?? "<not-created>") + " workspacePending=" + GetOptional(workspace, "pendingId") +
                        " featureBusy=" + feature.Busy + Environment.NewLine + cleanupError);
                    // Preserve the first exception's original stack. A cleanup
                    // error is independently logged and fails an otherwise good run.
                    if (firstFailure == null) throw;
                }
            }
        }
        // Only the OS IME service is replaced. Native character queues, text
        // lease, trusted input phases and the binding writer stay production.
        internal sealed class InputIme : RealProxy
        {
            internal InputIme(Type type) : base(type) { }
            public override IMessage Invoke(IMessage message)
            {
                var call = (IMethodCallMessage)message;
                object value = call.MethodName == "get_Composition" ? (object)"" : call.MethodName == "get_Candidates" ? false : (object)null;
                return new ReturnMessage(value, null, 0, call.LogicalCallContext, call);
            }
        }
        internal static void Membership(object shell,Guid id,bool joined)
        {
            var host=Get(shell,"Companions");var window=Get(shell,"CompanionUi");var body=(Player)Call(host,"GetBody",id);bool dead=body.dead;
            try
            {
                // Exercise the native CanTransfer predicate's dead-body branch
                // without running KillMe/Update or changing this fixture's actor
                // lifecycle. The test concerns only the existing UI projection.
                if(joined) body.dead=true;
                Require((bool)Call(host,"IsJoined",id)==joined&&!(bool)Call(host,"CanTransfer",id),"membership and temporary resource qualification are independent actual Host facts");
                Call(window,"Close");NativeToolsUiChecks.Mouse(Vector2.Zero,false);Call(window,"Toggle",Get(Get(Get(shell,"State"),"Layout"),"Viewport"));Set(window,"selected",id);Set(window,"page",0);
                Call(window,"PrepareLayout",Matrix.Identity,new Vector2(1280,900));
                var status=Controls(window).Single(c=>((string)Get(c,"Key")).StartsWith("actor-state/",StringComparison.Ordinal));
                Require((bool)Get(status,"ActorState")&&(bool)Get(status,"Enabled")&&((string)Get(status,"Hint")).Contains(joined?"收回":"加入"),"actual inventory status control offers "+(joined?"recall for an ineligible joined actor":"join for an archived offline profile"));
                Console.WriteLine("PASS companion native membership layout joined="+joined+" transfer=false action="+(joined?"recall":"join")+"; no Draw/death-lifecycle claim");
            }
            finally {body.dead=dead;Call(window,"Close");NativeToolsUiChecks.Mouse(Vector2.Zero,false);}
        }
        internal static void StatusPressIdentity(object shell,Guid id)
        {
            var host=Get(shell,"Companions");var window=Get(shell,"CompanionUi");var actors=(IDictionary)Get(host,"actors");var actor=actors[id];
            var body=(Player)Call(host,"GetBody",id);long activation=(long)Get(actor,"Generation");int timer=body.respawnTimer;bool dead=body.dead;
            var screen=new Vector2(1280,900);Action prepare=()=>Call(window,"PrepareLayout",Matrix.Identity,screen);
            Func<object> status=()=>Controls(window).Single(c=>((string)Get(c,"Key")).StartsWith("actor-state/",StringComparison.Ordinal));
            Action<Vector2,bool> sample=(p,left)=>{NativeToolsUiChecks.Mouse(p,left);Call(window,"ProcessLayoutInput",true,new KeyboardState(),p,Matrix.Identity,screen,true,0);};
            try
            {
                Require(actor!=null,"status press projection starts from the actual joined activation");
                body.dead=true;Call(window,"Close");NativeToolsUiChecks.Mouse(Vector2.Zero,false);Call(window,"Toggle",Get(Get(Get(shell,"State"),"Layout"),"Viewport"));Set(window,"selected",id);Set(window,"page",0);prepare();
                var p=NativeToolsUiChecks.Point(Get(status(),"Rect"));sample(p,false);sample(p,true);var pressed=Get(window,"armedKey");
                body.respawnTimer=timer+7;prepare();Require(pressed!=null&&Equals(Get(window,"armedKey"),pressed),"ordinary countdown/status refresh retains the valid pressed operation");
                // Change only the real Host membership projection; do not run
                // lifecycle/network work while its dictionary is temporarily
                // absent. Restore the exact actor before leaving this check.
                actors.Remove(id);prepare();Require(GetOptional(window,"armedKey")==null,"natural membership identity change retires an armed recall");sample(p,false);
                Require(!(bool)Call(host,"IsJoined",id),"release after recall-to-offline transition cannot reverse into Join");actors[id]=actor;prepare();
                p=NativeToolsUiChecks.Point(Get(status(),"Rect"));sample(p,false);sample(p,true);Set(actor,"Generation",activation+1);prepare();Require(GetOptional(window,"armedKey")==null,"same-body actual activation change retires the old pressed operation");sample(p,false);
                Require(ReferenceEquals(actors[id],actor),"release of retired activation cannot recall the replacement activation");
                Console.WriteLine("PASS bounded inventory status press identity: timer keeps press; membership and actual activation retire it; no death/network lifecycle claim.");
            }
            finally {actors[id]=actor;Set(actor,"Generation",activation);body.respawnTimer=timer;body.dead=dead;Call(window,"Close");NativeToolsUiChecks.Mouse(Vector2.Zero,false);}
        }
        internal static void Equipment(object shell, Guid id)
        {
            var host = Get(shell, "Companions"); var window = Get(shell, "CompanionUi"); var layout = Get(Get(shell, "State"), "Layout");
            var companion = (Player)Call(host, "GetBody", id); var owner = Main.LocalPlayer;
            Require((bool)Call(host, "CanTransfer", id), "equipment UI scenario uses a real active owned actor");
            var screen = new Vector2(960, 760); var matrix = Matrix.Identity;
            Action prepare = () => Call(window, "PrepareLayout", matrix, screen);
            Action<Vector2, bool, int> sample = (point, left, wheel) =>
            {
                NativeToolsUiChecks.Mouse(point, left);
                Call(window, "ProcessLayoutInput", true, new KeyboardState(), point, matrix, screen, true, wheel);
            };
            Action<bool,int> scroll = (ownerSide,direction) =>
            {
                string offsetName=ownerSide?"ownerOffset":"bodyOffset";
                int before=(int)Get(window,offsetName);
                sample(NativeToolsUiChecks.Point(Get(window,ownerSide?"ownerView":"companionView")),false,direction*120);
                Require((int)Get(window,offsetName)!=before,"actual side scroll advances to equipment slot");
            };
            Action<string,bool> click = (prefix,ownerSide) =>
            {
                string offsetName=ownerSide?"ownerOffset":"bodyOffset",maximumName=ownerSide?"ownerMaximum":"bodyMaximum";
                if(prefix.StartsWith("slot:",StringComparison.Ordinal)||prefix.StartsWith("gear:",StringComparison.Ordinal))
                    while((int)Get(window,offsetName)>0) scroll(ownerSide,1);
                object control;
                while((control=Controls(window).SingleOrDefault(c=>((string)Get(c,"Key")).StartsWith(prefix,StringComparison.Ordinal)))==null)
                { Require((int)Get(window,offsetName)<(int)Get(window,maximumName),"actual direct-item control exists: "+prefix);scroll(ownerSide,-1); }
                var point=NativeToolsUiChecks.Point(Get(control,"Rect"));sample(point,false,0);sample(point,true,0);sample(point,false,0);
            };
            var savedSlot = owner.inventory[49].Clone();
            int[] types = { ItemID.RedDye, ItemID.GrapplingHook };
            Func<int, int> total = type => InventoryCount(owner, type) + InventoryCount(companion, type);
            var originalShaders = Terraria.Graphics.Shaders.GameShaders.Armor;
            var hook = new Item(); hook.SetDefaults(ItemID.GrapplingHook);
            Require(hook.shoot > 0, "native grappling hook exposes its actual projectile category");
            bool originalHookCategory = Main.projHook[hook.shoot];
            try
            {
                // This CPU fixture bypasses Main.LoadContent. Item.SetDefaults
                // obtains dye IDs from the native armor-shader registry, while
                // Main's content loading derives hook categories from original
                // Projectile defaults. Supply those exact metadata dependencies,
                // not item.dye or a successful Equip substitute. ShaderData here
                // is never applied/rendered; no effect/Draw behavior is asserted.
                if (originalShaders.GetShaderIdFromItemId(ItemID.RedDye) == 0)
                {
                    Terraria.Graphics.Shaders.GameShaders.Armor = new Terraria.Graphics.Shaders.ArmorShaderDataSet();
                    var initializeDye = typeof(Terraria.Initializers.DyeInitializer).GetMethod("LoadBasicColorDye",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null,
                        new[] { typeof(int), typeof(float), typeof(float), typeof(float), typeof(float), typeof(int) }, null);
                    Require(initializeDye != null, "native red-dye initializer matches the fixed original game API");
                    initializeDye.Invoke(null, new object[] { ItemID.RedDye, 1f, 0f, 0f, 1.2f, 1 });
                }
                var hookProjectile = new Projectile(); hookProjectile.SetDefaults(hook.shoot);
                Main.projHook[hook.shoot] = hookProjectile.aiStyle == 7;
                Require(Terraria.Graphics.Shaders.GameShaders.Armor.GetShaderIdFromItemId(ItemID.RedDye) > 0 && Main.projHook[hook.shoot],
                    "native content metadata classifies red dye and the original grappling-hook projectile");
                Console.WriteLine("COMPANION UI equipment fixture native dyeId=" + Terraria.Graphics.Shaders.GameShaders.Armor.GetShaderIdFromItemId(ItemID.RedDye) +
                    " hookProjectile=" + hook.shoot + " nativeAiStyle=" + hookProjectile.aiStyle + " priorHookCategory=" + originalHookCategory + "; no shader Apply/Draw");
                Call(window, "Close"); NativeToolsUiChecks.Mouse(Vector2.Zero, false);
                Call(window, "Toggle", Get(layout, "Viewport")); prepare();
                click("profile:" + id + "/",false); click("inventory/",false);
                Require(companion.dye[0].IsAir && companion.miscEquips[4].IsAir && types.All(t => total(t) == 0),
                    "isolated fresh profile has no fixture dye/hook before the UI resource scenario");
                for (int i = 0; i < types.Length; i++)
                {
                    int type = types[i]; string area = i == 0 ? "Dye" : "Misc"; int slot = i == 0 ? 0 : 4;
                    owner.inventory[49].SetDefaults(type); owner.inventory[49].stack = 1; prepare();
                    Require(total(type) == 1, "one real fixture item exists before equipment: " + area);
                    var source = owner.inventory[49];
                    Console.WriteLine("COMPANION UI equipment native source=" + source.type + ":" + source.stack + " dye=" + source.dye +
                        " head/body/leg=" + source.headSlot + "/" + source.bodySlot + "/" + source.legSlot + " accessory=" + source.accessory +
                        " mount=" + source.mountType + " shoot=" + source.shoot + " canTransfer=" + Call(host, "CanTransfer", id));
                    if(i==1) click("gear:False:2/",false);
                    click("slot:True:Inventory:49/",true);
                    click("slot:False:"+area+":"+slot+"/",false);
                    var equipped = i == 0 ? companion.dye[slot] : companion.miscEquips[slot];
                    Console.WriteLine("COMPANION UI equipment actual owner49=" + owner.inventory[49].type + ":" + owner.inventory[49].stack +
                        " expected=" + area + "[" + slot + "]=" + equipped.type + ":" + equipped.stack +
                        " dyeSlots=" + string.Join(",", companion.dye.Select((item, index) => index + "=" + item.type + ":" + item.stack)) +
                        " miscSlots=" + string.Join(",", companion.miscEquips.Select((item, index) => index + "=" + item.type + ":" + item.stack)) +
                        " ownerCount=" + InventoryCount(owner, type) + " companionCount=" + InventoryCount(companion, type) +
                        " sameBody=" + ReferenceEquals(companion, Call(host, "GetBody", id)) + " feedback=" + Get(host, "Feedback"));
                    Require(equipped.type == type && equipped.stack == 1 && total(type) == 1,
                        "UI equip moves the real item into its native " + area + " slot without duplicating it");
                    click("slot:False:" + area + ":" + slot + "/",false);
                    click("slot:False:Inventory:49/",false);
                    Require((i==0?companion.dye[slot]:companion.miscEquips[slot]).IsAir && InventoryCount(companion, type) == 1 && InventoryCount(owner, type) == 0 && total(type) == 1,
                        "area-aware UI unequip removes the exact native " + area + " slot and retains one item in companion inventory");
                    int received = Array.FindIndex(companion.inventory, item => item.type == type && item.stack == 1);
                    Require(received >= 0, "actual companion inventory received unequipped item: " + area);
                    click("slot:False:Inventory:" + received + "/",false); click("slot:True:Inventory:49/",true);
                    Require(InventoryCount(owner, type) == 1 && InventoryCount(companion, type) == 0 && total(type) == 1,
                        "UI take-back completes " + area + " round trip with the actual owner/companion total unchanged");
                    var archive = Get(host, "profiles");
                    NativeQuickItemChecks.Until(() => { Call(archive, "Pump"); return (bool)Call(archive, "IsSaved", id); });
                    var committed = ((IEnumerable)Get(host, "Profiles")).Cast<JueMingR.Platform.Companions.CompanionProfile>().Single(p => p.Id == id);
                    var readback = (Player)Call(archive, "LoadBody", committed);
                    Require(InventoryCount(readback, type) == 0 && InventoryCount(owner, type) == 1,
                        "actual saved native profile retains the completed " + area + " take-back without restoring an equipment copy");
                    foreach (var item in owner.inventory) if (item.type == type) item.TurnToAir();
                }
                Console.WriteLine("PASS real companion direct UI Dye/Misc exact-slot placement, balanced unequip chain and take-back with native item conservation; no Draw/IME/FPS claim.");
            }
            finally
            {
                Call(window, "Close"); NativeToolsUiChecks.Mouse(Vector2.Zero, false);
                owner.inventory[49] = savedSlot;
                Terraria.Graphics.Shaders.GameShaders.Armor = originalShaders;
                Main.projHook[hook.shoot] = originalHookCategory;
            }
        }
        private static int InventoryCount(Player player, int type)
        {
            return new[] { player.inventory, player.armor, player.dye, player.miscEquips, player.miscDyes }
                .Sum(items => items.Where(item => item != null && item.type == type).Sum(item => item.stack));
        }
        private static object[] Controls(object window) { return ((IEnumerable)Get(window, "controls")).Cast<object>().ToArray(); }
    }
}
