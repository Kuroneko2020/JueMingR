using System;
using System.Collections.Generic;
using JueMingR.Features.Movement;
using JueMingR.Features.Tools;
using JueMingR.TerrariaHost.World;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Tools
{
    // Current contact only: no future landing, projectile-pool scan or durable
    // protection. Reused storage is rebuilt at each target-selection boundary.
    internal sealed class MiningSupport
    {
        private readonly HashSet<long> points = new HashSet<long>();
        private static long Key(int x, int y) { return ((long)x << 32) | (uint)y; }
        internal void Clear() { points.Clear(); }
        internal void Capture(Player player)
        {
            points.Clear();
            float left = player.position.X, right = left + player.width;
            float direction = player.gravDir < 0 ? -1 : 1;
            float edge = player.position.Y + (direction > 0 ? player.height : 0);
            int row = (int)Math.Floor(edge / 16);
            for (int x = (int)Math.Floor(left / 16); x <= (int)Math.Floor((right - .001f) / 16); x++)
            for (int y = row - 1; y <= row; y++)
            {
                var observed = WorldTileObservation.ReadCurrent(x, y);
                if (!observed.Readable || !observed.Active || observed.Inactive) continue;
                Tile tile = Main.tile[x, y];
                bool platform = Main.tileSolidTop[tile.type];
                if (!Main.tileSolid[tile.type] && (!platform || tile.frameY != 0) || platform && direction < 0) continue;
                float surface = SmartFallDecision.Surface(x, y, tile.slope(), tile.halfBrick(), left, right, direction);
                if (Math.Abs(edge - surface) <= .25f) points.Add(Key(x, y));
            }
            // .8 GrappleMovement consumes only grapCount with grappling[0] as
            // sentinel. Attached AI_007 revalidates Center.ToTileCoordinates,
            // including visual-hitbox offsets; do not infer an old launch tile.
            if (player.grappling == null || player.grappling.Length == 0 || player.grappling[0] < 0) return;
            for (int i = 0; i < player.grapCount && i < player.grappling.Length; i++)
            {
                int index = player.grappling[i];
                if (index < 0 || index >= Main.projectile.Length) continue;
                Projectile hook = Main.projectile[index];
                if (hook == null || !hook.active || hook.whoAmI != index || hook.owner != player.whoAmI || hook.aiStyle != 7 || hook.ai[0] != 2f) continue;
                int x = (int)Math.Floor(hook.Center.X / 16), y = (int)Math.Floor(hook.Center.Y / 16);
                var tile = WorldTileObservation.ReadCurrent(x, y);
                if (tile.Readable && tile.Active && !tile.Inactive && (hook.type != 403 || tile.Type == 314) &&
                    (Main.tileSolid[tile.Type] || tile.Type == 314 || hook.type == 865 && (TileID.Sets.IsATreeTrunk[tile.Type] || tile.Type == 323))) points.Add(Key(x, y));
            }
        }
        internal bool Deferred(MiningPoint point)
        {
            // The supported static spike set includes decorative 745. It is
            // intentionally wider than the native damage-only spike set.
            return point.Type != 48 && point.Type != 232 && point.Type != 745 && point.Type != 750 && points.Contains(Key(point.X, point.Y));
        }
    }
}
