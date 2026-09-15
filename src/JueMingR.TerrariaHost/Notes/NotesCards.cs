using System;
using System.Collections.Generic;
using JueMingR.Features.Notes;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.Notes
{
    internal sealed class NotesCard
    {
        internal Note Note;
        internal F5Rect Rect, Title, Body, Pin, Delete;
        internal NotesTextLayout TitleLayout, BodyLayout;
        internal float TitleScroll, BodyScroll;
        internal object Font;
        internal float Width;
        internal NoteEditor Editor;
        internal long EditRevision;
        internal bool PendingCaret;
        internal NotesControl PinControl, DeleteControl;
    }
    internal sealed class NotesControl
    {
        internal string Key, Text;
        internal F5Rect Rect;
        internal bool Enabled;
        internal Color? Color;
    }
    internal sealed class NotesCards
    {
        private readonly NotesWorkspace workspace;
        private readonly NotesInput input;
        private readonly NotesRenderer renderer;
        private readonly Dictionary<string, NotesCard> states = new Dictionary<string, NotesCard>();
        private readonly List<NotesCard> cards = new List<NotesCard>();
        private readonly Action<NotesAction> request;
        private readonly List<NotesControl> controls = new List<NotesControl>();
        private readonly Dictionary<string, NotesControl> topControls = new Dictionary<string, NotesControl>();
        private NotesControl armedControl;
        private F5Rect armedRect, projection;
        private string armedText;
        private float projectedScroll;
        private int projectedLayout = -1;
        private NoteEditor actionEditor, selectionEditor;
        private NotesCard selectionCard;
        private bool actionBusy, actionDirty, actionComposition, actionReadable;
        private string actionDelete;
        private long actionRevision;
        private int actionGeneration, armedGeneration;
        private float statusY, titleLineHeight, bodyLineHeight, statusLineHeight;
        private string lastField, armed, status;
        private Color statusColor = Color.LightGray;
        private float clickX, clickY;
        private uint clickTime;
        private NoteEditor seenEditor;
        private long seenCaret = -1;
        private NotesTextLayout statusLayout;
        private object statusFont;
        private Notebook geometryBook;
        private NoteEditor geometryEditor;
        private long geometryEditRevision = -1;
        private object geometryFont;
        private float geometryWidth, geometryHeight, contentHeight;
        private bool pendingGeometry;
#if DEBUG
        // Counts actual whole-book geometry passes; CPU regression only, no runtime logging.
        internal int DebugGeometryPasses { get; private set; }
#endif
        internal NotesCards(NotesWorkspace workspace, NotesInput input, NotesRenderer renderer, Action<NotesAction> request)
        { this.workspace = workspace; this.input = input; this.renderer = renderer; this.request = request; }
        internal IReadOnlyList<NotesCard> Cards { get { return cards; } }
        internal IReadOnlyList<NotesControl> Controls { get { return controls; } }
        internal bool Selecting { get { return selectionEditor != null; } }
        internal bool ActionStateChanged { get { return !ActionsCurrent(); } }
        internal bool PendingLayout
        {
            get { foreach (NotesCard card in cards) if (card.TitleLayout != null && !card.TitleLayout.Complete || card.BodyLayout != null && !card.BodyLayout.Complete) return true; return false; }
        }
        internal NotesTextLayout EditingLayout
        {
            get
            {
                NotesCard card;
                if (workspace.Editor == null || !states.TryGetValue(workspace.EditingId, out card)) return null;
                return workspace.Editor.IsTitle ? card.TitleLayout : card.BodyLayout;
            }
        }
        internal void Prepare(F5Interaction shell, string feedback, Color? feedbackColor = null)
        {
            statusColor = feedbackColor ?? Color.LightGray;
            // Previously visible long text gets an early share before geometry-only
            // short previews. Completed offscreen previews stay cached below, so
            // they cannot consume this allowance again on every pending frame.
            foreach (NotesCard card in cards)
                if (card.BodyLayout != null && card.Rect.Bottom > shell.Scroll && card.Rect.Y < shell.Scroll + shell.Layout.Viewport.Height)
                    renderer.Advance(card.BodyLayout, 2048);
            float width = shell.Layout.Viewport.Width;
            titleLineHeight = renderer.LineHeight(0.8f); bodyLineHeight = renderer.LineHeight(0.76f); statusLineHeight = renderer.LineHeight(0.62f);
            bool statusChanged = feedback != status || statusFont != renderer.FontIdentity || geometryWidth != width;
            if (statusChanged)
            { status = feedback; statusFont = renderer.FontIdentity; statusLayout = renderer.Layout(feedback, width - 8, 0.62f); }
            bool rebuild = statusChanged || !ActionsCurrent() || pendingGeometry || geometryBook != workspace.Feature.Saved ||
                geometryEditor != workspace.Editor || geometryFont != renderer.FontIdentity || geometryWidth != width || geometryHeight != shell.Layout.Viewport.Height;
            // A text edit can update one retained card without repacking the book.
            // Only a real height change requires the masonry pass below. Caret,
            // selection, main scrolling and long-body progress never require it.
            if (!rebuild && workspace.Editor != null && geometryEditRevision != workspace.Editor.Revision)
            {
                NotesCard edited = states[workspace.EditingId];
                F5Size size = PrepareText(edited, shell.Layout.Viewport.Height);
                rebuild = size.Width != edited.Title.Height || size.Height != edited.Body.Height;
            }
            if (rebuild)
            {
#if DEBUG
            DebugGeometryPasses++;
#endif
            actionGeneration++; pendingGeometry = false;
            actionEditor = workspace.Editor; actionBusy = workspace.Feature.Busy; actionDirty = actionEditor != null && actionEditor.Dirty;
            actionComposition = input.HasComposition; actionReadable = workspace.Feature.Readable && !workspace.Feature.NeedsRecovery;
            actionDelete = workspace.DeleteConfirmation; actionRevision = workspace.Feature.Revision;
            controls.Clear(); float controlX = 0, controlY = 0;
            AddTop("add", "+", actionReadable && !actionBusy, width, ref controlX, ref controlY);
            if (actionEditor != null)
            {
                if (actionDirty || actionComposition || actionBusy) AddTop("save", actionBusy ? "保存中" : "保存", actionReadable && !actionBusy, width, ref controlX, ref controlY);
                AddTop("cancel", "取消编辑", true, width, ref controlX, ref controlY);
            }
            if (actionDelete != null) AddTop("cancel-delete", "取消删除", true, width, ref controlX, ref controlY);
            statusY = controlY + renderer.ControlHeight + 6;
            float header = statusY + Math.Min(4, statusLayout.Lines.Count) * statusLineHeight + 8;
            float minimumCard = renderer.ButtonWidth("已悬挂") + renderer.ButtonWidth("确认") + 100;
            int columns = width < Math.Max(360, minimumCard * 2 + 10) ? 1 : 2; float cardWidth = (width - (columns - 1) * 10) / columns;
            if (cardWidth < minimumCard - 60) throw new InvalidOperationException("Notes font controls do not fit the available viewport.");
            float[] bottoms = new float[columns]; for (int i = 0; i < columns; i++) bottoms[i] = header;
            cards.Clear(); var retained = new HashSet<string>();
            foreach (Note note in workspace.Feature.Saved.Notes)
            {
                retained.Add(note.Id); NotesCard card;
                if (!states.TryGetValue(note.Id, out card))
                {
                    card = new NotesCard { PinControl = new NotesControl { Key = note.Id + ":pin" },
                        DeleteControl = new NotesControl { Key = note.Id + ":delete", Color = Color.Salmon } };
                    states.Add(note.Id, card);
                }
                if (card.Font != renderer.FontIdentity || card.Width != cardWidth)
                { card.TitleLayout = card.BodyLayout = null; card.Font = renderer.FontIdentity; card.Width = cardWidth; }
                card.Note = note;
                float pinWidth = renderer.ButtonWidth("已悬挂"), deleteWidth = renderer.ButtonWidth("确认");
                float titleWidth = Math.Max(24, cardWidth - pinWidth - deleteWidth - 28);
                F5Size textSize = PrepareText(card, shell.Layout.Viewport.Height);
                float titleHeight = textSize.Width, bodyHeight = textSize.Height;
                int column = columns == 1 || bottoms[0] <= bottoms[1] ? 0 : 1;
                float x = column * (cardWidth + 10), y = bottoms[column];
                card.Rect = new F5Rect(x, y, cardWidth, titleHeight + bodyHeight + 24);
                card.Title = new F5Rect(x + 8, y + 8, titleWidth, titleHeight);
                card.Pin = new F5Rect(x + cardWidth - pinWidth - deleteWidth - 12, y + 8, pinWidth, renderer.ControlHeight);
                card.Delete = new F5Rect(x + cardWidth - deleteWidth - 8, y + 8, deleteWidth, renderer.ControlHeight);
                card.PinControl.Text = note.Pinned ? "已悬挂" : "悬挂"; card.PinControl.Rect = card.Pin;
                card.PinControl.Enabled = actionReadable && !actionBusy && !note.Pinned;
                card.DeleteControl.Text = actionDelete == note.Id ? "确认" : "删除"; card.DeleteControl.Rect = card.Delete;
                card.DeleteControl.Enabled = actionReadable && !actionBusy;
                controls.Add(card.PinControl); controls.Add(card.DeleteControl);
                card.Body = new F5Rect(x + 8, y + titleHeight + 16, cardWidth - 16, bodyHeight);
                bottoms[column] = card.Rect.Bottom + 10; cards.Add(card);
            }
            foreach (string id in new List<string>(states.Keys)) if (!retained.Contains(id)) states.Remove(id);
            contentHeight = Math.Max(bottoms[0], columns == 2 ? bottoms[1] : 0);
            geometryBook = workspace.Feature.Saved; geometryEditor = workspace.Editor; geometryFont = renderer.FontIdentity;
            geometryWidth = width; geometryHeight = shell.Layout.Viewport.Height;
            }
            // F5 may rebuild its outer layout on page reentry without changing
            // this book's local geometry. Republish the retained height then too.
            shell.Layout.SetNotesContentHeight(contentHeight); shell.ClampScroll();
            geometryEditRevision = workspace.Editor == null ? -1 : workspace.Editor.Revision;
            bool caretChanged = workspace.Editor != null && (!ReferenceEquals(seenEditor, workspace.Editor) || seenCaret != workspace.Editor.CaretRevision);
            if (caretChanged)
            {
                NotesCard card = states[workspace.EditingId]; bool title = workspace.Editor.IsTitle;
                F5Rect field = title ? card.Title : card.Body;
                if (field.Y < shell.Scroll) shell.ScrollTo(field.Y);
                else if (field.Bottom > shell.Scroll + shell.Layout.Viewport.Height)
                    shell.ScrollTo(field.Height > shell.Layout.Viewport.Height ? field.Y : field.Bottom - shell.Layout.Viewport.Height);
            }
            foreach (NotesCard card in cards)
            {
                bool visible = card.Rect.Bottom > shell.Scroll && card.Rect.Y < shell.Scroll + shell.Layout.Viewport.Height;
                bool editing = workspace.Editor != null && workspace.EditingId == card.Note.Id;
                string body = editing && !workspace.Editor.IsTitle ? workspace.Editor.Text : card.Note.Body;
                if (visible || editing)
                {
                    int changedStart = editing && ReferenceEquals(card.Editor, workspace.Editor) && workspace.Editor.Revision == card.EditRevision + 1 ? workspace.Editor.LastChangeStart : 0;
                    if (card.BodyLayout == null || !ReferenceEquals(card.BodyLayout.Text, body))
                        card.BodyLayout = renderer.Layout(body, card.Body.Width, 0.76f, editing && !workspace.Editor.IsTitle ? workspace.Editor.Boundaries : card.Note.BodyBoundaries, card.BodyLayout, changedStart);
                    renderer.Advance(card.BodyLayout);
                    card.BodyScroll = ClampScroll(card.BodyScroll, card.BodyLayout, card.Body.Height, bodyLineHeight);
                    card.TitleScroll = ClampScroll(card.TitleScroll, card.TitleLayout, card.Title.Height, titleLineHeight);
                    card.PendingCaret |= caretChanged && editing;
                    NotesTextLayout editingLayout = editing && workspace.Editor.IsTitle ? card.TitleLayout : card.BodyLayout;
                    if (card.PendingCaret && editing && editingLayout.CanLocate(workspace.Editor.Caret))
                    {
                        if (workspace.Editor.IsTitle) card.TitleScroll = CaretScroll(card.TitleScroll, card.Title.Height, card.TitleLayout, workspace.Editor.Caret, titleLineHeight);
                        else card.BodyScroll = CaretScroll(card.BodyScroll, card.Body.Height, card.BodyLayout, workspace.Editor.Caret, bodyLineHeight);
                        card.PendingCaret = false;
                    }
                }
                else if (body.Length >= 256) card.BodyLayout = null;
                card.Editor = editing ? workspace.Editor : null; card.EditRevision = editing ? workspace.Editor.Revision : -1;
            }
            seenEditor = workspace.Editor; seenCaret = seenEditor == null ? -1 : seenEditor.CaretRevision;
            ObserveProjection(shell);
        }
        private F5Size PrepareText(NotesCard card, float viewportHeight)
        {
            Note note = card.Note;
            bool editing = workspace.EditingId == note.Id && workspace.Editor != null;
            string title = editing && workspace.Editor.IsTitle ? workspace.Editor.Text : note.Title;
            int changedStart = editing && ReferenceEquals(card.Editor, workspace.Editor) && workspace.Editor.Revision == card.EditRevision + 1 ? workspace.Editor.LastChangeStart : 0;
            float titleWidth = Math.Max(24, card.Width - renderer.ButtonWidth("已悬挂") - renderer.ButtonWidth("确认") - 28);
            if (card.TitleLayout == null || !ReferenceEquals(card.TitleLayout.Text, title))
                card.TitleLayout = renderer.Layout(title, titleWidth, 0.8f, editing && workspace.Editor.IsTitle ? workspace.Editor.Boundaries : note.TitleBoundaries, card.TitleLayout, changedStart);
            renderer.Advance(card.TitleLayout, 1024);
            float titleRows = editing && workspace.Editor.IsTitle ? 4 : 2;
            float titleHeight = Math.Max(renderer.ControlHeight, Math.Min(titleRows * titleLineHeight, card.TitleLayout.Lines.Count * titleLineHeight));
            string body = editing && !workspace.Editor.IsTitle ? workspace.Editor.Text : note.Body;
            float maximum = Math.Max(48, Math.Min(240, viewportHeight / 2 - titleHeight - 24)), bodyHeight = maximum;
            // Only short previews affect masonry height. A pending title/short body
            // must still repack when its budgeted line table grows on a later frame.
            pendingGeometry |= !card.TitleLayout.Complete;
            if (body.Length < 256)
            {
                if (card.BodyLayout == null || !ReferenceEquals(card.BodyLayout.Text, body))
                    card.BodyLayout = renderer.Layout(body, card.Width - 16, 0.76f, editing && !workspace.Editor.IsTitle ? workspace.Editor.Boundaries : note.BodyBoundaries);
                renderer.Advance(card.BodyLayout, 1024); pendingGeometry |= !card.BodyLayout.Complete;
                bodyHeight = Math.Max(48, Math.Min(maximum, card.BodyLayout.Lines.Count * bodyLineHeight));
            }
            return new F5Size(titleHeight, bodyHeight);
        }
        private void ObserveProjection(F5Interaction shell)
        {
            F5Rect next = shell.Layout.Viewport.Offset(shell.X, shell.Y);
            if (!next.Equals(projection) || projectedScroll != shell.Scroll || projectedLayout != shell.Layout.Generation)
            {
                // Retained controls are mutable. A press belongs to its original
                // screen projection, even if the window later returns there.
                armed = lastField = null; armedControl = null;
                projection = next; projectedScroll = shell.Scroll; projectedLayout = shell.Layout.Generation;
            }
        }
        internal bool Wheel(F5Interaction shell, float x, float y, int wheel)
        {
            if (wheel == 0 || !shell.Visible || shell.Page != 4) return false;
            F5Rect view = shell.Layout.Viewport.Offset(shell.X, shell.Y); if (!view.Contains(x, y)) return false;
            x -= view.X; y += shell.Scroll - view.Y;
            foreach (NotesCard card in cards) if (card.Body.Contains(x, y) && card.BodyLayout != null)
            {
                float next = ClampScroll(card.BodyScroll - wheel / 3f, card.BodyLayout, card.Body.Height, bodyLineHeight);
                bool changed = next != card.BodyScroll; card.BodyScroll = next; card.PendingCaret = false; return changed || !card.BodyLayout.Complete;
            }
            return false;
        }
        private bool ActionsCurrent()
        {
            return ReferenceEquals(actionEditor, workspace.Editor) && actionBusy == workspace.Feature.Busy &&
                actionDirty == (workspace.Editor != null && workspace.Editor.Dirty) && actionComposition == input.HasComposition &&
                actionReadable == (workspace.Feature.Readable && !workspace.Feature.NeedsRecovery) &&
                actionDelete == workspace.DeleteConfirmation && actionRevision == workspace.Feature.Revision;
        }
        private void AddTop(string key, string text, bool enabled, float width, ref float x, ref float y)
        {
            float size = renderer.ButtonWidth(text);
            if (x > 0 && x + size > width) { x = 0; y += renderer.ControlHeight + 4; }
            NotesControl control;
            if (!topControls.TryGetValue(key, out control)) { control = new NotesControl { Key = key }; topControls.Add(key, control); }
            control.Text = text; control.Enabled = enabled; control.Rect = new F5Rect(x, y, size, renderer.ControlHeight); controls.Add(control);
            x += size + 6;
        }
        internal void Pointer(F5Interaction shell, bool pressed, bool released, bool left = false, bool shift = false)
        {
            ObserveProjection(shell);
            F5Rect view = shell.Layout.Viewport.Offset(shell.X, shell.Y);
            float x = shell.PointerX - view.X, y = shell.PointerY - view.Y + shell.Scroll;
            string hit = view.Contains(shell.PointerX, shell.PointerY) ? Hit(x, y) : null;
            if (selectionEditor != null)
            {
                if (!ReferenceEquals(selectionEditor, workspace.Editor) || input.HasComposition) ClearSelectionCapture();
                else
                {
                    if (left || released) DragSelection(x, y);
                    if (released || !left && !pressed) ClearSelectionCapture();
                    return; // A selection release can never activate a card or save blank space.
                }
            }
            if (pressed)
            {
                armed = hit;
                armedControl = null; armedGeneration = actionGeneration;
                if (ActionsCurrent()) foreach (NotesControl control in controls) if (control.Key == hit && control.Enabled)
                { armedControl = control; armedRect = control.Rect; armedText = control.Text; break; }
                if (hit != null && (hit.EndsWith(":title", StringComparison.Ordinal) || hit.EndsWith(":body", StringComparison.Ordinal)))
                {
                    bool title = hit.EndsWith(":title", StringComparison.Ordinal); string id = hit.Substring(0, 32); NotesCard card = states[id];
                    NotesTextLayout layout = title ? card.TitleLayout : card.BodyLayout; F5Rect rect = title ? card.Title : card.Body;
                    float scroll = title ? card.TitleScroll : card.BodyScroll;
                    float lineHeight = title ? titleLineHeight : bodyLineHeight;
                    int caret = layout == null ? -1 : layout.Hit(x - rect.X, (int)((y - rect.Y + scroll) / lineHeight));
                    if (caret < 0) { lastField = null; return; }
                    if (workspace.Editor != null && workspace.EditingId == id && workspace.Editor.IsTitle == title)
                    {
                        // Composition owns its replacement target until commit/cancel.
                        // Never move that target using an obsolete visual layout.
                        if (input.HasComposition || !ReferenceEquals(layout.Text, workspace.Editor.Text)) return;
                        workspace.Editor.MoveTo(caret, shift); selectionEditor = workspace.Editor; selectionCard = card; armed = null;
                    }
                    else if (lastField == hit && unchecked((uint)Environment.TickCount - clickTime) <= 500 && Math.Abs(x - clickX) <= 6 && Math.Abs(y - clickY) <= 6)
                    { request(new NotesAction(NotesActionKind.BeginEdit, id, title, caret)); lastField = null; return; }
                    lastField = hit; clickTime = unchecked((uint)Environment.TickCount); clickX = x; clickY = y;
                }
                else
                {
                    lastField = null;
                    if (hit == "blank" && workspace.Editor != null && !workspace.Feature.Busy) request(new NotesAction(NotesActionKind.FinishEdit));
                }
            }
            if (released)
            {
                if (armed == hit && hit != null && armedControl != null && armedGeneration == actionGeneration && ActionsCurrent())
                    foreach (NotesControl control in controls)
                        if (control.Key == hit && control.Enabled && control.Rect.Equals(armedRect) && control.Text == armedText) { Activate(hit, shell); break; }
                armed = null; armedControl = null;
            }
        }
        private void DragSelection(float x, float y)
        {
            bool title = selectionEditor.IsTitle;
            F5Rect rect = title ? selectionCard.Title : selectionCard.Body;
            NotesTextLayout layout = title ? selectionCard.TitleLayout : selectionCard.BodyLayout;
            if (layout == null || !ReferenceEquals(layout.Text, selectionEditor.Text)) return;
            const float band = 36;
            if (x < rect.X - band || x > rect.Right + band || y < rect.Y - band || y > rect.Bottom + band) return;
            float height = title ? titleLineHeight : bodyLineHeight, scroll = title ? selectionCard.TitleScroll : selectionCard.BodyScroll;
            float delta = y < rect.Y + 8 ? -Math.Min(12, (rect.Y + 8 - y) / 3) : y > rect.Bottom - 8 ? Math.Min(12, (y - rect.Bottom + 8) / 3) : 0;
            scroll = ClampScroll(scroll + delta, layout, rect.Height, height);
            int caret = layout.Hit(Math.Max(0, Math.Min(rect.Width, x - rect.X)), (int)((Math.Max(0, Math.Min(rect.Height - 1, y - rect.Y)) + scroll) / height));
            if (caret >= 0) selectionEditor.MoveTo(caret, true);
            if (title) selectionCard.TitleScroll = scroll; else selectionCard.BodyScroll = scroll;
            selectionCard.PendingCaret = false;
        }
        private void ClearSelectionCapture() { selectionEditor = null; selectionCard = null; armed = null; armedControl = null; }
        private string Hit(float x, float y)
        {
            foreach (NotesControl control in controls) if (control.Rect.Contains(x, y)) return control.Enabled ? control.Key : "disabled";
            foreach (NotesCard card in cards)
            {
                if (card.Title.Contains(x, y)) return card.Note.Id + ":title";
                if (card.Body.Contains(x, y)) return card.Note.Id + ":body";
                if (card.Rect.Contains(x, y)) return "card";
            }
            return "blank";
        }
        private void Activate(string hit, F5Interaction shell)
        {
            if (hit == "add") request(new NotesAction(NotesActionKind.Create));
            // The visible Save action returns to preview only after this draft is
            // acknowledged; FinishEdit already preserves failures and newer input.
            else if (hit == "save") request(new NotesAction(NotesActionKind.FinishEdit));
            else if (hit == "cancel") { input.Release(true); workspace.CancelEdit(); }
            else if (hit == "cancel-delete") workspace.CancelDelete();
            else if (hit.EndsWith(":pin", StringComparison.Ordinal))
            {
                // Default screen coordinates are derived by the presentation owner;
                // this page does not persist a logical F5 coordinate as pixels.
                request(new NotesAction(NotesActionKind.Pin, hit.Substring(0, 32)));
            }
            else if (hit.EndsWith(":delete", StringComparison.Ordinal))
            { string id = hit.Substring(0, 32); request(new NotesAction(workspace.DeleteConfirmation == id ? NotesActionKind.Delete : NotesActionKind.ConfirmDelete, id)); }
        }
        internal void Draw(F5Interaction shell)
        {
            F5Rect view = shell.Layout.Viewport.Offset(shell.X, shell.Y); float ox = view.X, oy = view.Y - shell.Scroll;
            renderer.TextView(statusLayout, new F5Rect(ox, oy + statusY, view.Width - 8, 4 * statusLineHeight), 0, 0.62f, statusLineHeight, statusColor);
            foreach (NotesCard card in cards)
            {
                if (card.Rect.Bottom <= shell.Scroll || card.Rect.Y >= shell.Scroll + view.Height) continue;
                renderer.Panel(card.Rect.Offset(ox, oy));
                bool editing = workspace.Editor != null && workspace.EditingId == card.Note.Id;
                NoteEditor title = editing && workspace.Editor.IsTitle ? workspace.Editor : null;
                NoteEditor body = editing && !workspace.Editor.IsTitle ? workspace.Editor : null;
                renderer.TextView(card.TitleLayout, card.Title.Offset(ox, oy), card.TitleScroll, 0.8f, titleLineHeight, Color.Wheat, title, title == null ? "" : input.Composition);
                if (body == null && card.Note.EmptyBody) renderer.Label("双击进入编辑", ox + card.Body.X, oy + card.Body.Y, 0.7f, Color.Gray);
                renderer.TextView(card.BodyLayout, card.Body.Offset(ox, oy), card.BodyScroll, 0.76f, bodyLineHeight, body == null ? Color.LightBlue : Color.LightYellow, body, body == null ? "" : input.Composition);
            }
            foreach (NotesControl control in controls)
            {
                F5Rect rect = control.Rect.Offset(ox, oy);
                if (rect.Bottom <= view.Y || rect.Y >= view.Bottom) continue;
                renderer.Button(rect, control.Text, view.Contains(shell.PointerX, shell.PointerY) && rect.Contains(shell.PointerX, shell.PointerY), control.Color, control.Enabled);
            }
        }
        internal void Suspend() { lastField = armed = null; ClearSelectionCapture(); }
        private static float ClampScroll(float value, NotesTextLayout layout, float height, float lineHeight)
        { return Math.Max(0, layout.Complete ? Math.Min(Math.Max(0, layout.Lines.Count * lineHeight - height), value) : Math.Min(layout.Text.Length * lineHeight, value)); }
        private static float CaretScroll(float scroll, float height, NotesTextLayout layout, int caret, float lineHeight)
        {
            float y = layout.LineOf(caret) * lineHeight;
            if (y < scroll) return y;
            return y + lineHeight > scroll + height ? y + lineHeight - height : scroll;
        }
    }
}
