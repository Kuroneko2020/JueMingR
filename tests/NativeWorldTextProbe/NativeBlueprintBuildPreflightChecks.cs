using System;
using System.IO;
using System.Linq;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        // Collision.EmptyTile is the locked ordinary-solid oracle. It checks
        // active NPCs and living, non-ghost players; custom chair/wall paths do
        // not inherit this gate. Only native Player.Update writes goal bodies.
        private static void PreflightEntityBlocker(object context, object host, object tools, object input, bool remote, bool afterReady)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 5;
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone },
                new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = TileID.Stone });
            var placed = Place(context, host, input, entry, 42, 42);
            PreflightEntityScope entity = null;
            try
            {
                if (!afterReady) entity = new PreflightEntityScope(remote, 42, 42);
                long started = (long)Get(host, "BuildUsesStarted");
                SetAutomatic(host, input, true);
                if (afterReady)
                {
                    AwaitPrepared(context, host, input);
                    var flight = Get(host, "preparedBuild"); var target = Get(flight, "Target");
                    Require((int)Get(target, "WorldX") == 42 && (int)Get(target, "WorldY") == 42,
                        "the independent ordered source prepared its first supported Stone before the late entity enters");
                    entity = new PreflightEntityScope(remote, 42, 42);
                }
                Require(!Collision.EmptyTile(42, 42) && Collision.EmptyTile(44, 42), "original solid-placement oracle distinguishes the actual blocked and legal destinations");
                Until(context, input, () => Main.tile[44, 42].active());
                for (int i = 0; i < 32; i++) Step(context, input);
                Require(!Main.tile[42, 42].active() && Main.tile[44, 42].type == TileID.Stone && p.inventory[12].stack == 4 &&
                    (long)Get(host, "BuildUsesStarted") == started + 1 && Scene(host).Contains(placed),
                    "known entity refusal never starts or consumes an action, including a formerly Ready target; the legal sibling consumes exactly once");
                var standing = p.position; string tiles = NativeBlueprintProjectionVisualChecks.Snapshot(41, 41, 5, 3);
                entity.MoveAway();
                Require(Collision.EmptyTile(42, 42) && p.position == standing && p.inventory[12].stack == 4 &&
                    NativeBlueprintProjectionVisualChecks.Snapshot(41, 41, 5, 3) == tiles,
                    "recovery changes only the real entity position, with no Tile store, player movement, supply edit, toggle or blueprint change");
                int updates = PreflightRecovered(context, input, () => !Scene(host).Contains(placed));
                Require(Main.tile[42, 42].type == TileID.Stone && p.inventory[12].stack == 3 && (long)Get(host, "BuildUsesStarted") == started + 2,
                    "native entity departure resumes the exact unfinished target through real consumption and full Match retirement");
                Console.WriteLine("Blueprint preflight entity remote=" + remote + " afterReady=" + afterReady + " recoveryUpdates=" + updates + "; only entity moved, old 240-token wait excluded");
            }
            finally { entity?.Dispose(); }
        }

        private static void PreflightAllowedEntityOverlap(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneWall); p.inventory[12].stack = 3;
            int wall = p.inventory[12].createWall; WorldGen.PlaceWall(41, 40, wall, true);
            using (var npc = new PreflightEntityScope(false, 42, 40))
            {
                Require(!Collision.EmptyTile(42, 40), "an active real NPC actually intersects the wall destination");
                var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Wall, WallType = (ushort)wall });
                Place(context, host, input, entry, 42, 40); SetAutomatic(host, input, true);
                Until(context, input, () => Scene(host).Length == 0);
                Require(Main.tile[42, 40].wall == wall && p.inventory[12].stack == 2 && !Collision.EmptyTile(42, 40),
                    "the native wall consumer can place while the intersecting NPC remains; solid-tile collision cannot become a universal veto");
            }
            p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.WoodenChair); p.inventory[12].stack = 2;
            var chair = PreflightChairAsset(context, host, input);
            using (var npc = new PreflightEntityScope(false, 44, 41))
            {
                Require(!Collision.EmptyTile(44, 41) && PreflightChairCanPlace(44, 42, p.inventory[12]),
                    "actual custom-chair CanPlace accepts an NPC overlap rejected by the ordinary solid oracle");
                Place(context, host, input, chair, 44, 41); SetAutomatic(host, input, true);
                Until(context, input, () => Scene(host).Length == 0);
                Require(Main.tile[44, 41].type == TileID.Chairs && Main.tile[44, 42].active() && p.inventory[12].stack == 1,
                    "the real complete chair body consumes once with its NPC still overlapping");
            }
            p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 2;
            using (var ghost = new PreflightEntityScope(true, 44, 42))
            {
                ghost.Remote.ghost = true;
                Require(Collision.EmptyTile(44, 42), "the locked oracle explicitly excludes an active remote ghost from solid obstruction");
                Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone }), 44, 42);
                SetAutomatic(host, input, true); Until(context, input, () => Scene(host).Length == 0);
                Require(Main.tile[44, 42].type == TileID.Stone && p.inventory[12].stack == 1, "remote ghost overlap remains a legal ordinary placement");
            }
        }

        private static void PreflightExistingWorldSupport(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.WoodenChair); p.inventory[12].stack = 2;
            p.inventory[13].SetDefaults(ItemID.StoneBlock); p.inventory[13].stack = 20; p.inventory[14].SetDefaults(ItemID.Wood); p.inventory[14].stack = 30;
            var chairAndFloor = PreflightChairAsset(context, host, input, true);
            WorldGen.KillTile(44, 43, false, false, true); Require(WorldGen.PlaceTile(44, 43, TileID.WoodBlock, true, true), "pre-existing Wood is created before enabling the Stone-floor blueprint");
            Require(PreflightChairCanPlace(44, 42, p.inventory[12]), "original chair metadata accepts the existing Wood floor despite the blueprint's Stone target");
            var placed = Place(context, host, input, chairAndFloor, 44, 41); long started = (long)Get(host, "BuildUsesStarted");
            SetAutomatic(host, input, true); Until(context, input, () => Main.tile[44, 41].active() && Main.tile[44, 42].active());
            for (int i = 0; i < 48; i++) Step(context, input);
            Require(Main.tile[44, 43].type == TileID.WoodBlock && p.inventory[12].stack == 1 && p.inventory[13].stack == 20 && p.inventory[14].stack == 30 &&
                Scene(host).Contains(placed) && (long)Get(Get(placed, "Match"), "Targets") == 3 && (long)Get(Get(placed, "Match"), "Matches") == 2 &&
                (long)Get(host, "BuildUsesStarted") == started + 1,
                "only the supported chair consumes: the existing wrong Wood remains, its own Stone target stays unmatched, and the mixed instance cannot retire");

            p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.WoodenChair); p.inventory[12].stack = 2;
            p.inventory[13].SetDefaults(ItemID.StoneBlock); p.inventory[13].stack = 20; p.inventory[14].SetDefaults(ItemID.Wood); p.inventory[14].stack = 30;
            var chairOnly = PreflightChairAsset(context, host, input);
            Place(context, host, input, chairOnly, 44, 40);
            Require(!PreflightChairCanPlace(44, 41, p.inventory[12]), "original chair metadata rejects the genuinely missing anchor at 44,42");
            started = (long)Get(host, "BuildUsesStarted"); long checkedBefore = (long)Get(host, "BuildCandidateChecks"); SetAutomatic(host, input, true);
            Until(context, input, () => (long)Get(host, "BuildCandidateChecks") > checkedBefore);
            for (int i = 0; i < 80; i++) Step(context, input);
            Require((long)Get(host, "BuildUsesStarted") == started && p.inventory[12].stack == 2 && p.inventory[13].stack == 20 && p.inventory[14].stack == 30 &&
                !Main.tile[44, 40].active() && !Main.tile[44, 41].active() && !Main.tile[44, 42].active(),
                "a missing anchor causes zero starts and zero consumption even with abundant ordinary blocks; no extra support is placed");
            Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone }), 44, 42);
            Until(context, input, () => Scene(host).Length == 0);
            Require(Main.tile[44, 42].type == TileID.Stone && Main.tile[44, 40].type == TileID.Chairs && p.inventory[12].stack == 1 && p.inventory[13].stack == 19 && p.inventory[14].stack == 30,
                "a newly requested correct blueprint support builds normally and naturally unlocks the waiting chair without an auxiliary placement role");
        }

        private static void PreflightActuatedAnchor(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.WoodenChair); p.inventory[12].stack = 2;
            p.inventory[13].SetDefaults(ItemID.StoneBlock); p.inventory[13].stack = 3;
            var chair = PreflightChairAsset(context, host, input);
            Wiring.ActuateForced(44, 43);
            Require(Main.tile[44, 43].inActive() && !PreflightChairCanPlace(44, 42, p.inventory[12]),
                "actual native actuation makes the apparently solid floor fail the original chair's nactive anchor rule");
            Place(context, host, input, chair, 44, 41);
            Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone }), 42, 42);
            long started = (long)Get(host, "BuildUsesStarted"); SetAutomatic(host, input, true);
            Until(context, input, () => Main.tile[42, 42].active()); for (int i = 0; i < 32; i++) Step(context, input);
            Require(!Main.tile[44, 41].active() && p.inventory[12].stack == 2 && p.inventory[13].stack == 2 && (long)Get(host, "BuildUsesStarted") == started + 1,
                "an invalid actuated anchor starts no chair action while the separate supported Stone consumes normally");
            Wiring.ActuateForced(44, 43);
            Require(!Main.tile[44, 43].inActive() && PreflightChairCanPlace(44, 42, p.inventory[12]), "the original reactivation writer restores the actual anchor qualification");
            PreflightRecovered(context, input, () => Scene(host).Length == 0);
            Require(p.inventory[12].stack == 1 && p.inventory[13].stack == 2 && Main.tile[44, 41].type == TileID.Chairs,
                "anchor-state recovery resumes the original full chair without rebuilding the blueprint or consuming extra supports");
        }

        private static void PreflightPreparedSupport(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 5;
            Require(WorldGen.PlaceTile(42, 41, TileID.Stone, true, true), "prepared target starts with an independently existing support");
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone },
                new CaptureCell { X = 2, Y = 2, Layers = CaptureLayer.Tile, TileType = TileID.Stone });
            Place(context, host, input, entry, 42, 40); SetAutomatic(host, input, true); AwaitPrepared(context, host, input);
            var target = Get(Get(host, "preparedBuild"), "Target");
            Require((int)Get(target, "WorldX") == 42 && (int)Get(target, "WorldY") == 40, "the first independently ordered goal has a real Ready flight before its support is removed");
            long started = (long)Get(host, "BuildUsesStarted"); WorldGen.KillTile(42, 41, false, false, true);
            Require(!Main.tile[42, 41].active(), "the original world writer really removes the prepared support before the next Player.Update");
            Until(context, input, () => Main.tile[44, 42].active()); for (int i = 0; i < 32; i++) Step(context, input);
            Require(!Main.tile[42, 40].active() && p.inventory[12].stack == 4 && (long)Get(host, "BuildUsesStarted") == started + 1,
                "the stale Ready cannot start after support removal; its independent legal sibling still builds once");
            Require(WorldGen.PlaceTile(42, 41, TileID.Stone, true, true), "only the original support is restored through the native writer");
            PreflightRecovered(context, input, () => Scene(host).Length == 0);
            Require(Main.tile[42, 40].type == TileID.Stone && p.inventory[12].stack == 3, "the same unfinished target resumes from current support evidence and real native consumption");
        }

        private static void PreflightLiquidQualification(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 5;
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile | CaptureLayer.Liquid, TileType = TileID.Stone, LiquidAmount = 20 },
                new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = TileID.Stone });
            PreflightLiquidStore(42, 42, 80, 1);
            Require(PreflightLavaBlocked(p, 42, 42, p.inventory[12]) && !PreflightLavaBlocked(p, 44, 42, p.inventory[12]),
                "the original Player lava gate distinguishes the solid destination independently of blueprint liquid amount");
            Place(context, host, input, entry, 42, 42); long started = (long)Get(host, "BuildUsesStarted"); SetAutomatic(host, input, true);
            Until(context, input, () => Main.tile[44, 42].active()); for (int i = 0; i < 32; i++) Step(context, input);
            Require(!Main.tile[42, 42].active() && p.inventory[12].stack == 4 && (long)Get(host, "BuildUsesStarted") == started + 1,
                "known lava rejection causes zero blocked actions while the dry Stone really consumes once");
            PreflightLiquidStore(42, 42, 0, 0); Require(!PreflightLavaBlocked(p, 42, 42, p.inventory[12]), "native liquid packet removal clears the actual Player refusal");
            PreflightRecovered(context, input, () => Scene(host).Length == 0);
            Require(Main.tile[42, 42].type == TileID.Stone && p.inventory[12].stack == 3 && Main.tile[42, 42].liquid == 0,
                "BuildSupport still wakes on real liquid recovery, and absent display liquid does not block final retirement");

            p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.WoodenChair); p.inventory[12].stack = 2;
            var chair = PreflightChairAsset(context, host, input); PreflightLiquidStore(44, 42, 80, 1);
            Require(!PreflightChairCanPlace(44, 42, p.inventory[12]) && PreflightLavaBlocked(p, 44, 42, p.inventory[12]),
                "the original object liquid rule and Player lava gate both reject this otherwise anchored wooden chair");
            Place(context, host, input, chair, 44, 41); started = (long)Get(host, "BuildUsesStarted"); long checkedBefore = (long)Get(host, "BuildCandidateChecks"); SetAutomatic(host, input, true);
            Until(context, input, () => (long)Get(host, "BuildCandidateChecks") > checkedBefore);
            for (int i = 0; i < 80; i++) Step(context, input);
            Require((long)Get(host, "BuildUsesStarted") == started && p.inventory[12].stack == 2 && !Main.tile[44, 41].active(),
                "a known object liquid refusal is filtered before any native use rather than discovered by an empty swing");
            PreflightLiquidStore(44, 42, 0, 0); Require(PreflightChairCanPlace(44, 42, p.inventory[12]), "the native liquid writer restores the chair's actual current eligibility");
            PreflightRecovered(context, input, () => Scene(host).Length == 0);
            Require(Main.tile[44, 41].type == TileID.Chairs && Main.tile[44, 42].active() && p.inventory[12].stack == 1,
                "the original chair consumer resumes and spends once after liquid recovery");
        }

        private static LibraryEntry PreflightChairAsset(object context, object host, object input, bool withFloor = false)
        {
            Require(WorldGen.PlaceObject(60, 42, TileID.Chairs, true, 0, direction: -1), "preflight source is a real left-facing chair on the native Stone floor");
            return NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, (LibraryState)Get(host, "Library"),
                () => Step(context, input), 60, 41, 1, withFloor ? 3 : 2, withFloor ? "椅子与石地面" : "真实预检椅子");
        }
        private static bool PreflightChairCanPlace(int x, int y, Item item)
        {
            // Run before automatic use owns any original placement state. The
            // real non-random chair oracle supplies the anchor/liquid result;
            // it does not create the destination object or authorize the Host.
            TileObject choice; return TileObject.CanPlace(x, y, item.createTile, item.placeStyle, -1, out choice, false, 0);
        }
        private static bool PreflightLavaBlocked(Player player, int x, int y, Item item)
        { return (bool)typeof(Player).GetMethod("PlaceThing_Tiles_IsBlockedByLava", F).Invoke(player, new object[] { item.createTile, item.placeStyle, Main.tile[x, y] }); }
        private static int PreflightRecovered(object context, object input, Func<bool> complete)
        {
            int updates = 0; Until(context, input, () => { updates++; return complete(); });
            Require(updates < 240, "an already sampled tiny target must recover through current qualification before the old 240-token generic rejection delay; updates=" + updates);
            return updates;
        }
        private static void PreflightLiquidStore(int x, int y, byte amount, byte kind)
        {
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true)) { writer.Write((ushort)1); writer.Write((x << 16) | y); writer.Write(amount); writer.Write(kind); }
                bytes.Position = 0; using (var reader = new BinaryReader(bytes)) new Terraria.GameContent.NetModules.NetLiquidModule().Deserialize(reader, 256);
            }
        }
        private sealed class PreflightEntityScope : IDisposable
        {
            private readonly NPC oldNpc;
            private readonly Player oldRemote;
            private readonly NPC npc;
            internal readonly Player Remote;
            internal PreflightEntityScope(bool remote, int x, int y)
            {
                if (remote)
                {
                    oldRemote = Main.player[1]; Remote = new Player { whoAmI = 1, active = true, dead = false, ghost = false };
                    Remote.position = new Vector2(x * 16, (y + 1) * 16 - Remote.height);
                    Main.player[1] = Remote;
                }
                else
                {
                    oldNpc = Main.npc[0]; npc = new NPC(); npc.SetDefaults(NPCID.Bunny); npc.whoAmI = 0; npc.active = true; npc.position = new Vector2(x * 16, (y + 1) * 16 - npc.height); Main.npc[0] = npc;
                }
            }
            internal void MoveAway() { if (Remote != null) Remote.position = new Vector2(90 * 16, 43 * 16 - Remote.height); else npc.position = new Vector2(90 * 16, 43 * 16 - npc.height); }
            public void Dispose() { if (Remote != null) Main.player[1] = oldRemote; else Main.npc[0] = oldNpc; }
        }
    }
}
