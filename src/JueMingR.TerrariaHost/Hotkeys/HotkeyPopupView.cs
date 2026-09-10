using JueMingR.Platform.Hotkeys;

namespace JueMingR.TerrariaHost.Hotkeys
{
    internal enum HotkeyPopupCommand { None = -1, Record, Clear, Close, Help }
    internal enum HotkeyTextRole { Normal, Muted, Success, Warning, Error }

    // A read-only projection, not an effective table. Chords are immutable and
    // candidate is transient; only the persistence owner can activate a binding.
    internal sealed class HotkeyPopupView
    {
        internal readonly string Title;
        internal readonly HotkeyChord Effective, Candidate;
        internal readonly HotkeyModifiers Modifiers;
        internal readonly HotkeyFeedback Feedback;
        internal readonly bool Editable, Capturing, Known;
        internal HotkeyPopupView(string title, HotkeyChord effective, HotkeyChord candidate, HotkeyModifiers modifiers,
            HotkeyFeedback feedback, bool editable, bool capturing, bool known)
        { Title = title; Effective = effective; Candidate = candidate; Modifiers = modifiers; Feedback = feedback; Editable = editable; Capturing = capturing; Known = known; }
        internal bool Same(HotkeyPopupView other)
        { return other != null && Title == other.Title && Equals(Effective, other.Effective) && Equals(Candidate, other.Candidate) && Modifiers == other.Modifiers &&
            ReferenceEquals(Feedback, other.Feedback) && Editable == other.Editable && Capturing == other.Capturing && Known == other.Known; }
    }
}
