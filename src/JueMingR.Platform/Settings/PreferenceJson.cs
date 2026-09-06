using System;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace JueMingR.Platform.Settings
{
    // A bounded reader for the two real preference documents, not a schema registry.
    public static class PreferenceJson
    {
        public const int MaximumBytes = 65536;
        public static XElement Read(byte[] contents, string identity, params string[] fields)
        {
            try
            {
                if (contents == null || contents.Length == 0 || contents.Length > MaximumBytes) throw Invalid();
                new UTF8Encoding(false, true).GetString(contents);
                var quotas = new XmlDictionaryReaderQuotas
                { MaxDepth = 8, MaxStringContentLength = MaximumBytes, MaxArrayLength = 32, MaxBytesPerRead = MaximumBytes, MaxNameTableCharCount = 1024 };
                using (XmlDictionaryReader reader = JsonReaderWriterFactory.CreateJsonReader(contents, quotas))
                {
                    XElement root = XElement.Load(reader);
                    if ((string)root.Attribute("type") != "object") throw Invalid();
                    XElement format = Required(root, "format", "string");
                    if (format.Value != identity) throw Invalid();
                    int version = Integer(Required(root, "version", "number"));
                    if (version != 1) throw new PreferenceFormatException(PreferenceStatus.UnsupportedVersion, "Unsupported preference version.");
                    ExactFields(root, fields);
                    return root;
                }
            }
            catch (PreferenceFormatException) { throw; }
            catch (Exception exception) when (exception is XmlException || exception is ArgumentException || exception is InvalidOperationException)
            { throw Invalid(); }
        }

        public static void ExactFields(XElement parent, params string[] fields)
        {
            foreach (XElement child in parent.Elements())
                if (!fields.Contains(child.Name.LocalName))
                    throw new PreferenceFormatException(PreferenceStatus.UnknownFields, "Unknown fields are protected from rewriting.");
            foreach (string name in fields)
                if (parent.Elements(name).Count() != 1) throw Invalid();
        }
        public static XElement Required(XElement parent, string name, string type)
        {
            XElement[] values = parent.Elements(name).ToArray();
            if (values.Length != 1 || (string)values[0].Attribute("type") != type) throw Invalid();
            return values[0];
        }
        public static int Integer(XElement value)
        {
            int result;
            if (value.HasElements || !Int32.TryParse(value.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result)) throw Invalid();
            return result;
        }
        public static PreferenceFormatException Invalid()
        { return new PreferenceFormatException(PreferenceStatus.Invalid, "Invalid preference fields or JSON."); }
    }
}
