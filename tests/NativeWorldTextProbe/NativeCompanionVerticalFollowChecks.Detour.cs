using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeCompanionVerticalFollowChecks
    {
        private static void LongAutonomousDetour(object host, object profiles, Player human)
        {
            const int upper = 82, lower = 94, lip = 65, closeWall = 112;
            const int startX = 105 * 16, firstGoal = 107 * 16, secondGoal = 98 * 16;
            using (var run = new Scenario(host, profiles, human, "long-detour", startX, upper, () =>
            {
                Fill(lip, closeWall, upper, TileID.Stone);
                Fill(60, closeWall, 77, TileID.Stone);
                Fill(57, closeWall, lower, TileID.Stone);
                for (int y = 78; y < lower; y++) Fill(closeWall, closeWall, y, TileID.Stone);
            }))
            {
                Require((bool)Call(host, "Create", "long-detour-peer"), "a second companion uses the public Follow lifecycle");
                Guid peerId = (Guid)Get(host, "LastCreatedId");
                NativeQuickItemChecks.Until(() => { Call(profiles, "Pump"); return (bool)Call(profiles, "IsSaved", peerId); });
                Require((bool)Call(host, "Join", peerId), "the second genuine player joins");
                var peer = (Player)Call(host, "GetBody", peerId);
                object peerActor = Call(host, "Owned", peer);
                peer.Bottom = run.Body.Bottom - new Vector2(16, 0);
                try
                {
                    Require((bool)Call(host, "SetBehavior", peerId, new CompanionBehavior
                    { Follow = true, AttackEnabled = false, HealthRecoveryEnabled = false }), "both joined bodies accept ordinary Follow");
                    // The owner starts below the sealed upper corridor. An actual
                    // native teleport splits the trail; no earlier sample enters
                    // the lower route or shows either body the distant exit.
                    human.Teleport(new Vector2(firstGoal - human.width * .5f, lower * 16 - human.height), 0);
                    var first = new DetourObservation(run.Body, run.Actor);
                    var second = new DetourObservation(peer, peerActor);
                    bool debugWork = GetOptional(first.Navigation, "RouteWork") != null;
                    int previousWork = debugWork ? (int)Get(first.Navigation, "RouteWork") : 0;
                    int teamWork = 0, resume = -1, steps = 0;
                    int nextLipTrace = 0, nextPeerTrace = 0;
                    long elapsed = 0, peak = 0, pairedElapsed = 0, pairedPeak = 0;
                    for (; steps < 930; steps++)
                    {
                        if (resume < 0 && first.Stable >= 24 && second.Stable >= 24)
                        {
                            resume = steps;
                            first.Stable = second.Stable = 0;
                        }
                        Vector2 target = new Vector2(resume < 0 ? firstGoal : secondGoal, lower * 16);
                        Vector2 beforeFirst = run.Body.position, beforePeer = peer.position;
                        long total = run.StepToward(target, new Vector2(2, 0), measure: true);
                        long peerStart = Stopwatch.GetTimestamp();
                        peer.Update(peer.whoAmI);
                        long sample = total + Stopwatch.GetTimestamp() - peerStart;
                        Measure(sample, ref elapsed, ref peak);
                        if (steps < 400) Measure(sample, ref pairedElapsed, ref pairedPeak);
                        var granted = GetOptional(host, "planningActor");
                        first.Observe(steps, beforeFirst, human, upper, lower, lip, resume, granted);
                        second.Observe(steps, beforePeer, human, upper, lower, lip, resume, granted);
                        if (debugWork)
                        {
                            int work = (int)Get(first.Navigation, "RouteWork") + (int)Get(second.Navigation, "RouteWork");
                            teamWork = Math.Max(teamWork, work - previousWork);
                            previousWork = work;
                        }
                        if (steps == 99 || steps == 249 || steps == 499 || steps == 749 || steps == 929)
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
                        if (resume >= 0 && first.Resumed >= resume && second.Resumed >= resume &&
                            first.Stable >= 28 && second.Stable >= 28 && Math.Abs(human.Bottom.X - secondGoal) < 1)
                        { steps++; break; }
                    }
                    first.Report("first");
                    second.Report("second");
                    Console.WriteLine(FormattableString.Invariant($"DETOUR team steps={steps} ownerMoved={resume} maxWork={teamWork} nativeTotalMs={1000d * elapsed / Stopwatch.Frequency:F3} nativeMaxMs={1000d * peak / Stopwatch.Frequency:F3}"));
                    Console.WriteLine(FormattableString.Invariant($"DETOUR matched initialSteps=400 nativeTotalMs={1000d * pairedElapsed / Stopwatch.Frequency:F3} nativeMaxMs={1000d * pairedPeak / Stopwatch.Frequency:F3}"));
                    Require(first.Result(upper, lower, resume) && second.Result(upper, lower, resume) && first.Anchors == 0 && second.Anchors == 0,
                        "two autonomous native bodies retreat along the upper floor, take the far exit, descend, settle below, and follow the owner's next movement");
                    Require(first.WithoutGrant > 30 && second.WithoutGrant > 30 && (!debugWork || teamWork > 0 && teamWork <= 160),
                        "both bodies execute real movement while the other actor owns the shared planning slice");
                }
                finally
                {
                    Call(host, "Recall", peerId);
                    NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); return !((IDictionary)Get(host, "actors")).Contains(peerId); });
                }
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
            internal int Stable, WithoutGrant, Anchors, Faults;
            private float farthestRetreat, maxStep;

            internal DetourObservation(Player body, object actor)
            {
                this.body = body; this.actor = actor;
                originX = body.Bottom.X; life = body.statLife;
                Navigation = Navigator(actor);
            }

            internal void Observe(int tick, Vector2 before, Player owner, int upper, int lower, int lip, int resume, object granted)
            {
                float moved = Vector2.Distance(before, body.position);
                maxStep = Math.Max(maxStep, moved);
                farthestRetreat = Math.Max(farthestRetreat, originX - body.Bottom.X);
                if (!ReferenceEquals(granted, actor) && moved > .5f) WithoutGrant++;
                if ((long)(GetOptional(Navigation, "trailJoined") ?? 0L) != 0) Anchors++;
                if ((bool)Get(actor, "Faulted")) Faults++;
                if (Reverse < 0 && body.Bottom.X < originX - 16 && body.controlLeft && OnFloor(body, upper)) Reverse = tick;
                if (Exit < 0 && Reverse >= 0 && body.position.X + body.width <= lip * 16 + 1 && body.Bottom.Y > upper * 16 + 4 && body.velocity.Y > 0)
                    Exit = tick;
                if (Landing < 0 && Exit >= 0 && OnFloor(body, lower)) Landing = tick;
                bool close = Landing >= 0 && OnFloor(body, lower) && Math.Abs(owner.Bottom.X - body.Bottom.X) <= 26 &&
                    Math.Abs(owner.velocity.X) < .01f && Math.Abs(body.velocity.X) < .65f;
                Stable = close ? Stable + 1 : 0;
                if (FirstSettled < 0 && resume < 0 && Stable == 24) FirstSettled = tick;
                if (resume >= 0 && Resumed < 0 && body.controlLeft && before.X - body.position.X > .5f) Resumed = tick;
            }

            internal bool Result(int upper, int lower, int resume) =>
                Reverse >= 0 && Exit > Reverse && Landing > Exit && FirstSettled > Landing && resume > FirstSettled &&
                Resumed >= resume && Stable >= 28 && farthestRetreat >= 600 && maxStep < 16 && OnFloor(body, lower) &&
                !OnFloor(body, upper) && body.statLife == life && body.wingsLogic == 0 && body.mount.Type < 0 && Faults == 0;

            internal void Report(string key) =>
                Console.WriteLine(FormattableString.Invariant($"DETOUR {key} reverse={Reverse} exit={Exit} landing={Landing} settled={FirstSettled} resumed={Resumed} stable={Stable} withoutGrant={WithoutGrant} anchors={Anchors} retreat={farthestRetreat:F1} maxStep={maxStep:F2} faults={Faults} {SearchState()} {ActionState()}"));

            internal string SearchState()
            {
                object search = GetOptional(Navigation, "search");
                if (search == null) return "noSearch";
                var nodes = (IList)Get(search, "Nodes");
                var terrain = Get(search, "Terrain");
                float left = originX;
                int pending = 0, drops = 0;
                foreach (object node in nodes)
                {
                    if ((bool)(GetOptional(node, "Vacant") ?? false) || (bool)Get(node, "Invalid")) continue;
                    var pose = (Vector2)Get(node, "Position");
                    left = Math.Min(left, pose.X + body.width / 2f);
                    if ((bool)Get(node, "Pending")) pending++;
                    if (Get(Get(node, "Step"), "Kind").ToString() == "LedgeDrop") drops++;
                }
                return FormattableString.Invariant($"nodes={GetOptional(search, "LiveCount") ?? nodes.Count}/{nodes.Count},left={left:F1},pending={pending},drops={drops},dominated={GetOptional(search, "DominatedCount") ?? 0},window={Get(search, "Window")},reads={((IList)Get(terrain, "Cells")).Count},active={Get(search, "Active")},closed={Get(search, "Exhausted")},pressure={GetOptional(search, "CapacityLimited") ?? false},starts={GetOptional(Navigation, "RouteSearchStarts") ?? -1},edges={GetOptional(Navigation, "RouteCandidateChecks") ?? -1},publish={GetOptional(Navigation,"publishing") ?? -1},result={GetOptional(Navigation,"pendingResult") ?? -1}");
            }

            internal string ActionState()
            {
                var blocked = Get(Navigation, "blockedStep");
                var steps = (IList)Get(Navigation, "route");
                int next = (int)Get(Navigation, "routeIndex") + 1;
                object action = next < steps.Count ? steps[next] : null;
                string suffix = action == null ? "none" : $"{Get(action,"Kind")}:{Get(action,"From")}→{Get(action,"To")}";
                return $"status={Get(actor, "Status")},next={suffix},lastRejected={Get(blocked,"Kind")}:{Get(blocked,"From")}→{Get(blocked,"To")}@{Get(Navigation,"blockedAt")},tick={Main.GameUpdateCount},advancing={Get(Navigation,"advancing")}";
            }
        }

        private static void BoundedTerrainDependencies(object host, object profiles, Player human)
        {
            using (var run = new Scenario(host, profiles, human, "detour-dependency", 65 * 16, 82,
                () => Fill(50, 80, 82, TileID.Stone)))
            {
                Type nav = Navigator(run.Actor).GetType();
                var type = nav.GetNestedType("ReadSet", BindingFlags.NonPublic);
                var reads = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { true }, null);
                Call(reads, "Clear");
                for (int i = 0; i < 2048; i++)
                    Require((bool)Call(reads, "Record", 40 + i % 50, 45 + i / 50), "each isolated native tile is known");
                Require((bool)Call(reads, "Record", 40, 45) && !(bool)Get(reads, "Truncated"),
                    "filling the finite read set does not make an already watched terrain cell unknown");
                var replace = type.GetMethod("TakeEvictedArea", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(replace != null && (bool)Call(reads, "Record", 91, 78), "a new loaded dependency has an explicit bounded replacement path");
                var retired = (Rectangle)replace.Invoke(reads, null);
                Require(retired == new Rectangle(40, 45, 1, 1) && ((IList)Get(reads, "Cells")).Count == 2048,
                    "the replaced dependency identifies precisely the old watched cell");
                Main.tile[91, 78].active(true);
                Main.tile[91, 78].type = TileID.Stone;
                object[] scan = { 0, 1 };
                var changed = (Rectangle)Call(reads, "Changes", scan);
                Require(changed == new Rectangle(91, 78, 1, 1), "the new watched cell records a real later terrain edit");
                Tile watched = Main.tile[41, 45];
                watched.active(true);
                watched.type = TileID.Stone;
                Require((bool)Call(reads, "Record", 41, 45), "the original read set observes the new solid native tile");
                watched.active(false);
                var intervening = (Rectangle)replace.Invoke(reads, null);
                Require(intervening == new Rectangle(41, 45, 1, 1),
                    "a sampled intermediate edit retires older route proof even when the cell is restored before a rolling read");
                Console.WriteLine($"DETOUR native read boundary watched=2048 retired={retired} changed={changed} intermediate={intervening}");
                VerifyFrontierPressure(nav, run.Body.position, retired);
            }
        }

        private static void VerifyFrontierPressure(Type nav, Vector2 root, Rectangle lostDependency)
        {
            Type searchType = nav.GetNestedType("RouteSearch", BindingFlags.NonPublic);
            Type nodeType = nav.GetNestedType("RouteNode", BindingFlags.NonPublic);
            var ground = Enum.Parse(nav.GetNestedType("RouteMotion", BindingFlags.NonPublic), "Ground");
            object search = Activator.CreateInstance(searchType, nonPublic: true);
            Call(search, "Begin", root, root + new Vector2(720, 160), ground, 0, 0, 0f, 1f, 256, 0);
            var nodes = (IList)Get(search, "Nodes");

            object Node(Vector2 position, int previous, int fuel, float cost, Rectangle reads)
            {
                object node = Activator.CreateInstance(nodeType);
                Set(node, "Position", position);
                Set(node, "Previous", previous);
                Set(node, "Root", previous < 0 ? -1 : 0);
                Set(node, "Motion", ground);
                Set(node, "Fuel", fuel);
                Set(node, "Cost", cost);
                Set(node, "EdgeReads", reads);
                return node;
            }

            int Add(object node)
            {
                object[] args = { node, -1 };
                return Call(search, "Add", args).ToString() == "Added" ? (int)args[1] : -1;
            }

            Vector2 projected = root + new Vector2(16, 0);
            int old = Add(Node(projected, 0, 16, 16, lostDependency));
            Require(old >= 0 && Add(Node(projected, 0, 8, 22, lostDependency)) < 0,
                "same physical phase with less resource and higher cost leaves no duplicate candidate");
            Call(search, "Dirty", lostDependency);
            while ((bool)Get(search, "RepairPending")) Call(search, "Repair", 40);
            Require((bool)Get(nodes[old], "Invalid") && Add(Node(projected, 0, 8, 22, lostDependency)) >= 0,
                "a branch that has lost an actual watched terrain dependency cannot dominate a fresh proof");

            // Synthetic frontier load, using the same arena and work methods.
            // Positions are distinct within the local loaded support span.
            for (int i = (int)Get(search, "LiveCount"); i < 768; i++)
                Require(Add(Node(root + new Vector2(i * .25f + 24, 0), -1, 0, i, Rectangle.Empty)) >= 0,
                    "the existing frontier retains its admissible slots");
            object extra = Node(root + new Vector2(260, 0), -1, 0, 260, Rectangle.Empty);
            Require(Add(extra) < 0 && (bool)Get(search, "CapacityLimited") && (bool)Get(search, "CanExpand") &&
                !(bool)Get(search, "FullyKnownClosure"), "capacity pressure is separate from a proved closed graph");
            Call(search, "DeferCapacity", 0);
            int survivor = nodes.Count - 1, pruned = -1;
            for (int i = 0; i < 4; i++)
            {
                object[] spend = { 160 };
                int expanded = (int)Call(search, "Next", spend);
                if (expanded > 1 && expanded != survivor) pruned = expanded;
            }
            Require(pruned >= 0 && (bool)Get(nodes[survivor], "Pending"), "a retained queue still yields real expansion work");
            for (int i = 0; i < 33 && (int)Get(search, "LiveCount") == 768; i++)
            {
                object[] spend = { 24, -1, -1, -1 };
                Call(search, "Reclaim", spend);
            }
            Require(Add(extra) >= 0 && nodes.Count == 768 && (bool)Get(nodes[survivor], "Pending") &&
                (Vector2)Get(nodes[0], "Position") == root, "closed leaves repay storage while the original root and open entries survive");
            object[] readmitted = { 25, -1, -1, -1 };
            Call(search, "Reclaim", readmitted);
            for (int slice = 0; slice < 8 && (bool)Get(search, "CanExpand"); slice++)
            {
                object[] spend = { 160 };
                while ((int)Call(search, "Next", spend) >= 0) { }
            }
            Call(search, "WaitForChange", 100u);
            Require(!(bool)Get(search, "Active") && !(bool)Get(search, "Exhausted") && !(bool)Get(search, "CanExpand"),
                "after the retained work drains, a formerly capacity-limited graph rests without claiming a closed graph");
            Call(search, "RequestRoot");
            Call(search, "WaitForChange", 101u);
            Require((bool)Get(search, "Active") && (bool)Get(search, "CanExpand") && (Vector2)Get(nodes[0], "Position") == root,
                "requested live-root recheck wakes the same retained frontier");
            Console.WriteLine($"DETOUR frontier capacity={nodes.Count} live={Get(search,"LiveCount")} equivalents={Get(search,"DominatedCount")} resumed=True pausedAfterDrain=True");
        }
    }
}
