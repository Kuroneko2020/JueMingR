using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed class FishingThreats
    {
        private readonly List<NPC> enemies = new List<NPC>(4);
        internal int Count => enemies.Count;
        internal NPC this[int index] => enemies[index];
        internal bool Danger, JumpClear;
        internal int Escape;

        internal bool Contains(NPC enemy, int generation) => enemy != null && enemy.generation == generation && enemies.Contains(enemy);
        internal void Clear() { enemies.Clear(); Danger = JumpClear = false; Escape = 0; }
        internal void Add(NPC enemy)
        { if (enemy != null && !enemies.Contains(enemy)) enemies.Add(enemy); }
    }

    internal static partial class CompanionThreatField
    {
        // The projectile key identifies its network birth, not an NPC. Only
        // a locally observed native parent can license retaliation against it.
        private struct ShotSource
        {
            internal Projectile Shot;
            internal NPC Enemy;
            internal object World;
            internal int ShotKey, EnemyGeneration;
        }

        private static ShotSource[] shotSources;

        internal static void RecordShotSource(Projectile shot, IEntitySource source)
        {
            if (shot == null || !shot.active || !shot.hostile || shot.damage <= 0 || !(source is EntitySource_Parent parent)) return;
            NPC enemy = parent.Entity as NPC;
            if (enemy == null && parent.Entity is Projectile previous) enemy = ShotEnemy(previous);
            if (enemy == null || !LiveEnemy(enemy, enemy.generation) || !IndexValid(shot.whoAmI, Main.projectile.Length)) return;
            if (shotSources == null) shotSources = new ShotSource[Main.projectile.Length];
            shotSources[shot.whoAmI] = new ShotSource { Shot = shot, ShotKey = shot.key, Enemy = enemy,
                EnemyGeneration = enemy.generation, World = Main.ActiveWorldFileData };
        }

        private static NPC ShotEnemy(Projectile shot)
        {
            if (shotSources == null || !IndexValid(shot.whoAmI, shotSources.Length)) return null;
            var source = shotSources[shot.whoAmI];
            return ReferenceEquals(source.Shot, shot) && source.ShotKey == (int)shot.key && ReferenceEquals(source.World, Main.ActiveWorldFileData) &&
                LiveEnemy(source.Enemy, source.EnemyGeneration) ? source.Enemy : null;
        }

        private static bool IndexValid(int index, int capacity) => index >= 0 && index < capacity;

        private static bool LiveEnemy(NPC npc, int generation) => npc != null && npc.active && npc.generation == generation &&
            IndexValid(npc.whoAmI, Main.npc.Length) && ReferenceEquals(npc, Main.npc[npc.whoAmI]) && !npc.friendly && !npc.townNPC;

        internal static void ObserveFishing(Player body, int[] seen, int stamp, FishingThreats result)
        {
            Ensure();
            result.Clear();
            if (body.creativeGodMode) return;
            var box = body.Hitbox;
            var left = box; left.Offset(-32, 0);
            var right = box; right.Offset(32, 0);
            var up = box; up.Offset(0, body.gravDir < 0 ? 48 : -48);
            // Some actual combat frames extend past an NPC's movement box.
            var region = box; region.Inflate(160, 160);
            int current = 0, leftRisk = 0, rightRisk = 0, jumpRisk = 0;
            float push = 0;
            using (CompanionCollisionScope.Save())
            {
                for (int x = region.Left / Cell; x <= region.Right / Cell; x++)
                    for (int y = region.Top / Cell; y <= region.Bottom / Cell; y++)
                        if (bins.TryGetValue(Key(x, y), out var list))
                            foreach (int index in list) ScoreThreat(index);
                foreach (int index in large) ScoreThreat(index);
            }
            result.Danger = current != 0;
            result.Escape = leftRisk < rightRisk ? -1 : rightRisk < leftRisk ? 1 : Math.Sign(push);
            result.JumpClear = jumpRisk < current;

            void ScoreThreat(int index)
            {
                if (seen[index] == stamp) return;
                seen[index] = stamp;
                var sample = threats[index];
                var bounds = sample.Bounds;
                if (sample.Enemy != null) bounds.Offset((int)sample.Enemy.netOffset.X, (int)sample.Enemy.netOffset.Y);
                if (!bounds.Intersects(region) || sample.Enemy != null &&
                    IndexValid(sample.Enemy.type, body.npcTypeNoAggro.Length) && body.npcTypeNoAggro[sample.Enemy.type]) return;
                int hits = FishingContact(sample, body, box, left, right, up, out NPC enemy);
                if ((hits & 1) != 0)
                {
                    current++;
                    push += body.Center.X - sample.Center.X;
                    result.Add(enemy);
                }
                if ((hits & 2) != 0) leftRisk++;
                if ((hits & 4) != 0) rightRisk++;
                if ((hits & 8) != 0) jumpRisk++;
            }
        }

        private static int FishingContact(Threat sample, Player receiver, Rectangle body, Rectangle left, Rectangle right, Rectangle up, out NPC attacker)
        {
            attacker = null;
            Rectangle area;
            Vector2 position, motion;
            int width, height;
            bool shot = sample.Shot != null;
            bool terrain;
            if (shot)
            {
                var projectile = sample.Shot;
                if (!projectile.active || !projectile.hostile || projectile.damage <= 0 || sample.Identity != (int)projectile.key ||
                    !IndexValid(projectile.whoAmI, Main.projectile.Length) || !ReferenceEquals(Main.projectile[projectile.whoAmI], projectile) ||
                    !ProjectileThreatensPlayer(projectile, receiver)) return 0;
                area = sample.Contact;
                projectile.GetCollisionParams(out var anchor, out width, out height);
                position = projectile.Center - new Vector2(width, height) * anchor;
                motion = projectile.velocity * 10;
                terrain = projectile.tileCollide;
                attacker = ShotEnemy(projectile);
            }
            else
            {
                var enemy = sample.Enemy;
                if (!LiveEnemy(enemy, sample.Identity) || enemy.damage <= 0 ||
                    enemy.type == NPCID.HallowBoss && (enemy.ai[0] == 0f || enemy.ai[0] == 10f)) return 0;
                position = enemy.position + enemy.netOffset;
                width = enemy.width; height = enemy.height;
                motion = enemy.velocity * 8;
                terrain = !enemy.noTileCollide;
                attacker = enemy;
                area = NpcDamageArea(enemy, Rectangle.Union(Rectangle.Union(body, up), Rectangle.Union(left, right)), motion);
            }

            if (area.Width <= 0 || area.Height <= 0) return 0;
            int hit = HitRegions(area, body, left, right, up);
            float length = motion.Length();
            if (float.IsNaN(length) || float.IsInfinity(length) || length < .01f || !IndexValid(width - 1, 256) || !IndexValid(height - 1, 256)) return hit;
            if (length > 192) { motion *= 192 / length; length = 192; }
            var projected = area;
            projected.Offset((int)motion.X, (int)motion.Y);
            var corridor = Rectangle.Union(area, projected);
            corridor.Inflate(1, 1);
            if (HitRegions(corridor, body, left, right, up) == hit) return hit;

            // Use short steps against the physical hitbox. Running a single
            // broad sweep from one side of a wall to the other admits false
            // threats, as does the unused corner of a diagonal swept box.
            int steps = (int)Math.Ceiling(length / 8);
            Vector2 step = motion / steps;
            Vector2 start = position;
            for (int i = 0; i < steps && step.LengthSquared() >= .001f && hit != 15; i++)
            {
                if (terrain && !KnownFootprint(position, step, width, height)) break;
                var advance = terrain ? Collision.TileCollision(position, step, width, height, shot, shot) : step;
                if (terrain)
                {
                    var slope = Collision.SlopeCollision(position, advance, width, height, 0f, fall: shot);
                    position = new Vector2(slope.X, slope.Y);
                    advance = new Vector2(slope.Z, slope.W);
                }
                position += advance;
                bool blockedX = Math.Abs(advance.X - step.X) > .01f;
                bool blockedY = Math.Abs(advance.Y - step.Y) > .01f;
                var moved = area;
                moved.Offset((int)(position.X - start.X), (int)(position.Y - start.Y));
                hit |= HitRegions(moved, body, left, right, up);
                // A shot may die or change phase on native impact. Evaluate
                // the contact point, then wait for its real next observation.
                if (shot && (blockedX || blockedY)) break;
                if (blockedX) step.X = 0;
                if (blockedY) step.Y = 0;
            }
            return hit;
        }

        private static bool ProjectileThreatensPlayer(Projectile shot, Player body)
        {
            // Mirror the current actor-specific Enemy-vs-Player admission
            // without spending immunity or entering the native hurt path.
            if ((uint)body.whoAmI >= 255 || ProjectileID.Sets.SkipDamage_EVP[shot.type] ||
                shot.type == ProjectileID.ExplosiveBunny && !Main.getGoodWorld && shot.ai[1] - 1f != body.whoAmI ||
                shot.type == 1073 && body.npcTypeNoAggro[1] || shot.type == 1078 && body.spaceGun ||
                shot.type == 1091 && Main.noLightningSeed && Main.moreLightningSeed) return false;
            return CompanionHooks.ProjectileCanDealDamage(shot);
        }

        private static Rectangle NpcDamageArea(NPC enemy, Rectangle receiver, Vector2 motion)
        {
            // GetMeleeCollisionData checks the current hit against the
            // receiver for certain lunges. Move the observations into the
            // same relative frame instead of mutating a shared NPC position.
            var approaching = receiver;
            approaching.Offset(-(int)MathHelper.Clamp(motion.X, -192, 192), -(int)MathHelper.Clamp(motion.Y, -192, 192));
            receiver = Rectangle.Union(receiver, approaching);
            int offsetX = (int)enemy.netOffset.X, offsetY = (int)enemy.netOffset.Y;
            receiver.Offset(-offsetX, -offsetY);
            var area = enemy.Hitbox;
            int cooldown = 0;
            float multiplier = 1f;
            NPC.GetMeleeCollisionData(receiver, enemy.whoAmI, ref cooldown, ref multiplier, ref area);
            area.Offset(offsetX, offsetY);
            return area;
        }

        private static int HitRegions(Rectangle danger, Rectangle body, Rectangle left, Rectangle right, Rectangle up) =>
            (danger.Intersects(body) ? 1 : 0) | (danger.Intersects(left) ? 2 : 0) |
            (danger.Intersects(right) ? 4 : 0) | (danger.Intersects(up) ? 8 : 0);

        private static bool KnownFootprint(Vector2 position, Vector2 travel, int width, int height)
        {
            int left = (int)(Math.Min(position.X, position.X + travel.X) / 16) - 1;
            int right = (int)((Math.Max(position.X, position.X + travel.X) + width) / 16) + 1;
            int top = (int)(Math.Min(position.Y, position.Y + travel.Y) / 16) - 1;
            int bottom = (int)((Math.Max(position.Y, position.Y + travel.Y) + height) / 16) + 1;
            for (int x = left; x <= right; x++) for (int y = top; y <= bottom; y++)
                if (!CompanionNativeContact.Loaded(x, y)) return false;
            return true;
        }
    }
}
