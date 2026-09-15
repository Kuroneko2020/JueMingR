using System;
using System.Globalization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Guidance
{
    public sealed class GuidancePreferenceCodec : IPreferenceCodec<GuidancePreferences>
    {
        public GuidancePreferences Decode(byte[] bytes) { int ignored; return Decode(bytes, out ignored); }
        public GuidancePreferences Decode(byte[] bytes, out int sourceVersion)
        {
            sourceVersion = 0;
            try
            {
                if (bytes == null || bytes.Length == 0 || bytes.Length > PreferenceJson.MaximumBytes) throw PreferenceJson.Invalid();
                new UTF8Encoding(false, true).GetCharCount(bytes);
                var quotas = new XmlDictionaryReaderQuotas { MaxDepth = 4, MaxArrayLength = 32, MaxStringContentLength = PreferenceJson.MaximumBytes, MaxBytesPerRead = 4096, MaxNameTableCharCount = 1024 };
                using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, quotas))
                {
                    var root = XElement.Load(reader);
                    foreach (var node in root.DescendantsAndSelf()) foreach (var attribute in node.Attributes())
                        if (attribute.Name != "type") throw new PreferenceFormatException(PreferenceStatus.UnknownFields, "Unknown guidance fields are protected.");
                    var format = PreferenceJson.Required(root, "format", "string");
                    if ((string)root.Attribute("type") != "object" || format.HasElements || format.Value != "JueMingR.Guidance") throw PreferenceJson.Invalid();
                    int version = Number(root, "version");
                    if (version != 1 && version != 2) throw new PreferenceFormatException(PreferenceStatus.UnsupportedVersion, "Unsupported guidance version.");
                    GuidancePreferences value;
                    if (version == 1)
                    {
                        PreferenceJson.ExactFields(root, "format", "version", "enabled");
                        // Only the known pre-style format may supply defaults.
                        value = new GuidancePreferences(Number(root, "enabled"));
                    }
                    else
                    {
                        PreferenceJson.ExactFields(root, "format", "version", "enabled", "rare", "merchant");
                        value = new GuidancePreferences(Number(root, "enabled"), Style(root, "rare"), Style(root, "merchant"));
                    }
                    sourceVersion = version; return value;
                }
            }
            catch (PreferenceFormatException) { throw; }
            catch (Exception e) when (e is XmlException || e is ArgumentException || e is InvalidOperationException) { throw PreferenceJson.Invalid(); }
        }
        public byte[] Encode(GuidancePreferences value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            string json = "{\"format\":\"JueMingR.Guidance\",\"version\":2,\"enabled\":" + Integer(value.Mask);
            foreach (var kind in new[] { GuidanceKind.Rare, GuidanceKind.Merchant })
            {
                var style = value.Style(kind);
                json += ",\"" + (kind == GuidanceKind.Rare ? "rare" : "merchant") + "\":{\"rgb\":" + Integer(style.Rgb) + ",\"size\":" + Integer(style.Size) + "}";
            }
            return new UTF8Encoding(false, true).GetBytes(json + "}\n");
        }
        private static GuidanceStyle Style(XElement root, string name)
        { var child = PreferenceJson.Required(root, name, "object"); PreferenceJson.ExactFields(child, "rgb", "size"); return new GuidanceStyle(Number(child, "rgb"), Number(child, "size")); }
        private static int Number(XElement root, string name) { return PreferenceJson.Integer(PreferenceJson.Required(root, name, "number")); }
        private static string Integer(int value) { return value.ToString(CultureInfo.InvariantCulture); }
    }
}
