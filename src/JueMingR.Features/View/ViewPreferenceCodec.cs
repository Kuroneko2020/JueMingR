using System.Text;
using System.Xml.Linq;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.View
{
    // Only the permission is a preference. The native zoom target remains native
    // state and must never be restored from this document.
    public sealed class ViewPreferenceCodec : IPreferenceCodec<bool>
    {
        public bool Decode(byte[] contents)
        {
            XElement root = PreferenceJson.Read(contents, "JueMingR.View", "format", "version", "enabled");
            XElement enabled = PreferenceJson.Required(root, "enabled", "boolean");
            if (enabled.Value != "true" && enabled.Value != "false") throw PreferenceJson.Invalid();
            return enabled.Value == "true";
        }

        public byte[] Encode(bool value)
        {
            return new UTF8Encoding(false, true).GetBytes("{\"format\":\"JueMingR.View\",\"version\":1,\"enabled\":" +
                (value ? "true" : "false") + "}\n");
        }
    }
}
