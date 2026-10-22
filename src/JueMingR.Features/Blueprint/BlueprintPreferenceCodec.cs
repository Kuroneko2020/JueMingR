using System.Text;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Blueprint
{
    public sealed class BlueprintPreferenceCodec : IPreferenceCodec<bool>
    {
        public bool Decode(byte[] contents)
        {
            var root = PreferenceJson.Read(contents, "JueMingR.Blueprint", "format", "version", "gelToolbar");
            string value = PreferenceJson.Required(root, "gelToolbar", "boolean").Value;
            if (value != "true" && value != "false") throw PreferenceJson.Invalid();
            return value == "true";
        }
        public byte[] Encode(bool value)
        { return new UTF8Encoding(false, true).GetBytes("{\"format\":\"JueMingR.Blueprint\",\"version\":1,\"gelToolbar\":" + (value ? "true" : "false") + "}\n"); }
    }
}
