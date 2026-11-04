using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // No Draw certificate, game loop, installation arm, or world simulation.
    // Exercise the real business Request and nonempty Harmony metadata instead
    // of only calling a recorder against methods without installed patches.
    internal static class NativeTargetDiagnosticRequestChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static void Run(Assembly assembly, Type trace, string output)
        {
            Action<string, object> set = (name, value) => trace.GetField(name, F).SetValue(null, value);
            Func<string, object> get = name => trace.GetField(name, F).GetValue(null);
            set("active", false); ((Timer)get("timer"))?.Dispose();
            set("initialized", true); // Never touch an installation's arm gate.
            Main.myPlayer = 0; Main.player[0] = new Player { active = true, gravDir = 1 };
            Main.gameMenu = Main.mapFullscreen = Main.hideUI = false;
            Main.maxTilesX = Main.maxTilesY = 64; Main.tile = new Tile[64, 64];
            Main.GameViewMatrix = new Terraria.Graphics.SpriteViewMatrix(null);
            Main.GameViewMatrix.SetViewportOverride(new Viewport(0, 0, 960, 640));
            Main.screenWidth = 960; Main.screenHeight = 640;
            typeof(Main).GetField("_uiScaleMatrix", F).SetValue(null, Matrix.Identity);
            typeof(PlayerInput).GetField("_originalScreenWidth", F).SetValue(null, 960);
            typeof(PlayerInput).GetField("_originalScreenHeight", F).SetValue(null, 640);
            typeof(PlayerInput).GetField("RawMouseScale", F).SetValue(null, Vector2.One);
            PlayerInput.Triggers.Initialize(); Main.keyState = new KeyboardState(); PlayerInput.MouseInfo = new MouseState();
            var input = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Input.HostInputState"), F, null,
                new object[] { (Func<IntPtr>)(() => new IntPtr(1)), (Func<IntPtr>)(() => new IntPtr(1)) }, null);
            FocusHelper.IsSelectedApplication = true;
            for (int i = 0; i < 2; i++) { Call(input, "BeginUpdate"); Call(input, "AfterMapping"); Call(input, "AfterKeyboardRefresh"); }
            var catalog = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.NativeItemCatalog"), true);
            object gesture = null;
            object blueprint = null;
            try
            {
                gesture = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.ItemBrowser.ReadOnlyTargetGesture"), F, null, new[] { input, catalog }, null);
                Require((bool)Get(gesture, "Ready"), "real target owner installs for diagnostic Request integration");
                // Install the actual second production owner. Its host is
                // intentionally absent: no Capture/Draw/Consume is invoked.
                blueprint = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintPointerProof"), F, null, new object[] { null }, null);
                Call(blueprint, "Install");
                HotkeyChord key; string reason; Require(HotkeyChord.TryParse("K", out key, out reason), "ordinary keyboard announcement chord");
                Set(gesture, "CanAnnounce", (Func<bool>)(() => true));
                Set(gesture, "AnnouncementBinding", (Func<HotkeyChord>)(() => key));
                string feedback = null; Set(gesture, "Feedback", (Action<string>)(text => feedback = text));
                Action<string> reset = file =>
                {
                    ((Timer)get("timer"))?.Dispose();
                    set("count", 0); set("request", 0); set("kind", 0); set("pass", 0L);
                    set("active", true); set("exported", false); set("requestActive", false);
                    trace.GetField("claimed", F)?.SetValue(null, true);
                    trace.GetField("errorType", F)?.SetValue(null, null);
                    trace.GetField("metadataErrorType", F)?.SetValue(null, null);
                    trace.GetField("metadataErrors", F)?.SetValue(null, 0);
                    set("output", Path.Combine(output, file)); set("identity", "owned actual Request fixture\n");
                    set("deadline", Stopwatch.GetTimestamp() + 600L * Stopwatch.Frequency);
                    set("timer", new Timer(_ => { }, null, Timeout.Infinite, Timeout.Infinite));
                };
                reset("actual-request.log");
                Call(gesture, "RequestAnnouncement");
                Require((int)Get(gesture, "pending") == 2, "observer cannot prevent the real business announcement request");
                if (!(bool)get("active")) InspectOwnerFailures(trace);
                Require((bool)get("active") && (int)get("request") == 1 && (int)get("count") == 1 &&
                    ((string)get("identity")).Contains("JueMingR.ItemTargets.ReadOnlyGesture") && ((string)get("identity")).Contains("JueMingR.Blueprint.Pointer"),
                    "actual Request records point2 with nonempty independent Harmony owners");
                Call(gesture, "Update", 6L);
                Require(!(bool)Get(gesture, "Busy") && feedback == "未取得有效的新目标，点选或宣告已取消",
                    "unconfirmed real request keeps the original finite timeout and player feedback");
                Call(gesture, "RequestAnnouncement"); Call(gesture, "Update", 12L);
                string actualFile = (string)get("output");
                Require(!(bool)Get(gesture, "Busy") && File.Exists(actualFile) && !(bool)get("active"),
                    "second real business cancellation returns only after synchronous bounded export, without ProcessExit");
                string recorded = File.ReadAllText(actualFile);
                int requestRows = File.ReadLines(actualFile).Count(line => { var columns = line.Split(','); return columns.Length == 11 && columns[4] == "2"; });
                Require(requestRows == 2 && (int)get("request") == 2 && recorded.Contains("stop=two-requests"),
                    "normal two-action export contains both actual Request rows");
                trace.GetMethod("Stop", F).Invoke(null, new object[] { "later-exit" });
                Require(File.ReadAllText(actualFile) == recorded, "later exit cannot replace or append the two-action export");

                // New observer-only APIs are exercised after the two-action
                // boundary, independently of its old/new package comparison.
                Require(trace.GetField("claimed", F) != null && trace.GetMethod("AppendOwners", F) != null &&
                    trace.GetMethod("Fail", F).GetParameters().Length == 3, "repaired observer exposes bounded failure ownership and classification");
                reset("metadata-request.log");
                Call(gesture, "RequestAnnouncement");
                trace.GetMethod("AppendOwners", F).Invoke(null, new object[] { null, "test", "test.metadata" });
                Require((bool)get("active") && (int)get("metadataErrors") == 1, "optional metadata failure does not retire request observation");
                Call(gesture, "Update", 18L);
                trace.GetMethod("Stop", F).Invoke(null, new object[] { "owned-metadata-complete" });
                string metadata = File.ReadAllText((string)get("output"));
                Require(metadata.Contains("metadataErrors=1") && metadata.Contains("stage=test.metadata"),
                    "bounded export preserves optional metadata failure classification");
                reset("fatal-request.log");
                Call(gesture, "RequestAnnouncement");
                var injected = new InvalidOperationException("owned-observer-failure");
                trace.GetMethod("Fail", F).Invoke(null, new object[] { injected, 9, "test.injected" });
                string fatalFile = (string)get("output");
                Require(!(bool)get("active") && (int)Get(gesture, "pending") == 2 && File.Exists(fatalFile),
                    "fatal observer failure exports its prefix while preserving the accepted business request");
                string fatal = File.ReadAllText(fatalFile);
                Require(fatal.Contains("stop=observer-failure") && fatal.Contains("rows=1") &&
                    fatal.Contains("observerErrorType=System.InvalidOperationException") &&
                    fatal.Contains("hresult=" + injected.HResult) && fatal.Contains("point=9 stage=test.injected"),
                    "fatal export retains the actual Request row plus exception type HResult point and fixed stage");
                Call(gesture, "Update", 24L);
                Require(!(bool)Get(gesture, "Busy") && feedback == "未取得有效的新目标，点选或宣告已取消" && File.ReadAllText(fatalFile) == fatal,
                    "observer failure cannot change business timeout or rewrite its saved prefix");

                // Model a rejected claim after an existing run: the observer
                // has no ownership and must not touch that run's prior file.
                string previous = Path.Combine(output, "previous-run.log"); File.WriteAllText(previous, "owned previous run");
                reset("previous-run.log"); set("claimed", false); set("active", false);
                trace.GetMethod("Fail", F).Invoke(null, new object[] { injected, 1, "test.rejected-claim" });
                trace.GetMethod("Stop", F).Invoke(null, new object[] { "later-exit" });
                Require(File.ReadAllText(previous) == "owned previous run", "rejected claim cannot overwrite a prior run's evidence");
                Console.WriteLine("PASS actual RequestAnnouncement with Targets+Blueprint owners; two actual terminal cancellations export synchronously; optional/fatal observer failures retain evidence and business semantics; no synthetic Draw proof.");
            }
            finally
            {
                if (gesture != null) ((IDisposable)gesture).Dispose();
                if (blueprint != null) ((IDisposable)blueprint).Dispose();
                ((IDisposable)catalog).Dispose();
                ((Timer)get("timer"))?.Dispose();
            }
        }
        private static void InspectOwnerFailures(Type trace)
        {
            foreach (var pair in new[] {
                Tuple.Create(typeof(Main), "DrawInterface"), Tuple.Create(typeof(Main), "DrawInterface_39_MouseOver"),
                Tuple.Create(typeof(Terraria.GameContent.UI.ResourceSets.PlayerResourceSetsManager), "TryToHoverOverResources"),
                Tuple.Create(typeof(Terraria.GameContent.UI.ResourceSets.ClassicPlayerResourcesDisplaySet), "TryToHover"),
                Tuple.Create(typeof(Terraria.GameContent.UI.ResourceSets.FancyClassicPlayerResourcesDisplaySet), "TryToHover"),
                Tuple.Create(typeof(Terraria.GameContent.UI.ResourceSets.HorizontalBarsPlayerResourcesDisplaySet), "TryToHover") })
                try { trace.GetMethod("PatchOwners", F).Invoke(null, new object[] { pair.Item1, pair.Item2 }); }
                catch (TargetInvocationException error) { Console.Error.WriteLine("ACTUAL METADATA FAILURE " + pair.Item1.FullName + "." + pair.Item2 + ": " + error.InnerException); }
        }
    }
}
