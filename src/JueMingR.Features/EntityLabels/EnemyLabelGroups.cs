using System;
using JueMingR.Platform.Entities;

namespace JueMingR.Features.EntityLabels
{
    // Scratch is one slot per native entity, reset each observation. A worm is
    // walked once from its verified head; a bad edge never falls back to summing
    // members or treating a reused index as the old life owner.
    internal sealed class EnemyLabelGroups
    {
        private int[] owner = new int[0], anchor = new int[0];
        private bool[] valid = new bool[0], complete = new bool[0];
        internal int Unresolved { get; private set; }
        internal int Owner(int slot) { return owner[slot]; }
        internal int Anchor(int root) { return anchor[root]; }

        internal void Build(EntityObservation observation)
        {
            int count = observation.Count;
            if (owner.Length != count) { owner = new int[count]; anchor = new int[count]; valid = new bool[count]; complete = new bool[count]; }
            for (int i = 0; i < count; i++) { owner[i] = -1; anchor[i] = -1; valid[i] = complete[i] = false; }
            Unresolved = 0;
            for (int root = 0; root < count; root++)
            {
                EntityFact head = observation[root];
                if (!EntityLabelFeature.IsEnemy(head) || head.SharedFamily == 0 ||
                    head.Role != EntitySegmentRole.Head && head.Role != EntitySegmentRole.SharedOwner) continue;
                if (!Healthy(head) || head.HealthOwnerSlot != -1 && head.HealthOwnerSlot != root) { Unresolved++; continue; }
                owner[root] = root;
                // An unsynchronized/broken tail cannot invalidate the current
                // head's own life. It only removes remote member anchor choices.
                valid[root] = true;
                if (head.Role == EntitySegmentRole.SharedOwner) { complete[root] = true; continue; }
                int previous = root, next = head.NextSlot;
                while (next >= 0 && next < count)
                {
                    EntityFact part = observation[next];
                    if (owner[next] != -1 || !EntityLabelFeature.IsEnemy(part) || part.SharedFamily != head.SharedFamily ||
                        part.HealthOwnerSlot != root || part.PreviousSlot != previous ||
                        part.Role != EntitySegmentRole.Body && part.Role != EntitySegmentRole.Tail) break;
                    owner[next] = root;
                    if (part.Role == EntitySegmentRole.Tail) { complete[root] = true; break; }
                    previous = next; next = part.NextSlot;
                }
                if (!complete[root]) Unresolved++;
            }
            for (int i = 0; i < count; i++)
            {
                EntityFact part = observation[i];
                if (!EntityLabelFeature.IsEnemy(part)) continue;
                if (part.Role == EntitySegmentRole.SharedMember)
                {
                    int target = part.HealthOwnerSlot;
                    if (target >= 0 && target < count && valid[target] && observation[target].Role == EntitySegmentRole.SharedOwner &&
                        observation[target].SharedFamily == part.SharedFamily) owner[i] = target;
                }
                int root = owner[i];
                if (root < 0 || !valid[root] || i != root && !complete[root]) { owner[i] = -1; if (part.HealthOwnerSlot >= 0 || part.Role != EntitySegmentRole.None) Unresolved++; continue; }
                if (!part.Visible || !part.DrawEligible) continue;
                int selected = anchor[root];
                if (i == root || selected != root && (selected < 0 || Distance(part, observation) < Distance(observation[selected], observation))) anchor[root] = i;
            }
        }
        private static double Distance(EntityFact part, EntityObservation observation)
        { double x = part.X - observation.ViewCenterX, y = part.Y - observation.ViewCenterY; return x * x + y * y; }
        internal static bool Healthy(EntityFact fact) { return fact.Life > 0 && fact.LifeMax > 0; }
    }
}
