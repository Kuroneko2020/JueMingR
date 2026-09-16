using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace JueMingR.Features.MapMarkers
{
    public static class MarkerCodec
    {
        public const int MaximumBytes = 4 * 1024 * 1024;
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        public static MarkerDocument Decode(byte[] bytes, string pair, int width, int height)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumBytes) throw Invalid();
            try
            {
                var quotas = new XmlDictionaryReaderQuotas { MaxDepth = 8, MaxStringContentLength = 10240, MaxArrayLength = MaximumBytes, MaxBytesPerRead = 4096, MaxNameTableCharCount = 16384 };
                // Bound the XML bridge before materializing a tree; tiny JSON
                // objects and special __type attributes also count as input.
                using (var scan = JsonReaderWriterFactory.CreateJsonReader(bytes, quotas))
                { int nodes = 0; while (scan.Read()) if (++nodes > 150000) throw Invalid(); }
                XElement root;
                using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, quotas)) root = XElement.Load(reader);
                foreach (var e in root.DescendantsAndSelf()) foreach (var a in e.Attributes()) if (a.Name != "type") throw Invalid();
                Fields(root, "format", "version", "pair", "width", "height", "revision", "records");
                if (Get(root, "format", "string") != "JueMingR.MapMarkers" || Integer(root, "version") != 1 || Get(root, "pair", "string") != pair || Integer(root, "width") != width || Integer(root, "height") != height) throw Invalid();
                var array = root.Element("records"); if ((string)array.Attribute("type") != "array") throw Invalid();
                var values = new List<MarkerRecord>();
                foreach (var e in array.Elements())
                {
                    if (e.Name != "item" || values.Count == 4096) throw Invalid();
                    Fields(e, "id", "x", "y", "icon", "name"); double x, y;
                    if (!Double.TryParse(Get(e, "x", "string"), NumberStyles.Float, Culture, out x) || !Double.TryParse(Get(e, "y", "string"), NumberStyles.Float, Culture, out y)) throw Invalid();
                    values.Add(new MarkerRecord(Get(e, "id", "string"), x, y, checked((int)Integer(e, "icon")), Get(e, "name", "string")));
                }
                return new MarkerDocument(pair, width, height, Integer(root, "revision"), values);
            }
            catch (InvalidDataException) { throw; }
            catch (Exception ex) when (ex is ArgumentException || ex is XmlException || ex is FormatException || ex is OverflowException || ex is System.Runtime.Serialization.SerializationException) { throw Invalid(); }
        }
        public static byte[] Encode(MarkerDocument value)
        {
            var root = new XElement("root", new XAttribute("type", "object"), S("format", "JueMingR.MapMarkers"), N("version", 1), S("pair", value.Pair), N("width", value.Width), N("height", value.Height), N("revision", value.Revision));
            var array = new XElement("records", new XAttribute("type", "array")); root.Add(array);
            foreach (var r in value.Records) array.Add(new XElement("item", new XAttribute("type", "object"), S("id", r.Id), S("x", r.X.ToString("R", Culture)), S("y", r.Y.ToString("R", Culture)), N("icon", r.Icon), S("name", r.Name)));
            using (var stream = new MemoryStream())
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, new UTF8Encoding(false, true), false, false)) root.WriteTo(writer);
                if (stream.Length > MaximumBytes) throw Invalid(); return stream.ToArray();
            }
        }
        private static XElement S(string key, string value) { return new XElement(key, new XAttribute("type", "string"), value); }
        private static XElement N(string key, long value) { return new XElement(key, new XAttribute("type", "number"), value.ToString(Culture)); }
        private static string Get(XElement root, string key, string type)
        { var value = root.Element(key); if (value == null || value.HasElements || (string)value.Attribute("type") != type) throw Invalid(); return value.Value; }
        private static long Integer(XElement root, string key) { long value; if (!Int64.TryParse(Get(root, key, "number"), NumberStyles.None, Culture, out value)) throw Invalid(); return value; }
        private static void Fields(XElement root, params string[] names)
        {
            if ((string)root.Attribute("type") != "object") throw Invalid(); var fields = new HashSet<string>(names, StringComparer.Ordinal);
            foreach (var e in root.Elements()) if (!fields.Remove(e.Name.ToString())) throw Invalid(); if (fields.Count != 0) throw Invalid();
        }
        private static InvalidDataException Invalid() { return new InvalidDataException("marker-document-protected"); }
    }
}
