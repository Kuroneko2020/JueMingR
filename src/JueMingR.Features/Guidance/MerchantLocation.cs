using JueMingR.Platform.Guidance;

namespace JueMingR.Features.Guidance
{
    public sealed class MerchantLocation
    {
        private static readonly string[] pylons = { "森林晶塔附近", "丛林晶塔附近", "神圣晶塔附近", "地下晶塔附近", "海洋晶塔附近", "沙漠晶塔附近", "雪地晶塔附近", "蘑菇晶塔附近", "万能晶塔附近", "地狱晶塔附近", "微光晶塔附近" };
        private int remaining;
        private GuidanceNpc previous;
        public string Text { get; private set; } = "位置未知";
#if DEBUG
        public int Explanations { get; private set; }
        public int ResidentPasses { get; private set; }
#endif
        public void Clear() { remaining = 0; previous = default(GuidanceNpc); Text = "位置未知"; }
        public void Update(GuidanceNpc target, IGuidanceNpcSource npcs, IGuidanceLocationSource source)
        {
            // Camera/player motion does not invalidate this explanation. A
            // 15-update metadata revisit also admits late housing/pylon facts.
            if (remaining-- > 0 && previous.SameIdentity(target)) return;
            remaining = 14; previous = target;
#if DEBUG
            Explanations++;
#endif
            double x = target.X / 16, y = target.Y / 16, nearest = 120 * 120;
            int match = -1;
            for (int i = 0; i < source.PylonCount; i++)
            {
                GuidancePylon p;
                if (!source.TryPylon(i, out p) || p.Type < 0 || p.Type >= pylons.Length) continue;
                double distance = RareCreatureDirection.Distance(x, y, p.X, p.Y);
                if (distance <= nearest && (match < 0 || distance < nearest)) { match = p.Type; nearest = distance; }
            }
            if (match >= 0) { Text = pylons[match]; return; }
#if DEBUG
            ResidentPasses++;
#endif
            int residents = 0;
            for (int i = 0; i < npcs.Count; i++)
            {
                GuidanceNpc n;
                if (!npcs.TryRead(i, NpcDemand.Housing, out n) || !n.Active || !n.Town || n.Type == 368 || n.Homeless || n.HomeX < 0 || n.HomeY < 0) continue;
                if (RareCreatureDirection.Distance(n.X / 16, n.Y / 16, n.HomeX, n.HomeY) <= 100 * 100 &&
                    RareCreatureDirection.Distance(x, y, n.HomeX, n.HomeY) <= 120 * 120 && ++residents >= 2)
                { Text = "居民聚居处附近"; return; }
            }
            int width, height; double surface;
            if (!source.TryWorld(out width, out height, out surface) || width <= 0 || height <= 200 || !RareCreatureDirection.Finite(surface) ||
                !RareCreatureDirection.Finite(x) || !RareCreatureDirection.Finite(y) || x < 0 || y < 0 || x >= width || y >= height) { Text = "位置未知"; return; }
            Text = y >= height - 200 ? "地狱附近" : (x <= 380 || x >= width - 380) && y <= surface + 40 ? "海岸附近" : y > surface + 40 ? "地下" : "地表";
        }
    }
}
