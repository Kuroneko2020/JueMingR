using System;
using JueMingR.Platform.Footprints;

namespace JueMingR.Features.Footprints
{
    // Game-thread owner. The accepting outlet must freeze the bounded array
    // before returning true; false leaves every previously accepted fact here.
    public sealed class FootprintRecorder
    {
        public const int Capacity = 256;
        private readonly FootprintSample[] active = new FootprintSample[Capacity];
        private readonly Func<FootprintSample[], int, bool> offer;
        private readonly Func<long> milliseconds;
        private int count;
        private long segment, dirtyAt;
        private bool dirty, breakNext = true;
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
        public void Break() { breakNext = true; }
        public bool Observe(float x, float y, FootprintPosition position, bool discontinuity, long steps = 1)
        {
            if (steps <= 0 || (byte)position > 3 || !FootprintSample.Finite(x) || !FootprintSample.Finite(y)) throw new ArgumentException("footprint-observation");
            if (Blocked && !Flush()) return false;
            FootprintSample last = count == 0 ? default(FootprintSample) : active[count - 1];
            bool same = count > 0 && !breakNext && !discontinuity && last.Position == position && (!last.HasPosition || last.X == x && last.Y == y);
            if (!same && count == Capacity && !Flush()) { Blocked = breakNext = true; return false; }
            long next = checked(End + steps);
            if (same) active[count - 1] = last.Extend(next);
            else
            {
                if (breakNext || discontinuity || count == 0 || last.Position != position) segment = checked(segment + 1);
                active[count++] = new FootprintSample(checked(Count + 1), End, next, segment, x, y, position); Count++; GeometryVersion++; BufferVersion++;
            }
            End = next; breakNext = false;
            if (!dirty) { dirtyAt = milliseconds(); dirty = true; }
            return true;
        }
        public bool FlushDue() { return !dirty || milliseconds() - dirtyAt < 10000 || Flush(); }
        public bool Flush()
        {
            if (!dirty) { Blocked = false; return true; }
            if (!offer(active, count)) return false;
            if (count > 1) BufferVersion++;
            active[0] = active[count - 1]; count = 1; dirty = false; Blocked = false;
            return true;
        }
    }
}
