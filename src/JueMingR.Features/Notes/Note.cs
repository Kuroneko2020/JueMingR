using System;

namespace JueMingR.Features.Notes
{
    public sealed class Note
    {
        public const int MaximumTitleElements = 80;
        public const int MaximumBodyUnits = 1048576;
        public Note(string id, string title, string body, bool pinned, int x, int y, int opacity)
        {
            Guid parsed;
            if (!Guid.TryParseExact(id, "N", out parsed) || parsed == Guid.Empty || id != parsed.ToString("N"))
                throw new ArgumentException("invalid-note-id");
            if (title == null || body == null || body.Length > MaximumBodyUnits || title.Length > MaximumBodyUnits ||
                title.IndexOfAny(new[] { '\r', '\n' }) >= 0 || TextElements.Boundaries(title).Length - 1 > MaximumTitleElements ||
                !TextElements.IsValid(body) || opacity < 0 || opacity > 100 || x < 0 || y < 0 || x > 100000 || y > 100000)
                throw new ArgumentException("invalid-note-fields-or-size");
            Id = id; Title = title; Body = body; Pinned = pinned; X = x; Y = y; Opacity = opacity;
        }
        public string Id { get; }
        public string Title { get; }
        public string Body { get; }
        public bool Pinned { get; }
        public int X { get; }
        public int Y { get; }
        public int Opacity { get; }
        public static Note Create() { return new Note(Guid.NewGuid().ToString("N"), "新笔记", "", false, 0, 0, 0); }
        public Note WithText(bool title, string value)
        {
            if (title) { value = value.Trim(); if (value.Length == 0) value = "新笔记"; }
            return new Note(Id, title ? value : Title, title ? Body : value, Pinned, X, Y, Opacity);
        }
        public Note Pin(int x, int y) { return new Note(Id, Title, Body, true, x, y, 0); }
        public Note Unpin() { return new Note(Id, Title, Body, false, X, Y, Opacity); }
        public Note WithPosition(int x, int y) { return new Note(Id, Title, Body, Pinned, x, y, Opacity); }
        public Note WithOpacity(int value) { return new Note(Id, Title, Body, Pinned, X, Y, Math.Max(0, Math.Min(100, value))); }
    }
}
