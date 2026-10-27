using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Fixtures may create the source and supports. The destination body is
    // written only by the production Build -> ToolUse -> natural Player.Update
    // -> ItemCheck -> PlaceThing chain, never by a planner/fake operation port.
    internal static partial class NativeBlueprintBuildChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static object faultTools;
        private static bool reportedNativeFault;
        internal static void Run(object context, bool throughputOnly=false)
        {
            var host = Get(context, "Blueprint"); var tools = Get(context, "Tools"); var input = Get(context, "Input");
            NativeToolExecutionChecks.Initialize(context, null);
            Framing.Initialize(); Main.SetupTileMerge(); TorchID.Initialize();
            // This CPU fixture exercises the real held-torch AddLight queue,
            // not a rendered lighting pass. The original public mode setter
            // selects its already constructed color engine; rebuilding legacy
            // render buffers would require the separate graphics host/camera.
            var lightingMode = Lighting.Mode;
            Lighting.Mode = Terraria.Graphics.Light.LightMode.Color;
            var patch = new Harmony("JueMingR.Tests.BlueprintBuildOutlets");
            faultTools = tools; reportedNativeFault = false;
            patch.Patch(typeof(Player).GetMethod("ItemCheck", F), finalizer: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(NativeFault)));
            foreach (string name in new[] { "HandleSpecialEvent", "HandleMining", "HandleRunning" })
                patch.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name, F), prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SkipOutlet)));
            try
            {
                NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return (bool)Get(host, "ControlsEnabled"); });
                Require(!(bool)Get(host, "AutoBuildEnabled"), "ordinary blueprint construction defaults OFF");
                using (NativeBlueprintWorldMatchChecks.EmptyWorld(host, 200, 160))
                {
                    // Each body begins with the same real reset and owns its
                    // cleanup. Preserve the first failure, but let independent
                    // native consumers report in this one composition too.
                    Exception first = null;
                    Action<string, Action> check = (name, body) =>
                    {
                        try { body(); Console.WriteLine("PASS BlueprintBuild case=" + name); }
                        catch (Exception error) { Console.Error.WriteLine("BUILD CASE " + name + ": " + error); if (first == null) first = error; }
                    };
                    check("sustained native throughput", () => SustainedNativeThroughput(context,host,tools,input));
                    check("pending build input is neutral", () => PendingBuildInput(context,host,tools,input));
                    if(!throughputOnly)
                    {
                    check("missing material fairness", () => MissingMaterialFairness(context, host, tools, input));
                    check("continuous movement candidate fairness", () => ContinuousCandidateProgress(context, host, tools, input, true, false, false));
                    check("continuous unrelated inventory fairness", () => ContinuousCandidateProgress(context, host, tools, input, false, true, false));
                    check("supply restored before active walk tail", () => ContinuousCandidateProgress(context, host, tools, input, true, true, true));
                    check("refill before all-missing active tail", () => RefillBeforeMissingTail(context, host, tools, input));
                    check("cold 300-record prefix continuation", () => BudgetPrefixContinuation(context, host, tools, input, false, false));
                    check("current 300-record prefix continuation", () => BudgetPrefixContinuation(context, host, tools, input, true, false));
                    check("shared budget smaller range continuation", () => BudgetPrefixContinuation(context, host, tools, input, false, true));
                    check("retained source world identity replacement", () => BuildSourceWorldIdentity(context, host, tools, input));
                    check("full source cache continuation", () => BuildSourceFullContinuation(context, host, tools, input));
                    check("mixed large ordinary and object source continuation", () => MixedSourceContinuation(context, host, tools, input));
                    check("legal continuous native opportunity", () => NativeContinuousOpportunity(context, host, tools, input));
                    check("native preflight query isolation", () => PlacementQueryIsolation(context, host, tools, input));
                    check("legacy torch platform and flattening", () => LegacyTorchAttachments(context, host, tools, input));
                    check("legacy torch late support interruption", () => LegacyTorchInterruptedFlattening(context, host, tools, input));
                    check("legacy alchemy native eligibility", () => LegacyAlchemyEligibility(context, host, tools, input));
                    check("legacy dye native eligibility", () => LegacyDyeEligibility(context, host, tools, input));
                    check("query readable section and null protection", () => LegacyQueryAvailability(context, host, tools, input));
                    check("NPC blocker and entity-only recovery", () => PreflightEntityBlocker(context, host, tools, input, false, false));
                    check("remote player blocker and entity-only recovery", () => PreflightEntityBlocker(context, host, tools, input, true, false));
                    check("entity enters after Ready", () => PreflightEntityBlocker(context, host, tools, input, false, true));
                    check("native legal entity overlaps", () => PreflightAllowedEntityOverlap(context, host, tools, input));
                    check("existing wrong support and no extra support", () => PreflightExistingWorldSupport(context, host, tools, input));
                    check("actuated anchor and recovery", () => PreflightActuatedAnchor(context, host, tools, input));
                    check("support disappears after Ready", () => PreflightPreparedSupport(context, host, tools, input));
                    check("world liquid placement qualification", () => PreflightLiquidQualification(context, host, tools, input));
                    check("review platform exact connectivity", () => ReviewPlatformConnectivity(context, host, tools, input));
                    check("review special ordinary cardinal adjacency", () => ReviewOrdinaryAdjacency(context, host, tools, input));
                    check("review legacy liquid and transient qualification", () => ReviewLegacyLiquidEligibility(context, host, tools, input));
                    check("review unknown removal replay and real session", () => ReviewUnknownReplays(context, host, tools, input));
                    check("review unknown complete furniture footprint", () => ReviewUnknownFurniture(context, host, tools, input));
                    check("stable ranges ignore inventory and consumption noise", () => StableRangesIgnoreSupplyNoise(context, host, tools, input));
                    check("exact source owner recovery", () => ProtectedSourceRecovery(context, host, tools, input));
                    check("accessory source recovery", () => AccessorySourceRecovery(context, host, tools, input));
                    check("continuous signals while source waits", () => ProgressWhileSourceWaiting(context, host, tools, input));
                    check("immediate reach and admission recovery", () => ImmediateReachAndAdmissionRecovery(context, host, tools, input));
                    check("pulley direction recovery", () => PulleyDirectionRecovery(context, host, tools, input));
                    check("subpixel native reach recovery", () => SubpixelReachRecovery(context, host, tools, input));
                    check("wall side effects", () => WallSideEffect(context, host, tools, input));
                    check("platform and shape", () => PlatformAndShape(context, host, tools, input));
                    check("original accessories", () => OriginalAccessories(context, host, tools, input));
                    check("materials viewing", () => MaterialsViewing(context, host, tools, input));
                    check("wood chair left", () => Furniture(context, host, tools, input, ItemID.WoodenChair, -1));
                    check("glass chair right", () => Furniture(context, host, tools, input, ItemID.GlassChair, 1));
                    check("wrong native direction alternate and style", () => WrongNativeFurnitureChoice(context, host, tools, input));
                    check("torch and open door", () => TorchAndOpenDoor(context, host, tools, input));
                    check("support and distance", () => SupportAndDistance(context, host, tools, input));
                    check("furniture anchor and closed support cost", () => FurnitureAnchor(context, host, tools, input));
                    check("remote global pylon eligibility and range fairness", () => GlobalPylonEligibility(context, host, tools, input));
                    check("material-only invalidation retains global wait", () => MaterialOnlyBuildEligibility(context, host, tools, input));
                    check("remote global chest capacity eligibility", () => GlobalChestEligibility(context, host, tools, input));
                    check("remote biome style eligibility", () => GlobalBiomeEligibility(context, host, tools, input));
                    check("unknown result excludes eligibility retry", () => UnknownEligibilityProtection(context, host, tools, input));
                    check("stop restart and manual selection", () => OffAndManual(context, host, tools, input));
                    check("stale source and target", () => StaleSourcesAndTargets(context, host, tools, input));
                    check("edit mask deletion and session", () => ProjectionLifecycle(context, host, tools, input));
                    check("background and manual protection", () => BackgroundAndManualProtection(context, host, tools, input));
                    check("native network", () => NetworkProgress(context, host, tools, input));
                    }
                    if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
                }
                Console.WriteLine(throughputOnly?"PASS BlueprintBuildThroughput production natural Player.Update/ItemCheck: complete paired block/wall/platform consumption sequences, nearby sparse prefix/local reentry and neutral Pending. Full build siblings/ordinary startup/real multiplayer/GPU/FPS NOT_RUN.":
                    "PASS BlueprintBuild production natural Player.Update/ItemCheck: real block/wall/chair material consumption, precise style/direction, first-missing fairness, supplement, distance/support resume, stop/restart, manual selection, complete current match retirement. Ordinary startup/real multiplayer/GPU/FPS NOT_RUN.");
            }
            finally
            {
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "SetAutoBuild", false); Call(host, "ClearProjection");
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                faultTools = null;
                Lighting.Clear(); Lighting.Mode = lightingMode;
            }
        }
        private static bool SkipOutlet() { return false; }
        private static Exception NativeFault(Exception __exception)
        {
            if (__exception != null && !reportedNativeFault && faultTools != null && (bool)Get(Get(faultTools, "Use"), "Building"))
            { reportedNativeFault = true; Console.Error.WriteLine("BUILD ORIGINAL FAULT: " + __exception); }
            return __exception;
        }
        private static Player Reset(object context, object host, object tools, object input)
        {
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "SetAutoBuild", false); Call(host, "ClearProjection");
            var state = (BlueprintCaptureState)Get(host, "State"); state.Exit();
            var player = NativeToolExecutionChecks.Reset(context, tools, input, ItemID.CopperPickaxe, 0, 0);
            player.releaseUseItem = true; player.builderAccStatus[10] = 0;
            Call(Get(context, "Shell"), "CloseAndSubmitPosition");
            var library = (LibraryState)Get(host, "Library");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return (bool)Get(host, "ControlsEnabled") && !library.Snapshot.Busy &&
                !(bool)Get(host, "deletePending") && !(bool)Get(host, "discardPending"); });
            return player;
        }
        private static void Step(object context, object input)
        {
            NativeToolExecutionChecks.Sample(context, input, new Vector2(480, 540), false);
            NativeQuickItemChecks.BeginWorldStep(); Main.LocalPlayer.Update(0); Call(context, "UpdateRuntime");
        }
        private static void Until(object context, object input, Func<bool> condition)
        {
            try { NativeQuickItemChecks.Until(() => { Step(context, input); return condition(); }); }
            catch
            {
                var host = Get(context, "Blueprint"); var tools = Get(context, "Tools"); var p = Main.LocalPlayer;
                Console.Error.WriteLine("BUILD STATE enabled=" + Get(host, "AutoBuildEnabled") + " can=" + Call(host, "CanBuildAutomatically", p, false) +
                    " available=" + Get(tools, "Available") + " input=" + Get(input, "CanRunAutomaticActions") + " catalog=" + (GetOptional(host, "MaterialCatalog") != null) +
                    " inventory=" + (GetOptional(host, "MaterialInventory") == null ? "null" : Get(Get(host, "MaterialInventory"), "Ready")) +
                    " candidates=" + Get(host, "BuildCandidateChecks") + " choices=" + Get(host, "BuildNativeChoices") + " checked=" + Get(host, "BuildNativeChecks") + " started=" + Get(host, "BuildUsesStarted") +
                    " use=" + Get(Get(tools, "Use"), "Active") + " selected=" + p.selectedItem + " anim/time/reuse=" + p.itemAnimation + "/" + p.itemTime + "/" + p.reuseDelay +
                    " release=" + p.releaseUseItem + " override=" + p.selectedItemState.HasActiveOverride + " buffered=" + p.selectedItemState.HasBufferedChange + " position=" + p.position +
                    " unknown=" + Get(host, "BuildUnknownResults") + " toolError=" + GetOptional(tools, "Error"));
                foreach (var scene in Scene(host))
                {
                    var match = GetOptional(scene, "Match"); if (match == null) continue;
                    Console.Error.WriteLine("MATCH current=" + Get(match, "Current") + " targets=" + Get(match, "Targets") + " unknown=" + Get(match, "Unknown"));
                    foreach (var range in ((IEnumerable)Get(match, "Ranges")).Cast<object>())
                    {
                        var material = GetOptional(range, "Materials");
                        Console.Error.WriteLine("RANGE current=" + Get(range, "Current") + " reading=" + Get(range, "Reading") + " demanded=" + Get(range, "Demand") +
                            " material=" + (material == null ? "null" : "current=" + Get(material, "Current") + " cursor=" + Get(material, "CandidateCursor") + " exhausted=" + Get(material, "BuildExhausted") + " candidate=" + (GetOptional(material, "Candidate") != null)));
                    }
                }
                throw;
            }
        }
        private static void SetAutomatic(object host, object input, bool value)
        { NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "SetAutoBuild", value); Require((bool)Get(host, "AutoBuildEnabled") == value, "F5/hotkey common automatic preference accepted"); }
        private static object[] Scene(object host) { return ((IEnumerable)Get(host, "ProjectionScene")).Cast<object>().ToArray(); }
        private static LibraryEntry Asset(object host, params CaptureCell[] cells)
        {
            var summary = new CaptureSummary { Width = cells.Max(c => c.X) + 1, Height = cells.Max(c => c.Y) + 1, ContentCount = cells.LongLength };
            var window = new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "build-independent-fixture", 1, 1), 1, summary,
                new BlueprintProjectionBounds(0, 0, summary.Width, summary.Height), cells, Array.Empty<CaptureObject>());
            return NativeBlueprintProjectionVisualChecks.Save((LibraryState)Get(host, "Library"), window);
        }
        private static object Place(object context, object host, object input, LibraryEntry entry, int x, int y)
        { return NativeBlueprintWorldMatchChecks.Place(host, input, entry, x, y, () => Step(context, input)); }
        private static void MissingMaterialFairness(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input);
            p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 5;
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Dirt }, new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = TileID.Stone });
            Place(context, host, input, entry, 42, 42); SetAutomatic(host, input, true);
            Until(context, input, () => Main.tile[44, 42].active());
            Require(Main.tile[44, 42].type == TileID.Stone && !Main.tile[42, 42].active() && p.inventory[12].stack == 4 && Scene(host).Length == 1,
                "first missing dirt yields within the same range; original StoneBlock spends exactly one real source, dirt and projection remain");
            Require(Enumerable.Range(0, 3).All(i => (int)Call(tools, "Mode", i) == 0), "construction runs while all original tool modes are OFF");
            long started = (long)Get(host, "BuildUsesStarted");
            for (int i = 0; i < 80; i++) Step(context, input);
            Require((long)Get(host, "BuildUsesStarted") == started && p.inventory[12].stack == 4, "stable material wait does not repeat native consumption");
            p.inventory[13].SetDefaults(ItemID.DirtBlock); p.inventory[13].stack = 1;
            Until(context, input, () => Scene(host).Length == 0);
            Require(Main.tile[42, 42].active() && Main.tile[42, 42].type == TileID.Dirt && p.inventory[13].IsAir && p.inventory[12].stack == 4,
                "supplemented last dirt uses original tile type zero, real final consumption and existing full current matcher retires the only instance");
        }
        private static void WallSideEffect(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneWall); p.inventory[12].stack = 8;
            // This is host-lifetime telemetry. Earlier cases deliberately
            // create unknown results; only this operation's increment matters.
            long unknown = (long)Get(host, "BuildUnknownResults");
            int wall = p.inventory[12].createWall;
            foreach (var point in new[] { new Point(42, 40), new Point(45, 40), new Point(44, 39), new Point(44, 41) }) WorldGen.PlaceWall(point.X, point.Y, wall);
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Wall, WallType = (ushort)wall });
            Place(context, host, input, entry, 43, 40); SetAutomatic(host, input, true);
            Until(context, input, () => Scene(host).Length == 0);
            Require(Main.tile[43, 40].wall == wall && Main.tile[44, 40].wall == wall && p.inventory[12].stack == 6,
                "natural PlaceThing_Walls_FillEmptySpace fills the independent adjacent hole and consumes two real walls, never restored by old single-consumption guard");
            Until(context, input, () => !(bool)Get(Get(tools, "Use"), "Active"));
            Require(!(bool)Get(Get(Get(tools, "Items"), "Ownership"), "AnyProtected"),
                "the real native animation/selection return releases its source lock after final-instance retirement");
            Require((long)Get(host, "BuildUnknownResults") == unknown, "legal multi-wall consumption does not add an unknown result through source release");
            Console.WriteLine("PASS BlueprintBuild multi-wall actualWalls=2 consumed=2 unknownBefore=" + unknown + " unknownAfter=" + Get(host, "BuildUnknownResults") + "; source released");
        }
        private static void Furniture(object context, object host, object tools, object input, int itemType, int direction)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(itemType); p.inventory[12].stack = 2;
            int type = p.inventory[12].createTile, style = p.inventory[12].placeStyle;
            Require(WorldGen.PlaceObject(60, 42, type, true, style, direction: direction), "source chair is created by original object metadata and anchoring");
            var library = (LibraryState)Get(host, "Library");
            var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, library, () => Step(context, input), 60, 41, 1, 2, "真实椅子" + direction);
            p.direction = -direction;
            Place(context, host, input, entry, 44, 41); SetAutomatic(host, input, true);
            Until(context, input, () => Scene(host).Length == 0);
            Require(Main.tile[44, 41].active() && Main.tile[44, 42].active() && Main.tile[44, 41].type == type && p.inventory[12].stack == 1,
                "one true original furniture item creates both complete members and retires from full Match with captured direction/style=" + style);
            Require((long)Get(host, "BuildNativeChoices") > 0, "real TileObject.CanPlace result passed the production before-PlaceIt canonical identity gate");
        }
        private static void PlatformAndShape(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.WoodPlatform); p.inventory[12].stack = 3;
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Platforms, FrameY = (short)(p.inventory[12].placeStyle * 18) });
            Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
            Until(context, input, () => Scene(host).Length == 0);
            Require(Main.tile[44, 42].active() && Main.tile[44, 42].type == TileID.Platforms && p.inventory[12].stack == 2,
                "true legacy platform placement consumes its own original item and completes exact platform style");
            p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 3;
            entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone, Slope = 1 });
            Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
            Until(context, input, () => Main.tile[44, 42].active());
            for (int i = 0; i < 120; i++) Step(context, input);
            Require(p.inventory[12].stack == 2 && Scene(host).Length == 1 && Main.tile[44, 42].slope() == 0,
                "matching stone body with pending hammer shape is neither charged twice nor falsely retired");
            WorldGen.SlopeTile(44, 42, 1, true, true);
            Until(context, input, () => Scene(host).Length == 0);
            Require(p.inventory[12].stack == 2, "original manual hammer write completes the preserved full shape target without another body use");
        }
        private static void OriginalAccessories(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input);
            try
            {
                p.armor[3].SetDefaults(ItemID.PaintSprayer); p.armor[4].SetDefaults(ItemID.ActuationAccessory);
                p.builderAccStatus[2] = p.builderAccStatus[3] = 0;
                p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 3;
                p.inventory[54].SetDefaults(ItemID.RedPaint); p.inventory[54].stack = 1;
                p.inventory[11].SetDefaults(ItemID.RedPaint); p.inventory[11].stack = 3;
                p.inventory[14].SetDefaults(ItemID.Actuator); p.inventory[14].stack = 2;
                var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile | CaptureLayer.Actuator, TileType = TileID.Stone, TilePaint = 1, Actuator = true });
                Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
                Until(context, input, () => Scene(host).Length == 0);
                Require(Main.tile[44, 42].color() == 1 && Main.tile[44, 42].actuator() && p.inventory[12].stack == 2 && p.inventory[54].IsAir &&
                    p.inventory[11].stack == 3 && p.inventory[14].stack == 1,
                    "real equipped sprayer/presserator preserve vanilla ammo-paint priority and exact accessory consumption under one build token");
                for (int i = 0; i < 50; i++) Step(context, input);
                Require(!(bool)Get(Get(Get(tools, "Items"), "Ownership"), "AnyProtected"), "legal primary/paint/actuator use releases every exact dependency without restoring consumed items");
            }
            finally { p.armor[3].TurnToAir(); p.armor[4].TurnToAir(); }
        }
        private static void MaterialsViewing(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 3;
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone });
            Place(context, host, input, entry, 44, 42); Call(host, "OpenMaterials");
            var shell = Get(context, "Shell"); var popup = Get(shell, "BlueprintMaterialsUi");
            Require((bool)Get(popup, "Visible") && !(bool)Get(Get(shell, "State"), "Visible"), "world material viewing opens the common modal without an empty F5 shell");
            SetAutomatic(host, input, true);
            Until(context, input, () => Scene(host).Length == 0);
            Require(Main.tile[44, 42].active() && p.inventory[12].stack == 2 && (bool)Get(host, "AutoBuildEnabled"),
                "material modal's actual CopyInto/interaction/ItemCheck gates preserve exactly the admitted automatic press and consume a real block");
            Call(popup, "Close");
            p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 3;
            var missing = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Dirt });
            Place(context, host, input, missing, 44, 42); Place(context, host, input, entry, 45, 42);
            var viewed = ((System.Collections.Generic.IReadOnlyList<BlueprintMaterialInstance>)Get(host, "MaterialInstances"))[0];
            Call(host, "OpenMaterials"); Require((bool)Call(popup, "SelectScope", viewed.ProjectionGeneration), "the common material view selects the missing-material instance");
            SetAutomatic(host, input, true);
            Until(context, input, () => Main.tile[45, 42].active() && Scene(host).Length == 1);
            Require(!Main.tile[44, 42].active() && p.inventory[12].stack == 2 && (long)Get(popup, "Selected") == viewed.ProjectionGeneration &&
                ((BlueprintProjectionIdentity)Get(Scene(host)[0], "Identity")).ProjectionGeneration == viewed.ProjectionGeneration,
                "viewing one missing-material instance reserves no inventory and cannot exclude another fixed instance from actual native use and natural retirement");
            Call(popup, "Close");
        }
        private static void SupportAndDistance(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 3;
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone });
            Place(context, host, input, entry, 44, 40); SetAutomatic(host, input, true);
            for (int i = 0; i < 100; i++) Step(context, input);
            Require(!Main.tile[44, 40].active() && p.inventory[12].stack == 3, "unsupported in-range body neither spends nor claims success");
            Require(WorldGen.PlaceTile(44, 41, TileID.Stone, true, true), "only the adjacent support changes through the original writer");
            Vector2 standing = p.position;
            Until(context, input, () => Scene(host).Length == 0);
            Require(Main.tile[44, 40].active() && p.inventory[12].stack == 2 && p.position == standing,
                "support-only native change wakes the exhausted target without inventory, player or toggle changes");

            p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 3;
            Place(context, host, input, entry, 51, 42); SetAutomatic(host, input, true);
            for (int i = 0; i < 100; i++) Step(context, input);
            Require(!Main.tile[51, 42].active() && p.inventory[12].stack == 3, "supported body beyond actual vanilla reach does not spend");
            // Stay inside vanilla's 640px world margins and on the existing
            // floor, beside the destination rather than inside its hitbox.
            p.position = new Vector2(46 * 16, 43 * 16 - p.height); p.velocity = Vector2.Zero;
            Until(context, input, () => Scene(host).Length == 0);
            Require(Main.tile[51, 42].active() && p.inventory[12].stack == 2,
                "real position change resumes the same supported target within original reach");
        }
        private static void TorchAndOpenDoor(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.Torch); p.inventory[12].stack = 2;
            Require(WorldGen.PlaceTile(60, 42, TileID.Torches, true, true), "source torch is placed and framed by original legacy writer");
            var library = (LibraryState)Get(host, "Library");
            var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, library, () => Step(context, input), 60, 42, 1, 1, "原版火把");
            Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
            Until(context, input, () => Scene(host).Length == 0);
            Require(Main.tile[44, 42].active() && Main.tile[44, 42].type == TileID.Torches && p.inventory[12].stack == 1,
                "real legacy torch ItemCheck follows original attachment form instead of rejecting every metadata object outside custom placement");

            p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.WoodenDoor); p.inventory[12].stack = 2;
            Require(WorldGen.PlaceTile(60, 39, TileID.Stone, true, true), "source door upper anchor exists");
            Require(WorldGen.PlaceObject(60, 40, TileID.ClosedDoor, true) && WorldGen.OpenDoor(60, 40, -1), "source is a genuine original left-open door with two-column pose");
            entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, library, () => Step(context, input), 59, 40, 2, 3, "开启木门");
            Require(WorldGen.PlaceTile(45, 39, TileID.Stone, true, true), "destination door upper anchor exists");
            Place(context, host, input, entry, 44, 40); SetAutomatic(host, input, true);
            Until(context, input, () => Scene(host).Length == 0);
            Require(Main.tile[45, 40].active() && Main.tile[45, 40].type == TileID.ClosedDoor && !Main.tile[44, 40].active() && p.inventory[12].stack == 1,
                "canonical captured open-door hinge uses one real closed-door placement and current legal door semantics retires the original two-column target");
        }
        private static void FurnitureAnchor(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.WoodenChair); p.inventory[12].stack = 2;
            Require(WorldGen.PlaceObject(60, 42, TileID.Chairs, true), "independent original chair has a real floor anchor");
            var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, (LibraryState)Get(host, "Library"),
                () => Step(context, input), 60, 41, 1, 2, "等待支撑的椅子");
            Place(context, host, input, entry, 44, 39); long initialStarted = (long)Get(host, "BuildUsesStarted"); SetAutomatic(host, input, true);
            for (int i = 0; i < 100; i++) Step(context, input);
            Require(!Main.tile[44, 39].active() && p.inventory[12].stack == 2 && initialStarted == (long)Get(host, "BuildUsesStarted"), "known missing chair anchor is rejected before any original animation or consumption starts");
            long started = (long)Get(host, "BuildUsesStarted");
            for (int i = 0; i < 80; i++) Step(context, input);
            Require(started == (long)Get(host, "BuildUsesStarted"), "stable missing furniture support does not restart original use continuously");
            Vector2 standing = p.position;
            Require(WorldGen.PlaceTile(44, 41, TileID.Stone, true, true), "only the real furniture anchor changes outside its target footprint");
            Until(context, input, () => Scene(host).Length == 0);
            Require(Main.tile[44, 39].type == TileID.Chairs && Main.tile[44, 40].active() && p.inventory[12].stack == 1 && p.position == standing,
                "furniture anchor propagation wakes only the current body candidate and the original full object naturally retires");

            p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 2;
            var block = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone });
            Place(context, host, input, block, 44, 40);
            for (int i = 0; i < 80; i++) Step(context, input);
            long checks = (long)Get(host, "BuildCandidateChecks"), invalidations = (long)Get(host, "ProjectionMatchInvalidations"), reads = (long)Get(host, "ProjectionMatchCellReads");
            Require(WorldGen.PlaceTile(44, 41, TileID.Stone, true, true), "closed-build support changes through the same original writer");
            for (int i = 0; i < 80; i++) Step(context, input);
            Require(checks == (long)Get(host, "BuildCandidateChecks") && invalidations == (long)Get(host, "ProjectionMatchInvalidations") && reads == (long)Get(host, "ProjectionMatchCellReads") && p.inventory[12].stack == 2,
                "closed automatic build adds no support wake, matcher invalidation, sample or native use");
            SetAutomatic(host, input, true); Until(context, input, () => Scene(host).Length == 0);
        }
        private static void OffAndManual(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 8;
            var entry = Asset(host, Enumerable.Range(0, 4).Select(i => new CaptureCell { X = i, Layers = CaptureLayer.Tile, TileType = TileID.Stone }).ToArray());
            Place(context, host, input, entry, 42, 42); SetAutomatic(host, input, true);
            Until(context, input, () => Enumerable.Range(42, 4).Any(x => Main.tile[x, 42].active()));
            SetAutomatic(host, input, false); int remaining = p.inventory[12].stack;
            for (int i = 0; i < 100; i++) Step(context, input);
            Require(p.inventory[12].stack == remaining && Scene(host).Length == 1, "OFF stops new submissions while preserving real partial work and remaining target");
            SetAutomatic(host, input, true);
            Until(context, input, () => (bool)Get(Get(tools, "Use"), "Active"));
            p.inventory[1].SetDefaults(ItemID.CopperPickaxe); p.selectedItemState.Select(1);
            for (int i = 0; i < 2; i++) { NativeToolExecutionChecks.Sample(context, input, new Vector2(480, 540), true); NativeQuickItemChecks.BeginWorldStep(); p.Update(0); Call(context, "UpdateRuntime"); }
            Require(p.selectedItem == 1 || p.selectedItemState.HasBufferedChange, "new native manual selection owns the return; no stale selection restoration");
            Until(context, input, () => Scene(host).Length == 0);
            Require(p.inventory[12].stack == 4 && Enumerable.Range(42, 4).All(x => Main.tile[x, 42].active()), "ON resumes exactly the unfinished original cells with no duplicate consumption");
        }
    }
}
