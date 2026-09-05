using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace JueMingR.Platform.Settings
{
    public sealed class UiPreferenceCodec : IPreferenceCodec<WindowPosition>
    {
        public WindowPosition Decode(byte[] contents)
        {
            XElement root = PreferenceJson.Read(contents, "JueMingR.Ui", "format", "version", "windowPosition");
            XElement position = root.Element("windowPosition");
            if ((string)position.Attribute("type") == "null") return null;
            position = PreferenceJson.Required(root, "windowPosition", "object");
            PreferenceJson.ExactFields(position, "x", "y");
            return new WindowPosition(PreferenceJson.Integer(PreferenceJson.Required(position, "x", "number")),
                PreferenceJson.Integer(PreferenceJson.Required(position, "y", "number")));
        }
        public byte[] Encode(WindowPosition value)
        {
            // Coordinates are integral Terraria UI logical values, never desktop pixels.
            string position = value == null ? "null" : "{\"x\":" + value.X.ToString(CultureInfo.InvariantCulture) +
                ",\"y\":" + value.Y.ToString(CultureInfo.InvariantCulture) + "}";
            return new UTF8Encoding(false, true).GetBytes("{\"format\":\"JueMingR.Ui\",\"version\":1,\"windowPosition\":" + position + "}\n");
        }
    }
}
