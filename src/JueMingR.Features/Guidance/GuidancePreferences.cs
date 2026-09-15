using System;

namespace JueMingR.Features.Guidance
{
    public enum GuidanceKind { Rare, Merchant, Equipment }
    public sealed class GuidanceStyle : IEquatable<GuidanceStyle>
    {
        public GuidanceStyle(int rgb, int size)
        { if (rgb < 0 || rgb > 0xFFFFFF || size < 50 || size > 180) throw new ArgumentOutOfRangeException(); Rgb = rgb; Size = size; }
        public int Rgb { get; }
        public int Size { get; }
        public GuidanceStyle WithColor(int rgb) { return new GuidanceStyle(rgb, Size); }
        public GuidanceStyle Step(int direction) { return new GuidanceStyle(Rgb, Math.Max(50, Math.Min(180, Size + Math.Sign(direction) * 10))); }
        public bool Equals(GuidanceStyle other) { return other != null && Rgb == other.Rgb && Size == other.Size; }
        public override bool Equals(object other) { return Equals(other as GuidanceStyle); }
        public override int GetHashCode() { return Rgb ^ Size; }
    }
    public sealed class GuidancePreferences : IEquatable<GuidancePreferences>
    {
        private static readonly GuidanceStyle defaultStyle = new GuidanceStyle(0xFFE060, 100);
        private readonly GuidanceStyle rare, merchant;
        public static readonly GuidancePreferences Default = new GuidancePreferences(0);
        public GuidancePreferences(int mask) : this(mask, defaultStyle, defaultStyle) { }
        public GuidancePreferences(int mask, GuidanceStyle rare, GuidanceStyle merchant)
        {
            if ((mask & ~7) != 0) throw new ArgumentOutOfRangeException(nameof(mask));
            Mask = mask; this.rare = rare ?? throw new ArgumentNullException(nameof(rare)); this.merchant = merchant ?? throw new ArgumentNullException(nameof(merchant));
        }
        public int Mask { get; }
        public GuidanceStyle Style(GuidanceKind kind)
        { if (kind != GuidanceKind.Rare && kind != GuidanceKind.Merchant) throw new ArgumentOutOfRangeException(nameof(kind)); return kind == GuidanceKind.Rare ? rare : merchant; }
        public GuidancePreferences WithStyle(GuidanceKind kind, GuidanceStyle style)
        { Style(kind); return new GuidancePreferences(Mask, kind == GuidanceKind.Rare ? style : rare, kind == GuidanceKind.Merchant ? style : merchant); }
        public GuidancePreferences ResetStyle(GuidanceKind kind) { return WithStyle(kind, Default.Style(kind)); }
        public bool Enabled(GuidanceKind kind) { return (Mask & 1 << (int)kind) != 0; }
        public GuidancePreferences WithEnabled(GuidanceKind kind, bool value)
        {
            if (kind < GuidanceKind.Rare || kind > GuidanceKind.Equipment) throw new ArgumentOutOfRangeException(nameof(kind));
            int bit = 1 << (int)kind; return new GuidancePreferences(value ? Mask | bit : Mask & ~bit, rare, merchant);
        }
        public bool Equals(GuidancePreferences other) { return other != null && Mask == other.Mask && rare.Equals(other.rare) && merchant.Equals(other.merchant); }
        public override bool Equals(object other) { return Equals(other as GuidancePreferences); }
        public override int GetHashCode() { return Mask ^ rare.GetHashCode() ^ merchant.GetHashCode(); }
    }
}
