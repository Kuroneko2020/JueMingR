using System.Text;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Footprints
{
    public struct FootprintPreferences
    {
        public FootprintPreferences(bool recording, bool display) { Recording = recording; Display = display; }
        public bool Recording { get; }
        public bool Display { get; }
    }
    public sealed class FootprintPreferenceCodec : IPreferenceCodec<FootprintPreferences>
    {
        public FootprintPreferences Decode(byte[] bytes)
        {
            var root = PreferenceJson.Read(bytes, "JueMingR.FootprintPreferences", "format", "version", "recording", "display");
            string recording = PreferenceJson.Required(root, "recording", "boolean").Value, display = PreferenceJson.Required(root, "display", "boolean").Value;
            if (recording != "true" && recording != "false" || display != "true" && display != "false") throw PreferenceJson.Invalid();
            return new FootprintPreferences(recording == "true", display == "true");
        }
        public byte[] Encode(FootprintPreferences value)
        { return Encoding.UTF8.GetBytes("{\"format\":\"JueMingR.FootprintPreferences\",\"version\":1,\"recording\":" + (value.Recording ? "true" : "false") + ",\"display\":" + (value.Display ? "true" : "false") + "}"); }
    }
}
