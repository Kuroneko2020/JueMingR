using System;
using JueMingR.TerrariaHost.Movement;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class CompanionNavigation
    {
        private static bool ImmuneToDrop(Player body) => body.noFallDmg || body.hasWings || body.mount.CanFly(body) ||
            body.mount.Type == 1 || body.isPerformingJump_DownDash;

        private static int FallTolerance(Player body) => 25 + body.extraFall + (body.mount.Active ? body.mount.ExtraFall : 0);

        private static int DescentSafetyKey(Player body)
        {
            unchecked
            {
                int key = FallTolerance(body);
                key = key * 31 + (ImmuneToDrop(body) ? 1 : 0);
                key = key * 31 + (body.stoned ? 1 : 0);
                key = key * 31 + (body.slowFall ? 1 : 0);
                key = key * 31 + (body.waterWalk || body.waterWalk2 ? 1 : 0);
                key = key * 31 + (body.mount.Active ? body.mount.FallDamage.GetHashCode() : 0);
                key = key * 31 + body.statDefense;
                key = key * 31 + body.endurance.GetHashCode();
                return key * 31 + (Main.masterMode ? 2 : Main.expertMode ? 1 : 0);
            }
        }

        private static bool LowGravityFallReset(float y)
        {
            float worldSize = Main.maxTilesX / 4200f;
            worldSize *= worldSize;
            float ratio = (float)((y / 16f - (60f + 10f * worldSize)) / (Main.worldSurface / (Main.remixWorld ? 1.0 : 6.0)));
            return ratio < .8;
        }

        private static int NativeFallLoss(Player body, float y, int fallStart)
        {
            if (body.mount.CanFly(body) || body.mount.Type == 1 || body.isPerformingJump_DownDash) return 0;
            int fall = ((int)(y / 16f) - fallStart) * (body.gravDir < 0 ? -1 : 1);
            int damage;
            if (body.stoned) damage = (fall - 2) * 20;
            else
            {
                if (ImmuneToDrop(body)) return 0;
                damage = (fall - FallTolerance(body)) * 10;
                if (body.mount.Active) damage = (int)(damage * body.mount.FallDamage);
            }
            if (damage <= 0) return 0;
            // Fall damage calls Hurt directly. Use its defense coefficients
            // and endurance truncation, without assuming consumable shields,
            // dodges, other players' damage sharing or future treatment.
            return Math.Max(1, (int)((double)(1f - body.endurance) * Main.CalculateDamagePlayersTake(damage, body.statDefense)));
        }

        private static int LandingFallLoss(Player body, Vector2 landing, int fallStart)
        {
            int loss = NativeFallLoss(body, landing.Y, fallStart);
            if (loss == 0) return 0;
            // Player.Update checks every foot tile including its right edge.
            int first = (int)(landing.X / 16f), last = (int)((landing.X + body.width) / 16f);
            int row = (int)((landing.Y + (body.gravDir < 0 ? -1 : body.height + 1)) / 16f);
            for (int x = first; x <= last; x++)
            {
                if (!Loaded(x, row)) continue;
                Tile tile = Main.tile[x, row];
                if (tile.active() && (TileID.Sets.Clouds[tile.type] || tile.type == 666 || tile.type == TileID.Platforms && tile.frameY / 18 == 49))
                    return 0;
            }
            return loss;
        }

        private bool ShouldRescueFall(Player body, CompanionMobilitySnapshot mobility)
        {
            if (body.grapCount != 0 || body.velocity.Y * body.gravDir <= 0 || body.slowFall || LowGravityFallReset(body.position.Y)) return false;
            const int horizon = 12;
            int fallStart = body.fallStart;
            if (NativeFallLoss(body, body.position.Y + body.gravDir * body.maxFallSpeed * horizon, fallStart) < body.statLife) return false;

            if (actions.HasStep && body.gravDir == 1f)
            {
                var action = actions.Current;
                if ((action.Kind == RouteKind.Drop || action.Kind == RouteKind.LedgeDrop) &&
                    DescentContactRisk(body, mobility, action, horizon, out bool threatened)) return threatened;
            }

            long reads = 0;
            var contact = SmartFallGeometry.Contact(body.position, body.velocity, body.width, body.height, body.gravity,
                body.maxFallSpeed, body.gravDir, body.controlDown || body.stairFall, ref reads, horizon: horizon);
            return contact.Found && Loaded(contact.TileX, contact.TileY) &&
                LandingFallLoss(body, new Vector2(contact.X, contact.Y), fallStart) >= body.statLife;
        }

        private static bool DescentContactRisk(Player body, CompanionMobilitySnapshot mobility, RouteStep action, int horizon, out bool threatened)
        {
            threatened = false;
            var probe = new DescentProbe { Position = body.position, Release = action.Operand,
                Velocity = body.velocity, FallStart = body.fallStart, Steer = action.DescentSteer, AimX = action.DescentAimX,
                StairFall = body.stairFall };
            using (Scratch.Save())
            {
                // A live descent can be intentionally about to steer onto a
                // different shelf. The rescue must look ahead with that same
                // native input, including the real accumulated fall-start.
                for (int tick = 0; tick < horizon; tick++)
                {
                    if (!AdvanceDescent(body, mobility, action.Direction, body.gravity, body.maxFallSpeed,
                        null, ref probe, out Step contact)) return false;
                    if (!contact.Ground) continue;
                    threatened = LandingFallLoss(body, contact.Position, probe.FallStart) >= body.statLife;
                    break;
                }
            }
            return true;
        }
    }
}
