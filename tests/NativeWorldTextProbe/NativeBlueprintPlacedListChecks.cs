using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Literal geometry, the real committed-instance seam, and production CPU
    // layout are separate oracles. This fixture does not certify native Draw,
    // Windows input, multiplayer, or persistent save success.
    internal static class NativeBlueprintPlacedListChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static void Run(object context)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var state = Get(shell, "State");
            var assembly = host.GetType().Assembly;
            var type = assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintPlacedListPresentation", true);
            var one = new BlueprintMaterialInstance(1, 1, 1, "同模板", 10, 20, 1, 1, 1);
            var even = new BlueprintMaterialInstance(1, 2, 2, "同模板", 10, 20, 2, 4, 2);
            Require((string)type.GetMethod("RelativePosition", F).Invoke(null, new object[] { one, 10, 20 }) == "附近 · 距中心约 0 格", "one tile instance and player tile center coincide exactly");
            Require((string)type.GetMethod("RelativePosition", F).Invoke(null, new object[] { even, 10, 20 }) == "下方 · 距中心约 2 格", "2x4 center lies on grid seams: independent half-tile offsets round to two");
            Require((string)type.GetMethod("RelativePosition", F).Invoke(null, new object[] { one, 11, 21 }) == "左上方 · 距中心约 1 格", "world Y direction and eight-way diagonal remain independent of screen gravity");
            var oldFont = FontAssets.MouseText;
            object ui = null;
            try
            {
                FiniteCostChecks.SetCpuFont(10); var painter = Get(shell, "renderer");
                Call(painter, "RefreshResources"); Call(painter, "Prepare", state, 960f, 640f, 1f);
                var rectType = assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect", true);
                ui = Activator.CreateInstance(type, F, null, new[] { host, state }, null);
                Set(ui, "view", Activator.CreateInstance(rectType, F, null, new object[] { 0f, 0f, 522f, 300f }, null));
                Set(ui, "directory", Array.Empty<BlueprintMaterialInstance>()); Call(ui, "Build"); Call(ui, "Project", 10, 20, true);
                Require(((IEnumerable)Get(ui, "visible")).Cast<object>().Any(part => (string)Get(Get(part, "Element"), "Text") == "当前没有已放置蓝图"), "the real independent content projection has an explicit empty state");
                var many = Enumerable.Range(1, 2000).Select(index => new BlueprintMaterialInstance(1, index, index, "同名模板副本中文长名称" + index, index, 20, 2, 4, index)).ToArray();
                Set(ui, "directory", many); Call(ui, "Build"); Set(ui, "scroll", 70000f); Call(ui, "Project", 10, 20, true);
                int first = (int)Get(ui, "firstVisible"), last = (int)Get(ui, "lastVisible");
                Require(first > 10 && last - first < 10 && last >= first, "deep scroll uses the measured prefix geometry and a bounded visible slice");
                long builds = (long)Get(ui, "DirectoryBuilds"), visits = (long)Get(ui, "VisibleRowVisits");
                Call(ui, "Project", 11, 20, true);
                Require((long)Get(ui, "DirectoryBuilds") == builds && (long)Get(ui, "VisibleRowVisits") - visits < 10, "player movement recomputes only visible relative positions and never rebuilds the directory");
                var rows = ((IEnumerable)Get(ui, "rows")).Cast<object>().ToArray();
                Require(rows.Select(row => ((BlueprintMaterialInstance)Get(row, "Instance")).ProjectionGeneration).SequenceEqual(many.Select(item => item.ProjectionGeneration)), "player movement leaves the stable committed order unchanged");
                Toolbar(host, assembly);
            }
            finally { if (ui != null) ((IDisposable)ui).Dispose(); FontAssets.MouseText = oldFont; Call(Get(shell, "renderer"), "RefreshResources"); }
            Console.WriteLine("PASS Blueprint placed list CPU: explicit empty state, literal tile-center geometry, stable instance order, bounded visible rows and toolbar presence across capture/edit modes. Native Draw/OS input remain separate evidence.");
        }
        private static void Toolbar(object host, Assembly assembly)
        {
            var toolbar = Get(host, "Toolbar"); var type = assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintToolbarLayout", true);
            var layout = Activator.CreateInstance(type, true); var font = FontAssets.MouseText.Value;
            var oldFont = GetOptional(toolbar, "font"); Set(toolbar, "font", font);
            try
            {
                var measureType = type.GetMethod("EnsureProjection", F).GetParameters()[4].ParameterType;
                var measure = Delegate.CreateDelegate(measureType, toolbar, toolbar.GetType().GetMethod("Measure", F));
                Call(layout, "SetPlacedListVisible", true);
                foreach (bool capture in new[] { false, true })
                for (int mode = 0; mode <= 7; mode++)
                {
                    Require((bool)Call(layout, "EnsureProjection", 1280f, 720f, 1f, font, measure, capture, mode), "all capture/editor toolbar variants fit with committed list entry");
                    var buttons = ((IEnumerable)Get(layout, "Buttons")).Cast<object>().ToArray();
                    Require(buttons.Count(button => (string)Get(button, "Text") == "已放置列表") == 1, "committed scene has exactly one list entry in every toolbar layout");
                    int generation = (int)Get(layout, "Generation");
                    for (int i = 0; i < 120; i++) Call(layout, "EnsureProjection", 1280f, 720f, 1f, font, measure, capture, mode);
                    Require((int)Get(layout, "Generation") == generation, "stable toolbar frames retain their measured geometry");
                }
                Call(layout, "SetPlacedListVisible", false); Call(layout, "EnsureProjection", 1280f, 720f, 1f, font, measure, false, 2);
                Require(!((IEnumerable)Get(layout, "Buttons")).Cast<object>().Any(button => (string)Get(button, "Text") == "已放置列表"), "a following candidate alone does not create a committed-list toolbar entry");
            }
            finally { Set(toolbar, "font", oldFont); }
        }
        internal static void AssertPinned(object context, long first, long second)
        {
            var host = Get(context, "Blueprint"); var popup = Get(Get(context, "Shell"), "BlueprintMaterialsUi");
            var entries = (IReadOnlyList<BlueprintMaterialInstance>)Get(host, "MaterialInstances");
            var item = entries.Single(value => value.ProjectionGeneration == first);
            var rect = Get(popup, "Panel");
            Require(entries.Count == 2 && entries[0].CommitOrder < entries[1].CommitOrder && entries.All(value => value.Width == 50 && value.Height == 1), "shared directory publishes real size and stable commit order for same-template copies");
            Require((bool)Call(popup, "OpenWorldInstance", rect, item.SessionGeneration, first), "strict material opening admits the exact live instance");
            Require((long)Get(popup, "Selected") == first && (bool)Get(popup, "strictInstance"), "Open reset cannot discard the exact initial scope or flash all instances");
            Call(popup, "ReadSnapshot");
            Require((long)Get(popup, "Selected") == first, "first strict query keeps the target while pending");
            Call(popup, "Close");
            Require(!(bool)Call(popup, "OpenWorldInstance", rect, item.SessionGeneration - 1, second), "another session cannot bind a surviving similarly named copy");
        }
        internal static void RemoveAndRetire(object context, long instance)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var input = Get(context, "Input");
            var list = Get(shell, "BlueprintPlacedUi"); var popup = Get(shell, "BlueprintMaterialsUi");
            var entries = (IReadOnlyList<BlueprintMaterialInstance>)Get(host, "MaterialInstances"); var target = entries.Single(value => value.ProjectionGeneration == instance);
            bool auto = (bool)Get(host, "AutoBuildEnabled"); int stone = Main.tile[40, 70].type;
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            Call(host, "OpenPlacedList");
            Require((bool)Get(list, "Visible") && !(bool)Get(host, "ProjectionEditing") && (bool)Get(host, "AutoBuildEnabled") == auto, "list navigation is a shared read-only content view, not an editor or preference change");
            Require((bool)Call(popup, "OpenWorldInstance", Get(popup, "Panel"), target.SessionGeneration, instance), "the last surviving real instance opens in strict mode");
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            Require(!(bool)Call(host, "TryRemovePlacedInstance", target.SessionGeneration - 1, instance), "stale session removal is rejected by Host");
            Require((bool)Call(host, "TryRemovePlacedInstance", target.SessionGeneration, instance), "the exact current Host command retires only its selected live operand");
            Call(popup, "ReadSnapshot");
            Require(!(bool)Get(popup, "Visible") && (bool)Get(list, "Visible") && ((IReadOnlyList<BlueprintMaterialInstance>)Get(host, "MaterialInstances")).Count == 0,
                "strict target retirement closes details to the same empty list instead of silently showing all");
            Require(Main.tile[40, 70].active() && Main.tile[40, 70].type == stone && (bool)Get(host, "AutoBuildEnabled") == auto, "instance removal preserves real built tiles and automatic preference");
            Require(!(bool)Call(host, "TryRemovePlacedInstance", target.SessionGeneration, instance), "a retired identity cannot be removed again or resolve to a sibling");
            Call(shell, "CloseAndSubmitPosition");
        }
        internal static void Visual(object context, ProbeGraphics graphics, string output)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var state = Get(shell, "State"); var input = Get(context, "Input");
            var list = Get(shell, "BlueprintPlacedUi"); var popup = Get(shell, "BlueprintMaterialsUi"); var library = (LibraryState)Get(host, "Library");
            int oldWidth = Main.screenWidth, oldHeight = Main.screenHeight; float oldScale = Main.UIScale;
            bool auto = (bool)Get(host, "AutoBuildEnabled");
            Directory.CreateDirectory(output);
            using (NativeBlueprintWorldMatchChecks.EmptyWorld(host, 1024, 160)) try
            {
                Call(shell, "CloseAndSubmitPosition"); Call(host, "SetAutoBuild", false);
                // Match the existing world-match fixture's original framing
                // setup before invoking the real PlaceWall retirement writer.
                NativeBlueprintWorldMatchChecks.InitializeMetadata(); Framing.Initialize(); Main.SetupTileMerge();
                Main.screenWidth = 960; Main.screenHeight = 640; NativeBlueprintUiChecks.SetScale(1f); PlayerInput.CacheOriginalScreenDimensions();
                Action step = () => { NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime"); };
                var cells = new[] { new CaptureCell { Layers = CaptureLayer.Wall, WallType = 7 }, new CaptureCell { X = 1, Y = 3, Layers = CaptureLayer.Wall, WallType = 7 } };
                var entry = NativeBlueprintProjectionVisualChecks.Save(library, new BlueprintProjectionWindow(
                    new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "qualification", 1, 1), 1,
                    new CaptureSummary { Width = 2, Height = 4, ContentCount = 2 }, new BlueprintProjectionBounds(0, 0, 2, 4), cells, Array.Empty<CaptureObject>()));
                var first = NativeBlueprintWorldMatchChecks.Place(host, input, entry, 40, 70, step);
                var second = NativeBlueprintWorldMatchChecks.Place(host, input, entry, 400, 70, step);
                long firstId = ((BlueprintProjectionIdentity)Get(first, "Identity")).ProjectionGeneration;
                long secondId = ((BlueprintProjectionIdentity)Get(second, "Identity")).ProjectionGeneration;
                // Only presentation names are supplied here. The two scene
                // instances and remaining-material scope stay on real owners.
                string longName = string.Concat(Enumerable.Repeat("同名中文长蓝图", 6));
                Set(first, "Name", longName); Set(second, "Name", longName); Set(host, "materialSceneIdentity", -1L);
                NativeBlueprintMaterialsUiChecks.Frame(shell, input, 0, 0, false);
                NativeBlueprintMaterialsUiChecks.Frame(shell, input, 0, 0, false); Call(host, "OpenPlacedList");
                Action draw = () => Call(shell, "DrawLayer");
                Action prepare = () =>
                {
                    Exception observed = null;
                    EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> observe = (sender, args) => observed = args.Exception;
                    AppDomain.CurrentDomain.FirstChanceException += observe;
                    try { Call(shell, "AfterUpdate"); graphics.Pixels(draw, Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight); }
                    finally { AppDomain.CurrentDomain.FirstChanceException -= observe; }
                    Require(!(bool)Get(shell, "Failed"), "placed-list production prepare/draw stays healthy; screen=" + PlayerInput.OriginalScreenSize + " matrix=" + Main.UIScaleMatrix.M11 + "; original=" + observed);
                };
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); prepare(); return (int)Get(list, "PreviewCount") > 0; });
                Require((int)Get(list, "PreviewCount") == 1 && (long)Get(list, "PreviewRequests") == 1,
                    "two visible copies reuse the same original-template hash/size/device preview");
                for (int i = 0; i < 8; i++) prepare();
                Require((long)Get(list, "PreviewRequests") == 1, "stable production Prepare never rereads the template body");
                foreach (var visibleRow in Rows(list))
                {
                    var previewRect = Get(Get(Get(visibleRow, "Preview"), "Element"), "Rect");
                    var materialRect = Get(Get(Get(visibleRow, "Materials"), "Element"), "Rect");
                    Require((float)Get(previewRect, "X") > (float)Get(materialRect, "Right"), "original-template image occupies the row right column without overlapping controls");
                }
                graphics.Image(Path.Combine(output, "blueprint-placed-list-two-copies.png"), draw, Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
                var row = Rows(list).Single(value => ((BlueprintMaterialInstance)Get(value, "Instance")).ProjectionGeneration == firstId);
                Action<object> click = part =>
                {
                    var point = ButtonPoint(list, part);
                    NativeBlueprintMaterialsUiChecks.Frame(shell, input, point.X, point.Y, false); prepare();
                    NativeBlueprintMaterialsUiChecks.Frame(shell, input, point.X, point.Y, true);
                    NativeBlueprintMaterialsUiChecks.Frame(shell, input, point.X, point.Y, false); prepare();
                };
                float savedScroll = (float)Get(state, "Scroll"); click(Get(row, "Materials"));
                Require((bool)Get(popup, "Visible") && (long)Get(popup, "Selected") == firstId && Get(popup, "Source").ToString() == "PlacedList", "real clipped row release opens the exact material popup without all-scope reset");
                long pausedRequests = (long)Get(list, "PreviewRequests");
                for (int frame = 0; frame < 6; frame++)
                {
                    NativeBlueprintMaterialsUiChecks.Frame(shell, input, 0, 0, false);
                    Set(list, "drawnGeneration", -1L); prepare();
                    Require((bool)Get(popup, "Visible") && (bool)Get(list, "ready") && (long)Get(list, "drawnGeneration") == (long)Get(list, "generation") && Rows(list).Length == 2,
                        "material popup keeps real list body prepared and rendered on continuous Shell frame " + frame);
                    Require(GetOptional(list, "armed") == null && (long)Get(list, "PreviewRequests") == pausedRequests && (float)Get(state, "Scroll") == savedScroll,
                        "popup suspends list actions/new preview demand while preserving background scroll");
                }
                graphics.Image(Path.Combine(output, "blueprint-placed-list-materials-background.png"), draw, Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
                var close = ((IEnumerable)Get(popup, "buttons")).Cast<object>().First(); var closeRect = Get(close, "Rect");
                float cx = (float)Get(closeRect, "X") + (float)Get(closeRect, "Width") / 2, cy = (float)Get(closeRect, "Y") + (float)Get(closeRect, "Height") / 2;
                NativeBlueprintMaterialsUiChecks.Frame(shell, input, cx, cy, false); prepare(); NativeBlueprintMaterialsUiChecks.Frame(shell, input, cx, cy, true); NativeBlueprintMaterialsUiChecks.Frame(shell, input, cx, cy, false); prepare();
                Require(!(bool)Get(popup, "Visible") && (bool)Get(list, "Visible") && (float)Get(state, "Scroll") == savedScroll, "real material close returns to the same list scroll and sole content owner");

                row = Rows(list).Single(value => ((BlueprintMaterialInstance)Get(value, "Instance")).ProjectionGeneration == firstId);
                var remove = Get(row, "Remove"); var oldPoint = ButtonPoint(list, remove);
                NativeBlueprintMaterialsUiChecks.Frame(shell, input, oldPoint.X, oldPoint.Y, false); prepare(); NativeBlueprintMaterialsUiChecks.Frame(shell, input, oldPoint.X, oldPoint.Y, true);
                Require(ReferenceEquals(GetOptional(list, "armed"), remove), "actual list remove control is armed only after its real draw");
                WorldGen.PlaceWall(40, 70, 7, true); WorldGen.PlaceWall(41, 73, 7, true);
                try { NativeQuickItemChecks.Until(() => { step(); return !((IEnumerable)Get(host, "ProjectionScene")).Cast<object>().Contains(first); }); }
                catch
                {
                    var match = GetOptional(first, "Match");
                    Console.WriteLine("LIST retirement evidence wall=" + Main.tile[40, 70].wall + "/" + Main.tile[41, 73].wall + " paint=" + Main.tile[40, 70].wallColor() + "/" + Main.tile[41, 73].wallColor() + " origin=" + Get(first, "OriginX") + "/" + Get(first, "OriginY") + " mode=" + Get(host, "ProjectionMode") + " current=" + GetOptional(match, "Current") + " targets=" + GetOptional(match, "Targets") + " matches=" + GetOptional(match, "Matches") + " unknown=" + GetOptional(match, "Unknown") + " reads=" + GetOptional(match, "WorldReads") + " sourceRead=" + Get(host, "ProjectionMatchReading"));
                    throw;
                }
                prepare(); NativeBlueprintMaterialsUiChecks.Frame(shell, input, oldPoint.X, oldPoint.Y, false); prepare();
                Require(((IEnumerable)Get(host, "ProjectionScene")).Cast<object>().Single() == second, "natural completion between press and release cannot remove the sibling shifted into its row");
                row = Rows(list).Single(value => ((BlueprintMaterialInstance)Get(value, "Instance")).ProjectionGeneration == secondId);
                click(Get(row, "Remove"));
                Require(((IReadOnlyList<BlueprintMaterialInstance>)Get(host, "MaterialInstances")).Count == 0 && Main.tile[40, 70].wall == 7 && Main.tile[41, 73].wall == 7,
                    "real surviving-row removal preserves original built walls and empties only the committed scene");
                graphics.Image(Path.Combine(output, "blueprint-placed-list-empty.png"), draw, Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
                LargeNumberVisual(host, shell, graphics, output);
                Call(shell, "CloseAndSubmitPosition"); long closedRequests = (long)Get(list, "PreviewRequests");
                for (int i = 0; i < 4; i++) { Call(host, "Poll"); Call(shell, "AfterUpdate"); }
                Require((int)Get(list, "PreviewCount") == 0 && (long)Get(list, "PreviewBytes") == 0 && (long)Get(list, "PreviewRequests") == closedRequests,
                    "closing releases all list-owned preview textures and starts no new body read");
            }
            finally
            {
                Call(shell, "CloseAndSubmitPosition"); Call(host, "SetAutoBuild", auto);
                Main.screenWidth = oldWidth; Main.screenHeight = oldHeight; NativeBlueprintUiChecks.SetScale(oldScale); PlayerInput.CacheOriginalScreenDimensions();
            }
            Console.WriteLine("PASS Blueprint placed-list native UI: two same-template instances, wrapped names, short viewport/final clip, real Draw-to-release materials return, natural-retirement stale release, accurate removal and empty state. OS and multiplayer are separate evidence.");
        }
        private static object[] Rows(object list) { return ((IEnumerable)Get(list, "rows")).Cast<object>().ToArray(); }
        private static Vector2 ButtonPoint(object list, object part)
        {
            var rect = Get(Get(part, "Element"), "Rect"); var view = Get(list, "view"); var clip = Get(list, "hitClip");
            float x = (float)Get(view, "X") + (float)Get(rect, "X") + (float)Get(rect, "Width") / 2;
            float y = (float)Get(view, "Y") + (float)Get(rect, "Y") + (float)Get(rect, "Height") / 2 - (float)Get(list, "scroll");
            Require(x >= (float)Get(clip, "X") && x < (float)Get(clip, "Right") && y >= (float)Get(view, "Y") + (float)Get(list, "headerHeight") && y < (float)Get(clip, "Bottom"), "actual row button center lies inside the final pixel clip and below its fixed header");
            return new Vector2(x, y);
        }
        private static void LargeNumberVisual(object host, object shell, ProbeGraphics graphics, string output)
        {
            var state = Get(shell, "State"); var type = Get(shell, "BlueprintPlacedUi").GetType();
            var ui = Activator.CreateInstance(type, F, null, new[] { host, state }, null);
            try
            {
                Call(Get(ui, "renderer"), "Refresh"); Set(ui, "view", Get(Get(state, "Layout"), "Viewport"));
                var view = Get(ui, "view"); Set(ui, "view", Call(view, "Offset", (float)Get(state, "X"), (float)Get(state, "Y")));
                Set(ui, "directory", new[] { new BlueprintMaterialInstance(1, 123456789, 123456789, "大整数布局样本", 1234567890, 1234567890, 1000000000, 1000000000, 1) });
                Set(ui, "scroll", 0f); Set(ui, "matrix", Main.UIScaleMatrix); Set(ui, "screen", PlayerInput.OriginalScreenSize);
                Call(ui, "Build"); Call(ui, "Project", 0, 0, true); Set(ui, "Opened", true); Set(ui, "ready", true);
                // This is an explicit renderer-value fixture, not a live world
                // coordinate or a source of executable row identity.
                graphics.Image(Path.Combine(output, "blueprint-placed-list-large-values.png"), () => { Call(Get(shell, "renderer"), "Draw", state, Main.UIScaleMatrix, false, false); Call(ui, "Draw"); }, Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
            }
            finally { ((IDisposable)ui).Dispose(); }
        }
    }
}
