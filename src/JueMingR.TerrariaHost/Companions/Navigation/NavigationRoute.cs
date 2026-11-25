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
            internal readonly Vector2 Contact;

            internal Failure(NavigationTransition edge, NavigationCapabilities capabilities, int until, int revision, bool geometric,
                Vector2 contact)
            {
                Edge = edge;
                Capabilities = capabilities;
                Until = until;
                Revision = revision;
                Geometric = geometric;
                Contact = contact;
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
        private Vector2? waitingGoalPose;
        private NavigationCapabilities waitingCaps;
        private NavigationQueryStatus waitingStatus;
        private int waitingRetry;
        private NavigationCapabilities capabilities;
        private NavigationNode corridorGoal;
        private Vector2? corridorGoalPose;
        private NavigationTransition offer, checking;
        private int worldEpoch, corridorIndex, cursor, nextRetry, clock;
        private int offerIndex, offerEnd, checkIndex, checkEnd, verificationIndex;
        private int lastTraversal = -1;
        private bool known, offered, issued, committed, verifying, connector;
        private bool facilitiesChanged, skipFlightMerge, verificationChanged;
        private Vector2 trailHint;
        private Vector2 targetMotion;
        private bool hasTrail, needFlightMount, targetAirborne, attachedHook;
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
            attachedHook = false;
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

        internal void SetHookContact(bool attached) => attachedHook = attached;

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
            Vector2? goalPose = goal.EqualsRegion(observed) ? target : (Vector2?)null;
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

            // The query's endpoint and heuristic are bound together. A live
            // positional drift can still use its old exit, while an obsolete
            // medium or different contacted floor cannot spend another slice.
            if (query != null && ObsoleteGoal(query.Goal, goal, query.GoalPose, goalPose))
            {
                query = null;
                nextRetry = 0;
            }

            var localCandidate = localFixture;
            bool sameCorridorGoal = corridor.Count != 0 && SameGoal(corridorGoal, goal, corridorGoalPose, goalPose);
            if (!issued && needFlightMount && traversal.LocalFlightMount(feet, start, rejectFilter, out var mount))
                localCandidate = mount;
            if (!issued && !localCandidate.HasValue && corridorIndex > lastTraversal &&
                traversal.LocalHoik(feet, target, start, out var hoik) && !IsRejected(hoik)) localCandidate = hoik;
            if (!issued && attachedHook && !localCandidate.HasValue &&
                traversal.LocalAttachedDrop(feet, start, goal, goalPose, out var hookDrop)) localCandidate = hookDrop;
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
                if (!localCandidate.HasValue && waitingStart.EqualsRegion(start) && SameGoal(waitingGoal, goal, waitingGoalPose, goalPose) &&
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
                    corridorGoalPose = goalPose;
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
                    corridorGoalPose = goalPose;
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
                if (clock < nextRetry && SameGoal(corridorGoal, goal, corridorGoalPose, goalPose))
                {
                    if (issued) Status = NavigationQueryStatus.ReadyPrefix;
                    return;
                }
                query = new NavigationQuery(traversal, current, start, goal, facilities, world, trailHint, hasTrail, observed,
                    feet, target);
                Generation++;
            }
            query.Advance(budget, rejectFilter);
            Status = offered ? NavigationQueryStatus.ReadyPrefix : query.Status == NavigationQueryStatus.ReadyPrefix
                ? NavigationQueryStatus.PendingBudget : query.Status;
            if (!query.Valid)
            {
                query = null;
                return;
            }
            if (query.Status == NavigationQueryStatus.PendingBudget || committed || issued && sameCorridorGoal) return;
            var completedQuery = query;
            query.CopyCorridor(candidate);
            bool currentGoal = SameGoal(query.Goal, goal, query.GoalPose, goalPose);
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
                    waitingGoalPose = goalPose;
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
            corridorGoalPose = goalPose;
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
            Status = NavigationQueryStatus.PendingBudget;
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
            // Walk and Dash share a ground-run waypoint while proof is
            // pending. Ordinary steering still sweeps and brakes on its own
            // support. Only a refined offer can start the native dash or
            // the next traversal.
            approach = first.Kind == NavigationAction.Walk || first.Kind == NavigationAction.Dash ? first.To : first.From;
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
            Status = NavigationQueryStatus.PendingBudget;
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
            RememberFailure(transition, actualFeet, 48, true);
        }

        internal void Defer(NavigationTransition transition, bool awaitingTerrain)
        {
            RememberFailure(transition, transition.From, awaitingTerrain ? 24 : 8, false);
            if (awaitingTerrain) Status = NavigationQueryStatus.AwaitingWorldData;
        }

        internal bool TryShortenFlight(NavigationTransition transition)
        {
            if (!offered || committed || offerEnd == offerIndex || !offer.SameEdge(transition)) return false;
            offered = issued = verifying = false;
            Status = NavigationQueryStatus.PendingBudget;
            cursor = corridorIndex;
            skipFlightMerge = true;
            return true;
        }

        private void RememberFailure(NavigationTransition transition, Vector2 contact, int retry, bool geometric)
        {
            var edge = transition;
            if (offered && offer.SameEdge(transition) && !connector && offerEnd == offerIndex &&
                SameFailure(transition, corridor[offerIndex])) edge = corridor[offerIndex];
            int until = clock + retry;
            int revision = EdgeVersion(edge, contact, capabilities);
            for (int i = failures.Count - 1; i >= 0; i--)
            {
                var previous = failures[i];
                if (!SameFailure(previous.Edge, edge)) continue;
                bool current = previous.Revision == EdgeVersion(previous.Edge, previous.Contact, previous.Capabilities) &&
                    previous.Capabilities.SameFailureConditions(capabilities, edge.Kind);
                if (current && previous.Geometric && !geometric && previous.Until > clock)
                {
                    // Preserve an already proved obstruction when a later
                    // observation only reports unavailable input or terrain.
                    ClearCorridor();
                    return;
                }
                if (current && geometric && previous.Geometric)
                    until = Math.Min(clock + 120, Math.Max(until, previous.Until + 24));
                failures.RemoveAt(i);
            }
            if (failures.Count == MaximumFailures) failures.RemoveAt(0);
            // An executor can reject direct motion too. Rejections bind the
            // actual attempted direction/entry to this body and the terrain
            // revision seen during the attempt, with a finite recheck time.
            failures.Add(new Failure(edge, capabilities, until, revision, geometric, contact));
            ClearCorridor();
            if (geometric) query = null;
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
                    failure.Revision == EdgeVersion(failure.Edge, failure.Contact, capabilities)) return true;
            }
            return false;
        }

        internal bool HasRejectedLaunchNearby(Vector2 feet)
        {
            foreach (var failure in failures)
                if (failure.Until > clock && failure.Geometric && failure.Edge.Kind == NavigationAction.Jump &&
                    Vector2.DistanceSquared(feet, failure.Edge.From) <= 18f * 18f &&
                    failure.Capabilities.SameFailureConditions(capabilities, failure.Edge.Kind) &&
                    failure.Revision == EdgeVersion(failure.Edge, failure.Contact, capabilities)) return true;
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
                    if (!attached || IsRejected(edge) || IsRejected(joined)) continue;
                    checking = joined;
                    checkIndex = checkEnd = index;
                    if (!skipFlightMerge) MergeVerticalFlight();
                    if (checkEnd != checkIndex && IsRejected(checking))
                    {
                        checking = joined;
                        checkEnd = checkIndex;
                    }
                    verificationIndex = 0;
                    verificationChanged = false;
                    verifying = true;
                    break;
                }
                if (!verifying)
                {
                    if (cursor > limit)
                    {
                        // A body can outgrow an old entry while another
                        // query is still finding the live alternate outlet.
                        ClearCorridor();
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
                if (checking.Kind == NavigationAction.Jump && checking.Detail == NavigationTraversalDetail.None)
                {
                    float halfWidth = capabilities.Width * .5f;
                    float left = Math.Min(feet.X, Math.Min(checking.From.X, checking.To.X)) - halfWidth;
                    float right = Math.Max(feet.X, Math.Max(checking.From.X, checking.To.X)) + halfWidth;
                    // The scan includes side padding. The normal jump's
                    // native arc still checks excursions into it from the
                    // actual pose and velocity. Running jumps keep runway checks.
                    if ((point.X + 1) * 16f <= left || point.X * 16f >= right)
                    {
                        if (query != null && !query.Valid) query = null;
                        continue;
                    }
                }
                verificationChanged = true;
            }
            // A multi-tile edit can change several cached cells at once.
            // Finish the bounded scan across slices before planning anew.
            // The stale edge stays unoffered during the entire scan.
            if (verificationChanged)
            {
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
            if (Status == NavigationQueryStatus.ReadyPrefix) Status = NavigationQueryStatus.PendingBudget;
        }

        private bool SameGoal(NavigationNode previous, NavigationNode current, Vector2? previousPose, Vector2? currentPose)
        {
            if (previous.EqualsRegion(current) && previousPose == currentPose) return true;
            if (previous.Medium != current.Medium || previous.Gravity != current.Gravity) return false;
            if (previous.Medium == NavigationMedium.Air) return traversal.SameAirRegion(previous, current, previousPose, currentPose);
            // A tile-distance comparison would merge positions on the
            // opposite sides of a wall or above/below distinct platforms.
            return traversal.SameGroundRegion(previous, current, previousPose, currentPose);
        }

        private bool ObsoleteGoal(NavigationNode previous, NavigationNode current, Vector2? previousPose, Vector2? currentPose)
        {
            if (previous.Gravity != current.Gravity || previous.Medium != current.Medium) return true;
            // Nearby separated floors can be distinguished from gradual
            // slopes. Horizontal pursuit and open-air travel keep the search
            // as the owner moves beyond the original terminal envelope.
            return current.Grounded && Math.Abs(previous.Cell.Y - current.Cell.Y) > 1 &&
                Math.Abs(previous.Cell.X - current.Cell.X) <= 18 &&
                !SameGoal(previous, current, previousPose, currentPose) && !traversal.Pending && !traversal.Unknown;
        }

        internal static bool SameFailure(NavigationTransition a, NavigationTransition b) =>
            a.Kind == b.Kind && a.Detail == b.Detail && a.RailFrame == b.RailFrame && a.RailDirection == b.RailDirection &&
            a.RailEntryFrame == b.RailEntryFrame && a.MountChoice.Equals(b.MountChoice) &&
            a.EntryTile == b.EntryTile && a.ExitTile == b.ExitTile &&
            NavigationTraversal.CellAt(a.From) == NavigationTraversal.CellAt(b.From) &&
            NavigationTraversal.CellAt(a.To) == NavigationTraversal.CellAt(b.To) &&
            Math.Sign(a.To.X - a.From.X) == Math.Sign(b.To.X - b.From.X);

        private int EdgeVersion(NavigationTransition edge, Vector2 contact, NavigationCapabilities body)
        {
            int revision = unchecked(world.EditVersion(edge.EntryTile) * 397 ^ world.EditVersion(edge.ExitTile));
            var margin = new Vector2(body.Width * .5f + 16f, body.Height + 16f);
            var end = edge.Kind == NavigationAction.Facility ? edge.From : edge.To;
            var first = NavigationTraversal.CellAt(Vector2.Min(contact, Vector2.Min(edge.From, end)) - margin);
            var last = NavigationTraversal.CellAt(Vector2.Max(contact, Vector2.Max(edge.From, end)) + margin);
            // A head-height edit can open the inlet without changing the
            // ground chunk. Include the consulted chunks around the body.
            int size = NavigationWorld.ChunkSize;
            for (int y = first.Y / size; y <= last.Y / size; y++)
                for (int x = first.X / size; x <= last.X / size; x++)
                {
                    revision = unchecked(revision * 397 ^ world.EditVersion(new Point(x * size, y * size)));
                }
            return revision;
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
