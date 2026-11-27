using System;
using System.Text;

namespace JueMingR.TerrariaHost.Information
{
    internal static class AnglerQuestLocationText
    {
        private const int MaximumTextLength = 16384;
        private const int MaximumAnnotationLength = 768;
        private const int MaximumLocationLength = 512;

        internal static string Parse(string text)
        {
            if (String.IsNullOrEmpty(text) || text.Length > MaximumTextLength) return null;
            int end = text.Length - 1;
            while (end >= 0 && Char.IsWhiteSpace(text[end])) end--;
            if (end < 0) return null;

            int floor = Math.Max(0, end - MaximumAnnotationLength + 1);
            int open = FindPairedOpening(text, end, floor);
            // The annotation begins on its own line, but a qualifier within it may span several lines.
            if (open >= 0 && StartsLine(text, open, floor))
                return ReadLabel(text.Substring(open + 1, end - open - 1), true);

            int start = end;
            while (start > floor && !IsNewline(text[start - 1])) start--;
            if (start > 0 && !IsNewline(text[start - 1])) return null;
            return ReadLabel(text.Substring(start, end - start + 1), false);
        }

        private static bool StartsLine(string text, int start, int floor)
        {
            for (int index = start - 1; index >= floor; index--)
            {
                if (IsNewline(text[index])) return true;
                if (!Char.IsWhiteSpace(text[index])) return false;
            }
            return floor == 0 || IsNewline(text[floor - 1]);
        }

        private static string ReadLabel(string annotation, bool wrapped)
        {
            annotation = annotation.Trim();
            string location = ReadTaggedLocation(annotation);
            if (location == null && wrapped)
            {
                int separator = annotation.IndexOfAny(new[] { ',', '，' });
                // ItemName and the caption can use different translations. Localize already picked
                // this fish's exact quest key; only trust a short caption followed by an explicit label.
                if (separator > 0 && IsCaption(annotation.Substring(0, separator).Trim()))
                    location = ReadTaggedLocation(annotation.Substring(separator + 1).TrimStart());
            }
            if (String.IsNullOrWhiteSpace(location) || location.Length > MaximumLocationLength ||
                !BalancedQualifiers(location))
                return null;
            return JoinLocationLines(location.Trim());
        }

        private static string ReadTaggedLocation(string annotation)
        {
            const string english = "Caught in";
            if (annotation.StartsWith(english, StringComparison.OrdinalIgnoreCase) &&
                annotation.Length > english.Length &&
                (Char.IsWhiteSpace(annotation[english.Length]) || IsColon(annotation[english.Length])))
            {
                string tail = annotation.Substring(english.Length).TrimStart();
                return tail.Length > 0 && IsColon(tail[0]) ? tail.Substring(1).Trim() : tail.Trim();
            }
            foreach (string label in ChineseLabels)
            {
                if (!annotation.StartsWith(label, StringComparison.Ordinal)) continue;
                string tail = annotation.Substring(label.Length).TrimStart();
                if (tail.Length > 0 && IsColon(tail[0])) return tail.Substring(1).Trim();
            }
            return null;
        }

        private static readonly string[] ChineseLabels = { "捕获位置", "抓捕位置", "钓获位置" };

        private static bool IsCaption(string text)
        {
            if (text.Length == 0 || text.Length > 80) return false;
            foreach (char character in text)
                if (!(Char.IsLetterOrDigit(character) || character == ' ' || character == '\t' ||
                    character == '-' || character == '·' || character == '\'' || character == '’'))
                    return false;
            return true;
        }

        private static int FindPairedOpening(string text, int close, int floor)
        {
            if (text[close] != ')' && text[close] != '）') return -1;
            // Encodes a bounded stack of the corresponding parenthesis widths.
            uint wide = 0;
            int depth = 0;
            for (int index = close; index >= floor; index--)
            {
                char character = text[index];
                if (character == ')' || character == '）')
                {
                    if (depth == 32) return -1;
                    wide = (wide << 1) | (character == '）' ? 1u : 0u);
                    depth++;
                }
                else if (character == '(' || character == '（')
                {
                    if ((wide & 1u) != (character == '（' ? 1u : 0u)) return -1;
                    wide >>= 1;
                    if (--depth == 0) return index;
                }
            }
            return -1;
        }

        private static bool BalancedQualifiers(string location)
        {
            for (int index = location.Length - 1; index >= 0; index--)
            {
                if (location[index] == '(' || location[index] == '（') return false;
                if (location[index] != ')' && location[index] != '）') continue;
                index = FindPairedOpening(location, index, 0);
                if (index < 0) return false;
            }
            return true;
        }

        private static string JoinLocationLines(string text)
        {
            if (text.IndexOf('\r') < 0 && text.IndexOf('\n') < 0) return text;
            var joined = new StringBuilder(text.Length);
            foreach (string segment in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = segment.Trim();
                if (line.Length == 0) continue;
                if (joined.Length > 0 && line[0] != '(' && line[0] != '（') joined.Append(' ');
                joined.Append(line);
            }
            return joined.ToString();
        }

        private static bool IsColon(char character) => character == ':' || character == '：';
        private static bool IsNewline(char character) => character == '\n' || character == '\r';
    }
}
