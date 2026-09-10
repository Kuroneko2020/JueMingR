using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using JueMingR.Platform.Settings;

namespace JueMingR.Platform.Hotkeys
{
    public sealed class HotkeyDocument
    {
        public IReadOnlyList<KeyValuePair<string, string>> Entries { get; }
        public static readonly HotkeyDocument Empty = new HotkeyDocument(new KeyValuePair<string, string>[0]);
        public HotkeyDocument(IEnumerable<KeyValuePair<string, string>> entries)
        {
            var copy = new List<KeyValuePair<string, string>>();
            foreach (var entry in entries)
            {
                if (!HotkeyAction.ValidId(entry.Key) || entry.Value == null || entry.Value.Length > 128 || copy.Count >= 256) throw PreferenceJson.Invalid();
                copy.Add(entry);
            }
            Entries = copy.AsReadOnly();
        }
        public HotkeyDocument With(string id, HotkeyChord chord)
        {
            var copy = new List<KeyValuePair<string, string>>(); bool found = false;
            foreach (var entry in Entries)
            {
                if (entry.Key != id) copy.Add(entry);
                else { copy.Add(new KeyValuePair<string, string>(id, chord == null ? "" : chord.Text)); found = true; }
            }
            if (!found) copy.Add(new KeyValuePair<string, string>(id, chord == null ? "" : chord.Text));
            return new HotkeyDocument(copy);
        }
        public static HotkeyDocument Decode(byte[] bytes)
        {
            XElement root = PreferenceJson.Read(bytes, "JueMingR.Hotkeys", "format", "version", "bindings");
            XElement bindings = PreferenceJson.Required(root, "bindings", "array");
            var entries = new List<KeyValuePair<string, string>>();
            foreach (XElement node in bindings.Elements())
            {
                if (node.Name.LocalName != "item" || (string)node.Attribute("type") != "object") throw PreferenceJson.Invalid();
                PreferenceJson.ExactFields(node, "action", "binding");
                entries.Add(new KeyValuePair<string, string>(PreferenceJson.Required(node, "action", "string").Value,
                    PreferenceJson.Required(node, "binding", "string").Value));
            }
            return new HotkeyDocument(entries);
        }
        public static byte[] Encode(HotkeyDocument value)
        {
            // IDs and canonical keys have a restricted alphabet. Unknown action
            // binding strings still round-trip safely; they are never executed.
            var text = new StringBuilder("{\"format\":\"JueMingR.Hotkeys\",\"version\":1,\"bindings\":[");
            bool first = true;
            foreach (var entry in value.Entries)
            {
                if (!first) text.Append(','); first = false;
                text.Append("{\"action\":\"").Append(entry.Key).Append("\",\"binding\":\"").Append(Escape(entry.Value)).Append("\"}");
            }
            return new UTF8Encoding(false, true).GetBytes(text.Append("]}\n").ToString());
        }
        private static string Escape(string value)
        {
            var result = new StringBuilder();
            foreach (char c in value)
            {
                if (c == '"' || c == '\\') result.Append('\\').Append(c);
                else if (c < 32) result.Append("\\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                else result.Append(c);
            }
            return result.ToString();
        }
    }
}
