using JueMingR.Platform.Guidance;

namespace JueMingR.Features.Guidance
{
    public sealed class TravellingMerchantDirection
    {
        private readonly IGuidanceNpcSource source;
        private int untilDiscovery;
        public TravellingMerchantDirection(IGuidanceNpcSource source) { this.source = source; }
        public GuidanceNpc Target { get; private set; }
        public bool Visible { get; private set; }
        public void Clear() { Visible = false; untilDiscovery = 0; Target = default(GuidanceNpc); }
        public void InvalidateDiscovery() { untilDiscovery = 0; }
        public void Update(bool enabled)
        {
            if (!enabled) { Clear(); return; }
            GuidanceNpc tracked = default(GuidanceNpc);
            if (Visible && (!source.TryRead(Target.Slot, NpcDemand.Direction, out tracked) || !tracked.SameIdentity(Target) || !Eligible(tracked))) Clear();
            else if (Visible) Target = tracked;
            if (untilDiscovery-- > 0) return;
            untilDiscovery = 14;
            GuidanceNpc best = default(GuidanceNpc); bool found = false;
            for (int i = 0; i < source.Count; i++)
            {
                GuidanceNpc n;
                if (source.TryRead(i, NpcDemand.Direction, out n) && Eligible(n) &&
                    (!found || n.StableIndex < best.StableIndex || n.StableIndex == best.StableIndex && n.Slot < best.Slot)) { best = n; found = true; }
            }
            Target = best; Visible = found;
        }
        private static bool Eligible(GuidanceNpc n)
        { return n.Active && !n.Hidden && n.Type == 368 && n.StableIndex >= 0 && RareCreatureDirection.Finite(n.X) && RareCreatureDirection.Finite(n.Y); }
    }
}
