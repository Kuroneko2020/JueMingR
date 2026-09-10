using System;
using JueMingR.Platform.Hotkeys;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Input;

namespace JueMingR.TerrariaHost.Hotkeys
{
    // One popup and one capture owner. Pending saves belong to Bindings, not to
    // window lifetime; an epoch prevents an old completion relabelling a new UI.
    internal sealed class HotkeyPopup
    {
        private readonly HotkeyBindings bindings;
        private readonly HotkeyRegistry registry;
        private readonly HostInputState input;
        internal readonly HotkeyPopupLayout Layout = new HotkeyPopupLayout();
        internal string Target { get; private set; }
        internal bool Visible { get { return Target != null; } }
        internal bool Capturing { get; private set; }
        internal string Status { get; private set; } = "双击功能行的键盘图标设置。";
        internal bool OwnsPointer { get; private set; }
        internal bool BlockPointer { get; private set; }
        private string lastClick;
        private long lastClickTime, epoch, pendingEpoch, pendingCommand;
        private int lastGeneration, page, armed = -1, armedGeneration;
        private F5Rect anchor, lastAnchor;
        private bool previousLeft, startMouseRelease;
        private bool awaitingSaveSlot;
        private const string ReadyMessage = "请选择开始录入。保存后若再改原版键位，不会自动检查或禁用。";
        private const string BusyMessage = "其他已提交的设置仍在保存，请稍候。";
        private HotkeyModifiers shownModifiers;
        internal HotkeyPopup(HotkeyBindings bindings, HotkeyRegistry registry, HostInputState input)
        { this.bindings = bindings; this.registry = registry; this.input = input; }
        internal void Click(string target, F5Rect bounds, int generation, int page, long milliseconds)
        {
            if (registry.Find(target) == null) return;
            bool twice = lastClick == target && lastGeneration == generation && this.page == page && milliseconds - lastClickTime <= 500 &&
                Math.Abs(lastAnchor.X - bounds.X) <= 6 && Math.Abs(lastAnchor.Y - bounds.Y) <= 6;
            lastClick = target; lastGeneration = generation; this.page = page; lastAnchor = bounds; lastClickTime = milliseconds;
            if (!twice) return;
            EndCapture(); Target = target; anchor = bounds; epoch++; armed = -1; lastClick = null;
            awaitingSaveSlot = bindings.Busy;
            Status = bindings.Error(target) ?? (bindings.Protected ? bindings.Message : awaitingSaveSlot ? BusyMessage : ReadyMessage);
        }
        internal void Process(bool active, int currentPage, float x, float y, bool geometryCurrent)
        {
            bindings.Poll();
            // Global I/O availability is distinct from this window's result.
            // A reopened target never inherits the previous target's success or
            // failure, but must leave its temporary busy prompt when I/O ends.
            if (awaitingSaveSlot && !bindings.Busy)
            {
                awaitingSaveSlot = false;
                if (Visible) Status = bindings.Error(Target) ?? (bindings.Protected ? bindings.Message : ReadyMessage);
            }
            if (pendingCommand != 0 && bindings.CompletionId == pendingCommand)
            {
                if (Visible && epoch == pendingEpoch && Target == bindings.CompletionAction) Status = bindings.Message;
                pendingCommand = 0;
            }
            OwnsPointer = BlockPointer = false;
            bool left = input.Hotkeys.IsDown(256);
            if (!active || currentPage != page && Visible) { Close(); previousLeft = input.SampleFocused ? left : true; return; }
            if (!Visible) { previousLeft = left; return; }
            bool pressed = input.Hotkeys.IsNew(256), released = !left && previousLeft && input.SampleFocused;
            OwnsPointer = Capturing || Layout.Panel.Contains(x, y) || armed >= 0 || input.HotkeyPointerOwned;
            BlockPointer = OwnsPointer;
            if (!Capturing && Layout.Panel.Contains(x, y))
                for (int key = 256; key <= 260; key++) if (input.Hotkeys.IsNew(key)) input.Hotkeys.SuppressKey(key);
            int hit = geometryCurrent ? Layout.Hit(x, y) : -1;
            if (!geometryCurrent) { armed = -1; if (Capturing) { EndCapture(); Status = "布局已变化，录入已取消，请重新开始。"; } }
            // Controls reserve their press before capture sees Mouse1. Suppressed
            // physical keys still release normally, so this does not fake edges.
            if (pressed && hit >= 0) { armed = hit; armedGeneration = Layout.Generation; input.Hotkeys.SuppressHeld(); }
            bool controlGesture = armed >= 0;
            if (released && armed >= 0)
            {
                int command = armed; bool valid = hit == command && armedGeneration == Layout.Generation; armed = -1;
                if (valid)
                {
                    if (command == 2) Close();
                    else if (command == 1) { EndCapture(); Submit(null); }
                    else if (Capturing) { EndCapture(); Status = "录入已取消，当前绑定保留。"; }
                    else if (bindings.Busy) { awaitingSaveSlot = true; Status = BusyMessage; }
                    else if (!bindings.Loaded || bindings.Protected) Status = bindings.Message ?? "快捷键文件暂不可编辑。";
                    else { Capturing = input.HotkeyCapture = true; epoch++; input.Hotkeys.SuppressHeld(); startMouseRelease = true; shownModifiers = (HotkeyModifiers)(-1); Status = "请按组合键。"; }
                }
            }
            if (Capturing && !controlGesture)
            {
                if (!startMouseRelease) { if (input.SampleFocused && !left) startMouseRelease = true; }
                else Capture();
            }
            // A newly suppressed middle/side press may already have mapped a
            // native command this tick. Retaining only its tail is too late.
            if (Capturing || OwnsPointer && input.Hotkeys.HasSuppressedKeys) input.ConsumeHotkeyActions();
            previousLeft = left;
        }
        private void Capture()
        {
            if (input.Hotkeys.IsNew(27)) { EndCapture(); Status = "录入已取消，当前绑定保留。"; return; }
            if (input.Hotkeys.SystemModifier) { Status = "Win 不参与组合；系统保留键可能不会送达游戏。"; return; }
            int key; int count = input.Hotkeys.NewPrimary(out key);
            if (count > 1) { EndCapture(); Status = "同时按下了多个主键，未保存。请重新开始，只按一个主键。"; return; }
            if (count == 0)
            {
                if (shownModifiers != input.Hotkeys.Modifiers) { shownModifiers = input.Hotkeys.Modifiers; Status = "等待主键：" + HotkeyChord.ModifierDisplay(shownModifiers); }
                return;
            }
            HotkeyChord chord; string reason;
            bool valid = HotkeyChord.TryCreate(key, input.Hotkeys.Modifiers, out chord, out reason);
            EndCapture();
            if (!valid) { Status = reason; return; }
            Submit(chord);
        }
        private void Submit(HotkeyChord chord)
        {
            string reason; long command;
            // One submit rejects internal conflicts and reads the current native
            // profile for an advisory only; no second confirmation is needed.
            if (!bindings.TrySet(Target, chord, VanillaHotkeyConflicts.Check, out command, out reason)) { Status = reason; return; }
            pendingCommand = command; pendingEpoch = epoch; Status = bindings.Message;
        }
        internal void Prepare(float width, float height, object font, Func<string, float, F5Size> measure)
        {
            if (!Visible) return;
            var chord = bindings.Get(Target);
            Layout.Build(width, height, font, anchor, registry.Find(Target).Name, chord == null ? "未绑定" : chord.DisplayText, Status, Capturing, measure);
        }
        private void EndCapture() { if (Capturing) input.Hotkeys.SuppressHeld(); Capturing = input.HotkeyCapture = false; }
        internal void Close() { EndCapture(); Target = null; epoch++; armed = -1; lastClick = null; awaitingSaveSlot = false; OwnsPointer = BlockPointer = false; }
    }
}
