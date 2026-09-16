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
        internal readonly List<F5Rect> Sections = new List<F5Rect>();
        internal readonly F5HintLayout Hint = new F5HintLayout();
        internal F5Rect Panel, Body, EditRect;
        internal readonly SingleLineEditView EditView = new SingleLineEditView();
        private int valueIndex = -1, scanIndex = -1;
#if DEBUG
        internal long LayoutBuilds, ValueMeasures;
#endif
        internal bool Visible { get { return mode != 0; } }
        internal bool BlockPointer { get { return Visible; } }
        internal bool OwnsPointer { get; private set; }
        internal bool ConsumeWheel { get; private set; }
        internal int Hovered = -1, Pressed = -1, Generation;
        private int mode, page, scroll, maximumScroll, pressedGeneration, skin;
        private long session = -1, lastClick, shownEditorRevision = -1, shownCaretRevision = -1;
        private string lastId, pressedId, shownStatus, shownValue, shownScan, shownComposition, shownDelete;
        private float lastX, lastY, width, height, rowHeight;
        private bool previousLeft, dirty = true, shownBusy, shownFast, shownPaused, shownDynamic, shownScanning;
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
        { TextInput.Release(false); host.Workspace.Suspend(); mode = 0; Pressed = Hovered = -1; OwnsPointer = false; dirty = true; Hint.Clear(); }
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
            if (mode == 2 && !dirty && !resources && shownStatus == status && shownBusy == host.Markers.Busy && shownFast == host.FastScan && shownPaused == host.ScanPaused && shownDynamic == host.DynamicEnabled && shownScanning == host.ScanActive)
            {
                string value = host.ExplorationText, scan = host.ScanText;
                if (value != shownValue) UpdateValue(valueIndex, value, measure);
                if (scan != shownScan) UpdateValue(scanIndex, scan, measure);
                shownValue = value; shownScan = scan; PrepareHint(measure); return;
            }
            if (!dirty && !resources && ReferenceEquals(shown, host.Markers.Saved) && ReferenceEquals(shownEditor, Editor) && shownEditorRevision == (Editor?.Revision ?? -1) && shownCaretRevision == (Editor?.CaretRevision ?? -1) && shownComposition == TextInput.Composition && shownStatus == status && (mode != 2 || shownValue == host.ExplorationText && shownScan == host.ScanText) && shownBusy == host.Markers.Busy && shownFast == host.FastScan && shownPaused == host.ScanPaused && shownDynamic == host.DynamicEnabled && shownDelete == host.Workspace.DeleteConfirmation) { PrepareHint(measure); return; }
            var oldButtons = Buttons.ToArray(); var oldTargets = Targets.ToArray(); var oldCommands = Commands.ToArray(); var oldEnabled = Enabled.ToArray(); var oldPanel = Panel;
            bool confirmationChanged = shownDelete != host.Workspace.DeleteConfirmation;
            width = w; height = h; font = f; skin = s; shown = host.Markers.Saved; shownEditor = Editor; shownEditorRevision = Editor?.Revision ?? -1; shownCaretRevision = Editor?.CaretRevision ?? -1; shownComposition = TextInput.Composition;
            shownStatus = status; shownValue = host.ExplorationText; shownScan = mode == 2 ? host.ScanText : null; shownBusy = host.Markers.Busy; shownFast = host.FastScan; shownPaused = host.ScanPaused; shownDynamic = host.DynamicEnabled; shownDelete = host.Workspace.DeleteConfirmation;
            valueIndex = scanIndex = -1;
            shownScanning = host.ScanActive;
#if DEBUG
            LayoutBuilds++;
#endif
            Text.Clear(); Buttons.Clear(); Commands.Clear(); Enabled.Clear(); Targets.Clear(); Icons.Clear(); Sections.Clear(); EditRect = default(F5Rect);
            rowHeight = mode == 1 ? Math.Max(36, measure("测试Ag", .75f).Height + 12) : Math.Max(50, measure("测试Ag", .70f).Height + 24);
            page = Math.Min(page, Math.Max(0, ((shown?.Records.Count ?? 0) - 1) / 10));
            int visibleRows = Math.Min(10, Math.Max(0, (shown?.Records.Count ?? 0) - page * 10));
            bool notice = status != null || host.Markers.Busy || !host.Markers.Loaded || shown?.IsReadOnly == true || Editor != null || visibleRows == 0;
            int contentRows = mode == 1 ? visibleRows + (notice ? 1 : 0) : status == null ? 4 : 5;
            // Include the body's eight-pixel footer gap before deriving slots;
            // otherwise a fully sized window silently loses its final row.
            float top = mode == 1 ? rowHeight + 16 : 56;
            float pw = Math.Min(mode == 1 ? 480 : 320, w - 24), ph = Math.Min(top + contentRows * rowHeight + (mode == 1 ? rowHeight + 20 : 12), h - 24);
            Panel = new F5Rect((w - pw) / 2, (h - ph) / 2, pw, ph);
            AddText(mode == 1 ? "地图标记" : "揭示区域统计", 16, mode == 1 ? 10 : 17, pw - (mode == 1 ? 32 : 88), measure);
            float footer = ph - rowHeight - 12;
            Body = new F5Rect(Panel.X + 16, Panel.Y + top, pw - 32, Math.Max(0, (mode == 1 ? footer - 8 : ph - 12) - top));
            int slots = Math.Max(0, (int)(Body.Height / rowHeight));
            Button("关闭", 0, null, pw - (mode == 1 ? 84 : 60), mode == 1 ? footer : 10, mode == 1 ? 68 : 44, true, measure);
            if (mode == 1)
            {
                int count = shown?.Records.Count ?? 0; page = Math.Min(page, Math.Max(0, (count - 1) / 10)); int rows = Math.Min(10, Math.Max(0, count - page * 10));
                maximumScroll = Math.Max(0, contentRows - slots); scroll = Math.Min(scroll, maximumScroll);
                string capacity = "共 " + count + " 条 · 上限 120 · 每页 10 条";
                float capacityWidth = measure(capacity, .60f).Width;
                AddText(capacity, Math.Max(104, pw - 16 - capacityWidth), 12, pw - 120, measure, .60f);
                string message = status ?? (host.Markers.Busy ? "正在保存…" : !host.Markers.Loaded ? "正在读取…" : shown?.IsReadOnly == true ? "超出容量，现有标记只读保留。" : Editor != null ? "10 汉字 / 20 字母 · Enter 保存 · Esc 取消" : "尚无标记；开启后在大地图右键选点。");
                if (notice && scroll == 0 && slots > 0) AddText(message, 16, top, pw - 32, measure);
                for (int i = 0; i < rows; i++)
                {
                    int slot = i + (notice ? 1 : 0) - scroll; if (slot < 0 || slot >= slots) continue;
                    var record = shown.Records[page * 10 + i]; float y = top + slot * rowHeight;
                    Icons.Add(Tuple.Create(record.Icon, new F5Rect(16, y, 30, 30)));
                    bool editing = Editor != null && host.Workspace.EditingId == record.Id;
                    string name = editing ? "" : record.Name;
                    Button(Fit(name, pw - 234, measure), 10, record.Id, 50, y, pw - 234, host.Markers.CanEdit && Editor == null, measure);
                    if (editing)
                    {
                        EditRect = new F5Rect(Panel.X + 50, Panel.Y + y, pw - 234, 30);
                        EditView.Prepare(Editor, TextInput.Composition, EditRect.Width - 10, measure);
                    }
                    Button("定位", 11, record.Id, pw - 178, y, 60, !host.Markers.Busy, measure);
                    bool confirm = host.Workspace.DeleteConfirmation == record.Id;
                    Button(confirm ? "确认删除" : "删除", 12, record.Id, pw - 112, y, 96, host.Markers.CanEdit, measure);
                }
                Button("上一页", 1, null, 16, footer, 68, page > 0, measure);
                string pages = "第 " + (page + 1) + " / " + Math.Max(1, (count + 9) / 10) + " 页";
                float pagesWidth = measure(pages, .70f).Width;
                AddText(pages, 90 + Math.Max(0, (102 - pagesWidth) / 2), footer + (30 - measure(pages, .70f).Height) / 2, 102, measure);
                Button("下一页", 2, null, 198, footer, 68, (page + 1) * 10 < count, measure);
                if (host.Workspace.DeleteConfirmation != null) Button("取消删除", 13, null, pw - 192, footer, 102, true, measure);
                else AddText("双击名称改名", 16, top - 17, pw - 32, measure, .55f);
            }
            else
            {
                maximumScroll = Math.Max(0, contentRows - slots); scroll = Math.Min(scroll, maximumScroll);
                AddSection(1, 4, top, slots, pw);
                for (int row = scroll; row < Math.Min(contentRows, scroll + slots); row++)
                {
                    float y = top + (row - scroll) * rowHeight;
                    if (row == 0)
                    {
                        valueIndex = Text.Count; AddText(shownValue, 16, y, 168, measure, 1.05f);
                        scanIndex = Text.Count; AddText(shownScan, 192, y + 6, pw - 208, measure, .60f);
                    }
                    if (row == 1)
                    {
                        AddText(host.ScanPaused && host.ScanActive ? "扫描已暂停" : "扫描地图", 24, y + 14, 82, measure);
                        Button("重新统计", 23, null, pw - 204, y + 8, host.ScanActive ? 86 : 180, host.ControlsEnabled, measure);
                        if (host.ScanActive) Button(host.ScanPaused ? "继续" : "暂停", 22, null, pw - 110, y + 8, 86, host.ControlsEnabled, measure);
                    }
                    if (row == 2)
                    {
                        AddText("扫描速度", 24, y + 14, 82, measure);
                        Button("性能优先", 20, null, pw - 204, y + 8, 86, host.ControlsEnabled, measure);
                        Button("快速完成", 21, null, pw - 110, y + 8, 86, host.ControlsEnabled, measure);
                    }
                    if (row == 3)
                    {
                        AddText("动态更新", 24, y + 14, 82, measure);
                        Button(host.DynamicEnabled ? "已开启 · 关闭" : "已关闭 · 开启", host.DynamicEnabled ? 25 : 24, null, pw - 204, y + 8, 180, host.ControlsEnabled, measure);
                    }
                    if (row == 4) AddText(status, 16, y, pw - 32, measure);
                }
            }
            bool changed = resources || confirmationChanged || !oldPanel.Equals(Panel) || oldButtons.Length != Buttons.Count;
            for (int i = 0; !changed && i < Buttons.Count; i++) changed = !oldButtons[i].Rect.Equals(Buttons[i].Rect) || oldTargets[i] != Targets[i] || oldCommands[i] != Commands[i] || oldEnabled[i] != Enabled[i];
            if (changed) { Generation++; Hovered = Pressed = -1; } dirty = false; PrepareHint(measure);
        }
        internal bool Selected(int command) { return command == 20 && !host.FastScan || command == 21 && host.FastScan || (command == 24 || command == 25) && host.DynamicEnabled; }
        private void AddSection(int first, int end, float top, int slots, float pw)
        {
            int from = Math.Max(first, scroll), to = Math.Min(end, scroll + slots);
            if (from < to) Sections.Add(new F5Rect(16, top + (from - scroll) * rowHeight, pw - 32, (to - from) * rowHeight - 4));
        }
        private void PrepareHint(Func<string, float, F5Size> measure)
        {
            if (mode != 2 || Hovered < 0 || Hovered >= Commands.Count || Pressed >= 0 || Commands[Hovered] != 24 && Commands[Hovered] != 25) { Hint.Hide(); return; }
            Hint.Prepare("开启后先重新统计，再随探索更新比例。\n关闭后保留上次结果；暂停扫描不会关闭动态更新。",
                Buttons[Hovered].Rect.Offset(Panel.X, Panel.Y), new F5Rect(8, 8, width - 16, height - 16), font, measure);
        }
        private void UpdateValue(int index, string text, Func<string, float, F5Size> measure)
        {
            if (index < 0) return;
            var old = Text[index]; text = Fit(text, old.Rect.Width, measure, old.TextScale); var size = measure(text, old.TextScale);
            Text[index] = new F5Element(F5ElementKind.Text, old.Rect, text, size, old.TextScale, F5Command.None);
#if DEBUG
            ValueMeasures++;
#endif
        }
        private static string Fit(string text, float width, Func<string, float, F5Size> measure, float scale = .70f)
        {
            if (measure(text, scale).Width <= width) return text; var boundaries = TextElements.Boundaries(text);
            for (int i = boundaries.Length - 2; i >= 0; i--) { string candidate = text.Substring(0, boundaries[i]) + "…"; if (measure(candidate, scale).Width <= width) return candidate; } return "";
        }
        private void AddText(string value, float x, float y, float width, Func<string, float, F5Size> measure, float scale = .70f)
        { value = Fit(value, width, measure, scale); var size = measure(value, scale); Text.Add(new F5Element(F5ElementKind.Text, new F5Rect(x, y, width, size.Height), value, size, scale, F5Command.None)); }
        private void Button(string value, int command, string target, float x, float y, float width, bool enabled, Func<string, float, F5Size> measure)
        { var size = measure(value, .70f); Buttons.Add(new F5Element(F5ElementKind.Button, new F5Rect(x, y, width, Math.Max(30, size.Height + 8)), value, size, .70f, F5Command.None)); Commands.Add(command); Targets.Add(target); Enabled.Add(enabled); }
    }
}
