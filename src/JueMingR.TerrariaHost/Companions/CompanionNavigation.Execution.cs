using System;
using JueMingR.TerrariaHost.Movement;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class CompanionNavigation
    {
        private int age, sequence, commandSequence, noProgress, positionRevision;
        private bool jumpStarted, jumpAccepted, actionStarted, advancing;
        private Vector2 progressPosition, actionOrigin, routeGoal;
        private RouteStep blockedStep;
        private uint blockedAt, postureChangedAt, fallHookAt, pylonRejectedAt, platformProbeAt;
        private Vector2 pylonRejectedAtBody, pylonRejectedDestination;
        private Vector2 platformProbePosition;
        private int pylonRejectedTile;
        private object routeWorld;

        internal static bool HasMobilitySupport(Player body, CompanionMobilitySnapshot mobility)
        {
            if (mobility.GrappleAttached || mobility.Pulley || mobility.Track || mobility.Shimmer ||
                body.velocity.Y * body.gravDir < 0 || body.velocity.Y * body.gravDir > 1) return false;
            using (Scratch.Save()) return Surface(body, body.position, null, !body.stairFall);
        }

        internal CompanionMobilityFrame Escape(Player body, CompanionMobilityState state, Vector2 target, bool grounded,
            bool needsAir, bool lava, int threatEscape, bool canJump)
        {
            ReleaseRoute();
            var mobility = state.Observed;
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
                mobility.Pulley || !SmartFallCapabilities.CriticalApproach(body)) return false;

            long reads = 0;
            var contact = SmartFallGeometry.Contact(body.position, body.velocity, body.width, body.height, body.gravity,
                body.maxFallSpeed, body.gravDir, body.controlDown, ref reads, horizon: 12);
            if (!SmartFallCapabilities.ContactRisk(body, contact) || !Loaded(contact.TileX, contact.TileY)) return false;
            using (Scratch.Save())
            {
                if (AirInput(body, mobility, RouteMotion.Air, 0, -1, out frame))
                {
                    ReleaseRoute();
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
                ReleaseRoute();
                route.Add(step);
                return ExecuteStep(body, state, mobility, RouteMotion.Air, step, out frame, out _);
            }
        }

        internal void CancelRoute()
        {
            ReleaseRoute();
            RetirePublication();
            pendingResult = -1;
            roofProbeSet = false;
            advancing = false;
            if (search != null) { search.Active = false; search.Exhausted = false; }
        }

        private void ReleaseRoute()
        { route.Clear(); routeIndex = 0; ResetStep(); }

        private void ResetStep()
        {
            age = noProgress = commandSequence = 0;
            jumpStarted = jumpAccepted = actionStarted = false;
        }

        internal bool TryCombatJump(Player body, CompanionMobilityState state, Vector2 aimOpening, out CompanionMobilityFrame frame)
        {
            frame = default(CompanionMobilityFrame);
            if (routeIndex < route.Count || !cached || !cachedResult || !hasLanding || !ReferenceEquals(plannedBody, body) ||
                !ReferenceEquals(plannedWorld, Main.ActiveWorldFileData) || targetHeight != aimOpening.Y ||
                launch != body.position || initialVelocity != body.velocity || !jumpReads.Current()) return false;
            // Combat asks for a shooting window during the already-proven
            // jump. Its aim point can be in mid-air; finish at the certified
            // landing instead of searching for a place to stand at the aim point.
            var step = new RouteStep { Kind = RouteKind.Jump, From = launch, To = landing, Direction = plannedDirection, Duration = jumpHold,
                Launch = new JumpInputs(body, state.Observed) };
            if (RecentlyRejected(step)) return false;
            ReleaseRoute();
            routeWorld = Main.ActiveWorldFileData;
            positionRevision = state.PositionRevision;
            routeGoal = aimOpening - body.Size * .5f;
            route.Add(step);
            return ExecuteStep(body, state, state.Observed, RouteMotion.Ground, step, out frame, out _);
        }

        private void RejectStep(RouteStep step)
        {
            blockedStep = step;
            blockedAt = Main.GameUpdateCount;
            if (step.Kind == RouteKind.Pylon && actionStarted)
            {
                pylonRejectedAt = blockedAt;
                pylonRejectedAtBody = step.From;
                pylonRejectedDestination = step.Operand;
                pylonRejectedTile = FacilityTileValue(step.Operand);
            }
            ReleaseRoute();
        }

        private static int FacilityTileValue(Vector2 point)
        {
            int x = (int)point.X / 16, y = (int)point.Y / 16;
            return Loaded(x, y) ? TileValue(Main.tile[x, y]) : int.MinValue;
        }

        private bool RecentlyRejected(RouteStep step)
        {
            // A pylon refusal may emit a vanilla chat message. Avoid repeated
            // requests from the same entrance to the same unchanged endpoint.
            // Housing/biome changes without a tile edit get a low-frequency
            // recheck through the real native authority.
            if (step.Kind == RouteKind.Pylon && pylonRejectedAt != 0 &&
                unchecked(Main.GameUpdateCount - pylonRejectedAt) < 600 && step.Operand == pylonRejectedDestination &&
                Vector2.DistanceSquared(step.From, pylonRejectedAtBody) < 48 * 48 &&
                FacilityTileValue(step.Operand) == pylonRejectedTile) return true;
            if (blockedAt == 0 || unchecked(Main.GameUpdateCount - blockedAt) > 48 || step.Kind != blockedStep.Kind) return false;
            return step.DescentEntersLiquid == blockedStep.DescentEntersLiquid &&
                Vector2.DistanceSquared(step.From, blockedStep.From) < 20 * 20 &&
                Vector2.DistanceSquared(step.To, blockedStep.To) < 20 * 20 && step.Direction == blockedStep.Direction && step.Launch.Equals(blockedStep.Launch);
        }

        private static RouteMotion CurrentMotion(Player body, CompanionMobilitySnapshot mobility, bool grounded)
        {
            if (mobility.GrappleAttached || mobility.GrappleInFlight) return RouteMotion.Hooked;
            if (mobility.Pulley) return RouteMotion.Rope;
            if (mobility.Track) return RouteMotion.Track;
            if (mobility.Wet && !grounded) return RouteMotion.Liquid;
            return grounded ? RouteMotion.Ground : RouteMotion.Air;
        }

        internal CompanionMobilityFrame FollowRoute(Player body, CompanionMobilityState state, Vector2 target, Vector2 targetVelocity, bool allowPlanning,
            bool grounded, int stopDistance, int resumeDistance)
        {
            descentCommitRead = false;
            var mobility = state.Observed;
            var offset = new Vector2(body.width / 2f, body.height / 2f);
            Vector2 goal = target - offset;
            var motion = CurrentMotion(body, mobility, grounded);
            if (!ReferenceEquals(routeWorld, Main.ActiveWorldFileData) || positionRevision != state.PositionRevision)
            {
                CancelRoute();
                blockedAt = 0;
                descentObstruction = null;
                pylonRejectedAt = 0;
                routeWorld = Main.ActiveWorldFileData;
                positionRevision = state.PositionRevision;
            }
            if (body.dead || !body.active || body.CCed || mobility.Controlled || mobility.Shimmer)
            {
                ReleaseRoute();
                return new CompanionMobilityFrame { Status = "暂时受控" };
            }
            if (allowPlanning && WakeChangedDescentObstruction(body, motion, goal) && routeIndex < route.Count &&
                route[routeIndex].Kind == RouteKind.Walk)
                ReleaseRoute();

            int threshold = advancing ? stopDistance : resumeDistance;
            if (motion == RouteMotion.Ground && mobility.DashActive && body.dashDelay < 0)
            {
                using (Scratch.Save())
                {
                    advancing = true;
                    return ContinueDash(body, mobility.DashKind, goal, threshold);
                }
            }
            if (allowPlanning && !state.AwaitingEquipmentObservation && motion == RouteMotion.Ground &&
                (routeIndex >= route.Count || route[routeIndex].Kind == RouteKind.Walk) &&
                WantsPlatformAscent(body, mobility, goal))
            {
                // An actual higher platform under this body's next vertical
                // jump is useful even while the owner moves along that tier.
                // Spend the one selected planning grant on this native sweep.
                allowPlanning = false;
                platformProbeAt = Main.GameUpdateCount;
                platformProbePosition = body.position;
#if DEBUG
                RouteCandidateChecks++;
                RouteWork += WorkForCandidate(RouteMotion.Ground, 4);
#endif
                using (Scratch.Save())
                    if (TryJumpEdge(body, mobility, body.position, 0, true, 0, null, out var landing, out int hold, out _) &&
                        Math.Abs(goal.Y - body.position.Y) - Math.Abs(goal.Y - landing.Y) > 22)
                    {
                        var step = new RouteStep { Kind = RouteKind.Jump, From = body.position, To = landing, Duration = hold,
                            Launch = new JumpInputs(body, mobility) };
                        if (!RecentlyRejected(step))
                        {
                            ReleaseRoute(); route.Add(step); routeGoal = goal;
                            if (ExecuteStep(body, state, mobility, motion, step, out var rise, out _)) return rise;
                            RejectStep(step);
                        }
                    }
            }
            if (routeIndex < route.Count && motion == RouteMotion.Ground && !actionStarted && !body.stairFall &&
                (route[routeIndex].Kind == RouteKind.Drop || route[routeIndex].Kind == RouteKind.LedgeDrop) &&
                NearbySupportedGoal(body, goal))
            {
                // A live owner back on this clear level no longer needs
                // this descent. After native release, finish the safe motion.
                ReleaseRoute();
                routeGoal = goal;
            }
            if (routeIndex < route.Count && GoalRedirects(body.position, routeGoal, goal))
            {
                // Each active segment still checks its next input. Aim changes
                // expire the remote suffix, including while walking or flying.
                if (routeIndex + 1 < route.Count) route.RemoveRange(routeIndex + 1, route.Count - routeIndex - 1);
                routeGoal = goal;
            }

            for (int next = 0; next < 2 && routeIndex < route.Count; next++)
            {
                RouteStep step = route[routeIndex];
                if (ExecuteStep(body, state, mobility, motion, step, out CompanionMobilityFrame frame, out bool finished, allowPlanning))
                {
                    if (allowPlanning && !descentCommitRead && route.Count - routeIndex <= 2 &&
                        (step.Retreat || search != null && search.Active))
                        SearchRoute(body, mobility, goal, stopDistance, motion, publish: false);
                    return frame;
                }
                if (descentCommitRead) allowPlanning = false;
                if (finished) CompleteRouteStep(step);
                else RejectStep(step);
            }

            if (allowPlanning && routeIndex >= route.Count && pendingResult >= 0 && search != null && search.NeedsLiveRoot)
            {
                allowPlanning = false;
                if (TryPlanRoute(body, state, mobility, motion, goal, stopDistance, out var handoff)) return handoff;
            }

            int direction = Math.Sign(goal.X - body.position.X);
            using (Scratch.Save())
            {
                bool movingWithOwner = motion == RouteMotion.Ground && Math.Abs(targetVelocity.X) >= 1f;
                int groundMove = movingWithOwner ? FollowHorizontal(body, goal.X, targetVelocity.X, body.accRunSpeed,
                    body.runAcceleration, body.runSlowdown, stopDistance / 2) : direction;
                if (motion == RouteMotion.Ground && (!movingWithOwner || groundMove == 0) &&
                    Reached(body.position, goal, threshold) && InGoalRegion(body, body.position, goal, null))
                {
                    ReleaseRoute(); advancing = false;
                    return new CompanionMobilityFrame { Status = "停留" };
                }
                if (allowPlanning && (motion == RouteMotion.Ground || motion == RouteMotion.Track || motion == RouteMotion.Liquid || motion == RouteMotion.Air) &&
                    TryModePrefix(body, state, mobility, goal, motion, direction, out var modeFrame)) return modeFrame;
                if (allowPlanning && motion == RouteMotion.Ground &&
                    TryStandingTeleporter(body, state, mobility, goal, out var transit)) return transit;
                if (motion == RouteMotion.Ground && !state.AwaitingEquipmentObservation &&
                    ((goal.Y - body.position.Y) * body.gravDir < -42 || WantsAirCatch(body, mobility, goal, targetVelocity)) &&
                    CanPowerAscent(mobility) && SafeTakeoff(body, mobility) &&
                    AirInput(body, mobility, motion, 0, -1, out var takeoff) && (takeoff.Jump || takeoff.Vertical != 0))
                {
                    advancing = true;
                    if (allowPlanning && search != null && search.Active)
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
                if (motion == RouteMotion.Ground && Math.Abs(goal.Y - body.position.Y) < 36 &&
                    QuickWalk(body, groundMove, out bool wall))
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
            if ((motion == RouteMotion.Ground || motion == RouteMotion.Air) &&
                TryDoorPrefix(body, state, motion, goal, direction, out var passage)) return passage;
            CompanionMobilityFrame airFallback = default;
            if (motion == RouteMotion.Air)
            {
                // Cheap live steering can continue independently of the
                // frontier while it is making progress in the current pose.
                using (Scratch.Save())
                {
                    if (TryAirFollow(body, mobility, goal, targetVelocity, stopDistance / 2, out airFallback))
                    {
                        if (allowPlanning && search != null && search.Active)
                            SearchRoute(body, mobility, goal, stopDistance, motion, publish: false);
                        return airFallback;
                    }
                }
                if (allowPlanning && routeIndex >= route.Count && (pendingResult >= 0 || publishing >= 0))
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
            if (allowPlanning && routeIndex >= route.Count &&
                TryPlanRoute(body, state, mobility, motion, goal, stopDistance, out var planned)) return planned;

            if (airFallback.Jump || airFallback.Vertical != 0 || airFallback.Move != 0) return airFallback;
            var recovery = LocalRecovery(body, mobility, motion, goal);
            if (recovery.Status == "寻找路线" && search != null && search.Exhausted)
                recovery.Status = search.Terrain.HasUnknown ? "等待地形就绪" : "暂无可行路线";
            return recovery;
        }

        private bool TryPlanRoute(Player body, CompanionMobilityState state, CompanionMobilitySnapshot mobility, RouteMotion motion,
            Vector2 goal, int stopDistance, out CompanionMobilityFrame frame)
        {
            frame = default;
            SearchRoute(body, mobility, goal, stopDistance, motion);
            if (routeIndex >= route.Count) return false;
            routeGoal = goal;
            var step = route[routeIndex];
            if (ExecuteStep(body, state, mobility, motion, step, out frame, out bool finished)) return true;
            if (finished) CompleteRouteStep(step);
            else RejectStep(step);
            return false;
        }

        private void CompleteRouteStep(RouteStep step)
        {
            routeIndex++;
            ResetStep();
            if (step.ObserveAfter && search != null) search.NeedsLiveRoot = true;
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
                    (direction == 0 || !TryHookRelease(body, body.position, goal, 0, null, ref step)) || RecentlyRejected(step)) return false;
            // Retraction can move the last attached pose several pixels per
            // update. Validate the release here without waiting to connect
            // this body back to an older airborne search root.
            ReleaseRoute(); route.Add(step); routeGoal = goal;
            if (ExecuteStep(body, state, state.Observed, RouteMotion.Hooked, step, out frame, out _)) return true;
            RejectStep(step);
            return false;
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
                (!body.releaseJump && !body.autoJump) || goal.Y >= body.position.Y - 28 ||
                platformProbeAt != 0 && unchecked(Main.GameUpdateCount - platformProbeAt) < 18 &&
                Vector2.DistanceSquared(platformProbePosition, body.position) < 12 * 12) return false;
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

        private bool TryDoorPrefix(Player body, CompanionMobilityState state, RouteMotion motion, Vector2 goal, int direction,
            out CompanionMobilityFrame frame)
        {
            frame = default;
            var step = new RouteStep { Kind = RouteKind.Door, From = body.position, Direction = direction };
            using (Scratch.Save())
            {
                float height = goal.Y - body.position.Y;
                bool vertical = Math.Abs(height) > 28 && Math.Abs(goal.X - body.position.X) < 48 &&
                    TryTrapdoorEdge(body, body.position, Math.Sign(height), null, ref step, out _);
                bool horizontal = !vertical && direction != 0 && motion == RouteMotion.Ground && Math.Abs(height) < 48 &&
                    TryDoorEdge(body, body.position, direction, null, out step.Operand, out step.To);
                if ((!vertical && !horizontal) || RecentlyRejected(step)) return false;
            }
            ReleaseRoute();
            route.Add(step);
            routeGoal = goal;
            if (ExecuteStep(body, state, state.Observed, motion, step, out frame, out _)) return true;
            RejectStep(step);
            return false;
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
            if (!TryTransit(body, mobility, node, goal, 26, ref step) || RecentlyRejected(step)) return false;
            ReleaseRoute();
            route.Add(step);
            routeGoal = goal;
            if (ExecuteStep(body, state, mobility, RouteMotion.Ground, step, out frame, out _)) return true;
            RejectStep(step);
            return false;
        }

        private bool TryModePrefix(Player body, CompanionMobilityState state, CompanionMobilitySnapshot mobility,
            Vector2 goal, RouteMotion motion, int direction, out CompanionMobilityFrame frame)
        {
            frame = default(CompanionMobilityFrame);
            if (direction == 0 && Math.Abs(goal.Y - body.position.Y) < 44) return false;
            var step = new RouteStep { From = body.position, To = body.position, Direction = direction, ItemSlot = -1 };
            bool rail = motion == RouteMotion.Ground && direction != 0 && TryRailEdge(body, mobility, motion, body.position,
                direction, goal, null, ref step);
            if (!rail && !TryPostureChange(body, mobility, motion, goal, ref step) || RecentlyRejected(step)) return false;
            if (motion == RouteMotion.Track && TryRailEdge(body, mobility, motion, body.position, direction, goal, null, ref step)) return false;
            route.Clear(); route.Add(step); routeIndex = 0; routeGoal = goal;
            ResetStep();
            return ExecuteStep(body, state, mobility, motion, step, out frame, out _);
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

        private bool ExecuteStep(Player body, CompanionMobilityState state, CompanionMobilitySnapshot mobility,
            RouteMotion motion, RouteStep step, out CompanionMobilityFrame frame, out bool finished, bool allowDescentCommit = false)
        {
            frame = new CompanionMobilityFrame { Status = "移动中" };
            finished = false;
            if (age == 0) actionOrigin = progressPosition = body.position;
            age++;
            if (Vector2.DistanceSquared(progressPosition, body.position) > 4)
            { progressPosition = body.position; noProgress = 0; }
            else noProgress++;
            bool inVerifiedDescent = actionStarted && (step.Kind == RouteKind.Drop || step.Kind == RouteKind.LedgeDrop);
            if (!inVerifiedDescent && age > (step.Kind == RouteKind.GlideJump ? 190 : 150) ||
                noProgress > 24 && !actionStarted && !jumpAccepted) return false;

            using (Scratch.Save())
            {
                if (!Footprint(body, body.position, body.velocity)) return false;
                float along = (step.To.X - body.position.X) * step.Direction;
                float vertical = (step.To.Y - body.position.Y) * (body.gravDir < 0 ? -1 : 1);
                int direction = step.Direction;
                switch (step.Kind)
                {
                    case RouteKind.Walk:
                        if (along <= 4 && Math.Abs(vertical) <= 18) { finished = motion == RouteMotion.Ground; if (finished) return false; }
                        if (motion != RouteMotion.Ground || !QuickWalk(body, direction, out _)) return false;
                        frame.Move = direction;
                        return true;
                    case RouteKind.Jump: case RouteKind.RopeExit: case RouteKind.GlideJump:
                        bool glide = step.Kind == RouteKind.GlideJump;
                        if (glide && !jumpStarted && (!mobility.CarpetAvailable || mobility.Wet)) return false;
                        if (!jumpStarted)
                        {
                            if (motion != RouteMotion.Ground && motion != RouteMotion.Rope) return false;
                            if (state.AwaitingEquipmentObservation) { frame.Status = "重新判断起跳"; return true; }
                            if (step.Launch.Captured && !step.Launch.Equals(new JumpInputs(body, mobility)))
                            {
                                // A previously certified jump cannot launch
                                // with different physics. Rejoin the existing
                                // frontier with this real pose and capabilities.
                                if (search != null) search.NeedsLiveRoot = true;
                                return false;
                            }
                            frame.Status = "跨越障碍";
                            if (motion == RouteMotion.Ground && body.velocity.Y != 0 && !mobility.Sliding) return true;
                            if (!body.releaseJump && !body.autoJump && motion != RouteMotion.Rope) return true;
                            if (!NextClear(body, 0, -(int)body.gravDir)) return false;
                            // A walk prefix may outlive its far bank. Check
                            // the landing again at the actual takeoff input.
                            if (glide ? !GlideLandingClear(body, step.To, new Vector2(direction * 12, 0), null) :
                                !SolidForBody(body, step.To, null) || !Surface(body, step.To, null)) return false;
                            jumpStarted = frame.Jump = true; frame.Move = direction;
                            actionOrigin = body.position;
                            return true;
                        }
                        if (!jumpAccepted)
                        {
                            jumpAccepted = body.velocity.Y * body.gravDir < 0 && (body.position.Y - actionOrigin.Y) * body.gravDir < 0;
                            if (!jumpAccepted) return false;
                        }
                        if (motion == RouteMotion.Ground)
                        { finished = (!glide || actionStarted) && Math.Abs(vertical) <= 24 && (direction == 0 || along <= 32); return false; }
                        frame.Move = along > 4 && NextClear(body, direction, 0) ? direction : 0;
                        frame.Jump = mobility.JumpTime > 0 && body.velocity.Y * body.gravDir < 0 && NextClear(body, 0, -(int)body.gravDir);
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
                        return DriveDescentStep(body, state, mobility, motion, step, allowDescentCommit, out frame, out finished);
                    case RouteKind.Descend:
                        if (age > 1 && vertical <= 4 && (direction == 0 || along <= 5) &&
                            body.velocity.Y * body.gravDir >= -.01f)
                        {
                            if (step.Retreat && motion == RouteMotion.Air &&
                                ContinueAirRetreat(body, mobility, routeGoal, ref step))
                            {
                                route[routeIndex] = step;
                                return AirInput(body, mobility, motion, step.Direction,
                                    step.Kind == RouteKind.AirAcross ? 0 : 1, out frame);
                            }
                            finished = true;
                            return false;
                        }
                        return motion == RouteMotion.Air && DriveFlight(body, mobility, motion, step.To, Vector2.Zero, 3, 1, out frame);
                    case RouteKind.Ascend: case RouteKind.AirAcross:
                        if (step.Retreat)
                        {
                            if (step.Kind == RouteKind.Ascend)
                            {
                                if (vertical >= -4) { finished = true; return false; }
                                return AirInput(body, mobility, motion, 0, -1, out frame, quickAscent: vertical < -64);
                            }
                            if (Math.Abs(step.To.X - body.position.X) <= 8 && Math.Abs(body.velocity.X) < 2 ||
                                motion == RouteMotion.Ground)
                            {
                                if (step.Vertical == 0 || !NextClear(body, 0, -(int)body.gravDir))
                                { finished = true; return false; }
                                step.Kind = RouteKind.Ascend;
                                step.To = new Vector2(body.position.X, routeGoal.Y);
                                step.Direction = 0;
                                route[routeIndex] = step;
                                return AirInput(body, mobility, motion, 0, -1, out frame);
                            }
                            return motion == RouteMotion.Air &&
                                DriveFlight(body, mobility, motion, step.To, Vector2.Zero, 2, 0, out frame);
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
                        return DriveDoorStep(body, state, step, out frame, out finished);
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
            if (actionStarted && state.Sequence == commandSequence)
            {
                if (state.Result == CompanionMobilityResult.Rejected) return false;
                if (state.Result == CompanionMobilityResult.Completed)
                {
                    if (step.Kind == RouteKind.Hook && mobility.GrappleAttached &&
                        Vector2.DistanceSquared(body.position, step.To) > 16 * 16 && body.velocity.LengthSquared() > 1)
                    { frame.Status = "抓钩牵引中"; return true; }
                    if (step.Kind == RouteKind.Mount || step.Kind == RouteKind.Dismount || step.Kind == RouteKind.Cart)
                        postureChangedAt = Main.GameUpdateCount;
                    finished = true;
                    return false;
                }
            }

            if (commandSequence == 0) commandSequence = ++sequence;
            actionStarted = true;
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
                    if (vertical != 0 && TryRopeEdge(body, body.position, vertical, null, out _))
                        return new CompanionMobilityFrame { Vertical = vertical, Status = "攀绳中" };
                }
                if (motion == RouteMotion.Track)
                {
                    int brake = Math.Abs(body.velocity.X) > 1.5f ? -Math.Sign(body.velocity.X) : 0;
                    return new CompanionMobilityFrame { Move = brake, Status = "矿车减速中" };
                }
                if (motion == RouteMotion.Ground && Math.Abs(goal.X - body.position.X) > 40 && QuickWalk(body, direction, out _))
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
