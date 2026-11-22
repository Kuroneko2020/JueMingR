using System;
using System.Collections;
using System.Diagnostics;
using HarmonyLib;
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
                float maxExtraDistance = 0;
                long elapsed = 0, peak = 0;
                for (int tick = 0; tick < 420; tick++)
                {
                    float x = (74 * 16) + 50 * (1 - Math.Abs(tick % 84 - 42) / 42f);
                    var before = body.Bottom;
                    Measure(run.StepToward(new Vector2(x, ownerShelf * 16), new Vector2(1.5f, 0), measure: true), ref elapsed, ref peak);
                    if (Math.Abs(human.velocity.X) > .5f) ownerWalking++;
                    string route = RouteStep(run.Actor);
                    if (planned < 0 && route.StartsWith("Drop:", StringComparison.Ordinal)) planned = tick;
                    if (down < 0 && planned >= 0 && body.controlDown && Math.Abs(before.Y - platform * 16) < 2)
                    { down = tick; departure = before; }
                    if (stair < 0 && down >= 0 && body.stairFall && body.Bottom.Y < platform * 16 + 18) stair = tick;
                    if (crossed < 0 && stair >= 0 && body.Bottom.Y > platform * 16 + 18 && body.velocity.Y > 0) crossed = tick;
                    if (crossed >= 0 && lowerLanding < 0 && OnFloor(body, floor)) lowerLanding = tick;
                    if (crossed >= 0 && belowWall < 0 && body.Left.X >= (rightWall + 1) * 16 && body.Top.Y > 88 * 16 - 1)
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
                    if (tick % 25 == 0 || tick == planned || tick == down || tick == stair || tick == crossed || tick == lowerLanding || tick == belowWall || tick == returned)
                        Console.WriteLine(FormattableString.Invariant($"TERRAIN C/D t={tick} owner={human.Bottom} body={body.Bottom}/{body.velocity} down={body.controlDown} stair={body.stairFall} jump={body.controlJump} life={body.statLife} wings={body.wingTime:F1} route={route} search={PlanStatus(Navigator(run.Actor))}"));
                }
                Console.WriteLine(FormattableString.Invariant($"TERRAIN C/D support={support} publishedDrop={planned} down={down} stairFall={stair} crossed={crossed} lowerLanding={lowerLanding} underWall={belowWall} return={returned} stableTail={stable} ownerWalk={ownerWalking} detourExtra={maxExtraDistance:F1} healQueued={healQueued} consumedWhileMoving={healed} potions={body.inventory[potion].stack}"));
                work.Report("C/D", 420, elapsed, peak);
                Require(support >= 0 && planned >= 0 && down >= planned && stair >= down && crossed > stair && lowerLanding > crossed && belowWall > lowerLanding &&
                    maxExtraDistance >= 70 && returned > belowWall && ownerWalking > 60 && stable >= 18,
                    "C/D: the actual supported platform gives a published Drop, native Down and stairFall, then reaches the moving owner via the longer route below the wall");
                Require(healed > healQueued && body.inventory[potion].stack == 1,
                    "the same following body consumes its real potion once while moving through the detour");
            }
        }

        private static void StoneVertical(int x, int y) => Fill(x, x, y, TileID.Stone);

        private static object Navigator(object actor) => Get(Get(actor, "Behavior"), "navigation");

        private static void Measure(long ticks, ref long elapsed, ref long peak)
        { elapsed += ticks; peak = Math.Max(peak, ticks); }

        private static string PlanStatus(object navigation)
        {
            var search = GetOptional(navigation, "search");
            if (search == null) return "absent";
            var nodes = (IList)Get(search, "Nodes");
            int drops = 0, observed = 0, below = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if ((int)Get(node, "Previous") < 0) continue;
                string kind = Get(Get(node, "Step"), "Kind").ToString();
                if (kind == "Drop" || kind == "LedgeDrop") drops++;
                if ((bool)Get(node, "Observe")) observed++;
                if (((Vector2)Get(node, "Position")).Y > 1390) below++;
            }
            return FormattableString.Invariant($"nodes={nodes.Count}/drop={drops}/obs={observed}/below={below}/active={Get(search, "Active")}/exhausted={Get(search, "Exhausted")}/window={Get(search, "Window")}/candidate={Get(search, "Candidate")}/pending={GetOptional(navigation, "pendingResult") ?? -1}");
        }

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
                var body = run.Body;
                var navigation = Navigator(run.Actor);
                var state = Get(run.Actor, "Mobility");
                var root = Enum.Parse(AccessTools.Inner(navigation.GetType(), "RouteMotion"), "Ground");
                var route = (IList)Get(navigation, "route");
                human.Bottom = new Vector2(68 * 16, floor * 16);
                var goal = human.Center - new Vector2(body.width * .5f, body.height * .5f);
                int candidate = -1, pending = -1, grants = 0, publications = 0;
                bool backgroundExhausted = false;
                object search = null;
                using (var work = new TerrainSearchWork(navigation))
                {
                    long begin = Stopwatch.GetTimestamp();
                    for (; grants < 120; grants++)
                    {
                        // A bounded state-level slice exercises the real caller's
                        // publication gate without emitting or substituting input.
                        NativeQuickItemChecks.BeginWorldStep();
                        Call(navigation, "SearchRoute", body, Get(state, "Observed"), goal, 22, root, false);
                        search = Get(navigation, "search");
                        var nodes = (IList)Get(search, "Nodes");
                        for (int i = 0; i < nodes.Count; i++)
                            if ((int)Get(nodes[i], "Previous") >= 0 && Vector2.DistanceSquared((Vector2)Get(nodes[i], "Position"), goal) <= 22 * 22)
                            { candidate = i; break; }
                        pending = (int)(GetOptional(navigation, "pendingResult") ?? -1);
                        backgroundExhausted = (bool)Get(search, "Exhausted");
                        if (candidate >= 0 && pending >= 0 || backgroundExhausted) break;
                    }
                    long elapsed = Stopwatch.GetTimestamp() - begin;
                    Require(candidate >= 0 && route.Count == 0, "E fixture actually discovered a reachable candidate behind the closed publication gate");
                    var from = body.position;
                    int starts = work.Starts;
                    int candidateId = pending >= 0 ? pending : candidate;
                    var discovered = (Vector2)Get(((IList)Get(search, "Nodes"))[candidateId], "Position");
                    // Real Follow walks from the original pose before the gate
                    // opens. It must connect to the old result from today's body.
                    for (int tick = 0; tick < 19; tick++) run.StepOwner(Vector2.Zero);
                    var live = body.position;
                    for (; publications < 10 && route.Count == 0; publications++)
                    {
                        NativeQuickItemChecks.BeginWorldStep();
                        Call(navigation, "SearchRoute", body, Get(state, "Observed"), goal, 22, root, true);
                    }
                    bool published = route.Count > 0;
                    int startsAfter = work.Starts;
                    float reconnect = published ? Vector2.Distance(live, (Vector2)Get(route[0], "From")) : -1;
                    float retainedEnd = published ? Vector2.Distance(discovered, (Vector2)Get(route[route.Count - 1], "To")) : -1;
                    int applied = 0, stable = 0;
                    for (int tick = 0; tick < 115; tick++)
                    {
                        run.StepOwner(Vector2.Zero);
                        if (body.controlRight && !RouteStep(run.Actor).StartsWith("local", StringComparison.Ordinal)) applied++;
                        stable = OnFloor(body, floor) && Math.Abs(body.Center.X - human.Center.X) < 28 ? stable + 1 : 0;
                    }
                    Console.WriteLine(FormattableString.Invariant($"TERRAIN E state candidate={candidate} pending={pending} backgroundExhausted={backgroundExhausted} backgroundSlices={grants+1} published={published} publishSlices={publications} movedBefore={live.X-from.X:F1} reconnect={reconnect:F1} retainedEnd={retainedEnd:F1} startsBefore={starts} startsAtPublish={startsAfter} workCalls={work.Candidates} routeInputs={applied} stableTail={stable} backgroundMs={elapsed*1000d/Stopwatch.Frequency:F3}"));
                    Require(pending >= 0 && published && startsAfter == starts &&
                        live.X - from.X >= 8 && reconnect >= 0 && reconnect <= 26 && retainedEnd >= 0 && retainedEnd < 4 && applied > 0 && stable >= 12,
                        "E: a real discovered plan remains pending without exhaustion, reconnects from a body advanced by native Follow, then supplies actual uninterrupted movement without restarting planning");
                }
            }
        }

        private sealed class TerrainSearchWork : IDisposable
        {
            private static TerrainSearchWork current;
            private readonly Harmony observation = new Harmony("JueMingR.Tests.TerrainSearchWork");
            internal int Starts, Candidates;

            internal TerrainSearchWork(object navigation)
            {
                current = this;
                var type = navigation.GetType();
                observation.Patch(AccessTools.Method(AccessTools.Inner(type, "RouteSearch"), "Begin"),
                    prefix: new HarmonyMethod(typeof(TerrainSearchWork), nameof(CountStart)));
                observation.Patch(AccessTools.Method(type, "TryEdge"),
                    prefix: new HarmonyMethod(typeof(TerrainSearchWork), nameof(CountCandidate)));
            }

            private static void CountStart() { if (current != null) current.Starts++; }
            private static void CountCandidate() { if (current != null) current.Candidates++; }
            internal void Report(string sample, int updates, long elapsed, long peak)
            {
                Console.WriteLine(FormattableString.Invariant($"TERRAIN {sample} work updates={updates} starts={Starts} edgeEvaluations={Candidates} nativeTotalMs={elapsed*1000d/Stopwatch.Frequency:F3} nativeMaxMs={peak*1000d/Stopwatch.Frequency:F3}"));
            }
            public void Dispose() { observation.UnpatchAll(observation.Id); current = null; }
        }
    }
}
