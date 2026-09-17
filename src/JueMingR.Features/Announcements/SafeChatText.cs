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
        { return BuildCore(entries, byteBudget, "[c/FFD966:这里有 "); }
        public static string BuildNotice(IEnumerable<string> entries, int byteBudget = 1024)
        { return BuildCore(entries, byteBudget, "[c/FFD966:"); }
        private static string BuildCore(IEnumerable<string> entries, int byteBudget, string prefix)
        {
            const string suffix = "]";
            const string omittedText = "部分内容已省略";
            if (byteBudget < 32) return "";
            var accepted = new List<string>(); int bytes = Encoding.UTF8.GetByteCount(prefix + suffix), count = 0;
            bool omitted = false;
            foreach (string entry in entries)
            {
                if (++count > 24) { omitted = true; break; }
                if (string.IsNullOrEmpty(entry) || entry.Length > 1024) { omitted = true; continue; }
                // Entries are already composed from sanitized names and trusted
                // punctuation. Reject unexpected tags rather than nesting parsers.
                if (entry.IndexOf('[') >= 0 || entry.IndexOf(']') >= 0 || !TextElements.IsValid(entry)) { omitted = true; continue; }
                int next = Encoding.UTF8.GetByteCount(entry) + (accepted.Count == 0 ? 0 : 3);
                if (bytes + next > byteBudget) { omitted = true; continue; }
                accepted.Add(entry); bytes += next;
            }
            if (omitted)
            {
                int notice = Encoding.UTF8.GetByteCount(omittedText);
                while (accepted.Count > 0 && bytes + 3 + notice > byteBudget)
                { int last = accepted.Count - 1; bytes -= Encoding.UTF8.GetByteCount(accepted[last]) + (last == 0 ? 0 : 3); accepted.RemoveAt(last); }
                if (bytes + (accepted.Count == 0 ? 0 : 3) + notice <= byteBudget) accepted.Add(omittedText);
            }
            return accepted.Count == 0 ? "" : prefix + string.Join("，", accepted) + suffix;
        }
    }
}
