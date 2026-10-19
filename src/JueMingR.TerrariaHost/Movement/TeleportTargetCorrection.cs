using System;
using JueMingR.Features.Movement;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Movement
{
    internal static class TeleportTargetCorrection
    {
        // This runs after native ApplyItemTime and native target limiting. Do
        // not test itemTime==0 here: the real use has already consumed it.
        internal static bool TryCorrect(Player player, ref Vector2 point, out int enumerated)
        {
            var validator = new NativeValidator(player);
            float x, y;
            bool found = TeleportTargetSearch.TryFind(point.X, point.Y, ref validator, out x, out y, out enumerated);
            if (found) point = new Vector2(x, y);
            return found;
        }

        private struct NativeValidator : ITeleportTargetValidator
        {
            private readonly Player player;
            internal NativeValidator(Player player) { this.player = player; }
            public bool IsValid(float x, float y)
            {
                if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y) ||
                    x <= 50f || y <= 50f || x >= Main.maxTilesX * 16f - 50f || y >= Main.maxTilesY * 16f - 50f)
                    return false;
                var point = new Vector2(x, y);
                var limited = point;
                player.LimitPointToPlayerReachableArea(ref limited);
                // Candidates are native-canonical positions plus integral
                // eight-pixel offsets. Require the actual limiter to preserve
                // the sample, never adopt a clamped point as that candidate.
                if (limited.X != x || limited.Y != y) return false;
                Point from = player.Center.ToTileCoordinates();
                Point to = (point + new Vector2(player.width / 2, player.height / 2)).ToTileCoordinates();
                // Wall350 is the unbreakable line barrier; wall87 is a separate
                // destination/progression gate. Neither is a tile-type check.
                if (Collision.AnyWallOfTypeOnLine(from.X, from.Y, to.X, to.Y, 350)) return false;
                int tileX = (int)(x / 16f), tileY = (int)(y / 16f);
                if (Main.tile == null || Main.tile[tileX, tileY] == null) throw new InvalidOperationException("teleport-target-tile-unavailable");
                if (Main.tile[tileX, tileY].wall == 87 && !NPC.downedPlantBoss &&
                    (Main.remixWorld || tileY > Main.worldSurface)) return false;
                return !Collision.SolidCollision(point, player.width, player.height);
            }
        }
    }
}
