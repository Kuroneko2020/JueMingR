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
        private static readonly int[] ordinaryNonSolidHammerTypes = {
            TileID.LivingFire, TileID.LivingCursedFire, TileID.LivingDemonFire, TileID.LivingFrostFire,
            TileID.LivingIchor, TileID.LivingUltrabrightFire, TileID.ChimneySmoke
        };
        private static readonly int[] ordinaryNonSolidHammerItems = {
            ItemID.LivingFireBlock, ItemID.LivingCursedFireBlock, ItemID.LivingDemonFireBlock, ItemID.LivingFrostFireBlock,
            ItemID.LivingIchorBlock, ItemID.LivingUltrabrightFireBlock, ItemID.SmokeBlock
        };
        private static bool hammerEligibilityWatch;
        private static readonly Dictionary<Point, int> hammerNativeAttempts = new Dictionary<Point, int>();
        private static readonly Dictionary<Point, int> hammerNativeChanges = new Dictionary<Point, int>();

        private static void HammerEligibilityBefore(Player __instance, int __3, int __4, out int __state)
        {
            __state = hammerEligibilityWatch && ReferenceEquals(__instance, Main.LocalPlayer) && __instance.poundRelease ?
                HammerShape(Main.tile[__3, __4]) : -1;
        }
        private static void HammerEligibilityAfter(bool __runOriginal, int __3, int __4, int __state)
        {
            if (!__runOriginal || __state < 0) return;
            var point = new Point(__3, __4); int count;
            hammerNativeAttempts[point] = hammerNativeAttempts.TryGetValue(point, out count) ? count + 1 : 1;
            if (HammerShape(Main.tile[__3, __4]) != __state)
                hammerNativeChanges[point] = hammerNativeChanges.TryGetValue(point, out count) ? count + 1 : 1;
        }
        private static int HammerShape(Tile tile) { return tile.slope() | (tile.halfBrick() ? 8 : 0); }
        private static int HammerAttempts(int x, int y)
        { int count; return hammerNativeAttempts.TryGetValue(new Point(x, y), out count) ? count : 0; }
        private static void StartHammerEligibilityWatch()
        { hammerNativeAttempts.Clear(); hammerNativeChanges.Clear(); hammerEligibilityWatch = true; }

        private static void ConstructionHammerEligibility(object context, object host, object tools, object input)
        {
            var patch = new Harmony("JueMingR.Tests.OrdinaryHammerEligibility");
            var self = typeof(NativeBlueprintBuildChecks);
            patch.Patch(typeof(Player).GetMethod("ItemCheck_UseMiningTools_TryPoundingTile", F),
                prefix: new HarmonyMethod(self, nameof(HammerEligibilityBefore)),
                postfix: new HarmonyMethod(self, nameof(HammerEligibilityAfter)));
            patch.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod("NotifyProgressionEvent", F),
                prefix: new HarmonyMethod(self, nameof(SkipOutlet)));
            try
            {
                OrdinaryHammerEligibilityContract(context, host, tools, input);
                for (int i = 0; i < ordinaryNonSolidHammerTypes.Length; i++)
                    ConstructionOrdinaryNonSolidHammer(context, host, tools, input, i);
                ConstructionCapturedOrdinaryHammer(context, host, tools, input);
                foreach (int side in new[] { -1, 1 }) ConstructionLockedDoorHammer(context, host, tools, input, side);
            }
            finally
            {
                hammerEligibilityWatch = false;
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                hammerNativeAttempts.Clear(); hammerNativeChanges.Clear();
            }
        }

        private static void OrdinaryHammerEligibilityContract(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input);
            // The original player predicate establishes poundability, while
            // its separate special consumers establish the exclusion set.
            foreach (int type in ordinaryNonSolidHammerTypes)
            {
                var tile = new Tile(); tile.type = (ushort)type; tile.active(true);
                Require(!Main.tileSolid[type] && !TileID.Sets.Platforms[type] && (bool)Call(p, "IsTilePoundable", tile) &&
                    (bool)Call(host, "OrdinaryHammerShapeType", type), "all seven original non-solid ordinary hammer types qualify: " + type);
            }
            foreach (int type in new[] { TileID.Traps, TileID.MinecartTrack, TileID.WirePipe, TileID.ProjectilePressurePad, TileID.DisplayDoll, 131 })
                Require(!(bool)Call(host, "OrdinaryHammerShapeType", type), "special hammer consumer or save-only slope is excluded: " + type);

            var assembly = host.GetType().Assembly;
            var target = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintBuildTarget", true), true);
            var operation = Activator.CreateInstance(assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintBuildOperation", true), true);
            Set(target, "Cell", new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone, HalfBrick = true });
            Set(operation, "Kind", Enum.Parse(operation.GetType().GetField("Kind", F).FieldType, "Hammer")); Set(target, "Operation", operation);
            var tool = new Item(); tool.SetDefaults(ItemID.MoltenHamaxe);
            Require((bool)Call(host, "AppearanceToolMatches", target, tool), "ordinary hamaxe remains legal for a non-axe body");
            foreach (string forbidden in new[] { "pick", "channel", "shoot" })
            {
                tool.SetDefaults(ItemID.WoodenHammer);
                if (forbidden == "pick") tool.pick = 1;
                else if (forbidden == "channel") tool.channel = true;
                else tool.shoot = 1;
                Require(!(bool)Call(host, "AppearanceToolMatches", target, tool), "shape capability does not admit destructive/projectile/channel tool: " + forbidden);
            }
            Console.WriteLine("PASS OrdinaryHammerEligibility sevenTypes=336,340,341,342,343,344,351 specialAndSaveOnlyExcluded=true toolGuards=true");
        }

        private static void ConstructionOrdinaryNonSolidHammer(object context, object host, object tools, object input, int index)
        {
            var p = Reset(context, host, tools, input);
            p.inventory[12].SetDefaults(ItemID.WoodenHammer); var hammer = p.inventory[12];
            p.inventory[13].SetDefaults(ordinaryNonSolidHammerItems[index]); p.inventory[13].stack = 7; var material = p.inventory[13];
            int type = ordinaryNonSolidHammerTypes[index];
            Require(material.createTile == type && WorldGen.PlaceTile(44, 42, type, mute: true, forced: true, plr: 0, style: material.placeStyle),
                "non-solid source is a genuine original material placement: " + type);
            WorldGen.PlaceWall(44, 42, WallID.Stone);
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile | CaptureLayer.Wall,
                TileType = (ushort)type, WallType = WallID.Stone, HalfBrick = true });
            Place(context, host, input, entry, 44, 42);
            long unknown = (long)Get(host, "BuildUnknownResults"); StartHammerEligibilityWatch();
            SetAutomatic(host, input, true);
            Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"));
            hammerEligibilityWatch = false;
            var tile = Main.tile[44, 42];
            Require(tile.active() && tile.type == type && tile.halfBrick() && tile.slope() == 0 && tile.wall == WallID.Stone &&
                HammerAttempts(44, 42) > 0 && hammerNativeChanges.ContainsKey(new Point(44, 42)) &&
                ReferenceEquals(p.inventory[12], hammer) && hammer.type == ItemID.WoodenHammer && hammer.stack == 1 &&
                ReferenceEquals(p.inventory[13], material) && material.stack == 7 && (long)Get(host, "BuildUnknownResults") == unknown,
                "native neutral/pound reaches a non-solid half brick and retires complete Match without altering body/wall or quantity: " + type);
            Console.WriteLine("PASS OrdinaryNonSolidHammer type=" + type + " nativeAttempts=" + HammerAttempts(44, 42) + " fullMatchRetired=true bodySpent=0");
        }

        private static void ConstructionCapturedOrdinaryHammer(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
            int[] dx = { 0, 1, 2, 0, 2 }, dy = { 0, 0, 0, 1, 1 };
            for (int i = 0; i < dx.Length; i++)
            {
                int x = 60 + dx[i], y = 41 + dy[i];
                Require(WorldGen.PlaceTile(x, y, TileID.LivingFire, mute: true, forced: true, plr: 0), "capture source uses original Living Fire placement");
                WorldGen.PlaceWall(x, y, WallID.Stone);
                Require(i == 0 ? WorldGen.PoundTile(x, y) : WorldGen.SlopeTile(x, y, i), "source has original half/four-slope state before formal capture");
            }
            var library = (LibraryState)Get(host, "Library");
            var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, library,
                () => Step(context, input), 60, 41, 3, 2, "正式非实心半砖四坡");
            var saved = OpenCapturedAppearance(library, entry);
            Require(saved.Cells.Count == 5 && Enumerable.Range(0, dx.Length).All(i =>
            {
                var cell = saved.Cells.Single(c => c.X == dx[i] && c.Y == dy[i]); var source = Main.tile[60 + dx[i], 41 + dy[i]];
                return cell.TileType == TileID.LivingFire && cell.Slope == source.slope() && cell.HalfBrick == source.halfBrick() &&
                    cell.WallType == source.wall && cell.FrameX == source.frameX && cell.FrameY == source.frameY;
            }), "formal CaptureJob/save/open preserves five actual ordinary non-solid shapes, raw frames and walls");

            p = Reset(context, host, tools, input); p.position.X = 42 * 16;
            p.inventory[12].SetDefaults(ItemID.WoodenHammer); var hammer = p.inventory[12];
            p.inventory[13].SetDefaults(ItemID.LivingFireBlock); p.inventory[13].stack = 7; var material = p.inventory[13];
            for (int i = 0; i < dx.Length; i++)
            {
                int x = 44 + dx[i], y = 41 + dy[i];
                Require(WorldGen.PlaceTile(x, y, TileID.LivingFire, mute: true, forced: true, plr: 0) &&
                    p.IsInTileInteractionRange(x, y, Terraria.DataStructures.TileReachCheckSettings.Simple, hammer.tileBoost) &&
                    !p.Hitbox.Intersects(new Rectangle(x * 16, y * 16, 16, 16)), "every flat destination is in actual hammer reach and outside the player");
                WorldGen.PlaceWall(x, y, WallID.Stone);
            }
            // These real neighbours reverse the original horizontal and
            // vertical cycle preference; the planner cannot count fixed hits.
            NativeToolsChecks.Tile(47, 41, TileID.Stone); NativeToolsChecks.Tile(46, 40, TileID.Stone);
            var position = p.position; long unknown = (long)Get(host, "BuildUnknownResults");
            Place(context, host, input, entry, 44, 41); StartHammerEligibilityWatch(); SetAutomatic(host, input, true);
            Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"));
            hammerEligibilityWatch = false;
            Require(Enumerable.Range(0, dx.Length).All(i =>
            {
                var tile = Main.tile[44 + dx[i], 41 + dy[i]];
                return tile.active() && tile.type == TileID.LivingFire && tile.wall == WallID.Stone &&
                    tile.halfBrick() == (i == 0) && tile.slope() == i && HammerAttempts(44 + dx[i], 41 + dy[i]) > 0;
            }) && hammerNativeAttempts.Values.Sum() > 5 && material.stack == 7 && ReferenceEquals(p.inventory[13], material) &&
                hammer.stack == 1 && ReferenceEquals(p.inventory[12], hammer) && p.position == position &&
                Main.tile[47, 41].type == TileID.Stone && Main.tile[46, 40].type == TileID.Stone &&
                (long)Get(host, "BuildUnknownResults") == unknown,
                "actual native cycles complete the reopened half/four slopes despite adjacency, preserving all bodies/walls/tools/materials and full Match");
            long settled = (long)Get(host, "BuildUsesStarted");
            for (int frame = 0; frame < 80; frame++) Step(context, input);
            Require(Scene(host).Length == 0 && (long)Get(host, "BuildUsesStarted") == settled && material.stack == 7,
                "complete captured Match retires the instance and cannot replay later empty hammering");
            Console.WriteLine("PASS CapturedOrdinaryHammer asset=" + entry.AssetId + " revision=" + entry.ContentRevision +
                " shapeCount=5 nativeAttempts=" + hammerNativeAttempts.Values.Sum() + " neighborCycle=true fullMatchRetired=true bodySpent=0");
        }

        private static void ConstructionLockedDoorHammer(object context, object host, object tools, object input, int side)
        {
            var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
            p.inventory[12].SetDefaults(ItemID.WoodenHammer); var hammer = p.inventory[12];
            p.inventory[13].SetDefaults(ItemID.StoneBlock); p.inventory[13].stack = 7; var material = p.inventory[13];
            NativeToolsChecks.Tile(44, 42, TileID.Stone); NativeToolsChecks.Tile(46, 42, TileID.Stone);
            WorldGen.PlaceWall(44, 42, WallID.Stone); WorldGen.PlaceWall(46, 42, WallID.Stone);
            int middle = 42 + side * 2;
            for (int y = middle - 1; y <= middle + 1; y++) Main.tile[44, y].ClearEverything();
            NativeToolsChecks.Tile(44, 42 + side * 4, TileID.Stone);
            Require(WorldGen.PlaceDoor(44, middle, TileID.ClosedDoor, 11) && WorldGen.IsLockedDoor(44, 42 + side) && WorldGen.CanPoundTile(44, 42),
                "an original complete locked door " + (side < 0 ? "above" : "below") + " independently blocks player pounding despite CanPoundTile=true");
            short[] lockedX = Enumerable.Range(middle - 1, 3).Select(y => Main.tile[44, y].frameX).ToArray();
            short[] lockedY = Enumerable.Range(middle - 1, 3).Select(y => Main.tile[44, y].frameY).ToArray();
            var entry = Asset(host,
                new CaptureCell { Layers = CaptureLayer.Tile | CaptureLayer.Wall, TileType = TileID.Stone, WallType = WallID.Stone, HalfBrick = true },
                new CaptureCell { X = 2, Layers = CaptureLayer.Tile | CaptureLayer.Wall, TileType = TileID.Stone, WallType = WallID.Stone, HalfBrick = true });
            Place(context, host, input, entry, 44, 42); long unknown = (long)Get(host, "BuildUnknownResults");
            StartHammerEligibilityWatch(); SetAutomatic(host, input, true);
            Until(context, input, () => Main.tile[46, 42].halfBrick() && !(bool)Get(Get(tools, "Use"), "Active"));
            long settled = (long)Get(host, "BuildUsesStarted");
            for (int frame = 0; frame < 80; frame++) Step(context, input);
            Require(HammerAttempts(44, 42) == 0 && HammerAttempts(46, 42) > 0 && !Main.tile[44, 42].halfBrick() && Scene(host).Length == 1 &&
                (long)Get(host, "BuildUsesStarted") == settled && material.stack == 7 && hammer.stack == 1 &&
                Enumerable.Range(0, 3).All(i => Main.tile[44, middle - 1 + i].active() && Main.tile[44, middle - 1 + i].type == TileID.ClosedDoor &&
                    Main.tile[44, middle - 1 + i].frameX == lockedX[i] && Main.tile[44, middle - 1 + i].frameY == lockedY[i]) &&
                Main.tile[44, 42].wall == WallID.Stone && Main.tile[46, 42].wall == WallID.Stone,
                "locked neighbour causes zero real hammer starts, leaves the door intact and allows the independent target to finish without repeated empty uses");
            // A real external unlock, with no player movement or auto toggle,
            // must wake the existing world-change / bounded eligibility path.
            WorldGen.UnlockDoor(44, 42 + side);
            Require(!WorldGen.IsLockedDoor(44, 42 + side), "original unlock really removes the local rejection");
            Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"));
            hammerEligibilityWatch = false;
            Require(HammerAttempts(44, 42) > 0 && Main.tile[44, 42].active() && Main.tile[44, 42].type == TileID.Stone && Main.tile[44, 42].halfBrick() &&
                Main.tile[44, 42].wall == WallID.Stone && Main.tile[46, 42].wall == WallID.Stone && material.stack == 7 && hammer.stack == 1 &&
                ReferenceEquals(p.inventory[12], hammer) && ReferenceEquals(p.inventory[13], material) && (long)Get(host, "BuildUnknownResults") == unknown,
                "unlock resumes real hammering and full Match retirement without material loss, wall removal or false Unknown");
            Console.WriteLine("PASS LockedDoorHammer side=" + side + " nativeWhileLocked=0 independentTargetCompleted=true nativeAfterUnlock=" +
                HammerAttempts(44, 42) + " fullMatchRetired=true");
        }
    }
}
