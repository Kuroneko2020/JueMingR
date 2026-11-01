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
        private static bool QuickloadSink(DebugMessage __0, ref bool __result)
        { Require(__0.CommandName == "quickload-regen", "Only the native menu quickload command reaches the persistence sink."); quickloadCalls++; __result = true; return false; }
        private static bool VisibilitySink() { return false; }
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
                Terraria.Initializers.UILinksInitializer.Load();
                var host = context.GetType().Assembly; object input = Get(context, "Input");
                var hooks = Activator.CreateInstance(host.GetType("JueMingR.TerrariaHost.WorldGenPreview.WorldGenPreviewHooks"), Flags, null,
                    new object[] { input, (Action<Exception>)(e => Console.WriteLine("PREVIEW FAILURE " + e)) }, null);
                var sinks = new Harmony("JueMingR.Tests.WorldGenPreview.VisualOutlets");
                try
                {
                    Call(hooks, "Install", typeof(Main).Assembly); Require(GetOptional(hooks, "Failure") == null, "Actual original UI hooks install.");
                    sinks.Patch(typeof(ToolkitDebugCommands).GetMethod("QuickLoadRegenCommand"), prefix: new HarmonyMethod(typeof(NativeWorldGenPreviewVisualChecks).GetMethod(nameof(QuickloadSink), Flags)));
                    sinks.Patch(typeof(WorldGenerator).GetMethod("SetDebugWorldGenUIVisibility", Flags), prefix: new HarmonyMethod(typeof(NativeWorldGenPreviewVisualChecks).GetMethod(nameof(VisibilitySink), Flags)));
                    Main.MenuUI = new UserInterface(); Main.gameMenu = true; Main.netMode = 0; DebugOptions.enableDebugCommands = false;
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
                            NativeQuickItemChecks.Sample(input, new[] { Keys.F5 }); load.Update(new GameTime());
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
                            Require(quickloadCalls == 0, "Opening/painting the preview does not save quickload settings.");
                            var save = tooltips.First(t => Title(t).StartsWith("保存快速加载设置：", StringComparison.Ordinal)).Parent;
                            save.LeftClick(new UIMouseEvent(save, Vector2.Zero)); Require(quickloadCalls == 1, "Only the actual native save-button action reaches its original command sink.");
                            typeof(WorldGenerator.Controller).GetMethod("ReportException", Flags).Invoke(controller, new object[] { "Exception in Pass: Terrain", null, true });
                            Require(controller.Paused, "Actual owned Controller.ReportException keeps its native pause behavior."); controller.Paused = false;
                            var unrelated = new WorldGenerator.Controller(); typeof(WorldGenerator.Controller).GetMethod("ReportException", Flags).Invoke(unrelated, new object[] { "unrelated", null, true });
                            Require(!unrelated.Paused && !DebugOptions.enableDebugCommands, "Unowned exceptions and global debug behavior stay outside the local gate.");
                            var cancel = tooltips.First(t => Title(t) == "取消生成").Parent;
                            cancel.LeftClick(new UIMouseEvent(cancel, Vector2.Zero)); Require(controller.QueuedAbort, "Original cancel button queues generation abort."); controller.QueuedAbort = false;
                            // Automatic snapshot I/O is not the assertion here;
                            // keep this finite scene's post-pass tail disposable.
                            controller.SnapshotFrequency = WorldGenerator.SnapshotFrequency.None;
                            Main.MenuUI.SetState(new UIState());
                        }
                        catch (Exception e) { failure = e; controller.QueuedAbort = true; controller.Paused = false; Main.MenuUI.SetState(new UIState()); }
                    }));
                    generator.Append(new PassLegacy("Jungle", (p, c) => { }));
                    generator.GenerateWorld();
                    if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
                    Console.WriteLine("WORLDGEN VISUAL PASS: real XNB/font/device, original load and complete preview construction, native child rendering, original control/delegate/exception/button consumers. Map/chat background render, OS physical input and owner startup NOT_EVALUATED; quickload persistence terminal intercepted.");
                }
                finally
                {
                    Main.MenuUI.SetState(new UIState()); WorldGen.generatingWorld = WorldGen.isGeneratingOrLoadingWorld = false;
                    WorldGenerator.CurrentController = null; WorldGenerator.CurrentGenerationProgress = null;
                    sinks.UnpatchAll(sinks.Id); ((IDisposable)hooks).Dispose();
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
