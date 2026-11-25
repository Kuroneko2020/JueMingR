using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
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
        private const int RoomUpper = 75;
        private const int RoomLower = 80;
        private const int RoomPartition = 64;
        private const int RoomDoor = 83;
        private const int RoomDeadline = 1700;

        private static void MovingRoom(object context, object host, object profiles, Player human,
            bool interruptedTeam = false, bool stress = false)
        {
            string variant = stress ? "moving-room-sliced" : interruptedTeam ? "moving-room-team" : "moving-room";
            FieldInfo budgetOverride = null;
            object priorBudget = null;
            if (stress)
            {
                budgetOverride = host.GetType().GetField("NavigationBudgetOverride", ReflectionMembers);
                Require(budgetOverride != null, "the formal pressure run must use the existing runtime budget seam");
                priorBudget = budgetOverride.GetValue(host);
                budgetOverride.SetValue(host, Make(budgetOverride.FieldType, 208, 4, 6));
            }
            try
            {
                RunMovingRoom(context, host, profiles, human, interruptedTeam, stress, variant);
            }
            finally { budgetOverride?.SetValue(host, priorBudget); }
        }

        private static void RunMovingRoom(object context, object host, object profiles, Player human,
            bool interruptedTeam, bool stress, string variant)
        {
            using (var run = new NativeFollowWorld(context, host, profiles, human, variant,
                new Vector2(RoomX(47), (stress ? RoomLower : RoomUpper) * 16),
                new Vector2(RoomX(53), RoomUpper * 16), BuildMovingRoom))
            {
                int startingLife = run.Body.statLife;
                if (interruptedTeam)
                {
                    run.JoinPeer("moving-room-peer", new Vector2(RoomX(58), RoomLower * 16));
                    startingLife = Math.Max(1, run.Body.statLifeMax2 - 40);
                    run.Body.statLife = startingLife;
                    run.Body.inventory[33] = new Item(ItemID.LesserHealingPotion);
                }
                var driver = new RoomOwnerRoute();
                var ownerProgress = new RoomProgress(human);
                var movement = new RoomProgress(run.Body);
                var peer = run.Peer == null ? null : new RoomProgress(run.Peer);
                var allocationCounter = (Func<long>)typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",
                    BindingFlags.Static | BindingFlags.Public)?.CreateDelegate(typeof(Func<long>));
                var samples = new List<long>(RoomDeadline);
                int nearby = 0, longestNearby = 0, nearWindows = 0, movingSamples = 0, teleports = 0;
                int nearPeer = 0, peerNearWindows = 0;
                int farQuiet = 0, longestFarQuiet = 0;
                int farProgressAge = 0, longestFarProgressAge = 0, firstUseful = -1, longestProgressEnd = -1;
                Vector2 achieved = run.Body.Bottom;
                int frozenStart = -1, frozenEnd = -1, resumed = -1, laterDescent = -1;
                int healedInMotion = -1;
                int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
                long allocated = 0;
                const int interruptionTick = 680;
                const int potionTick = 60;
                object sameNavigator = Navigator(run.Actor);
                object interruptedWorld = null;
                int worldEpochAtInterruption = -1;
                var primaryWork = new RoomWork(sameNavigator);
                var peerWork = new RoomWork(run.PeerActor == null ? null : Navigator(run.PeerActor));
                bool realReleaseTimer = !primaryWork.Available && sameNavigator != null;
                using (var measurement = realReleaseTimer ? new RoomPhaseTiming(sameNavigator,
                    run.PeerActor == null ? null : Navigator(run.PeerActor)) : null)
                {
                    int maxTeamTiles = 0, maxTeamExpansions = 0, maxTeamPredictions = 0, maxTeamShapes = 0;
                    var recent = new Queue<string>(24);
                    for (int tick = 0; tick < RoomDeadline; tick++)
                    {
                        // A fixed native immobilization changes the actual body
                        // result without editing terrain or retiring its session.
                        if (interruptedTeam && tick == interruptionTick)
                        {
                            interruptedWorld = GetOptional(sameNavigator, "world");
                            if (interruptedWorld != null) worldEpochAtInterruption = (int)Get(interruptedWorld, "Epoch");
                            run.Body.AddBuff(BuffID.Frozen, 38);
                        }
                        if (interruptedTeam && tick == potionTick)
                            Require((bool)Call(host, "UseItemOnce", ((CompanionProfile)Get(run.Actor, "Profile")).Id,
                                CompanionInventoryArea.Inventory, 33), "a real manual potion request is accepted while Follow is active");
                        OwnerButtons buttons = driver.Read(human, tick);
                        long allocationBefore = allocationCounter?.Invoke() ?? 0;
                        samples.Add(run.Step(buttons.Axis, buttons.Jump, buttons.Down));
                        measurement?.Capture(tick >= 45);
                        if (tick >= 45 && allocationCounter != null) allocated += allocationCounter() - allocationBefore;
                        int phase = driver.Phase;
                        primaryWork.Read(phase, !run.Body.frozen);
                        peerWork.Read(phase);
                        maxTeamTiles = Math.Max(maxTeamTiles, primaryWork.StepTiles + peerWork.StepTiles);
                        maxTeamExpansions = Math.Max(maxTeamExpansions, primaryWork.StepExpansions + peerWork.StepExpansions);
                        maxTeamPredictions = Math.Max(maxTeamPredictions, primaryWork.StepPredictions + peerWork.StepPredictions);
                        maxTeamShapes = Math.Max(maxTeamShapes, primaryWork.StepShapes + peerWork.StepShapes);
                        Vector2 ownerDelta = ownerProgress.Record(human, tick);
                        int previousDescents = movement.Descents;
                        int previousAscents = movement.Ascents, previousDoors = movement.DoorCrossings;
                        Vector2 bodyDelta = movement.Record(run.Body, tick);
                        bool crossedSupport = movement.Descents != previousDescents || movement.Ascents != previousAscents ||
                            movement.DoorCrossings != previousDoors;
                        // Empty jumps and planner/action turnover do not advance the
                        // checkpoint. Count a real floor/door crossing or a ground-scale stride.
                        bool useful = crossedSupport || Math.Abs(achieved.X - run.Body.Bottom.X) >= 24f;
                        bool following = SameOpenRoomBand(human, run.Body) && Vector2.DistanceSquared(human.Bottom, run.Body.Bottom) <= 96 * 96;
                        if (useful)
                        {
                            achieved = run.Body.Bottom;
                            if (firstUseful < 0) firstUseful = tick;
                        }
                        farProgressAge = useful || following ? 0 : farProgressAge + 1;
                        if (farProgressAge > longestFarProgressAge)
                        {
                            longestFarProgressAge = farProgressAge;
                            longestProgressEnd = tick;
                        }
                        if (farProgressAge > 0 && farProgressAge % 50 == 0)
                            Console.WriteLine($"MOVING ROOM stalled variant={variant} age={farProgressAge} " +
                                RoomTrace(run, human, sameNavigator, tick, phase));
                        if (interruptedTeam && healedInMotion < 0 && tick >= potionTick && run.Body.statLife > startingLife &&
                            run.Body.inventory[33].IsAir && bodyDelta.LengthSquared() > .25f)
                            healedInMotion = tick;
                        if (peer != null && peer.Record(run.Peer, tick).LengthSquared() > 24 * 24) teleports++;
                        if (bodyDelta.LengthSquared() > 24 * 24) teleports++;
                        if (run.Body.frozen)
                        {
                            if (frozenStart < 0) frozenStart = tick;
                            frozenEnd = tick;
                        }
                        if (frozenEnd >= 0 && !run.Body.frozen && Math.Abs(bodyDelta.X) > .5f && resumed < 0 &&
                            (run.Body.controlLeft || run.Body.controlRight)) resumed = tick;
                        if (resumed >= 0 && laterDescent < 0 && movement.Descents > previousDescents) laterDescent = tick;
                        if (ownerDelta.LengthSquared() > .25f)
                        {
                            movingSamples++;
                            nearby = following ? nearby + 1 : 0;
                            nearPeer = run.Peer != null && SameOpenRoomBand(human, run.Peer) &&
                                Vector2.DistanceSquared(human.Bottom, run.Peer.Bottom) <= 96 * 96 ? nearPeer + 1 : 0;
                            farQuiet = !following && bodyDelta.LengthSquared() <= .25f && !run.Body.frozen ? farQuiet + 1 : 0;
                            if (nearby == 20) nearWindows++;
                            if (nearPeer == 20) peerNearWindows++;
                            longestNearby = Math.Max(nearby, longestNearby);
                            longestFarQuiet = Math.Max(longestFarQuiet, farQuiet);
                        }
                        else nearby = nearPeer = farQuiet = 0;
                        if (tick % 150 == 0)
                            Console.WriteLine(RoomTrace(run, human, sameNavigator, tick, phase));
                        if (tick % 12 == 0)
                        {
                            if (recent.Count == 24) recent.Dequeue();
                            recent.Enqueue(RoomTrace(run, human, sameNavigator, tick, phase));
                        }
                    }
                    Console.WriteLine($"MOVING ROOM variant={variant} inputPhases={driver.Transitions} ownerMoving={movingSamples} owner[{ownerProgress}] ai[{movement}] " +
                        $"peer[{peer}] nearWindows={nearWindows}/{peerNearWindows} nearbyMoving={longestNearby} nativeTeleports={run.NativeRelocations} displacementOver24={teleports} " +
                        $"farQuietWhileOwnerMoves={longestFarQuiet} firstUseful={firstUseful} farWithoutSupportOr24px={longestFarProgressAge} " +
                        $"farInterval={longestProgressEnd - longestFarProgressAge + 1}..{longestProgressEnd} " +
                    $"frozen={frozenStart}..{frozenEnd} resumed={resumed} laterDescent={laterDescent} healedInMotion={healedInMotion}");
                    Console.WriteLine($"MOVING ROOM warmup=45 {NativeStepTiming(samples.GetRange(0, 45))} " +
                        $"active={RoomDeadline - 45} {NativeStepTiming(samples.GetRange(45, RoomDeadline - 45))} " +
                        $"nativeStepAllocatedBytes={(allocationCounter == null ? "unavailable" : allocated.ToString())} " +
                        $"processGcAllSteps={GC.CollectionCount(0) - gen0}/{GC.CollectionCount(1) - gen1}/{GC.CollectionCount(2) - gen2}");
                    Console.WriteLine($"MOVING ROOM work primary[{primaryWork}] peer[{peerWork}] " +
                        $"maxTeamTiles/expansions/prediction/shapes={maxTeamTiles}/{maxTeamExpansions}/{maxTeamPredictions}/{maxTeamShapes}");
                    measurement?.Report(variant);
                    // The solo loop owns repeated through-door proof. The team
                    // requires both actual paths and checks rotation and recovery
                    // while one actor handles the interruption.
                    int requiredPasses = interruptedTeam ? 1 : 2;
                    if (movement.Descents < 2 || movement.Ascents < 1 || movement.DoorCrossings < requiredPasses || nearWindows < 2)
                        foreach (string trace in recent) Console.WriteLine(trace);
                    Require(driver.Transitions >= 24 && movingSamples >= RoomDeadline * 4 / 5 &&
                        ownerProgress.Descents >= 4 && ownerProgress.Ascents >= 4 && ownerProgress.DoorCrossings >= 4,
                        "independent owner completes several native ordinary room laps within the preset moving window");
                    Require(run.NativeRelocations == 0, "no original teleport outlet moves any measured room body");
                    if (stress)
                    {
                        Require(primaryWork.PhaseChangesWhilePending > 0 && primaryWork.MaxPendingAge > 0 &&
                            movement.FirstMovement >= 0 && movement.Ascents >= 1 && teleports == 0 && !run.Body.dead,
                            "the fixed smaller slice sees an independent owner change regions while work is pending and the same body still progresses");
                    }
                    else
                    {
                        // The frozen f026 Release in this same fixture reached its first real stride
                        // at 23 and held for at most 103/136 updates in the single/team run. Allow one
                        // 24-pixel acceleration interval (25 native updates from rest) and four scheduler steps.
                        int longestAllowed = (interruptedTeam ? 136 : 103) + 25 + 4;
                        Require(firstUseful >= 0 && firstUseful <= 27 && longestFarProgressAge <= longestAllowed &&
                            movement.Descents >= 2 && movement.Ascents >= 1 && movement.DoorCrossings >= requiredPasses &&
                            nearWindows >= 2 && teleports == 0 && !run.Body.dead,
                            "the ordinary companion makes prompt useful moves, descends, climbs and crosses the actual door beside a moving owner without relocating");
                    }
                    if (interruptedTeam)
                    {
                        Require(frozenStart >= interruptionTick && frozenEnd >= frozenStart + 20 &&
                            resumed > frozenEnd && laterDescent > resumed && ReferenceEquals(sameNavigator, Navigator(run.Actor)) &&
                            ReferenceEquals(run.Body, Get(run.Actor, "Body")) && (interruptedWorld == null ||
                                ReferenceEquals(interruptedWorld, Get(sameNavigator, "world")) &&
                                (int)Get(interruptedWorld, "Epoch") == worldEpochAtInterruption),
                            "the real frozen interruption ends and the original body resumes and descends on unchanged terrain");
                        Require(peer.Descents >= 2 && peer.Ascents >= 1 && peer.DoorCrossings >= 1 && peerNearWindows >= 1 && !run.Peer.dead,
                            "the second original body climbs around the inside partition, drops and continues through the door while the first is active");
                        Require(healedInMotion >= potionTick && healedInMotion <= potionTick + 15,
                            "the same native body pays for and benefits from the potion while continuing world-space movement");
                    }
                    if (primaryWork.Available)
                    {
                        int tiles = stress ? 208 : 2600, expansion = stress ? 4 : 96, prediction = stress ? 6 : 48;
                        primaryWork.AssertComplete(RoomDeadline);
                        if (interruptedTeam)
                        {
                            peerWork.AssertComplete(RoomDeadline);
                            Require(primaryWork.MaxServiceGap <= 4 && peerWork.MaxServiceGap <= 4,
                                "the unchanged world-step scheduler gives both real active navigators regular planning service");
                        }
                        Require(maxTeamTiles <= tiles && maxTeamExpansions <= expansion && maxTeamPredictions <= prediction &&
                            maxTeamShapes <= Math.Max(2048, tiles + expansion * 64),
                            "actual summed heavy work stays within the world's shared production or fixed stress slice");
                    }
                    Console.WriteLine($"PASS native moving room scenario {variant}.");
                }
            }
        }

        private static string RoomTrace(NativeFollowWorld run, Player owner, object nav, int step, int phase)
        {
            Player body = run.Body;
            string action = GetOptional(nav, "ActionId") is long number && number != 0 ? RouteStep(run.Actor) : "local";
            string suffix = RouteTrace(nav);
            return $"MOVING ROOM sample={step} phase={phase} owner={owner.Bottom}/{owner.velocity} ai={body.Bottom}/{body.velocity} " +
                $"controls={body.controlLeft}/{body.controlRight}/{body.controlJump}/{body.controlDown} " +
                $"query={GetOptional(nav, "QueryStatus")}/{GetOptional(nav, "QueryGeneration")}/{GetOptional(nav, "QueryPendingAge")} " +
                $"target={GetOptional(nav, "Target")} action={action} deferral={GetOptional(nav, "LastDeferral")} {suffix} status={Get(run.Actor, "Status")}";
        }

        internal static string RouteTrace(object nav)
        {
            object route = GetOptional(nav, "route");
            if (route == null || !(GetOptional(route, "corridor") is IList corridor)) return "";
            int start = (int)Get(route, "corridorIndex");
            var next = new List<string>();
            for (int i = start; i < corridor.Count && i < start + 4; i++)
            {
                object edge = corridor[i];
                next.Add($"{Get(edge, "Kind")}:{Get(edge, "From")}->{Get(edge, "To")}");
            }
            var query = GetOptional(route, "query");
            object goal = query == null ? null : GetOptional(query, "Goal");
            return $"corridor={start}/{corridor.Count}[{string.Join(",", next)}] goalCell={(goal == null ? null : GetOptional(goal, "Cell"))}";
        }

        internal static string RefinementTrace(object nav)
        {
            object refine = Get(nav, "refine"), edge = Get(refine, "Edge");
            return $"refinement={Get(refine, "Active")}/{Get(Get(refine, "Witness"), "Valid")} " +
                $"ticks={Get(refine, "ticks")} edge={Get(edge, "Kind")}/{Get(edge, "Detail")}:{Get(edge, "From")}→{Get(edge, "To")}";
        }

        private static void BuildMovingRoom()
        {
            PlaceFixtureStrip(43, 98, RoomLower, TileID.Stone);
            PlaceFixtureStrip(46, 77, RoomUpper, TileID.Platforms);
            for (int row = RoomUpper + 1; row < RoomLower; row++)
                PlaceFixtureTile(RoomPartition, row, TileID.Stone);
            PlaceFixtureStrip(80, 88, RoomLower - 4, TileID.Stone);
            for (int x = RoomDoor + 1; x <= 93; x++)
                Main.tile[x, RoomLower].halfBrick(true);
            TileObject doorChoice;
            Require(TileObject.CanPlace(RoomDoor, RoomLower - 3, TileID.ClosedDoor, 0, 1, out doorChoice) &&
                TileObject.Place(doorChoice), "the two-level room uses an original anchored side door");
            WorldGen.SquareTileFrame(RoomDoor, RoomLower - 2);
        }

        private static void MovingNearPartition(object context, object host, object profiles, Player human, bool interruptJump = false)
        {
            float innerWall = RoomPartition * 16;
            float bodyStart = innerWall - 10.5f;
            float ownerStart = innerWall + 26.5f;
            using (var run = new NativeFollowWorld(context, host, profiles, human, interruptJump ? "partition-roof" : "moving-partition",
                new Vector2(bodyStart, RoomLower * 16), new Vector2(ownerStart, RoomLower * 16), BuildMovingRoom))
            {
                Player body = run.Body;
                object navigation = Navigator(run.Actor);
                long lastAction = -1;
                long blockedAction = 0, recoveredAction = 0;
                long lastCheckedLaunch = 0;
                float blockedEntry = 0;
                int roofRow = 0;
                int blockedAt = -1, actualContact = -1, failed = -1, walkedAfterFailure = -1, differentJump = -1, released = -1;
                int repeatedLaunches = 0;
                bool blockedDirect = false;
                int direction = -1, turns = 0, over = -1, arrived = -1, close = 0, longestClose = 0, moving = 0;
                for (int tick = 0; tick < 680; tick++)
                {
                    // The owner starts within normal Follow spacing across
                    // solid tiles and reverses at its own interior bounds.
                    if (direction < 0 && human.Center.X <= ownerStart || direction > 0 && human.Center.X >= RoomX(74))
                    {
                        direction = -direction;
                        turns++;
                    }
                    Vector2 before = human.Bottom;
                    Vector2 beforeBody = body.Bottom;
                    float priorVy = body.velocity.Y;
                    run.Step(direction);
                    bool moved = Vector2.DistanceSquared(before, human.Bottom) > .25f;
                    if (moved) moving++;
                    if (over < 0 && body.Left.X >= innerWall && OnFloor(body, RoomUpper)) over = tick;
                    if (arrived < 0 && over >= 0 && body.Left.X >= innerWall + 16 && OnFloor(body, RoomLower)) arrived = tick;
                    close = arrived >= 0 && moved && SameOpenRoomBand(body, human) &&
                        Vector2.DistanceSquared(body.Bottom, human.Bottom) <= 72 * 72 ? close + 1 : 0;
                    longestClose = Math.Max(close, longestClose);
                    long action = (long)Get(navigation, "ActionId");
                    string kind = Get(navigation, "ActionKind").ToString();
                    if (interruptJump && blockedAt < 0 && action > 0 && kind == "Jump" && body.controlJump && body.velocity.Y < -2 &&
                        (bool)Get(navigation, "ActionCommitted"))
                    {
                        // Place a real roof above the advancing jump. The left edge leaves
                        // another ascent available along the same floor and upper platforms.
                        roofRow = (int)Math.Floor((body.Top.Y - 2) / 16) - 1;
                        blockedAt = tick;
                        blockedAction = action;
                        blockedDirect = (bool)Get(Get(navigation, "action"), "Direct");
                        blockedEntry = ((Vector2)Get(Get(navigation, "ActionTransition"), "From")).X;
                        PlaceFixtureStrip(RoomPartition - 2, RoomPartition - 1, roofRow, TileID.Stone);
                        Console.WriteLine($"MOVING PARTITION edited roof row={roofRow} tick={tick} action={blockedAction} before={body.Top}");
                    }
                    if (blockedAt >= 0 && action == blockedAction)
                    {
                        if (actualContact < 0 && tick > blockedAt && priorVy < -.5f && body.velocity.Y >= -.02f &&
                            Math.Abs(body.Top.Y - (roofRow + 1) * 16) < 1) actualContact = tick;
                        string outcome = Get(navigation, "ActionOutcome").ToString();
                        if (failed < 0 && (outcome == "Blocked" || outcome == "Rejected"))
                        {
                            failed = tick;
                            Console.WriteLine($"MOVING PARTITION actual {outcome} tick={tick} action={action} body={body.Bottom}");
                        }
                    }
                    if (failed >= 0 && differentJump < 0 && walkedAfterFailure < 0 && action != blockedAction && kind == "Walk" &&
                        OnFloor(body, RoomLower) && body.controlLeft && beforeBody.X - body.Bottom.X > .05f)
                        walkedAfterFailure = tick;
                    if (failed >= 0 && kind == "Jump" && body.controlJump && body.velocity.Y < -2 && action != blockedAction &&
                        action != lastCheckedLaunch && differentJump < 0)
                    {
                        lastCheckedLaunch = action;
                        if (Math.Abs(((Vector2)Get(Get(navigation, "ActionTransition"), "From")).X - blockedEntry) >= 14)
                        { differentJump = tick; recoveredAction = action; }
                        else repeatedLaunches++;
                    }
                    if (differentJump >= 0 && released < 0 && kind == "PlatformDrop" && body.Bottom.Y > RoomUpper * 16 + 10 && body.velocity.Y > 0)
                        released = tick;
                    if (tick % 60 == 0 || action != lastAction || tick == over || tick == arrived || tick >= 50 && tick <= 130 && tick % 8 == 0)
                        Console.WriteLine($"MOVING PARTITION tick={tick} owner={human.Bottom}/{human.velocity} body={body.Bottom}/{body.velocity} " +
                            $"inputs={body.controlLeft}/{body.controlRight}/{body.controlJump}/{body.controlDown} route={RouteStep(run.Actor)} " +
                            $"{PlanStatus(navigation)} {RouteTrace(navigation)}");
                    lastAction = action;
                }
                Console.WriteLine($"MOVING PARTITION startDistance={ownerStart - bodyStart:F1} ownerTurns={turns} moving={moving} over={over} " +
                    $"lowerArrival={arrived} nearMoving={longestClose} {RoomTrace(run, human, navigation, 680, turns)}");
                if (interruptJump)
                    Console.WriteLine($"FOLLOW LIVE RECOVERY issued={blockedAt}/{blockedAction} direct={blockedDirect} roofContact={actualContact} rejected={failed} " +
                        $"walkedAfterFailure={walkedAfterFailure} repeatedLaunches={repeatedLaunches} " +
                        $"alternateAscent={differentJump}/{recoveredAction} nativeRelease={released} land={arrived} nearbyMoving={longestClose}");
                Require(turns >= 5 && moving > 450 && over >= 0 && arrived > over && longestClose >= 20 &&
                    run.NativeRelocations == 0 && !body.dead,
                    "close distance across a solid partition cannot replace a real route into the moving owner's room");
                if (interruptJump)
                {
                    Require(blockedAt >= 0 && actualContact > blockedAt && failed >= actualContact && walkedAfterFailure > failed &&
                        repeatedLaunches == 0 && differentJump > walkedAfterFailure &&
                        released > differentJump && arrived > released && ReferenceEquals(navigation, Navigator(run.Actor)) &&
                        ReferenceEquals(body, Get(run.Actor, "Body")) && Main.tile[RoomPartition - 1, roofRow].nactive(),
                        "the same body rejects a real interrupted jump, walks away without relaunching at the blocked entry, ascends elsewhere and drops with the roof still present");
                }
                Console.WriteLine("PASS native moving owner near an interior partition.");
            }
        }

        private static void MovingStarboardRoom(object context, object host, object profiles, Player human)
        {
            Item savedAccessory = human.armor[3];
            try
            {
                using (var run = new NativeFollowWorld(context, host, profiles, human, "moving-room-wings",
                    new Vector2(RoomX(49), RoomUpper * 16), new Vector2(RoomX(54), RoomUpper * 16), () =>
                    {
                        BuildMovingRoom();
                        PlaceFixtureStrip(47, 51, RoomUpper - 4, TileID.Stone);
                    }))
                {
                    human.armor[3] = new Item(Starboard);
                    run.Body.armor[3] = new Item(Starboard);
                    var driver = new RoomOwnerRoute(starboard: true);
                    var owner = new WingRoomProgress(human);
                    var follower = new WingRoomProgress(run.Body);
                    var samples = new List<long>(RoomDeadline);
                    var work = new RoomWork(Navigator(run.Actor));
                    for (int tick = 0; tick < RoomDeadline; tick++)
                    {
                        var buttons = driver.Read(human, tick);
                        samples.Add(run.Step(buttons.Axis, buttons.Jump, buttons.Down));
                        owner.Record(tick);
                        follower.Record(tick);
                        work.Read(driver.Phase);
                        if (tick % 150 == 0)
                            Console.WriteLine($"MOVING STARBOARD sample={tick} phase={driver.Phase} owner={human.Bottom}/{human.velocity} actor={run.Body.Bottom}/{run.Body.velocity} status={Get(run.Actor, "Status")}");
                    }
                    Console.WriteLine($"MOVING STARBOARD phaseChanges={driver.Transitions} owner[{owner}] ai[{follower}] work[{work}] active {NativeStepTiming(samples)}");
                    Require(driver.Transitions >= 14 && owner.LongestLevel >= 8 && owner.Reversals >= 1 &&
                        owner.PlatformCrossing >= 0 && owner.AirborneConsumption > 20 && owner.AirborneRefill == 0,
                        "the independent winged owner uses native level flight, changes direction and descends through the same room platform");
                    Require(follower.FirstFuelUse >= 0 && follower.AirborneConsumption > 20 && follower.AirborneRefill == 0 &&
                        follower.LandRefill > follower.FirstFuelUse && follower.PlatformCrossing >= 0 &&
                        follower.WingAfterCrossing > follower.PlatformCrossing && follower.LongestLevel >= 8 &&
                        follower.Oscillations <= 6 && follower.DoorPasses >= 2 && follower.Discontinuities == 0 && run.NativeRelocations == 0 &&
                        (follower.CeilingHit < 0 || follower.CeilingExit > follower.CeilingHit) && !run.Body.dead,
                        "the original Starboard body flies steadily, leaves ceiling contact if reached, passes through the platform, recovers its own wings on landing and continues around the room");
                    if (work.Available) work.AssertComplete(RoomDeadline);
                    Console.WriteLine("PASS native moving Starboard room.");
                }
            }
            finally { human.armor[3] = savedAccessory; }
        }

        private static void MovingStarboardCeiling(object context, object host, object profiles, Player human)
        {
            const int roofBottom = (RoomUpper - 3) * 16;
            Item savedAccessory = human.armor[3];
            try
            {
                using (var run = new NativeFollowWorld(context, host, profiles, human, "starboard-roof",
                    new Vector2(RoomX(49), RoomUpper * 16), new Vector2(RoomX(57), RoomUpper * 16), () =>
                    {
                        BuildMovingRoom();
                        PlaceFixtureStrip(47, 51, RoomUpper - 4, TileID.Stone);
                    }))
                {
                    human.armor[3] = new Item(Starboard);
                    Player body = run.Body;
                    body.armor[3] = new Item(Starboard);
                    // A legal incoming jump posture is part of this variant's
                    // initial state. After step zero, only original movement
                    // decides contact, braking, retreat, and wing reserve.
                    body.Bottom = new Vector2(RoomX(49), roofBottom + body.height + 2);
                    body.velocity = new Vector2(0, -3.5f);
                    var driver = new RoomOwnerRoute(starboard: true);
                    var owner = new WingRoomProgress(human);
                    var follower = new WingRoomProgress(body);
                    int followingOutsideRoof = -1, activeOwner = 0;
                    for (int tick = 0; tick < 650; tick++)
                    {
                        var ownerBefore = human.Bottom;
                        var input = driver.Read(human, tick);
                        run.Step(input.Axis, input.Jump, input.Down);
                        owner.Record(tick);
                        follower.Record(tick);
                        if (Vector2.DistanceSquared(human.Bottom, ownerBefore) > .25f)
                        {
                            activeOwner++;
                            if (followingOutsideRoof < 0 && follower.CeilingExit >= 0 && body.Left.X >= 52 * 16 &&
                                Vector2.DistanceSquared(body.Bottom, human.Bottom) < 96 * 96)
                                followingOutsideRoof = tick;
                        }
                    }
                    Console.WriteLine($"MOVING STARBOARD ROOF ownerActive={activeOwner} owner[{owner}] ai[{follower}] " +
                        $"followOutside={followingOutsideRoof} {RoomTrace(run, human, Navigator(run.Actor), 650, driver.Phase)}");
                    Require(driver.Transitions >= 10 && activeOwner >= 500 && owner.LongestLevel >= 8 && owner.PlatformCrossing >= 0,
                        "the roof variant's owner starts moving independently, then flies and changes regions under native controls");
                    Require(follower.CeilingHit >= 0 && follower.CeilingExit > follower.CeilingHit &&
                        followingOutsideRoof > follower.CeilingExit && follower.Discontinuities == 0 && run.NativeRelocations == 0 && !body.dead,
                        "an actual near-roof collision releases the same body into a viable route and resumes follow while the owner still flies and walks");
                    Console.WriteLine("PASS native moving Starboard roof contact.");
                }
            }
            finally { human.armor[3] = savedAccessory; }
        }

        private static float RoomX(int tile) => tile * 16 + 8;

        // Timing is injected only by the isolated ordinary-Release probe. A
        // sample is one world's step and includes only actual method invocations.
        private sealed class RoomPhaseTiming : IDisposable
        {
            private static RoomPhaseTiming current;
            private readonly Harmony harmony = new Harmony("JueMingR.Tests.CompanionRoomPhaseTiming");
            private readonly object first, second;
            private readonly List<long> firstSamples = new List<long>(RoomDeadline), secondSamples = new List<long>(RoomDeadline),
                teamSearchSamples = new List<long>(RoomDeadline);
            private long firstTime, secondTime, teamSearchTime;

            internal RoomPhaseTiming(object first, object second)
            {
                this.first = first;
                this.second = second;
                Type type = first.GetType();
                var enter = new HarmonyMethod(typeof(RoomPhaseTiming), nameof(Enter));
                try
                {
                    harmony.Patch(AccessTools.Method(type, "Decide"), enter, new HarmonyMethod(typeof(RoomPhaseTiming), nameof(Control)));
                    harmony.Patch(AccessTools.Method(type, "Observe"), enter, new HarmonyMethod(typeof(RoomPhaseTiming), nameof(Control)));
                    harmony.Patch(AccessTools.Method(type, "Advance"), enter, new HarmonyMethod(typeof(RoomPhaseTiming), nameof(Search)));
                    current = this;
                }
                catch { harmony.UnpatchAll(harmony.Id); throw; }
            }

            private static void Enter(out long __state) => __state = Stopwatch.GetTimestamp();

            private static void Control(object __instance, long __state)
            {
                var sample = current;
                if (sample == null) return;
                long elapsed = Stopwatch.GetTimestamp() - __state;
                if (ReferenceEquals(__instance, sample.first)) sample.firstTime += elapsed;
                else if (ReferenceEquals(__instance, sample.second)) sample.secondTime += elapsed;
            }

            private static void Search(object __instance, long __state)
            {
                var sample = current;
                if (sample != null && (ReferenceEquals(__instance, sample.first) || ReferenceEquals(__instance, sample.second)))
                    sample.teamSearchTime += Stopwatch.GetTimestamp() - __state;
            }

            internal void Capture(bool active)
            {
                if (active)
                {
                    firstSamples.Add(firstTime);
                    if (second != null) secondSamples.Add(secondTime);
                    teamSearchSamples.Add(teamSearchTime);
                }
                firstTime = secondTime = teamSearchTime = 0;
            }

            internal void Report(string variant)
            {
                Require(firstSamples.Exists(x => x > 0) && teamSearchSamples.Exists(x => x > 0) &&
                    (second == null || secondSamples.Exists(x => x > 0)), "the isolated timing hooks observe both actual control and budgeted navigation");
                Console.WriteLine($"MOVING ROOM phase timings variant={variant} samples={firstSamples.Count} " +
                    $"firstObservedControl[{NativeStepTiming(firstSamples)}] " +
                    $"secondObservedControl[{(second == null ? "—" : NativeStepTiming(secondSamples))}] " +
                    $"teamAdvance[{NativeStepTiming(teamSearchSamples)}]");
            }

            public void Dispose()
            {
                current = null;
                harmony.UnpatchAll(harmony.Id);
            }
        }

        private sealed class RoomWork
        {
            private readonly object nav;
            private readonly int startDelivered, startObserved, startFacility;
            private int lastTiles, lastExpansions, lastPredictions, lastShapes, lastSlices, lastPhase = -1, quietService;
            private readonly bool hasShapeCounter;
            private bool pending;
            internal readonly bool Available;
            internal int StepTiles, StepExpansions, StepPredictions, StepShapes, PhaseChangesWhilePending, MaxServiceGap;
            internal uint MaxPendingAge;

            internal RoomWork(object nav)
            {
                this.nav = nav;
                Available = nav != null && nav.GetType().GetProperty("PlannerSlices", ReflectionMembers) != null;
                if (!Available) return;
                startDelivered = Count("MovementDeliveries");
                startObserved = Count("MovementObservations");
                startFacility = Count("FacilityUses");
                lastTiles = Count("TileWork");
                lastExpansions = Count("ExpansionWork");
                lastPredictions = Count("PredictionWork");
                lastSlices = Count("PlannerSlices");
                hasShapeCounter = nav.GetType().GetProperty("ShapeReadWork", ReflectionMembers) != null;
                if (hasShapeCounter) lastShapes = Count("ShapeReadWork");
            }

            private int Count(string name) => (int)Get(nav, name);

            internal void Read(int phase, bool canPlan = true)
            {
                if (!Available) return;
                int tiles = Count("TileWork"), expansions = Count("ExpansionWork"), predictions = Count("PredictionWork");
                StepTiles = tiles - lastTiles;
                StepExpansions = expansions - lastExpansions;
                StepPredictions = predictions - lastPredictions;
                lastTiles = tiles;
                lastExpansions = expansions;
                lastPredictions = predictions;
                if (hasShapeCounter)
                {
                    int shapes = Count("ShapeReadWork");
                    StepShapes = shapes - lastShapes;
                    lastShapes = shapes;
                }
                int slices = Count("PlannerSlices");
                quietService = canPlan && slices == lastSlices ? quietService + 1 : 0;
                lastSlices = slices;
                MaxServiceGap = Math.Max(quietService, MaxServiceGap);
                if (lastPhase >= 0 && lastPhase != phase && pending) PhaseChangesWhilePending++;
                lastPhase = phase;
                pending = Get(nav, "QueryStatus").ToString() == "PendingBudget";
                MaxPendingAge = Math.Max(MaxPendingAge, (uint)Get(nav, "QueryPendingAge"));
            }

            internal void AssertComplete(int steps)
            {
                Require(Available && Count("MovementDeliveries") - startDelivered == steps &&
                    Count("MovementObservations") - startObserved == steps && Count("FacilityUses") == startFacility &&
                    lastSlices > 0 && lastTiles > 0,
                    "each native world step sends and observes one control for the same body with real bounded plan work and zero relocation facility use");
            }

            public override string ToString() => Available
                ? $"slices={lastSlices} maxServiceGap={MaxServiceGap} tiles={lastTiles} expansions={lastExpansions} predictions={lastPredictions} " +
                    $"shapeReads={GetOptional(nav, "ShapeReadWork")} candidates={GetOptional(nav, "CandidateWork")} aStarExpansions={GetOptional(nav, "AStarWork")} " +
                    $"immediateContactReads={GetOptional(nav, "ImmediateContactReads")} immediateContactPredictions={GetOptional(nav, "ImmediateContactPredictions")} " +
                    $"immediateBrakingTicks={GetOptional(nav, "ImmediateBrakingTicks")} " +
                    $"deliveries={Count("MovementDeliveries") - startDelivered} observations={Count("MovementObservations") - startObserved} " +
                    $"facilities={Count("FacilityUses") - startFacility} generation={Count("QueryGeneration")} pendingAge={MaxPendingAge} pendingChanges={PhaseChangesWhilePending}"
                : "duration counters unavailable on this host";
        }

        private static bool SameOpenRoomBand(Player first, Player second)
        {
            int floor = RoomProgress.RoomFloor(first);
            if (floor == 0 || RoomProgress.RoomFloor(second) != floor) return false;
            if (floor == RoomUpper) return true;
            return (first.Center.X < RoomX(RoomPartition)) == (second.Center.X < RoomX(RoomPartition)) &&
                (first.Center.X < RoomX(RoomDoor)) == (second.Center.X < RoomX(RoomDoor));
        }

        private readonly struct OwnerButtons
        {
            internal readonly int Axis;
            internal readonly bool Jump, Down;
            internal OwnerButtons(int axis, bool jump = false, bool down = false)
            {
                Axis = axis;
                Jump = jump;
                Down = down;
            }
        }

        private sealed class RoomOwnerRoute
        {
            private readonly bool starboard;
            private int phaseStart;
            internal int Phase { get; private set; }
            internal int Transitions { get; private set; }

            internal RoomOwnerRoute(bool starboard = false) { this.starboard = starboard; }

            // This script receives only the owner's native state. The AI starts
            // with no earlier lap to consult and cannot affect these timings.
            internal OwnerButtons Read(Player owner, int tick)
            {
                float x = owner.Center.X;
                bool upper = OnFloor(owner, RoomUpper);
                bool lower = OnFloor(owner, RoomLower);
                string reason = null;
                switch (Phase)
                {
                    case 0: if (starboard ? x >= RoomX(59) : x >= RoomX(72)) reason = starboard ? "upper takeoff approach" : "right drop approach"; break;
                    case 1: if (lower) reason = "right lower floor"; break;
                    case 2: if (x >= RoomX(starboard ? 92 : 94)) reason = "exterior turn"; break;
                    case 3: if (x <= RoomX(74) && (!starboard || lower)) reason = "interior right jump approach"; break;
                    case 4: if (upper) reason = "right upper platform"; break;
                    case 5: if (x <= RoomX(54)) reason = "left drop approach"; break;
                    case 6: if (lower) reason = "left lower floor"; break;
                    case 7: if (x <= RoomX(47)) reason = "lower left turn"; break;
                    case 8: if (x >= RoomX(53)) reason = "left jump approach"; break;
                    case 9: if (upper) reason = "left upper platform"; break;
                    case 10: if (owner.Bottom.Y < RoomUpper * 16 - 55 && owner.wingTime > 0) reason = "airborne above upper platform"; break;
                    case 11: if (x >= RoomX(70)) reason = "right flight turn"; break;
                    case 12: if (owner.velocity.X <= -2 && x <= RoomX(64)) reason = "native flight reversed"; break;
                    case 13: if (x >= RoomX(68)) reason = "brake over the right platform"; break;
                    case 14: if (x >= RoomX(68) && x < RoomX(74) && Math.Abs(owner.velocity.X) <= 2)
                            reason = "right approach to platform pass-through"; break;
                }
                if (reason != null)
                {
                    Transitions++;
                    if (starboard && Phase == 0) Phase = 10;
                    else if (Phase == 14) Phase = 1;
                    else if (Phase == 9) Phase = 0;
                    else Phase++;
                    phaseStart = tick;
                    Console.WriteLine($"MOVING ROOM owner tick={tick} next={Phase} why={reason} feet={owner.Bottom}");
                }
                switch (Phase)
                {
                    case 1: return new OwnerButtons(starboard ? NativeAxisTo(owner, RoomX(73)) : 1, down: true);
                    case 3: return new OwnerButtons(-1);
                    case 4: return new OwnerButtons(-1, jump: !starboard || tick - phaseStart < 12);
                    case 5: return new OwnerButtons(-1);
                    case 6: return new OwnerButtons(-1, down: true);
                    case 7: return new OwnerButtons(-1);
                    case 9: return new OwnerButtons(1, jump: !starboard || tick - phaseStart < 12);
                    case 10: return new OwnerButtons(0, jump: true);
                    case 11: return new OwnerButtons(1, jump: true, down: true);
                    case 12: return new OwnerButtons(-1, jump: true, down: true);
                    case 13: return new OwnerButtons(1, jump: true, down: true);
                    case 14: return new OwnerButtons(NativeAxisTo(owner, RoomX(69)), jump: true, down: true);
                    default: return new OwnerButtons(1);
                }
            }
        }

        private sealed class WingRoomProgress
        {
            private readonly Player body;
            private readonly RoomProgress room;
            private Vector2 previous;
            private float fuel, previousVertical;
            private int level, flightDirection, previousTurn = -100;
            internal int CeilingHit = -1, CeilingExit = -1, PlatformCrossing = -1;
            internal int FirstFuelUse = -1, WingAfterCrossing = -1, LandRefill = -1;
            internal int AirborneConsumption, AirborneRefill, Oscillations, Reversals, LongestLevel, Discontinuities;
            internal int DoorPasses => room.DoorCrossings;

            internal WingRoomProgress(Player body)
            {
                this.body = body;
                room = new RoomProgress(body);
                previous = body.Bottom;
                fuel = body.wingTime;
                previousVertical = body.velocity.Y;
            }

            internal void Record(int tick)
            {
                Vector2 movement = room.Record(body, tick);
                if (movement.LengthSquared() > 24 * 24) Discontinuities++;
                bool supported = OnNativeRoomSupport();
                bool used = body.wingsLogic == 45 && !body.mount.Active && !body.empressBrooch && body.wingTime < fuel && !supported;
                if (used)
                {
                    AirborneConsumption++;
                    if (FirstFuelUse < 0) FirstFuelUse = tick;
                    if (PlatformCrossing >= 0 && WingAfterCrossing < 0) WingAfterCrossing = tick;
                }
                if (body.wingTime > fuel && tick > 2)
                {
                    if (!supported)
                    {
                        AirborneRefill++;
                        Console.WriteLine($"MOVING STARBOARD unsupported refill tick={tick} owner={ReferenceEquals(body, Main.LocalPlayer)} feet={body.Bottom}/{body.velocity} fuel={fuel}->{body.wingTime}");
                    }
                    else if (FirstFuelUse >= 0 && LandRefill < 0) LandRefill = tick;
                }
                fuel = body.wingTime;
                level = used && Math.Abs(movement.Y) <= .65f && Math.Abs(movement.X) >= 2 ? level + 1 : 0;
                LongestLevel = Math.Max(LongestLevel, level);
                if (used && Math.Abs(movement.X) >= 2)
                {
                    int direction = Math.Sign(movement.X);
                    if (flightDirection != 0 && flightDirection != direction)
                    {
                        Reversals++;
                        if (tick - previousTurn <= 10) Oscillations++;
                        previousTurn = tick;
                    }
                    flightDirection = direction;
                }
                else if (supported) flightDirection = 0;
                bool belowRoof = body.Center.X >= RoomX(47) - 8 && body.Center.X <= RoomX(51) + 8;
                if (CeilingHit < 0 && belowRoof && previousVertical < -.3f &&
                    Math.Abs(body.Top.Y - (RoomUpper - 3) * 16) <= 1 && movement.Y <= 0 && body.velocity.Y >= -.02f)
                    CeilingHit = tick;
                if (CeilingHit >= 0 && CeilingExit < 0 && (!belowRoof ||
                    (body.Top.Y > (RoomUpper - 3) * 16 + 10 && movement.Y > .5f && !body.controlJump)))
                    CeilingExit = tick;
                if (PlatformCrossing < 0 && body.Center.X > RoomX(48) && body.Center.X < RoomX(76) &&
                    previous.Y <= RoomUpper * 16 + 1 && body.Bottom.Y > RoomUpper * 16 + 1)
                    PlatformCrossing = tick;
                previous = body.Bottom;
                previousVertical = body.velocity.Y;
            }

            private bool OnNativeRoomSupport()
            {
                if (body.velocity.Y < -.02f) return false;
                float feet = body.Bottom.Y;
                int column = (int)(body.Bottom.X / 16);
                int row = (int)((feet + 1) / 16);
                if (column < 0 || column >= Width || row < 0 || row >= Height) return false;
                Tile tile = Main.tile[column, row];
                if (tile == null || !tile.nactive() || !Main.tileSolid[tile.type] && !Main.tileSolidTop[tile.type]) return false;
                return Math.Abs(feet - (row * 16 + (tile.halfBrick() ? 8 : 0))) < 2;
            }

            public override string ToString() => $"room={room} nativeWing={body.wingsLogic}/{body.wingTimeMax} " +
                $"ceiling={CeilingHit}/{CeilingExit} drop={PlatformCrossing} fuelFirst={FirstFuelUse} fuelAfterDrop={WingAfterCrossing} " +
                $"spentTicks={AirborneConsumption} airborneRefills={AirborneRefill} landedRefill={LandRefill} " +
                $"level={LongestLevel} reversals={Reversals} rapidReversals={Oscillations} discontinuities={Discontinuities}";
        }

        private sealed class RoomProgress
        {
            private Vector2 last;
            private int floor;
            private int doorSide;
            private int quietTicks;
            private int lastMotion;
            private int longestQuiet;
            private bool heldDoorway;
            private readonly List<string> crossings = new List<string>();
            internal int FirstMovement { get; private set; } = -1;
            internal int Descents { get; private set; }
            internal int Ascents { get; private set; }
            internal int DoorCrossings { get; private set; }

            internal RoomProgress(Player body)
            {
                last = body.Bottom;
                floor = RoomFloor(body);
                doorSide = DoorSide(body);
            }

            internal Vector2 Record(Player body, int tick)
            {
                Vector2 delta = body.Bottom - last;
                last = body.Bottom;
                if (delta.LengthSquared() > .25f)
                {
                    if (FirstMovement < 0) FirstMovement = tick;
                    lastMotion = tick;
                    quietTicks = 0;
                }
                else longestQuiet = Math.Max(longestQuiet, ++quietTicks);
                int contact = RoomFloor(body);
                if (contact != 0 && contact != floor)
                {
                    if (floor == RoomUpper) Descents++;
                    if (floor == RoomLower) Ascents++;
                    floor = contact;
                    crossings.Add($"{tick}:floor{floor}@{(int)(body.Center.X / 16)}");
                }
                int side = DoorSide(body);
                if (side == 0 && body.Bottom.Y >= RoomLower * 16 - 1 &&
                    body.Right.X > RoomDoor * 16 && body.Left.X < (RoomDoor + 1) * 16 &&
                    Main.tile[RoomDoor, RoomLower - 2].type == TileID.OpenDoor)
                    heldDoorway = true;
                if (side != 0)
                {
                    if (heldDoorway && doorSide != 0 && side != doorSide)
                    {
                        DoorCrossings++;
                        crossings.Add($"{tick}:door{side}");
                    }
                    doorSide = side;
                    heldDoorway = false;
                }
                return delta;
            }

            internal static int RoomFloor(Player body) =>
                OnFloor(body, RoomUpper) && body.Center.X < RoomX(78) ? RoomUpper :
                OnFloor(body, RoomLower) || (body.velocity.Y == 0 && Math.Abs(body.Bottom.Y - (RoomLower * 16 + 8)) < .25f) ? RoomLower : 0;

            private static int DoorSide(Player body)
            {
                if (body.Bottom.Y < RoomLower * 16 - 1) return 0;
                if (body.Left.X >= (RoomDoor + 1) * 16) return 1;
                return body.Right.X <= RoomDoor * 16 ? -1 : 0;
            }

            public override string ToString() => $"first={FirstMovement} last={lastMotion} quiet={longestQuiet} " +
                $"down={Descents} up={Ascents} door={DoorCrossings} [{string.Join(",", crossings)}]";
        }
    }
}
