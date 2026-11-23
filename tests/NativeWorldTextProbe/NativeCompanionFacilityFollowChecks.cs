using System;
using System.Collections.Generic;
using JueMingR.Platform.Companions;
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
    internal static class NativeCompanionFacilityFollowChecks
    {
        internal static void PylonFollow(object host, object profiles, Player human)
        {
            const int source = 46, destination = 108, floor = 73, pylonY = floor - 4;
            var savedSystem = Main.PylonSystem;
            double savedSurface = Main.worldSurface;
            var guide = Main.npc[0];
            var merchant = Main.npc[1];
            try
            {
                Main.PylonSystem = new TeleportPylonsSystem();
                Main.worldSurface = pylonY - 12;
                if (NetManager.Instance.GetModule<NetTeleportPylonModule>() == null) NetManager.Instance.Register<NetTeleportPylonModule>();
                using (var run = new NativeCompanionVerticalFollowChecks.Scenario(host, profiles, human, "pylon-follow", source * 16 + 8, floor, () =>
                {
                    Floor(43, 68, floor);
                    Floor(101, 118, floor);
                    Floor(43, 118, floor + 36);
                }))
                {
                    human.Bottom = new Vector2((destination + 2) * 16, floor * 16);
                    for (int tick = 0; tick < 48; tick++) run.StepOwner(Vector2.Zero);
                    Require(run.Body.Right.X < 70 * 16 && run.Body.Bottom.Y <= floor * 16 + 1,
                        "normal Follow has no safe crossing of this deep gap with no registered facilities");

                    var entry = PlacePylon(source, pylonY, TeleportPylonType.Victory);
                    var exit = PlacePylon(destination, pylonY, TeleportPylonType.Underground);
                    Main.npc[0] = PylonResident(NPCID.Guide, 0, destination + 5, floor);
                    Main.npc[1] = PylonResident(NPCID.Merchant, 1, destination + 6, floor);
                    Vector2 owner = human.Bottom;
                    int arrived = -1;
                    Vector2 actualEntry = default, actualExit = default;
                    var state = Get(run.Actor, "Mobility");
                    for (int tick = 0; tick < 220; tick++)
                    {
                        Vector2 before = run.Body.position;
                        run.StepOwner(Vector2.Zero);
                        if (Vector2.DistanceSquared(before, run.Body.position) < 112 * 112) continue;
                        actualEntry = before; actualExit = run.Body.position;
                        arrived = tick; break;
                    }
                    Vector2 exitPoint = exit.PositionInTiles.ToWorldCoordinates();
                    string action = Get(state, "Action").ToString(), result = Get(state, "Result").ToString();
                    Require(arrived >= 0 && action == "Pylon" && result == "Completed" && human.Bottom == owner &&
                        Math.Abs(actualExit.X - exitPoint.X) < 2 && Math.Abs(actualExit.Y + run.Body.height - floor * 16) < 2,
                        $"normal Follow uses the registered pylon and reaches solid ground at its authentic exit (entry={actualEntry}, exit={actualExit}, action={action}, result={result}, status={Get(run.Actor, "Status")})");

                    int walked = -1;
                    for (int tick = 0; tick < 125; tick++)
                    {
                        run.StepToward(owner + new Vector2(105, 0), new Vector2(2.5f, 0));
                        if (run.Body.Center.X > owner.X + 42 && run.Body.Bottom.Y == floor * 16 && run.Body.controlRight)
                        { walked = tick; break; }
                    }
                    Console.WriteLine($"FOLLOW pylon source={entry.PositionInTiles} target={exit.PositionInTiles} arrived={arrived} before={actualEntry} after={actualExit} resumed={walked}");
                    Require(walked >= 0, "the same actor follows the moving owner on foot from the observed native pylon exit");
                }
            }
            finally
            {
                Main.npc[0] = guide; Main.npc[1] = merchant;
                Main.worldSurface = savedSurface;
                Main.PylonSystem = savedSystem;
            }
        }

        private static TeleportPylonInfo PlacePylon(int x, int y, TeleportPylonType style)
        {
            var info = new TeleportPylonInfo { PositionInTiles = new Point16(x, y), TypeOfPylon = style };
            for (int column = 0; column < 3; column++)
                for (int row = 0; row < 4; row++)
                {
                    Tile tile = TileAt(x + column, y + row, TileID.TeleportationPylon);
                    tile.frameX = (short)((int)style * 54 + column * 18);
                    tile.frameY = (short)(row * 18);
                }
            Main.PylonSystem.AddForClient(info);
            return info;
        }

        private static NPC PylonResident(int type, int slot, int x, int floor)
        {
            var npc = new NPC();
            npc.SetDefaults(type);
            npc.active = true;
            npc.whoAmI = slot;
            npc.homeless = false;
            npc.homeTileX = x;
            npc.homeTileY = floor - 1;
            npc.position = new Vector2(x * 16, floor * 16 - npc.height);
            return npc;
        }

        internal static void Run(object host, object actor, Player body, Player human)
        {
            var profile = (CompanionProfile)Get(actor, "Profile");
            var preferences = profile.Behavior.Clone();
            var position = body.position;
            var velocity = body.velocity;
            var owner = human.position;
            var area = new Dictionary<Point, Tile>();
            var failures = new List<Exception>();
            for (int x = 73; x <= 119; x++)
                for (int y = 55; y <= 82; y++)
                    area.Add(new Point(x, y), Main.tile[x, y]);
            try
            {
                Call(host, "SetBehavior", profile.Id, new CompanionBehavior { Follow = true, AttackEnabled = false, HealthRecoveryEnabled = false });
                foreach (Action<object, object, Player, Player> test in new Action<object, object, Player, Player>[] { Rope, Track, TrackNearOwner, WiredPads, Grapple })
                {
                    foreach (Point tile in area.Keys) Main.tile[tile.X, tile.Y] = new Tile();
                    try { test(host, actor, body, human); }
                    catch (Exception failure) { Console.WriteLine(failure); failures.Add(failure); }
                    finally { if (body.mount.Active) body.mount.Dismount(body, true); }
                }
                if (failures.Count > 0) throw new AggregateException("Actual companion facility Follow", failures);
            }
            finally
            {
                foreach (var tile in area) Main.tile[tile.Key.X, tile.Key.Y] = tile.Value;
                body.position = position;
                body.velocity = velocity;
                human.position = owner;
                Call(Get(Get(actor, "Behavior"), "navigation"), "CancelRoute");
                Call(host, "SetBehavior", profile.Id, preferences);
            }
        }

        private static void Rope(object host, object actor, Player body, Player human)
        {
            const int ropeX = 90, lower = 76, upper = 65;
            Floor(77, 109, lower);
            Floor(92, 113, upper);
            for (int y = 61; y < lower; y++) TileAt(ropeX, y, TileID.Rope);
            Place(host, actor, body, human, ropeX * 16 + 8, lower);
            human.Bottom = new Vector2(93 * 16, upper * 16);
            bool attached = false, rose = false, released = false, reached = false;
            for (int tick = 0; tick < 330 && !reached; tick++)
            {
                Step(host, body);
                if (body.pulley && !attached) { attached = true; human.Bottom = new Vector2(104 * 16, upper * 16); }
                rose |= attached && body.pulley && body.Bottom.Y < (lower - 5) * 16;
                released |= rose && !body.pulley && body.controlJump && body.velocity.Y < 0;
                reached = released && body.Center.X > 95 * 16 && Math.Abs(body.Bottom.Y - upper * 16) < 2;
                if (tick % 55 == 0) TraceFollow(actor, body, "rope", tick);
            }
            Require(attached && rose && released && reached,
                $"Follow climbs the original rope and jumps onto the higher landing: attach={attached} rise={rose} released={released} landed={reached} body={body.position} speed={body.velocity} status={Get(actor, "Status")}");
            Console.WriteLine("PASS native companion Follow rope: attachment, climbing, native jump at the top and upper platform landing.");
        }

        private static void Track(object host, object actor, Player body, Player human) =>
            FollowTrack(host, actor, body, human, nearbyOwner: false);

        private static void TrackNearOwner(object host, object actor, Player body, Player human) =>
            FollowTrack(host, actor, body, human, nearbyOwner: true);

        private static void FollowTrack(object host, object actor, Player body, Player human, bool nearbyOwner)
        {
            NativeCombatLiveContextChecks.InitializeMount();
            bool dedicated = Main.dedServ;
            try { Main.dedServ = true; Minecart.Initialize(); }
            finally { Main.dedServ = dedicated; }
            const int floor = 73;
            Floor(76, 115, floor);
            for (int x = 83; x <= 99; x++) Minecart.PlaceTrack(TileAt(x, 71, TileID.MinecartTrack), 0);
            for (int x = 83; x <= 99; x++) Minecart.FrameTrack(x, 71, pound: false, mute: true);
            Place(host, actor, body, human, 82 * 16 + 8, floor);
            human.Bottom = new Vector2(112 * 16, floor * 16);
            bool ride = false, driving = false, brake = false, dismounted = false;
            float initialRailX = 0, topSpeed = 0;
            int stable = 0;
            float farthest = 0;
            Vector2 nearbyGoal = new Vector2(94 * 16, floor * 16);
            var mobility = Get(actor, "Mobility");
            int priorHook = (int)Get(mobility, "HookKey");
            int railHookKey = priorHook;
            Projectile railHook = null;
            try
            {
                for (int tick = 0; tick < 450 && (nearbyOwner ? stable < 24 : !(dismounted && body.Center.X > 107 * 16)); tick++)
                {
                    var next = (Projectile)GetOptional(mobility, "Hook");
                    int key = (int)Get(mobility, "HookKey");
                    if (next != null && key != priorHook && next.owner == body.whoAmI && next.type == ProjectileID.TrackHook)
                    {
                        railHook = next;
                        railHookKey = key;
                    }
                    if (railHook?.active == true && (int)railHook.key == railHookKey)
                    {
                        int previous = Main.ProjectileUpdateLoopIndex;
                        try { Main.ProjectileUpdateLoopIndex = railHook.whoAmI; railHook.Update(railHook.whoAmI); }
                        finally { Main.ProjectileUpdateLoopIndex = previous; }
                    }
                    Step(host, body);
                    if (body.mount.Cart && body.onTrack)
                    {
                        if (!ride) { ride = true; initialRailX = body.Center.X; }
                        topSpeed = Math.Max(topSpeed, Math.Abs(body.velocity.X));
                        if (!driving && body.Center.X > initialRailX + 48)
                        {
                            driving = true;
                            if (nearbyOwner) human.Bottom = nearbyGoal;
                        }
                        brake |= driving && topSpeed > 1.5f && Math.Abs(body.velocity.X) <= 1.5f;
                    }
                    dismounted |= driving && !body.mount.Active;
                    if (nearbyOwner && driving) farthest = Math.Max(farthest, body.Center.X);
                    if (nearbyOwner && dismounted && Math.Abs(body.Bottom.Y - floor * 16) < 2 &&
                        Math.Abs(body.Center.X - nearbyGoal.X) <= 36 && Math.Abs(body.velocity.X) < .5f) stable++;
                    else stable = 0;
                    if (tick % 75 == 0) TraceFollow(actor, body, nearbyOwner ? "track-near" : "track", tick);
                }
            }
            finally { if (railHook?.active == true && (int)railHook.key == railHookKey) railHook.Kill(); }
            if (nearbyOwner)
            {
                Console.WriteLine($"NAV FACILITY track-near ride={ride} drive={driving} brake={brake} dismount={dismounted} stable={stable} farthest={farthest} goal={nearbyGoal.X}");
                Require(ride && driving && brake && dismounted && stable >= 24 && farthest <= nearbyGoal.X + 36,
                    "Follow brakes on the actual rail at a new nearby owner, dismounts and settles without riding through the arrival band");
                return;
            }
            Require(ride && driving && brake && dismounted && body.Center.X > 107 * 16,
                $"Follow rides the existing track, brakes and resumes on foot: ride={ride} drive={driving} speed={topSpeed} brake={brake} dismount={dismounted} body={body.position} status={Get(actor, "Status")}");
            Console.WriteLine("PASS native companion Follow minecart: track attachment, sustained travel, native braking, dismount and continued ground Follow.");
        }

        private static void WiredPads(object host, object actor, Player body, Player human)
        {
            const int start = 83, end = 106, floor = 73;
            Floor(77, 87, floor);
            Floor(102, 115, floor);
            Wiring.Initialize();
            TileAt(start, 70, TileID.Switches).wire(true);
            for (int part = 0; part < 3; part++)
            {
                TileAt(start + part, floor, TileID.Teleporter).frameX = (short)(part * 18);
                TileAt(end + part, floor, TileID.Teleporter).frameX = (short)(part * 18);
            }
            Main.tile[start, floor].wire(true);
            Main.tile[end, floor].wire(true);
            for (int x = start; x <= end; x++) Main.tile[x, 69].wire(true);
            for (int y = 69; y < floor; y++)
            { Main.tile[start, y].wire(true); Main.tile[end, y].wire(true); }
            Place(host, actor, body, human, (start + 1) * 16, floor);
            human.Bottom = new Vector2((end + 5) * 16, floor * 16);
            var owner = human.position;
            float exit = -1;
            for (int tick = 0; tick < 240 && (exit < 0 || body.Center.X < exit + 24); tick++)
            {
                float previous = body.Center.X;
                Step(host, body);
                if (body.Center.X - previous > 250) exit = body.Center.X;
            }
            Require(exit > end * 16 && body.Center.X >= exit + 24 && human.position == owner && Main.tile[start, 70].frameY == 18,
                $"Follow operates one existing safe circuit, resumes from the real landing and leaves the human alone: exit={exit} body={body.position} signal={Main.tile[start, 70].frameY} status={Get(actor, "Status")}");
            Console.WriteLine("PASS native companion Follow wired pads: own switch pulse, verified discontinuity and ordinary movement from the actual destination.");
        }

        private static void Grapple(object host, object actor, Player body, Player human)
        {
            const int leftWall = 108, rightWall = 111, anchorX = 110, anchorY = 66, floor = 73;
            Floor(102, 115, floor);
            for (int y = 65; y < floor; y++)
            { TileAt(leftWall, y, TileID.Stone); TileAt(rightWall, y, TileID.Stone); }
            TileAt(anchorX, anchorY, TileID.Stone);
            var gear = body.miscEquips[4];
            var hookItem = new Item(ItemID.GrapplingHook);
            bool nativeHook = Main.projHook[hookItem.shoot];
            try
            {
                Main.projHook[hookItem.shoot] = ContentSamples.ProjectilesByType[hookItem.shoot].aiStyle == 7;
                body.miscEquips[4] = hookItem;
                Place(host, actor, body, human, anchorX * 16, floor);
                human.Bottom = new Vector2(anchorX * 16, (anchorY - 1) * 16);
                var mobility = Get(actor, "Mobility");
                int initialKey = (int)Get(mobility, "HookKey");
                Projectile hook = null;
                bool shot = false, attached = false, verticalRelease = false, detached = false;
                float hangingY = body.position.Y;
                for (int i = 0; i < 290 && !detached; i++)
                {
                    var current = (Projectile)GetOptional(mobility, "Hook");
                    if (current != null && (int)Get(mobility, "HookKey") != initialKey && current.owner == body.whoAmI && current.type == hookItem.shoot)
                    { hook = current; shot = true; }
                    if (hook?.active == true)
                    {
                        int previous = Main.ProjectileUpdateLoopIndex;
                        try { Main.ProjectileUpdateLoopIndex = hook.whoAmI; hook.Update(hook.whoAmI); }
                        finally { Main.ProjectileUpdateLoopIndex = previous; }
                    }
                    Step(host, body);
                    if (!attached && hook?.active == true && hook.ai[0] == 2 && body.position.Y < (floor - 4) * 16)
                    {
                        attached = true;
                        hangingY = body.position.Y;
                        human.Bottom = new Vector2(body.Center.X, floor * 16);
                    }
                    if (attached && Get(mobility, "Action").ToString() == "ReleaseHook")
                    {
                        verticalRelease |= body.controlJump && body.controlDown && !body.controlLeft && !body.controlRight;
                        detached |= hook?.active == false && body.position.Y > hangingY + 8;
                    }
                }
                Require(shot && attached && verticalRelease && detached && !(bool)Get(actor, "Faulted"),
                    $"Follow chooses its own hook, pulls upward, then releases to the lower target: birth={shot} attached={attached} release={verticalRelease} down={detached} body={body.position} action={Get(mobility, "Action")}/{Get(mobility, "Result")} status={Get(actor, "Status")}");
                Console.WriteLine("PASS native companion Follow grapple: autonomous own shot, attachment, vertical release after moving the owner and real descent.");
            }
            finally
            {
                body.RemoveAllGrapplingHooks();
                body.miscEquips[4] = gear;
                Main.projHook[hookItem.shoot] = nativeHook;
            }
        }

        private static void Place(object host, object actor, Player body, Player human, float centerX, int floor)
        {
            body.Bottom = new Vector2(centerX, floor * 16);
            body.velocity = Vector2.Zero;
            human.Center = body.Center;
            Call(Get(Get(actor, "Behavior"), "navigation"), "CancelRoute");
            Step(host, body);
        }

        private static void TraceFollow(object actor, Player body, string caseName, int tick)
        {
            var navigation = Get(Get(actor, "Behavior"), "navigation");
            var steps = (System.Collections.IList)Get(navigation, "route");
            int current = (int)Get(navigation, "routeIndex");
            object action = current < steps.Count ? steps[current] : null;
            var search = GetOptional(navigation, "search");
            string route = action == null ? "local" : $"{Get(action,"Kind")}:{Get(action,"From")}→{Get(action,"To")}";
            string frontier = search == null ? "none" : $"{Get(search,"Active")}/{Get(search,"Exhausted")}/{((System.Collections.IList)Get(search,"Nodes")).Count}";
            Console.WriteLine($"NAV FACILITY {caseName} tick={tick} body={body.Bottom}/{body.velocity} pulley={body.pulley} track={body.onTrack} up={body.controlUp} down={body.controlDown} route={route} frontier={frontier} pending={Get(navigation,"pendingResult")} publishing={Get(navigation,"publishing")} status={Get(actor,"Status")}");
        }

        private static void Floor(int left, int right, int y)
        { for (int x = left; x <= right; x++) TileAt(x, y, TileID.Stone); }

        private static Tile TileAt(int x, int y, int type)
        {
            var tile = new Tile { type = (ushort)type };
            tile.active(true);
            Main.tile[x, y] = tile;
            return tile;
        }

        private static void Step(object host, Player body)
        {
            NativeQuickItemChecks.BeginWorldStep();
            Call(host, "Update", (ulong)Main.GameUpdateCount);
            body.Update(body.whoAmI);
        }
    }
}
