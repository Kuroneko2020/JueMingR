using System;
using System.Collections.Generic;
using System.Text;
using JueMingR.Features.Text;

namespace JueMingR.Features.Announcements
{
    public static class SafeChatText
    {
        public static string CleanName(string source, int maximumElements)
        {
            if (string.IsNullOrEmpty(source) || maximumElements <= 0) return "";
            var clean = new StringBuilder(); bool tag = false;
            // Bound source inspection as well as output; hostile names must not
            // turn a single public action into an unbounded tag/text parser.
            int end = Math.Min(source.Length, 2048);
            for (int i = 0; i < end; i++)
            {
                char c = source[i]; if (c == '[') { tag = true; continue; } if (c == ']') { tag = false; continue; }
                if (tag || char.IsControl(c) || c == '/' || c == '\\') continue;
                if (char.IsHighSurrogate(c)) { if (i + 1 < end && char.IsLowSurrogate(source[i + 1])) { clean.Append(c); clean.Append(source[++i]); } }
                else if (!char.IsLowSurrogate(c)) clean.Append(c);
            }
            string value = clean.ToString().Trim();
            try { int[] boundaries = TextElements.Boundaries(value); return value.Substring(0, boundaries[Math.Min(maximumElements, boundaries.Length - 1)]); }
            catch (ArgumentException) { return ""; }
        }
        public static string Build(IEnumerable<string> entries, int byteBudget = 1024)
        {
            const string prefix = "[c/FFD966:"; const string suffix = "]";
            if (byteBudget < 32) return "";
            var body = new StringBuilder(); int bytes = Encoding.UTF8.GetByteCount(prefix + suffix), count = 0;
            foreach (string entry in entries)
            {
                if (++count > 24) break;
                if (string.IsNullOrEmpty(entry) || entry.Length > 1024) continue;
                // Entries are already composed from sanitized names and trusted
                // punctuation. Reject unexpected tags rather than nesting parsers.
                if (entry.IndexOf('[') >= 0 || entry.IndexOf(']') >= 0 || !TextElements.IsValid(entry)) continue;
                int next = Encoding.UTF8.GetByteCount(entry) + (body.Length == 0 ? 0 : Encoding.UTF8.GetByteCount("；"));
                if (bytes + next > byteBudget) continue;
                if (body.Length != 0) body.Append('；'); body.Append(entry); bytes += next;
            }
            return body.Length == 0 ? "" : prefix + body + suffix;
        }
    }
}
