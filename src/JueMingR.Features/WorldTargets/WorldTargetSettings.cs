using System;
using JueMingR.Platform.WorldTargets;

namespace JueMingR.Features.WorldTargets
{
    public sealed class WorldTargetSettings : IEquatable<WorldTargetSettings>
    {
        private readonly int[] colors;
        private readonly int enabled;
        public static readonly WorldTargetSettings Default = new WorldTargetSettings(0,
            new[] { 0xFF69B4, 0x7CFC00, 0x66CCFF, 0xFFC460, 0x9370DB });
        private WorldTargetSettings(int enabled, int[] colors) { this.enabled = enabled; this.colors = colors; }
        public bool AnyEnabled { get { return enabled != 0; } }
        public bool Enabled(WorldTargetKind kind) { return (enabled & (1 << Index(kind))) != 0; }
        public int Color(WorldTargetKind kind) { return colors[Index(kind)]; }
        public WorldTargetSettings WithEnabled(WorldTargetKind kind, bool value)
        { int bit = 1 << Index(kind); return new WorldTargetSettings(value ? enabled | bit : enabled & ~bit, colors); }
        public WorldTargetSettings Toggle(WorldTargetKind kind) { return WithEnabled(kind, !Enabled(kind)); }
        public WorldTargetSettings WithColor(WorldTargetKind kind, int rgb)
        {
            if (rgb < 0 || rgb > 0xFFFFFF) throw new ArgumentOutOfRangeException(nameof(rgb));
            int index = Index(kind); var next = (int[])colors.Clone(); next[index] = rgb;
            return new WorldTargetSettings(enabled, next);
        }
        public WorldTargetSettings ResetColor(WorldTargetKind kind) { return WithColor(kind, Default.Color(kind)); }
        private static int Index(WorldTargetKind kind)
        { int index = (int)kind; if (index < 0 || index > 4) throw new ArgumentOutOfRangeException(nameof(kind)); return index; }
        public bool Equals(WorldTargetSettings other)
        {
            if (other == null || enabled != other.enabled) return false;
            for (int i = 0; i < colors.Length; i++) if (colors[i] != other.colors[i]) return false;
            return true;
        }
        public override bool Equals(object obj) { return Equals(obj as WorldTargetSettings); }
        public override int GetHashCode() { int hash = enabled; foreach (int color in colors) hash = unchecked(hash * 31 + color); return hash; }
    }
}
