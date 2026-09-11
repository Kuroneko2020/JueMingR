using System;
using System.Globalization;

namespace JueMingR.TerrariaHost.EntityLabels
{
    // One popup draft. Only Commit invokes the target's settings command; RGB
    // remains exact until an actual edit, independently of displayed HSL rounding.
    internal sealed class StyleEditor
    {
        private readonly Func<int, bool> submit;
        private int committed, caret, selection;
        internal StyleEditor(int rgb, Func<int, bool> submit) { this.submit = submit; Load(rgb); }
        internal int Rgb { get; private set; }
        internal int Committed { get { return committed; } }
        internal double Hue { get; private set; }
        internal double Saturation { get; private set; }
        internal double Lightness { get; private set; }
        internal string Hex { get; private set; }
        internal string Error { get; private set; }
        internal int Revision { get; private set; }
        internal int Caret { get { return caret; } }
        internal int SelectionStart { get { return Math.Min(caret, selection); } }
        internal int SelectionLength { get { return Math.Abs(caret - selection); } }
        internal void Load(int rgb)
        { committed = rgb; SetRgb(rgb); Hex = Format(rgb); caret = selection = Hex.Length; Error = null; Revision++; }
        internal void CancelDraft() { Load(committed); }
        internal void BeginHex() { Hex = Format(Rgb); SelectAll(); Error = null; Revision++; }
        internal void SelectAll() { selection = 0; caret = Hex.Length; Revision++; }
        internal void Move(int delta, bool extend)
        { caret = Math.Max(0, Math.Min(Hex.Length, caret + delta)); if (!extend) selection = caret; Revision++; }
        internal void Delete(bool backwards)
        {
            int start = SelectionStart, length = SelectionLength;
            if (length == 0)
            {
                if (backwards && caret > 0) { start = caret - 1; length = 1; }
                else if (!backwards && caret < Hex.Length) { start = caret; length = 1; }
            }
            if (length == 0) return;
            Hex = Hex.Remove(start, length); caret = selection = start; Error = null; Revision++;
        }
        internal void Insert(string text)
        {
            if (String.IsNullOrEmpty(text)) return;
            int start = SelectionStart;
            string candidate = Hex.Remove(start, SelectionLength).Insert(start, text);
            if (candidate.Length > 6 || !AllHex(candidate)) { SetError("请输入六位 RGB 色码"); return; }
            Hex = candidate.ToUpperInvariant(); caret = selection = start + text.Length; Error = null; Revision++;
            if (Hex.Length == 6) CommitHex();
        }
        internal void CommitHex()
        {
            int rgb;
            if (Hex.Length != 6 || !Int32.TryParse(Hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out rgb))
            { SetError("色码尚未满六位"); return; }
            SetRgb(rgb); Commit();
        }
        internal void PreviewHsl(int axis, double value)
        {
            if (Double.IsNaN(value) || Double.IsInfinity(value) || axis < 0 || axis > 2) throw new ArgumentOutOfRangeException();
            value = Math.Max(0, Math.Min(axis == 0 ? 360 : 100, value));
            if (axis == 0) Hue = value; else if (axis == 1) Saturation = value; else Lightness = value;
            Rgb = HslToRgb(Hue, Saturation, Lightness); Hex = Format(Rgb); caret = selection = Hex.Length; Error = null; Revision++;
        }
        internal bool Commit()
        {
            if (Rgb == committed) { Error = null; return true; }
            if (!submit(Rgb)) { SetError("本次修改未被接受，请重试"); return false; }
            committed = Rgb; Error = null; Revision++; return true;
        }
        internal void SetError(string value) { if (Error != value) { Error = value; Revision++; } }
        private void SetRgb(int rgb)
        {
            if (rgb < 0 || rgb > 0xFFFFFF) throw new ArgumentOutOfRangeException(nameof(rgb));
            Rgb = rgb;
            double r = (rgb >> 16) / 255d, g = ((rgb >> 8) & 255) / 255d, b = (rgb & 255) / 255d;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
            Lightness = (max + min) * 50;
            Saturation = delta == 0 ? 0 : delta / (1 - Math.Abs(2 * Lightness / 100 - 1)) * 100;
            // Gray has no defined hue. Preserve this editor's last useful hue;
            // it is transient and never serialized as an extra user preference.
            if (delta > 0)
            {
                double sector = max == r ? (g - b) / delta : max == g ? (b - r) / delta + 2 : (r - g) / delta + 4;
                Hue = (sector * 60 + 360) % 360;
            }
        }
        internal static int HslToRgb(double hue, double saturation, double lightness)
        {
            double h = hue % 360 / 60, s = saturation / 100, l = lightness / 100;
            double c = (1 - Math.Abs(2 * l - 1)) * s, x = c * (1 - Math.Abs(h % 2 - 1)), m = l - c / 2;
            double r, g, b;
            if (h < 1) { r = c; g = x; b = 0; } else if (h < 2) { r = x; g = c; b = 0; }
            else if (h < 3) { r = 0; g = c; b = x; } else if (h < 4) { r = 0; g = x; b = c; }
            else if (h < 5) { r = x; g = 0; b = c; } else { r = c; g = 0; b = x; }
            return (Channel(r + m) << 16) | (Channel(g + m) << 8) | Channel(b + m);
        }
        private static int Channel(double value) { return Math.Max(0, Math.Min(255, (int)Math.Round(value * 255, MidpointRounding.AwayFromZero))); }
        private static bool AllHex(string text)
        { foreach (char c in text) if (!(c >= '0' && c <= '9') && !(c >= 'A' && c <= 'F') && !(c >= 'a' && c <= 'f')) return false; return true; }
        private static string Format(int rgb) { return rgb.ToString("X6", CultureInfo.InvariantCulture); }
    }
}
