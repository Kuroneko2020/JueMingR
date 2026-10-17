using System;
using System.Diagnostics;

namespace JueMingR.TerrariaHost.Footprints
{
    // This clock belongs only to footprint recording. The anchor spans normal
    // frame waits, but is discarded at a real native pause/session boundary.
    internal sealed class FootprintPlayingClock
    {
        private readonly Func<long> timestamp;
        private readonly long frequency;
        private long anchor;
        private decimal remainder;
        private bool anchored;
        internal FootprintPlayingClock() : this(Stopwatch.GetTimestamp, Stopwatch.Frequency) { }
        internal FootprintPlayingClock(Func<long> timestamp, long frequency)
        { if (timestamp == null || frequency <= 0) throw new ArgumentException("footprint-clock-source"); this.timestamp = timestamp; this.frequency = frequency; }
        internal void Begin()
        { if (!anchored) { anchor = timestamp(); anchored = true; } }
        internal long Completed()
        {
            if (!anchored) return 0;
            long now = timestamp(), elapsed = Math.Max(0, now - anchor); anchor = now;
            // Keep the fractional conversion across callbacks. Raw hardware
            // ticks are never persisted, and same-time callbacks add no time.
            decimal numerator = (decimal)elapsed * TimeSpan.TicksPerSecond + remainder;
            long whole = checked((long)Decimal.Truncate(numerator / frequency)); remainder = numerator % frequency; return whole;
        }
        internal void Suspend() { anchored = false; remainder = 0; }
    }
}
