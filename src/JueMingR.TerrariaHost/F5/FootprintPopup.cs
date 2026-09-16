using System;
using System.Collections.Generic;
using JueMingR.Features.Footprints;
using JueMingR.TerrariaHost.Input;

namespace JueMingR.TerrariaHost.F5
{
    // A destructive activation requires a fresh physical press/release after
    // this exact stage was drawn. Update calls cannot advance unseen stages.
    internal sealed class FootprintPopup
    {
        private readonly IFootprintControls host;
        private readonly HostInputState input;
        private readonly FootprintClearConfirmation confirmation = new FootprintClearConfirmation();
        internal readonly List<F5Element> Text = new List<F5Element>();
        internal readonly List<Microsoft.Xna.Framework.Color> TextColors = new List<Microsoft.Xna.Framework.Color>();
        internal readonly List<F5Element> Buttons = new List<F5Element>();
        internal readonly List<int> Commands = new List<int>();
        internal readonly List<bool> Enabled = new List<bool>();
        internal F5Rect Panel;
        internal F5Rect RecordingPanel, ClearPanel;
        internal bool RecordingSelected { get { return recording; } }
        internal bool Visible { get; private set; }
        internal bool OwnsPointer { get; private set; }
        internal bool BlockPointer { get { return Visible; } }
        internal int Pressed = -1, Hovered = -1;
        private bool previousLeft, dirty = true, recording, canClear, clearing, canRetry;
        private int page, skin, generation, pressedGeneration;
        private long session;
        private float width, height;
        private float scale = .70f;
        private object font;
        private string archive, status;
        internal FootprintPopup(IFootprintControls host, HostInputState input) { this.host = host; this.input = input; }
        internal void Open(int currentPage)
        { Close(); host.PrepareConfiguration(); Visible = true; page = currentPage; session = host.Session; archive = host.Generation; confirmation.Open(archive); input.Hotkeys.SuppressHeld(); input.ConsumeHotkeyActions(); }
        internal void Close() { Visible = OwnsPointer = false; Pressed = Hovered = -1; confirmation.Cancel(); dirty = true; }
        internal bool ContainsPointer(float x, float y) { return Visible && Panel.Contains(x, y); }
        internal bool Matches(float w, float h, object f, int s) { return w == width && h == height && ReferenceEquals(f, font) && s == skin; }
        internal void CheckSession()
        {
            if (!Visible) return;
            if (session != host.Session) { Close(); return; }
            if (archive != host.Generation) { archive = host.Generation; confirmation.Open(archive); Pressed = -1; dirty = true; }
        }
        internal void Process(bool active, int currentPage, float x, float y, bool geometryCurrent)
        {
            OwnsPointer = false; bool left = input.Hotkeys.IsDown(256); CheckSession();
            if (!active || !input.SampleFocused || Visible && page != currentPage) { Close(); previousLeft = input.SampleFocused ? left : true; return; }
            if (!Visible) { previousLeft = left; return; }
            OwnsPointer = ContainsPointer(x, y) || Pressed >= 0 || input.HotkeyPointerOwned;
            if (input.Hotkeys.IsNew(27)) { Close(); input.Hotkeys.SuppressHeld(); input.ConsumeHotkeyActions(); previousLeft = left; return; }
            if (!geometryCurrent) { confirmation.Open(archive); Pressed = -1; }
            Hovered = geometryCurrent ? Hit(x, y) : -1;
            if (input.Hotkeys.IsNew(256) && Hovered >= 0)
            { Pressed = Hovered; pressedGeneration = generation; if (Pressed == 2) confirmation.Press(archive); }
            if (!left && previousLeft && Pressed >= 0)
            {
                int command = Pressed; Pressed = -1;
                if (command == Hovered && pressedGeneration == generation)
                {
                    if (command == 0) Close();
                    else if (command == 1) { host.SetRecording(!host.Recording); dirty = true; }
                    else if (command == 3) { confirmation.Open(archive); dirty = true; }
                    else if (command == 4) { host.RetryClear(); dirty = true; }
                    else if (command == 5) { host.RetrySave(); dirty = true; }
                    else if (command == 2) { if (confirmation.Release(archive)) host.Clear(archive); dirty = true; }
                }
                else if (command == 2) { confirmation.Open(archive); dirty = true; }
            }
            if (OwnsPointer) { for (int key = 256; key <= 260; key++) if (input.Hotkeys.IsNew(key)) input.Hotkeys.SuppressKey(key); input.ConsumeHotkeyActions(); }
            previousLeft = left;
        }
        private int Hit(float x, float y)
        { for (int i = 0; i < Buttons.Count; i++) if (Enabled[i] && Buttons[i].Rect.Offset(Panel.X, Panel.Y).Contains(x, y)) return Commands[i]; return -1; }
        internal void Presented() { if (Visible && !dirty && host.CanClear && !host.Clearing) confirmation.Presented(); }
        internal void Prepare(float w, float h, object f, Func<string, float, F5Size> measure, int s)
        {
            CheckSession(); if (!Visible) return;
            bool resources = w != width || h != height || !ReferenceEquals(f, font) || s != skin;
            if (resources) { confirmation.Open(archive); Pressed = -1; }
            if (!dirty && !resources && recording == host.Recording && canClear == host.CanClear && clearing == host.Clearing && canRetry == host.CanRetryClear && status == host.StatusMessage) return;
            width = w; height = h; font = f; skin = s; recording = host.Recording; canClear = host.CanClear; clearing = host.Clearing; canRetry = host.CanRetryClear; status = host.StatusMessage;
            Text.Clear(); TextColors.Clear(); Buttons.Clear(); Commands.Clear(); Enabled.Clear();
            float pw = Math.Min(340, w - 24); scale = .70f;
            bool compact = h < 280;
            if (compact) scale = Math.Min(scale, Math.Max(.45f, (h - 76) / 6 / measure("测试Ag", 1).Height));
            float textHeight = measure("测试Ag", scale).Height, gap = compact ? 8 : 12, header = Math.Max(30, textHeight + 14);
            float recordHeight = Math.Max(58, textHeight * 2 + (compact ? 18 : 28));
            float clearHeight = Math.Max(84, textHeight * 2 + (compact ? 44 : 56));
            float ph = header + gap + recordHeight + gap + clearHeight + gap;
            Panel = new F5Rect((w - pw) / 2, Math.Max(8, (h - ph) / 2), pw, ph);
            RecordingPanel = new F5Rect(12, header + gap, pw - 24, recordHeight);
            ClearPanel = new F5Rect(12, RecordingPanel.Bottom + gap, pw - 24, clearHeight);
            AddText("足迹", 16, 10, measure);
            AddButton("关闭", 0, pw - 70, 7, 58, true, measure);
            AddText("录制路线", 24, RecordingPanel.Y + 10, measure);
            AddButton(recording ? "已开启" : "已关闭", 1, pw - 104, RecordingPanel.Y + 5, 80, host.ControlsEnabled && !clearing, measure);
            string detail = host.CanRetrySave ? "记录尚未保存" : host.HasIssue || !canClear && !clearing ? status : recording ? "记录走过的路线" : "停止新增，已有路线保留";
            AddText(detail ?? "", 24, RecordingPanel.Bottom - textHeight - 9, measure, Microsoft.Xna.Framework.Color.LightGray);
            if (host.CanRetrySave) AddButton("重试保存", 5, pw - 120, RecordingPanel.Bottom - 32, 96, true, measure);
            AddText(clearing ? canRetry ? "清除未完成" : "正在清除…" : "清除历史", 24, ClearPanel.Y + 9, measure, new Microsoft.Xna.Framework.Color(255, 190, 160));
            AddText("当前角色在此世界的足迹", 24, ClearPanel.Y + textHeight + 14, measure, Microsoft.Xna.Framework.Color.LightGray);
            bool confirm = confirmation.Stage > 0 && !clearing;
            float buttonY = ClearPanel.Bottom - Math.Max(28, textHeight + 8) - 8;
            AddButton(clearing ? canRetry ? "重试清除" : "清除处理中…" : confirmation.Label, canRetry ? 4 : 2, 24, buttonY, confirm ? pw - 134 : pw - 48, canRetry || canClear, measure);
            if (confirm) AddButton("取消", 3, pw - 98, buttonY, 74, true, measure);
            generation++; dirty = false;
        }
        private void AddText(string value, float x, float y, Func<string, float, F5Size> measure, Microsoft.Xna.Framework.Color? color = null)
        { var size = measure(value, scale); Text.Add(new F5Element(F5ElementKind.Text, new F5Rect(x, y, size.Width, size.Height), value, size, scale, F5Command.None)); TextColors.Add(color ?? Microsoft.Xna.Framework.Color.White); }
        private void AddButton(string value, int command, float x, float y, float width, bool enabled, Func<string, float, F5Size> measure)
        { var size = measure(value, scale); Buttons.Add(new F5Element(F5ElementKind.Button, new F5Rect(x, y, width, Math.Max(26, size.Height + 8)), value, size, scale, F5Command.None)); Commands.Add(command); Enabled.Add(enabled); }
    }
}
