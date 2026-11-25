using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed class NavigationReadSet
    {
        private readonly NavigationWorld world;
        private readonly Dictionary<Point, int> chunks = new Dictionary<Point, int>();
        private readonly List<Point> order = new List<Point>();
        private int checkedRevision;
        private int probeChunk, probeCell;
        private bool changed;

        internal NavigationReadSet(NavigationWorld world)
        {
            this.world = world;
            checkedRevision = world.Revision;
        }

        internal void Record(Point cell)
        {
            if (!world.TryVersion(cell, out Point chunk, out int version)) return;
            if (!chunks.TryGetValue(chunk, out int previous))
            {
                chunks.Add(chunk, version);
                order.Add(chunk);
            }
            else changed |= previous != version;
        }

        internal bool ProbeChange(NavigationBudget budget, int maxProbes = 12)
        {
            if (!Current()) return true;
            if (order.Count == 0) return false;
            const int size = NavigationWorld.ChunkSize;
            for (int probe = 0; probe < maxProbes; probe++)
            {
                var origin = order[probeChunk];
                var cell = new Point(origin.X + probeCell % size, origin.Y + probeCell / size);
                if (!world.TryVerify(cell, budget, out var edited)) return changed;
                changed |= edited;
                if (++probeCell != size * size) continue;
                probeCell = 0;
                probeChunk = (probeChunk + 1) % order.Count;
            }
            return changed;
        }

        internal bool Current()
        {
            if (changed) return false;
            if (checkedRevision == world.Revision) return true;
            checkedRevision = world.Revision;
            // An evicted coarse block is reconstructible. Re-adoption still
            // rechecks the real action envelope. A rebuilt or modified block
            // invalidates this query only if it previously read that block.
            foreach (var pair in chunks)
            {
                if (world.TryVersion(pair.Key, out _, out int version) && version != pair.Value)
                {
                    changed = true;
                    return false;
                }
            }
            return true;
        }
    }

    // Goal changes create a fresh OPEN, best labels and parent tree. The
    // shared terrain survives generation changes and never stores CLOSED.
    internal sealed class NavigationQuery
    {
        private const int NodeLimit = 1800;
        private const int LabelLimit = 4200;

        private sealed class Label
        {
            internal NavigationNode Node;
            internal Label Parent;
            internal NavigationTransition Edge;
            internal float Cost, Estimate, TrailDistance;
            internal int Sequence;
            internal int RemainingEnergy => Node.Energy;
        }

        private sealed class OpenOrder : IComparer<Label>
        {
            internal static readonly OpenOrder Instance = new OpenOrder();
            public int Compare(Label a, Label b)
            {
                int score = (a.Cost + a.Estimate).CompareTo(b.Cost + b.Estimate);
                if (score != 0) return score;
                int heuristic = a.Estimate.CompareTo(b.Estimate);
                if (heuristic != 0) return heuristic;
                int trail = a.TrailDistance.CompareTo(b.TrailDistance);
                return trail != 0 ? trail : a.Sequence.CompareTo(b.Sequence);
            }
        }

        private readonly SortedSet<Label> open = new SortedSet<Label>(OpenOrder.Instance);
        private readonly Dictionary<NavigationNode, List<Label>> labels = new Dictionary<NavigationNode, List<Label>>();
        private readonly IReadOnlyList<NavigationTransition> facilities;
        private readonly NavigationTraversal traversal;
        private readonly NavigationCapabilities capabilities;
        private readonly NavigationReadSet readSet;
        private readonly Point trail;
        private readonly bool hasTrail;
        private Label current, closest, frontier, result;
        private int option, branch, sequence, expanded;
        private bool unknown, capacity;
        private float closestDistance;
        internal readonly NavigationNode Start, Goal;
        private readonly NavigationNode observedGoal;
        private readonly Vector2? actualStart, actualGoal;
        internal Vector2? GoalPose => Goal.EqualsRegion(observedGoal) ? actualGoal : null;
        internal bool Valid => readSet.Current();
        internal NavigationReadSet Dependencies => readSet;
        internal NavigationQueryStatus Status { get; private set; }

        internal NavigationQuery(NavigationTraversal traversal, NavigationCapabilities capabilities,
            NavigationNode start, NavigationNode goal, IReadOnlyList<NavigationTransition> facilities,
            NavigationWorld world, Vector2 trailHint, bool hasTrail, NavigationNode? observedGoal = null,
            Vector2? actualStart = null, Vector2? actualGoal = null)
        {
            this.traversal = traversal;
            this.capabilities = capabilities;
            this.facilities = capabilities.Has(NavigationModes.Facility) ? facilities : null;
            readSet = new NavigationReadSet(world);
            this.hasTrail = hasTrail;
            trail = NavigationTraversal.CellAt(trailHint);
            Start = start;
            Goal = goal;
            this.observedGoal = observedGoal ?? goal;
            this.actualStart = actualStart;
            this.actualGoal = actualGoal;
            readSet.Record(start.Cell);
            readSet.Record(goal.Cell);
            readSet.Record(this.observedGoal.Cell);
            closest = Add(start, null, default, 0);
            closestDistance = GoalDistance(start);
            Status = NavigationQueryStatus.PendingBudget;
        }

        internal void Advance(NavigationBudget budget, Predicate<NavigationTransition> rejected)
        {
            if (Status != NavigationQueryStatus.PendingBudget) return;
            int candidates = NavigationTraversal.CandidateCount(capabilities);
            int count = candidates + (facilities?.Count ?? 0);
            while (budget.ExpansionsRemaining > 0)
            {
                if (current == null)
                {
                    if (open.Count == 0 || expanded >= NodeLimit || sequence >= LabelLimit)
                    {
                        capacity = open.Count != 0;
                        Finish();
                        return;
                    }
                    current = open.Min;
                    open.Remove(current);
                    option = -1;
                    branch = NavigationTraversal.FirstBranch;
                    expanded++;
                    budget.RecordExpansion();
                }
                traversal.Begin(capabilities, budget, readSet);
                if (option < 0)
                {
                    budget.TryExpand();
                    bool projected = !Goal.EqualsRegion(observedGoal);
                    var goalFeet = projected ? traversal.Feet(Goal) : actualGoal ?? traversal.Feet(Goal);
                    // Only the navigator knows whether its live body is
                    // already in follow range. The root has no action to offer.
                    bool accepted = current.Parent != null && (traversal.GoalEndpoint(current.Node, Goal, goalFeet) ||
                        projected && traversal.GoalEndpoint(current.Node, observedGoal, actualGoal ?? traversal.Feet(observedGoal)));
                    if (traversal.Pending) return;
                    unknown |= traversal.Unknown;
                    if (accepted)
                    {
                        result = current;
                        Status = NavigationQueryStatus.ReadyPrefix;
                        return;
                    }
                    // The connected part of the destination support band is
                    // an ordinary graph edge. Publishing its label avoids
                    // expanding every interior point on a traversable run.
                    bool linked = traversal.GroundGoalLink(current.Node, Goal, out var goalNode,
                        out var groundEdge, out float groundCost, current.Parent == null ? actualStart : null, goalFeet);
                    if (traversal.Pending) return;
                    if (linked && !rejected(groundEdge))
                    {
                        // The precise last step can share a coarse label
                        // with its parent. Do not let label dominance erase it.
                        result = new Label { Node = goalNode, Parent = current, Edge = groundEdge,
                            Cost = current.Cost + TravelCost(groundEdge, groundCost) };
                        Status = NavigationQueryStatus.ReadyPrefix;
                        return;
                    }
                    if (current.Parent == null && TryLocalFacility(current, budget, rejected))
                    {
                        Status = NavigationQueryStatus.ReadyPrefix;
                        return;
                    }
                    option = 0;
                    continue;
                }
                if (option < candidates && !traversal.IsApplicable(current.Node, option))
                {
                    if (++option == count) current = null;
                    continue;
                }
                budget.TryExpand();
                budget.RecordCandidate();
                bool offered = option < candidates
                    ? traversal.OfferNext(current.Node, option, ref branch, out var destination, out var edge, out var cost)
                    : TryFacility(current.Node, option - candidates, out destination, out edge, out cost);
                if (traversal.Pending) return;
                unknown |= traversal.Unknown;
                if (offered && !rejected(edge)) Add(destination, current, edge, current.Cost + TravelCost(edge, cost));
                if (branch != NavigationTraversal.FirstBranch) continue;
                if (++option == count) current = null;
            }
        }

        private bool TryLocalFacility(Label source, NavigationBudget budget, Predicate<NavigationTransition> rejected)
        {
            if (facilities == null || !source.Node.Grounded) return false;
            for (int i = 0; i < facilities.Count && budget.TryExpand(); i++)
            {
                budget.RecordCandidate();
                if (!TryFacility(source.Node, i, out var destination, out var edge, out float cost) || rejected(edge))
                    continue;
                var approach = edge;
                var inlet = destination;
                float approachCost = cost;
                bool walksToInlet = edge.Kind == NavigationAction.Walk;
                if (walksToInlet &&
                    (!TryFacility(inlet, i, out destination, out edge, out cost) || edge.Kind != NavigationAction.Facility ||
                        rejected(edge))) continue;
                // An already-qualified entrance near this body can connect
                // all the way to the current goal region. It is an ordinary
                // path with its own approach and native facility proof.
                if (!traversal.IsGoal(destination, Goal) && !traversal.SameGroundRegion(destination, Goal)) continue;
                var head = walksToInlet ? Add(inlet, source, approach, approachCost) : source;
                result = Add(destination, head, edge, head.Cost + cost);
                return true;
            }
            return false;
        }

        internal void CopyCorridor(List<NavigationTransition> corridor)
        {
            corridor.Clear();
            for (var label = result; label?.Parent != null; label = label.Parent)
                corridor.Add(label.Edge);
            corridor.Reverse();
        }

        private Label Add(NavigationNode node, Label parent, NavigationTransition edge, float cost)
        {
            // Only equal portal/body-mode envelopes are comparable. A lower
            // cost with less flight time cannot dominate an otherwise legal label.
            var portal = new NavigationNode(node.Cell, node.Medium, node.Gravity, node.Mounted, 0,
                node.MountMode, node.RailFrame, node.OffsetX, node.MountChoice);
            if (!labels.TryGetValue(portal, out var peers))
            {
                peers = new List<Label>(1);
                labels.Add(portal, peers);
            }
            for (int i = peers.Count - 1; i >= 0; i--)
            {
                var peer = peers[i];
                if (peer.Cost <= cost && peer.RemainingEnergy >= node.Energy) return peer;
                if (cost <= peer.Cost && node.Energy >= peer.RemainingEnergy)
                {
                    open.Remove(peer);
                    peers.RemoveAt(i);
                }
            }
            float distance = GoalDistance(node);
            var label = new Label
            {
                Node = node, Parent = parent, Edge = edge, Cost = cost,
                Estimate = facilities?.Count > 0 ? 0 : Math.Max(0, distance - 3f),
                TrailDistance = hasTrail ? Math.Abs(node.Cell.X - trail.X) + Math.Abs(node.Cell.Y - trail.Y) : 0f,
                Sequence = sequence++
            };
            peers.Add(label);
            open.Add(label);
            // A grounded exit is independently useful when this body can
            // survive reaching it. Native refinement owns that life proof.
            bool decisionBoundary = !edge.Irreversible || node.Grounded;
            if (parent != null && distance < closestDistance && decisionBoundary)
            {
                closest = label;
                closestDistance = distance;
            }
            if (parent != null && decisionBoundary &&
                (frontier == null || label.Cost + label.Estimate < frontier.Cost + frontier.Estimate) &&
                Distance(node, Start) >= 5f) frontier = label;
            return label;
        }

        private bool TryFacility(NavigationNode from, int index, out NavigationNode to, out NavigationTransition edge, out float cost)
        {
            edge = facilities[index];
            to = default;
            cost = 0;
            var feet = traversal.Feet(from);
            float distance = Vector2.DistanceSquared(feet, edge.From);
            if (distance > 30f * 30f)
            {
                // Link the ordinary supported approach to an already
                // qualified entrance. Its independent facility edge stays
                // in the graph and must still get its own native receipt.
                if (!from.Grounded || distance > 128f * 128f) return false;
                var entry = traversal.Anchor(edge.From, true);
                return traversal.GroundGoalLink(from, entry, out to, out edge, out cost);
            }
            if (!traversal.KnownClear(feet, edge.From, from.Gravity, from.MountMode, 3, from.MountChoice)) return false;
            to = traversal.Anchor(edge.To, true);
            cost = 12;
            return true;
        }

        private float TravelCost(NavigationTransition edge, float cost) =>
            Start.Medium == NavigationMedium.Track && edge.Detail != NavigationTraversalDetail.TrackRide
                ? cost / NavigationRail.RelativeTravelCost : cost;

        private void Finish()
        {
            // A discoverable frontier is a checkpoint, never a claim that
            // the target is reached or that the omitted area cannot connect.
            if (closest?.Parent != null || capacity && frontier != null)
            {
                result = closest?.Parent != null ? closest : frontier;
                Status = NavigationQueryStatus.NeedsMoreRegion;
                return;
            }
            Status = capacity ? NavigationQueryStatus.NeedsMoreRegion : unknown
                ? NavigationQueryStatus.AwaitingWorldData : NavigationQueryStatus.NoRouteInKnownGraph;
        }

        private static float Distance(NavigationNode a, NavigationNode b)
        {
            float dx = a.Cell.X - b.Cell.X, dy = a.Cell.Y - b.Cell.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private float GoalDistance(NavigationNode node) =>
            Math.Min(Distance(node, Goal), Distance(node, observedGoal));
    }
}
