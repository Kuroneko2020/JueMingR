using System;
using System.Text;
using JueMingR.TerrariaHost.EntityLabels;
using JueMingR.TerrariaHost.Notes;
using Microsoft.Xna.Framework.Input;
using ReLogic.Localization.IME;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Input
{
    // The existing Host owns physical sampling. This small ASCII field borrows
    // Terraria's committed-character queue and the same text/IME lease pattern
    // as Notes; it never polls a keyboard or owns hotkey preferences.
    internal sealed class HexTextInput
    {
        private readonly object token = new object();
        private readonly HostInputState input;
        private readonly INotesClipboard clipboard;
        private readonly INotesIme ime;
        private readonly StringBuilder committed = new StringBuilder(8);
        private StyleEditor editor;
        private bool leased, priorBlock, previousComposition;
        internal HexTextInput(HostInputState input, INotesClipboard clipboard = null, INotesIme ime = null)
        { this.input = input; this.clipboard = clipboard ?? new NotesClipboard(() => Main.instance.Window.Handle); this.ime = ime ?? new NativeIme(); }
        internal bool Editing { get { return editor != null; } }
        internal bool OwnsTextToken { get { return ReferenceEquals(Main.CurrentInputTextTakerOverride, token); } }
        internal bool OtherTextOwner { get { return Main.drawingPlayerChat || Main.editSign || Main.editChest || Main.CurrentInputTextTakerOverride != null && !OwnsTextToken; } }
        internal bool Begin(StyleEditor value)
        {
            if (OtherTextOwner) return false;
            End(true); editor = value; editor.BeginHex();
            // Clear at activation, before the next prefix, so a newly typed
            // first character waiting then is not thrown away as stale input.
            Main.clrInput(); return true;
        }
        internal void BeforeInput(bool active)
        {
            if (!Editing) return;
            if (!active || OtherTextOwner) { End(true); return; }
            if (!leased) { priorBlock = Main.blockInput; leased = true; }
            Main.CurrentInputTextTakerOverride = token; Main.blockInput = true; PlayerInput.WritingText = true; ime.Toggle(true);
        }
        internal void Process(bool active)
        {
            if (!Editing) return;
            if (!active || !input.SampleFocused || OtherTextOwner) { End(true); return; }
            if (!leased) return;
            bool composing = !String.IsNullOrEmpty(ime.Composition) || ime.Candidates;
            bool compositionPriority = composing || previousComposition;
            KeyboardState keys = input.KeyboardSample;
            bool control = keys.IsKeyDown(Keys.LeftControl) || keys.IsKeyDown(Keys.RightControl);
            bool shift = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
            bool enter = Pressed(Keys.Enter), escape = Pressed(Keys.Escape), tooLong = false;
            committed.Clear();
            int count = Math.Min(Main.keyCount, Math.Min(Main.keyInt.Length, Main.keyString.Length));
            for (int i = 0; i < count; i++)
            {
                int key = Main.keyInt[i];
                if (key == 13) enter = true; else if (key == 27) escape = true;
                else if (key >= 32 && key != 127 && !control)
                {
                    string text = Main.keyString[i] ?? "";
                    if (committed.Length + text.Length > 6) tooLong = true;
                    else if (!tooLong) committed.Append(text);
                }
            }
            Main.clrInput();
            // The IME's confirmation frame still carries committed characters.
            // Its priority only suppresses control actions, never those characters.
            if (!control)
            {
                if (tooLong) editor.SetError("请输入六位 RGB 色码");
                else if (committed.Length != 0) editor.Insert(committed.ToString());
            }
            if (!compositionPriority)
            {
                if (control && Pressed(Keys.A)) editor.SelectAll();
                else if (control && Pressed(Keys.V) || shift && Pressed(Keys.Insert))
                {
                    string text;
                    if (!clipboard.TryPaste(out text)) editor.SetError("未能读取剪贴板，草稿保留");
                    else
                    {
                        // Accept exactly one optional external #, never trim or
                        // truncate a longer candidate into an accidental color.
                        if (text != null && text.Length == 7 && text[0] == '#') text = text.Substring(1);
                        if (text == null || text.Length > 6) editor.SetError("粘贴内容须为六位 RGB 色码"); else editor.Insert(text);
                    }
                }
                else if (!control)
                {
                    if (Pressed(Keys.Back)) editor.Delete(true); else if (Pressed(Keys.Delete)) editor.Delete(false);
                    if (Pressed(Keys.Left)) editor.Move(-1, shift); else if (Pressed(Keys.Right)) editor.Move(1, shift);
                    else if (Pressed(Keys.Home)) editor.Move(-6, shift); else if (Pressed(Keys.End)) editor.Move(6, shift);
                }
                if (escape) End(true);
                else if (enter)
                { editor.CommitHex(); if (editor.Error == null) End(false); }
            }
            previousComposition = composing;
            SuppressKeyboard(); input.ConsumeHotkeyActions();
        }
        private bool Pressed(Keys key) { return input.Hotkeys.IsNew((int)key); }
        internal void End(bool cancel)
        {
            if (editor != null && cancel) editor.CancelDraft();
            editor = null;
            if (!leased) return;
            bool foreign = OtherTextOwner;
            if (!foreign) { ime.Toggle(false); Main.clrInput(); PlayerInput.WritingText = false; }
            if (OwnsTextToken) Main.CurrentInputTextTakerOverride = null;
            if (Main.blockInput) Main.blockInput = priorBlock;
            leased = false; previousComposition = false;
            SuppressKeyboard();
        }
        private void SuppressKeyboard()
        {
            // Pointer transfer to another normal entry is separate. Keyboard
            // tails remain suppressed until a real focused release is sampled.
            for (int key = 1; key < 256; key++) if (input.Hotkeys.IsDown(key)) input.Hotkeys.SuppressKey(key);
        }
        private sealed class NativeIme : INotesIme
        {
            public string Composition { get { return ReLogic.OS.Platform.Get<IImeService>().CompositionString; } }
            public bool Candidates { get { return ReLogic.OS.Platform.Get<IImeService>().IsCandidateListVisible; } }
            public void Toggle(bool enabled) { PlayerInput.WritingText = enabled; Main.instance.HandleIME(); }
        }
    }
}
