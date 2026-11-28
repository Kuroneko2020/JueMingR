using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.World
{
    internal sealed partial class WorldMemory
    {
        private static (Point point, bool complete) ShimmerRepresentative(Point start)
        {
            Point anchor = start;
            bool complete = ScanShimmer(start, point =>
            {
                if (point.Y < anchor.Y || point.Y == anchor.Y && point.X < anchor.X) anchor = point;
            });
            return (complete ? anchor : start, complete);
        }

        private static bool ScanShimmer(Point start, Action<Point> visit)
        {
            var seen = new HashSet<Point> { start };
            var work = new Queue<Point>();
            work.Enqueue(start);
            bool complete = true;
            while (work.Count != 0 && seen.Count < 512)
            {
                var point = work.Dequeue();
                visit(point);
                Visit(point.X - 1, point.Y); Visit(point.X + 1, point.Y);
                Visit(point.X, point.Y - 1); Visit(point.X, point.Y + 1);
            }
            return complete && work.Count == 0;

            void Visit(int x, int y)
            {
                if (x < 0 || y < 0 || x >= Main.maxTilesX || y >= Main.maxTilesY) return;
                if (!WorldTileKnowledge.CellKnown(x, y)) { complete = false; return; }
                var point = new Point(x, y);
                if (WetShimmer(point) && seen.Add(point)) work.Enqueue(point);
            }
        }

        private static bool WetShimmer(Point point)
        {
            var tile = Main.tile[point.X, point.Y];
            return tile?.liquid > 0 && tile.shimmer();
        }

        internal bool RecheckShimmer(Place place, Vector2 bodyCenter, out Point wetPoint, out bool invalid)
        {
            wetPoint = place.Point;
            invalid = false;
            var point = place.Point;
            if (!Ready || place.Category != "shimmer" || !WorldTileKnowledge.CellKnown(point.X, point.Y)) return false;
            if (WetShimmer(point))
            {
                Point target = point;
                float best = Single.PositiveInfinity;
                ScanShimmer(point, cell =>
                {
                    var world = cell.ToWorldCoordinates();
                    float distance = Vector2.DistanceSquared(world, bodyCenter);
                    if (distance >= best || bodyCenter.Y > world.Y + 8 || !Collision.CanHitLine(bodyCenter, 1, 1, world, 1, 1)) return;
                    target = cell;
                    best = distance;
                });
                wetPoint = target;
                if (place.Reported || place.Invalid || !PlaceWitnessCurrent(place))
                {
                    var sight = place.Copy();
                    sight.Reported = false; sight.Invalid = false; sight.Evidence = "到场复核：原版微光，确认代表格有液体。";
                    sight.ConfirmedUtc = DateTime.UtcNow.Ticks;
                    WitnessPlace(sight);
                    if (place.Revision > 0) live.Corrections[place.Id] = sight;
                }
                return true;
            }
            // Near another pool does not authorize throwing into it. Only
            // a completely synchronized dry neighborhood invalidates this
            // pool; partial evidence leaves the historic fact pending.
            int left = Math.Max(0, point.X - 5), top = Math.Max(0, point.Y - 5);
            int right = Math.Min(Main.maxTilesX, point.X + 6), bottom = Math.Min(Main.maxTilesY, point.Y + 6);
            if (!WorldTileKnowledge.RegionKnown(left, top, right, bottom)) return false;
            for (int y = top; y < bottom; y++)
                for (int x = left; x < right; x++)
                    if (WetShimmer(new Point(x, y))) return false;
            invalid = true;
            RecordDryPool(place);
            Mark(new Rectangle(left, top, right - left, bottom - top));
            return false;
        }
    }
}
