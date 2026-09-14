using System;
using JueMingR.Platform.Guidance;

namespace JueMingR.Features.Guidance
{
    public sealed class RareCreatureDirection
    {
        private readonly IGuidanceNpcSource source;
        private int untilDiscovery;
        private float previousX, previousY;
        private bool observed;
        public RareCreatureDirection(IGuidanceNpcSource source) { this.source = source; }
        public GuidanceNpc Target { get; private set; }
        public bool Visible { get; private set; }
#if DEBUG
        public int Discoveries { get; private set; }
#endif
        public void Clear() { Visible = observed = false; Target = default(GuidanceNpc); untilDiscovery = 0; }
        public void Update(bool qualified, float x, float y)
        {
            // accCritterGuide/hideInfo are completed vanilla ability facts; no
            // item list, metal-detector or UI exception is reimplemented here.
            if (!qualified || !Finite(x) || !Finite(y)) { Clear(); return; }
            if (observed && Distance(x, y, previousX, previousY) > 1300 * 1300) { Visible = false; untilDiscovery = 0; }
            observed = true; previousX = x; previousY = y;
            GuidanceNpc tracked = default(GuidanceNpc);
            if (Visible && (!source.TryRead(Target.Slot, NpcDemand.Direction, out tracked) || !tracked.SameIdentity(Target) || !Eligible(tracked, x, y)))
            { Visible = false; untilDiscovery = 0; }
            else if (Visible) Target = tracked;
            if (untilDiscovery-- > 0) return;
            untilDiscovery = 14;
#if DEBUG
            Discoveries++;
#endif
            GuidanceNpc best = Target; bool found = Visible;
            double bestDistance = found ? Distance(best.X, best.Y, x, y) : double.MaxValue;
            // One pass for discovery; selected identity/position is checked on
            // every Update above. A saved slot never stands for a new entity.
            for (int i = 0; i < source.Count; i++)
            {
                GuidanceNpc candidate;
                if (!source.TryRead(i, NpcDemand.Direction, out candidate) || !Eligible(candidate, x, y)) continue;
                double distance = Distance(candidate.X, candidate.Y, x, y);
                bool current = Visible && candidate.SameIdentity(Target), bestCurrent = Visible && best.SameIdentity(Target);
                if (!found || candidate.Rarity > best.Rarity || candidate.Rarity == best.Rarity &&
                    (distance < bestDistance || distance == bestDistance && (current && !bestCurrent || !current && !bestCurrent && candidate.Slot < best.Slot)))
                { best = candidate; bestDistance = distance; found = true; }
            }
            Visible = found; Target = found ? best : default(GuidanceNpc);
        }
        private static bool Eligible(GuidanceNpc n, float x, float y)
        { return n.Active && n.Life > 0 && !n.Hidden && n.Rarity > 0 && Finite(n.X) && Finite(n.Y) && Distance(n.X, n.Y, x, y) < 1300 * 1300; }
        internal static bool Finite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }
        internal static double Distance(double x, double y, double a, double b) { x -= a; y -= b; return x * x + y * y; }
    }
}
