using System;

namespace JueMingR.Platform.Hotkeys
{
    public enum HotkeyFeedbackKind { Ready, Loading, Saving, Saved, Cleared, Rejected, Failed, Protected, Unconfirmed, Capturing, Cancelled }

    // Immutable command feedback. The effective binding remains owned solely by
    // HotkeyBindings; presentation never infers success or severity from prose.
    public sealed class HotkeyFeedback
    {
        public HotkeyFeedbackKind Kind { get; }
        public string Summary { get; }
        public string Detail { get; }
        public string Advisory { get; }
        public HotkeyFeedback(HotkeyFeedbackKind kind, string summary = null, string detail = null, string advisory = null)
        { Kind = kind; Summary = summary; Detail = detail; Advisory = advisory; }
        public string Message { get { return (Summary ?? "") + (String.IsNullOrEmpty(Detail) ? "" : " " + Detail) + (String.IsNullOrEmpty(Advisory) ? "" : " 提醒：" + Advisory); } }
    }
}
