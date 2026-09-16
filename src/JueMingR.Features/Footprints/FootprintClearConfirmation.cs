using System;

namespace JueMingR.Features.Footprints
{
    public sealed class FootprintClearConfirmation
    {
        private string archive;
        private bool presented, pressed, accepted;
        public int Stage { get; private set; }
        public string Label { get { return Stage == 0 ? "清除足迹" : Stage == 1 ? "确定？" : "不可恢复，确定？"; } }
        public void Open(string identity) { archive = identity; Stage = 0; presented = pressed = accepted = false; }
        public void Cancel() { Open(null); }
        // Only an actual completed render enables a new stage. Multiple input
        // updates or duplicate releases without a Draw cannot advance twice.
        public void Presented() { if (archive != null && !accepted) presented = true; }
        public void Press(string identity) { pressed = presented && !accepted && String.Equals(identity, archive, StringComparison.Ordinal) && archive != null; }
        public bool Release(string identity)
        {
            if (!String.Equals(identity, archive, StringComparison.Ordinal)) { Cancel(); return false; }
            bool activate = pressed && presented && !accepted; pressed = false;
            if (!activate) return false;
            presented = false;
            if (Stage < 2) { Stage++; return false; }
            accepted = true; return true;
        }
    }
}
