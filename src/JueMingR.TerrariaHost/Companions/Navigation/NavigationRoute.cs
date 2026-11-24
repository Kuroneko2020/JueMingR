using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.Companions
{
    // One actual body owns an attachment, a corridor and at most one pending
    // search. Offers already issued to the executor survive target revisions
    // until the corresponding actual outcome retires them.
    internal sealed class NavigationRoute
    {
        private const int RetargetWindow = 16;
        private const int RetryWindow = 32;
        private const int MaximumFailures = 12;

        private readonly struct Failure
        {
            internal readonly NavigationTransition Edge;
            internal readonly NavigationCapabilities Capabilities;
            internal readonly int Until, Revision;
            internal Failure(NavigationTransition edge, NavigationCapabilities capabilities, int until, int revision)
            {
                Edge = edge;
                Capabilities = capabilities;
                Until = until;
                Revision = revision;
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
        private int worldEpoch, corridorIndex, cursor, nextRetry, retargetSince = -1, clock;
        private int offerIndex, checkIndex, verificationIndex;
        private bool known, candidateTerminal, corridorTerminal, offered, issued, verifying, connector;
        private bool facilitiesChanged;
        private Vector2 trailHint;
        private Vector2 targetMotion;
        private bool hasTrail, needFlightMount;
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
            retargetSince = -1;
            offered = issued = verifying = false;
            facilities = Array.Empty<NavigationTransition>();
            facilitiesChanged = false;
            hasTrail = false;
            needFlightMount = false;
            targetMotion = default;
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
                if (!issued) ClearCorridor();
            }
            capabilities = current;
            known = true;
            worldEpoch = world.Epoch;
            traversal.Begin(current, budget);
            NavigationNode start = traversal.Anchor(feet, current.Supported, actual: true);
            NavigationNode goal = traversal.Anchor(target, true);
            goal = traversal.FallingGoal(feet, target, targetMotion, start, goal);
            if (traversal.Pending)
            {
                Status = offered ? NavigationQueryStatus.ReadyPrefix : NavigationQueryStatus.PendingBudget;
                return;
            }
            if (!NavigationWorld.InWorld(start.Cell) || !NavigationWorld.InWorld(goal.Cell) || traversal.Unknown)
            {
                Status = offered ? NavigationQueryStatus.ReadyPrefix : NavigationQueryStatus.AwaitingWorldData;
                return;
            }

            var localCandidate = localFixture;
            if (!issued && needFlightMount && traversal.LocalFlightMount(feet, start, out var mount) && !IsRejected(mount))
                localCandidate = mount;
            bool sameCorridorGoal = corridor.Count != 0 && SameGoal(corridorGoal, goal);
            if (!issued && !sameCorridorGoal && corridor.Count != 0)
                ClearCorridor();
            if (query != null && (!SameGoal(query.Goal, goal) || facilitiesChanged))
            {
                if (retargetSince < 0) retargetSince = clock;
                if (clock - retargetSince >= RetargetWindow || query.Status != NavigationQueryStatus.PendingBudget)
                    query = null;
            }
            else retargetSince = -1;
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
                    Math.Sign(corridor[corridorIndex].To.X - corridor[corridorIndex].From.X) ==
                    Math.Sign(fixture.To.X - fixture.From.X);
                if (!alreadyHead)
                {
                    ClearCorridor();
                    corridor.Add(fixture);
                    corridorGoal = goal;
                    corridorTerminal = false;
                    sameCorridorGoal = true;
                }
            }
            if (!issued && corridor.Count == 0)
            {
                bool localTerminal = traversal.LocalLevel(feet, start, goal, out var local) && !IsRejected(local);
                if (localTerminal || traversal.LocalDoor(feet, target, start, out local) && !IsRejected(local) ||
                    traversal.LocalTrack(feet, target, start, out local) && !IsRejected(local))
                {
                    corridor.Add(local);
                    corridorGoal = goal;
                    corridorTerminal = localTerminal;
                    sameCorridorGoal = true;
                }
            }
            if (!issued && sameCorridorGoal && corridorIndex < corridor.Count)
            {
                Prepare(feet, budget);
                if (corridor.Count != 0)
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
                query = new NavigationQuery(traversal, current, start, goal, facilities, world, trailHint, hasTrail);
                Generation++;
                retargetSince = -1;
            }
            query.Advance(budget, rejectFilter);
            Status = issued ? NavigationQueryStatus.ReadyPrefix : query.Status;
            if (!query.Valid)
            {
                query = null;
                return;
            }
            if (query.Status == NavigationQueryStatus.PendingBudget || issued) return;
            var completedQuery = query;
            query.CopyCorridor(candidate);
            candidateTerminal = query.Terminal;
            bool currentGoal = SameGoal(query.Goal, goal);
            if (!currentGoal)
            {
                // An older query may still provide a useful handoff toward
                // the latest goal. Its distant tail cannot drive this body.
                if (candidate.Count == 0 || Vector2.DistanceSquared(candidate[candidate.Count - 1].To, target) + 32f * 32f >=
                    Vector2.DistanceSquared(feet, target))
                    candidate.Clear();
                else if (candidate.Count > 2) candidate.RemoveRange(2, candidate.Count - 2);
                candidateTerminal = false;
            }
            corridorGoal = goal;
            query = null;
            if (candidate.Count == 0)
            {
                if (currentGoal && (completedQuery.Status == NavigationQueryStatus.NoRouteInKnownGraph ||
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
                else nextRetry = currentGoal ? clock + RetryWindow : 0;
                return;
            }
            corridor.Clear();
            corridor.AddRange(candidate);
            candidate.Clear();
            corridorTerminal = candidateTerminal;
            corridorIndex = cursor = 0;
            Prepare(feet, budget);
            Status = offered ? NavigationQueryStatus.ReadyPrefix : NavigationQueryStatus.PendingBudget;
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

        internal bool TryGetContinuation(NavigationTransition transition, out NavigationTransition next) =>
            TryGetContinuation(transition, 1, out next, out _);

        internal bool TryGetContinuation(NavigationTransition transition, int stepsAhead,
            out NavigationTransition next, out bool terminal)
        {
            next = default;
            terminal = false;
            if (!offered || connector || !offer.SameEdge(transition) ||
                stepsAhead < 1 || stepsAhead >= corridor.Count - offerIndex) return false;
            int index = offerIndex + stepsAhead;
            next = corridor[index];
            terminal = corridorTerminal && index + 1 == corridor.Count;
            return true;
        }

        internal bool IsTerminal(NavigationTransition transition) =>
            offered && !connector && offer.SameEdge(transition) && corridorTerminal && offerIndex + 1 == corridor.Count;

        internal bool ContinuesIntoRunningJump(NavigationTransition transition)
        {
            if (!offered || offer.Kind != NavigationAction.Walk || !offer.SameEdge(transition)) return false;
            int direction = Math.Sign(offer.To.X - offer.From.X);
            int first = connector ? offerIndex : offerIndex + 1;
            int limit = Math.Min(corridor.Count, first + 5);
            for (int i = first; i < limit; i++)
            {
                var edge = corridor[i];
                if (Math.Sign(edge.To.X - edge.From.X) != direction) return false;
                if (edge.Kind == NavigationAction.Jump) return edge.Detail == NavigationTraversalDetail.RunningJump;
                if (edge.Kind != NavigationAction.Walk) return false;
            }
            return false;
        }

        internal bool IsTraversalApproach(NavigationTransition transition, Vector2 target)
        {
            if (!offered || !offer.SameEdge(transition)) return false;
            return HasTraversalAfter(connector ? offerIndex : offerIndex + (offer.Kind == NavigationAction.Walk ? 1 : 0), target);
        }

        internal bool HasTraversalApproach(Vector2 feet, Vector2 target)
        {
            if (issued || corridorIndex >= corridor.Count) return false;
            var first = corridor[corridorIndex];
            return Vector2.DistanceSquared(feet, first.From) <= 42f * 42f && HasTraversalAfter(corridorIndex, target);
        }

        private bool HasTraversalAfter(int first, Vector2 target)
        {
            int limit = Math.Min(corridor.Count, first + 8);
            for (int i = first; i < limit; i++)
            {
                var edge = corridor[i];
                if (edge.Kind == NavigationAction.Facility || edge.Detail == NavigationTraversalDetail.TrackBoard ||
                    edge.Kind == NavigationAction.Hoik) return true;
                // The route below the obstruction still needs its native
                // continuation proof. A competing direct walk can otherwise
                // leave this inlet while that proof is advancing by slices.
                if (edge.Kind == NavigationAction.PlatformDrop && edge.Irreversible && corridorTerminal &&
                    (edge.To.Y - target.Y) * capabilities.Gravity >= 48f) return true;
                // The existing corridor has chosen to rise above something
                // between the body and the same-level goal. Walking straight
                // toward the owner here can undo the approach to that launch.
                if (edge.Kind == NavigationAction.Jump && Math.Abs(target.Y - edge.From.Y) <= 16f &&
                    (edge.From.Y - edge.To.Y) * capabilities.Gravity >= 32f &&
                    (target.X - edge.From.X) * Math.Sign(edge.To.X - edge.From.X) >= 24f) return true;
                if (edge.Kind != NavigationAction.Walk) return false;
            }
            return false;
        }

        internal void Complete(NavigationTransition transition, Vector2 actualFeet)
        {
            if (!offered || !offer.SameEdge(transition)) return;
            offered = issued = verifying = false;
            corridorIndex = connector ? offerIndex : offerIndex + 1;
            cursor = corridorIndex;
            if (Vector2.DistanceSquared(actualFeet, offer.To) > 35f * 35f)
            {
                ClearCorridor();
                query = null;
            }
            else if (corridorIndex >= corridor.Count)
            {
                ClearCorridor();
                nextRetry = 0;
            }
        }

        internal void Reject(NavigationTransition transition, Vector2 actualFeet)
        {
            var edge = offered && offer.SameEdge(transition) && !connector ? corridor[offerIndex] : transition;
            int until = clock + 48;
            for (int i = failures.Count - 1; i >= 0; i--)
            {
                if (!SameFailure(failures[i].Edge, edge)) continue;
                until = Math.Min(clock + 120, Math.Max(until, failures[i].Until + 24));
                failures.RemoveAt(i);
            }
            if (failures.Count == MaximumFailures) failures.RemoveAt(0);
            // An executor can reject direct motion too. Rejections bind the
            // actual attempted direction/entry to this body and the terrain
            // revision seen during the attempt, with a finite recheck time.
            failures.Add(new Failure(edge, capabilities, until, EdgeVersion(edge)));
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
                    if (joined.Irreversible && !corridorTerminal) continue;
                    checking = joined;
                    checkIndex = index;
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
            connector = !checking.SameEdge(corridor[checkIndex]) && checking.To == corridor[checkIndex].From;
            verifying = false;
            offered = true;
        }

        private bool AlreadyAtWalkExit(Vector2 feet, NavigationTransition edge) =>
            capabilities.Supported && (edge.Kind == NavigationAction.Walk || edge.Kind == NavigationAction.Dash) &&
            Math.Abs(feet.X - edge.To.X) <= 3f && Math.Abs(feet.Y - edge.To.Y) <= 13f;

        private void ClearCorridor()
        {
            corridor.Clear();
            corridorIndex = cursor = 0;
            offered = issued = verifying = false;
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

        private static bool SameFailure(NavigationTransition a, NavigationTransition b) =>
            a.Kind == b.Kind && a.Detail == b.Detail && a.RailFrame == b.RailFrame && a.RailDirection == b.RailDirection &&
            a.RailEntryFrame == b.RailEntryFrame &&
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
