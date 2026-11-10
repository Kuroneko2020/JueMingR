using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;

namespace JueMingR.TerrariaHost.F5
{
    // UI owns only this physical gesture and its release tail. Every sample
    // reads the host's current target; cancel never restores an older value.
    internal sealed class ViewZoomControl
    {
        private readonly IViewControls host;
        private readonly F5Interaction state;
        private readonly Func<double> clock;
        private bool tail;
        private int generation, skin, direction;
        private object font;
        private Matrix matrix;
        private F5Rect capturedTrack, capturedButton;
        private float offset, minimum, maximum;
        private double nextRepeat;
        internal bool Captured { get; private set; }
        internal bool ConsumeLeft { get; private set; }
        internal bool ConsumeWheel { get; private set; }
        internal bool OwnsPointer { get { return Captured || tail || ConsumeLeft; } }
        internal bool Available { get { return host.ControlsEnabled; } }
        internal float Value { get { return host.ZoomTarget; } }
        internal float Minimum { get { return host.MinimumZoomTarget; } }
        internal float Maximum { get { return host.MaximumZoomTarget; } }
        internal ViewZoomControl(IViewControls host, F5Interaction state, Func<double> clock = null)
        { this.host = host; this.state = state; this.clock = clock ?? (() => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency); }

        internal static bool Owns(F5Command command)
        { return command == F5Command.ViewZoomMinus || command == F5Command.ViewZoomPlus || command == F5Command.ViewZoomTrack || command == F5Command.ViewZoomValue; }
        internal static float BlueStart(float minimum) { return minimum < 1f ? .3f : 0f; }
        internal static float BlueEnd(float minimum) { return minimum < 1f ? .7f : 4f / 7f; }
        private static float LogLerp(float a, float b, float t)
        { if (t <= 0) return a; if (t >= 1) return b; return (float)Math.Exp(Math.Log(a) + t * Math.Log(b / a)); }
        internal static float FromPosition(float position, float minimum, float maximum)
        {
            float p = Math.Max(0, Math.Min(1, position)), low = BlueStart(minimum), high = BlueEnd(minimum);
            if (p <= 0) return minimum;
            if (p >= 1) return maximum;
            if (p < low) return LogLerp(minimum, 1f, p / low);
            if (p <= high) return LogLerp(1f, 2f, (p - low) / (high - low));
            return LogLerp(2f, maximum, (p - high) / (1f - high));
        }
        internal static float ToPosition(float value, float minimum, float maximum)
        {
            float low = BlueStart(minimum), high = BlueEnd(minimum);
            if (value <= minimum) return 0;
            if (value >= maximum) return 1;
            if (value < 1f) return low * (float)(Math.Log(value / minimum) / Math.Log(1f / minimum));
            if (value <= 2f) return low + (high - low) * (float)(Math.Log(value) / Math.Log(2));
            return high + (1f - high) * (float)(Math.Log(value / 2f) / Math.Log(maximum / 2f));
        }
        internal void Process(bool active, bool focused, bool left, bool newLeft, bool cancel, float x, float y,
            Matrix projection, object fontIdentity, int skinGeneration)
        {
            ConsumeLeft = tail; ConsumeWheel = Captured;
            F5Rect track = default(F5Rect), minus = default(F5Rect), plus = default(F5Rect);
            F5Rect view = state.Layout.Viewport.Offset(state.X, state.Y);
            if (state.Visible && state.Page == 2)
                foreach (var element in state.Layout.Elements)
                {
                    var rect = element.Rect.Offset(view.X, view.Y - state.Scroll);
                    if (element.Command == F5Command.ViewZoomTrack) track = rect;
                    if (element.Command == F5Command.ViewZoomMinus) minus = rect;
                    if (element.Command == F5Command.ViewZoomPlus) plus = rect;
                }
            bool valid = active && focused && state.Visible && state.Page == 2 && Available && track.Width > 0 && !cancel;
            if (Captured && (!valid || generation != state.Layout.Generation || skin != skinGeneration ||
                !ReferenceEquals(font, fontIdentity) || matrix != projection || !Same(track, capturedTrack) ||
                minimum != Minimum || maximum != Maximum)) Cancel();
            if (valid && !Captured && !tail && left && newLeft && view.Contains(x, y) &&
                (track.Contains(x, y) || minus.Contains(x, y) || plus.Contains(x, y)))
            {
                state.CancelControlCapture();
                Captured = tail = true; ConsumeLeft = ConsumeWheel = true;
                generation = state.Layout.Generation; skin = skinGeneration; font = fontIdentity; matrix = projection;
                capturedTrack = track; minimum = Minimum; maximum = Maximum;
                direction = minus.Contains(x, y) ? -1 : plus.Contains(x, y) ? 1 : 0;
                capturedButton = direction < 0 ? minus : plus;
                float thumb = track.X + track.Width * ToPosition(Value, Minimum, Maximum);
                offset = direction == 0 && Math.Abs(x - thumb) <= 6 ? x - thumb : 0;
                if (direction != 0) { host.TrySetZoomTarget(Value + direction * .05f); nextRepeat = clock() + 350; }
            }
            if (Captured)
            {
                ConsumeLeft = ConsumeWheel = true;
                if (!left) Captured = false;
                else if (direction == 0) host.TrySetZoomTarget(FromPosition((x - offset - track.X) / track.Width, Minimum, Maximum));
                else if (!capturedButton.Contains(x, y)) Cancel();
                else
                {
                    double now = clock();
                    // Auto-pause does not stop this monotonic UI clock. A late
                    // sample issues one step, never a backlog of repeat events.
                    if (now >= nextRepeat) { host.TrySetZoomTarget(Value + direction * .05f); nextRepeat = now + 70; }
                }
            }
            // Synthetic focus-loss releases do not end the owned physical tail.
            if (focused && !left) tail = false;
        }
        private static bool Same(F5Rect a, F5Rect b)
        { return a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height; }
        internal void Cancel()
        { if (Captured) { Captured = false; tail = true; ConsumeLeft = true; } ConsumeWheel = false; direction = 0; nextRepeat = 0; }
    }
}
