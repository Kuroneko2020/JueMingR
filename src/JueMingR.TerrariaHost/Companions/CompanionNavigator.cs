using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.Movement;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    // The per-body session chooses one input owner. The shared world and
    // query can progress after the original update even when a prefix is held.
    internal sealed class CompanionNavigator
    {
        private readonly CompanionMotionAction action = new CompanionMotionAction();
        private readonly CompanionActionRefinement refine = new CompanionActionRefinement();
        private readonly CompanionActionRefinement refineExit = new CompanionActionRefinement();
        private readonly List<NavigationTransition> facilities = new List<NavigationTransition>();
        private NavigationWorld world;
        private NavigationRoute route;
        private NavigationTransition offered;
        private Vector2 offeredEntry;
        private CompanionFallRisk.ReleaseCorridor offeredFallRelease;
        private int offeredLifeCost, offeredProtection, activeLifeCost, activeProtection;
        private bool hasOffer, hasTarget, supported, targetChanged, refineReady, retireIntent, rescuePending, rescuing, offeredFlightLanding;
        private bool actionEmergency, knowsPositionRevision, keepAirContact, hasBlockedRoof, targetAirborne;
        private CompanionOwnerTrail observedTrail;
        private uint nextFacilities, directRetry, pendingSince, nextDashCheck, dashPreparedUntil, lastRescue;
        private Vector2 blockedAt, blockedTarget, target, targetVelocity;
        private Vector2 blockedRoof, blockedRoofGoal;
        private long actionGeneration, nextAction, bodyGeneration;
        private float spacing, offeredFallOutletX;
        private int previousGravity, trailSegment, previousPositionRevision, continuationOffset, continuationGeneration, pylonCount;
        private CompanionMotionWitness continuationWitness;
        private NavigationTransition rescue;
        private NavigationTransition suspendedEdge;
        private CompanionMovementDeferral suspendedReason;
        private uint retrySuspendedAt;
        private SmartFallGrapple rescueHook;

        internal long ActionId => action.Id;
        internal NavigationAction ActionKind => action.Edge.Kind;
        internal NavigationTransition ActionTransition => action.Edge;
        internal bool ActionCommitted => action.Committed;
        internal CompanionActionOutcome ActionOutcome => action.Outcome;
        internal CompanionMovementDeferral LastDeferral => action.Deferral;
        internal NavigationQueryStatus QueryStatus => route?.Status ?? NavigationQueryStatus.Idle;
        internal int QueryGeneration => route?.Generation ?? 0;
        internal uint QueryPendingAge => pendingSince == 0 ? 0 : Main.GameUpdateCount - pendingSince;
        internal Vector2 Target => target;
        internal bool Supported => supported;

#if DEBUG
        internal int PlannerSlices { get; private set; }
        internal int TileWork { get; private set; }
        internal int ExpansionWork { get; private set; }
        internal int CandidateWork { get; private set; }
        internal int ShapeReadWork { get; private set; }
        internal int AStarWork { get; private set; }
        internal int PredictionWork { get; private set; }
        internal int MovementDeliveries { get; private set; }
        internal int MovementObservations { get; private set; }
        internal int ImmediateContactReads { get; private set; }
        internal int ImmediateContactPredictions { get; private set; }
        internal int ImmediateBrakingTicks { get; private set; }
        internal int RejectedActions { get; private set; }
        internal int FacilityUses { get; private set; }
        internal CompanionMovementDelivery LastMovementDelivery { get; private set; }
#endif

        internal void BindWorld(NavigationWorld current)
        {
            if (ReferenceEquals(current, world)) return;
            world = current;
            route = new NavigationRoute(world);
            ResetPosition();
        }

        internal void ResetPosition()
        {
            route?.Reset();
            action.Reset();
            refine.Reset();
            refineExit.Reset();
            hasOffer = hasTarget = supported = targetChanged = refineReady = retireIntent = rescuing = rescuePending = false;
            directRetry = nextFacilities = pendingSince = nextDashCheck = dashPreparedUntil = lastRescue = 0;
            previousGravity = 0;
            pylonCount = -1;
            observedTrail = null;
            knowsPositionRevision = false;
            actionEmergency = false;
            hasBlockedRoof = false;
            retrySuspendedAt = 0;
        }

        internal void IntentChanged()
        {
            targetChanged = true;
            ClearPrefix();
            if (action.Id != 0 && !Terminal(action.Outcome) && (action.InNativeMotion || action.Committed))
            {
                retireIntent = true;
                return;
            }
            // An owner warp invalidates the staged entry before the next
            // actual key. A body already airborne finishes its own action.
            action.Reset();
            route?.Reset();
            retireIntent = false;
        }

        internal void ObserveOwnerTrail(CompanionOwnerTrail trail)
        {
            int segment = trail?.Segment ?? 0;
            if (ReferenceEquals(trail, observedTrail) && trailSegment != segment) IntentChanged();
            observedTrail = trail;
            trailSegment = segment;
        }

        internal CompanionMobilityFrame Decide(Player body, CompanionMobilityState native, Vector2 feetGoal, Vector2 targetVelocity,
            float desiredSpacing, bool emergency, int threatDirection, bool ownerInAir)
        {
#if DEBUG
            int reads = CompanionNativeContact.Reads, steps = CompanionNativeContact.Predictions, braking = CompanionNativeContact.BrakingTicks;
            try { return DecideCurrent(body, native, feetGoal, targetVelocity, desiredSpacing, emergency, threatDirection, ownerInAir); }
            finally
            {
                ImmediateContactReads += CompanionNativeContact.Reads - reads;
                ImmediateContactPredictions += CompanionNativeContact.Predictions - steps;
                ImmediateBrakingTicks += CompanionNativeContact.BrakingTicks - braking;
            }
#else
            return DecideCurrent(body, native, feetGoal, targetVelocity, desiredSpacing, emergency, threatDirection, ownerInAir);
#endif
        }

        private CompanionMobilityFrame DecideCurrent(Player body, CompanionMobilityState native, Vector2 feetGoal, Vector2 targetVelocity,
            float desiredSpacing, bool emergency, int threatDirection, bool ownerInAir)
        {
            if (native.Generation != bodyGeneration)
            {
                ResetPosition();
                bodyGeneration = native.Generation;
            }
            if (knowsPositionRevision && previousPositionRevision != native.PositionRevision)
            {
                // A shared-world decoder can apply a confirmed warp between
                // our native updates. Retire the old input before staging the
                // next frame, allowing only its matching facility receipt.
                if (action.CompletedFacilityReceipt(native))
                {
#if DEBUG
                    FacilityUses++;
#endif
                    route?.Complete(action.Edge, CompanionNativeContact.Feet(body));
                }
                RetireBinding();
                supported = CompanionNativeContact.ObservedSupport(body);
            }
            previousPositionRevision = native.PositionRevision;
            knowsPositionRevision = true;
            Vector2 feet = CompanionNativeContact.Feet(body);
            targetChanged |= !hasTarget || !SameLocalTarget(target, feetGoal);
            target = feetGoal;
            this.targetVelocity = targetVelocity;
            spacing = desiredSpacing;
            hasTarget = true;
            targetAirborne = ownerInAir;
            if (!native.Observed.Ready) supported = CompanionNativeContact.ObservedSupport(body);
            keepAirContact = ownerInAir && !supported && native.Observed.WingHover && CompanionMotionProfile.Wings(native.Observed);
            if (body.CCed || native.Observed.Controlled)
                return new CompanionMobilityFrame { Status = "停留" };

            if (rescuePending)
            {
                // The latest real update can already have stabilized the
                // fall. A stale forecast must never trigger a tool anyway.
                rescuePending = false;
                if (!supported && CompanionFallRisk.MayNeedRescue(body) && !native.AwaitingEquipmentObservation)
                {
                    ClearPrefix();
                    route?.Reset();
                    rescuing = true;
                    actionEmergency = true;
                    action.Begin(++nextAction, rescue, false, rescue.From);
                    actionGeneration = bodyGeneration;
                }
            }

            if (Terminal(action.Outcome))
            {
                if (action.Outcome != CompanionActionOutcome.Complete && action.Direct)
                {
                    blockedAt = feet;
                    blockedTarget = target;
                    directRetry = Main.GameUpdateCount + 36;
                    if (action.Edge.Kind == NavigationAction.Flight && action.HasRoofContact)
                    {
                        blockedRoof = action.RoofContact;
                        blockedRoofGoal = target;
                        hasBlockedRoof = true;
                    }
                }
                action.Reset();
                rescuing = false;
            }
            if (action.Id != 0 && !actionEmergency && emergency && !action.InNativeMotion && !action.Committed)
            {
                // Oxygen or danger can evict a request that has not released
                // support or entered a native operation. In-flight drops
                // still own their controls until a real rescue or landing.
                if (!action.Direct) route?.Abandon(action.Edge);
                action.Reset();
                ClearPrefix();
            }
            if (action.Id != 0 && action.Edge.Irreversible && !action.InNativeMotion &&
                (body.statLife <= activeLifeCost || CompanionFallRisk.ProtectionKey(body) != activeProtection))
            {
                route?.Abandon(action.Edge);
                action.Reset();
                ClearPrefix();
            }
            if (hasOffer && (!route.TrySelect(feet, out var current) || !current.SameEdge(offered))) ClearPrefix();
            if (hasOffer && offered.Irreversible &&
                (body.statLife <= offeredLifeCost || CompanionFallRisk.ProtectionKey(body) != offeredProtection)) ClearPrefix();
            bool selectedApproach = hasOffer && route.IsTraversalApproach(offered, feetGoal);
            bool plannedHandoff = !emergency && !hasOffer && route?.HasTraversalApproach(feet, feetGoal) == true;
            if (plannedHandoff && action.Id != 0 && action.Direct && action.Edge.Kind == NavigationAction.Walk)
                action.Reset();
            if (action.Id != 0 && action.Direct && hasOffer && ReachableEntry(feet, offered) &&
                (action.LosingProgress || action.Edge.Kind == NavigationAction.Walk && offered.Kind != NavigationAction.Walk &&
                    (Math.Abs(feetGoal.Y - feet.Y) > 18f || offered.Kind == NavigationAction.Facility ||
                        offered.Detail == NavigationTraversalDetail.TrackBoard) || selectedApproach))
                action.Reset();
            if (action.Id != 0 && action.Direct && action.Edge.Kind == NavigationAction.Flight && hasOffer &&
                offered.Detail == NavigationTraversalDetail.MountFlight && ReachableEntry(feet, offered) &&
                !native.AwaitingEquipmentObservation && CanResume(native, offered) && NeedsFlightMount(body, native.Observed, feet))
                action.Reset();
            if (action.Id != 0 && action.Direct && action.Edge.Kind == NavigationAction.Walk && CanTakeDash(native.Observed, feet))
                action.Reset();
            if (action.Id != 0 && action.Direct && action.Edge.Kind == NavigationAction.Walk && !hasOffer &&
                (feetGoal.Y - feet.Y) * body.gravDir < -32f && CanLiftDirectly(body, native.Observed, feet))
            {
                // When the actual owner climbs, a horizontal local prefix
                // should hand off immediately to legal native flight in
                // free air. A published terrain action keeps precedence.
                Begin(new NavigationTransition(NavigationAction.Flight, feet, feetGoal), true, emergency);
            }
            bool atGoal = action.Id == 0 && CloseTarget(body, feet);
            if (action.Id == 0 && !atGoal)
            {
                bool offerReady = hasOffer && ReachableEntry(feet, offered) && CanResume(native, offered) &&
                    (!native.AwaitingEquipmentObservation || offered.Kind == NavigationAction.Walk);
                bool allowDirect = !plannedHandoff && (emergency || directRetry == 0 || unchecked((int)(Main.GameUpdateCount - directRetry)) >= 0 ||
                    Vector2.DistanceSquared(feet, blockedAt) > 24f * 24f ||
                    Math.Sign(blockedTarget.X - blockedAt.X) != Math.Sign(feetGoal.X - feet.X));
                if (offerReady &&
                    (!allowDirect || selectedApproach || offered.Kind != NavigationAction.Walk || !DirectWalk(body, feet, native.Observed)))
                {
                    Begin(offered, false, emergency);
                    hasOffer = false;
                }
                else if (allowDirect && TryDirect(body, native.Observed, feet, out var edge))
                    Begin(edge, true, emergency);
                else if (offerReady)
                {
                    Begin(offered, false, emergency);
                    hasOffer = false;
                }
            }
            if (action.Id != 0)
            {
                bool rollingWalk = !action.Direct && route?.ContinuesIntoRunningJump(action.Edge) == true;
                var frame = action.Input(body, native, supported, target, this.targetVelocity, action.Direct ? spacing : 0f,
                    threatDirection, emergency || rescuing, keepAirContact, ownerInAir, rollingWalk);
                if (rescuing) frame.Status = "缓解坠落";
                return frame;
            }
            return new CompanionMobilityFrame { Status = atGoal || desiredSpacing == 0f ? "停留" :
                QueryStatus == NavigationQueryStatus.AwaitingWorldData ? "等待地形" : "正在找路" };
        }

        private void Begin(NavigationTransition edge, bool direct, bool emergency)
        {
            if (!direct) route.MarkIssued(edge);
            action.Begin(++nextAction, edge, direct, direct ? edge.From : offeredEntry);
            if (!direct)
            {
                action.FallRelease = offeredFallRelease;
                action.FallOutletX = offeredFallOutletX;
                action.FlightLanding = offeredFlightLanding;
            }
            activeLifeCost = direct ? 0 : offeredLifeCost;
            activeProtection = direct ? 0 : offeredProtection;
            actionEmergency = emergency;
            actionGeneration = bodyGeneration;
        }

        private bool CanResume(CompanionMobilityState native, NavigationTransition edge)
        {
            if (retrySuspendedAt == 0 || edge.Kind != suspendedEdge.Kind || edge.EntryTile != suspendedEdge.EntryTile) return true;
            switch (suspendedReason)
            {
                case CompanionMovementDeferral.HandBusy:
                case CompanionMovementDeferral.ReleaseInput:
                    return !native.Observed.HandBusy;
                case CompanionMovementDeferral.EquipmentObservation:
                    return !native.AwaitingEquipmentObservation;
                case CompanionMovementDeferral.NativeTransition:
                    return native.Result != CompanionMobilityResult.Started;
                default:
                    return unchecked((int)(Main.GameUpdateCount - retrySuspendedAt)) >= 0;
            }
        }

        internal void RecordDelivery(CompanionMovementDelivery delivery)
        {
            action.Record(delivery);
#if DEBUG
            MovementDeliveries++;
            LastMovementDelivery = delivery;
#endif
        }

        internal void Observe(Player body, CompanionMobilityState native)
        {
#if DEBUG
            MovementObservations++;
#endif
            var state = native.Observed;
            supported = (!state.Wet || CompanionNativeContact.CanObserveWetSupport(body, state)) &&
                CompanionNativeContact.ObservedSupport(body);
            int gravity = body.gravDir < 0 ? -1 : 1;
            bool gravityChanged = previousGravity != 0 && previousGravity != gravity;
            bool repositioned = knowsPositionRevision && previousPositionRevision != native.PositionRevision;
            previousGravity = gravity;
            previousPositionRevision = native.PositionRevision;
            knowsPositionRevision = true;
            if (action.Id == 0 || native.Generation != actionGeneration)
            {
                if (repositioned || gravityChanged) RetireBinding();
                return;
            }
            if (Terminal(action.Outcome))
            {
                if (repositioned || gravityChanged) RetireBinding();
                return;
            }
            var result = action.Observe(body, native, supported, target, action.Direct && CloseTarget(body, CompanionNativeContact.Feet(body)));
            if (result == CompanionActionOutcome.Complete)
            {
#if DEBUG
                if (action.Edge.Kind == NavigationAction.Facility) FacilityUses++;
#endif
                if (!action.Direct && !rescuing) route?.Complete(action.Edge, CompanionNativeContact.Feet(body));
                if (action.Edge.Kind == NavigationAction.Facility || gravityChanged || repositioned)
                {
                    // The host confirmed the real endpoint. Old waits and
                    // evidence are position-bound, while the shared map is not.
                    ClearPrefix();
                    route?.Reset();
                }
            }
            else if (repositioned || gravityChanged) RetireBinding();
            else if (result == CompanionActionOutcome.Suspended)
            {
                suspendedEdge = action.Edge;
                suspendedReason = action.Deferral;
                retrySuspendedAt = Main.GameUpdateCount + 8;
                if (!action.Direct && !rescuing) route?.Abandon(action.Edge);
                ClearPrefix();
            }
            else if (Terminal(result))
            {
#if DEBUG
                RejectedActions++;
#endif
                if (!action.Direct && !rescuing) route?.Reject(action.Edge, CompanionNativeContact.Feet(body));
                ClearPrefix();
            }
            if (retireIntent && (action.Id == 0 || Terminal(result)))
            {
                retireIntent = false;
                route?.Reset();
            }
        }

        private void RetireBinding()
        {
            action.Reset();
            ClearPrefix();
            route?.Reset();
            directRetry = nextFacilities = dashPreparedUntil = retrySuspendedAt = 0;
            retireIntent = rescuing = rescuePending = false;
            hasBlockedRoof = false;
            targetChanged = true;
        }

        private void ClearPrefix()
        {
            hasOffer = refineReady = false;
            refine.Reset();
            refineExit.Reset();
            continuationOffset = 0;
        }

        internal void Advance(Player body, CompanionMobilityState native, CompanionOwnerTrail trail, NavigationBudget budget)
        {
            if (!hasTarget || route == null || native.Observed.Controlled) return;
#if DEBUG
            PlannerSlices++;
            int tiles = budget.Tiles, expansions = budget.Expansions, predictions = budget.Predictions;
            int candidates = budget.Candidates, shapes = budget.ShapeReads, nodes = budget.AStarExpansions;
            try { AdvanceCore(body, native, trail, budget); }
            finally
            {
                TileWork += budget.Tiles - tiles;
                ExpansionWork += budget.Expansions - expansions;
                PredictionWork += budget.Predictions - predictions;
                CandidateWork += budget.Candidates - candidates;
                ShapeReadWork += budget.ShapeReads - shapes;
                AStarWork += budget.AStarExpansions - nodes;
            }
#else
            AdvanceCore(body, native, trail, budget);
#endif
        }

        private void AdvanceCore(Player body, CompanionMobilityState native, CompanionOwnerTrail trail, NavigationBudget budget)
        {
            Vector2 feet = CompanionNativeContact.Feet(body);
            var state = native.Observed;
            if (TryPrepareRescue(body, native, feet, budget)) return;
            ObserveOwnerTrail(trail);
            if (retireIntent) return;
            if (state.DashReady && supported && Math.Abs(feet.Y - target.Y) <= 8f &&
                Math.Abs(feet.X - target.X) > 140f && unchecked((int)(Main.GameUpdateCount - nextDashCheck)) >= 0 &&
                budget.TryTiles(160) && budget.TryPredict())
            {
                nextDashCheck = Main.GameUpdateCount + 10;
                int direction = Math.Sign(target.X - feet.X);
                if (CompanionGroundRun.CanDash(body, direction, state.DashType, target.X))
                    dashPreparedUntil = Main.GameUpdateCount + 3;
            }
            bool active = action.Id != 0 && !Terminal(action.Outcome);
            if (active && action.Committed) return;
            bool directPrefix = active && action.Direct && !action.LosingProgress && !targetChanged;
            bool selectedApproach = route.HasTraversalApproach(feet, target);
            bool needFlightMount = NeedsFlightMount(body, state, feet) && !native.AwaitingEquipmentObservation;
            bool needsDifferentFloor = action.Edge.Kind == NavigationAction.Walk && !targetAirborne && Math.Abs(target.Y - feet.Y) > 24f;
            if (directPrefix && !selectedApproach && !needFlightMount && !needsDifferentFloor && (action.Edge.Kind != NavigationAction.Walk ||
                Vector2.DistanceSquared(feet, target) <= 100f * 100f)) return;
            if (action.Id == 0 && CloseTarget(body, feet) && !targetChanged) return;

            // Facilities are optional edges with their own legal native gates.
            // An ordinary blockage does not authorize a new teleport target.
            int registeredPylons = Main.PylonSystem?.Pylons?.Count ?? 0;
            if ((pylonCount != registeredPylons || unchecked((int)(Main.GameUpdateCount - nextFacilities)) >= 0) &&
                budget.TryExpand())
            {
                // Registration can change while an ordinary walk is already
                // being applied. Wake the actual facilities list on that next
                // budget slice; the walk remains the delivered prefix.
                pylonCount = registeredPylons;
                nextFacilities = Main.GameUpdateCount + 45;
                facilities.Clear();
                CompanionNavigationFacilities.Collect(body, state, feet, target, budget, facilities);
                route.SetFacilities(facilities);
            }
            // Keep ordinary walking cheap. A known utility or an actual
            // nearby rail may justify comparing a second legal route while
            // the local walking prefix still moves the body.
            if (directPrefix && !selectedApproach && !needFlightMount && !needsDifferentFloor && facilities.Count == 0 &&
                (!state.CanUseCart || !CompanionNavigationFacilities.HasNearbyTrack(feet, budget))) return;
            targetChanged = false;
            var capabilities = CompanionMotionProfile.Read(body, state, supported);
            Vector2 hint = default;
            bool hasHint = trail != null && trail.TryOutletHint(feet, target, Main.GameUpdateCount, out hint);
            route.SetTrailHint(hint, hasHint);
            route.SetLocalFixture(LocalFixture(body, feet, capabilities, budget));
            route.SetTargetMotion(targetVelocity);
            route.SetNeedFlightMount(needFlightMount);
            route.Advance(feet, target, capabilities, budget);
            if (route.Status == NavigationQueryStatus.PendingBudget)
            {
                if (pendingSince == 0) pendingSince = Main.GameUpdateCount;
            }
            else pendingSince = 0;
            if (hasOffer && !ReachableEntry(feet, offered))
                ClearPrefix();
            if (!route.TrySelect(feet, out var edge)) return;
            if (action.Id != 0 && !action.Direct && !Terminal(action.Outcome)) return;
            if (hasOffer && offered.SameEdge(edge)) return;

            // A sliced loss proof must describe one unchanged equipment and
            // damage model, including any already-refined continuation.
            if (!refine.Active || !refine.Edge.SameEdge(edge) ||
                edge.Irreversible && refine.EntryProtection != CompanionFallRisk.ProtectionKey(body))
            {
                refine.Begin(body, edge, supported);
                refineExit.Reset();
                refineReady = false;
                continuationOffset = 0;
            }
            var result = refineReady ? CompanionRefinementResult.Ready : refine.Advance(body, state, budget);
            if (result == CompanionRefinementResult.Pending) return;
            if (result == CompanionRefinementResult.Failed)
            {
                route.Reject(edge, feet);
                ClearPrefix();
                return;
            }
            refineReady = true;
            CompanionMotionWitness end = refine.Witness;
            if (edge.Irreversible && !route.IsTerminal(edge))
            {
                var continuation = CheckContinuation(body, state, edge, budget);
                if (continuation == CompanionRefinementResult.Pending) return;
                if (continuation == CompanionRefinementResult.Failed)
                {
                    route.Reject(edge, feet);
                    ClearPrefix();
                    return;
                }
                end = continuationWitness;
            }
            offered = edge;
            offeredEntry = refine.Entry;
            offeredFallRelease = refine.FallRelease;
            offeredFallOutletX = refine.FallOutletX;
            offeredFlightLanding = refine.FlightLanding;
            offeredLifeCost = end.Valid ? refine.EntryLife - end.RemainingLife : 0;
            offeredProtection = refine.EntryProtection;
            hasOffer = true;
        }

        private CompanionRefinementResult CheckContinuation(Player body, CompanionMobilitySnapshot state,
            NavigationTransition first, NavigationBudget budget)
        {
            if (continuationOffset == 0 || continuationGeneration != route.Generation)
            {
                continuationOffset = 1;
                continuationGeneration = route.Generation;
                continuationWitness = refine.Witness;
                refineExit.Reset();
            }
            // Follow the candidate's actual sequence, carrying the coupled
            // end pose and resources through each proven native segment. A
            // few tiles of walkable pit floor do not prove an onward exit.
            while (route.TryGetContinuation(first, continuationOffset, out var next, out bool terminal))
            {
                if (!refineExit.Active && !refineExit.BeginContinuation(body, next, continuationWitness))
                    return CompanionRefinementResult.Failed;
                var result = refineExit.Advance(body, state, budget);
                if (result != CompanionRefinementResult.Ready) return result;
                var witness = refineExit.Witness;
                if (!witness.Valid) return CompanionRefinementResult.Failed;
                continuationWitness = witness;
                var feet = witness.Position + new Vector2(body.width * .5f, body.gravDir > 0 ? body.height : 0);
                if (terminal || witness.Grounded && CompanionMotionAction.Near(feet, first.From, 12f, 10f)) return result;
                continuationOffset++;
                refineExit.Reset();
            }
            return CompanionRefinementResult.Failed;
        }

        private NavigationTransition? LocalFixture(Player body, Vector2 feet, NavigationCapabilities caps, NavigationBudget budget)
        {
            int direction = Math.Sign(target.X - feet.X);
            if (!supported || Math.Abs(target.X - feet.X) < 18f || Math.Abs(target.Y - feet.Y) > caps.JumpHeight + 32f ||
                !budget.TryTiles(40) || !CompanionDoor.AtHorizontalInlet(body, direction, out var door)) return null;
            var aperture = door.Aperture;
            float farSide = direction > 0 ? aperture.Right : aperture.Left;
            if ((target.X - farSide) * direction < 10f) return null;
            var exit = new Vector2(farSide + direction * (body.width * .5f + 6f), feet.Y);
            return new NavigationTransition(NavigationAction.Door, feet, exit, entryTile: door.Target.ToTileCoordinates());
        }

        private bool TryPrepareRescue(Player body, CompanionMobilityState native, Vector2 feet, NavigationBudget budget)
        {
            var state = native.Observed;
            int horizon = Math.Min(budget.PredictionsRemaining, CompanionFallRisk.RescueHorizon);
            if (rescuing || supported || state.Wet || state.Pulley || state.GrappleAttached || state.GrappleInFlight ||
                native.AwaitingEquipmentObservation || !CompanionFallRisk.MayNeedRescue(body) ||
                lastRescue != 0 && unchecked(Main.GameUpdateCount - lastRescue) < 12 ||
                horizon == 0 || !budget.TryPredict(horizon)) return false;
            if (!CompanionFallRisk.Threatened(body, horizon, out var contact)) return false;
            if (CompanionMotionProfile.CanFly(state) && CompanionMotionAction.ClearVertical(body, -(int)body.gravDir))
            {
                rescue = new NavigationTransition(NavigationAction.Flight, feet, feet - new Vector2(0, 48f * body.gravDir));
            }
            else if (state.ExtraJumps > 0 && CompanionMotionAction.ClearVertical(body, -(int)body.gravDir))
            {
                // An observed saved extra jump owns its ordinary release
                // and ignition just like every other native jump action.
                rescue = new NavigationTransition(NavigationAction.Jump, feet, feet - new Vector2(0, 48f * body.gravDir));
            }
            else
            {
                if (state.HookRange <= 0f || !budget.TryTiles(1200)) return false;
                long reads = 0;
                var item = body.QuickGrapple_GetItemToUse();
                if (rescueHook == null) rescueHook = new SmartFallGrapple();
                var plan = rescueHook.Find(body, item, contact, ref reads);
                if (!plan.Found) return false;
                rescue = new NavigationTransition(NavigationAction.Hook, feet, plan.Target, exitTile: plan.Target.ToTileCoordinates());
            }
            lastRescue = Main.GameUpdateCount;
            rescuePending = true;
            return true;
        }

        private bool TryDirect(Player body, CompanionMobilitySnapshot state, Vector2 feet, out NavigationTransition edge)
        {
            edge = default;
            NavigationAction kind;
            if (state.Wet && !state.Shimmer && NearLine(body, feet, target))
                kind = NavigationAction.Swim;
            else if (DirectWalk(body, feet, state))
                kind = CanTakeDash(state, feet) ? NavigationAction.Dash : NavigationAction.Walk;
            else if ((target.Y - feet.Y) * body.gravDir < 18f && CanLiftDirectly(body, state, feet))
                kind = NavigationAction.Flight;
            else return false;
            edge = new NavigationTransition(kind, feet, target);
            if (kind == NavigationAction.Dash) dashPreparedUntil = 0;
            return true;
        }

        private bool CanTakeDash(CompanionMobilitySnapshot state, Vector2 feet) =>
            state.DashReady && dashPreparedUntil != 0 && unchecked((int)(dashPreparedUntil - Main.GameUpdateCount)) >= 0 &&
            Math.Abs(feet.Y - target.Y) <= 8f && Math.Abs(feet.X - target.X) > 140f;

        private bool CanLiftDirectly(Player body, CompanionMobilitySnapshot state, Vector2 feet)
        {
            float rise = (feet.Y - target.Y) * body.gravDir;
            if (!CompanionMotionProfile.CanFly(state) || supported && !targetAirborne && rise <= 24f || StillBlockedByRoof(body, feet))
                return false;
            // A newly mounted body can have genuine open vertical clearance
            // even when the current wall blocks every diagonal first step.
            return NearLine(body, feet, target) || !supported && rise > 24f && CompanionMotionProfile.MountFlight(state) &&
                CompanionMotionAction.ClearVertical(body, -(int)body.gravDir);
        }

        private bool NeedsFlightMount(Player body, CompanionMobilitySnapshot state, Vector2 feet)
        {
            if (supported || body.mount.Active || state.FlightMountType < 0 || state.UnlimitedWings || state.Wet ||
                state.Pulley || state.GrappleAttached || state.GrappleInFlight) return false;
            float rise = (feet.Y - target.Y) * body.gravDir;
            float upwardSpeed = Math.Max(0f, -body.velocity.Y * body.gravDir);
            float coast = upwardSpeed * upwardSpeed / (2f * Math.Max(.05f, body.gravity));
            float available = state.WingTime + (state.RocketAvailable ? (state.RocketTime + 1) * Math.Max(8, state.RocketDelay) : 0);
            // Keep an active native wing as the local prefix. Ask the same
            // route for a mounted continuation before its observed reserve
            // can no longer reach the higher moving objective.
            return rise > 40f && rise + 20f > coast + available * Math.Max(3f, state.JumpSpeed);
        }

        private bool StillBlockedByRoof(Player body, Vector2 feet)
        {
            if (!hasBlockedRoof) return false;
            float center = blockedRoof.X + body.width * .5f;
            // Releasing lift often separates the body from the exact
            // contact for a few frames. Recheck that contact, since checking
            // only the new pose would immediately retry the same ceiling.
            if (Math.Abs(feet.X - center) <= 16f && Math.Abs(target.X - blockedRoofGoal.X) <= 64f &&
                (target.Y - feet.Y) * body.gravDir < -12f)
            {
                var rise = new Vector2(0, -6f * body.gravDir);
                if (!CompanionNativeContact.TryMove(body, blockedRoof, rise,
                    CompanionNativeContact.PlatformContact.Native(body, false, false), false, out var step) ||
                    Math.Abs(step.Position.Y - blockedRoof.Y) < 3f) return true;
            }
            hasBlockedRoof = false;
            return false;
        }

        private bool DirectWalk(Player body, Vector2 feet, CompanionMobilitySnapshot state)
        {
            float heightTolerance = !targetAirborne && CompanionMotionProfile.CanFly(state) ? 17f : 12f;
            if (!supported || state.GrappleAttached || state.Pulley || state.Track || Math.Abs(feet.Y - target.Y) > heightTolerance) return false;
            int direction = Math.Sign(target.X - feet.X);
            return direction != 0 && CompanionMotionAction.ClearAhead(body, direction,
                Math.Max(7f, Math.Abs(body.velocity.X) + 2f), true);
        }

        private bool CloseTarget(Player body, Vector2 feet)
        {
            // A nearby airborne owner still needs the original hover input
            // on the next update. Idle would spend that frame falling.
            if (keepAirContact || !CompanionMotionAction.Near(feet, target, spacing, 10f)) return false;
            float dx = target.X - feet.X;
            // A short same-floor distance can still cross a divider. A flat
            // lane or native stair sweep must connect the two contact points.
            if (supported) return Math.Abs(dx) < 1f || CompanionGroundRun.TryFlatLane(body, dx, false) ||
                CompanionGroundRun.TryStepLane(body, dx, 10f);
            return NearLine(body, feet, target);
        }

        private static bool NearLine(Player body, Vector2 from, Vector2 to)
        {
            Vector2 direction = to - from;
            float length = direction.Length();
            if (length <= 1f) return true;
            Vector2 passage = direction * Math.Min(1f, 18f / length);
            if (!CompanionNativeContact.TryMove(body, body.position, passage,
                CompanionNativeContact.PlatformContact.Native(body, false, false), false, out var contact)) return false;
            return Vector2.DistanceSquared(contact.Position, body.position + passage) <= 5f * 5f &&
                (body.lavaImmune || body.lavaTime > 24 || body.lavaWet ||
                    !CompanionMobilityGeometry.TouchesLava(contact.Position, body.width, body.height)) &&
                !CompanionMobilityGeometry.TouchesShimmer(contact.Position, body.width, body.height);
        }

        private static bool SameLocalTarget(Vector2 previous, Vector2 current) =>
            (int)(previous.X / 64f) == (int)(current.X / 64f) && (int)(previous.Y / 32f) == (int)(current.Y / 32f);
        private static bool ReachableEntry(Vector2 feet, NavigationTransition edge) =>
            CompanionMotionAction.Near(feet, edge.From, edge.Kind == NavigationAction.Walk ? 48f : 36f, 24f);
        private static bool Terminal(CompanionActionOutcome outcome) => outcome == CompanionActionOutcome.Complete ||
            outcome == CompanionActionOutcome.Blocked || outcome == CompanionActionOutcome.Rejected || outcome == CompanionActionOutcome.Suspended;
    }
}
