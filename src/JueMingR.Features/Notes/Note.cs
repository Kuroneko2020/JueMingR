using System;
using System.Collections.Generic;

namespace JueMingR.Features.Notes
{
    public sealed class Note
    {
        public const int MaximumTitleElements = 80;
        public const int MaximumBodyUnits = 1048576;
        private readonly IReadOnlyList<int> titleBoundaries, bodyBoundaries;
        public Note(string id, string title, string body, bool pinned, int x, int y, int opacity, NoteReading reading = null)
        {
            Guid parsed;
            if (!Guid.TryParseExact(id, "N", out parsed) || parsed == Guid.Empty || id != parsed.ToString("N"))
                throw new ArgumentException("invalid-note-id");
            if (title == null || body == null || body.Length > MaximumBodyUnits || title.Length > MaximumBodyUnits ||
                title.IndexOfAny(new[] { '\r', '\n' }) >= 0 ||
                !TextElements.IsValid(body) || opacity < 0 || opacity > 100 || x < 0 || y < 0 || x > 100000 || y > 100000)
                throw new ArgumentException("invalid-note-fields-or-size");
            titleBoundaries = TextElements.Index(title); bodyBoundaries = TextElements.Index(body);
            if (titleBoundaries.Count - 1 > MaximumTitleElements) throw new ArgumentException("invalid-note-title-size");
            TitleBoundaries = titleBoundaries; BodyBoundaries = bodyBoundaries;
            Id = id; Title = title; Body = body; Pinned = pinned; X = x; Y = y; Opacity = opacity;
            Reading = reading ?? NoteReading.Default; PinIdentity = pinned ? new object() : null;
            EmptyBody = String.IsNullOrWhiteSpace(body);
        }
        private Note(Note source, string title, IReadOnlyList<int> titleIndex, string body, IReadOnlyList<int> bodyIndex, bool pinned, int x, int y, int opacity,
            NoteReading reading = null, bool newPin = false)
        {
            if (x < 0 || y < 0 || x > 100000 || y > 100000 || opacity < 0 || opacity > 100) throw new ArgumentException("invalid-note-position");
            Id = source.Id; Title = title; Body = body; titleBoundaries = titleIndex; bodyBoundaries = bodyIndex;
            TitleBoundaries = titleIndex; BodyBoundaries = bodyIndex;
            Pinned = pinned; X = x; Y = y; Opacity = opacity;
            Reading = reading ?? source.Reading; PinIdentity = !pinned ? null : newPin ? new object() : source.PinIdentity;
            EmptyBody = ReferenceEquals(body, source.Body) ? source.EmptyBody : String.IsNullOrWhiteSpace(body);
        }
        public string Id { get; }
        public string Title { get; }
        public string Body { get; }
        public bool Pinned { get; }
        public int X { get; }
        public int Y { get; }
        public int Opacity { get; }
        public NoteReading Reading { get; }
        internal object PinIdentity { get; }
        public bool EmptyBody { get; }
        public IReadOnlyList<int> TitleBoundaries { get; }
        public IReadOnlyList<int> BodyBoundaries { get; }
        internal IReadOnlyList<int> Index(bool title) { return title ? titleBoundaries : bodyBoundaries; }
        public static Note Create() { return new Note(Guid.NewGuid().ToString("N"), "新笔记", "", false, 0, 0, 0); }
        public Note WithText(bool title, string value)
        {
            if (title) { value = value.Trim(); if (value.Length == 0) value = "新笔记"; }
            var validated = new Note(Id, title ? value : Title, title ? Body : value, Pinned, X, Y, Opacity, Reading);
            return new Note(this, validated.Title, validated.TitleBoundaries, validated.Body, validated.BodyBoundaries, Pinned, X, Y, Opacity);
        }
        // Existing immutable text has already passed validation. Position/background
        // actions must not rescan megabytes of content on the game thread.
        public Note Pin(int x, int y) { return Pinned ? this : new Note(this, Title, titleBoundaries, Body, bodyBoundaries, true, x, y, 0, NoteReading.Default, true); }
        public Note Unpin() { return new Note(this, Title, titleBoundaries, Body, bodyBoundaries, false, X, Y, Opacity, NoteReading.Default); }
        public Note WithReading(NoteReading value)
        { if (value == null) throw new ArgumentNullException(nameof(value)); return new Note(this, Title, titleBoundaries, Body, bodyBoundaries, Pinned, X, Y, Opacity, value); }
        public Note WithPosition(int x, int y) { return new Note(this, Title, titleBoundaries, Body, bodyBoundaries, Pinned, x, y, Opacity); }
        public Note WithOpacity(int value) { return new Note(this, Title, titleBoundaries, Body, bodyBoundaries, Pinned, X, Y, Math.Max(0, Math.Min(100, value))); }
        internal Note WithEditor(NoteEditor editor)
        {
            string value = editor.Text; IReadOnlyList<int> index = TextElements.Compact(editor.RawBoundaries, value.Length);
            if (editor.IsTitle)
            {
                value = value.Trim(); if (value.Length == 0) value = "新笔记";
                if (!ReferenceEquals(value, editor.Text)) index = TextElements.Index(value);
            }
            return new Note(this, editor.IsTitle ? value : Title, editor.IsTitle ? index : titleBoundaries,
                editor.IsTitle ? Body : value, editor.IsTitle ? bodyBoundaries : index, Pinned, X, Y, Opacity);
        }
    }
}
