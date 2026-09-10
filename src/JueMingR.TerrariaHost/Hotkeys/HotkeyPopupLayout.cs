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
        internal readonly List<F5Element> DetailText = new List<F5Element>();
        internal F5Rect Panel, HelpPanel, DetailViewport;
        internal float HeaderBottom, FooterTop, DetailLineHeight;
        internal int DetailVisibleLines, DetailMaxOffset;
        internal int Generation { get; private set; }
        internal float Width, Height;
        private object font;
        private int skin;
        private HotkeyPopupView previous;
        private bool anchored, above;
        private F5Rect openingAnchor;
        private int helpGeneration = -1;
        private const float Pad = 12, BodyPad = 20;
        private static readonly string[] help = {
            "单键直接按；组合先按住修饰键，再按主键。",
            "Ctrl/Shift/Alt 区分左右，最多三个修饰键，总计最多四键。",
            "可识别的键盘键、鼠标按钮可录入；滚轮滚动不参与。",
            "Esc 取消录入；清除按钮移除当前绑定。",
            "决明同场景重复不保存；原版键位重合仅提醒。",
            "只在设置时核对原版键位，后续改键需自行调整。"
        };
        internal void ResetAnchor() { anchored = false; previous = null; }
        internal bool Matches(float width, float height, object currentFont, int currentSkin = 0)
        { return Width == width && Height == height && ReferenceEquals(font, currentFont) && skin == currentSkin; }
        internal void Build(float width, float height, object font, F5Rect anchor, HotkeyPopupView view, Func<string, float, F5Size> measure, int skin = 0)
        {
            if (Matches(width, height, font, skin) && view.Same(previous)) return;
            Width = width; Height = height; this.font = font; this.skin = skin; previous = view; Generation++;
            if (width < 604 || height < 340) { SmallViewport(measure); return; }
            var shown = view.Candidate ?? view.Effective;
            var labels = new List<string>(4);
            var modifiers = view.Capturing ? view.Modifiers : shown == null ? HotkeyModifiers.None : shown.Modifiers;
            for (int i = 0; i < 6; i++) if (((int)modifiers & (1 << i)) != 0) labels.Add(HotkeyChord.ModifierLabel(i));
            bool tooMany = labels.Count > 3;
            if (tooMany) labels.Clear();
            if (!view.Capturing && shown != null) labels.Add(HotkeyChord.DisplayName(shown.MainKey));
            string primary = view.Capturing ? "取消录入" : view.Effective == null ? "录入快捷键" : "重新录入";
            float primaryWidth = Math.Max(measure("录入快捷键", .7f).Width, measure("重新录入", .7f).Width) + 24;
            float required = Math.Max(measure(view.Title + " · 快捷键", .75f).Width + 104, primaryWidth + measure("清除", .7f).Width + 56);
            required = Math.Max(required, measure("请按单键或组合键", .8f).Width + BodyPad * 2);
            float allCaps = 0, widest = 0;
            foreach (string label in labels) { float size = measure(label, .95f).Width + 20; widest = Math.Max(widest, size); allCaps += size + 18; }
            // A normal three-key row defines the preferred density in this font.
            // Long rows wrap whole caps; only the widest cap must fit on one row.
            float preferred = (measure("LShift", .95f).Width + 20) * 3 + 36;
            float w = Math.Min(width - 24, Math.Max(required, Math.Max(widest + 24, Math.Min(Math.Max(0, allCaps - 18), preferred) + 24)));
            for (int attempt = 0; ; attempt++)
            {
                BuildContent(w, height, view, labels, tooMany, primary, primaryWidth, measure);
                if (Panel.Height <= height - 24 || w >= width - 24 || attempt >= 4) break;
                w = Math.Min(width - 24, w * 1.25f);
            }
            if (Panel.Height > height - 24 || Keycaps.Exists(cap => cap.Rect.X < Pad || cap.Rect.Right > w - Pad))
            { SmallViewport(measure); return; }
            if (!anchored) { openingAnchor = anchor; above = anchor.Bottom + 6 + Panel.Height > height - 12; }
            float top = above ? openingAnchor.Y - Panel.Height - 6 : openingAnchor.Bottom + 6;
            float x = openingAnchor.Right - w;
            Panel = new F5Rect((float)Math.Floor(Math.Max(12, Math.Min(width - w - 12, x))),
                (float)Math.Floor(Math.Max(12, Math.Min(height - Panel.Height - 12, top))), w, Panel.Height);
            anchored = true;
        }
        private void BuildContent(float w, float height, HotkeyPopupView view, List<string> labels, bool tooMany, string primary, float primaryWidth, Func<string, float, F5Size> measure)
        {
            Text.Clear(); Roles.Clear(); Keycaps.Clear(); Buttons.Clear(); Commands.Clear(); Enabled.Clear(); DetailText.Clear();
            DetailViewport = default(F5Rect); DetailMaxOffset = DetailVisibleLines = 0;
            float y = 12, buttonHeight = Math.Max(32, measure(primary, .7f).Height + 8);
            AddText(view.Title + " · 快捷键", Pad, ref y, w - 104, .75f, HotkeyTextRole.Normal, measure);
            HeaderBottom = Math.Max(42, y + 8);
            AddButton(HotkeyPopupCommand.Help, "?", new F5Rect(w - 80, 10, 30, 30), true, measure);
            AddButton(HotkeyPopupCommand.Close, "X", new F5Rect(w - 42, 10, 30, 30), true, measure);
            y = HeaderBottom + 10;
            if (tooMany) AddText("修饰键超过三个", BodyPad, ref y, w - BodyPad * 2, .8f, HotkeyTextRole.Warning, measure, true);
            else if (labels.Count > 0) BuildCaps(labels, ref y, w, measure);
            else AddText(view.Capturing ? "请按单键或组合键" : view.Known ? "未设置快捷键" : "快捷键状态待核对", BodyPad, ref y, w - BodyPad * 2, .8f, HotkeyTextRole.Muted, measure, true);
            if (view.Capturing && labels.Count > 0)
            { y += 6; AddText("请再按一个主键", BodyPad, ref y, w - BodyPad * 2, .7f, HotkeyTextRole.Muted, measure, true); }
            if (view.Capturing)
            { y += 8; AddText("单键直接按；组合先按修饰键。Esc 取消录入。", BodyPad, ref y, w - BodyPad * 2, .65f, HotkeyTextRole.Muted, measure, true); }
            if ((view.Capturing || view.Feedback.Kind == HotkeyFeedbackKind.Saving && view.Candidate != null) && view.Effective != null)
            { y += 8; AddText("当前绑定：" + view.Effective.DisplayText, BodyPad, ref y, w - BodyPad * 2, .65f, HotkeyTextRole.Muted, measure, true); }
            var feedback = view.Feedback;
            bool success = feedback.Kind == HotkeyFeedbackKind.Saved || feedback.Kind == HotkeyFeedbackKind.Cleared;
            bool shortResult = success || feedback.Kind == HotkeyFeedbackKind.Cancelled || feedback.Kind == HotkeyFeedbackKind.Saving;
            var role = success ? HotkeyTextRole.Success : feedback.Kind == HotkeyFeedbackKind.Rejected || feedback.Kind == HotkeyFeedbackKind.Failed || feedback.Kind == HotkeyFeedbackKind.Protected || feedback.Kind == HotkeyFeedbackKind.Unconfirmed ? HotkeyTextRole.Error : HotkeyTextRole.Muted;
            if (feedback.Kind != HotkeyFeedbackKind.Capturing && !String.IsNullOrEmpty(feedback.Summary))
            { y += 8; AddText(feedback.Summary, BodyPad, ref y, w - BodyPad * 2, .7f, role, measure, shortResult); }
            if (!String.IsNullOrEmpty(feedback.Detail))
            { y += 5; AddText(feedback.Detail, BodyPad, ref y, w - BodyPad * 2, .65f, HotkeyTextRole.Muted, measure, shortResult); }
            if (feedback.Advisory != null && feedback.Advisory.HasNotice)
                BuildAdvisory(feedback.Advisory, view.DetailsExpanded, ref y, w, height, buttonHeight, measure);
            FooterTop = y + 10;
            float primaryX = w - Pad - primaryWidth;
            AddButton(HotkeyPopupCommand.Record, primary, new F5Rect(primaryX, FooterTop + 6, primaryWidth, buttonHeight), view.Capturing || view.Editable, measure);
            if (view.Effective != null && view.Editable && !view.Capturing)
                AddButton(HotkeyPopupCommand.Clear, "清除", new F5Rect(primaryX - 12 - measure("清除", .7f).Width - 20, FooterTop + 6, measure("清除", .7f).Width + 20, buttonHeight), true, measure);
            Panel = new F5Rect(Panel.X, Panel.Y, w, FooterTop + buttonHeight + 18);
        }
        private void BuildAdvisory(HotkeyAdvisory advisory, bool expanded, ref float y, float w, float height, float buttonHeight, Func<string, float, F5Size> measure)
        {
            y += 8; AddText(advisory.Summary, BodyPad, ref y, w - BodyPad * 2, .65f, HotkeyTextRole.Warning, measure);
            float dy = 0;
            var rows = new F5RowLayout(DetailText, measure);
            foreach (var item in advisory.Items) rows.TextLines(item.Text, 0, ref dy, w - BodyPad * 2, .65f);
            foreach (var part in advisory.UncheckedParts) rows.TextLines(part, 0, ref dy, w - BodyPad * 2, .65f);
            DetailLineHeight = 1;
            foreach (var line in DetailText) DetailLineHeight = Math.Max(DetailLineHeight, line.Rect.Height + 3);
            float available = height - 24 - y - buttonHeight - 38;
            bool more = advisory.Items.Count > 3 || DetailText.Count > 3 || DetailText.Count * DetailLineHeight > available;
            if (more)
            {
                string label = expanded ? "收起明细" : advisory.Items.Count == 0 ? "查看核对详情" : "查看全部（" + advisory.Items.Count + "项）";
                float bw = measure(label, .7f).Width + 20;
                AddButton(HotkeyPopupCommand.Details, label, new F5Rect(w - BodyPad - bw, y + 6, bw, 32), true, measure);
                y += 44; available -= 44;
            }
            y += 5;
            int visible = DetailText.Count;
            if (more && !expanded) visible = Math.Min(2, visible);
            bool scrolling = expanded && DetailText.Count * DetailLineHeight > available;
            if (scrolling) visible = Math.Max(1, (int)((available - 38) / DetailLineHeight));
            visible = Math.Min(visible, DetailText.Count);
            DetailVisibleLines = visible; DetailMaxOffset = expanded ? Math.Max(0, DetailText.Count - visible) : 0;
            DetailViewport = new F5Rect(BodyPad, y, w - BodyPad * 2, visible * DetailLineHeight);
            y += DetailViewport.Height;
            if (DetailMaxOffset > 0)
            {
                float bw = measure("下移", .7f).Width + 20;
                AddButton(HotkeyPopupCommand.DetailUp, "上移", new F5Rect(w - BodyPad - bw * 2 - 8, y + 4, bw, 30), true, measure);
                AddButton(HotkeyPopupCommand.DetailDown, "下移", new F5Rect(w - BodyPad - bw, y + 4, bw, 30), true, measure);
                y += 38;
            }
        }
        private void BuildCaps(List<string> labels, ref float y, float w, Func<string, float, F5Size> measure)
        {
            float capHeight = 34;
            foreach (string label in labels) capHeight = Math.Max(capHeight, measure(label, .95f).Height + 12);
            int first = 0;
            while (first < labels.Count)
            {
                int end = first; float rowWidth = 0;
                while (end < labels.Count)
                {
                    float next = measure(labels[end], .95f).Width + 20 + (end == first ? 0 : 18);
                    if (end > first && rowWidth + next > w - Pad * 2) break;
                    rowWidth += next; end++;
                }
                float x = (w - rowWidth) / 2;
                for (int i = first; i < end; i++)
                {
                    if (i > first) { float py = y + (capHeight - measure("+", .7f).Height) / 2; AddText("+", x + 3, ref py, 18, .7f, HotkeyTextRole.Muted, measure); x += 18; }
                    var size = measure(labels[i], .95f);
                    Keycaps.Add(new F5Element(F5ElementKind.Button, new F5Rect(x, y, size.Width + 20, capHeight), labels[i], size, .95f, F5Command.None));
                    x += size.Width + 20;
                }
                y += capHeight; first = end; if (first < labels.Count) y += 8;
            }
        }
        internal void PrepareHelp(Func<string, float, F5Size> measure)
        {
            if (helpGeneration == Generation) return;
            helpGeneration = Generation;
            float w = Math.Min(Width - 24, Math.Max(Panel.Width, measure(help[0], .65f).Width + 24));
            float h = HelpRows(w, measure), x = Panel.Right - w, y;
            if (Panel.Y - h - 6 >= 12) y = Panel.Y - h - 6;
            else if (Panel.Bottom + h + 6 <= Height - 12) y = Panel.Bottom + 6;
            else
            {
                float left = Panel.X - 18, right = Width - Panel.Right - 18;
                float side = Math.Max(left, right);
                if (side >= measure("最多三个修饰键", .65f).Width + 24 && HelpRows(side, measure) <= Height - 24)
                { w = side; h = HelpRows(w, measure); x = left >= right ? 12 : Panel.Right + 6; y = Panel.Y; }
                else
                {
                    // The popup can fill the viewport when details are expanded.
                    // Search a free rectangle around its actual controls, without
                    // adding hidden help height to the main layout or moving it.
                    if (PlaceHelpAroundControls(measure)) return;
                    HelpText.Clear(); HelpPanel = default(F5Rect); return;
                }
            }
            HelpPanel = new F5Rect(Math.Max(12, Math.Min(Width - w - 12, x)), Math.Max(12, Math.Min(Height - h - 12, y)), w, h);
        }
        private bool PlaceHelpAroundControls(Func<string, float, F5Size> measure)
        {
            var lefts = new List<float> { 12 }; var rights = new List<float> { Width - 12 };
            var tops = new List<float> { 12 };
            foreach (var button in Buttons)
            {
                var r = button.Rect.Offset(Panel.X, Panel.Y);
                lefts.Add(r.Right + 6); rights.Add(r.X - 6); tops.Add(r.Bottom + 6);
            }
            float best = Single.MaxValue; F5Rect chosen = default(F5Rect);
            foreach (float left in lefts) foreach (float right in rights)
            {
                float w = right - left;
                if (w < measure("最多三个修饰键", .65f).Width + 24) continue;
                float h = HelpRows(w, measure);
                foreach (float top in tops)
                {
                    if (top + h > Height - 12) continue;
                    var candidate = new F5Rect(left, top, w, h); bool blocked = false;
                    foreach (var button in Buttons)
                    {
                        var b = button.Rect.Offset(Panel.X, Panel.Y);
                        if (candidate.X < b.Right && candidate.Right > b.X && candidate.Y < b.Bottom && candidate.Bottom > b.Y) { blocked = true; break; }
                    }
                    float distance = Math.Abs(candidate.Right - Panel.Right) + Math.Abs(candidate.Y - Panel.Y) + h;
                    if (!blocked && distance < best) { best = distance; chosen = candidate; }
                }
            }
            if (best == Single.MaxValue) return false;
            HelpRows(chosen.Width, measure); HelpPanel = chosen; return true;
        }
        private void SmallViewport(Func<string, float, F5Size> measure)
        {
            Text.Clear(); Roles.Clear(); Keycaps.Clear(); Buttons.Clear(); Commands.Clear(); Enabled.Clear(); DetailText.Clear(); HelpText.Clear();
            DetailViewport = HelpPanel = default(F5Rect); DetailVisibleLines = DetailMaxOffset = 0;
            float w = Math.Min(Width - 24, measure("视口过小，请降低 UI 缩放", .65f).Width + 24), y = 12;
            AddText("视口过小，请降低 UI 缩放", 12, ref y, w - 24, .65f, HotkeyTextRole.Warning, measure);
            HeaderBottom = 0; FooterTop = y + 8;
            AddButton(HotkeyPopupCommand.Close, "X", new F5Rect(w - 42, FooterTop + 6, 30, 30), true, measure);
            Panel = new F5Rect(12, 12, w, FooterTop + 48);
        }
        private float HelpRows(float w, Func<string, float, F5Size> measure)
        {
            HelpText.Clear(); float y = 10; var rows = new F5RowLayout(HelpText, measure);
            foreach (string line in help) { rows.TextLines(line, 10, ref y, w - 20, .65f); y += 4; }
            return y + 6;
        }
        private void AddText(string text, float x, ref float y, float width, float scale, HotkeyTextRole role, Func<string, float, F5Size> measure, bool centered = false)
        {
            if (String.IsNullOrEmpty(text)) return;
            int first = Text.Count;
            new F5RowLayout(Text, measure).TextLines(text, x, ref y, width, scale);
            for (int i = first; i < Text.Count; i++)
            {
                var t = Text[i];
                if (centered) Text[i] = new F5Element(t.Kind, t.Rect.Offset((width - t.Rect.Width) / 2, 0), t.Text, t.TextSize, t.TextScale, t.Command);
                Roles.Add(role);
            }
        }
        private void AddButton(HotkeyPopupCommand command, string label, F5Rect rect, bool enabled, Func<string, float, F5Size> measure)
        { Buttons.Add(new F5Element(F5ElementKind.Button, rect, label, measure(label, .7f), .7f, F5Command.None)); Commands.Add(command); Enabled.Add(enabled); }
        internal int Index(HotkeyPopupCommand command) { return Commands.IndexOf(command); }
        internal bool IsEnabled(int i, int offset)
        { return Enabled[i] && (Commands[i] != HotkeyPopupCommand.DetailUp || offset > 0) && (Commands[i] != HotkeyPopupCommand.DetailDown || offset < DetailMaxOffset); }
        internal HotkeyPopupCommand Hit(float x, float y, int offset = 0)
        {
            for (int i = 0; i < Buttons.Count; i++) if (IsEnabled(i, offset) && Buttons[i].Rect.Offset(Panel.X, Panel.Y).Contains(x, y)) return Commands[i];
            return HotkeyPopupCommand.None;
        }
        internal bool OverControl(float x, float y)
        { foreach (var b in Buttons) if (b.Rect.Offset(Panel.X, Panel.Y).Contains(x, y)) return true; return false; }
    }
}
