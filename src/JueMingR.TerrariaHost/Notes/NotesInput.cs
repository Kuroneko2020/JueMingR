using System;
using System.Text;
using JueMingR.Features.Notes;
using Microsoft.Xna.Framework.Input;
using ReLogic.OS;
using ReLogic.Localization.IME;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Notes
{
    internal interface INotesIme
    {
        string Composition { get; }
        bool Candidates { get; }
        void Toggle(bool enabled);
    }
    internal sealed class NotesInput
    {
        private readonly object owner = new object();
        private readonly NotesWorkspace workspace;
        private readonly INotesClipboard clipboard;
        private readonly INotesIme ime;
        private readonly StringBuilder committed = new StringBuilder(100);
        private KeyboardState previous, sample;
        private NoteEditor bound;
        private NoteEditor prepared;
        private bool leased, priorBlock, previousComposition, enterTail, escapeTail, keyboardTail;
        private int repeatTicks;
        internal NotesInput(NotesWorkspace workspace, INotesClipboard clipboard, INotesIme ime = null)
        { this.workspace = workspace; this.clipboard = clipboard; this.ime = ime ?? new NativeIme(); }
        internal string Composition { get; private set; } = "";
        internal string Error { get; private set; }
        internal bool Owned { get { return leased; } }
        internal bool OtherTextOwner
        {
            get { return Main.drawingPlayerChat || Main.editSign || Main.editChest ||
                Main.CurrentInputTextTakerOverride != null && !ReferenceEquals(Main.CurrentInputTextTakerOverride, owner); }
        }
        internal void BeforeSample(bool active)
        {
            if (active && workspace.Editor != null && !OtherTextOwner)
            {
                // Activation happens here, before this frame's mapping/zoom/UI keys.
                // A double click merely arms the editor in the preceding postfix.
                if (!leased) { priorBlock = Main.blockInput; leased = true; if (prepared != workspace.Editor) Main.clrInput(); }
                bound = workspace.Editor; Main.CurrentInputTextTakerOverride = owner;
                Main.blockInput = true; PlayerInput.WritingText = true; ime.Toggle(true);
            }
            else Release(false);
            if (keyboardTail && !OtherTextOwner) PlayerInput.WritingText = true;
        }
        internal void PrepareEditor()
        {
            if (prepared == workspace.Editor) return;
            prepared = workspace.Editor;
            // Discard pre-activation text at the click/completion boundary, not in
            // the next prefix where a newly typed first character may already wait.
            if (prepared != null && !OtherTextOwner) Main.clrInput();
        }
        internal void AfterSample(bool active, NotesTextLayout layout)
        {
            sample = Main.keyState;
            if (!active || OtherTextOwner || !ReferenceEquals(bound, workspace.Editor)) Release(false);
            if (leased)
            {
                Main.CurrentInputTextTakerOverride = owner; Main.blockInput = true; PlayerInput.WritingText = true;
                Composition = ime.Composition ?? "";
                bool composing = Composition.Length != 0 || ime.Candidates;
                bool imePriority = composing || previousComposition;
                if (imePriority && sample.IsKeyDown(Keys.Enter)) enterTail = true;
                if (imePriority && sample.IsKeyDown(Keys.Escape)) escapeTail = true;
                if (sample.IsKeyUp(Keys.Enter)) enterTail = false;
                if (sample.IsKeyUp(Keys.Escape)) escapeTail = false;
                ProcessText(imePriority || enterTail, imePriority || escapeTail);
                if (workspace.Editor != null && !imePriority) ProcessKeys(layout);
                if (leased && workspace.Editor != null) PlayerInput.WritingText = true;
                previousComposition = composing;
                // Raw shortcuts earlier in the next Update consume this cached state.
                // Never restore consumed keys after Update and replay them there.
                Main.keyState = default(KeyboardState); keyboardTail = sample.GetPressedKeys().Length != 0;
            }
            else if (keyboardTail && !OtherTextOwner)
            {
                keyboardTail = sample.GetPressedKeys().Length != 0;
                Main.keyState = default(KeyboardState);
            }
            previous = sample;
        }
        private void ProcessText(bool suppressEnter, bool suppressEscape)
        {
            committed.Clear(); bool enter = false, escape = false;
            int count = Math.Min(Main.keyCount, Math.Min(Main.keyInt.Length, Main.keyString.Length));
            for (int i = 0; i < count; i++)
            {
                int key = Main.keyInt[i];
                if (key == 13) enter = true;
                else if (key == 27) escape = true;
                else if (key >= 32 && key != 127) committed.Append(Main.keyString[i]);
            }
            Main.clrInput();
            if (bound == null) return;
            if (committed.Length != 0) bound.Insert(committed.ToString());
            if (escape && !suppressEscape) { Release(true); workspace.CancelEdit(); return; }
            if (enter && !suppressEnter)
            {
                if (bound.IsTitle) { FinishComposition(false); workspace.Request(new NotesAction(NotesActionKind.FinishEdit)); }
                else bound.Insert("\n");
            }
        }
        private void ProcessKeys(NotesTextLayout layout)
        {
            NoteEditor editor = workspace.Editor; if (editor == null) return;
            bool control = sample.IsKeyDown(Keys.LeftControl) || sample.IsKeyDown(Keys.RightControl);
            bool shift = sample.IsKeyDown(Keys.LeftShift) || sample.IsKeyDown(Keys.RightShift);
            bool cut = control && Pressed(Keys.X) || shift && Pressed(Keys.Delete);
            if (cut || control && Pressed(Keys.C) || control && Pressed(Keys.Insert))
            {
                if (clipboard.TryCopy(editor.Text)) { if (cut) editor.ClearAfterCopy(); Error = null; }
                else Error = "剪贴板不可用，草稿没有删改。";
                return;
            }
            if (control && Pressed(Keys.V) || shift && Pressed(Keys.Insert))
            {
                string text; if (clipboard.TryPaste(out text)) { editor.Insert(text); Error = null; }
                else Error = "未能读取剪贴板（不可用或超过正文上限），草稿保留。";
                return;
            }
            if (control) return; // No undo/selection contract: Ctrl+Z must never clear.
            if (sample == previous) repeatTicks++; else repeatTicks = 0;
            bool repeat = repeatTicks >= 24 && repeatTicks % 3 == 0;
            if (Pressed(Keys.Left) || repeat && sample.IsKeyDown(Keys.Left)) editor.Move(-1);
            if (Pressed(Keys.Right) || repeat && sample.IsKeyDown(Keys.Right)) editor.Move(1);
            if (Pressed(Keys.Back) || repeat && sample.IsKeyDown(Keys.Back)) editor.Backspace();
            if (Pressed(Keys.Delete) || repeat && sample.IsKeyDown(Keys.Delete)) editor.Delete();
            if (layout == null || layout.Text != editor.Text) return;
            if (Pressed(Keys.Up) || repeat && sample.IsKeyDown(Keys.Up)) editor.MoveTo(layout.Vertical(editor.Caret, -1));
            if (Pressed(Keys.Down) || repeat && sample.IsKeyDown(Keys.Down)) editor.MoveTo(layout.Vertical(editor.Caret, 1));
            if (Pressed(Keys.Home)) editor.MoveTo(layout.LineHome(editor.Caret));
            if (Pressed(Keys.End)) editor.MoveTo(layout.LineEnd(editor.Caret));
        }
        private bool Pressed(Keys key) { return sample.IsKeyDown(key) && previous.IsKeyUp(key); }
        internal void FinishComposition(bool cancel)
        {
            if (!leased || OtherTextOwner) return;
            // Keep the old draft bound while Terraria finalizes IME. Synchronous
            // completion characters belong to it, before a save snapshot is frozen.
            PlayerInput.WritingText = false; ime.Toggle(false);
            if (!cancel) DrainFinalText();
            Main.clrInput(); Composition = ""; previousComposition = false;
        }
        private void DrainFinalText()
        {
            committed.Clear();
            for (int i = 0; i < Math.Min(Main.keyCount, Main.keyInt.Length); i++)
                if (Main.keyInt[i] >= 32 && Main.keyInt[i] != 127) committed.Append(Main.keyString[i]);
            if (bound != null && committed.Length != 0) bound.Insert(committed.ToString());
        }
        internal void Release(bool cancel)
        {
            if (!leased) return;
            bool other = OtherTextOwner;
            if (!other) FinishComposition(cancel);
            if (ReferenceEquals(Main.CurrentInputTextTakerOverride, owner)) Main.CurrentInputTextTakerOverride = null;
            // Return our block lease even when chat/sign/chest takes over. Those
            // owners keep their own text/IME gates; leaving our bool set here
            // would block vanilla input after they close, with no lease to undo it.
            if (Main.blockInput) Main.blockInput = priorBlock;
            if (!other) PlayerInput.WritingText = false;
            bound = null; leased = false; Composition = ""; previousComposition = false;
        }
        private sealed class NativeIme : INotesIme
        {
            public string Composition { get { return ReLogic.OS.Platform.Get<IImeService>().CompositionString; } }
            public bool Candidates { get { return ReLogic.OS.Platform.Get<IImeService>().IsCandidateListVisible; } }
            public void Toggle(bool enabled) { PlayerInput.WritingText = enabled; Main.instance.HandleIME(); }
        }
    }
}
