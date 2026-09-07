using System;
using System.Collections.Generic;
using JueMingR.Features.Notes;
using JueMingR.TerrariaHost.F5;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.Notes
{
    internal sealed class NotesPin
    {
        internal Note Note;
        internal F5Rect Rect;
        internal NotesTextLayout Layout;
        internal object Font;
        internal float Scroll;
    }
    internal sealed class NotesPins
    {
        internal const int Width = 280, Height = 304;
        private readonly NotesWorkspace workspace;
        private readonly NotesRenderer renderer;
        private readonly Func<NotesAction, bool> request;
        private readonly Dictionary<string, NotesPin> states = new Dictionary<string, NotesPin>();
        private readonly List<NotesPin> pins = new List<NotesPin>();
        private NotesPin hover, drag, pendingDrop;
        private string armed;
        private bool previousLeft, leftTail, rightTail;
        private float grabX, grabY, dragWidth, dragHeight;
        private int layoutCursor;
        internal bool OwnsPointer { get; private set; }
        internal bool ConsumeLeft { get; private set; }
        internal bool ConsumeRight { get; private set; }
        internal bool ConsumeWheel { get; private set; }
        internal bool HasPins { get { return pins.Count != 0; } }
        internal bool PendingLayout { get { foreach (NotesPin pin in pins) if (!pin.Layout.Complete) return true; return false; } }
        internal IReadOnlyList<NotesPin> Pins { get { return pins; } }
        internal NotesPins(NotesWorkspace workspace, NotesRenderer renderer, Func<NotesAction, bool> request)
        { this.workspace = workspace; this.renderer = renderer; this.request = request; }
        internal void Prepare(float width, float height)
        {
            if (pendingDrop != null && !workspace.Feature.Busy) pendingDrop = null;
            pins.Clear(); var retained = new HashSet<string>();
            foreach (Note note in workspace.Feature.Saved.Notes)
            {
                if (!note.Pinned) continue;
                retained.Add(note.Id); NotesPin pin;
                if (!states.TryGetValue(note.Id, out pin)) { pin = new NotesPin(); states.Add(note.Id, pin); }
                pin.Note = note;
                if (!ReferenceEquals(drag, pin) && !ReferenceEquals(pendingDrop, pin))
                    pin.Rect = Place(note.X, note.Y, width, height);
                if (pin.Layout == null || !ReferenceEquals(pin.Layout.Text, note.Body) || pin.Font != renderer.FontIdentity)
                { pin.Layout = renderer.Layout(note.Body, Width - 16, 1.2f, note.BodyBoundaries); pin.Font = renderer.FontIdentity; }
                if (pin.Layout.Complete) pin.Scroll = Math.Max(0, Math.Min(pin.Scroll, Math.Max(0, pin.Layout.Lines.Count * 36 - Height + 16)));
                pins.Add(pin);
            }
            // Rotate the starting note, so a long lower pin cannot starve the
            // remaining transparent layers under the shared frame allowance.
            for (int i = 0; i < pins.Count; i++)
            {
                NotesPin pin = pins[(layoutCursor + i) % pins.Count]; renderer.Advance(pin.Layout, 1024);
                if (pin.Layout.Complete) pin.Scroll = Math.Max(0, Math.Min(pin.Scroll, Math.Max(0, pin.Layout.Lines.Count * 36 - Height + 16)));
            }
            if (pins.Count != 0) layoutCursor = (layoutCursor + 8) % pins.Count;
            foreach (string id in new List<string>(states.Keys)) if (!retained.Contains(id)) states.Remove(id);
        }
        internal void Pointer(float x, float y, bool left, bool right, int wheel, bool active, bool focused, bool windowOwns, float width, float height)
        {
            ConsumeLeft = leftTail; ConsumeRight = rightTail; ConsumeWheel = false; OwnsPointer = false;
            bool pressed = left && !previousLeft, released = !left && previousLeft;
            if (drag != null && (!active || windowOwns || width != dragWidth || height != dragHeight)) EndDrag();
            hover = null;
            if (active && !windowOwns)
            {
                // Reverse drawing order picks exactly one target, including transparent
                // backgrounds. A boundary wheel still belongs to this target, not hotbar.
                for (int i = pins.Count - 1; i >= 0; i--)
                    if (HitRect(pins[i]).Contains(x, y)) { hover = pins[i]; break; }
                OwnsPointer = drag != null || hover != null;
                if (OwnsPointer)
                {
                    if (left) leftTail = true; if (right) rightTail = true; ConsumeWheel = true;
                    if (wheel != 0 && hover != null && drag == null)
                        hover.Scroll = Math.Max(0, Math.Min(Math.Max(0, hover.Layout.Complete ? hover.Layout.Lines.Count * 36 - Height + 16 : hover.Note.Body.Length * 36), hover.Scroll - wheel / 120f * 108));
                    if (pressed && hover != null)
                    {
                        armed = Tool(hover, x, y);
                        if (armed == "drag" && !workspace.Feature.Busy)
                        {
                            if (workspace.Editor != null) request(new NotesAction(NotesActionKind.FinishEdit));
                            if (workspace.Editor == null && !workspace.Feature.Busy)
                            { drag = hover; grabX = x - drag.Rect.X; grabY = y - drag.Rect.Y; dragWidth = width; dragHeight = height; }
                        }
                    }
                    if (drag != null && left) drag.Rect = Place((int)(x - grabX), (int)(y - grabY), width, height);
                    if (released && drag == null && hover != null && armed == Tool(hover, x, y))
                    {
                        if (armed == "less") request(new NotesAction(NotesActionKind.Opacity, hover.Note.Id, x: hover.Note.Opacity - 5));
                        else if (armed == "more") request(new NotesAction(NotesActionKind.Opacity, hover.Note.Id, x: hover.Note.Opacity + 5));
                        else if (armed == "close") request(new NotesAction(NotesActionKind.Unpin, hover.Note.Id));
                    }
                }
            }
            if (released) EndDrag();
            ConsumeLeft = leftTail; ConsumeRight = rightTail;
            if (focused && !left) { leftTail = false; armed = null; }
            if (focused && !right) rightTail = false;
            previousLeft = left;
        }
        internal void Suspend()
        { EndDrag(); OwnsPointer = false; hover = null; armed = null; }
        private void EndDrag()
        {
            if (drag == null) return;
            NotesPin ended = drag; drag = null; pendingDrop = ended;
            if (!request(new NotesAction(NotesActionKind.Position, ended.Note.Id, x: (int)ended.Rect.X, y: (int)ended.Rect.Y)))
            {
                // Immediate refusal has no worker completion/revision to redraw us.
                // Restore the trusted model now, before retaining a false saved pose.
                pendingDrop = null; ended.Rect = Place(ended.Note.X, ended.Note.Y, dragWidth, dragHeight);
            }
        }
        internal void Draw()
        {
            foreach (NotesPin pin in pins)
            {
                if (pin.Note.Opacity != 0) renderer.Fill(pin.Rect, Color.Black * (pin.Note.Opacity / 100f));
                renderer.TextView(pin.Layout, new F5Rect(pin.Rect.X + 8, pin.Rect.Y + 8, Width - 16, Height - 16), pin.Scroll, 1.2f, 36, Color.White);
                if (workspace.Error != null || workspace.Feature.NeedsRecovery)
                    renderer.Button(new F5Rect(pin.Rect.X, pin.Rect.Bottom - 24, Width, 24), "未保存；F5 笔记页查看原因", false);
                if (ReferenceEquals(pin, hover) || ReferenceEquals(pin, drag))
                {
                    float x = pin.Rect.X, y = ToolbarY(pin);
                    renderer.Button(new F5Rect(x, y, Width - 84, 24), "拖动", true);
                    renderer.Button(new F5Rect(x + Width - 84, y, 28, 24), "<", true);
                    renderer.Button(new F5Rect(x + Width - 56, y, 28, 24), ">", true);
                    renderer.Button(new F5Rect(x + Width - 28, y, 28, 24), "×", true);
                }
            }
        }
        private static F5Rect Place(int x, int y, float width, float height)
        { return new F5Rect(Math.Max(0, Math.Min(Math.Max(0, width - Width), x)), Math.Max(24, Math.Min(Math.Max(24, height - Height), y)), Width, Height); }
        private static float ToolbarY(NotesPin pin) { return Math.Max(0, pin.Rect.Y - 24); }
        private static F5Rect HitRect(NotesPin pin) { return new F5Rect(pin.Rect.X, ToolbarY(pin), Width, pin.Rect.Bottom - ToolbarY(pin)); }
        private static string Tool(NotesPin pin, float x, float y)
        {
            if (y < ToolbarY(pin) || y >= ToolbarY(pin) + 24) return null;
            float local = x - pin.Rect.X;
            return local < Width - 84 ? "drag" : local < Width - 56 ? "less" : local < Width - 28 ? "more" : "close";
        }
    }
}
