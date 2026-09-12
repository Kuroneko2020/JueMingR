using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JueMingR.Features.Text;

namespace JueMingR.Features.WorldObjectText
{
    public enum WorldTextStep { Pending, Element, End }
    public sealed class WorldTextPart
    {
        internal WorldTextPart(string text, int rgb) { Text = text; Rgb = rgb; }
        public string Text { get; }
        public int Rgb { get; } // -1 follows the current feature color without reparsing.
    }
    public sealed class WorldTextElement
    {
        internal WorldTextElement(WorldTextPart[] parts, string item = null, bool newline = false)
        {
            Parts = Array.AsReadOnly(parts); ItemTag = item; IsNewLine = newline;
            if (newline) Text = "\n";
            else { var text = new StringBuilder(); foreach (var part in parts) text.Append(part.Text); Text = text.ToString(); }
        }
        public IReadOnlyList<WorldTextPart> Parts { get; }
        public string Text { get; }
        public string ItemTag { get; }
        public bool IsNewLine { get; }
    }

    // A forward-only, bounded lexer and extended-character cursor. Validation
    // searches resume, never restart a growing prefix. Original source is never
    // modified/copied wholesale. Unknown or malformed tags remain literal.
    public sealed class WorldTextCursor
    {
        private const int MaximumTagSource = 65536;
        private readonly string source;
        private readonly Func<int, bool> validItem;
        private readonly List<WorldTextPart> parts = new List<WorldTextPart>();
        private readonly StringBuilder run = new StringBuilder();
        private int offset, tagScan = -1, literalUntil, colorEnd = -1, color = -1;
        private int previous = -1, regionalRun, elementUnits, runColor = -1;
        private Atom pending;
        private bool hasPending;
        public WorldTextCursor(string source, Func<int, bool> validItem)
        { this.source = source ?? String.Empty; this.validItem = validItem ?? throw new ArgumentNullException(nameof(validItem)); }
        public int SourceOffset { get { return offset; } }
        public int WorkUsed { get; private set; }
        public bool ResourceLimited { get; private set; }
        public WorldTextElement Current { get; private set; }
        public WorldTextStep MoveNext(int workBudget)
        {
            if (workBudget < 4 || workBudget > 65536) throw new ArgumentOutOfRangeException(nameof(workBudget));
            int remaining = workBudget; WorkUsed = 0;
            while (remaining >= 2 || hasPending)
            {
                Atom atom;
                int state;
                if (hasPending) { atom = pending; hasPending = false; state = 1; }
                else state = ReadAtom(ref remaining, out atom);
                WorkUsed = workBudget - remaining;
                if (state == 0) return WorldTextStep.Pending;
                if (state < 0) return elementUnits == 0 ? WorldTextStep.End : Publish();
                if (atom.NewLine || atom.Item != null)
                {
                    if (elementUnits != 0) { pending = atom; hasPending = true; return Publish(); }
                    Current = new WorldTextElement(new WorldTextPart[0], atom.Item, atom.NewLine);
                    return WorldTextStep.Element;
                }
                var category = atom.Scalar == 0xfffd ? UnicodeCategory.OtherSymbol : CharUnicodeInfo.GetUnicodeCategory(source, atom.Start);
                if (elementUnits != 0 && !VisibleTextBoundary.Joins(previous, atom.Scalar, category, regionalRun))
                { pending = atom; hasPending = true; return Publish(); }
                if (elementUnits + atom.Length > VisibleTextBoundary.MaximumElementUnits)
                {
                    // One pathological cluster must not become a huge native font
                    // call. Withdraw it as a whole, then stop with one ellipsis.
                    ResetElement(); offset = source.Length; colorEnd = -1; ResourceLimited = true;
                    Current = new WorldTextElement(new[] { new WorldTextPart("…", -1) });
                    return WorldTextStep.Element;
                }
                if (run.Length != 0 && runColor != atom.Rgb) FinishRun();
                runColor = atom.Rgb;
                if (atom.Replacement) run.Append('\ufffd'); else run.Append(source, atom.Start, atom.Length);
                elementUnits += atom.Length; previous = atom.Scalar;
                regionalRun = atom.Scalar >= 0x1f1e6 && atom.Scalar <= 0x1f1ff ? regionalRun + 1 : 0;
            }
            WorkUsed = workBudget - remaining;
            return WorldTextStep.Pending;
        }
        private WorldTextStep Publish()
        { FinishRun(); Current = new WorldTextElement(parts.ToArray()); ResetElement(); return WorldTextStep.Element; }
        private void FinishRun()
        { if (run.Length != 0) { parts.Add(new WorldTextPart(run.ToString(), runColor)); run.Clear(); } }
        private void ResetElement() { parts.Clear(); run.Clear(); elementUnits = regionalRun = 0; previous = -1; }
        private int ReadAtom(ref int remaining, out Atom atom)
        {
            atom = default(Atom);
            while (remaining >= 2)
            {
                if (offset == source.Length) return -1;
                if (offset == colorEnd) { offset++; remaining--; colorEnd = color = -1; continue; }
                if (colorEnd < 0 && offset >= literalUntil && source[offset] == '[' && (offset == 0 || source[offset - 1] != '\\'))
                {
                    int close;
                    if (!FindTagEnd(ref remaining, out close)) return 0;
                    if (close >= 0 && ReadTag(close, out atom))
                    { if (atom.Item != null) return 1; continue; }
                    // A failed validation is literal through the already scanned
                    // span. Nested '[' cannot cause quadratic rescanning.
                    literalUntil = close >= 0 ? close + 1 : Math.Max(offset + 1, tagScan);
                    tagScan = -1;
                }
                if (remaining < 2) return 0;
                int start = offset;
                if (colorEnd > offset && source[offset] == '\\' && offset + 1 < colorEnd && source[offset + 1] == ']') { offset++; remaining--; start++; }
                char c = source[offset];
                if (c == '\r' || c == '\n')
                {
                    offset++; remaining--;
                    if (c == '\r' && offset < source.Length && source[offset] == '\n') { offset++; remaining--; }
                    atom.NewLine = true; return 1;
                }
                int length = Char.IsHighSurrogate(c) && offset + 1 < source.Length && Char.IsLowSurrogate(source[offset + 1]) ? 2 : 1;
                bool replacement = length == 1 && Char.IsSurrogate(c) || Char.IsControl(c) && c != '\t';
                atom = new Atom { Start = start, Length = length, Rgb = color, Replacement = replacement,
                    Scalar = replacement ? 0xfffd : length == 2 ? Char.ConvertToUtf32(source, offset) : c };
                offset += length; remaining -= length; return 1;
            }
            return 0;
        }
        private bool FindTagEnd(ref int remaining, out int close)
        {
            close = -1;
            if (tagScan < 0) tagScan = offset + 1;
            int limit = Math.Min(source.Length, offset + MaximumTagSource);
            while (tagScan < limit && remaining > 0)
            {
                int at = tagScan++; remaining--;
                if (source[at] == ']' && source[at - 1] != '\\') { close = at; return true; }
            }
            if (tagScan < limit) return false;
            if (tagScan < source.Length) ResourceLimited = true;
            return true;
        }
        private bool ReadTag(int close, out Atom atom)
        {
            atom = default(Atom);
            // Header validation is capped independently of a long color payload.
            int colon = source.IndexOf(':', offset + 1, Math.Min(close - offset - 1, 128));
            if (colon < 0 || colon + 1 == close) return false;
            string head = source.Substring(offset + 1, colon - offset - 1);
            int slash = head.IndexOf('/'); string name = slash < 0 ? head : head.Substring(0, slash);
            string options = slash < 0 ? null : head.Substring(slash + 1);
            if (String.Equals(name, "c", StringComparison.OrdinalIgnoreCase) || String.Equals(name, "color", StringComparison.OrdinalIgnoreCase))
            {
                int rgb;
                if (options == null || !Int32.TryParse(options, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out rgb)) return false;
                color = rgb & 0xffffff; colorEnd = close; offset = colon + 1; tagScan = -1; return true;
            }
            if (!(String.Equals(name, "i", StringComparison.OrdinalIgnoreCase) || String.Equals(name, "item", StringComparison.OrdinalIgnoreCase)) || close - offset > 128) return false;
            int item;
            if (!Int32.TryParse(source.Substring(colon + 1, close - colon - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out item) || !validItem(item)) return false;
            if (options != null)
                foreach (string option in options.Split(','))
                {
                    int number;
                    if (option.Length < 2 || option[0] != 's' && option[0] != 'x' && option[0] != 'p' ||
                        !Int32.TryParse(option.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) return false;
                }
            atom.Item = source.Substring(offset, close - offset + 1); offset = close + 1; tagScan = -1; return true;
        }
        private struct Atom { internal int Start, Length, Scalar, Rgb; internal bool NewLine, Replacement; internal string Item; }
    }
}
