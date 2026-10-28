using System;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        // These declared metadata objects use legacy .8 Player/WorldGen paths,
        // not custom TileObject.CanPlace. Reference writers run at 60/70 only;
        // every asserted destination body is written by natural Player.Update.
        private static void LegacyTorchAttachments(object context, object host, object tools, object input)
        {
            for (int mode = 0; mode < 6; mode++)
            {
                var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.Torch); p.inventory[12].stack = 2;
                LegacyTorchSupport(60, mode == 1 || mode == 2 ? 0 : mode);
                Require(LegacyTorchOracle(p, 60, 42), "original torch gate/writer accepts the independent source support mode=" + mode);
                int frame = Main.tile[60, 42].frameX % 66;
                Require(frame == (mode == 4 ? 22 : 0) && (mode < 4 || Main.tile[60, 43].slope() == 1),
                    "native attachment priority records the expected bottom/left/wall form without flattening an already usable side or wall");
                var entry = LegacyCapture(context, host, input, "原版火把依托 " + mode);
                LegacyTorchSupport(44, mode);
                if (mode == 1 || mode == 2)
                {
                    LegacyTorchSupport(70, mode);
                    Require(!LegacyTorchOracle(p, 70, 42) && !Main.tile[70, 42].active(),
                        "the original Player/WorldGen pair rejects a half platform or a platform used only as a side attachment");
                }
                var placed = Place(context, host, input, entry, 44, 42);
                long started = (long)Get(host, "BuildUsesStarted"), checks = (long)Get(host, "BuildCandidateChecks");
                using (var trace = new LegacyTorchTraceScope(host, tools, p, placed, mode))
                {
                    SetAutomatic(host, input, true);
                    if (mode == 1 || mode == 2)
                    {
                        Until(context, input, () => (long)Get(host, "BuildCandidateChecks") > checks);
                        for (int i = 0; i < 64; i++) Step(context, input);
                        Require(!Main.tile[44, 42].active() && p.inventory[12].stack == 2 && (long)Get(host, "BuildUsesStarted") == started &&
                            (mode == 1 ? Main.tile[44, 43].halfBrick() : !Main.tile[44, 43].active()),
                            "known invalid platform attachment causes no empty use, material consumption, or unsolicited support flattening");
                        if (mode == 1) Require(WorldGen.SlopeTile(44, 43, 0, true, true), "original hammer writer restores the platform top");
                        else LegacyReplaceTile(44, 43, TileID.Platforms);
                        Require(WorldGen.TopEdgeCanBeAttachedTo(44, 43), "restored platform has an original attachable top edge");
                    }
                    else if (mode >= 3)
                    {
                        AwaitPrepared(context, host, input);
                        Require(Main.tile[44, 43].slope() == 1 && !Main.tile[44, 42].active() && p.inventory[12].stack == 2,
                            "a Ready query predicts the original torch form without flattening or writing the actual destination");
                        LegacyTorchTraceEvent("prepared");
                    }
                    PreflightRecovered(context, input, () => Scene(host).Length == 0);
                    string facts = LegacyTorchTraceFacts(); LegacyTorchTraceEvent("final");
                    Require(Main.tile[44, 42].active() && Main.tile[44, 42].type == TileID.Torches && Main.tile[44, 42].frameX % 66 == frame &&
                        p.inventory[12].stack == 1 && (long)Get(host, "BuildUsesStarted") == started + 1 &&
                        (mode < 3 || Main.tile[44, 43].slope() == (mode == 3 ? 0 : 1)),
                        "one actual torch consumption keeps native platform, flattening, side/wall priority and exact captured attachment mode=" + mode + " " + facts);
                    Console.WriteLine("Blueprint legacy torch mode=" + mode + " nativeFrame=" + frame + " actualConsumed=1 supportSlope=" + Main.tile[44, 43].slope());
                }
            }
        }

        private static void LegacyTorchInterruptedFlattening(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.Torch); p.inventory[12].stack = 2;
            LegacyTorchSupport(60, 3); Require(LegacyTorchOracle(p, 60, 42), "interrupted torch has an independently legal original flattened source");
            var entry = LegacyCapture(context, host, input, "整平后支撑消失的火把"); LegacyTorchSupport(44, 3);
            var placed = Place(context, host, input, entry, 44, 42);
            long started = (long)Get(host, "BuildUsesStarted"), unknown = (long)Get(host, "BuildUnknownResults");
            using (var trace = new LegacyTorchTraceScope(host, tools, p, placed, 3, true))
            {
                SetAutomatic(host, input, true); AwaitPrepared(context, host, input);
                Require(Main.tile[44, 43].slope() == 1 && !Main.tile[44, 42].active(), "interrupted torch is prepared without pre-flattening its real support");
                Until(context, input, () => legacyTorchInterruptedChoice.HasValue);
                Require(legacyTorchInterrupted && legacyTorchFlattenedAtInterruption && legacyTorchSupportRemoved && legacyTorchInterruptedChoice == false &&
                    !Main.tile[44, 42].active() && !Main.tile[44, 43].active() && p.inventory[12].stack == 2 && Scene(host).Contains(placed) &&
                    (long)Get(host, "BuildUsesStarted") == started + 1 && (long)Get(host, "BuildUnknownResults") == unknown,
                    "the real callback rejects a support removed after same-flight original flattening, before PlaceIt, with no body, spend or unknown: " + LegacyTorchTraceFacts());
                for (int i = 0; i < 64; i++) Step(context, input);
                Require(!Main.tile[44, 42].active() && p.inventory[12].stack == 2 && Scene(host).Contains(placed) &&
                    (long)Get(host, "BuildUsesStarted") == started + 1 && (long)Get(host, "BuildUnknownResults") == unknown,
                    "the known missing support stays a preflight wait after the one interrupted native use");
                LegacyReplaceTile(44, 43, TileID.Stone);
                PreflightRecovered(context, input, () => !Scene(host).Contains(placed));
                Require(Main.tile[44, 42].active() && Main.tile[44, 42].type == TileID.Torches && Main.tile[44, 42].frameX % 66 == 0 &&
                    p.inventory[12].stack == 1 && (long)Get(host, "BuildUsesStarted") == started + 2 && (long)Get(host, "BuildUnknownResults") == unknown,
                    "only the native support writer unlocks a second actual use, one material consumption and full Match retirement");
                Console.WriteLine("PASS Blueprint legacy torch interrupted flattening: originalGateAccepted=true originalFlatten=true nativeKill=true actualChoice=false refusedConsumed=0 unknownDelta=0 restoredConsumed=1");
            }
        }

        // This bounded fixture-only trace reads fields and observer stamps. It
        // never invokes ActionValid/geometry to probe them: those predicates
        // can change build waits. Original gate ordering and results stay live.
        private static object legacyTorchHost, legacyTorchTools, legacyTorchPlaced;
        private static Player legacyTorchPlayer;
        private static int legacyTorchMode, legacyTorchTraceCount;
        private static long legacyTorchUses, legacyTorchNativeChecks, legacyTorchChoices, legacyTorchUnknown;
        private static bool legacyTorchInterruptArmed, legacyTorchAssortedAccepted, legacyTorchInterrupted, legacyTorchFlattenedAtInterruption, legacyTorchSupportRemoved;
        private static bool? legacyTorchInterruptedChoice;
        private static object legacyTorchAssortedFlight;
        private static long legacyTorchAssortedNative;
        private sealed class LegacyTorchTraceScope : IDisposable
        {
            private readonly Harmony patch;
            internal LegacyTorchTraceScope(object host, object tools, Player p, object placed, int mode, bool interruptBeforeChoice = false)
            {
                legacyTorchHost = host; legacyTorchTools = tools; legacyTorchPlayer = p; legacyTorchPlaced = placed;
                legacyTorchMode = mode; legacyTorchTraceCount = 0;
                legacyTorchInterruptArmed = interruptBeforeChoice; legacyTorchAssortedAccepted = legacyTorchInterrupted = legacyTorchFlattenedAtInterruption = legacyTorchSupportRemoved = false;
                legacyTorchInterruptedChoice = null; legacyTorchAssortedFlight = null; legacyTorchAssortedNative = 0;
                legacyTorchUses = (long)Get(host, "BuildUsesStarted"); legacyTorchNativeChecks = (long)Get(host, "BuildNativeChecks");
                legacyTorchChoices = (long)Get(host, "BuildNativeChoices"); legacyTorchUnknown = (long)Get(host, "BuildUnknownResults");
                if (mode < 3) return;
                patch = new Harmony("JueMingR.Tests.LegacyTorchOrdering");
                try
                {
                    patch.Patch(typeof(Player).GetMethod("PlaceThing_Tiles_BlockPlacementForAssortedThings", F),
                        prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(LegacyTorchGateBefore)),
                        postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(LegacyTorchGateAfter)));
                    patch.Patch(typeof(WorldGen).GetMethod(nameof(WorldGen.SlopeTile), F),
                        prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(LegacyTorchSlopeBefore)),
                        postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(LegacyTorchSlopeAfter)));
                    patch.Patch(Get(tools, "Use").GetType().GetMethod("AllowBuildTileChoice", F),
                        prefix: interruptBeforeChoice ? new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(LegacyTorchInterruptChoice)) : null,
                        postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(LegacyTorchChoiceAfter)));
                }
                catch { Dispose(); throw; }
            }
            public void Dispose()
            {
                if (patch != null) foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                legacyTorchHost = legacyTorchTools = legacyTorchPlaced = null; legacyTorchPlayer = null;
                legacyTorchInterruptArmed = legacyTorchAssortedAccepted = legacyTorchInterrupted = legacyTorchFlattenedAtInterruption = legacyTorchSupportRemoved = false;
                legacyTorchAssortedFlight = null; legacyTorchInterruptedChoice = null; legacyTorchAssortedNative = 0;
            }
        }
        private static bool LegacyTorchTracing()
        { return legacyTorchHost != null && Player.tileTargetX == 44 && Player.tileTargetY == 42 && (bool)Get(Get(legacyTorchTools, "Use"), "InNativeUse"); }
        private static void LegacyTorchGateBefore(Player __instance)
        { if (ReferenceEquals(__instance, legacyTorchPlayer) && LegacyTorchTracing()) LegacyTorchTraceEvent("assorted-before"); }
        private static void LegacyTorchGateAfter(Player __instance, bool __result)
        {
            if (!ReferenceEquals(__instance, legacyTorchPlayer) || !LegacyTorchTracing()) return;
            legacyTorchAssortedAccepted = __result; legacyTorchAssortedFlight = GetOptional(legacyTorchHost, "activeBuild");
            legacyTorchAssortedNative = (long)Get(Get(legacyTorchTools, "Use"), "NativeGeneration");
            LegacyTorchTraceEvent("assorted-after allowed=" + __result);
        }
        private static void LegacyTorchSlopeBefore(int __0, int __1)
        { if (__0 == 44 && __1 == 43 && LegacyTorchTracing()) LegacyTorchTraceEvent("slope-before"); }
        private static void LegacyTorchSlopeAfter(int __0, int __1, bool __result)
        { if (__0 == 44 && __1 == 43 && LegacyTorchTracing()) LegacyTorchTraceEvent("slope-postfix result=" + __result); }
        private static void LegacyTorchChoiceAfter(bool __result)
        {
            if (!LegacyTorchTracing()) return;
            if (legacyTorchInterrupted && !legacyTorchInterruptedChoice.HasValue) legacyTorchInterruptedChoice = __result;
            LegacyTorchTraceEvent("native-choice-result=" + __result);
        }
        private static void LegacyTorchInterruptChoice(int __2)
        {
            if (!legacyTorchInterruptArmed || legacyTorchInterrupted || __2 != TileID.Torches || !LegacyTorchTracing() || !legacyTorchAssortedAccepted ||
                !ReferenceEquals(legacyTorchAssortedFlight, GetOptional(legacyTorchHost, "activeBuild")) ||
                legacyTorchAssortedNative != (long)Get(Get(legacyTorchTools, "Use"), "NativeGeneration")) return;
            // Intervene at the actual callback boundary, after the original
            // assorted gate returned. The callback still executes normally;
            // neither its qualification predicates nor its result are faked.
            legacyTorchFlattenedAtInterruption = Main.tile[44, 43].active() && Main.tile[44, 43].type == TileID.Stone &&
                Main.tile[44, 43].slope() == 0 && !Main.tile[44, 43].halfBrick();
            legacyTorchInterrupted = true;
            WorldGen.KillTile(44, 43, false, false, true); legacyTorchSupportRemoved = !Main.tile[44, 43].active();
            LegacyTorchTraceEvent("external-native-support-kill");
        }
        private static void LegacyTorchTraceEvent(string point)
        {
            if (legacyTorchHost == null || legacyTorchTraceCount++ >= 24) return;
            Console.WriteLine("Blueprint legacy torch TRACE mode=" + legacyTorchMode + " point=" + point + " " + LegacyTorchTraceFacts());
        }
        private static object LegacyTorchValue(object value, string name) { return value == null ? null : GetOptional(value, name); }
        private static string LegacyTorchTraceFacts()
        {
            var host = legacyTorchHost; var p = legacyTorchPlayer; var use = Get(legacyTorchTools, "Use");
            var active = GetOptional(host, "activeBuild"); var prepared = GetOptional(host, "preparedBuild");
            var target = LegacyTorchValue(active ?? prepared, "Target"); var range = LegacyTorchValue(target, "Range");
            var match = LegacyTorchValue(legacyTorchPlaced, "Match"); var ranges = LegacyTorchValue(match, "Ranges") as Array;
            if (range == null && ranges?.Length > 0) range = ranges.GetValue(0);
            string support = "unavailable";
            if (range != null)
            {
                var changes = Get(host, "projectionChanges"); object[] values = { Get(range, "Watch"), Get(range, "PartIndex"), 0L };
                if ((bool)changes.GetType().GetMethod("TryGetBuildStamp", F).Invoke(changes, values)) support = values[2].ToString();
            }
            return "body=" + LegacyTorchTile(44, 42) + " support=" + LegacyTorchTile(44, 43) +
                " stock=" + p.inventory[12].type + "/" + p.inventory[12].stack + " usesDelta=" + ((long)Get(host, "BuildUsesStarted") - legacyTorchUses) +
                " nativeChecksDelta=" + ((long)Get(host, "BuildNativeChecks") - legacyTorchNativeChecks) +
                " choicesDelta=" + ((long)Get(host, "BuildNativeChoices") - legacyTorchChoices) + " unknownDelta=" + ((long)Get(host, "BuildUnknownResults") - legacyTorchUnknown) +
                " rangeVersion/raw/current=" + LegacyTorchValue(range, "Version") + "/" + LegacyTorchValue(range, "RawVersion") + "/" + LegacyTorchValue(range, "Current") +
                " targetRange/support=" + LegacyTorchValue(target, "RangeStamp") + "/" + LegacyTorchValue(target, "SupportStamp") + " observerSupport=" + support +
                " activeFlight=" + LegacyTorchFlight(active) + " preparedFlight=" + LegacyTorchFlight(prepared) +
                " interruptionArmed/ran/flat/removed/choice=" + legacyTorchInterruptArmed + "/" + legacyTorchInterrupted + "/" + legacyTorchFlattenedAtInterruption + "/" + legacyTorchSupportRemoved + "/" + legacyTorchInterruptedChoice +
                " useOperation/native/active/inNative=" + Get(use, "Operation") + "/" + Get(use, "NativeGeneration") + "/" + Get(use, "Active") + "/" + Get(use, "InNativeUse") +
                " anim/time=" + p.itemAnimation + "/" + p.itemTime;
        }
        private static string LegacyTorchFlight(object flight)
        {
            if (flight == null) return "null";
            return "[generation=" + Get(flight, "Generation") + " token=" + Get(flight, "Token") + " started/entered/chosen/checked=" +
                Get(flight, "Started") + "/" + Get(flight, "Entered") + "/" + Get(flight, "Chosen") + "/" + Get(flight, "Checked") + "]";
        }
        private static string LegacyTorchTile(int x, int y)
        {
            var tile = Main.tile[x, y]; if (tile == null) return "null";
            return "[active=" + tile.active() + " type=" + tile.type + " frame=" + tile.frameX + "," + tile.frameY +
                " slope/half/inactive=" + tile.slope() + "/" + tile.halfBrick() + "/" + tile.inActive() + " wall=" + tile.wall + "]";
        }

        private static void LegacyTorchSupport(int x, int mode)
        {
            if (mode <= 2)
            {
                WorldGen.KillTile(x, 43, false, false, true);
                if (mode == 2) LegacyReplaceTile(x - 1, 42, TileID.Platforms);
                else
                {
                    LegacyReplaceTile(x, 43, TileID.Platforms);
                    if (mode == 1) Require(WorldGen.PoundTile(x, 43) && Main.tile[x, 43].halfBrick(), "native hammer makes a non-attachable platform half block");
                }
            }
            else
            {
                Require(WorldGen.SlopeTile(x, 43, 1, true, true) && Main.tile[x, 43].slope() == 1, "native hammer creates the sloped Stone torch support");
                if (mode == 4) LegacyReplaceTile(x - 1, 42, TileID.Stone);
                if (mode == 5) WorldGen.PlaceWall(x, 42, WallID.Stone, true);
            }
        }
        private static bool LegacyTorchOracle(Player p, int x, int y)
        {
            // The real preliminary Player gate may flatten this reference
            // support. It is deliberately never called on an assertion target.
            int oldX = Player.tileTargetX, oldY = Player.tileTargetY, selected = p.selectedItem;
            try
            {
                p.selectedItemState.Select(12); p.selectedItemState.Update();
                Require(p.selectedItem == 12 && p.HeldItem.type == ItemID.Torch, "original selection Update commits the oracle's Torch before its native createTile lookup");
                Player.tileTargetX = x; Player.tileTargetY = y;
                bool allowed = (bool)typeof(Player).GetMethod("PlaceThing_Tiles_BlockPlacementForAssortedThings", F).Invoke(p, new object[] { false });
                return allowed && WorldGen.PlaceTile(x, y, TileID.Torches, true, false, Main.myPlayer, p.inventory[12].placeStyle);
            }
            finally { Player.tileTargetX = oldX; Player.tileTargetY = oldY; p.selectedItemState.Select(selected); p.selectedItemState.Update(); }
        }

        private static void LegacyAlchemyEligibility(object context, object host, object tools, object input)
        { LegacyPlantEligibility(context, host, tools, input, 82, Enumerable.Range(0, 7).ToArray()); }
        private static void LegacyDyeEligibility(object context, object host, object tools, object input)
        {
            Reset(context, host, tools, input); Call(host, "SetMaterialViewDemand", true);
            try
            {
                Until(context, input, () => GetOptional(host, "MaterialCatalog") != null);
                // PlaceDye retains its world-generation cactus-fruit branch,
                // but .8 item 1113 declares no placement. Do not invent a use
                // item or claim native consumption for this unavailable body.
                Require(!ContentSamples.ItemsByType.Values.Any(i => i.createTile == 227 && i.placeStyle == 6),
                    "the locked original has no direct item declaration for dye style6");
                var cell = new CaptureCell { Layers = CaptureLayer.Tile, TileType = 227, FrameX = 34 * 6 };
                Require(((BlueprintPlacementCatalog)Get(host, "MaterialCatalog")).Tile(cell) == null,
                    "the current canonical catalog preserves style6 as unsupported instead of assigning another dye material");
                Console.WriteLine("Blueprint legacy dye style=6 nativeBranch=retained directItem=absent canonicalCatalog=unsupported actualConsumption=NOT_RUN");
            }
            finally { Call(host, "SetMaterialViewDemand", false); }
            LegacyPlantEligibility(context, host, tools, input, 227, new[] { 7, 5, 3, 4, 0, 8, 9, 10, 11 });
        }
        private static void LegacyPlantEligibility(object context, object host, object tools, object input, int type, int[] styles)
        {
            Exception first = null;
            foreach (int style in styles)
            {
                try
                {
                    var p = Reset(context, host, tools, input); var item = LegacyPlantItem(type, style);
                    p.inventory[12].SetDefaults(item.type); p.inventory[12].stack = 2;
                    LegacyPlantSupport(60, type, style); LegacyPlantSupport(70, type, style); LegacyPlantSupport(44, type, style);
                    Require(LegacyPlantOracle(60, 42, type, style), "locked original plant writer accepts its independent valid source type/style=" + type + "/" + style);
                    Require(Main.tile[60, 42].frameX == (type == 82 ? 18 : 34) * style && Main.tile[60, 42].frameY == 0,
                        "original legacy writer supplies the real starting frame rather than a fabricated custom-object body");
                    LegacyAssertCatalog(context, host, input, p.inventory[12]);
                    var entry = LegacyCapture(context, host, input, "原版植物资格 " + type + " " + style);
                    string before = NativeBlueprintProjectionVisualChecks.Snapshot(43, 41, 3, 3);
                    Require(LegacyPlantQuery(host, 44, 42, type, style) && before == NativeBlueprintProjectionVisualChecks.Snapshot(43, 41, 3, 3),
                        "the production read-only legacy copy agrees with the original valid writer and writes no actual Tile");
                    LegacyPlantInvalidate(70, type, style, false); LegacyPlantInvalidate(44, type, style, false);
                    Require(!LegacyPlantOracle(70, 42, type, style) && !Main.tile[70, 42].active(),
                        "the independent original writer rejects this exact invalid style condition");
                    before = NativeBlueprintProjectionVisualChecks.Snapshot(43, 41, 3, 3);
                    Require(!LegacyPlantQuery(host, 44, 42, type, style) && before == NativeBlueprintProjectionVisualChecks.Snapshot(43, 41, 3, 3),
                        "the production legacy query preserves the original refusal and the full actual query footprint");
                    var placed = Place(context, host, input, entry, 44, 42);
                    long started = (long)Get(host, "BuildUsesStarted"), checks = (long)Get(host, "BuildCandidateChecks");
                    SetAutomatic(host, input, true); Until(context, input, () => (long)Get(host, "BuildCandidateChecks") > checks);
                    for (int i = 0; i < 64; i++) Step(context, input);
                    Require(!Main.tile[44, 42].active() && p.inventory[12].stack == 2 && (long)Get(host, "BuildUsesStarted") == started && Scene(host).Contains(placed),
                        "known plant refusal keeps its real catalog material and projection without any native use type/style=" + type + "/" + style);
                    LegacyPlantInvalidate(44, type, style, true);
                    Require(LegacyPlantQuery(host, 44, 42, type, style), "only the original support/wall/liquid writer restores the same target's actual qualification");
                    int updates = PreflightRecovered(context, input, () => !Scene(host).Contains(placed));
                    Require(Main.tile[44, 42].active() && Main.tile[44, 42].type == type && Main.tile[44, 42].frameX == (type == 82 ? 18 : 34) * style &&
                        p.inventory[12].stack == 1 && (long)Get(host, "BuildUsesStarted") == started + 1,
                        "real Player.Update consumes the declared plant exactly once, writes the original frame, and retires from current Match");
                    Console.WriteLine("PASS Blueprint legacy plant type=" + type + " style=" + style + " item=" + item.type + " refusedUses=0 actualConsumed=1 recoveryUpdates=" + updates);
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine("LEGACY PLANT type=" + type + " style=" + style + ": " + error);
                    if (first == null) first = error;
                }
            }
            if (first != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(first).Throw();
        }
        private static Item LegacyPlantItem(int type, int style)
        {
            var item = ContentSamples.ItemsByType.Values.Where(i => i.createTile == type && i.placeStyle == style && i.consumable).OrderBy(i => i.type).FirstOrDefault();
            Require(item != null, "the locked original ContentSamples has a consumable direct declaration for " + type + "/" + style);
            return item;
        }
        private static void LegacyAssertCatalog(object context, object host, object input, Item item)
        {
            Call(host, "SetMaterialViewDemand", true);
            try
            {
                Until(context, input, () => GetOptional(host, "MaterialCatalog") != null);
                var catalog = (BlueprintPlacementCatalog)Get(host, "MaterialCatalog");
                var type = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintCaptureSource", true);
                var source = (IBlueprintCaptureSource)Activator.CreateInstance(type, F, null, new object[] { (Func<bool>)(() => true) }, null);
                CaptureCell cell; Require(source.Read(60, 42, out cell), "catalog witness is read through the actual section-gated capture source");
                var descriptor = catalog.Tile(cell, source.Describe(cell)?.Object);
                Require(descriptor != null && descriptor.RequiresObject && descriptor.UseItemType == item.type && descriptor.ConsumeItemType == item.type &&
                    descriptor.TileType == item.createTile && descriptor.Style == item.placeStyle && descriptor.Units == 1,
                    "this native legacy body is actually reachable through the existing exact catalog, with no special test descriptor");
            }
            finally { Call(host, "SetMaterialViewDemand", false); }
        }
        private static bool LegacyPlantOracle(int x, int y, int type, int style)
        {
            if (type == 82) return WorldGen.PlaceAlch(x, y, style);
            WorldGen.PlaceDye(x, y, style); return Main.tile[x, y].active() && Main.tile[x, y].type == type;
        }
        private static bool LegacyPlantQuery(object host, int x, int y, int type, int style)
        { return (bool)host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintPlacementQuery", true).GetMethod("LegacyPlant", F).Invoke(null, new object[] { x, y, type, style }); }
        private static void LegacyPlantSupport(int x, int type, int style)
        {
            if (type == 82)
            {
                int support = new[] { 2, 60, 0, 199, 53, 57, 147 }[style];
                LegacyReplaceTile(x, 44, TileID.Stone); LegacyReplaceTile(x, 43, support);
                if (style >= 4) PreflightLiquidStore(x, 42, 80, (byte)(style == 5 ? 1 : 0));
            }
            else if (style == 7)
            { WorldGen.KillTile(x, 43, false, false, true); LegacyReplaceTile(x, 41, TileID.Stone); }
            else if (style == 6)
            {
                WorldGen.KillTile(x - 1, 43, false, false, true); WorldGen.KillTile(x + 1, 43, false, false, true);
                LegacyReplaceTile(x, 45, TileID.Stone); LegacyReplaceTile(x, 44, TileID.Sand); LegacyReplaceTile(x, 43, TileID.Cactus);
            }
            else if (style == 5 || style >= 8) PreflightLiquidStore(x, 42, (byte)(style == 5 ? 255 : 80), 0);
        }
        private static void LegacyPlantInvalidate(int x, int type, int style, bool restore)
        {
            // Each failure is an original rule boundary; recovery changes the
            // world through its own writer, without touching inventory/asset.
            if (type == 82)
            {
                if (style == 0) PreflightLiquidStore(x, 42, (byte)(restore ? 0 : 1), 0);
                else if (style == 1) LegacyReplaceTile(x, 43, restore ? TileID.JungleGrass : TileID.Stone);
                else if (style == 2)
                { if (restore) Require(WorldGen.SlopeTile(x, 43, 0, true, true), "original slope writer restores the herb's full dirt top"); else Require(WorldGen.PoundTile(x, 43), "original hammer invalidates the dirt top"); }
                else if (style == 3) Wiring.ActuateForced(x, 43);
                else PreflightLiquidStore(x, 42, 80, (byte)(style == 5 ? (restore ? 1 : 0) : (restore ? 0 : 1)));
            }
            else if (style == 7) Wiring.ActuateForced(x, 41);
            else if (style == 6)
            { if (restore) WorldGen.KillTile(x - 1, 43, false, false, true); else LegacyReplaceTile(x - 1, 43, TileID.Stone); }
            else if (style == 5) PreflightLiquidStore(x, 42, (byte)(restore ? 255 : 254), 0);
            else if (style == 3 || style == 4)
            { if (restore) WorldGen.KillWall(x, 42); else WorldGen.PlaceWall(x, 42, WallID.Stone, true); }
            else if (style >= 8)
            { if (restore) WorldGen.KillTile(x, 41, false, false, true); else LegacyReplaceTile(x, 41, TileID.Stone); }
            else PreflightLiquidStore(x, 42, (byte)(restore ? 0 : 1), 0);
        }

        private static void LegacyQueryAvailability(object context, object host, object tools, object input)
        {
            foreach (bool chair in new[] { false, true })
            {
                var p = Reset(context, host, tools, input); LibraryEntry entry;
                if (chair) { p.inventory[12].SetDefaults(ItemID.WoodenChair); entry = PreflightChairAsset(context, host, input); }
                else
                {
                    p.inventory[12].SetDefaults(LegacyPlantItem(82, 2).type); LegacyPlantSupport(60, 82, 2);
                    Require(LegacyPlantOracle(60, 42, 82, 2), "null-query herb has an original legal source");
                    entry = LegacyCapture(context, host, input, "空锚点查询保护"); LegacyPlantSupport(44, 82, 2);
                }
                p.inventory[12].stack = 2; Place(context, host, input, entry, 44, chair ? 41 : 42);
                // Null is test input, never a production writer. Both native
                // CanPlace's Framing and PlaceAlch's early branch can fill it.
                var anchor = Main.tile[44, 43];
                try
                {
                    Main.tile[44, 43] = null;
                    long started = (long)Get(host, "BuildUsesStarted"), checks = (long)Get(host, "BuildCandidateChecks");
                    SetAutomatic(host, input, true); Until(context, input, () => (long)Get(host, "BuildCandidateChecks") > checks);
                    for (int i = 0; i < 64; i++) Step(context, input);
                    Require(Main.tile[44, 43] == null && !Main.tile[44, chair ? 41 : 42].active() && p.inventory[12].stack == 2 && (long)Get(host, "BuildUsesStarted") == started,
                        "unreadable query anchor rejects before Framing/legacy null creation and before any native use; chair=" + chair);
                    LegacyReplaceTile(44, 43, chair ? TileID.Stone : TileID.Dirt);
                    PreflightRecovered(context, input, () => Scene(host).Length == 0);
                    Require(p.inventory[12].stack == 1 && (long)Get(host, "BuildUsesStarted") == started + 1 && Main.tile[44, chair ? 41 : 42].active(),
                        "a native writer restores the formerly null anchor and the same complete target consumes exactly once");
                }
                finally
                {
                    // Failed assertions keep their original exception while
                    // leaving the shared fixture safe for the next Reset.
                    if (Main.tile[44, 43] == null) Main.tile[44, 43] = anchor;
                }
            }

            using (NativeBlueprintWorldMatchChecks.EmptyWorld(host, 400, 160))
            {
                int mode = Main.netMode; var connection = Netplay.Connection; var client = Main.clientPlayer; var buffer = NetMessage.buffer[256];
                try
                {
                    var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(LegacyPlantItem(82, 2).type); p.inventory[12].stack = 2;
                    LegacyPlantSupport(60, 82, 2); Require(LegacyPlantOracle(60, 42, 82, 2), "section-query herb has an original legal source");
                    var entry = LegacyCapture(context, host, input, "跨区查询保护");
                    for (int x = 190; x <= 204; x++) LegacyReplaceTile(x, 43, TileID.Stone);
                    LegacyReplaceTile(199, 43, TileID.Dirt); p.position = new Vector2(195 * 16, 43 * 16 - p.height); p.velocity = Vector2.Zero;
                    Netplay.Connection = new RemoteServer { PendingTermination = true, State = 10 }; Main.clientPlayer = new Player(); NetMessage.buffer[256] = new MessageBuffer();
                    Main.netMode = 1; Main.sectionManager = new WorldSections(2, 2); Main.sectionManager.SetSectionLoaded(0, 0); Call(context, "UpdateRuntime");
                    Place(context, host, input, entry, 199, 42);
                    Require(Main.sectionManager.TileLoaded(199, 42) && !Main.sectionManager.TileLoaded(200, 42),
                        "the body and anchor are received but the actual query halo crosses into an unreceived section");
                    string before = NativeBlueprintProjectionVisualChecks.Snapshot(198, 41, 3, 3);
                    long started = (long)Get(host, "BuildUsesStarted"), checks = (long)Get(host, "BuildCandidateChecks");
                    SetAutomatic(host, input, true); Until(context, input, () => (long)Get(host, "BuildCandidateChecks") > checks);
                    for (int i = 0; i < 64; i++) Step(context, input);
                    Require(!Main.tile[199, 42].active() && p.inventory[12].stack == 2 && (long)Get(host, "BuildUsesStarted") == started &&
                        before == NativeBlueprintProjectionVisualChecks.Snapshot(198, 41, 3, 3),
                        "an unreceived query-only section cannot be read as empty, written, or used to start an otherwise reachable native placement");
                    Main.sectionManager.SetSectionLoaded(1, 0);
                    PreflightRecovered(context, input, () => Scene(host).Length == 0);
                    Require(Main.tile[199, 42].active() && Main.tile[199, 42].type == 82 && Main.tile[199, 42].frameX == 36 &&
                        p.inventory[12].stack == 1 && (long)Get(host, "BuildUsesStarted") == started + 1,
                        "only original section receipt unlocks the same real item and natural native consumer");
                    Console.WriteLine("PASS Blueprint legacy query availability: null original/custom anchors unallocated, received body with unreceived halo refused, original writer/section recovery actualConsumed=1 each");
                }
                finally
                {
                    Call(host, "SetAutoBuild", false); Call(host, "ClearProjection"); Main.netMode = mode; Netplay.Connection = connection; Main.clientPlayer = client; NetMessage.buffer[256] = buffer;
                }
            }
            Call(context, "UpdateRuntime");
        }
        private static LibraryEntry LegacyCapture(object context, object host, object input, string name)
        { return NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, (LibraryState)Get(host, "Library"), () => Step(context, input), 60, 42, 1, 1, name); }
        private static void LegacyReplaceTile(int x, int y, int type)
        {
            if (Main.tile[x, y]?.active() == true) WorldGen.KillTile(x, y, false, false, true);
            int baseType = type == TileID.Grass || type == TileID.CrimsonGrass ? TileID.Dirt : type == TileID.JungleGrass ? TileID.Mud : type;
            Require(WorldGen.PlaceTile(x, y, baseType, true, true), "native support writer places type=" + baseType + " at " + x + "," + y);
            if (baseType != type) Require(WorldGen.PlaceTile(x, y, type, true, true), "native grass writer converts its actual soil substrate");
            Require(Main.tile[x, y].active() && Main.tile[x, y].type == type, "native fixture support retains its actual requested type=" + type);
        }
    }
}
