using System;
using System.Collections;
using System.Diagnostics;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionVerticalFollowChecks
    {
        private static void AdjacentPlatforms(object host, object profiles, Player human)
        {
            const int top = 80, lower = 82, x = 61 * 16 + 8;
            using (var run = new Scenario(host, profiles, human, "follow-two-tiers", x, top, () =>
            {
                Fill(48, 83, top, TileID.Platforms);
                Fill(48, 83, lower, TileID.Platforms);
                Fill(47, 84, lower + 4, TileID.Stone);
            }))
            {
                human.Bottom = new Vector2(x, lower * 16);
                int firstDown = -1, crossed = -1, landing = -1, stable = 0;
                for (int tick = 0; tick < 105; tick++)
                {
                    run.StepOwner(Vector2.Zero);
                    Player body = run.Body;
                    if (firstDown < 0 && body.controlDown && body.stairFall) firstDown = tick;
                    if (crossed < 0 && body.Bottom.Y > top * 16 + 16) crossed = tick;
                    if (OnFloor(body, lower)) { if (landing < 0) landing = tick; stable++; }
                    else stable = 0;
                }
                Console.WriteLine($"FOLLOW two tiers initialGap=32 down={firstDown} crossed={crossed} landing={landing} stable={stable} final={run.Body.Bottom}");
                Require(firstDown >= 0 && crossed > firstDown && landing >= crossed && stable >= 24 && !run.Body.controlDown,
                    "a nearby stationary owner on the next platform is reached through the real support transition and stable landing");
            }
            TrailFlightMismatch(host, profiles, human);
            OwnerWarpBeforeDeparture(host, profiles, human);
            FailedMovingRoot(host, profiles, human);
        }

        private static void TrailFlightMismatch(object host, object profiles, Player human)
        {
            const int floor = 92, wall = 70, wallTop = 74, start = 65 * 16, exit = 81 * 16;
            using (var run = new Scenario(host, profiles, human, "follow-flight-gap", start, floor, () =>
            {
                Fill(50, 104, floor, TileID.Stone);
                for (int y = wallTop; y < floor - 3; y++) StoneVertical(wall, y);
                for (int row = 0; row < 3; row++)
                {
                    var tile = Main.tile[wall, floor - 3 + row];
                    tile.active(true);
                    tile.type = TileID.ClosedDoor;
                    tile.frameX = 0;
                    tile.frameY = (short)(row * 18);
                }
            }))
            {
                int ownerCleared = -1, opened = -1, crossed = -1, stable = 0;
                float highestBody = run.Body.Bottom.Y;
                // Only the owner's legs of the flight are fixtures. The
                // ground-only actor controls itself with live door geometry.
                for (int tick = 0; tick < 330; tick++)
                {
                    Vector2 target = tick < 39 ? new Vector2(start, (wallTop - 1) * 16) :
                        tick < 79 ? new Vector2(exit, (wallTop - 1) * 16) : new Vector2(exit, floor * 16);
                    run.StepToward(target, new Vector2(7, 8));
                    var body = run.Body;
                    if (ownerCleared < 0 && human.Left.X > (wall + 1) * 16 && human.Bottom.Y <= wallTop * 16)
                        ownerCleared = tick;
                    if (opened < 0 && Main.tile[wall, floor - 2].type == TileID.OpenDoor) opened = tick;
                    if (crossed < 0 && body.Left.X > (wall + 1) * 16) crossed = tick;
                    highestBody = Math.Min(highestBody, body.Bottom.Y);
                    stable = OnFloor(body, floor) && Math.Abs(body.Bottom.X - human.Bottom.X) < 32 &&
                        Math.Abs(body.velocity.X) < 1 ? stable + 1 : 0;
                }
                Console.WriteLine($"FOLLOW flight mismatch ownerOver={ownerCleared} opened={opened} crossed={crossed} actorHighest={highestBody:F1} stable={stable}");
                Require(ownerCleared >= 0 && opened >= 0 && crossed > opened && highestBody > wallTop * 16 + 80 &&
                    stable > 30 && run.Body.wingsLogic == 0 && !run.Body.mount.Active,
                    "the ground-only body chooses its actual door after the owner flies over a wall it cannot fly over");
            }
        }

        private static void OwnerWarpBeforeDeparture(object host, object profiles, Player human)
        {
            const int upper = 80, lower = 86, edge = 65, lip = edge * 16, start = lip - 53, target = lip + 55;
            using (var run = new Scenario(host, profiles, human, "follow-warp-at-lip", start, upper, () =>
            {
                Fill(47, edge - 1, upper, TileID.Stone);
                Fill(edge + 2, edge + 4, lower, TileID.Stone);
                Fill(47, 90, 115, TileID.Stone);
            }))
            {
                int planned = -1;
                for (int tick = 0; tick < 150 && planned < 0; tick++)
                {
                    run.StepToward(new Vector2(target, (human.Left.X < lip ? upper : lower) * 16), new Vector2(3.2f, 4));
                    Player body = run.Body;
                    float room = lip - body.Left.X;
                    if (OnFloor(body, upper) && body.velocity.X > 1 && room > OriginalBrakeRoom(body) + 3 &&
                        RouteStep(run.Actor).StartsWith("LedgeDrop:", StringComparison.Ordinal))
                        planned = tick;
                }
                Require(planned >= 0, "real Follow has an unissued drop at a still-brakeable upper ledge before the owner moves");
                Player companion = run.Body;
                Vector2 ownerTarget = new Vector2(54 * 16 - human.width * .5f, upper * 16 - human.height);
                human.Teleport(ownerTarget, 0);
                int firstAway = -1, stable = 0;
                float maxDrop = 0;
                bool retainedOldDrop = false;
                for (int tick = 0; tick < 175; tick++)
                {
                    run.StepOwner(Vector2.Zero);
                    if (tick == 0) retainedOldDrop = RouteStep(run.Actor).StartsWith("LedgeDrop:", StringComparison.Ordinal);
                    if (firstAway < 0 && companion.velocity.X < -.1f) firstAway = tick;
                    maxDrop = Math.Max(maxDrop, companion.Bottom.Y - upper * 16);
                    stable = OnFloor(companion, upper) && Math.Abs(companion.Bottom.X - human.Bottom.X) < 32 ? stable + 1 : 0;
                }
                Console.WriteLine($"FOLLOW owner warp before lip planned={planned} oldDropAfterWarp={retainedOldDrop} reversed={firstAway} maxDrop={maxDrop:F2} stable={stable}");
                Require(human.position == ownerTarget && !retainedOldDrop && firstAway >= 0 && firstAway <= 24 && maxDrop <= 1 && stable >= 20,
                    "a real owner warp retires an unstarted old ledge plan before it commits to the wrong layer");
            }
        }

        private static void FailedMovingRoot(object host, object profiles, Player human)
        {
            const int floor = 80, wall = 66, start = 59 * 16, targetX = 77 * 16;
            using (var run = new Scenario(host, profiles, human, "follow-brake-root", start, floor, () =>
            {
                Fill(51, 90, floor, TileID.Stone);
                Fill(55, wall, floor - 4, TileID.Stone);
                for (int y = floor - 3; y < floor; y++)
                {
                    StoneVertical(55, y);
                    StoneVertical(wall, y);
                }
            }))
            {
                int running = -1;
                Player body = run.Body;
                for (int tick = 0; tick < 100 && running < 0; tick++)
                {
                    run.StepToward(new Vector2(targetX, floor * 16), new Vector2(6, 0));
                    if (body.Left.X > wall * 16 - 48 && body.velocity.X >= 2.2f) running = tick;
                }
                Require(running >= 0, "the follower actually reaches normal high running speed with the obstacle still ahead");
                Vector2 origin = body.position;
                float initialSpeed = body.velocity.X;
                human.Bottom = new Vector2(targetX, floor * 16);
                var navigation = Navigator(run.Actor);
                var state = Get(run.Actor, "Mobility");
                var root = Enum.Parse(AccessTools.Inner(navigation.GetType(), "RouteMotion"), "Ground");
                var route = (IList)Get(GetOptional(navigation, "actions") ?? navigation, "route");
                Call(navigation, "CancelRoute");
                using (var work = new TerrainSearchWork(navigation))
                {
                    var goal = human.Center - body.Size * .5f;
                    int firstBand = -1;
                    for (int t = 0; t < 12 && firstBand < 0; t++)
                    {
                        // Study the failed outgoing candidates at the
                        // observed fast pose without applying a planner frame.
                        NativeQuickItemChecks.BeginWorldStep();
                        Call(navigation, "SearchRoute", body, Get(state, "Observed"), goal, 22, root, false);
                        var initial = Get(navigation, "search");
                        if ((bool)(GetOptional(initial, "RootVelocityRead") ?? false) && route.Count == 0)
                            firstBand = (int)Get(initial, "RootVelocityBand");
                    }
                    int checksBefore = work.Candidates, speedWake = -1;
                    for (int t = 0; t < 55 && speedWake < 0; t++)
                    {
                        run.StepOwner(Vector2.Zero);
                        var current = Get(navigation, "search");
                        if (firstBand > 0 && Math.Abs(body.velocity.X) < .3f &&
                            (int)Get(current, "RootVelocityBand") == 0 && (bool)Get(current, "RootVelocityRead") &&
                            work.Candidates > checksBefore) speedWake = t;
                    }
                    float distance = Vector2.Distance(origin, body.position);
                    Console.WriteLine($"FOLLOW brake root initialSpeed={initialSpeed:F2} band={firstBand} wake={speedWake} walked={distance:F1} starts={work.Starts} candidates={checksBefore}->{work.Candidates}");
                    Require(firstBand > 0 && speedWake >= 0 && distance < 42 && work.Starts == 1 &&
                        body.Right.X <= wall * 16 && !body.dead,
                        "braking alone reopens the actual movement-dependent root before 42 pixels, preserving the prior graph and blocking the unchanged solid wall");
                }
            }
        }

        private static void TrailFollow(object host, object profiles, Player human)
        {
            const int top = 80, lower = 92, wall = 68, start = 65 * 16;
            using (var run = new Scenario(host, profiles, human, "follow-owner-turn", start, top, () =>
            {
                Fill(62, wall - 1, top, TileID.Stone);
                Fill(49, 98, lower, TileID.Stone);
                for (int y = 75; y < 88; y++) StoneVertical(wall, y);
            }))
            using (var work = new TerrainSearchWork(Navigator(run.Actor)))
            {
                // These genuine native effects hold the first actor while
                // his moving owner records a useful old lip. He has no wings.
                run.Body.AddBuff(BuffID.Frozen, 38);
                Require((bool)Call(host, "Create", "follow-owner-peer"), "a second same-owner profile enters the normal lifecycle");
                Guid secondId = (Guid)Get(host, "LastCreatedId");
                NativeQuickItemChecks.Until(() => { Call(profiles, "Pump"); return (bool)Call(profiles, "IsSaved", secondId); });
                Require((bool)Call(host, "Join", secondId), "join the second real body");
                Player second = (Player)Call(host, "GetBody", secondId);
                object secondActor = Call(host, "Owned", second);
                second.Bottom = run.Body.Bottom;
                Require((bool)Call(host, "SetBehavior", secondId, new CompanionBehavior { Follow = true, AttackEnabled = false }),
                    "both real actors share the owner's trajectory");
                try
                {
                    int joined = -1, lowerLanding = -1, stable = 0, otherMoves = 0, maxSamplesInStep = 0;
                    int unplannedFirst = 0, unplannedSecond = 0, maxTeamWork = 0, previousTeamWork = 0;
                    float leftmost = run.Body.Bottom.X;
                    long elapsed = 0, peak = 0;
                    long previous = 0, consumed = 0, observedAtArrival = 0;
                    bool together = false;
                    var navigation = Navigator(run.Actor);
                    var goals = GetOptional(navigation, "goals") ?? navigation;
                    var peerNavigation = Navigator(secondActor);
                    bool debugWork = GetOptional(navigation, "RouteWork") != null;
                    for (int tick = 0; tick < 470; tick++)
                    {
                        Vector2 target = tick < 16 ? new Vector2(59 * 16, top * 16) : tick < 46 ? new Vector2(59 * 16, lower * 16) :
                            tick < 93 ? new Vector2(79 * 16, lower * 16) : new Vector2(75 * 16, lower * 16);
                        if (tick >= 93 && tick < 108) target.X = 82 * 16;
                        Vector2 firstBefore = run.Body.position, before = second.position;
                        long used = run.StepToward(target, new Vector2(8, 6.5f), measure: true);
                        long peerStarted = Stopwatch.GetTimestamp();
                        second.Update(second.whoAmI);
                        Measure(used + Stopwatch.GetTimestamp() - peerStarted, ref elapsed, ref peak);
                        if (Vector2.DistanceSquared(before, second.position) > .25f) otherMoves++;
                        object selected = GetOptional(host, "planningActor");
                        if (!ReferenceEquals(selected, run.Actor) && Vector2.DistanceSquared(firstBefore, run.Body.position) > .25f)
                            unplannedFirst++;
                        if (!ReferenceEquals(selected, secondActor) && Vector2.DistanceSquared(before, second.position) > .25f)
                            unplannedSecond++;
                        if (debugWork)
                        {
                            int total = (int)Get(navigation, "RouteWork") + (int)Get(peerNavigation, "RouteWork");
                            maxTeamWork = Math.Max(maxTeamWork, total - previousTeamWork);
                            previousTeamWork = total;
                        }
                        object trail = GetOptional(host, "ownerTrail");
                        if (trail != null)
                        {
                            long latest = (long)Get(trail, "Latest");
                            if (previous != 0) maxSamplesInStep = Math.Max(maxSamplesInStep, (int)(latest - previous));
                            previous = latest;
                            together |= ReferenceEquals(GetOptional(Get(run.Actor, "Behavior"), "OwnerTrail"),
                                GetOptional(Get(secondActor, "Behavior"), "OwnerTrail")) && latest > 3;
                        }
                        long anchor = (long)(GetOptional(goals, "trailJoined") ?? 0L);
                        if (anchor > 0 && joined < 0) joined = tick;
                        consumed = Math.Max(consumed, (long)(GetOptional(goals, "trailConsumed") ?? 0L));
                        leftmost = Math.Min(leftmost, run.Body.Bottom.X);
                        if (lowerLanding < 0 && OnFloor(run.Body, lower)) lowerLanding = tick;
                        stable = OnFloor(run.Body, lower) && Math.Abs(run.Body.Bottom.X - human.Bottom.X) < 34 &&
                            Math.Abs(run.Body.velocity.X) < 1 ? stable + 1 : 0;
                        if (stable == 24 && observedAtArrival == 0) observedAtArrival = previous;
                        if (tick == 50 || tick == 100 || tick == 180 || tick == 260 || tick == 469)
                            Console.WriteLine($"FOLLOW trail t={tick} owner={human.Bottom} actor={run.Body.Bottom}/{run.Body.velocity} route={RouteStep(run.Actor)} join={anchor} cursor={consumed}");
                    }
                    Console.WriteLine($"FOLLOW trail turn join={joined} consumed={consumed} nativeLower={lowerLanding} leftmost={leftmost:F1} stable={stable} peerMoves={otherMoves} shared={together} maxSamples={maxSamplesInStep} unplannedSteps={unplannedFirst}/{unplannedSecond} teamWork={(debugWork ? maxTeamWork : -1)}");
                    work.Report("owner-trail-team", 470, elapsed, peak);
                    Require(together && maxSamplesInStep <= 2 && unplannedFirst > 20 && unplannedSecond > 20 &&
                        (!debugWork || maxTeamWork <= 160) && otherMoves > 30 && observedAtArrival != 0 && observedAtArrival == previous,
                        "two native followers observe one shared bounded owner trail and a stationary owner creates no duplicate samples");
                    Require(joined >= 0 && consumed > 0 && leftmost < 62 * 16 && lowerLanding > joined && stable >= 24 &&
                        run.Body.wingsLogic == 0 && !run.Body.mount.Active,
                        "the blocked ground-only follower uses an earlier owner's waypoint, takes its own real path into the lower tier and settles after the owner's reversal");

                    var shared = Get(host, "ownerTrail");
                    int segment = (int)Get(shared, "Segment");
                    Vector2 oldBody = run.Body.position, oldHuman = human.position;
                    human.Teleport(oldHuman + new Vector2(190, 0), 0);
                    run.StepOwner(Vector2.Zero);
                    second.Update(second.whoAmI);
                    Console.WriteLine($"FOLLOW owner warp oldSegment={segment} newSegment={Get(shared, "Segment")} ownerDisplacement={human.position.X-oldHuman.X:F1} actorDisplacement={Vector2.Distance(run.Body.position,oldBody):F1}");
                    Require(human.position.X >= oldHuman.X + 188 && (int)Get(shared, "Segment") != segment &&
                        Vector2.DistanceSquared(run.Body.position, oldBody) < 12 * 12 &&
                        (long)Get(goals, "trailJoined") == 0,
                        "an actual owner teleport splits the shared trail and retires only guide progress without relocating the follower");
                    int guidedOnOpenGround = 0, shortcutStable = 0;
                    float peakSpeed = 0;
                    for (int tick = 0; tick < 155; tick++)
                    {
                        run.StepToward(new Vector2(94 * 16, lower * 16), new Vector2(1.15f, 0));
                        second.Update(second.whoAmI);
                        if ((long)Get(goals, "trailJoined") != 0) guidedOnOpenGround++;
                        peakSpeed = Math.Max(peakSpeed, Math.Abs(run.Body.velocity.X));
                        shortcutStable = OnFloor(run.Body, lower) && Math.Abs(run.Body.Bottom.X-human.Bottom.X) < 32 ?
                            shortcutStable + 1 : 0;
                    }
                    Console.WriteLine($"FOLLOW open shortcut ownerSpeed=1.15 actorPeak={peakSpeed:F2} historicSteps={guidedOnOpenGround} stable={shortcutStable}");
                    Require(peakSpeed > 2 && guidedOnOpenGround == 0 && shortcutStable >= 20,
                        "a faster body takes the continuously supported live shortcut without replaying the older trail");
                }
                finally
                {
                    Call(host, "Recall", secondId);
                    NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); return !((IDictionary)Get(host, "actors")).Contains(secondId); });
                }
            }
        }
    }
}
