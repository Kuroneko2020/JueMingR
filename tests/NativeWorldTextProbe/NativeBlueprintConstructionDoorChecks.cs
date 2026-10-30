using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private sealed class DoorNativeAction
        { internal int X, Y; internal string Kind; }
        private static object doorRepairHost;
        private static bool doorRepairWatch;
        private static bool doorWallBillWatch;
        private static bool doorWallStandaloneContext;
        private static readonly List<DoorNativeAction> doorRepairActions = new List<DoorNativeAction>();

        internal static void ConstructionPlacementDoorRepair(object context, object host, object tools, object input)
        {
            var patch = new Harmony("JueMingR.Tests.BlueprintPlacementDoorRepair");
            foreach (string name in new[] { "paintTile", "paintCoatTile", "paintWall", "paintCoatWall" })
                patch.Patch(typeof(WorldGen).GetMethod(name, F, null, new[] { typeof(int), typeof(int), typeof(byte), typeof(bool), typeof(bool) }, null),
                    postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(DoorRepairPainted)));
            var readType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintProjectionMatchRead", true);
            patch.Patch(host.GetType().GetMethod("AddMaterialObjectAppearance", F, null,
                    new[] { readType, typeof(CaptureCell), typeof(BlueprintWorldMatchSample) }, null),
                postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(DoorWallMaterialObserved)));
            doorRepairHost = host;
            try
            {
                Action<string, Action> check = (name, body) => { body(); Console.WriteLine("PASS BlueprintPlacementDoorRepair case=" + name); };
                foreach (int mode in new[] { 0, 1, 2 }) check("closed-source-open-actual-mode" + mode, () => DoorClosedSourceOpenActual(context, host, tools, input, mode));
                check("same-open-pose", () => DoorOpenSourceChangedPose(context, host, tools, input, false, false, false, true));
                check("opposite-open-pose", () => DoorOpenSourceChangedPose(context, host, tools, input, false, false, false));
                check("uniform-open-source-closed-actual", () => DoorOpenSourceChangedPose(context, host, tools, input, true, false, false));
                check("incompatible-color-closed-actual", () => DoorOpenSourceChangedPose(context, host, tools, input, true, true, false));
                check("incompatible-coating-closed-actual", () => DoorOpenSourceChangedPose(context, host, tools, input, true, false, true));
                check("selected-door-wing-wall-restoration", () => DoorMappedWallRestoration(context, host, tools, input, true));
                check("unselected-door-wing-wall-protection", () => DoorMappedWallRestoration(context, host, tools, input, false));
                check("planned-wing-pose-revocation", () => DoorPlanPoseRevocation(context, host, tools, input));
                check("dense-context-prefix-real-continuation", () => DoorDenseContextContinuation(context, host, tools, input));
                Console.WriteLine("PASS BlueprintPlacementDoorRepair: real Capture/Save/Open, closed-to-open six actual members, opposite open roles, uniform collapse and incompatible closed rows, native paint/coating/scraper, independent old-coordinate wall/wire/actuator, mapped-wing wall restoration permission and planned-pose revocation; full Draw/socket/multiplayer/FPS/owner acceptance NOT_RUN");
            }
            finally
            {
                doorRepairWatch = doorWallBillWatch = false; doorRepairHost = null;
                Call(host, "SetMaterialViewDemand", false);
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
            }
        }

        private static void DoorWallMaterialObserved(object __instance, object read, CaptureCell target, BlueprintWorldMatchSample sample)
        {
            if (!doorWallBillWatch || sample.Cell.X != 46 || sample.Cell.Y != 40) return;
            var owner = Get(read, "Owner");
            var batch = (BlueprintProjectionSourcePart)Get(read, "Batch");
            object[] query = { owner, batch, sample.Cell.X, sample.Cell.Y, default(CaptureCell), false };
            bool wallFound = (bool)__instance.GetType().GetMethod("TryCoordinateWall", F, null,
                new[] { owner.GetType(), typeof(BlueprintProjectionSourcePart), typeof(int), typeof(int), typeof(CaptureCell).MakeByRefType(), typeof(bool).MakeByRefType() }, null).Invoke(__instance, query);
            var wall = (CaptureCell)query[4]; bool known = (bool)query[5];
            var accumulator = (BlueprintMaterialAccumulator)Get(read, "MaterialObjectAppearance");
            var works = (System.Collections.IDictionary)Get(__instance, "projectionMatchWork");
            var work = works[Get(owner, "Identity")];
            string parts = work == null ? "none" : string.Join(";", ((System.Collections.IEnumerable)Get(work, "Parts")).Cast<object>()
                .Where(part => part != null).Select(part => Get(part, "Range") + ":object=" + Get(part, "LogicalObjectId") +
                    ":cells=" + ((BlueprintProjectionWindow)Get(part, "Window")).Cells.Count));
            var liveParts = work == null ? Array.Empty<BlueprintProjectionSourcePart>() : ((System.Collections.IEnumerable)Get(work, "Parts")).Cast<BlueprintProjectionSourcePart>().Where(part => part != null).ToArray();
            bool independent = ((System.Collections.IDictionary)Get(owner, "Windows")).Count == 0 &&
                !((System.Collections.IEnumerable)Get(__instance, "buildSources")).Cast<object>().Any() && liveParts.All(part => part.LogicalObjectId > 0);
            if (independent)
            {
                Require(known && wallFound && batch.DoorContexts.Count == 1 && batch.DoorContexts[0].Cells.Count == 9 && wall.WallType == WallID.Stone && wall.WallPaint == 0,
                    "the door part alone retains exact mapped-wing source wall after ordinary source release and without Draw/build retention");
                doorWallStandaloneContext = true;
            }
            Console.WriteLine("DOOR_WALL_SAMPLE coordinate=46,40 wallKnown=" + known + " wallFound=" + wallFound + " contexts=" + batch.DoorContexts.Count + " standalone=" + independent + " sourceWall=" + wall.WallType + ":" + wall.WallPaint +
                " sourceTile=" + target.TilePaint + ":" + target.FullbrightTile + ":" + target.InvisibleTile +
                " actualTile=" + sample.Cell.TilePaint + ":" + sample.Cell.FullbrightTile + ":" + sample.Cell.InvisibleTile +
                " actualWall=" + sample.Cell.WallType + ":" + sample.Cell.WallPaint + " objectBill=" + accumulator.Status + ":" +
                string.Join(";", accumulator.Counts.Select(pair => pair.Key + "=" + pair.Value)) +
                " windows=" + ((System.Collections.IDictionary)Get(owner, "Windows")).Count + " activeParts=" + parts);
        }

        private static void DoorRepairPainted(int __0, int __1, bool __result)
        {
            if (!doorRepairWatch || !__result) return;
            var flight = GetOptional(doorRepairHost, "activeBuild");
            var operation = flight == null ? null : GetOptional(flight, "Operation");
            if (operation == null) return;
            var target = Get(flight, "Target");
            Require((int)Get(target, "WorldX") == __0 && (int)Get(target, "WorldY") == __1 &&
                (GetOptional(operation, "DoorForm") != null || Get(operation, "Kind").ToString() == "PaintWall"),
                "native door appearance belongs to its precise actual-member flight");
            doorRepairActions.Add(new DoorNativeAction { X = __0, Y = __1, Kind = Get(operation, "Kind").ToString() });
        }

        private static Player DoorRepairReset(object context, object host, object tools, object input)
        {
            doorRepairWatch = doorWallBillWatch = doorWallStandaloneContext = false; doorRepairActions.Clear();
            var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
            foreach (var pair in new[] { new[] { 10, ItemID.RedPaint }, new[] { 11, ItemID.BluePaint }, new[] { 12, ItemID.GlowPaint }, new[] { 13, ItemID.EchoCoating } })
            { p.inventory[pair[0]].SetDefaults(pair[1]); p.inventory[pair[0]].stack = 32; }
            p.inventory[14].SetDefaults(ItemID.Paintbrush); p.inventory[15].SetDefaults(ItemID.PaintRoller); p.inventory[16].SetDefaults(ItemID.PaintScraper);
            return p;
        }
        private static void DoorRepairPlace(int hinge, int direction)
        {
            Require(WorldGen.PlaceTile(hinge, 39, TileID.Stone, true, true) && WorldGen.PlaceObject(hinge, 40, TileID.ClosedDoor, true),
                "door has genuine original anchors and original complete closed placement");
            if (direction != 0) Require(WorldGen.OpenDoor(hinge, 40, direction), "actual original OpenDoor establishes the requested occupied pose");
        }
        private static void DoorRepairAppearance(int hinge, int direction, byte hingePaint, byte wingPaint, byte hingeCoating = 0, byte wingCoating = 0)
        {
            for (int row = 0; row < 3; row++)
            {
                WorldGen.paintTile(hinge, 40 + row, hingePaint, false, false);
                if (hingeCoating != 0) WorldGen.paintCoatTile(hinge, 40 + row, hingeCoating, false, false);
                if (direction == 0) continue;
                WorldGen.paintTile(hinge + direction, 40 + row, wingPaint, false, false);
                if (wingCoating != 0) WorldGen.paintCoatTile(hinge + direction, 40 + row, wingCoating, false, false);
            }
        }
        private static LibraryEntry DoorRepairCapture(object context, object host, object input, int direction,
            byte hingePaint, byte wingPaint, byte hingeCoating = 0, byte wingCoating = 0, bool oldWingLayers = false)
        {
            DoorRepairPlace(60, direction);
            DoorRepairAppearance(60, direction, hingePaint, wingPaint, hingeCoating, wingCoating);
            WorldGen.PlaceWall(59, 40, WallID.Stone); WorldGen.PlaceWall(61, 40, WallID.Stone);
            if (oldWingLayers)
            { WorldGen.paintWall(59, 40, 9, false, false); Main.tile[59, 40].wire(true); Main.tile[59, 40].actuator(true); }
            var library = (LibraryState)Get(host, "Library");
            var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, library, () => Step(context, input),
                59, 40, 3, 3, "门运行态返修 " + direction + "/" + hingePaint + "/" + wingPaint + "/" + hingeCoating + "/" + wingCoating);
            var saved = OpenCapturedAppearance(library, entry);
            var obj = saved.Objects.Single();
            Require(saved.Summary.Width == 3 && obj.Id > 0 && obj.Reliable && obj.Width == (direction == 0 ? 1 : 2) && obj.Height == 3 &&
                saved.Cells.Count(c => c.ObjectId == obj.Id && (c.Layers & CaptureLayer.Tile) != 0) == obj.Width * obj.Height &&
                saved.Cells.Where(c => c.ObjectId == obj.Id && (c.Layers & CaptureLayer.Tile) != 0).All(c => c.FrameX >= 0 && c.FrameY >= 0),
                "formal reopened asset preserves raw frames, positive ObjectId and the complete native door");
            return entry;
        }
        private static void DoorRepairDestination(int direction, bool oldWingLayers = false)
        {
            WorldGen.PlaceWall(44, 40, WallID.Stone); WorldGen.PlaceWall(46, 40, WallID.Stone);
            DoorRepairPlace(45, direction);
            if (oldWingLayers) { Main.tile[44, 40].wire(true); Main.tile[44, 40].actuator(true); }
        }
        private static BlueprintMaterialSnapshot DoorRepairBill(object context, object host, object input, object instance)
        {
            Call(host, "SetMaterialViewDemand", true);
            long projection = ((BlueprintProjectionIdentity)Get(instance, "Identity")).ProjectionGeneration;
            BlueprintMaterialSnapshot bill = null;
            Until(context, input, () =>
            {
                bill = (BlueprintMaterialSnapshot)Call(host, "MaterialWorld", projection);
                var match = GetOptional(instance, "Match");
                if (match == null) return false;
                var totals = Get(match, "Materials");
                return (int)Get(totals, "Current") == (int)Get(totals, "Expected") && (int)Get(totals, "Updating") == 0 &&
                    GetOptional(host, "materialPublishing") == null && ReferenceEquals(GetOptional(host, "materialPublishingTotals"), totals) &&
                    (long)Get(host, "materialPublishingRevision") == (long)Get(totals, "Revision") && bill.InventoryReady &&
                    bill.Status != BlueprintMaterialStatus.Pending;
            });
            return bill;
        }
        private static long DoorBillCount(BlueprintMaterialSnapshot bill, int type)
        { return bill.Rows.Where(row => row.ItemType == type).Sum(row => row.Required); }
        private static int DoorHeld(Player p, int type)
        {
            // Native inventory[58] mirrors/clones the cursor. It is not an
            // additional stack, even when the real inventory tail has already
            // returned that cursor's contents. Use the lifecycle's same total.
            return LifecycleQuantity(p, type);
        }
        private static void DoorRepairRun(object context, object host, object tools, object input)
        {
            doorRepairWatch = true; SetAutomatic(host, input, true);
            Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active") && Main.mouseItem.IsAir);
            doorRepairWatch = false;
        }
        private static void DoorClosedSourceOpenActual(object context, object host, object tools, object input, int mode)
        {
            var p = DoorRepairReset(context, host, tools, input);
            var entry = DoorRepairCapture(context, host, input, 0, mode == 2 ? (byte)0 : (byte)1, 0, mode == 1 ? (byte)1 : (byte)0);
            DoorRepairDestination(1);
            if (mode != 0) DoorRepairAppearance(45, 1, mode == 1 ? (byte)9 : (byte)1, mode == 1 ? (byte)9 : (byte)1, 2, 2);
            var instance = Place(context, host, input, entry, 44, 40); var bill = DoorRepairBill(context, host, input, instance);
            Require(bill.Status == BlueprintMaterialStatus.Exact && DoorBillCount(bill, ItemID.RedPaint) == (mode == 2 ? 0 : 6) &&
                DoorBillCount(bill, ItemID.GlowPaint) == (mode == 1 ? 6 : 0) && bill.Total == (mode == 0 ? 6 : mode == 1 ? 12 : 0),
                "closed source bills every actual wing/hinge exactly once from current native clear/restore steps; mode=" + mode);
            long unknown = (long)Get(host, "BuildUnknownResults"); DoorRepairRun(context, host, tools, input);
            for (int x = 45; x <= 46; x++) for (int y = 40; y <= 42; y++)
                Require(Main.tile[x, y].color() == (mode == 2 ? 0 : 1) && Main.tile[x, y].fullbrightBlock() == (mode == 1) && !Main.tile[x, y].invisibleBlock(),
                    "real native brush/coating/scraper completes both occupied door columns");
            int red = DoorHeld(p, ItemID.RedPaint), glow = DoorHeld(p, ItemID.GlowPaint);
            long unknownAfter = (long)Get(host, "BuildUnknownResults");
            var paintCells = doorRepairActions.Where(a => a.Kind == "PaintTile").Select(a => a.X + "," + a.Y).Distinct().ToArray();
            Console.WriteLine("DOOR_ACTUAL_MEMBERS mode=" + mode + " remainingRed=" + red + " remainingGlow=" + glow +
                " native58=" + p.inventory[58].type + ":" + p.inventory[58].stack + " cursor=" + Main.mouseItem.type + ":" + Main.mouseItem.stack +
                " unknownDelta=" + (unknownAfter - unknown) + " paintCells=" + string.Join(";", paintCells));
            Require(red == 32 - (mode == 2 ? 0 : 6) && glow == 32 - (mode == 1 ? 6 : 0),
                "native resource debits agree with the actual-member bill; mode=" + mode + " remainingRed=" + red + " remainingGlow=" + glow);
            Require(unknownAfter == unknown, "actual-member construction has no new Unknown result; mode=" + mode + " delta=" + (unknownAfter - unknown));
            Require(paintCells.Length == (mode == 2 ? 0 : 6),
                "native paint/coating actions cover every actual member once; mode=" + mode + " cells=" + string.Join(";", paintCells));
        }
        private static void DoorOpenSourceChangedPose(object context, object host, object tools, object input, bool closed, bool incompatibleColor, bool incompatibleCoating, bool samePose = false)
        {
            var p = DoorRepairReset(context, host, tools, input);
            bool incompatible = incompatibleColor || incompatibleCoating;
            byte wingPaint = !closed || incompatibleColor ? (byte)9 : (byte)1;
            var entry = DoorRepairCapture(context, host, input, -1, 1, wingPaint, incompatibleCoating ? (byte)1 : (byte)0,
                incompatibleCoating ? (byte)2 : (byte)0, true);
            DoorRepairDestination(closed ? 0 : samePose ? -1 : 1, true);
            var instance = Place(context, host, input, entry, 44, 40); var bill = DoorRepairBill(context, host, input, instance);
            if (!incompatible)
                Require(bill.Status == BlueprintMaterialStatus.Exact && DoorBillCount(bill, ItemID.RedPaint) == 3 &&
                    DoorBillCount(bill, ItemID.BluePaint) == (closed ? 1 : 4),
                    "open source counts actual hinge/wing roles, collapses uniform rows once, and retains the old-coordinate wall bill");
            else Require(bill.Status != BlueprintMaterialStatus.Exact, "incompatible saved open rows remain unresolved in a closed pose");
            long unknown = (long)Get(host, "BuildUnknownResults");
            if (!incompatible) DoorRepairRun(context, host, tools, input);
            else
            {
                doorRepairWatch = true; SetAutomatic(host, input, true);
                Until(context, input, () => Main.tile[44, 40].wallColor() == 9 && !(bool)Get(Get(tools, "Use"), "Active"));
                long uses = (long)Get(host, "BuildUsesStarted");
                for (int frame = 0; frame < 120; frame++) Step(context, input);
                Require(Scene(host).Length == 1 && Main.tile[45, 40].color() == 0 && !Main.tile[45, 40].invisibleBlock() &&
                    !Main.tile[45, 40].fullbrightBlock() && (long)Get(host, "BuildUsesStarted") == uses &&
                    doorRepairActions.All(action => action.Kind == "PaintWall"), "impossible closed rows keep Conflict without alternating paint/coating actions");
                doorRepairWatch = false;
            }
            if (!incompatible)
                for (int row = 0; row < 3; row++)
                    Require(Main.tile[45, 40 + row].color() == 1 && (closed || Main.tile[samePose ? 44 : 46, 40 + row].color() == wingPaint),
                        "actual native action coordinates follow hinge/wing roles across operating pose");
            Require(Main.tile[44, 40].wallColor() == 9 && Main.tile[44, 40].wire() && Main.tile[44, 40].actuator() &&
                Main.tile[46, 40].wallColor() == 0 && !Main.tile[46, 40].wire() && !Main.tile[46, 40].actuator() &&
                (long)Get(host, "BuildUnknownResults") == unknown,
                "source old-wing wall/wire/actuator remain independent at their original world coordinate");
        }
        private static void DoorMappedWallRestoration(object context, object host, object tools, object input, bool selectedDoor)
        {
            var p = DoorRepairReset(context, host, tools, input);
            var entry = DoorRepairCapture(context, host, input, 0, 1, 1, 1);
            if (!selectedDoor)
                entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, (LibraryState)Get(host, "Library"),
                    () => Step(context, input), 61, 40, 1, 1, "墙未选择前景门");
            DoorRepairDestination(1); DoorRepairAppearance(45, 1, 1, 1, 1, 1);
            WorldGen.paintWall(46, 40, 9, false, false);
            doorWallBillWatch = selectedDoor;
            var instance = Place(context, host, input, entry, selectedDoor ? 44 : 46, 40);
            if (selectedDoor)
            {
                var bill = DoorRepairBill(context, host, input, instance);
                doorWallBillWatch = false;
                Console.WriteLine("DOOR_WALL_BILL selectedDoor=" + selectedDoor + " status=" + bill.Status + " error=" + bill.Error +
                    " rows=" + string.Join(";", bill.Rows.Select(row => row.ItemType + "=" + row.Required)) + " total=" + bill.Total +
                    " windows=" + ((System.Collections.IDictionary)Get(instance, "Windows")).Count +
                    " retained=" + ((System.Collections.IEnumerable)Get(host, "buildSources")).Cast<object>().Count() +
                    " works=" + ((System.Collections.IDictionary)Get(host, "projectionMatchWork")).Count);
                Require(bill.Status == BlueprintMaterialStatus.Exact && DoorBillCount(bill, ItemID.RedPaint) == 1 && DoorBillCount(bill, ItemID.GlowPaint) == 1,
                    "actual mapped-wing wall includes exactly its authorized foreground restoration cost");
                Require(doorWallStandaloneContext && ((System.Collections.IDictionary)Get(instance, "Windows")).Count == 0 &&
                    !((System.Collections.IEnumerable)Get(host, "buildSources")).Cast<object>().Any(),
                    "the exact authorized restoration bill is independent of Draw, build-source retention and completed ordinary parts");
                DoorRepairRun(context, host, tools, input);
                Require(Main.tile[46, 40].wallColor() == 0 && Main.tile[46, 40].color() == 1 && Main.tile[46, 40].fullbrightBlock() &&
                    DoorHeld(p, ItemID.RedPaint) == 31 && DoorHeld(p, ItemID.GlowPaint) == 31,
                    "real scraper reaches actual-coordinate wall and restores the selected complete-door wing with exact supplies");
            }
            else
            {
                doorRepairWatch = true; long uses = (long)Get(host, "BuildUsesStarted"); SetAutomatic(host, input, true);
                for (int frame = 0; frame < 160; frame++) Step(context, input);
                Require(Scene(host).Length == 1 && Main.tile[46, 40].wallColor() == 9 && Main.tile[46, 40].color() == 1 &&
                    Main.tile[46, 40].fullbrightBlock() && (long)Get(host, "BuildUsesStarted") == uses && doorRepairActions.Count == 0 &&
                    DoorHeld(p, ItemID.RedPaint) == 32 && DoorHeld(p, ItemID.GlowPaint) == 32,
                    "wall-only source cannot acquire restoration permission from an unselected actual open door");
                doorRepairWatch = false;
            }
        }
        private static void DoorPlanPoseRevocation(object context, object host, object tools, object input)
        {
            var p = DoorRepairReset(context, host, tools, input);
            var entry = DoorRepairCapture(context, host, input, 0, 1, 1);
            DoorRepairDestination(1); DoorRepairAppearance(45, 0, 1, 1);
            Place(context, host, input, entry, 44, 40); SetAutomatic(host, input, true);
            object flight = null;
            Until(context, input, () =>
            {
                flight = GetOptional(host, "preparedBuild") ?? GetOptional(host, "activeBuild");
                return flight != null && (int)Get(Get(flight, "Target"), "WorldX") == 46 && !(bool)Get(flight, "Entered");
            });
            var target = Get(flight, "Target"); int paint = DoorHeld(p, ItemID.RedPaint); long unknown = (long)Get(host, "BuildUnknownResults");
            Require(WorldGen.CloseDoor(45, 40, true), "real original CloseDoor changes the already planned occupied pose");
            Require(!(bool)Call(host, "IsBuildTargetCurrent", target) && !(bool)Call(host, "ConstructionBuildCurrent", flight),
                "a planned actual wing and its flight are revoked immediately after the real pose change");
            doorRepairWatch = true;
            Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active") && Main.mouseItem.IsAir);
            Require(DoorHeld(p, ItemID.RedPaint) == paint && doorRepairActions.Count == 0 && !Main.tile[46, 40].active() &&
                (long)Get(host, "BuildUnknownResults") == unknown, "stale wing receives no native action or debit; fresh complete closed-door Match settles normally");
            doorRepairWatch = false;
        }
        private static void DoorDenseContextContinuation(object context, object host, object tools, object input)
        {
            const int count = 2048, left = 60, top = 60, wallEnd = 1005, tileOnlyGap = 100, walls = wallEnd - 1, wires = count - wallEnd, lastWall = left + wallEnd - 1;
            var p = DoorRepairReset(context, host, tools, input);
            var originalPosition = p.position; var originalVelocity = p.velocity;
            var originalLeftWorld = Main.leftWorld; var originalTopWorld = Main.topWorld;
            var originalRightWorld = Main.rightWorld; var originalBottomWorld = Main.bottomWorld;
            try
            {
                using (NativeBlueprintWorldMatchChecks.EmptyWorld(host, count + left + 64, 160))
                {
                    // Original Player.BordersMovement clamps the actor at
                    // least 640 pixels from each world edge. Both actual
                    // consumers need legal interior positions, not a fixture
                    // position which Player.Update immediately moves away.
                    Main.leftWorld = Main.topWorld = 0; Main.rightWorld = Main.maxTilesX * 16; Main.bottomWorld = Main.maxTilesY * 16;
                    for (int x = 5; x < Main.maxTilesX - 5; x++) Require(WorldGen.PlaceTile(x, top + 3, TileID.Stone, true, true), "dense door fixture has native bottom anchors");
                    for (int x = left; x < left + count; x++)
                    {
                        Require(WorldGen.PlaceTile(x, top - 1, TileID.Stone, true, true) && WorldGen.PlaceObject(x, top, TileID.ClosedDoor, true), "dense source door is a complete original placement");
                        for (int row = 0; row < 3; row++)
                        {
                            WorldGen.paintTile(x, top + row, 1, false, false);
                            WorldGen.paintCoatTile(x, top + row, 1, false, false);
                        }
                        if (x - left < wallEnd && x - left != tileOnlyGap) WorldGen.PlaceWall(x, top + 1, WallID.Stone);
                        else if (x - left >= wallEnd) Main.tile[x, top + 1].wire(true);
                    }
                    p.position = new Microsoft.Xna.Framework.Vector2((left - 3) * 16, top * 16); p.velocity = Microsoft.Xna.Framework.Vector2.Zero;
                    var library = (LibraryState)Get(host, "Library");
                    var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, library, () => Step(context, input),
                        left, top, count, 3, "门跨层上下文密集分片真实续办");
                    var saved = OpenCapturedAppearance(library, entry);
                    Console.WriteLine("DOOR_DENSE_SOURCE doors=" + saved.Objects.Count + " cells=" + saved.Cells.Count + " walls=" +
                        saved.Cells.Count(cell => (cell.Layers & CaptureLayer.Wall) != 0) + " wires=" + saved.Cells.Count(cell => cell.Wires == 1));
                    Require(saved.Objects.Count == count && saved.Cells.Count == count * 3 && saved.Objects.All(obj => obj.Reliable && obj.Width == 1 && obj.Height == 3) &&
                        saved.Cells.Count(cell => (cell.Layers & CaptureLayer.Wall) != 0) == walls && saved.Cells.Count(cell => cell.Wires == 1) == wires,
                        "formal reopened dense native asset retains 2048 complete raw doors, 1004 independent walls, a Tile-only raw gap and a wire-only ordinary tail");
                    WorldGen.paintWall(left, top + 1, 9, false, false); WorldGen.paintWall(lastWall, top + 1, 9, false, false);
                    var instance = Place(context, host, input, entry, left, top); var bill = DoorRepairBill(context, host, input, instance);
                    var match = Get(instance, "Match");
                    Console.WriteLine("DOOR_DENSE_BILL status=" + bill.Status + " rows=" + string.Join(";", bill.Rows.Select(row => row.ItemType + "=" + row.Required)) +
                        " targets=" + Get(match, "Targets") + " matches=" + Get(match, "Matches") + " unknown=" + Get(match, "Unknown"));
                    Require(bill.Status == BlueprintMaterialStatus.Exact && DoorBillCount(bill, ItemID.RedPaint) == 2 && DoorBillCount(bill, ItemID.GlowPaint) == 2 &&
                        (long)Get(match, "Targets") == count * 3L + walls + wires && (long)Get(match, "Matches") == count * 3L + walls + wires - 2 && (long)Get(match, "Unknown") == 0 &&
                        ((System.Collections.IDictionary)Get(instance, "Windows")).Count == 0 && !((System.Collections.IEnumerable)Get(host, "buildSources")).Cast<object>().Any(),
                        "no-Draw material-only dense prefixes accumulate the full original denominator and exact two-wall restoration bill");
                    long unknown = (long)Get(host, "BuildUnknownResults"), previousSteps = (long)Get(host, "ProjectionMatchSourceSteps"), previousChecks = (long)Get(host, "BuildCandidateChecks");
                    Action bounded = () =>
                    {
                        long steps = (long)Get(host, "ProjectionMatchSourceSteps"), checks = (long)Get(host, "BuildCandidateChecks");
                        Require(steps - previousSteps <= 256 && checks - previousChecks <= 64 && (int)Get(host, "materialBuildSourceBudget") >= 0 &&
                            ((System.Collections.IEnumerable)Get(host, "buildSources")).Cast<object>().Count() <= 4 && (long)Get(host, "buildSourceBytes") <= 4 * BlueprintProjectionSourceReceipt.TargetBytes,
                            "dense context replay shares unchanged per-Update discovery and four-part hard retained-byte budgets");
                        previousSteps = steps; previousChecks = checks;
                    };
                    var ordinaryRange = ((System.Collections.IEnumerable)Get(match, "Ranges")).Cast<object>().Single(range => ((BlueprintProjectionSourceRange)Get(range, "Source")).ObjectId == 0);
                    // Closing the view then enabling automatic work establishes a
                    // real new material demand. A physical right hold lets the
                    // same source scheduler finish its suffix without admitting a
                    // build candidate, rather than injecting a cursor/cache state.
                    Call(host, "SetMaterialViewDemand", false); doorRepairWatch = true; SetAutomatic(host, input, true);
                    BlueprintProjectionSourcePart tailPart = null;
                    NativeQuickItemChecks.Until(() =>
                    {
                        LifecycleInput(context, input, "right"); LifecycleWorldUpdate(context); bounded();
                        tailPart = (BlueprintProjectionSourcePart)Call(host, "FindBuildSource", ordinaryRange);
                        var material = Get(ordinaryRange, "Materials");
                        return tailPart != null && tailPart.Offset > 0 && tailPart.Complete &&
                            (int)Get(material, "CandidateCursor") == 0 && GetOptional(material, "Progress") == null && !(bool)Get(ordinaryRange, "Reading");
                    });
                    Console.WriteLine("DOOR_DENSE_HELD offset=" + tailPart.Offset + " next=" + tailPart.NextOffset + " cells=" + tailPart.Window.Cells.Count +
                        " walls=" + tailPart.Window.Cells.Count(cell => (cell.Layers & CaptureLayer.Wall) != 0) + " cursor=" + Get(Get(ordinaryRange, "Materials"), "CandidateCursor") +
                        " crossLayer=" + Get(ordinaryRange, "MaterialCrossLayers"));
                    Require(tailPart.Window.Cells.All(cell => (cell.Layers & CaptureLayer.Wall) == 0), "dense raw-prefix fixture reaches its actual wire-only terminal part");
                    Require((bool)Get(tools, "ManualRight") && (bool)Get(ordinaryRange, "MaterialCrossLayers") && doorRepairActions.Count == 0 &&
                        Main.tile[left, top + 1].wallColor() == 9 && Main.tile[lastWall, top + 1].wallColor() == 9,
                        "a real held manual input leaves cursor zero behind a completed wire-only suffix, while earlier wall cross-layer subscription stays active");
                    int heldSuffixOffset = tailPart.Offset;
                    bool firstReleaseObserved = false;
                    Until(context, input, () =>
                    {
                        bounded();
                        if (!firstReleaseObserved)
                        {
                            bool reach = p.IsInTileInteractionRange(left, top + 1, Terraria.DataStructures.TileReachCheckSettings.Simple, p.inventory[16].tileBoost + p.blockRange);
                            Console.WriteLine("DOOR_DENSE_FIRST position=" + p.position + " target=" + left + "," + (top + 1) + " reach=" + reach +
                                " manualRight=" + Get(tools, "ManualRight") + " input=" + Get(input, "CanRunAutomaticActions"));
                            Require(reach && !(bool)Get(tools, "ManualRight") && (bool)Get(input, "CanRunAutomaticActions"),
                                "physical release leaves the first original consumer really reachable after Player.Update and current input admission");
                            firstReleaseObserved = true;
                        }
                        return Main.tile[left, top + 1].wallColor() == 0 && Main.tile[left, top + 1].color() == 1 && Main.tile[left, top + 1].fullbrightBlock() && !(bool)Get(Get(tools, "Use"), "Active");
                    });
                    Require(Scene(host).Length == 1 && Main.tile[lastWall, top + 1].wallColor() == 9 && DoorHeld(p, ItemID.RedPaint) == 31 && DoorHeld(p, ItemID.GlowPaint) == 31,
                        "the old first ordinary prefix is really reacquired and serviced while the distant tail remains unfinished");
                    var remaining = DoorRepairBill(context, host, input, instance);
                    Require(remaining.Status == BlueprintMaterialStatus.Exact && DoorBillCount(remaining, ItemID.RedPaint) == 1 && DoorBillCount(remaining, ItemID.GlowPaint) == 1 &&
                        (bool)Get(ordinaryRange, "MaterialCrossLayers"), "real early-wall native changes refresh the coupled full-range bill after the wire-only suffix");
                    previousSteps = (long)Get(host, "ProjectionMatchSourceSteps"); previousChecks = (long)Get(host, "BuildCandidateChecks");
                    p.position = new Microsoft.Xna.Framework.Vector2((lastWall + 3) * 16, (top - 4) * 16); p.velocity = Microsoft.Xna.Framework.Vector2.Zero;
                    bool tailReachObserved = false;
                    Until(context, input, () =>
                    {
                        bounded();
                        if (!tailReachObserved)
                        {
                            bool reach = p.IsInTileInteractionRange(lastWall, top + 1, Terraria.DataStructures.TileReachCheckSettings.Simple, p.inventory[16].tileBoost + p.blockRange);
                            Console.WriteLine("DOOR_DENSE_TAIL position=" + p.position + " target=" + lastWall + "," + (top + 1) + " reach=" + reach);
                            Require(reach && !(bool)Get(tools, "ManualRight") && (bool)Get(input, "CanRunAutomaticActions"),
                                "the final original consumer is reachable from a legal interior roof position after Player.Update");
                            tailReachObserved = true;
                        }
                        return Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active") && Main.mouseItem.IsAir;
                    });
                    Require(Main.tile[lastWall, top + 1].wallColor() == 0 && Main.tile[lastWall, top + 1].color() == 1 && Main.tile[lastWall, top + 1].fullbrightBlock() &&
                        DoorHeld(p, ItemID.RedPaint) == 30 && DoorHeld(p, ItemID.GlowPaint) == 30 && (long)Get(host, "BuildUnknownResults") == unknown &&
                        doorRepairActions.Select(action => action.X).Distinct().OrderBy(x => x).SequenceEqual(new[] { left, lastWall }),
                        "moving to the real raw tail completes the original full scene with exact supplies and no skipped/repeated target or Unknown receipt");
                    Console.WriteLine("DOOR_DENSE_CONTEXT nativeCaptureDoors=" + count + " originalTargets=" + (count * 3 + walls + wires) +
                        " actualWalls=2 redDebit=2 glowDebit=2 noDraw=true heldSuffixOffset=" + heldSuffixOffset + " heldCursor=0 earlyWallBillRefresh=true totalSourceAndCandidateBudget=256 candidateLimit=64 retainedParts=4 retainedHardBytes=" + (4 * BlueprintProjectionSourceReceipt.TargetBytes));
                    doorRepairWatch = false; SetAutomatic(host, input, false);
                }
            }
            finally
            {
                Main.leftWorld = originalLeftWorld; Main.topWorld = originalTopWorld; Main.rightWorld = originalRightWorld; Main.bottomWorld = originalBottomWorld;
                p.position = originalPosition; p.velocity = originalVelocity; doorRepairWatch = false; SetAutomatic(host, input, false); Call(host, "SetMaterialViewDemand", false);
            }
        }
    }
}
