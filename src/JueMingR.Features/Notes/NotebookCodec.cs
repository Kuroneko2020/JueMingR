using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Notes
{
    public sealed class NotebookCodec
    {
        public Notebook Decode(byte[] bytes)
        {
            try
            {
                if (bytes == null || bytes.Length == 0 || bytes.Length > Notebook.MaximumBytes) throw PreferenceJson.Invalid();
                new UTF8Encoding(false, true).GetCharCount(bytes);
                var quotas = new XmlDictionaryReaderQuotas { MaxDepth = 8, MaxArrayLength = Notebook.MaximumNotes,
                    MaxStringContentLength = Notebook.MaximumBytes, MaxBytesPerRead = 4096, MaxNameTableCharCount = 4096 };
                // Bound nodes before materializing XElement. MaxArrayLength alone
                // does not bound a tree built via Read(), so hostile tiny elements
                // cannot amplify a 16 MiB file into millions of managed objects.
                using (XmlDictionaryReader scan = JsonReaderWriterFactory.CreateJsonReader(bytes, quotas))
                {
                    int elements = 0;
                    while (scan.Read()) if (scan.NodeType == XmlNodeType.Element && (++elements > Notebook.MaximumNotes * 9 + 4 || scan.Depth > 4))
                        throw PreferenceJson.Invalid();
                }
                using (XmlDictionaryReader reader = JsonReaderWriterFactory.CreateJsonReader(bytes, quotas))
                {
                    XElement root = XElement.Load(reader);
                    // The JSON bridge maps a leading __type member to an XML
                    // attribute, not a child. Reject it (and every other extra
                    // attribute) so a future document cannot be accepted then
                    // silently rewritten without fields unknown to this schema.
                    foreach (XElement node in root.DescendantsAndSelf())
                        foreach (XAttribute attribute in node.Attributes())
                            if (attribute.Name != "type") throw PreferenceJson.Invalid();
                    if ((string)root.Attribute("type") != "object") throw PreferenceJson.Invalid();
                    if (PreferenceJson.Integer(PreferenceJson.Required(root, "schema", "number")) != 1)
                        throw new PreferenceFormatException(PreferenceStatus.UnsupportedVersion, "unsupported-notes-schema");
                    PreferenceJson.ExactFields(root, "schema", "notes");
                    var notes = new List<Note>();
                    foreach (XElement item in PreferenceJson.Required(root, "notes", "array").Elements())
                    {
                        if (item.Name != "item" || (string)item.Attribute("type") != "object") throw PreferenceJson.Invalid();
                        PreferenceJson.ExactFields(item, "id", "title", "body", "pinned", "x", "y", "opacity");
                        string pin = Value(item, "pinned", "boolean");
                        if (pin != "true" && pin != "false") throw PreferenceJson.Invalid();
                        notes.Add(new Note(Value(item, "id", "string"), Value(item, "title", "string"), Value(item, "body", "string"),
                            pin == "true", Number(item, "x"), Number(item, "y"), Number(item, "opacity")));
                        if (notes.Count > Notebook.MaximumNotes) throw PreferenceJson.Invalid();
                    }
                    return new Notebook(notes);
                }
            }
            catch (PreferenceFormatException) { throw; }
            catch (Exception e) when (e is XmlException || e is ArgumentException || e is InvalidOperationException)
            { throw PreferenceJson.Invalid(); }
        }
        public byte[] Encode(Notebook book)
        {
            var array = new XElement("notes", new XAttribute("type", "array"));
            foreach (Note note in book.Notes)
                array.Add(new XElement("item", new XAttribute("type", "object"), Field("id", "string", note.Id),
                    Field("title", "string", note.Title), Field("body", "string", note.Body), Field("pinned", "boolean", note.Pinned ? "true" : "false"),
                    Field("x", "number", note.X), Field("y", "number", note.Y), Field("opacity", "number", note.Opacity)));
            var root = new XElement("root", new XAttribute("type", "object"), Field("schema", "number", 1), array);
            using (var stream = new BoundedOutput())
            {
                using (XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(stream, new UTF8Encoding(false, true), false)) root.WriteTo(writer);
                if (stream.Length > Notebook.MaximumBytes) throw new InvalidOperationException("notes-document-exceeds-16-MiB");
                return stream.ToArray();
            }
        }
        private static XElement Field(string name, string type, object value)
        { return new XElement(name, new XAttribute("type", type), Convert.ToString(value, CultureInfo.InvariantCulture)); }
        private static string Value(XElement parent, string name, string type)
        { XElement value = PreferenceJson.Required(parent, name, type); if (value.HasElements) throw PreferenceJson.Invalid(); return value.Value; }
        private static int Number(XElement parent, string name) { return PreferenceJson.Integer(PreferenceJson.Required(parent, name, "number")); }
        private sealed class BoundedOutput : MemoryStream
        {
            public override void Write(byte[] buffer, int offset, int count)
            { if (Position + count > Notebook.MaximumBytes) throw new InvalidOperationException("notes-document-exceeds-16-MiB"); base.Write(buffer, offset, count); }
            public override void WriteByte(byte value)
            { if (Position == Notebook.MaximumBytes) throw new InvalidOperationException("notes-document-exceeds-16-MiB"); base.WriteByte(value); }
        }
    }
}
