using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class CompanionNavigation
    {
        private enum ActionResult { None, Waiting, Delivered, Accepted, Advancing, Reached, LandedElsewhere, Rejected, Deviated, Reanchor, Superseded, Retired }

        private readonly struct ActionOutcome
        {
            internal readonly long ActionId;
            internal readonly long PlanId;
            internal readonly RouteStep Step;
            internal readonly ActionResult Result;
            internal readonly Vector2 Position;
            internal readonly Vector2 Velocity;
            internal readonly bool NativeAccepted, ToolAttempted;
            internal readonly bool LastStep, PrefixTrimmed;
            internal readonly CompanionMovementDeferral Deferral;
            internal readonly int Health, FallKey;

            internal ActionOutcome(long actionId, long planId, RouteStep step, ActionResult result, Player body, bool nativeAccepted,
                bool toolAttempted = false, CompanionMovementDeferral deferral = CompanionMovementDeferral.None,
                bool lastStep = false, bool prefixTrimmed = false)
            {
                ActionId = actionId;
                PlanId = planId;
                Step = step;
                Result = result;
                Position = body.position;
                Velocity = body.velocity;
                NativeAccepted = nativeAccepted;
                ToolAttempted = toolAttempted;
                Deferral = deferral;
                LastStep = lastStep;
                PrefixTrimmed = prefixTrimmed;
                Health = body.statLife;
                bool failed = result == ActionResult.Rejected || result == ActionResult.Deviated;
                FallKey = failed ? DescentSafetyKey(body) : 0;
            }
        }

        private sealed partial class ActionExecutor
        {
            private readonly CompanionNavigation navigation;
            private List<RouteStep> route = new List<RouteStep>();
            private int routeIndex;
            private long nextId, actionId, planId, deliveredId, controllingId;
            private int age, sequence, commandSequence, noProgress;
            private bool jumpStarted, jumpAccepted, actionStarted, landedElsewhere, awaitingJumpDelivery, rearmJump, retriedJump, toolAttempted;
            private bool descentValidated, movedThroughAction, hoikDisplaced;
            private bool prefixTrimmed;
            private Vector2 progressPosition, actionOrigin, routeGoal;
            private CompanionMovementDelivery delivery;
            private CompanionMovementDeferral deferral;
            private bool pendingOutcome;
            private DoorPassageProof doorActivationProof;
            private int doorActivationSequence;
            private ActionResult nextFailure;
            internal ActionOutcome Outcome { get; private set; }
            internal bool HasStep => routeIndex < route.Count;
            internal int Remaining => route.Count - routeIndex;
            internal RouteStep Current => HasStep ? route[routeIndex] : default;
            internal Vector2 Goal => routeGoal;
            internal bool Started => actionStarted;
            private bool HasNativeMotion => actionStarted || jumpAccepted || hoikDisplaced;

            internal ActionExecutor(CompanionNavigation navigation)
            {
                this.navigation = navigation;
            }

            private void Begin(Vector2 position)
            {
                actionId = ++nextId;
                actionOrigin = progressPosition = position;
            }

            private void ResetStep()
            {
                actionId = deliveredId = controllingId = 0;
                age = noProgress = commandSequence = 0;
                jumpStarted = jumpAccepted = actionStarted = awaitingJumpDelivery = rearmJump = retriedJump = landedElsewhere = toolAttempted = false;
                descentValidated = movedThroughAction = hoikDisplaced = false;
            }

            internal void Release()
            {
                route.Clear();
                routeIndex = 0;
                planId = 0;
                prefixTrimmed = false;
                ResetStep();
            }

            internal void RetireBody(Player body)
            {
                if (actionId != 0)
                {
                    Outcome = new ActionOutcome(actionId, planId, Current, ActionResult.Retired, body, HasNativeMotion);
                    pendingOutcome = true;
                }
                Release();
            }

            internal bool TryTakeOutcome(out ActionOutcome outcome)
            {
                outcome = Outcome;
                bool available = pendingOutcome;
                pendingOutcome = false;
                return available;
            }

            internal void Adopt(RouteStep step, Vector2 goal)
            {
                Release();
                routeGoal = goal;
                route.Add(step);
            }

            internal void Adopt(List<RouteStep> steps, Vector2 goal, long identity)
            {
                Release();
                routeGoal = goal;
                planId = identity;
                // Ready is consumed once. Transfer its proven prefix and
                // avoid a second list and copy at each successful handoff.
                route = steps;
            }

            internal void TrimSuffix()
            {
                if (Remaining <= 1) return;
                route.RemoveRange(routeIndex + 1, Remaining - 1);
                prefixTrimmed = true;
            }

            internal ActionOutcome Interrupt(Player body)
            {
                Outcome = new ActionOutcome(actionId, planId, Current, ActionResult.Superseded, body, HasNativeMotion);
                Release();
                return Outcome;
            }

            internal void Retarget(Vector2 goal)
            {
                routeGoal = goal;
            }

            internal ActionOutcome Finish(Player body, RouteStep step, bool reached)
            {
                var result = reached ? ActionResult.Reached : landedElsewhere ? ActionResult.LandedElsewhere : nextFailure;
                if (result == ActionResult.Rejected && (HasNativeMotion || movedThroughAction)) result = ActionResult.Deviated;
                Outcome = new ActionOutcome(actionId, planId, step, result, body, HasNativeMotion, toolAttempted,
                    lastStep: Remaining == 1, prefixTrimmed: prefixTrimmed);
                if (reached)
                {
                    routeIndex++;
                    ResetStep();
                }
                else Release();
                return Outcome;
            }

            internal void RetireGoal(Player body, CompanionMobilityState state, RouteMotion motion)
            {
                if (!HasStep) return;
                var step = Current;
                bool committed;
                switch (step.Kind)
                {
                    case RouteKind.Drop: case RouteKind.LedgeDrop:
                        committed = actionStarted || body.stairFall || motion == RouteMotion.Air &&
                            body.velocity.Y * body.gravDir > 0 && (body.position.Y - actionOrigin.Y) * body.gravDir > 0;
                        break;
                    case RouteKind.Jump: case RouteKind.GlideJump: case RouteKind.RopeExit:
                        committed = jumpAccepted;
                        break;
                    case RouteKind.Hoik:
                        committed = hoikDisplaced || HoikEngaged(body, state.Observed, step);
                        break;
                    default:
                        committed = actionStarted && commandSequence != 0 && state.Sequence == commandSequence &&
                            (state.Result == CompanionMobilityResult.Started || state.Result == CompanionMobilityResult.Completed);
                        break;
                }
                if (committed) TrimSuffix();
                else
                {
                    Outcome = new ActionOutcome(actionId, planId, step, ActionResult.Superseded, body, false);
                    pendingOutcome = true;
                    Release();
                }
            }

            internal CompanionMobilityFrame Issue(Player body, CompanionMobilityFrame frame)
            {
                // One decision is delivered per native Player.Update. A
                // direct walk/recovery gets the same receipt as a plan step.
                if (actionId == 0) Begin(body.position);
                if (HasStep && controllingId != actionId) deferral = CompanionMovementDeferral.NativeCondition;
                controllingId = 0;
                frame.ActionId = actionId;
                return frame;
            }

            internal void Record(CompanionMovementDelivery actual)
            {
                if (actual.ActionId == 0 || actual.ActionId != actionId) return;
                deliveredId = actual.ActionId;
                delivery = actual;
            }

            internal void Observe(Player body, CompanionMobilityState native)
            {
                if (deliveredId == 0 || deliveredId != actionId || delivery.Generation != native.Generation) return;
                deliveredId = 0;
                if (body.dead || !body.active)
                {
                    Outcome = new ActionOutcome(actionId, planId, Current, ActionResult.Retired, body, false);
                    pendingOutcome = true;
                    Release();
                    return;
                }

                if (delivery.NativeShapeExit && native.ShapeHandExit && native.Result == CompanionMobilityResult.Completed && HasStep &&
                    !HasNativeMotion)
                {
                    Outcome = new ActionOutcome(actionId, planId, Current, ActionResult.Reanchor, body, false);
                    pendingOutcome = true;
                    Release();
                    return;
                }

                var nativeResult = !delivery.NativeShapeExit && delivery.Sequence != 0 && native.Sequence == delivery.Sequence && native.Action == delivery.Activation ?
                    native.Result : CompanionMobilityResult.Deferred;
                toolAttempted |= delivery.ToolAdmitted && nativeResult != CompanionMobilityResult.Deferred;
                bool toolDeferred = delivery.Activation != CompanionMovementActivation.None && nativeResult == CompanionMobilityResult.Deferred;
                bool nativeAccepted = nativeResult == CompanionMobilityResult.Completed || nativeResult == CompanionMobilityResult.Started &&
                    (delivery.Activation == CompanionMovementActivation.Hook && native.Observed.GrappleInFlight ||
                     delivery.Activation == CompanionMovementActivation.QuickCart && body.mount.Cart);
                if (nativeAccepted) actionStarted = true;
                if (nativeResult == CompanionMobilityResult.Completed && HasStep && IsRelocation(delivery.Activation))
                {
                    // These native consumers report completion from the
                    // actual teleport. The old suffix still starts in the
                    // old room, even if the displacement was small.
                    Outcome = new ActionOutcome(actionId, planId, Current, ActionResult.Reached, body, true,
                        lastStep: true, prefixTrimmed: prefixTrimmed || Remaining > 1);
                    pendingOutcome = true;
                    Release();
                    return;
                }
                if (awaitingJumpDelivery)
                {
                    // Upward native velocity alone could be a rebound or an
                    // external impulse. It verifies this jump only after the
                    // action's request actually reaches the body.
                    jumpStarted = delivery.Jump;
                    jumpAccepted = jumpStarted && body.velocity.Y * body.gravDir < 0 &&
                        (body.position.Y - delivery.Position.Y) * body.gravDir < 0;
                    nativeAccepted |= jumpAccepted;
                    awaitingJumpDelivery = false;
                }
                else if (rearmJump && !delivery.Jump && body.releaseJump)
                {
                    jumpStarted = rearmJump = false;
                    retriedJump = true;
                }
                var active = Current;
                bool moved = Vector2.DistanceSquared(progressPosition, body.position) > 4;
                if (HasStep && active.Kind == RouteKind.Hoik && !delivery.InputsDeferred && delivery.Move == active.Direction)
                {
                    // Collision can clear the body's horizontal velocity at
                    // each tooth while native steps still make tiny progress.
                    moved = (body.position.X - delivery.Position.X) * active.Direction > .01f;
                    if (!hoikDisplaced && HoikDisplaced(body, active, delivery.Position, delivery.Move))
                        nativeAccepted = hoikDisplaced = true;
                }
                bool descentAccepted = HasStep && (active.Kind == RouteKind.Drop || active.Kind == RouteKind.LedgeDrop) &&
                    (!delivery.StairFall && body.stairFall && delivery.Vertical > 0 ||
                     !delivery.InputsDeferred && (body.position.Y - delivery.Position.Y) * body.gravDir > .125f);
                actionStarted |= descentAccepted;

                if (moved) { progressPosition = body.position; noProgress = 0; }
                var reason = deferral != CompanionMovementDeferral.None ? deferral : delivery.Deferral;
                if (reason == CompanionMovementDeferral.None && (delivery.InputsDeferred || toolDeferred))
                    reason = CompanionMovementDeferral.NativeCondition;
                bool deferred = reason != CompanionMovementDeferral.None;
                movedThroughAction |= moved && !deferred && (delivery.Move != 0 || delivery.Jump || delivery.Vertical != 0 || delivery.Dash);
                if (!deferred)
                {
                    age++;
                    if (!moved) noProgress++;
                }
                var result = nativeAccepted || descentAccepted ? ActionResult.Accepted : deferred ? ActionResult.Waiting :
                    moved ? ActionResult.Advancing : ActionResult.Delivered;
                Outcome = new ActionOutcome(actionId, planId, active, result, body, nativeAccepted || HasNativeMotion,
                    toolAttempted, reason);
                pendingOutcome = true;
                if (!HasStep) ResetStep();
            }

            private static bool IsRelocation(CompanionMovementActivation activation) =>
                activation == CompanionMovementActivation.Pylon || activation == CompanionMovementActivation.Portal ||
                activation == CompanionMovementActivation.Rod || activation == CompanionMovementActivation.Teleporter;

            internal bool Step(Player body, CompanionMobilityState state, CompanionMobilitySnapshot mobility,
                RouteMotion motion, RouteStep step, out CompanionMobilityFrame frame, out bool finished, bool allowDescentCommit = false)
            {
                frame = new CompanionMobilityFrame { Status = "移动中" };
                nextFailure = ActionResult.Rejected;
                landedElsewhere = false;
                finished = false;
                deferral = CompanionMovementDeferral.None;
                if (actionId == 0) Begin(body.position);
                controllingId = actionId;

                bool inVerifiedDescent = actionStarted && (step.Kind == RouteKind.Drop || step.Kind == RouteKind.LedgeDrop);
                if (step.Kind == RouteKind.Hoik)
                {
                    if (age > step.Duration || noProgress > 24) return false;
                }
                else if (!inVerifiedDescent && age > (step.Kind == RouteKind.GlideJump ? 190 : 150) ||
                    noProgress > 24 && !actionStarted && !jumpAccepted) return false;

                using (Scratch.Save())
                {
                    if (!Footprint(body, body.position, body.velocity)) { nextFailure = ActionResult.Reanchor; return false; }
                    float along = (step.To.X - body.position.X) * step.Direction;
                    float vertical = (step.To.Y - body.position.Y) * (body.gravDir < 0 ? -1 : 1);
                    int direction = step.Direction;
                    if (jumpStarted && !jumpAccepted)
                    {
                        // A delivered press without takeoff can reflect a
                        // transient native release phase. Consume one real
                        // neutral update before rechecking the same proof.
                        if (retriedJump || motion != RouteMotion.Ground && motion != RouteMotion.Rope)
                        { nextFailure = ActionResult.Reanchor; return false; }
                        rearmJump = true;
                        deferral = CompanionMovementDeferral.ReleaseInput;
                        return true;
                    }
                    switch (step.Kind)
                    {
                        case RouteKind.Walk:
                            if (motion == RouteMotion.Ground && along >= -4 && along <= Math.Abs(step.To.X - step.From.X) + 4 &&
                                Math.Abs(vertical) <= 4 &&
                                routeIndex + 1 < route.Count)
                            {
                                var departure = route[routeIndex + 1];
                                if (departure.Kind == RouteKind.LedgeDrop && departure.Direction == step.Direction && departure.From == step.To)
                                {
                                    // The next step owns acceleration, braking
                                    // and actual departure checks at the edge.
                                    // Ordinary Walk intentionally stops earlier.
                                    finished = true;
                                    return false;
                                }
                                if (departure.Kind == RouteKind.Jump && departure.Direction == step.Direction &&
                                    departure.From == step.To && departure.HasEntryVelocity && departure.EntryVelocityX * direction > .8f)
                                {
                                    if (along <= 8 && !state.AwaitingEquipmentObservation && body.velocity.Y == 0 &&
                                        (body.releaseJump || body.autoJump) && JumpEntryReady(body, mobility, departure) &&
                                        JumpLandingAtLiveEntry(body, mobility, departure))
                                    {
                                        // A checked takeoff owns the last few
                                        // pixels of its runway. Generic braking
                                        // would remove its proved launch speed.
                                        finished = true;
                                        return false;
                                    }
                                    if (along <= 0 || !CanAdvanceJumpRunway(body, mobility, direction))
                                    {
                                        if (body.velocity.X * direction > 0 && CanBrakeJumpRunway(body, mobility, direction, body.velocity.X, 0))
                                        {
                                            frame.Move = -direction;
                                            return true;
                                        }
                                        return false;
                                    }
                                    frame.Move = direction;
                                    return true;
                                }
                            }
                            if (along <= 4 && Math.Abs(vertical) <= 18) { finished = motion == RouteMotion.Ground; if (finished) return false; }
                            if (motion != RouteMotion.Ground || !QuickWalk(body, direction, out _)) return false;
                            frame.Move = direction;
                            return true;
                        case RouteKind.Jump: case RouteKind.RopeExit: case RouteKind.GlideJump:
                            bool glide = step.Kind == RouteKind.GlideJump;
                            if (glide && !jumpStarted && (!mobility.CarpetAvailable || mobility.Wet))
                            { nextFailure = ActionResult.Reanchor; return false; }
                            if (!jumpStarted)
                            {
                                if (motion != RouteMotion.Ground && motion != RouteMotion.Rope) return false;
                                if (state.AwaitingEquipmentObservation) { deferral = CompanionMovementDeferral.EquipmentObservation; frame.Status = "重新判断起跳"; return true; }
                                if (step.Launch.Captured && !step.Launch.Equals(new JumpInputs(body, mobility)))
                                {
                                    // A previously certified jump cannot launch
                                    // with different physics. Rejoin the existing
                                    // frontier with this real pose and capabilities.
                                    nextFailure = ActionResult.Reanchor;
                                    return false;
                                }
                                frame.Status = "跨越障碍";
                                if (motion == RouteMotion.Ground && body.velocity.Y != 0 && !mobility.Sliding) return true;
                                if (!body.releaseJump && !body.autoJump && motion != RouteMotion.Rope) { deferral = CompanionMovementDeferral.ReleaseInput; return true; }
                                if (step.HasEntryVelocity && (!JumpEntryReady(body, mobility, step) ||
                                    !glide && !JumpLandingAtLiveEntry(body, mobility, step)))
                                {
                                    float adjust = step.From.X - body.position.X;
                                    if (direction == 0 && motion == RouteMotion.Ground && Math.Abs(adjust) > .25f &&
                                        Math.Abs(adjust) <= 8 && Math.Abs(step.From.Y - body.position.Y) <= 2)
                                    {
                                        int approach = Math.Sign(adjust);
                                        float speed = body.velocity.X * approach;
                                        if (speed > 0 && TryStopDistance(body, speed + body.runAcceleration, 0, out float brake) &&
                                            brake >= Math.Abs(adjust)) return true;
                                        if (QuickWalk(body, approach, out _))
                                        {
                                            // Even a short walk can overshoot an
                                            // exact platform lip. Align using the
                                            // same legal ground input before the
                                            // native takeoff, not mid-jump.
                                            frame.Move = approach;
                                            return true;
                                        }
                                    }
                                    nextFailure = ActionResult.Reanchor;
                                    return false;
                                }
                                bool plannedJump = step.Kind == RouteKind.Jump && step.HasEntryVelocity;
                                if (plannedJump ? !navigation.PlannedJumpInputClear(body, mobility, direction, afterLaunch: false, hold: true) :
                                    !NextClear(body, 0, -(int)body.gravDir)) return false;
                                // A walk prefix may outlive its far bank. Check
                                // the landing again at the actual takeoff input.
                                if (glide ? !GlideLandingClear(body, step.To, new Vector2(direction * 12, 0), null) :
                                    !SolidForBody(body, step.To, null) || !Surface(body, step.To, null)) return false;
                                frame.Jump = true; awaitingJumpDelivery = true; frame.Move = direction;
                                actionOrigin = body.position;
                                return true;
                            }
                            if (motion == RouteMotion.Ground)
                            {
                                if (step.Kind == RouteKind.Jump && step.HasEntryVelocity)
                                {
                                    finished = NearbySupportedGoal(body, step.To);
                                    landedElsewhere = !finished;
                                    return false;
                                }
                                finished = (!glide || actionStarted) && Math.Abs(vertical) <= 24 && (direction == 0 || along <= 32);
                                return false;
                            }
                            bool hold = mobility.JumpTime > 0 && body.velocity.Y * body.gravDir < 0;
                            if (step.Kind == RouteKind.Jump && step.HasEntryVelocity)
                            {
                                // The graph proves a combined native Jump/Move
                                // trajectory. Near a platform edge, releasing the
                                // axis four pixels early changes the landing.
                                if (navigation.PlannedJumpInputClear(body, mobility, direction, afterLaunch: true, hold: hold))
                                {
                                    frame.Move = direction;
                                    frame.Jump = hold;
                                }
                                else if (hold && navigation.PlannedJumpInputClear(body, mobility, 0, afterLaunch: true, hold: true))
                                    frame.Jump = true;
                            }
                            else
                            {
                                frame.Move = along > 4 && NextClear(body, direction, 0) ? direction : 0;
                                frame.Jump = hold && NextClear(body, 0, -(int)body.gravDir);
                            }
                            frame.Status = "跳跃中";
                            if (glide && !frame.Jump && mobility.JumpTime == 0)
                            {
                                if (!actionStarted && body.carpetFrame >= 0 && mobility.CarpetTime > 0) actionStarted = true;
                                if (frame.Move != 0 && (actionStarted || body.velocity.Y >= -mobility.Gravity) && mobility.CarpetReady)
                                {
                                    frame.Jump = true;
                                    frame.Status = "滑翔中";
                                }
                                else if (!actionStarted && along > 4 && body.velocity.Y > 2 * mobility.Gravity) return false;
                            }
                            return true;
                        case RouteKind.Drop: case RouteKind.LedgeDrop:
                            bool descentRunning = DriveDescentStep(body, state, mobility, motion, step, allowDescentCommit,
                                out frame, out DescentOutcome descentOutcome);
                            finished = descentOutcome == DescentOutcome.Reached;
                            landedElsewhere = descentOutcome == DescentOutcome.LandedElsewhere;
                            return descentRunning;
                        case RouteKind.Descend:
                            if (age > 1 && vertical <= 4 && (direction == 0 || along <= 5) &&
                                body.velocity.Y * body.gravDir >= -.01f)
                            {
                                if (step.Retreat && motion == RouteMotion.Air)
                                {
                                    // The descent already happened. Wait for the
                                    // native equipment refresh before choosing a
                                    // powered exit from its new actual position.
                                    if (state.AwaitingEquipmentObservation)
                                    {
                                        deferral = CompanionMovementDeferral.EquipmentObservation;
                                        frame.Status = "下降中";
                                        return true;
                                    }
                                    if (ContinueAirRetreat(body, mobility, routeGoal, ref step))
                                    {
                                        route[routeIndex] = step;
                                        return AirInput(body, mobility, motion, step.Direction,
                                            step.Kind == RouteKind.AirAcross ? 0 : 1, out frame);
                                    }
                                }
                                finished = true;
                                return false;
                            }
                            return motion == RouteMotion.Air && DriveFlight(body, mobility, motion, step.To, Vector2.Zero, 3, 1, out frame);
                        case RouteKind.Ascend: case RouteKind.AirAcross:
                            if (step.Retreat)
                            {
                                if (state.AwaitingEquipmentObservation)
                                {
                                    deferral = CompanionMovementDeferral.EquipmentObservation;
                                    return true;
                                }
                                if (HoverReserve(body, mobility) <= 0) { nextFailure = ActionResult.Reanchor; return false; }
                                if (step.Kind == RouteKind.Ascend)
                                {
                                    if (vertical >= -4)
                                    {
                                        if (!TryShortExitAscent(body, mobility, routeGoal, ref step))
                                        { finished = true; return false; }
                                        route[routeIndex] = step;
                                    }
                                    return AirInput(body, mobility, motion, 0, -1, out frame, quickAscent: vertical < -64);
                                }
                                if (Math.Abs(step.To.X - body.position.X) <= 8 && Math.Abs(body.velocity.X) < 2 ||
                                    motion == RouteMotion.Ground)
                                {
                                    if (step.Vertical == 0 || !TryShortExitAscent(body, mobility, routeGoal, ref step))
                                    { finished = true; return false; }
                                    route[routeIndex] = step;
                                    return AirInput(body, mobility, motion, 0, -1, out frame);
                                }
                                if (motion != RouteMotion.Air || !CanHoldRetreat(body, mobility, Math.Sign(step.To.X - body.position.X),
                                    Math.Abs(step.To.X - body.position.X))) { nextFailure = ActionResult.Reanchor; return false; }
                                return DriveFlight(body, mobility, motion, step.To, Vector2.Zero, 2, 0, out frame);
                            }
                            if (step.Kind == RouteKind.Ascend ? vertical >= -3 : along <= 5 && Math.Abs(vertical) <= 14)
                            { finished = true; return false; }
                            if (step.Kind == RouteKind.AirAcross && (motion != RouteMotion.Air || along < -24)) return false;
                            int heightIntent = step.Kind == RouteKind.Ascend || step.To.Y != step.From.Y && vertical < -3 ?
                                -1 : FlightHeight(body, mobility, step.To.Y, 0, 10);
                            return DriveFlight(body, mobility, motion, step.To, Vector2.Zero, 3, heightIntent, out frame);
                        case RouteKind.Swim:
                            if (!mobility.Wet || (step.Vertical == 0 ? along <= 5 : Math.Abs(vertical) <= 5)) { finished = true; return false; }
                            if (!CanLeaveLiquid(body, direction, step.Vertical, mobility.Lava && mobility.LavaTime > 24)) return false;
                            frame.Move = direction;
                            frame.Jump = step.Vertical == -(int)body.gravDir && mobility.CanSwim && (mobility.ReleaseJump || body.merman);
                            if (mobility.MountSwim) frame.Vertical = step.Vertical;
                            frame.Status = "游动中";
                            return true;
                        case RouteKind.Rope:
                            if (motion == RouteMotion.Rope && (step.To.Y - body.position.Y) * step.Vertical <= 4) { finished = true; return false; }
                            if (!TryRopeEdge(body, body.position, step.Vertical, null, out _)) return false;
                            frame.Vertical = step.Vertical;
                            frame.Status = "攀绳中";
                            return true;
                        case RouteKind.Track:
                            if (motion != RouteMotion.Track) return false;
                            if (along <= 5)
                            {
                                Vector2 actualMagnet = body.position + RailMagnetOffset(body);
                                finished = (int)actualMagnet.Y / 16 == (int)step.Operand.Y / 16 && Math.Abs(vertical) <= 10;
                                return false;
                            }
                            if (!TrackCell((int)step.Operand.X / 16, (int)step.Operand.Y / 16, null) || !NextClear(body, direction, step.Vertical)) return false;
                            frame.Move = direction; frame.Vertical = step.Vertical; frame.Status = "矿车行驶中";
                            return true;
                        case RouteKind.Door:
                            return DriveDoorStep(body, state, step, allowDescentCommit, out frame, out finished);
                        case RouteKind.Hoik:
                            DriveHoikStep(body, mobility, motion, step, hoikDisplaced, out frame, out var hoikOutcome);
                            finished = hoikOutcome == HoikOutcome.Reached;
                            landedElsewhere = hoikOutcome == HoikOutcome.LandedElsewhere;
                            return hoikOutcome == HoikOutcome.Running;
                        default:
                            return ActivationStep(body, state, mobility, step, out frame, out finished);
                    }
                }
            }

            private bool ActivationStep(Player body, CompanionMobilityState state, CompanionMobilitySnapshot mobility,
                RouteStep step, out CompanionMobilityFrame frame, out bool finished)
            {
                frame = default(CompanionMobilityFrame);
                finished = false;
                if (step.Kind == RouteKind.Portal && !actionStarted)
                {
                    Vector2 entry = step.Operand - body.Center;
                    int toward = Math.Sign(entry.X);
                    bool contact = Math.Abs(entry.X) <= body.width + 12 &&
                        CompanionMobilityGeometry.PortalEntry(body, step.Operand, out _, out _);
                    if (!contact)
                    {
                        if (toward == 0 || !QuickWalk(body, toward, out _)) return false;
                        frame.Move = toward;
                        frame.Status = "接近传送门";
                        return true;
                    }
                    frame.Move = toward;
                }
                if (commandSequence != 0 && state.Sequence == commandSequence)
                {
                    if (state.Result == CompanionMobilityResult.Rejected) return false;
                    if (state.Result == CompanionMobilityResult.Completed)
                    {
                        if (step.Kind == RouteKind.Hook && mobility.GrappleAttached &&
                            Vector2.DistanceSquared(body.position, step.To) > 16 * 16 && body.velocity.LengthSquared() > 1)
                        { frame.Status = "抓钩牵引中"; return true; }
                        if (step.Kind == RouteKind.ReleaseHook && step.Direction == 0 && step.Vertical > 0 &&
                            (step.To.Y - body.position.Y) * body.gravDir > 4 &&
                            (routeGoal.Y - body.position.Y) * body.gravDir > 16 &&
                            (body.velocity.Y != 0 || !Surface(body, body.position, null)) &&
                            NextClear(body, 0, (int)body.gravDir))
                        {
                            // Detaching ends the hook action, but its upward
                            // inertia can precede the requested free descent.
                            // Do not start a new tool operation before observing
                            // the released body carry out this short prefix.
                            frame.Status = "下降中";
                            return true;
                        }
                        if (step.Kind == RouteKind.Mount || step.Kind == RouteKind.Dismount || step.Kind == RouteKind.Cart)
                            navigation.postureChangedAt = Main.GameUpdateCount;
                        finished = true;
                        return false;
                    }
                }

                if (commandSequence == 0) commandSequence = ++sequence;
                frame.Status = "准备移动";
                frame.Activation = ActivationFor(step.Kind);
                frame.WorldX = step.Operand.X;
                frame.WorldY = step.Kind == RouteKind.FlipGravity ? -body.gravDir : step.Operand.Y;
                frame.ItemSlot = step.ItemSlot;
                frame.Sequence = commandSequence;
                frame.MountTypePlusOne = step.MountTypePlusOne;
                frame.EmergencyHand = step.EmergencyHand;
                if (step.Kind == RouteKind.ReleaseHook)
                {
                    if (!mobility.GrappleAttached || !NextClear(body, step.Direction, step.Vertical * (int)body.gravDir)) return false;
                    frame.Move = step.Direction;
                    frame.Vertical = step.Vertical;
                }
                return frame.Activation != CompanionMovementActivation.None;
            }


        }
    }
}
