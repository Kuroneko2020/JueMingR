using System;
using System.Collections.Generic;
using JueMingR.Platform.Hotkeys;
using JueMingR.TerrariaHost.F5;

namespace JueMingR.TerrariaHost.Hotkeys
{
    internal sealed class HotkeyPopupLayout
    {
        internal readonly List<F5Element> Text = new List<F5Element>();
        internal readonly List<HotkeyTextRole> Roles = new List<HotkeyTextRole>();
        internal readonly List<F5Element> Keycaps = new List<F5Element>();
        internal readonly List<F5Element> Buttons = new List<F5Element>();
        internal readonly List<HotkeyPopupCommand> Commands = new List<HotkeyPopupCommand>();
        internal readonly List<bool> Enabled = new List<bool>();
        internal readonly List<F5Element> HelpText = new List<F5Element>();
        internal F5Rect Panel, HelpPanel;
        internal float HeaderBottom, FooterTop;
        internal int Generation { get; private set; }
        internal float Width, Height;
        private object font;
        private int skin;
        private HotkeyPopupView previous;
        private bool anchored;
        private static readonly string[] help = {
            "最多三个左右修饰键 + 一个键盘或鼠标键。",
            "单键直接按；组合键先按修饰键，再按主键。",
            "Esc 取消录入；按钮清除；滚轮不录入。",
            "决明内部重复会拒绝；原版重合仅提醒。",
            "以后改原版键位，不会自动复查或禁用。"
        };
        internal void ResetAnchor() { anchored = false; previous = null; }
        internal bool Matches(float width, float height, object currentFont, int currentSkin = 0)
        { return Width == width && Height == height && ReferenceEquals(font, currentFont) && skin == currentSkin; }
        internal void Build(float width, float height, object font, F5Rect anchor, HotkeyPopupView view, Func<string, float, F5Size> measure, int skin = 0, bool expanded = false)
        {
            if (!expanded && Matches(width, height, font, skin) && view.Same(previous)) return;
            Width = width; Height = height; this.font = font; this.skin = skin; previous = view; Generation++;
            Text.Clear(); Roles.Clear(); Keycaps.Clear(); Buttons.Clear(); Commands.Clear(); Enabled.Clear(); HelpText.Clear();
            float w = expanded ? width - 24 : Math.Min(440, width - 24), y = 12;
            // Below the supported readable viewport, keep a safe close affordance
            // instead of throwing through the host and permanently failing F5.
            if (width < 604 || height < 340)
            {
                w = Math.Max(96, w);
                AddText("视口过小，请降低 UI 缩放", 12, ref y, Math.Max(72, w - 24), .65f, HotkeyTextRole.Warning, measure);
                AddButton(HotkeyPopupCommand.Close, "X", new F5Rect(w - 42, y + 8, 30, 30), true, measure);
                Panel = new F5Rect(12, 12, w, y + 50); HeaderBottom = FooterTop = y; HelpPanel = default(F5Rect); return;
            }
            AddText(view.Title + " · 快捷键", 12, ref y, w - 108, .75f, HotkeyTextRole.Normal, measure);
            HeaderBottom = Math.Max(42, y + 8);
            AddButton(HotkeyPopupCommand.Help, "?", new F5Rect(w - 80, 10, 30, 30), true, measure);
            AddButton(HotkeyPopupCommand.Close, "X", new F5Rect(w - 42, 10, 30, 30), true, measure);
            y = HeaderBottom + 12;
            var shown = view.Candidate ?? view.Effective;
            if (view.Capturing) BuildCaps(null, view.Modifiers, ref y, w, measure);
            else if (shown != null) BuildCaps(shown, shown.Modifiers, ref y, w, measure);
            else AddText(view.Known ? "未设置快捷键" : "快捷键状态待核对", 12, ref y, w - 24, .8f, HotkeyTextRole.Muted, measure);
            y += 10;
            if ((view.Capturing || view.Feedback.Kind == HotkeyFeedbackKind.Saving && view.Candidate != null) && view.Effective != null)
            { AddText("原绑定：" + view.Effective.DisplayText, 12, ref y, w - 24, .65f, HotkeyTextRole.Muted, measure); y += 6; }
            float resultTop = y;
            var feedback = view.Feedback;
            HotkeyTextRole role = feedback.Kind == HotkeyFeedbackKind.Saved || feedback.Kind == HotkeyFeedbackKind.Cleared ? HotkeyTextRole.Success :
                feedback.Kind == HotkeyFeedbackKind.Rejected || feedback.Kind == HotkeyFeedbackKind.Failed || feedback.Kind == HotkeyFeedbackKind.Protected || feedback.Kind == HotkeyFeedbackKind.Unconfirmed ? HotkeyTextRole.Error : HotkeyTextRole.Muted;
            AddText(feedback.Summary, 12, ref y, w - 24, .7f, role, measure);
            if (!String.IsNullOrEmpty(feedback.Detail)) { y += 5; AddText(feedback.Detail, 12, ref y, w - 24, .65f, HotkeyTextRole.Muted, measure); }
            if (!String.IsNullOrEmpty(feedback.Advisory)) { y += 8; AddText(feedback.Advisory, 12, ref y, w - 24, .65f, HotkeyTextRole.Warning, measure); }
            if (view.Capturing) { y += 6; AddText("先按修饰键，再按主键；Esc 取消。", 12, ref y, w - 24, .65f, HotkeyTextRole.Muted, measure); }
            // Help uses the same font/surface as existing R hover hints. Its
            // fallback area excludes keycaps, header controls and footer.
            float helpY = 10;
            var helpRows = new F5RowLayout(HelpText, measure);
            foreach (string line in help) { helpRows.TextLines(line, 10, ref helpY, w - 44, .65f); helpY += 5; }
            float helpHeight = helpY + 5;
            FooterTop = Math.Max(182, y + 12);
            float buttonHeight = Math.Max(32, measure("重新录入", .7f).Height + 8);
            float h = FooterTop + buttonHeight + 18;
            float x = anchored ? Panel.X : anchor.Right - w;
            float top = anchored ? Panel.Y : anchor.Bottom + 6;
            if (!anchored && top + h > height - 12) top = anchor.Y - h - 6;
            Panel = new F5Rect((float)Math.Floor(Math.Max(12, Math.Min(width - w - 12, x))),
                (float)Math.Floor(Math.Max(12, Math.Min(height - h - 12, top))), w, h);
            if (Panel.Y < helpHeight + 18 && Panel.Bottom + helpHeight + 6 > height - 12)
            {
                FooterTop = Math.Max(FooterTop, resultTop + helpHeight + 6);
                h = FooterTop + buttonHeight + 18;
                Panel = new F5Rect(Panel.X, (float)Math.Floor(Math.Max(12, Math.Min(height - h - 12, top))), w, h);
            }
            // A short viewport can fit a long name by using its available width.
            // Reflow once before accepting geometry; never clip the footer or
            // shrink fonts just because the usual compact width is insufficient.
            if (!expanded && (h > height - 24 || Keycaps.Exists(c => c.Rect.Right > w - 12)))
            { Build(width, height, font, anchor, view, measure, skin, true); return; }
            anchored = true;
            string primary = view.Capturing ? "取消录入" : view.Effective == null ? "录入快捷键" : "重新录入";
            // Reserve the widest primary label; clear visibility cannot shift it.
            float primaryWidth = Math.Max(measure("录入快捷键", .7f).Width, measure("重新录入", .7f).Width) + 24;
            float primaryX = w - 12 - primaryWidth;
            AddButton(HotkeyPopupCommand.Record, primary, new F5Rect(primaryX, FooterTop + 6, primaryWidth, buttonHeight), view.Capturing || view.Editable, measure);
            if (view.Effective != null && view.Editable && !view.Capturing)
                AddButton(HotkeyPopupCommand.Clear, "清除", new F5Rect(primaryX - 12 - measure("清除", .7f).Width - 20, FooterTop + 6, measure("清除", .7f).Width + 20, buttonHeight), true, measure);
            float helpTop = Panel.Y + resultTop;
            if (Panel.Y >= helpHeight + 18) helpTop = Panel.Y - helpHeight - 6;
            else if (Panel.Bottom + helpHeight + 6 <= height - 12) helpTop = Panel.Bottom + 6;
            HelpPanel = new F5Rect(Panel.X + 12, helpTop, w - 24, helpHeight);
        }
        private void BuildCaps(HotkeyChord chord, HotkeyModifiers modifiers, ref float y, float w, Func<string, float, F5Size> measure)
        {
            var labels = new List<string>(4);
            for (int i = 0; i < 6; i++) if (((int)modifiers & (1 << i)) != 0) labels.Add(HotkeyChord.ModifierLabel(i));
            if (labels.Count > 3) { AddText("修饰键超过三个", 12, ref y, w - 24, .8f, HotkeyTextRole.Warning, measure); return; }
            if (chord != null) labels.Add(HotkeyChord.DisplayName(chord.MainKey));
            if (labels.Count == 0) { AddText("请按快捷键", 12, ref y, w - 24, .8f, HotkeyTextRole.Muted, measure); return; }
            float capHeight = 34;
            foreach (string label in labels) capHeight = Math.Max(capHeight, measure(label, .95f).Height + 12);
            float x = 12; int first = 0;
            for (int i = 0; i < labels.Count; i++)
            {
                var size = measure(labels[i], .95f); float cw = size.Width + 20;
                if (i > first && x + 18 + cw > w - 12) { CenterCaps(first, i, w, x); first = i; x = 12; y += capHeight + 8; }
                if (i > first) { float plusY = y + (capHeight - measure("+", .7f).Height) / 2; AddText("+", x + 3, ref plusY, 18, .7f, HotkeyTextRole.Muted, measure); x += 18; }
                Keycaps.Add(new F5Element(F5ElementKind.Button, new F5Rect(x, y, cw, capHeight), labels[i], size, .95f, F5Command.None)); x += cw;
            }
            CenterCaps(first, labels.Count, w, x);
            y += capHeight;
        }
        private void CenterCaps(int first, int end, float width, float right)
        {
            float shift = (width - 12 - right) / 2;
            if (shift <= 0) return;
            float top = Keycaps[first].Rect.Y;
            for (int i = first; i < end; i++) { var c = Keycaps[i]; Keycaps[i] = new F5Element(c.Kind, c.Rect.Offset(shift, 0), c.Text, c.TextSize, c.TextScale, c.Command); }
            for (int i = 0; i < Text.Count; i++) if (Text[i].Text == "+" && Text[i].Rect.Y >= top && Text[i].Rect.Y < top + Keycaps[first].Rect.Height)
            { var t = Text[i]; Text[i] = new F5Element(t.Kind, t.Rect.Offset(shift, 0), t.Text, t.TextSize, t.TextScale, t.Command); }
        }
        private void AddText(string text, float x, ref float y, float width, float scale, HotkeyTextRole role, Func<string, float, F5Size> measure)
        {
            if (String.IsNullOrEmpty(text)) return;
            new F5RowLayout(Text, measure).TextLines(text, x, ref y, width, scale);
            while (Roles.Count < Text.Count) Roles.Add(role);
        }
        private void AddButton(HotkeyPopupCommand command, string label, F5Rect rect, bool enabled, Func<string, float, F5Size> measure)
        { Buttons.Add(new F5Element(F5ElementKind.Button, rect, label, measure(label, .7f), .7f, F5Command.None)); Commands.Add(command); Enabled.Add(enabled); }
        internal int Index(HotkeyPopupCommand command) { return Commands.IndexOf(command); }
        internal HotkeyPopupCommand Hit(float x, float y)
        {
            for (int i = 0; i < Buttons.Count; i++) if (Enabled[i] && Buttons[i].Rect.Offset(Panel.X, Panel.Y).Contains(x, y)) return Commands[i];
            return HotkeyPopupCommand.None;
        }
        internal bool OverControl(float x, float y)
        { foreach (var b in Buttons) if (b.Rect.Offset(Panel.X, Panel.Y).Contains(x, y)) return true; return false; }
    }
}
