using System;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.F5;

namespace JueMingR.TerrariaHost.Information
{
    internal struct InformationPointerSample
    {
        internal bool Active, Focused, Neutral, NewLeft, Left, CanGrab, HigherOwner;
        internal float X, Y;
        internal long Geometry, Session, NativeEpoch;
    }
    internal sealed class InformationAdjustment
    {
        private WindowPosition original;
        private float grabX, grabY, pressX, pressY;
        private int startX, startY;
        private long geometry, session, nativeEpoch;
        private bool neutral, moved, lastValid, leftTail;
        internal bool Active { get; private set; }
        internal bool Dragging { get; private set; }
        internal bool ReturnToF5 { get; private set; }
        internal bool ConsumeLeft { get; private set; }
        internal WindowPosition Draft { get; private set; }
        internal void Begin(WindowPosition intent, bool returnToF5, long sessionGeneration, long nativeGeneration = 0, bool entryNeutral = false)
        {
            if (Active) return;
            original = intent; Draft = null; ReturnToF5 = returnToF5; session = sessionGeneration; nativeEpoch = nativeGeneration;
            Active = true; neutral = entryNeutral; Dragging = moved = lastValid = false; geometry = -1;
        }
        internal void BindGeometry(long value) { if (Active && geometry < 0) geometry = value; }
        // A returned position is a single submitted intent. The caller applies
        // it to its document; ongoing drag samples never mutate preferences.
        internal WindowPosition Step(InformationPointerSample sample, F5Rect bounds)
        {
            ConsumeLeft = leftTail;
            if (sample.Focused && !sample.Left) leftTail = false;
            if (!Active) return null;
            if (!sample.Active || !sample.Focused || sample.Session != session || sample.NativeEpoch != nativeEpoch || geometry >= 0 && sample.Geometry != geometry || sample.HigherOwner ||
                Single.IsNaN(sample.X) || Single.IsInfinity(sample.X) || Single.IsNaN(sample.Y) || Single.IsInfinity(sample.Y) ||
                Math.Abs((double)sample.X) > 1000000 || Math.Abs((double)sample.Y) > 1000000)
            { Cancel(); return null; }
            BindGeometry(sample.Geometry);
            if (!neutral) { neutral = sample.Neutral; return null; }
            if (!Dragging)
            {
                if (!sample.NewLeft || !sample.CanGrab || !bounds.Contains(sample.X, sample.Y)) return null;
                Dragging = true; leftTail = ConsumeLeft = true; pressX = sample.X; pressY = sample.Y;
                grabX = sample.X - bounds.X; grabY = sample.Y - bounds.Y;
                startX = (int)Math.Round(bounds.X); startY = (int)Math.Round(bounds.Y); lastValid = true;
            }
            ConsumeLeft = true;
            lastValid = true;
            if (sample.X != pressX || sample.Y != pressY || moved)
            {
                int x = (int)Math.Round(sample.X - grabX), y = (int)Math.Round(sample.Y - grabY);
                moved |= x != startX || y != startY;
                if (Draft == null || Draft.X != x || Draft.Y != y) Draft = new WindowPosition(x, y);
            }
            // The physical release is also a valid final position sample.
            if (!sample.Left) { WindowPosition result = CommitCandidate(); End(); return result; }
            return null;
        }
        private WindowPosition CommitCandidate()
        {
            if (!Dragging || !moved || !lastValid || Draft == null || Draft.X == startX && Draft.Y == startY || Draft.Equals(original)) return null;
            return Draft;
        }
        internal WindowPosition FinishNormal()
        { WindowPosition result = Active ? CommitCandidate() : null; End(); return result; }
        internal void Cancel() { lastValid = false; End(); }
        private void End() { Active = Dragging = moved = lastValid = false; Draft = null; original = null; }
    }
}
