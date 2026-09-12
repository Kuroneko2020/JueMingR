using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingR.Features.EntityLabels;
using JueMingR.TerrariaHost.F5;

namespace JueMingR.TerrariaHost.EntityLabels
{
    internal enum StylePopupCommand { None, Close, Smaller, Larger, Reset }
    internal sealed class StylePopupLayout
    {
        internal readonly List<F5Element> Text = new List<F5Element>();
        internal readonly List<F5Element> Buttons = new List<F5Element>();
        internal readonly List<StylePopupCommand> Commands = new List<StylePopupCommand>();
        internal readonly F5Rect[] Sliders = new F5Rect[3];
        internal F5Rect Panel { get; private set; }
        internal F5Rect HexField { get; private set; }
        internal F5Rect Preview { get; private set; }
        internal F5Size HexSize { get; private set; }
        internal F5Rect Selection { get; private set; }
        internal float CaretX { get; private set; }
        internal int Generation { get; private set; }
        internal int ErrorStartIndex { get; private set; }
        private float width, height, scale;
        private int skin, editorRevision = -1, nameSize;
        private string target;
        private string error;
        private object font;
        private F5Rect anchor;
        private Func<string, float, F5Size> measure;
        internal static string Name(EntityLabelKind kind) { return kind == EntityLabelKind.Enemy ? "敌怪显名" : kind == EntityLabelKind.Critter ? "动物显名" : "NPC显名"; }
        internal void Reset() { font = null; editorRevision = -1; }
        internal bool Matches(float w, float h, float uiScale, object currentFont, int currentSkin, F5Rect currentAnchor)
        { return Generation > 0 && width == w && height == h && scale == uiScale && ReferenceEquals(font, currentFont) && skin == currentSkin && Same(anchor, currentAnchor); }
        internal void Build(float w, float h, float uiScale, object currentFont, Func<string, float, F5Size> measure,
            int currentSkin, F5Rect currentAnchor, EntityLabelKind kind, StyleEditor editor, int size, string message)
        { Build(w, h, uiScale, currentFont, measure, currentSkin, currentAnchor, Name(kind), kind == EntityLabelKind.Critter, editor, size, message); }
        internal void Build(float w, float h, float uiScale, object currentFont, Func<string, float, F5Size> measure,
            int currentSkin, F5Rect currentAnchor, string titleText, bool goldNote, StyleEditor editor, int size, string message)
        {
            bool sameGeometryInputs = Matches(w, h, uiScale, currentFont, currentSkin, currentAnchor);
            if (sameGeometryInputs && target == titleText && editorRevision == editor.Revision && nameSize == size && error == message) return;
            this.measure = measure ?? throw new ArgumentNullException(nameof(measure));
            F5Rect oldPanel = Panel; string oldError = error;
            Text.Clear(); Buttons.Clear(); Commands.Clear();
            F5Size title = Size(titleText + " · 显示设置", .75f), close = Size("X", .7f);
            float buttonHeight = Math.Max(32, close.Height + 10), closeWidth = Math.Max(32, close.Width + 18);
            float labelWidth = Math.Max(Size("饱和度 S", .7f).Width, Size("亮度 L", .7f).Width);
            float numberWidth = Math.Max(Size("360", .7f).Width, Size("100%", .7f).Width);
            float resetWidth = Size("恢复默认", .7f).Width + 20, valueWidth = Size("1.80", .7f).Width;
            float needed = Math.Max(title.Width + closeWidth + 38, Math.Max(labelWidth + 140 + numberWidth + 48,
                size > 0 ? Size("字号", .7f).Width + 64 + valueWidth + resetWidth + 78 : resetWidth + 24));
            float panelWidth = Math.Max(352, needed);
            if (panelWidth > w - 24) throw new InvalidOperationException("style-popup-readable-width-unavailable");
            AddText(titleText + " · 显示设置", 12, 12 + (buttonHeight - title.Height) / 2, .75f);
            AddButton(StylePopupCommand.Close, "X", new F5Rect(panelWidth - 12 - closeWidth, 12, closeWidth, buttonHeight));
            float y = 12 + buttonHeight + 8, rowHeight = Math.Max(34, Size("FFFFFF", .7f).Height + 12);
            AddText("颜色", 12, y + (rowHeight - Size("颜色", .7f).Height) / 2, .7f);
            Preview = new F5Rect(12 + Size("颜色", .7f).Width + 10, y + (rowHeight - 24) / 2, 24, 24);
            float fieldWidth = Size("FFFFFF", .7f).Width + 22;
            HexField = new F5Rect(panelWidth - 12 - fieldWidth, y, fieldWidth, rowHeight);
            AddText("#", HexField.X - Size("#", .7f).Width - 6, y + (rowHeight - Size("#", .7f).Height) / 2, .7f);
            HexSize = Size(editor.Hex, .7f);
            float before = PrefixWidth(editor.Hex, editor.SelectionStart), selected = PrefixWidth(editor.Hex, editor.SelectionStart + editor.SelectionLength) - before;
            CaretX = HexField.X + 8 + PrefixWidth(editor.Hex, editor.Caret);
            Selection = new F5Rect(HexField.X + 8 + before, HexField.Y + 5, selected, HexField.Height - 10);
            y += rowHeight + 8;
            string[] labels = { "色相 H", "饱和度 S", "亮度 L" };
            double[] values = { editor.Hue, editor.Saturation, editor.Lightness };
            for (int i = 0; i < 3; i++)
            {
                float lineHeight = Math.Max(34, Size(labels[i], .7f).Height + 12);
                AddText(labels[i], 12, y + (lineHeight - Size(labels[i], .7f).Height) / 2, .7f);
                float x = 12 + labelWidth + 10;
                Sliders[i] = new F5Rect(x, y, panelWidth - 24 - labelWidth - numberWidth - 24, lineHeight);
                string number = Math.Round(values[i], MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture) + (i == 0 ? "" : "%");
                F5Size numberSize = Size(number, .7f);
                AddText(number, panelWidth - 12 - numberSize.Width, y + (lineHeight - numberSize.Height) / 2, .7f);
                y += lineHeight + 2;
            }
            y += 6;
            string value = (size / 100d).ToString("0.00", CultureInfo.InvariantCulture);
            float sizeRowHeight = Math.Max(32, Size("恢复默认", .7f).Height + 10);
            if (size > 0)
            {
            AddText("字号", 12, y + (sizeRowHeight - Size("字号", .7f).Height) / 2, .7f);
            float start = 12 + Size("字号", .7f).Width + 12;
            AddButton(StylePopupCommand.Smaller, "-", new F5Rect(start, y, 32, sizeRowHeight));
            AddText(value, start + 40 + (valueWidth - Size(value, .7f).Width) / 2, y + (sizeRowHeight - Size(value, .7f).Height) / 2, .7f);
            AddButton(StylePopupCommand.Larger, "+", new F5Rect(start + 48 + valueWidth, y, 32, sizeRowHeight));
            }
            AddButton(StylePopupCommand.Reset, "恢复默认", new F5Rect(panelWidth - 12 - resetWidth, y, resetWidth, sizeRowHeight));
            y += sizeRowHeight + 12;
            if (goldNote) AddWrapped("金色动物保持金色", panelWidth - 24, ref y, .65f);
            ErrorStartIndex = Text.Count;
            if (!String.IsNullOrEmpty(message)) { y += 4; AddWrapped(message, panelWidth - 24, ref y, .65f); }
            if (y > h - 24) throw new InvalidOperationException("style-popup-readable-height-unavailable");
            float px = currentAnchor.Right + 8;
            if (px + panelWidth > w - 12) px = currentAnchor.X - panelWidth - 8;
            px = Math.Max(12, Math.Min(w - panelWidth - 12, px));
            float py = Math.Max(12, Math.Min(h - y - 12, currentAnchor.Y));
            Panel = new F5Rect((float)Math.Floor(px), (float)Math.Floor(py), panelWidth, y);
            if (!sameGeometryInputs || !Same(oldPanel, Panel) || oldError != message && oldPanel.Height != Panel.Height) Generation++;
            width = w; height = h; scale = uiScale; font = currentFont; skin = currentSkin; anchor = currentAnchor;
            target = titleText; editorRevision = editor.Revision; nameSize = size; error = message;
        }
        internal StylePopupCommand Hit(float x, float y)
        { for (int i = 0; i < Buttons.Count; i++) if (Buttons[i].Rect.Offset(Panel.X, Panel.Y).Contains(x, y)) return Commands[i]; return StylePopupCommand.None; }
        internal static bool Enabled(StylePopupCommand command, int size)
        { return (command != StylePopupCommand.Smaller || size > 50) && (command != StylePopupCommand.Larger || size > 0 && size < 180); }
        private F5Size Size(string text, float textScale)
        { F5Size s = measure(text, textScale); return new F5Size(s.Width + 4, s.Height + 4, s.OffsetX - 2, s.OffsetY - 2); }
        private float PrefixWidth(string text, int count) { return count == 0 ? 0 : measure(text.Substring(0, count), .7f).Width; }
        private void AddText(string text, float x, float y, float textScale)
        { F5Size size = Size(text, textScale); Text.Add(new F5Element(F5ElementKind.Text, new F5Rect(x, y, size.Width, size.Height), text, size, textScale, F5Command.None)); }
        private void AddButton(StylePopupCommand command, string text, F5Rect rect)
        { Buttons.Add(new F5Element(F5ElementKind.Button, rect, text, Size(text, .7f), .7f, F5Command.None)); Commands.Add(command); }
        private void AddWrapped(string text, float maximumWidth, ref float y, float textScale)
        {
            int start = 0;
            while (start < text.Length)
            {
                int end = start + 1;
                while (end < text.Length && Size(text.Substring(start, end - start + 1), textScale).Width <= maximumWidth) end++;
                string line = text.Substring(start, end - start); F5Size size = Size(line, textScale);
                AddText(line, 12, y, textScale); y += size.Height + 4; start = end;
            }
        }
        private static bool Same(F5Rect a, F5Rect b) { return a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height; }
    }
}
