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
        private const int StallUpdates = 44;
        private readonly CompanionMotionAction action = new CompanionMotionAction();
        private readonly CompanionActionRefinement refine = new CompanionActionRefinement();
        private readonly List<NavigationTransition> facilities = new List<NavigationTransition>();
        private enum DirectKind { Swim, Walk, Dash, Flight, FlightDash, Count }
        private readonly DirectFailure[] directFailures = new DirectFailure[(int)DirectKind.Count];

        private struct DirectFailure
        {
            internal uint Until;
            internal Vector2 Position;
            internal Point Contact;
            internal int Direction, Elevation, Width, Height, Source, FootVersion, ContactVersion;
            internal bool Supported;
        }

        private NavigationWorld world;
        private NavigationRoute route;
        private NavigationTransition offered;
        private Vector2 offeredEntry;
        private CompanionFallRisk.ReleaseCorridor offeredFallRelease;
        private int offeredLifeCost, offeredProtection, activeLifeCost, activeProtection;
        private bool hasOffer, hasTarget, supported, targetChanged, retireIntent, rescuePending, rescuing, offeredFlightLanding;
        private bool offeredLandingRisk, activeLandingRisk;
        private bool actionEmergency, knowsPositionRevision, keepAirContact, hasBlockedRoof, targetAirborne, targetContactKnown;
        private CompanionOwnerTrail observedTrail;
        private uint nextFacilities, pendingSince, nextDashCheck, nextCruiseCheck, nextMountCheck, dashPreparedUntil, lastRescue;
        private Vector2 target, targetVelocity;
        private Vector2 blockedRoof, blockedRoofGoal;
        private Vector2? offeredFlightTouchdown, activeFlightTouchdown;
        private long actionGeneration, nextAction, bodyGeneration;
        private float spacing, offeredFallOutletX;
        private int previousGravity, trailSegment, previousPositionRevision, pylonCount;
        private NavigationTransition rescue;
        private NavigationTransition suspendedEdge;
        private CompanionMovementDeferral suspendedReason;
        private uint retrySuspendedAt;
        private SmartFallGrapple rescueHook;
        private Vector2 progressBoundary, actionOwnerTarget;
        private int noUsefulProgress, nextStallCheck = StallUpdates;
        private readonly CompanionDashMotion.Preparation dashPreparation = new CompanionDashMotion.Preparation();
        private CompanionDashMotion.Plan dashPlan;

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
            hasOffer = hasTarget = supported = targetChanged = retireIntent = rescuing = rescuePending = false;
            Array.Clear(directFailures, 0, directFailures.Length);
            nextFacilities = pendingSince = nextDashCheck = nextCruiseCheck = nextMountCheck = dashPreparedUntil = lastRescue = 0;
            dashPreparation.Reset();
            dashPlan = default;
            previousGravity = 0;
            pylonCount = -1;
            knowsPositionRevision = false;
            actionEmergency = false;
            activeLandingRisk = false;
            activeFlightTouchdown = null;
            hasBlockedRoof = false;
            retrySuspendedAt = 0;
            noUsefulProgress = 0;
            nextStallCheck = StallUpdates;
        }

        internal void IntentChanged(CompanionMobilityState native)
        {
            // The target and its segment are published together by Control.
            // A later native owner warp can arrive after Control but before
            // this body's planner slice. Retire that target before any work.
            hasTarget = false;
            targetChanged = true;
            ClearPrefix();
            route?.Reset();
            pendingSince = nextFacilities = dashPreparedUntil = retrySuspendedAt = 0;
            dashPreparation.Reset();
            dashPlan = default;
            // A latched hook already controls the body even after its
            // launch receipt completes. Its next input may release it
            // against the newly published target using the actual hang.
            bool attachedHook = action.Edge.Kind == NavigationAction.Hook && native.Observed.GrappleAttached;
            if (action.Id != 0 && !Terminal(action.Outcome) &&
                (action.Committed || attachedHook || action.AwaitingNativeTool(native)))
            {
                retireIntent = true;
                return;
            }
            // An owner warp invalidates the staged entry before the next
            // actual key. A body already airborne finishes its own action.
            action.Reset();
            retireIntent = false;
        }

        internal bool ObserveOwnerTrail(CompanionOwnerTrail trail, CompanionMobilityState native)
        {
            int segment = trail?.Segment ?? 0;
            bool changed = !ReferenceEquals(trail, observedTrail) || trailSegment != segment;
            if (changed) IntentChanged(native);
            observedTrail = trail;
            trailSegment = segment;
            return changed;
        }

        internal CompanionMobilityFrame Decide(Player body, CompanionMobilityState native, Vector2 feetGoal, Vector2 targetVelocity,
            float desiredSpacing, bool emergency, int threatDirection, bool ownerInAir, bool contactKnown)
        {
#if DEBUG
            int reads = CompanionNativeContact.Reads, steps = CompanionNativeContact.Predictions, braking = CompanionNativeContact.BrakingTicks;
            try { return DecideCurrent(body, native, feetGoal, targetVelocity, desiredSpacing, emergency, threatDirection, ownerInAir, contactKnown); }
            finally
            {
                ImmediateContactReads += CompanionNativeContact.Reads - reads;
                ImmediateContactPredictions += CompanionNativeContact.Predictions - steps;
                ImmediateBrakingTicks += CompanionNativeContact.BrakingTicks - braking;
            }
#else
            return DecideCurrent(body, native, feetGoal, targetVelocity, desiredSpacing, emergency, threatDirection, ownerInAir, contactKnown);
#endif
        }

        private CompanionMobilityFrame DecideCurrent(Player body, CompanionMobilityState native, Vector2 feetGoal, Vector2 targetVelocity,
            float desiredSpacing, bool emergency, int threatDirection, bool ownerInAir, bool contactKnown)
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
            targetContactKnown = contactKnown;
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
                    activeLandingRisk = false;
                    activeFlightTouchdown = null;
                    action.Begin(++nextAction, rescue, false, rescue.From);
                    actionGeneration = bodyGeneration;
                }
            }

            if (Terminal(action.Outcome))
            {
                if (action.Direct && (action.Outcome == CompanionActionOutcome.Rejected ||
                    action.Outcome == CompanionActionOutcome.Blocked))
                {
                    RememberDirectFailure(body, native.Observed, feet);
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
            if (action.Id != 0 && !actionEmergency && emergency && !action.Committed)
            {
                // Oxygen or danger can evict a request that has not released
                // support or entered a native operation. In-flight drops
                // still own their controls until a real rescue or landing.
                if (!action.Direct) route?.Abandon(action.Edge);
                action.Reset();
                ClearPrefix();
            }
            if (action.Id != 0 && activeLandingRisk && !action.Committed &&
                action.Edge.Kind != NavigationAction.Drop && action.Edge.Kind != NavigationAction.PlatformDrop &&
                (body.statLife <= activeLifeCost || CompanionFallRisk.ProtectionKey(body) != activeProtection))
            {
                route?.Abandon(action.Edge);
                action.Reset();
                ClearPrefix();
            }
            if (hasOffer && (!route.TrySelect(feet, out var current) || !current.SameEdge(offered))) ClearPrefix();
            if (hasOffer && offeredLandingRisk &&
                (body.statLife <= offeredLifeCost || CompanionFallRisk.ProtectionKey(body) != offeredProtection)) ClearPrefix();
            bool offerReady = ReadyOffer(body, native, feet);
            bool selectedApproach = hasOffer && route.IsTraversalApproach(offered);
            Vector2 approach = default;
            bool approaching = !emergency && route?.TryTraversalApproach(feet, out approach) == true;
            if (approaching && supported)
            {
                // Steering toward an unissued entrance still needs a real
                // stride on this floor. A stale blocked corridor must not
                // displace a different usable local move.
                float offset = approach.X - feet.X;
                approaching = Math.Abs(approach.Y - feet.Y) <= 24f && (Math.Abs(offset) <= 3f ||
                    WalkStride(body, Math.Sign(offset), Math.Min(Math.Abs(offset), Math.Max(4f, Math.Abs(body.velocity.X) + 1f))));
            }
            if (action.Id != 0 && action.Direct && action.CanRetarget && offerReady &&
                (action.LosingProgress || action.Edge.Kind == NavigationAction.Walk &&
                    (selectedApproach || offered.Kind != NavigationAction.Walk && (Math.Abs(feetGoal.Y - feet.Y) > 18f ||
                        offered.Kind == NavigationAction.Facility || offered.Detail == NavigationTraversalDetail.TrackBoard)) ||
                    action.Edge.Kind == NavigationAction.Flight && offered.Detail == NavigationTraversalDetail.MountFlight &&
                        NeedsFlightMount(body, native.Observed, feet) ||
                    supported && (offered.Kind == NavigationAction.Drop || offered.Kind == NavigationAction.PlatformDrop) &&
                        (feetGoal.Y - feet.Y) * body.gravDir >= 16f))
            {
                // Eligibility is checked with the current body before the
                // prepared traversal replaces any useful steering input.
                Begin(offered, false, emergency);
                hasOffer = false;
            }
            bool replaceablePlan = action.Id != 0 && !action.Direct && action.CanRetarget;
            bool reversedPlan = replaceablePlan && !approaching && !route.IsTraversalApproach(action.Edge) &&
                (target.X - actionOwnerTarget.X) * Math.Sign(action.Edge.To.X - action.Edge.From.X) < -4f &&
                (action.Edge.To.X - feet.X) * (feetGoal.X - feet.X) < -48f;
            if (replaceablePlan && hasOffer && offerReady && !offered.SameEdge(action.Edge))
            {
                Begin(offered, false, emergency);
                hasOffer = false;
            }
            else if (reversedPlan && action.Edge.Kind == NavigationAction.Flight && !supported &&
                !native.AwaitingEquipmentObservation && DirectReady(body, native.Observed, feet, feetGoal, DirectKind.Flight, emergency) &&
                Vector2.DistanceSquared(feet, feetGoal) <= 48f * 48f && CanLiftDirectly(body, native.Observed, feet) &&
                NearLine(body, feet, feetGoal, wholeLane: true))
            {
                route.Supersede(action.Edge);
                ClearPrefix();
                Begin(new NavigationTransition(NavigationAction.Flight, feet, feetGoal), true, emergency);
            }
            else if (reversedPlan && DirectWalk(body, feet, native.Observed, target.X, LocalSpacing(body), this.targetVelocity.X, emergency) &&
                CompanionGroundRun.TryFlatLane(body, feetGoal.X - feet.X, false))
            {
                // A proved lane now points back along the body's own floor.
                // An obsolete command retires without a geometric rejection.
                route.Supersede(action.Edge);
                ClearPrefix();
                Begin(new NavigationTransition(NavigationAction.Walk, feet, new Vector2(feetGoal.X, feet.Y)), true, emergency);
            }
            bool cruiseCheck = action.Id != 0 && action.Edge.Kind == NavigationAction.Walk &&
                Math.Abs(feetGoal.X - feet.X) > 104f && CompanionMotionProfile.CanFly(native.Observed) &&
                unchecked((int)(Main.GameUpdateCount - nextCruiseCheck)) >= 0;
            bool ordinaryOffer = hasOffer && offered.Kind == NavigationAction.Walk && !selectedApproach &&
                Math.Abs(offered.To.Y - feet.Y) <= 10f && Math.Abs(feetGoal.Y - feet.Y) <= 10f &&
                (offered.To.X - offered.From.X) * (feetGoal.X - feet.X) >= 0f;
            bool preparedDash = CanTakeDash(body, native.Observed);
            bool forwardFloorPlan = replaceablePlan && preparedDash && supported && !approaching &&
                action.Edge.Kind == NavigationAction.Walk && !route.IsTraversalApproach(action.Edge) &&
                Math.Abs(action.Edge.From.Y - feet.Y) <= 3f && Math.Abs(action.Edge.To.Y - feet.Y) <= 3f &&
                Math.Abs(feetGoal.Y - feet.Y) <= 3f &&
                (action.Edge.To.X - action.Edge.From.X) * (feetGoal.X - feet.X) > 0f &&
                (!hasOffer || ordinaryOffer);
            if (forwardFloorPlan && TryDirect(body, native, feet, emergency, out var pulse) &&
                pulse.Kind == NavigationAction.Dash)
            {
                // The graph only owns an ordinary forward floor prefix.
                // Keep its ongoing search and never bypass a traversal inlet.
                route.Supersede(action.Edge);
                ClearPrefix();
                Begin(pulse, true, emergency);
            }
            // A queued ordinary floor walk should not suppress a pulse
            // that has already proved a longer stopping lane. Keep actual
            // traversal approaches, facilities and handoffs in control.
            if (action.Id != 0 && action.Direct && action.CanRetarget && !approaching &&
                (!hasOffer || ordinaryOffer && preparedDash) &&
                (action.Edge.Kind == NavigationAction.Walk || action.Edge.Kind == NavigationAction.Flight) &&
                (cruiseCheck || preparedDash || action.Edge.Kind == NavigationAction.Walk &&
                    (feetGoal.Y - feet.Y) * body.gravDir < -26f ||
                    action.Edge.Kind == NavigationAction.Flight && supported && !ownerInAir))
            {
                if (cruiseCheck) nextCruiseCheck = Main.GameUpdateCount + 10;
                if (TryDirect(body, native, feet, emergency, out var faster) &&
                    (faster.Kind != action.Edge.Kind || faster.Detail != action.Edge.Detail) &&
                    (!hasOffer || faster.Kind == NavigationAction.Dash))
                {
                    if (!action.TryRetarget(faster)) Begin(faster, true, emergency);
                    else if (faster.Detail == NavigationTraversalDetail.FlightDash)
                    {
                        action.DashPlan = dashPlan;
                        dashPreparedUntil = 0;
                    }
                }
            }
            bool atGoal = action.Id == 0 && CloseTarget(body, feet);
            if (action.Id == 0 && !atGoal)
            {
                if (offerReady && !supported && preparedDash && offered.Kind == NavigationAction.Drop &&
                    !ownerInAir && body.velocity.Y * body.gravDir > 0f &&
                    dashPlan.LandsOn(offered.To.Y) && dashPlan.LandsOn(feetGoal.Y) &&
                    TryDirect(body, native, feet, emergency, out var airbornePulse) &&
                    airbornePulse.Detail == NavigationTraversalDetail.FlightDash)
                {
                    // The original fall is already in motion and its
                    // unreleased graph offer lands on the same verified
                    // floor. Keep the corridor for reattachment from the
                    // actual end of this horizontal pulse.
                    ClearPrefix();
                    Begin(airbornePulse, true, emergency);
                }
                else if (offerReady && (selectedApproach || offered.Kind != NavigationAction.Walk))
                {
                    Begin(offered, false, emergency);
                    hasOffer = false;
                }
                else if (approaching && DirectWalk(body, feet, native.Observed, approach.X, 1f, 0f, emergency))
                    Begin(new NavigationTransition(NavigationAction.Walk, feet, approach), true, emergency);
                else if ((!approaching || !supported) && TryDirect(body, native, feet, emergency, out var edge))
                    Begin(edge, true, emergency);
                else if (offerReady)
                {
                    Begin(offered, false, emergency);
                    hasOffer = false;
                }
            }
            if (action.Id != 0)
            {
                if (activeLandingRisk && action.Edge.Kind == NavigationAction.Flight && supported && !action.Committed &&
                    native.AwaitingEquipmentObservation) return new CompanionMobilityFrame { Status = "停留" };
                bool rollingWalk = !action.Direct && route?.ContinuesWithForwardSpeed(action.Edge) == true;
                bool followingEntry = action.Direct && action.Edge.Kind == NavigationAction.Walk && approaching;
                Vector2 live = followingEntry ? approach : action.Direct ? LiveTarget(body, feet) : target;
                Vector2 liveVelocity = followingEntry ? Vector2.Zero : this.targetVelocity;
                var frame = action.Input(body, native, supported, live, liveVelocity, followingEntry ? 1f : action.Direct ? LocalSpacing(body) : 0f,
                    threatDirection, emergency || rescuing, keepAirContact, ownerInAir, rollingWalk);
                if (action.DashPlanRejected)
                {
                    dashPreparation.Reset();
                    dashPreparedUntil = 0;
                }
                // Check the particular proved landing when a grounded
                // flight is actually about to hand over its native jump.
                // Once released, the airborne body keeps its controls.
                if (frame.Jump && supported && !action.Committed && activeFlightTouchdown.HasValue &&
                    !CompanionNativeContact.Supported(body, activeFlightTouchdown.Value))
                {
                    route.Abandon(action.Edge);
                    action.Reset();
                    ClearPrefix();
                    return new CompanionMobilityFrame { Status = "正在找路" };
                }
                if (rescuing) frame.Status = "缓解坠落";
                return frame;
            }
            return new CompanionMobilityFrame { Status = atGoal || desiredSpacing == 0f ? "停留" :
                QueryStatus == NavigationQueryStatus.AwaitingWorldData ? "等待地形" : "正在找路" };
        }

        private bool ReadyOffer(Player body, CompanionMobilityState native, Vector2 feet)
        {
            if (!hasOffer || !ReachableEntry(feet, offered) || !CanResume(native, offered) ||
                native.AwaitingEquipmentObservation && offered.Kind != NavigationAction.Walk) return false;
            if (offered.Kind == NavigationAction.Mount && offered.MountChoice.Valid && !offered.MountChoice.SourceCurrent(body) ||
                offeredFlightTouchdown.HasValue && !CompanionNativeContact.Supported(body, offeredFlightTouchdown.Value))
            {
                route.Abandon(offered);
                ClearPrefix();
                return false;
            }
            // StageMobilityInputs owns the native hand. A ready tool must
            // reach it to request the release edge while combat holds use.
            return true;
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
            activeLandingRisk = !direct && offeredLandingRisk;
            activeFlightTouchdown = direct ? null : offeredFlightTouchdown;
            actionEmergency = emergency;
            actionOwnerTarget = target;
            actionGeneration = bodyGeneration;
            if (noUsefulProgress == 0) progressBoundary = edge.From;
            if (edge.Kind == NavigationAction.Dash || edge.Detail == NavigationTraversalDetail.FlightDash)
            {
                action.DashPlan = dashPlan;
                dashPreparedUntil = 0;
            }
        }

        private bool CanResume(CompanionMobilityState native, NavigationTransition edge)
        {
            if (retrySuspendedAt == 0 || !NavigationRoute.SameFailure(edge, suspendedEdge)) return true;
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
            if (retireIntent)
            {
                // A matching native facility can complete during the old
                // action. Otherwise changing intent earns no route failure.
                // The first real landing or settled hook owns the handoff,
                // even when the old edge's endpoint was never reached.
                if (!action.AwaitingNativeTool(native) && (result == CompanionActionOutcome.Complete ||
                    action.CanYieldIntent(native, supported)))
                {
                    if (result == CompanionActionOutcome.Complete && action.Edge.Kind == NavigationAction.Facility)
                    {
#if DEBUG
                        FacilityUses++;
#endif
                    }
                    else action.Outcome = CompanionActionOutcome.Superseded;
                    retireIntent = false;
                }
                else if (Terminal(result)) action.Outcome = CompanionActionOutcome.Running;
                if (repositioned || gravityChanged) RetireBinding();
                return;
            }
            if (result == CompanionActionOutcome.Running)
            {
                Vector2 feet = CompanionNativeContact.Feet(body);
                // Measure passage across the actual floor/air envelope,
                // including intentional detours. Reissuing a local command
                // does not erase the age of progress at the same contact.
                Vector2 destination = action.ProgressTarget;
                bool horizontallyCloser = Math.Abs(destination.X - progressBoundary.X) -
                    Math.Abs(destination.X - feet.X) >= 10f;
                if (action.RoofEscapeDirection != 0)
                    horizontallyCloser = (feet.X - progressBoundary.X) * action.RoofEscapeDirection >= 10f;
                bool verticallyCloser = Math.Abs(destination.Y - progressBoundary.Y) -
                    Math.Abs(destination.Y - feet.Y) >= 12f;
                bool holding = keepAirContact && action.Direct && action.Edge.Kind == NavigationAction.Flight &&
                    CompanionMotionAction.Near(feet, target, spacing + 4f, 11f) && Math.Abs(targetVelocity.Y) < .4f ||
                    action.Edge.Kind == NavigationAction.Hook && state.GrappleAttached &&
                    (target.Y - feet.Y) * body.gravDir <= 18f && Math.Abs(target.X - feet.X) <= 36f;
                if (horizontallyCloser || verticallyCloser && (supported || action.Edge.Kind == NavigationAction.Flight))
                {
                    progressBoundary = feet;
                    noUsefulProgress = 0;
                    nextStallCheck = StallUpdates;
                }
                else if (!holding && !action.Braking && ++noUsefulProgress >= nextStallCheck && !action.Committed &&
                    action.Edge.Kind != NavigationAction.Hoik)
                    result = action.Outcome = CompanionActionOutcome.Blocked;
            }
            if (result == CompanionActionOutcome.Complete)
            {
                if (action.Edge.Kind == NavigationAction.Door || action.Edge.Kind == NavigationAction.Facility ||
                    action.Edge.Kind == NavigationAction.Mount || action.Edge.Kind == NavigationAction.Dismount ||
                    supported && (action.Edge.Kind == NavigationAction.Drop || action.Edge.Kind == NavigationAction.PlatformDrop) &&
                    Math.Abs(CompanionNativeContact.Feet(body).Y - progressBoundary.Y) > 8f)
                {
                    progressBoundary = CompanionNativeContact.Feet(body);
                    noUsefulProgress = 0;
                    nextStallCheck = StallUpdates;
                }
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
                if (!action.Direct && !rescuing) route?.Defer(action.Edge, false);
                ClearPrefix();
            }
            else if (result == CompanionActionOutcome.Superseded)
            {
                if (!action.Direct && !rescuing) route?.Abandon(action.Edge);
                ClearPrefix();
            }
            else if (Terminal(result))
            {
                // An actual failed candidate earns one bounded attempt at
                // an alternative. The same contact's stall age is retained.
                nextStallCheck = Math.Max(nextStallCheck, noUsefulProgress + 20);
#if DEBUG
                RejectedActions++;
#endif
                if (!action.Direct && !rescuing && route?.TryShortenFlight(action.Edge) != true)
                    route?.Reject(action.Edge, CompanionNativeContact.Feet(body));
                ClearPrefix();
            }
        }

        private void RetireBinding()
        {
            action.Reset();
            ClearPrefix();
            route?.Reset();
            Array.Clear(directFailures, 0, directFailures.Length);
            nextFacilities = dashPreparedUntil = retrySuspendedAt = 0;
            dashPreparation.Reset();
            dashPlan = default;
            retireIntent = rescuing = rescuePending = false;
            hasBlockedRoof = false;
            targetChanged = true;
            noUsefulProgress = 0;
            nextStallCheck = StallUpdates;
        }

        private void ClearPrefix()
        {
            hasOffer = false;
            refine.Reset();
        }

        internal void Advance(Player body, CompanionMobilityState native, CompanionOwnerTrail trail, NavigationBudget budget)
        {
            ObserveOwnerTrail(trail, native);
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
            if (retireIntent || !targetContactKnown) return;
            if (state.DashReady && !native.AwaitingEquipmentObservation &&
                Math.Abs(target.X - feet.X) > 48f && unchecked((int)(Main.GameUpdateCount - nextDashCheck)) >= 0)
            {
                int direction = Math.Sign(target.X - feet.X);
                if (dashPreparation.Advance(body, state, supported, direction, target, budget, out var plan, out bool rejected))
                {
                    dashPreparedUntil = Main.GameUpdateCount + 3;
                    dashPlan = plan;
                }
                // Pending budget and insufficient owner gap leave the
                // cursor ready for the next allocation. Back off only after
                // reading a real obstacle or unavailable contact band.
                else if (rejected) nextDashCheck = Main.GameUpdateCount + 10;
            }
            bool active = action.Id != 0 && !Terminal(action.Outcome);
            route.SetCommitted(active && action.Committed);
            if (active && action.Committed) return;
            bool directPrefix = active && action.Direct && !action.LosingProgress && !targetChanged;
            bool selectedApproach = route.TryTraversalApproach(feet, out _);
            bool needFlightMount = NeedsFlightMount(body, state, feet) && !native.AwaitingEquipmentObservation;
            bool needsDifferentFloor = action.Edge.Kind == NavigationAction.Walk && Math.Abs(target.Y - feet.Y) > 17f;
            var groundMount = GroundMountFixture(body, state, feet, budget);
            if (directPrefix && !groundMount.HasValue && !selectedApproach && !needFlightMount && !needsDifferentFloor &&
                (action.Edge.Kind != NavigationAction.Walk ||
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
            if (directPrefix && !groundMount.HasValue && !selectedApproach && !needFlightMount && !needsDifferentFloor && facilities.Count == 0 &&
                (!state.CanUseCart || !CompanionNavigationFacilities.HasNearbyTrack(feet, budget))) return;
            targetChanged = false;
            var capabilities = CompanionMotionProfile.Read(body, state, supported);
            Vector2 hint = default;
            bool hasHint = trail != null && trail.TryOutletHint(feet, target, Main.GameUpdateCount, out hint);
            route.SetTrailHint(hint, hasHint);
            route.SetLocalFixture(LocalFixture(body, feet, budget) ?? groundMount ?? LocalRiseFixture(body, native, feet, budget));
            route.SetTargetMotion(targetVelocity);
            route.SetTargetAirborne(targetAirborne);
            route.SetHookContact(state.GrappleAttached && !state.GrappleInFlight);
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
            if (active && !action.Direct && (!action.CanRetarget || action.Edge.SameEdge(edge))) return;
            if (hasOffer && offered.SameEdge(edge)) return;

            // Each accepted prefix carries only the cost to its next actual
            // decision boundary. The subsequent action uses fresh life,
            // equipment, and contact after the real landing or handoff.
            bool dependsOnLanding = edge.Irreversible || edge.Kind == NavigationAction.Flight ||
                edge.Kind == NavigationAction.Jump || edge.Kind == NavigationAction.Drop || edge.Kind == NavigationAction.PlatformDrop;
            if (!refine.Active || !refine.Edge.SameEdge(edge) ||
                dependsOnLanding && refine.EntryProtection != CompanionFallRisk.ProtectionKey(body))
            {
                refine.Begin(body, edge, supported);
            }
            var result = refine.Advance(body, state, budget);
            if (result == CompanionRefinementResult.Pending) return;
            if (result != CompanionRefinementResult.Ready)
            {
                if (route.TryShortenFlight(edge))
                {
                    ClearPrefix();
                    return;
                }
                if (result == CompanionRefinementResult.Unknown || result == CompanionRefinementResult.Unavailable)
                    route.Defer(edge, result == CompanionRefinementResult.Unknown);
                else route.Reject(edge, feet);
                ClearPrefix();
                return;
            }
            CompanionMotionWitness end = refine.Witness;
            offered = edge;
            offeredEntry = refine.Entry;
            offeredFallRelease = refine.FallRelease;
            offeredFallOutletX = refine.FallOutletX;
            offeredFlightLanding = refine.FlightLanding;
            offeredLifeCost = end.Valid ? refine.EntryLife - end.RemainingLife : 0;
            offeredProtection = refine.EntryProtection;
            offeredFlightTouchdown = edge.Kind == NavigationAction.Flight && end.Valid && end.Grounded
                ? (Vector2?)end.Position : null;
            // A reversible flight or jump can still carry a landing proof
            // whose safety depends on current life, including zero damage
            // granted by actual protection.
            offeredLandingRisk = edge.Irreversible || dependsOnLanding && end.Valid && end.Grounded;
            hasOffer = true;
        }

        private NavigationTransition? GroundMountFixture(Player body, CompanionMobilitySnapshot state, Vector2 feet,
            NavigationBudget budget)
        {
            float offset = target.X - feet.X;
            if (!supported || body.mount.Active || body.gravDir < 0 || state.HandBusy || body.noItems || body.cursed ||
                state.Wet || state.GrappleAttached || state.GrappleInFlight || state.Pulley ||
                state.MountChoices == null || state.MountChoices.Length == 0 || hasOffer || route.CorridorLength != 0 ||
                Math.Abs(offset) < 144f ||
                unchecked((int)(Main.GameUpdateCount - nextMountCheck)) < 0 || !budget.TryTiles(160)) return null;
            nextMountCheck = Main.GameUpdateCount + 12;
            int heading = Math.Sign(offset);
            float runway = Math.Min(192f, Math.Abs(offset));
            if (!CompanionGroundRun.TryFlatLane(body, heading * runway, false)) return null;

            var objective = new Vector2(feet.X + heading * runway, feet.Y);
            float best = CompanionGroundRun.DirectTravelTicks(body, state, NavigationAction.Walk, feet, objective);
            if (CanLiftDirectly(body, state, feet))
                best = Math.Min(best, CompanionGroundRun.DirectTravelTicks(body, state, NavigationAction.Flight, feet, objective));
            if (CanTakeDash(body, state))
                best = Math.Min(best, dashPlan.TravelTicks(body, state, feet, objective, false));
            var cell = NavigationTraversal.CellAt(feet);
            NavigationTransition? selected = null;
            foreach (var choice in state.MountChoices)
            {
                if (choice.Compact || !choice.SourceCurrent(body) || choice.RunSpeed <= 0f || choice.Acceleration <= 0f)
                    continue;
                float ticks = 12f + CompanionGroundRun.TravelTicks(runway, choice.RunSpeed, choice.Acceleration,
                    body.velocity.X * heading);
                var edge = new NavigationTransition(NavigationAction.Mount, feet, feet, choice, cell, cell,
                    detail: NavigationTraversalDetail.MountGround);
                if (ticks + 3f >= best || route.IsRejected(edge)) continue;
                var corner = feet - new Vector2(choice.Width * .5f + (heading < 0 ? runway : 0f), choice.Height);
                if (!budget.TryTiles((choice.Height / 16 + 2) * ((choice.Width + (int)runway) / 16 + 2))) break;
                if (!CompanionMobilityGeometry.ClearBody(corner, choice.Width + (int)runway, choice.Height)) continue;
                selected = edge;
                best = ticks;
            }
            return selected;
        }

        private NavigationTransition? LocalFixture(Player body, Vector2 feet, NavigationBudget budget)
        {
            int direction = Math.Sign(target.X - feet.X);
            if (!supported || route.CorridorLength != 0 || Math.Abs(target.X - feet.X) < 18f ||
                !budget.TryTiles(40) || !CompanionDoor.AtHorizontalInlet(body, direction, out var door)) return null;
            var aperture = door.Aperture;
            float farSide = direction > 0 ? aperture.Right : aperture.Left;
            if ((target.X - farSide) * direction < 10f) return null;
            var exit = new Vector2(farSide + direction * (body.width * .5f + 6f), feet.Y);
            return new NavigationTransition(NavigationAction.Door, feet, exit, entryTile: door.Target.ToTileCoordinates());
        }

        private NavigationTransition? LocalRiseFixture(Player body, CompanionMobilityState native, Vector2 feet, NavigationBudget budget)
        {
            var state = native.Observed;
            float rise = (feet.Y - target.Y) * body.gravDir;
            if (rise < -6f || route.CorridorLength != 0 || hasOffer || native.AwaitingEquipmentObservation ||
                state.Pulley || state.GrappleAttached || state.GrappleInFlight || state.Track || state.Wet || body.stoned ||
                !(CompanionMotionProfile.Wings(state) || state.RocketAvailable || CompanionMotionProfile.MountFlight(state)) ||
                Math.Abs(target.X - feet.X) <= 8f || StillBlockedByRoof(body, feet) || !budget.TryPredict() ||
                NearLine(body, feet, target) || !budget.TryTiles(40) || !budget.TryPredict(3)) return null;
            // A wall can block the diagonal while the body has a usable
            // vertical inlet. The shared refiner still requires enough real
            // lift, clearance and reserve for this bounded air endpoint.
            float clearance = MathHelper.Clamp(rise + 12f, 20f, 48f);
            Vector2 outlet = feet - new Vector2(0f, clearance * body.gravDir);
            if (!NearLine(body, feet, outlet, wholeLane: true)) return null;
            return new NavigationTransition(NavigationAction.Flight, feet, outlet,
                NavigationTraversal.CellAt(feet), NavigationTraversal.CellAt(outlet));
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

        private bool TryDirect(Player body, CompanionMobilityState native, Vector2 feet, bool emergency, out NavigationTransition edge)
        {
            edge = default;
            var state = native.Observed;
            if (state.Wet && !state.Shimmer && DirectReady(body, state, feet, target, DirectKind.Swim, emergency) && NearLine(body, feet, target))
            {
                edge = new NavigationTransition(NavigationAction.Swim, feet, target);
                return true;
            }
            var groundTarget = new Vector2(target.X, feet.Y);
            bool walk = DirectWalk(body, feet, state, target.X, LocalSpacing(body), targetVelocity.X, emergency);
            // Going back toward the same divider after a rejected launch
            // can undo the approach before an alternate launch is refined.
            // A complete nearby lane still permits an immediate reversal.
            if (walk && route?.HasRejectedLaunchNearby(feet) == true &&
                !CompanionGroundRun.TryFlatLane(body, target.X - feet.X, false)) walk = false;
            float horizontal = Math.Abs(target.X - feet.X);
            float rise = (feet.Y - target.Y) * body.gravDir;
            bool runway = false;
            if (walk && horizontal > 104f && CompanionMotionProfile.CanFly(state))
            {
                // A horizontal speed advantage can justify lift on an open
                // run. A nearby upcoming ledge belongs to the plan instead
                // of a cruise that changes its required approach posture.
                float lookAhead = Math.Max(body.width * 2f, Math.Abs(targetVelocity.X) * 12f);
                float span = Math.Min(192f, horizontal + lookAhead);
                runway = CompanionGroundRun.TryFlatLane(body, Math.Sign(target.X - feet.X) * span, false);
            }
            bool flight = !native.AwaitingEquipmentObservation && (!walk || rise > 24f || runway) && rise > -18f &&
                DirectReady(body, state, feet, target, DirectKind.Flight, emergency) &&
                CanLiftDirectly(body, state, feet);
            bool dash = !native.AwaitingEquipmentObservation && CanTakeDash(body, state) &&
                DirectReady(body, state, feet, supported ? groundTarget : target, supported ? DirectKind.Dash : DirectKind.FlightDash, emergency);
            if (!(walk || flight || dash)) return false;
            if (flight && rise > 24f)
            {
                // This candidate advances the unresolved rise. Don't rank
                // its climb against the time for a ground-only prefix.
                var airDetail = !supported && dash && dashPlan.TravelTicks(body, state, feet, target, true) + 2f <
                    CompanionGroundRun.DirectTravelTicks(body, state, NavigationAction.Flight, feet, target)
                    ? NavigationTraversalDetail.FlightDash : NavigationTraversalDetail.None;
                edge = new NavigationTransition(NavigationAction.Flight, feet, target, detail: airDetail);
                return true;
            }
            // Compare travel tools over the same local ground run while the
            // real two-dimensional owner location remains a planner input.
            Vector2 comparison = supported && (walk || dash) ? groundTarget : target;
            var kind = walk ? NavigationAction.Walk : NavigationAction.Flight;
            float best = walk ? CompanionGroundRun.DirectTravelTicks(body, state, kind, feet, comparison) : float.PositiveInfinity;
            float hold = action.Id != 0 && action.Direct && !action.LosingProgress ? 2f : 0f;
            if (flight)
            {
                float time = CompanionGroundRun.DirectTravelTicks(body, state, NavigationAction.Flight, feet, comparison);
                if (time + hold < best)
                {
                    kind = NavigationAction.Flight;
                    best = time;
                }
            }
            var detail = NavigationTraversalDetail.None;
            if (dash && dashPlan.TravelTicks(body, state, feet, comparison, !supported) + hold < best)
            {
                if (supported) kind = NavigationAction.Dash;
                else { kind = NavigationAction.Flight; detail = NavigationTraversalDetail.FlightDash; }
            }
            if (kind == NavigationAction.Flight && !flight && detail != NavigationTraversalDetail.FlightDash) return false;
            edge = new NavigationTransition(kind, feet, kind == NavigationAction.Flight ? target : groundTarget, detail: detail);
            return true;
        }

        private bool CanTakeDash(Player body, CompanionMobilitySnapshot state) =>
            dashPreparedUntil != 0 && unchecked((int)(dashPreparedUntil - Main.GameUpdateCount)) >= 0 &&
            dashPlan.CanLaunch(body, state, supported, Math.Sign(target.X - body.Center.X), target.X, false);

        private bool CanLiftDirectly(Player body, CompanionMobilitySnapshot state, Vector2 feet)
        {
            float rise = (feet.Y - target.Y) * body.gravDir;
            // Finite unprotected launch needs the shared native refinement
            // to a safe contact or controlled reserve. Steering an already
            // airborne body remains available while that route is sought.
            if (supported && (!CompanionFallRisk.ImmuneToDrop(body) || body.stoned)) return false;
            if (!CompanionMotionProfile.CanFly(state) || StillBlockedByRoof(body, feet) ||
                supported && rise <= 24f && !NearLine(body, feet, feet - new Vector2(0f, 18f * body.gravDir)))
                return false;
            // A newly mounted body can have genuine open vertical clearance
            // even when the current wall blocks every diagonal first step.
            return NearLine(body, feet, target) || !supported && rise > 24f && CompanionMotionProfile.MountFlight(state) &&
                CompanionMotionAction.ClearVertical(body, -(int)body.gravDir);
        }

        private bool NeedsFlightMount(Player body, CompanionMobilitySnapshot state, Vector2 feet)
        {
            if (supported || body.mount.Active || state.MountChoices == null || state.UnlimitedWings || state.Wet ||
                state.Pulley || state.GrappleAttached || state.GrappleInFlight) return false;
            float rise = (feet.Y - target.Y) * body.gravDir;
            float upwardSpeed = Math.Max(0f, -body.velocity.Y * body.gravDir);
            float coast = upwardSpeed * upwardSpeed / (2f * Math.Max(.05f, body.gravity));
            float available = state.WingTime + (state.RocketAvailable ? (state.RocketTime + 1) * Math.Max(8, state.RocketDelay) : 0);
            // Keep an active native wing as the local prefix. Ask the same
            // route for a mounted continuation before its observed reserve
            // can no longer reach the higher moving objective.
            if (rise <= 40f || rise + 20f <= coast + available * Math.Max(3f, state.JumpSpeed)) return false;
            foreach (var choice in state.MountChoices)
                if (choice.CanFly) return true;
            return false;
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

        private bool DirectWalk(Player body, Vector2 feet, CompanionMobilitySnapshot state, float x, float band, float speed, bool emergency)
        {
            if (!supported || state.GrappleAttached || state.Pulley || state.Track ||
                !DirectReady(body, state, feet, new Vector2(x, feet.Y), DirectKind.Walk, emergency)) return false;
            int direction = CompanionMotionProfile.Steer(body, state, x, speed, band, false);
            if (direction == 0) return false;
            return WalkStride(body, direction, Math.Max(7f, Math.Abs(body.velocity.X) + 2f));
        }

        private static bool WalkStride(Player body, int direction, float stride) =>
            CompanionMotionAction.ClearAhead(body, direction, stride, true) ||
                CompanionGroundRun.HasShallowStepOff(body, direction) &&
                CompanionMotionAction.ClearAhead(body, direction, stride, false, allowSteps: true);

        private void RememberDirectFailure(Player body, CompanionMobilitySnapshot state, Vector2 feet)
        {
            var kind = action.Edge.Kind == NavigationAction.Walk ? DirectKind.Walk : action.Edge.Kind == NavigationAction.Dash ? DirectKind.Dash :
                action.Edge.Kind == NavigationAction.Swim ? DirectKind.Swim :
                action.Edge.Detail == NavigationTraversalDetail.FlightDash ? DirectKind.FlightDash : DirectKind.Flight;
            Vector2 goal = action.ProgressTarget;
            int direction = Math.Sign(goal.X - feet.X);
            int elevation = Elevation(goal, feet);
            float contactY = elevation == 0 ? -body.height * .5f * body.gravDir :
                elevation * (elevation * body.gravDir < 0f ? body.height + 5f : 8f);
            Point contact = NavigationTraversal.CellAt(feet + new Vector2(direction * (body.width * .5f + 6f), contactY));
            directFailures[(int)kind] = new DirectFailure
            {
                Until = Main.GameUpdateCount + 36, Position = feet, Supported = supported,
                Contact = contact, Direction = direction, Elevation = elevation, Width = body.width, Height = body.height,
                Source = DirectSource(state, kind), FootVersion = TerrainVersion(NavigationTraversal.CellAt(feet)),
                ContactVersion = TerrainVersion(contact)
            };
        }

        private bool DirectReady(Player body, CompanionMobilitySnapshot state, Vector2 feet, Vector2 goal, DirectKind kind, bool emergency)
        {
            // Retry only the failed input at this local contact. A stalled
            // launch must never occupy the next known horizontal stride.
            var failure = directFailures[(int)kind];
            return emergency || failure.Until == 0 || unchecked((int)(Main.GameUpdateCount - failure.Until)) >= 0 ||
                failure.Supported != supported || failure.Width != body.width || failure.Height != body.height ||
                Vector2.DistanceSquared(feet, failure.Position) > 24f * 24f || failure.Direction != Math.Sign(goal.X - feet.X) ||
                failure.Elevation != Elevation(goal, feet) || failure.Source != DirectSource(state, kind) ||
                failure.FootVersion != TerrainVersion(NavigationTraversal.CellAt(failure.Position)) ||
                failure.ContactVersion != TerrainVersion(failure.Contact);
        }

        private static int Elevation(Vector2 goal, Vector2 feet) => Math.Abs(goal.Y - feet.Y) > 16f ? Math.Sign(goal.Y - feet.Y) : 0;

        private int TerrainVersion(Point cell) => world != null && world.TryVersion(cell, out _, out int version) ? version : 0;

        private static int DirectSource(CompanionMobilitySnapshot state, DirectKind kind)
        {
            if (kind == DirectKind.Dash) return state.DashType;
            if (kind == DirectKind.Walk) return state.MountType;
            if (kind == DirectKind.Swim) return state.MountType * 2 + (state.CanSwim ? 1 : 0);
            return unchecked((((state.MountType * 397 + state.Wings) * 397 + state.RocketBoots) * 397 + state.DashType) * 4 +
                (state.RocketAvailable ? 1 : 0) + (state.CarpetReady ? 2 : 0));
        }

        private bool CloseTarget(Player body, Vector2 feet)
        {
            // A nearby airborne owner still needs the original hover input
            // on the next update. Idle would spend that frame falling.
            if (keepAirContact || !CompanionMotionAction.Near(feet, target, LocalSpacing(body), 10f)) return false;
            // A short same-floor distance can still cross a divider. A flat
            // lane or native stair sweep must connect the two contact points.
            if (supported) return CompanionGroundRun.ConnectGroundTarget(body, target);
            return NearLine(body, feet, target);
        }

        private float LocalSpacing(Player body)
        {
            // When the owner has stopped, the body settles toward the inner
            // part of its following band. Moving input keeps the extra room
            // for native braking and matches the observed owner velocity.
            return supported && !targetAirborne && spacing >= 30f && Math.Abs(targetVelocity.X) < .4f
                ? spacing - body.width * .4f : spacing;
        }

        private Vector2 LiveTarget(Player body, Vector2 feet)
        {
            bool walk = action.Edge.Kind == NavigationAction.Walk;
            var destination = walk || action.Edge.Kind == NavigationAction.Dash ? new Vector2(target.X, feet.Y) : target;
            if (!walk && action.Edge.Kind != NavigationAction.Flight) return destination;
            float dx = target.X - feet.X;
            if (Math.Abs(dx) <= spacing + 12f || Math.Abs(targetVelocity.X) < 1f) return destination;
            float lead = MathHelper.Clamp(targetVelocity.X * 2f, -12f, 12f);
            // Follow only the short observed lane. GroundInput still owns
            // braking and support, and planned detours never lead the owner.
            if (dx * lead <= 0f || Math.Abs(dx) > 56f) return destination;
            if (walk)
            {
                if (!CompanionGroundRun.TryFlatLane(body, dx + lead, false)) return destination;
            }
            else if (!NearLine(body, feet, target + new Vector2(lead, 0f), wholeLane: true)) return destination;
            return destination + new Vector2(lead, 0f);
        }

        private static bool NearLine(Player body, Vector2 from, Vector2 to, bool wholeLane = false)
        {
            Vector2 direction = to - from;
            float length = direction.Length();
            if (length <= 1f) return true;
            int steps = wholeLane ? (int)Math.Ceiling(length / 18f) : 1;
            Vector2 passage = direction * Math.Min(1f / steps, 18f / length);
            Vector2 position = body.position;
            var platforms = CompanionNativeContact.PlatformContact.Native(body, false, false);
            for (int i = 0; i < steps; i++)
            {
                if (!CompanionNativeContact.TryMove(body, position, passage, platforms, false, out var contact) ||
                    Vector2.DistanceSquared(contact.Position, position + passage) > 5f * 5f ||
                    !(body.lavaImmune || body.lavaTime > 24 || body.lavaWet) &&
                        CompanionMobilityGeometry.TouchesLava(contact.Position, body.width, body.height) ||
                    CompanionMobilityGeometry.TouchesShimmer(contact.Position, body.width, body.height)) return false;
                position = contact.Position;
            }
            return true;
        }

        private static bool SameLocalTarget(Vector2 previous, Vector2 current) =>
            (int)(previous.X / 64f) == (int)(current.X / 64f) && (int)(previous.Y / 32f) == (int)(current.Y / 32f);
        private static bool ReachableEntry(Vector2 feet, NavigationTransition edge) =>
            CompanionMotionAction.Near(feet, edge.From, edge.Kind == NavigationAction.Walk ? 48f : 36f, 24f);
        private static bool Terminal(CompanionActionOutcome outcome) => outcome == CompanionActionOutcome.Complete ||
            outcome == CompanionActionOutcome.Blocked || outcome == CompanionActionOutcome.Rejected ||
            outcome == CompanionActionOutcome.Suspended || outcome == CompanionActionOutcome.Superseded;
    }
}
