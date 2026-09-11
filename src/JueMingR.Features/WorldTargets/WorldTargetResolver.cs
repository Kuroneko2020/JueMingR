using System;
using JueMingR.Platform.WorldTargets;

namespace JueMingR.Features.WorldTargets
{
    public struct WorldTarget
    {
        public WorldTargetKind Kind;
        public int TileX, TileY, Style;
        public float X, Y, Width, Height;
        public float CenterX { get { return X + Width / 2; } }
        public float CenterY { get { return Y + Height / 2; } }
    }
    internal static class WorldTargetResolver
    {
        internal static bool TryResolve(int x, int y, WorldTargetTile tile, WorldTargetSettings settings,
            Func<int, int, WorldTargetTile> read, out WorldTarget target)
        {
            target = default(WorldTarget); WorldTargetKind kind;
            switch (tile.Type)
            {
                case 12: kind = WorldTargetKind.LifeCrystal; break;
                case 236: kind = WorldTargetKind.LifeFruit; break;
                case 639: kind = WorldTargetKind.ManaCrystal; break;
                case 751: kind = WorldTargetKind.SleepingDigtoise; break;
                case 752: kind = WorldTargetKind.ChilletEgg; break;
                default: return false;
            }
            if (!settings.Enabled(kind) || !Valid(tile, kind) || tile.FrameX < 0 || tile.FrameX % 18 != 0 ||
                tile.FrameY < 0 || tile.FrameY > 18 || tile.FrameY % 18 != 0) return false;
            int style = tile.FrameX / 36;
            if (style > (kind == WorldTargetKind.LifeFruit ? 2 : 0)) return false;
            int originX = x - tile.FrameX % 36 / 18, originY = y - tile.FrameY / 18;
            // Frames identify independent 2x2 objects even when touching. Fruit
            // styles differ by 36; sleeping turtle draw variants are NOT raw frames.
            for (int dy = 0; dy < 2; dy++) for (int dx = 0; dx < 2; dx++)
            {
                WorldTargetTile part = read(originX + dx, originY + dy);
                if (!Valid(part, kind) || part.Type != tile.Type || part.FrameX != style * 36 + dx * 18 || part.FrameY != dy * 18) return false;
            }
            target = new WorldTarget { Kind = kind, TileX = originX, TileY = originY, Style = style,
                X = originX * 16, Y = originY * 16, Width = 32, Height = 32 };
            if (kind == WorldTargetKind.SleepingDigtoise) { target.X -= 9; target.Y -= 8; target.Width = 56; target.Height = 46; }
            else if (kind == WorldTargetKind.ChilletEgg) { target.X -= 2; target.Y += 2; target.Width = 36; target.Height = 38; }
            return true;
        }
        private static bool Valid(WorldTargetTile tile, WorldTargetKind kind)
        { return tile.Readable && tile.Active && (!(kind == WorldTargetKind.LifeCrystal || kind == WorldTargetKind.ManaCrystal) || !tile.Inactive); }
    }
}
