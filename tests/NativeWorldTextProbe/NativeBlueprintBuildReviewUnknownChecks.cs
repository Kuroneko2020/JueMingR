using System;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.WorldTargets;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static void ReviewUnknownReplays(object context, object host, object tools, object input)
        {
            var patch = new Harmony("JueMingR.Tests.BlueprintUnknownReplay");
            try
            {
                var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 3;
                p.inventory[13].SetDefaults(ItemID.DirtBlock); p.inventory[13].stack = 2;
                var initial = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone },
                    new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = TileID.Dirt });
                var single = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone });
                var placed = Place(context, host, input, initial, 44, 42);
                patch.Patch(typeof(Player).GetMethod("PlaceThing_Tiles_PlaceIt", F), postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(RemoveEligibilityBody)));
                long unknown = (long)Get(host, "BuildUnknownResults"); removeEligibilityBody = true; SetAutomatic(host, input, true);
                Until(context, input, () => (long)Get(host, "BuildUnknownResults") > unknown && Main.tile[46, 42].active() && !(bool)Get(Get(tools, "Use"), "Active"));
                Require(!Main.tile[44, 42].active() && p.inventory[12].stack == 2 && p.inventory[13].stack == 1 && Scene(host).Contains(placed),
                    "actual spent-then-removed Stone is unknown while an independent real Dirt target still consumes once");
                p.inventory[14].SetDefaults(ItemID.StoneBlock); p.inventory[14].stack = 2;
                foreach (string exit in new[] { "remove", "all", "clear", "toggle", "menu" })
                {
                    SetAutomatic(host, input, false);
                    if (exit == "remove") ReviewRemoveOne(context, host, input, placed, 44, 42);
                    else if (exit == "all")
                    {
                        NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "RemoveProjection");
                        Require((bool)Get(host, "CanRemoveAllProjections"), "real RemoveAll is admitted only from the existing removal mode");
                        Call(host, "RemoveAllProjections"); Require(Scene(host).Length == 0, "real RemoveAll retires the scene");
                    }
                    else if (exit == "clear") { Call(host, "ClearProjection"); Require(Scene(host).Length == 0, "normal Clear retires the scene"); }
                    else if (exit == "menu")
                    {
                        long generation = (long)Get(Get(tools, "Runtime"), "Generation");
                        Main.gameMenu = true; Call(context, "UpdateRuntime"); Main.gameMenu = false; Call(context, "UpdateRuntime");
                        Require((long)Get(Get(tools, "Runtime"), "Generation") > generation && Scene(host).Length == 0,
                            "real menu invalidation creates a runtime generation without replacing player/world/connection identity");
                    }
                    if (exit != "toggle") placed = Place(context, host, input, single, 44, 42);
                    long uses = (long)Get(host, "BuildUsesStarted"), checks = (long)Get(host, "BuildCandidateChecks");
                    SetAutomatic(host, input, true); Until(context, input, () => (long)Get(host, "BuildCandidateChecks") > checks);
                    for (int i = 0; i < 80; i++) Step(context, input);
                    Require(!Main.tile[44, 42].active() && p.inventory[12].stack == 2 && p.inventory[14].stack == 2 &&
                        (long)Get(host, "BuildUsesStarted") == uses && Scene(host).Contains(placed),
                        "unknown world target cannot be renewed by " + exit + ", a new projection identity, or a fresh same-type slot14");
                    Console.WriteLine("PASS Blueprint review unknown replay exit=" + exit + " newSameTypeSource=14 actualRetries=0");
                }
                // Same type at another coordinate, and another layer at the
                // unknown coordinate, remain independently usable.
                long independentUses = (long)Get(host, "BuildUsesStarted");
                Place(context, host, input, single, 42, 42); Until(context, input, () => Main.tile[42, 42].active() && !(bool)Get(Get(tools, "Use"), "Active"));
                Require(p.inventory[14].stack == 1 && !Main.tile[44, 42].active() && (long)Get(host, "BuildUsesStarted") == independentUses + 1,
                    "a new same-type source can still build an independent coordinate exactly once");
                p.inventory[15].SetDefaults(ItemID.StoneWall); p.inventory[15].stack = 2; WorldGen.PlaceWall(43, 42, p.inventory[15].createWall, true);
                var wall = Asset(host, new CaptureCell { Layers = CaptureLayer.Wall, WallType = (ushort)p.inventory[15].createWall });
                var wallPlacement = Place(context, host, input, wall, 44, 42); Until(context, input, () => !Scene(host).Contains(wallPlacement));
                Require(p.inventory[15].stack == 1 && Main.tile[44, 42].wall > 0 && !Main.tile[44, 42].active() && Scene(host).Contains(placed),
                    "unknown Tile protects its layer, not a legal independent Wall target at the same coordinate");
                ReviewFreshPlayerSession(context);
                int before = p.inventory[12].stack + p.inventory[14].stack;
                placed = Place(context, host, input, single, 44, 42); SetAutomatic(host, input, true);
                Until(context, input, () => !Scene(host).Contains(placed));
                Require(Main.tile[44, 42].active() && p.inventory[12].stack + p.inventory[14].stack == before - 1 &&
                    (long)Get(host, "BuildUnknownResults") == unknown + 1,
                    "an actual different-player session releases old target/source responsibility and permits one newly confirmed construction");
                Console.WriteLine("PASS Blueprint review unknown target remains across remove/all/clear/off-on/menu, independent coordinate/layer consumes, actual fresh identity releases");
            }
            finally { ReviewUnknownCleanup(context, host, patch); }
        }

        private static void ReviewUnknownFurniture(object context, object host, object tools, object input)
        {
            var patch = new Harmony("JueMingR.Tests.BlueprintUnknownFurniture");
            try
            {
                var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.WoodenChair); p.inventory[12].stack = 2;
                var chair = PreflightChairAsset(context, host, input);
                var placed = Place(context, host, input, chair, 44, 41);
                patch.Patch(typeof(Player).GetMethod("PlaceThing_Tiles_PlaceIt", F), postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(RemoveEligibilityBody)));
                long unknown = (long)Get(host, "BuildUnknownResults"); removeEligibilityBody = true; SetAutomatic(host, input, true);
                Until(context, input, () => (long)Get(host, "BuildUnknownResults") > unknown && !(bool)Get(Get(tools, "Use"), "Active"));
                Require(p.inventory[12].stack == 1 && !Main.tile[44, 41].active() && !Main.tile[44, 42].active(),
                    "the original complete furniture really consumed before its entire native body was externally removed");
                SetAutomatic(host, input, false); ReviewRemoveOne(context, host, input, placed, 44, 41);
                p.inventory[14].SetDefaults(ItemID.WoodenChair); p.inventory[14].stack = 2;
                placed = Place(context, host, input, chair, 44, 41); long uses = (long)Get(host, "BuildUsesStarted");
                SetAutomatic(host, input, true); for (int i = 0; i < 120; i++) Step(context, input);
                Require(!Main.tile[44, 41].active() && !Main.tile[44, 42].active() && p.inventory[14].stack == 2 && (long)Get(host, "BuildUsesStarted") == uses,
                    "replaying the complete chair with a new same-type source cannot renew its unknown footprint");
                SetAutomatic(host, input, false); Call(host, "ClearProjection");
                p.inventory[14].SetDefaults(ItemID.StoneBlock); p.inventory[14].stack = 3;
                WorldGen.PlaceWall(44, 41, WallID.Stone, true);
                // Upper member has a destination wall, lower member a native
                // floor, so both independent ordinary recipes are legal in
                // the absence of the previous unknown furniture obligation.
                var members = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone },
                    new CaptureCell { Y = 1, Layers = CaptureLayer.Tile, TileType = TileID.Stone });
                placed = Place(context, host, input, members, 44, 41);
                long checks = (long)Get(host, "BuildCandidateChecks"); SetAutomatic(host, input, true);
                Until(context, input, () => (long)Get(host, "BuildCandidateChecks") >= checks + 2);
                for (int i = 0; i < 80; i++) Step(context, input);
                Require(!Main.tile[44, 41].active() && !Main.tile[44, 42].active() && p.inventory[14].stack == 3 && (long)Get(host, "BuildUsesStarted") == uses,
                    "unknown protects both actual furniture members even when replay changes target type and click coordinate");
                var independent = Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone }), 42, 42);
                Until(context, input, () => !Scene(host).Contains(independent));
                Require(p.inventory[14].stack == 2 && Main.tile[42, 42].active() && !Main.tile[44, 41].active() && !Main.tile[44, 42].active(),
                    "footprint protection leaves an independent target of that new type usable");
                Console.WriteLine("PASS Blueprint review unknown complete furniture protects both members across single removal, new identity/source/type; independent native Stone consumes1");
            }
            finally { ReviewUnknownCleanup(context, host, patch); }
        }
        private static void ReviewRemoveOne(object context, object host, object input, object placed, int x, int y)
        {
            // CPU Update/Poll does not simulate Draw's real viewport source
            // preparation. Use the existing WorldMatch CPU view entry before
            // clicking, rather than treating unloaded source as selectable.
            Until(context, input, () =>
            {
                Call(host, "UpdateProjectionWindow", new WorldTargetView(x - 2, y - 2, 8, 8, 0));
                return GetOptional(placed, "Window") != null;
            });
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "RemoveProjection");
            Require(Get(host, "ProjectionMode").ToString() == "SelectingRemove", "real single-remove selection is admitted");
            Call(host, "AcceptWorldPointer", true, false, false, x, y, Main.maxTilesX, Main.maxTilesY);
            Call(host, "AcceptWorldPointer", true, true, false, x, y, Main.maxTilesX, Main.maxTilesY);
            // Hover never initiates IO. If this explicit click requested a
            // missing point core, release, await its real hover and click
            // anew. Source arrival itself must not choose/remove anything.
            if (Scene(host).Contains(placed))
            {
                Call(host, "AcceptWorldPointer", true, false, false, x, y, Main.maxTilesX, Main.maxTilesY);
                Until(context, input, () =>
                {
                    Call(host, "AcceptWorldPointer", true, false, false, x, y, Main.maxTilesX, Main.maxTilesY);
                    return ReferenceEquals(GetOptional(host, "ProjectionHover"), placed);
                });
                Call(host, "AcceptWorldPointer", true, true, false, x, y, Main.maxTilesX, Main.maxTilesY);
            }
            Until(context, input, () => !Scene(host).Contains(placed));
            Require(GetOptional(placed, "Match") == null && GetOptional(placed, "SourceRanges") == null,
                "real single removal releases the retired source/match DTOs while scalar unknown responsibility must remain");
        }
        private static void ReviewFreshPlayerSession(object context)
        {
            var previous = Main.player[Main.myPlayer];
            try { Main.player[Main.myPlayer] = new Player { whoAmI = Main.myPlayer, active = true }; Call(context, "UpdateRuntime"); }
            finally { Main.player[Main.myPlayer] = previous; Call(context, "UpdateRuntime"); }
        }
        private static void ReviewUnknownCleanup(object context, object host, Harmony patch)
        {
            removeEligibilityBody = false; Main.gameMenu = false;
            foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
            Call(host, "SetAutoBuild", false); Call(host, "ClearProjection"); ReviewFreshPlayerSession(context);
        }
    }
}
