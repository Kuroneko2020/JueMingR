namespace JueMingR.Platform.WorldTargets
{
    public enum WorldTargetKind { LifeCrystal, LifeFruit, ManaCrystal, SleepingDigtoise, ChilletEgg }

    // Synchronous game-thread values. Unknown client cells are not empty cells.
    public struct WorldTargetTile
    {
        public bool Readable, Active, Inactive;
        public int Type, FrameX, FrameY;
    }
    public struct WorldTargetView
    {
        public WorldTargetView(int x, int y, int width, int height, long readRevision, long geometryRevision = 0)
        { X = x; Y = y; Width = width; Height = height; ReadRevision = readRevision; GeometryRevision = geometryRevision; }
        public int X, Y, Width, Height;
        public long ReadRevision;
        public long GeometryRevision;
        public int Right { get { return X + Width; } }
        public int Bottom { get { return Y + Height; } }
        public bool Intersects(WorldTargetView other)
        { return X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y; }
    }
    public interface IWorldTargetSource
    {
        bool TryBegin(out WorldTargetView view);
        WorldTargetTile Read(int x, int y);
    }
}
