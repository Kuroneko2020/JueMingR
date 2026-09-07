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
    }
    internal sealed class NotesCards
    {
        private readonly NotesWorkspace workspace;
        private readonly NotesInput input;
        private readonly NotesRenderer renderer;
        private readonly Dictionary<string, NotesCard> states = new Dictionary<string, NotesCard>();
        private readonly List<NotesCard> cards = new List<NotesCard>();
        private readonly Action<NotesAction> request;
        private F5Rect add, save, cancel, cancelDelete;
        private string lastField, armed, status;
        private float clickX, clickY;
        private uint clickTime;
        private NoteEditor seenEditor;
        private long seenCaret = -1;
        private NotesTextLayout statusLayout;
        private object statusFont;
        internal NotesCards(NotesWorkspace workspace, NotesInput input, NotesRenderer renderer, Action<NotesAction> request)
        { this.workspace = workspace; this.input = input; this.renderer = renderer; this.request = request; }
        internal IReadOnlyList<NotesCard> Cards { get { return cards; } }
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
        internal void Prepare(F5Interaction shell, string feedback)
        {
            // Previously visible long text gets an early share before geometry-only
            // short previews. Completed offscreen previews stay cached below, so
            // they cannot consume this allowance again on every pending frame.
            foreach (NotesCard card in cards)
                if (card.BodyLayout != null && card.Rect.Bottom > shell.Scroll && card.Rect.Y < shell.Scroll + shell.Layout.Viewport.Height)
                    renderer.Advance(card.BodyLayout, 2048);
            float width = shell.Layout.Viewport.Width;
            if (feedback != status || statusFont != renderer.FontIdentity)
            { status = feedback; statusFont = renderer.FontIdentity; statusLayout = renderer.Layout(feedback, width - 8, 0.62f); }
            float header = 40 + Math.Min(4, statusLayout.Lines.Count) * 20;
            add = new F5Rect(0, 0, 38, 30); save = new F5Rect(46, 0, 56, 30);
            cancel = new F5Rect(110, 0, 56, 30); cancelDelete = new F5Rect(width - 94, 0, 94, 30);
            int columns = width < 360 ? 1 : 2; float cardWidth = (width - (columns - 1) * 10) / columns;
            float[] bottoms = new float[columns]; for (int i = 0; i < columns; i++) bottoms[i] = header;
            cards.Clear(); var retained = new HashSet<string>();
            foreach (Note note in workspace.Feature.Saved.Notes)
            {
                retained.Add(note.Id); NotesCard card;
                if (!states.TryGetValue(note.Id, out card)) { card = new NotesCard(); states.Add(note.Id, card); }
                if (card.Font != renderer.FontIdentity || card.Width != cardWidth)
                { card.TitleLayout = card.BodyLayout = null; card.Font = renderer.FontIdentity; card.Width = cardWidth; }
                card.Note = note;
                bool editing = workspace.EditingId == note.Id && workspace.Editor != null;
                string title = editing && workspace.Editor.IsTitle ? workspace.Editor.Text : note.Title;
                int changedStart = editing && ReferenceEquals(card.Editor, workspace.Editor) && workspace.Editor.Revision == card.EditRevision + 1 ? workspace.Editor.LastChangeStart : 0;
                if (card.TitleLayout == null || !ReferenceEquals(card.TitleLayout.Text, title))
                    card.TitleLayout = renderer.Layout(title, cardWidth - 132, 0.8f, editing && workspace.Editor.IsTitle ? workspace.Editor.Boundaries : note.TitleBoundaries, card.TitleLayout, changedStart);
                renderer.Advance(card.TitleLayout, 1024);
                float titleHeight = editing && workspace.Editor.IsTitle ? Math.Min(96, Math.Max(48, card.TitleLayout.Lines.Count * 24)) : 48;
                string body = editing && !workspace.Editor.IsTitle ? workspace.Editor.Text : note.Body;
                float maximum = Math.Max(48, Math.Min(240, shell.Layout.Viewport.Height / 2 - titleHeight - 24));
                float bodyHeight = maximum;
                // Only short previews influence masonry height; long bodies already
                // reach the viewport cap. Full text layout is limited to visible cards.
                if (body.Length < 256)
                {
                    if (card.BodyLayout == null || !ReferenceEquals(card.BodyLayout.Text, body))
                        card.BodyLayout = renderer.Layout(body, cardWidth - 16, 0.76f, editing && !workspace.Editor.IsTitle ? workspace.Editor.Boundaries : note.BodyBoundaries);
                    renderer.Advance(card.BodyLayout, 1024);
                    bodyHeight = Math.Max(48, Math.Min(maximum, card.BodyLayout.Lines.Count * 24));
                }
                int column = columns == 1 || bottoms[0] <= bottoms[1] ? 0 : 1;
                float x = column * (cardWidth + 10), y = bottoms[column];
                card.Rect = new F5Rect(x, y, cardWidth, titleHeight + bodyHeight + 24);
                card.Title = new F5Rect(x + 8, y + 7, cardWidth - 132, titleHeight);
                card.Pin = new F5Rect(x + cardWidth - 118, y + 8, 60, 28);
                card.Delete = new F5Rect(x + cardWidth - 54, y + 8, 48, 28);
                card.Body = new F5Rect(x + 8, y + titleHeight + 16, cardWidth - 16, bodyHeight);
                bottoms[column] = card.Rect.Bottom + 10; cards.Add(card);
            }
            foreach (string id in new List<string>(states.Keys)) if (!retained.Contains(id)) states.Remove(id);
            shell.Layout.SetNotesContentHeight(Math.Max(bottoms[0], columns == 2 ? bottoms[1] : 0)); shell.ClampScroll();
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
                    card.BodyScroll = ClampScroll(card.BodyScroll, card.BodyLayout, card.Body.Height, 24);
                    card.TitleScroll = ClampScroll(card.TitleScroll, card.TitleLayout, card.Title.Height, 24);
                    card.PendingCaret |= caretChanged && editing;
                    NotesTextLayout editingLayout = editing && workspace.Editor.IsTitle ? card.TitleLayout : card.BodyLayout;
                    if (card.PendingCaret && editing && editingLayout.CanLocate(workspace.Editor.Caret))
                    {
                        if (workspace.Editor.IsTitle) card.TitleScroll = CaretScroll(card.TitleScroll, card.Title.Height, card.TitleLayout, workspace.Editor.Caret);
                        else card.BodyScroll = CaretScroll(card.BodyScroll, card.Body.Height, card.BodyLayout, workspace.Editor.Caret);
                        card.PendingCaret = false;
                    }
                }
                else if (body.Length >= 256) card.BodyLayout = null;
                card.Editor = editing ? workspace.Editor : null; card.EditRevision = editing ? workspace.Editor.Revision : -1;
            }
            seenEditor = workspace.Editor; seenCaret = seenEditor == null ? -1 : seenEditor.CaretRevision;
        }
        internal bool Wheel(F5Interaction shell, float x, float y, int wheel)
        {
            if (wheel == 0 || !shell.Visible || shell.Page != 4) return false;
            F5Rect view = shell.Layout.Viewport.Offset(shell.X, shell.Y); if (!view.Contains(x, y)) return false;
            x -= view.X; y += shell.Scroll - view.Y;
            foreach (NotesCard card in cards) if (card.Body.Contains(x, y) && card.BodyLayout != null)
            {
                float next = ClampScroll(card.BodyScroll - wheel / 3f, card.BodyLayout, card.Body.Height, 24);
                bool changed = next != card.BodyScroll; card.BodyScroll = next; card.PendingCaret = false; return changed || !card.BodyLayout.Complete;
            }
            return false;
        }
        internal void Pointer(F5Interaction shell, bool pressed, bool released)
        {
            F5Rect view = shell.Layout.Viewport.Offset(shell.X, shell.Y);
            float x = shell.PointerX - view.X, y = shell.PointerY - view.Y + shell.Scroll;
            string hit = view.Contains(shell.PointerX, shell.PointerY) ? Hit(x, y) : null;
            if (pressed)
            {
                armed = hit;
                if (hit != null && (hit.EndsWith(":title", StringComparison.Ordinal) || hit.EndsWith(":body", StringComparison.Ordinal)))
                {
                    bool title = hit.EndsWith(":title", StringComparison.Ordinal); string id = hit.Substring(0, 32); NotesCard card = states[id];
                    NotesTextLayout layout = title ? card.TitleLayout : card.BodyLayout; F5Rect rect = title ? card.Title : card.Body;
                    float scroll = title ? card.TitleScroll : card.BodyScroll;
                    int caret = layout == null ? -1 : layout.Hit(x - rect.X, (int)((y - rect.Y + scroll) / 24));
                    if (caret < 0) { lastField = null; return; }
                    if (workspace.Editor != null && workspace.EditingId == id && workspace.Editor.IsTitle == title) workspace.Editor.MoveTo(caret);
                    else if (lastField == hit && unchecked((uint)Environment.TickCount - clickTime) <= 500 && Math.Abs(x - clickX) <= 6 && Math.Abs(y - clickY) <= 6)
                    { request(new NotesAction(NotesActionKind.BeginEdit, id, title, caret)); lastField = null; return; }
                    lastField = hit; clickTime = unchecked((uint)Environment.TickCount); clickX = x; clickY = y;
                }
                else
                {
                    lastField = null;
                    if (hit == "blank") request(new NotesAction(NotesActionKind.FinishEdit));
                }
            }
            if (released)
            {
                if (armed == hit && hit != null) Activate(hit, shell);
                armed = null;
            }
        }
        private string Hit(float x, float y)
        {
            if (add.Contains(x, y)) return "add";
            if (save.Contains(x, y)) return "save";
            if (cancel.Contains(x, y)) return "cancel";
            if (workspace.DeleteConfirmation != null && cancelDelete.Contains(x, y)) return "cancel-delete";
            foreach (NotesCard card in cards)
            {
                if (card.Pin.Contains(x, y)) return card.Note.Id + ":pin";
                if (card.Delete.Contains(x, y)) return card.Note.Id + ":delete";
                if (card.Title.Contains(x, y)) return card.Note.Id + ":title";
                if (card.Body.Contains(x, y)) return card.Note.Id + ":body";
                if (card.Rect.Contains(x, y)) return "card";
            }
            return "blank";
        }
        private void Activate(string hit, F5Interaction shell)
        {
            if (hit == "add") request(new NotesAction(NotesActionKind.Create));
            else if (hit == "save") request(new NotesAction(NotesActionKind.Save));
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
            renderer.Button(add.Offset(ox, oy), "+", add.Offset(ox, oy).Contains(shell.PointerX, shell.PointerY));
            renderer.Button(save.Offset(ox, oy), "保存", false); renderer.Button(cancel.Offset(ox, oy), "取消", false);
            if (workspace.Editor != null && workspace.Editor.Dirty) renderer.Label("未保存", ox + 176, oy + 5, 0.65f, Color.Gold);
            if (workspace.DeleteConfirmation != null) renderer.Button(cancelDelete.Offset(ox, oy), "取消删除", false);
            renderer.TextView(statusLayout, new F5Rect(ox, oy + 36, view.Width - 8, 80), 0, 0.62f, 20, Color.Orange);
            foreach (NotesCard card in cards)
            {
                if (card.Rect.Bottom <= shell.Scroll || card.Rect.Y >= shell.Scroll + view.Height) continue;
                renderer.Fill(card.Rect.Offset(ox, oy), new Color(28, 39, 59, 230));
                bool editing = workspace.Editor != null && workspace.EditingId == card.Note.Id;
                NoteEditor title = editing && workspace.Editor.IsTitle ? workspace.Editor : null;
                NoteEditor body = editing && !workspace.Editor.IsTitle ? workspace.Editor : null;
                renderer.TextView(card.TitleLayout, card.Title.Offset(ox, oy), card.TitleScroll, 0.8f, 24, Color.Wheat, title, title == null ? "" : input.Composition);
                renderer.Button(card.Pin.Offset(ox, oy), card.Note.Pinned ? "已悬挂" : "悬挂", false);
                renderer.Button(card.Delete.Offset(ox, oy), workspace.DeleteConfirmation == card.Note.Id ? "确认" : "删除", false, Color.Salmon);
                if (body == null && card.Note.EmptyBody) renderer.Label("双击进入编辑", ox + card.Body.X, oy + card.Body.Y, 0.7f, Color.Gray);
                renderer.TextView(card.BodyLayout, card.Body.Offset(ox, oy), card.BodyScroll, 0.76f, 24, body == null ? Color.LightBlue : Color.LightYellow, body, body == null ? "" : input.Composition);
            }
        }
        internal void Suspend() { lastField = armed = null; }
        private static float ClampScroll(float value, NotesTextLayout layout, float height, float lineHeight)
        { return Math.Max(0, layout.Complete ? Math.Min(Math.Max(0, layout.Lines.Count * lineHeight - height), value) : Math.Min(layout.Text.Length * lineHeight, value)); }
        private static float CaretScroll(float scroll, float height, NotesTextLayout layout, int caret)
        {
            float y = layout.LineOf(caret) * 24;
            if (y < scroll) return y;
            return y + 24 > scroll + height ? y + 24 - height : scroll;
        }
    }
}
