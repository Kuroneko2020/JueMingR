using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal enum CompanionActionOutcome { Idle, Running, Waiting, Complete, Blocked, Rejected, Suspended, Superseded }

    // One action spans staging, the original Player.Update and actual observation.
    // Deferred hands do not count against contact progress, but can time out.
    internal sealed class CompanionMotionAction
    {
        internal NavigationTransition Edge;
        internal CompanionFallRisk.ReleaseCorridor FallRelease;
        internal float FallOutletX;
        internal CompanionDashMotion.Plan DashPlan;
        internal bool DashPlanRejected { get; private set; }
        internal const float FlightBankClearance = 4f;
        internal const float FlightClimbTolerance = 4f;
        internal bool FlightLanding;
        internal long Id;
        internal bool Direct, Committed;
        internal bool CanRetarget => Id != 0 && !Committed &&
            (Edge.Kind == NavigationAction.Walk || Edge.Kind == NavigationAction.Dash ||
             Edge.Kind == NavigationAction.Jump || Edge.Kind == NavigationAction.Drop || Edge.Kind == NavigationAction.PlatformDrop ||
             Edge.Kind == NavigationAction.Flight);
        internal CompanionActionOutcome Outcome;
        private enum Phase { Approach, Release, Travel }
        private Phase phase;
        private float best, walkBand;
        private int noProgress, waiting, elapsed, sequence, roofEscape, roofEscapeAge, roofEscapeRow, ledgeBrakeDirection;
        private Vector2 target, entry, roofEscapeStart;
        private CompanionMovementDelivery delivered;
        private bool hasDelivery, startedMovement, airborne, hookReleasing, dashIssued, dashBraking, unsafeRelease;
        private bool cancellingDrop, crossedJumpHeight;
        private bool observedInlet, groundedInlet, maintainingAirContact, clearedFlightBank, levelCruise, roofDropping;
        internal CompanionMovementDeferral Deferral { get; private set; }
        internal bool LosingProgress => noProgress >= 4;
        internal bool Braking { get; private set; }
        internal Vector2 ProgressTarget => phase == Phase.Approach ? entry : target;
        internal bool InNativeMotion { get; private set; }
        internal bool HasRoofContact { get; private set; }
        internal Vector2 RoofContact { get; private set; }
        internal int RoofEscapeDirection => roofEscape;

        internal bool TryRetarget(NavigationTransition edge)
        {
            if (!Direct || !CanRetarget || Edge.Kind != edge.Kind) return false;
            if (Edge.Detail != edge.Detail) dashIssued = dashBraking = DashPlanRejected = false;
            Edge = edge;
            target = edge.To;
            return true;
        }

        internal void Begin(long id, NavigationTransition edge, bool direct, Vector2 launch)
        {
            Edge = edge;
            Id = id;
            FallRelease = default;
            DashPlan = default;
            DashPlanRejected = false;
            FallOutletX = edge.To.X;
            Direct = direct;
            Outcome = CompanionActionOutcome.Running;
            phase = direct ? Phase.Travel : Phase.Approach;
            Committed = false;
            best = float.NegativeInfinity;
            noProgress = waiting = elapsed = sequence = 0;
            roofEscape = roofEscapeAge = ledgeBrakeDirection = 0;
            roofDropping = false;
            hasDelivery = startedMovement = airborne = hookReleasing = dashIssued = dashBraking = unsafeRelease = false;
            cancellingDrop = crossedJumpHeight = false;
            observedInlet = groundedInlet = maintainingAirContact = levelCruise = false;
            clearedFlightBank = FlightLanding = false;
            InNativeMotion = false;
            Braking = false;
            HasRoofContact = false;
            Deferral = CompanionMovementDeferral.None;
            target = edge.To;
            entry = launch;
        }

        internal void Reset()
        {
            Id = 0;
            Outcome = CompanionActionOutcome.Idle;
            hasDelivery = false;
        }

        internal void Record(CompanionMovementDelivery delivery)
        {
            if (Id == 0 || Id != delivery.ActionId) return;
            delivered = delivery;
            hasDelivery = true;
        }

        internal bool CompletedFacilityReceipt(CompanionMobilityState native) => Id != 0 &&
            Edge.Kind == NavigationAction.Facility && delivered.ActionId == Id && delivered.Generation == native.Generation &&
            delivered.Activation != CompanionMovementActivation.None && delivered.Activation == native.Action &&
            delivered.Sequence == native.Sequence && native.Result == CompanionMobilityResult.Completed;

        // A later offer can name another sub-operation before the original
        // command finishes. The native sequence still names this action.
        internal bool AwaitingNativeTool(CompanionMobilityState native) => native.Result == CompanionMobilityResult.Started &&
            !native.ShapeHandExit && native.Action != CompanionMovementActivation.None && native.Sequence == ToolSequence(native.Action);

        internal bool CanYieldIntent(CompanionMobilityState native, bool supported) =>
            !native.Observed.DashActive && !native.Observed.GrappleInFlight &&
            (supported && (!Committed || Edge.Kind == NavigationAction.Hoik && Outcome == CompanionActionOutcome.Blocked) ||
                native.Observed.GrappleAttached);

        internal CompanionMobilityFrame Input(Player body, CompanionMobilityState native, bool supported,
            Vector2 liveTarget, Vector2 liveVelocity, float spacing, int threatDirection, bool emergency,
            bool keepAirContact, bool ownerInAir, bool rollingWalk)
        {
            DashPlanRejected = false;
            var state = native.Observed;
            Vector2 feet = CompanionNativeContact.Feet(body);
            maintainingAirContact = Direct && Edge.Kind == NavigationAction.Flight && keepAirContact &&
                Near(feet, liveTarget, spacing + 4f, 11f);
            if (!observedInlet)
            {
                observedInlet = true;
                groundedInlet = supported;
                levelCruise = Direct && supported && Edge.Kind == NavigationAction.Flight &&
                    Math.Abs(liveTarget.Y - feet.Y) <= 8f && Math.Abs(liveTarget.X - feet.X) > spacing + 64f;
            }
            target = Direct ? liveTarget : Edge.To;
            walkBand = spacing;
            var frame = new CompanionMobilityFrame { ActionId = Id, ItemSlot = -1, Status = "跟随中" };
            if (IsDrop && FallReleased(body, feet, supported)) phase = Phase.Travel;
            if (IsDrop && phase != Phase.Travel && native.AwaitingEquipmentObservation)
            {
                // Retain the pre-release action while equipment settles,
                // with native braking so existing momentum cannot release it.
                BrakeDrop(body, supported, ref frame);
                return frame;
            }
            if (!Direct && (Edge.Kind == NavigationAction.Walk || Edge.Kind == NavigationAction.Hoik || Edge.Kind == NavigationAction.Dash))
                phase = Phase.Travel;
            else if (phase == Phase.Approach && Aligned(body, feet))
            {
                phase = Phase.Release;
                noProgress = 0;
                best = float.NegativeInfinity;
            }

            if (phase == Phase.Approach)
            {
                // Hold release/drop authority until our own body reaches the
                // accepted entry. The owner's new target may only change the tail.
                if (Edge.Kind == NavigationAction.Dismount && state.MountCart && Math.Abs(body.velocity.X) > 1.25f)
                {
                    if (state.Track) frame.Move = -Math.Sign(body.velocity.X);
                    frame.Status = "减速中";
                    return frame;
                }
                float arrivalSpeed = IsRunningJump ? Math.Sign(Edge.To.X - Edge.From.X) * body.maxRunSpeed : 0f;
                frame.Move = GroundInput(body, state, entry.X, arrivalSpeed, 0f, supported, coastAtEdge: IsDrop);
                return frame;
            }

            if (state.GrappleAttached && Edge.Kind != NavigationAction.Hook ||
                state.GrappleInFlight && Edge.Kind == NavigationAction.PlatformDrop)
            {
                hookReleasing = true;
                SetTool(ref frame, CompanionMovementActivation.ReleaseHook, body.Center);
                return frame;
            }

            Vector2 velocity = Direct ? liveVelocity : Vector2.Zero;
            switch (Edge.Kind)
            {
                case NavigationAction.Walk:
                    // A walk does not own an unplanned fall. A short stair
                    // transition can still take its next legal stride.
                    if (supported || body.velocity.Y * body.gravDir <= 1f ||
                        CompanionGroundRun.HasShallowStepOff(body, Math.Sign(target.X - feet.X)))
                    {
                        float arrivalSpeed = rollingWalk ? Math.Sign(Edge.To.X - Edge.From.X) * body.maxRunSpeed : velocity.X;
                        frame.Move = GroundInput(body, state, target.X, arrivalSpeed, spacing, supported, coastAtEdge: true);
                    }
                    if (state.Pulley && frame.Move != 0) frame.Jump = state.ReleaseJump;
                    break;
                case NavigationAction.Dash:
                    if (state.DashActive)
                    {
                        SteerDash(body, state, supported, target.X, spacing, false, ref frame);
                        break;
                    }
                    frame.Move = GroundInput(body, state, target.X, 0f, 2f, supported);
                    int launch = Math.Sign(target.X - feet.X);
                    if (!dashIssued && supported && launch != 0 && frame.Move != -launch &&
                        launch == Math.Sign(Edge.To.X - Edge.From.X))
                    {
                        DashPlanRejected = !DashPlan.CanLaunch(body, state, true, launch, target.X, true);
                        if (!DashPlanRejected)
                        {
                            // A steady run can release its ordinary throttle
                            // at speed cap. The pulse still needs direction.
                            frame.Move = frame.Dash = launch;
                        }
                    }
                    break;
                case NavigationAction.Hoik:
                    // Native horizontal input keeps the body on the proved
                    // wedge. Do not release when native collision clears vx.
                    frame.Move = Math.Sign(Edge.To.X - Edge.From.X);
                    break;
                case NavigationAction.Drop:
                case NavigationAction.PlatformDrop:
                    Drop(body, state, feet, supported, ref frame);
                    break;
                case NavigationAction.Jump:
                    int intoGap = Math.Sign(Edge.To.X - Edge.From.X);
                    if (IsRunningJump && supported && !airborne && body.velocity.X * intoGap < 1.2f)
                        frame.Move = GroundInput(body, state, entry.X, intoGap * body.maxRunSpeed, 0f, true);
                    else
                    {
                        frame.Move = CompanionMotionProfile.Steer(body, state, target.X, 0, 2f, !supported);
                        frame.Jump = Jump(body, state, supported);
                    }
                    break;
                case NavigationAction.Flight:
                    if (Edge.Detail == NavigationTraversalDetail.CarpetGlide)
                        Glide(body, state, feet, supported, ref frame);
                    else
                    {
                        bool cruise = levelCruise && Math.Abs(target.Y - entry.Y) <= 16f &&
                            Math.Abs(target.X - feet.X) > spacing + 34f;
                        var destination = cruise ? target - new Vector2(0, 32f * body.gravDir) : target;
                        if (CompanionMotionProfile.CanFly(state) || Edge.Detail != NavigationTraversalDetail.FlightDash)
                            Fly(body, state, feet, destination, velocity, spacing, supported, ref frame,
                                levelHold: cruise || Direct && keepAirContact && Math.Abs(liveVelocity.Y) < .4f,
                                preferDescent: Direct ? !cruise && !ownerInAir : (Edge.To.Y - Edge.From.Y) * body.gravDir > 6f);
                        else frame.Move = CompanionMotionProfile.Steer(body, state, target.X, velocity.X, spacing, !supported);
                        if (Edge.Detail == NavigationTraversalDetail.FlightDash)
                        {
                            // DashMovement alters X before WingMovement in
                            // Player.Update. Keep native lift/hover control in
                            // the very same frame, including during the pulse.
                            int heading = Math.Sign(target.X - feet.X);
                            if (state.DashActive)
                                SteerDash(body, state, supported, target.X, spacing, roofEscape != 0 || roofDropping, ref frame);
                            else if (!dashIssued && !supported)
                            {
                                DashPlanRejected = !DashPlan.CanLaunch(body, state, false, heading, target.X, true, frame.Vertical > 0);
                                if (frame.Move != -heading && roofEscape == 0 && !roofDropping && !DashPlanRejected)
                                {
                                    frame.Move = heading;
                                    frame.Dash = heading;
                                }
                                else if (CompanionMotionProfile.CanFly(state)) RetireAirDash();
                                else DashPlanRejected = true;
                            }
                        }
                    }
                    break;
                case NavigationAction.Swim:
                    if (state.Wet)
                    {
                        frame.Move = CompanionMotionProfile.Steer(body, state, target.X, velocity.X, spacing, true);
                        frame.Vertical = Math.Abs(target.Y - feet.Y) > 6f ? Math.Sign(target.Y - feet.Y) : 0;
                        frame.Jump = (target.Y - feet.Y) * body.gravDir < -5f &&
                            (state.CanSwim || state.MountSwim || state.JumpTime > 0 || supported && state.ReleaseJump) &&
                            ClearVertical(body, -(int)body.gravDir);
                    }
                    else
                    {
                        frame.Move = Math.Sign(target.X - feet.X);
                        frame.Jump = target.Y < feet.Y - 8 && Jump(body, state, supported);
                    }
                    break;
                case NavigationAction.Rope:
                    frame.Vertical = Math.Sign(target.Y - feet.Y);
                    if (frame.Vertical == 0 || state.Pulley && Math.Abs(target.Y - feet.Y) <= 10f)
                    {
                        frame.Move = Math.Sign(target.X - feet.X);
                        frame.Jump = frame.Move != 0 && state.ReleaseJump;
                    }
                    else if (!state.Pulley)
                    {
                        var ropeX = Edge.EntryTile.X * 16f + 8f;
                        frame.Move = GroundInput(body, state, ropeX, 0, 1f, supported);
                    }
                    break;
                case NavigationAction.Track:
                    if (!state.MountCart && Edge.Detail == NavigationTraversalDetail.TrackRide)
                        unsafeRelease = true;
                    else if (!state.MountCart)
                        SetTool(ref frame, CompanionMovementActivation.QuickCart, TileCenter(Edge.EntryTile));
                    else
                    {
                        bool braking = TrackShouldBrake(body, feet, liveTarget);
                        frame.Move = braking ? TrackBrakeInput(body) : Math.Sign(target.X - feet.X);
                        frame.Vertical = Edge.RailDirection;
                        if (braking) frame.Status = "减速中";
                    }
                    break;
                case NavigationAction.Hook:
                    if (state.GrappleAttached)
                    {
                        bool down = (liveTarget.Y - feet.Y) * body.gravDir > 18f;
                        bool otherSide = Math.Abs(liveTarget.X - feet.X) > 36f;
                        bool madePull = (Edge.From.Y - feet.Y) * body.gravDir > 12f ||
                            Math.Abs(Edge.From.X - feet.X) > 12f;
                        if (madePull && (down && !otherSide || otherSide && Near(feet, target, 18f, 16f)))
                        {
                            hookReleasing = true;
                            SetTool(ref frame, CompanionMovementActivation.ReleaseHook, body.Center);
                        }
                        else if (Math.Abs(target.X - feet.X) > 4f) frame.Move = Math.Sign(target.X - feet.X);
                    }
                    else if (!hookReleasing)
                        SetTool(ref frame, CompanionMovementActivation.Hook, TileCenter(Edge.ExitTile));
                    break;
                case NavigationAction.Mount:
                    Mount(body, ref frame);
                    break;
                case NavigationAction.Dismount:
                    if (!state.MountCart || Math.Abs(body.velocity.X) <= 1.25f)
                        SetTool(ref frame, CompanionMovementActivation.Dismount, body.Center);
                    else
                    {
                        if (state.Track) frame.Move = -Math.Sign(body.velocity.X);
                        frame.Status = "减速中";
                    }
                    break;
                case NavigationAction.Gravity:
                    SetTool(ref frame, CompanionMovementActivation.Gravity,
                        new Vector2(0, Edge.To.Y < Edge.From.Y ? -1 : 1));
                    break;
                case NavigationAction.Door:
                    Door(body, state, feet, supported, ref frame);
                    break;
                case NavigationAction.Facility:
                    Facility(body, state, ref frame);
                    break;
            }
            if (emergency)
            {
                frame.EmergencyHand = true;
                if (frame.Move == 0 && threatDirection != 0 && (Edge.Kind == NavigationAction.Walk || Edge.Kind == NavigationAction.Swim) &&
                    ClearAhead(body, threatDirection, 5f, supported)) frame.Move = threatDirection;
            }
            // The Ram Rune consumes Down+Jump on a fresh jump edge. Wing
            // hover needs that same pair only after jump is already held.
            if (!IsDrop && frame.Jump && frame.Vertical > 0 && CanStartRam(body, state, supported))
                frame.Vertical = 0;
            return frame;
        }

        private void SteerDash(Player body, CompanionMobilitySnapshot state, bool supported, float destination, float spacing,
            bool yield, ref CompanionMobilityFrame frame)
        {
            int heading = Math.Sign(body.velocity.X);
            frame.Move = DashPlan.ActiveInput(body, state, supported, destination - heading * spacing, yield || dashBraking,
                frame.Drop || frame.Vertical > 0);
            // A stop already delivered belongs to this pulse. Shorter
            // projected distance on its next step does not resume throttle.
            dashBraking |= frame.Move != heading || heading == 0;
            frame.Status = dashBraking ? "减速中" : "冲刺中";
        }

        private void RetireAirDash()
        {
            // The optional pulse is a single budgeted candidate. Resume the
            // existing flight control and leave any next pulse to selection.
            if (!Direct) return;
            Edge = new NavigationTransition(NavigationAction.Flight, Edge.From, Edge.To);
            dashIssued = false;
        }

        private bool Aligned(Player body, Vector2 feet)
        {
            if (Edge.Kind == NavigationAction.Door || Edge.Kind == NavigationAction.Track)
                return Math.Abs(feet.X - Edge.From.X) <= 24f && Math.Abs(feet.Y - Edge.From.Y) <= 18f;
            if (Edge.Kind == NavigationAction.Flight || Edge.Kind == NavigationAction.Swim)
                return Near(feet, Edge.From, 32f, 32f);
            float xTolerance = Edge.Kind == NavigationAction.Hook || Edge.Kind == NavigationAction.Facility ? 22f : 10f;
            if (IsRunningJump) xTolerance = 4f;
            if (Edge.Kind == NavigationAction.PlatformDrop)
            {
                if (body.gravDir * (feet.Y - entry.Y) > 4f && Math.Abs(feet.X - entry.X) < 16f) return true;
                bool shifted = entry.X != Edge.From.X;
                if (!Near(feet, entry, shifted ? 2f : 7f, 16f)) return false;
                // The shifted approach may be within its position tolerance
                // while one foot still rests on solid ground. A native Down
                // sweep must use the first gravity step: a faster forecast
                // can collide with the same platform despite holding Down.
                if (!shifted) return true;
                float fallSpeed = Math.Min(Math.Max(4f, body.maxFallSpeed), body.velocity.Y * body.gravDir + Math.Max(.05f, body.gravity));
                return fallSpeed > 0f && CompanionNativeContact.TryMove(body, body.position, new Vector2(0f, fallSpeed * body.gravDir),
                    CompanionNativeContact.PlatformContact.Native(body, true, true), false, out var release) &&
                    !release.Ground && (release.Position.Y - body.position.Y) * body.gravDir > 0f;
            }
            return Near(feet, entry, xTolerance, 14f);
        }

        private bool IsRunningJump => Edge.Detail == NavigationTraversalDetail.RunningJump;

        private static bool TrackShouldBrake(Player body, Vector2 feet, Vector2 goal)
        {
            float offset = goal.X - feet.X;
            if (Math.Abs(goal.Y - feet.Y) > 38f) return false;
            if (Math.Abs(offset) <= 30f) return true;
            float speed = Math.Abs(body.velocity.X);
            // A settled native cart coasts on neutral. Opposite input
            // subtracts the track acceleration until real low speed.
            float friction = body.runAcceleration;
            if (friction <= 0f || Math.Sign(offset) != Math.Sign(body.velocity.X)) return false;
            // The default cart's neutral input subtracts the actual native
            // track friction per update. Sum the strides without a scan.
            float steps = Math.Max(0f, (float)Math.Ceiling(speed / friction) - 1f);
            float stoppingDistance = steps * speed - friction * steps * (steps + 1f) * .5f;
            return Math.Abs(offset) <= 18f + stoppingDistance;
        }

        private static int TrackBrakeInput(Player body) => Math.Abs(body.velocity.X) > .12f ? -Math.Sign(body.velocity.X) : 0;

        private bool IsDrop => Edge.Kind == NavigationAction.Drop || Edge.Kind == NavigationAction.PlatformDrop;

        private bool FallReleased(Player body, Vector2 feet, bool supported)
        {
            float depth = (feet.Y - Edge.From.Y) * body.gravDir;
            return observedInlet && !supported && (!groundedInlet || depth > 1f ||
                Edge.Kind == NavigationAction.PlatformDrop && body.stairFall && depth > .05f);
        }

        private static void BrakeDrop(Player body, bool supported, ref CompanionMobilityFrame frame)
        {
            int direction = -Math.Sign(body.velocity.X);
            if (supported && direction != 0 && ClearAhead(body, direction, 3f, true)) frame.Move = direction;
            frame.Status = "减速中";
        }

        private void Drop(Player body, CompanionMobilitySnapshot state, Vector2 feet, bool supported, ref CompanionMobilityFrame frame)
        {
            int gravity = body.gravDir < 0 ? -1 : 1;
            bool platform = Edge.Kind == NavigationAction.PlatformDrop;
            if (phase != Phase.Travel && (cancellingDrop || !FallRelease.CanRelease(body, feet, target, supported)))
            {
                // Damage and accessories can change while aligning. Never
                // release the source support with stale survivability.
                BrakeDrop(body, supported, ref frame);
                cancellingDrop = true;
                unsafeRelease = Math.Abs(body.velocity.X) <= .25f || frame.Move == 0;
                return;
            }
            if (platform && (phase != Phase.Travel || (feet.Y - Edge.From.Y) * gravity < 6f))
            {
                // Down holds the source release through the native stairFall
                // phase. Let go before approaching the destination platform.
                frame.Drop = gravity > 0;
                frame.Move = CompanionMotionProfile.Steer(body, state, entry.X, 0f, 2f, !supported);
            }
            else
            {
                bool releasingLedge = !platform && phase != Phase.Travel && groundedInlet;
                // The verified first landing can be a toe-width overlap.
                // Keep the next native stride at a wall face until actual
                // support arrives, even if the bank is one pixel away.
                frame.Move = releasingLedge ? Math.Sign(FallOutletX - entry.X) :
                    CompanionMotionProfile.Steer(body, state, target.X, 0f, supported ? 1.5f : .25f, !supported);
                // A shifted release beside solid support must not steer
                // back into that support while the body is still crossing
                // its height. Keep safe air steering and the normal Down tail.
                if (platform && (entry.X - Edge.From.X) * frame.Move < 0f &&
                    (feet.Y - Edge.From.Y) * gravity < body.height + 16f &&
                    !ClearAhead(body, frame.Move, Math.Max(3f, Math.Abs(body.velocity.X) + 1f), false)) frame.Move = 0;
            }
            if (state.MountHover) frame.Vertical = gravity;
            if (phase == Phase.Travel && !frame.Drop && CanStartRam(body, state, supported) &&
                (target.Y - feet.Y) * gravity > 40f && Math.Abs(target.X - feet.X) <= 12f &&
                Math.Abs(body.velocity.X) < 1f && CanReleaseRam(body, feet, target))
            {
                frame.Jump = frame.Drop = true;
            }
            frame.Status = "下降中";
        }

        private static bool CanStartRam(Player body, CompanionMobilitySnapshot state, bool supported) =>
            body.hasDeadCellsDownDash && !body.isPerformingJump_DownDash && !supported && !body.mount.Active &&
            body.velocity.Y != 0f && state.ReleaseJump && state.JumpTime == 0 && !state.Sliding &&
            !state.Wet && !state.Controlled && !state.GrappleAttached && !state.Pulley &&
            !body.canJumpAgain_Basilisk && !body.canJumpAgain_WallOfFleshGoat &&
            !body.canJumpAgain_Santank && !body.canJumpAgain_Unicorn;

        private static bool CanReleaseRam(Player body, Vector2 feet, Vector2 landing)
        {
            // JumpMovement gives the Ram Rune a downward impulse and the
            // native dry path can move twice. Prove both Down segments on
            // the already selected fall, then let go of Down before landing.
            var pass = CompanionNativeContact.PlatformContact.Native(body, true, true);
            var hold = CompanionNativeContact.PlatformContact.Native(body, false, false);
            var position = body.position;
            float stepY = (16f + body.gravity) * body.gravDir;
            for (int i = 0; i < 2; i++)
            {
                var velocity = new Vector2(i == 0 ? body.velocity.X : 0f, stepY);
                if (!CompanionNativeContact.TryMove(body, position, velocity, pass, false, out var step) ||
                    !CompanionNativeContact.TryMove(body, position, velocity, hold, false, out var normal) ||
                    step.Position != normal.Position || step.Ground || step.Wall ||
                    CompanionFallRisk.OutsideModeledLiquid(body, step.Position, body.width, body.height)) return false;
                position = step.Position;
            }
            return CompanionFallRisk.CanLand(body, feet, landing, false);
        }

        private bool Jump(Player body, CompanionMobilitySnapshot state, bool supported)
        {
            if (!ClearVertical(body, -(int)body.gravDir)) return false;
            if (supported || state.Pulley) return state.ReleaseJump || body.autoJump || !state.Ready && body.releaseJump;
            if (state.JumpTime > 0 && body.velocity.Y * body.gravDir < 0) return true;
            return (state.ExtraJumps > 0 || state.Sliding) && state.ReleaseJump && body.velocity.Y * body.gravDir >= -.5f;
        }

        private bool Fly(Player body, CompanionMobilitySnapshot state, Vector2 feet, Vector2 destination, Vector2 velocity,
            float spacing, bool supported, ref CompanionMobilityFrame frame, bool levelHold = false, bool preferDescent = false)
        {
            int gravity = body.gravDir < 0 ? -1 : 1;
            float error = (destination.Y - feet.Y) * gravity;
            float projected = error - (body.velocity.Y - velocity.Y) * gravity * 4f;
            // An airborne corridor step has a 12px arrival band. Native
            // hover may hold a moving owner within 16px, but can hold a
            // planned body outside its next step indefinitely.
            float tolerance = levelHold ? 7f : Direct && state.WingHovering ? 16f : supported ? 8f : 10f;
            bool climbingToOutlet = !Direct && !FlightLanding && (Edge.From.Y - Edge.To.Y) * gravity > 8f;
            if (climbingToOutlet) tolerance = FlightClimbTolerance;
            int elevation = projected < -tolerance ? -1 : projected > tolerance ? 1 : 0;
            if (climbingToOutlet && error < -FlightClimbTolerance && elevation > 0) elevation = 0;
            // Successive downhill flight samples can be closer than the
            // hover band. Let native gravity reach them instead of spending
            // reserve holding a pose above the intended continuation.
            if (preferDescent && error > 4f && elevation == 0) elevation = 1;
            if (Direct && levelHold && !supported && !FlightLanding && elevation > 0 &&
                body.velocity.Y * gravity < -.5f && Math.Abs(error) <= 16f &&
                state.WingHover && CompanionMotionProfile.Wings(state))
            {
                // A level owner calls for catching the remaining climb
                // now. Native hover can brake that climb and spend the
                // same frame on useful horizontal pursuit.
                elevation = 0;
            }
            if (FlightLanding)
            {
                // Complete a supported endpoint with a native landing.
                // While the foot is still below the bank, keep using the
                // actual lift path even inside the hover tolerance.
                if (error >= FlightBankClearance) clearedFlightBank = true;
                elevation = clearedFlightBank ? Math.Max(0, elevation) : -1;
            }
            int toward = Math.Sign(destination.X - feet.X);
            bool liftBesideLedge = (Direct && preferDescent || FlightLanding && !clearedFlightBank) && gravity > 0 && error < -.25f &&
                Math.Abs(destination.X - feet.X) > spacing && NeedsOpenColumn(body, toward, destination.Y);
            if (liftBesideLedge) elevation = -1;
            if (ContinueRoofExit(body, state, supported, gravity, toward, error, ref frame)) return true;
            bool hover = !FlightLanding && elevation == 0 && !supported && state.WingHover && CompanionMotionProfile.Wings(state);
            frame.Move = CompanionMotionProfile.Steer(body, state, destination.X, velocity.X, spacing, !supported, hover);
            if (frame.Move != 0 && !ClearAhead(body, frame.Move, Math.Max(3f, Math.Abs(body.velocity.X)), supported))
                frame.Move = 0;
            if (liftBesideLedge)
            {
                // Lift beside the known overhang before entering its span.
                // A clear lower lateral stride alone cannot reach the upper
                // support when it takes the body beneath that lip.
                int opposing = Math.Sign(body.velocity.X) == toward ? -toward : 0;
                frame.Move = opposing != 0 && ClearAhead(body, opposing, 4f, false) ? opposing : 0;
            }
            if (elevation < 0 && !LiftRoom(body, gravity, out bool hitRoof))
            {
                // Coast beneath a roof. A blocked lift must not be converted
                // to hover, which would keep the body against the same roof.
                if (hitRoof && Direct && frame.Move == 0)
                {
                    HasRoofContact = true;
                    RoofContact = body.position;
                    if (gravity > 0 && !supported)
                    {
                        roofEscapeAge = 0;
                        roofEscapeRow = RoofRow(body);
                        roofEscapeStart = body.position;
                        roofEscape = FindNearbyRoofSide(body, toward, roofEscapeRow);
                        roofDropping = roofEscape == 0;
                        if (ContinueRoofExit(body, state, supported, gravity, toward, error, ref frame)) return true;
                    }
                }
                elevation = 1;
                hover = false;
            }
            if (state.MountHover) frame.Vertical = elevation * gravity;
            if (elevation < 0)
            {
                bool wing = CompanionMotionProfile.Wings(state);
                frame.Jump = supported ? Jump(body, state, true) : wing || CompanionMotionProfile.MountFlight(state) ||
                    state.RocketReady || Jump(body, state, false);
                if (wing && state.WingFastAscent && error < -64f) frame.Vertical = -gravity;
            }
            else if (hover)
            {
                frame.Vertical = 1;
                frame.Jump = true;
            }
            else if (elevation == 0 && state.CarpetReady && frame.Move != 0)
                frame.Jump = true;
            frame.Status = elevation > 0 ? "下降中" : "空中移动";
            return frame.Move != 0 || frame.Jump || frame.Vertical != 0 || elevation > 0;
        }

        private static bool NeedsOpenColumn(Player body, int heading, float targetFeetY)
        {
            int bottom = (int)Math.Floor((body.Bottom.Y - .01f) / 16f);
            int top = Math.Max(bottom - 12, (int)Math.Floor((targetFeetY - body.height) / 16f));
            float sideways = heading * Math.Max(14f, Math.Abs(body.velocity.X) * 4f);
            return AirColumnClear(body, body.position.X, top, bottom) &&
                !AirColumnClear(body, body.position.X + sideways, top, bottom);
        }

        private static int RoofRow(Player body) => (int)Math.Floor((body.Top.Y - 5f) / 16f);

        private static bool LiftRoom(Player body, int gravity, out bool hitRoof)
        {
            const float probe = 4f;
            bool known = Predict(body, new Vector2(0, -gravity * probe), false, out var step);
            hitRoof = known && (step.Position.Y - body.position.Y) * -gravity < probe - .05f;
            return known && !hitRoof;
        }

        private bool ContinueRoofExit(Player body, CompanionMobilitySnapshot state, bool supported, int gravity,
            int preferred, float error, ref CompanionMobilityFrame frame)
        {
            if (roofEscape == 0 && !roofDropping) return false;
            if (error > 12f || roofEscapeAge >= 40 ||
                roofEscape != 0 && Math.Abs(body.position.X - roofEscapeStart.X) > 8f &&
                ClearVertical(body, -gravity) && AirColumnClear(body, body.position.X, roofEscapeRow, roofEscapeRow))
            {
                roofEscape = 0;
                roofDropping = false;
                return false;
            }
            // Never resume the same upward pressure after a clipped stride
            // and a single release frame. Follow the clear lateral lane or
            // give native gravity time to move the body clear of the lip.
            if (roofDropping && roofEscapeAge > 0 && roofEscapeAge % 4 == 0)
            {
                roofEscape = FindNearbyRoofSide(body, preferred, roofEscapeRow);
                roofDropping = roofEscape == 0;
            }
            roofEscapeAge++;
            frame.Move = roofEscape != 0 && ClearAhead(body, roofEscape, Math.Max(4f, Math.Abs(body.velocity.X)), supported)
                ? roofEscape : 0;
            if (state.MountHover) frame.Vertical = gravity;
            frame.Status = roofEscape == 0 ? "下降中" : "空中移动";
            return true;
        }

        private static int FindNearbyRoofSide(Player body, int preferred, int roof)
        {
            int first = preferred == 0 ? -1 : preferred;
            Vector2 forward = body.position, backward = body.position;
            bool firstClear = true, secondClear = true;
            var contact = CompanionNativeContact.PlatformContact.Native(body, false, body.stairFall);
            // A clear head tile beyond a side wall is not a reachable exit.
            // Check each small lateral sweep to the first complete opening.
            for (int offset = 8; offset <= 88 && (firstClear || secondClear); offset += 8)
            {
                firstClear = firstClear && TryRoofStride(body, first, contact, ref forward);
                secondClear = secondClear && TryRoofStride(body, -first, contact, ref backward);
                if (firstClear && AirColumnClear(body, forward.X, roof, roof)) return first;
                if (secondClear && AirColumnClear(body, backward.X, roof, roof)) return -first;
            }
            return 0;
        }

        private static bool TryRoofStride(Player body, int direction, CompanionNativeContact.PlatformContact contact, ref Vector2 at)
        {
            if (!CompanionNativeContact.TryMove(body, at, new Vector2(direction * 8f, 0), contact, false, out var step) ||
                step.Wall || CompanionFallRisk.OutsideModeledLiquid(body, step.Position, body.width, body.height)) return false;
            at = step.Position;
            return true;
        }

        private static bool AirColumnClear(Player body, float x, int top, int bottom)
        {
            int left = (int)Math.Floor((x + .01f) / 16f), right = (int)Math.Floor((x + body.width - .01f) / 16f);
            for (int y = bottom; y >= top; y--)
                for (int column = left; column <= right; column++)
                {
                    if (!CompanionNativeContact.Loaded(column, y)) return false;
                    Tile tile = Main.tile[column, y];
                    if (tile.nactive() && Main.tileSolid[tile.type] && !Main.tileSolidTop[tile.type]) return false;
                }
            return true;
        }

        private void Glide(Player body, CompanionMobilitySnapshot state, Vector2 feet, bool supported, ref CompanionMobilityFrame frame)
        {
            float gap = target.X - feet.X;
            bool launching = supported && !airborne;
            if (launching)
            {
                var bank = CompanionNativeContact.TopLeft(body, target);
                if (!CompanionNativeContact.Supported(body, bank) || !CompanionFallRisk.CanLand(body, feet, target, true))
                {
                    unsafeRelease = true;
                    return;
                }
                frame.Jump = Math.Abs(gap) > 14f && state.CarpetAvailable && Jump(body, state, true);
            }
            else if (!supported && Math.Abs(gap) > 10f)
                frame.Jump = state.CarpetReady || state.JumpTime > 0 && body.velocity.Y * body.gravDir < 0;
            if (!launching || frame.Jump)
            {
                int heading = CompanionMotionProfile.Steer(body, state, target.X, 0f, 2f, !supported);
                if (heading != 0 && ClearAhead(body, heading, Math.Max(3f, Math.Abs(body.velocity.X)), false)) frame.Move = heading;
            }
            // Near the bank let gravity finish the same action on the real
            // support. Releasing the key in mid-gap cannot count as arrival.
            frame.Status = "空中移动";
        }

        private void Mount(Player body, ref CompanionMobilityFrame frame)
        {
            var choice = Edge.MountChoice;
            if (body.mount.Active && body.mount.Type == choice.Type) return;
            if (!choice.SourceCurrent(body))
            {
                unsafeRelease = true;
                return;
            }
            if (body.mount.Active)
            {
                unsafeRelease = true;
                return;
            }
            SetTool(ref frame, CompanionMovementActivation.QuickMount, body.Center);
            frame.ItemSlot = choice.ItemSlot;
            frame.MountTypePlusOne = choice.Type + 1;
        }

        private void Door(Player body, CompanionMobilitySnapshot state, Vector2 feet, bool supported, ref CompanionMobilityFrame frame)
        {
            int horizontal = Math.Sign(Edge.To.X - Edge.From.X);
            if (!CompanionDoor.TryRead(Edge.EntryTile, out var door))
            {
                if (phase == Phase.Travel) frame.Move = horizontal;
                return;
            }
            bool hatch = door.Family == CompanionDoorFamily.Trapdoor;
            int passage = hatch ? Math.Sign(Edge.To.Y - Edge.From.Y) : horizontal;
            if (hatch)
            {
                // Its hinge is one tile; the useful body inlet is the
                // center of both tiles. Reach the switch through an actual
                // jump/flight in a tall shaft before issuing any tool input.
                float center = door.Aperture.Center.X;
                float alignment = Math.Max(1f, (door.Aperture.Width - body.width) * .5f - .5f);
                bool centered = Math.Abs(center - feet.X) <= alignment;
                if (passage > 0 && phase != Phase.Travel && !FallRelease.CanRelease(body, feet, Edge.To, supported))
                {
                    unsafeRelease = true;
                    return;
                }
                if (passage < 0 && (centered || door.Open))
                {
                    if (CompanionMotionProfile.CanFly(state))
                        Fly(body, state, feet, new Vector2(center, Edge.To.Y), Vector2.Zero, 0f, supported, ref frame);
                    else frame.Jump = Jump(body, state, supported);
                }
                frame.Move = Math.Abs(center - feet.X) > 2f ? Math.Sign(center - feet.X) : 0;
                if (!centered) return;
            }
            if (door.Open)
            {
                phase = Phase.Travel;
                if (!hatch && ClearAhead(body, horizontal, Math.Max(4f, Math.Abs(body.velocity.X)), supported))
                    frame.Move = horizontal;
                return;
            }
            if (!door.AtApproach(body, passage))
            {
                if (!hatch) frame.Move = horizontal;
                return;
            }
            SetTool(ref frame, CompanionMovementActivation.Door, door.Target);
            frame.DoorFamily = door.Family;
            frame.DoorPass = passage;
        }

        private void Facility(Player body, CompanionMobilitySnapshot state, ref CompanionMobilityFrame frame)
        {
            switch (Edge.Detail)
            {
                case NavigationTraversalDetail.Pylon:
                    SetTool(ref frame, CompanionMovementActivation.Pylon, TileCenter(Edge.ExitTile));
                    break;
                case NavigationTraversalDetail.Portal:
                    frame.Move = Math.Sign(Edge.InteractionPosition.X - body.Center.X);
                    SetTool(ref frame, CompanionMovementActivation.Portal, Edge.InteractionPosition);
                    break;
                case NavigationTraversalDetail.Teleporter:
                    SetTool(ref frame, CompanionMovementActivation.Teleporter, TileCenter(Edge.EntryTile));
                    break;
                case NavigationTraversalDetail.Rod:
                    SetTool(ref frame, CompanionMovementActivation.Rod, Edge.To);
                    frame.ItemSlot = state.RodSlot;
                    break;
            }
        }

        private CompanionMovementActivation tool;
        private int ToolSequence(CompanionMovementActivation activation) => unchecked((int)(Id * 16 + (int)activation));

        private void SetTool(ref CompanionMobilityFrame frame, CompanionMovementActivation activation, Vector2 point)
        {
            if (tool != activation || sequence == 0)
            {
                // A sub-operation (e.g. release after hook pull) must not
                // consume the previous native receipt of the same action.
                tool = activation;
                sequence = ToolSequence(activation);
            }
            frame.Sequence = sequence;
            frame.Activation = activation;
            frame.WorldX = point.X;
            frame.WorldY = point.Y;
        }

        internal CompanionActionOutcome Observe(Player body, CompanionMobilityState native, bool supported, Vector2 latestTarget, bool atGoal)
        {
            if (!hasDelivery || delivered.Generation != native.Generation) return Outcome;
            hasDelivery = false;
            Deferral = delivered.Deferral;
            if (unsafeRelease) return Outcome = CompanionActionOutcome.Superseded;
            elapsed++;
            var feet = CompanionNativeContact.Feet(body);
            var state = native.Observed;
            if (DashPlanRejected && !state.DashActive &&
                (Edge.Kind == NavigationAction.Dash || Edge.Detail == NavigationTraversalDetail.FlightDash))
                return Outcome = CompanionActionOutcome.Superseded;
            Braking = delivered.Move != 0 && delivered.Velocity.X * delivered.Move < -.04f &&
                (body.velocity.X - delivered.Velocity.X) * delivered.Move > .015f;
            if (delivered.Dash) dashIssued |= state.DashActive;
            if (Edge.Detail == NavigationTraversalDetail.FlightDash && !state.DashActive && (dashIssued || delivered.Dash))
            {
                if (!CompanionMotionProfile.CanFly(state)) return Outcome = CompanionActionOutcome.Complete;
                RetireAirDash();
            }
            if (Edge.Kind == NavigationAction.Mount && Edge.MountChoice.Valid && body.mount.Active &&
                body.mount.Type == Edge.MountChoice.Type) return Outcome = CompanionActionOutcome.Complete;
            InNativeMotion |= delivered.ToolAdmitted || delivered.Jump && !supported ||
                (Edge.Kind == NavigationAction.Drop || Edge.Kind == NavigationAction.PlatformDrop) &&
                (feet.Y - Edge.From.Y) * body.gravDir > 6f;
            if (Edge.Kind == NavigationAction.Hoik && (body.position.X - delivered.Position.X) * delivered.Move >
                Math.Max(7f, body.accRunSpeed + 2f))
            {
                // Real collision has moved the body inside the teeth. Its
                // already-checked direction owns the exit through danger.
                InNativeMotion = Committed = true;
            }
            if (delivered.Sequence != 0 && delivered.Activation != CompanionMovementActivation.None &&
                delivered.Sequence == native.Sequence && delivered.Activation == native.Action)
            {
                if (native.Result == CompanionMobilityResult.Rejected) return Outcome = CompanionActionOutcome.Rejected;
                if (native.Result == CompanionMobilityResult.Completed)
                {
                    if (Edge.Kind == NavigationAction.Facility || Edge.Kind == NavigationAction.Gravity || Edge.Kind == NavigationAction.Dismount ||
                        Edge.Kind == NavigationAction.Mount && native.Action == CompanionMovementActivation.QuickMount ||
                        hookReleasing && Edge.Kind == NavigationAction.Hook)
                        return Outcome = CompanionActionOutcome.Complete;
                    if (hookReleasing) { hookReleasing = false; waiting = 0; }
                }
            }

            float horizontal = Math.Sign(Edge.To.X - Edge.From.X);
            float vertical = (feet.Y - Edge.From.Y) * body.gravDir;
            startedMovement |= !supported || delivered.Move != 0 && Math.Abs(body.position.X - delivered.Position.X) > .08f;
            airborne |= !supported;
            // Commitment follows actual native state. Normal steering can be
            // replaced while a body is moving. A released fall or launch
            // needs continuous control until its next actual contact.
            bool fallReleased = IsDrop && FallReleased(body, feet, supported);
            if (fallReleased) phase = Phase.Travel;
            bool launched = Edge.Kind == NavigationAction.Jump || Edge.Kind == NavigationAction.Door ||
                Edge.Kind == NavigationAction.Flight && (FlightLanding || Edge.Detail == NavigationTraversalDetail.CarpetGlide);
            if (Edge.Kind != NavigationAction.Hoik)
                Committed = state.DashActive || !supported && (fallReleased ||
                    launched && (airborne || InNativeMotion)) ||
                    AwaitingNativeTool(native);

            if (Direct)
            {
                if (Edge.Kind == NavigationAction.Dash && dashIssued && !state.DashActive && supported)
                    return Outcome = CompanionActionOutcome.Complete;
                if (atGoal && !Committed)
                    return Outcome = CompanionActionOutcome.Complete;
                // Completion of the horizontal prefix leaves the owner's Y
                // outstanding. A settled body can now wait for its inlet
                // without recording a blocked walk or ending the follow.
                if (Edge.Kind == NavigationAction.Walk && supported && delivered.Move == 0 &&
                    Math.Abs(body.velocity.X) < .4f && Math.Abs(feet.X - target.X) <= walkBand + .2f)
                    return Outcome = CompanionActionOutcome.Complete;
            }
            else if (phase != Phase.Approach && Reached(body, state, supported, feet, horizontal, vertical))
                return Outcome = CompanionActionOutcome.Complete;

            if (delivered.InputsDeferred || delivered.Deferral != CompanionMovementDeferral.None || state.Controlled ||
                delivered.ToolAdmitted && native.Result == CompanionMobilityResult.Started)
                return Outcome = ++waiting < 132 ? CompanionActionOutcome.Waiting : CompanionActionOutcome.Suspended;

            if (Outcome == CompanionActionOutcome.Waiting) waiting = 0;
            if (Edge.Kind == NavigationAction.Jump && (feet.Y - Edge.To.Y) * body.gravDir <= 0f)
                crossedJumpHeight = true;
            if (roofEscape != 0 && (body.position.X - delivered.Position.X) * roofEscape > .08f)
            {
                noProgress = 0;
                return Outcome = CompanionActionOutcome.Running;
            }
            if (maintainingAirContact && delivered.Jump && delivered.Vertical > 0 && Math.Abs(body.velocity.Y) < .5f)
            {
                noProgress = 0;
                return Outcome = CompanionActionOutcome.Running;
            }
            float progress = Direct ? 0f : Measure(feet, horizontal, vertical);
            if (Direct)
            {
                // Compare both physical samples to the steering target used
                // for this native update, including an inlet on another side.
                // Owner motion alone cannot make a stalled body progress.
                var oldFeet = delivered.Position + new Vector2(body.width * .5f, body.gravDir > 0 ? body.height : 0);
                float lateral = Math.Abs(oldFeet.X - target.X) - Math.Abs(feet.X - target.X);
                progress = Edge.Kind == NavigationAction.Walk || Edge.Kind == NavigationAction.Dash ? lateral :
                    Math.Max(lateral, Math.Abs(oldFeet.Y - target.Y) - Math.Abs(feet.Y - target.Y));
                if (progress > .08f) noProgress = 0;
                else if (!Braking || ledgeBrakeDirection == 0 && (target.X - oldFeet.X) * delivered.Move <= 0f)
                    noProgress++;
            }
            if (!Direct && Edge.Kind == NavigationAction.Walk && horizontal != 0f &&
                delivered.Velocity.X * horizontal < -.04f &&
                (body.velocity.X - delivered.Velocity.X) * horizontal > .015f)
            {
                // Original inertia may carry the body away before it can
                // reverse. A measured reduction of that opposing velocity
                // is progress for this already-proved approach.
                best = progress;
                noProgress = 0;
                return Outcome = CompanionActionOutcome.Running;
            }
            if (!Direct && progress > best + .45f)
            {
                best = progress;
                noProgress = 0;
            }
            else if (!Direct) noProgress++;
            int limit = Edge.Kind == NavigationAction.Hoik ? 75 : Edge.Kind == NavigationAction.Flight ? 25 : 24;
            if (phase == Phase.Approach) limit += 10;
            if (Edge.Kind == NavigationAction.Hook)
            {
                if (state.GrappleAttached && !hookReleasing && !delivered.InputsDeferred &&
                    (latestTarget.Y - feet.Y) * body.gravDir <= 18f && Math.Abs(latestTarget.X - feet.X) <= 36f)
                {
                    // The original hook holds an airborne owner-side pose.
                    // Do not confuse an attached hold with failed approach.
                    noProgress = 0;
                    return Outcome = CompanionActionOutcome.Running;
                }
                limit += 24;
            }
            bool settlingJump = Edge.Kind == NavigationAction.Jump && Committed && crossedJumpHeight && elapsed < 240;
            // Native wedge contact can keep vx at zero while advancing in
            // small real strides between teeth. Keep its already-committed
            // controls while progress is observed, including past the
            // generic action lifetime. A stationary wedge still times out.
            bool expired = elapsed >= 480 && !Direct && (Edge.Kind != NavigationAction.Hoik || !Committed);
            if (noProgress >= limit && !settlingJump || expired) return Outcome = CompanionActionOutcome.Blocked;
            return Outcome = CompanionActionOutcome.Running;
        }

        private bool Reached(Player body, CompanionMobilitySnapshot state, bool supported, Vector2 feet,
            float horizontal, float vertical)
        {
            bool near = Near(feet, Edge.To, 9f, 12f);
            switch (Edge.Kind)
            {
                case NavigationAction.Walk:
                    // The graph names a coarse sample. A slope can carry the
                    // leading foot higher than that sample; compare progress
                    // along the actual support before retiring the connector.
                    return supported && Math.Abs(feet.Y - Edge.To.Y) <= 17f &&
                        (horizontal == 0f || (Edge.To.X - feet.X) * horizontal <= 4f) &&
                        (startedMovement || Math.Abs(Edge.To.X - Edge.From.X) < .5f);
                case NavigationAction.Drop:
                    // An airborne connector can start immediately above its
                    // target shelf. Its real landing may travel under 6px.
                    return supported && Math.Abs(body.velocity.Y) <= .2f &&
                        (!groundedInlet || vertical > 6f) && Near(feet, Edge.To, 24f, 12f);
                case NavigationAction.PlatformDrop:
                    return vertical > 6f && supported && Math.Abs(body.velocity.Y) <= .2f &&
                        Near(feet, Edge.To, 27f, 13f);
                case NavigationAction.Jump:
                    return startedMovement && near && (supported || state.Pulley || state.Wet);
                case NavigationAction.Flight:
                    // An upward air endpoint can feed a shelf or another
                    // input stage. Handing off below its actual clearance
                    // plane leaves a real next contact out of reach.
                    if (!FlightLanding && (Edge.From.Y - Edge.To.Y) * body.gravDir > 8f &&
                        (feet.Y - Edge.To.Y) * body.gravDir > FlightClimbTolerance) return false;
                    return near && (!FlightLanding && Edge.Detail != NavigationTraversalDetail.CarpetGlide || supported);
                case NavigationAction.Door:
                    if (phase != Phase.Travel || !near) return false;
                    if (!CompanionDoor.TryRead(Edge.EntryTile, out var door)) return false;
                    if (door.Family == CompanionDoorFamily.Trapdoor)
                        return Edge.To.Y > Edge.From.Y ? supported && body.Top.Y > door.Aperture.Bottom + 4f :
                            body.Bottom.Y < door.Aperture.Top - 4f;
                    return horizontal > 0 ? body.Left.X >= door.Aperture.Right : body.Right.X <= door.Aperture.Left;
                case NavigationAction.Track:
                    if (!near || !state.Track || !state.MountCart) return false;
                    if (Edge.Detail != NavigationTraversalDetail.TrackRide) return true;
                    Vector2 magnet = feet + CompanionMotionProfile.RailMagnetFromFeet(body, body.height);
                    return NavigationRail.Matches(Edge.ExitTile, Edge.RailFrame, magnet);
                case NavigationAction.Hook:
                case NavigationAction.Mount:
                case NavigationAction.Dismount:
                case NavigationAction.Gravity:
                case NavigationAction.Facility:
                    return false;
                case NavigationAction.Rope:
                    return near && (state.Pulley || supported);
                case NavigationAction.Hoik:
                    return startedMovement && (near || (feet.X - Edge.To.X) * horizontal >= 0 && Math.Abs(feet.Y - Edge.To.Y) < 8f);
                default:
                    return near;
            }
        }

        private float Measure(Vector2 feet, float horizontal, float vertical)
        {
            if (phase == Phase.Approach) return -Vector2.Distance(feet, entry);
            if (Edge.Kind == NavigationAction.Drop || Edge.Kind == NavigationAction.PlatformDrop)
                return vertical + (feet.X - Edge.From.X) * horizontal * .3f;
            if (!Direct && Edge.Kind == NavigationAction.Walk)
                return (feet.X - Edge.From.X) * horizontal;
            return Vector2.Distance(Edge.From, Edge.To) - Vector2.Distance(feet, target);
        }

        internal static bool Near(Vector2 left, Vector2 right, float x, float y) =>
            Math.Abs(left.X - right.X) <= x && Math.Abs(left.Y - right.Y) <= y;

        internal static Vector2 TileCenter(Point tile) => new Vector2(tile.X * 16f + 8f, tile.Y * 16f + 8f);

        private int GroundInput(Player body, CompanionMobilitySnapshot state, float x, float vx, float spacing, bool supported,
            bool coastAtEdge = false)
        {
            int move = CompanionMotionProfile.Steer(body, state, x, vx, spacing, !supported);
            int momentum = Math.Sign(body.velocity.X);
            float speed = Math.Abs(body.velocity.X);
            bool stepOff = false;
            bool canCoast = coastAtEdge && body.velocity.Y == 0f && !state.DashActive && !state.MountCart &&
                !state.Wet && !state.Pulley && !state.GrappleAttached && !state.GrappleInFlight;
            bool pushing = move == momentum && !state.DashActive;
            bool continuingStop = supported && canCoast && speed > 0f && ledgeBrakeDirection == momentum && move != -momentum;
            if (!continuingStop) ledgeBrakeDirection = 0;
            var room = supported && speed > .5f ? CompanionGroundRun.BrakingRoom(body, momentum,
                pushing ? speed + Math.Max(0, body.runAcceleration) : speed,
                state.DashActive ? state.DashKind : 0, includeForwardStride: canCoast && pushing) : CompanionBrakingRoom.Clear;
            if (continuingStop || room == CompanionBrakingRoom.Wall || room == CompanionBrakingRoom.Ledge)
            {
                stepOff = !continuingStop && pushing && CompanionGroundRun.HasShallowStepOff(body, move);
                if (!stepOff)
                {
                    if (canCoast && (room == CompanionBrakingRoom.Ledge || continuingStop) && move != -momentum)
                    {
                        // Finish this physical stop before driving forward
                        // again. One braking step can make the next stride
                        // appear safe without removing the edge ahead.
                        ledgeBrakeDirection = momentum;
                        if (CompanionGroundRun.CanCoastToStop(body, momentum, speed)) return 0;
                    }
                    return GroundBrakeInput(body, momentum, speed, supported);
                }
            }
            // Unproved shapes still admit a short observed stride. Release
            // throttle when that stride would add speed beyond our known
            // stopping prefix, without inventing a direction reversal.
            if (room == CompanionBrakingRoom.Unproved && pushing && speed > 3f) return 0;
            // The original horizontal update can still advance toward the
            // obstacle while the opposite key reduces its forward momentum.
            if (move != 0 && move == -momentum) return GroundBrakeInput(body, momentum, speed, supported);
            if (move != 0 && !ClearAhead(body, move, Math.Max(4f, Math.Abs(body.velocity.X) + 1f), supported && !stepOff,
                allowSteps: supported)) return 0;
            return move;
        }

        private static int GroundBrakeInput(Player body, int momentum, float speed, bool supported)
        {
            float delta = speed - body.runAcceleration - (speed > body.runSlowdown ? body.runSlowdown : 0f);
            if (delta >= 0f) return -momentum; // This key still reduces the approaching sweep.
            return ClearAhead(body, -momentum, Math.Max(1f, -delta), supported, allowSteps: supported) ? -momentum : 0;
        }

        internal static bool ClearAhead(Player body, int direction, float pixels, bool keepSupport, bool allowSteps = false) =>
            ClearAhead(body, direction, pixels, keepSupport, out _, allowSteps);

        internal static bool ClearAhead(Player body, int direction, float pixels, bool keepSupport, out bool unsupported,
            bool allowSteps = false)
        {
            unsupported = false;
            var move = new Vector2(direction * pixels, Math.Max(.05f, body.gravity) * body.gravDir);
            if (!Predict(body, move, keepSupport || allowSteps, out var contact) || contact.Wall) return false;
            unsupported = keepSupport && !contact.Ground && !CompanionNativeContact.Supported(body, contact.Position);
            return !unsupported;
        }

        internal static bool ClearVertical(Player body, int direction) =>
            Predict(body, new Vector2(0, direction * Math.Max(4f, Math.Abs(body.velocity.Y))), false, out var step) &&
            (step.Position.Y - body.position.Y) * direction >= 1f;

        private static bool Predict(Player body, Vector2 move, bool steps, out CompanionNativeContact.Step contact)
        {
            if (!CompanionNativeContact.TryMove(body, body.position, move,
                CompanionNativeContact.PlatformContact.Native(body, false, body.stairFall), steps, out contact)) return false;
            return (body.lavaImmune || body.lavaTime > 24 || body.lavaWet ||
                !CompanionMobilityGeometry.TouchesLava(contact.Position, body.width, body.height)) &&
                !CompanionMobilityGeometry.TouchesShimmer(contact.Position, body.width, body.height);
        }
    }
}
