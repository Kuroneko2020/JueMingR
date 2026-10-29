using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static object wallContextHost;
        private static int preparedObjectWalls, blockedPendingContainers;
        private static Exception wallContextProbeError;

        // Observe the real candidate and result without binding __args or its
        // out flight. A wall may legitimately retain its foreground proof.
        private static void ObserveWallContextPreparation(object __instance, object __0, bool __result)
        {
            try { ObserveWallContextPreparationCore(__instance, __0, __result); }
            catch (Exception error) { if (wallContextProbeError == null) wallContextProbeError = error; }
        }
        private static void ObserveWallContextPreparationCore(object __instance, object __0, bool __result)
        {
            if (!ReferenceEquals(__instance, wallContextHost)) return;
            var placement = (BlueprintPlacementDescriptor)Get(__0, "Placement");
            if (placement.Kind == BlueprintPlacementKind.Wall && GetOptional(__0, "Body") != null)
            {
                Require(placement.TileType == -1, "object-member wall retains the native wall descriptor sentinel");
                if (__result) preparedObjectWalls++;
            }
            if (placement.Kind == BlueprintPlacementKind.Tile && TileID.Sets.IsAContainer[placement.TileType] &&
                GetOptional(__instance, "buildContainer") != null && !__result &&
                Get(Get(Get(__0, "Range"), "Materials"), "BuildWaits").ToString().Contains("Container"))
                blockedPendingContainers++;
        }

        private static void PrepareCapturedObjectWall(object context, object host, object input, object owner)
        {
            object source = null;
            // Warm the real worker/proof source before any Player.Update can
            // consume the small template's ordinary wall candidate first.
            NativeQuickItemChecks.Until(() =>
            {
                NativeToolExecutionChecks.Sample(context, input, new Vector2(480, 540), false);
                NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime");
                source = ((System.Collections.IEnumerable)Get(host, "buildSources")).Cast<object>().FirstOrDefault(value =>
                    ReferenceEquals(Get(value, "Owner"), owner) && (int)Get(Get(value, "Part"), "LogicalObjectId") > 0);
                return source != null;
            });
            var range = Get(source, "Range"); var part = Get(source, "Part");
            object[] supportArgs = { Get(range, "Watch"), Get(range, "PartIndex"), 0L };
            Require((bool)Call(Get(host, "projectionChanges"), "TryGetBuildStamp", supportArgs), "actual object source has a current support proof");
            object[] candidateArgs = { owner, range, part, 0, 1, supportArgs[2], null, 0 };
            Require((bool)Call(host, "TryCreateConstructionTarget", candidateArgs), "real object-member wall candidate is currently admissible before writes");
            var target = candidateArgs[6];
            Require(GetOptional(target, "Body") != null && ((BlueprintPlacementDescriptor)Get(target, "Placement")).Kind == BlueprintPlacementKind.Wall,
                "production wall target retains the captured foreground context");
            object[] preparationArgs = { target, Main.LocalPlayer, null };
            Require((bool)Call(host, "TryPrepareBuild", preparationArgs), "valid object-member wall prepares even with a real container pending");
        }

        private static void ConstructionWallContexts(object context, object host, object tools, object input)
        {
            var patch = new Harmony("JueMingR.Tests.BlueprintWallForegroundContext");
            var method = host.GetType().GetMethod("TryPrepareBuild", F);
            wallContextHost = host; preparedObjectWalls = blockedPendingContainers = 0; wallContextProbeError = null;
            patch.Patch(method, postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(ObserveWallContextPreparation)));
            try
            {
                foreach (int item in new[] { ItemID.WoodPlatform, ItemID.WoodenChair })
                    foreach (bool wall in new[] { false, true })
                        ConstructionObjectWall(context, host, tools, input, item, wall);
                var p = Reset(context, host, tools, input);
                p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 2;
                p.inventory[13].SetDefaults(ItemID.WoodWall); p.inventory[13].stack = 8;
                WorldGen.PlaceWall(43, 42, WallID.Wood, true);
                Place(context, host, input, Asset(host,
                    new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone },
                    new CaptureCell { X = 2, Layers = CaptureLayer.Wall, WallType = WallID.Wood }), 44, 42);
                SetAutomatic(host, input, true);
                Until(context, input, () => Scene(host).Length == 0);
                Require(Main.tile[44, 42].type == TileID.Stone && Main.tile[46, 42].wall == WallID.Wood &&
                    p.inventory[12].stack == 1 && (bool)Get(host, "ControlsEnabled"),
                    "ordinary body and pure wall remain independently executable without an object context");
                ConstructionPendingContainerWall(context, host, tools, input);
                Require(wallContextProbeError == null && preparedObjectWalls >= 3 && blockedPendingContainers > 0,
                    "real candidates exercise object walls and the still-enforced pending Tile container gate");
                Console.WriteLine("PASS BlueprintConstruction wall contexts: captured platform/chair with and without walls, member paint, ordinary body/pure wall, pending real container refuses another container while object wall continues; objectWallPreparations=" +
                    preparedObjectWalls + " pendingContainerRefusals=" + blockedPendingContainers);
            }
            finally
            {
                patch.Unpatch(method, HarmonyPatchType.All, patch.Id); wallContextHost = null;
                Call(host, "SetAutoBuild", false); Call(host, "ClearProjection");
            }
        }

        private static void ConstructionObjectWall(object context, object host, object tools, object input, int itemType, bool wall)
        {
            var p = Reset(context, host, tools, input);
            p.inventory[12].SetDefaults(itemType); p.inventory[12].stack = 2;
            p.inventory[13].SetDefaults(ItemID.WoodWall); p.inventory[13].stack = 16;
            p.inventory[14].SetDefaults(ItemID.Paintbrush);
            p.inventory[15].SetDefaults(ItemID.RedPaint); p.inventory[15].stack = 8;
            bool chair = itemType == ItemID.WoodenChair;
            int top = chair ? 41 : 42, height = chair ? 2 : 1;
            Require(chair ? WorldGen.PlaceObject(60, 42, TileID.Chairs, true, 0, direction: -1) :
                WorldGen.PlaceTile(60, 42, TileID.Platforms, true, true), "independent original foreground source is complete");
            for (int y = top; y <= 42; y++)
            {
                if (wall) WorldGen.PlaceWall(60, y, WallID.Wood, true);
                WorldGen.paintTile(60, y, PaintID.RedPaint, false, false);
            }
            var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input,
                (LibraryState)Get(host, "Library"), () => Step(context, input), 60, top, 1, height, "对象背景墙职责回归");
            if (wall) for (int y = top; y <= 42; y++) WorldGen.PlaceWall(43, y, WallID.Wood, true);
            var placed = Place(context, host, input, entry, 44, top); SetAutomatic(host, input, true);
            if (wall) PrepareCapturedObjectWall(context, host, input, placed);
            Until(context, input, () => Scene(host).Length == 0);
            Require(wallContextProbeError == null, "wall preflight probe: " + wallContextProbeError);
            Require((bool)Get(host, "ControlsEnabled") && p.inventory[12].stack == 1 && p.inventory[15].stack == 8 - height &&
                Enumerable.Range(top, height).All(y => Main.tile[44, y].active() && Main.tile[44, y].type == p.inventory[12].createTile &&
                    Main.tile[44, y].color() == PaintID.RedPaint && (wall ? Main.tile[44, y].wall == WallID.Wood : Main.tile[44, y].wall == 0)),
                "captured object keeps its complete foreground, member appearance and independent wall results, item=" + itemType + " wall=" + wall);
            Console.WriteLine("PASS BlueprintWallContext item=" + itemType + " wall=" + wall + " objectWallPreparations=" + preparedObjectWalls);
        }

        private static void ConstructionPendingContainerWall(object context, object host, object tools, object input)
        {
            int mode = Main.netMode; var connection = Netplay.Connection; var sections = Main.sectionManager;
            var client = Main.clientPlayer; var buffer = NetMessage.buffer[256];
            var chests = Main.chest; var byCoords = AccessTools.Field(typeof(Chest), "_chestsByCoords").GetValue(null);
            try
            {
                Netplay.Connection = new RemoteServer { PendingTermination = true, State = 10 };
                NetMessage.buffer[256] = new MessageBuffer(); Main.clientPlayer = new Player(); Main.netMode = 1;
                Main.sectionManager = new WorldSections(1, 1); Main.sectionManager.SetAllSectionsLoaded(); Call(context, "UpdateRuntime");
                var p = Reset(context, host, tools, input);
                Main.chest = new Chest[8000]; AccessTools.Field(typeof(Chest), "_chestsByCoords").SetValue(null, new Dictionary<Point, Chest>());
                p.inventory[12].SetDefaults(ItemID.Chest); p.inventory[12].stack = 2;
                p.inventory[13].SetDefaults(ItemID.WoodWall); p.inventory[13].stack = 16;
                p.inventory[14].SetDefaults(ItemID.WoodPlatform); p.inventory[14].stack = 2;
                Require(WorldGen.PlaceObject(60, 42, TileID.Containers, true), "original source container exists");
                var library = (LibraryState)Get(host, "Library");
                var chest = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, library,
                    () => Step(context, input), 60, 41, 2, 2, "容器等待职责回归");
                Require(WorldGen.PlaceTile(64, 42, TileID.Platforms, true, true), "original source platform exists");
                WorldGen.PlaceWall(64, 42, WallID.Wood, true);
                var wall = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, library,
                    () => Step(context, input), 64, 42, 1, 1, "等待容器时独立对象墙");
                Place(context, host, input, chest, 44, 41); SetAutomatic(host, input, true);
                Until(context, input, () => GetOptional(host, "buildContainer") != null && (bool)Get(Get(host, "buildContainer"), "Sent"));
                var pending = Get(host, "buildContainer");
                Require(p.inventory[12].stack == 1 && Main.tile[44, 41].active(), "pending receipt comes from real original container placement and consumption");
                p.position = new Vector2(44 * 16, 646);
                WorldGen.PlaceWall(48, 42, WallID.Wood, true);
                int wallsBefore = preparedObjectWalls;
                SetAutomatic(host, input, false);
                Place(context, host, input, chest, 46, 41); var placedWall = Place(context, host, input, wall, 49, 42);
                SetAutomatic(host, input, true); PrepareCapturedObjectWall(context, host, input, placedWall);
                Until(context, input, () => Main.tile[49, 42].active() && Main.tile[49, 42].wall == WallID.Wood && !(bool)Get(Get(tools, "Use"), "Active"));
                Console.WriteLine("BlueprintWallContext pending newChestActive=" + Main.tile[46, 41].active() + " chestStack=" + p.inventory[12].stack +
                    " platformStack=" + p.inventory[14].stack + " sameReceipt=" + ReferenceEquals(pending, GetOptional(host, "buildContainer")) +
                    " objectWallsBefore/After=" + wallsBefore + "/" + preparedObjectWalls + " refusedContainers=" + blockedPendingContainers + " controls=" + Get(host, "ControlsEnabled"));
                Require(!Main.tile[46, 41].active() && p.inventory[12].stack == 1 && p.inventory[14].stack == 1 &&
                    ReferenceEquals(pending, GetOptional(host, "buildContainer")) && preparedObjectWalls > wallsBefore && blockedPendingContainers > 0 &&
                    (bool)Get(host, "ControlsEnabled"), "pending Tile container stays blocked while a wall with foreground Body executes without borrowing that gate");
                SetAutomatic(host, input, false);
                ReceiveBuildReply((int)Get(pending, "Action"), (int)Get(pending, "X"), (int)Get(pending, "Y"), (int)Get(pending, "Style"), -1);
                Require(GetOptional(host, "buildContainer") == null, "the original rejection receiver settles the actual pending receipt");
            }
            finally
            {
                Call(host, "SetAutoBuild", false); Call(host, "ClearProjection");
                Main.netMode = mode; Netplay.Connection = connection; Main.sectionManager = sections; Main.clientPlayer = client; NetMessage.buffer[256] = buffer;
                Main.chest = chests; AccessTools.Field(typeof(Chest), "_chestsByCoords").SetValue(null, byCoords); Call(context, "UpdateRuntime");
            }
        }
    }
}
