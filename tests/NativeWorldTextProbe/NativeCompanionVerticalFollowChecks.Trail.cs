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
                long oldAction = 0, previousAction = -1;
                var navigator = Navigator(run.Actor);
                for (int tick = 0; tick < 150 && planned < 0; tick++)
                {
                    run.StepToward(new Vector2(target, (human.Left.X < lip ? upper : lower) * 16), new Vector2(3.2f, 4));
                    Player body = run.Body;
                    float room = lip - body.Left.X;
                    if (OnFloor(body, upper) && body.velocity.X > 1 && room > OriginalBrakeRoom(body) + 3 &&
                        RouteStep(run.Actor).StartsWith("Drop:", StringComparison.Ordinal))
                    {
                        if (!(bool)Get(navigator, "ActionCommitted"))
                        {
                            planned = tick;
                            oldAction = (long)Get(navigator, "ActionId");
                        }
                    }
                    long action = (long)Get(navigator, "ActionId");
                    if (tick % 25 == 0 || action != previousAction)
                        Console.WriteLine($"FOLLOW owner warp preparation tick={tick} feet={body.Bottom}/{body.velocity} room={room:F2} " +
                            $"brake={OriginalBrakeRoom(body):F2} committed={Get(navigator, "ActionCommitted")} {RouteStep(run.Actor)} {PlanStatus(navigator)} {RouteTrace(navigator)}");
                    previousAction = action;
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
                    if (tick == 0)
                    {
                        var delivered = GetOptional(navigator, "LastMovementDelivery");
                        retainedOldDrop = (long)Get(navigator, "ActionId") == oldAction ||
                            delivered != null && (long)Get(delivered, "ActionId") == oldAction;
                    }
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
                int brake = -1;
                for (int t = 0; t < 55; t++)
                {
                    run.StepOwner(Vector2.Zero);
                    if (brake < 0 && Math.Abs(body.velocity.X) < .3f) brake = t;
                }
                float distance = Vector2.Distance(origin, body.position);
                Console.WriteLine($"FOLLOW brake root initialSpeed={initialSpeed:F2} brake={brake} walked={distance:F1} {PlanStatus(navigation)}");
                Require(brake >= 0 && distance < 42 && body.Right.X <= wall * 16 && !body.dead,
                    "the real moving body brakes at the unchanged wall without tunneling or manufacturing a different root pose");
            }
        }

        private static void TrailFollow(object host, object profiles, Player human)
        {
            const int top = 80, lower = 92, wall = 68, wallTop = 75, leftEdge = 62, start = 65 * 16;
            using (var run = new Scenario(host, profiles, human, "follow-owner-turn", start, top, () =>
            {
                Fill(leftEdge, wall - 1, top, TileID.Stone);
                Fill(49, 98, lower, TileID.Stone);
                for (int y = wallTop; y < 88; y++) StoneVertical(wall, y);
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
                    Player first = run.Body;
                    int joined = -1, lowerLanding = -1, stable = 0, otherMoves = 0, maxSamplesInStep = 0;
                    int leftExit = -1, crown = -1, farSide = -1;
                    int unplannedFirst = 0, unplannedSecond = 0, maxTeamWork = 0, previousTeamWork = 0;
                    float leftmost = first.Bottom.X, largestStep = 0f;
                    Require(OnFloor(first, top) && Math.Abs(first.Bottom.X - start) <= .1f && first.Right.X < wall * 16,
                        "the first native body starts on the unchanged upper shelf strictly to the west of the wall");
                    long elapsed = 0, peak = 0;
                    long previous = 0, observedAtOwnerRest = 0;
                    Vector2 ownerAtRest = Vector2.Zero;
                    bool ownerRemainedStill = true;
                    bool together = false;
                    var navigation = Navigator(run.Actor);
                    var peerNavigation = Navigator(secondActor);
                    bool debugWork = GetOptional(navigation, "ExpansionWork") != null;
                    for (int tick = 0; tick < 470; tick++)
                    {
                        Vector2 target = tick < 16 ? new Vector2(59 * 16, top * 16) : tick < 46 ? new Vector2(59 * 16, lower * 16) :
                            tick < 93 ? new Vector2(79 * 16, lower * 16) : new Vector2(75 * 16, lower * 16);
                        if (tick >= 93 && tick < 108) target.X = 82 * 16;
                        Vector2 firstBefore = run.Body.position, before = second.position;
                        int firstSlice = (int)(GetOptional(navigation, "PlannerSlices") ?? 0), secondSlice = (int)(GetOptional(peerNavigation, "PlannerSlices") ?? 0);
                        long used = run.StepToward(target, new Vector2(8, 6.5f), measure: true, peer: second);
                        Measure(used, ref elapsed, ref peak);
                        largestStep = Math.Max(largestStep, Vector2.Distance(firstBefore, first.position));
                        if (Vector2.DistanceSquared(before, second.position) > .25f) otherMoves++;
                        if (firstSlice == (int)(GetOptional(navigation, "PlannerSlices") ?? 0) &&
                            Vector2.DistanceSquared(firstBefore, run.Body.position) > .25f)
                            unplannedFirst++;
                        if (secondSlice == (int)(GetOptional(peerNavigation, "PlannerSlices") ?? 0) &&
                            Vector2.DistanceSquared(before, second.position) > .25f)
                            unplannedSecond++;
                        if (debugWork)
                        {
                            int total = (int)Get(navigation, "ExpansionWork") + (int)Get(peerNavigation, "ExpansionWork");
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
                        string action = ObservedActionKind(navigation);
                        if (joined < 0 && (action == "Drop" || action == "PlatformDrop")) joined = tick;
                        leftmost = Math.Min(leftmost, first.Bottom.X);
                        if (leftExit < 0 && first.Right.X <= leftEdge * 16 + .1f && first.Bottom.Y > top * 16 + 4 && first.velocity.Y > 0)
                            leftExit = tick;
                        if (crown < 0 && OnFloor(first, wallTop) && first.Left.X < (wall + 1) * 16 && first.Right.X > wall * 16)
                            crown = tick;
                        if (crown >= 0 && farSide < 0 && first.Left.X >= (wall + 1) * 16)
                            farSide = tick;
                        if (lowerLanding < 0 && OnFloor(run.Body, lower)) lowerLanding = tick;
                        stable = OnFloor(run.Body, lower) && Math.Abs(run.Body.Bottom.X - human.Bottom.X) < 34 &&
                            Math.Abs(run.Body.velocity.X) < 1 ? stable + 1 : 0;
                        // The owner clock is independent of the two followers' arrival.
                        if (tick == 445) { observedAtOwnerRest = previous; ownerAtRest = human.Bottom; }
                        if (tick >= 445) ownerRemainedStill &= human.velocity == Vector2.Zero && human.Bottom == ownerAtRest;
                        if (tick == 50 || tick == 100 || tick == 180 || tick == 260 || tick == 469)
                            Console.WriteLine($"FOLLOW trail t={tick} owner={human.Bottom} actor={run.Body.Bottom}/{run.Body.velocity} route={RouteStep(run.Actor)} dropAction={joined}");
                    }
                    Console.WriteLine($"FOLLOW trail turn dropAction={joined} nativeLower={lowerLanding} leftmost={leftmost:F1} leftExit={leftExit} wallCrown={crown} east={farSide} maxStep={largestStep:F2} stable={stable} peerMoves={otherMoves} shared={together} maxSamples={maxSamplesInStep} ownerRest={ownerRemainedStill}/{observedAtOwnerRest}→{previous} unplannedSteps={unplannedFirst}/{unplannedSecond} teamWork={(debugWork ? maxTeamWork : -1)}");
                    work.Report("owner-trail-team", 470, elapsed, peak);
                    Require(together && maxSamplesInStep <= 2 && (!debugWork || unplannedFirst > 20 && unplannedSecond > 20 && maxTeamWork <= 96) && otherMoves > 30 && ownerRemainedStill && observedAtOwnerRest != 0 && observedAtOwnerRest == previous,
                        "two native followers observe one shared bounded owner trail and a stationary owner creates no duplicate samples");
                    bool exitedLeft = leftExit >= 0 && lowerLanding > leftExit;
                    bool crossedCrown = crown >= 0 && farSide >= crown && farSide < lowerLanding;
                    Require(joined >= 0 && lowerLanding > joined && (exitedLeft || crossedCrown) && stable >= 24 &&
                        ReferenceEquals(first, Main.player[first.whoAmI]) && largestStep < 24 && first.wingsLogic == 0 && !first.mount.Active,
                        "the blocked ground-only follower uses a real descent path into the lower tier and settles after the owner's reversal");

                    var shared = Get(host, "ownerTrail");
                    int segment = (int)Get(shared, "Segment");
                    Vector2 oldBody = run.Body.position, oldHuman = human.position;
                    human.Teleport(oldHuman + new Vector2(190, 0), 0);
                    run.StepOwner(Vector2.Zero, peer: second);
                    Console.WriteLine($"FOLLOW owner warp oldSegment={segment} newSegment={Get(shared, "Segment")} ownerDisplacement={human.position.X-oldHuman.X:F1} actorDisplacement={Vector2.Distance(run.Body.position,oldBody):F1}");
                    Require(human.position.X >= oldHuman.X + 188 && (int)Get(shared, "Segment") != segment &&
                        Vector2.DistanceSquared(run.Body.position, oldBody) < 12 * 12,
                        "an actual owner teleport splits the shared trail and retires only guide progress without relocating the follower");
                    int shortcutStable = 0;
                    float peakSpeed = 0;
                    for (int tick = 0; tick < 155; tick++)
                    {
                        run.StepToward(new Vector2(94 * 16, lower * 16), new Vector2(1.15f, 0), peer: second);
                        peakSpeed = Math.Max(peakSpeed, Math.Abs(run.Body.velocity.X));
                        shortcutStable = OnFloor(run.Body, lower) && Math.Abs(run.Body.Bottom.X-human.Bottom.X) < 32 ?
                            shortcutStable + 1 : 0;
                    }
                    Console.WriteLine($"FOLLOW open shortcut ownerSpeed=1.15 actorPeak={peakSpeed:F2} stable={shortcutStable}");
                    Require(peakSpeed > 2 && shortcutStable >= 20,
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
