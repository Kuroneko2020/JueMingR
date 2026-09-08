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
        internal F5Rect Rect, Body, Drag, Less, More, Close;
        internal NotesTextLayout Layout;
        internal object Font;
        internal float Scroll, Scale = 1.2f, LineHeight = 36, ToolbarHeight = 36, ToolWidth = 36;
        internal int Anchor = -1, Geometry;
        internal float AnchorFraction;
    }
    internal sealed class NotesPins
    {
        internal const int Width = 280, Height = 304;
        private readonly NotesWorkspace workspace;
        private readonly NotesRenderer renderer;
        private readonly Func<NotesAction, bool> request;
        private readonly Dictionary<string, NotesPin> states = new Dictionary<string, NotesPin>();
        private readonly List<NotesPin> pins = new List<NotesPin>();
        private NotesPin hover, drag, pendingDrop, armedPin;
        private string armed;
        private int armedGeometry;
        private bool previousLeft, leftTail, rightTail;
        private float grabX, grabY, dragWidth, dragHeight;
        private int layoutCursor;
        private NotesTextLayout hintLayout;
        private string hintText;
        private object hintFont;
        private float hintWidth;
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
                NoteReading reading = workspace.Feature.ReadingFor(note);
                float toolbar = renderer.ControlHeight, tool = Math.Max(36, Math.Max(renderer.ButtonWidth("×"), renderer.ButtonWidth(">")));
                float minimum = renderer.ButtonWidth("按住拖动") + 3 * tool + 20;
                // Stored preference is independent of viewport and current font.
                // Temporary projection never rewrites it, including a tiny screen.
                float w = Math.Min(width, Math.Max(minimum, reading.Width)), h = Math.Min(height, Math.Max(toolbar + 48, reading.Height));
                if (w < minimum || h < toolbar + 40) continue;
                float scale = reading.FontPercent / 100f;
                bool reflow = pin.Layout == null || !ReferenceEquals(pin.Layout.Text, note.Body) || pin.Font != renderer.FontIdentity || pin.Body.Width != w - 16 || pin.Scale != scale;
                if (reflow && pin.Layout != null && pin.Anchor < 0 && ReferenceEquals(pin.Layout.Text, note.Body))
                {
                    int line = Math.Min(pin.Layout.Lines.Count - 1, (int)(pin.Scroll / pin.LineHeight));
                    pin.Anchor = pin.Layout.Lines[line].Start; pin.AnchorFraction = pin.Scroll / pin.LineHeight - line;
                }
                pin.Note = note; pin.ToolbarHeight = toolbar; pin.ToolWidth = tool;
                if (!ReferenceEquals(drag, pin) && !ReferenceEquals(pendingDrop, pin)) SetRect(pin, Place(note.X, note.Y, w, h, width, height));
                if (reflow)
                {
                    pin.Scale = scale; pin.LineHeight = renderer.LineHeight(scale); pin.Font = renderer.FontIdentity;
                    pin.Layout = renderer.Layout(note.Body, pin.Body.Width, scale, note.BodyBoundaries); pin.Geometry++;
                }
                pins.Add(pin);
            }
            for (int i = 0; i < pins.Count; i++)
            {
                NotesPin pin = pins[(layoutCursor + i) % pins.Count]; renderer.Advance(pin.Layout, 1024);
                if (pin.Anchor >= 0 && pin.Layout.CanLocate(pin.Anchor))
                { pin.Scroll = (pin.Layout.LineOf(pin.Anchor) + pin.AnchorFraction) * pin.LineHeight; pin.Anchor = -1; }
                pin.Scroll = ClampScroll(pin, pin.Scroll);
            }
            if (pins.Count != 0) layoutCursor = (layoutCursor + 8) % pins.Count;
            foreach (string id in new List<string>(states.Keys)) if (!retained.Contains(id)) states.Remove(id);
            if (hover != null && !pins.Contains(hover)) hover = null;
        }
        internal void Pointer(float x, float y, bool left, bool right, int wheel, bool active, bool focused, bool windowOwns,
            float width, float height, bool shift = false, bool control = false)
        {
            ConsumeLeft = leftTail; ConsumeRight = rightTail; ConsumeWheel = false; OwnsPointer = false;
            bool pressed = left && !previousLeft, released = !left && previousLeft;
            if (drag != null && (!active || !focused || windowOwns || width != dragWidth || height != dragHeight)) EndDrag();
            hover = null;
            if (active && focused && !windowOwns)
            {
                // One topmost actual visible rectangle owns the complete gesture.
                // Both modifiers, limit hits and drag-wheel still consume it.
                for (int i = pins.Count - 1; i >= 0; i--) if (pins[i].Rect.Contains(x, y)) { hover = pins[i]; break; }
                OwnsPointer = drag != null || hover != null;
                if (OwnsPointer)
                {
                    if (left) leftTail = true; if (right) rightTail = true; ConsumeWheel = true;
                    if (wheel != 0 && hover != null && drag == null)
                    {
                        if (shift != control) workspace.Feature.AdjustReading(hover.Note.Id, shift, Math.Sign(wheel) * Math.Max(1, Math.Abs(wheel / 120)));
                        else if (!shift) { hover.Anchor = -1; hover.Scroll = ClampScroll(hover, hover.Scroll - wheel / 120f * hover.LineHeight * 3); }
                    }
                    if (pressed && hover != null)
                    {
                        armed = Tool(hover, x, y); armedPin = hover; armedGeometry = hover.Geometry;
                        if (armed == "drag" && !workspace.Feature.Busy && workspace.Editor == null)
                        { drag = hover; grabX = x - drag.Rect.X; grabY = y - drag.Rect.Y; dragWidth = width; dragHeight = height; }
                    }
                    if (drag != null && left) SetRect(drag, Place((int)(x - grabX), (int)(y - grabY), drag.Rect.Width, drag.Rect.Height, width, height));
                    if (released && drag == null && hover != null && ReferenceEquals(armedPin, hover) && armedGeometry == hover.Geometry && armed == Tool(hover, x, y) && !workspace.Feature.Busy)
                    {
                        if (armed == "less") request(new NotesAction(NotesActionKind.Opacity, hover.Note.Id, x: hover.Note.Opacity - 5));
                        else if (armed == "more") request(new NotesAction(NotesActionKind.Opacity, hover.Note.Id, x: hover.Note.Opacity + 5));
                        else if (armed == "close") request(new NotesAction(NotesActionKind.Unpin, hover.Note.Id));
                    }
                }
            }
            if (released) EndDrag();
            ConsumeLeft = leftTail; ConsumeRight = rightTail;
            if (focused && !left) { leftTail = false; armed = null; armedPin = null; }
            if (focused && !right) rightTail = false;
            previousLeft = left;
            UpdateHint(x, y);
        }
        private void UpdateHint(float x, float y)
        {
            if (hover == null || renderer == null) return;
            string tool = Tool(hover, x, y);
            string text = tool == "drag" ? "按住左键拖动位置" : tool == "less" ? "背景更透明（每次 5%）" :
                tool == "more" ? "背景更不透明（每次 5%）" : tool == "close" ? "取消悬挂，保留正文" :
                "滚轮阅读；Shift 调区域，Ctrl 调字号。两键同时按下不调整。";
            float width = hover.Body.Width - 8;
            if (hintText == text && hintWidth == width && hintFont == renderer.FontIdentity) return;
            hintText = text; hintWidth = width; hintFont = renderer.FontIdentity;
            hintLayout = renderer.Layout(text, width, 0.6f);
        }
        internal void Suspend() { EndDrag(); OwnsPointer = false; hover = null; armed = null; armedPin = null; }
        private void EndDrag()
        {
            if (drag == null) return;
            NotesPin ended = drag; drag = null; pendingDrop = ended;
            if (!request(new NotesAction(NotesActionKind.Position, ended.Note.Id, x: (int)ended.Rect.X, y: (int)ended.Rect.Y)))
            {
                pendingDrop = null;
                SetRect(ended, Place(ended.Note.X, ended.Note.Y, ended.Rect.Width, ended.Rect.Height, dragWidth, dragHeight));
            }
        }
        internal void Draw()
        {
            foreach (NotesPin pin in pins)
            {
                if (pin.Note.Opacity != 0) renderer.Panel(pin.Rect, pin.Note.Opacity / 100f);
                renderer.TextView(pin.Layout, pin.Body, pin.Scroll, pin.Scale, pin.LineHeight, Color.White);
                if (ReferenceEquals(pin, hover) || ReferenceEquals(pin, drag))
                {
                    // The fixed control font shares glyph-derived geometry; body
                    // zoom never shrinks it and no hidden outside strip owns input.
                    renderer.Button(pin.Drag, "按住拖动", true);
                    renderer.Button(pin.Less, "<", true); renderer.Button(pin.More, ">", true); renderer.Button(pin.Close, "×", true);
                    if (hintLayout != null && drag == null)
                    {
                        float line = renderer.LineHeight(0.6f), height = Math.Min(pin.Body.Height, hintLayout.Lines.Count * line + 8);
                        var hint = new F5Rect(pin.Body.X, pin.Body.Bottom - height, pin.Body.Width, height);
                        renderer.Panel(hint); renderer.TextView(hintLayout, new F5Rect(hint.X + 4, hint.Y + 4, hint.Width - 8, hint.Height - 8), 0, 0.6f, line, Color.LightGray);
                    }
                }
                if (workspace.Error != null || workspace.Feature.NeedsRecovery || workspace.Feature.ReadingError != null)
                    renderer.Button(new F5Rect(pin.Rect.X, pin.Rect.Bottom - renderer.ControlHeight, pin.Rect.Width, renderer.ControlHeight), "未保存；F5 查看原因", false);
            }
        }
        private static float ClampScroll(NotesPin pin, float value)
        { return Math.Max(0, Math.Min(Math.Max(0, (pin.Layout.Complete ? pin.Layout.Lines.Count : pin.Note.Body.Length) * pin.LineHeight - pin.Body.Height), value)); }
        private static F5Rect Place(int x, int y, float w, float h, float width, float height)
        { return new F5Rect(Math.Max(0, Math.Min(Math.Max(0, width - w), x)), Math.Max(0, Math.Min(Math.Max(0, height - h), y)), w, h); }
        internal static void SetRect(NotesPin pin, F5Rect rect)
        {
            if (!pin.Rect.Equals(rect)) pin.Geometry++;
            pin.Rect = rect; float x = rect.X + 4, y = rect.Y + 4, h = pin.ToolbarHeight, tool = pin.ToolWidth;
            pin.Drag = new F5Rect(x, y, rect.Width - 3 * tool - 20, h);
            pin.Less = new F5Rect(pin.Drag.Right + 4, y, tool, h);
            pin.More = new F5Rect(pin.Less.Right + 4, y, tool, h);
            pin.Close = new F5Rect(pin.More.Right + 4, y, tool, h);
            pin.Body = new F5Rect(rect.X + 8, y + h + 8, rect.Width - 16, Math.Max(1, rect.Height - h - 20));
        }
        private static string Tool(NotesPin pin, float x, float y)
        { return pin.Drag.Contains(x, y) ? "drag" : pin.Less.Contains(x, y) ? "less" : pin.More.Contains(x, y) ? "more" : pin.Close.Contains(x, y) ? "close" : null; }
    }
}
