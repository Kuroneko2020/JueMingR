using System;
using JueMingR.Platform.WorldObjectText;
using JueMingR.Platform.WorldTargets;

namespace JueMingR.Features.WorldObjectText
{
    public static class WorldObjectResolver
    {
        public static bool TryResolve(int x, int y, WorldTargetTile tile,
            Func<int, int, WorldTargetTile> read, out WorldObject result)
        {
            result = default(WorldObject);
            if (!tile.Readable || !tile.Active || tile.Inactive || x < 0 || y < 0) return false;
            int width = 2, styles;
            WorldObjectKind kind;
            switch (tile.Type)
            {
                case 21: case 441: kind = WorldObjectKind.Chest; styles = 52; break;
                case 467: case 468: kind = WorldObjectKind.Chest; styles = 38; break;
                case 88: kind = WorldObjectKind.Chest; width = 3; styles = 65; break;
                case 55: case 425: case 573: kind = WorldObjectKind.Sign; styles = 5; break;
                case 85: kind = WorldObjectKind.Tombstone; styles = 11; break;
                default: return false;
            }
            if (tile.FrameX < 0 || tile.FrameX % 18 != 0 || tile.FrameY != 0 && tile.FrameY != 18) return false;
            int style = tile.FrameX / (width * 18);
            if (style >= styles) return false;
            int left = x - tile.FrameX / 18 % width, top = y - tile.FrameY / 18;
            if (left < 0 || top < 0) return false;
            // Every constituent must agree. Do not call native Check/Read/Place:
            // those helpers can allocate cells, remove signs or mutate the world.
            for (int dy = 0; dy < 2; dy++)
                for (int dx = 0; dx < width; dx++)
                {
                    var cell = left + dx == x && top + dy == y ? tile : read(left + dx, top + dy);
                    if (!cell.Readable || !cell.Active || cell.Inactive || cell.Type != tile.Type ||
                        cell.FrameX != style * width * 18 + dx * 18 || cell.FrameY != dy * 18) return false;
                }
            result = new WorldObject { Kind = kind, TileX = left, TileY = top, Type = tile.Type, Style = style, Width = width };
            return true;
        }

        public static string ContainerName(WorldObject value, string customName, Func<ContainerNameFamily, int, string> native)
        {
            if (!String.IsNullOrWhiteSpace(customName)) return customName;
            ContainerNameFamily family;
            int index = value.Style;
            if (value.Type == 88) family = ContainerNameFamily.Dresser;
            else if (value.Type == 467 && value.Style == 4) { family = ContainerNameFamily.Item; index = 3988; }
            else family = value.Type == 467 || value.Type == 468 ? ContainerNameFamily.Chest2 : ContainerNameFamily.Chest;
            return native(family, index); // Host supplies an explicit fallback only when native information is missing.
        }
    }
}
