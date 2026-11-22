using System;
using JueMingR.Features.Companions;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed class CompanionBehaviorHost
    {
        private readonly CompanionBehaviorBrain brain = new CompanionBehaviorBrain();
        private readonly CompanionNavigation navigation = new CompanionNavigation();
        private readonly PredictionTerrain sight = new PredictionTerrain();
        private bool wasFollowing = true, hasStand;
        private Vector2 stand, lastAir, ownerRest, progress;
        private bool hasAir, hadOwner;
        private Player planningOwner;
        private int stalled, threatStamp;
        private NPC lateralTarget;
        private Item lateralWeapon;
        private int lateralGeneration;
        private uint lateralUntil;
        private Vector2 lateralGoal, lateralEnemy;
        private bool lateralJump;
        private PredictionTerrain.CellLease lateralTerrain;
        private readonly int[] seenThreats = new int[Main.projectile.Length + Main.npc.Length];
        internal void ObserveCapabilities(Player body) { navigation.ObserveCapabilities(body); }
        internal CompanionControlFrame Decide(Player body, Player owner, CompanionBehavior behavior, bool allowPlanning)
        { return DecideMovement(body, owner, behavior, allowPlanning, null, false, false, null); }

        internal CompanionControlFrame DecideMovement(Player body, Player owner, CompanionBehavior behavior, bool allowPlanning,
            NPC combatTarget, bool closeCombat, bool canAttack, Item pursuitWeapon)
        {
            var frame = DecideMobility(body, owner, behavior, allowPlanning, combatTarget, closeCombat, canAttack, pursuitWeapon, null);
            return new CompanionControlFrame { Move = frame.Move, Jump = frame.Jump, Drop = frame.Drop, Status = frame.Status };
        }

        internal CompanionMobilityFrame DecideMobility(Player body, Player owner, CompanionBehavior behavior, bool allowPlanning,
            NPC combatTarget, bool closeCombat, bool canAttack, Item pursuitWeapon, CompanionMobilityState mobility)
        {
            bool anchorChanged = hasStand && wasFollowing != behavior.Follow;
            if (!hasStand || wasFollowing && !behavior.Follow) { stand = body.Center; hasStand = true; }
            wasFollowing = behavior.Follow;
            bool ownerAvailable = owner != null && owner.active && !owner.dead;
            bool ownerChanged = hadOwner != ownerAvailable || ownerAvailable && !ReferenceEquals(planningOwner, owner);
            planningOwner = ownerAvailable ? owner : null;
            if (!ownerAvailable && hadOwner) ownerRest = body.Center;
            if (!hadOwner && !ownerAvailable && ownerRest == Vector2.Zero) ownerRest = body.Center;
            hadOwner = ownerAvailable;
            Vector2 target = behavior.Follow ? (ownerAvailable ? owner.Center : ownerRest) : stand;
            // Combat requests are local offsets from this frame's follow/stand
            // anchor. A distant owner always retains the long-range route.
            bool needsAir = body.wet && body.breath < body.breathMax / 3 && !body.gills &&
                !(mobility != null && mobility.Observed.BreathProtected);
            if (!body.wet) { lastAir = body.Center; hasAir = true; }
            if (needsAir && hasAir) target = lastAir;
            // A slope contact is geometric; descending native slope motion can
            // retain Y velocity. Never retire a jump from zero velocity alone.
            // Native stairFall outlives the down edge while passing a platform;
            // it is not a fresh support/drop-planning opportunity next frame.
            bool grounded = body.velocity.Y >= 0 && (!body.wet || needsAir) &&
                CompanionNavigation.Supported(body, body.position, !body.controlDown && !body.stairFall);
            // Supported at +1px is a geometry observation. JumpMovement runs
            // before collision and still requires native zero Y (or sliding)
            // and a previously consumed release, unless native auto-jump applies.
            bool canTakeOff = grounded && (body.sliding || body.velocity.Y == 0) &&
                (body.releaseJump || body.autoJump && !body.sliding);
            bool canSwimUp = needsAir && (body.accFlipper || body.merman) &&
                (!body.mount.Active || !body.mount.Cart) && (body.releaseJump || body.merman);
            bool lavaDanger = body.lavaWet && !body.lavaImmune &&
                (mobility == null || !mobility.Observed.Ready || mobility.Observed.LavaTime <= 24);
            int threatEscape; bool jumpClear;
            if (++threatStamp == Int32.MaxValue) { Array.Clear(seenThreats, 0, seenThreats.Length); threatStamp = 1; }
            bool danger = CompanionThreatField.Observe(body, seenThreats, threatStamp, out threatEscape, out jumpClear) || lavaDanger;
            Vector2 combatPosition = target;
            bool reposition = !danger && !needsAir && !canAttack && combatTarget != null &&
                TryCombatPosition(body, target, combatTarget, pursuitWeapon, closeCombat, allowPlanning, out combatPosition);
            if (reposition) target = combatPosition;
            if (danger || needsAir || canAttack || combatTarget == null) lateralTarget = null;
            int stopDistance = reposition ? 10 : 32, resumeDistance = reposition ? 18 : 56;
            if (mobility != null && mobility.Observed.Ready)
            {
                if (anchorChanged || behavior.Follow && ownerChanged) navigation.CancelRoute();
                grounded = grounded || CompanionNavigation.HasMobilitySupport(body, mobility.Observed);
                if (danger || needsAir)
                    return navigation.Escape(body, mobility, target, grounded, needsAir, lavaDanger, threatEscape, jumpClear);
                if (!grounded && navigation.TryFallRescue(body, mobility, out var rescue)) return rescue;
                if (reposition && grounded && !body.CCed)
                {
                    int toward = Math.Sign(target.X - body.Center.X);
                    // A retained firing lane can still have a newly imminent
                    // obstacle on approach. Preserve the existing narrow leap
                    // admission as soon as the real body meets that obstacle.
                    bool leap = lateralJump || allowPlanning && CompanionNavigation.Wall(body, toward) &&
                        navigation.JumpSafe(body, target, toward, true);
                    if (leap && navigation.TryCombatJump(body, mobility, target, out var jump)) return jump;
                }
                return navigation.FollowRoute(body, mobility, target, allowPlanning, grounded, stopDistance, resumeDistance);
            }
            int direction = brain.DirectionTo(target.X - body.Center.X, stopDistance, resumeDistance);
            var route = navigation.PlanWalk(body, direction, allowPlanning);
            bool walk = route == CompanionNavigation.WalkOutcome.Clear || route == CompanionNavigation.WalkOutcome.Wall;
            bool left = direction < 0 && walk, right = direction > 0 && walk;
            if (danger || needsAir)
            {
                var leftRoute = navigation.PlanWalk(body, -1, allowPlanning);var rightRoute = navigation.PlanWalk(body, 1, allowPlanning);
                left = leftRoute == CompanionNavigation.WalkOutcome.Clear || leftRoute == CompanionNavigation.WalkOutcome.Wall;
                right = rightRoute == CompanionNavigation.WalkOutcome.Clear || rightRoute == CompanionNavigation.WalkOutcome.Wall;
            }
            int escape = direction;
            if (lavaDanger)
            {
                bool safeLeft = CompanionNavigation.SafeBody(body, body.position + new Vector2(-32, 0));
                bool safeRight = CompanionNavigation.SafeBody(body, body.position + new Vector2(32, 0));
                escape = safeLeft && left ? -1 : safeRight && right ? 1 : direction != 0 ? direction : body.direction;
            }
            else if (danger) escape = threatEscape;
            if (danger && !lavaDanger)
            {
                if (escape < 0 && !left) escape = right ? 1 : 0;
                else if (escape > 0 && !right) escape = left ? -1 : 0;
            }
            bool wall = CompanionNavigation.Wall(body, direction);
            if (Vector2.DistanceSquared(progress, body.position) > 16) { progress = body.position; stalled = 0; }
            else if (!body.CCed && grounded && direction != 0) stalled++;
            bool planNeeded = !walk || wall || target.Y < body.Center.Y - 36 || stalled >= 24;
            var observation = new CompanionMovementObservation
            {
                X = body.Center.X, Y = body.Center.Y, TargetX = target.X, TargetY = target.Y,
                VelocityX = body.velocity.X, VelocityY = body.velocity.Y,
                Grounded = grounded, CanTakeOff = canTakeOff, Controlled = body.CCed, Dead = body.dead || !body.active,
                WalkSafe = walk, Wall = wall,
                JumpSafe = canTakeOff && (lavaDanger || danger && jumpClear ||
                    needsAir && CompanionNavigation.SafeBody(body, body.position + new Vector2(0, -8 * body.gravDir)) ||
                    planNeeded && navigation.JumpSafe(body, target, direction, allowPlanning)),
                DropSafe = grounded && target.Y > body.Center.Y + 56 && navigation.PlanDrop(body, allowPlanning),
                Danger = danger, NeedsAir = needsAir, CanSwimUp = canSwimUp, EscapeLeft = left, EscapeRight = right, EscapeDirection = escape,
                JumpHoldTicks = navigation.JumpHold, StopDistance = stopDistance, ResumeDistance = resumeDistance
            };
            var frame = brain.Decide(observation);
            bool unknown = !CompanionNavigation.CurrentKnown(body) ||
                (frame.Move == 0 || frame.Move == direction ? route == CompanionNavigation.WalkOutcome.Unknown :
                    navigation.PlanWalk(body, frame.Move, allowPlanning) == CompanionNavigation.WalkOutcome.Unknown);
            // Unknown ordinary geometry is not an airborne route grant. Only
            // this movement intent waits; the body's one native update, healing
            // and independent attack channel continue under their own gates.
            if (unknown) { frame.Move = 0; frame.Jump = frame.Drop = false; frame.Status = "等待地形就绪"; }
            return new CompanionMobilityFrame { Move = frame.Move, Jump = frame.Jump, Drop = frame.Drop, Status = frame.Status };
        }

        private bool TryCombatPosition(Player body, Vector2 anchor, NPC target, Item weapon, bool closeCombat, bool allowPlanning, out Vector2 position)
        {
            const int leash = 320;
            position = body.Center;
            if (Vector2.DistanceSquared(position, anchor) > leash * leash) return false;
            Vector2 enemy = target.Center + target.netOffset;
            if (Vector2.DistanceSquared(enemy, position) > leash * leash) return false;
            int toward = Math.Sign(enemy.X - position.X);
            if (closeCombat && Vector2.DistanceSquared(enemy, anchor) <= leash * leash)
            {
                var walk = navigation.PlanWalk(body, toward, allowPlanning);
                if (walk == CompanionNavigation.WalkOutcome.Clear || walk == CompanionNavigation.WalkOutcome.Wall)
                { position = enemy; lateralJump = false; return true; }
            }
            // An admitted local goal can survive another actor's planning
            // slice. Its real walk/jump admission is still checked each frame.
            if (ReferenceEquals(lateralTarget, target) && ReferenceEquals(lateralWeapon, weapon) && lateralGeneration == target.generation &&
                Main.GameUpdateCount < lateralUntil && Vector2.DistanceSquared(enemy, lateralEnemy) < 24 * 24 &&
                Vector2.DistanceSquared(lateralGoal, anchor) <= leash * leash && lateralTerrain.Current &&
                (Math.Abs(lateralGoal.X - body.Center.X) > 10 || lateralGoal.Y < body.Center.Y - 36))
            { position = lateralGoal; return true; }
            lateralTarget = null;
            if (!allowPlanning) return false;
            // Two local sight probes spend this actor's normal planning slice.
            // Movement still goes through the same walking/jump safety checks.
            if (toward == 0) toward = body.direction;
            var geometry = new CompanionShotGeometry(body, weapon);
            for (int side = toward; side != 0; side = side == toward ? -toward : 0)
            {
                Vector2 offset = new Vector2(side * 96, 0);
                var destination = body.Center + offset;
                if (Vector2.DistanceSquared(destination, anchor) > leash * leash) continue;
                var route = navigation.PlanWalk(body, side, allowPlanning);
                if (route == CompanionNavigation.WalkOutcome.Clear && LocalOpening(body, target, geometry, destination, enemy))
                { position = RetainCombatPosition(target, weapon, enemy, destination); return true; }
            }
            var high = body.Center + new Vector2(toward * 64, -52);
            if (Vector2.DistanceSquared(high, anchor) <= leash * leash && LocalOpening(body, target, geometry, high, enemy) &&
                navigation.JumpSafe(body, high, toward, true))
            { position = RetainCombatPosition(target, weapon, enemy, high, jump: true); return true; }
            return false;
        }

        private bool LocalOpening(Player body, NPC target, CompanionShotGeometry geometry, Vector2 destination, Vector2 enemy)
        {
            if (!geometry.KnownFamily) return HostAttackAim.ActorDirectLine(sight, destination, enemy);
            sight.Reset();
            return geometry.Opening(body, target, sight, destination - body.Center, out _) == HostAttackReachability.Reachable;
        }

        private Vector2 RetainCombatPosition(NPC target, Item weapon, Vector2 enemy, Vector2 destination, bool jump = false)
        {
            lateralTarget = target; lateralWeapon = weapon; lateralGeneration = target.generation; lateralEnemy = enemy;
            lateralGoal = destination; lateralJump = jump; lateralUntil = Main.GameUpdateCount + 45;
            lateralTerrain = sight.FreezeCells();
            return destination;
        }
    }
}
