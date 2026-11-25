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

        private static void NativeOwnerWarpReturn(object context, object host, object profiles, Player human, bool attacking = false)
        {
            float floor = RoomLower * 16;
            float innerWall = RoomPartition * 16;
            Vector2 left = new Vector2(innerWall - 10.5f, floor);
            Vector2 right = new Vector2(innerWall + 26.5f, floor);
            using (var run = new NativeFollowWorld(context, host, profiles, human, attacking ? "warp-shots" : "warp-return", left, left, BuildMovingRoom))
            {
                run.JoinPeer(attacking ? "warp-shots-peer" : "warp-return-peer", left - new Vector2(20f, 0f));
                var first = Navigator(run.Actor);
                var peer = Navigator(run.PeerActor);
                for (int tick = 0; tick < 10; tick++) run.Step();
                var trail = Get(host, "ownerTrail");
                int initialSegment = (int)Get(trail, "Segment");
                WarpOwnerTo(human, new Vector2(RoomX(86), RoomUpper * 16 - 64));
                for (int tick = 0; tick < 2; tick++) run.Step();
                int awaySegment = (int)Get(trail, "Segment");
                WarpOwnerTo(human, right);
                run.Step(1);
                var published = (Vector2)Get(first, "Target");
                var peerPublished = (Vector2)Get(peer, "Target");
                bool air = (bool)Get(first, "targetAirborne"), peerAir = (bool)Get(peer, "targetAirborne");
                object firstQuery = GetOptional(Get(first, "route"), "query");
                string returnMedium = firstQuery == null ? null : Get(Get(firstQuery, "Goal"), "Medium").ToString();
                Console.WriteLine($"FOLLOW WARP return frame={Main.GameUpdateCount} segments={initialSegment}/{awaySegment}/{Get(trail, "Segment")} " +
                    $"owner={human.Bottom}/{human.velocity} support={OnFloor(human, RoomLower)} first={published}/{air} peer={peerPublished}/{peerAir} " +
                    $"{PlanStatus(first)} {RouteTrace(first)} medium={returnMedium} peer={PlanStatus(peer)} {RouteTrace(peer)}");

                int direction = 1, moved = 0, crossed = -1, peerCrossed = -1, firstInputs = 0, peerInputs = 0;
                int firstShotInMotion = -1, peerShotInMotion = -1;
                const int enemySlot = 17;
                NPC savedEnemy = Main.npc[enemySlot];
                if (attacking)
                {
                    var enemy = new NPC();
                    enemy.SetDefaults(NPCID.Zombie);
                    enemy.whoAmI = enemySlot;
                    enemy.active = enemy.noGravity = enemy.noTileCollide = true;
                    enemy.life = enemy.lifeMax = 100000;
                    enemy.damage = 0;
                    enemy.aiStyle = -1;
                    enemy.Center = new Vector2(RoomX(74), floor - 24);
                    Main.npc[enemySlot] = enemy;
                    EquipWarpAttack(host, run.Actor, run.Body);
                    EquipWarpAttack(host, run.PeerActor, run.Peer);
                }
                try
                {
                    for (int tick = 0; tick < 570; tick++)
                    {
                        if (direction < 0 && human.Center.X <= right.X || direction > 0 && human.Center.X >= RoomX(74))
                            direction = -direction;
                        Vector2 before = human.Bottom, firstBefore = run.Body.Bottom, peerBefore = run.Peer.Bottom;
                        int firstAmmo = run.Body.inventory[54].stack, peerAmmo = run.Peer.inventory[54].stack;
                        run.Step(direction);
                        if (Vector2.DistanceSquared(before, human.Bottom) > .25f) moved++;
                        if (HasMovementInput(run.Body)) firstInputs++;
                        if (HasMovementInput(run.Peer)) peerInputs++;
                        if (crossed < 0 && run.Body.Left.X >= innerWall + 16 && OnFloor(run.Body, RoomLower)) crossed = tick;
                        if (peerCrossed < 0 && run.Peer.Left.X >= innerWall + 16 && OnFloor(run.Peer, RoomLower)) peerCrossed = tick;
                        if (firstShotInMotion < 0 && HasMovementInput(run.Body) && Vector2.DistanceSquared(run.Body.Bottom, firstBefore) > .04f &&
                            firstAmmo > run.Body.inventory[54].stack) firstShotInMotion = tick;
                        if (peerShotInMotion < 0 && HasMovementInput(run.Peer) && Vector2.DistanceSquared(run.Peer.Bottom, peerBefore) > .04f &&
                            peerAmmo > run.Peer.inventory[54].stack) peerShotInMotion = tick;
                        if (crossed >= 0 && peerCrossed >= 0 && (!attacking || firstShotInMotion >= 0 && peerShotInMotion >= 0)) break;
                    }
                }
                finally
                {
                    Main.npc[enemySlot] = savedEnemy;
                    if (attacking)
                        foreach (var projectile in Main.projectile)
                            if (projectile.active && (projectile.owner == run.Body.whoAmI || projectile.owner == run.Peer.whoAmI) &&
                                projectile.type == ProjectileID.Bullet) projectile.Kill();
                }
                Console.WriteLine($"FOLLOW WARP native pursuit attack={attacking} moved={moved} first={crossed}/{firstInputs}/{GetOptional(first, "PlannerSlices")} " +
                    $"peer={peerCrossed}/{peerInputs}/{GetOptional(peer, "PlannerSlices")} simultaneousAmmoUse={firstShotInMotion}/{peerShotInMotion} " +
                    $"body={run.Body.Bottom} peerBody={run.Peer.Bottom}");
                Require(awaySegment > initialSegment && (int)Get(trail, "Segment") > awaySegment &&
                    !air && !peerAir && Math.Abs(published.Y - floor) < 1 && Math.Abs(peerPublished.Y - floor) < 1 &&
                    (returnMedium == null || returnMedium == "Ground"),
                    "the first two native bodies after the actual warp return publish the new owner's solid contact in that update");
                Require(crossed >= 0 && peerCrossed >= 0 && moved > 30 && firstInputs > 10 && peerInputs > 10 &&
                    run.NativeRelocations == 0 && !run.Body.dead && !run.Peer.dead,
                    "both ordinary bodies use original input to route around the solid partition while the returned owner keeps moving");
                Require(attacking ? firstShotInMotion >= 0 && peerShotInMotion >= 0 : firstShotInMotion < 0 && peerShotInMotion < 0,
                    "each actor spends its own real ammunition during movement only in the enabled attack comparison");
                if (!attacking)
                {
                    uint warpedAt = 0, firstReadAt = 0;
                    int segmentBefore = (int)Get(trail, "Segment");
                    Vector2 publishedBefore = default;
                    run.Step(betweenFollowers: () =>
                    {
                        warpedAt = Main.GameUpdateCount;
                        firstReadAt = (uint)Get(Get(trail, "previous"), "Tick");
                        publishedBefore = (Vector2)Get(first, "Target");
                        WarpOwnerTo(human, left);
                    });
                    var sameUpdatePose = (Vector2)Get(peer, "Target");
                    bool valid = !(bool)Get(peer, "targetAirborne") && sameUpdatePose == human.Bottom;
                    Console.WriteLine($"FOLLOW WARP same-update firstObserved={firstReadAt} warped={warpedAt} after={Main.GameUpdateCount} " +
                        $"segment={segmentBefore}/{Get(trail, "Segment")} firstTarget={publishedBefore} peerTarget={sameUpdatePose} valid={valid}");
                    Require(firstReadAt == warpedAt && warpedAt == Main.GameUpdateCount &&
                        (int)Get(trail, "Segment") > segmentBefore && valid && publishedBefore.X > innerWall,
                        "the second original Player.Update reads the real new support after a warp within the same game update");
                }
            }
        }

        private static bool HasMovementInput(Player body) =>
            body.controlLeft || body.controlRight || body.controlJump || body.controlDown;

        private static void EquipWarpAttack(object host, object actor, Player body)
        {
            const int weaponSlot = 41;
            body.inventory[weaponSlot] = new Item(ItemID.Musket);
            body.inventory[54] = new Item(ItemID.MusketBall) { stack = 999 };
            var profile = (CompanionProfile)Get(actor, "Profile");
            Require((bool)Call(host, "SetBehavior", profile.Id, new CompanionBehavior
            {
                Follow = true, AttackEnabled = true, HealthRecoveryEnabled = false,
                MainWeapons = new[] { new CompanionWeaponPreference(ItemID.Musket, 0, weaponSlot) }
            }), "enable an ordinary ammunition-using weapon through the real companion configuration");
        }

        private static void NativeOwnerLandsWhileQueryPending(object context, object host, object profiles, Player human)
        {
            float floor = RoomLower * 16, wall = RoomPartition * 16;
            Vector2 left = new Vector2(wall - 10.5f, floor);
            using (var run = new NativeFollowWorld(context, host, profiles, human, "warp-contact", left,
                new Vector2(RoomX(69), floor - 20), BuildMovingRoom))
            {
                var navigation = Navigator(run.Actor);
                object oldAir = null;
                int firstAir = -1, landed = -1, firstGround = -1, oldSearchOnGround = -1, sharedAirFrames = 0;
                int initialSegment = -1, contactSegment = -1, oldExpanded = 0;
                float priorX = human.Bottom.X;
                for (int tick = 0; tick < 75; tick++)
                {
                    run.Step(1);
                    object trail = Get(host, "ownerTrail"), route = Get(navigation, "route");
                    int segment = (int)Get(trail, "Segment");
                    if (initialSegment < 0) initialSegment = segment;
                    object query = GetOptional(route, "query");
                    string medium = query == null ? null : Get(Get(query, "Goal"), "Medium").ToString();
                    bool pending = query != null && Get(query, "Status").ToString() == "PendingBudget";
                    if (firstAir < 0 && pending && medium == "Air")
                    {
                        firstAir = tick;
                        oldAir = query;
                    }
                    else if (pending && ReferenceEquals(query, oldAir) && human.Bottom.X > priorX + .01f)
                        sharedAirFrames++;
                    if (pending && ReferenceEquals(query, oldAir)) oldExpanded = (int)Get(oldAir, "expanded");
                    if (landed < 0 && OnFloor(human, RoomLower))
                    {
                        landed = tick;
                        contactSegment = segment;
                    }
                    if (landed >= 0 && firstGround < 0 && pending && ReferenceEquals(query, oldAir) && oldSearchOnGround < 0)
                        oldSearchOnGround = tick;
                    if (firstGround < 0 && medium == "Ground") firstGround = tick;
                    if (tick < 14)
                        Console.WriteLine($"FOLLOW WARP landing frame={Main.GameUpdateCount} tick={tick} segment={segment} " +
                            $"owner={human.Bottom}/{human.velocity} targetAir={Get(navigation, "targetAirborne")} query={medium}/{PlanStatus(navigation)}");
                    priorX = human.Bottom.X;
                }
                int finalOldExpanded = oldAir == null ? -1 : (int)Get(oldAir, "expanded");
                Console.WriteLine($"FOLLOW WARP landing oldAir={firstAir} sameQueryWhileMoving={sharedAirFrames} " +
                    $"nativeGround={landed} firstGround={firstGround} oldAfterContact={oldSearchOnGround} " +
                    $"oldExpansions={oldExpanded}/{finalOldExpanded} segment={initialSegment}/{contactSegment}");
                Require(firstAir >= 0 && sharedAirFrames > 0 && landed > firstAir && initialSegment == contactSegment &&
                    oldSearchOnGround < 0 && firstGround >= firstAir && firstGround <= landed && finalOldExpanded == oldExpanded,
                    "an in-progress Air search survives ordinary coordinates and releases its budget when the native landing invalidates its target");
            }
        }

        private static void NativeOwnerWarpDuringHook(object context, object host, object profiles, Player human)
        {
            NativeOwnerWarpDuringHook(context, host, profiles, human, removeAnchor: false);
            NativeOwnerWarpDuringHook(context, host, profiles, human, removeAnchor: true);
            RetiringActionReplacement(context, host, profiles, human);
        }

        private static bool RetiringIntent(object navigation) =>
            (bool)(GetOptional(navigation, "RetiringIntent") ?? Get(navigation, "retireIntent"));

        private static void NativeOwnerWarpDuringHook(object context, object host, object profiles, Player human, bool removeAnchor)
        {
            const int shaftLeft = 108, shaftRight = 111, anchor = 110, anchorRow = 66, floor = 73;
            var start = new Vector2(anchor * 16, floor * 16);
            var above = new Vector2(anchor * 16, anchorRow * 16);
            using (var run = new NativeFollowWorld(context, host, profiles, human, removeAnchor ? "warp-hook-rejected" : "warp-hook", start, start, () =>
            {
                PlaceFixtureStrip(95, 120, floor, TileID.Stone);
                for (int y = 65; y < (removeAnchor ? floor - 3 : floor); y++)
                {
                    PlaceFixtureTile(shaftLeft, y, TileID.Stone);
                    PlaceFixtureTile(shaftRight, y, TileID.Stone);
                }
                PlaceFixtureTile(anchor, anchorRow, TileID.Stone);
            }))
            {
                var body = run.Body;
                var gear = new Item(ItemID.GrapplingHook);
                bool originalHookAdmission = Main.projHook[gear.shoot];
                var mobility = Get(run.Actor, "Mobility");
                var navigation = Navigator(run.Actor);
                Projectile projectile = null;
                int birth = -1, away = -1, returned = -1, attached = -1, received = -1, released = -1, landed = -1;
                int rejected = -1, firstSafe = -1, retired = -1, resumed = -1, hookGone = -1, movingOwner = 0, motion = -1;
                int birthKey = 0, repeatBirths = 0, movingFollow = 0;
                int sequence = 0;
                long oldAction = 0;
                bool lostReceipt = false, committedAtBirth = false, retiringInFlight = false, resumedOldAction = false;
                bool rejectedDuringRetraction = false;
                float followFrom = run.Body.Bottom.X, followTravel = 0;
                try
                {
                    Main.projHook[gear.shoot] = ContentSamples.ProjectilesByType[gear.shoot].aiStyle == 7;
                    body.miscEquips[4] = gear;
                    for (int tick = 0; tick < 10; tick++) run.Step();
                    WarpOwnerTo(human, above);
                    for (int tick = 0; tick < 280 && landed < 0; tick++)
                    {
                        if (birth >= 0 && away < 0)
                        {
                            WarpOwnerTo(human, new Vector2(RoomX(115), floor * 16));
                            away = tick;
                        }
                        else if (away >= 0 && returned < 0)
                        {
                            WarpOwnerTo(human, removeAnchor ? new Vector2(RoomX(106), floor * 16) : start);
                            returned = tick;
                            if (removeAnchor)
                            {
                                Require(projectile.active && projectile.ai[0] == 0 && Main.tile[anchor, anchorRow].nactive(),
                                    "the original hook is still flying toward the intact anchor before the terrain edit");
                                WorldGen.KillTile(anchor, anchorRow, noItem: true);
                                Require(!Main.tile[anchor, anchorRow].nactive(), "the original tile edit removes the hook anchor before impact");
                            }
                        }
                        Vector2 ownerBefore = human.Bottom, bodyBefore = body.Bottom;
                        if (removeAnchor && returned >= 0)
                        {
                            if (motion < 0 && human.Center.X <= RoomX(98) || motion > 0 && human.Center.X >= RoomX(106))
                                motion = -motion;
                            run.Step(motion);
                            if (Vector2.DistanceSquared(ownerBefore, human.Bottom) > .25f) movingOwner++;
                        }
                        else run.Step();
                        var currentHook = (Projectile)GetOptional(mobility, "Hook");
                        if (birth < 0 && currentHook?.active == true && currentHook.owner == body.whoAmI && currentHook.type == gear.shoot &&
                            Get(mobility, "Result").ToString() == "Started")
                        {
                            birth = tick;
                            sequence = (int)Get(mobility, "Sequence");
                            oldAction = (long)Get(navigation, "ActionId");
                            committedAtBirth = (bool)Get(navigation, "ActionCommitted");
                            projectile = currentHook;
                            birthKey = (int)currentHook.key;
                        }
                        int toolSequence = (int)Get(mobility, "Sequence");
                        string toolState = Get(mobility, "Result").ToString();
                        if (returned >= 0 && received < 0 && rejected < 0 && toolSequence != sequence) lostReceipt = true;
                        if (birth >= 0 && toolSequence == sequence && currentHook?.active == true && (int)currentHook.key != birthKey)
                            repeatBirths++;
                        if (projectile?.active == true)
                        {
                            int previousIndex = Main.ProjectileUpdateLoopIndex;
                            try
                            {
                                Main.ProjectileUpdateLoopIndex = projectile.whoAmI;
                                projectile.Update(projectile.whoAmI);
                            }
                            finally { Main.ProjectileUpdateLoopIndex = previousIndex; }
                            if (attached < 0 && projectile.ai[0] == 2) attached = tick;
                        }
                        if (returned >= 0 && received < 0 && toolSequence == sequence && toolState == "Completed") received = tick;
                        if (removeAnchor && returned >= 0)
                        {
                            if (hookGone < 0 && projectile?.active == false) hookGone = tick;
                            bool retiring = RetiringIntent(navigation);
                            retiringInFlight |= retiring && projectile?.active == true && projectile.ai[0] == 0f &&
                                toolState == "Started" && toolSequence == sequence;
                            if (rejected < 0 && toolSequence == sequence && toolState == "Rejected")
                            {
                                rejected = tick;
                                rejectedDuringRetraction = projectile?.active == true && projectile.ai[0] == 1f;
                            }
                            var observed = Get(mobility, "Observed");
                            if (rejected >= 0 && firstSafe < 0 && (bool)Get(navigation, "Supported") &&
                                !(bool)Get(observed, "GrappleAttached") && !(bool)Get(observed, "GrappleInFlight"))
                                firstSafe = tick;
                            long activeAction = (long)Get(navigation, "ActionId");
                            if (firstSafe >= 0 && retired < 0 && !retiring &&
                                (activeAction != oldAction || Get(navigation, "ActionOutcome").ToString() == "Superseded"))
                                retired = tick;
                            bool movingLeft = body.controlLeft && bodyBefore.X - body.Bottom.X > .04f;
                            if (retired >= 0 && resumed < 0 && activeAction != 0 && activeAction != oldAction && movingLeft)
                            {
                                resumed = tick;
                                followFrom = bodyBefore.X;
                            }
                            if (resumed >= 0)
                            {
                                resumedOldAction |= activeAction == oldAction || retiring;
                                if (movingLeft) movingFollow++;
                                followTravel = Math.Max(followTravel, followFrom - body.Bottom.X);
                            }
                            if (hookGone >= 0 && movingOwner >= 30 && movingFollow >= 8 && followTravel >= 8) landed = tick;
                        }
                        if (received >= 0 && released < 0 && body.controlJump && projectile?.active == false) released = tick;
                        if (released >= 0 && OnFloor(body, floor)) landed = tick;
                        if (tick % 25 == 0 || tick == birth || tick == returned || tick == received || tick == released || tick == landed ||
                            tick == rejected || tick == firstSafe || tick == retired || tick == resumed || tick == hookGone)
                            Console.WriteLine($"FOLLOW WARP hook removeAnchor={removeAnchor} frame={Main.GameUpdateCount} step={tick} " +
                                $"owner={human.Bottom}/{human.velocity} body={body.Bottom}/{body.velocity} supported={Get(navigation, "Supported")} " +
                                $"hook={projectile?.active}/{projectile?.ai[0]} command={Get(mobility, "Action")}/{Get(mobility, "Result")}/" +
                                $"{toolSequence} committed={Get(navigation, "ActionCommitted")} retire={RetiringIntent(navigation)} " +
                                $"left={body.controlLeft} jump={body.controlJump} {PlanStatus(navigation)}");
                    }
                }
                finally
                {
                    body.RemoveAllGrapplingHooks();
                    Main.projHook[gear.shoot] = originalHookAdmission;
                }
                if (removeAnchor)
                {
                    Console.WriteLine($"FOLLOW WARP rejected hook old={oldAction}/{sequence} birth={birth} away={away} return={returned} " +
                        $"rejected={rejected} supported={firstSafe} gone={hookGone} retired={retired} resumed={resumed} " +
                        $"ownerMoving={movingOwner} followerMoving={movingFollow}/{followTravel:F2}px sameSequenceBirths={repeatBirths} " +
                        $"oldActionReturned={resumedOldAction} lostReceipt={lostReceipt}");
                    Require(oldAction > 0 && committedAtBirth && retiringInFlight && away > birth && returned > away &&
                        attached < 0 && received < 0 && rejected > returned && rejectedDuringRetraction &&
                        firstSafe >= rejected && retired == firstSafe && resumed > retired && resumed - retired <= 3 &&
                        hookGone > rejected && movingOwner >= 30 && movingFollow >= 8 && followTravel >= 8 &&
                        !resumedOldAction && repeatBirths == 0 && !lostReceipt && run.NativeRelocations == 0,
                        "the matching rejected native hook retires on real support and the same body follows a moving owner without re-firing that sequence");
                    return;
                }
                Console.WriteLine($"FOLLOW WARP hook old={oldAction}/{sequence} birth={birth} away={away} return={returned} " +
                    $"attached={attached} matchingReceipt={received} release={released} land={landed} lostReceipt={lostReceipt}");
                Require(oldAction > 0 && away > birth && returned > away && attached >= returned && received >= attached &&
                    released > received && landed > released && !lostReceipt && run.NativeRelocations == 0,
                    "the real in-flight hook settles under its original command and releases by native Jump after the owner's return");
            }
        }

        private static void RetiringActionReplacement(object context, object host, object profiles, Player human)
        {
            float wall = RoomPartition * 16;
            var left = new Vector2(wall - 10.5f, RoomLower * 16);
            var right = new Vector2(wall + 26.5f, RoomLower * 16);
            using (var run = new NativeFollowWorld(context, host, profiles, human, "retiring-action", left, right, BuildMovingRoom))
            {
                run.Step();
                var navigation = Navigator(run.Actor);
                var mobility = Get(run.Actor, "Mobility");
                var action = Get(navigation, "action");
                var assembly = navigation.GetType().Assembly;
                var kind = NavigationType(assembly, "NavigationAction");
                var edge = Make(NavigationType(assembly, "NavigationTransition"), Enum.Parse(kind, "Hook"), left,
                    left - new Vector2(0, 80), default(Point), default(Point), 0, false,
                    Enum.Parse(NavigationType(assembly, "NavigationTraversalDetail"), "None"), Vector2.Zero);

                // The native scenes above exercise the actual hook and warp.
                // Isolate ownership after that receipt lost its commitment:
                // emergency steering can cancel it while no move is ready.
                Call(action, "Begin", 900L, edge, false, left);
                Set(action, "Committed", true);
                Call(navigation, "IntentChanged", mobility);
                Require(RetiringIntent(navigation), "the prior committed action owns the handoff");
                Set(action, "Committed", false);
                Set(navigation, "supported", false);
                Call(navigation, "Decide", run.Body, mobility, right, Vector2.Zero, 34f, true, 0, false, true);
                long cancelledId = (long)Get(navigation, "ActionId");
                bool inheritedAfterCancel = RetiringIntent(navigation);
                Require(OnFloor(run.Body, RoomLower), "the fixture retains the original floor for the next planner opportunity");
                Set(navigation, "supported", true);
                Call(navigation, "Advance", run.Body, mobility, Get(host, "ownerTrail"),
                    Make(NavigationType(assembly, "NavigationBudget"), 2600, 96, 48));
                string newPlan = Get(navigation, "QueryStatus").ToString();

                // A verified rescue uses the same Begin entry to install a
                // fresh ID before normal control. It owns its own lifetime.
                Call(action, "Begin", 901L, edge, false, left);
                Set(action, "Committed", true);
                Call(navigation, "IntentChanged", mobility);
                Call(action, "Begin", 902L, edge, false, left);
                bool inheritedAfterBegin = RetiringIntent(navigation);
                Console.WriteLine($"FOLLOW WARP retirement ownership emergencyAction={cancelledId} oldAfterCancel={inheritedAfterCancel} " +
                    $"nextPlan={newPlan} replacementAction={Get(navigation, "ActionId")} oldAfterBegin={inheritedAfterBegin}");
                Require(cancelledId == 0 && !inheritedAfterCancel && newPlan != "Idle" && (long)Get(navigation, "ActionId") == 902L &&
                    !inheritedAfterBegin, "an abandoned or replaced action cannot keep the planner waiting on its old intent");
            }
        }

        private static void NativeOwnerWarpDuringJump(object context, object host, object profiles, Player human)
        {
            float floor = RoomLower * 16, wall = RoomPartition * 16;
            Vector2 left = new Vector2(wall - 10.5f, floor);
            using (var run = new NativeFollowWorld(context, host, profiles, human, "warp-jump", left,
                new Vector2(wall + 26.5f, floor), BuildMovingRoom))
            {
                var navigation = Navigator(run.Actor);
                Player body = run.Body;
                long oldAction = 0;
                int roof = 0;
                for (int tick = 0; tick < 160 && oldAction == 0; tick++)
                {
                    run.Step(1);
                    if (human.Center.X >= RoomX(74)) break;
                    if (Get(navigation, "ActionKind").ToString() != "Jump" || !body.controlJump || body.velocity.Y >= -2 ||
                        !(bool)Get(navigation, "ActionCommitted")) continue;
                    oldAction = (long)Get(navigation, "ActionId");
                    roof = (int)Math.Floor((body.Top.Y - 2) / 16) - 1;
                    PlaceFixtureStrip(RoomPartition - 2, RoomPartition - 1, roof, TileID.Stone);
                }
                Require(oldAction != 0, "the native jump has committed before the owner leaves");
                WarpOwnerTo(human, new Vector2(RoomX(87), (RoomUpper - 4) * 16));
                run.Step();
                WarpOwnerTo(human, new Vector2(RoomX(60), floor));
                int ceiling = -1, contact = -1, resumed = -1, oldJumpAfterContact = -1;
                string contactOutcome = null;
                for (int tick = 0; tick < 75; tick++)
                {
                    Vector2 before = body.Bottom;
                    float previousVy = body.velocity.Y;
                    run.Step(human.Center.X > RoomX(48) ? -1 : 1);
                    var delivery = GetOptional(navigation, "LastMovementDelivery");
                    long currentAction = (long)Get(navigation, "ActionId");
                    if (ceiling < 0 && previousVy < -.5f && body.velocity.Y >= -.02f &&
                        Math.Abs(body.Top.Y - (roof + 1) * 16) < 1f) ceiling = tick;
                    if (contact >= 0 && currentAction == oldAction && body.controlJump && oldJumpAfterContact < 0)
                        oldJumpAfterContact = tick;
                    if (contact < 0 && (bool)Get(navigation, "Supported") && Math.Abs(body.Bottom.Y - floor) < 1f)
                    {
                        contact = tick;
                        contactOutcome = Get(navigation, "ActionOutcome").ToString();
                    }
                    if (contact >= 0 && currentAction != oldAction && body.controlLeft && before.X - body.Bottom.X > .1f && resumed < 0)
                        resumed = tick;
                    if (tick < 18 || tick == contact || tick == resumed)
                        Console.WriteLine($"FOLLOW WARP jump frame={Main.GameUpdateCount} tick={tick} owner={human.Bottom}/{human.velocity} " +
                            $"body={body.Bottom}/{body.velocity} support={Get(navigation, "Supported")} action={currentAction}/{Get(navigation, "ActionOutcome")}/" +
                            $"{Get(navigation, "ActionCommitted")} retire={RetiringIntent(navigation)} left={body.controlLeft} jump={body.controlJump} " +
                            $"deliver={(delivery == null ? body.controlJump.ToString() : GetOptional(delivery, "ActionId"))}/" +
                            $"{(delivery == null ? Get(navigation, "LastDeferral") : GetOptional(delivery, "Deferral"))} {RouteTrace(navigation)}");
                }
                Console.WriteLine($"FOLLOW WARP jump outcome old={oldAction} ceiling={ceiling} contact={contact}/{contactOutcome} reissued={oldJumpAfterContact} resumed={resumed}");
                Require(ceiling >= 0 && contact > ceiling && contactOutcome == "Superseded" && oldJumpAfterContact < 0 &&
                    resumed > contact && resumed - contact <= 3 && Main.tile[RoomPartition - 1, roof].nactive(),
                    "the real blocked jump keeps its airborne control and gives the new direction the next safe grounded input");
            }
        }

        private static void WarpOwnerTo(Player human, Vector2 feet)
        {
            Vector2 before = human.Bottom;
            human.Teleport(feet - new Vector2(human.width * .5f, human.height), 0);
            Require(Vector2.DistanceSquared(feet, human.Bottom) < 1f && Vector2.DistanceSquared(before, human.Bottom) > 48f * 48f,
                "original Player.Teleport moved the human owner to the intended site");
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
