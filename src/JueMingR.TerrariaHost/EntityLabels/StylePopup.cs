using System;
using JueMingR.Features.EntityLabels;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Input;
using JueMingR.TerrariaHost.Notes;
using Microsoft.Xna.Framework.Input;

namespace JueMingR.TerrariaHost.EntityLabels
{
    // Exactly one target and one capture. Submitted preferences outlive the
    // popup; closing only discards the unfinished editor state.
    internal sealed class StylePopup
    {
        private readonly HostEntityLabels host;
        private readonly WorldTargets.HostWorldTargets worldTargets;
        private StyleTarget selection;
        private readonly HostInputState input;
        internal readonly StylePopupLayout Layout = new StylePopupLayout();
        internal readonly HexTextInput TextInput;
        internal EntityLabelKind? Target { get { return selection?.Entity; } }
        internal Platform.WorldTargets.WorldTargetKind? WorldTarget { get { return selection?.World; } }
        internal StyleEditor Editor { get; private set; }
        internal bool Visible { get { return selection != null; } }
        internal bool OwnsPointer { get; private set; }
        internal bool BlockPointer { get; private set; }
        internal bool ConsumeWheel { get; private set; }
        internal int ActiveSlider { get; private set; } = -1;
        internal StylePopupCommand Hovered { get; private set; }
        internal StylePopupCommand Pressed { get { return armed; } }
        internal bool HasCapture { get { return ActiveSlider >= 0 || armed != StylePopupCommand.None || TextInput.Editing; } }
        internal string Failure { get; private set; }
        private int page, armedGeneration;
        private bool previousLeft;
        private StylePopupCommand armed;
        internal StylePopup(HostEntityLabels host, HostInputState input, INotesClipboard clipboard = null, INotesIme ime = null, WorldTargets.HostWorldTargets worldTargets = null)
        { this.host = host; this.worldTargets = worldTargets; this.input = input; TextInput = new HexTextInput(input, clipboard, ime); }
        internal void Click(EntityLabelKind target, F5Rect anchor, int currentPage)
        { if (host != null) Open(StyleTarget.For(host, target), currentPage); }
        internal void Click(Platform.WorldTargets.WorldTargetKind target, F5Rect anchor, int currentPage)
        { if (worldTargets != null) Open(StyleTarget.For(worldTargets, target), currentPage); }
        private void Open(StyleTarget target, int currentPage)
        {
            bool same = target.Same(selection); Close();
            if (same || !target.CanConfigure()) return;
            selection = target; page = currentPage; Failure = null;
            // The editor captures this target, never the next popup selection.
            Editor = new StyleEditor(target.Color(), target.SetColor); Layout.Reset();
        }
        internal int NameSize { get { return selection?.Size == null ? 0 : selection.Size(); } }
        internal string Message { get { return Editor?.Error ?? selection?.Message(); } }
        internal bool ContainsPointer(float x, float y) { return Visible && Layout.Panel.Contains(x, y); }
        internal void BeforeInput(bool active) { TextInput.BeforeInput(active && Visible); }
        internal void YieldTextToEntry() { if (ActiveSlider < 0 && armed == StylePopupCommand.None) TextInput.End(true); }
        internal void Close()
        {
            if (HasCapture) input.Hotkeys.SuppressHeld();
            TextInput.End(true); Editor?.CancelDraft(); selection = null; ActiveSlider = -1; armed = StylePopupCommand.None;
            Hovered = StylePopupCommand.None; OwnsPointer = BlockPointer = ConsumeWheel = false;
        }
        private void CancelGesture()
        { ActiveSlider = -1; armed = StylePopupCommand.None; TextInput.End(true); Editor?.CancelDraft(); input.Hotkeys.SuppressHeld(); }
        internal void Prepare(float width, float height, float scale, object font, Func<string, float, F5Size> measure, int skin, F5Rect anchor)
        {
            if (!Visible) return;
            try
            {
                int before = Layout.Generation;
                bool externalChange = before > 0 && !Layout.Matches(width, height, scale, font, skin, anchor);
                Layout.Build(width, height, scale, font, measure, skin, anchor, selection.Title, selection.Entity == EntityLabelKind.Critter, Editor, NameSize, Message);
                // Error text may legitimately grow the panel while HEX owns its
                // draft. Only external geometry invalidates that text lease.
                if (before != Layout.Generation && (externalChange && HasCapture || ActiveSlider >= 0 || armed != StylePopupCommand.None))
                {
                    CancelGesture();
                    Layout.Build(width, height, scale, font, measure, skin, anchor, selection.Title, selection.Entity == EntityLabelKind.Critter, Editor, NameSize, Message);
                }
            }
            catch (Exception e) { Failure = "显示设置窗口暂不可用：" + e.GetType().Name; Close(); }
        }
        internal void Process(bool active, int currentPage, float x, float y, bool geometryCurrent, int wheel = 0)
        {
            OwnsPointer = BlockPointer = ConsumeWheel = false;
            bool left = input.Hotkeys.IsDown(256), wasEditing = TextInput.Editing, captured = HasCapture;
            if (!active || !input.SampleFocused || Visible && currentPage != page)
            { Close(); previousLeft = input.SampleFocused ? left : true; return; }
            if (!Visible) { previousLeft = left; return; }
            if (!geometryCurrent && captured)
            { CancelGesture(); BlockPointer = OwnsPointer = true; input.ConsumeHotkeyActions(); previousLeft = left; return; }
            TextInput.Process(active);
            bool pressed = input.Hotkeys.IsNew(256), released = !left && previousLeft;
            Hovered = geometryCurrent ? Layout.Hit(x, y) : StylePopupCommand.None;
            OwnsPointer = captured || HasCapture || ContainsPointer(x, y) || input.HotkeyPointerOwned;
            BlockPointer = OwnsPointer;
            if (!wasEditing && input.Hotkeys.IsNew((int)Keys.Escape))
            {
                input.Hotkeys.SuppressKey((int)Keys.Escape);
                if (ActiveSlider >= 0 || armed != StylePopupCommand.None) CancelGesture(); else Close();
                input.ConsumeHotkeyActions(); previousLeft = left; return;
            }
            if (geometryCurrent && pressed)
            {
                if (Layout.HexField.Offset(Layout.Panel.X, Layout.Panel.Y).Contains(x, y)) TextInput.Begin(Editor);
                else
                {
                    for (int i = 0; i < 3; i++) if (Layout.Sliders[i].Offset(Layout.Panel.X, Layout.Panel.Y).Contains(x, y))
                    { TextInput.End(true); ActiveSlider = i; armedGeneration = Layout.Generation; break; }
                    if (Hovered != StylePopupCommand.None && StylePopupLayout.Enabled(Hovered, NameSize))
                    { TextInput.End(true); armed = Hovered; armedGeneration = Layout.Generation; }
                }
            }
            if (ActiveSlider >= 0)
            {
                if (!geometryCurrent || armedGeneration != Layout.Generation) CancelGesture();
                else
                {
                    F5Rect track = Layout.Sliders[ActiveSlider].Offset(Layout.Panel.X, Layout.Panel.Y);
                    Editor.PreviewHsl(ActiveSlider, (x - track.X) / track.Width * (ActiveSlider == 0 ? 360 : 100));
                    if (released) { Editor.Commit(); ActiveSlider = -1; }
                }
            }
            if (released && armed != StylePopupCommand.None)
            {
                StylePopupCommand command = armed; armed = StylePopupCommand.None;
                if (geometryCurrent && armedGeneration == Layout.Generation && command == Hovered)
                {
                    if (command == StylePopupCommand.Close) Close();
                    else if (command == StylePopupCommand.Smaller) selection.StepSize?.Invoke(-1);
                    else if (command == StylePopupCommand.Larger) selection.StepSize?.Invoke(1);
                    else if (command == StylePopupCommand.Reset) { selection.Reset(); Editor.Load(selection.Color()); }
                }
            }
            if (Visible && ContainsPointer(x, y))
                for (int key = 256; key <= 260; key++) if (input.Hotkeys.IsNew(key)) input.Hotkeys.SuppressKey(key);
            OwnsPointer |= HasCapture; BlockPointer |= OwnsPointer;
            ConsumeWheel = OwnsPointer && wheel != 0;
            if (captured || HasCapture || OwnsPointer && input.Hotkeys.HasSuppressedKeys) { input.Hotkeys.SuppressHeld(); input.ConsumeHotkeyActions(); }
            previousLeft = left;
        }
    }
}
