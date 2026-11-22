using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionDoorFollowChecks
    {
        internal static void HalfBrick(object host, object profiles, Player human)
        {
            ExceptionDispatchInfo first = null;
            for (int scene = 0; scene < 3; scene++)
                try { HalfBrickPassage(host, profiles, human, scene == 1, scene == 2); }
                catch (Exception error) { Console.WriteLine(error); if (first == null) first = ExceptionDispatchInfo.Capture(error); }
            first?.Throw();
        }

        private static void HalfBrickPassage(object host, object profiles, Player human, bool openAtStart, bool lowHeadroom)
        {
            const int door = 96, floor = 73, approach = door * 16 - 96;
            string name = lowHeadroom ? "door-low-roof" : openAtStart ? "door-already-open" : "door-halfbrick";
            using (var run = new NativeCompanionVerticalFollowChecks.Scenario(host, profiles, human, name, approach, floor, () =>
            {
                for (int x = 76; x <= 114; x++)
                {
                    TileAt(x, floor, TileID.Stone);
                    Main.tile[x, floor].halfBrick(x > door);
                    for (int y = 65; y < floor - 3; y++) TileAt(x, y, TileID.Stone);
                }
                for (int row = 0; row < 3; row++)
                {
                    TileAt(door, floor - 3 + row, openAtStart ? TileID.OpenDoor : TileID.ClosedDoor, 0, row * 18);
                    if (openAtStart) TileAt(door + 1, floor - 3 + row, TileID.OpenDoor, 18, row * 18);
                }
                if (lowHeadroom)
                    for (int x = door + 2; x <= door + 8; x++) TileAt(x, floor - 3, TileID.Stone);
            }))
            {
                Player body = run.Body;
                Vector2 exterior = new Vector2((door + 10) * 16, floor * 16 + 8);
                human.Bottom = exterior;
                int opened = -1, crossed = -1, downstep = -1, resumed = -1;
                bool sampledWithoutGrant = false;
                for (int tick = 0; tick < 240; tick++)
                {
                    run.StepOwner(Vector2.Zero);
                    if (opened < 0 && Main.tile[door, floor - 2].type == TileID.OpenDoor) opened = tick;
                    float gap = door * 16 - body.Right.X;
                    if (!sampledWithoutGrant && !openAtStart && !lowHeadroom && opened < 0 && gap > 8 && gap < 16)
                        sampledWithoutGrant = CheckUnselectedDoorApproach(run.Actor, body);
                    if (crossed < 0 && body.Left.X > (door + 1) * 16 + 3) crossed = tick;
                    if (downstep < 0 && body.Left.X > (door + 1) * 16 && Math.Abs(body.Bottom.Y - exterior.Y) < .2f && body.velocity.Y == 0)
                        downstep = tick;
                    if (downstep >= 0 && body.Center.X > exterior.X - 35) { resumed = tick; break; }
                }
                Console.WriteLine($"FOLLOW halfbrick door openStart={openAtStart} blocked={lowHeadroom} opened={opened} crossed={crossed} treadDown={downstep} resumed={resumed} body={body.Bottom} status={Get(run.Actor, "Status")}");
                if (lowHeadroom)
                {
                    Require(opened < 0 && crossed < 0 && body.Right.X <= door * 16 + 1,
                        "a real near-door ceiling with less than body clearance blocks departure");
                    return;
                }
                Require(opened >= 0 && crossed > opened && downstep >= opened && resumed > crossed,
                    "ordinary Follow opens or uses the real door and treads down onto the exterior half-brick before continuing");
                if (openAtStart) return;
                if (Get(Get(run.Actor, "Behavior"), "navigation").GetType().GetMethod("TryDoorImmediate", BindingFlags.Instance | BindingFlags.NonPublic) != null)
                    Require(sampledWithoutGrant, "the real prepass has a loaded door approach under the no-grant probe");
                Require(Main.tile[door, floor - 2].type == TileID.ClosedDoor, "the real closed door is restored after the first passage");

                human.Bottom = new Vector2(approach, floor * 16);
                int reverse = -1, upstep = -1, returned = -1;
                for (int tick = 0; tick < 240; tick++)
                {
                    run.StepOwner(Vector2.Zero);
                    if (reverse < 0 && Main.tile[door, floor - 2].type == TileID.OpenDoor) reverse = tick;
                    if (upstep < 0 && reverse >= 0 && Math.Abs(body.Bottom.Y - floor * 16) < .2f && body.velocity.Y == 0) upstep = tick;
                    if (upstep >= 0 && body.Center.X < approach + 35) { returned = tick; break; }
                }
                Console.WriteLine($"FOLLOW halfbrick reverse reopened={reverse} treadUp={upstep} resumed={returned} body={body.Bottom} status={Get(run.Actor, "Status")}");
                Require(reverse >= 0 && upstep >= reverse && returned > upstep && Main.tile[door, floor - 2].type == TileID.ClosedDoor,
                    "the same recovered door reopens as the actual body treads up from half-brick and follows into the interior");
            }
        }

        private static bool CheckUnselectedDoorApproach(object actor, Player body)
        {
            var navigation = Get(Get(actor, "Behavior"), "navigation");
            Type type = navigation.GetType();
            var query = type.GetMethod("TryDoorImmediate", BindingFlags.Instance | BindingFlags.NonPublic);
            if (query == null) return false; // The fixed earlier comparison has no split entrance.
            var scratch = type.GetNestedType("Scratch", BindingFlags.NonPublic);
            var route = type.GetNestedType("RouteStep", BindingFlags.NonPublic);
            var count = type.GetProperty("DoorPassageProbes", BindingFlags.Instance | BindingFlags.NonPublic);
            object before = count?.GetValue(navigation);
            var values = new[] { body, (object)1, false, Activator.CreateInstance(route), false, false };
            bool planned;
            using ((IDisposable)scratch.GetMethod("Save", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null))
                planned = (bool)query.Invoke(navigation, values);
            object after = count?.GetValue(navigation);
            Require(!planned && !(bool)values[4] && (bool)values[5] && Equals(before, after),
                "an unselected real body can prove its next doorstep approach without an entire closed-door forecast");
            Console.WriteLine($"FOLLOW no-grant door step={values[5]} fullForecasts={before ?? "release"}->{after ?? "release"}");
            return true;
        }

        internal static void Run(object host, object actor, Player body, Player human)
        {
            const int doorX = 96, floor = 73;
            var profile = (CompanionProfile)Get(actor, "Profile");
            var prefs = profile.Behavior.Clone();
            var before = body.position;
            var speed = body.velocity;
            var owner = human.position;
            var wing = body.armor[3];
            var wingStats = ArmorIDs.Wing.Sets.Stats;
            var area = new Dictionary<Point, Tile>();
            for (int x = 73; x <= 116; x++)
                for (int y = 61; y <= 79; y++)
                    area.Add(new Point(x, y), Main.tile[x, y]);
            try
            {
                Require(!body.mount.Active && body.width == 20 && body.height == 42, "normal doorway has the actual full-size player");
                Call(host, "SetBehavior", profile.Id, new CompanionBehavior { Follow = true, AttackEnabled = false, HealthRecoveryEnabled = false });
                foreach (int height in new[] { 3, 5 })
                    foreach (bool fromContact in height == 3 ? new[] { true, false } : new[] { false })
                    {
                        Clear(area.Keys);
                        for (int x = 76; x <= 112; x++)
                            for (int y = 64; y <= floor; y++)
                                if (y == floor || y < floor - height) TileAt(x, y, TileID.Stone);
                        int closed = height == 3 ? TileID.ClosedDoor : TileID.TallGateClosed;
                        int open = height == 3 ? TileID.OpenDoor : TileID.TallGateOpen;
                        var gate = height == 5 ? TileObjectData.GetTileData(TileID.TallGateClosed, 0) : null;
                        int frameY = 0;
                        for (int row = 0; row < height; row++)
                        {
                            TileAt(doorX, floor - height + row, closed, 0, frameY);
                            frameY += gate == null ? 18 : gate.CoordinateHeights[row] + gate.CoordinatePadding;
                        }
                        Place(host, actor, human, body, new Vector2(doorX * 16 - (fromContact ? 24 : 158), floor * 16 - body.height));
                        human.Bottom = new Vector2(107 * 16, floor * 16);
                        bool opened = false;
                        bool crossed = false;
                        for (int tick = 0; tick < 170 && (!crossed || Main.tile[doorX, floor - 2].type != closed); tick++)
                        {
                            Step(host, body);
                            opened |= Main.tile[doorX, floor - 2].type == open;
                            crossed |= body.Left.X > (doorX + 1) * 16 + 18;
                        }
                        Require(opened && crossed && Main.tile[doorX, floor - 2].type == closed && !(bool)Get(actor, "Faulted"),
                            $"real Follow opens, crosses and closes {closed} from {(fromContact ? "4px" : "afar")}: open={opened} crossed={crossed} tile={Main.tile[doorX, floor - 2].type} body={body.position} status={Get(actor, "Status")}");
                        Console.WriteLine($"DOOR FOLLOW horizontal tile={closed} approach={(fromContact ? "4px" : "afar")} body={body.position} opened={opened} crossed={crossed} restored={Main.tile[doorX, floor - 2].type == closed}");
                        if (fromContact)
                        {
                            human.Bottom = new Vector2(83 * 16, floor * 16);
                            bool reopened = false;
                            for (int tick = 0; tick < 170 && body.Right.X >= doorX * 16 - 16; tick++)
                            {
                                Step(host, body);
                                reopened |= Main.tile[doorX, floor - 2].type == open;
                            }
                            Require(reopened && body.Right.X < doorX * 16 - 16,
                                $"normal reverse Follow reopens the actual randomized closed-door frames and walks back: open={reopened} body={body.position} status={Get(actor, "Status")}");
                        }
                    }

                foreach (int kind in new[] { 1, -1, -2 })
                {
                    Clear(area.Keys);
                    const int tx = 94, ty = 67;
                    int direction = Math.Sign(kind), lower = kind == -2 ? 79 : 72;
                    bool flying = kind == -2;
                    if (flying)
                    {
                        Terraria.Initializers.WingStatsInitializer.Load();
                        body.armor[3] = new Item(ItemID.FishronWings);
                    }
                    for (int x = 76; x <= 112; x++)
                    {
                        TileAt(x, lower, TileID.Stone);
                        if (x == tx || x == tx + 1) continue;
                        for (int y = ty; y < lower; y++) TileAt(x, y, TileID.Stone);
                    }
                    TileAt(tx, ty, TileID.TrapdoorClosed);
                    TileAt(tx + 1, ty, TileID.TrapdoorClosed, 18);
                    float center = (tx + 1) * 16;
                    Place(host, actor, human, body, new Vector2(center - body.width * .5f, (direction > 0 ? ty : lower) * 16 - body.height));
                    human.Bottom = new Vector2(center, (direction > 0 ? lower : ty - 2) * 16);
                    bool opened = false, crossed = false, wrongShape = false;
                    bool wingApproach = false, wingThrough = false;
                    for (int tick = 0; tick < 145 && (!crossed || Main.tile[tx, ty].type != TileID.TrapdoorClosed); tick++)
                    {
                        float beforeFlight = body.wingTime;
                        Step(host, body);
                        if (flying && body.wingTime < beforeFlight && body.velocity.Y < 0)
                        {
                            if (opened) wingThrough = true;
                            else if (Main.tile[tx, ty].type == TileID.TrapdoorClosed) wingApproach = true;
                        }
                        var hinge = Main.tile[tx, ty];
                        if (hinge.active() && hinge.type == TileID.TrapdoorOpen)
                        {
                            opened = true;
                            int leafRow = ty + direction;
                            wrongShape |= !Main.tile[tx, leafRow].active() || Main.tile[tx, leafRow].type != TileID.TrapdoorOpen ||
                                Main.tile[tx + 1, leafRow].frameX % 36 != 18;
                        }
                        crossed |= direction > 0 ? body.Top.Y > (ty + 1) * 16 + 4 : body.Bottom.Y < ty * 16 - 4;
                    }
                    Require(opened && crossed && !wrongShape && Main.tile[tx, ty].type == TileID.TrapdoorClosed && !(bool)Get(actor, "Faulted"),
                        $"real Follow opens real trapdoor geometry, passes {(flying ? "up in flight" : direction > 0 ? "down" : "up")} and closes: open={opened} crossed={crossed} wrongLeaf={wrongShape} tile={Main.tile[tx, ty].type} body={body.position} velocity={body.velocity} status={Get(actor, "Status")}");
                    if (flying) Require(wingApproach && wingThrough, "actual wings lift the body before the hatch opens and continue lifting through its opening");
                }
                Console.WriteLine("PASS native companion Follow doors: 4px and remote ordinary door, tall gate, and ground or wing-flight passage through the native trapdoor.");
            }
            finally
            {
                foreach (var entry in area) Main.tile[entry.Key.X, entry.Key.Y] = entry.Value;
                body.position = before;
                body.velocity = speed;
                body.armor[3] = wing;
                ArmorIDs.Wing.Sets.Stats = wingStats;
                human.position = owner;
                Call(Get(Get(actor, "Behavior"), "navigation"), "CancelRoute");
                Call(host, "SetBehavior", profile.Id, prefs);
            }
        }

        private static void Place(object host, object actor, Player human, Player body, Vector2 position)
        {
            body.position = position;
            body.velocity = Vector2.Zero;
            human.Center = body.Center;
            // Each geometry is independent. Retire the old route without
            // reusing its command sequence on the same live actor.
            Call(Get(Get(actor, "Behavior"), "navigation"), "CancelRoute");
            Step(host, body);
        }

        private static void Clear(IEnumerable<Point> area)
        { foreach (Point cell in area) Main.tile[cell.X, cell.Y] = new Tile(); }

        private static void TileAt(int x, int y, int type, int frameX = 0, int frameY = 0)
        {
            var tile = new Tile { type = (ushort)type, frameX = (short)frameX, frameY = (short)frameY };
            tile.active(true);
            Main.tile[x, y] = tile;
        }

        private static void Step(object host, Player body)
        {
            NativeQuickItemChecks.BeginWorldStep();
            Call(host, "Update", (ulong)Main.GameUpdateCount);
            body.Update(body.whoAmI);
        }

    }
}
