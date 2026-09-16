using System;

namespace JueMingR.Platform.Footprints
{
    public enum FootprintPosition : byte { Valid, Dead, Ghost, Unavailable }

    // Times are whole completed simulation steps (60/s), never UTC ticks.
    // Sequence is adjacency, Segment is continuity; a file boundary is neither.
    public struct FootprintSample
    {
        public FootprintSample(long sequence, long start, long end, long segment, float x, float y, FootprintPosition position)
        { Sequence = sequence; Start = start; End = end; Segment = segment; X = position == FootprintPosition.Valid ? x : 0; Y = position == FootprintPosition.Valid ? y : 0; Position = position; }
        public long Sequence { get; }
        public long Start { get; }
        public long End { get; }
        public long Segment { get; }
        public float X { get; }
        public float Y { get; }
        public FootprintPosition Position { get; }
        public bool HasPosition { get { return Position == FootprintPosition.Valid; } }
        public FootprintSample Extend(long end) { return new FootprintSample(Sequence, Start, end, Segment, X, Y, Position); }
        public bool SameIdentity(FootprintSample other)
        { return Sequence == other.Sequence && Start == other.Start && Segment == other.Segment && X == other.X && Y == other.Y && Position == other.Position; }
        public bool Follows(FootprintSample previous)
        { return HasPosition && previous.HasPosition && Sequence == previous.Sequence + 1 && Segment == previous.Segment && Start == previous.End; }
        public static bool Finite(float value) { return !Single.IsNaN(value) && !Single.IsInfinity(value); }
    }
}
