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
        private const int RouteCandidates = 29;
        private enum RouteMotion : byte { Ground, Air, Rope, Track, Liquid, Hooked }
        private enum RouteKind : byte
        {
            Walk, Jump, Drop, Descend, Ascend, AirAcross, Rope, RopeExit, Track, Swim,
            Hook, ReleaseHook, Mount, Dismount, Cart, Door, Rod, Pylon, Portal, FlipGravity, Teleporter
        }

        private struct RouteStep
        {
            internal RouteKind Kind;
            internal Vector2 From, To, Operand;
            internal int Direction, Vertical, Duration, ItemSlot, MountTypePlusOne;
            internal float Cost;
            internal bool EmergencyHand;
        }

        private struct RouteNode
        {
            internal Vector2 Position;
            internal RouteMotion Motion;
            internal int Fuel, GlideFuel, Previous;
            internal bool Observe;
            internal RouteStep Step;
            internal float Cost, Score;
        }

        private sealed class RouteSearch
        {
            internal readonly ReadSet Terrain = new ReadSet(retainUnknown: true);
            internal readonly List<RouteNode> Nodes = new List<RouteNode>();
            private readonly List<int> heap = new List<int>();
            private readonly Dictionary<long, int> cheapest = new Dictionary<long, int>();
            internal Vector2 Origin, Goal;
            internal int Expanding, Candidate, Closest, TerrainCursor, Window, Capabilities;
            internal uint RetryAt;
            internal bool Active, Exhausted;

            internal void Begin(Vector2 origin, Vector2 goal, RouteMotion motion, int fuel, int glideFuel, int window, int capabilities)
            {
                Nodes.Clear(); heap.Clear(); cheapest.Clear(); Terrain.Clear();
                Origin = origin; Goal = goal; Window = window;
                Capabilities = capabilities;
                Expanding = -1; Candidate = Closest = TerrainCursor = 0;
                Active = true; Exhausted = false;
                Add(new RouteNode { Position = origin, Motion = motion, Fuel = fuel, GlideFuel = glideFuel, Previous = -1, Score = Estimate(origin, goal) });
            }

            internal bool Add(RouteNode node)
            {
                long key = Key(node);
                if (cheapest.TryGetValue(key, out int previous) && Nodes[previous].Cost <= node.Cost) return false;
                int index = Nodes.Count;
                Nodes.Add(node);
                cheapest[key] = index;
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
                return true;
            }

            internal int Next(ref int work)
            {
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
                    if (cheapest[Key(Nodes[index])] == index) return index;
                }
                return -1;
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
        private int routeIndex;

        private static float Estimate(Vector2 position, Vector2 target)
        { return Math.Abs(position.X - target.X) + Math.Abs(position.Y - target.Y) * 1.4f; }

        private static bool Reached(Vector2 position, Vector2 target, int distance)
        { return Math.Abs(position.X - target.X) <= distance && Math.Abs(position.Y - target.Y) <= 38; }

        private void SearchRoute(Player body, CompanionMobilitySnapshot mobility, Vector2 target, int stop, RouteMotion root)
        {
            if (search == null) search = new RouteSearch();
            // Snapshot targets use body top-left. A minor target move can use the
            // same frontier without invalidating work or resetting its queue age.
            bool changed = (search.Active || search.Exhausted) && search.Terrain.Changed(ref search.TerrainCursor, 32);
            int capabilities = RouteCapabilityKey(mobility, root);
            changed |= search.Capabilities != capabilities;
            bool moved = Vector2.DistanceSquared(body.position, search.Origin) > 36 * 36;
            bool targetMoved = Math.Abs(target.X - search.Goal.X) > 64 || Math.Abs(target.Y - search.Goal.Y) > 44;
            if (search.Exhausted && !changed && !moved && !targetMoved && unchecked((int)(Main.GameUpdateCount - search.RetryAt)) < 0) return;
            if (!search.Active || changed || moved || targetMoved)
            {
                int window = search.Exhausted && !moved && !targetMoved && !changed ? Math.Min(1024, search.Window * 2) : 256;
                search.Begin(body.position, target, root, FlightBudget(mobility), mobility.CarpetReady ? Math.Min(120, mobility.CarpetTime) : 0, window, capabilities);
#if DEBUG
                RouteSearchStarts++;
#endif
            }

            int remaining = SliceLimit;
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
                        FinishSearch(search.Closest, target);
                        return;
                    }
                    search.Candidate = 0;
                    if (search.Expanding != 0 && Reached(search.Nodes[search.Expanding].Position, target, stop) &&
                        InGoalRegion(body, search.Nodes[search.Expanding].Position, target, search.Terrain))
                    { FinishSearch(search.Expanding, target); return; }
                    if (search.Nodes[search.Expanding].Observe) { search.Expanding = -1; continue; }
                }

                int parent = search.Expanding;
                RouteNode node = search.Nodes[parent];
                int candidate = search.Candidate;
                if (!CanConsider(node, mobility, candidate))
                {
                    if (++search.Candidate == RouteCandidates) search.Expanding = -1;
                    continue;
                }
                int charge = WorkForCandidate(node.Motion, candidate);
                if (charge > remaining) break;
                remaining -= charge;
                candidates++;
                if (++search.Candidate == RouteCandidates) search.Expanding = -1;
#if DEBUG
                RouteCandidateChecks++;
                RouteWork += charge;
#endif
                if (!TryEdge(body, mobility, node, target, candidate, out RouteNode next, out bool observe) || RecentlyRejected(next.Step)) continue;
                if (!InPlayableBounds(body, next.Position)) continue;
                if (!observe && (Math.Abs(next.Position.X - search.Origin.X) > search.Window ||
                    Math.Abs(next.Position.Y - search.Origin.Y) > search.Window)) continue;
                next.Previous = parent;
                next.Cost = node.Cost + next.Step.Cost;
                next.Score = next.Cost + Estimate(next.Position, target);
                if (!search.Add(next)) continue;
                int index = search.Nodes.Count - 1;
                if (Estimate(next.Position, target) + 12 < Estimate(search.Nodes[search.Closest].Position, target)) search.Closest = index;
                if (!observe && next.Motion == RouteMotion.Ground &&
                    Math.Abs(search.Origin.Y - target.Y) - Math.Abs(next.Position.Y - target.Y) >= 48 &&
                    Estimate(search.Origin, target) - Estimate(next.Position, target) >= 32)
                {
                    // A proven landing on the required higher/lower terrain
                    // is usable now. Resume search from the real landing pose
                    // without making the body wait for the last remote segment.
                    FinishSearch(index, target, confirmedPrefix: true);
                    return;
                }
                if (observe)
                {
                    // Tool activation or a change of body size must be observed
                    // before searching from the resulting real state. Queue the
                    // legal prefix once it is a real route alternative.
                    if (Reached(next.Position, target, stop) && InGoalRegion(body, next.Position, target, search.Terrain) ||
                        search.Closest == index && next.Step.Kind != RouteKind.Mount && next.Step.Kind != RouteKind.Dismount)
                    { FinishSearch(index, target, confirmedPrefix: true); return; }
                }
            }
        }

        private static int RouteCapabilityKey(CompanionMobilitySnapshot mobility, RouteMotion mode)
        {
            unchecked
            {
                int result = (int)mode;
                result = result * 31 + mobility.Width; result = result * 31 + mobility.Height;
                result = result * 31 + mobility.GravityDirection.GetHashCode();
                result = result * 31 + mobility.Gravity.GetHashCode();
                result = result * 31 + mobility.JumpSpeed.GetHashCode();
                result = result * 31 + mobility.RunAcceleration.GetHashCode();
                result = result * 31 + mobility.MountType; result = result * 31 + mobility.NarrowMountType;
                result = result * 31 + mobility.FlightMountType; result = result * 31 + mobility.WaterMountType;
                result = result * 31 + mobility.Wings; result = result * 31 + mobility.HookType;
                result = result * 31 + mobility.RocketBoots; result = result * 31 + mobility.Carpet.GetHashCode();
                return result * 31 + mobility.CanUseRod.GetHashCode();
            }
        }

        private static bool CanConsider(RouteNode node, CompanionMobilitySnapshot mobility, int candidate)
        {
            switch (candidate)
            {
                case 0: case 1: case 5: case 6: case 7: return node.Motion == RouteMotion.Ground;
                case 2: case 3: case 4: return node.Motion == RouteMotion.Ground || node.Motion == RouteMotion.Rope;
                case 8: case 9: case 10: case 11:
                    return node.Motion == RouteMotion.Liquid || node.Motion == RouteMotion.Air || node.Fuel > 0 ||
                        node.GlideFuel > 0 || mobility.UnlimitedWings || mobility.UnlimitedMountHover;
                case 14: case 15: return node.Motion == RouteMotion.Track || mobility.CanUseCart;
                case 18: case 19: case 27: return node.Previous < 0 && mobility.HookRange > 0;
                case 20: return node.Previous < 0 && (mobility.MountType >= 0 || mobility.DefaultMountType >= 0);
                case 21: return mobility.CanChangeGravity;
                case 22: case 23: case 26: return node.Previous < 0;
                case 24: return node.Previous < 0 && mobility.CanUseRod;
                case 25: case 28: return node.Previous < 0 && node.Motion == RouteMotion.Hooked;
                default: return true;
            }
        }

        private void FinishSearch(int last, Vector2 target, bool confirmedPrefix = false)
        {
            search.Active = false;
            search.Exhausted = true;
            search.RetryAt = Main.GameUpdateCount + (uint)(search.Window < 1024 ? 12 : 150);
            if (last == 0 || Estimate(search.Origin, target) - Estimate(search.Nodes[last].Position, target) < 20 && !confirmedPrefix) return;
            route.Clear();
            routeIndex = 0;
            while (last > 0)
            {
                var node = search.Nodes[last];
                route.Add(node.Step);
                last = node.Previous;
            }
            route.Reverse();
            search.Exhausted = false;
        }

#if DEBUG
        internal int RouteSearchStarts { get; private set; }
        internal int RouteCandidateChecks { get; private set; }
        internal int RouteWork { get; private set; }
#endif
    }
}
