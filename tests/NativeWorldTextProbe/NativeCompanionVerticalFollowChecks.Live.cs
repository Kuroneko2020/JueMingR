using System;
using System.Collections.Generic;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionVerticalFollowChecks
    {
        private static void NativeOwnerReverses(object context, object host, object profiles, Player human)
        {
            const int floor = 82, ticks = 325, firstStop = 67, firstReverse = 105, secondStop = 184, secondReverse = 205;
            using (var run = new NativeFollowWorld(context, host, profiles, human, "live-owner-reversal",
                new Vector2(RoomX(51), floor * 16), new Vector2(RoomX(58), floor * 16),
                () => PlaceFixtureStrip(42, 108, floor, TileID.Stone)))
            {
                Player body = run.Body;
                Item prior = human.armor[3];
                try
                {
                    // Different native running speeds make the gap change in both directions.
                    body.armor[3] = new Item(ItemID.HermesBoots);
                    human.armor[3] = new Item();
                    var timing = new List<long>(ticks);
                    var navigation = Navigator(run.Actor);
                    int firstRight = -1, firstUsefulLeft = -1, firstLeft = -1, leftMotion = -1;
                    int firstUsefulRight = -1, secondRight = -1, rightMotion = -1;
                    float initialGap = human.Center.X - body.Center.X, closestGap = initialGap, fastestBody = 0f, fastestOwner = 0f;
                    bool slowed = false;
                    for (int tick = 0; tick < ticks; tick++)
                    {
                        // Ordinary .08 native acceleration reaches only 3.8 px/update by 85.
                        // A complete stop includes the remaining approach and native deceleration.
                        int axis = tick < firstStop ? 1 : tick < firstReverse ? 0 : tick < secondStop ? -1 : tick < secondReverse ? 0 : 1;
                        float before = body.Center.X;
                        timing.Add(run.Step(axis));
                        float gap = human.Center.X - body.Center.X;
                        float bodySpeed = body.Center.X - before;
                        fastestBody = Math.Max(fastestBody, Math.Abs(bodySpeed));
                        fastestOwner = Math.Max(fastestOwner, Math.Abs(human.velocity.X));
                        if (firstRight < 0 && body.controlRight) firstRight = tick;
                        if (tick < firstReverse) closestGap = Math.Min(closestGap, Math.Abs(gap));
                        if (tick >= firstStop + 6 && tick < firstReverse && Math.Abs(human.velocity.X) < 1.1f && Math.Abs(gap) < 52f &&
                            Math.Abs(body.velocity.X) < 1.8f) slowed = true;
                        if (tick >= firstStop && tick < firstReverse && (tick - firstStop) % 5 == 0)
                            Console.WriteLine($"FOLLOW LIVE TURN brake tick={tick} gap={gap:F1} velocities={human.velocity.X:F2}/{body.velocity.X:F2} " +
                                $"keys={body.controlLeft}/{body.controlRight} id={(long)Get(navigation, "ActionId")}");
                        if (tick >= firstReverse && tick < secondStop && firstUsefulLeft < 0 && gap <= -48 && human.velocity.X < -1)
                            firstUsefulLeft = tick;
                        if (firstUsefulLeft >= 0 && firstLeft < 0 && body.controlLeft && !body.controlRight) firstLeft = tick;
                        if (firstLeft >= 0 && leftMotion < 0 && bodySpeed < -.5f) leftMotion = tick;
                        if (tick >= secondReverse && firstUsefulRight < 0 && gap >= 48 && human.velocity.X > 1)
                            firstUsefulRight = tick;
                        if (firstUsefulRight >= 0 && secondRight < 0 && body.controlRight && !body.controlLeft) secondRight = tick;
                        if (secondRight >= 0 && rightMotion < 0 && bodySpeed > .5f) rightMotion = tick;
                    }
                    Console.WriteLine($"FOLLOW LIVE TURN firstRight={firstRight} gap={initialGap:F1}->{closestGap:F1} brake={slowed} " +
                        $"left={firstUsefulLeft}/{firstLeft}/{leftMotion} right={firstUsefulRight}/{secondRight}/{rightMotion} " +
                        $"peakNativeSpeed={fastestBody:F2}/{fastestOwner:F2} {NativeStepTiming(timing)} {RouteTrace(navigation)}");
                    Require(firstRight <= 1 && firstRight >= 0 && initialGap - closestGap >= 55 && slowed &&
                        fastestBody > fastestOwner && run.NativeRelocations == 0 && !body.dead,
                        "on open native ground the faster companion closes a genuine gap then brakes near the slowing owner");
                    Require(firstUsefulLeft >= 0 && firstLeft >= firstUsefulLeft && firstLeft <= firstUsefulLeft + 1 &&
                        leftMotion >= firstLeft && leftMotion <= firstLeft + 13 && firstUsefulRight >= 0 &&
                        secondRight >= firstUsefulRight && secondRight <= firstUsefulRight + 1 && rightMotion >= secondRight &&
                        rightMotion <= secondRight + 13,
                        "both actionable native owner turns reach the next available delivery and subsequent real body displacement");
                }
                finally { human.armor[3] = prior; }
            }
        }

        private static void NativeMovingFall(object context, object host, object profiles, Player human)
        {
            const int upper = 80, lower = 111, ticks = 280;
            using (var run = new NativeFollowWorld(context, host, profiles, human, "live-owner-deep-drop",
                new Vector2(RoomX(56), upper * 16), new Vector2(RoomX(60), upper * 16), () =>
                {
                    PlaceFixtureStrip(43, 100, upper, TileID.Platforms);
                    PlaceFixtureStrip(43, 106, lower, TileID.Stone);
                }))
            {
                Player body = run.Body;
                Item prior = human.armor[3];
                try
                {
                    human.armor[3] = new Item(ItemID.LuckyHorseshoe);
                    const int startingLife = 85;
                    body.statLife = startingLife;
                    body.AddBuff(BuffID.Bleeding, 600);
                    var events = new List<string>();
                    var navigation = Navigator(run.Actor);
                    int direction = 1, ownerDrop = -1, ownerLand = -1, bodyDown = -1, bodyDrop = -1, bodyLand = -1;
                    int hurt = -1, followAfterHurt = -1, closeMoving = 0, longestClose = 0;
                    for (int tick = 0; tick < ticks; tick++)
                    {
                        // This patrol continues on the lower floor based only on the owner's own position.
                        if (human.Center.X >= RoomX(91)) direction = -1;
                        else if (human.Center.X <= RoomX(51)) direction = 1;
                        Vector2 ownerBefore = human.Bottom, before = body.Bottom;
                        run.Step(direction, down: tick >= 22 && ownerLand < 0);
                        if (ownerDrop < 0 && human.Bottom.Y > upper * 16 + 4 && Math.Abs(human.velocity.X) > 1)
                        { ownerDrop = tick; events.Add($"{tick}:owner release {human.Bottom}/{human.velocity}"); }
                        if (ownerDrop >= 0 && ownerLand < 0 && OnFloor(human, lower))
                        { ownerLand = tick; events.Add($"{tick}:owner land {human.Bottom}"); }
                        if (bodyDown < 0 && body.controlDown)
                        { bodyDown = tick; events.Add($"{tick}:body Down {body.Bottom} {RouteStep(run.Actor)}"); }
                        if (ownerDrop >= 0 && bodyDown < 0 && tick % 8 == 0)
                            Console.WriteLine("FOLLOW LIVE DROP pending " + RoomTrace(run, human, navigation, tick, ownerLand < 0 ? 1 : 2));
                        if (bodyDown >= 0 && bodyDrop < 0 && body.Bottom.Y > upper * 16 + 16 && body.velocity.Y > 0)
                        { bodyDrop = tick; events.Add($"{tick}:body released {body.Bottom}/{body.velocity}"); }
                        if (bodyDrop >= 0 && bodyLand < 0 && OnFloor(body, lower))
                        { bodyLand = tick; events.Add($"{tick}:body land {body.Bottom}"); }
                        if (hurt < 0 && body.statLife < startingLife)
                        { hurt = tick; events.Add($"{tick}:native Hurt {startingLife}->{body.statLife}"); }
                        bool moving = Math.Abs(human.Bottom.X - ownerBefore.X) > .75f;
                        if (hurt >= 0 && tick > hurt && followAfterHurt < 0 && moving && Math.Abs(body.Bottom.X - before.X) > 1 &&
                            (body.controlLeft || body.controlRight)) followAfterHurt = tick;
                        closeMoving = hurt >= 0 && moving && OnFloor(body, lower) && Math.Abs(body.Center.X - human.Center.X) < 72 ? closeMoving + 1 : 0;
                        longestClose = Math.Max(longestClose, closeMoving);
                    }
                    Console.WriteLine($"FOLLOW LIVE DROP {string.Join(" | ", events)} resumed={followAfterHurt} sustained={longestClose} " +
                        $"life={body.statLife} nativeTeleports={run.NativeRelocations} end={body.Bottom}/{human.Bottom}");
                    Require(ownerDrop >= 22 && ownerLand > ownerDrop && bodyDown >= ownerDrop - 6 && bodyDown < ownerLand &&
                        bodyDrop >= bodyDown && bodyLand > bodyDrop && hurt >= bodyLand && body.statLife == startingLife - 60 &&
                        followAfterHurt > hurt && longestClose >= 16 && !body.dead && run.NativeRelocations == 0,
                        "a horizontally moving airborne owner cues real release, native nonlethal Hurt, and sustained lower-floor follow by the original body");
                }
                finally { human.armor[3] = prior; }
            }
        }

        private static void NativeAirDashSelection(object context, object host, object profiles, Player human)
        {
            const int floor = 97, shieldSlot = 4, packSlot = 45, wall = 61, ticks = 325;
            using (var run = new NativeFollowWorld(context, host, profiles, human, "air-dash-live",
                new Vector2(RoomX(58), floor * 16), new Vector2(RoomX(82), floor * 16), () =>
                {
                    PlaceFixtureStrip(44, 130, floor, TileID.Stone);
                    for (int y = floor - 5; y < floor; y++) PlaceFixtureTile(wall, y, TileID.Stone);
                }))
            {
                Item previous = human.armor[3];
                Player body = run.Body;
                object navigation = Navigator(run.Actor);
                var profile = (CompanionProfile)Get(run.Actor, "Profile");
                object leftClick = Enum.Parse(NavigationType(host.GetType().Assembly, "CompanionItemGesture"), "Left");
                try
                {
                    human.armor[3] = new Item(Starboard);
                    // Ordinary wings leave room for a useful dash. The Starboard's
                    // native hover can already match or beat the shield's impulse.
                    body.armor[3] = new Item(ItemID.AngelWings);
                    body.armor[shieldSlot] = new Item(ItemID.EoCShield);
                    int dash = -1, wingAndDash = -1, unequipped = -1, wingAfter = -1, wallCross = -1;
                    int direction = 1, turns = 0, ownerMoving = 0, nearby = 0, longestNear = 0;
                    long dashAction = 0;
                    float maxDashSpeed = 0;
                    for (int tick = 0; tick < ticks; tick++)
                    {
                        if (direction > 0 && human.Center.X >= RoomX(106) || direction < 0 && human.Center.X <= RoomX(74))
                        { direction = -direction; turns++; }
                        Vector2 previousOwner = human.Bottom, before = body.Bottom;
                        float wingBefore = body.wingTime;
                        int delayBefore = body.dashDelay;
                        // The owner continues its original climb while the follower clears the obstruction.
                        run.Step(direction, jump: tick < 218, down: tick >= 61 && tick < 165);
                        if (tick < 130 && tick % 8 == 0)
                            Console.WriteLine($"FOLLOW LIVE ABILITY tick={tick} dashType={body.dashType}/{body.dashDelay} " +
                                $"prepared={GetOptional(navigation, "dashPreparedUntil")} wing={wingBefore}->{body.wingTime} " +
                                RoomTrace(run, human, navigation, tick, turns));
                        if (dash < 0 && delayBefore >= 0 && body.dashDelay < 0 && before.Y < floor * 16 - 12)
                        {
                            dash = tick;
                            dashAction = (long)Get(navigation, "ActionId");
                            maxDashSpeed = Math.Abs(body.velocity.X);
                            Console.WriteLine($"FOLLOW LIVE ABILITY dash tick={tick} id={dashAction} route={RouteStep(run.Actor)} " +
                                $"feet={body.Bottom} speed={body.velocity} wing={wingBefore}->{body.wingTime} controls={body.controlJump}/{body.controlDown}");
                        }
                        if (dash >= 0 && body.dashDelay < 0 && body.controlJump && body.wingTime < wingBefore)
                            wingAndDash = tick;
                        if (dash >= 0 && wingAndDash >= dash && unequipped < 0 && body.dashDelay >= 0)
                        {
                            Require((bool)Call(host, "ItemGesture", profile.Id, false, CompanionInventoryArea.Armor, shieldSlot, leftClick) &&
                                (bool)Call(host, "ItemGesture", profile.Id, false, CompanionInventoryArea.Inventory, packSlot, leftClick) &&
                                body.armor[shieldSlot].IsAir && body.inventory[packSlot].type == ItemID.EoCShield,
                                "the ordinary item transaction actually transfers the equipped shield to the same body's backpack");
                            unequipped = tick;
                        }
                        if (unequipped >= 0 && tick > unequipped && body.controlJump && body.dashType == 0 && body.wingTime < wingBefore &&
                            Math.Abs(body.Bottom.X - before.X) > 1f && wingAfter < 0) wingAfter = tick;
                        if (wallCross < 0 && body.Left.X >= (wall + 1) * 16 && body.Bottom.Y <= (floor - 5) * 16) wallCross = tick;
                        bool moving = Vector2.DistanceSquared(previousOwner, human.Bottom) > .75f;
                        if (moving) ownerMoving++;
                        nearby = moving && unequipped >= 0 && Vector2.DistanceSquared(body.Bottom, human.Bottom) <= 96 * 96 ? nearby + 1 : 0;
                        longestNear = Math.Max(longestNear, nearby);
                    }
                    Console.WriteLine($"FOLLOW LIVE ABILITY ownerMoving={ownerMoving} turns={turns} airDash={dash}/{dashAction} speed={maxDashSpeed:F2} " +
                        $"wingsWithDash={wingAndDash} clearedWall={wallCross} unequipped={unequipped} wingOnly={wingAfter} close={longestNear} " +
                        $"nativeTeleports={run.NativeRelocations} body={body.Bottom}/{body.velocity} owner={human.Bottom}/{human.velocity}");
                    Require(dash >= 0 && dashAction > 0 && maxDashSpeed > 10 && wingAndDash >= dash && wallCross >= 0 &&
                        unequipped > wingAndDash && wingAfter > unequipped && longestNear >= 12 && turns >= 2 && ownerMoving > 220 &&
                        run.NativeRelocations == 0 && !body.dead,
                        "the original body combines real airborne wing use and dash, then continues following with wings after the shield is transferred");
                }
                finally { human.armor[3] = previous; }
            }
        }

        private static void NativeGroundMountSelection(object context, object host, object profiles, Player human)
        {
            const int floor = 86, ticks = 300, slowSlot = 27, fastSlot = 31;
            using (var run = new NativeFollowWorld(context, host, profiles, human, "two-owned-mounts",
                new Vector2(RoomX(49), floor * 16), new Vector2(RoomX(75), floor * 16),
                () => PlaceFixtureStrip(43, 129, floor, TileID.Stone)))
            {
                Item prior = human.armor[3];
                Player body = run.Body;
                body.inventory[slowSlot] = new Item(ItemID.SlimySaddle);
                body.inventory[fastSlot] = new Item(ItemID.BlessedApple);
                var fast = body.inventory[fastSlot];
                var mobility = Get(run.Actor, "Mobility");
                var navigation = Navigator(run.Actor);
                try
                {
                    human.armor[3] = new Item();
                    int direction = 1, turns = 0, ownSources = 0, source = -1, mounted = -1, movingNear = 0, longestNear = 0;
                    float peak = 0f;
                    bool neverUsedSlime = true;
                    for (int tick = 0; tick < ticks; tick++)
                    {
                        if (direction > 0 && human.Center.X >= RoomX(108) || direction < 0 && human.Center.X <= RoomX(81))
                        { direction = -direction; turns++; }
                        float ownerBefore = human.Center.X;
                        run.Step(direction);
                        if (ownSources == 0 && Get(Get(mobility,"Observed"), "MountChoices") is Array choices)
                            ownSources = choices.Length;
                        var edge = Get(navigation, "ActionTransition");
                        var choice = Get(edge, "MountChoice");
                        if (Get(edge, "Kind").ToString() == "Mount" && (int)Get(choice, "Type") == fast.mountType)
                            source = (int)Get(choice, "ItemSlot");
                        if (body.mount.Active)
                        {
                            neverUsedSlime &= body.mount.Type != body.inventory[slowSlot].mountType;
                            if (body.mount.Type == fast.mountType)
                            {
                                if (mounted < 0) mounted = tick;
                                peak = Math.Max(peak, Math.Abs(body.velocity.X));
                            }
                        }
                        movingNear = mounted >= 0 && Math.Abs(human.Center.X - ownerBefore) > .75f &&
                            Math.Abs(body.Center.X - human.Center.X) < 86 ? movingNear + 1 : 0;
                        longestNear = Math.Max(longestNear, movingNear);
                    }
                    Console.WriteLine($"FOLLOW LIVE MOUNTS count={ownSources} plannedSlot={source} actualMount={mounted}/{fast.mountType} " +
                        $"peak={peak:F2} unrelatedSlowUnused={neverUsedSlime} ownerTurns={turns} nearMoving={longestNear} nativeTeleports={run.NativeRelocations}");
                    Require(ownSources == 2 && source == fastSlot && mounted >= 0 && peak > 4.5f && neverUsedSlime &&
                        turns >= 1 && longestNear >= 18 && ReferenceEquals(fast, body.inventory[fastSlot]) && run.NativeRelocations == 0 && !body.dead,
                        "an actual sustained ground pursuit selects the faster of two owned native mount sources and catches a moving owner");
                }
                finally { human.armor[3] = prior; }
            }
        }
    }
}
