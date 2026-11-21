using System;
using JueMingR.TerrariaHost.Movement;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class CompanionNavigation
    {
        private int age, sequence, commandSequence, noProgress, positionRevision;
        private bool jumpStarted, jumpAccepted, actionStarted, advancing;
        private Vector2 progressPosition, actionOrigin, routeGoal;
        private RouteStep blockedStep;
        private uint blockedAt, postureChangedAt, fallHookAt, pylonRejectedAt;
        private Vector2 pylonRejectedAtBody, pylonRejectedDestination;
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
            CancelRoute();
            var mobility = state.Observed;
            if (body.dead || body.CCed || mobility.Controlled || mobility.Shimmer) return new CompanionMobilityFrame { Status = "暂时受控" };
            var motion = CurrentMotion(body, mobility, grounded);
            int direction = needsAir || lava ? Math.Sign(target.X - body.Center.X) : threatEscape;
            string status = needsAir ? "寻找可呼吸位置" : "避险中";
            using (Scratch.Save())
            {
                if (motion == RouteMotion.Ground && !lava)
                {
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
                if (AirInput(body, mobility, motion, direction, true, out var rise)) { rise.Status = status; return rise; }
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
                if (AirInput(body, mobility, RouteMotion.Air, 0, true, out frame))
                {
                    CancelRoute();
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
                CancelRoute();
                route.Add(step);
                return ExecuteStep(body, state, mobility, RouteMotion.Air, step, out frame, out _);
            }
        }

        internal void CancelRoute()
        {
            route.Clear(); routeIndex = 0; advancing = false;
            ResetStep();
            if (search != null) { search.Active = false; search.Exhausted = false; }
        }

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
            var step = new RouteStep { Kind = RouteKind.Jump, From = launch, To = landing, Direction = plannedDirection, Duration = jumpHold };
            if (RecentlyRejected(step)) return false;
            CancelRoute();
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
            CancelRoute();
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
            return Vector2.DistanceSquared(step.From, blockedStep.From) < 20 * 20 &&
                Vector2.DistanceSquared(step.To, blockedStep.To) < 20 * 20 && step.Direction == blockedStep.Direction;
        }

        private static RouteMotion CurrentMotion(Player body, CompanionMobilitySnapshot mobility, bool grounded)
        {
            if (mobility.GrappleAttached || mobility.GrappleInFlight) return RouteMotion.Hooked;
            if (mobility.Pulley) return RouteMotion.Rope;
            if (mobility.Track) return RouteMotion.Track;
            if (mobility.Wet && !grounded) return RouteMotion.Liquid;
            return grounded ? RouteMotion.Ground : RouteMotion.Air;
        }

        internal CompanionMobilityFrame FollowRoute(Player body, CompanionMobilityState state, Vector2 target, bool allowPlanning,
            bool grounded, int stopDistance, int resumeDistance)
        {
            var mobility = state.Observed;
            var offset = new Vector2(body.width / 2f, body.height / 2f);
            Vector2 goal = target - offset;
            var motion = CurrentMotion(body, mobility, grounded);
            if (!ReferenceEquals(routeWorld, Main.ActiveWorldFileData) || positionRevision != state.PositionRevision)
            {
                CancelRoute();
                blockedAt = 0;
                pylonRejectedAt = 0;
                routeWorld = Main.ActiveWorldFileData;
                positionRevision = state.PositionRevision;
            }
            if (body.dead || !body.active || body.CCed || mobility.Controlled || mobility.Shimmer)
            {
                CancelRoute();
                return new CompanionMobilityFrame { Status = "暂时受控" };
            }

            int threshold = advancing ? stopDistance : resumeDistance;
            if (routeIndex < route.Count && (Math.Abs(goal.X - routeGoal.X) > 120 || Math.Abs(goal.Y - routeGoal.Y) > 72))
            {
                // Keep an accepted jump/traction until a real safe transition.
                if (!jumpAccepted && !actionStarted) CancelRoute();
                else if (routeIndex + 1 < route.Count) route.RemoveRange(routeIndex + 1, route.Count - routeIndex - 1);
            }

            if (routeIndex < route.Count)
            {
                RouteStep step = route[routeIndex];
                if (ExecuteStep(body, state, mobility, motion, step, out CompanionMobilityFrame frame, out bool finished)) return frame;
                if (finished) { routeIndex++; ResetStep(); }
                else RejectStep(step);
            }

            if (routeIndex < route.Count)
            {
                RouteStep step = route[routeIndex];
                if (ExecuteStep(body, state, mobility, motion, step, out CompanionMobilityFrame frame, out bool finished)) return frame;
                if (finished) { routeIndex++; ResetStep(); }
                else RejectStep(step);
            }

            int direction = Math.Sign(goal.X - body.position.X);
            using (Scratch.Save())
            {
                if (motion == RouteMotion.Ground && Reached(body.position, goal, threshold) && InGoalRegion(body, body.position, goal, null))
                {
                    CancelRoute();
                    return new CompanionMobilityFrame { Status = "停留" };
                }
                if (allowPlanning && (motion == RouteMotion.Ground || motion == RouteMotion.Track || motion == RouteMotion.Liquid) &&
                    TryModePrefix(body, state, mobility, goal, motion, direction, out var modeFrame)) return modeFrame;
                // On wrong ground, vanilla treats a fresh horizontal press
                // as dismount. Let the real cart settle or request an explicit
                // safe dismount before using ordinary left/right controls.
                if (mobility.MountCart && !mobility.Track) return new CompanionMobilityFrame { Status = "等待接轨" };
                if (motion == RouteMotion.Ground && Math.Abs(goal.Y - body.position.Y) < 36 &&
                    QuickWalk(body, direction, out bool wall))
                {
                    advancing = true;
                    // A wide, supported run lane permits the real dash. Short
                    // approaches and dynamic obstacles continue using native braking.
                    if (!wall && Math.Abs(goal.X - body.position.X) > 240 && allowPlanning && mobility.DashReady &&
                        DashLane(body, direction))
                        return new CompanionMobilityFrame { Move = direction, Dash = direction, Status = "冲刺中" };
                    return new CompanionMobilityFrame { Move = direction, Status = "移动中" };
                }
            }

            advancing = true;
            if (allowPlanning && routeIndex >= route.Count)
            {
                SearchRoute(body, mobility, goal, stopDistance, motion);
                if (routeIndex < route.Count)
                {
                    routeGoal = goal;
                    var step = route[routeIndex];
                    if (ExecuteStep(body, state, mobility, motion, step, out CompanionMobilityFrame frame, out bool finished)) return frame;
                    if (finished) { routeIndex++; ResetStep(); }
                    else RejectStep(step);
                }
            }

            return LocalRecovery(body, mobility, motion, goal);
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
            if (search != null) search.Active = false;
            ResetStep();
            return ExecuteStep(body, state, mobility, motion, step, out frame, out _);
        }

        private static bool QuickWalk(Player body, int direction, out bool wall)
        {
            wall = false;
            if (direction == 0) return false;
            float along = Math.Max(0, body.velocity.X * direction);
            float braking = Math.Min(28, along * along / Math.Max(.14f, 2 * body.runSlowdown));
            int checks = (int)(braking / 6) + 1;
            Vector2 position = body.position;
            if (!InPlayableBounds(body, position + new Vector2(direction * Math.Max(6, braking), 0))) return false;
            for (int i = 0; i < checks; i++)
            {
                if (body.gravDir < 0)
                {
                    Vector2 delta = new Vector2(direction * 6, 0);
                    if (!Swept(body, position, delta, null) || !Surface(body, position + delta, null)) return false;
                    position += delta;
                    continue;
                }
                if (!Move(body, position, new Vector2(direction * 6, Math.Max(.1f, body.gravity)), true, true, out var step)) return false;
                if (step.Wall) { wall = true; return false; }
                position = step.Position;
                if (!step.Ground && !Surface(body, position, null)) return false;
            }
            return true;
        }

        private static bool InPlayableBounds(Player body, Vector2 position)
        {
            if (Terraria.Testing.DebugOptions.noLimits || Main.rightWorld <= Main.leftWorld + 1280) return true;
            return position.X >= Main.leftWorld + 640 && position.X <= Main.rightWorld - 640 - body.width &&
                position.Y >= Main.topWorld + 640 && position.Y <= Main.bottomWorld - 640;
        }

        private static bool DashLane(Player body, int direction)
        {
            Vector2 from = body.position;
            for (int i = 1; i <= 5; i++)
            {
                var next = body.position + new Vector2(direction * 16 * i, 0);
                if (!Swept(body, from, next - from, null) || !Surface(body, next, null)) return false;
                from = next;
            }
            return true;
        }

        private bool ExecuteStep(Player body, CompanionMobilityState state, CompanionMobilitySnapshot mobility,
            RouteMotion motion, RouteStep step, out CompanionMobilityFrame frame, out bool finished)
        {
            frame = new CompanionMobilityFrame { Status = "移动中" };
            finished = false;
            if (age == 0) actionOrigin = progressPosition = body.position;
            age++;
            if (Vector2.DistanceSquared(progressPosition, body.position) > 4)
            { progressPosition = body.position; noProgress = 0; }
            else noProgress++;
            if (age > 150 || noProgress > 24 && !actionStarted && !jumpAccepted) return false;

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
                    case RouteKind.Jump: case RouteKind.RopeExit:
                        if (!jumpStarted)
                        {
                            if (motion != RouteMotion.Ground && motion != RouteMotion.Rope) return false;
                            frame.Status = "跨越障碍";
                            if (!mobility.ReleaseJump && !body.autoJump && motion != RouteMotion.Rope) return true;
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
                        { finished = Math.Abs(vertical) <= 24 && (direction == 0 || along <= 32); return false; }
                        frame.Move = along > 4 && NextClear(body, direction, 0) ? direction : 0;
                        frame.Jump = mobility.JumpTime > 0 && body.velocity.Y * body.gravDir < 0 && NextClear(body, 0, -1);
                        frame.Status = "跳跃中";
                        return true;
                    case RouteKind.Drop: case RouteKind.Descend:
                        if (motion == RouteMotion.Ground && age > 1 && vertical < 6) { finished = true; return false; }
                        if (vertical < -24 || motion == RouteMotion.Hooked || motion == RouteMotion.Track) return false;
                        frame.Drop = step.Kind == RouteKind.Drop && body.position.Y - actionOrigin.Y < 14;
                        frame.Move = along > 4 && NextClear(body, direction, 0) ? direction : 0;
                        frame.Status = "下降中";
                        return true;
                    case RouteKind.Ascend: case RouteKind.AirAcross:
                        if (step.Kind == RouteKind.Ascend ? vertical >= -3 : along <= 5) { finished = true; return false; }
                        if (!AirInput(body, mobility, motion, direction, step.Kind == RouteKind.Ascend,
                            out frame)) return false;
                        return true;
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
            if (step.Kind == RouteKind.Door) frame.Move = step.Direction;
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

        private static bool AirInput(Player body, CompanionMobilitySnapshot mobility, RouteMotion motion,
            int direction, bool rise, out CompanionMobilityFrame frame)
        {
            frame = new CompanionMobilityFrame { Status = "空中移动" };
            int vertical = rise ? -(int)body.gravDir : 0;
            if (motion == RouteMotion.Hooked || motion == RouteMotion.Track || motion == RouteMotion.Rope ||
                !NextClear(body, direction, vertical)) return false;
            bool thrust = mobility.UnlimitedWings || mobility.WingTime > 0 || mobility.RocketAvailable || mobility.MountFly ||
                mobility.MountHover || mobility.CarpetReady || mobility.ExtraJumps != 0 || mobility.Sliding || mobility.JumpTime > 0;
            if (rise && !thrust || !rise && !thrust && motion == RouteMotion.Ground) return false;
            frame.Move = direction;
            if (mobility.WingHover && !rise) frame.Vertical = 1;
            if (mobility.MountHover) frame.Vertical = vertical;
            bool hold = mobility.JumpTime > 0 && body.velocity.Y * body.gravDir < 0;
            bool powered = mobility.WingTime > 0 || mobility.UnlimitedWings || mobility.MountFly || mobility.MountHover ||
                mobility.RocketReady || !rise && mobility.CarpetReady;
            // A new extra/wall jump can only see a native release edge. Never
            // write releaseJump or refill jump resources to make a plan succeed.
            frame.Jump = hold || motion != RouteMotion.Ground && powered && (mobility.ExtraJumps == 0 || mobility.WingTime > 0 || mobility.UnlimitedWings) ||
                mobility.ReleaseJump && (motion == RouteMotion.Ground || mobility.ExtraJumps != 0 || mobility.Sliding || powered);
            // An ordinary takeoff spends the rocket release edge. Give the
            // native air consumer one real neutral update before ignition.
            if (motion == RouteMotion.Air && !powered && mobility.RocketAvailable && !mobility.RocketReady &&
                mobility.ExtraJumps == 0 && !mobility.Sliding) frame.Jump = false;
            return true;
        }

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
                if (motion == RouteMotion.Air && AirInput(body, mobility, motion, direction,
                    (goal.Y - body.position.Y) * body.gravDir < -16, out CompanionMobilityFrame frame)) return frame;
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
