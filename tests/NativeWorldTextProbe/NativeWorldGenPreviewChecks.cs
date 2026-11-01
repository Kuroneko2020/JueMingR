using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.GameContent.UI.States;
using Terraria.GameInput;
using Terraria.Testing;
using Terraria.UI;
using Terraria.Utilities;
using Terraria.WorldBuilding;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Original native generator, load-page update and shared input owner are
    // executed. Only Open's terminal asset/UI creation is observed in CPU mode;
    // that limit does not stand in for the explicit original-UI graphics scope.
    internal static class NativeWorldGenPreviewChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static int opened;
        private static WorldGenerator.Controller observedController;
        private static bool ObserveOpen()
        { opened++; observedController = WorldGenerator.CurrentController; Main.MenuUI.SetState(new UIState()); return false; }
        internal static void Run(object context, string output)
        {
            Assembly host = context.GetType().Assembly;
            object input = Get(context, "Input"), shell = Get(context, "Shell");
            var hooksType = host.GetType("JueMingR.TerrariaHost.WorldGenPreview.WorldGenPreviewHooks", true);
            var hooks = Activator.CreateInstance(hooksType, Flags, null, new object[] { input, (Action<Exception>)(e => Console.WriteLine("PREVIEW FAILURE " + e)) }, null);
            var observer = new Harmony("JueMingR.Tests.WorldGenPreview.OpenSink");
            bool oldDebug = DebugOptions.enableDebugCommands;
            try
            {
                Call(hooks, "Install", typeof(Main).Assembly);
                Require(GetOptional(hooks, "Failure") == null, "All exact original preview seams install: " + GetOptional(hooks, "Failure"));
                object owner = Get(hooks, "Owner"); Require((bool)Get(owner, "Ready"), "Preview readiness commits after all seams.");
                var patched = Harmony.GetAllPatchedMethods().Where(m => Harmony.GetPatchInfo(m).Owners.Contains("JueMingR.WorldGenPreview")).ToArray();
                File.WriteAllLines(Path.Combine(output, "patched-methods.txt"), patched.Select(m => m.DeclaringType.FullName + "." + m.Name + " | " + m));
                Require(patched.Any(m => m.DeclaringType == typeof(UIWorldLoad) && m.Name == "Update") && patched.Any(m => m.DeclaringType == typeof(WorldGenerator) && m.Name == "GenerateWorld"), "Actual Harmony targets include native entry and generator.");
                observer.Patch(typeof(UIWorldGenDebug).GetMethod("Open"), prefix: new HarmonyMethod(typeof(NativeWorldGenPreviewChecks).GetMethod(nameof(ObserveOpen), Flags)));
                DebugOptions.enableDebugCommands = false;
                Main.MenuUI = new UserInterface(); Main.gameMenu = true;
                // Constructor asset reads are avoided through its native server
                // branch; all later Update/layout behavior remains the original.
                Main.netMode = 2; Main.dedServ = true; var page = new UIWorldLoad(); Main.dedServ = false; Main.netMode = 0;
                Main.MenuUI.SetState(page);
                Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(7))); Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(7)));
                NativeQuickItemChecks.Sample(input, new Keys[0]); NativeQuickItemChecks.Sample(input, new Keys[0]);
                Require(!(bool)Get(owner, "IsGenerationAvailable"), "Page class alone cannot promise a usable generation.");
                WorldGen.generatingWorld = WorldGen.isGeneratingOrLoadingWorld = true;
                WorldGen.Manifest = new WorldManifest();
                var controller = new WorldGenerator.Controller();
                var generator = new WorldGenerator(17, WorldGenConfiguration.FromEmbeddedPath("Terraria.GameContent.WorldBuilding.Configuration.json"), new GenerationProgress(), controller);
                controller.OnPassesLoaded += c => Require(!(bool)Get(owner, "IsGenerationAvailable"), "SetGenerator precedes publication; no premature entry.");
                Exception passFailure = null;
                generator.Append(new PassLegacy("Reset", (progress, config) =>
                {
                    try
                    {
                    Require((bool)Get(owner, "IsGenerationAvailable"), "Native GenerateWorld publication establishes exact ready identity.");
                    Require((bool)Call(owner, "CanShow", page), "Current native load page and controller are eligible.");
                    Main.gameMenu = false; Require(!(bool)Call(owner, "CanShow", page), "Gameplay excludes menu preview."); Main.gameMenu = true;
                    Main.MenuUI.SetState(new UIState()); Require(!(bool)Call(owner, "CanShow", page), "Other menu states exclude preview."); Main.MenuUI.SetState(page);
                    page.Update(new GameTime());
                    int count = page.Children.Count(c => c.GetType().Name == "WorldGenPreviewBanner"); Require(count == 1, "One non-interactive banner attaches to actual page.");
                    object banner = page.Children.First(c => c.GetType().Name == "WorldGenPreviewBanner");
                    for (int i = 0; i < 100; i++) page.Update(new GameTime());
                    Require((int)Get(banner, "Measurements") == 0 && (int)Get(banner, "Layouts") == 0 && page.Children.Count(c => c.GetType().Name == "WorldGenPreviewBanner") == 1, "Unavailable native texture footprint avoids font/layout work and duplicate nodes.");
                    NativeQuickItemChecks.Sample(input, new[] { Keys.F5 }); Call(shell, "ProcessInput"); page.Update(new GameTime());
                    Require(opened == 1 && ReferenceEquals(observedController, controller), "Actual load Update dispatches its one native Open with the correct controller.");
                    Main.MenuUI.SetState(page); NativeQuickItemChecks.Sample(input, new[] { Keys.F5 }); Call(shell, "ProcessInput"); page.Update(new GameTime());
                    Require(opened == 1, "Held F5 across a page transition does not reopen.");
                    NativeQuickItemChecks.Sample(input, new Keys[0]); Call(shell, "ProcessInput"); page.Update(new GameTime());
                    NativeQuickItemChecks.Sample(input, new[] { Keys.F5 }); Call(shell, "ProcessInput"); page.Update(new GameTime());
                    Require(opened == 2, "A genuine release admits the next fresh F5.");
                    Main.MenuUI.SetState(page); Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(8)));
                    NativeQuickItemChecks.Sample(input, new[] { Keys.F5 }); page.Update(new GameTime()); Require(opened == 2, "Unfocused input cannot enter.");
                    Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(7)));
                    NativeQuickItemChecks.Sample(input, new[] { Keys.F5 }); page.Update(new GameTime()); Require(opened == 2, "Refocus does not synthesize a fresh press.");
                    NativeQuickItemChecks.Sample(input, new Keys[0]); page.Update(new GameTime());
                    NativeQuickItemChecks.Sample(input, new[] { Keys.F5 }); page.Update(new GameTime()); Require(opened == 3, "Focused neutral sample restores the first legal F5.");
                    Main.MenuUI.SetState(page); PlayerInput.Triggers.Current.Inventory = true; page.Update(new GameTime());
                    Require(controller.QueuedAbort && !(bool)Call(owner, "CanShow", page), "Actual native Inventory consumer queues abort and immediately retires the banner.");
                    PlayerInput.Triggers.Current.Inventory = false; controller.QueuedAbort = false;
                    Require(!DebugOptions.enableDebugCommands, "Entry, lifecycle and layout do not enable global debug commands.");
                    TextChecks(host);
                    Main.MenuUI.SetState(new UIState());
                    }
                    catch (Exception e) { passFailure = e; controller.QueuedAbort = true; Main.MenuUI.SetState(new UIState()); }
                }));
                bool completed = generator.GenerateWorld();
                // RunPass intentionally catches native errors. Test assertions
                // must escape that boundary through their original exception.
                if (passFailure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(passFailure).Throw();
                Require(completed, "Original bounded generation completes normally.");
                Require(!(bool)Get(owner, "IsGenerationAvailable"), "Native normal finalization retires the generation token.");
                WorldGen.generatingWorld = WorldGen.isGeneratingOrLoadingWorld = false; Main.gameMenu = false;
                NativeQuickItemChecks.Sample(input, new[] { Keys.F5 }); Call(shell, "ProcessInput");
                Require(!(bool)Get(Get(shell, "State"), "Visible"), "Held generation F5 does not open the world shell across the boundary.");
                NativeQuickItemChecks.Sample(input, new Keys[0]); Call(shell, "ProcessInput");
                NativeQuickItemChecks.Sample(input, new[] { Keys.F5 }); Call(shell, "ProcessInput");
                Require((bool)Get(Get(shell, "State"), "Visible"), "The next real release/press opens the ordinary world shell.");
                Main.gameMenu = true; Call(shell, "ProcessInput"); WorldGen.generatingWorld = WorldGen.isGeneratingOrLoadingWorld = true;
                WorldGenerator.CurrentController = controller; WorldGenerator.CurrentGenerationProgress = new GenerationProgress();
                Require(!(bool)Get(owner, "IsGenerationAvailable"), "Old static leftovers cannot revive after normal retirement.");
                var failed = new WorldGenerator.Controller(); failed.OnPassesLoaded += c => { throw new InvalidOperationException("test-generation-initialization-failure"); };
                try { new WorldGenerator(18, WorldGenConfiguration.FromEmbeddedPath("Terraria.GameContent.WorldBuilding.Configuration.json"), new GenerationProgress(), failed).GenerateWorld(); throw new Exception("Expected original exception."); }
                catch (InvalidOperationException e) { Require(e.Message == "test-generation-initialization-failure", "Original generation exception is preserved."); }
                Require(!(bool)Get(owner, "IsGenerationAvailable"), "Exceptional generation retires its token despite stale native statics.");
                // The next native call is a distinct identity even before it has
                // published. Cancelled calls take their own normal native tail.
                WorldGen.Manifest = new WorldManifest(); var cancelled = new WorldGenerator.Controller { QueuedAbort = true };
                Require(!new WorldGenerator(19, WorldGenConfiguration.FromEmbeddedPath("Terraria.GameContent.WorldBuilding.Configuration.json"), new GenerationProgress(), cancelled).GenerateWorld(), "Original cancellation result is retained.");
                Require(!(bool)Get(owner, "IsGenerationAvailable") && !DebugOptions.enableDebugCommands, "Repeated generation/cancel cleanup releases identity without global flag writes.");
                Console.WriteLine("WORLDGEN CPU PASS: exact Harmony installation, original generator publication/normal/cancel/exception, actual load-page entry consumer, input/focus edges, bounded banner cache, text semantics. Native preview construction/render and physical input NOT_EVALUATED.");
            }
            finally
            {
                Main.MenuUI.SetState(new UIState()); WorldGen.generatingWorld = WorldGen.isGeneratingOrLoadingWorld = false;
                WorldGenerator.CurrentController = null; WorldGenerator.CurrentGenerationProgress = null;
                DebugOptions.enableDebugCommands = oldDebug; observer.UnpatchAll(observer.Id); ((IDisposable)hooks).Dispose(); ((IDisposable)hooks).Dispose();
            }
        }
        private static void TextChecks(Assembly host)
        {
            var text = host.GetType("JueMingR.TerrariaHost.WorldGenPreview.WorldGenPreviewText", true);
            Func<string, string> step = s => (string)text.GetMethod("Step", Flags).Invoke(null, new object[] { s });
            Func<string, string> display = s => (string)text.GetMethod("Display", Flags).Invoke(null, new object[] { s });
            Require(step("Terrain") != "Terrain" && step("Unknown Seed Terrain 123") == "Unknown Seed Terrain 123", "Known step is translated while unknown/user parameters remain intact.");
            Require(display("Paused after Terrain").Contains(step("Terrain")), "Dynamic pause display uses the step display without changing original key.");
            var pass = new PassLegacy("Terrain", (p, c) => { }); step(pass.Name); Require(pass.Name == "Terrain", "Display translation never mutates native GenPass.Name.");
            Require(display("User Terrain seed Reset 123") == "User Terrain seed Reset 123", "Ordinary user content is not globally replaced.");
        }
    }
}
