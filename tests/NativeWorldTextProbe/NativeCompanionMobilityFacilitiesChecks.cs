using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.NetModules;
using Terraria.ID;
using Terraria.Net;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionMobilityChecks
    {
        private static void ContinuousRope(object host, Player body, Type frame)
        {
            Ground(body, 101);
            var tiles = new Dictionary<Point, Tile>();
            try
            {
                for (int y = 63; y <= 71; y++) FixtureTile(tiles, 101, y, TileID.Rope);
                Set(frame, vertical: -1);
                float ground = body.position.Y;
                for (int i = 0; i < 16; i++) Step(host, body);
                Require(body.pulley && body.position.Y < ground - 10, "continuous native Up attaches and climbs through real rope segments");
                Set(frame, jump: true, move: 1);
                Step(host, body);
                Require(!body.pulley, "real Jump leaves the rope and releases ownership of horizontal movement");
            }
            finally { RestoreTiles(tiles); }
        }

        private static void OpenDoor(object host, Player body, Type frame, object state)
        {
            Ground(body, 104);
            const int doorX = 106;
            var tiles = new Dictionary<Point, Tile>();
            try
            {
                FixtureTile(tiles, doorX, 69, TileID.Stone);
                for (int r = 0; r < 3; r++)
                {
                    FixtureTile(tiles, doorX, 70 + r, TileID.ClosedDoor).frameY = (short)(r * 18);
                    FixtureTile(tiles, doorX + 1, 70 + r);
                    FixtureTile(tiles, doorX - 1, 70 + r);
                }

                Set(frame, "Door", 2011, new Vector2(doorX * 16 + 8, 71 * 16 + 8), move: 1);
                for (int i = 0; i < 45 && body.Center.X < doorX * 16 + 34; i++) Step(host, body);
                Require(body.Center.X >= doorX * 16 + 34 && Get(state, "Result").ToString() == "Completed",
                    "native auto-opening handler opens the named door as the body reaches its threshold");
            }
            finally { RestoreTiles(tiles); }
        }

        private static void RideExistingTrack(object host, Player body, Type frame, object state)
        {
            Ground(body, 102);
            var tiles = new Dictionary<Point, Tile>();
            try
            {
                bool dedicated = Main.dedServ;
                try { Main.dedServ = true; Minecart.Initialize(); }
                finally { Main.dedServ = dedicated; }
                NativeCombatLiveContextChecks.InitializeMount();
                for (int x = 104; x <= 114; x++) Minecart.PlaceTrack(FixtureTile(tiles, x, 71), 0);
                for (int x = 104; x <= 114; x++) Minecart.FrameTrack(x, 71, pound: false, mute: true);
                Set(frame);
                Step(host, body);
                Set(frame, "QuickCart", 2012, new Vector2(105 * 16 + 8, 71 * 16 + 8));
                Step(host, body);
                var hook = (Projectile)Get(state, "Hook");
                Require(hook.active && hook.type == ProjectileID.TrackHook && !body.mount.Active, "track request launches the real cart hook before mounting");
                for (int tick = 0; tick < 60 && Get(state, "Result").ToString() != "Completed"; tick++)
                {
                    AdvanceHook(hook);
                    Step(host, body);
                }
                Require(body.mount.Active && body.mount.Cart && Get(state, "Result").ToString() == "Completed",
                    "track hook reaches the named rail and native GrappleMovement mounts the actual cart");
                Vector2 onTrack = body.position;
                bool nativeRail = Minecart.OnTrack(body.position, body.width, body.height, body.MinecartSettings);
                float acceleration = body.mount.Acceleration;
                Set(frame, move: 1);
                // The default wooden cart accelerates at .03 px/step².
                // Allow its real gentle start before checking displacement.
                for (int tick = 0; tick < 24; tick++) Step(host, body);
                Require(body.position.X > onTrack.X + 4 && body.mount.Cart && body.velocity.X > 0 && body.IsRidingTracks,
                    "native track accel moves the mounted body along rails: " + onTrack + " => " + body.position +
                    ", velocity=" + body.velocity + ", accel=" + acceleration + ", cart=" + body.mount.Cart +
                    ", track=" + nativeRail + "/" + body.onTrack + ", control=" + body.controlLeft + "/" + body.controlRight +
                    ", frames=" + Main.tile[104, 71].frameX + "/" + Main.tile[105, 71].frameX + "/" + Main.tile[106, 71].frameX);
                Set(frame, "Dismount", 2013);
                Step(host, body);
                Require(!body.mount.Active && Get(state, "Result").ToString() == "Completed", "native single dismount restores the actual standing body");
            }
            finally { RestoreTiles(tiles); }
        }

        private static void DedicatedSwitch(object host, Player body, Player human, Type frame, object state)
        {
            Ground(body, 97);
            var tiles = new Dictionary<Point, Tile>();
            Vector2 originalHuman = human.position;
            try
            {
                Wiring.Initialize();
                var switchTile = FixtureTile(tiles, 96, 70, TileID.Switches);
                switchTile.wire(true);
                for (int k = 0; k < 3; k++)
                {
                    FixtureTile(tiles, 96 + k, 73, TileID.Teleporter).frameX = (short)(k * 18);
                    FixtureTile(tiles, 114 + k, 73, TileID.Teleporter).frameX = (short)(k * 18);
                }
                for (int x = 96; x <= 115; x++) FixtureTile(tiles, x, 69).wire(true);
                for (int y = 71; y < 73; y++) FixtureTile(tiles, 96, y).wire(true);
                for (int y = 70; y < 73; y++) FixtureTile(tiles, 115, y).wire(true);
                Main.tile[96, 73].wire(true);
                Main.tile[115, 73].wire(true);
                var trigger = new Vector2(96 * 16 + 8, 70 * 16 + 8);
                human.position = new Vector2(115 * 16, 73 * 16 - human.height);
                Set(frame, "Teleporter", 2014, trigger);
                Step(host, body);
                Require(switchTile.frameY == 0 && body.Center.X < 100 * 16 && Get(state, "Result").ToString() == "Rejected",
                    "the actual switch remains untouched when the exit would move the human");
                human.position = originalHuman;
                Set(frame, "Teleporter", 2015, trigger);
                Step(host, body);
                Require(switchTile.frameY == 18 && Math.Abs(body.position.X - 115 * 16) < 2 && Get(state, "Result").ToString() == "Completed",
                    "one real switch pulse moves this body between the two verified vanilla wired pads");
            }
            finally { human.position = originalHuman; RestoreTiles(tiles); }
        }

        private static void CompactMount(object host, Player body, Type frame, object state)
        {
            Ground(body, 101);
            var tiles = new Dictionary<Point, Tile>();
            const int slot = 24;
            body.inventory[slot] = Gear(ItemID.RatMountItem);
            try
            {
                Set(frame);
                Step(host, body);
                Set(frame, "QuickMount", 2016, slot: slot, mountType: MountID.Rat);
                Step(host, body);
                Require(body.mount.Active && body.mount.Type == MountID.Rat && body.width == 14 && body.height == 14,
                    "the exact backpack mount goes through held-item ItemCheck and adopts the native Rat size");
                Set(frame);
                for (int tick = 0; tick < 32; tick++) Step(host, body);
                FixtureTile(tiles, 101, 71, TileID.Stone);
                var underCeiling = body.position;
                Set(frame, "Dismount", 2017);
                for (int tick = 0; tick < 5; tick++) Step(host, body);
                Require(body.mount.Active && body.height == 14 && Vector2.DistanceSquared(underCeiling, body.position) < 9 &&
                    Get(state, "Result").ToString() == "Rejected", "a low ceiling cannot cause triple-press forced dismount and teleport");
                FixtureTile(tiles, 101, 71);
                Set(frame, "Dismount", 2018);
                Step(host, body);
                Require(!body.mount.Active && body.height == 42, "normal size returns only once native unmount clearance exists");
            }
            finally { RestoreTiles(tiles); }
        }

        private static void ExistingPortal(object host, Player body, Player human, Type frame, object state)
        {
            Ground(body, 101);
            int startSlot = Array.FindIndex(Main.projectile, shot => shot != null && !shot.active);
            int endSlot = Array.FindIndex(Main.projectile, startSlot + 1, shot => shot != null && !shot.active);
            Require(startSlot >= 0 && endSlot > startSlot, "the isolated projectile fixture has room for an existing portal pair");
            var originalStart = Main.projectile[startSlot];
            var originalEnd = Main.projectile[endSlot];
            Vector2 entry = body.Center;
            Vector2 exit = new Vector2(112 * 16 + 8, 69 * 16 + 8);
            try
            {
                Main.projectile[startSlot] = Portal(entry, human.whoAmI, startSlot, 0, MathHelper.PiOver2);
                PortalHelper.UpdatePortalPoints();
                Set(frame, "Portal", 2030, entry);
                Step(host, body);
                Require(Math.Abs(body.Center.X - entry.X) < 2 && Get(state, "Result").ToString() == "Deferred",
                    "a real unpaired portal cannot move the body");
                Main.projectile[endSlot] = Portal(exit, human.whoAmI, endSlot, 1, -MathHelper.PiOver2);
                PortalHelper.UpdatePortalPoints();
                Set(frame, "Portal", 2031, entry);
                Step(host, body);
                Require(Vector2.DistanceSquared(body.Center, exit) < 30 * 30 && Get(state, "Result").ToString() == "Completed",
                    "native PortalHelper consumes matching endpoints and moves this actor to the paired exit");
            }
            finally
            {
                Main.projectile[startSlot] = originalStart;
                Main.projectile[endSlot] = originalEnd;
                PortalHelper.UpdatePortalPoints();
            }
        }

        private static Projectile Portal(Vector2 position, int owner, int slot, int side, float angle)
        {
            var shot = new Projectile();
            shot.SetDefaults(ProjectileID.PortalGunGate);
            shot.active = true;
            shot.owner = owner;
            shot.whoAmI = slot;
            shot.Center = position;
            shot.ai[0] = angle;
            shot.ai[1] = side;
            return shot;
        }

        private static void PylonLoopback(object host, Player body, Player human, Type frame, object state)
        {
            Ground(body, 97);
            var tiles = new Dictionary<Point, Tile>();
            var originalSystem = Main.PylonSystem;
            var npc0 = Main.npc[0];
            var npc1 = Main.npc[1];
            var ownerPosition = human.position;
            const int destinationX = 113, pylonY = 69;
            try
            {
                Main.PylonSystem = new TeleportPylonsSystem();
                if (NetManager.Instance.GetModule<NetTeleportPylonModule>() == null) NetManager.Instance.Register<NetTeleportPylonModule>();
                var start = new TeleportPylonInfo { PositionInTiles = new Point16(96, pylonY), TypeOfPylon = TeleportPylonType.Victory };
                var target = new TeleportPylonInfo { PositionInTiles = new Point16(destinationX, pylonY), TypeOfPylon = TeleportPylonType.Underground };
                Main.PylonSystem.AddForClient(start);
                Main.PylonSystem.AddForClient(target);
                for (int y = pylonY; y < pylonY + 4; y++)
                    for (int x = 0; x < 3; x++)
                    {
                        FixtureTile(tiles, 96 + x, y, TileID.TeleportationPylon);
                        FixtureTile(tiles, destinationX + x, y, TileID.TeleportationPylon);
                    }
                Main.npc[0] = Resident(NPCID.Guide, 0, destinationX + 3);
                Main.npc[1] = Resident(NPCID.Merchant, 1, destinationX + 4);
                var point = target.PositionInTiles.ToWorldCoordinates();
                Set(frame, "Pylon", 2032, point);
                Step(host, body);
                Require(Math.Abs(body.position.X - point.X) < 2 && Get(state, "Result").ToString() == "Completed" && human.position == ownerPosition,
                    "native loopback request uses this actual actor, source pylon, underground biome and nearby town residents");
            }
            finally
            {
                Main.npc[0] = npc0;
                Main.npc[1] = npc1;
                Main.PylonSystem = originalSystem;
                RestoreTiles(tiles);
            }
        }

        private static NPC Resident(int type, int slot, int x)
        {
            var npc = new NPC();
            npc.SetDefaults(type);
            npc.active = true;
            npc.whoAmI = slot;
            npc.homeless = false;
            npc.homeTileX = x;
            npc.homeTileY = 72;
            npc.position = new Vector2(x * 16, 73 * 16 - npc.height);
            return npc;
        }

        private static Tile FixtureTile(Dictionary<Point, Tile> restore, int x, int y, int type = -1)
        {
            var key = new Point(x, y);
            if (!restore.ContainsKey(key)) restore.Add(key, Main.tile[x, y]);
            var tile = new Tile();
            if (type >= 0) { tile.active(true); tile.type = (ushort)type; }
            Main.tile[x, y] = tile;
            return tile;
        }

        private static void RestoreTiles(Dictionary<Point, Tile> restore)
        { foreach (var entry in restore) Main.tile[entry.Key.X, entry.Key.Y] = entry.Value; }
    }
}
