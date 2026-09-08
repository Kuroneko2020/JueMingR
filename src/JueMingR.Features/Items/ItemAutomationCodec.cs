using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Items
{
    public sealed class ItemAutomationCodec : IPreferenceCodec<ItemAutomationSettings>
    {
        private readonly int itemCount;
        public ItemAutomationCodec(int itemCount)
        { if (itemCount < 2340 || itemCount > 65536) throw new ArgumentOutOfRangeException(nameof(itemCount)); this.itemCount = itemCount; }

        public ItemAutomationSettings Decode(byte[] contents)
        {
            try
            {
                if (contents == null || contents.Length == 0 || contents.Length > PreferenceJson.MaximumBytes) throw PreferenceJson.Invalid();
                new UTF8Encoding(false, true).GetCharCount(contents);
                var quotas = new XmlDictionaryReaderQuotas { MaxDepth = 4, MaxArrayLength = itemCount,
                    MaxStringContentLength = PreferenceJson.MaximumBytes, MaxBytesPerRead = 4096, MaxNameTableCharCount = 1024 };
                using (XmlDictionaryReader scan = JsonReaderWriterFactory.CreateJsonReader(contents, quotas))
                {
                    int nodes = 0;
                    while (scan.Read()) if (scan.NodeType == XmlNodeType.Element && ++nodes > itemCount * 2 + 11) throw PreferenceJson.Invalid();
                }
                using (XmlDictionaryReader reader = JsonReaderWriterFactory.CreateJsonReader(contents, quotas))
                {
                    XElement root = XElement.Load(reader);
                    foreach (XElement node in root.DescendantsAndSelf())
                        foreach (XAttribute attribute in node.Attributes())
                            if (attribute.Name != "type") throw new PreferenceFormatException(PreferenceStatus.UnknownFields, "unknown-item-preference-attribute");
                    if ((string)root.Attribute("type") != "object" || Scalar(root, "format", "string") != "JueMingR.ItemAutomation") throw PreferenceJson.Invalid();
                    if (Number(root, "version") != 1) throw new PreferenceFormatException(PreferenceStatus.UnsupportedVersion, "unsupported-item-preference-version");
                    PreferenceJson.ExactFields(root, "format", "version", "stackEnabled", "sellEnabled", "discardEnabled", "sellTypes", "discardTypes", "stackBinding", "sellBinding", "discardBinding");
                    return new ItemAutomationSettings(Boolean(root, "stackEnabled"), Boolean(root, "sellEnabled"), Boolean(root, "discardEnabled"),
                        Types(root, "sellTypes"), Types(root, "discardTypes"), Number(root, "stackBinding"), Number(root, "sellBinding"), Number(root, "discardBinding"));
                }
            }
            catch (PreferenceFormatException) { throw; }
            catch (Exception e) when (e is XmlException || e is ArgumentException || e is InvalidOperationException) { throw PreferenceJson.Invalid(); }
        }
        public byte[] Encode(ItemAutomationSettings value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var root = new XElement("root", new XAttribute("type", "object"), Field("format", "string", "JueMingR.ItemAutomation"),
                Field("version", "number", 1), Field("stackEnabled", "boolean", value.StackEnabled ? "true" : "false"),
                Field("sellEnabled", "boolean", value.SellEnabled ? "true" : "false"), Field("discardEnabled", "boolean", value.DiscardEnabled ? "true" : "false"),
                TypeArray("sellTypes", value.SellTypes), TypeArray("discardTypes", value.DiscardTypes),
                Field("stackBinding", "number", value.StackBinding), Field("sellBinding", "number", value.SellBinding), Field("discardBinding", "number", value.DiscardBinding));
            using (var output = new MemoryStream())
            {
                using (XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(output, new UTF8Encoding(false, true), false)) root.WriteTo(writer);
                if (output.Length > PreferenceJson.MaximumBytes) throw PreferenceJson.Invalid();
                return output.ToArray();
            }
        }
        private IEnumerable<int> Types(XElement root, string name)
        {
            int count = 0;
            foreach (XElement entry in PreferenceJson.Required(root, name, "array").Elements())
            {
                if (++count > itemCount || entry.Name != "item" || (string)entry.Attribute("type") != "number") throw PreferenceJson.Invalid();
                int value = PreferenceJson.Integer(entry);
                if (value <= 0 || value >= itemCount || ItemAutomationSettings.IsCoin(value)) throw PreferenceJson.Invalid();
                yield return value;
            }
        }
        private XElement TypeArray(string name, IEnumerable<int> values)
        {
            var array = new XElement(name, new XAttribute("type", "array"));
            foreach (int value in values)
            { if (value <= 0 || value >= itemCount || ItemAutomationSettings.IsCoin(value)) throw PreferenceJson.Invalid(); array.Add(Field("item", "number", value)); }
            return array;
        }
        private static XElement Field(string name, string type, object value)
        { return new XElement(name, new XAttribute("type", type), Convert.ToString(value, CultureInfo.InvariantCulture)); }
        private static string Scalar(XElement root, string name, string type)
        { XElement value = PreferenceJson.Required(root, name, type); if (value.HasElements) throw PreferenceJson.Invalid(); return value.Value; }
        private static bool Boolean(XElement root, string name)
        { string value = Scalar(root, name, "boolean"); if (value != "true" && value != "false") throw PreferenceJson.Invalid(); return value == "true"; }
        private static int Number(XElement root, string name) { return PreferenceJson.Integer(PreferenceJson.Required(root, name, "number")); }
    }
}
