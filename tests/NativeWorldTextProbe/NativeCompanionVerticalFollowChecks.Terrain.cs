using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using JueMingR.Platform.Companions;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionVerticalFollowChecks
    {
        private static void LowCeiling(object host, object profiles, Player human)
        {
            const int ground = 94, roof = 83, wall = 66, x = 64 * 16;
            using (var run = new Scenario(host, profiles, human, "terrain-low-ceiling", x, ground, () =>
            {
                Fill(48, 93, ground, TileID.Stone);
                Fill(62, wall, roof, TileID.Stone);
                for (int y = roof + 1; y <= 88; y++) StoneVertical(wall, y);
                Fill(70, 86, roof - 2, TileID.Stone);
            }, Starboard))
            using (var work = new TerrainSearchWork(Navigator(run.Actor)))
            {
                // The fixture owner is already outside above the roof; its
                // subsequent motion stays above every solid tile in the span.
                human.Bottom = new Vector2(x, (roof - 2) * 16);
                var body = run.Body;
                int roofHit = -1, backInput = -1, retreated = -1, open = -1, reached = -1, stable = 0;
                float hitFuel = 0, exitFuel = 0, hitX = 0, hitTop = 0;
                long elapsed = 0, peak = 0;
                for (int tick = 0; tick < 290; tick++)
                {
                    float beforeX = body.Center.X;
                    Measure(run.StepToward(new Vector2(75 * 16, (roof - 2) * 16), new Vector2(roofHit < 0 ? 0 : 3.5f, 0), measure: true), ref elapsed, ref peak);
                    bool atRoof = Math.Abs(body.Top.Y - (roof + 1) * 16) < 3 && body.Center.X >= 62 * 16 + 10 && body.Right.X <= (wall + 1) * 16;
                    if (roofHit < 0 && atRoof) { roofHit = tick; hitFuel = body.wingTime; hitTop = body.Top.Y; hitX = body.Center.X; }
                    if (roofHit >= 0 && backInput < 0 && body.controlLeft && body.Center.X < beforeX - .25f) backInput = tick;
                    if (roofHit >= 0 && retreated < 0 &&
                        (body.Top.Y > hitTop + 24 && body.velocity.Y > 1 && !body.controlJump ||
                         backInput >= roofHit && body.Center.X < hitX - 24))
                    { retreated = tick; exitFuel = body.wingTime; }
                    if (retreated >= 0 && open < 0 && (body.Left.X >= (wall + 1) * 16 + 3 || body.Right.X < 62 * 16)) open = tick;
                    bool arrived = open >= 0 && Vector2.DistanceSquared(body.Bottom, human.Bottom) < 36 * 36 &&
                        body.Center.X >= 70 * 16 && Math.Abs(body.velocity.Y) < 1.5f;
                    stable = arrived ? stable + 1 : 0;
                    if (stable == 12 && reached < 0) reached = tick - 11;
                    if (tick % 20 == 0 || tick == roofHit || tick == retreated || tick == open || tick == reached)
                        Console.WriteLine(FormattableString.Invariant($"TERRAIN B t={tick} owner={human.Bottom} body={body.Bottom}/{body.velocity} inputs[L,R,U,D,Jump]={body.controlLeft},{body.controlRight},{body.controlUp},{body.controlDown},{body.controlJump} reserve={body.wingTime:F1} route={RouteStep(run.Actor)}"));
                }
                Console.WriteLine(FormattableString.Invariant($"TERRAIN B lowCeiling firstContact={roofHit} backInput={backInput} retreat={retreated} open={open} reached={reached} finalStable={stable} wingOnContact={hitFuel:F1} wingOnRetreat={exitFuel:F1} body={body.Bottom}"));
                work.Report("B", 290, elapsed, peak);
                Require(roofHit >= 0 && retreated > roofHit && retreated - roofHit <= 90 && exitFuel > 25 &&
                    open > retreated && reached > open && stable >= 12,
                    "B: a genuine finite-wing ceiling trap actively releases or backs away into native free air, clears the obstruction and reaches the owner before waiting for exhaustion");
            }
            RoofReserveAndWake(host, profiles, human);
        }

        private static void RoofReserveAndWake(object host, object profiles, Player human)
        {
            const int ground = 94, roof = 83, wall = 66, blockerX = 62, blockerY = 85, x = 64 * 16;
            const int ownerMovesAt = 24;
            int ownerFloor = roof - 2;
            using (var run = new Scenario(host, profiles, human, "terrain-roof-reserve", x, ground, () =>
            {
                Fill(48, 93, ground, TileID.Stone);
                Fill(blockerX, wall, roof, TileID.Stone);
                StoneVertical(blockerX, blockerY);
                for (int y = roof + 1; y <= 88; y++) StoneVertical(wall, y);
                Fill(70, 86, ownerFloor, TileID.Stone);
            }, Starboard))
            {
                var body = run.Body;
                var navigation = Navigator(run.Actor);
                human.Bottom = new Vector2(x, ownerFloor * 16);
                int obstructed = -1, turned = -1, crossedOpening = -1, stable = 0;
                Vector2 failedProbePose = default;
                float movedBeforeWake = -1;
                bool originalBlocked = false, openingClear = false;
                var blockedTile = new Rectangle(blockerX * 16, blockerY * 16, 16, 16);
                for (int tick = 0; tick < 220 && stable < 12; tick++)
                {
                    var before = body.position;
                    run.StepToward(new Vector2(75 * 16, ownerFloor * 16), new Vector2(tick < ownerMovesAt ? 0 : 3.5f, 0));
                    bool caughtBelowRoof = body.Top.Y < (roof + 1) * 16 + 6 && body.Center.X >= blockerX * 16;
                    if (obstructed < 0 && caughtBelowRoof && Math.Abs(body.velocity.Y) < .25f)
                    {
                        obstructed = tick;
                        failedProbePose = body.position;
                        // Test the same observed height just inside the blocked side.
                        var entry = new Vector2(blockedTile.Right - 1, body.position.Y);
                        originalBlocked = Main.tile[blockerX, blockerY].active() &&
                            Collision.SolidCollision(entry, body.width, body.height);
                        Main.tile[blockerX, blockerY] = new Tile();
                        openingClear = !Collision.SolidCollision(entry, body.width, body.height);
                    }
                    if (obstructed >= 0 && tick > obstructed && turned < 0 && body.controlLeft && body.velocity.X < -.25f)
                    { turned = tick; movedBeforeWake = Vector2.Distance(failedProbePose, before); }
                    if (turned >= 0 && crossedOpening < 0 && blockedTile.Intersects(body.Hitbox)) crossedOpening = tick;
                    stable = OnFloor(body, ownerFloor) && Math.Abs(body.Center.X - human.Center.X) < 28 ? stable + 1 : 0;
                }
                Console.WriteLine(FormattableString.Invariant($"TERRAIN B' roof edit readAndClosed={obstructed} resumedLeft={turned} crossedOpening={crossedOpening} movedBeforeWake={movedBeforeWake:F1} closedCollision={originalBlocked} openCollision={!openingClear} stable={stable} blocker={Main.tile[blockerX,blockerY].active()} opposite={Main.tile[wall,blockerY].active()} body={body.Bottom}"));
                Require(obstructed >= 0 && originalBlocked && openingClear && turned > obstructed && turned - obstructed <= 22 &&
                    crossedOpening >= turned && movedBeforeWake < 16 && stable >= 12 &&
                    !Main.tile[blockerX, blockerY].active() && Main.tile[wall, blockerY].active(),
                    "B': opening a real previously read tile wakes the same blocked native follower, which uses that side and reaches the owner");

                // Fly naturally above the cleared area until the same real
                // Starboard has little of its own finite reserve remaining.
                Vector2 target = human.Bottom - new Vector2(0, 132);
                float lowest = body.wingTime;
                int airborneRefills = 0;
                for (int tick = 0; tick < 450 && body.wingTime > 0; tick++)
                {
                    float before = body.wingTime;
                    run.StepToward(target, new Vector2(0, 4.6f));
                    lowest = Math.Min(lowest, body.wingTime);
                    if (body.wingTime > before && body.Bottom.Y < ownerFloor * 16 - 16) airborneRefills++;
                }
                Console.WriteLine($"TERRAIN B' finite reserve minimum={lowest:F1} airborneRefills={airborneRefills} {PlanStatus(navigation)}");
                Require(lowest < 30 && airborneRefills == 0,
                    "the actual same-body wing route uses its finite reserve without replenishing it in open air");
            }
        }

        private static void DescendingDetour(object host, object profiles, Player human)
        {
            const int platform = 80, floor = 92, rightWall = 65, leftWall = 54, ownerShelf = 79;
            using (var run = new Scenario(host, profiles, human, "terrain-detour", 60 * 16, platform, () =>
            {
                Fill(47, 97, floor, TileID.Stone);
                Fill(leftWall, rightWall, 75, TileID.Stone);
                Fill(leftWall + 1, rightWall - 1, platform, TileID.Platforms);
                for (int y = 76; y <= 87; y++) { StoneVertical(leftWall, y); StoneVertical(rightWall, y); }
                Fill(70, 95, ownerShelf, TileID.Stone);
            }, Starboard))
            using (var work = new TerrainSearchWork(Navigator(run.Actor)))
            {
                var body = run.Body;
                var profile = (CompanionProfile)Get(run.Actor, "Profile");
                const int potion = 33;
                body.inventory[potion] = new Item(ItemID.LesserHealingPotion) { stack = 2 };
                body.statLife = 40;
                human.Bottom = new Vector2(74 * 16, ownerShelf * 16);
                int support = OnFloor(body, platform) ? 0 : -1;
                int planned = -1, down = -1, stair = -1, crossed = -1, lowerLanding = -1, belowWall = -1, returned = -1, stable = 0;
                int ownerWalking = 0, healQueued = -1, healed = -1;
                var departure = Vector2.Zero;
                float maxExtraDistance = 0, landedDepth = 0;
                long elapsed = 0, peak = 0;
                var navigator = Navigator(run.Actor);
                var refinement = GetOptional(navigator, "refine");
                string previousProof = null;
                for (int tick = 0; tick < 420; tick++)
                {
                    float x = (74 * 16) + 50 * (1 - Math.Abs(tick % 84 - 42) / 42f);
                    var before = body.Bottom;
                    Measure(run.StepToward(new Vector2(x, ownerShelf * 16), new Vector2(1.5f, 0), measure: true), ref elapsed, ref peak);
                    if (Math.Abs(human.velocity.X) > .5f) ownerWalking++;
                    string route = RouteStep(run.Actor);
                    if (planned < 0 && route.StartsWith("PlatformDrop:", StringComparison.Ordinal)) planned = tick;
                    if (down < 0 && planned >= 0 && body.controlDown && Math.Abs(before.Y - platform * 16) < 2)
                    { down = tick; departure = before; }
                    if (stair < 0 && down >= 0 && body.stairFall && body.Bottom.Y < platform * 16 + 18) stair = tick;
                    if (crossed < 0 && stair >= 0 && body.Bottom.Y > platform * 16 + 18 && body.velocity.Y > 0) crossed = tick;
                    if (crossed >= 0 && lowerLanding < 0 && OnFloor(body, floor))
                    {
                        lowerLanding = tick;
                        landedDepth = body.Bottom.Y - departure.Y;
                    }
                    if (lowerLanding >= 0 && belowWall < 0 && OnFloor(body, floor) && body.controlRight && body.velocity.X > 0 &&
                        body.Left.X >= (rightWall + 1) * 16 && body.Top.Y > 88 * 16 - 1)
                        belowWall = tick;
                    if (healQueued < 0 && crossed >= 0 && belowWall < 0 && body.velocity.Y > 1)
                    {
                        Require((bool)Call(host, "UseItemOnce", profile.Id, CompanionInventoryArea.Inventory, potion), "a real existing potion request is accepted during the observed descent");
                        healQueued = tick;
                    }
                    if (healed < 0 && healQueued >= 0 && body.inventory[potion].stack == 1 && body.statLife > 40 && Vector2.DistanceSquared(before, body.Bottom) > 1)
                        healed = tick;
                    if (down >= 0 && belowWall < 0) maxExtraDistance = Math.Max(maxExtraDistance,
                        Vector2.Distance(human.Bottom, body.Bottom) - Vector2.Distance(human.Bottom, departure));
                    bool arrived = belowWall >= 0 && body.Center.X >= 71 * 16 && OnFloor(body, ownerShelf) && Math.Abs(body.Center.X - human.Center.X) < 45;
                    stable = arrived ? stable + 1 : 0;
                    if (stable == 18 && returned < 0) returned = tick - 17;
                    var inlet = refinement == null ? null : Get(refinement, "Edge");
                    string proof = inlet == null ? "historical" : $"{Get(refinement, "Active")}/{Get(Get(refinement, "Witness"), "Valid")} " +
                        $"{Get(inlet, "Kind")}:{Get(inlet, "From")}→{Get(inlet, "To")} {RouteTrace(navigator)}";
                    if (proof != previousProof || tick % 25 == 0 || tick == planned || tick == down || tick == stair || tick == crossed || tick == lowerLanding || tick == belowWall || tick == returned)
                    {
                        string ticks = inlet == null ? "historical" : $"{Get(refinement, "ticks")}";
                        Console.WriteLine(FormattableString.Invariant($"TERRAIN C/D t={tick} owner={human.Bottom} body={body.Bottom}/{body.velocity} move={body.controlLeft}/{body.controlRight} down={body.controlDown} stair={body.stairFall} jump={body.controlJump} life={body.statLife} wings={body.wingTime:F1} route={route} search={PlanStatus(navigator)} proof={proof} ticks={ticks}"));
                    }
                    previousProof = proof;
                }
                Console.WriteLine(FormattableString.Invariant($"TERRAIN C/D support={support} publishedDrop={planned} down={down} stairFall={stair} crossed={crossed} lowerLanding={lowerLanding} landedDepth={landedDepth:F1} underWall={belowWall} return={returned} stableTail={stable} ownerWalk={ownerWalking} detourExtra={maxExtraDistance:F1} healQueued={healQueued} consumedWhileMoving={healed} potions={body.inventory[potion].stack}"));
                work.Report("C/D", 420, elapsed, peak);
                Require(support >= 0 && planned >= 0 && down >= planned && stair >= down && crossed > stair && lowerLanding > crossed && belowWall > lowerLanding &&
                    Math.Abs(landedDepth - (floor - platform) * 16f) < 2f && maxExtraDistance > 0 &&
                    returned > belowWall && ownerWalking > 60 && stable >= 18,
                    "C/D: the actual supported platform gives a published platform drop, native Down and stairFall, then reaches the moving owner via the longer route below the wall");
                Require(healed > healQueued && body.inventory[potion].stack == 1,
                    "the same following body consumes its real potion once while moving through the detour");
            }
        }

        private static void StoneVertical(int x, int y) => Fill(x, x, y, TileID.Stone);

        private static object Navigator(object actor)
        {
            object behavior = Get(actor, "Behavior");
            return GetOptional(behavior, "Navigator") ?? Get(behavior, "navigation");
        }

        private static void Measure(long ticks, ref long elapsed, ref long peak)
        { elapsed += ticks; peak = Math.Max(peak, ticks); }

        private static string PlanStatus(object navigation) =>
            $"query={Get(navigation, "QueryStatus")}/{Get(navigation, "QueryGeneration")} age={Get(navigation, "QueryPendingAge")} " +
            $"action={Get(navigation, "ActionId")}/{Get(navigation, "ActionKind")}/{Get(navigation, "ActionOutcome")} reason={Get(navigation, "LastDeferral")}";

        private static void RetainedPlan(object host, object profiles, Player human)
        {
            const int floor = 80;
            using (var run = new Scenario(host, profiles, human, "terrain-retained", 58 * 16, floor, () =>
            {
                Fill(53, 73, floor, TileID.Stone);
                Fill(53, 73, floor - 4, TileID.Stone);
                for (int y = floor - 3; y < floor; y++) { StoneVertical(53, y); StoneVertical(73, y); }
            }))
            {
                Player body = run.Body;
                object navigation = Navigator(run.Actor);
                human.Bottom = new Vector2(68 * 16, floor * 16);
                long action = 0;
                int sameActionMoves = 0, stable = 0;
                float start = body.Bottom.X;
                for (int tick = 0; tick < 140; tick++)
                {
                    float priorX = body.Bottom.X;
                    run.StepToward(new Vector2(71 * 16, floor * 16), new Vector2(1.1f, 0));
                    long id = (long)Get(navigation, "ActionId");
                    if (action == 0 && body.controlRight) action = id;
                    if (action != 0 && id == action && body.Bottom.X > priorX + .5f) sameActionMoves++;
                    stable = OnFloor(body, floor) && Math.Abs(body.Bottom.X - human.Bottom.X) < 38 ? stable + 1 : 0;
                }
                Console.WriteLine($"TERRAIN E action={action} nativeStepsWithSameAction={sameActionMoves} advance={body.Bottom.X - start:F1} stable={stable} {PlanStatus(navigation)}");
                Require(action > 0 && sameActionMoves >= 10 && body.Bottom.X - start > 90 && stable >= 12,
                    "the actual ground action survives nearby changing goals and carries the same body to the later owner position");
            }
        }

        private sealed class TerrainSearchWork : IDisposable
        {
            private readonly object navigation;
            private readonly int initialGeneration, initialExpansions;

            internal TerrainSearchWork(object navigation)
            {
                this.navigation = navigation;
                initialGeneration = (int)Get(navigation, "QueryGeneration");
                initialExpansions = (int)(GetOptional(navigation, "ExpansionWork") ?? 0);
            }

            internal void Report(string sample, int updates, long elapsed, long peak)
            {
                int generations = (int)Get(navigation, "QueryGeneration") - initialGeneration;
                object total = GetOptional(navigation, "ExpansionWork");
                string expansions = total is int work ? (work - initialExpansions).ToString() : "unavailable";
                Console.WriteLine(FormattableString.Invariant($"TERRAIN {sample} work updates={updates} queryGenerations={generations} expansions={expansions} nativeTotalMs={elapsed*1000d/Stopwatch.Frequency:F3} nativeMaxMs={peak*1000d/Stopwatch.Frequency:F3}"));
            }
            public void Dispose() { }
        }
    }
}
