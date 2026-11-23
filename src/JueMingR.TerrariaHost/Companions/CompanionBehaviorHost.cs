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
        private readonly CompanionNavigation navigation = new CompanionNavigation();
        private readonly PredictionTerrain sight = new PredictionTerrain();
        private Vector2 lastAir;
        private bool hasAir;
        private int threatStamp;
        private NPC lateralTarget;
        private Item lateralWeapon;
        private int lateralGeneration;
        private uint lateralUntil, nextTacticalProbe;
        private Vector2 lateralGoal, lateralEnemy;
        private bool lateralJump;
        private PredictionTerrain.CellLease lateralTerrain;
        private readonly int[] seenThreats = new int[Main.projectile.Length + Main.npc.Length];
        internal CompanionOwnerTrail OwnerTrail { private get; set; }

        internal void PositionChanged(Player body)
        {
            hasAir = false;
            lateralTarget = null;
            lateralTerrain = null;
            lateralJump = false;
            nextTacticalProbe = Main.GameUpdateCount;
            sight.Reset();
            navigation.RetirePosition(body);
        }
        internal void ObserveCapabilities(Player body) { navigation.ObserveCapabilities(body); }
        internal void RecordMovementDelivery(CompanionMovementDelivery delivery) => navigation.RecordMovementDelivery(delivery);
        internal void ObserveMovement(Player body, CompanionMobilityState state) => navigation.ObserveMovement(body, state);
        internal CompanionMobilityFrame DecideMobility(Player body, Player owner, CompanionBehavior behavior, bool allowPlanning,
            NPC combatTarget, bool closeCombat, bool canAttack, Item pursuitWeapon, CompanionMobilityState mobility)
        {
            var goal = navigation.ObserveGoal(body, owner, behavior.Follow, OwnerTrail);
            Vector2 target = goal.Position;
            // Combat requests are local offsets from this frame's follow/stand
            // anchor. A distant owner always retains the long-range route.
            bool needsAir = body.wet && body.breath < body.breathMax / 3 && !body.gills && !mobility.Observed.BreathProtected;
            if (!body.wet) { lastAir = body.Center; hasAir = true; }
            if (needsAir && hasAir) target = lastAir;
            bool grounded = (!body.wet || needsAir || CompanionNavigation.CanObserveWetSupport(body, mobility.Observed)) &&
                CompanionNavigation.ObservedSupport(body);
            bool lavaDanger = body.lavaWet && !body.lavaImmune && (!mobility.Observed.Ready || mobility.Observed.LavaTime <= 24);
            if (++threatStamp == Int32.MaxValue) { Array.Clear(seenThreats, 0, seenThreats.Length); threatStamp = 1; }
            bool danger = CompanionThreatField.Observe(body, seenThreats, threatStamp, out int threatEscape, out bool jumpClear) || lavaDanger;

            bool reposition = false, leap = false;
            // A combat probe consumes the same planning opportunity this
            // body would use for a route. An already executed prefix can
            // still be controlled while that bounded local probe runs.
            if (mobility.Observed.Ready && navigation.AcceptsCombatIntent && !danger && !needsAir && !canAttack && combatTarget != null)
            {
                reposition = TryCombatPosition(body, target, combatTarget, pursuitWeapon, closeCombat, ref allowPlanning, out var opening);
                if (reposition)
                {
                    target = opening;
                    int toward = Math.Sign(target.X - body.Center.X);
                    leap = lateralJump;
                    if (!leap && allowPlanning && CompanionNavigation.Wall(body, toward))
                    {
                        allowPlanning = false;
                        leap = navigation.JumpSafe(body, target, toward, true);
                    }
                }
            }
            else lateralTarget = null;

            return navigation.Resolve(body, mobility, goal, grounded, allowPlanning, target, danger, needsAir, lavaDanger,
                threatEscape, jumpClear, reposition, leap);
        }

        private bool TryCombatPosition(Player body, Vector2 anchor, NPC target, Item weapon, bool closeCombat, ref bool allowPlanning, out Vector2 position)
        {
            const int leash = 320;
            position = body.Center;
            if (Vector2.DistanceSquared(position, anchor) > leash * leash) return false;
            Vector2 enemy = target.Center + target.netOffset;
            if (Vector2.DistanceSquared(enemy, position) > leash * leash) return false;
            int toward = Math.Sign(enemy.X - position.X);
            if (closeCombat && Vector2.DistanceSquared(enemy, anchor) <= leash * leash)
            {
                var walk = navigation.PlanWalk(body, toward, false);
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
            if (!allowPlanning || unchecked((int)(Main.GameUpdateCount - nextTacticalProbe)) < 0) return false;
            allowPlanning = false;
            // No opening can leave the same NPC in pursuit for a long time.
            // Give long-range planning real turns between local attempts.
            nextTacticalProbe = Main.GameUpdateCount + 6;
            // Two local sight probes spend this actor's normal planning slice.
            // Movement still goes through the same walking/jump safety checks.
            if (toward == 0) toward = body.direction;
            var geometry = new CompanionShotGeometry(body, weapon);
            for (int side = toward; side != 0; side = side == toward ? -toward : 0)
            {
                Vector2 offset = new Vector2(side * 96, 0);
                var destination = body.Center + offset;
                if (Vector2.DistanceSquared(destination, anchor) > leash * leash) continue;
                var route = navigation.PlanWalk(body, side, false);
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
