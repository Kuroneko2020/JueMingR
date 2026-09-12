using System;
using System.Collections.Generic;
using JueMingR.Platform.WorldTargets;

namespace JueMingR.Features.WorldObjectText
{
    public enum OpenedQueryStep { Pending, Position, End }
    public sealed class OpenedPositionQuery
    {
        private readonly OpenedPositionIndex[] sources;
        private readonly WorldTargetView view;
        private readonly int left, right, top, bottom;
        private int source, x, y, offset;
        private List<long> bucket;
        internal OpenedPositionQuery(OpenedPositionIndex[] sources, WorldTargetView view)
        {
            this.sources = sources; this.view = view; left = Math.Max(0, view.X) / 64; right = (view.Right - 1) / 64; top = Math.Max(0, view.Y) / 64; bottom = (view.Bottom - 1) / 64;
            x = left; y = top;
            if (view.Width <= 0 || view.Height <= 0 || view.Width > 1024 || view.Height > 1024) source = sources.Length;
        }
        public long Current { get; private set; }
        public int WorkUsed { get; private set; }
        public OpenedQueryStep MoveNext(int budget)
        {
            if (budget < 1 || budget > 65536) throw new ArgumentOutOfRangeException(nameof(budget));
            WorkUsed = 0;
            while (source < sources.Length && WorkUsed < budget)
            {
                WorkUsed++;
                if (sources[source] == null) { NextSource(); continue; }
                if (bucket == null)
                { bucket = sources[source].Bucket(x, y); offset = 0; if (bucket == null) NextBucket(); continue; }
                if (offset == bucket.Count) { bucket = null; NextBucket(); continue; }
                long key = bucket[offset++]; int px = (int)(key >> 32), py = (int)key;
                if (px < view.X || px >= view.Right || py < view.Y || py >= view.Bottom) continue;
                bool duplicate = false;
                // At most baseline + accepted + eight frozen segments + delta.
                // Duplicate and out-of-rect visits both spend this raw budget.
                for (int i = 0; i < source; i++) if (sources[i] != null && sources[i].Contains(key)) { duplicate = true; break; }
                if (duplicate) continue;
                Current = key; return OpenedQueryStep.Position;
            }
            return source == sources.Length ? OpenedQueryStep.End : OpenedQueryStep.Pending;
        }
        private void NextBucket() { x++; if (x <= right) return; x = left; y++; if (y > bottom) NextSource(); }
        private void NextSource() { source++; x = left; y = top; bucket = null; offset = 0; }
    }
}
