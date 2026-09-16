using System;
using System.Collections.Generic;
using System.Diagnostics;
using JueMingR.Features.MapMarkers;
using JueMingR.Features.Text;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Notes;
using TextElements = JueMingR.Features.Text.TextElements;
using Terraria;

namespace JueMingR.TerrariaHost.F5
{
    internal sealed class MapManagementPopup : ITextEditSession
    {
        private readonly IMapControls host;
        private readonly HostInputState input;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        internal readonly TextEditInput TextInput;
        internal readonly List<F5Element> Text = new List<F5Element>();
        internal readonly List<F5Element> Buttons = new List<F5Element>();
        internal readonly List<int> Commands = new List<int>();
        internal readonly List<bool> Enabled = new List<bool>();
        internal readonly List<string> Targets = new List<string>();
        internal readonly List<Tuple<int, F5Rect>> Icons = new List<Tuple<int, F5Rect>>();
        internal F5Rect Panel, Body, EditRect;
        internal readonly SingleLineEditView EditView = new SingleLineEditView();
        internal bool Visible { get { return mode != 0; } }
        internal bool BlockPointer { get { return Visible; } }
        internal bool OwnsPointer { get; private set; }
        internal bool ConsumeWheel { get; private set; }
        internal int Hovered = -1, Pressed = -1, Generation;
        private int mode, page, scroll, maximumScroll, pressedGeneration, skin;
        private long session = -1, lastClick, shownEditorRevision = -1, shownCaretRevision = -1;
        private string lastId, pressedId, shownStatus, shownValue, shownScan, shownComposition, shownDelete;
        private float lastX, lastY, width, height, rowHeight;
        private bool previousLeft, dirty = true, shownBusy, shownFast, shownPaused, shownDynamic;
        private object font;
        private MarkerDocument shown;
        private TextEditBuffer shownEditor;
        internal MapManagementPopup(IMapControls host, HostInputState input, INotesIme ime = null)
        { this.host = host; this.input = input; TextInput = new TextEditInput(this, new NotesClipboard(() => Main.instance.Window.Handle), ime); }
        public TextEditBuffer Editor { get { return host.Workspace.Editor; } }
        public bool RequestFinish() { return SafeAction(null); }
        public void CancelEdit() { host.Workspace.CancelEdit(); dirty = true; }
        public void PreserveUncommittedInput(TextEditBuffer editor) { host.Workspace.PreserveUncommittedInput(editor); }
        internal void Open(bool exploration)
        { mode = exploration ? 2 : 1; session = host.Session; scroll = 0; dirty = true; input.Hotkeys.SuppressHeld(); input.ConsumeHotkeyActions(); }
        internal void BeforeInput(bool active) { TextInput.BeforeSample(active && Visible && mode == 1); }
        internal void Suspend()
        { TextInput.Release(false); host.Workspace.Suspend(); mode = 0; Pressed = Hovered = -1; OwnsPointer = false; dirty = true; }
        internal bool TryLeave(Action continuation)
        {
            if (!Visible && Editor == null) return true;
            if (Editor == null && !host.Markers.Busy) { Suspend(); return true; }
            SafeAction(() => { Suspend(); continuation?.Invoke(); }); return false;
        }
        private bool SafeAction(Action action)
        {
            TextInput.FinishComposition(false); if (TextInput.HasComposition) return false;
            return host.Workspace.Request(() => { TextInput.Release(false); dirty = true; action?.Invoke(); });
        }
        internal void CheckSession() { if (session != host.Session) { Suspend(); session = host.Session; page = 0; } }
        internal bool ContainsPointer(float x, float y) { return Visible && Panel.Contains(x, y); }
        internal bool Matches(float w, float h, object f, int s) { return width == w && height == h && ReferenceEquals(font, f) && skin == s && !dirty; }
        internal void Process(bool active, float x, float y, bool geometryCurrent, int wheel)
        {
            CheckSession(); ConsumeWheel = OwnsPointer = false;
            bool left = input.Hotkeys.IsDown(256);
            bool wasEditing = Editor != null;
            TextInput.AfterSample(active && Visible && mode == 1, null, input.KeyboardSample, input.SampleFocused);
            if (!active || !input.SampleFocused) { if (Visible) Suspend(); previousLeft = left; return; }
            if (!Visible) { previousLeft = left; return; }
            OwnsPointer = ContainsPointer(x, y) || Pressed >= 0 || TextInput.Owned;
            Hovered = geometryCurrent ? Hit(x, y) : -1;
            if (input.Hotkeys.IsNew(27) && !wasEditing)
            { SafeAction(Suspend); input.Hotkeys.SuppressHeld(); input.ConsumeHotkeyActions(); previousLeft = left; return; }
            if (!geometryCurrent || Pressed >= 0 && pressedGeneration != Generation) { Pressed = -1; pressedId = null; }
            if (geometryCurrent && input.Hotkeys.IsNew(256) && Hovered >= 0)
            { Pressed = Hovered; pressedGeneration = Generation; pressedId = Targets[Hovered]; input.Hotkeys.SuppressHeld(); }
            if (!left && previousLeft && Pressed >= 0)
            {
                int index = Pressed; string target = pressedId; Pressed = -1; pressedId = null;
                if (index == Hovered && pressedGeneration == Generation && Targets[index] == target) Execute(Commands[index], target, x, y);
            }
            if (geometryCurrent && Body.Contains(x, y) && wheel != 0)
            { ConsumeWheel = true; scroll = Math.Max(0, Math.Min(maximumScroll, scroll + (wheel > 0 ? -1 : 1))); dirty = true; Pressed = -1; }
            if (OwnsPointer) input.ConsumeHotkeyActions(); previousLeft = left;
        }
        private int Hit(float x, float y)
        { for (int i = 0; i < Buttons.Count; i++) if (Enabled[i] && Buttons[i].Rect.Offset(Panel.X, Panel.Y).Contains(x, y)) return i; return -1; }
        private void Execute(int command, string id, float x, float y)
        {
            if (command == 0) { SafeAction(Suspend); return; }
            if (command == 1 || command == 2) { SafeAction(() => { page = Math.Max(0, page + (command == 1 ? -1 : 1)); scroll = 0; dirty = true; }); return; }
            if (command == 10)
            {
                if (id == lastId && clock.ElapsedMilliseconds - lastClick <= 500 && Math.Abs(x - lastX) <= 6 && Math.Abs(y - lastY) <= 6)
                { SafeAction(() => { if (host.Workspace.BeginEdit(id)) { TextInput.PrepareEditor(); dirty = true; } }); lastId = null; }
                else { lastId = id; lastClick = clock.ElapsedMilliseconds; lastX = x; lastY = y; }
                return;
            }
            if (command == 11) { SafeAction(() => { if (host.Locate(id)) Suspend(); }); return; }
            if (command == 12)
            { SafeAction(() => { if (host.Workspace.DeleteConfirmation == id) host.Workspace.Delete(id); else host.Workspace.ConfirmDelete(id); dirty = true; }); return; }
            if (command == 13) { host.Workspace.CancelDelete(); dirty = true; return; }
            if (command == 20) host.FastScan = false;
            else if (command == 21) host.FastScan = true;
            else if (command == 22) host.PauseScan(!host.ScanPaused);
            else if (command == 23) host.Recount();
            else if (command == 24) host.SetDynamic(true);
            else if (command == 25) host.SetDynamic(false);
            dirty = true;
        }
        internal void Prepare(float w, float h, object f, Func<string, float, F5Size> measure, int s)
        {
            CheckSession(); if (!Visible) return;
            string status = Editor?.Error ?? TextInput.Error ?? host.Workspace.Error ?? host.StatusMessage;
            bool resources = width != w || height != h || !ReferenceEquals(font, f) || skin != s;
            if (!dirty && !resources && ReferenceEquals(shown, host.Markers.Saved) && ReferenceEquals(shownEditor, Editor) && shownEditorRevision == (Editor?.Revision ?? -1) && shownCaretRevision == (Editor?.CaretRevision ?? -1) && shownComposition == TextInput.Composition && shownStatus == status && shownValue == host.ExplorationText && shownScan == host.ScanText && shownBusy == host.Markers.Busy && shownFast == host.FastScan && shownPaused == host.ScanPaused && shownDynamic == host.DynamicEnabled && shownDelete == host.Workspace.DeleteConfirmation) return;
            var oldButtons = Buttons.ToArray(); var oldTargets = Targets.ToArray(); var oldCommands = Commands.ToArray(); var oldEnabled = Enabled.ToArray(); var oldPanel = Panel;
            bool confirmationChanged = shownDelete != host.Workspace.DeleteConfirmation;
            width = w; height = h; font = f; skin = s; shown = host.Markers.Saved; shownEditor = Editor; shownEditorRevision = Editor?.Revision ?? -1; shownCaretRevision = Editor?.CaretRevision ?? -1; shownComposition = TextInput.Composition;
            shownStatus = status; shownValue = host.ExplorationText; shownScan = host.ScanText; shownBusy = host.Markers.Busy; shownFast = host.FastScan; shownPaused = host.ScanPaused; shownDynamic = host.DynamicEnabled; shownDelete = host.Workspace.DeleteConfirmation;
            Text.Clear(); Buttons.Clear(); Commands.Clear(); Enabled.Clear(); Targets.Clear(); Icons.Clear(); EditRect = default(F5Rect);
            rowHeight = Math.Max(36, measure("测试Ag", .75f).Height + 12);
            page = Math.Min(page, Math.Max(0, ((shown?.Records.Count ?? 0) - 1) / 10));
            int visibleRows = Math.Min(10, Math.Max(0, (shown?.Records.Count ?? 0) - page * 10));
            float pw = Math.Min(560, w - 24), ph = Math.Min((mode == 1 ? visibleRows + 3 : 9) * rowHeight + 44, h - 24);
            Panel = new F5Rect((w - pw) / 2, (h - ph) / 2, pw, ph);
            AddText(mode == 1 ? "地图标记 · 双击名称改名" : "揭示区域统计", 16, 10, pw - 32, measure);
            float footer = ph - rowHeight - 12, top = rowHeight + 16;
            Body = new F5Rect(Panel.X + 16, Panel.Y + top, pw - 32, Math.Max(0, footer - top - 8));
            int slots = Math.Max(0, (int)(Body.Height / rowHeight));
            Button("关闭", 0, null, pw - 84, footer, 68, true, measure);
            if (mode == 1)
            {
                int count = shown?.Records.Count ?? 0; page = Math.Min(page, Math.Max(0, (count - 1) / 10)); int rows = Math.Min(10, Math.Max(0, count - page * 10));
                maximumScroll = Math.Max(0, rows + 1 - slots); scroll = Math.Min(scroll, maximumScroll);
                string message = status ?? (host.Markers.Busy ? "正在保存…" : !host.Markers.Loaded ? "正在读取…" : shown?.IsReadOnly == true ? "超出容量，只读保留；未截断。" : Editor != null ? "Enter 保存 · Esc 取消草稿" : "每页 10 条；最多新建 120 条。");
                if (scroll == 0 && slots > 0) AddText(message, 16, top, pw - 32, measure);
                for (int i = 0; i < rows; i++)
                {
                    int slot = i + 1 - scroll; if (slot < 0 || slot >= slots) continue;
                    var record = shown.Records[page * 10 + i]; float y = top + slot * rowHeight;
                    Icons.Add(Tuple.Create(record.Icon, new F5Rect(16, y, 30, 30)));
                    bool editing = Editor != null && host.Workspace.EditingId == record.Id;
                    string name = editing ? "" : record.Name;
                    Button(Fit(name, pw - 250, measure), 10, record.Id, 50, y, pw - 250, host.Markers.CanEdit && Editor == null, measure);
                    if (editing)
                    {
                        EditRect = new F5Rect(Panel.X + 50, Panel.Y + y, pw - 250, 30);
                        EditView.Prepare(Editor, TextInput.Composition, EditRect.Width - 10, measure);
                    }
                    Button("定位", 11, record.Id, pw - 192, y, 66, !host.Markers.Busy, measure);
                    bool confirm = host.Workspace.DeleteConfirmation == record.Id;
                    Button(confirm ? "确认删除" : "删除", 12, record.Id, pw - 120, y, 104, host.Markers.CanEdit, measure);
                }
                Button("上一页", 1, null, 16, footer, 76, page > 0, measure); Button("下一页", 2, null, 98, footer, 76, (page + 1) * 10 < count, measure);
                AddText((page + 1) + "/" + Math.Max(1, (count + 9) / 10) + " · " + count, 184, footer + 4, pw - 278, measure);
                if (host.Workspace.DeleteConfirmation != null) Button("取消删除", 13, null, pw - 202, 7, 96, true, measure);
            }
            else
            {
                maximumScroll = Math.Max(0, 7 - slots); scroll = Math.Min(scroll, maximumScroll);
                for (int row = scroll; row < Math.Min(7, scroll + slots); row++)
                {
                    float y = top + (row - scroll) * rowHeight;
                    if (row == 0) AddText(host.ExplorationText, 16, y, pw - 32, measure);
                    if (row == 1) AddText(host.ScanText, 16, y, pw - 32, measure);
                    if (row == 2) { Button("性能模式", 20, null, 16, y, 118, true, measure); Button("快速模式", 21, null, 142, y, 118, true, measure); }
                    if (row == 3) { Button(host.ScanPaused ? "继续扫描" : "暂停扫描", 22, null, 16, y, 118, true, measure); Button("重新统计", 23, null, 142, y, 118, true, measure); }
                    if (row == 4) { Button("动态开启", 24, null, 16, y, 118, true, measure); Button("动态关闭", 25, null, 142, y, 118, true, measure); }
                    if (row == 5) AddText("动态关闭保留上次结果；再次开启需重新统计。", 16, y, pw - 32, measure);
                    if (row == 6) AddText(status ?? "完整扫描与动态更新分别控制。", 16, y, pw - 32, measure);
                }
            }
            bool changed = resources || confirmationChanged || !oldPanel.Equals(Panel) || oldButtons.Length != Buttons.Count;
            for (int i = 0; !changed && i < Buttons.Count; i++) changed = !oldButtons[i].Rect.Equals(Buttons[i].Rect) || oldTargets[i] != Targets[i] || oldCommands[i] != Commands[i] || oldEnabled[i] != Enabled[i];
            if (changed) Generation++; dirty = false;
        }
        internal bool Selected(int command) { return command == 20 && !host.FastScan || command == 21 && host.FastScan || command == 24 && host.DynamicEnabled || command == 25 && !host.DynamicEnabled; }
        private static string Fit(string text, float width, Func<string, float, F5Size> measure)
        {
            if (measure(text, .70f).Width <= width) return text; var boundaries = TextElements.Boundaries(text);
            for (int i = boundaries.Length - 2; i >= 0; i--) { string candidate = text.Substring(0, boundaries[i]) + "…"; if (measure(candidate, .70f).Width <= width) return candidate; } return "";
        }
        private void AddText(string value, float x, float y, float width, Func<string, float, F5Size> measure)
        { value = Fit(value, width, measure); var size = measure(value, .70f); Text.Add(new F5Element(F5ElementKind.Text, new F5Rect(x, y, size.Width, size.Height), value, size, .70f, F5Command.None)); }
        private void Button(string value, int command, string target, float x, float y, float width, bool enabled, Func<string, float, F5Size> measure)
        { var size = measure(value, .70f); Buttons.Add(new F5Element(F5ElementKind.Button, new F5Rect(x, y, width, Math.Max(30, size.Height + 8)), value, size, .70f, F5Command.None)); Commands.Add(command); Targets.Add(target); Enabled.Add(enabled); }
    }
}
