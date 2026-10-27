using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // UI oracle comes from the task's decimal/grid/table/identity contract.
    // The pure values here do not prove native material mapping or inventory.
    internal static class NativeBlueprintMaterialsUiChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static long secondSequence;
        private static string observedEntry;
        private static object imageSurface;
        private static Matrix expectedImageMatrix, actualImageMatrix, mismatchedImageMatrix;
        private static int imagePasses;
        private static bool imageMatrixMismatch;
        internal static void BeginInstanceScope(object context, long firstId, long secondId)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var popup = Get(shell, "BlueprintMaterialsUi");
            NativeQuickItemChecks.Sample(Get(context, "Input"), Array.Empty<Keys>()); Call(host, "OpenMaterials");
            Require((bool)Get(popup, "Visible") && (long)Get(popup, "Selected") == 0, "every real world material opening defaults to all committed instances");
            var instances = (IReadOnlyList<BlueprintMaterialInstance>)Get(host, "MaterialInstances");
            secondSequence = instances.Single(i => i.ProjectionGeneration == secondId).Sequence;
            Require((bool)Call(popup, "SelectScope", firstId), "the real popup range command selects the first exact instance identity");
            var value = (BlueprintMaterialSnapshot)Call(popup, "ReadSnapshot");
            Require((long)Get(popup, "Selected") == firstId && value.Status == BlueprintMaterialStatus.Exact && value.Rows.Any(r => r.Required == 50 && r.Held == 80 && r.Missing == 0),
                "actual popup scope consumes the independently established single-instance 50/80/0 result");
        }
        internal static void AssertRetiredScope(object context, long secondId)
        {
            var host = Get(context, "Blueprint"); var popup = Get(Get(context, "Shell"), "BlueprintMaterialsUi");
            try
            {
                var value = (BlueprintMaterialSnapshot)Call(popup, "ReadSnapshot"); var instances = (IReadOnlyList<BlueprintMaterialInstance>)Get(host, "MaterialInstances");
                Require((long)Get(popup, "Selected") == 0 && instances.Count == 1 && instances[0].ProjectionGeneration == secondId && instances[0].Sequence == secondSequence,
                    "real first-instance retirement resets the popup to all while the surviving instance keeps its sequence");
                Require(value.Status == BlueprintMaterialStatus.Exact && value.Rows.Any(r => r.Required == 50 && r.Held == 79 && r.Missing == 0),
                    "recovered actual popup all scope consumes only the survivor against current shared inventory");
            }
            finally { Call(popup, "Close"); }
        }
        internal static void Run(object context)
        {
            NativeBlueprintDraftMaterialsChecks.Cpu(context);
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var state = Get(shell, "State");
            var painter = Get(shell, "renderer"); var oldFont = Terraria.GameContent.FontAssets.MouseText;
            try
            {
                FiniteCostChecks.SetCpuFont(10); Call(painter, "RefreshResources"); Call(painter, "Prepare", state, 960f, 640f, 1f);
                var type = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintMaterialsLayout", true);
                object layout = Activator.CreateInstance(type, true); var signature = type.GetMethod("Prepare", F);
                var measureType = signature.GetParameters()[3].ParameterType;
                var production = Delegate.CreateDelegate(measureType, Get(state, "Layout"), Get(state, "Layout").GetType().GetMethod("DynamicTextSize", F));
                int measures = 0; var text = Expression.Parameter(typeof(string)); var scale = Expression.Parameter(typeof(float));
                Action count = () => measures++;
                var measure = Expression.Lambda(measureType, Expression.Block(Expression.Invoke(Expression.Constant(count)),
                    Expression.Invoke(Expression.Constant(production, measureType), text, scale)), text, scale).Compile();
                Func<int, string> name = id => (id == 1 ? "A\u0301𠮷" : "") + new string('材', id == 1 ? 70 : 3);
                Func<BlueprintMaterialSnapshot, bool, float, float, float> prepare = (value, world, width, scroll) =>
                    (float)Call(layout, "Prepare", value, world, width, measure, name, 1L, 1, scroll);
                var longValue = new BlueprintMaterialSnapshot(1, BlueprintMaterialStatus.Exact,
                    new[] { new BlueprintMaterialRow(1, long.MaxValue - 2, long.MaxValue - 1, true), new BlueprintMaterialRow(2, 2, 1, true) }, true);
                prepare(longValue, true, 320, 0);
                var rectType = type.Assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect", true);
                Func<float, float, object> clip = (y, h) => Activator.CreateInstance(rectType, F, null, new object[] { 0f, y, 320f, h }, null);
                Call(layout, "Project", 0f, 0f, clip(0, 1000));
                var cells = Objects(Get(layout, "cells"));
                string[] digits = Objects(Get(cells[0], "Glyphs")).Select(g => (string)Get(Get(g, "Element"), "Text")).Where(s => s.All(char.IsDigit)).ToArray();
                Require(string.Concat(digits) == (long.MaxValue - 2).ToString(CultureInfo.InvariantCulture) + (long.MaxValue - 1).ToString(CultureInfo.InvariantCulture) + "0",
                    "compressed real table preserves every decimal digit for required/held/missing instead of int, stack or 999+ truncation");
                Require(Objects(Get(cells[0], "Glyphs")).Count(g => ((string)Get(Get(g, "Element"), "Text")).Contains("材")) > 1,
                    "long material names wrap at the readable scale in their actual name column");
                Require(string.Concat(Objects(Get(cells[0], "Glyphs")).Where(g => (bool)Get(g, "Name")).Select(g => (string)Get(Get(g, "Element"), "Text"))) == name(1),
                    "wrapped labels preserve complete Chinese, surrogate and combining text elements in their original order");
                int generation = (int)Get(layout, "Generation"), stableMeasures = measures;
                for (int i = 0; i < 120; i++) prepare(longValue, true, 320, 0);
                Require((int)Get(layout, "Generation") == generation && measures == stableMeasures, "120 stable material preparations reuse rows and glyph metrics");
                float stableHeight = (float)Get(layout, "Height");
                prepare(BlueprintMaterialSnapshot.Pending, true, 320, 37); Call(layout, "Project", 0f, -37f, clip(0, 1000));
                Require((bool)Get(layout, "HoldingPending") && (int)Get(layout, "Count") == 2 && (float)Get(layout, "Height") == stableHeight &&
                    Objects(Get(Objects(Get(layout, "Visible"))[0], "Glyphs")).Count(g => (string)Get(Get(g, "Element"), "Text") == "—") == 3,
                    "an empty Pending slice retains measured ItemType geometry but draws no previous exact quantities");
                int pendingMeasures = measures;
                for (int i = 0; i < 120; i++) Require(prepare(BlueprintMaterialSnapshot.Pending, true, 320, 37) == 37, "stable Pending does not reset its scroll");
                Require(measures == pendingMeasures, "stable Pending placeholders reuse measured glyphs");
                prepare(longValue, true, 320, 37);
                Require(!(bool)Get(layout, "HoldingPending") && (float)Get(layout, "Height") == stableHeight, "the same committed rows recover from Pending without replacing their scroll geometry");
                var partial = new BlueprintMaterialSnapshot(2, BlueprintMaterialStatus.Unresolved, new[] { new BlueprintMaterialRow(2, 7) }, false, "这一目标暂不能确定材料");
                Require(((string)type.GetMethod("Summary", F).Invoke(null, new object[] { partial, true })).Contains("—") &&
                    ((string)type.GetMethod("Summary", F).Invoke(null, new object[] { BlueprintMaterialSnapshot.Empty, true })).Contains("0"),
                    "unresolved known rows and exact zero retain distinct public summaries");
                var incomplete = new BlueprintMaterialSnapshot(20, BlueprintMaterialStatus.Unresolved, new[] { new BlueprintMaterialRow(2, 50, 80, true) }, true);
                prepare(incomplete, true, 522, 0); var incompleteGlyphs = Objects(Get(Objects(Get(layout, "cells"))[0], "Glyphs"));
                Require(incompleteGlyphs.Any(g => (string)Get(Get(g, "Element"), "Text") == "50") && incompleteGlyphs.Any(g => (string)Get(Get(g, "Element"), "Text") == "80") &&
                    incompleteGlyphs.Any(g => (string)Get(Get(g, "Element"), "Text") == "—") && !incompleteGlyphs.Any(g => (string)Get(Get(g, "Element"), "Text") == "0"),
                    "unresolved scope retains reliable known need/held but cannot infer exact zero missing from ready inventory");
                var complete = new BlueprintMaterialSnapshot(21, BlueprintMaterialStatus.Exact, new[] { new BlueprintMaterialRow(2, 50, 80, true) }, true);
                prepare(complete, true, 522, 0);
                Require(Objects(Get(Objects(Get(layout, "cells"))[0], "Glyphs")).Any(g => (string)Get(Get(g, "Element"), "Text") == "0"),
                    "completing the same row scope replaces the unknown missing slot with exact zero");
                var noInventory = new BlueprintMaterialSnapshot(22, BlueprintMaterialStatus.Exact, new[] { new BlueprintMaterialRow(2, 50) });
                Require(((string)type.GetMethod("Summary", F).Invoke(null, new object[] { noInventory, true })).Contains("—") &&
                    !((string)type.GetMethod("Summary", F).Invoke(null, new object[] { noInventory, false })).Contains("—"),
                    "inventory failure prevents complete dynamic totals while the independent static requirement remains exact");
                var rows = Enumerable.Range(1, 700).Select(i => new BlueprintMaterialRow(i, 1)).ToArray();
                var many = new BlueprintMaterialSnapshot(3, BlueprintMaterialStatus.Exact, rows);
                prepare(many, false, 320, 0); Call(layout, "Project", 0f, 0f, clip(0, 90));
                Require(Objects(Get(layout, "Visible")).Length < 20 && (float)Get(layout, "Height") > 90, "large static grid projects only visible cells and retains its true full scroll height");
                float last = Math.Max(0, (float)Get(layout, "Height") - 90);
                Call(layout, "Project", 0f, -last, clip(0, 90));
                Require(Objects(Get(layout, "Visible")).Any(c => (int)Get(Get(c, "Cell"), "ItemType") == 700), "the final static material remains reachable in a short viewport");
                foreach (var cell in Objects(Get(layout, "cells")))
                foreach (var glyph in Objects(Get(cell, "Glyphs")))
                {
                    var bounds = Get(Get(glyph, "Element"), "Rect"); var owner = Get(cell, "Rect");
                    Require((float)Get(bounds, "Right") <= (float)Get(owner, "Right") - 3.9f && (float)Get(bounds, "Bottom") <= (float)Get(owner, "Bottom") - 3.9f,
                        "grid quantities including one keep the full measured right/bottom boundary");
                }
                prepare(BlueprintMaterialSnapshot.Pending, false, 320, 0);
                Require(!(bool)Get(layout, "HoldingPending") && (int)Get(layout, "Count") == 0 && (float)Get(layout, "Height") == 0,
                    "a static draft or exclusion key change cannot borrow the previous key's material rows while pending");
                var three = new BlueprintMaterialSnapshot(4, BlueprintMaterialStatus.Exact, new[] { new BlueprintMaterialRow(1, 3), new BlueprintMaterialRow(2, 4), new BlueprintMaterialRow(3, 5) });
                prepare(three, true, 522, 0); var before = Objects(Get(layout, "cells")); float anchor = (float)Get(Get(before[1], "Rect"), "Y");
                var changed = new BlueprintMaterialSnapshot(5, BlueprintMaterialStatus.Exact, new[] { new BlueprintMaterialRow(1, 9), new BlueprintMaterialRow(2, 4), new BlueprintMaterialRow(3, 5) });
                float retained = prepare(changed, true, 522, anchor); var after = Objects(Get(layout, "cells"));
                Require(ReferenceEquals(before[1], after[1]) && retained == anchor, "quantity-only changes reuse the unchanged ItemType row and its current scroll anchor");
                float successorOffset = (float)Get(Get(after[2], "Rect"), "Y") - anchor;
                var retired = new BlueprintMaterialSnapshot(6, BlueprintMaterialStatus.Exact, new[] { new BlueprintMaterialRow(1, 9), new BlueprintMaterialRow(3, 5) });
                float recovered = prepare(retired, true, 522, anchor); var survivors = Objects(Get(layout, "cells"));
                Require((float)Get(Get(survivors[1], "Rect"), "Y") - recovered == successorOffset,
                    "completion of the first visible material preserves the next surviving ItemType at its prior screen offset");
                NativeBlueprintMaterialGeometryChecks.Run(context);
                Console.WriteLine("PASS Blueprint material UI CPU: exact long decimals, wrapped names, pending/unresolved versus zero, stable 120 preparations, virtual visible set, 700th-row reachability and ItemType anchor. GPU/OS/IME NOT_EXECUTED.");
            }
            finally { Terraria.GameContent.FontAssets.MouseText = oldFont; Call(painter, "RefreshResources"); }
        }

        internal static void Input(object context, ProbeGraphics graphics, string output = null)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var state = Get(shell, "State"); var input = Get(context, "Input");
            var popup = Get(shell, "BlueprintMaterialsUi"); var controls = Get(Get(shell, "renderer"), "BlueprintControls");
            var commands = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.F5Command", true);
            var registry = (HotkeyRegistry)Get(Get(shell, "hotkeys"), "Registry"); var bindings = (HotkeyBindings)Get(Get(shell, "hotkeys"), "Bindings");
            var priorAutoKey = bindings.Get("blueprint.autobuild"); var priorMaterialKey = bindings.Get("blueprint.materials");
            var capture = (BlueprintCaptureState)Get(host, "State"); var player = Main.LocalPlayer;
            bool priorAuto = (bool)Get(host, "AutoBuildEnabled"); int selected = player.selectedItem; bool priorInterface = player.mouseInterface;
            var oldMouse = PlayerInput.MouseInfo; bool oldLeft = Main.mouseLeft, oldRight = Main.mouseRight;
            int oldWheel = PlayerInput.ScrollWheelDelta, oldUiWheel = PlayerInput.ScrollWheelDeltaForUI;
            Exception failure = null;
            try
            {
                Call(shell, "CloseAndSubmitPosition"); capture.Exit(); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
                Require(registry.Find("blueprint.autobuild") != null && registry.Find("blueprint.materials") != null && bindings.Get("blueprint.autobuild") == null && bindings.Get("blueprint.materials") == null,
                    "both real common actions register with unbound defaults");
                Call(controls, "Execute", Enum.Parse(commands, "BlueprintAutoBuildOn")); Call(controls, "Execute", Enum.Parse(commands, "BlueprintAutoBuildOn"));
                Require((bool)Get(host, "AutoBuildEnabled"), "idempotent F5 enable reads the unique preference owner");
                HotkeyChord autoKey = null, materialKey = null; string reason;
                Require(HotkeyChord.TryParse("F8", out autoKey, out reason) && HotkeyChord.TryParse("F9", out materialKey, out reason), "isolated material action chords parse");
                SetBinding(bindings, "blueprint.autobuild", autoKey); SetBinding(bindings, "blueprint.materials", materialKey);
                Frame(shell, input, 1, 1, false, 0, Keys.F8);
                Require(!(bool)Get(host, "AutoBuildEnabled"), "actual binding/sample/shell dispatch toggles the same F5 preference owner");
                Frame(shell, input, 1, 1, false, 0, Keys.F8);
                Require(!(bool)Get(host, "AutoBuildEnabled"), "held automatic-build chord cannot toggle a second time");
                Frame(shell, input, 1, 1, false); Frame(shell, input, 1, 1, false, 0, Keys.F8);
                Require((bool)Get(host, "AutoBuildEnabled"), "a fresh automatic-build chord toggles on through the shared dispatcher");
                Call(controls, "Execute", Enum.Parse(commands, "BlueprintAutoBuildOff")); Frame(shell, input, 1, 1, false);
                Frame(shell, input, 1, 1, false, 0, Keys.F9);
                Require((bool)Get(popup, "Visible") && !(bool)Get(state, "Visible") && Get(popup, "Source").ToString() == "World", "actual material binding opens the world modal without a hidden F5 host window");
                Frame(shell, input, 1, 1, false, 0, Keys.F9); Frame(shell, input, 1, 1, false);
                Require((bool)Get(popup, "Visible"), "the opening material chord cannot retrigger while held");
                Prepare(shell, graphics);
                var body = Get(popup, "Body"); float x = (float)Get(body, "X") + 5, y = (float)Get(body, "Y") + 5;
                player.selectedItemState.Select(2); player.selectedItemState.Update(); int hotbar = player.selectedItemState.Hotbar;
                Frame(shell, input, x, y, false, -120); typeof(Player).GetMethod("HandleHotbarControls", F).Invoke(player, null); player.selectedItemState.Update();
                Require(player.selectedItemState.Hotbar == hotbar && PlayerInput.ScrollWheelDelta == 0 && PlayerInput.ScrollWheelDeltaForUI == 0, "actual native hotbar consumer sees no wheel at material list top/bottom");
                Prepare(shell, graphics);
                var close = Objects(Get(popup, "buttons"))[0]; var r = Get(close, "Rect"); x = (float)Get(r, "X") + (float)Get(r, "Width") / 2; y = (float)Get(r, "Y") + (float)Get(r, "Height") / 2;
                Frame(shell, input, x, y, true); PlayerInput.Triggers.Current.CopyInto(player);
                Require(!player.controlUseItem && (bool)Get(popup, "Visible"), "material press passes production CopyInto with no extra world use");
                Frame(shell, input, x, y, false); PlayerInput.Triggers.Current.CopyInto(player);
                Require(!(bool)Get(popup, "Visible") && !(bool)Get(state, "Visible") && !player.controlUseItem, "real close release stays consumed and returns directly to world");
                Frame(shell, input, 1, 1, false); Frame(shell, input, 1, 1, false);
                Frame(shell, input, 1, 1, false, -120); typeof(Player).GetMethod("HandleHotbarControls", F).Invoke(player, null); player.selectedItemState.Update();
                Require(player.selectedItemState.Hotbar != hotbar, "same native hotbar consumer resumes on a later independent world wheel");
                Call(host, "OpenMaterials"); Prepare(shell, graphics);
                var selector = Get(popup, "selector"); x = (float)Get(selector, "X") + 10; y = (float)Get(selector, "Y") + 10;
                Frame(shell, input, x, y, true); Frame(shell, input, x, y, false); Prepare(shell, graphics);
                Require((bool)Get(popup, "RangeExpanded"), "the real single scope selector opens");
                Frame(shell, input, x, y, false, 0, Keys.Escape); Require((bool)Get(popup, "Visible") && !(bool)Get(popup, "RangeExpanded"), "first Escape only closes expanded scope");
                Frame(shell, input, x, y, false); Prepare(shell, graphics); Frame(shell, input, x, y, false, 0, Keys.Escape);
                Require(!(bool)Get(popup, "Visible"), "later independent Escape closes only the material window");
                Frame(shell, input, 1, 1, false); Frame(shell, input, 1, 1, false);
                LifecycleAndToolbar(context, graphics);
                Call(state, "Navigate", 6); Call(state, "RestoreVisible"); ClickF5(shell, input, graphics, "BlueprintAutoBuildOn");
                Require((bool)Get(host, "AutoBuildEnabled"), "actual F5 On button reaches the unique automatic-build owner");
                ClickF5(shell, input, graphics, "BlueprintAutoBuildOff"); Require(!(bool)Get(host, "AutoBuildEnabled"), "actual F5 Off button is an idempotent setting command");
                ClickF5(shell, input, graphics, "BlueprintMaterials"); Prepare(shell, graphics);
                Require((bool)Get(popup, "Visible") && (bool)Get(state, "Visible") && Get(popup, "Source").ToString() == "F5", "F5 row calls the same modal through its F5 return context");
                Call(popup, "Close"); Require((bool)Get(state, "Visible") && (int)Get(state, "Page") == 6, "closing F5 materials retains the blueprint main page");
                LibraryReturn(context, graphics);
                NativeBlueprintMaterialsDragChecks.Run(context, graphics, output);
                HostPublicationGaps(context, graphics, output);
                Console.WriteLine("PASS Blueprint material native input: real Host/registered action/F5 owner, source return, scope Escape hierarchy, original CopyInto and HandleHotbarControls consume opening/closing/list tails and resume later. OS clicks/IME/multiplayer NOT_EXECUTED.");
            }
            catch (Exception error) { failure = error; throw; }
            finally
            {
                RestoreAfterCheck(failure,
                    () => { Call(popup, "Close"); Call(host, "SetAutoBuild", priorAuto); Call(shell, "CloseAndSubmitPosition"); },
                    () => { Frame(shell, input, 1, 1, false); Frame(shell, input, 1, 1, false); },
                    () => { SetBinding(bindings, "blueprint.autobuild", priorAutoKey); SetBinding(bindings, "blueprint.materials", priorMaterialKey); },
                    () => { player.selectedItemState.Select(selected); player.selectedItemState.Update(); player.mouseInterface = priorInterface; },
                    () => { PlayerInput.ScrollWheelDelta = oldWheel; PlayerInput.ScrollWheelDeltaForUI = oldUiWheel;
                        PlayerInput.MouseInfo = oldMouse; Main.mouseLeft = oldLeft; Main.mouseRight = oldRight; });
            }
        }
        private static void HostPublicationGaps(object context, ProbeGraphics graphics, string output)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var popup = Get(shell, "BlueprintMaterialsUi"); var input = Get(context, "Input");
            var field = host.GetType().GetField("<Library>k__BackingField", F); var original = (LibraryState)field.GetValue(host);
            var player = Main.LocalPlayer; Item slot = player.inventory[0]; bool auto = (bool)Get(host, "AutoBuildEnabled");
            int originalHeight = Main.screenHeight;
            Call(shell, "CloseAndSubmitPosition"); Call(host, "SetAutoBuild", false);
            using (var library = new LibraryState(new FileBlueprintLibrary(Path.Combine(Terraria.Program.SavePath, "material-publication-" + Guid.NewGuid().ToString("N")))))
            using (NativeBlueprintWorldMatchChecks.EmptyWorld(host, 1024, 160))
            {
                field.SetValue(host, library);
                // FailClosed intentionally contains UI exceptions. Observe only
                // this regression's synchronous production entries, retaining
                // the original exception before a later health assertion.
                int thread = Thread.CurrentThread.ManagedThreadId;
                var faults = new List<KeyValuePair<string, Exception>>();
                EventHandler<FirstChanceExceptionEventArgs> trace = (sender, args) =>
                {
                    if (Thread.CurrentThread.ManagedThreadId == thread && observedEntry != null && faults.Count < 8)
                        faults.Add(new KeyValuePair<string, Exception>(observedEntry, args.Exception));
                };
                Exception failure = null;
                AppDomain.CurrentDomain.FirstChanceException += trace;
                try
                {
                    NativeBlueprintWorldMatchChecks.InitializeMetadata(); Framing.Initialize(); Main.SetupTileMerge();
                    Action tick = () =>
                    {
                        NativeQuickItemChecks.BeginWorldStep(); ObservedCall(context, "UpdateRuntime");
                        Require(!(bool)Get(shell, "Failed"), "material production shell remains healthy after PostfixContext.UpdateRuntime in the seventy-row publication regression");
                    };
                    Call(host, "SetMaterialViewDemand", true);
                    NativeQuickItemChecks.Until(() => { tick(); return GetOptional(host, "MaterialCatalog") != null; });
                    var catalog = (BlueprintPlacementCatalog)Get(host, "MaterialCatalog");
                    var declarations = (IDictionary<int, BlueprintPlacementDescriptor>)Get(catalog, "walls");
                    var walls = declarations.Where(p => p.Value.ConsumeItemType > 0).GroupBy(p => p.Value.ConsumeItemType).Select(g => g.First()).OrderBy(p => p.Value.ConsumeItemType).Take(70).ToArray();
                    Require(walls.Length == 70, "the real frozen vanilla wall catalog supplies more than one 64-row publication slice");
                    // ProbeGraphics does not run the full native asset initializer.
                    // Supply these real Content textures before the real UI asks
                    // Main.LoadItem to inspect their native asset descriptors.
                    graphics.LoadItemTextures(walls.Select(p => p.Value.ConsumeItemType));
                    var cells = walls.Select((p, i) => new CaptureCell { X = i, Layers = CaptureLayer.Wall, WallType = (ushort)p.Key }).ToArray();
                    var entry = NativeBlueprintProjectionVisualChecks.Save(library, new BlueprintProjectionWindow(
                        new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "publication-gap", 1, 1), 1,
                        new CaptureSummary { Width = cells.Length, Height = 1, ContentCount = cells.Length }, new BlueprintProjectionBounds(0, 0, cells.Length, 1), cells, Array.Empty<CaptureObject>()));
                    var placed = NativeBlueprintWorldMatchChecks.Place(host, input, entry, 40, 70, tick);
                    Frame(shell, input, 1, 1, false); Frame(shell, input, 1, 1, false); Call(host, "OpenMaterials");
                    NativeQuickItemChecks.Until(() => { tick(); return ((BlueprintMaterialSnapshot)Call(host, "MaterialWorld", 0L)).Status == BlueprintMaterialStatus.Exact; });
                    Prepare(shell, graphics); var content = Get(popup, "Content");
                    Require((int)Get(content, "Count") == 70, "actual source/match/material publication reaches the real common popup with seventy material rows");
                    NativeBlueprintMaterialNameChecks.Standalone(context, graphics);
                    if (output != null) ImagesAtScales(shell, popup, graphics, output, "blueprint-world-materials-compact-first");
                    Main.screenHeight = Math.Min(originalHeight, 400); PlayerInput.CacheOriginalScreenDimensions(); Prepare(shell, graphics);
                    var shortBody = Get(popup, "Body"); float shortX = (float)Get(shortBody, "X") + 8, shortY = (float)Get(shortBody, "Y") + 8;
                    int hotbar = player.selectedItemState.Hotbar;
                    BoundaryWheel(shell, input, graphics, shortX, shortY, 120, hotbar);
                    Require((float)Get(popup, "scroll") == 0, "the nonempty first material keeps its top boundary under an extra real wheel");
                    for (int i = 0; i < 80 && (float)Get(popup, "scroll") < (float)Get(content, "Height") - (float)Get(shortBody, "Height"); i++)
                    { Frame(shell, input, shortX, shortY, false, -120); Prepare(shell, graphics); }
                    float bottomScroll = (float)Get(popup, "scroll");
                    var last = Objects(Get(content, "Visible")).SingleOrDefault(r => (int)Get(Get(r, "Cell"), "ItemType") == walls[walls.Length - 1].Value.ConsumeItemType);
                    Require(bottomScroll > 0 && last != null && (float)Get(Get(last, "Rect"), "Y") >= (float)Get(shortBody, "Y") &&
                        (float)Get(Get(last, "Rect"), "Bottom") <= (float)Get(shortBody, "Bottom") + .01f,
                        "the actual seventieth material is fully visible at the bottom of the short native viewport");
                    BoundaryWheel(shell, input, graphics, shortX, shortY, -120, hotbar);
                    Require((float)Get(popup, "scroll") == bottomScroll, "the nonempty last material retains its bottom boundary under an extra real wheel");
                    if (output != null) graphics.Image(Path.Combine(output, "blueprint-world-materials-short-last-row.png"), () => Call(popup, "Draw"), Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
                    for (int i = 0; i < 80 && (float)Get(popup, "scroll") > 0; i++)
                    { Frame(shell, input, shortX, shortY, false, 120); Prepare(shell, graphics); }
                    Require((float)Get(popup, "scroll") == 0, "the same real wheel returns the nonempty list to its first material");
                    BoundaryWheel(shell, input, graphics, shortX, shortY, 120, hotbar);
                    Main.screenHeight = originalHeight; PlayerInput.CacheOriginalScreenDimensions(); Prepare(shell, graphics);
                    var body = Get(popup, "Body"); float x = (float)Get(body, "X") + 8, y = (float)Get(body, "Y") + 8;
                    for (int i = 0; i < 6; i++) { Frame(shell, input, x, y, false, -120); Prepare(shell, graphics); }
                    float scroll = (float)Get(popup, "scroll"); Require(scroll > 0, "actual material wheel establishes a nonzero scroll anchor");
                    var first = Objects(Get(content, "Visible"))[0]; int item = (int)Get(Get(first, "Cell"), "ItemType");
                    float top = (float)Get(Get(first, "Rect"), "Y");
                    Frame(shell, input, x, Math.Max(y, top + 5), false); Prepare(shell, graphics);
                    int hovered = (int)Get(popup, "HoveredItem"); Require(hovered != 0, "actual material row establishes a hover identity before publication loss");
                    Console.WriteLine("MATERIAL ANCHOR baseline " + MaterialAnchorState(popup, content, item, top));
                    player.inventory[0] = new Item(); player.inventory[0].SetDefaults(walls[0].Value.ConsumeItemType); player.inventory[0].stack = 2;
                    tick(); var pending = (BlueprintMaterialSnapshot)Call(host, "MaterialWorld", 0L);
                    Require(pending.Status == BlueprintMaterialStatus.Pending && pending.Rows.Count == 0 && GetOptional(host, "materialPublishing") != null,
                        "real inventory revision starts the bounded seventy-row publisher and exposes its empty Pending slice after Host.Update");
                    Prepare(shell, graphics); AssertPendingView(popup, content, scroll);
                    NativeBlueprintMaterialNameChecks.PendingSummary(context, graphics);
                    NativeQuickItemChecks.Until(() => { tick(); Prepare(shell, graphics); return ((BlueprintMaterialSnapshot)Get(popup, "snapshot")).Status == BlueprintMaterialStatus.Exact; });
                    AssertAnchor(popup, content, item, top, "inventory-exact");
                    NativeBlueprintMaterialsDragChecks.DuringPublication(context, graphics, tick, () =>
                    {
                        WorldGen.PlaceWall(40, 70, cells[0].WallType, true);
                        Require(Main.tile[40, 70].wall == cells[0].WallType, "independent native wall writer completes one material while the title remains grabbed");
                    });

                    var close = Objects(Get(popup, "buttons"))[0]; var closeRect = Get(close, "Rect"); x = (float)Get(closeRect, "X") + 8; y = (float)Get(closeRect, "Y") + 8;
                    Frame(shell, input, x, y, true); Require(ReferenceEquals(GetOptional(popup, "armed"), close), "real close press is armed before the world-match publication gap");
                    long reads = (long)Get(host, "ProjectionMatchCellReads"); bool afterMatchGap = false;
                    string priorAnchorState = null; int anchorTraces = 0;
                    WorldGen.PlaceWall(41, 70, cells[1].WallType, true);
                    Require(Main.tile[41, 70].wall == cells[1].WallType, "independent original wall writer changes the next actual target");
                    NativeQuickItemChecks.Until(() =>
                    {
                        tick(); var value = (BlueprintMaterialSnapshot)Call(host, "MaterialWorld", 0L); Prepare(shell, graphics);
                        string anchorState = "hostStatus=" + value.Status + " hostRows=" + value.Rows.Count + " " + MaterialAnchorState(popup, content, item, top);
                        if (anchorState != priorAnchorState && anchorTraces < 32)
                        { Console.WriteLine("MATERIAL ANCHOR world-step " + anchorState); anchorTraces++; }
                        priorAnchorState = anchorState;
                        if ((long)Get(host, "ProjectionMatchCellReads") > reads && value.Status == BlueprintMaterialStatus.Pending && value.Rows.Count == 0 && GetOptional(host, "materialPublishing") == null) afterMatchGap = true;
                        Require(ReferenceEquals(GetOptional(popup, "armed"), close), "same physical Close remains eligible through actual Host material/match updates");
                        return afterMatchGap && value.Status == BlueprintMaterialStatus.Exact && value.Rows.Count == 68;
                    });
                    Require(afterMatchGap, "actual Host.Update material pass then matching/revocation exposes empty Pending to the actual popup Prepare");
                    AssertAnchor(popup, content, item, top, "world-68-exact");
                    Frame(shell, input, x, y, false); PlayerInput.Triggers.Current.CopyInto(player);
                    Require(!(bool)Get(popup, "Visible") && !player.controlUseItem, "the original close release succeeds after Pending recovery without world-use click-through");
                    Frame(shell, input, 1, 1, false); Frame(shell, input, 1, 1, false); Call(host, "OpenMaterials");
                    NativeQuickItemChecks.Until(() => { tick(); Prepare(shell, graphics); return ((BlueprintMaterialSnapshot)Get(popup, "snapshot")).Status == BlueprintMaterialStatus.Exact; });
                    long identity = ((BlueprintProjectionIdentity)Get(placed, "Identity")).ProjectionGeneration;
                    var selector = Get(popup, "selector"); x = (float)Get(selector, "X") + 8; y = (float)Get(selector, "Y") + 8;
                    Frame(shell, input, x, y, false); Frame(shell, input, x, y, true); Frame(shell, input, x, y, false); Prepare(shell, graphics);
                    Require((bool)Get(popup, "RangeExpanded"), "the actual nonempty selector press and release opens its projected choices");
                    var choice = Objects(Get(popup, "visibleScopes")).Single(r => (long)Get(r, "Identity") == identity); var choiceRect = Get(Get(choice, "Element"), "Rect");
                    x = (float)Get(choiceRect, "X") + 8; y = (float)Get(choiceRect, "Y") + (float)Get(choiceRect, "Height") / 2;
                    Frame(shell, input, x, y, false); Prepare(shell, graphics);
                    var scopeHint = Get(Get(popup, "renderer"), "hint");
                    Require((bool)Get(scopeHint, "Visible") && string.Concat(Objects(Get(scopeHint, "Lines")).Select(e => (string)Get(e, "Text"))).Contains("40，70"),
                        "the expanded range retains its real instance-position hint after item-name hints are removed");
                    Frame(shell, input, x, y, false); Frame(shell, input, x, y, true); Frame(shell, input, x, y, false); Prepare(shell, graphics);
                    Require((long)Get(popup, "Selected") == identity && !(bool)Get(popup, "RangeExpanded"), "the actual instance choice release changes the one selector's identity");
                    Require(!(bool)Get(content, "HoldingPending") && (int)Get(content, "Count") == 0 && (float)Get(popup, "scroll") == 0,
                        "changing scope discards retained rows rather than showing another scope's historical bill");
                    Console.WriteLine("PASS material publication UI: real 70-row inventory publisher and Host.Update material→match revocation expose Pending empty slices; old numbers become dashes, ItemType/scroll survive, live 69-row set updates during title drag, fresh 68-row anchor recovers and original Close release still consumes; short-viewport first/last wheel reaches the native hotbar consumer without switching slots, last row fully visible, actual selector choice clears retained rows.");
                }
                catch (Exception error) { failure = error; throw; }
                finally
                {
                    AppDomain.CurrentDomain.FirstChanceException -= trace;
                    foreach (var fault in faults) Console.Error.WriteLine("MATERIAL PUBLICATION FIRST-CHANCE entry=" + fault.Key + Environment.NewLine + fault.Value);
                    RestoreAfterCheck(failure,
                        () => { Call(popup, "Close"); Call(host, "ClearProjection"); Call(host, "CancelStaticMaterialViews"); },
                        () => { field.SetValue(host, original); player.inventory[0] = slot; Call(host, "SetAutoBuild", auto); Main.screenHeight = originalHeight; PlayerInput.CacheOriginalScreenDimensions(); },
                        () => { Frame(shell, input, 1, 1, false); Frame(shell, input, 1, 1, false); });
                }
            }
        }
        private static void BoundaryWheel(object shell, object input, ProbeGraphics graphics, float x, float y, int wheel, int hotbar)
        {
            Frame(shell, input, x, y, false, wheel);
            typeof(Player).GetMethod("HandleHotbarControls", F).Invoke(Main.LocalPlayer, null); Main.LocalPlayer.selectedItemState.Update();
            Require(Main.LocalPlayer.selectedItemState.Hotbar == hotbar && PlayerInput.ScrollWheelDelta == 0 && PlayerInput.ScrollWheelDeltaForUI == 0,
                "an extra wheel at a nonempty material boundary is consumed before the original hotbar consumer");
            Prepare(shell, graphics);
        }
        private static void AssertPendingView(object popup, object content, float scroll)
        {
            Require((bool)Get(content, "HoldingPending") && (int)Get(content, "Count") == 70 && (float)Get(popup, "scroll") == scroll,
                "empty Pending preserves same-scope measured ItemType rows and scroll instead of clamping to the top");
            var labels = Objects(Get(popup, "labels"));
            Require(labels.Any(e => ((string)Get(e, "Text")).Contains("剩余材料 —")), "Pending summary never presents the last exact total as current");
            foreach (var row in Objects(Get(content, "Visible")))
            {
                var glyphs = Objects(Get(row, "Glyphs"));
                Require(glyphs.Count(g => (string)Get(Get(g, "Element"), "Text") == "—") == 3 && glyphs.All(g => (Color)Get(g, "Ink") != Color.LightSalmon),
                    "retained geometry paints all three old quantities as unknown and cannot imply sufficient or missing stock");
            }
        }
        private static void AssertAnchor(object popup, object content, int item, float top, string phase)
        {
            var row = Objects(Get(content, "Visible")).SingleOrDefault(r => (int)Get(Get(r, "Cell"), "ItemType") == item);
            string state = MaterialAnchorState(popup, content, item, top);
            Console.WriteLine("MATERIAL ANCHOR " + phase + " " + state);
            Require(row != null && Math.Abs((float)Get(Get(row, "Rect"), "Y") - top) < .01f, "the surviving ItemType returns at its prior viewport offset after actual publication recovery; phase=" + phase + "; " + state);
        }
        private static string MaterialAnchorState(object popup, object content, int item, float expectedTop)
        {
            var row = Objects(Get(content, "Visible")).SingleOrDefault(r => (int)Get(Get(r, "Cell"), "ItemType") == item);
            var cell = Objects(Get(content, "cells")).SingleOrDefault(r => (int)Get(r, "ItemType") == item);
            var body = Get(popup, "Body"); var snapshot = (BlueprintMaterialSnapshot)Get(popup, "snapshot");
            Func<float, string> number = value => value.ToString("R", CultureInfo.InvariantCulture);
            return "item=" + item + " expectedTop=" + number(expectedTop) + " actualTop=" + (row == null ? "absent" : number((float)Get(Get(row, "Rect"), "Y"))) +
                " localY=" + (cell == null ? "absent" : number((float)Get(Get(cell, "Rect"), "Y"))) + " scroll=" + number((float)Get(popup, "scroll")) +
                " bodyY=" + number((float)Get(body, "Y")) + " bodyHeight=" + number((float)Get(body, "Height")) + " summaryHeight=" + number((float)Get(popup, "summaryHeight")) +
                " contentHeight=" + number((float)Get(content, "Height")) + " count=" + Get(content, "Count") + " holding=" + Get(content, "HoldingPending") +
                " shownStatus=" + snapshot.Status + " shownRows=" + snapshot.Rows.Count;
        }
        private static void LifecycleAndToolbar(object context, ProbeGraphics graphics)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var popup = Get(shell, "BlueprintMaterialsUi"); var input = Get(context, "Input");
            var toolbar = Get(host, "Toolbar"); var player = Main.LocalPlayer;
            var foreground = (Func<IntPtr>)Get(input, "foregroundWindow"); float scale = Main.UIScaleWanted;
            int oldWidth = Main.screenWidth, oldHeight = Main.screenHeight;
            bool auto = (bool)Get(host, "AutoBuildEnabled"), bar = (bool)Get(host, "ToolbarEnabled"); Item slot = player.inventory[0]; int selected = player.selectedItem;
            Exception failure = null;
            try
            {
                EnsureNativeScaleRoom();
                Call(host, "SetAutoBuild", true); Call(shell, "CloseAndSubmitPosition");
                Frame(shell, input, 1, 1, false); Frame(shell, input, 1, 1, false); Call(host, "OpenMaterials"); Prepare(shell, graphics);
                var close = Get(Objects(Get(popup, "buttons"))[0], "Rect"); float x = (float)Get(close, "X") + 8, y = (float)Get(close, "Y") + 8;
                Frame(shell, input, x, y, true); Require(GetOptional(popup, "armed") != null, "a fresh real close press arms the current popup geometry");
                ChangeNativeScale("material-close-release"); Frame(shell, input, x, y, false);
                Require((bool)Get(popup, "Visible") && GetOptional(popup, "armed") == null, "UI matrix change cancels the stale close release before it can act");
                Main.UIScale = scale; PlayerInput.CacheOriginalScreenDimensions(); Prepare(shell, graphics);
                close = Get(Objects(Get(popup, "buttons"))[0], "Rect"); x = (float)Get(close, "X") + 8; y = (float)Get(close, "Y") + 8;
                Frame(shell, input, x, y, true); Set(input, "foregroundWindow", (Func<IntPtr>)(() => IntPtr.Zero)); Frame(shell, input, x, y, true);
                PlayerInput.Triggers.Current.CopyInto(player);
                Require(!(bool)Get(popup, "Visible") && GetOptional(popup, "armed") == null && !player.controlUseItem,
                    "actual focus sample closes the material window and preserves its held world-use tail");
                Set(input, "foregroundWindow", foreground); Frame(shell, input, x, y, true); PlayerInput.Triggers.Current.CopyInto(player);
                Require(!(bool)Get(input, "CanStartActions") && !player.controlUseItem, "held focus return cannot rearm actions or manufacture a release");
                Frame(shell, input, 1, 1, false); Frame(shell, input, 1, 1, false); Call(host, "OpenMaterials"); Prepare(shell, graphics);
                close = Get(Objects(Get(popup, "buttons"))[0], "Rect"); x = (float)Get(close, "X") + 8; y = (float)Get(close, "Y") + 8;
                Frame(shell, input, x, y, true); FrameCore(shell, input, x, y, true, 0, Array.Empty<Keys>(), true); PlayerInput.Triggers.Current.CopyInto(player);
                Require((bool)Get(input, "NativeUiTransitionRequest"), "the same raw native mapping records its map request before pointer-action filtering");
                Require(!(bool)Get(popup, "Visible") && GetOptional(popup, "armed") == null && !player.controlUseItem && (bool)Get(host, "AutoBuildEnabled"),
                    "the real sampled map request closes viewing, cancels armed and consumes the tail without changing automatic-build intent");
                Require(!player.mapFullScreen && !Main.mapFullscreen, "cancelling the owned modal does not restore its consumed map trigger or pretend the native map already opened");
                Call(input, "BeginUpdate"); Require(!(bool)Get(input, "NativeUiTransitionRequest"), "an update with no mapping cannot reuse the previous native transition request");
                Frame(shell, input, 1, 1, false); Frame(shell, input, 1, 1, false);
                Require(!(bool)Get(input, "NativeUiTransitionRequest"), "a later neutral mapping does not inherit the old transition request");
                Set(input, "foregroundWindow", (Func<IntPtr>)(() => IntPtr.Zero)); FrameCore(shell, input, 1, 1, false, 0, Array.Empty<Keys>(), true);
                PlayerInput.Triggers.Current.CopyInto(player);
                Require(!(bool)Get(input, "CanStartActions") && !player.mapFullScreen && !Main.mapFullscreen && !player.controlUseItem,
                    "an unfocused mapped map request cannot restore a native action through the new UI observation");
                Set(input, "foregroundWindow", foreground); Frame(shell, input, 1, 1, false); Frame(shell, input, 1, 1, false);
                PlayerInput.Triggers.Current.CopyInto(player);
                Require(!(bool)Get(input, "NativeUiTransitionRequest") && !player.mapFullScreen, "trusted neutral focus return never replays the old map request");
                FrameCore(shell, input, 1, 1, false, 0, Array.Empty<Keys>(), true); PlayerInput.Triggers.Current.CopyInto(player);
                Require(player.mapFullScreen && !(bool)Get(popup, "Visible"), "a later independent world map request reaches the original CopyInto consumer again");
                bool oldMapEnabled = Main.mapEnabled, oldMap = Main.mapFullscreen, oldReset = Main.resetMapFull, oldInventory = Main.playerInventory, oldReleaseInventory = player.releaseInventory;
                float oldMapScale = Main.mapFullscreenScale; var oldPylon = Main.MapPylonTile; string oldBuff = Main.buffString;
                try
                {
                    Main.mapEnabled = true; player.TryOpeningFullscreenMap();
                    Require(Main.mapFullscreen, "the original Player.Update map-branch outlet opens the map from the independent request; this is a selected native stage, not complete Player.Update or OS input");
                }
                finally
                {
                    Main.mapEnabled = oldMapEnabled; Main.mapFullscreen = oldMap; Main.resetMapFull = oldReset; Main.playerInventory = oldInventory; player.releaseInventory = oldReleaseInventory;
                    Main.mapFullscreenScale = oldMapScale; Main.MapPylonTile = oldPylon; Main.buffString = oldBuff;
                }
                Frame(shell, input, 1, 1, false); Frame(shell, input, 1, 1, false);
                Call(host, "SetToolbar", true); player.inventory[0] = new Item(); player.inventory[0].SetDefaults(Terraria.ID.ItemID.Gel);
                player.selectedItemState.Select(0); player.selectedItemState.Update();
                DrawCurrentViewport(graphics, () => Call(toolbar, "Draw"));
                Require((bool)Get(host, "ShowToolbar"), "actual held gel and preference display the material toolbar entry");
                var layout = Get(toolbar, "Layout"); var panel = Get(layout, "Panel"); int materialIndex = (int)Get(layout, "MaterialsIndex");
                var button = Get(Objects(Get(layout, "Buttons"))[materialIndex], "Rect");
                x = (float)Get(panel, "X") + (float)Get(button, "X") + (float)Get(button, "Width") / 2;
                y = (float)Get(panel, "Y") + (float)Get(button, "Y") + (float)Get(button, "Height") / 2;
                Frame(shell, input, x, y, false); DrawCurrentViewport(graphics, () => Call(toolbar, "Draw"));
                var clip = Get(toolbar, "visibleClip");
                Console.WriteLine("MATERIAL TOOLBAR screen=" + Main.screenWidth + "x" + Main.screenHeight + " panelY=" + Get(panel, "Y") + " panelHeight=" + Get(panel, "Height") +
                    " clipY=" + Get(clip, "Y") + " clipHeight=" + Get(clip, "Height") + " pointer=" + x + "," + y + " rawScale=" + PlayerInput.RawMouseScale);
                Require((bool)Call(toolbar, "HitCurrent"), "the actual gel material point hits the current toolbar inside its real screen-sized drawing target");
                Frame(shell, input, x, y, true); PlayerInput.Triggers.Current.CopyInto(player);
                Require(!player.controlUseItem, "actual gel material press cannot become a native gel use");
                Frame(shell, input, x, y, false); PlayerInput.Triggers.Current.CopyInto(player); Prepare(shell, graphics);
                Require((bool)Get(popup, "Visible") && Get(popup, "Source").ToString() == "World" && !(bool)Get(Get(shell, "State"), "Visible") && !player.controlUseItem,
                    "actual gel material button opens the shared world popup and consumes the opening release");
                Call(popup, "Close"); Require((bool)Get(host, "AutoBuildEnabled"), "closing only materials retains automatic-build intent");
                Frame(shell, input, 1, 1, false); Frame(shell, input, 1, 1, false); Call(host, "Toggle");
                DrawCurrentViewport(graphics, () => Call(toolbar, "Draw"));
                Require(((BlueprintCaptureState)Get(host, "State")).Active && (int)Get(layout, "MaterialsIndex") == -1 &&
                    Objects(Get(layout, "Buttons")).Select(b => (string)Get(b, "Text")).SequenceEqual(new[] { "保存蓝图", "退出采集" }),
                    "the actual capture toolbar has exactly Save/Exit and no material action");
                button = Get(Objects(Get(layout, "Buttons"))[1], "Rect"); panel = Get(layout, "Panel");
                x = (float)Get(panel, "X") + (float)Get(button, "X") + (float)Get(button, "Width") / 2;
                y = (float)Get(panel, "Y") + (float)Get(button, "Y") + (float)Get(button, "Height") / 2;
                Frame(shell, input, x, y, false); DrawCurrentViewport(graphics, () => Call(toolbar, "Draw"));
                Frame(shell, input, x, y, true); Frame(shell, input, x, y, false); PlayerInput.Triggers.Current.CopyInto(player);
                Require(!((BlueprintCaptureState)Get(host, "State")).Active && !(bool)Get(popup, "Visible") && !player.controlUseItem,
                    "the real Exit release exits capture through its owner and cannot open Materials or leak a world use");
                Frame(shell, input, 1, 1, false); Frame(shell, input, 1, 1, false); Call(host, "Toggle");
                DrawCurrentViewport(graphics, () => Call(toolbar, "Draw")); panel = Get(layout, "Panel"); button = Get(Objects(Get(layout, "Buttons"))[1], "Rect");
                x = (float)Get(panel, "X") + (float)Get(button, "X") + 8; y = (float)Get(panel, "Y") + (float)Get(button, "Y") + 8;
                Frame(shell, input, x, y, false); DrawCurrentViewport(graphics, () => Call(toolbar, "Draw")); Frame(shell, input, x, y, true);
                Call(host, "Toggle"); DrawCurrentViewport(graphics, () => Call(toolbar, "Draw"));
                Frame(shell, input, x, y, false); PlayerInput.Triggers.Current.CopyInto(player);
                Require(!((BlueprintCaptureState)Get(host, "State")).Active && !(bool)Get(popup, "Visible") && !(bool)Get(Get(shell, "BlueprintUi"), "Submenu") && !player.controlUseItem,
                    "a mode change after press invalidates the old capture slot before its release can call a replacement world action");
                player.inventory[0] = new Item(); player.inventory[0].SetDefaults(Terraria.ID.ItemID.Wood);
                Require(!(bool)Get(host, "ShowToolbar"), "changing the real held item removes gel-only entry eligibility");
            }
            catch (Exception error) { failure = error; throw; }
            finally
            {
                RestoreAfterCheck(failure,
                    () => { Set(input, "foregroundWindow", foreground); Main.screenWidth = oldWidth; Main.screenHeight = oldHeight; PlayerInput.CacheOriginalScreenDimensions(); Main.UIScale = scale; Call(popup, "Close"); },
                    () => { player.inventory[0] = slot; player.selectedItemState.Select(selected); player.selectedItemState.Update(); },
                    () => { Call(host, "SetToolbar", bar); Call(host, "SetAutoBuild", auto); },
                    () => { Frame(shell, input, 1, 1, false); Frame(shell, input, 1, 1, false); });
            }
        }
        private static void ClickF5(object shell, object input, ProbeGraphics graphics, string command)
        {
            Prepare(shell, graphics); var state = Get(shell, "State"); var layout = Get(state, "Layout");
            var element = Objects(Get(layout, "Elements")).Single(e => Get(e, "Command").ToString() == command); var rect = Get(element, "Rect");
            Call(state, "ScrollTo", Math.Max(0, (float)Get(rect, "Y") - 20)); Prepare(shell, graphics);
            var viewport = Get(layout, "Viewport"); float x = (float)Get(state, "X") + (float)Get(viewport, "X") + (float)Get(rect, "X") + (float)Get(rect, "Width") / 2;
            float y = (float)Get(state, "Y") + (float)Get(viewport, "Y") + (float)Get(rect, "Y") + (float)Get(rect, "Height") / 2 - (float)Get(state, "Scroll");
            Frame(shell, input, x, y, false); Frame(shell, input, x, y, true); Frame(shell, input, x, y, false);
            Require(Get(state, "Command").ToString() == command, "the actual current F5 row release dispatches " + command);
        }
        internal static void Visual(object context, ProbeGraphics graphics, string output)
        {
            Directory.CreateDirectory(output);
            NativeBlueprintDraftMaterialsChecks.Visual(context, graphics, output);
            NativeBlueprintMaterialGeometryChecks.Run(context);
            Input(context, graphics, output);
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var popup = Get(shell, "BlueprintMaterialsUi");
            Call(host, "OpenMaterials"); Prepare(shell, graphics);
            var pixels = graphics.Pixels(() => Call(popup, "Draw"), Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
            Require(pixels.Any(p => p.A > 0 && p.R > 100 && p.G > 100), "actual common material renderer produces native font/surface pixels");
            Directory.CreateDirectory(output); graphics.Image(Path.Combine(output, "blueprint-world-materials.png"), () => Call(popup, "Draw"), Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
            Call(popup, "Close");
            DrawValues(context, graphics, output);
            StaticAssembly(context, graphics, output);
        }
        private static void StaticAssembly(object context, ProbeGraphics graphics, string output)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var state = Get(shell, "State");
            var ui = Get(shell, "BlueprintUi"); var renderer = Get(shell, "renderer"); var popup = Get(shell, "BlueprintMaterialsUi");
            var library = (LibraryState)Get(host, "Library"); var flow = (BlueprintCaptureFlow)Get(host, "Flow");
            Exception failure = null;
            using (NativeBlueprintWorldMatchChecks.EmptyWorld(host, 120, 120))
            try
            {
                Call(shell, "CloseAndSubmitPosition"); NativeBlueprintWorldMatchChecks.InitializeMetadata(); Framing.Initialize(); Main.SetupTileMerge();
                Main.tile[40, 40].wall = Terraria.ID.WallID.Wood; Main.tile[41, 40].wall = Terraria.ID.WallID.Stone;
                graphics.LoadItemTextures(new[] { (int)Terraria.ID.ItemID.WoodWall, (int)Terraria.ID.ItemID.StoneWall });
                NativeBlueprintLibraryVisualChecks.Capture(context, 41, 40, 40);
                Call(ui, "ShowCapture"); Prepare(shell, graphics);
                var sealedSummary = flow.Summary; var handle = flow.ContentHandle; string draftKey = (string)Call(ui, "DraftKey");
                Action step = () => { NativeQuickItemChecks.BeginWorldStep(); ObservedCall(context, "UpdateRuntime"); Prepare(shell, graphics); };
                BlueprintMaterialSnapshot bill = null;
                NativeQuickItemChecks.Until(() =>
                {
                    step(); bill = (BlueprintMaterialSnapshot)Call(host, "MaterialDraft");
                    return bill.Status == BlueprintMaterialStatus.Exact && GetOptional(ui, "draftMaterials") is BlueprintMaterialSnapshot shown &&
                        shown.Status == BlueprintMaterialStatus.Exact && Call(Get(ui, "previews"), "Summary", draftKey) != null;
                });
                Require(bill.KindCount == 2 && bill.Total == 2 && bill.Rows.Count == 2 && bill.Rows.All(r => r.Required == 1) &&
                    bill.Rows.Any(r => r.ItemType == Terraria.ID.ItemID.WoodWall) && bill.Rows.Any(r => r.ItemType == Terraria.ID.ItemID.StoneWall),
                    "the actual fixed two-wall capture independently derives one Wood Wall and one Stone Wall");
                Require(ReferenceEquals(flow.Summary, sealedSummary) && flow.ContentHandle.Equals(handle) && (string)Call(ui, "DraftKey") == draftKey,
                    "waiting for exact materials keeps the same sealed source and exclusion key");
                var grid = Get(ui, "captureMaterials");
                Require((int)Get(grid, "Count") == 2 && !(bool)Get(grid, "HoldingPending") && Objects(Get(grid, "cells")).All(c =>
                    Objects(Get(c, "Glyphs")).Any(g => (string)Get(Get(g, "Element"), "Text") == "1")),
                    "the real capture page assembles the complete material grid including both explicit one counts");
                const string summary = "材料 2 种 · 总计 2";
                Require(string.Concat(Objects(Get(ui, "parts")).Select(p => (string)Get(Get(p, "Element"), "Text"))).Contains(summary),
                    "the real fixed capture page assembles both exact material aggregates");
                Call(state, "ScrollTo", float.MaxValue); Prepare(shell, graphics);
                Require(Objects(Get(grid, "Visible")).Length == 2 && (long)Get(Get(ui, "materialRenderer"), "IconLoads") >= 2,
                    "both real capture material icons and counts reach the actual scrollable body");
                Action drawPage = () => { Call(renderer, "Draw", state, Main.UIScaleMatrix, false, false); Call(ui, "Draw"); };
                graphics.Image(Path.Combine(output, "blueprint-capture-materials-exact.png"), drawPage, Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
                NativeBlueprintMaterialNameChecks.Capture(context, graphics);
                graphics.Image(Path.Combine(output, "blueprint-capture-materials-hover.png"), drawPage, Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
                var save = Objects(Get(ui, "parts")).Single(p => Get(p, "Command").ToString() == "ConfirmDraft");
                Require((bool)Get(save, "Enabled"), "the real exact material grid preserves the original current-key Save eligibility");
                Call(ui, "Execute", save);
                NativeQuickItemChecks.Until(() => { step(); return flow.Status == BlueprintCaptureStatus.Saved; });
                Guid savedId = flow.SelectedAssetId.Value; BlueprintMaterialSnapshot savedBill = null;
                NativeQuickItemChecks.Until(() =>
                {
                    step(); var page = library.Snapshot.Page;
                    return page != null && page.Entries.Any(e => e.AssetId == savedId) &&
                        ((IDictionary<Guid, BlueprintMaterialSnapshot>)Get(ui, "pageMaterials")).TryGetValue(savedId, out savedBill) && savedBill.Status == BlueprintMaterialStatus.Exact;
                });
                Require(savedBill.KindCount == 2 && savedBill.Total == 2 && savedBill.Rows.All(r => r.Required == 1),
                    "the reliably saved file derives the same complete static bill in the actual library page");
                var parts = Objects(Get(ui, "parts")); var card = parts.Single(p => (bool)Get(p, "Card") && (Guid)Get(p, "AssetId") == savedId); var cardRect = Get(Get(card, "Element"), "Rect");
                string cardText = string.Concat(parts.Where(p =>
                {
                    var r = Get(Get(p, "Element"), "Rect");
                    return (float)Get(r, "X") >= (float)Get(cardRect, "X") && (float)Get(r, "Right") <= (float)Get(cardRect, "Right") &&
                        (float)Get(r, "Y") >= (float)Get(cardRect, "Y") && (float)Get(r, "Bottom") <= (float)Get(cardRect, "Bottom");
                }).Select(p => (string)Get(Get(p, "Element"), "Text")));
                Require(cardText.Contains(summary), "the actual saved asset card contains both exact material aggregates inside its own measured card");
                Call(state, "ScrollTo", Math.Max(0, (float)Get(cardRect, "Y") - 48)); Prepare(shell, graphics);
                graphics.Image(Path.Combine(output, "blueprint-library-materials-exact.png"), drawPage, Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
                var materials = Objects(Get(ui, "parts")).Single(p => Get(p, "Command").ToString() == "Materials" && (Guid)Get(p, "AssetId") == savedId);
                Require((bool)Get(materials, "Enabled"), "the original saved card material button is enabled for this real asset");
                Call(ui, "Execute", materials); Prepare(shell, graphics);
                var detail = (BlueprintMaterialSnapshot)Get(popup, "snapshot");
                Require((bool)Get(popup, "Visible") && !(bool)Get(popup, "WorldMode") && detail.Status == BlueprintMaterialStatus.Exact && detail.KindCount == 2 && detail.Total == 2,
                    "the original card material action assembles the same exact bill in the common static detail window");
                NativeBlueprintMaterialNameChecks.Standalone(context, graphics);
                ImagesAtScales(shell, popup, graphics, output, "blueprint-library-materials-detail-exact");
                Console.WriteLine("PASS Blueprint static material assembly: actual native two-wall Capture/DraftKey, exact worker bill, real capture grid/count-one/two aggregates, reliable Save, own library card aggregates and common static detail, with actual native-resource Draws. No injected Flow/snapshot; OS/IME/owner acceptance NOT_EXECUTED.");
            }
            catch (Exception error) { failure = error; throw; }
            finally
            {
                RestoreAfterCheck(failure,
                    () => { Call(popup, "Close"); Call(shell, "CloseAndSubmitPosition"); Call(host, "CancelStaticMaterialViews"); },
                    () => { Call(host, "CancelCaptureOrDraft"); ((BlueprintCaptureState)Get(host, "State")).Exit(); });
            }
        }
        private static void LibraryReturn(object context, ProbeGraphics graphics)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var state = Get(shell, "State"); var ui = Get(shell, "BlueprintUi"); var popup = Get(shell, "BlueprintMaterialsUi");
            var field = host.GetType().GetField("<Library>k__BackingField", F); var original = (LibraryState)field.GetValue(host);
            Call(shell, "CloseAndSubmitPosition");
            using (var library = new LibraryState(new FileBlueprintLibrary(Path.Combine(Terraria.Program.SavePath, "material-return-" + Guid.NewGuid().ToString("N")))))
            {
                field.SetValue(host, library);
                try
                {
                    var entry = NativeBlueprintPlacementChecks.Seed(library, 2, 2, "材料详情返回"); Call(ui, "OpenLibrary");
                    NativeQuickItemChecks.Until(() =>
                    { Call(host, "Poll"); Call(shell, "AfterUpdate"); return !(bool)Get(ui, "dirty") && Objects(Get(ui, "parts")).Any(p => Get(p, "Command").ToString() == "Materials"); });
                    Call(ui, "BeginEdit", entry.AssetId, "尚未提交的名称"); Call(shell, "AfterUpdate");
                    var editor = Get(ui, "Editor"); float scroll = (float)Get(state, "Scroll"); object selected = GetOptional(ui, "selected");
                    var materials = Objects(Get(ui, "parts")).Single(p => Get(p, "Command").ToString() == "Materials" && (Guid)Get(p, "AssetId") == entry.AssetId);
                    Call(ui, "Execute", materials); Prepare(shell, graphics);
                    Require((bool)Get(popup, "Visible") && Get(popup, "Source").ToString() == "Library" && !(bool)Get(popup, "WorldMode"), "the existing card material button opens the common static mode");
                    Call(popup, "Close");
                    Require((bool)Get(ui, "Visible") && ReferenceEquals(editor, Get(ui, "Editor")) && Equals(selected, GetOptional(ui, "selected")) && scroll == (float)Get(state, "Scroll") &&
                        (string)Get(editor, "Text") == "尚未提交的名称", "static detail close retains actual library page, scroll, selection and uncommitted name buffer");
                    Call(shell, "CloseAndSubmitPosition");
                }
                finally { Call(popup, "Close"); Call(ui, "Suspend"); Call(host, "CancelStaticMaterialViews"); field.SetValue(host, original); }
            }
        }
        private static void DrawValues(object context, ProbeGraphics graphics, string output)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var state = Get(shell, "State"); var assembly = host.GetType().Assembly;
            var layoutType = assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintMaterialsLayout", true);
            var renderer = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintMaterialsRenderer", true), true);
            var layout = Activator.CreateInstance(layoutType, true); var rectType = assembly.GetType("JueMingR.TerrariaHost.F5.F5Rect", true);
            var measureType = layoutType.GetMethod("Prepare", F).GetParameters()[3].ParameterType;
            var measure = Delegate.CreateDelegate(measureType, Get(state, "Layout"), Get(state, "Layout").GetType().GetMethod("DynamicTextSize", F));
            var values = new BlueprintMaterialSnapshot(100, BlueprintMaterialStatus.Exact,
                new[] { new BlueprintMaterialRow(2, 4000000000, long.MaxValue, true), new BlueprintMaterialRow(3, 1, 1, true), new BlueprintMaterialRow(9, 100, 80, true) }, true);
            graphics.LoadItemTextures(values.Rows.Select(row => row.ItemType));
            Func<int, string> names = id => Lang.GetItemNameValue(id) + (id == 2 ? new string('材', 40) : "");
            float oldScale = Main.UIScaleWanted; int oldWidth = Main.screenWidth, oldHeight = Main.screenHeight; int identity = 0;
            try
            {
                foreach (float scale in new[] { 1f, 1.2f })
                foreach (bool world in new[] { false, true })
                {
                    Main.screenWidth = 1280; Main.screenHeight = 900; PlayerInput.CacheOriginalScreenDimensions(); Main.UIScale = scale;
                    Call(Get(shell, "renderer"), "RefreshResources"); Call(Get(shell, "renderer"), "Prepare", state, 1280f, 900f, scale);
                    VerifyActualScale("values-" + world, scale);
                    Require((bool)Call(renderer, "Refresh"), "material actual native font/surface are available");
                    Call(layout, "Prepare", values, world, 522f, measure, names, 1L, (int)Get(renderer, "Generation"), 0f);
                    var clip = Activator.CreateInstance(rectType, F, null, new object[] { 40f, 40f, 522f, 260f }, null);
                    Call(layout, "Project", 40f, 40f, clip); Call(renderer, "Prepare", layout, ++identity);
                    long loads = (long)Get(renderer, "IconLoads"), sets = (long)Get(renderer, "VisibleSetBuilds");
                    for (int i = 0; i < 120; i++) Call(renderer, "Prepare", layout, identity);
                    Require(loads == (long)Get(renderer, "IconLoads") && sets == (long)Get(renderer, "VisibleSetBuilds"), "120 prepared native-resource frames reload no icons or visible sets");
                    Action draw = () => Call(renderer, "DrawContent", layout, Main.UIScaleMatrix, clip);
                    var pixels = graphics.Pixels(draw, Main.UIScaleMatrix, 1280, 900);
                    Require(loads > 0 && pixels.Any(p => p.A > 0), "real borrowed item icons and exact prepared material glyphs reach the GPU consumer");
                    graphics.Image(Path.Combine(output, "blueprint-material-values-" + world + "-" + scale.ToString(CultureInfo.InvariantCulture) + ".png"), draw, Main.UIScaleMatrix, 1280, 900);
                }
                Console.WriteLine("PASS Blueprint materials actual GPU adapter: common static/dynamic value views, original item icons, long counts/names, UI 100/120%, stable 120 visible-resource preparations. Values are UI fixtures; native mapping/inventory, OS/IME and owner experience are separate.");
            }
            finally { ((IDisposable)renderer).Dispose(); Main.screenWidth = oldWidth; Main.screenHeight = oldHeight; PlayerInput.CacheOriginalScreenDimensions(); Main.UIScale = oldScale; }
        }
        private static void ImagesAtScales(object shell, object popup, ProbeGraphics graphics, string output, string name)
        {
            float original = Main.UIScaleWanted; int oldWidth = Main.screenWidth, oldHeight = Main.screenHeight;
            var input = Get(shell, "inputState");
            imageSurface = Get(Get(popup, "renderer"), "surface");
            var audit = new Harmony("JueMingR.Tests.MaterialImageMatrix");
            audit.Patch(AccessTools.Method(imageSurface.GetType(), "Pass"), prefix: new HarmonyMethod(typeof(NativeBlueprintMaterialsUiChecks), nameof(BeforeImagePass)));
            Exception failure = null;
            try
            {
                foreach (float scale in new[] { 1f, 1.2f })
                {
                    // UIScale's original setter clamps against PlayerInput's
                    // cached real viewport. Publish a legal viewport first;
                    // a filename alone is no evidence that 120% was accepted.
                    Main.screenWidth = 1280; Main.screenHeight = 900; PlayerInput.CacheOriginalScreenDimensions(); Main.UIScale = scale;
                    // ProcessInput freezes the native matrix for this UI frame.
                    // AfterUpdate alone would keep the preceding shell matrix.
                    Frame(shell, input, 1, 1, false); Prepare(shell, graphics); VerifyActualScale(name, scale);
                    VerifyPreparedMatrix(shell, popup);
                    expectedImageMatrix = Main.UIScaleMatrix; imagePasses = 0; imageMatrixMismatch = false; mismatchedImageMatrix = default(Matrix);
                    graphics.Image(Path.Combine(output, name + "-" + scale.ToString(CultureInfo.InvariantCulture) + ".png"), () => Call(popup, "Draw"), Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
                    Console.WriteLine("MATERIAL IMAGE " + name + " scale=" + scale.ToString("R", CultureInfo.InvariantCulture) +
                        " shell=" + MatrixScale((Matrix)Get(shell, "matrix")) + " popup=" + MatrixScale((Matrix)Get(popup, "matrix")) +
                        " renderer=" + MatrixScale(actualImageMatrix) + " expected=" + MatrixScale(expectedImageMatrix) +
                        " passes=" + imagePasses + " mismatch=" + imageMatrixMismatch + " firstMismatch=" + MatrixScale(mismatchedImageMatrix));
                    Require(imagePasses >= 2 && !imageMatrixMismatch, "the actual material panel/content GPU passes consume the same current matrix as native input and prepared geometry");
                }
            }
            catch (Exception error) { failure = error; throw; }
            finally
            {
                RestoreAfterCheck(failure,
                    () =>
                    {
                        Main.screenWidth = oldWidth; Main.screenHeight = oldHeight; PlayerInput.CacheOriginalScreenDimensions(); Main.UIScale = original;
                        expectedImageMatrix = Main.UIScaleMatrix; imagePasses = 0; imageMatrixMismatch = false; mismatchedImageMatrix = default(Matrix);
                        Frame(shell, input, 1, 1, false); Prepare(shell, graphics); VerifyPreparedMatrix(shell, popup);
                        Require(imagePasses >= 2 && !imageMatrixMismatch, "restoring the actual viewport and native sample restores the material renderer matrix too");
                    },
                    () => { imageSurface = null; audit.UnpatchAll(audit.Id); });
            }
        }
        private static void BeforeImagePass(object __instance, Matrix matrix)
        {
            if (!ReferenceEquals(__instance, imageSurface)) return;
            imagePasses++; actualImageMatrix = matrix;
            if (matrix != expectedImageMatrix) { if (!imageMatrixMismatch) mismatchedImageMatrix = matrix; imageMatrixMismatch = true; }
        }
        private static void VerifyPreparedMatrix(object shell, object popup)
        {
            Require((Matrix)Get(shell, "matrix") == Main.UIScaleMatrix && (Matrix)Get(popup, "matrix") == Main.UIScaleMatrix,
                "the real sampled shell and prepared material popup use the current native matrix before drawing: main=" + MatrixScale(Main.UIScaleMatrix) +
                " shell=" + MatrixScale((Matrix)Get(shell, "matrix")) + " popup=" + MatrixScale((Matrix)Get(popup, "matrix")));
        }
        private static string MatrixScale(Matrix value)
        { return value.M11.ToString("R", CultureInfo.InvariantCulture) + "/" + value.M22.ToString("R", CultureInfo.InvariantCulture); }
        private static void VerifyActualScale(string view, float requested)
        {
            Require(Main.UIScale == requested && Main.UIScaleMatrix.M11 == requested && Main.UIScaleMatrix.M22 == requested &&
                PlayerInput.OriginalScreenSize.X == Main.screenWidth && PlayerInput.OriginalScreenSize.Y == Main.screenHeight,
                "the original native scale setter accepts the requested material view scale in its actual cached viewport");
            Console.WriteLine("MATERIAL VIEW " + view + " requested=" + requested.ToString("R", CultureInfo.InvariantCulture) +
                " actual=" + Main.UIScale.ToString("R", CultureInfo.InvariantCulture) + " matrix=" + MatrixScale(Main.UIScaleMatrix) +
                " real=" + PlayerInput.RealScreenWidth + "x" + PlayerInput.RealScreenHeight + " worldZoom=" + Main.GameViewMatrix.Zoom);
        }
        internal static void EnsureNativeScaleRoom()
        {
            if (Main.instance.UIScaleMax > 1f) return;
            Main.screenWidth = Math.Max(1280, Main.screenWidth); Main.screenHeight = Math.Max(900, Main.screenHeight);
            PlayerInput.CacheOriginalScreenDimensions(); Main.UIScale = Main.UIScaleWanted;
        }
        internal static void ChangeNativeScale(string scenario)
        {
            var before = Main.UIScaleMatrix;
            float requested = Main.UIScale == 1f ? Math.Min(1.2f, Main.instance.UIScaleMax) : 1f;
            Main.UIScale = requested;
            Console.WriteLine("MATERIAL SCALE " + scenario + " before=" + before.M11.ToString("R", CultureInfo.InvariantCulture) +
                " requested=" + requested.ToString("R", CultureInfo.InvariantCulture) + " actual=" + Main.UIScale.ToString("R", CultureInfo.InvariantCulture) +
                " matrix=" + Main.UIScaleMatrix.M11.ToString("R", CultureInfo.InvariantCulture) + " real=" + PlayerInput.RealScreenWidth + "x" + PlayerInput.RealScreenHeight);
            Require(before != Main.UIScaleMatrix && Main.UIScaleMatrix.M11 == Main.UIScale && Main.UIScaleMatrix.M22 == Main.UIScale,
                "the original native setter changes the actual UI matrix before the stale gesture's next sample");
        }
        internal static void Prepare(object shell, ProbeGraphics graphics)
        {
            ObservedCall(shell, "AfterUpdate");
            Require(!(bool)Get(shell, "Failed"), "material production shell remains healthy after F5Shell.AfterUpdate");
            DrawCurrentViewport(graphics, () => ObservedCall(Get(shell, "BlueprintMaterialsUi"), "Draw"));
        }
        private static void DrawCurrentViewport(ProbeGraphics graphics, Action draw)
        {
            // Render's shared cost canvas is 960x640. The accumulated visual
            // scope can leave a larger real UI viewport, whose bottom controls
            // must actually be inside the target before testing their input.
            graphics.Pixels(draw, Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
        }
        private static object ObservedCall(object target, string method, params object[] args)
        {
            string previous = observedEntry; observedEntry = target.GetType().Name + "." + method;
            try { return Call(target, method, args); }
            finally { observedEntry = previous; }
        }
        private static void RestoreAfterCheck(Exception primary, params Action[] actions)
        {
            Exception firstCleanup = null;
            foreach (var action in actions)
                try { action(); }
                catch (Exception error)
                {
                    if (firstCleanup == null) firstCleanup = error;
                    Console.Error.WriteLine("MATERIAL UI CLEANUP supplemental; primary retained=" + (primary != null) + Environment.NewLine + error);
                }
            // A cleanup-only failure still fails the scope; it cannot turn a
            // successful body into PASS or replace an already thrown first fault.
            if (primary == null && firstCleanup != null) ExceptionDispatchInfo.Capture(firstCleanup).Throw();
        }
        private static void SetBinding(HotkeyBindings bindings, string id, HotkeyChord chord)
        {
            NativeQuickItemChecks.Until(() => { bindings.Poll(); return !bindings.Busy; });
            long command; string reason;
            Require(bindings.TrySet(id, chord, null, out command, out reason), "isolated action binding saves through the common document: " + reason);
            NativeQuickItemChecks.Until(() => { bindings.Poll(); return !bindings.Busy; });
            Require(Equals(bindings.Get(id), chord), "isolated action binding publishes after the reliable write");
        }
        internal static void Frame(object shell, object input, float x, float y, bool left, int wheel = 0, params Keys[] keys)
        { FrameCore(shell, input, x, y, left, wheel, keys, false); }
        private static void FrameCore(object shell, object input, float x, float y, bool left, int wheel, Keys[] keys, bool mapRequest)
        {
            Main.LocalPlayer.mouseInterface = Main.mouseText = false; FocusHelper.IsSelectedApplication = true;
            PlayerInput.MouseInfo = new MouseState((int)(x * Main.UIScale), (int)(y * Main.UIScale), 0, left ? ButtonState.Pressed : ButtonState.Released,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Main.mouseLeft = left; Main.mouseRight = false; Call(input, "BeginUpdate"); Call(input, "AfterNativeMouse", new List<string>(left ? new[] { "Mouse1" } : Array.Empty<string>()));
            PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.MouseLeft = left; PlayerInput.Triggers.Current.MapFull = mapRequest; PlayerInput.Triggers.Update();
            PlayerInput.ScrollWheelDelta = PlayerInput.ScrollWheelDeltaForUI = wheel;
            Call(input, "AfterMapping");
            // Original DoUpdate_HandleInput refreshes keyState after native
            // mapping, which may have cleared its previous UI-owned sample.
            Main.keyState = new KeyboardState(keys);
            Call(input, "AfterKeyboardRefresh"); ObservedCall(shell, "ProcessInput");
            Require(!(bool)Get(shell, "Failed"), "material production shell remains healthy after F5Shell.ProcessInput");
            ObservedCall(Get(shell, "blueprint"), "ProcessInput");
            Require(!(bool)Get(shell, "Failed"), "material production shell remains healthy for the actual sample");
        }
        private static object[] Objects(object values) { return ((IEnumerable)values).Cast<object>().ToArray(); }
    }
}
