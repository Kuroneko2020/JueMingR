using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class CompanionNavigation
    {
        private const int SliceLimit = 160;
        private const int LocalNodes = 768;
        private const int RouteCandidates = 33;
        private enum RouteMotion : byte { Ground, Air, Rope, Track, Liquid, Hooked }
        private enum RouteKind : byte
        {
            Walk, Jump, Drop, Descend, Ascend, AirAcross, Rope, RopeExit, Track, Swim,
            Hook, ReleaseHook, Mount, Dismount, Cart, Door, Rod, Pylon, Portal, FlipGravity, Teleporter, GlideJump, LedgeDrop
        }

        private struct RouteStep
        {
            internal RouteKind Kind;
            internal Vector2 From, To, Operand;
            internal int Direction, Vertical, Duration, ItemSlot, MountTypePlusOne;
            internal float Cost;
            internal bool EmergencyHand, DescentLands;
            internal Rectangle DescentArea;
            internal JumpInputs Launch;
        }

        private readonly struct JumpInputs : IEquatable<JumpInputs>
        {
            private readonly float gravity, direction, speed, acceleration, slowdown, runSpeed, fallSpeed;
            private readonly int hold, size;
            private readonly bool wet;

            internal JumpInputs(Player body, CompanionMobilitySnapshot observed)
            {
                gravity = body.gravity; direction = body.gravDir; speed = observed.JumpSpeed; hold = observed.JumpHeight;
                acceleration = body.runAcceleration; slowdown = body.runSlowdown; runSpeed = body.maxRunSpeed; fallSpeed = body.maxFallSpeed;
                size = body.width | body.height << 16;
                wet = observed.Wet;
            }

            internal bool Captured => size != 0;

            public bool Equals(JumpInputs other) => gravity == other.gravity && direction == other.direction && speed == other.speed &&
                acceleration == other.acceleration && slowdown == other.slowdown && runSpeed == other.runSpeed &&
                fallSpeed == other.fallSpeed && hold == other.hold && size == other.size && wet == other.wet;
        }

        private struct RouteNode
        {
            internal Vector2 Position;
            internal RouteMotion Motion;
            internal int Fuel, GlideFuel, Previous, Root, PriorityRevision;
            internal bool Observe, Pending, Invalid;
            internal Rectangle EdgeReads, ExpandedReads;
            internal RouteStep Step;
            internal float Cost, Score;
        }

        private sealed class RouteSearch
        {
            internal readonly ReadSet Terrain = new ReadSet(retainUnknown: true);
            internal readonly List<RouteNode> Nodes = new List<RouteNode>();
            private readonly List<int> heap = new List<int>();
            private readonly Dictionary<long, int> cheapest = new Dictionary<long, int>();
            internal Vector2 Origin, Goal, ExpansionGoal;
            internal RouteMotion OriginMotion;
            internal int OriginFuel, OriginGlideFuel;
            internal bool RodAvailable, AttachedAtRoot;
            internal JumpInputs OriginJumpInputs;
            private Vector2 priorityGoal, scoreOrigin;
            private int priorityRevision, nextRedirect;
            internal int Expanding, Candidate, Closest, TerrainCursor, Window, Capabilities;
            internal bool AscentFirst, DescentFirst;
            internal DescentProbe Descent;
            internal Rectangle DescentReads;
            private Rectangle repairing, queuedRepair;
            private int repairCursor;
            internal uint RetryAt;
            internal bool Active, Exhausted, NeedsLiveRoot, WindowLimited, AwaitsNativeChange;

            internal void Begin(Vector2 origin, Vector2 goal, RouteMotion motion, int fuel, int glideFuel, int window, int capabilities)
            {
                Nodes.Clear(); heap.Clear(); cheapest.Clear(); Terrain.Clear();
                Origin = scoreOrigin = origin; Goal = priorityGoal = goal; Window = window;
                Capabilities = capabilities;
                Expanding = Closest = -1;
                ResetDescent();
                Candidate = TerrainCursor = nextRedirect = repairCursor = 0;
                repairing = queuedRepair = Rectangle.Empty;
                Active = true; Exhausted = NeedsLiveRoot = false;
                WindowLimited = AwaitsNativeChange = false;
                Join(origin, motion, fuel, glideFuel);
            }

            internal void Join(Vector2 origin, RouteMotion motion, int fuel, int glideFuel)
            {
                Origin = origin;
                OriginMotion = motion;
                OriginFuel = fuel; OriginGlideFuel = glideFuel;
                var candidate = new RouteNode { Position = origin, Motion = motion, Fuel = fuel, GlideFuel = glideFuel, Previous = -1, Root = Nodes.Count };
                if (!Add(candidate)) Enqueue(cheapest[Key(candidate)]);
            }

            internal void Retarget(Vector2 goal)
            {
                Goal = goal;
                if (nextRedirect > 0) nextRedirect--;
                if (nextRedirect != 0 || Vector2.DistanceSquared(priorityGoal, goal) < 40 * 40) return;
                // Latest acceptance is immediate. Bounded lazy reprioritizing
                // lets a moving target redirect the queue without a heap rebuild.
                priorityGoal = goal;
                priorityRevision++;
                nextRedirect = 8;
                Closest = -1;
            }

            internal bool Add(RouteNode node)
            {
                long key = Key(node);
                if (cheapest.TryGetValue(key, out int previous) && !Nodes[previous].Invalid && Nodes[previous].Cost <= node.Cost) return false;
                int index = Nodes.Count;
                Nodes.Add(node);
                cheapest[key] = index;
                Enqueue(index);
                return true;
            }

            private void Enqueue(int index)
            {
                var node = Nodes[index];
                if (node.Pending || node.Invalid) return;
                node.Pending = true;
                node.PriorityRevision = priorityRevision;
                node.Score = node.Cost + Estimate(node.Position, priorityGoal) - Estimate(scoreOrigin, priorityGoal);
                Nodes[index] = node;
                int insertion = heap.Count;
                heap.Add(index);
                while (insertion > 0)
                {
                    int parent = (insertion - 1) / 2;
                    if (Nodes[heap[parent]].Score <= node.Score) break;
                    heap[insertion] = heap[parent];
                    insertion = parent;
                }
                heap[insertion] = index;
            }

            internal int Next(ref int work)
            {
                bool redirected = false;
                while (heap.Count != 0)
                {
                    if (work == 0) return -2;
                    work--;
                    int index = heap[0];
                    int last = heap.Count - 1;
                    int replacement = heap[last];
                    heap.RemoveAt(last);
                    int hole = 0;
                    while (hole * 2 + 1 < last)
                    {
                        int child = hole * 2 + 1;
                        if (child + 1 < last && Nodes[heap[child + 1]].Score < Nodes[heap[child]].Score) child++;
                        if (Nodes[replacement].Score <= Nodes[heap[child]].Score) break;
                        heap[hole] = heap[child];
                        hole = child;
                    }
                    if (last != 0) heap[hole] = replacement;
                    var node = Nodes[index];
                    node.Pending = false;
                    Nodes[index] = node;
                    if (node.Invalid || cheapest[Key(node)] != index) continue;
                    if (!redirected && node.PriorityRevision != priorityRevision)
                    { Enqueue(index); redirected = true; continue; }
                    return index;
                }
                return -1;
            }

            internal void Dirty(Rectangle area)
            {
                if (area.Width == 0) return;
                if (DescentReads.Intersects(area)) ResetDescent();
                queuedRepair = Cover(queuedRepair, area);
            }

            internal void ResetDescent()
            { Descent = default; DescentReads = Rectangle.Empty; }

            internal Rectangle RememberDescent(Rectangle reads)
            { return DescentReads = Cover(DescentReads, reads); }

            internal int Repair(int budget)
            {
                if (repairing.Width == 0)
                {
                    if (queuedRepair.Width == 0) return 0;
                    repairing = queuedRepair; queuedRepair = Rectangle.Empty; repairCursor = 0;
                }
                int used = 0;
                while (repairCursor < Nodes.Count && used < budget)
                {
                    int index = repairCursor++;
                    used++;
                    var node = Nodes[index];
                    if (node.Invalid) continue;
                    if (node.Previous >= 0 && (Nodes[node.Previous].Invalid || node.EdgeReads.Intersects(repairing)))
                    {
                        node.Invalid = true;
                        Nodes[index] = node;
                        Enqueue(node.Previous);
                    }
                    else if (node.ExpandedReads.Intersects(repairing)) Enqueue(index);
                    else continue;
                    if (Expanding == index) Expanding = -1;
                    if (Closest == index) Closest = -1;
                }
                if (repairCursor >= Nodes.Count) repairing = Rectangle.Empty;
                return used;
            }

            internal bool RepairPending => repairing.Width != 0 || queuedRepair.Width != 0;
            internal bool FullyKnownClosure => !WindowLimited && !AwaitsNativeChange && Nodes.Count < LocalNodes &&
                !Terrain.HasUnknown && !Terrain.Truncated;

            private static Rectangle Cover(Rectangle left, Rectangle right) => left.Width == 0 ? right :
                right.Width == 0 ? left : Rectangle.Union(left, right);

            internal void RecordExpansion(int parent, Rectangle reads)
            {
                var node = Nodes[parent];
                node.ExpandedReads = Cover(node.ExpandedReads, reads);
                Nodes[parent] = node;
            }

            private static long Key(RouteNode node)
            {
                // Use eight-pixel position cells so half-brick landings remain distinct.
                // Different movement modes and remaining resources are separate states.
                int x = (int)Math.Round(node.Position.X / 8);
                int y = (int)Math.Round(node.Position.Y / 8);
                return ((long)x << 32) + ((long)y << 16) + ((int)node.Motion << 11) + (node.Observe ? 1024 : 0) +
                    (node.GlideFuel / 8 << 4) + node.Fuel / 8;
            }
        }

        private RouteSearch search;
        private readonly List<RouteStep> route = new List<RouteStep>();
        private readonly List<int> connectionPath = new List<int>();
        private int routeIndex;
        private int publishing = -1, publicationCursor, connectCursor, lastRejectedPublication = -1;
        private Vector2 publicationRejectionPosition;

        private static float Estimate(Vector2 position, Vector2 target)
        { return Math.Abs(position.X - target.X) + Math.Abs(position.Y - target.Y) * 1.4f; }

        private static bool Reached(Vector2 position, Vector2 target, int distance)
        { return Math.Abs(position.X - target.X) <= distance && Math.Abs(position.Y - target.Y) <= 38; }

        private void SearchRoute(Player body, CompanionMobilitySnapshot mobility, Vector2 target, int stop, RouteMotion root, bool publish = true)
        {
            if (search == null) search = new RouteSearch();
            int capabilities = RouteCapabilityKey(mobility) * 31 + DescentSafetyKey(body);
            bool sameGeometry = search.Capabilities == capabilities;
            var changed = (search.Active || search.Exhausted) && sameGeometry ?
                search.Terrain.Changes(ref search.TerrainCursor, search.Exhausted ? 8 : 24) : Rectangle.Empty;
            // A landing creates new launch/ground states even when its exact
            // position is near the previous airborne planning root.
            bool moved = search.NeedsLiveRoot || root != search.OriginMotion ||
                root == RouteMotion.Hooked && mobility.GrappleAttached != search.AttachedAtRoot ||
                Vector2.DistanceSquared(body.position, search.Origin) > 42 * 42;
            int fuel = FlightBudget(mobility), glide = GlideBudget(mobility);
            var jumpInputs = new JumpInputs(body, mobility);
            bool changedAbility = search.Exhausted && (fuel > search.OriginFuel || glide > search.OriginGlideFuel ||
                mobility.CanUseRod && !search.RodAvailable || !search.OriginJumpInputs.Equals(jumpInputs));
            moved |= changedAbility;
            search.RodAvailable = mobility.CanUseRod;
            if (search.Exhausted && sameGeometry && !moved && changed.Width == 0 &&
                Vector2.DistanceSquared(target, search.Goal) < 48 * 48 &&
                (search.FullyKnownClosure || unchecked((int)(Main.GameUpdateCount - search.RetryAt)) < 0)) return;
            if ((!search.Active && !search.Exhausted) || !sameGeometry || search.Exhausted && search.Nodes.Count >= LocalNodes ||
                search.Exhausted && changed.Width == 0 && !moved)
            {
                RetirePublication();
                lastRejectedPublication = -1;
                int window = sameGeometry && search.Exhausted && !moved ? Math.Min(1024, search.Window * 2) : 256;
                search.Begin(body.position, target, root, fuel, glide, window, capabilities);
                search.OriginJumpInputs = jumpInputs;
#if DEBUG
                RouteSearchStarts++;
#endif
            }
            else
            {
                search.Dirty(changed);
                if (changed.Width != 0) { RetirePublication(); lastRejectedPublication = -1; }
                search.Retarget(target);
                // The actual body can establish an additional root while
                // earlier connections remain available to reconnect or repair.
                if (moved)
                {
                    search.Join(body.position, root, fuel, glide);
                    search.OriginJumpInputs = jumpInputs;
                    search.NeedsLiveRoot = false;
                }
                search.Active = true; search.Exhausted = false;
            }
            search.AttachedAtRoot = mobility.GrappleAttached;

            int remaining = SliceLimit;
            search.AwaitsNativeChange |= mobility.GrappleInFlight || mobility.HandBusy || mobility.RocketAvailable && !mobility.RocketReady ||
                postureChangedAt != 0 && unchecked(Main.GameUpdateCount - postureChangedAt) < 90;
            int repair = search.Repair(40);
            remaining -= repair;
#if DEBUG
            RouteWork += repair;
#endif
            if (search.RepairPending) return;
            if (publish && publishing >= 0 && ContinuePublication(body, root, ref remaining)) return;
            if (remaining <= 0 || publish && publishing >= 0) return;
            int candidates = 0;
            while (remaining > 0 && candidates < 24)
            {
                if (search.Expanding < 0)
                {
#if DEBUG
                    int before = remaining;
#endif
                    int nodeIndex = search.Next(ref remaining);
#if DEBUG
                    RouteWork += before - remaining;
#endif
                    if (nodeIndex == -2) return;
                    search.Expanding = nodeIndex;
                    if (search.Expanding < 0 || search.Nodes.Count >= LocalNodes)
                    {
                        FinishSearch(body, root, search.Closest, target, publish, ref remaining);
                        return;
                    }
                    search.Candidate = 0;
                    search.ResetDescent();
                    search.ExpansionGoal = target;
                    var expanded = search.Nodes[search.Expanding];
                    search.AscentFirst = expanded.Motion == RouteMotion.Ground && expanded.Fuel > 0 &&
                        (target.Y - expanded.Position.Y) * body.gravDir < -40;
                    search.DescentFirst = expanded.Motion == RouteMotion.Ground &&
                        (target.Y - expanded.Position.Y) * body.gravDir > 40;
                    if (publish && expanded.Previous >= 0 && Reached(expanded.Position, target, stop) &&
                        InGoalRegion(body, search.Nodes[search.Expanding].Position, target, search.Terrain))
                    { if (Propose(body, root, search.Expanding, ref remaining)) return; }
                    if (search.Nodes[search.Expanding].Observe) { search.Expanding = -1; continue; }
                }

                int parent = search.Expanding;
                RouteNode node = search.Nodes[parent];
                int candidate = SearchCandidate(search.Candidate, search.AscentFirst, search.DescentFirst);
                bool nearRoot = node.Previous < 0 && node.Motion == root && Vector2.DistanceSquared(node.Position, body.position) <= 16 * 16;
                if (!CanConsider(node, mobility, candidate, nearRoot))
                {
                    if (++search.Candidate == RouteCandidates) search.Expanding = -1;
                    continue;
                }
                int charge = WorkForCandidate(node.Motion, candidate);
                if (charge > remaining) break;
                remaining -= charge;
                candidates++;
#if DEBUG
                RouteCandidateChecks++;
                RouteWork += charge;
#endif
                search.Terrain.BeginArea();
                bool found = TryEdge(body, mobility, node, search.ExpansionGoal, candidate, out RouteNode next, out bool observe, out bool pending);
                next.EdgeReads = search.Terrain.EndArea();
                search.RecordExpansion(parent, next.EdgeReads);
                if (candidate >= 5 && candidate <= 7)
                {
                    next.EdgeReads = search.RememberDescent(next.EdgeReads);
                    if (pending) continue;
                    search.ResetDescent();
                }
                if (++search.Candidate == RouteCandidates) search.Expanding = -1;
                if (!found) continue;
                if (next.Step.Kind == RouteKind.Drop || next.Step.Kind == RouteKind.LedgeDrop)
                    next.Step.DescentArea = next.EdgeReads;
                if (RecentlyRejected(next.Step)) { search.AwaitsNativeChange = true; continue; }
                if (!InPlayableBounds(body, next.Position)) continue;
                var origin = search.Nodes[node.Root].Position;
                if (!observe && (Math.Abs(next.Position.X - origin.X) > search.Window ||
                    Math.Abs(next.Position.Y - origin.Y) > search.Window)) { search.WindowLimited = true; continue; }
                next.Previous = parent;
                next.Root = node.Root;
                next.Cost = node.Cost + next.Step.Cost;
                if (!search.Add(next)) continue;
                int index = search.Nodes.Count - 1;
                if (search.Closest < 0 || Estimate(next.Position, target) + 12 < Estimate(search.Nodes[search.Closest].Position, target)) search.Closest = index;
                if (publish && next.Step.Kind == RouteKind.GlideJump && Estimate(origin, target) - Estimate(next.Position, target) >= 96)
                {
                    // The far-bank landing is proved by this edge. Start the
                    // crossing without also requiring the owner's final path.
                    if (Propose(body, root, index, ref remaining)) return;
                }
                if (publish && !observe && next.Motion == RouteMotion.Ground &&
                    Math.Abs(origin.Y - target.Y) - Math.Abs(next.Position.Y - target.Y) >= 48 &&
                    Estimate(origin, target) - Estimate(next.Position, target) >= 32)
                {
                    // A proven landing on the required higher/lower terrain
                    // is usable now. Resume search from the real landing pose
                    // without making the body wait for the last remote segment.
                    if (Propose(body, root, index, ref remaining)) return;
                }
                if (publish && observe)
                {
                    // Tool activation or a change of body size must be observed
                    // before searching from the resulting real state. Queue the
                    // legal prefix once it is a real route alternative.
                    if (Reached(next.Position, target, stop) && InGoalRegion(body, next.Position, target, search.Terrain) ||
                        search.Closest == index && next.Step.Kind != RouteKind.Mount && next.Step.Kind != RouteKind.Dismount)
                    { if (Propose(body, root, index, ref remaining)) return; }
                }
            }
        }

        private static int SearchCandidate(int cursor, bool ascentFirst, bool descentFirst)
        {
            if (descentFirst) return cursor < 3 ? 5 + cursor : cursor < 8 ? cursor - 3 : cursor;
            if (!ascentFirst) return cursor;
            return cursor < 4 ? 8 + cursor : cursor < 12 ? cursor - 4 : cursor;
        }

        private static int RouteCapabilityKey(CompanionMobilitySnapshot mobility)
        {
            unchecked
            {
                int result = mobility.MountType;
                result = result * 31 + mobility.Width; result = result * 31 + mobility.Height;
                result = result * 31 + mobility.GravityDirection.GetHashCode();
                result = result * 31 + mobility.NarrowMountType;
                result = result * 31 + mobility.FlightMountType; result = result * 31 + mobility.WaterMountType;
                result = result * 31 + mobility.Wings; result = result * 31 + mobility.HookType;
                result = result * 31 + mobility.RocketBoots; result = result * 31 + mobility.Carpet.GetHashCode();
                return result;
            }
        }

        private static bool CanConsider(RouteNode node, CompanionMobilitySnapshot mobility, int candidate, bool nearRoot)
        {
            switch (candidate)
            {
                case 0: case 1: case 5: case 6: case 7: return node.Motion == RouteMotion.Ground;
                case 2: case 3: case 4: return node.Motion == RouteMotion.Ground || node.Motion == RouteMotion.Rope;
                case 8: case 9: case 10: case 11:
                    return node.Motion == RouteMotion.Liquid || node.Motion == RouteMotion.Air || node.Fuel > 0 ||
                        node.GlideFuel > 0 || mobility.UnlimitedWings || mobility.UnlimitedMountHover;
                case 14: case 15: return node.Motion == RouteMotion.Track || mobility.CanUseCart;
                case 18: case 19: case 27: return nearRoot && mobility.HookRange > 0;
                case 20: return nearRoot && (mobility.MountType >= 0 || mobility.DefaultMountType >= 0 || mobility.FlightMountType >= 0);
                case 21: return mobility.CanChangeGravity;
                case 22: case 23: case 26: return nearRoot;
                case 24: return nearRoot && mobility.CanUseRod;
                case 25: case 28: return nearRoot && node.Motion == RouteMotion.Hooked;
                case 29: case 30: return node.Motion == RouteMotion.Ground && node.GlideFuel > 0 && mobility.CarpetAvailable &&
                    !mobility.Wet && mobility.GravityDirection > 0 && mobility.MountType < 0 &&
                    mobility.ExtraJumps == 0 && mobility.WingTime == 0 && !mobility.RocketAvailable;
                case 31: case 32: return node.Motion == RouteMotion.Ground || node.Motion == RouteMotion.Air;
                default: return true;
            }
        }

        private void FinishSearch(Player body, RouteMotion motion, int last, Vector2 target, bool publish, ref int remaining)
        {
            search.Active = false;
            search.Exhausted = true;
            search.RetryAt = Main.GameUpdateCount + (uint)(search.Window < 1024 ? 12 : 150);
            if (publish && last >= 0 && Estimate(body.position, target) - Estimate(search.Nodes[last].Position, target) >= 20 &&
                Propose(body, motion, last, ref remaining) && publishing >= 0)
            { search.Active = true; search.Exhausted = false; }
        }

        private void RetirePublication()
        {
            connectionPath.Clear();
            publishing = -1;
        }

        private bool Propose(Player body, RouteMotion motion, int last, ref int remaining)
        {
            if (search.Nodes[last].Previous < 0 || last == lastRejectedPublication &&
                Vector2.DistanceSquared(body.position, publicationRejectionPosition) < 24 * 24) return false;
            publishing = publicationCursor = last;
            connectCursor = -1;
            ContinuePublication(body, motion, ref remaining);
            // One proposal per grant. Its path walk and the live connection
            // checks share the same budget as frontier expansion.
            return true;
        }

        private bool ContinuePublication(Player body, RouteMotion motion, ref int remaining)
        {
            const int ConnectCost = 48;
            while (publicationCursor >= 0 && remaining > 0)
            {
                ChargePublication(ref remaining, 1);
                var node = search.Nodes[publicationCursor];
                if (node.Invalid) { RetirePublication(); return false; }
                connectionPath.Add(publicationCursor);
                publicationCursor = node.Previous;
            }
            if (publicationCursor >= 0) return false;
            // The path was collected from the destination back to its root.
            // Reconnect to the furthest live-compatible suffix first so an
            // actor that already walked its prefix does not return to it.
            if (connectCursor < 0) connectCursor = 1;
            using (Scratch.Save())
            while (connectCursor < connectionPath.Count && remaining >= ConnectCost)
            {
                ChargePublication(ref remaining, 1);
                int i = connectCursor++;
                var node = search.Nodes[connectionPath[i]];
                Vector2 adjust = node.Position - body.position;
                if (node.Motion != motion || Math.Abs(adjust.Y) > 8 || Math.Abs(adjust.X) > ConnectCost) continue;
                ChargePublication(ref remaining, ConnectCost - 1);
                int direction = Math.Sign(adjust.X);
                if (motion == RouteMotion.Ground && Math.Abs(adjust.X) > 5)
                {
                    if (!Surface(body, node.Position, null) || !SupportedLane(body, direction, Math.Abs(adjust.X), steps: true, out _)) continue;
                }
                else if (Math.Abs(adjust.X) > 6 || !Swept(body, body.position, adjust, null)) continue;

                ReleaseRoute();
                if (motion == RouteMotion.Ground && Math.Abs(adjust.X) > 5)
                    route.Add(new RouteStep { Kind = RouteKind.Walk, From = body.position, To = node.Position, Direction = direction });
                int count = Math.Min(12, i);
                for (int step = i - 1; step >= i - count; step--) route.Add(search.Nodes[connectionPath[step]].Step);
                RetirePublication();
                return true;
            }
            if (connectCursor < connectionPath.Count) return false;
            lastRejectedPublication = publishing;
            publicationRejectionPosition = body.position;
            RetirePublication();
            return false;
        }

        private void ChargePublication(ref int remaining, int cost)
        {
            remaining -= cost;
#if DEBUG
            RouteWork += cost;
#endif
        }

#if DEBUG
        internal int RouteSearchStarts { get; private set; }
        internal int RouteCandidateChecks { get; private set; }
        internal int RouteWork { get; private set; }
#endif
    }
}
