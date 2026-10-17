using System;
using JueMingR.Platform.Footprints;

namespace JueMingR.Features.Footprints
{
    // Game-thread owner. Only the last unpublished interval may change. A save
    // freezes its endpoint; the next interval continues geographically from it.
    public sealed class FootprintRecorder
    {
        public const int Capacity = 256;
        public const double RecordErrorTiles = .125;
        private const double AxisError = RecordErrorTiles / 1.4142135623730951;
        private readonly FootprintSample[] active = new FootprintSample[Capacity];
        private readonly Func<FootprintSample[], int, bool> offer;
        private readonly Func<long> milliseconds;
        private int count;
        private long segment, dirtyAt;
        private bool dirty, breakNext = true, mutable;
        private double minX, maxX, minY, maxY;
        public FootprintRecorder(long count, long end, long segment, Func<FootprintSample[], int, bool> offer, Func<long> milliseconds)
        { if (count < 0 || end < 0 || segment < 0 || offer == null || milliseconds == null) throw new ArgumentException("footprint-recorder-input"); Count = count; End = end; this.segment = segment; this.offer = offer; this.milliseconds = milliseconds; }
        public long Count { get; private set; }
        public long End { get; private set; }
        public long GeometryVersion { get; private set; }
        public long BufferVersion { get; private set; }
        public bool Blocked { get; private set; }
        public bool Dirty { get { return dirty; } }
        public long Segment { get { return segment; } }
        public int ActiveCount { get { return count; } }
        public FootprintSample At(int index) { if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index)); return active[index]; }
        public void Break() { breakNext = true; mutable = false; }
        public bool Observe(float x, float y, FootprintPosition position, bool discontinuity, long elapsedTicks = 1)
        {
            if (elapsedTicks < 0 || (byte)position > 3 || !FootprintSample.Finite(x) || !FootprintSample.Finite(y)) throw new ArgumentException("footprint-observation");
            if (discontinuity) Break();
            // Repeated callbacks at the same monotonic instant add no invented time.
            if (elapsedTicks == 0) return true;
            if (Blocked && !Flush()) return false;
            var last = count == 0 ? default(FootprintSample) : active[count - 1];
            bool continuous = count > 0 && !breakNext && last.Position == position;
            long next = checked(End + elapsedTicks);
            bool stationary = continuous && (!last.HasPosition || last.X == x && last.Y == y);
            // A single unchanged float position can be quantization during very
            // slow movement. Let the time corridor decide whether that endpoint
            // is still valid; a real sustained stop fails it and creates Stay.
            bool extend = mutable && continuous && (stationary && !last.Moving || last.Moving && Fits(last, next, x, y));
            if (extend)
            {
                active[count - 1] = new FootprintSample(last.Sequence, last.Start, next, last.Segment, last.FromX, last.FromY, x, y, position);
                if (last.Moving) { Constrain(active[count - 1]); GeometryVersion++; BufferVersion++; }
            }
            else
            {
                if (count == Capacity && !Flush()) { Blocked = breakNext = true; return false; }
                // Flush may move the finite array but does not change the saved
                // endpoint used for this new interval, nor create a new segment.
                if (!continuous) segment = checked(segment + 1);
                float fromX = continuous && last.HasPosition ? last.X : x;
                float fromY = continuous && last.HasPosition ? last.Y : y;
                var value = new FootprintSample(checked(Count + 1), End, next, segment, fromX, fromY, x, y, position);
                active[count++] = value; Count++; GeometryVersion++; BufferVersion++;
                minX = minY = Double.NegativeInfinity; maxX = maxY = Double.PositiveInfinity;
                if (value.Moving) Constrain(value);
                mutable = true;
            }
            End = next; breakNext = false;
            if (!dirty) { dirtyAt = milliseconds(); dirty = true; }
            return true;
        }
        private bool Fits(FootprintSample anchor, long end, float x, float y)
        {
            double dt = end - anchor.Start;
            double vx = (x - (double)anchor.FromX) / dt, vy = (y - (double)anchor.FromY) / dt;
            return vx >= minX && vx <= maxX && vy >= minY && vy <= maxY;
        }
        private void Constrain(FootprintSample sample)
        {
            double dt = sample.End - sample.Start, x = sample.X - (double)sample.FromX, y = sample.Y - (double)sample.FromY;
            minX = Math.Max(minX, (x - AxisError) / dt); maxX = Math.Min(maxX, (x + AxisError) / dt);
            minY = Math.Max(minY, (y - AxisError) / dt); maxY = Math.Min(maxY, (y + AxisError) / dt);
        }
        public bool FlushDue() { return !dirty || milliseconds() - dirtyAt < 10000 || Flush(); }
        public bool Flush()
        {
            if (!dirty) { Blocked = false; return true; }
            if (!offer(active, count)) return false;
            if (count > 1) BufferVersion++;
            active[0] = active[count - 1]; count = 1; dirty = false; mutable = false; Blocked = false;
            return true;
        }
    }
}
