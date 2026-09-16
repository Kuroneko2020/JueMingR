using System;
using System.Globalization;

namespace JueMingR.Features.Footprints
{
    public sealed class FootprintPlayback
    {
        private static readonly int[] speeds = { 1, 10, 60, 300, 1800 };
        private int speed;
        public double Cursor { get; private set; }
        public long DisplayEnd { get; private set; }
        public bool Latest { get; private set; }
        public bool Playing { get; private set; }
        public bool Visible { get; private set; }
        public int Speed { get { return speeds[speed]; } }
        public void Show(long end) { speed = 0; Visible = true; GoLatest(end); }
        public void Hide() { Visible = Playing = false; }
        public void GoLatest(long end) { Cursor = DisplayEnd = Math.Max(0, end); Latest = true; Playing = false; }
        public void Seek(double value, long end)
        { DisplayEnd = Math.Max(0, end); Cursor = Math.Max(0, Math.Min(DisplayEnd, value)); Latest = false; Playing = false; }
        public void CycleSpeed() { speed = (speed + 1) % speeds.Length; }
        public void Toggle(long end)
        {
            if (Playing) { Playing = false; Latest = false; return; }
            if (end <= 0) return;
            if (Latest || Cursor >= end) Cursor = 0;
            DisplayEnd = end; Latest = false; Playing = true;
        }
        // Caller passes a monotonic *visible* UI delta. Fractional steps stay in
        // Cursor; the world may be paused while this independent clock advances.
        public void Advance(double seconds, long end, bool active)
        {
            if (!Visible) return;
            if (Latest) { Cursor = DisplayEnd = end; return; }
            if (!Playing || !active || seconds <= 0 || Double.IsNaN(seconds) || Double.IsInfinity(seconds)) return;
            DisplayEnd = end; Cursor = Math.Min(end, Cursor + seconds * 60 * Speed);
            if (Cursor >= end) Playing = false;
        }
        public static string FormatTime(long steps)
        { long seconds = Math.Max(0, steps) / 60; return (seconds / 3600).ToString(CultureInfo.InvariantCulture) + ":" + (seconds / 60 % 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (seconds % 60).ToString("00", CultureInfo.InvariantCulture); }
    }
}
