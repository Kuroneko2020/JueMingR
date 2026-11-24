using System;
using HarmonyLib;
using JueMingR.TerrariaHost.World;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    // Track frames are Terraria's switch and grade data. Reciprocal
    // connections are expressed at the two adjacent columns of a rail.
    internal static class NavigationRail
    {
        internal const float RelativeTravelCost = .35f;
        private static readonly AccessTools.FieldRef<int[]> left =
            AccessTools.StaticFieldRefAccess<int[]>(AccessTools.Field(typeof(Minecart), "_leftSideConnection"));
        private static readonly AccessTools.FieldRef<int[]> right =
            AccessTools.StaticFieldRefAccess<int[]>(AccessTools.Field(typeof(Minecart), "_rightSideConnection"));
        private static readonly AccessTools.FieldRef<int[][]> heights =
            AccessTools.StaticFieldRefAccess<int[][]>(AccessTools.Field(typeof(Minecart), "_tileHeight"));

        internal static bool TryHeight(int frame, int sample, out int height)
        {
            height = -1;
            var table = heights();
            if (table == null || frame < 0 || frame >= table.Length) return false;
            height = table[frame][sample];
            return height >= 0;
        }

        internal static int FrameAt(NavigationCell track, Point cell, Vector2 magnet)
        {
            int sample = Math.Max(0, Math.Min(7, ((int)magnet.X - cell.X * 16) / 2));
            float relativeY = magnet.Y - cell.Y * 16;
            float frontError = TryHeight(track.FrameX, sample, out int front) ? Math.Abs(front - relativeY) : float.PositiveInfinity;
            float backError = TryHeight(track.FrameY, sample, out int back) ? Math.Abs(back - relativeY) : float.PositiveInfinity;
            if (frontError <= backError) return frontError <= 5 ? track.FrameX : -1;
            return backError <= 5 ? track.FrameY : -1;
        }

        internal static bool Matches(Point tile, int frame, Vector2 magnet)
        {
            if (NavigationTraversal.CellAt(magnet) != tile || !WorldTileKnowledge.CellKnown(tile.X, tile.Y)) return false;
            var track = Main.tile[tile.X, tile.Y];
            int sample = ((int)magnet.X - tile.X * 16) / 2;
            return track != null && track.nactive() && track.type == TileID.MinecartTrack &&
                (frame == track.frameX || frame == track.frameY) && TryHeight(frame, sample, out int height) &&
                Math.Abs(tile.Y * 16 + height - magnet.Y) <= 5;
        }

        internal static bool Exit(int frame, int dx, out int grade)
        {
            grade = 0;
            var connections = dx > 0 ? right() : left();
            if (connections == null || frame < 0 || frame >= connections.Length || connections[frame] < 0) return false;
            grade = connections[frame] - 1;
            return true;
        }

        internal static bool Enters(int frame, int dx, int grade) =>
            Exit(frame, -dx, out int reverse) && reverse == -grade && TryHeight(frame, 4, out _);

        internal static int BranchInput(NavigationCell next, int frame, int grade)
        {
            if (grade != 0) return grade;
            int alternative = frame == next.FrameX ? next.FrameY : next.FrameX;
            return TryHeight(frame, 4, out var chosen) && TryHeight(alternative, 4, out var other) ?
                Math.Sign(chosen - other) : 0;
        }
    }
}
