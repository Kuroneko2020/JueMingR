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
                new UTF8Encoding(false, true).GetString(bytes);
                var quotas = new XmlDictionaryReaderQuotas { MaxDepth = 8, MaxArrayLength = Notebook.MaximumNotes,
                    MaxStringContentLength = Notebook.MaximumBytes, MaxBytesPerRead = 4096, MaxNameTableCharCount = 4096 };
                using (XmlDictionaryReader reader = JsonReaderWriterFactory.CreateJsonReader(bytes, quotas))
                {
                    XElement root = XElement.Load(reader);
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
            using (var stream = new MemoryStream())
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
    }
}
