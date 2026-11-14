using System;
using JueMingR.Features.Companions;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed class CompanionBehaviorHost
    {
        private readonly CompanionBehaviorBrain brain = new CompanionBehaviorBrain();
        private readonly CompanionNavigation navigation = new CompanionNavigation();
        private bool wasFollowing = true, hasStand;
        private Vector2 stand, lastAir, ownerRest, progress;
        private bool hasAir, hadOwner;
        private int stalled, threatStamp;
        private readonly int[] seenThreats = new int[Main.projectile.Length + Main.npc.Length];
        internal void ObserveCapabilities(Player body) { navigation.ObserveCapabilities(body); }
        internal CompanionControlFrame Decide(Player body, Player owner, CompanionBehavior behavior, bool allowPlanning)
        {
            if (!hasStand || wasFollowing && !behavior.Follow) { stand = body.Center; hasStand = true; }
            wasFollowing = behavior.Follow;
            bool ownerAvailable = owner != null && owner.active && !owner.dead;
            if (!ownerAvailable && hadOwner) ownerRest = body.Center;
            if (!hadOwner && !ownerAvailable && ownerRest == Vector2.Zero) ownerRest = body.Center;
            hadOwner = ownerAvailable;
            Vector2 target = behavior.Follow ? (ownerAvailable ? owner.Center : ownerRest) : stand;
            bool needsAir = body.wet && body.breath < body.breathMax / 3 && !body.gills;
            if (!body.wet) { lastAir = body.Center; hasAir = true; }
            if (needsAir && hasAir) target = lastAir;
            int direction = Math.Abs(target.X - body.Center.X) > 40 ? Math.Sign(target.X - body.Center.X) : 0;
            bool grounded = Math.Abs(body.velocity.Y) < 0.05f && CompanionNavigation.Supported(body, body.position);
            bool lavaDanger = body.lavaWet && !body.lavaImmune;
            int threatEscape; bool jumpClear;
            if (++threatStamp == Int32.MaxValue) { Array.Clear(seenThreats, 0, seenThreats.Length); threatStamp = 1; }
            bool danger = CompanionThreatField.Observe(body, seenThreats, threatStamp, out threatEscape, out jumpClear) || lavaDanger;
            bool walk = CompanionNavigation.WalkSafe(body, direction);
            bool left = direction < 0 && walk, right = direction > 0 && walk;
            if (danger || needsAir) { left = CompanionNavigation.WalkSafe(body, -1); right = CompanionNavigation.WalkSafe(body, 1); }
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
                Grounded = grounded, Controlled = body.CCed, Dead = body.dead || !body.active,
                WalkSafe = walk, Wall = wall,
                JumpSafe = grounded && (lavaDanger || danger && jumpClear || planNeeded && navigation.JumpSafe(body, target, direction, allowPlanning)), DropSafe = grounded && CompanionNavigation.DropSafe(body),
                Danger = danger, NeedsAir = needsAir, EscapeLeft = left, EscapeRight = right, EscapeDirection = escape,
                JumpHoldTicks = navigation.JumpHold,
                CanHeal = !body.dead && !body.CCed && body.potionDelay <= 0 && body.statLife * 5 < body.statLifeMax2 * 3 && body.QuickHeal_GetItemToUse() != null
            };
            return brain.Decide(observation);
        }
    }
}
