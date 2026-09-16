using System;
using System.Text;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.MapMarkers
{
    public sealed class MarkerPreferenceCodec : IPreferenceCodec<bool>
    {
        public bool Decode(byte[] bytes)
        {
            var root = PreferenceJson.Read(bytes, "JueMingR.MapMarkersDisplay", "format", "version", "enabled");
            string value = PreferenceJson.Required(root, "enabled", "boolean").Value;
            if (value != "true" && value != "false") throw PreferenceJson.Invalid(); return value == "true";
        }
        public byte[] Encode(bool value)
        { return Encoding.UTF8.GetBytes("{\"format\":\"JueMingR.MapMarkersDisplay\",\"version\":1,\"enabled\":" + (value ? "true" : "false") + "}"); }
    }
}
