using System;
using JueMingR.Platform.WorldObjectText;

namespace JueMingR.Features.WorldObjectText
{
    public sealed class WorldObjectStyle : IEquatable<WorldObjectStyle>
    {
        public WorldObjectStyle(WorldObjectKind kind, WorldObjectMode mode, WorldObjectMode lastMode, int rgb, int size, int lines, int characters)
        {
            if (!ValidMode(kind, mode) || lastMode == WorldObjectMode.Off || !ValidMode(kind, lastMode) ||
                rgb < 0 || rgb > 0xFFFFFF || size < 50 || size > 180 || lines < 1 || lines > 10 || characters < 1 || characters > 1200)
                throw new ArgumentOutOfRangeException(nameof(mode));
            Kind = kind; Mode = mode; LastMode = lastMode; Rgb = rgb; Size = size; Lines = lines; Characters = characters;
        }
        public WorldObjectKind Kind { get; }
        public WorldObjectMode Mode { get; }
        public WorldObjectMode LastMode { get; }
        public int Rgb { get; }
        public int Size { get; }
        public int Lines { get; }
        public int Characters { get; }
        public static bool ValidMode(WorldObjectKind kind, WorldObjectMode mode)
        { return (int)kind >= 0 && (int)kind <= 2 && (mode == WorldObjectMode.Off || (kind == WorldObjectKind.Chest ? mode == WorldObjectMode.Always || mode == WorldObjectMode.Opened : mode == WorldObjectMode.All || mode == WorldObjectMode.Lines || mode == WorldObjectMode.Characters)); }
        public WorldObjectStyle WithMode(WorldObjectMode mode)
        { return new WorldObjectStyle(Kind, mode, mode == WorldObjectMode.Off ? LastMode : mode, Rgb, Size, Lines, Characters); }
        public WorldObjectStyle WithColor(int rgb) { return new WorldObjectStyle(Kind, Mode, LastMode, rgb, Size, Lines, Characters); }
        public WorldObjectStyle WithSize(int size) { return new WorldObjectStyle(Kind, Mode, LastMode, Rgb, size, Lines, Characters); }
        public WorldObjectStyle WithLimits(int lines, int characters) { return new WorldObjectStyle(Kind, Mode, LastMode, Rgb, Size, lines, characters); }
        public bool Equals(WorldObjectStyle other) { return other != null && Kind == other.Kind && Mode == other.Mode && LastMode == other.LastMode && Rgb == other.Rgb && Size == other.Size && Lines == other.Lines && Characters == other.Characters; }
        public override bool Equals(object obj) { return Equals(obj as WorldObjectStyle); }
        public override int GetHashCode() { return unchecked((int)Mode + (int)LastMode * 7 + Rgb * 31 + Size * 101 + Lines * 11 + Characters); }
    }
    public sealed class WorldObjectSettings : IEquatable<WorldObjectSettings>
    {
        private readonly WorldObjectStyle[] values;
        public static readonly WorldObjectSettings Default = new WorldObjectSettings(new[] {
            new WorldObjectStyle(WorldObjectKind.Chest, WorldObjectMode.Off, WorldObjectMode.Opened, 0xFFA500, 70, 3, 80),
            new WorldObjectStyle(WorldObjectKind.Sign, WorldObjectMode.Off, WorldObjectMode.Lines, 0xE6C16A, 70, 3, 80),
            new WorldObjectStyle(WorldObjectKind.Tombstone, WorldObjectMode.Off, WorldObjectMode.Lines, 0xFF5555, 70, 3, 80) });
        private WorldObjectSettings(WorldObjectStyle[] values) { this.values = values; }
        public WorldObjectStyle Style(WorldObjectKind kind) { return values[(int)kind]; }
        public bool AnyEnabled { get { return values[0].Mode != WorldObjectMode.Off || values[1].Mode != WorldObjectMode.Off || values[2].Mode != WorldObjectMode.Off; } }
        public WorldObjectSettings With(WorldObjectStyle value)
        { var next = (WorldObjectStyle[])values.Clone(); next[(int)value.Kind] = value; return new WorldObjectSettings(next); }
        public WorldObjectSettings WithMode(WorldObjectKind kind, WorldObjectMode mode) { return With(Style(kind).WithMode(mode)); }
        public WorldObjectSettings Toggle(WorldObjectKind kind)
        { var style = Style(kind); return WithMode(kind, style.Mode == WorldObjectMode.Off ? style.LastMode : WorldObjectMode.Off); }
        public bool Equals(WorldObjectSettings other)
        { return other != null && values[0].Equals(other.values[0]) && values[1].Equals(other.values[1]) && values[2].Equals(other.values[2]); }
        public override bool Equals(object obj) { return Equals(obj as WorldObjectSettings); }
        public override int GetHashCode() { return unchecked(values[0].GetHashCode() * 31 + values[1].GetHashCode() * 7 + values[2].GetHashCode()); }
    }
}
