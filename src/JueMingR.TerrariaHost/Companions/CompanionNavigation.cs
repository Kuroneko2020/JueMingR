using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    // Finite ordinary-ground traversal. These tests use the real body size and
    // native solid/platform/half-brick/slope collision primitives, not empty-tile
    // four-way connectivity. No position or velocity is written to the actor.
    internal sealed class CompanionNavigation
    {
        private float jumpSpeed = 5.01f;
        private int jumpHold = 15;
        private Vector2 launch, landing;
        private bool hasLanding;
        private int plannedDirection;
        private bool failed;
        private Vector2 failedLaunch;
        private int failedDirection, failedTargetHeight, failedTerrain;
        private float failedJumpSpeed;
        private int failedJumpHold;
        internal int JumpHold { get { return jumpHold; } }
        internal void ObserveCapabilities(Player body)
        {
            if (body.gravDir == 1 && !body.mount.Active)
            { jumpSpeed = Math.Max(0, Player.jumpSpeed); jumpHold = Math.Max(1, Player.jumpHeight); }
        }
        internal static bool Supported(Player body, Vector2 position, bool platforms = true)
        {
            Vector2 down = Collision.TileCollision(position, new Vector2(0, 3), body.width, body.height, !platforms, !platforms);
            if (down.Y < 2.9f) return true;
            Vector4 slope = Collision.SlopeCollision(position, new Vector2(0, 3), body.width, body.height, body.gravity, !platforms);
            return slope.W < 2.9f || slope.Y < position.Y;
        }
        internal static bool SafeBody(Player body, Vector2 position)
        {
            if (position.X < 16 || position.Y < 16 || position.X + body.width >= (Main.maxTilesX - 1) * 16 || position.Y + body.height >= (Main.maxTilesY - 1) * 16) return false;
            if (!ClearBody(body, position)) return false;
            if (!body.lavaImmune && Collision.LavaCollision(position, body.width, body.height)) return false;
            return true;
        }
        private static bool ClearBody(Player body, Vector2 position)
        {
            if (!Collision.SolidCollision(position, body.width, body.height)) return true;
            // SolidCollision deliberately treats a slope as its full tile box.
            // Reject full solids first, then let native slope geometry validate
            // the actual resting position instead of making slopes impassable.
            int minX = Math.Max(0, (int)position.X / 16), maxX = Math.Min(Main.maxTilesX - 1, (int)(position.X + body.width - 0.01f) / 16);
            int minY = Math.Max(0, (int)position.Y / 16), maxY = Math.Min(Main.maxTilesY - 1, (int)(position.Y + body.height - 0.01f) / 16);
            for (int x = minX; x <= maxX; x++) for (int y = minY; y <= maxY; y++)
            {
                var tile = Main.tile[x, y];
                if (tile == null || !tile.active() || tile.inActive() || !Main.tileSolid[tile.type] || Main.tileSolidTop[tile.type] || tile.slope() != 0) continue;
                float top = y * 16 + (tile.halfBrick() ? 8 : 0);
                if (position.Y + body.height > top && position.Y < (y + 1) * 16) return false;
            }
            Vector4 slope = Collision.SlopeCollision(position, Vector2.Zero, body.width, body.height, body.gravity);
            return Vector2.DistanceSquared(position, new Vector2(slope.X, slope.Y)) <= 4;
        }
        internal static bool WalkSafe(Player body, int direction)
        {
            if (direction == 0) return true;
            float braking = body.velocity.X * body.velocity.X / Math.Max(0.08f, 2 * body.runSlowdown);
            float ahead = Math.Min(128, Math.Max(16, braking + 12));
            for (float x = 8; x <= ahead; x += 8)
            {
                Vector2 at = body.position + new Vector2(direction * x, 0);
                if (Collision.SolidCollision(at, body.width, body.height))
                {
                    // One-tile step/slope is resolved by native movement. A tall
                    // obstacle is not a reason to keep walking into an abyss.
                    at.Y -= 16;
                    if (Collision.SolidCollision(at, body.width, body.height)) return true;
                }
                bool found = false;
                for (int y = 0; y <= 48; y += 8)
                {
                    Vector2 foot = at + new Vector2(0, y);
                    if (SafeBody(body, foot) && Supported(body, foot)) { found = true; break; }
                }
                if (!found) return false;
            }
            return true;
        }
        internal static bool Wall(Player body, int direction)
        {
            if (direction == 0) return false;
            var request = new Vector2(direction * 12, 0);
            var actual = Collision.TileCollision(body.position - new Vector2(0, 16), request, body.width, body.height);
            return Math.Abs(actual.X - request.X) > 0.1f;
        }
        internal bool JumpSafe(Player body, Vector2 target, int direction, bool allowPlanning)
        {
            if (hasLanding && plannedDirection == direction && Vector2.DistanceSquared(body.position, launch) <= 24 * 24 &&
                SafeBody(body, landing) && Supported(body, landing)) return true;
            hasLanding = false;
            if (!allowPlanning || direction == 0 && target.Y >= body.Center.Y - 36 || body.gravDir != 1 || body.mount.Active || jumpSpeed <= 0) return false;
            int terrain = TerrainIdentity(body), targetHeight = (int)target.Y / 32;
            if (failed && failedDirection == direction && failedTargetHeight == targetHeight && failedTerrain == terrain &&
                failedJumpSpeed == jumpSpeed && failedJumpHold == jumpHold && Vector2.DistanceSquared(body.position, failedLaunch) <= 16) return false;
            failed = true; failedDirection = direction; failedTargetHeight = targetHeight; failedTerrain = terrain;
            failedLaunch = body.position; failedJumpSpeed = jumpSpeed; failedJumpHold = jumpHold;
            Vector2 position = body.position;
            float speedX = direction * Math.Min(Math.Max(2.5f, body.maxRunSpeed), 8);
            float speedY = -jumpSpeed;
            // A single 90-step trajectory is the actual planning slice. A team
            // round-robin grants slices; every actor still consumes its valid
            // prefix and performs the finite immediate checks every tick.
            for (int step = 0; step < 90; step++)
            {
                if (step < jumpHold) speedY = -jumpSpeed;
                else speedY = Math.Min(body.maxFallSpeed, speedY + Math.Max(0.05f, body.gravity));
                Vector2 desired = new Vector2(speedX, speedY);
                Vector2 actual = Collision.TileCollision(position, desired, body.width, body.height);
                if (Math.Abs(actual.X - desired.X) > 0.1f) return false;
                position += actual;
                Vector4 slope = Collision.SlopeCollision(position, actual, body.width, body.height, body.gravity);
                position = new Vector2(slope.X, slope.Y);
                if (!SafeBody(body, position)) return false;
                if (speedY > 0 && (actual.Y < desired.Y || Supported(body, position)))
                {
                    if (position.Y > body.position.Y + 64) return false;
                    if (direction != 0 && Math.Abs(position.X - body.position.X) < 24) return false;
                    launch = body.position; landing = position; plannedDirection = direction; hasLanding = true; failed = false; return true;
                }
            }
            return false;
        }
        private static int TerrainIdentity(Player body)
        {
            int cx = (int)body.Center.X / 16, cy = (int)body.Center.Y / 16;
            unchecked
            {
                int hash = 17;
                // Recheck only this ordinary-jump dependency rectangle when a
                // team planning slice is granted. An unchanged failed route is
                // not simulated again until terrain/target/capability changes.
                for (int x = Math.Max(0, cx - 12); x <= Math.Min(Main.maxTilesX - 1, cx + 12); x++)
                    for (int y = Math.Max(0, cy - 12); y <= Math.Min(Main.maxTilesY - 1, cy + 6); y++)
                    {
                        var tile = Main.tile[x, y];
                        hash = hash * 31 + (tile == null ? 0 : tile.type + (tile.active() ? 65536 : 0) + (tile.inActive() ? 131072 : 0) + tile.slope() * 262144 + (tile.halfBrick() ? 2097152 : 0) + tile.liquid * 4194304 + tile.liquidType());
                    }
                return hash;
            }
        }
        internal static bool DropSafe(Player body)
        {
            if (body.gravDir != 1) return false;
            // Platforms must be the current support and a real safe landing must
            // exist below. A solid block cannot be passed by pressing down.
            if (!Supported(body, body.position) || Supported(body, body.position, platforms: false)) return false;
            for (int y = 16; y <= 160; y += 8)
            {
                Vector2 at = body.position + new Vector2(0, y);
                if (SafeBody(body, at) && Supported(body, at)) return true;
            }
            return false;
        }
    }
}
