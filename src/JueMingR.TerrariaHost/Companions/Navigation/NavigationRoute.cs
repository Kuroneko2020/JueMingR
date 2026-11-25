using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.Companions
{
    // One actual body owns an attachment, a corridor and at most one pending
    // search. A native commitment owns the input until a physical handoff.
    // Issuing reversible input alone does not pin the published corridor.
    internal sealed class NavigationRoute
    {
        private const int RetryWindow = 32;
        private const int MaximumFailures = 12;

        private readonly struct Failure
        {
            internal readonly NavigationTransition Edge;
            internal readonly NavigationCapabilities Capabilities;
            internal readonly int Until, Revision;
            internal readonly bool Geometric;

            internal Failure(NavigationTransition edge, NavigationCapabilities capabilities, int until, int revision, bool geometric)
            {
                Edge = edge;
                Capabilities = capabilities;
                Until = until;
                Revision = revision;
                Geometric = geometric;
            }
        }

        private readonly NavigationWorld world;
        private readonly NavigationTraversal traversal;
        private readonly List<NavigationTransition> corridor = new List<NavigationTransition>();
        private readonly List<NavigationTransition> candidate = new List<NavigationTransition>();
        private readonly List<Failure> failures = new List<Failure>(MaximumFailures);
        private readonly Predicate<NavigationTransition> rejectFilter;
        private IReadOnlyList<NavigationTransition> facilities = Array.Empty<NavigationTransition>();
        private NavigationQuery query;
        private NavigationReadSet waitingReads;
        private NavigationNode waitingStart, waitingGoal;
        private NavigationCapabilities waitingCaps;
        private NavigationQueryStatus waitingStatus;
        private int waitingRetry;
        private NavigationCapabilities capabilities;
        private NavigationNode corridorGoal;
        private NavigationTransition offer, checking;
        private int worldEpoch, corridorIndex, cursor, nextRetry, clock;
        private int offerIndex, offerEnd, checkIndex, checkEnd, verificationIndex;
        private int lastTraversal = -1;
        private bool known, offered, issued, committed, verifying, connector;
        private bool facilitiesChanged, skipFlightMerge;
        private Vector2 trailHint;
        private Vector2 targetMotion;
        private bool hasTrail, needFlightMount, targetAirborne;
        private NavigationTransition? localFixture;

        internal NavigationQueryStatus Status { get; private set; }
        internal int Generation { get; private set; }
        internal int CorridorLength => Math.Max(0, corridor.Count - corridorIndex);
        internal Point? QueryGoal => query?.Goal.Cell;

        internal NavigationRoute(NavigationWorld world)
        {
            this.world = world;
            traversal = new NavigationTraversal(world);
            rejectFilter = IsRejected;
        }

        internal void Reset()
        {
            known = false;
            query = null;
            waitingReads = null;
            corridor.Clear();
            candidate.Clear();
            failures.Clear();
            corridorIndex = cursor = nextRetry = 0;
            offered = issued = committed = verifying = false;
            skipFlightMerge = false;
            facilities = Array.Empty<NavigationTransition>();
            facilitiesChanged = false;
            hasTrail = false;
            needFlightMount = false;
            targetMotion = default;
            targetAirborne = false;
            localFixture = null;
            Status = NavigationQueryStatus.Idle;
        }

        internal void SetFacilities(IReadOnlyList<NavigationTransition> offers)
        {
            if (SameFacilities(facilities, offers)) return;
            int length = offers?.Count ?? 0;
            var snapshot = length == 0 ? Array.Empty<NavigationTransition>() : new NavigationTransition[length];
            for (int i = 0; i < length; i++) snapshot[i] = offers[i];
            facilities = snapshot;
            facilitiesChanged = true;
        }

        internal void SetTrailHint(Vector2 feet, bool exists)
        {
            trailHint = feet;
            hasTrail = exists;
        }

        internal void SetTargetMotion(Vector2 velocity)
        {
            targetMotion = velocity;
        }

        internal void SetTargetAirborne(bool airborne) => targetAirborne = airborne;

        internal void SetCommitted(bool value) => committed = issued && value;

        internal void SetLocalFixture(NavigationTransition? fixture)
        {
            localFixture = fixture;
        }

        internal void SetNeedFlightMount(bool needed)
        {
            needFlightMount = needed;
        }

        internal void Advance(Vector2 feet, Vector2 target, NavigationCapabilities current, NavigationBudget budget)
        {
            clock++;
            world.Synchronize();
            if (known && worldEpoch != world.Epoch)
            {
                var snapshot = facilities;
                Reset();
                facilities = snapshot;
            }
            if (!known || !capabilities.SameShape(current))
            {
                query = null;
                waitingReads = null;
                if (!committed) ClearCorridor();
            }
            capabilities = current;
            known = true;
            worldEpoch = world.Epoch;
            traversal.Begin(current, budget);
            NavigationNode start = traversal.Anchor(feet, current.Supported, actual: true);
            var observed = traversal.Anchor(target, !targetAirborne);
            bool unavailable = traversal.Unknown;
            NavigationNode goal = traversal.FallingGoal(target, targetMotion, start, observed);
            if (traversal.Pending)
            {
                Status = offered ? NavigationQueryStatus.ReadyPrefix : NavigationQueryStatus.PendingBudget;
                return;
            }
            if (!NavigationWorld.InWorld(start.Cell) || !NavigationWorld.InWorld(goal.Cell) || unavailable)
            {
                Status = offered ? NavigationQueryStatus.ReadyPrefix : NavigationQueryStatus.AwaitingWorldData;
                return;
            }

            var localCandidate = localFixture;
            bool sameCorridorGoal = corridor.Count != 0 && SameGoal(corridorGoal, goal);
            if (!issued && needFlightMount && traversal.LocalFlightMount(feet, start, rejectFilter, out var mount))
                localCandidate = mount;
            // A local release can feed the next layer even after the owner has
            // landed far to the side. Keep a usable corridor to the same goal.
            if (!issued && !localCandidate.HasValue && corridorIndex >= corridor.Count && start.Grounded)
            {
                Vector2 descentTarget = targetAirborne && goal.Grounded ? traversal.Feet(goal) : target;
                if (traversal.LocalPlatformExit(feet, descentTarget, start, rejectFilter, out var descent))
                    localCandidate = descent;
            }
            if (facilitiesChanged) query = null;
            if (facilitiesChanged) waitingReads = null;
            facilitiesChanged = false;
            if (waitingReads != null)
            {
                if (!localCandidate.HasValue && waitingStart.EqualsRegion(start) && SameGoal(waitingGoal, goal) &&
                    waitingCaps.SameFlightConditions(current) && WaitingFailuresCurrent(current) &&
                    clock < waitingRetry && !waitingReads.ProbeChange(budget))
                {
                    Status = waitingStatus;
                    return;
                }
                waitingReads = null;
            }

            if (!issued && localCandidate.HasValue && !IsRejected(localCandidate.Value))
            {
                var fixture = localCandidate.Value;
                bool alreadyHead = corridorIndex < corridor.Count && corridor[corridorIndex].Kind == fixture.Kind &&
                    corridor[corridorIndex].EntryTile == fixture.EntryTile &&
                    corridor[corridorIndex].ExitTile == fixture.ExitTile &&
                    corridor[corridorIndex].MountChoice.Equals(fixture.MountChoice) &&
                    Math.Sign(corridor[corridorIndex].To.X - corridor[corridorIndex].From.X) ==
                    Math.Sign(fixture.To.X - fixture.From.X);
                if (!alreadyHead)
                {
                    ClearCorridor();
                    AddCorridorEdge(fixture);
                    corridorGoal = goal;
                    sameCorridorGoal = true;
                }
            }
            if (!issued && (corridor.Count == 0 || !sameCorridorGoal))
            {
                bool localTerminal = traversal.LocalLevel(feet, start, goal, out var local) && !IsRejected(local);
                if (localTerminal || traversal.LocalDoor(feet, target, start, out local) && !IsRejected(local) ||
                    traversal.LocalTrack(feet, target, start, out local) && !IsRejected(local))
                {
                    ClearCorridor();
                    AddCorridorEdge(local);
                    corridorGoal = goal;
                    sameCorridorGoal = true;
                }
            }
            if (!issued && corridorIndex < corridor.Count)
            {
                Prepare(feet, budget);
                if (sameCorridorGoal && corridor.Count != 0)
                {
                    Status = offered ? NavigationQueryStatus.ReadyPrefix : NavigationQueryStatus.PendingBudget;
                    return;
                }
            }
            if (issued && sameCorridorGoal)
            {
                Status = NavigationQueryStatus.ReadyPrefix;
                return;
            }
            // A change can open a portal already examined by OPEN/CLOSED
            // while this query still has a distant frontier to expand.
            // Reserve most of the slice for search and audit its own read
            // set even before it reaches a waiting disposition.
            if (query != null && query.Dependencies.ProbeChange(budget, Math.Min(6, budget.TilesRemaining / 4)))
                query = null;
            if (query == null)
            {
                if (clock < nextRetry && SameGoal(corridorGoal, goal))
                {
                    if (issued) Status = NavigationQueryStatus.ReadyPrefix;
                    return;
                }
                query = new NavigationQuery(traversal, current, start, goal, facilities, world, trailHint, hasTrail, observed);
                Generation++;
            }
            query.Advance(budget, rejectFilter);
            Status = issued ? NavigationQueryStatus.ReadyPrefix : query.Status;
            if (!query.Valid)
            {
                query = null;
                return;
            }
            if (query.Status == NavigationQueryStatus.PendingBudget || committed || issued && sameCorridorGoal) return;
            var completedQuery = query;
            query.CopyCorridor(candidate);
            bool currentGoal = SameGoal(query.Goal, goal);
            if (!currentGoal)
            {
                // Allow an older result to supply a short approach to a
                // verified outlet even when that approach goes away from
                // the owner. A stale plain run has no such outlet evidence.
                int entry = candidate.FindIndex(edge => edge.Kind != NavigationAction.Walk && edge.Kind != NavigationAction.Dash);
                if (!SharesOutlet(candidate, entry)) candidate.Clear();
                else candidate.RemoveRange(entry + 1, candidate.Count - entry - 1);
            }
            query = null;
            if (candidate.Count == 0)
            {
                if (corridor.Count == 0 && currentGoal && (completedQuery.Status == NavigationQueryStatus.NoRouteInKnownGraph ||
                    completedQuery.Status == NavigationQueryStatus.AwaitingWorldData))
                {
                    // Retain only consulted chunks/conditions while a settled
                    // search waits. A scheduled terrain edit probe can wake
                    // this body without rebuilding the same A* labels.
                    waitingReads = completedQuery.Dependencies;
                    waitingStart = start;
                    waitingGoal = goal;
                    waitingCaps = current;
                    waitingStatus = completedQuery.Status;
                    waitingRetry = int.MaxValue;
                    foreach (var failure in failures)
                        if (failure.Until > clock && failure.Capabilities.SameFailureConditions(current, failure.Edge.Kind))
                            waitingRetry = Math.Min(waitingRetry, failure.Until);
                    nextRetry = 0;
                }
                else if (corridor.Count == 0) nextRetry = currentGoal ? clock + RetryWindow : 0;
                return;
            }
            if (issued && !HasNearbyEntry(feet, candidate)) return;
            ClearCorridor();
            foreach (var edge in candidate) AddCorridorEdge(edge);
            candidate.Clear();
            corridorGoal = goal;
            corridorIndex = cursor = 0;
            Prepare(feet, budget);
            Status = offered ? NavigationQueryStatus.ReadyPrefix : NavigationQueryStatus.PendingBudget;
        }

        private bool SharesOutlet(List<NavigationTransition> path, int entry)
        {
            if (entry < 0 || entry >= 8) return false;
            var outlet = path[entry];
            // Remote facilities are qualified against their query's target.
            // Reconsider them under the latest request through its own graph.
            if (outlet.Kind == NavigationAction.Facility) return false;
            if (corridorIndex >= corridor.Count) return true;
            for (int i = corridorIndex; i < Math.Min(corridorIndex + 8, corridor.Count); i++)
                if (SameFailure(corridor[i], outlet)) return true;
            return false;
        }

        private static bool HasNearbyEntry(Vector2 feet, List<NavigationTransition> path)
        {
            for (int i = 0; i < Math.Min(9, path.Count); i++)
                if (Vector2.DistanceSquared(feet, path[i].From) <= 42f * 42f) return true;
            return false;
        }

        internal bool TrySelect(Vector2 feet, out NavigationTransition transition)
        {
            transition = offer;
            if (!offered) return false;
            if (issued || Vector2.DistanceSquared(feet, offer.From) <= 42f * 42f) return true;
            offered = verifying = false;
            cursor = corridorIndex;
            return false;
        }

        internal void MarkIssued(NavigationTransition transition)
        {
            if (offered && offer.SameEdge(transition)) issued = true;
        }

        internal bool ContinuesWithForwardSpeed(NavigationTransition transition)
        {
            if (!offered || offer.Kind != NavigationAction.Walk || !offer.SameEdge(transition)) return false;
            int direction = Math.Sign(offer.To.X - offer.From.X);
            int index = connector ? offerIndex : offerIndex + 1;
            if (index >= corridor.Count) return false;
            var next = corridor[index];
            // Adjacent support labels preserve branches without requiring a
            // complete stop at each still-connected forward checkpoint.
            return direction != 0 && Math.Sign(next.To.X - next.From.X) == direction &&
                (next.Kind == NavigationAction.Walk || next.Detail == NavigationTraversalDetail.RunningJump);
        }

        internal bool IsTraversalApproach(NavigationTransition transition)
        {
            if (!offered || !offer.SameEdge(transition)) return false;
            return (connector ? offerIndex : offerIndex + (offer.Kind == NavigationAction.Walk ? 1 : 0)) <= lastTraversal;
        }

        internal bool TryTraversalApproach(Vector2 feet, out Vector2 approach)
        {
            approach = default;
            if (issued || corridorIndex >= corridor.Count) return false;
            var first = corridor[corridorIndex];
            if (Vector2.DistanceSquared(feet, first.From) > 42f * 42f || corridorIndex > lastTraversal)
                return false;
            // This is a steering waypoint while proof is pending. The live
            // Walk still sweeps and brakes on its own support. Only a refined
            // offer can release that support or start the next traversal.
            approach = first.Kind == NavigationAction.Walk ? first.To : first.From;
            return true;
        }

        private void AddCorridorEdge(NavigationTransition edge)
        {
            // Keep the chosen exit visible even beyond a long floor run.
            // The per-update handoff check never scans the published path.
            if (edge.Kind != NavigationAction.Walk && edge.Kind != NavigationAction.Dash)
                lastTraversal = corridor.Count;
            corridor.Add(edge);
        }

        internal void Complete(NavigationTransition transition, Vector2 actualFeet)
        {
            if (!offered || !offer.SameEdge(transition)) return;
            offered = issued = committed = verifying = false;
            corridorIndex = connector ? offerIndex : offerEnd + 1;
            cursor = corridorIndex;
            skipFlightMerge = false;
            if (Vector2.DistanceSquared(actualFeet, offer.To) > 35f * 35f)
            {
                ClearCorridor();
                // A survivable actual landing can differ from its coarse
                // exit. The pending search may still attach from that pose.
            }
            else if (corridorIndex >= corridor.Count)
            {
                ClearCorridor();
                nextRetry = 0;
            }
        }

        internal void Reject(NavigationTransition transition, Vector2 actualFeet)
        {
            RememberFailure(transition, 48, true);
        }

        internal void Defer(NavigationTransition transition, bool awaitingTerrain)
        {
            RememberFailure(transition, awaitingTerrain ? 24 : 8, false);
            if (awaitingTerrain) Status = NavigationQueryStatus.AwaitingWorldData;
        }

        internal bool TryShortenFlight(NavigationTransition transition)
        {
            if (!offered || committed || offerEnd == offerIndex || !offer.SameEdge(transition)) return false;
            offered = issued = verifying = false;
            cursor = corridorIndex;
            skipFlightMerge = true;
            return true;
        }

        private void RememberFailure(NavigationTransition transition, int retry, bool backoff)
        {
            var edge = offered && offer.SameEdge(transition) && !connector && offerEnd == offerIndex
                ? corridor[offerIndex] : transition;
            int until = clock + retry;
            for (int i = failures.Count - 1; i >= 0; i--)
            {
                if (!SameFailure(failures[i].Edge, edge)) continue;
                if (backoff) until = Math.Min(clock + 120, Math.Max(until, failures[i].Until + 24));
                failures.RemoveAt(i);
            }
            if (failures.Count == MaximumFailures) failures.RemoveAt(0);
            // An executor can reject direct motion too. Rejections bind the
            // actual attempted direction/entry to this body and the terrain
            // revision seen during the attempt, with a finite recheck time.
            failures.Add(new Failure(edge, capabilities, until, EdgeVersion(edge), backoff));
            ClearCorridor();
            query = null;
            waitingReads = null;
            nextRetry = 0;
        }

        internal void Abandon(NavigationTransition transition)
        {
            if (!offered || !offer.SameEdge(transition)) return;
            ClearCorridor();
            query = null;
            waitingReads = null;
            nextRetry = 0;
        }

        internal void Supersede(NavigationTransition transition)
        {
            if (!offered || !offer.SameEdge(transition)) return;
            ClearCorridor();
            waitingReads = null;
            nextRetry = 0;
        }

        internal bool IsRejected(NavigationTransition transition)
        {
            for (int i = failures.Count - 1; i >= 0; i--)
            {
                var failure = failures[i];
                if (failure.Until <= clock)
                {
                    failures.RemoveAt(i);
                    continue;
                }
                if (SameFailure(failure.Edge, transition) &&
                    failure.Capabilities.SameFailureConditions(capabilities, transition.Kind) &&
                    failure.Revision == EdgeVersion(transition)) return true;
            }
            return false;
        }

        internal bool HasRejectedLaunchNearby(Vector2 feet)
        {
            foreach (var failure in failures)
                if (failure.Until > clock && failure.Geometric && failure.Edge.Kind == NavigationAction.Jump &&
                    Vector2.DistanceSquared(feet, failure.Edge.From) <= 18f * 18f &&
                    failure.Capabilities.SameFailureConditions(capabilities, failure.Edge.Kind) &&
                    failure.Revision == EdgeVersion(failure.Edge)) return true;
            return false;
        }

        private void Prepare(Vector2 feet, NavigationBudget budget)
        {
            if (offered && !issued && Vector2.DistanceSquared(feet, offer.From) > 42f * 42f)
            {
                offered = verifying = false;
                cursor = corridorIndex;
            }
            if (offered || corridorIndex == corridor.Count) return;
            while (corridorIndex < corridor.Count && AlreadyAtWalkExit(feet, corridor[corridorIndex]))
            {
                corridorIndex++;
                cursor = corridorIndex;
            }
            if (corridorIndex == corridor.Count)
            {
                ClearCorridor();
                return;
            }
            if (!verifying)
            {
                // Prefer the furthest nearby suffix. A completed query can
                // arrive while the body is already past its original anchor.
                int limit = Math.Min(corridor.Count - 1, corridorIndex + 8);
                while (cursor <= limit && budget.TryExpand())
                {
                    int index = limit - (cursor - corridorIndex);
                    var edge = corridor[index];
                    traversal.Begin(capabilities, budget);
                    bool attached = traversal.CanAttach(feet, edge, out var joined);
                    if (traversal.Pending) return;
                    cursor++;
                    if (!attached || IsRejected(joined)) continue;
                    checking = joined;
                    checkIndex = checkEnd = index;
                    if (!skipFlightMerge) MergeVerticalFlight();
                    if (checkEnd != checkIndex && IsRejected(checking))
                    {
                        checking = joined;
                        checkEnd = checkIndex;
                    }
                    verificationIndex = 0;
                    verifying = true;
                    break;
                }
                if (!verifying)
                {
                    if (cursor > limit)
                    {
                        ClearCorridor();
                        query = null;
                    }
                    return;
                }
            }

            // The base game has no complete tile-mutation event. Before a
            // route is offered, recheck its finite local envelope against the
            // real loaded tiles. Motion separately owns the actual sweep.
            var margin = new Vector2(capabilities.Width / 2f + 16f, capabilities.Height + 16f);
            Point first = NavigationTraversal.CellAt(Vector2.Min(checking.From, checking.To) - margin);
            Point last = NavigationTraversal.CellAt(Vector2.Max(checking.From, checking.To) + margin);
            if (checking.Kind == NavigationAction.Facility)
            {
                first = NavigationTraversal.CellAt(checking.From - margin);
                last = NavigationTraversal.CellAt(checking.From + margin);
            }
            int width = last.X - first.X + 1;
            int cells = width * (last.Y - first.Y + 1);
            while (verificationIndex < cells)
            {
                var point = new Point(first.X + verificationIndex % width, first.Y + verificationIndex / width);
                if (!world.TryVerify(point, budget, out bool changed)) return;
                verificationIndex++;
                if (!changed) continue;
                ClearCorridor();
                query = null;
                return;
            }
            offer = checking;
            offerIndex = checkIndex;
            offerEnd = checkEnd;
            connector = !checking.SameEdge(corridor[checkIndex]) && checking.To == corridor[checkIndex].From;
            verifying = false;
            offered = true;
        }

        private void MergeVerticalFlight()
        {
            if (checking.Kind != NavigationAction.Flight || checking.Detail != NavigationTraversalDetail.None ||
                checking.Irreversible || Math.Abs(checking.To.X - checking.From.X) > 2f ||
                (checking.To.Y - checking.From.Y) * capabilities.Gravity >= -12f) return;
            // A straight known shaft can keep one set of flight controls
            // instead of slowing at every coarse air label. This is still
            // one bounded native refinement to a local viable endpoint.
            while (checkEnd + 1 < corridor.Count)
            {
                var next = corridor[checkEnd + 1];
                if (next.Kind != NavigationAction.Flight || next.Detail != NavigationTraversalDetail.None ||
                    next.Irreversible || next.From != checking.To || Math.Abs(next.To.X - checking.From.X) > 2f ||
                    (next.To.Y - next.From.Y) * capabilities.Gravity >= -8f ||
                    Math.Abs(next.To.Y - checking.From.Y) > 112f ||
                    !traversal.KnownClear(checking.From, next.To, capabilities.Gravity, capabilities.ActiveMount, 3,
                        checking.MountChoice)) return;
                checking = new NavigationTransition(NavigationAction.Flight, checking.From, next.To, checking.MountChoice,
                    checking.EntryTile, next.ExitTile, world.Revision);
                checkEnd++;
            }
        }

        private bool AlreadyAtWalkExit(Vector2 feet, NavigationTransition edge) =>
            capabilities.Supported && (edge.Kind == NavigationAction.Walk || edge.Kind == NavigationAction.Dash) &&
            Math.Abs(feet.X - edge.To.X) <= 3f && Math.Abs(feet.Y - edge.To.Y) <= 13f;

        private void ClearCorridor()
        {
            corridor.Clear();
            lastTraversal = -1;
            corridorIndex = cursor = 0;
            offered = issued = committed = verifying = false;
            skipFlightMerge = false;
        }

        private bool SameGoal(NavigationNode previous, NavigationNode current)
        {
            if (previous.EqualsRegion(current)) return true;
            if (previous.Medium != current.Medium || previous.Gravity != current.Gravity) return false;
            if (previous.Medium == NavigationMedium.Air) return traversal.SameAirRegion(previous, current);
            // A tile-distance comparison would merge positions on the
            // opposite sides of a wall or above/below distinct platforms.
            return traversal.SameGroundRegion(previous, current);
        }

        internal static bool SameFailure(NavigationTransition a, NavigationTransition b) =>
            a.Kind == b.Kind && a.Detail == b.Detail && a.RailFrame == b.RailFrame && a.RailDirection == b.RailDirection &&
            a.RailEntryFrame == b.RailEntryFrame && a.MountChoice.Equals(b.MountChoice) &&
            a.EntryTile == b.EntryTile && a.ExitTile == b.ExitTile &&
            NavigationTraversal.CellAt(a.From) == NavigationTraversal.CellAt(b.From) &&
            NavigationTraversal.CellAt(a.To) == NavigationTraversal.CellAt(b.To);

        private int EdgeVersion(NavigationTransition edge)
        {
            world.TryVersion(NavigationTraversal.CellAt(edge.From), out _, out int entry);
            world.TryVersion(NavigationTraversal.CellAt(edge.To), out _, out int exit);
            world.TryVersion(edge.EntryTile, out _, out int fixture);
            return unchecked(((entry * 397) ^ exit) * 397 ^ fixture);
        }

        private bool WaitingFailuresCurrent(NavigationCapabilities current)
        {
            foreach (var failure in failures)
            {
                var kind = failure.Edge.Kind;
                if (failure.Until > clock && waitingCaps.SameFailureConditions(failure.Capabilities, kind) &&
                    !waitingCaps.SameFailureConditions(current, kind)) return false;
            }
            return true;
        }

        private static bool SameFacilities(IReadOnlyList<NavigationTransition> old, IReadOnlyList<NavigationTransition> current)
        {
            if (old.Count != (current?.Count ?? 0)) return false;
            for (int i = 0; i < old.Count; i++)
                if (!old[i].SameEdge(current[i])) return false;
            return true;
        }
    }
}
