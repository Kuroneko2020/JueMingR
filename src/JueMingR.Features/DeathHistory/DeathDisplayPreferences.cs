using System;
using System.Globalization;
using System.Text;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.DeathHistory
{
    public sealed class DeathDisplayPreferences : IEquatable<DeathDisplayPreferences>
    {
        public static readonly DeathDisplayPreferences Default = new DeathDisplayPreferences(false, 256);
        public DeathDisplayPreferences(bool enabled, int count)
        { if (count != 128 && count != 256 && count != 512 && count != 1024) throw new ArgumentOutOfRangeException(nameof(count)); Enabled = enabled; Count = count; }
        public bool Enabled { get; }
        public int Count { get; }
        public bool Equals(DeathDisplayPreferences value) { return value != null && Enabled == value.Enabled && Count == value.Count; }
        public override bool Equals(object value) { return Equals(value as DeathDisplayPreferences); }
        public override int GetHashCode() { return Count ^ (Enabled ? 1 : 0); }
    }
    public sealed class DeathDisplayCodec : IPreferenceCodec<DeathDisplayPreferences>
    {
        public DeathDisplayPreferences Decode(byte[] bytes)
        {
            try
            {
                var root = PreferenceJson.Read(bytes, "JueMingR.DeathMarkers", "format", "version", "enabled", "count");
                string enabled = PreferenceJson.Required(root, "enabled", "boolean").Value;
                if (enabled != "true" && enabled != "false") throw PreferenceJson.Invalid();
                return new DeathDisplayPreferences(enabled == "true", PreferenceJson.Integer(PreferenceJson.Required(root, "count", "number")));
            }
            catch (ArgumentException) { throw PreferenceJson.Invalid(); }
        }
        public byte[] Encode(DeathDisplayPreferences value)
        { if (value == null) throw new ArgumentNullException(nameof(value)); return Encoding.UTF8.GetBytes("{\"format\":\"JueMingR.DeathMarkers\",\"version\":1,\"enabled\":" + (value.Enabled ? "true" : "false") + ",\"count\":" + value.Count.ToString(CultureInfo.InvariantCulture) + "}"); }
    }
}
