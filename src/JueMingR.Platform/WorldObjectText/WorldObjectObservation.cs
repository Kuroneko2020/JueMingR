namespace JueMingR.Platform.WorldObjectText
{
    public enum WorldObjectKind { Chest, Sign, Tombstone }
    public enum WorldObjectMode { Off, Always, Opened, All, Lines, Characters }
    public enum ContainerNameFamily { Chest, Chest2, Dresser, Item }
    public struct WorldObjectView
    {
        public WorldTargets.WorldTargetView Visible, Discovery;
        public float PlayerX, PlayerY;
    }
    public interface IWorldObjectSource
    {
        bool HasDetector { get; }
        bool TryBegin(bool chestNames, bool signText, out WorldObjectView view);
        WorldTargets.WorldTargetTile Read(int x, int y);
        bool TryText(WorldObject value, out string text);
    }

    // Values belong to one Runtime session. A position becomes durable only via
    // the opened-position owner; geometry/name observations never become history.
    public struct WorldObject
    {
        public WorldObjectKind Kind;
        public int TileX, TileY, Type, Style, Width;
        public long Key { get { return PositionKey(TileX, TileY); } }
        public float CenterX { get { return TileX * 16f + Width * 8f; } }
        public float CenterY { get { return TileY * 16f + 16; } }
        public static long PositionKey(int x, int y) { return ((long)x << 32) | (uint)y; }
    }
}
