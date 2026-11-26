using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Companions
{
    // Candidate discovery is local to the fixed circle. A pond observation uses
    // the same horizontal row and continuous downward columns as vanilla.
    internal sealed class CompanionFishingWater
    {
        internal const int Radius = 320;
        internal const int ScanBudget = 160;

        internal readonly struct Entry
        {
            internal readonly Point Cell;
            internal readonly Vector2 Surface;
            internal readonly float Distance;

            internal Entry(Point cell, Vector2 surface, float distance)
            { Cell = cell; Surface = surface; Distance = distance; }
        }

        internal enum Result { Pending, Unknown, Ready }

        internal sealed class Pond
        {
            private int x, y, left, right, phase;
            private bool honey, lava;
            private int count;
            internal readonly int Row;
            internal bool Eligible => Count >= 75;
            internal int Count => honey ? (int)(count * 1.5) : count;
            internal int Liquid => lava ? 1 : honey ? 2 : 0;
            internal bool Contains(int tileX, int tileY) => tileY == Row && tileX > left && tileX < right;

            internal Pond(Point start)
            { left = right = start.X; x = start.X; Row = y = start.Y; }

            internal Result Advance(ref int budget, Func<int, int, Tile> read)
            {
                while (budget > 0 && phase < 3)
                {
                    budget--;
                    if (phase == 0 || phase == 1)
                    {
                        int column = phase == 0 ? left : right;
                        bool inRange = phase == 0 ? column > 10 : column < Main.maxTilesX - 10;
                        var tile = inRange ? read(column, Row) : null;
                        if (inRange && tile == null) return Result.Unknown;
                        if (inRange && MeasureTile(tile))
                        {
                            if (phase == 0) left--; else right++;
                        }
                        else if (++phase == 2) x = left;
                        continue;
                    }
                    var next = y < Main.maxTilesY - 10 ? read(x, y) : null;
                    if (next == null && y < Main.maxTilesY - 10) return Result.Unknown;
                    if (MeasureTile(next))
                    {
                        count++;
                        if (next.lava()) lava = true;
                        else if (next.honey()) honey = true;
                        y++;
                    }
                    else
                    {
                        y = Row;
                        if (++x > right) phase = 3;
                    }
                }
                return phase == 3 ? Result.Ready : Result.Pending;
            }
        }

        private readonly int minX, maxX, maxY;
        private int x, y;
        private readonly List<Entry> entries = new List<Entry>();
        internal IReadOnlyList<Entry> Entries => entries;
        internal Vector2 Center { get; }

        internal CompanionFishingWater(Vector2 center)
        {
            Center = center;
            minX = x = Math.Max(10, (int)((center.X - Radius) / 16));
            maxX = Math.Min(Main.maxTilesX - 11, (int)((center.X + Radius) / 16));
            y = Math.Max(11, (int)((center.Y - Radius) / 16));
            maxY = Math.Min(Main.maxTilesY - 11, (int)((center.Y + Radius) / 16));
        }

        internal Result Advance(ref int budget, Func<int, int, Tile> read)
        {
            while (y <= maxY && budget >= 2)
            {
                budget -= 2;
                float surfaceX = MathHelper.Clamp(Center.X, x * 16, x * 16 + 15.99f);
                float nearestY = MathHelper.Clamp(Center.Y, y * 16, y * 16 + 16);
                if (WithinCircle(Center, new Vector2(surfaceX, nearestY)))
                {
                    var tile = read(x, y);
                    if (tile == null) return Result.Unknown;
                    if (MeasureTile(tile) && !tile.shimmer())
                    {
                        var above = read(x, y - 1);
                        if (above == null) return Result.Unknown;
                        // A lower row with liquid above this column cannot
                        // supply a new water line reached by an ordinary cast.
                        if (!MeasureTile(above))
                        {
                            var surface = new Vector2(surfaceX, (y + 1) * 16 - tile.liquid / 16);
                            if (WithinCircle(Center, surface))
                                entries.Add(new Entry(new Point(x, y), surface, Vector2.DistanceSquared(Center, surface)));
                        }
                    }
                }
                if (++x > maxX) { x = minX; y++; }
            }
            if (y <= maxY) return Result.Pending;
            entries.Sort((a, b) =>
            {
                int order = a.Distance.CompareTo(b.Distance);
                if (order != 0) return order;
                order = a.Cell.Y.CompareTo(b.Cell.Y);
                return order != 0 ? order : a.Cell.X.CompareTo(b.Cell.X);
            });
            return Result.Ready;
        }

        internal static bool MeasureTile(Tile tile) => tile != null && tile.liquid > 0 && !WorldGen.SolidTile(tile);
        internal static bool WithinCircle(Vector2 center, Vector2 point) => Vector2.DistanceSquared(center, point) <= Radius * Radius;
    }
}
