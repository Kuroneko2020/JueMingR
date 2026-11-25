using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionVerticalFollowChecks
    {
        private static void MixedNativeFollow(object context, object host, object profiles, Player human)
        {
            const int initial = 74, lower = 80, upper = 69, finish = 75, door = 77, rope = 90, teeth = 100, lip = 110;
            float start = 66 * 16 + 8, ropeCenter = rope * 16 + 8, upperStop = 97 * 16 + 8, finalStop = 116 * 16;
            var bodyStart = new Vector2(start, initial * 16);
            var ownerStart = new Vector2(start, lower * 16);
            var input = new List<MixedFrame>();
            var activeTimes = new List<long>();
            int settledSamples = 0;
            int sampledChunks = 0;
            Action terrain = () =>
                {
                    PlaceFixtureStrip(62, 68, initial, TileID.Platforms);
                    PlaceFixtureStrip(62, 94, lower, TileID.Stone);
                    PlaceFixtureStrip(72, 83, lower - 4, TileID.Stone);
                    for (int x = door + 1; x <= door + 5; x++)
                    {
                        WorldGen.PoundTile(x, lower);
                        Require(Main.tile[x, lower].halfBrick(), "the original hammer makes the exterior half-brick tread");
                    }
                    Require(WorldGen.SlopeTile(84, lower, 2, noEffects: true) && Main.tile[84, lower].slope() == 2,
                        "the original hammer shapes the connector to the rope side");
                    TileObject doorChoice;
                    Require(TileObject.CanPlace(door, lower - 3, TileID.ClosedDoor, 0, 1, out doorChoice) && TileObject.Place(doorChoice),
                        "the complete native selector places the ordinary door on its genuine anchored floor and roof");
                    WorldGen.SquareTileFrame(door, lower - 2);
                    for (int y = lower - 3; y < lower; y++)
                        Require(Main.tile[door, y].active() && Main.tile[door, y].type == TileID.ClosedDoor,
                            "framing retained each part of the original closed door");
                    for (int y = upper - 4; y < lower; y++) PlaceFixtureTile(rope, y, TileID.Rope);
                    PlaceFixtureStrip(92, lip - 1, upper, TileID.Stone);
                    for (int x = 98; x <= 106; x++)
                        for (int y = upper - 11; y <= upper - 4; y++) PlaceFixtureTile(x, y, TileID.Stone);
                    BuildHoikModule(teeth, upper);
                    PlaceFixtureStrip(106, 119, finish, TileID.Stone);
                };
            using (var run = new NativeFollowWorld(context, host, profiles, human, "mixed-native-follow", bodyStart, ownerStart, terrain))
            {
                Player body = run.Body;
                int down = -1, dropped = -1, doorOpen = -1, doorCross = -1, half = -1, ropeOn = -1, ropeRise = -1;
                int ropeJump = -1, upperStand = -1, hoik = -1, impulses = 0, hoikExit = -1, obstructionCleared = -1, lowerExit = -1, stable = 0;
                int ownerPhase = 0, startWait = -1, held = 0, maxRouteWork = 0, resumed = -1, departed = -1;
                long firstAction = 0, lastAction = 0;
                long hoikTraceAction = -1;
                int hoikTraceRows = 0, hoikTraceImpulses = -1;
                float ropeEntryY = 0, pulleyTravel = 0;
                bool gate = false;
                object navigation = Navigator(run.Actor);
                int lastRouteWork = (int)(GetOptional(navigation, "ExpansionWork") ?? 0);
                // Keep the original deadline for committing the exit. Once
                // actually airborne, measure the landing and approach in
                // their own bounded native phases.
                for (int tick = 0; lowerExit >= 0 ? tick <= lowerExit + 120 : departed >= 0 ? tick <= departed + 90 : tick < 2350; tick++)
                {
                    if (ownerPhase == 0 && dropped >= 0) ownerPhase = 1;
                    if (ownerPhase == 1 && human.Center.X > ropeCenter + 39 && body.Center.X > ropeCenter + 12) ownerPhase = 2;
                    if (ownerPhase == 2 && human.pulley) ownerPhase = 3;
                    if (ownerPhase == 3 && human.Bottom.Y < upper * 16 - 3) ownerPhase = 4;
                    if (ownerPhase == 4 && OnFloor(human, upper) && human.Center.X > 93 * 16) ownerPhase = 5;
                    if (ownerPhase == 5 && upperStand >= 0 && Math.Abs(human.Center.X - upperStop) <= 24) ownerPhase = 6;

                    int direction = ownerPhase == 1 ? NativeAxisTo(human, ropeCenter + 44) :
                        ownerPhase == 2 ? NativeAxisTo(human, ropeCenter) : ownerPhase == 4 ? 1 :
                        ownerPhase == 5 ? NativeAxisTo(human, upperStop) : ownerPhase == 6 ? NativeAxisTo(human, finalStop) : 0;
                    bool jump = ownerPhase == 4 && human.pulley;
                    int edit = 0;
                    // The owner can cross first. Close the exit while the
                    // teammate is still on the other side of the tunnel.
                    if (!gate && ownerPhase == 6 && human.Left.X > (lip + 1) * 16 && body.Right.X < (lip - 2) * 16)
                    {
                        ChangeMixedGate(lip, upper, closed: true);
                        gate = true;
                        edit = 1;
                    }
                    if (gate && obstructionCleared < 0 && hoikExit >= 0)
                    {
                        bool stoppedAtGate = body.Left.X >= (teeth + HoikModuleWidth) * 16 && body.Right.X <= lip * 16 + .25f &&
                            OnFloor(body, upper) && Math.Abs(body.velocity.X) < .25f && Main.tile[lip, upper - 2].active();
                        held = stoppedAtGate ? held + 1 : 0;
                        if (held >= 26)
                        {
                            startWait = tick - held + 1;
                            ChangeMixedGate(lip, upper, closed: false);
                            Require(!Main.tile[lip, upper - 1].active(), "native demolition really freed the stopped exit");
                            obstructionCleared = tick;
                            edit = -1;
                        }
                    }
                    Vector2 before = body.position;
                    bool wasOnRope = body.pulley;
                    bool up = ownerPhase == 2 || ownerPhase == 3;
                    activeTimes.Add(run.Step(direction, jump, up: up));
                    input.Add(new MixedFrame(direction, edit, jump, up, human.Bottom));
                    if (tick % 32 == 0) sampledChunks = Math.Max(sampledChunks, (int)Get(Get(host, "navigationWorld"), "ChunkCount"));
                    long currentAction = (long)Get(navigation, "ActionId");
                    if (firstAction == 0) firstAction = currentAction;
                    lastAction = Math.Max(lastAction, currentAction);
                    int routeWork = (int)(GetOptional(navigation, "ExpansionWork") ?? 0);
                    maxRouteWork = Math.Max(maxRouteWork, routeWork - lastRouteWork);
                    lastRouteWork = routeWork;
                    string kind = ObservedActionKind(navigation);
                    if (down < 0 && body.controlDown) down = tick;
                    if (dropped < 0 && down >= 0 && OnFloor(body, lower)) dropped = tick;
                    if (doorOpen < 0 && Main.tile[door, lower - 2].type == TileID.OpenDoor) doorOpen = tick;
                    if (doorCross < 0 && body.Left.X > (door + 1) * 16 && doorOpen >= 0) doorCross = tick;
                    if (half < 0 && doorCross >= 0 && Math.Abs(body.Bottom.Y - (lower * 16 + 8)) <= .25f && body.velocity.Y == 0) half = tick;
                    if (ropeOn < 0 && body.pulley) { ropeOn = tick; ropeEntryY = body.Bottom.Y; }
                    if (body.pulley) pulleyTravel = Math.Max(pulleyTravel, ropeEntryY - body.Bottom.Y);
                    if (ropeRise < 0 && pulleyTravel >= 3 * 16) ropeRise = tick;
                    if (ropeJump < 0 && ropeRise >= 0 && wasOnRope && !body.pulley && body.controlJump && body.velocity.Y < 0 && body.position.Y < before.Y)
                        ropeJump = tick;
                    if (upperStand < 0 && ropeJump >= 0 && body.Center.X > 94 * 16 && OnFloor(body, upper)) upperStand = tick;
                    if (hoik < 0 && upperStand >= 0 && kind == "Hoik") hoik = tick;
                    if (upperStand >= 0 && body.position.X - before.X > 7 && body.velocity.X < 1) impulses++;
                    if (hoik >= 0 && hoikExit < 0 && hoikTraceRows < 20 &&
                        (currentAction != hoikTraceAction || impulses != hoikTraceImpulses))
                    {
                        hoikTraceAction = currentAction;
                        hoikTraceImpulses = impulses;
                        hoikTraceRows++;
                        Console.WriteLine($"MIXED hoik trace step={tick} impulses={impulses} owner={human.Bottom}/{human.velocity} " +
                            $"body={body.Bottom}/{body.velocity} inputs={body.controlLeft}/{body.controlRight}/{body.controlJump}/{body.controlDown} " +
                            $"{PlanStatus(navigation)} {RouteTrace(navigation)} {RefinementTrace(navigation)}");
                    }
                    if (hoikExit < 0 && impulses >= 2 && body.Left.X > (teeth + HoikModuleWidth) * 16 && OnFloor(body, upper)) hoikExit = tick;
                    if (resumed < 0 && obstructionCleared >= 0 && body.position.X - before.X > .05f && body.controlRight) resumed = tick;
                    if (departed < 0 && resumed >= 0 && body.Center.X >= lip * 16 - 12 && body.Bottom.Y > upper * 16 + .125f && body.velocity.Y > 0)
                        departed = tick;
                    if (lowerExit < 0 && obstructionCleared >= 0 && OnFloor(body, finish)) lowerExit = tick;
                    stable = lowerExit >= 0 && OnFloor(body, finish) && OnFloor(human, finish) &&
                        Math.Abs(body.Center.X - human.Center.X) <= 30 && Math.Abs(body.velocity.X) < .5f ? stable + 1 : 0;
                    if (stable >= 24) break;
                }
                settledSamples = stable;
                Console.WriteLine($"MIXED down={down}/{dropped} door={doorOpen}/{doorCross}/{half} rope={ropeOn}/{ropeRise}/{ropeJump}/{upperStand} pulleyTravel={pulleyTravel:F1} hoik={hoik}/{impulses}/{hoikExit} wait={startWait}/{obstructionCleared} resumed={resumed} departed={departed} exit={lowerExit} stable={stable} actions={firstAction}/{lastAction} maxRouteWork={maxRouteWork} phase={ownerPhase} body={body.Bottom}/{body.velocity} owner={human.Bottom} status={Get(run.Actor, "Status")}");
                Require(down >= 0 && dropped > down && doorOpen > dropped && doorCross > doorOpen && half >= doorCross &&
                    ropeOn > half && ropeRise > ropeOn && ropeJump > ropeRise && upperStand > ropeJump && hoik > upperStand &&
                    impulses >= 2 && hoikExit > hoik && startWait >= hoikExit && obstructionCleared > startWait && resumed >= obstructionCleared &&
                    departed >= resumed && lowerExit > departed && stable >= 24 &&
                    firstAction > 0 && lastAction > firstAction && maxRouteWork <= 96,
                    "the same actor naturally drops the platform, walks the half-brick door, ascends and exits the rope, crosses the original Hoik and resumes after the temporary obstruction");
            }

            using (var idle = new NativeFollowWorld(context, host, profiles, human, "mixed-native-idle", bodyStart, ownerStart, terrain))
            {
                idle.Recall();
                var idleTimes = new List<long>(input.Count);
                float largestOwnerDelta = 0;
                foreach (var frame in input)
                {
                    if (frame.Gate != 0) ChangeMixedGate(lip, upper, frame.Gate > 0);
                    idleTimes.Add(idle.Step(frame.Direction, frame.Jump, up: frame.Up));
                    largestOwnerDelta = Math.Max(largestOwnerDelta, Vector2.Distance(frame.OwnerBottom, human.Bottom));
                }
                Require(((IDictionary)Get(host, "actors")).Count == 0 && largestOwnerDelta <= 2,
                    "the same native controls and gate edits replay the owner trajectory after the active companion was recalled");
                int finalStart = activeTimes.Count - settledSamples;
                Console.WriteLine($"MIXED settled samples={settledSamples} activeCompanion {NativeStepTiming(activeTimes.GetRange(finalStart, settledSamples))} recalled {NativeStepTiming(idleTimes.GetRange(finalStart, settledSamples))}");
                Console.WriteLine($"MIXED timing samples={input.Count} ownerReplayMaxPixels={largestOwnerDelta:F2} activeCompanion {NativeStepTiming(activeTimes)} recalled {NativeStepTiming(idleTimes)}");
                Console.WriteLine($"MIXED storage sampledEveryUpdates=32 chunks={sampledChunks}");
            }
        }

        private readonly struct MixedFrame
        {
            internal readonly int Direction, Gate;
            internal readonly bool Jump, Up;
            internal readonly Vector2 OwnerBottom;
            internal MixedFrame(int direction, int gate, bool jump, bool up, Vector2 ownerBottom)
            { Direction = direction; Gate = gate; Jump = jump; Up = up; OwnerBottom = ownerBottom; }
        }

        private static void ChangeMixedGate(int column, int deck, bool closed)
        {
            for (int y = deck - 10; y < deck + 3; y++)
            {
                if (closed) PlaceFixtureTile(column, y, TileID.Stone);
                else WorldGen.KillTile(column, y, noItem: true);
            }
        }

        private static string NativeStepTiming(List<long> ticks)
        {
            ticks.Sort();
            double micros = 1000000d / Stopwatch.Frequency;
            return $"p50_us={ticks[ticks.Count / 2] * micros:F2} p95_us={ticks[ticks.Count * 95 / 100] * micros:F2} " +
                $"p99_us={ticks[ticks.Count * 99 / 100] * micros:F2} peak_us={ticks[ticks.Count - 1] * micros:F2}";
        }

        private static int NativeAxisTo(Player player, float x)
        {
            float delta = x - player.Center.X;
            float speed = Math.Abs(player.velocity.X);
            float drag = Math.Max(.05f, player.runSlowdown);
            float stop = speed * Math.Max(0, speed - drag) / (2 * drag);
            return Math.Abs(delta) <= 2 || Math.Sign(player.velocity.X) == Math.Sign(delta) && Math.Abs(delta) <= stop + 1
                ? 0 : Math.Sign(delta);
        }

        private static void TriangleProxyFollow(object context, object host, object profiles, Player human)
        {
            const int roof = 67, innerBottom = 80, downhillStart = 106, lower = 94, wall = 96;
            float actorX = 88 * 16 + 7, ownerX = (downhillStart + 2) * 16 + 12, finalX = 115 * 16;
            using (var run = new NativeFollowWorld(context, host, profiles, human, "triangle-proxy",
                new Vector2(actorX, 73 * 16), new Vector2(ownerX, 93 * 16), () =>
                {
                    PlaceFixtureStrip(53, wall + 1, roof, TileID.Stone);
                    for (int y = roof + 1; y <= lower - 4; y++) PlaceFixtureTile(wall, y, TileID.Stone);
                    PlaceFixtureStrip(44, 119, lower, TileID.Stone);
                    PlaceFixtureStrip(53, 63, innerBottom, TileID.Stone);
                    PlaceFixtureStrip(64, 80, innerBottom, TileID.Platforms);
                    for (int x = 81; x <= 90; x++) PlaceFixtureSlope(x, innerBottom - (x - 80), 2);
                    PlaceFixtureStrip(91, wall - 1, 70, TileID.Stone);
                    for (int x = downhillStart - 3; x < downhillStart; x++) PlaceFixtureSlope(x, lower - (x - downhillStart + 4), 2);
                    for (int x = downhillStart; x < downhillStart + 3; x++) PlaceFixtureSlope(x, lower - 3 + (x - downhillStart), 1);
                    // The other side reconnects to the roof through several
                    // ordinary jump heights, outside the enclosed triangle.
                    for (int row = lower - 4; row >= roof + 2; row -= 4)
                        PlaceFixtureStrip(47, 53, row, TileID.Platforms);
                }))
            {
                Player body = run.Body;
                int retreat = -1, lowerEntry = -1, overRoof = -1, exit = -1, stable = 0, longestStable = 0;
                int turns = 0, moved = 0, direction = 1, relocations = 0;
                float lowestX = body.Center.X;
                var samples = new List<long>(1700);
                object navigator = Navigator(run.Actor);
                var work = new RoomWork(navigator);
                var recent = new Queue<string>(24);
                for (int tick = 0; tick < 1700; tick++)
                {
                    // The exterior loop is entirely owner-driven. Exit
                    // observations below only score the companion.
                    if (direction > 0 && human.Center.X >= finalX ||
                        direction < 0 && human.Center.X <= (wall + 5) * 16)
                    {
                        direction = -direction;
                        turns++;
                        Console.WriteLine($"TRIANGLE proxy owner turn tick={tick} exterior bound direction={direction} feet={human.Bottom}");
                    }
                    var ownerBefore = human.Bottom;
                    var before = body.Bottom;
                    samples.Add(run.Step(direction));
                    work.Read(turns);
                    bool ownerMoving = Vector2.DistanceSquared(ownerBefore, human.Bottom) > .25f;
                    if (ownerMoving) moved++;
                    if (Vector2.DistanceSquared(before, body.Bottom) > 24 * 24) relocations++;
                    lowestX = Math.Min(lowestX, body.Center.X);
                    if (tick % 12 == 0)
                    {
                        string trace = RoomTrace(run, human, navigator, tick, turns);
                        if (recent.Count == 24) recent.Dequeue();
                        recent.Enqueue(trace);
                        if (tick % 120 == 0) Console.WriteLine("TRIANGLE proxy " + trace);
                    }
                    if (retreat < 0 && body.Center.X < actorX - 55) retreat = tick;
                    if (lowerEntry < 0 && retreat >= 0 && body.Bottom.Y > (innerBottom + 2) * 16) lowerEntry = tick;
                    if (overRoof < 0 && retreat >= 0 && body.Center.X > 55 * 16 && body.Bottom.Y <= roof * 16 + 1) overRoof = tick;
                    if (exit < 0 && retreat >= 0 && (lowerEntry >= 0 || overRoof >= 0) && body.Left.X > (wall + 1) * 16)
                        exit = tick;
                    stable = exit >= 0 && ownerMoving && body.Left.X > (wall + 1) * 16 &&
                        Math.Abs(body.Bottom.Y - human.Bottom.Y) <= 18 && Math.Abs(body.Center.X - human.Center.X) <= 72
                        ? stable + 1 : 0;
                    longestStable = Math.Max(longestStable, stable);
                }
                Console.WriteLine($"TRIANGLE proxy tiles=44..119x67..94 slope=81..90 roof={roof} wall={wall} retreat={retreat} low={lowerEntry} roofPass={overRoof} exit={exit} " +
                    $"ownerTurns={turns} ownerMoving={moved} nearMoving={longestStable} displacementOver24={relocations} leftmost={lowestX:F2} " +
                    $"body={body.Bottom}/{body.velocity} owner={human.Bottom} status={Get(run.Actor, "Status")} work[{work}] {NativeStepTiming(samples)}");
                if (exit < 0 || longestStable < 25)
                    foreach (string trace in recent) Console.WriteLine("TRIANGLE proxy " + trace);
                Require(turns >= 4 && moved >= 1350, "the independent exterior owner keeps walking and reversing during the proxy test");
                Require(retreat >= 0 && (lowerEntry > retreat || overRoof > retreat) && exit > retreat && longestStable >= 25 &&
                    relocations == 0 && run.NativeRelocations == 0 && !body.dead,
                    "an actual body exits the covered slope by a legal change of level and follows the already-moving owner outside");
                if (work.Available) work.AssertComplete(1700);
                Console.WriteLine("PASS native moving owner around triangle proxy terrain.");
            }
        }

        private static void PlaceFixtureSlope(int x, int y, int slope)
        {
            PlaceFixtureTile(x, y, TileID.Stone);
            Require(WorldGen.SlopeTile(x, y, slope, noEffects: true) && Main.tile[x, y].slope() == slope,
                "the original hammer retains the selected triangle slope orientation");
        }

        private static void PlaceFixtureStrip(int start, int end, int row, int type)
        {
            for (int x = start; x <= end; x++) PlaceFixtureTile(x, row, type);
        }
    }
}
