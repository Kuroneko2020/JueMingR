using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.QuickItems
{
    public sealed class QuickItemDocument
    {
        public const int MaximumEntries = 192;
        public static readonly QuickItemDocument Empty = new QuickItemDocument(false, false, new QuickItemEntry[0]);
        public bool KeepFavorited { get; }
        public bool Enabled { get; }
        public IReadOnlyList<QuickItemEntry> Entries { get; }
        public QuickItemDocument(bool keepFavorited, bool enabled, IEnumerable<QuickItemEntry> entries)
        {
            var copy = new List<QuickItemEntry>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            { if (entry == null || !ids.Add(entry.Id) || copy.Count >= MaximumEntries) throw PreferenceJson.Invalid(); copy.Add(entry); }
            KeepFavorited = keepFavorited; Enabled = enabled; Entries = copy.AsReadOnly();
        }
        public QuickItemEntry Find(string id) { foreach (var entry in Entries) if (entry.Id == id) return entry; return null; }
        public QuickItemDocument Change(QuickItemEntry entry, string remove = null)
        {
            var next = new List<QuickItemEntry>(); bool found = false;
            foreach (var old in Entries)
            {
                if (old.Id == remove) continue;
                if (entry != null && old.Id == entry.Id) { next.Add(entry); found = true; } else next.Add(old);
            }
            if (entry != null && !found) next.Add(entry);
            return new QuickItemDocument(KeepFavorited, Enabled, next);
        }
        public QuickItemDocument Toggles(bool favorite, bool enabled) { return new QuickItemDocument(favorite, enabled, Entries); }
        public static QuickItemDocument Decode(byte[] bytes)
        {
            var root = PreferenceJson.Read(bytes, "JueMingR.QuickItems", "format", "version", "keepFavorited", "enabled", "entries");
            var entries = new List<QuickItemEntry>();
            foreach (var row in PreferenceJson.Required(root, "entries", "array").Elements())
            {
                if (row.Name.LocalName != "item" || (string)row.Attribute("type") != "object") throw PreferenceJson.Invalid();
                PreferenceJson.ExactFields(row, "id", "target", "mode", "compatible", "enabled");
                try { entries.Add(new QuickItemEntry(PreferenceJson.Required(row, "id", "string").Value,
                    PreferenceJson.Integer(PreferenceJson.Required(row, "target", "number")),
                    (QuickItemMode)PreferenceJson.Integer(PreferenceJson.Required(row, "mode", "number")), Bool(row, "compatible"), Bool(row, "enabled"))); }
                catch (ArgumentException) { throw PreferenceJson.Invalid(); }
            }
            return new QuickItemDocument(Bool(root, "keepFavorited"), Bool(root, "enabled"), entries);
        }
        private static bool Bool(XElement root, string name)
        { string text = PreferenceJson.Required(root, name, "boolean").Value; if (text != "true" && text != "false") throw PreferenceJson.Invalid(); return text == "true"; }
        public static byte[] Encode(QuickItemDocument value)
        {
            var text = new StringBuilder("{\"format\":\"JueMingR.QuickItems\",\"version\":1,\"keepFavorited\":").Append(value.KeepFavorited ? "true" : "false")
                .Append(",\"enabled\":").Append(value.Enabled ? "true" : "false").Append(",\"entries\":[");
            bool first = true;
            foreach (var entry in value.Entries)
            {
                if (!first) text.Append(','); first = false;
                text.Append("{\"id\":\"").Append(entry.Id).Append("\",\"target\":").Append(entry.Target.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"mode\":").Append(((int)entry.Mode).ToString(CultureInfo.InvariantCulture)).Append(",\"compatible\":")
                    .Append(entry.Compatible ? "true" : "false").Append(",\"enabled\":").Append(entry.Enabled ? "true" : "false").Append('}');
            }
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(text.Append("]}\n").ToString());
            if (bytes.Length > PreferenceJson.MaximumBytes) throw PreferenceJson.Invalid(); return bytes;
        }
    }
}
