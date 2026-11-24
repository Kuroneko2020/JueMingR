using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionVerticalFollowChecks
    {
        private static void LongAutonomousDetour(object context, object host, object profiles, Player human)
        {
            const int upper = 82, lower = 94, lip = 65, closeWall = 112;
            const int startX = 105 * 16, firstGoal = 107 * 16, secondGoal = 98 * 16, ownerReversal = 720;
            using (var run = new NativeFollowWorld(context, host, profiles, human, "long-detour",
                new Vector2(startX, upper * 16), new Vector2(firstGoal, lower * 16), () =>
                {
                    Fill(lip, closeWall, upper, TileID.Stone);
                    Fill(60, closeWall, 77, TileID.Stone);
                    Fill(57, closeWall, lower, TileID.Stone);
                    for (int y = 78; y < lower; y++) Fill(closeWall, closeWall, y, TileID.Stone);
                }))
            {
                run.JoinPeer("long-detour-peer", run.Body.Bottom - new Vector2(16, 0));
                Player peer = run.Peer;
                object peerActor = run.PeerActor;
                var first = new DetourObservation(run.Body, run.Actor);
                var second = new DetourObservation(peer, peerActor);
                bool debugWork = GetOptional(first.Navigation, "ExpansionWork") != null;
                int previousWork = debugWork ? (int)Get(first.Navigation, "ExpansionWork") + (int)Get(second.Navigation, "ExpansionWork") : 0;
                int teamWork = 0, ownerMoved = 0, steps = 0;
                int nextLipTrace = 0, nextPeerTrace = 0;
                long elapsed = 0, peak = 0, pairedElapsed = 0, pairedPeak = 0;
                for (; steps < 930; steps++)
                {
                    // The owner starts below the sealed corridor and never visits the far exit.
                    // The final turn follows the same clock even if neither companion arrives.
                    int target = steps >= ownerReversal ? secondGoal : steps >= 520 ? firstGoal : firstGoal + (steps / 80 % 2 == 0 ? 24 : -24);
                    if (steps == ownerReversal) first.Stable = second.Stable = 0;
                    Vector2 beforeOwner = human.position, beforeFirst = run.Body.position, beforePeer = peer.position;
                    int firstSlice = (int)Get(first.Navigation, "PlannerSlices"), secondSlice = (int)Get(second.Navigation, "PlannerSlices");
                    long sample = run.Step(NativeAxisTo(human, target));
                    if (Vector2.DistanceSquared(beforeOwner, human.position) > .25f) ownerMoved++;
                    Measure(sample, ref elapsed, ref peak);
                    if (steps < 400) Measure(sample, ref pairedElapsed, ref pairedPeak);
                    first.Observe(steps, beforeFirst, human, upper, lower, lip, ownerReversal, (int)Get(first.Navigation, "PlannerSlices") != firstSlice);
                    second.Observe(steps, beforePeer, human, upper, lower, lip, ownerReversal, (int)Get(second.Navigation, "PlannerSlices") != secondSlice);
                    if (debugWork)
                    {
                        int work = (int)Get(first.Navigation, "ExpansionWork") + (int)Get(second.Navigation, "ExpansionWork");
                        teamWork = Math.Max(teamWork, work - previousWork);
                        previousWork = work;
                    }
                    if (steps == 99 || steps == 249 || steps == 499 || steps == ownerReversal || steps == 929)
                        Console.WriteLine($"DETOUR checkpoint tick={steps} owner={human.Bottom} first={run.Body.Bottom}/{RouteStep(run.Actor)} {first.SearchState()} second={peer.Bottom}/{RouteStep(peerActor)} {second.SearchState()}");
                    if (run.Body.Bottom.X < (lip + 4) * 16 && steps >= nextLipTrace && first.Exit < 0)
                    {
                        Console.WriteLine($"DETOUR lip tick={steps} first={run.Body.Bottom}/{run.Body.velocity} {RouteStep(run.Actor)} {first.ActionState()}");
                        nextLipTrace = steps + 8;
                    }
                    if (peer.Bottom.X < (lip + 4) * 16 && steps >= nextPeerTrace && second.Exit < 0 && steps < 600)
                    {
                        Console.WriteLine($"DETOUR peer lip tick={steps} body={peer.Bottom}/{peer.velocity} {RouteStep(peerActor)} {second.ActionState()}");
                        nextPeerTrace = steps + 4;
                    }
                    if (first.Resumed >= ownerReversal && second.Resumed >= ownerReversal &&
                        first.Stable >= 28 && second.Stable >= 28 && Math.Abs(human.Bottom.X - secondGoal) <= 2)
                    { steps++; break; }
                }
                first.Report("first");
                second.Report("second");
                Console.WriteLine(FormattableString.Invariant($"DETOUR team steps={steps} ownerMoved={ownerMoved} ownerReversal={ownerReversal} maxWork={teamWork} relocations={run.NativeRelocations} nativeTotalMs={1000d * elapsed / Stopwatch.Frequency:F3} nativeMaxMs={1000d * peak / Stopwatch.Frequency:F3}"));
                Console.WriteLine(FormattableString.Invariant($"DETOUR matched initialSteps=400 nativeTotalMs={1000d * pairedElapsed / Stopwatch.Frequency:F3} nativeMaxMs={1000d * pairedPeak / Stopwatch.Frequency:F3}"));
                Require(ownerMoved >= 200 && OnFloor(human, lower) && Math.Abs(human.Bottom.X - secondGoal) <= 2 && run.NativeRelocations == 0,
                    "the native owner walks the original lower floor and reaches its scheduled second destination independently");
                Require(first.Result(upper, lower, ownerReversal) && second.Result(upper, lower, ownerReversal),
                    "two autonomous native bodies retreat along the upper floor, take the far exit, descend, settle below, and follow the owner's next movement");
                Require(first.WithoutGrant > 30 && second.WithoutGrant > 30 && (!debugWork || teamWork > 0 && teamWork <= 96),
                    "both bodies execute real movement while the other actor owns the shared planning slice");
            }
        }

        private sealed class DetourObservation
        {
            private readonly Player body;
            private readonly object actor;
            private readonly float originX;
            private readonly int life;
            internal readonly object Navigation;
            internal int Reverse = -1, Exit = -1, Landing = -1, FirstSettled = -1, Resumed = -1;
            internal int Stable, WithoutGrant, Faults;
            private float farthestRetreat, maxStep;

            internal DetourObservation(Player body, object actor)
            {
                this.body = body; this.actor = actor;
                originX = body.Bottom.X; life = body.statLife;
                Navigation = Navigator(actor);
            }

            internal void Observe(int tick, Vector2 before, Player owner, int upper, int lower, int lip, int resume, bool granted)
            {
                float moved = Vector2.Distance(before, body.position);
                maxStep = Math.Max(maxStep, moved);
                farthestRetreat = Math.Max(farthestRetreat, originX - body.Bottom.X);
                if (!granted && moved > .5f) WithoutGrant++;
                if ((bool)Get(actor, "Faulted")) Faults++;
                if (Reverse < 0 && body.Bottom.X < originX - 16 && body.controlLeft && OnFloor(body, upper)) Reverse = tick;
                if (Exit < 0 && Reverse >= 0 && body.position.X + body.width <= lip * 16 + 1 && body.Bottom.Y > upper * 16 + 4 && body.velocity.Y > 0)
                    Exit = tick;
                if (Landing < 0 && Exit >= 0 && OnFloor(body, lower)) Landing = tick;
                bool close = Landing >= 0 && OnFloor(body, lower) && Math.Abs(owner.Bottom.X - body.Bottom.X) <= OrdinaryFollowSpacing + .01f &&
                    Math.Abs(owner.velocity.X) < .01f && Math.Abs(body.velocity.X) < .65f;
                Stable = close ? Stable + 1 : 0;
                if (FirstSettled < 0 && tick < resume && Stable == 24) FirstSettled = tick;
                if (Resumed < 0 && tick >= resume && Landing >= 0 && body.controlLeft && before.X - body.position.X > .5f) Resumed = tick;
            }

            internal bool Result(int upper, int lower, int resume) =>
                Reverse >= 0 && Exit > Reverse && Landing > Exit && FirstSettled > Landing && resume > FirstSettled &&
                Resumed >= resume && Stable >= 28 && farthestRetreat >= 600 && maxStep < 16 && OnFloor(body, lower) &&
                !OnFloor(body, upper) && body.statLife == life && body.wingsLogic == 0 && body.mount.Type < 0 && Faults == 0;

            internal void Report(string key) =>
                Console.WriteLine(FormattableString.Invariant($"DETOUR {key} reverse={Reverse} exit={Exit} landing={Landing} settled={FirstSettled} resumed={Resumed} stable={Stable} withoutGrant={WithoutGrant} retreat={farthestRetreat:F1} maxStep={maxStep:F2} faults={Faults} {SearchState()} {ActionState()}"));

            internal string SearchState() => PlanStatus(Navigation);
            internal string ActionState()
            {
                var refinement = Get(Navigation, "refine");
                var edge = Get(refinement, "Edge");
                var route = GetOptional(Navigation, "route");
                return $"status={Get(actor, "Status")} tick={Main.GameUpdateCount} {RouteTrace(Navigation)} " +
                    $"terminal={(route == null ? null : GetOptional(route, "corridorTerminal"))} " +
                    $"refinement={Get(refinement, "Active")}/{Get(Navigation, "refineReady")} {Get(edge, "Kind")}:{Get(edge, "From")}→{Get(edge, "To")} " +
                    $"irreversible={Get(edge, "Irreversible")} ticks={Get(refinement, "ticks")}";
            }
        }

    }
}
