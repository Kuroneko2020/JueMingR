using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Generation;
using Terraria.GameContent.UI.States;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.Testing;
using Terraria.Testing.ChatCommands;
using Terraria.UI;
using Terraria.WorldBuilding;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeWorldGenPreviewVisualChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static int quickloadCalls;
        private static bool failConstruction;
        private static bool retireConstruction;
        private static bool retireActivation;
        private static object previewOwner;
        private sealed class SyncCase
        {
            internal string Name;
            internal Harmony Seams;
            internal bool Armed, Retired, ScopeAtAttempt, SignalTokenMatched;
            internal Type HooksType;
            internal string OpenException;
            internal int IndexEntered, IndexReturned, ColorsEntered, TargetEntered, ReadsAfterRetirement;
        }
        private static SyncCase syncCase;
        // Install observation seams after production has had the opportunity to
        // patch the tiny Controller getter, but before localization compiles its
        // native constructor callers. No production action is supplied here.
        private static void BeforeLocalization()
        {
            if (syncCase == null) return;
            var pass = typeof(UIWorldGenDebug).GetNestedType("GenPassElement", Flags);
            var observer = syncCase.Seams;
            observer.Patch(typeof(UIWorldGenDebug).GetProperty("Controller", Flags).GetGetMethod(true),
                prefix: new HarmonyMethod(typeof(NativeWorldGenPreviewVisualChecks).GetMethod(nameof(SyncControllerRead), Flags)) { priority = Priority.First });
            observer.Patch(pass.GetProperty("Index", Flags).GetGetMethod(true),
                prefix: new HarmonyMethod(typeof(NativeWorldGenPreviewVisualChecks).GetMethod(nameof(SyncIndexBegin), Flags)),
                postfix: new HarmonyMethod(typeof(NativeWorldGenPreviewVisualChecks).GetMethod(nameof(SyncIndexEnd), Flags)));
            observer.Patch(pass.GetMethod("SetColorsToNotHovered", Flags), prefix: new HarmonyMethod(typeof(NativeWorldGenPreviewVisualChecks).GetMethod(nameof(SyncColors), Flags)));
            observer.Patch(typeof(UIWorldGenDebug).GetProperty("CurrentTargetPass", Flags).GetGetMethod(true), prefix: new HarmonyMethod(typeof(NativeWorldGenPreviewVisualChecks).GetMethod(nameof(SyncTarget), Flags)));
        }
        private static void SyncIndexBegin()
        { if (syncCase.Armed) { syncCase.IndexEntered++; syncCase.ScopeAtAttempt = syncCase.HooksType.GetField("activeEntry", Flags).GetValue(null) != null; Require(syncCase.ScopeAtAttempt, "Actual child Index executes inside the production Open scope."); if (syncCase.Name == "A") RetireSyncCase(); } }
        private static void SyncIndexEnd() { if (syncCase.Armed) syncCase.IndexReturned++; }
        private static void SyncColors() { if (syncCase.Armed) syncCase.ColorsEntered++; }
        private static void SyncTarget()
        {
            if (!syncCase.Armed) return;
            syncCase.TargetEntered++;
            if (syncCase.Name == "B")
            {
                Require(syncCase.IndexReturned > 0 && syncCase.ColorsEntered > 0, "B reaches CurrentTargetPass after actual Index return and color helper entry.");
                RetireSyncCase();
            }
        }
        private static void SyncControllerRead() { if (syncCase.Armed && syncCase.Retired) syncCase.ReadsAfterRetirement++; }
        private static Exception SyncOpenException(Exception __exception)
        {
            if (syncCase.Armed && __exception != null)
            {
                syncCase.OpenException = __exception.GetType().Name;
                if (syncCase.OpenException == "GenerationRetiredException")
                {
                    object scope = syncCase.HooksType.GetField("activeEntry", Flags).GetValue(null);
                    syncCase.SignalTokenMatched = scope != null && ReferenceEquals(Get(scope, "Generation"), __exception.GetType().GetField("Generation", Flags).GetValue(__exception));
                }
            }
            return __exception;
        }
        private static void RetireSyncCase()
        {
            if (syncCase.Retired) return;
            syncCase.Retired = true;
            typeof(WorldGenerator).GetMethod("SetDebugWorldGenUIVisibility", Flags).Invoke(null, new object[] { false });
            WorldGenerator.CurrentGenerationProgress = null;
            WorldGenerator.CurrentController = null;
        }
        private static void SyncConstructionCases(object context, string output)
        {
            var failures = new List<string>();
            foreach (string name in new[] { "A", "B" })
            {
                string caseOutput = Path.Combine(output, "sync-" + name); Directory.CreateDirectory(caseOutput);
                var host = context.GetType().Assembly; object input = Get(context, "Input");
                var hooks = Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.WorldGenPreview.WorldGenPreviewHooks"), Flags, null,
                    new object[] { input, (Action<Exception>)(e => Console.WriteLine("SYNC " + name + " PREVIEW FAILURE " + e)) }, null);
                var seams = new Harmony("JueMingR.Tests.WorldGenPreview.Sync." + name);
                syncCase = new SyncCase { Name = name, Seams = seams, HooksType = host.GetType("JueMingR.TerrariaHost.WorldGenPreview.WorldGenPreviewHooks") };
                try
                {
                    seams.Patch(host.GetType("JueMingR.TerrariaHost.WorldGenPreview.WorldGenPreviewLocalization").GetMethod("Install", Flags), prefix: new HarmonyMethod(typeof(NativeWorldGenPreviewVisualChecks).GetMethod(nameof(BeforeLocalization), Flags)));
                    seams.Patch(typeof(WorldGenerator).GetMethod("SetDebugWorldGenUIVisibility", Flags), prefix: new HarmonyMethod(typeof(NativeWorldGenPreviewVisualChecks).GetMethod(nameof(VisibilitySink), Flags)));
                    Call(hooks, "Install", typeof(Main).Assembly);
                    Require(GetOptional(hooks, "Failure") == null, "Sync " + name + " production hooks install before original construction.");
                    seams.Patch(typeof(UIWorldGenDebug).GetMethod("Open"), finalizer: new HarmonyMethod(typeof(NativeWorldGenPreviewVisualChecks).GetMethod(nameof(SyncOpenException), Flags)) { priority = Priority.First });
                    object owner = Get(hooks, "Owner");
                    Main.MenuUI = new UserInterface(); Main.gameMenu = true; Main.netMode = 0; DebugOptions.enableDebugCommands = false;
                    Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(7))); Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(7)));
                    using (var frames = new NativeWorldGenPreviewFrames(context, hooks, caseOutput, initializeNavigation: name == "A"))
                    {
                        WorldGen.generatingWorld = WorldGen.isGeneratingOrLoadingWorld = true; WorldGen.Manifest = new WorldManifest();
                        var controller = new WorldGenerator.Controller(); var progress = new GenerationProgress();
                        var generator = new WorldGenerator(41, WorldGenConfiguration.FromEmbeddedPath("Terraria.GameContent.WorldBuilding.Configuration.json"), progress, controller);
                        Exception assertion = null;
                        generator.Append(new PassLegacy("Terrain", (p, c) =>
                        {
                            try
                            {
                                var load = new UIWorldLoad(); Main.MenuUI.SetState(load);
                                frames.Frame(); frames.Frame(); syncCase.Armed = true; frames.Frame(Keys.F5); syncCase.Armed = false;
                                string failure = (string)GetOptional(hooks, "Failure");
                                string observation = "SYNC " + name + " scope=ORIGINAL_CONSTRUCTION activeEntry=" + syncCase.ScopeAtAttempt + " index=" + syncCase.IndexEntered + "/" + syncCase.IndexReturned + " colors=" + syncCase.ColorsEntered + " target=" + syncCase.TargetEntered + " retiredReads=" + syncCase.ReadsAfterRetirement + " ready=" + Get(owner, "Ready") + " openException=" + (syncCase.OpenException ?? "none") + " signalSameToken=" + syncCase.SignalTokenMatched + " failure=" + (failure ?? "none");
                                Console.WriteLine(observation); File.WriteAllText(Path.Combine(caseOutput, "observation.txt"), observation);
                                Require(syncCase.Retired && syncCase.ReadsAfterRetirement > 0, "Exact synchronous helper reaches the real Controller getter after retirement.");
                                Require((bool)Get(owner, "Ready") && failure == null && ReferenceEquals(Main.MenuUI.CurrentState, load), "Sync " + name + " retirement abandons only this Open, retaining load page and feature.");
                                Require(syncCase.OpenException == "GenerationRetiredException" && syncCase.SignalTokenMatched, "The actual native helper exception reaches Open's finalizer with the same generation token.");
                                Require(Harmony.GetPatchInfo(typeof(UIWorldGenDebug).GetMethod("Open")).Owners.Contains("JueMingR.WorldGenPreview"), "Normal synchronous retirement retains production hooks.");
                                Require(host.GetType("JueMingR.TerrariaHost.WorldGenPreview.WorldGenPreviewHooks").GetField("activeEntry", Flags).GetValue(null) == null, "Open finalizer restores its parent construction scope.");
                                Require(typeof(UIWorldGenDebug).GetProperty("Controller", Flags).GetValue(null, null) == null, "Outside Open, the original getter returns the actual null static.");
                            }
                            catch (Exception e) { assertion = e; }
                            finally { syncCase.Armed = false; controller.Paused = false; controller.SnapshotFrequency = WorldGenerator.SnapshotFrequency.None; Main.MenuUI.SetState(new UIState()); }
                        }));
                        generator.GenerateWorld();
                        if (assertion != null) { failures.Add(name + ": " + assertion.Message); continue; }
                        // Recovery is an actual new GenerateWorld invocation,
                        // not restoration of the retired token or its statics.
                        WorldGen.Manifest = new WorldManifest(); var recovered = new WorldGenerator.Controller();
                        var next = new WorldGenerator(42, WorldGenConfiguration.FromEmbeddedPath("Terraria.GameContent.WorldBuilding.Configuration.json"), new GenerationProgress(), recovered);
                        next.Append(new PassLegacy("Terrain", (p, c) =>
                        {
                            try
                            {
                                Main.MenuUI.SetState(new UIWorldLoad()); frames.Frame(); frames.Frame(); frames.Frame(Keys.F5);
                                Require(Main.MenuUI.CurrentState is UIWorldGenDebug && (bool)Call(owner, "OwnsController", recovered), "Sync " + name + " next native generation opens through production F5 with its new Controller.");
                                Require(ReferenceEquals(typeof(UIWorldGenDebug).GetProperty("Controller", Flags).GetValue(null, null), recovered), "Outside the completed Open, the original getter follows the current native Controller.");
                                Console.WriteLine("SYNC " + name + " RECOVERY PASS new-controller=True scope-original=True");
                            }
                            catch (Exception e) { assertion = e; }
                            finally { recovered.Paused = false; recovered.SnapshotFrequency = WorldGenerator.SnapshotFrequency.None; Main.MenuUI.SetState(new UIState()); }
                        }));
                        next.GenerateWorld(); if (assertion != null) failures.Add(name + ": " + assertion.Message);
                    }
                }
                finally
                {
                    ((IDisposable)hooks).Dispose(); seams.UnpatchAll(seams.Id); syncCase = null;
                    WorldGenerator.CurrentGenerationProgress = null; WorldGenerator.CurrentController = null;
                    WorldGen.generatingWorld = WorldGen.isGeneratingOrLoadingWorld = false; Main.MenuUI?.SetState(new UIState());
                }
            }
            Require(failures.Count == 0, "Synchronous construction retirement cases: " + string.Join(" | ", failures));
        }
        private static void ConstructionProbe()
        {
            if (retireConstruction)
            { retireConstruction = false; WorldGenerator.CurrentGenerationProgress = null; WorldGenerator.CurrentController = null; return; }
            if (!failConstruction) return;
            Require((bool)Get(previewOwner, "HasPreviewContext") && !(bool)Get(previewOwner, "CanSaveQuickload"), "Native construction has display scope before operation admission.");
            throw new InvalidOperationException("test-native-preview-construction-failure");
        }
        private static bool QuickloadSink(DebugMessage __0, ref bool __result)
        { Require(__0.CommandName == "quickload-regen", "Only the native menu quickload command reaches the persistence sink."); quickloadCalls++; __result = true; return false; }
        private static bool VisibilitySink() { return false; }
        private static void ActivationTail()
        {
            if (!retireActivation) return;
            retireActivation = false;
            // Execute the real production retirement prefix at the native tail
            // seam. Only the subsequent main-thread visibility outlet is sunk.
            typeof(WorldGenerator).GetMethod("SetDebugWorldGenUIVisibility", Flags).Invoke(null, new object[] { false });
            WorldGenerator.CurrentGenerationProgress = null; WorldGenerator.CurrentController = null;
        }
        internal static void Run(object context, string content, string output)
        {
            using (var graphics = new ProbeGraphics(content))
            {
                // The existing isolated XNB consumer creates a real device and
                // resource repository; no owner game constructor/Run is invoked.
                typeof(ProbeGraphics).GetMethod("EnsureNativeAssets", Flags).Invoke(graphics, null);
                LanguageManager.Instance.SetLanguage("zh-Hans");
                FontAssets.DeathText = Main.Assets.Request<DynamicSpriteFont>("Fonts/Death_Text", AssetRequestMode.ImmediateLoad);
                FontAssets.MouseText = Main.Assets.Request<DynamicSpriteFont>("Fonts/Mouse_Text", AssetRequestMode.ImmediateLoad);
                Main.gameTips = new Terraria.GameContent.UI.GameTipsDisplay(new Terraria.GameContent.UI.GameTipsProvider());
                // Main's normal initializer creates the navigator pages before
                // UIWorldLoad.DrawSelf consumes its 3000 squiggle point.
                // The shared production-frame fixture initializes navigation once.
                SyncConstructionCases(context, output);
                var host = context.GetType().Assembly; object input = Get(context, "Input");
                var hooks = Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.WorldGenPreview.WorldGenPreviewHooks"), Flags, null,
                    new object[] { input, (Action<Exception>)(e => Console.WriteLine("PREVIEW FAILURE " + e)) }, null);
                var sinks = new Harmony("JueMingR.Tests.WorldGenPreview.VisualOutlets");
                try
                {
                    Call(hooks, "Install", typeof(Main).Assembly); Require(GetOptional(hooks, "Failure") == null, "Actual original UI hooks install.");
                    previewOwner = Get(hooks, "Owner");
                    Require(Call(previewOwner, "BeginNativeEntry") == null, "Unpublished generation cannot be adopted by native Open.");
                    sinks.Patch(typeof(UIWorldGenDebug).GetConstructor(Type.EmptyTypes), prefix: new HarmonyMethod(typeof(NativeWorldGenPreviewVisualChecks).GetMethod(nameof(ConstructionProbe), Flags)));
                    sinks.Patch(typeof(UIWorldGenDebug).GetMethod("OnActivate"), postfix: new HarmonyMethod(typeof(NativeWorldGenPreviewVisualChecks).GetMethod(nameof(ActivationTail), Flags)));
                    sinks.Patch(typeof(ToolkitDebugCommands).GetMethod("QuickLoadRegenCommand"), prefix: new HarmonyMethod(typeof(NativeWorldGenPreviewVisualChecks).GetMethod(nameof(QuickloadSink), Flags)));
                    sinks.Patch(typeof(WorldGenerator).GetMethod("SetDebugWorldGenUIVisibility", Flags), prefix: new HarmonyMethod(typeof(NativeWorldGenPreviewVisualChecks).GetMethod(nameof(VisibilitySink), Flags)));
                    Main.MenuUI = new UserInterface(); Main.gameMenu = true; Main.netMode = 0; DebugOptions.enableDebugCommands = false;
                    using (var frames = new NativeWorldGenPreviewFrames(context, hooks, output, initializeNavigation: false))
                    {
                    Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(7))); Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(7)));
                    WorldGen.generatingWorld = WorldGen.isGeneratingOrLoadingWorld = true; WorldGen.Manifest = new WorldManifest();
                    var controller = new WorldGenerator.Controller(); var progress = new GenerationProgress();
                    var generator = new WorldGenerator(29, WorldGenConfiguration.FromEmbeddedPath("Terraria.GameContent.WorldBuilding.Configuration.json"), progress, controller);
                    Exception failure = null;
                    generator.Append(new PassLegacy("Terrain", (p, c) =>
                    {
                        try
                        {
                            var load = new UIWorldLoad(); Main.MenuUI.SetState(load);
                            progress.Message = Lang.gen[0].Value;
                            NativeQuickItemChecks.Sample(input, new Keys[0]); NativeQuickItemChecks.Sample(input, new Keys[0]); load.Update(new GameTime());
                            object banner = load.Children.First(e => e.GetType().Name == "WorldGenPreviewBanner");
                            for (int frame = 0; frame < 100; frame++) load.Update(new GameTime());
                            Require((int)Get(banner, "Measurements") == 1 && (int)Get(banner, "Layouts") == 1, "100 actual resource-ready frames reuse font and geometry.");
                            Require(!((Rectangle)Get(banner, "Bounds")).Intersects((Rectangle)Get(banner, "ProgressBounds")), "The banner clears all native outer/lower texture footprints.");
                            graphics.Image(Path.Combine(output, "native-load-960x640.png"), () => load.Draw(Main.spriteBatch), Main.UIScaleMatrix);
                            Viewport(800, 600, 1); load.Update(new GameTime());
                            graphics.Image(Path.Combine(output, "native-load-800x600-scale100.png"), () => load.Draw(Main.spriteBatch), Main.UIScaleMatrix, 800, 600);
                            Require(!((Rectangle)Get(banner, "Bounds")).Intersects((Rectangle)Get(banner, "ProgressBounds")), "Compact layout clears the native lower ornament.");
                            Viewport(1280, 720, 1.2f); load.Update(new GameTime());
                            graphics.Image(Path.Combine(output, "native-load-1280x720-scale120.png"), () => load.Draw(Main.spriteBatch), Main.UIScaleMatrix, 1280, 720);
                            Require(!((Rectangle)Get(banner, "Bounds")).Intersects((Rectangle)Get(banner, "ProgressBounds")), "Scaled native logical layout clears all ornament textures.");
                            Require((int)Get(banner, "Measurements") == 1 && (int)Get(banner, "Layouts") == 3, "Viewport/scale changes relayout without remeasuring stable font.");
                            Viewport(960, 640, 1);
                            Main.gameMenu = false;
                            Require(Call(previewOwner, "BeginNativeEntry") == null && !(bool)Get(previewOwner, "HasPreviewContext"), "Native gameplay preview stays outside this menu-generation scope.");
                            Main.gameMenu = true;
                            retireConstruction = true;
                            UIWorldGenDebug.Open();
                            Require(ReferenceEquals(Main.MenuUI.CurrentState, load) && (bool)Get(previewOwner, "Ready") && GetOptional(hooks, "Failure") == null && !(bool)Get(previewOwner, "HasPreviewContext"), "Exact native ctor retirement cancels entry without disabling preview.");
                            WorldGenerator.CurrentController = controller; WorldGenerator.CurrentGenerationProgress = progress;
                            failConstruction = true;
                            try { UIWorldGenDebug.Open(); throw new Exception("Expected native construction failure."); }
                            catch (InvalidOperationException e) { Require(e.Message == "test-native-preview-construction-failure", "Native auto-open preserves the original construction exception."); }
                            finally { failConstruction = false; }
                            Require(!(bool)Get(previewOwner, "HasPreviewContext") && !(bool)Get(previewOwner, "CanSaveQuickload") && !(bool)Call(previewOwner, "OwnsController", controller), "Failed native construction leaves neither entering nor operation ownership.");
                            // This is the same original Open called by native
                            // quickload regeneration, without a preceding F5.
                            UIWorldGenDebug.Open();
                            var automatic = Main.MenuUI.CurrentState as UIWorldGenDebug;
                            Require(automatic != null && (bool)Call(previewOwner, "OwnsController", controller), "Native automatic Open adopts only its published generation after activation.");
                            var automaticTips = Descendants(automatic).Where(e => e.GetType().Name == "TooltipElement").ToArray();
                            Require(automaticTips.Any(t => Title(t) == "操作说明") && ((string)Call(automatic, "GetControls")).Contains("暂停/继续"), "Native automatic preview receives Chinese controls and help without F5.");
                            Require(quickloadCalls == 0 && !DebugOptions.enableDebugCommands, "Automatic Open does not save or enable global debug.");
                            var automaticSave = automaticTips.First(t => Title(t).StartsWith("保存快速加载设置：", StringComparison.Ordinal)).Parent;
                            automaticSave.LeftClick(new UIMouseEvent(automaticSave, Vector2.Zero));
                            Require(quickloadCalls == 1, "Actual native automatic-preview save action reaches the original command terminal.");
                            typeof(WorldGenerator.Controller).GetMethod("ReportException", Flags).Invoke(controller, new object[] { "Exception in Pass: Terrain", null, true });
                            Require(controller.Paused, "Automatic preview retains the same-controller native exception behavior."); controller.Paused = false;
                            UIWorldGenDebug.Close(); Main.MenuUI.SetState(load);
                            frames.Frame(); frames.Frame(); frames.Frame(Keys.F5);
                            var ui = Main.MenuUI.CurrentState as UIWorldGenDebug;
                            Require(ui != null && ReferenceEquals(WorldGenerator.CurrentController, controller), "Actual native F5 Open constructs and activates original preview with the right controller.");
                            ui.Update(new GameTime());
                            Require(controller.SnapshotFrequency == WorldGenerator.SnapshotFrequency.Automatic && Main.menuChat && !DebugOptions.enableDebugCommands, "Native activation enables its snapshot mode/chat without global debug.");
                            Require(Descendants(ui).All(e => e.GetType().Name != "WorldGenPreviewBanner"), "The load-page banner does not overlay the preview.");
                            var tooltips = Descendants(ui).Where(e => e.GetType().Name == "TooltipElement").ToArray();
                            var titles = tooltips.Select(Title).ToArray();
                            foreach (string title in new[] { "删除全部生成快照", "重置", "后退一步", "暂停", "前进一步", "显示/隐藏地图", "显示/隐藏聊天", "操作说明", "取消生成" })
                                Require(titles.Contains(title), "Actual original control title is Chinese and present: " + title);
                            Require(titles.Any(t => t != null && t.StartsWith("保存快速加载设置：/quickload-regen", StringComparison.Ordinal)), "Native quickload command operand is retained in its Chinese title.");
                            controller.Paused = true; Require(tooltips.Any(t => Title(t) == "继续"), "The real dynamic pause delegate changes to resume."); controller.Paused = false;
                            string controls = (string)Call(ui, "GetControls");
                            Require(controls.Contains("暂停/继续") && controls.Contains("R") && controls.Contains("H") && controls.Contains("M") && controls.Contains("C"), "Actual native help consumer preserves all keyboard controls.");
                            File.WriteAllLines(Path.Combine(output, "native-controls.txt"), tooltips.Select(t => Title(t) + "\n" + Description(t)).Concat(new[] { controls }));
                            graphics.Image(Path.Combine(output, "native-preview-controls-960x640.png"), () => DrawChildren(ui), Main.UIScaleMatrix);
                            var pause = tooltips.First(t => Title(t) == "暂停").Parent;
                            pause.LeftClick(new UIMouseEvent(pause, Vector2.Zero)); Require(controller.Paused, "Original pause button action remains connected.");
                            pause.LeftClick(new UIMouseEvent(pause, Vector2.Zero)); Require(!controller.Paused, "Original resume action remains connected.");
                            Require(quickloadCalls == 1, "F5 opening/painting adds no quickload save to the earlier explicit automatic-preview action.");
                            var save = tooltips.First(t => Title(t).StartsWith("保存快速加载设置：", StringComparison.Ordinal)).Parent;
                            save.LeftClick(new UIMouseEvent(save, Vector2.Zero)); Require(quickloadCalls == 2, "Only the actual native save-button action reaches its original command sink.");
                            typeof(WorldGenerator.Controller).GetMethod("ReportException", Flags).Invoke(controller, new object[] { "Exception in Pass: Terrain", null, true });
                            Require(controller.Paused, "Actual owned Controller.ReportException keeps its native pause behavior."); controller.Paused = false;
                            var unrelated = new WorldGenerator.Controller(); typeof(WorldGenerator.Controller).GetMethod("ReportException", Flags).Invoke(unrelated, new object[] { "unrelated", null, true });
                            Require(!unrelated.Paused && !DebugOptions.enableDebugCommands, "Unowned exceptions and global debug behavior stay outside the local gate.");
                            var cancel = tooltips.First(t => Title(t) == "取消生成").Parent;
                            cancel.LeftClick(new UIMouseEvent(cancel, Vector2.Zero)); Require(controller.QueuedAbort, "Original cancel button queues generation abort."); controller.QueuedAbort = false;
                            // Automatic snapshot I/O is not the assertion here;
                            // keep this finite scene's post-pass tail disposable.
                            controller.SnapshotFrequency = WorldGenerator.SnapshotFrequency.None;
                            UIWorldGenDebug.Close(); Main.MenuUI.SetState(load);
                            frames.Frame(); frames.Frame(); retireActivation = true; frames.Frame(Keys.F5);
                            Require(ReferenceEquals(Main.MenuUI.CurrentState, load) && (bool)Get(previewOwner, "Ready") && GetOptional(hooks, "Failure") == null, "Successful activation followed by exact native-tail retirement restores only its load page without failing owner.");
                            var afterRetirement = new WorldGenerator.Controller();
                            WorldGen.Manifest = new WorldManifest();
                            var retry = new WorldGenerator(30, WorldGenConfiguration.FromEmbeddedPath("Terraria.GameContent.WorldBuilding.Configuration.json"), new GenerationProgress(), afterRetirement);
                            retry.Append(new PassLegacy("Reset", (rp, rc) =>
                            { frames.Frame(); frames.Frame(); failConstruction = true; frames.Frame(Keys.F5); failConstruction = false; }));
                            retry.GenerateWorld();
                            Require(!(bool)Get(previewOwner, "Ready") && ((string)GetOptional(hooks, "Failure")).Contains("test-native-preview-construction-failure"), "Real constructor failure through production F5 remains a local feature failure.");
                            Main.MenuUI.SetState(new UIState());
                        }
                        catch (Exception e) { failure = e; controller.QueuedAbort = true; controller.Paused = false; Main.MenuUI.SetState(new UIState()); }
                    }));
                    generator.Append(new PassLegacy("Jungle", (p, c) => { }));
                    generator.GenerateWorld();
                    if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
                    WorldGenerator.CurrentController = controller; WorldGenerator.CurrentGenerationProgress = progress;
                    Require(Call(previewOwner, "BeginNativeEntry") == null && !(bool)Get(previewOwner, "HasPreviewContext") && !(bool)Call(previewOwner, "OwnsController", controller), "Retired token cannot be adopted even when native statics retain the same controller/progress.");
                    Console.WriteLine("WORLDGEN VISUAL PASS: real XNB/font/device, original load and complete preview construction, native child rendering, original control/delegate/exception/button consumers. Map/chat background render, OS physical input and owner startup NOT_EVALUATED; quickload persistence terminal intercepted.");
                    }
                }
                finally
                {
                    Main.MenuUI.SetState(new UIState()); WorldGen.generatingWorld = WorldGen.isGeneratingOrLoadingWorld = false;
                    WorldGenerator.CurrentController = null; WorldGenerator.CurrentGenerationProgress = null;
                    sinks.UnpatchAll(sinks.Id); ((IDisposable)hooks).Dispose();
                    previewOwner = null; failConstruction = false;
                }
            }
        }
        private static IEnumerable<UIElement> Descendants(UIElement root)
        { foreach (var child in root.Children) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }
        private static string Title(UIElement tooltip) { return ((Func<string>)Get(tooltip, "_getTitle"))(); }
        private static string Description(UIElement tooltip) { var value = GetOptional(tooltip, "_getDescription") as Func<string>; return value == null ? null : value(); }
        private static void DrawChildren(UIElement state)
        { state.GetType().GetMethod("DrawChildren", Flags).Invoke(state, new object[] { Main.spriteBatch }); }
        private static void Viewport(int width, int height, float scale)
        {
            typeof(PlayerInput).GetField("_originalScreenWidth", Flags).SetValue(null, width);
            typeof(PlayerInput).GetField("_originalScreenHeight", Flags).SetValue(null, height);
            Main.UIScale = scale; PlayerInput.SetZoom_UI(); Main.MenuUI.Recalculate();
        }
    }
}
