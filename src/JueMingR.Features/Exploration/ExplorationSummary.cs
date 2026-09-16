using System;
using System.Globalization;
using System.Text;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Exploration
{
    // A persisted complete result is historical. Loading it never restores live
    // continuity or a scan cursor, even for the same pair and dimensions.
    public sealed class ExplorationSummary
    {
        public ExplorationSummary(int width, int height, long count, long utcTicks)
        {
            if (width <= 0 || height <= 0 || width > 20000 || height > 20000 || count < 0 || count > (long)width * height || utcTicks <= 0 || utcTicks > DateTime.MaxValue.Ticks) throw new ArgumentException("exploration-summary-invalid");
            Width = width; Height = height; Count = count; UtcTicks = utcTicks;
        }
        public int Width { get; }
        public int Height { get; }
        public long Count { get; }
        public long UtcTicks { get; }
        public static ExplorationSummary Decode(byte[] bytes, string pair, int width, int height)
        {
            var root = PreferenceJson.Read(bytes, "JueMingR.Exploration", "format", "version", "algorithm", "pair", "width", "height", "complete", "count", "utcTicks");
            if (PreferenceJson.Integer(PreferenceJson.Required(root, "algorithm", "number")) != 1 || PreferenceJson.Required(root, "pair", "string").Value != pair ||
                PreferenceJson.Required(root, "complete", "boolean").Value != "true" || PreferenceJson.Integer(PreferenceJson.Required(root, "width", "number")) != width || PreferenceJson.Integer(PreferenceJson.Required(root, "height", "number")) != height) throw PreferenceJson.Invalid();
            long count, ticks;
            if (!Int64.TryParse(PreferenceJson.Required(root, "count", "string").Value, NumberStyles.None, CultureInfo.InvariantCulture, out count) || !Int64.TryParse(PreferenceJson.Required(root, "utcTicks", "string").Value, NumberStyles.None, CultureInfo.InvariantCulture, out ticks)) throw PreferenceJson.Invalid();
            return new ExplorationSummary(width, height, count, ticks);
        }
        public byte[] Encode(string pair)
        {
            return Encoding.UTF8.GetBytes(String.Format(CultureInfo.InvariantCulture, "{{\"format\":\"JueMingR.Exploration\",\"version\":1,\"algorithm\":1,\"pair\":\"{0}\",\"width\":{1},\"height\":{2},\"complete\":true,\"count\":\"{3}\",\"utcTicks\":\"{4}\"}}", pair, Width, Height, Count, UtcTicks));
        }
    }
    public sealed class ExplorationPreferenceCodec : IPreferenceCodec<bool>
    {
        public bool Decode(byte[] bytes)
        {
            var root = PreferenceJson.Read(bytes, "JueMingR.ExplorationDynamic", "format", "version", "enabled");
            string value = PreferenceJson.Required(root, "enabled", "boolean").Value; if (value != "true" && value != "false") throw PreferenceJson.Invalid(); return value == "true";
        }
        public byte[] Encode(bool value) { return Encoding.UTF8.GetBytes("{\"format\":\"JueMingR.ExplorationDynamic\",\"version\":1,\"enabled\":" + (value ? "true" : "false") + "}"); }
    }
}
