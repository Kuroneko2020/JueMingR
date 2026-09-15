using System;
using System.Collections.Generic;
using System.Text;

namespace JueMingR.Platform.DeathHistory
{
    // Prepared only for the selected record on the archive worker. The original
    // fact remains untouched; this plain-text view never enters a chat parser.
    public sealed class DeathReadText
    {
        public DeathReadText(string source)
        {
            source = source ?? "死亡原因未记录";
            var text = new StringBuilder(source.Length); var index = new List<int> { 0 };
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '\r' && i + 1 < source.Length && source[i + 1] == '\n') { text.Append(c); text.Append(source[++i]); }
                else if (Char.IsHighSurrogate(c) && i + 1 < source.Length && Char.IsLowSurrogate(source[i + 1]))
                { text.Append(c); text.Append(source[++i]); }
                else text.Append(Char.IsSurrogate(c) ? '\uFFFD' : c == '\t' ? ' ' : Char.IsControl(c) && c != '\n' && c != '\r' ? '\uFFFD' : c);
                index.Add(text.Length);
            }
            Text = text.ToString(); Boundaries = index.AsReadOnly();
        }
        public string Text { get; }
        public IReadOnlyList<int> Boundaries { get; }
        public static string Preview(string value, int limit = 80)
        {
            if (value == null) return "死亡原因未记录";
            int count = Math.Min(value.Length, limit);
            if (count > 0 && Char.IsHighSurrogate(value[count - 1])) count--;
            return new DeathReadText(value.Substring(0, count)).Text.Replace('\n', ' ').Replace('\r', ' ') + (count < value.Length ? "…" : "");
        }
    }
}
