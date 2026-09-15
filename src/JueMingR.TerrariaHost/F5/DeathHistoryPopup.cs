using System;
using System.Collections.Generic;
using System.Globalization;
using JueMingR.Features.Notes;
using JueMingR.Platform.DeathHistory;
using JueMingR.TerrariaHost.Input;

namespace JueMingR.TerrariaHost.F5
{
    // One modal owner for quantity, list and full text. A press belongs to one
    // geometry generation and one event ID, never a mutable row index.
    internal sealed class DeathHistoryPopup
    {
        private readonly IDeathControls host;
        private readonly HostInputState input;
        internal readonly List<F5Element> Text = new List<F5Element>();
        internal readonly List<F5Element> Buttons = new List<F5Element>();
        internal readonly List<int> Commands = new List<int>();
        internal readonly List<bool> Enabled = new List<bool>();
        private readonly List<string> rowIds = new List<string>();
        internal F5Rect Panel, Body;
        internal int Mode { get; private set; } // 0 closed, 1 quantity, 2 list, 3 full
        internal bool Visible { get { return Mode != 0; } }
        internal bool OwnsPointer { get; private set; }
        internal bool BlockPointer { get; private set; }
        internal bool ConsumeWheel { get; private set; }
        internal int Hovered = -1, Pressed = -1;
        internal long Offset { get; private set; }
        internal string SelectedId { get; private set; }
        internal int Generation { get; private set; }
#if DEBUG
        internal int LayoutBuilds { get; private set; }
#endif
        private bool previousLeft, dirty = true, ready;
        private int pressedGeneration, page, skin, scroll;
        private long session = -1;
        private float width, height, bodyMax, lineHeight;
        private object font;
        private DeathHistorySnapshot shown;
        private DeathReadText readText;
        private NotesTextLayout full;
        private string status, preference;
        private int count;
        internal DeathHistoryPopup(IDeathControls host, HostInputState input) { this.host = host; this.input = input; }
        internal void Open(bool quantity, int currentPage)
        {
            Close(); Mode = quantity ? 1 : 2; page = currentPage; session = host.Session; Offset = 0; dirty = true;
            if (!quantity) host.RequestDetails(0);
            input.Hotkeys.SuppressHeld(); input.ConsumeHotkeyActions();
        }
        internal void Close()
        {
            if (Visible) { host.RequestDetails(-1); host.RequestSelection(null); input.Hotkeys.SuppressHeld(); input.ConsumeHotkeyActions(); }
            Mode = 0; Pressed = Hovered = -1; OwnsPointer = BlockPointer = false; scroll = 0; full = null; readText = null; SelectedId = null; shown = null; dirty = true;
        }
        internal bool ContainsPointer(float x, float y) { return Visible && Panel.Contains(x, y); }
        internal bool Matches(float w, float h, object f, int s)
        { return !dirty && w == width && h == height && ReferenceEquals(font, f) && skin == s; }
        internal void CheckSession() { if (Visible && session != host.Session) Close(); }
        internal void Process(bool active, int currentPage, float x, float y, bool geometryCurrent, int wheel)
        {
            OwnsPointer = BlockPointer = ConsumeWheel = false;
            bool left = input.Hotkeys.IsDown(256), pressed = input.Hotkeys.IsNew(256);
            CheckSession();
            if (!active || !input.SampleFocused || Visible && page != currentPage) { Close(); previousLeft = input.SampleFocused ? left : true; return; }
            if (!Visible) { previousLeft = left; return; }
            OwnsPointer = ContainsPointer(x, y) || Pressed >= 0 || input.HotkeyPointerOwned;
            // The modal blocks main-page actions everywhere; only its actual
            // panel claims native pointer input (no full-screen transparent hit).
            BlockPointer = true;
            Hovered = geometryCurrent ? Hit(x, y) : -1;
            if (input.Hotkeys.IsNew(27))
            { input.Hotkeys.SuppressHeld(); input.ConsumeHotkeyActions(); if (Mode == 3) Back(); else Close(); previousLeft = left; return; }
            if (ContainsPointer(x, y)) for (int key = 256; key <= 260; key++) if (input.Hotkeys.IsNew(key)) input.Hotkeys.SuppressKey(key);
            bool cancelled = Pressed >= 0 && (!geometryCurrent || pressedGeneration != Generation);
            if (!geometryCurrent || cancelled) Pressed = -1;
            if (pressed && Hovered >= 0 && !cancelled)
            { Pressed = Hovered; pressedGeneration = Generation; input.Hotkeys.SuppressHeld(); }
            if (!left && previousLeft && Pressed >= 0)
            { int command = Pressed; Pressed = -1; if (command == Hovered && pressedGeneration == Generation) Execute(command); }
            if (geometryCurrent && ContainsPointer(x, y) && wheel != 0)
            { ConsumeWheel = true; if (Body.Contains(x, y)) { int step = Math.Max(1, Math.Min(3, (int)(Body.Height / lineHeight))); scroll = Math.Max(0, Math.Min((int)bodyMax, scroll + (wheel > 0 ? -step : step))); dirty = true; Pressed = -1; } }
            if (OwnsPointer) input.ConsumeHotkeyActions();
            previousLeft = left;
        }
        private int Hit(float x, float y)
        { for (int i = 0; i < Buttons.Count; i++) if (Enabled[i] && Buttons[i].Rect.Offset(Panel.X, Panel.Y).Contains(x, y)) return Commands[i]; return -1; }
        private void Execute(int command)
        {
            if (command == 0) { Close(); return; }
            if (command == 1) { Back(); return; }
            if (command >= 128 && command <= 1024) { host.SetCount(command); dirty = true; return; }
            if (command == 2 || command == 3)
            { Offset = Math.Max(0, Offset + (command == 2 ? -6 : 6)); host.RequestDetails(Offset); scroll = 0; dirty = true; return; }
            if (command >= 10 && command < 16 && command - 10 < rowIds.Count)
            { SelectedId = rowIds[command - 10]; host.RequestSelection(SelectedId); Mode = 3; scroll = 0; full = null; readText = null; dirty = true; }
        }
        private void Back() { Mode = 2; SelectedId = null; host.RequestSelection(null); scroll = 0; full = null; readText = null; dirty = true; }
        // Time already carries the offset frozen at death; viewing it must not
        // convert it into the current machine's timezone.
        internal static string Stamp(DeathFact fact) { return fact.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture); }
        private static string Status(DeathHistorySnapshot value, bool ready)
        { return value.CommitUnconfirmed ? "保存结果未确认" : value.Error != null ? "读取或保存失败；现有记录已保留" : !value.Known || !ready ? "正在读取…" : value.Pending != 0 ? "有记录尚未保存" : value.Count == 0 ? "暂无记录" : null; }
        internal void Prepare(float w, float h, object f, Func<string, float, F5Size> measure, int s)
        {
            CheckSession(); if (!Visible) return;
            var value = host.Snapshot; bool queryReady = host.QueryReady; string nextStatus = Status(value, queryReady);
            bool resources = width != w || height != h || !ReferenceEquals(font, f) || skin != s;
            if (resources) { full = null; dirty = true; }
            if (Mode == 3 && queryReady && value.Selected?.EventId == SelectedId && (!ReferenceEquals(readText, value.SelectedText) || full == null))
            {
                readText = value.SelectedText;
                if (readText != null) full = new NotesTextLayout(readText.Text, Math.Min(560, w - 24) - 32, text => measure(text, .75f).Width, readText.Boundaries);
                dirty = true;
            }
            if (full != null && !full.Complete) { full.Continue(1024); dirty = true; }
            if (!dirty && ReferenceEquals(shown?.Rows, value.Rows) && shown?.Count == value.Count && nextStatus == status && ready == queryReady && count == host.Settings.Count && preference == host.PreferenceMessage) return;
            width = w; height = h; font = f; skin = s; shown = value; status = nextStatus; ready = queryReady; count = host.Settings.Count; preference = host.PreferenceMessage;
            var oldButtons = Buttons.ToArray(); var oldCommands = Commands.ToArray(); var oldEnabled = Enabled.ToArray(); var oldRows = rowIds.ToArray();
            Text.Clear(); Buttons.Clear(); Commands.Clear(); Enabled.Clear(); rowIds.Clear();
            // Every body slot fits both the ordinary glyph and a standard button.
            // Short viewports scroll complete slots; header/footer never move.
            float textHeight = measure("测试Ag", .75f).Height; lineHeight = Math.Max(34, textHeight + 10);
            float panelWidth = Math.Min(Mode == 1 ? 490 : 560, w - 24);
            // A short list uses only its actual rows. Full text keeps a fixed
            // viewport while incremental wrapping continues, so exit stays put.
            int listSlots = Math.Max(1, (queryReady ? value.Rows.Count : 0) + (nextStatus == null ? 0 : 1));
            float wantedHeight = Mode == 2 ? (listSlots + 2) * lineHeight + 50 : (Mode == 1 ? 4 : 6) * lineHeight + 84;
            float panelHeight = Math.Min(wantedHeight, h - 24);
            var oldPanel = Panel;
            Panel = new F5Rect((w - panelWidth) / 2, (h - panelHeight) / 2, panelWidth, panelHeight);
            AddText(Mode == 1 ? "死亡点常驻" : "死亡详情", 16, 12, panelWidth - 32, .75f, measure);
            float footer = panelHeight - lineHeight - 20;
            AddButton("关闭", 0, panelWidth - 84, footer, 68, true, measure);
            float bodyTop = lineHeight + 22;
            Body = new F5Rect(Panel.X + 16, Panel.Y + bodyTop, panelWidth - 32, Math.Max(0, footer - bodyTop - 8));
            if (Mode == 1)
            {
                int visible = Math.Max(0, (int)(Body.Height / lineHeight));
                bodyMax = Math.Max(0, (preference == null ? 3 : 4) - visible); scroll = Math.Min(scroll, (int)bodyMax);
                if (scroll == 0 && visible > 0) AddText("地图显示数量", 16, bodyTop, panelWidth - 32, .75f, measure);
                float optionWidth = (panelWidth - 50) / 4;
                int[] choices = { 128, 256, 512, 1024 };
                if (scroll <= 1 && 1 < scroll + visible) for (int i = 0; i < 4; i++) AddButton(choices[i].ToString(CultureInfo.InvariantCulture), choices[i], 16 + i * (optionWidth + 6), bodyTop + (1 - scroll) * lineHeight, optionWidth, host.ControlsEnabled, measure);
                if (scroll <= 2 && 2 < scroll + visible) AddText("显示最近的" + count.ToString(CultureInfo.InvariantCulture) + "次死亡；完整记录见详情。", 16, bodyTop + (2 - scroll) * lineHeight, panelWidth - 32, .70f, measure);
                if (preference != null && scroll <= 3 && 3 < scroll + visible) AddText(preference, 16, bodyTop + (3 - scroll) * lineHeight, panelWidth - 32, .70f, measure);
            }
            else
            {
                if (Mode == 2) BuildList(value, bodyTop, footer, measure);
                else BuildFull(value, bodyTop, footer, measure);
            }
            if (bodyMax > 0) AddText("滚轮滚动正文", 180, 16, panelWidth - 196, .65f, measure);
            // Text progress alone cannot invalidate a press on a fixed footer.
            // Geometry, enabled state, and command identity are the action epoch.
            bool geometryChanged = resources || !oldPanel.Equals(Panel) || oldButtons.Length != Buttons.Count;
            for (int i = 0; !geometryChanged && i < Buttons.Count; i++) geometryChanged = !oldButtons[i].Rect.Equals(Buttons[i].Rect) || oldCommands[i] != Commands[i] || oldEnabled[i] != Enabled[i];
            if (Mode == 2 && !geometryChanged)
            { geometryChanged = oldRows.Length != rowIds.Count; for (int i = 0; !geometryChanged && i < rowIds.Count; i++) geometryChanged = oldRows[i] != rowIds[i]; }
            if (geometryChanged) Generation++;
            dirty = false;
#if DEBUG
            LayoutBuilds++;
#endif
        }
        private void BuildList(DeathHistorySnapshot value, float top, float footer, Func<string, float, F5Size> measure)
        {
            float timeWidth = measure("2026-09-15 23:59:59", .70f).Width + 12;
            int visible = Math.Max(0, (int)(Body.Height / lineHeight));
            int leading = status == null ? 0 : 1;
            bodyMax = Math.Max(0, (ready ? value.Rows.Count : 0) + leading - visible); scroll = Math.Min(scroll, (int)bodyMax);
            if (leading != 0 && scroll == 0 && visible > 0) AddText(status, 16, top, Body.Width, .70f, measure);
            for (int i = 0; i < value.Rows.Count; i++) rowIds.Add(value.Rows[i].EventId);
            if (ready) for (int i = Math.Max(0, scroll - leading); i < Math.Min(value.Rows.Count, scroll + visible - leading); i++)
            {
                var fact = value.Rows[i]; float y = top + (i + leading - scroll) * lineHeight;
                AddText(Stamp(fact), 16, y, timeWidth, .70f, measure);
                string preview = Fit(DeathReadText.Preview(fact.DisplayCause), Body.Width - timeWidth - 8, .75f, measure);
                AddButton(preview, 10 + i, 16 + timeWidth, y, Body.Width - timeWidth, true, measure);
            }
            AddButton("上一页", 2, 16, footer, 82, ready && value.Known && Offset > 0, measure);
            AddButton("下一页", 3, 104, footer, 82, ready && value.Known && Offset + 6 < value.Count, measure);
            AddText(value.Known ? "合计 " + value.Count + " · " + (Offset / 6 + 1) + "/" + Math.Max(1, (value.Count + 5) / 6) : "合计 —", 194, footer + 4, Panel.Width - 286, .70f, measure);
        }
        private void BuildFull(DeathHistorySnapshot value, float top, float footer, Func<string, float, F5Size> measure)
        {
            AddButton("返回", 1, 16, footer, 68, true, measure);
            if (!ready || value.Selected?.EventId != SelectedId || full == null) { AddText(status ?? "正在读取…", 16, top, Body.Width, .70f, measure); return; }
            int total = full.Lines.Count - (full.Complete ? 0 : 1), visible = Math.Max(0, (int)(Body.Height / lineHeight));
            bodyMax = Math.Max(0, total + 1 - visible); scroll = Math.Min(scroll, (int)bodyMax);
            if (scroll == 0 && visible > 0) AddText(Stamp(value.Selected), 16, top, Body.Width, .70f, measure);
            for (int i = Math.Max(0, scroll - 1); i < Math.Min(total, scroll + visible - 1); i++)
            { var line = full.Lines[i]; AddText(full.Text.Substring(line.Start, line.End - line.Start), 16, top + (i + 1 - scroll) * lineHeight, Body.Width, .75f, measure); }
        }
        private static string Fit(string text, float width, float scale, Func<string, float, F5Size> measure)
        {
            if (measure(text, scale).Width <= width) return text;
            int low = 0, high = text.Length;
            while (low < high) { int middle = (low + high + 1) / 2; if (measure(text.Substring(0, middle) + "…", scale).Width <= width) low = middle; else high = middle - 1; }
            if (low > 0 && Char.IsHighSurrogate(text[low - 1])) low--;
            return text.Substring(0, low) + "…";
        }
        private void AddText(string value, float x, float y, float width, float scale, Func<string, float, F5Size> measure)
        {
            value = Fit(value, width, scale, measure); var size = measure(value, scale);
            Text.Add(new F5Element(F5ElementKind.Text, new F5Rect(x, y, size.Width, size.Height), value, size, scale, F5Command.None));
        }
        private void AddButton(string value, int command, float x, float y, float width, bool enabled, Func<string, float, F5Size> measure)
        {
            var size = measure(value, .70f);
            Buttons.Add(new F5Element(F5ElementKind.Button, new F5Rect(x, y, width, Math.Max(30, size.Height + 8)), value, size, .70f, F5Command.None)); Commands.Add(command); Enabled.Add(enabled);
        }
        internal bool IsSelected(int command) { return Mode == 1 && command == host.Settings.Count; }
    }
}
