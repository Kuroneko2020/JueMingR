using System;

namespace JueMingR.Platform.Settings
{
    public sealed class WindowPosition : IEquatable<WindowPosition>
    {
        public WindowPosition(int x, int y) { X = x; Y = y; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public bool Equals(WindowPosition other) { return other != null && X == other.X && Y == other.Y; }
        public override bool Equals(object obj) { return Equals(obj as WindowPosition); }
        public override int GetHashCode() { return unchecked(X * 397 ^ Y); }
    }
}
