using System;
using System.Globalization;
using System.Text;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Guidance
{
    public enum GuidanceKind { Rare, Merchant, Equipment }
    public sealed class GuidancePreferences : IEquatable<GuidancePreferences>
    {
        public static readonly GuidancePreferences Default = new GuidancePreferences(0);
        public GuidancePreferences(int mask) { if ((mask & ~7) != 0) throw new ArgumentOutOfRangeException(nameof(mask)); Mask = mask; }
        public int Mask { get; }
        public bool Enabled(GuidanceKind kind) { return (Mask & 1 << (int)kind) != 0; }
        public GuidancePreferences WithEnabled(GuidanceKind kind, bool value)
        {
            if (kind < GuidanceKind.Rare || kind > GuidanceKind.Equipment) throw new ArgumentOutOfRangeException(nameof(kind));
            int bit = 1 << (int)kind; return new GuidancePreferences(value ? Mask | bit : Mask & ~bit);
        }
        public bool Equals(GuidancePreferences other) { return other != null && Mask == other.Mask; }
        public override bool Equals(object other) { return Equals(other as GuidancePreferences); }
        public override int GetHashCode() { return Mask; }
    }
    public sealed class GuidancePreferenceCodec : IPreferenceCodec<GuidancePreferences>
    {
        public GuidancePreferences Decode(byte[] bytes)
        {
            var root = PreferenceJson.Read(bytes, "JueMingR.Guidance", "format", "version", "enabled");
            try { return new GuidancePreferences(PreferenceJson.Integer(PreferenceJson.Required(root, "enabled", "number"))); }
            catch (ArgumentException) { throw PreferenceJson.Invalid(); }
        }
        public byte[] Encode(GuidancePreferences value)
        { return new UTF8Encoding(false, true).GetBytes("{\"format\":\"JueMingR.Guidance\",\"version\":1,\"enabled\":" + value.Mask.ToString(CultureInfo.InvariantCulture) + "}\n"); }
    }
}
