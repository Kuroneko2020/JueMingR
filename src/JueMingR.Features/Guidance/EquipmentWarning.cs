using System;

namespace JueMingR.Features.Guidance
{
    public sealed class EquipmentWarning
    {
        private int[] danger = new int[0], nextDanger = new int[0];
        private readonly int[] risk = new int[15], nextRisk = new int[15];
        private int dangerCount, riskCount, events;
        private double started;
        private bool hasRisk;
        public const string Text = "当前装备含非战斗用品";
        public float Alpha { get; private set; }
#if DEBUG
        public int Notifications { get; private set; }
        public int Classifications { get; private set; }
#endif
        public void Clear() { hasRisk = false; dangerCount = riskCount = events = 0; Alpha = 0; }
        public void Update(bool known, bool alive, int eventBits, int[] bosses, int bossCount, int[] equipment, int equipmentCount, double now)
        {
            // Unknown removes unverified stale content, without claiming that
            // equipment was corrected. No external notification owns this clock.
            if (!known || !alive || !RareCreatureDirection.Finite(now) || eventBits == 0 && bossCount == 0) { Clear(); return; }
            if (nextDanger.Length < bossCount) { nextDanger = new int[bossCount]; Array.Resize(ref danger, bossCount); }
            int nd = 0, nr = 0;
            for (int i = 0; i < bossCount; i++) Insert(nextDanger, ref nd, bosses[i]);
            for (int i = 0; i < Math.Min(15, equipmentCount); i++)
            {
#if DEBUG
                Classifications++;
#endif
                if (EquipmentRules.IsNonCombat(equipment[i])) Insert(nextRisk, ref nr, equipment[i]);
            }
            if (nr == 0) { Clear(); return; }
            bool changed = !hasRisk || events != eventBits || !Same(danger, dangerCount, nextDanger, nd) || !Same(risk, riskCount, nextRisk, nr);
            if (changed)
            {
                started = now; events = eventBits; dangerCount = nd; riskCount = nr;
                Array.Copy(nextDanger, danger, nd); Array.Copy(nextRisk, risk, nr); hasRisk = true;
#if DEBUG
                Notifications++;
#endif
            }
            double age = Math.Max(0, now - started);
            Alpha = age <= 3 ? 1 : age < 3.25 ? (float)((3.25 - age) / .25) : 0;
        }
        private static void Insert(int[] buffer, ref int count, int value)
        {
            int at = 0; while (at < count && buffer[at] < value) at++;
            if (at < count && buffer[at] == value) return;
            for (int i = count; i > at; i--) buffer[i] = buffer[i - 1];
            buffer[at] = value; count++;
        }
        private static bool Same(int[] a, int ac, int[] b, int bc)
        { if (ac != bc) return false; for (int i = 0; i < ac; i++) if (a[i] != b[i]) return false; return true; }
    }
}
