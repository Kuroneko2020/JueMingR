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
        private int openingGeneration, openingSegment;
        private uint openingUntil, nextTacticalProbe;
        private Vector2 openingPosition, enemyAtOpening;
        private PredictionTerrain.CellLease openingTerrain;
        internal CompanionOwnerTrail OwnerTrail { private get; set; }
        internal HostCompanions.TaskTravel TaskTravel { private get; set; }
        internal HostCompanions.TaskCombat TaskCombat { private get; set; }
        internal bool? EffectiveFollow { private get; set; }

        internal void ResumeStandbyAt(Player body) => anchor=body.Center;

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

        private int NextThreatStamp()
        {
            if (++threatStamp == int.MaxValue) { Array.Clear(seenThreats, 0, seenThreats.Length); threatStamp = 1; }
            return threatStamp;
        }

        internal void RefreshFishingThreats(Player body, FishingThreats threats) =>
            CompanionThreatField.ObserveFishing(body, seenThreats, NextThreatStamp(), threats);

        internal CompanionMobilityFrame DecideMobility(Player body, Player owner, CompanionBehavior behavior, bool allowPlanning,
            NPC combatTarget, bool closeCombat, bool canAttack, Item pursuitWeapon, CompanionMobilityState mobility,
            bool maintainMeleeApproach = false, HostCompanions.FishingActivity fishing = null)
        {
            var currentOwner = fishing == null && (EffectiveFollow ?? (TaskTravel?.FollowOwner ?? behavior.Follow)) &&
                owner != null && owner.active && !owner.dead ? owner : null;
            bool segmentChanged = Navigator.ObserveOwnerTrail(OwnerTrail, mobility);
            if (!initialized || !ReferenceEquals(goalOwner, currentOwner))
            {
                initialized = true;
                goalOwner = currentOwner;
                anchor = body.Center;
                openingTarget = null;
                if (!segmentChanged) Navigator.IntentChanged(mobility);
            }
            if (segmentChanged)
            {
                openingTarget = null;
                openingTerrain = null;
                nextTacticalProbe = Main.GameUpdateCount;
            }
            CompanionOwnerTrail.Sample pose = default;
            bool hasPose = currentOwner != null && OwnerTrail?.TryGetCurrent(out pose) == true;
            Vector2 center = hasPose ? pose.Center : currentOwner?.Center ?? anchor;
            if (fishing != null) center = fishing.FeetGoal - new Vector2(0, body.height * .5f * body.gravDir);
            else if (TaskTravel != null && !TaskTravel.FollowOwner)
                center = TaskTravel.Feet - new Vector2(0, body.height * .5f * body.gravDir);
            Vector2 follow = center, velocity = hasPose ? pose.Velocity : currentOwner?.velocity ?? Vector2.Zero;
            // A fixed task is reached on its selected floor within one body
            // width. Requiring a pixel-perfect stop disagrees with the native
            // ground action's braking and can replan around a stationary body.
            float spacing = currentOwner != null ? 34f : fishing != null ? 8f : TaskTravel != null ? body.width : 0f;
            bool needsAir = body.wet && body.breath < body.breathMax / 3 && !body.gills && !mobility.Observed.BreathProtected;
            if (!body.wet) { lastAir = body.Center; hasAir = true; }
            bool lava = body.lavaWet && !body.lavaImmune && (!mobility.Observed.Ready || mobility.Observed.LavaTime <= 24);

            var fishingThreats = fishing?.Threats;
            int escape;
            bool jumpClear, threat;
            if (fishingThreats == null)
                threat = CompanionThreatField.Observe(body, seenThreats, NextThreatStamp(), out escape, out jumpClear);
            else
            {
                threat = fishingThreats.Danger;
                escape = fishingThreats.Escape;
                jumpClear = fishingThreats.JumpClear;
            }
            bool emergency = needsAir || lava || threat;
            bool temporaryGoal = emergency;
            if (emergency)
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
                temporaryGoal = true;
            }
            else if (canAttack || combatTarget == null) openingTarget = null;

            // A settled fishing pose is ordinary native physics without
            // navigation's small correction inputs. Dangers still get their
            // usual temporary goal, and the fixed anchor remains intact.
            if (fishing?.Hold == true && !temporaryGoal)
                return new CompanionMobilityFrame { Status = "停留" };

            Vector2 goal = center + new Vector2(0, body.height * .5f * body.gravDir);
            bool followInAir = false, targetContactKnown = true;
            if (currentOwner != null && center == follow)
            {
                const CompanionOwnerTrail.Contact nativeMedium = CompanionOwnerTrail.Contact.Wet |
                    CompanionOwnerTrail.Contact.Rope | CompanionOwnerTrail.Contact.Rail;
                bool mediumObserved = (pose.Mode & nativeMedium) != 0;
                targetContactKnown = hasPose && (pose.SupportKnown || mediumObserved);
                // Foot contact, not differences in armor/mount sprite sizes,
                // defines the shared target floor. History gaps alone cannot
                // prove the current owner is airborne.
                if (hasPose && body.gravDir == pose.Gravity)
                {
                    if ((pose.Mode & CompanionOwnerTrail.Contact.Supported) != 0) goal = pose.Feet;
                    else followInAir = pose.SupportKnown && !mediumObserved;
                }
            }
            var frame = Navigator.Decide(body, mobility, goal, velocity, spacing, emergency, escape, followInAir, targetContactKnown);
            frame.TemporaryGoal = temporaryGoal;
            return frame;
        }

        private bool TryCombatPosition(Player body, Vector2 follow, NPC target, Item weapon, bool closeCombat,
            bool allowSightProbe, out Vector2 position)
        {
            float leash = TaskCombat?.Radius ?? 320f;
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
                openingSegment == (OwnerTrail?.Segment ?? 0) &&
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
                openingSegment = OwnerTrail?.Segment ?? 0;
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
