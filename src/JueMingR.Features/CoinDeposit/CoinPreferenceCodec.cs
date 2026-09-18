using System.Text;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.CoinDeposit
{
    public sealed class CoinPreferenceCodec : IPreferenceCodec<bool>
    {
        public bool Decode(byte[] contents)
        {
            var root = PreferenceJson.Read(contents, "JueMingR.CoinDeposit", "format", "version", "enabled");
            var value = PreferenceJson.Required(root, "enabled", "boolean").Value;
            if (value != "true" && value != "false") throw PreferenceJson.Invalid();
            return value == "true";
        }
        public byte[] Encode(bool value)
        { return new UTF8Encoding(false, true).GetBytes("{\"format\":\"JueMingR.CoinDeposit\",\"version\":1,\"enabled\":" + (value ? "true" : "false") + "}\n"); }
    }
}
