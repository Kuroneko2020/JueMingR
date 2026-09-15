using System;
using System.Globalization;
using System.Text;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.WorldTime
{
    internal sealed class WorldTimeValue
    {
        internal WorldTimeValue(double total)
        { if (Double.IsNaN(total) || Double.IsInfinity(total) || total < 0 || total > 86400d * 1000000000) throw new ArgumentException("invalid-world-time-total"); Total = total; }
        internal double Total { get; }
        internal static WorldTimeValue Decode(byte[] bytes, string pair)
        {
            var root = PreferenceJson.Read(bytes, "JueMingR.WorldTime", "format", "version", "pair", "total"); double total;
            if (PreferenceJson.Required(root, "pair", "string").Value != pair || !Double.TryParse(PreferenceJson.Required(root, "total", "string").Value, NumberStyles.Float, CultureInfo.InvariantCulture, out total)) throw PreferenceJson.Invalid();
            return new WorldTimeValue(total);
        }
        internal byte[] Encode(string pair)
        { return Encoding.UTF8.GetBytes("{\"format\":\"JueMingR.WorldTime\",\"version\":1,\"pair\":\"" + pair + "\",\"total\":\"" + Total.ToString("R", CultureInfo.InvariantCulture) + "\"}"); }
    }
}
