using System.Text;
using System.Xml.Linq;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Biomes
{
    public sealed class BiomePreferenceCodec : IPreferenceCodec<bool>
    {
        public bool Decode(byte[] contents)
        {
            XElement root = PreferenceJson.Read(contents, "JueMingR.BiomeDisplay", "format", "version", "enabled");
            XElement enabled = PreferenceJson.Required(root, "enabled", "boolean");
            if (enabled.Value != "true" && enabled.Value != "false") throw PreferenceJson.Invalid();
            return enabled.Value == "true";
        }
        public byte[] Encode(bool value)
        {
            return new UTF8Encoding(false, true).GetBytes("{\"format\":\"JueMingR.BiomeDisplay\",\"version\":1,\"enabled\":" +
                (value ? "true" : "false") + "}\n");
        }
    }
}
