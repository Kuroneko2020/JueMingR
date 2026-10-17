namespace JueMingR.Platform.Footprints
{
    public sealed class FootprintOverviewRequest
    {
        public FootprintOverviewRequest(string generation, long serial, float minX, float minY, float maxX, float maxY, float zoom, long until)
        { Generation = generation; Serial = serial; MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY; Zoom = zoom; Until = until; }
        public string Generation { get; }
        public long Serial { get; }
        public float MinX { get; }
        public float MinY { get; }
        public float MaxX { get; }
        public float MaxY { get; }
        public float Zoom { get; }
        public long Until { get; }
    }
    public struct FootprintOverviewEdge
    {
        public FootprintOverviewEdge(float x1, float y1, float x2, float y2, long start, long end, long segment, long firstSequence, long lastSequence)
        { X1 = x1; Y1 = y1; X2 = x2; Y2 = y2; Start = start; End = end; Segment = segment; FirstSequence = firstSequence; LastSequence = lastSequence; }
        public float X1 { get; }
        public float Y1 { get; }
        public float X2 { get; }
        public float Y2 { get; }
        public long Start { get; }
        public long End { get; }
        public long Segment { get; }
        public long FirstSequence { get; }
        public long LastSequence { get; }
    }
    // Immutable ownership transfer. Complete describes all committed source blocks,
    // never merely a full output buffer. The live recorder tail remains separate.
    public sealed class FootprintOverviewSnapshot
    {
        public FootprintOverviewSnapshot(string generation, long requestSerial, long until, long sourceSequence, long coveredEnd, bool complete, FootprintOverviewEdge[] edges)
        { Generation = generation; RequestSerial = requestSerial; Until = until; SourceSequence = sourceSequence; CoveredEnd = coveredEnd; Complete = complete; Edges = edges; }
        public string Generation { get; }
        public long RequestSerial { get; }
        public long Until { get; }
        public long SourceSequence { get; }
        public long CoveredEnd { get; }
        public bool Complete { get; }
        public FootprintOverviewEdge[] Edges { get; }
    }
    public interface IFootprintOverviewFiles
    {
        byte[] ReadOverviewCache(long number);
        void WriteOverviewCache(long number, byte[] bytes);
    }
}
