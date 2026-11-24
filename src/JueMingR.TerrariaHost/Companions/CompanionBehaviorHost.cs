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
        internal CompanionNavigator Navigator { get; } = new CompanionNavigator();
        private readonly PredictionTerrain sight = new PredictionTerrain();
        private readonly int[] seenThreats = new int[Main.projectile.Length + Main.npc.Length];
        private Vector2 lastAir, anchor;
        private bool hasAir, initialized;
        private Player goalOwner;
        private int threatStamp;
        private NPC openingTarget;
        private Item openingWeapon;
        private int openingGeneration;
        private uint openingUntil, nextTacticalProbe;
        private Vector2 openingPosition, enemyAtOpening;
        private PredictionTerrain.CellLease openingTerrain;
        internal CompanionOwnerTrail OwnerTrail { private get; set; }

        internal void PositionChanged(Player body)
        {
            hasAir = false;
            anchor = body.Center;
            openingTarget = null;
            openingTerrain = null;
            nextTacticalProbe = Main.GameUpdateCount;
            sight.Reset();
            Navigator.ResetPosition();
        }

        internal void RecordMovementDelivery(CompanionMovementDelivery delivery) => Navigator.RecordDelivery(delivery);
        internal void ObserveMovement(Player body, CompanionMobilityState state) => Navigator.Observe(body, state);
        internal void AdvanceMovement(Player body, CompanionMobilityState state, NavigationBudget budget) =>
            Navigator.Advance(body, state, OwnerTrail, budget);

        internal CompanionMobilityFrame DecideMobility(Player body, Player owner, CompanionBehavior behavior, bool allowPlanning,
            NPC combatTarget, bool closeCombat, bool canAttack, Item pursuitWeapon, CompanionMobilityState mobility,
            bool maintainMeleeApproach = false)
        {
            var currentOwner = behavior.Follow && owner != null && owner.active && !owner.dead ? owner : null;
            Navigator.ObserveOwnerTrail(OwnerTrail);
            if (!initialized || !ReferenceEquals(goalOwner, currentOwner))
            {
                initialized = true;
                goalOwner = currentOwner;
                anchor = body.Center;
                openingTarget = null;
                Navigator.IntentChanged();
            }
            Vector2 center = currentOwner?.Center ?? anchor;
            Vector2 follow = center, velocity = currentOwner?.velocity ?? Vector2.Zero;
            float spacing = currentOwner == null ? 0f : 34f;
            bool needsAir = body.wet && body.breath < body.breathMax / 3 && !body.gills && !mobility.Observed.BreathProtected;
            if (!body.wet) { lastAir = body.Center; hasAir = true; }
            bool lava = body.lavaWet && !body.lavaImmune && (!mobility.Observed.Ready || mobility.Observed.LavaTime <= 24);

            if (++threatStamp == int.MaxValue) { Array.Clear(seenThreats, 0, seenThreats.Length); threatStamp = 1; }
            bool threat = CompanionThreatField.Observe(body, seenThreats, threatStamp, out int escape, out bool jumpClear);
            if (needsAir || lava || threat)
            {
                if (needsAir) center = hasAir ? lastAir : body.Center - new Vector2(0, 80f * body.gravDir);
                else center = body.Center + new Vector2(escape * 56f, (lava || jumpClear ? -48f : 0f) * body.gravDir);
                velocity = Vector2.Zero;
                spacing = 0;
            }
            // A close hand may enter legal swing range before its native
            // swing connects. Keep the same bounded approach through that
            // handoff instead of turning back toward the standby anchor.
            else if (mobility.Observed.Ready && (!canAttack || maintainMeleeApproach) && combatTarget != null &&
                TryCombatPosition(body, follow, combatTarget, pursuitWeapon, closeCombat, allowPlanning, out var opening))
            {
                center = opening;
                spacing = closeCombat ? 10f : 4f;
                velocity = Vector2.Zero;
            }
            else if (canAttack || combatTarget == null) openingTarget = null;

            Vector2 goal = center + new Vector2(0, body.height * .5f * body.gravDir);
            bool followInAir = false;
            if (currentOwner != null && center == follow && body.gravDir == currentOwner.gravDir)
            {
                // Foot contact, not differences in armor/mount sprite sizes,
                // defines the shared target floor. The owner's trail can
                // identify a recently supported target without claiming AI reach.
                if (OwnerTrail?.RecentlyGrounded == true)
                    goal = CompanionNativeContact.Feet(currentOwner);
                else followInAir = OwnerTrail != null && !currentOwner.wet && !currentOwner.pulley;
            }
            return Navigator.Decide(body, mobility, goal, velocity, spacing, needsAir || lava || threat, escape, followInAir);
        }

        private bool TryCombatPosition(Player body, Vector2 follow, NPC target, Item weapon, bool closeCombat,
            bool allowSightProbe, out Vector2 position)
        {
            const int leash = 320;
            position = body.Center;
            if (Vector2.DistanceSquared(position, follow) > leash * leash) return false;
            Vector2 enemy = target.Center + target.netOffset;
            if (Vector2.DistanceSquared(enemy, position) > leash * leash) return false;
            if (closeCombat && Vector2.DistanceSquared(enemy, follow) <= leash * leash)
            {
                position = enemy;
                return true;
            }
            if (ReferenceEquals(openingTarget, target) && ReferenceEquals(openingWeapon, weapon) &&
                openingGeneration == target.generation && Main.GameUpdateCount < openingUntil &&
                Vector2.DistanceSquared(enemy, enemyAtOpening) < 24f * 24f &&
                Vector2.DistanceSquared(openingPosition, follow) <= leash * leash && openingTerrain.Current)
            {
                position = openingPosition;
                return true;
            }
            openingTarget = null;
            if (!allowSightProbe || unchecked((int)(Main.GameUpdateCount - nextTacticalProbe)) < 0) return false;
            nextTacticalProbe = Main.GameUpdateCount + 6;
            int toward = Math.Sign(enemy.X - position.X);
            if (toward == 0) toward = body.direction;
            var geometry = new CompanionShotGeometry(body, weapon);
            for (int candidate = 0; candidate < 3; candidate++)
            {
                var offset = candidate == 2 ? new Vector2(toward * 64, -52) :
                    new Vector2((candidate == 0 ? toward : -toward) * 96, 0);
                var destination = body.Center + offset;
                if (Vector2.DistanceSquared(destination, follow) > leash * leash ||
                    !LocalOpening(body, target, geometry, destination, enemy)) continue;
                openingTarget = target;
                openingWeapon = weapon;
                openingGeneration = target.generation;
                enemyAtOpening = enemy;
                position = openingPosition = destination;
                openingUntil = Main.GameUpdateCount + 45;
                openingTerrain = sight.FreezeCells();
                return true;
            }
            return false;
        }

        private bool LocalOpening(Player body, NPC target, CompanionShotGeometry geometry, Vector2 destination, Vector2 enemy)
        {
            sight.Reset();
            if (!geometry.KnownFamily) return HostAttackAim.ActorDirectLine(sight, destination, enemy);
            return geometry.Opening(body, target, sight, destination - body.Center, out _) == HostAttackReachability.Reachable;
        }
    }
}
