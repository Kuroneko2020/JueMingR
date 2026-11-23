using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class CompanionNavigation
    {
        private bool advancing, followingOutlet;
        private uint postureChangedAt, fallHookAt, jumpProbeAt;
        private Vector2 jumpProbePosition;
        private int localPlanningWork;

        internal static bool CanObserveWetSupport(Player body, CompanionMobilitySnapshot mobility)
        {
            float vertical = body.velocity.Y * body.gravDir;
            return !mobility.GrappleAttached && !mobility.Pulley && !mobility.Track && !mobility.Shimmer && vertical >= 0 && vertical <= 1;
        }

        internal static bool OwnerHasSupport(Player body)
        {
            float vertical = body.velocity.Y * body.gravDir;
            if (body.pulley || body.stairFall || vertical < 0 || vertical > 1) return false;
            return ObservedSupport(body);
        }

        internal CompanionMobilityFrame Escape(Player body, CompanionMobilityState state, Vector2 target, bool grounded,
            bool needsAir, bool lava, int threatEscape, bool canJump)
        {
            var proposed = EscapeInput(body, state.Observed, target, grounded, needsAir, lava, threatEscape, canJump);
            if (!needsAir && !lava && !body.dead && !body.CCed && !state.Observed.Controlled && !state.Observed.Shimmer &&
                proposed.Move == 0 && !proposed.Jump && proposed.Vertical == 0 &&
                actions.HasStep && actions.Current.Kind == RouteKind.Hoik)
            {
                var step = actions.Current;
                var mobility = state.Observed;
                // Inside a live tooth, the ordinary escape sweep can reject
                // both directions. Use the proved native way out until an
                // actual escape input can take over or the passage closes.
                if (HoikEngaged(body, mobility, step))
                {
                    actions.TrimSuffix();
                    followingOutlet = false;
                    var motion = CurrentMotion(body, mobility, grounded);
                    if (actions.Step(body, state, mobility, motion, step, out var exit, out bool reached)) return exit;
                    CompleteAction(body, step, reached);
                }
            }
            InterruptAction(body);
            return proposed;
        }

        private static CompanionMobilityFrame EscapeInput(Player body, CompanionMobilitySnapshot mobility, Vector2 target,
            bool grounded, bool needsAir, bool lava, int threatEscape, bool canJump)
        {
            if (body.dead || body.CCed || mobility.Controlled || mobility.Shimmer) return new CompanionMobilityFrame { Status = "暂时受控" };
            var motion = CurrentMotion(body, mobility, grounded);
            int direction = needsAir || lava ? Math.Sign(target.X - body.Center.X) : threatEscape;
            string status = needsAir ? "寻找可呼吸位置" : "避险中";
            using (Scratch.Save())
            {
                if (motion == RouteMotion.Ground && !lava)
                {
                    if (body.dashDelay < 0) return new CompanionMobilityFrame { Status = status };
                    int move = direction;
                    if (move == 0 || !QuickWalk(body, move, out _))
                        move = direction != 0 && QuickWalk(body, -direction, out _) ? -direction : 0;
                    return new CompanionMobilityFrame { Move = move, Jump = (canJump || needsAir || lava) &&
                        (mobility.ReleaseJump || body.autoJump) && NextClear(body, 0, -(int)body.gravDir), Status = status };
                }
                if (motion == RouteMotion.Rope || motion == RouteMotion.Track)
                {
                    var attached = LocalRecovery(body, mobility, motion, target);
                    attached.Status = status;
                    return attached;
                }
                if (motion == RouteMotion.Liquid || lava)
                {
                    bool up = CanLeaveLiquid(body, 0, -(int)body.gravDir, lava);
                    int move = direction != 0 ? direction : body.direction;
                    if (!CanLeaveLiquid(body, move, 0, lava)) move = CanLeaveLiquid(body, -move, 0, lava) ? -move : 0;
                    return new CompanionMobilityFrame { Move = move, Jump = up && (mobility.CanSwim || grounded) && (mobility.ReleaseJump || body.merman),
                        Vertical = up && mobility.MountSwim ? -(int)body.gravDir : 0, Status = status };
                }
                if (AirInput(body, mobility, motion, direction, -1, out var rise)) { rise.Status = status; return rise; }
                return new CompanionMobilityFrame { Move = NextClear(body, direction, 0) ? direction : 0, Status = status };
            }
        }

        private static bool CanLeaveLiquid(Player body, int direction, int vertical, bool currentLava)
        {
            if (!currentLava) return NextClear(body, direction, vertical);
            var delta = new Vector2(direction * 8, vertical * 8);
            if (!Footprint(body, body.position, delta)) return false;
            var actual = Collision.TileCollision(body.position, delta, body.width, body.height, false, false, (int)body.gravDir);
            // The body is already exposed. Requiring the immediate neighbor
            // to be dry would immobilize it inside a wider lava pool.
            return Vector2.DistanceSquared(delta, actual) < .25f && ClearBody(body, body.position + delta);
        }

        internal bool TryFallRescue(Player body, CompanionMobilityState state, out CompanionMobilityFrame frame)
        {
            frame = default(CompanionMobilityFrame);
            var mobility = state.Observed;
            if (body.dead || body.CCed || mobility.Controlled || mobility.Wet || mobility.GrappleAttached || mobility.GrappleInFlight ||
                mobility.Pulley || !ShouldRescueFall(body, mobility)) return false;
            using (Scratch.Save())
            {
                if (!state.AwaitingEquipmentObservation && AirInput(body, mobility, RouteMotion.Air, 0, -1, out frame) &&
                    (frame.Jump || frame.Vertical != 0))
                {
                    InterruptAction(body);
                    frame.Status = "缓解坠落";
                    return true;
                }

                if (mobility.HookRange <= 0 || fallHookAt != 0 && unchecked(Main.GameUpdateCount - fallHookAt) < 30) return false;
                var node = new RouteNode { Position = body.position, Motion = RouteMotion.Air, Previous = -1 };
                var step = new RouteStep { From = body.position, To = body.position, ItemSlot = -1, EmergencyHand = true };
                var above = body.position - new Vector2(0, body.gravDir * 112);
                fallHookAt = Main.GameUpdateCount;
                if (!TryHookEdge(body, mobility, node, above, 0, null, ref step) &&
                    !TryHookEdge(body, mobility, node, above, Math.Sign(body.velocity.X), null, ref step)) return false;
                InterruptAction(body);
                return TryLocalAction(body, state, mobility, RouteMotion.Air, step, above, out frame);
            }
        }

        private bool TryCombatJump(Player body, CompanionMobilityState state, Vector2 aimOpening, out CompanionMobilityFrame frame)
        {
            frame = default;
            if (actions.HasStep || !cached || !cachedResult || !hasLanding || !ReferenceEquals(plannedBody, body) ||
                !ReferenceEquals(plannedWorld, Main.ActiveWorldFileData) || targetHeight != aimOpening.Y ||
                launch != body.position || initialVelocity != body.velocity || !jumpReads.Current()) return false;
            // The firing point can be in mid-air. The accepted action retains
            // responsibility for landing without changing the long-range goal.
            var step = new RouteStep { Kind = RouteKind.Jump, From = launch, To = landing, Direction = plannedDirection, Duration = jumpHold,
                Launch = new JumpInputs(body, state.Observed) };
            return !RecentlyRejected(body, step) &&
                TryLocalAction(body, state, state.Observed, RouteMotion.Ground, step, aimOpening - body.Size * .5f, out frame);
        }

        private bool TryLocalAction(Player body, CompanionMobilityState state, CompanionMobilitySnapshot observed,
            RouteMotion motion, RouteStep step, Vector2 goal, out CompanionMobilityFrame frame)
        {
            actions.Adopt(step, goal);
            if (actions.Step(body, state, observed, motion, step, out frame, out bool reached)) return true;
            ReportActionOutcome(actions.Finish(body, step, reached));
            return false;
        }

        private void InterruptAction(Player body)
        {
            if (!actions.HasStep) return;
            ReportActionOutcome(actions.Interrupt(body));
            followingOutlet = false;
        }

        private static RouteMotion CurrentMotion(Player body, CompanionMobilitySnapshot mobility, bool grounded)
        {
            if (mobility.GrappleAttached || mobility.GrappleInFlight) return RouteMotion.Hooked;
            if (mobility.Pulley) return RouteMotion.Rope;
            if (mobility.Track) return RouteMotion.Track;
            if (mobility.Wet && !grounded) return RouteMotion.Liquid;
            return grounded ? RouteMotion.Ground : RouteMotion.Air;
        }

        private CompanionMobilityFrame FollowRoute(Player body, CompanionMobilityState state, Vector2 target, Vector2 targetVelocity, bool allowPlanning,
            bool grounded, int stopDistance, int resumeDistance, CompanionOwnerTrail trail = null)
        {
            localPlanningWork = 0;
            var mobility = state.Observed;
            var offset = new Vector2(body.width / 2f, body.height / 2f);
            Vector2 goal = target - offset;
            var motion = CurrentMotion(body, mobility, grounded);
            Vector2 ownerGoal = goal;
            Vector2 ownerVelocity = targetVelocity;
            if (RetainTrailGoal(body, state, trail, motion, ownerGoal, ref goal)) targetVelocity = Vector2.Zero;
            if (allowPlanning && WakeChangedDescentObstruction(body, motion, goal) && actions.HasStep)
            {
                var action = actions.Current;
                if (action.Kind == RouteKind.Walk || action.Kind == RouteKind.LedgeDrop && !actions.Started && !body.stairFall)
                    InterruptAction(body);
            }

            int threshold = trail != null && goals.Joined ? 12 : advancing ? stopDistance : resumeDistance;
            if (motion == RouteMotion.Ground && mobility.DashActive && body.dashDelay < 0)
            {
                using (Scratch.Save())
                {
                    advancing = true;
                    return ContinueDash(body, mobility.DashKind, goal, threshold);
                }
            }
            if (allowPlanning && !state.AwaitingEquipmentObservation && motion == RouteMotion.Ground &&
                (!actions.HasStep || !followingOutlet && actions.Current.Kind == RouteKind.Walk &&
                    actions.Current.Direction * (goal.X - body.position.X) >= 0) &&
                WantsPlatformAscent(body, mobility, goal))
            {
                // An actual higher platform under this body's next vertical
                // jump may help an ordinary approach. A proved outlet or
                // reverse stride already has a different first purpose.
                // Spend the one selected planning grant on this native sweep.
                allowPlanning = false;
                if (TryImmediateJump(body, state, mobility, goal, 0, out var rise)) return rise;
            }
            if (actions.HasStep && motion == RouteMotion.Ground && !actions.Started && !body.stairFall &&
                (actions.Current.Kind == RouteKind.Drop || actions.Current.Kind == RouteKind.LedgeDrop) &&
                NearbySupportedGoal(body, goal))
            {
                // A live owner back on this clear level no longer needs
                // this descent. After native release, finish the safe motion.
                InterruptAction(body);
            }
            if (actions.HasStep && GoalRedirects(body.position, actions.Goal, goal))
            {
                // Each active segment still checks its next input. Aim changes
                // expire the remote suffix, including while walking or flying.
                actions.TrimSuffix();
                followingOutlet = false;
                actions.Retarget(goal);
            }

            if (motion == RouteMotion.Track && TrackShouldSlow(body, goal, stopDistance))
            {
                if (actions.HasStep && actions.Current.Kind == RouteKind.Track) InterruptAction(body);
                if (!actions.HasStep)
                {
                    if (allowPlanning && TryModePrefix(body, state, mobility, goal, motion, Math.Sign(goal.X - body.position.X),
                        out var exit, nearTrackGoal: true)) return exit;
                    // A reachable adjacent rail is still legal here, but it
                    // would start another approach inside the owner's band.
                    return LocalRecovery(body, mobility, motion, goal);
                }
            }

            for (int next = 0; next < 2 && actions.HasStep; next++)
            {
                RouteStep step = actions.Current;
                if (actions.Step(body, state, mobility, motion, step, out CompanionMobilityFrame frame, out bool finished, allowPlanning))
                {
                    if (allowPlanning && !coordinator.ProofGrantUsed && actions.Remaining <= 2 &&
                        (step.Retreat || PlannerHasWork))
                        SearchRoute(body, mobility, goal, stopDistance, motion, publish: false);
                    return frame;
                }
                if (coordinator.ProofGrantUsed) allowPlanning = false;
                CompleteAction(body, step, finished);
            }

            if (trail != null && goals.Joined && !actions.HasStep)
            {
                goal = ownerGoal;
                targetVelocity = RetainTrailGoal(body, state, trail, motion, ownerGoal, ref goal) ? Vector2.Zero : ownerVelocity;
            }
            int direction = Math.Sign(goal.X - body.position.X);
            bool movingWithOwner = motion == RouteMotion.Ground && Math.Abs(targetVelocity.X) >= 1f;
            int groundMove = movingWithOwner ? FollowHorizontal(body, goal.X, targetVelocity.X, body.accRunSpeed,
                body.runAcceleration, body.runSlowdown, stopDistance / 2) : direction;
            if (motion == RouteMotion.Ground && (trail == null || !goals.Joined) && (!movingWithOwner || groundMove == 0) &&
                Reached(body.position, goal, advancing ? stopDistance : resumeDistance) && InGoalRegion(body, body.position, goal, motion, null))
            {
                // Confirm real arrival before adopting a result that was
                // found while this body was still completing its last step.
                RetirePlannedCandidate();
                followingOutlet = false;
                // Every neutral braking update still observes the tighter
                // stop region. Resume hysteresis starts on actual stability,
                // so inertia cannot carry a newly arrived body back out and
                // leave it resting between the two ranges.
                advancing = Math.Abs(body.velocity.X) > .65f || Math.Abs(body.velocity.Y) > .65f;
                return new CompanionMobilityFrame { Status = "停留" };
            }
            if (allowPlanning && !actions.HasStep && PlannerShouldConnectEarly(motion))
            {
                allowPlanning = false;
                if (TryPlanRoute(body, state, mobility, motion, goal, stopDistance, out var handoff)) return handoff;
            }

            if (!actions.HasStep && TryFacilityStride(body, state, mobility, goal, motion, direction, out var stride))
                return stride;

            if (allowPlanning && !PlannerConnecting && motion == RouteMotion.Ground)
            {
                bool started = TryLocalDescentPrefix(body, state, goal, allowPlanning, out var nearby, out bool spent);
                if (spent)
                {
                    allowPlanning = false;
#if DEBUG
                    RouteCandidateChecks++;
                    RouteWork += DescentWork;
#endif
                }
                if (started && !RecentlyRejected(body, nearby))
                {
                    if (TryLocalAction(body, state, mobility, motion, nearby, goal, out var descent)) return descent;
                }
            }

            using (Scratch.Save())
            {
                if (allowPlanning && !PlannerConnecting && (motion == RouteMotion.Ground || motion == RouteMotion.Track || motion == RouteMotion.Liquid || motion == RouteMotion.Air) &&
                    TryModePrefix(body, state, mobility, goal, motion, direction, out var modeFrame)) return modeFrame;
                if (allowPlanning && motion == RouteMotion.Ground &&
                    TryStandingTeleporter(body, state, mobility, goal, out var transit)) return transit;
                if (motion == RouteMotion.Ground && !state.AwaitingEquipmentObservation &&
                    ((goal.Y - body.position.Y) * body.gravDir < -42 || WantsAirCatch(body, mobility, goal, targetVelocity)) &&
                    CanPowerAscent(mobility) && SafeTakeoff(body, mobility) &&
                    AirInput(body, mobility, motion, 0, -1, out var takeoff) && (takeoff.Jump || takeoff.Vertical != 0))
                {
                    advancing = true;
                    if (allowPlanning && PlannerHasWork)
                        SearchRoute(body, mobility, goal, stopDistance, motion, publish: false);
                    return takeoff;
                }
                // On wrong ground, vanilla treats a fresh horizontal press
                // as dismount. Let the real cart settle or request an explicit
                // safe dismount before using ordinary left/right controls.
                if (mobility.MountCart && !mobility.Track) return new CompanionMobilityFrame { Status = "等待接轨" };
                if (movingWithOwner && groundMove == 0 && Math.Abs(goal.Y - body.position.Y) < 36 &&
                    QuickWalk(body, direction, out _))
                    return new CompanionMobilityFrame { Status = "移动中" };
                bool wall = false;
                bool nearbyTier = motion == RouteMotion.Ground && Math.Abs(goal.Y - body.position.Y) < 36;
                bool clear = nearbyTier && QuickWalk(body, groundMove, out wall);
                // A normal tread may keep pressing against the lip rather
                // than offer a graph root. The known turned tooth can claim
                // this same planning slice through the usual local proof.
                if (nearbyTier && (!clear || wall) && allowPlanning && !state.AwaitingEquipmentObservation && CanHoik(body, mobility) && groundMove != 0 &&
                    HasHoikMouth(body, body.position, groundMove) &&
                    TryLocalHoik(body, state, mobility, goal, groundMove, out var hoik)) return hoik;
                if (nearbyTier && (!clear || wall) && allowPlanning && !PlannerHasKnownClosure &&
                    localPlanningWork == 0 && !state.AwaitingEquipmentObservation &&
                    groundMove != 0 && groundMove == direction && (body.velocity.Y == 0 || mobility.Sliding) &&
                    (body.releaseJump || body.autoJump) && MayProbeImmediateJump(body) && Wall(body, direction) &&
                    !FindClosedHorizontalDoor(body, body.position, direction, null, out _))
                {
                    // An imminent wall may already have a complete native
                    // jump-and-landing proof at this live pose. Give that
                    // proof the granted slice before waiting on remote exits.
                    // A known closure keeps its slice for dependency checks.
                    allowPlanning = false;
                    if (TryImmediateJump(body, state, mobility, goal, direction, out var cross)) return cross;
                }
                if (clear)
                {
                    advancing = true;
                    // The launch update itself uses vanilla's full impulse.
                    // The remaining lane must permit braking with neutral input.
                    if (!wall && !state.AwaitingEquipmentObservation && Math.Abs(goal.X - body.position.X) > 240 && allowPlanning && mobility.DashReady &&
                        groundMove == direction && CanStartDash(body, mobility.DashType, groundMove))
                        return new CompanionMobilityFrame { Move = groundMove, Dash = groundMove, Status = "冲刺中" };
                    return new CompanionMobilityFrame { Move = groundMove, Status = "移动中" };
                }
            }

            advancing = true;
            if (motion == RouteMotion.Hooked && mobility.GrappleAttached &&
                TryAttachedHookExit(body, state, goal, out var detach)) return detach;
            if (motion == RouteMotion.Ground || motion == RouteMotion.Air)
            {
                if (TryDoorPrefix(body, state, motion, goal, direction, allowPlanning, out var passage)) return passage;
                if (coordinator.ProofGrantUsed) allowPlanning = false;
            }
            CompanionMobilityFrame airFallback = default;
            if (motion == RouteMotion.Air)
            {
                // Cheap live steering can continue independently of the
                // frontier while it is making progress in the current pose.
                using (Scratch.Save())
                {
                    if (TryAirFollow(body, mobility, goal, targetVelocity, stopDistance / 2, out airFallback))
                    {
                        if (allowPlanning && PlannerHasWork)
                            SearchRoute(body, mobility, goal, stopDistance, motion, publish: false);
                        return airFallback;
                    }
                }
                if (allowPlanning && !actions.HasStep && PlannerHasCandidate)
                {
                    allowPlanning = false;
                    if (TryPlanRoute(body, state, mobility, motion, goal, stopDistance, out var retained)) return retained;
                }
                bool retreating = TryAirRetreat(body, state, mobility, goal, allowPlanning, out var retreat, out bool spent);
                if (spent) allowPlanning = false;
                if (retreating)
                {
                    if (allowPlanning) SearchRoute(body, mobility, goal, stopDistance, motion, publish: false);
                    return retreat;
                }
            }
            if (allowPlanning && !actions.HasStep)
            {
                if (trail != null)
                {
                    if (goals.OfferTrailGoal(body, trail, motion, ownerGoal, out Vector2 anchor, out int trailWork)) goal = anchor;
                    localPlanningWork += trailWork;
#if DEBUG
                    RouteWork += trailWork;
#endif
                }
                if (TryPlanRoute(body, state, mobility, motion, goal, trail != null && goals.Joined ? 12 : stopDistance,
                    out var planned)) return planned;
                if (trail != null && PlannerTrailClosed) goals.RejectHint();
            }

            if (airFallback.Jump || airFallback.Vertical != 0 || airFallback.Move != 0) return airFallback;
            if (followingOutlet && motion == RouteMotion.Ground && Math.Abs(goal.Y - body.position.Y) > 36)
                return new CompanionMobilityFrame { Status = "寻找路线" };
            var recovery = LocalRecovery(body, mobility, motion, goal);
            if (recovery.Status == "寻找路线") recovery.Status = PlannerStopStatus;
            return recovery;
        }

        private bool TryPlanRoute(Player body, CompanionMobilityState state, CompanionMobilitySnapshot mobility, RouteMotion motion,
            Vector2 goal, int stopDistance, out CompanionMobilityFrame frame)
        {
            frame = default;
            SearchRoute(body, mobility, goal, stopDistance, motion);
            if (!TakeConnectedRoute(out var plan) || plan.GoalEpoch != coordinator.GoalEpoch) return false;
            actions.Adopt(plan.Steps, plan.Goal, plan.PlanId);
            followingOutlet = plan.Outlet;
            if (!actions.HasStep) return false;
            var step = actions.Current;
            if (actions.Step(body, state, mobility, motion, step, out frame, out bool finished)) return true;
            CompleteAction(body, step, finished);
            return false;
        }

        private bool TryLocalHoik(Player body, CompanionMobilityState state, CompanionMobilitySnapshot mobility,
            Vector2 goal, int direction, out CompanionMobilityFrame frame)
        {
            frame = default;
            var at = new RouteNode { Position = body.position, Motion = RouteMotion.Ground, VelocityX = body.velocity.X };
            var step = new RouteStep { Kind = RouteKind.Hoik, From = body.position, ObserveAfter = true, ItemSlot = -1 };
            localPlanningWork += HoikWork;
#if DEBUG
            RouteCandidateChecks++;
            RouteWork += HoikWork;
#endif
            return TryHoikEdge(body, mobility, at, direction, null, ref step) && !RecentlyRejected(body, step) &&
                TryLocalAction(body, state, mobility, RouteMotion.Ground, step, goal, out frame);
        }

        private void CompleteAction(Player body, RouteStep step, bool reached)
        {
            var outcome = actions.Finish(body, step, reached);
            ReportActionOutcome(outcome);
            if (outcome.Result == ActionResult.LandedElsewhere || reached && (step.Kind == RouteKind.Drop || step.Kind == RouteKind.LedgeDrop))
                followingOutlet = false;
        }

        private bool RetainTrailGoal(Player body, CompanionMobilityState state, CompanionOwnerTrail trail, RouteMotion motion,
            Vector2 ownerGoal, ref Vector2 goal)
        {
            bool retained = goals.RetainTrailGoal(body, trail, motion, ownerGoal, actions.HasStep, ref goal, out bool retired);
            if (retired)
            {
                actions.RetireGoal(body, state, motion);
                RetirePlannedCandidate();
                followingOutlet = false;
            }
            return retained;
        }

        private bool TryAttachedHookExit(Player body, CompanionMobilityState state, Vector2 goal, out CompanionMobilityFrame frame)
        {
            frame = default;
            Vector2 delta = goal - body.position;
            int direction = Math.Abs(delta.X) >= 16 ? Math.Sign(delta.X) : 0;
            if (direction == 0 && Math.Abs(delta.Y) < 20) return false;
            var step = new RouteStep { From = body.position, To = body.position };
            using (Scratch.Save())
                if (!TryHookRelease(body, body.position, goal, direction, null, ref step) &&
                    (direction == 0 || !TryHookRelease(body, body.position, goal, 0, null, ref step)) || RecentlyRejected(body, step)) return false;
            // Retraction can move the last attached pose several pixels per
            // update. Validate the release here without waiting to connect
            // this body back to an older airborne search root.
            return TryLocalAction(body, state, state.Observed, RouteMotion.Hooked, step, goal, out frame);
        }

        private static bool GoalRedirects(Vector2 body, Vector2 previous, Vector2 current)
        {
            // Moving farther along the same corridor still needs the same
            // local detour. Only a changed side/layer can age out its suffix.
            return Math.Abs(current.X - previous.X) > 120 &&
                Math.Sign(current.X - body.X) != Math.Sign(previous.X - body.X) ||
                Math.Abs(current.Y - previous.Y) > 72 &&
                (Math.Abs(previous.Y - body.Y) <= 36 || Math.Abs(current.Y - body.Y) <= 36 ||
                    Math.Sign(current.Y - body.Y) != Math.Sign(previous.Y - body.Y));
        }

        private static bool NearbySupportedGoal(Player body, Vector2 goal)
        {
            Vector2 difference = goal - body.position;
            if (Math.Abs(difference.Y) > 12 || Math.Abs(difference.X) > 48) return false;
            using (Scratch.Save())
                return SolidForBody(body, goal, null) && Surface(body, goal, null) &&
                    SupportedLane(body, Math.Sign(difference.X), Math.Abs(difference.X), steps: true, out _);
        }

        private bool WantsPlatformAscent(Player body, CompanionMobilitySnapshot mobility, Vector2 goal)
        {
            if (body.gravDir != 1 || body.velocity.Y != 0 || Math.Abs(body.velocity.X) > 1 ||
                (!body.releaseJump && !body.autoJump) || goal.Y >= body.position.Y - 28 || !MayProbeImmediateJump(body)) return false;
            Point feet = body.Bottom.ToTileCoordinates();
            int maxRise = Math.Min(6, Math.Max(2, (int)(mobility.JumpSpeed * (mobility.JumpHeight + 5) / 16)));
            for (int lift = 2; lift <= maxRise; lift++)
            {
                int y = feet.Y - lift;
                if (!Loaded(feet.X, y)) return false;
                Tile tile = Main.tile[feet.X, y];
                if (tile.nactive() && Main.tileSolidTop[tile.type]) return true;
            }
            return false;
        }

        private bool MayProbeImmediateJump(Player body) => jumpProbeAt == 0 || unchecked(Main.GameUpdateCount - jumpProbeAt) >= 18 ||
            Vector2.DistanceSquared(jumpProbePosition, body.position) >= 12 * 12;

        private bool TryImmediateJump(Player body, CompanionMobilityState state, CompanionMobilitySnapshot mobility,
            Vector2 goal, int direction, out CompanionMobilityFrame frame)
        {
            frame = default;
            jumpProbeAt = Main.GameUpdateCount;
            jumpProbePosition = body.position;
#if DEBUG
            RouteCandidateChecks++;
            RouteWork += WorkForCandidate(RouteMotion.Ground, 4);
#endif
            using (Scratch.Save())
            {
                if (!TryJumpEdge(body, mobility, body.position, direction, true, 0, null, out var landing, out int hold, out _) ||
                    direction == 0 && Math.Abs(goal.Y - body.position.Y) - Math.Abs(goal.Y - landing.Y) <= 22) return false;
                var step = new RouteStep { Kind = RouteKind.Jump, From = body.position, To = landing, Direction = direction, Duration = hold,
                    Launch = new JumpInputs(body, mobility), EntryVelocityX = body.velocity.X, HasEntryVelocity = true };
                return !RecentlyRejected(body, step) && TryLocalAction(body, state, mobility, RouteMotion.Ground, step, goal, out frame);
            }
        }

        private bool TryDoorPrefix(Player body, CompanionMobilityState state, RouteMotion motion, Vector2 goal, int direction,
            bool allowPlanning, out CompanionMobilityFrame frame)
        {
            frame = default;
            var step = new RouteStep { Kind = RouteKind.Door, From = body.position, Direction = direction };
            using (Scratch.Save())
            {
                float height = goal.Y - body.position.Y;
                bool vertical = Math.Abs(height) > 28 && Math.Abs(goal.X - body.position.X) < 48 &&
                    TryTrapdoorEdge(body, body.position, Math.Sign(height), null, ref step, out _);
                bool horizontal = false;
                if (!vertical && direction != 0 && motion == RouteMotion.Ground && Math.Abs(height) < 48)
                {
                    horizontal = TryDoorImmediate(body, direction, allowPlanning, ref step, out bool spent, out bool approach);
                    if (spent) coordinator.ConsumeProofGrant();
                    if (!horizontal && approach && !RecentlyRejected(body, step))
                    {
                        frame = new CompanionMobilityFrame { Move = direction, Status = "接近门口" };
                        return true;
                    }
                }
                if ((!vertical && !horizontal) || RecentlyRejected(body, step)) return false;
            }
            return TryLocalAction(body, state, state.Observed, motion, step, goal, out frame);
        }

        private bool TryStandingTeleporter(Player body, CompanionMobilityState state, CompanionMobilitySnapshot mobility,
            Vector2 goal, out CompanionMobilityFrame frame)
        {
            frame = default;
            if (Vector2.DistanceSquared(body.position, goal) < 160 * 160) return false;
            Point feet = (body.Bottom + Vector2.UnitY).ToTileCoordinates();
            if (!Loaded(feet.X, feet.Y) || !Main.tile[feet.X, feet.Y].nactive() || Main.tile[feet.X, feet.Y].type != TileID.Teleporter)
                return false;
            var node = new RouteNode { Position = body.position, Motion = RouteMotion.Ground, Previous = -1 };
            var step = new RouteStep { From = body.position, To = body.position, ItemSlot = -1 };
            // A known native source underfoot can be lost by taking the first
            // ordinary walk step. Confirm its real destination while here.
            if (!TryTransit(body, mobility, node, goal, 26, ref step) || RecentlyRejected(body, step)) return false;
            return TryLocalAction(body, state, mobility, RouteMotion.Ground, step, goal, out frame);
        }

        private bool TryModePrefix(Player body, CompanionMobilityState state, CompanionMobilitySnapshot mobility,
            Vector2 goal, RouteMotion motion, int direction, out CompanionMobilityFrame frame, bool nearTrackGoal = false)
        {
            frame = default(CompanionMobilityFrame);
            if (!nearTrackGoal && direction == 0 && Math.Abs(goal.Y - body.position.Y) < 44) return false;
            var step = new RouteStep { From = body.position, To = body.position, Direction = direction, ItemSlot = -1 };
            bool rail = motion == RouteMotion.Ground && direction != 0 && TryRailEdge(body, mobility, motion, body.position,
                direction, goal, null, ref step);
            if (!rail && !TryPostureChange(body, mobility, motion, goal, ref step) || RecentlyRejected(body, step)) return false;
            if (motion == RouteMotion.Track && !nearTrackGoal && TryRailEdge(body, mobility, motion, body.position, direction, goal, null, ref step)) return false;
            return TryLocalAction(body, state, mobility, motion, step, goal, out frame);
        }

        private static bool TrackShouldSlow(Player body, Vector2 goal, int spacing)
        {
            float distance = Math.Abs(goal.X - body.position.X);
            if (distance > spacing + 64 || Math.Abs(goal.Y - body.position.Y) > 38) return false;
            if (distance <= spacing) return true;
            return Math.Sign(goal.X - body.position.X) == Math.Sign(body.velocity.X) &&
                TryStopDistance(body, Math.Abs(body.velocity.X), 0, out float braking) && distance <= spacing + braking;
        }

        private bool TryFacilityStride(Player body, CompanionMobilityState state, CompanionMobilitySnapshot mobility,
            Vector2 goal, RouteMotion motion, int direction, out CompanionMobilityFrame frame)
        {
            frame = default;
            using (Scratch.Save())
            {
                // Both short connections are local native control. Mounted
                // equipment changes and exits still require their usual
                // route/activation checks.
                if (motion == RouteMotion.Track && direction != 0)
                {
                    var rail = new RouteStep { From = body.position, Direction = direction, ItemSlot = -1 };
                    if (!TryRailEdge(body, mobility, motion, body.position, direction, goal, null, ref rail) ||
                        RecentlyRejected(body, rail)) return false;
                    return TryLocalAction(body, state, mobility, motion, rail, goal, out frame);
                }
                float height = goal.Y - body.position.Y;
                int vertical = Math.Sign(height);
                if (motion != RouteMotion.Rope && (motion != RouteMotion.Ground || height >= -28) ||
                    Math.Abs(height) < 24 || mobility.MountType >= 0 || body.gravDir < 0 ||
                    !TryRopeEdge(body, body.position, vertical, null, out var next) ||
                    Math.Abs(body.position.X - next.X) > 6) return false;
                frame = new CompanionMobilityFrame { Vertical = vertical, Status = "攀绳中" };
                return true;
            }
        }

        private static bool QuickWalk(Player body, int direction, out bool wall)
        {
            wall = false;
            if (direction == 0) return false;
            float along = body.velocity.X * direction;
            float ahead = 0;
            float nextSpeed = Math.Max(0, along + body.runAcceleration);
            if (along > 0 && !TryStopDistance(body, nextSpeed, 0, out ahead)) return false;
            if (along < 0 && (!TryStopDistance(body, -along, 0, out float behind) ||
                !SupportedLane(body, -direction, behind, steps: true, out _, proveBrakingSurface: behind >= 24))) return false;
            return SupportedLane(body, direction, ahead + Math.Max(WalkProbeDistance, nextSpeed), steps: true, out wall, proveBrakingSurface: ahead >= 24);
        }

        private static bool SupportedLane(Player body, int direction, float distance, bool steps, out bool wall, bool proveBrakingSurface = false)
        {
            wall = false;
            if (distance <= 0) return true;
            Vector2 position = body.position;
            if (!InPlayableBounds(body, position + new Vector2(direction * distance, 0))) return false;
            if (distance >= 16 && TryFlatLane(body, direction * distance, proveBrakingSurface)) return true;
            return TrySupportedTravel(body, position, direction, distance, null, out _, out wall, steps, proveBrakingSurface);
        }

        private static bool TryFlatLane(Player body, float delta, bool proveBrakingSurface)
        {
            bool upright = body.gravDir > 0;
            float supportY = body.position.Y + (upright ? body.height : 0);
            int edge = (int)Math.Round(supportY / 16);
            if (Math.Abs(edge * 16 - supportY) > .001f || body.wet) return false;
            int floor = upright ? edge : edge - 1;
            int top = upright ? (int)Math.Floor(body.position.Y / 16) : edge;
            int bottom = upright ? edge - 1 : (int)Math.Floor((body.position.Y + body.height - .01f) / 16);
            int left = (int)Math.Floor((body.position.X + Math.Min(0, delta)) / 16);
            int right = (int)Math.Floor((body.position.X + Math.Max(0, delta) + body.width - .01f) / 16);
            // On an aligned, unbroken slab the swept box and its support can
            // be proved from one tile rectangle. Slopes, ledges and moving
            // surfaces stay on the native per-step geometry path below.
            for (int x = left; x <= right; x++)
            {
                if (!Loaded(x, floor)) return false;
                Tile support = Main.tile[x, floor];
                if (!support.active() || support.inActive() || support.slope() != 0 || support.halfBrick() ||
                    !Main.tileSolid[support.type] || Main.tileSolidTop[support.type] || ChangesGroundSpeed(body, support.type) && proveBrakingSurface)
                    return false;
                for (int y = top; y <= bottom; y++)
                {
                    if (!Loaded(x, y)) return false;
                    Tile cell = Main.tile[x, y];
                    if (cell.liquid > 0 || cell.active() && !cell.inActive() && (Main.tileSolid[cell.type] || Main.tileSolidTop[cell.type])) return false;
                }
            }
            return true;
        }

        private static bool InPlayableBounds(Player body, Vector2 position)
        {
            if (Terraria.Testing.DebugOptions.noLimits || Main.rightWorld <= Main.leftWorld + 1280) return true;
            return position.X >= Main.leftWorld + 640 && position.X <= Main.rightWorld - 640 - body.width &&
                position.Y >= Main.topWorld + 640 && position.Y <= Main.bottomWorld - 640;
        }

        private static bool CanStartDash(Player body, int dash, int direction)
        {
            float impulse;
            switch (dash)
            {
                case 1: case 5: impulse = 16.9f; break;
                case 2: impulse = 14.5f; break;
                case 3: impulse = 21.9f; break;
                // The mount dash also launches the body upwards. Its track
                // and mounted movement stay in their native route handlers.
                default: return false;
            }
            if (body.wet || body.velocity.Y != 0 || body.windPushed || body.trackBoost != 0) return false;
            return TryStopDistance(body, impulse, dash, out float braking) &&
                SupportedLane(body, direction, impulse + braking + 6, steps: false, out _, proveBrakingSurface: true);
        }

        private static CompanionMobilityFrame ContinueDash(Player body, int dash, Vector2 goal, int stopDistance)
        {
            int direction = Math.Sign(body.velocity.X);
            float speed = Math.Abs(body.velocity.X);
            // Keep one more held update and its neutral stop inside known
            // ground. A shield hit can change speed, so use the latest body.
            if (direction != 0 && (goal.X - body.position.X) * direction > stopDistance + speed &&
                TryStopDistance(body, speed, dash, out float braking) &&
                SupportedLane(body, direction, speed + braking + 6, steps: false, out _, proveBrakingSurface: true))
                return new CompanionMobilityFrame { Move = direction, Status = "冲刺中" };
            return new CompanionMobilityFrame { Status = "减速中" };
        }

        private static bool SteadyBrakingSurface(Player body, Vector2 position)
        {
            int y = (int)((position.Y + (body.gravDir > 0 ? body.height + 1 : -1)) / 16);
            int left = (int)(position.X / 16), right = (int)((position.X + body.width - .01f) / 16);
            // FloorVisuals changes native acceleration/friction for these
            // surfaces. The rest of a dash cannot assume today's coefficients.
            for (int x = left; x <= right; x++)
            {
                var tile = Main.tile[x, y];
                if (tile.active() && !tile.inActive() && ChangesGroundSpeed(body, tile.type))
                    return false;
            }
            return true;
        }

        private static bool ChangesGroundSpeed(Player body, int type)
        {
            return TileID.Sets.IceSkateSlippery[type] || type == 197 || type == 198 || type == 229 ||
                type == TileID.MinecartTrack || TileID.Sets.ConveyorDirection[type] != 0 ||
                body.desertBoots && (TileID.Sets.Conversion.Sand[type] || TileID.Sets.Conversion.Sandstone[type] ||
                    TileID.Sets.Conversion.HardenedSand[type]);
        }

        private static bool TryStopDistance(Player body, float speed, int dash, out float distance)
        {
            distance = 0;
            float slowdown = body.runSlowdown;
            if (!(slowdown > 0) || body.windPushed || body.trackBoost != 0 || float.IsInfinity(speed) || float.IsNaN(speed)) return false;
            float runSpeed = Math.Max(body.accRunSpeed, body.maxRunSpeed);
            bool strongerDecay = dash == 2 || dash == 3;
            float fastThreshold = dash == 3 ? 14 : 12;
            // HorizontalMovement applies the neutral ground friction before
            // DashMovement damps overspeed or snaps back to ordinary running.
            // Failure to prove a stop inside this window never admits a dash.
            for (int tick = 0; speed > 0 && tick < 96; tick++)
            {
                speed = Math.Max(0, speed - slowdown);
                if (dash > 0 && speed > 0)
                {
                    if (speed > fastThreshold) speed *= strongerDecay ? .985f : .992f;
                    else if (speed > runSpeed) speed *= strongerDecay ? .94f : .96f;
                    else { speed = runSpeed; dash = 0; }
                }
                distance += speed;
            }
            return speed == 0;
        }

        private static CompanionMovementActivation ActivationFor(RouteKind stepKind)
        {
            switch (stepKind)
            {
                case RouteKind.Hook: return CompanionMovementActivation.Hook;
                case RouteKind.ReleaseHook: return CompanionMovementActivation.ReleaseHook;
                case RouteKind.Mount: return CompanionMovementActivation.QuickMount;
                case RouteKind.Dismount: return CompanionMovementActivation.Dismount;
                case RouteKind.Cart: return CompanionMovementActivation.QuickCart;
                case RouteKind.Door: return CompanionMovementActivation.Door;
                case RouteKind.Rod: return CompanionMovementActivation.Rod;
                case RouteKind.Pylon: return CompanionMovementActivation.Pylon;
                case RouteKind.Portal: return CompanionMovementActivation.Portal;
                case RouteKind.FlipGravity: return CompanionMovementActivation.Gravity;
                case RouteKind.Teleporter: return CompanionMovementActivation.Teleporter;
                default: return CompanionMovementActivation.None;
            }
        }

        private static bool NextClear(Player body, int horizontal, int vertical)
        {
            Vector2 motion = body.velocity;
            Vector2 delta = new Vector2(horizontal == 0 ? 0 : horizontal * Math.Max(6, Math.Abs(motion.X) + 2),
                vertical == 0 ? 0 : vertical * Math.Max(6, Math.Abs(motion.Y) + 2));
            return Swept(body, body.position, delta, null, passPlatform: vertical >= 0 && body.gravDir > 0);
        }

        private bool PlannedJumpInputClear(Player body, CompanionMobilitySnapshot mobility, int direction, bool afterLaunch, bool hold)
        {
            float speed = mobility.JumpSpeed > 0 ? mobility.JumpSpeed : jumpSpeed;
            var requested = JumpVelocity(body, mobility, body.velocity, direction, afterLaunch, hold, speed, body.gravity);
            return MoveJump(body, body.position, requested, body.wet, out var contact, null) &&
                (requested.Y * body.gravDir < 0 || !contact.Wall);
        }

        private static bool CanAdvanceJumpRunway(Player body, CompanionMobilitySnapshot mobility, int direction)
        {
            var run = MovementRun(body, mobility, airborne: false);
            float speed = run.Advance(body.velocity.X, direction);
            if (speed * direction <= 0) return QuickWalk(body, direction, out _);
            return CanBrakeJumpRunway(body, mobility, direction, speed, Math.Abs(speed));
        }

        private static bool CanBrakeJumpRunway(Player body, CompanionMobilitySnapshot mobility, int direction, float speed, float travel)
        {
            if (body.windPushed || body.dashDelay < 0 || mobility.Wet || Math.Abs(speed) > body.maxRunSpeed + .25f) return false;
            var run = MovementRun(body, mobility, airborne: false);
            for (int tick = 0; tick < 16; tick++)
            {
                speed = run.Advance(speed, -direction);
                if (speed * direction <= 0)
                    return SupportedLane(body, direction, travel, steps: false, out _, proveBrakingSurface: true);
                travel += Math.Abs(speed);
            }
            return false;
        }

        private static bool CanPowerAscent(CompanionMobilitySnapshot mobility) => HasWingThrust(mobility) ||
            mobility.RocketAvailable || mobility.MountFly || mobility.MountHover || mobility.ExtraJumps != 0 || mobility.Sliding;

        private static bool SafeTakeoff(Player body, CompanionMobilitySnapshot mobility)
        {
            var rise = new Vector2(body.velocity.X * 4, -body.gravDir * 24);
            // A short vertical prefix returns over the same support if power
            // runs out. A legal airborne transition can then replace it.
            return (!mobility.Lava || mobility.LavaImmune) && Swept(body, body.position, rise, null) &&
                (SafeFreeFlight(body, mobility) || Surface(body, body.position + new Vector2(rise.X, 0), null));
        }

        private static bool SafeFreeFlight(Player body, CompanionMobilitySnapshot mobility) =>
            body.noFallDmg || body.hasWings || mobility.UnlimitedWings || mobility.MountFly || mobility.MountHover;

        private static CompanionMobilityFrame LocalRecovery(Player body, CompanionMobilitySnapshot mobility, RouteMotion motion, Vector2 goal)
        {
            using (Scratch.Save())
            {
                int direction = Math.Sign(goal.X - body.position.X);
                if (motion == RouteMotion.Rope)
                {
                    int vertical = Math.Sign(goal.Y - body.position.Y);
                    if (Math.Abs(goal.Y - body.position.Y) >= 24 && TryRopeEdge(body, body.position, vertical, null, out _))
                        return new CompanionMobilityFrame { Vertical = vertical, Status = "攀绳中" };
                }
                if (motion == RouteMotion.Track)
                {
                    int brake = Math.Abs(body.velocity.X) > 1.5f ? -Math.Sign(body.velocity.X) : 0;
                    return new CompanionMobilityFrame { Move = brake, Status = "矿车减速中" };
                }
                // A clear next step in the owner's X direction is not a
                // connection to a different floor. Keep the live doorway or
                // approach already established by the route planner.
                if (motion == RouteMotion.Ground && Math.Abs(goal.Y - body.position.Y) < 36 &&
                    Math.Abs(goal.X - body.position.X) > 40 && QuickWalk(body, direction, out _))
                    return new CompanionMobilityFrame { Move = direction, Status = "接近目标" };
                if (motion == RouteMotion.Liquid && CanLeaveLiquid(body, direction, 0, mobility.Lava && mobility.LavaTime > 24))
                {
                    int vertical = Math.Abs(goal.Y - body.position.Y) > 10 ? Math.Sign(goal.Y - body.position.Y) : 0;
                    return new CompanionMobilityFrame { Move = direction,
                        Jump = mobility.CanSwim && (mobility.ReleaseJump || body.merman) && vertical == -(int)body.gravDir,
                        Vertical = mobility.MountSwim ? vertical : 0, Status = "游动中" };
                }
                return new CompanionMobilityFrame { Status = Footprint(body, body.position, Vector2.Zero) ? "寻找路线" : "等待地形就绪" };
            }
        }
    }
}
