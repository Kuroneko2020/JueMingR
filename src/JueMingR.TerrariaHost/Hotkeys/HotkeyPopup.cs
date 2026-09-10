using System;
using JueMingR.Platform.Hotkeys;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Input;

namespace JueMingR.TerrariaHost.Hotkeys
{
    // One capture owner; completion belongs to Bindings independently of window
    // lifetime. An epoch prevents results leaking into another target.
    internal sealed class HotkeyPopup
    {
        private readonly HotkeyBindings bindings;
        private readonly HotkeyRegistry registry;
        private readonly HostInputState input;
        internal readonly HotkeyPopupLayout Layout = new HotkeyPopupLayout();
        internal string Target { get; private set; }
        internal bool Visible { get { return Target != null; } }
        internal bool Capturing { get; private set; }
        internal HotkeyFeedback Feedback { get; private set; } = new HotkeyFeedback(HotkeyFeedbackKind.Ready);
        internal string Status { get { return Feedback.Message; } }
        internal bool OwnsPointer { get; private set; }
        internal bool BlockPointer { get; private set; }
        internal bool HelpVisible { get; private set; }
        internal HotkeyPopupCommand Hovered { get; private set; } = HotkeyPopupCommand.None;
        internal HotkeyPopupCommand Pressed { get { return armed; } }
        private string lastClick;
        private long lastClickTime, epoch, pendingEpoch, pendingCommand;
        private int lastGeneration, page, armedGeneration;
        private HotkeyPopupCommand armed = HotkeyPopupCommand.None;
        private F5Rect anchor, lastAnchor;
        private bool previousLeft, awaitingSaveSlot;
        private HotkeyModifiers shownModifiers;
        private HotkeyChord candidate;
        private HotkeyPopupView view;
        internal HotkeyPopup(HotkeyBindings bindings, HotkeyRegistry registry, HostInputState input)
        { this.bindings = bindings; this.registry = registry; this.input = input; }
        private HotkeyFeedback InitialFeedback()
        {
            if (!bindings.Loaded) return bindings.Feedback;
            if (bindings.Protected)
            {
                // Protection is global; a new target must not inherit another
                // command's retained-binding claim or success/failure result.
                bool unknown = bindings.CommitUnconfirmed;
                string current = bindings.Get(Target) == null ? "该功能当前没有有效绑定。" : "当前显示的绑定仍有效。";
                return new HotkeyFeedback(unknown ? HotkeyFeedbackKind.Unconfirmed : HotkeyFeedbackKind.Protected,
                    unknown ? "磁盘结果未确认，文件已保护" : "快捷键文件已保护",
                    current + (bindings.Error(Target) ?? (unknown ? "请退出后保留文件及恢复材料核对磁盘状态。" : "请退出后检查原文件或恢复材料。")));
            }
            return bindings.Busy ? new HotkeyFeedback(HotkeyFeedbackKind.Saving, "其他设置正在保存，请稍候") : new HotkeyFeedback(HotkeyFeedbackKind.Ready);
        }
        internal void Click(string target, F5Rect bounds, int generation, int page, long milliseconds)
        {
            if (registry.Find(target) == null) return;
            bool twice = lastClick == target && lastGeneration == generation && this.page == page && milliseconds - lastClickTime <= 500 &&
                Math.Abs(lastAnchor.X - bounds.X) <= 6 && Math.Abs(lastAnchor.Y - bounds.Y) <= 6;
            lastClick = target; lastGeneration = generation; this.page = page; lastAnchor = bounds; lastClickTime = milliseconds;
            if (!twice) return;
            EndCapture(); Target = target; anchor = bounds; epoch++; armed = HotkeyPopupCommand.None; lastClick = null;
            HelpVisible = false; candidate = null; view = null; Layout.ResetAnchor();
            awaitingSaveSlot = bindings.Busy; Feedback = InitialFeedback();
        }
        internal bool ContainsPointer(float x, float y)
        { return Visible && (Layout.Panel.Contains(x, y) || HelpVisible && Layout.HelpPanel.Contains(x, y)); }
        internal void Process(bool active, int currentPage, float x, float y, bool geometryCurrent)
        {
            bindings.Poll();
            if (Visible && (awaitingSaveSlot && !bindings.Busy || Feedback.Kind == HotkeyFeedbackKind.Loading && bindings.Loaded))
            { awaitingSaveSlot = false; Feedback = InitialFeedback(); }
            if (pendingCommand != 0 && bindings.CompletionId == pendingCommand)
            {
                if (Visible && epoch == pendingEpoch && Target == bindings.CompletionAction) { Feedback = bindings.Feedback; candidate = null; }
                pendingCommand = 0;
            }
            OwnsPointer = BlockPointer = false;
            bool left = input.Hotkeys.IsDown(256);
            if (!active || !input.SampleFocused || currentPage != page && Visible) { Close(); previousLeft = input.SampleFocused ? left : true; return; }
            if (!Visible) { previousLeft = left; return; }
            bool pressed = input.Hotkeys.IsNew(256), released = !left && previousLeft;
            bool wasHelp = HelpVisible && Layout.HelpPanel.Contains(x, y);
            Hovered = geometryCurrent ? Layout.Hit(x, y) : HotkeyPopupCommand.None;
            HelpVisible = geometryCurrent && (Hovered == HotkeyPopupCommand.Help || wasHelp);
            bool helpPointer = Hovered == HotkeyPopupCommand.Help || wasHelp;
            OwnsPointer = Capturing || ContainsPointer(x, y) || armed != HotkeyPopupCommand.None || input.HotkeyPointerOwned;
            BlockPointer = OwnsPointer;
            // Disabled controls and help reserve physical keys even in capture.
            // Suppression retains the tail; keycaps have no command or hover.
            bool controlPointer = geometryCurrent && Layout.OverControl(x, y);
            if ((!Capturing && ContainsPointer(x, y)) || controlPointer || helpPointer)
                for (int key = 256; key <= 260; key++) if (input.Hotkeys.IsNew(key)) input.Hotkeys.SuppressKey(key);
            bool cancelledGesture = armed != HotkeyPopupCommand.None && (!geometryCurrent || armedGeneration != Layout.Generation);
            if (!geometryCurrent || cancelledGesture)
            {
                armed = HotkeyPopupCommand.None;
                if (Capturing) Cancel("布局已变化，录入已取消");
            }
            if (pressed && Hovered != HotkeyPopupCommand.None && !cancelledGesture)
            { armed = Hovered; armedGeneration = Layout.Generation; input.Hotkeys.SuppressHeld(); }
            bool controlGesture = armed != HotkeyPopupCommand.None || cancelledGesture;
            if (released && armed != HotkeyPopupCommand.None)
            {
                var command = armed; bool valid = Hovered == command && armedGeneration == Layout.Generation; armed = HotkeyPopupCommand.None;
                if (valid)
                {
                    if (command == HotkeyPopupCommand.Close) Close();
                    else if (command == HotkeyPopupCommand.Clear && !Capturing && !bindings.Busy && !bindings.Protected) Submit(null);
                    else if (command == HotkeyPopupCommand.Record)
                    {
                        if (Capturing) Cancel("录入已取消");
                        else if (bindings.Loaded && !bindings.Busy && !bindings.Protected)
                        { Capturing = input.HotkeyCapture = true; epoch++; candidate = null; shownModifiers = HotkeyModifiers.None; input.Hotkeys.SuppressHeld(); Feedback = new HotkeyFeedback(HotkeyFeedbackKind.Capturing, "等待主键"); }
                    }
                }
            }
            if (Capturing && !controlGesture) Capture();
            if (Capturing || OwnsPointer && input.Hotkeys.HasSuppressedKeys) input.ConsumeHotkeyActions();
            previousLeft = left;
        }
        private void Cancel(string message)
        { EndCapture(); candidate = null; Feedback = new HotkeyFeedback(HotkeyFeedbackKind.Cancelled, message); }
        private void Capture()
        {
            if (input.Hotkeys.IsNew(27)) { Cancel("录入已取消"); return; }
            if (input.Hotkeys.SystemModifier) { if (Feedback.Kind != HotkeyFeedbackKind.Rejected) Feedback = new HotkeyFeedback(HotkeyFeedbackKind.Rejected, "Win 不参与组合"); return; }
            int key; int count = input.Hotkeys.NewPrimary(out key);
            if (count > 1) { EndCapture(); Feedback = new HotkeyFeedback(HotkeyFeedbackKind.Rejected, "同时按下多个主键，未保存", "请重新录入，只按一个主键。"); return; }
            if (count == 0)
            {
                int modifiers = 0; for (int i = 0; i < 6; i++) if (((int)input.Hotkeys.Modifiers & (1 << i)) != 0) modifiers++;
                var kind = modifiers > 3 ? HotkeyFeedbackKind.Rejected : HotkeyFeedbackKind.Capturing;
                if (shownModifiers != input.Hotkeys.Modifiers || Feedback.Kind != kind)
                { shownModifiers = input.Hotkeys.Modifiers; Feedback = new HotkeyFeedback(kind, modifiers > 3 ? "请松开多余修饰键" : "等待主键"); }
                return;
            }
            HotkeyChord chord; string reason;
            bool valid = HotkeyChord.TryCreate(key, input.Hotkeys.Modifiers, out chord, out reason);
            EndCapture();
            if (!valid) { Feedback = new HotkeyFeedback(HotkeyFeedbackKind.Rejected, reason); return; }
            Submit(chord);
        }
        private void Submit(HotkeyChord chord)
        {
            string reason; long command;
            if (!bindings.TrySet(Target, chord, VanillaHotkeyConflicts.Check, out command, out reason))
            { candidate = null; Feedback = new HotkeyFeedback(HotkeyFeedbackKind.Rejected, reason); return; }
            pendingCommand = command; pendingEpoch = epoch; candidate = chord; Feedback = bindings.Feedback;
        }
        internal void Prepare(float width, float height, object font, Func<string, float, F5Size> measure, int skin = 0)
        {
            if (!Visible) return;
            var chord = bindings.Get(Target); bool editable = bindings.Loaded && !bindings.Busy && !bindings.Protected;
            bool known = bindings.Loaded && (chord != null || !bindings.Protected);
            if (view == null || !Equals(view.Effective, chord) || !Equals(view.Candidate, candidate) || view.Modifiers != shownModifiers ||
                !ReferenceEquals(view.Feedback, Feedback) || view.Editable != editable || view.Capturing != Capturing || view.Known != known)
                view = new HotkeyPopupView(registry.Find(Target).Name, chord, candidate, shownModifiers, Feedback, editable, Capturing, known);
            Layout.Build(width, height, font, anchor, view, measure, skin);
        }
        private void EndCapture() { if (Capturing) input.Hotkeys.SuppressHeld(); Capturing = input.HotkeyCapture = false; }
        internal void Close()
        { EndCapture(); Target = null; epoch++; armed = Hovered = HotkeyPopupCommand.None; lastClick = null; awaitingSaveSlot = HelpVisible = false; candidate = null; OwnsPointer = BlockPointer = false; }
    }
}
