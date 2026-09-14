using System;
using JueMingR.Platform.Information;

namespace JueMingR.Features.Information
{
    public sealed class InformationStyle : IEquatable<InformationStyle>
    {
        public InformationStyle(int rgb, int size)
        {
            if (rgb < 0 || rgb > 0xFFFFFF || size < 50 || size > 180) throw new ArgumentOutOfRangeException();
            Rgb = rgb; Size = size;
        }
        public int Rgb { get; private set; }
        public int Size { get; private set; }
        public InformationStyle WithColor(int rgb) { return new InformationStyle(rgb, Size); }
        public InformationStyle Step(int direction) { return new InformationStyle(Rgb, Math.Max(50, Math.Min(180, Size + Math.Sign(direction) * 10))); }
        public bool Equals(InformationStyle other) { return other != null && Rgb == other.Rgb && Size == other.Size; }
        public override bool Equals(object other) { return Equals(other as InformationStyle); }
        public override int GetHashCode() { return Rgb ^ Size; }
    }

    public sealed class InformationPreferences : IEquatable<InformationPreferences>
    {
        private readonly InformationStyle[] styles;
        private readonly int enabled;
        public static readonly InformationPreferences Default = new InformationPreferences(0,
            new InformationStyle(0x90EE90, 82), new InformationStyle(0xDDA0DD, 82),
            new InformationStyle(0xFAFAD2, 82), new InformationStyle(0xE0FFFF, 82));
        public InformationPreferences(int enabled, InformationStyle biome, InformationStyle infection, InformationStyle luck, InformationStyle angler)
        {
            // Bit zero is deliberately absent: the existing biome document is
            // still the sole owner of its enabled preference.
            if ((enabled & ~14) != 0) throw new ArgumentOutOfRangeException(nameof(enabled));
            if (biome == null || infection == null || luck == null || angler == null) throw new ArgumentNullException();
            this.enabled = enabled; styles = new[] { biome, infection, luck, angler };
        }
        public int EnabledMask { get { return enabled; } }
        public bool AnySummaryEnabled { get { return enabled != 0; } }
        public bool Enabled(InformationKind kind) { Validate(kind); return kind != InformationKind.Biome && (enabled & 1 << (int)kind) != 0; }
        public InformationStyle Style(InformationKind kind) { Validate(kind); return styles[(int)kind]; }
        public InformationPreferences WithEnabled(InformationKind kind, bool value)
        {
            Validate(kind); if (kind == InformationKind.Biome) throw new ArgumentException("Biome enabled belongs to its existing document.");
            int bit = 1 << (int)kind;
            return new InformationPreferences(value ? enabled | bit : enabled & ~bit, styles[0], styles[1], styles[2], styles[3]);
        }
        public InformationPreferences WithStyle(InformationKind kind, InformationStyle style)
        {
            Validate(kind);
            return new InformationPreferences(enabled, kind == InformationKind.Biome ? style : styles[0],
                kind == InformationKind.Infection ? style : styles[1], kind == InformationKind.Luck ? style : styles[2], kind == InformationKind.Angler ? style : styles[3]);
        }
        public InformationPreferences ResetStyle(InformationKind kind) { return WithStyle(kind, Default.Style(kind)); }
        public bool Equals(InformationPreferences other)
        {
            if (other == null || enabled != other.enabled) return false;
            for (int i = 0; i < 4; i++) if (!styles[i].Equals(other.styles[i])) return false;
            return true;
        }
        public override bool Equals(object other) { return Equals(other as InformationPreferences); }
        public override int GetHashCode() { return enabled ^ styles[0].GetHashCode() ^ styles[1].GetHashCode() ^ styles[2].GetHashCode() ^ styles[3].GetHashCode(); }
        private static void Validate(InformationKind kind) { if (kind < InformationKind.Biome || kind > InformationKind.Angler) throw new ArgumentOutOfRangeException(nameof(kind)); }
    }
}
