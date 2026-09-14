using System;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using JueMingR.Platform.Information;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Information
{
    public sealed class InformationPreferenceCodec : IPreferenceCodec<InformationPreferences>
    {
        public InformationPreferences Decode(byte[] contents)
        {
            var root = PreferenceJson.Read(contents, "JueMingR.InformationDisplay", "format", "version", "enabled", "biome", "infection", "luck", "angler");
            try { return new InformationPreferences(Number(root, "enabled"), Style(root, "biome"), Style(root, "infection"), Style(root, "luck"), Style(root, "angler")); }
            catch (ArgumentException) { throw PreferenceJson.Invalid(); }
        }
        public byte[] Encode(InformationPreferences value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            string json = "{\"format\":\"JueMingR.InformationDisplay\",\"version\":1,\"enabled\":" + Integer(value.EnabledMask);
            string[] names = { "biome", "infection", "luck", "angler" };
            for (int i = 0; i < 4; i++)
            {
                var style = value.Style((InformationKind)i);
                json += ",\"" + names[i] + "\":{\"rgb\":" + Integer(style.Rgb) + ",\"size\":" + Integer(style.Size) + "}";
            }
            return new UTF8Encoding(false, true).GetBytes(json + "}\n");
        }
        private static InformationStyle Style(XElement root, string name)
        {
            var item = PreferenceJson.Required(root, name, "object"); PreferenceJson.ExactFields(item, "rgb", "size");
            return new InformationStyle(Number(item, "rgb"), Number(item, "size"));
        }
        private static int Number(XElement root, string name) { return PreferenceJson.Integer(PreferenceJson.Required(root, name, "number")); }
        private static string Integer(int value) { return value.ToString(CultureInfo.InvariantCulture); }
    }
}
