using System;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Creative;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static void PreciseSolidBackedWalls(object context, object host, object tools, object input)
        {
            bool mouse = Main.SmartCursorWanted_Mouse, pad = Main.SmartCursorWanted_GamePad;
            try
            {
                foreach (bool automatic in new[] { false, true })
                {
                    var p = Reset(context, host, tools, input); p.position = new Vector2(47 * 16, 646); p.velocity = Vector2.Zero;
                    p.inventory[12].SetDefaults(ItemID.WoodWall); p.inventory[12].stack = 8;
                    var points = new[] { new Point(43, 38), new Point(47, 38), new Point(51, 38) };
                    foreach (var point in points) WorldGen.PlaceWall(point.X, point.Y + 1, WallID.Wood, true);
                    WorldGen.PlaceTile(51, 38, TileID.Stone, true, true);
                    // The nearby non-goal wall has native smart support. The
                    // third actual goal is solid-backed and must not be replaced
                    // by this smart-eligible neighbour or filled as a side hole.
                    WorldGen.PlaceWall(52, 39, WallID.Wood, true);
                    Main.SmartCursorWanted_Mouse = automatic; Main.SmartCursorWanted_GamePad = automatic;
                    long unknown = (long)Get(host, "BuildUnknownResults");
                    if (automatic)
                    {
                        Place(context, host, input, Asset(host, points.Select(point => new CaptureCell { X = point.X - 43, Layers = CaptureLayer.Wall, WallType = WallID.Wood }).ToArray()), 43, 38);
                        SetAutomatic(host, input, true);
                        Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"));
                    }
                    else
                    {
                        p.selectedItemState.Select(12); Step(context, input);
                        foreach (var point in points)
                            NativeQuickItemChecks.Until(() =>
                            {
                                NativeToolExecutionChecks.Sample(context, input, new Vector2(point.X * 16 + 8, point.Y * 16 + 8), true);
                                NativeQuickItemChecks.BeginWorldStep(); p.Update(0); Call(context, "UpdateRuntime");
                                return Main.tile[point.X, point.Y].wall == WallID.Wood;
                            });
                        NativeToolExecutionChecks.Sample(context, input, new Vector2(480, 540), false);
                    }
                    Require(points.All(point => Main.tile[point.X, point.Y].wall == WallID.Wood) && p.inventory[12].stack == 5 && Main.tile[52, 38].wall == 0,
                        "the same original manual/automatic consumer covers all three separated wall goals, including solid-backed wall, without a non-goal placement");
                    Require(Main.tile[51, 38].active() && Main.tile[51, 38].type == TileID.Stone && (long)Get(host, "BuildUnknownResults") == unknown,
                        "existing solid is usable native support and stays untouched; no artificial unknown consumption");
                    Require(Main.SmartCursorWanted_Mouse == automatic && Main.SmartCursorWanted_GamePad == automatic,
                        "automatic precision returns both original smart preferences after natural completion");
                    Console.WriteLine("PASS BlueprintNativeTarget solid-backed separated walls auto=" + automatic + " actualGoals=3 consumed=3 nonGoal=0 smartPreferencesReturned=true");
                }
            }
            finally { Main.SmartCursorWanted_Mouse = mouse; Main.SmartCursorWanted_GamePad = pad; SetAutomatic(host, input, false); }
        }

        private static object rejectedHost, rejectedUse;
        private static bool rejectAtConsumer, rejectTarget;
        private static int rejectedCount;
        private static long rejectedGeneration;
        private static void ChangeResolvedTarget()
        {
            var flight = rejectedHost == null ? null : GetOptional(rejectedHost, "activeBuild");
            if (!rejectTarget || flight == null || (int)Get(Get(flight, "Target"), "WorldX") != 43 || (bool)Get(flight, "Rejected")) return;
            Player.tileTargetX = 44; Player.tileTargetY = 38;
        }
        private static void ChangeConsumerTarget(bool wall) { if (rejectAtConsumer && wall) ChangeResolvedTarget(); }
        private static void ChangeLookupTarget() { if (!rejectAtConsumer) ChangeResolvedTarget(); }
        private static void ObserveFinalRejection()
        {
            var flight = GetOptional(rejectedHost, "activeBuild");
            if (flight == null || !(bool)Get(flight, "Rejected")) return;
            rejectedCount++; rejectedGeneration = (long)Get(flight, "Generation");
            Require(!(bool)Get(flight, "Checked"), "final coordinate rejection is not forged into a returned NativeChecked receipt");
        }
        private static void FinalTargetRejection(object context, object host, object tools, object input, bool consumer)
        {
            var p = Reset(context, host, tools, input); p.position = new Vector2(47 * 16, 646); p.velocity = Vector2.Zero;
            p.inventory[12].SetDefaults(ItemID.WoodWall); p.inventory[12].stack = 8;
            foreach (int x in new[] { 43, 44, 50 }) WorldGen.PlaceWall(x, 39, WallID.Wood, true);
            Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Wall, WallType = WallID.Wood },
                new CaptureCell { X = 7, Layers = CaptureLayer.Wall, WallType = WallID.Wood }), 43, 38);
            rejectedHost = host; rejectedUse = Get(tools, "Use"); rejectAtConsumer = consumer; rejectTarget = true; rejectedCount = 0;
            var patch = new Harmony("JueMingR.Tests.BlueprintFinalTarget");
            patch.Patch(rejectedUse.GetType().GetMethod("AfterTargetResolution", F), prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(ChangeLookupTarget)));
            patch.Patch(rejectedUse.GetType().GetMethod("AllowBuildPlacement", F), prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(ChangeConsumerTarget)));
            patch.Patch(host.GetType().GetMethod("RejectAutomaticBuildTarget", F), postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(ObserveFinalRejection)));
            long unknown = (long)Get(host, "BuildUnknownResults");
            try
            {
                SetAutomatic(host, input, true);
                Until(context, input, () => Main.tile[50, 38].wall == WallID.Wood);
                Require(rejectedCount == 1 && rejectedGeneration > 0 && Main.tile[43, 38].wall == 0 && Main.tile[44, 38].wall == 0 && p.inventory[12].stack == 7,
                    "one final rejection ends its old flight and lets the independent goal consume; the wrong coordinate spends nothing and cannot monopolize old/new tokens");
                Require((long)Get(host, "BuildUnknownResults") == unknown, "zero-spend final rejection is separate from actual unknown consumption");
                // Remove only the simulated competing target adjustment. The
                // existing original-rejection wake must recover without motion,
                // editing the projection or toggling construction off/on.
                rejectTarget = false;
                Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(rejectedUse, "Active"));
                Require(Main.tile[43, 38].wall == WallID.Wood && Main.tile[44, 38].wall == 0 && p.inventory[12].stack == 6 && rejectedCount == 1,
                    "bounded eligibility retry restores the rejected unfinished goal and releases its native source naturally");
                Console.WriteLine("PASS BlueprintNativeTarget rejection boundary=" + (consumer ? "consumer" : "resolution") + " rejectedFlight=" + rejectedGeneration + " rejectionCount=1 successorConsumed=1 recoveredConsumed=1 wrongCoordinateConsumed=0 unknown=0");
            }
            finally
            {
                rejectTarget = false; foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                rejectedHost = rejectedUse = null; SetAutomatic(host, input, false);
            }
        }

        private static void NativeReachChanges(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 10;
            p.inventory[13].SetDefaults(ItemID.Rope); p.inventory[13].stack = 10;
            int oldMode = Main.GameMode; byte oldDifficulty = p.difficulty;
            var far = CreativePowerManager.Instance.GetPower<CreativePowers.FarPlacementRangePower>(); bool oldFar = far.IsEnabledForPlayer(p.whoAmI);
            p.ClearBuff(BuffID.Builder); Main.GameMode = 0; far.SetEnabledState(p.whoAmI, false); p.ResetEffects();
            int left, top, right, bottom; TileReachCheckSettings.Simple.GetTileRegion(p, out left, out top, out right, out bottom, p.inventory[12].tileBoost + p.blockRange);
            int ropeX = right + 3, buffX = right + 1, journeyX = right + 8;
            var position = p.position;
            try
            {
                Require(p.inventory[13].tileBoost == 3 && p.IsInTileInteractionRange(ropeX, 42, TileReachCheckSettings.Simple, p.inventory[13].tileBoost + p.blockRange) &&
                    !p.IsInTileInteractionRange(buffX, 42, TileReachCheckSettings.Simple, p.inventory[12].tileBoost + p.blockRange),
                    "actual catalog-supported Rope has its native boost while the shorter StoneBlock remains out of range");
                Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone },
                    new CaptureCell { X = 1, Layers = CaptureLayer.Tile, TileType = TileID.Stone },
                    new CaptureCell { X = 3, Layers = CaptureLayer.Tile, TileType = TileID.Rope },
                    new CaptureCell { X = 8, Layers = CaptureLayer.Tile, TileType = TileID.Stone }), right, 42);
                SetAutomatic(host, input, true);
                Until(context, input, () => Main.tile[right, 42].active() && Main.tile[ropeX, 42].active());
                Until(context, input, () => ProgressDormant(Scene(host).Single()));
                Require(!Main.tile[buffX, 42].active() && !Main.tile[journeyX, 42].active() && p.inventory[12].stack == 9 && p.inventory[13].stack == 9,
                    "the shared broad envelope does not lend Rope's reach to the actual shorter stone consumer");
                p.AddBuff(BuffID.Builder, 36000);
                Until(context, input, () => Main.tile[buffX, 42].active());
                Require(p.position == position && p.blockRange > 0 && p.inventory[12].stack == 8,
                    "the original Builder buff wakes the exhausted source and player wait at the same standing position");
                p.ClearBuff(BuffID.Builder); Main.GameMode = 3; p.difficulty = 3; far.SetEnabledState(p.whoAmI, true);
                Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"));
                Require(p.position == position && Main.tile[journeyX, 42].active() && p.inventory[12].stack == 7 &&
                    p.IsInTileInteractionRange(journeyX, 42, TileReachCheckSettings.Simple, p.inventory[12].tileBoost + p.blockRange),
                    "actual Journey range effects resume the same scene without movement, placement edits or a construction toggle");
                Console.WriteLine("PASS BlueprintNativeReach actualStoneBoundary=" + right + " actualRopeBoost=3 buffTarget=" + buffX + " journeyTarget=" + journeyX + " stationary=true consumedStone=3 consumedRope=1");
                SetAutomatic(host, input, false);
                Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone }), journeyX + 1, 42);
                SetAutomatic(host, input, true); AwaitPrepared(context, host, input);
                far.SetEnabledState(p.whoAmI, false); p.ResetEffects(); Step(context, input);
                Require(!Main.tile[journeyX + 1, 42].active() && p.inventory[12].stack == 7,
                    "shrinking actual Journey reach revokes an already Ready target before it can consume");
                far.SetEnabledState(p.whoAmI, true);
                Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"));
                Require(p.position == position && p.inventory[12].stack == 6, "re-expanding Journey reach restores that same unfinished goal");

                // A separate component counterexample covers a source field
                // changing without inventory publication. This is deliberately
                // not evidence that normal StoneBlock changes boost in play.
                SetAutomatic(host, input, false); Main.GameMode = 0; far.SetEnabledState(p.whoAmI, false); p.ResetEffects();
                Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone }), right + 2, 42);
                SetAutomatic(host, input, true); Until(context, input, () => ProgressDormant(Scene(host).Single()));
                var unchangedInventory = Get(host, "MaterialInventory"); long revision = (long)Get(host, "MaterialBuildEligibilityRevision");
                long reads = (long)Get(host, "BuildReachSourceReads");
                for (int i = 0; i < 12; i++) Step(context, input);
                Require((long)Get(host, "BuildReachSourceReads") - reads <= 12 * 50 && Get(host, "MaterialInventory") == unchangedInventory,
                    "steady active reach observation is bounded by the existing 50 primary slots and does not republish unchanged inventory");
                p.inventory[12].tileBoost = 2;
                Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"));
                Require((long)Get(host, "MaterialBuildEligibilityRevision") > revision && p.inventory[12].stack == 5 && p.position == position,
                    "component field change wakes exact-source Player wait/NearCursor even though Rope preserves the larger broad envelope");
                Console.WriteLine("PASS BlueprintNativeReach shrinkingReadyRevoked=true sourceFieldInvalidation=component-only unchangedMaxEnvelope=true workPerUpdate<=50");
            }
            finally
            {
                SetAutomatic(host, input, false); p.inventory[12].tileBoost = 0; p.ClearBuff(BuffID.Builder); far.SetEnabledState(p.whoAmI, oldFar); Main.GameMode = oldMode; p.difficulty = oldDifficulty; p.ResetEffects();
            }
        }
    }
}
