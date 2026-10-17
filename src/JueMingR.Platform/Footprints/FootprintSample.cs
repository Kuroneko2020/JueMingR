using System;

namespace JueMingR.Platform.Footprints
{
    public enum FootprintPosition : byte { Valid, Dead, Ghost, Unavailable }

    // Times are accumulated eligible real play time in TimeSpan ticks, never UTC.
    // Sequence is adjacency, Segment is continuity; a file boundary is neither.
    public struct FootprintSample
    {
        public FootprintSample(long sequence, long start, long end, long segment, float x, float y, FootprintPosition position)
            : this(sequence, start, end, segment, x, y, x, y, position) { }
        public FootprintSample(long sequence, long start, long end, long segment, float fromX, float fromY, float x, float y, FootprintPosition position)
        { Sequence = sequence; Start = start; End = end; Segment = segment; FromX = position == FootprintPosition.Valid ? fromX : 0; FromY = position == FootprintPosition.Valid ? fromY : 0; X = position == FootprintPosition.Valid ? x : 0; Y = position == FootprintPosition.Valid ? y : 0; Position = position; }
        public const long TicksPerSecond = TimeSpan.TicksPerSecond;
        public long Sequence { get; }
        public long Start { get; }
        public long End { get; }
        public long Segment { get; }
        public float X { get; }
        public float Y { get; }
        public float FromX { get; }
        public float FromY { get; }
        public FootprintPosition Position { get; }
        public bool HasPosition { get { return Position == FootprintPosition.Valid; } }
        public bool Moving { get { return HasPosition && (FromX != X || FromY != Y); } }
        public FootprintSample Extend(long end) { return new FootprintSample(Sequence, Start, end, Segment, FromX, FromY, X, Y, Position); }
        public bool SameIdentity(FootprintSample other)
        { return Sequence == other.Sequence && Start == other.Start && Segment == other.Segment && FromX == other.FromX && FromY == other.FromY && X == other.X && Y == other.Y && Position == other.Position; }
        public bool Follows(FootprintSample previous)
        { return HasPosition && previous.HasPosition && Sequence == previous.Sequence + 1 && Segment == previous.Segment && Start == previous.End && FromX == previous.X && FromY == previous.Y; }
        public static bool Finite(float value) { return !Single.IsNaN(value) && !Single.IsInfinity(value); }
    }
}
