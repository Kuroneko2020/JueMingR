using System;
using JueMingR.Features.Movement;
using JueMingR.TerrariaHost.Movement;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal static class CompanionFallRisk
    {
        internal const int RescueHorizon = 12;

        internal static bool MayNeedRescue(Player body) => body.grapCount == 0 && body.velocity.Y * body.gravDir > 0 &&
            !body.slowFall && !LowGravityFallReset(body.position.Y) &&
            NativeFallLoss(body, body.position.Y + body.gravDir * body.maxFallSpeed * RescueHorizon, body.fallStart) >= body.statLife;

        internal static bool Threatened(Player body, int horizon, out SmartFallContact contact)
        {
            long reads = 0;
            contact = SmartFallGeometry.Contact(body.position, body.velocity, body.width, body.height, body.gravity,
                body.maxFallSpeed, body.gravDir, body.controlDown || body.stairFall, ref reads, horizon: horizon);
            return contact.Found && CompanionNativeContact.Loaded(contact.TileX, contact.TileY) &&
                LandingFallLoss(body, new Vector2(contact.X, contact.Y), body.fallStart) >= body.statLife;
        }

        internal static bool CanLand(Player body, Vector2 from, Vector2 landing, bool supported)
            => ProjectedLoss(body, from, landing, supported) < body.statLife;

        internal static int ProjectedLoss(Player body, Vector2 from, Vector2 landing, bool supported)
        {
            Vector2 position = CompanionNativeContact.TopLeft(body, landing);
            int start = supported ? (int)(CompanionNativeContact.TopLeft(body, from).Y / 16f) : body.fallStart;
            return body.slowFall || LowGravityFallReset(position.Y) ? 0 : LandingFallLoss(body, position, start);
        }

        internal static int ProtectionKey(Player body)
        {
            unchecked
            {
                int key = FallTolerance(body);
                key = key * 31 + (ImmuneToDrop(body) ? 1 : 0);
                key = key * 31 + (body.stoned ? 1 : 0);
                key = key * 31 + (body.slowFall ? 1 : 0);
                key = key * 31 + (body.mount.Active ? body.mount.FallDamage.GetHashCode() : 0);
                key = key * 31 + body.statDefense;
                return key * 31 + body.endurance.GetHashCode();
            }
        }

        private static bool ImmuneToDrop(Player body) => body.noFallDmg || body.hasWings || body.mount.CanFly(body) ||
            body.mount.Type == 1 || body.isPerformingJump_DownDash;

        private static int FallTolerance(Player body) => 25 + body.extraFall + (body.mount.Active ? body.mount.ExtraFall : 0);

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

        internal static int LandingFallLoss(Player body, Vector2 landing, int fallStart)
        {
            int loss = NativeFallLoss(body, landing.Y, fallStart);
            if (loss == 0) return 0;
            // Player.Update checks every foot tile including its right edge.
            int first = (int)(landing.X / 16f), last = (int)((landing.X + body.width) / 16f);
            int row = (int)((landing.Y + (body.gravDir < 0 ? -1 : body.height + 1)) / 16f);
            for (int x = first; x <= last; x++)
            {
                if (!CompanionNativeContact.Loaded(x, row)) continue;
                Tile tile = Main.tile[x, row];
                if (tile.active() && (TileID.Sets.Clouds[tile.type] || tile.type == 666 || tile.type == TileID.Platforms && tile.frameY / 18 == 49))
                    return 0;
            }
            return loss;
        }

        // Keep the actual release footprint and its downstream contact
        // geometry attached to the prediction. An asynchronous tile/liquid
        // edit can occur after planning and before the original Down/stride.
        internal struct ReleaseCorridor
        {
            private Rectangle tiles;
            private int cursor, width, height, gravity;
            private uint signature;
            private bool initialized;
            private const uint Initial = 2166136261u;

            internal bool Begin(Player body, Vector2 start, Vector2 end, float releaseX)
            {
                int left = (int)Math.Floor((Math.Min(releaseX, Math.Min(start.X, end.X)) - 12f) / 16f);
                int right = (int)Math.Floor((Math.Max(releaseX, Math.Max(start.X, end.X)) + body.width + 12f) / 16f);
                int top = (int)Math.Floor(Math.Min(start.Y, end.Y) / 16f) - 1;
                int bottom = (int)Math.Floor((Math.Max(start.Y, end.Y) + body.height) / 16f) + 1;
                tiles = new Rectangle(left, top, right - left + 1, bottom - top + 1);
                width = body.width;
                height = body.height;
                gravity = body.gravDir < 0 ? -1 : 1;
                cursor = 0;
                signature = Initial;
                initialized = true;
                return tiles.Width <= 12 && tiles.Height <= 56;
            }

            internal CompanionRefinementResult Capture(NavigationBudget budget)
            {
                int size = tiles.Width * tiles.Height;
                while (cursor < size)
                {
                    if (!budget.TryTiles()) return CompanionRefinementResult.Pending;
                    if (!Read(cursor, ref signature)) return CompanionRefinementResult.Failed;
                    cursor++;
                }
                return CompanionRefinementResult.Ready;
            }

            internal bool IsCurrent(Player body)
            {
                if (!initialized) return true;
                if (body.width != width || body.height != height || (body.gravDir < 0 ? -1 : 1) != gravity) return false;
                uint live = Initial;
                // The same bounded footprint (at most 12 by 56) is counted
                // by the immediate native-read counter, with no new forecast.
                for (int i = 0; i < cursor; i++)
                    if (!Read(i, ref live)) return false;
                return signature == live;
            }

            private bool Read(int index, ref uint hash)
            {
                int x = tiles.X + index % tiles.Width, y = tiles.Y + index / tiles.Width;
                if (!CompanionNativeContact.Loaded(x, y)) return false;
                Tile tile = Main.tile[x, y];
                unchecked
                {
                    uint terrain = 0;
                    if (tile.nactive() && (Main.tileSolid[tile.type] || Main.tileSolidTop[tile.type] ||
                        tile.type == TileID.TrapdoorClosed || tile.type == TileID.TrapdoorOpen))
                    {
                        terrain = 1u + tile.type + ((uint)tile.slope() << 12) + (tile.halfBrick() ? 1u << 16 : 0);
                        if (Main.tileSolidTop[tile.type] || tile.type == TileID.TrapdoorClosed || tile.type == TileID.TrapdoorOpen)
                            terrain = (terrain ^ (ushort)tile.frameX) * 16777619u ^ (ushort)tile.frameY;
                    }
                    hash = (hash ^ terrain) * 16777619u;
                    hash = (hash ^ (uint)(tile.liquid + (tile.liquid == 0 ? 0 : tile.liquidType() << 8))) * 16777619u;
                }
                return true;
            }
        }
    }
}
