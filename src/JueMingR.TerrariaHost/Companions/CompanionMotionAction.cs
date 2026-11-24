using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal enum CompanionActionOutcome { Idle, Running, Waiting, Complete, Blocked, Rejected, Suspended }

    // One action spans staging, the original Player.Update and actual observation.
    // Deferred hands do not count against contact progress, but can time out.
    internal sealed class CompanionMotionAction
    {
        internal NavigationTransition Edge;
        internal CompanionFallRisk.ReleaseCorridor FallRelease;
        internal float FallOutletX;
        internal const float FlightBankClearance = 4f;
        internal bool FlightLanding;
        internal long Id;
        internal bool Direct, Committed;
        internal CompanionActionOutcome Outcome;
        private enum Phase { Approach, Release, Travel }
        private Phase phase;
        private float best;
        private int noProgress, waiting, elapsed, sequence, roofEscape, roofEscapeAge, roofEscapeRow;
        private Vector2 target, entry, roofEscapeStart;
        private CompanionMovementDelivery delivered;
        private bool hasDelivery, startedMovement, airborne, hookReleasing, dashIssued, unsafeRelease;
        private bool observedInlet, groundedInlet, maintainingAirContact, clearedFlightBank;
        internal CompanionMovementDeferral Deferral { get; private set; }
        internal bool LosingProgress => noProgress >= 4;
        internal bool InNativeMotion { get; private set; }
        internal bool HasRoofContact { get; private set; }
        internal Vector2 RoofContact { get; private set; }

        internal void Begin(long id, NavigationTransition edge, bool direct, Vector2 launch)
        {
            Edge = edge;
            Id = id;
            FallRelease = default;
            FallOutletX = edge.To.X;
            Direct = direct;
            Outcome = CompanionActionOutcome.Running;
            phase = direct ? Phase.Travel : Phase.Approach;
            Committed = false;
            best = float.NegativeInfinity;
            noProgress = waiting = elapsed = sequence = 0;
            roofEscape = roofEscapeAge = 0;
            hasDelivery = startedMovement = airborne = hookReleasing = dashIssued = unsafeRelease = false;
            observedInlet = groundedInlet = maintainingAirContact = false;
            clearedFlightBank = FlightLanding = false;
            InNativeMotion = false;
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

        internal CompanionMobilityFrame Input(Player body, CompanionMobilityState native, bool supported,
            Vector2 liveTarget, Vector2 liveVelocity, float spacing, int threatDirection, bool emergency,
            bool keepAirContact, bool ownerInAir, bool rollingWalk)
        {
            var state = native.Observed;
            Vector2 feet = CompanionNativeContact.Feet(body);
            maintainingAirContact = Direct && Edge.Kind == NavigationAction.Flight && keepAirContact &&
                Near(feet, liveTarget, spacing + 4f, 11f);
            if (!observedInlet)
            {
                observedInlet = true;
                groundedInlet = supported;
            }
            target = Direct ? liveTarget : Edge.To;
            var frame = new CompanionMobilityFrame { ActionId = Id, ItemSlot = -1, Status = "跟随中" };
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
                frame.Move = GroundInput(body, state, entry.X, arrivalSpeed, 0f, supported);
                return frame;
            }

            if (native.AwaitingEquipmentObservation && phase != Phase.Travel &&
                (Edge.Kind == NavigationAction.Drop || Edge.Kind == NavigationAction.PlatformDrop)) return frame;

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
                        frame.Move = GroundInput(body, state, target.X, arrivalSpeed, spacing, supported);
                    }
                    if (state.Pulley && frame.Move != 0) frame.Jump = state.ReleaseJump;
                    break;
                case NavigationAction.Dash:
                    if (state.DashActive)
                    {
                        int heading = Math.Sign(body.velocity.X);
                        float speed = Math.Abs(body.velocity.X);
                        bool canHold = heading != 0 && (target.X - feet.X) * heading > spacing + speed &&
                            CompanionGroundRun.HasBrakingRoom(body, heading, speed, state.DashKind);
                        frame.Move = canHold ? heading : 0;
                        frame.Status = canHold ? "冲刺中" : "减速中";
                        break;
                    }
                    frame.Move = GroundInput(body, state, target.X, 0f, 2f, supported);
                    if (!dashIssued && supported && state.DashReady && frame.Move != 0 &&
                        CompanionGroundRun.CanDash(body, frame.Move, state.DashType, target.X))
                        frame.Dash = frame.Move;
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
                    else Fly(body, state, feet, target, velocity, spacing, supported, ref frame,
                        levelHold: Direct && keepAirContact && Math.Abs(liveVelocity.Y) < .4f,
                        preferDescent: Direct ? !ownerInAir : (Edge.To.Y - Edge.From.Y) * body.gravDir > 6f);
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
                    Mount(body, state, ref frame);
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
            return frame;
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
                return Near(feet, entry, entry.X == Edge.From.X ? 7f : 2f, 16f) ||
                    body.gravDir * (feet.Y - entry.Y) > 4f && Math.Abs(feet.X - entry.X) < 16f;
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

        private void Drop(Player body, CompanionMobilitySnapshot state, Vector2 feet, bool supported, ref CompanionMobilityFrame frame)
        {
            int gravity = body.gravDir < 0 ? -1 : 1;
            bool platform = Edge.Kind == NavigationAction.PlatformDrop;
            if (gravity * (feet.Y - Edge.From.Y) >= 6f) phase = Phase.Travel;
            if (phase != Phase.Travel && (!FallRelease.IsCurrent(body) || !CompanionFallRisk.CanLand(body, feet, target, supported)))
            {
                // Damage and accessories can change while aligning. Never
                // release the source support with stale survivability.
                unsafeRelease = true;
                return;
            }
            if (platform && phase != Phase.Travel)
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
            }
            if (state.MountHover) frame.Vertical = gravity;
            frame.Status = "下降中";
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
            int elevation = projected < -tolerance ? -1 : projected > tolerance ? 1 : 0;
            // Successive downhill flight samples can be closer than the
            // hover band. Let native gravity reach them instead of spending
            // reserve holding a pose above the intended continuation.
            if (preferDescent && error > 4f && elevation == 0) elevation = 1;
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
            if (roofEscape != 0 && (error > 12f || roofEscapeAge >= 40 ||
                Math.Abs(body.position.X - roofEscapeStart.X) > 8f &&
                ClearVertical(body, -gravity) && AirColumnClear(body, body.position.X, roofEscapeRow, roofEscapeRow)))
                roofEscape = 0;
            if (roofEscape != 0)
            {
                roofEscapeAge++;
                frame.Move = ClearAhead(body, roofEscape, Math.Max(4f, Math.Abs(body.velocity.X)), supported) ? roofEscape : 0;
                frame.Status = "空中移动";
                return frame.Move != 0;
            }
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
            if (elevation < 0 && !ClearVertical(body, -gravity))
            {
                // Coast beneath a roof. A blocked lift must not be converted
                // to hover, which would keep the body against the same roof.
                if (Direct && frame.Move == 0)
                {
                    HasRoofContact = true;
                    RoofContact = body.position;
                    if (gravity > 0 && !supported)
                    {
                        roofEscape = FindNearbyRoofSide(body, toward);
                        roofEscapeAge = 0;
                        roofEscapeRow = RoofRow(body);
                        roofEscapeStart = body.position;
                        frame.Move = roofEscape;
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

        private static int FindNearbyRoofSide(Player body, int preferred)
        {
            int first = preferred == 0 ? -1 : preferred;
            bool firstClear = ClearAhead(body, first, 5f, false);
            bool secondClear = ClearAhead(body, -first, 5f, false);
            int roof = RoofRow(body);
            for (int offset = 8; offset <= 56 && (firstClear || secondClear); offset += 8)
            {
                if (firstClear && AirColumnClear(body, body.position.X + first * offset, roof, roof)) return first;
                if (secondClear && AirColumnClear(body, body.position.X - first * offset, roof, roof)) return -first;
            }
            return 0;
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

        private void Mount(Player body, CompanionMobilitySnapshot state, ref CompanionMobilityFrame frame)
        {
            int slot = state.DefaultMountSlot, type = state.DefaultMountType;
            switch (Edge.Detail)
            {
                case NavigationTraversalDetail.MountFlight: slot = state.FlightMountSlot; type = state.FlightMountType; break;
                case NavigationTraversalDetail.MountWater: slot = state.WaterMountSlot; type = state.WaterMountType; break;
                case NavigationTraversalDetail.MountNarrow: slot = state.NarrowMountSlot; type = state.NarrowMountType; break;
            }
            if (type < 0) return;
            if (body.mount.Active && body.mount.Type != type)
            {
                SetTool(ref frame, CompanionMovementActivation.Dismount, body.Center);
                return;
            }
            SetTool(ref frame, CompanionMovementActivation.QuickMount, body.Center);
            frame.ItemSlot = slot;
            frame.MountTypePlusOne = type + 1;
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
                if (passage > 0 && phase != Phase.Travel && !door.Open &&
                    (!FallRelease.IsCurrent(body) || !CompanionFallRisk.CanLand(body, feet, Edge.To, supported)))
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
        private void SetTool(ref CompanionMobilityFrame frame, CompanionMovementActivation activation, Vector2 point)
        {
            if (tool != activation || sequence == 0)
            {
                // A sub-operation (e.g. release after hook pull) must not
                // consume the previous native receipt of the same action.
                tool = activation;
                sequence = unchecked((int)(Id * 16 + (int)activation));
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
            if (unsafeRelease) return Outcome = CompanionActionOutcome.Rejected;
            elapsed++;
            var feet = CompanionNativeContact.Feet(body);
            var state = native.Observed;
            if (delivered.Dash) dashIssued = true;
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
            if (Edge.Irreversible && (Edge.Kind != NavigationAction.Drop && Edge.Kind != NavigationAction.PlatformDrop || vertical > 6f) &&
                (startedMovement || delivered.ToolAdmitted)) Committed = true;

            if (Direct)
            {
                target = latestTarget;
                if (atGoal)
                    return Outcome = CompanionActionOutcome.Complete;
            }
            else if (phase != Phase.Approach && Reached(body, state, supported, feet, horizontal, vertical))
                return Outcome = CompanionActionOutcome.Complete;

            if (delivered.InputsDeferred || delivered.Deferral != CompanionMovementDeferral.None || state.Controlled ||
                delivered.ToolAdmitted && native.Result == CompanionMobilityResult.Started)
                return Outcome = ++waiting < 132 ? CompanionActionOutcome.Waiting : CompanionActionOutcome.Suspended;

            if (Outcome == CompanionActionOutcome.Waiting) waiting = 0;
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
            float progress = Measure(body, feet, horizontal, vertical);
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
            if (progress > best + .45f)
            {
                best = progress;
                noProgress = 0;
            }
            else noProgress++;
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
            if (noProgress >= limit || elapsed >= 480 && !Direct) return Outcome = CompanionActionOutcome.Blocked;
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
                    return supported && (!groundedInlet || vertical > 6f) && Near(feet, Edge.To, 24f, 12f);
                case NavigationAction.PlatformDrop:
                    return vertical > 6f && supported && Near(feet, Edge.To, 27f, 13f);
                case NavigationAction.Jump:
                    return startedMovement && near && (supported || state.Pulley || state.Wet);
                case NavigationAction.Flight:
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

        private float Measure(Player body, Vector2 feet, float horizontal, float vertical)
        {
            if (phase == Phase.Approach) return -Vector2.Distance(feet, entry);
            if (Edge.Kind == NavigationAction.Drop || Edge.Kind == NavigationAction.PlatformDrop)
                return vertical + (feet.X - Edge.From.X) * horizontal * .3f;
            if (!Direct && Edge.Kind == NavigationAction.Walk)
                return (feet.X - Edge.From.X) * horizontal;
            if (Direct)
            {
                // The forward projection uses the committed start/heading.
                // Repeated owner movement does not renew this progress clock.
                var axis = Edge.To - Edge.From;
                return axis.LengthSquared() > 1 ? Vector2.Dot(Vector2.Normalize(axis), feet - Edge.From) : 0f;
            }
            return Vector2.Distance(Edge.From, Edge.To) - Vector2.Distance(feet, target);
        }

        internal static bool Near(Vector2 left, Vector2 right, float x, float y) =>
            Math.Abs(left.X - right.X) <= x && Math.Abs(left.Y - right.Y) <= y;

        internal static Vector2 TileCenter(Point tile) => new Vector2(tile.X * 16f + 8f, tile.Y * 16f + 8f);

        internal static int GroundInput(Player body, CompanionMobilitySnapshot state, float x, float vx, float spacing, bool supported)
        {
            int move = CompanionMotionProfile.Steer(body, state, x, vx, spacing, !supported);
            int momentum = Math.Sign(body.velocity.X);
            float speed = Math.Abs(body.velocity.X);
            bool stepOff = false;
            if (supported && speed > .5f && !CompanionGroundRun.HasBrakingRoom(body, momentum,
                move == momentum && !state.DashActive ? speed + Math.Max(0, body.runAcceleration) : speed,
                state.DashActive ? state.DashKind : 0))
            {
                stepOff = move == momentum && !state.DashActive && CompanionGroundRun.HasShallowStepOff(body, move);
                if (!stepOff) return ClearAhead(body, -momentum, 3f, true) ? -momentum : 0;
            }
            if (move != 0 && !ClearAhead(body, move, Math.Max(4f, Math.Abs(body.velocity.X) + 1f), supported && !stepOff)) return 0;
            return move;
        }

        internal static bool ClearAhead(Player body, int direction, float pixels, bool keepSupport)
        {
            var move = new Vector2(direction * pixels, Math.Max(.05f, body.gravity) * body.gravDir);
            return Predict(body, move, keepSupport, out var contact) && !contact.Wall &&
                (!keepSupport || contact.Ground || CompanionNativeContact.Supported(body, contact.Position));
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
