using System;
using System.Collections.Generic;
using JueMingR.Platform.WorldObjectText;
using JueMingR.Platform.WorldTargets;

namespace JueMingR.Features.WorldObjectText
{
    // Published indices are immutable to consumers. Only the record owner adds
    // session deltas; the I/O worker builds a new index before publication.
    public sealed class OpenedPositionIndex
    {
        public const int MaximumPositions = 262144;
        private readonly HashSet<long> positions = new HashSet<long>();
        private readonly Dictionary<long, List<long>> buckets = new Dictionary<long, List<long>>();
        public OpenedPositionIndex(IEnumerable<long> values)
        { if (values == null) throw new ArgumentNullException(nameof(values)); foreach (long key in values) if (!Add(key)) throw new ArgumentException("duplicate-opened-position"); }
        internal OpenedPositionIndex() { }
        public int Count { get { return positions.Count; } }
        public bool Contains(long key) { return positions.Contains(key); }
        internal IEnumerable<long> All { get { return positions; } }
        internal List<long> Bucket(int x, int y) { List<long> values; return buckets.TryGetValue(WorldObject.PositionKey(x, y), out values) ? values : null; }
        public OpenedPositionQuery Query(WorldTargetView view) { return new OpenedPositionQuery(new[] { this }, view); }
        internal bool Add(long key)
        {
            int x = (int)(key >> 32), y = (int)key;
            if (x < 0 || y < 0 || x > 1000000 || y > 1000000) throw new ArgumentOutOfRangeException(nameof(key));
            if (positions.Contains(key)) return false;
            if (positions.Count == MaximumPositions) throw new InvalidOperationException("opened-position-capacity");
            positions.Add(key); long bucket = WorldObject.PositionKey(x / 64, y / 64);
            List<long> values; if (!buckets.TryGetValue(bucket, out values)) { values = new List<long>(); buckets.Add(bucket, values); }
            values.Add(key); return true;
        }
        public IEnumerable<long> Nearby(WorldTargetView view)
        {
            if (view.Width <= 0 || view.Height <= 0 || view.Width > 1024 || view.Height > 1024) yield break;
            // Bounded visible buckets, not an H-wide filter, copy, sort or hash.
            for (int y = Math.Max(0, view.Y) / 64; y <= (view.Bottom - 1) / 64; y++)
                for (int x = Math.Max(0, view.X) / 64; x <= (view.Right - 1) / 64; x++)
                {
                    List<long> values;
                    if (!buckets.TryGetValue(WorldObject.PositionKey(x, y), out values)) continue;
                    for (int i = 0; i < values.Count; i++)
                    {
                        long key = values[i]; int px = (int)(key >> 32), py = (int)key;
                        if (px >= view.X && px < view.Right && py >= view.Y && py < view.Bottom) yield return key;
                    }
                }
        }
    }
}
