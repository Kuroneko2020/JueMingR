using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.World
{
    internal sealed partial class WorldMemory
    {
        private const int AutomaticPoolCells = 512, RequestedPoolCells = 2048, PoolMaxSide = 64;

        // An exact old wet footprint, including gaps between separate pools.
        // The immutable mask belongs to the durable place, not an actor.
        internal sealed class ShimmerExtent
        {
            internal readonly Rectangle Bounds;
            private readonly byte[] mask;
            internal ShimmerExtent(Rectangle bounds, byte[] mask) { Bounds = bounds; this.mask = mask; }
            internal bool Contains(Point point)
            {
                if (!Bounds.Contains(point)) return false;
                int bit = (point.Y - Bounds.Top) * Bounds.Width + point.X - Bounds.Left;
                return (mask[bit >> 3] & (1 << (bit & 7))) != 0;
            }
            internal bool Overlaps(ShimmerExtent other)
            {
                var overlap = Rectangle.Intersect(Bounds, other.Bounds);
                for (int y = overlap.Top; y < overlap.Bottom; y++)
                    for (int x = overlap.Left; x < overlap.Right; x++)
                    {
                        var point = new Point(x, y);
                        if (Contains(point) && other.Contains(point)) return true;
                    }
                return false;
            }
            internal void Encode(System.IO.BinaryWriter writer)
            {
                writer.Write(Bounds.X); writer.Write(Bounds.Y); writer.Write(Bounds.Width); writer.Write(Bounds.Height);
                writer.Write(mask);
            }
        }

        private static ShimmerExtent DecodeShimmerExtent(System.IO.BinaryReader reader, Point anchor, int worldWidth, int worldHeight)
        {
            int x = reader.ReadInt32(), y = reader.ReadInt32(), width = reader.ReadInt32(), height = reader.ReadInt32();
            if (x < 0 || y < 0 || width < 1 || height < 1 || width > PoolMaxSide || height > PoolMaxSide ||
                x > worldWidth - width || y > worldHeight - height) throw new System.IO.InvalidDataException();
            int cells = width * height, bytes = (cells + 7) / 8;
            byte[] mask = reader.ReadBytes(bytes);
            if (mask.Length != bytes) throw new System.IO.InvalidDataException();
            if (cells % 8 != 0 && mask[bytes - 1] >> (cells % 8) != 0) throw new System.IO.InvalidDataException();
            var extent = new ShimmerExtent(new Rectangle(x, y, width, height), mask);
            if (!extent.Contains(anchor)) throw new System.IO.InvalidDataException();
            return extent;
        }

        private static bool ScanShimmer(Point start, int limit, out Point anchor, out ShimmerExtent extent,
            Action<Point> observe = null)
        {
            anchor = start; extent = null;
            if (!WorldTileKnowledge.CellKnown(start.X, start.Y) || !WetShimmer(start)) return false;
            var seen = new HashSet<Point> { start };
            var work = new Queue<Point>();
            work.Enqueue(start);
            int left = start.X, right = start.X, top = start.Y, bottom = start.Y;
            bool complete = true;
            while (work.Count != 0 && complete)
            {
                var point = work.Dequeue();
                if (point.Y < anchor.Y || point.Y == anchor.Y && point.X < anchor.X) anchor = point;
                observe?.Invoke(point);
                Visit(point.X - 1, point.Y); Visit(point.X + 1, point.Y);
                Visit(point.X, point.Y - 1); Visit(point.X, point.Y + 1);
            }
            if (!complete) return false;
            var bounds = new Rectangle(left, top, right - left + 1, bottom - top + 1);
            var mask = new byte[(bounds.Width * bounds.Height + 7) / 8];
            foreach (var cell in seen)
            {
                int bit = (cell.Y - top) * bounds.Width + cell.X - left;
                mask[bit >> 3] |= (byte)(1 << (bit & 7));
            }
            extent = new ShimmerExtent(bounds, mask);
            return true;

            void Visit(int x, int y)
            {
                if (!complete || x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY) return;
                if (!WorldTileKnowledge.CellKnown(x, y) || Main.tile[x, y] == null) { complete = false; return; }
                var point = new Point(x, y);
                if (!WetShimmer(point) || !seen.Add(point)) return;
                left = Math.Min(left, x); right = Math.Max(right, x);
                top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                complete = seen.Count <= limit && right - left < PoolMaxSide && bottom - top < PoolMaxSide;
                if (complete) work.Enqueue(point);
            }
        }

        private static bool WetShimmer(Point point)
        {
            var tile = Main.tile[point.X, point.Y];
            return tile?.liquid > 0 && tile.shimmer();
        }

        private Place ObserveShimmer(Point observed, bool automatic)
        {
            if (!ScanShimmer(observed, automatic ? AutomaticPoolCells : RequestedPoolCells, out var anchor, out var extent)) return null;
            Place prior = null;
            var saved = VisiblePlaces(live);
            if (saved != null)
                foreach (var place in saved.Entries.Values)
                {
                    if (place.Category != "shimmer" || place.Reported ||
                        (place.ShimmerPool != null ? !place.ShimmerPool.Overlaps(extent) : !extent.Contains(place.Point))) continue;
                    // Joining previously distinct pools needs a new explicit
                    // determination. Do not silently award either durable ID.
                    if (prior != null) return null;
                    prior = place;
                }
            var result = NewPlace(live, "shimmer", prior?.Point ?? anchor, "实测原版连通微光液体及池界。");
            if (prior != null) result.Id = prior.Id;
            result.ShimmerPool = prior?.ShimmerPool ?? extent;
            WitnessPlace(result);
            return result;
        }

        internal bool RecheckShimmer(Place place, Vector2 bodyCenter, out Point wetPoint, out bool invalid)
        {
            wetPoint = place.Point; invalid = false;
            var point = place.Point;
            if (!Ready || place.Category != "shimmer" || !WorldTileKnowledge.CellKnown(point.X, point.Y)) return false;
            var prior = place.ShimmerPool;
            if (WetShimmer(point))
            {
                Point target = point;
                float best = Single.PositiveInfinity;
                bool complete = ScanShimmer(point, RequestedPoolCells, out _, out var observed, cell =>
                {
                    if (prior != null && !prior.Contains(cell)) return;
                    var world = cell.ToWorldCoordinates();
                    float distance = Vector2.DistanceSquared(world, bodyCenter);
                    if (distance >= best || bodyCenter.Y > world.Y + 8 || !Collision.CanHitLine(bodyCenter, 1, 1, world, 1, 1)) return;
                    target = cell; best = distance;
                });
                if (!complete) { live.Reviewed.Remove(place.Id); return false; }
                wetPoint = target;
                if (prior == null || place.Reported || place.Invalid || !PlaceWitnessCurrent(place))
                {
                    var sight = place.Copy();
                    sight.ShimmerPool = prior ?? observed;
                    sight.Reported = false; sight.Invalid = false; sight.Evidence = "到场复核：原版微光仍在已知池界内。";
                    sight.ConfirmedUtc = DateTime.UtcNow.Ticks;
                    WitnessPlace(sight);
                    if (place.Revision > 0) live.Corrections[place.Id] = sight;
                }
                return true;
            }

            // Drying the reference column is not evidence for the old pool.
            // Check its entire old footprint and adjacent cells, and never
            // switch an in-progress task to a disconnected remaining pocket.
            live.Reviewed.Remove(place.Id);
            if (prior == null) return false;
            var original = prior.Bounds;
            original.Inflate(1, 1);
            var area = Rectangle.Intersect(original, new Rectangle(0, 0, Main.maxTilesX, Main.maxTilesY));
            if (!WorldTileKnowledge.RegionKnown(area.Left, area.Top, area.Right, area.Bottom)) return false;
            for (int y = area.Top; y < area.Bottom; y++)
                for (int x = area.Left; x < area.Right; x++)
                {
                    if (Main.tile[x, y] == null) return false;
                    if (WetShimmer(new Point(x, y)) && (prior.Contains(new Point(x, y)) ||
                        prior.Contains(new Point(x - 1, y)) || prior.Contains(new Point(x + 1, y)) ||
                        prior.Contains(new Point(x, y - 1)) || prior.Contains(new Point(x, y + 1)))) return false;
                }
            invalid = true;
            RecordDryPool(place);
            Mark(area);
            return false;
        }
    }
}
