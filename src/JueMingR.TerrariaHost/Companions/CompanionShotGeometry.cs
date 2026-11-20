using System;
using JueMingR.Features.Combat;
using JueMingR.TerrariaHost.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal readonly struct CompanionShotGeometry
    {
        private readonly Item weapon;
        private readonly Projectile sample;
        private readonly AttackMotion motion;
        internal bool KnownFamily => sample != null;
        internal bool SameShot(CompanionShotGeometry other)
        {
            return ReferenceEquals(weapon, other.weapon) && ReferenceEquals(sample, other.sample) && motion.Speed == other.motion.Speed;
        }

        internal CompanionShotGeometry(Player body, Item weapon)
        {
            this.weapon = weapon;
            sample = null;
            motion = default;
            if (weapon == null || weapon.channel || ItemID.Sets.gunProj[weapon.type] ||
                weapon.useAmmo != AmmoID.Arrow && weapon.useAmmo != AmmoID.Bullet ||
                HostSkyAttack.Handles(weapon.type) || HostNativeArrowBirthAttack.Weapon(weapon.type) ||
                body.hasPhoenixQuiver || body.accSeraphNecklace) return;
            var ammo = AttackAmmoSnapshot.Capture(body, weapon);
            if (ammo == null || HostAmmoSteeringAttack.Handles(ammo.Projectile) ||
                HostAmmoImpactAttack.Handles(ammo.Projectile) || HostArrowFamilyAttack.Handles(ammo.Projectile) ||
                HostSpecialArrowAttack.Handles(ammo.Projectile) || !HostAttackModels.TryRead(ammo, out var movement))
                return;
            var projectile = ContentSamples.ProjectilesByType[ammo.Projectile];
            // Ordinary terminating native arrows and bullets have the
            // simple tile motion below. Ricochet, remote/held constructors,
            // compound impacts and homing retain their own solver.
            if (!projectile.tileCollide || projectile.aiStyle != 1 && projectile.aiStyle != 14 ||
                ammo.Projectile == 284) return;
            sample = projectile;
            motion = movement;
        }

        internal HostAttackReachability Opening(Player body, NPC target, PredictionTerrain terrain,
            Vector2 displacement, out Vector2 point)
        {
            var targetPosition = target.position + target.netOffset;
            var hitbox = new Rectangle((int)targetPosition.X, (int)targetPosition.Y, target.width, target.height);
            var center = new Vector2(hitbox.Center.X, hitbox.Center.Y);
            point = target.Center + target.netOffset;
            Vector2 origin = HostAttackModels.LaunchOrigin(body, weapon) + displacement;
            float flight = Vector2.Distance(origin, point) / motion.Speed;
            float falling = Math.Max(0, flight - motion.GravityStart + 1);
            // Seed the common ballistic correction. The real candidate
            // birth and each-sized collision step decide actual admission.
            point.Y -= motion.Gravity * falling * (falling + 1) * .5f;
            origin = HostAttackModels.NativeLaunchOrigin(body, weapon, origin, point, out int direction);
            Vector2 velocity = HostAttackModels.NativeLaunchVelocity(body, origin, point, motion.Speed, direction);
            var model = motion.ForLaunch(velocity.X);
            float vx = velocity.X, vy = velocity.Y;
            model.Launch(ref vx, ref vy);
            var half = new Vector2(sample.width, sample.height) * .5f;
            Vector2 position = origin - half;
            float previousDistance = Vector2.DistanceSquared(origin, center);
            int limit = Math.Min(motion.Lifetime, Math.Min(192, (int)flight + 24));
            for (int step = 1; step <= limit; step++)
            {
                bool wet = false;
                byte liquid = 0;
                if (!sample.ignoreWater && (!terrain.ProjectileWet(position.X + half.X, position.Y + half.Y,
                    sample.width, sample.height, out wet, out liquid, true)))
                    return HostAttackReachability.Unknown;
                float x = 0, y = 0;
                model.Advance(ref x, ref y, ref vx, ref vy, step);
                velocity = new Vector2(vx, vy);
                if (!terrain.ProjectileTileMotion(sample, ref position, ref velocity, wet, liquid == 2,
                    out var wetVelocity, out bool hit, shimmer: liquid == 3))
                    return HostAttackReachability.Unknown;
                if (hit) return HostAttackReachability.Blocked;
                position += wet ? wetVelocity : velocity;
                if (hitbox.Intersects(new Rectangle((int)position.X, (int)position.Y, sample.width, sample.height)))
                    return HostAttackReachability.Reachable;
                float distance = Vector2.DistanceSquared(position + half, center);
                if (distance > previousDistance && step > flight) break;
                previousDistance = distance;
            }
            return HostAttackReachability.Unknown;
        }
    }
}
