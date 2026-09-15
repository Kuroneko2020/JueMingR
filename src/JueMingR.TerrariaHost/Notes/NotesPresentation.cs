using System;
using JueMingR.Features.Notes;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Notes
{
    internal sealed class NotesPresentation
    {
        private readonly NotesWorkspace workspace;
        private readonly NotesRenderer renderer = new NotesRenderer();
        private readonly NotesInput input;
        private readonly NotesCards cards;
        private readonly NotesPins pins;
        private F5Interaction shell;
        private Matrix matrix;
        private Vector2 screen;
        private bool ready, wasActive, previousLeft;
        private bool textLeftTail, textRightTail, seenBusy;
        private long seenRevision = -1, seenReadingRevision = -1, seenEditRevision = -1, seenCaretRevision = -1;
        private int seenLayout = -1;
        private float seenScroll = -1;
        private Vector2 preparedScreen;
        private object preparedFont;
        private NoteEditor preparedEditor;
        private string preparedFeedback;
        private NotesAction externalLeave;
        private Action<bool> leaveCompleted;
        internal bool RequestSafeLeave(Action<bool> completed)
        {
            if (leaveCompleted != null) return false;
            externalLeave = new NotesAction(NotesActionKind.Leave, x: -1);
            if (!Request(externalLeave)) { externalLeave = null; return false; }
            leaveCompleted = completed; return true;
        }
        private void CompleteLeave(bool success)
        {
            Action<bool> callback = leaveCompleted; leaveCompleted = null; externalLeave = null;
            // Retire the dependent navigation through Notes' existing epoch.
            // Its accepted save still updates the acknowledged editor baseline.
            if (callback != null && !success) workspace.Suspend();
            callback?.Invoke(success);
        }
        internal NotesPresentation(NotesWorkspace workspace)
        {
            this.workspace = workspace;
            input = new NotesInput(workspace, new NotesClipboard(() => Main.instance.Window.Handle));
            cards = new NotesCards(workspace, input, renderer, action => Request(action));
            pins = new NotesPins(workspace, renderer, Request);
        }
        internal bool OwnsPointer { get { return ready && (pins.OwnsPointer || input.Owned || cards.Selecting); } }
        internal bool ConsumeLeft { get; private set; }
        internal bool ConsumeRight { get; private set; }
        internal bool ConsumeWheel { get { return pins.ConsumeWheel || input.Owned || cards.Selecting; } }
        internal bool OtherTextOwner { get { return input.OtherTextOwner; } }
        internal void Attach(F5Interaction state)
        {
            shell = state;
            shell.BeforeLeave = page =>
            {
                if (shell.Page != 4) return true;
                Request(new NotesAction(NotesActionKind.Leave, x: page)); return false;
            };
        }
        internal void BeforeInput(bool active)
        { input.BeforeSample(ready && active && shell.Visible && shell.Page == 4); }
        internal bool Wheel(float x, float y, int wheel)
        { return ready && cards.Wheel(shell, x, y, wheel); }
        internal void ProcessInput(bool active, Matrix transform, Vector2 dimensions, Vector2 raw, bool focused = true, bool blockButtons = false, KeyboardState? physicalSample = null)
        {
            matrix = transform; screen = dimensions;
            bool left = PlayerInput.MouseInfo.LeftButton == ButtonState.Pressed, right = PlayerInput.MouseInfo.RightButton == ButtonState.Pressed;
            // NotesInput consumes the sampled keyboard. Freeze modifiers first,
            // then honor the editor/F5 capture before considering any screen pin.
            KeyboardState modifiers = physicalSample ?? Main.keyState;
            bool shift = modifiers.IsKeyDown(Keys.LeftShift) || modifiers.IsKeyDown(Keys.RightShift);
            bool control = modifiers.IsKeyDown(Keys.LeftControl) || modifiers.IsKeyDown(Keys.RightControl);
            bool enabled = ready && active && !input.OtherTextOwner;
            if (input.Owned) { textLeftTail |= left; textRightTail |= right; }
            input.AfterSample(enabled && shell.Visible && shell.Page == 4, cards.EditingLayout);
            if (enabled && focused && !blockButtons && shell.Visible && shell.Page == 4)
                cards.Pointer(shell, left && !previousLeft, !left && previousLeft, left, shift);
            if (input.Owned || cards.Selecting) { textLeftTail |= left; textRightTail |= right; }
            pins.Pointer(raw.X, raw.Y, left, right, PlayerInput.ScrollWheelDeltaForUI, enabled, focused,
                shell.OwnsPointer || input.Owned || cards.Selecting || textLeftTail || textRightTail, screen.X, screen.Y, shift, control, blockButtons);
            ConsumeLeft = textLeftTail || pins.ConsumeLeft; ConsumeRight = textRightTail || pins.ConsumeRight;
            if (focused && !left) textLeftTail = false;
            if (focused && !right) textRightTail = false;
            if (!enabled && wasActive) Suspend(!focused);
            wasActive = enabled; previousLeft = focused ? left : true; ApplyNavigation();
        }
        internal void Prepare(bool active, Matrix transform, Vector2 dimensions)
        {
            matrix = transform; screen = dimensions; ApplyNavigation();
            if (!active || input.OtherTextOwner)
            { Suspend(); ready = false; renderer.Dispose(); return; }
            bool wasReady = ready; ready = renderer.Refresh();
            if (!ready) { Suspend(); return; }
            renderer.BeginLayoutFrame();
            bool contentChanged = !wasReady || seenRevision != workspace.Feature.Revision || preparedScreen != screen || preparedFont != renderer.FontIdentity;
            NoteEditor editor = workspace.Editor; string feedback = Feedback(out Color feedbackColor);
            if (shell.Visible && shell.Page == 4 && (contentChanged || preparedEditor != editor || seenEditRevision != (editor == null ? -1 : editor.Revision) ||
                seenCaretRevision != (editor == null ? -1 : editor.CaretRevision) || seenBusy != workspace.Feature.Busy || seenLayout != shell.Layout.Generation || seenScroll != shell.Scroll || preparedFeedback != feedback || cards.PendingLayout || cards.ActionStateChanged))
                cards.Prepare(shell, feedback, feedbackColor);
            if (contentChanged || seenBusy != workspace.Feature.Busy || seenReadingRevision != workspace.Feature.ReadingRevision || pins.PendingLayout) pins.Prepare(screen.X, screen.Y);
            preparedEditor = editor; seenEditRevision = editor == null ? -1 : editor.Revision; seenCaretRevision = editor == null ? -1 : editor.CaretRevision;
            seenRevision = workspace.Feature.Revision; preparedScreen = screen; preparedFont = renderer.FontIdentity; seenBusy = workspace.Feature.Busy;
            seenReadingRevision = workspace.Feature.ReadingRevision;
            seenLayout = shell.Visible && shell.Page == 4 ? shell.Layout.Generation : -1; seenScroll = shell.Scroll; preparedFeedback = feedback;
            input.PrepareEditor();
        }
        private string Feedback(out Color color)
        {
            var feature = workspace.Feature; color = Color.Salmon;
            if (!feature.Loaded) return "正在加载笔记……";
            if (feature.NeedsRecovery) return "无法确认是否保存成功，已暂停保存。";
            if (!feature.Readable)
            {
                string cause = feature.Error == "UnsupportedVersion" ? "笔记由较新版本保存，暂时无法打开" : feature.Error == "UnknownFields" ? "笔记包含当前版本不支持的内容，暂时无法打开" :
                    feature.Error == "another-writer" ? "笔记正被其他程序使用，暂时无法打开" : feature.Error == "missing-document-with-recovery-material" ? "未找到完整的笔记文件，暂时无法打开" : "笔记无法读取";
                return cause + "。现有文件未改动。";
            }
            if (workspace.Error != null) return workspace.Error;
            if (input.Error != null) return input.Error;
            if (workspace.Editor != null && workspace.Editor.Error != null) return workspace.Editor.Error;
            if (feature.ReadingError != null) return feature.ReadingError;
            color = Color.LightGray;
            if (feature.Busy) return workspace.Editor == null ? "正在保存……" : "正在保存；取消编辑不会取消这次保存。";
            if (input.HasComposition) { color = Color.Gold; return "Enter 确认候选，Esc 取消输入。"; }
            string editState = workspace.Editor != null && workspace.Editor.Dirty ? "未保存 · " : "";
            if (editState.Length != 0) color = Color.Gold;
            if (workspace.Editor != null) return workspace.Editor.IsTitle
                ? editState + "Enter 保存，Esc 取消编辑。"
                : editState + "Enter 换行，Esc 取消编辑。";
            if (feature.Saved.Notes.Count == 0) return "点击 + 新建笔记，双击标题或正文编辑。";
            if (workspace.DeleteConfirmation != null) return "点击确认删除，或取消删除。";
            return "双击标题或正文编辑。悬挂后可单独阅读，移到便签上查看操作提示。";
        }
        private bool Request(NotesAction action)
        {
            if (leaveCompleted != null && !ReferenceEquals(externalLeave, action)) CompleteLeave(false);
            input.FinishComposition(false);
            // Finalization can still leave a candidate or half of a WM_CHAR pair.
            // Keep its editor alive until a later complete input can be saved.
            // Navigation/create/pin also depend on committing this editor.
            // Ordinary field switching retains its explicit discard-old-tail
            // boundary; it must not transfer a half character to another field.
            if (action.Kind != NotesActionKind.BeginEdit && input.HasComposition) return false;
            if (action.Kind == NotesActionKind.Pin)
            {
                int count = 0; foreach (Note note in workspace.Feature.Saved.Notes) if (note.Pinned) count++;
                Vector2 origin = Vector2.Transform(new Vector2(shell.X + shell.Layout.Window.Width + 12, shell.Y + 32), matrix);
                int x = (int)Math.Max(0, Math.Min(Math.Max(0, screen.X - NotesPins.Width), origin.X + count % 8 * 20));
                int y = (int)Math.Max(0, Math.Min(Math.Max(0, screen.Y - NotesPins.Height), origin.Y + count % 8 * 20));
                action = new NotesAction(action.Kind, action.Id, x: x, y: y);
            }
            return workspace.Request(action);
        }
        private void ApplyNavigation()
        {
            NotesAction action = workspace.TakeNavigation();
            if (action == null) { if (leaveCompleted != null && !workspace.Feature.Busy) CompleteLeave(false); return; }
            bool completed = ReferenceEquals(action, externalLeave);
            input.Release(false); cards.Suspend();
            if (action.X < 0) shell.Close(); else shell.Navigate(action.X);
            if (leaveCompleted != null) CompleteLeave(completed);
        }
        internal void DrawPins()
        { if (ready && pins.HasPins) renderer.Pass(Matrix.Identity, null, pins.Draw); }
        internal void DrawCards()
        {
            if (!ready || !shell.Visible || shell.Page != 4) return;
            renderer.Pass(matrix, shell.Layout.Viewport.Offset(shell.X, shell.Y), () => cards.Draw(shell));
        }
        internal void Suspend(bool focusLost = false)
        { CompleteLeave(false); input.Release(false); pins.Suspend(focusLost); cards.Suspend(); if (wasActive) workspace.Suspend(); wasActive = false;
            if (focusLost) previousLeft = true; }
        internal void FailClosed() { Suspend(); ready = false; renderer.Dispose(); }
    }
}
