using System;
using System.Globalization;
using System.Text;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.AfkMode
{
    // Only preferences survive a session. The armed source, target and actual
    // operation/completion receipts belong exclusively to the live Host owner.
    public readonly struct AfkPreferences : IEquatable<AfkPreferences>
    {
        public bool Enabled { get; }
        public int GapMilliseconds { get; }
        public AfkPreferences(bool enabled, int gapMilliseconds)
        { if (gapMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(gapMilliseconds)); Enabled=enabled; GapMilliseconds=gapMilliseconds; }
        public bool Equals(AfkPreferences other) { return Enabled==other.Enabled && GapMilliseconds==other.GapMilliseconds; }
        public override bool Equals(object obj) { return obj is AfkPreferences && Equals((AfkPreferences)obj); }
        public override int GetHashCode() { return GapMilliseconds ^ (Enabled?int.MinValue:0); }
    }

    public sealed class AfkPreferenceCodec : IPreferenceCodec<AfkPreferences>
    {
        public AfkPreferences Decode(byte[] contents)
        {
            var root=PreferenceJson.Read(contents,"JueMingR.AfkMode","format","version","enabled","gapMs");
            string enabled=PreferenceJson.Required(root,"enabled","boolean").Value;
            int gap;
            if(enabled!="true" && enabled!="false" || !int.TryParse(PreferenceJson.Required(root,"gapMs","number").Value,NumberStyles.None,CultureInfo.InvariantCulture,out gap) || gap<0)
                throw PreferenceJson.Invalid();
            return new AfkPreferences(enabled=="true",gap);
        }
        public byte[] Encode(AfkPreferences value)
        { return new UTF8Encoding(false,true).GetBytes("{\"format\":\"JueMingR.AfkMode\",\"version\":1,\"enabled\":"+(value.Enabled?"true":"false")+",\"gapMs\":"+value.GapMilliseconds.ToString(CultureInfo.InvariantCulture)+"}\n"); }
    }
}
