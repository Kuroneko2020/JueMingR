using System;
using JueMingR.Platform.Entities;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.EntityLabels
{
    internal sealed class EntityHostObservation : IEntityObservationSource
    {
        private readonly Func<bool> sessionActive;
        private EntityFact[] facts = new EntityFact[0];
        private int[] moonParts = new int[0];
        private Relation[] relations = new Relation[0];
        // Terraria 1.4.5.8 NPC AI generation blocks: 50341–50363,
        // 51683–51917. This finite Host map describes verified shared families;
        // EoW 13/14/15 is intentionally absent: its realLife is forced to -1.
        private static readonly int[][] Worms = {
            new[] { 7, 8, 9 }, new[] { 10, 11, 12 }, new[] { 39, 40, 41 }, new[] { 87, 88, 89, 90, 91, 92 },
            new[] { 95, 96, 97 }, new[] { 98, 99, 100 }, new[] { 117, 118, 119 }, new[] { 134, 135, 136 },
            new[] { 412, 413, 414 }, new[] { 454, 455, 456, 457, 458, 459 }, new[] { 510, 511, 512 },
            new[] { 513, 514, 515 }, new[] { 621, 622, 623 }
        };
        internal EntityHostObservation(Func<bool> sessionActive) { this.sessionActive = sessionActive ?? throw new ArgumentNullException(nameof(sessionActive)); }
        internal int FailedObjects { get; private set; }
        internal int UnsupportedHidden { get; private set; }
        internal void EndSession()
        { Array.Clear(facts, 0, facts.Length); Array.Clear(relations, 0, relations.Length); FailedObjects = UnsupportedHidden = 0; }
        public bool TryObserve(EntityObservationDemand demand, out EntityObservation observation)
        {
            observation = default(EntityObservation);
            if (!sessionActive() || Main.gameMenu || Main.dedServ || Main.netMode != 0 && Main.netMode != 1 || Main.npc == null || Main.GameViewMatrix == null) return false;
            int count = Math.Min(Main.maxNPCs, Main.npc.Length);
            if (facts.Length != count) { facts = new EntityFact[count]; relations = new Relation[count]; moonParts = new int[count]; }
            Array.Clear(facts, 0, count); Array.Clear(moonParts, 0, count); FailedObjects = UnsupportedHidden = 0;
            if (demand == EntityObservationDemand.None) { observation = new EntityObservation(facts, 0, 0); return true; }
            bool enemies = (demand & EntityObservationDemand.Enemy) != 0;
            bool critters = (demand & EntityObservationDemand.Critter) != 0;
            bool npcs = (demand & EntityObservationDemand.Npc) != 0;
            float zoomX = Main.GameViewMatrix.ZoomMatrix.M11, zoomY = Main.GameViewMatrix.ZoomMatrix.M22;
            if (!Finite(zoomX) || !Finite(zoomY) || zoomX <= 0 || zoomY <= 0) return false;
            float centerX = Main.screenPosition.X + Main.screenWidth / 2f, centerY = Main.screenPosition.Y + Main.screenHeight / 2f;
            float halfWidth = Main.screenWidth / (2 * zoomX), halfHeight = Main.screenHeight / (2 * zoomY);
            for (int i = 0; i < count; i++)
            {
                NPC npc = Main.npc[i];
                if (npc == null || !npc.active) { relations[i] = null; continue; }
                try
                {
                    int type = npc.type;
                    if (type <= 0 || type >= NPCID.Count || !Finite(npc.position.X) || !Finite(npc.position.Y)) { FailedObjects++; continue; }
                    bool town = npc.townNPC, merchant = type == NPCID.SkeletonMerchant;
                    var fact = new EntityFact { Active = true, TownNpc = town, SkeletonMerchant = merchant, Friendly = npc.friendly,
                        Critter = (enemies || critters) && (npc.CountsAsACritter || NPCID.Sets.CountsAsCritter[type] || npc.catchItem > 0),
                        Gold = critters && NPCID.Sets.IsGoldCritter[type], TypeName = npc.TypeName,
                        GivenName = npcs && (town || merchant) ? npc.GivenOrTypeName : null,
                        // Training apparatus and two balloon rescue carriers are
                        // not enemies. The actual released slime has its own slot/type.
                        Excluded = type == NPCID.TargetDummy || type == NPCID.WindyBalloon || type == NPCID.BoundTownSlimePurple,
                        // Native DrawNPCs/DrawCachedNPCs temporarily add netOffset
                        // before drawing. Borrow the same interpolation read-only.
                        DrawEligible = !npc.hide, X = npc.position.X + npc.netOffset.X + npc.width / 2f, Y = npc.position.Y + npc.netOffset.Y + npc.gfxOffY,
                        Height = npc.height,
                        Life = enemies ? npc.life : 0, LifeMax = enemies ? npc.lifeMax : 0,
                        HealthOwnerSlot = enemies ? npc.realLife : -1, NextSlot = -1, PreviousSlot = -1 };
                    fact.Visible = Math.Abs(fact.X - centerX) <= halfWidth + npc.width / 2f + 64 &&
                        Math.Abs(fact.Y - centerY) <= halfHeight + npc.height + 96;
                    if (enemies)
                    {
                        SetFamily(type, ref fact);
                        if (fact.SharedFamily != 0)
                        {
                            // A padded scan candidate must not win the group's
                            // visible-head priority over a genuinely onscreen
                            // member, only to be culled by the text renderer.
                            fact.Visible = Math.Abs(fact.X - centerX) <= halfWidth + npc.width / 2f &&
                                fact.Y + npc.height >= centerY - halfHeight && fact.Y <= centerY + halfHeight;
                            fact.NextSlot = Slot(npc.ai[0], count); fact.PreviousSlot = Slot(npc.ai[1], count);
                            if (fact.Role == EntitySegmentRole.SharedOwner) fact.HealthOwnerSlot = -1;
                        }
                        if (type == 396 || type == 397)
                        {
                            int root = Slot(npc.ai[3], count);
                            int bit = type == 396 ? 1 : npc.ai[2] == 0 ? 2 : npc.ai[2] == 1 ? 4 : 0;
                            if (root >= 0 && bit != 0) { if ((moonParts[root] & bit) != 0) moonParts[root] |= 8; moonParts[root] |= bit; }
                        }
                    }
                    facts[i] = fact;
                }
                catch { FailedObjects++; facts[i] = default(EntityFact); }
            }
            if (enemies)
            {
                for (int i = 0; i < count; i++)
                {
                    if (!facts[i].Active) continue;
                    NPC npc = Main.npc[i]; EntityFact fact = facts[i];
                    if (npc.type == 396 || npc.type == 397 || npc.type == 398)
                    {
                        int root = npc.type == 398 ? i : Slot(npc.ai[3], count);
                        // Native cache requires core + head + both hands. Destroyed
                        // eyes restore life for animation; their negative phase is
                        // not a live full-health target. Never merge ai[3] life.
                        fact.DrawEligible = root >= 0 && facts[root].Active && Main.npc[root].type == 398 && moonParts[root] == 7 &&
                            (Main.npc[root].ai[0] == 0 || Main.npc[root].ai[0] == 1) && npc.ai[0] >= 0;
                        if (fact.DrawEligible && i != root && !SameObservedRoot(i, root)) { fact.DrawEligible = false; FailedObjects++; }
                    }
                    else if (npc.hide && !fact.Excluded && !fact.Friendly) UnsupportedHidden++;
                    if (fact.Role == EntitySegmentRole.SharedMember || fact.Role == EntitySegmentRole.Body || fact.Role == EntitySegmentRole.Tail)
                    {
                        int root = fact.HealthOwnerSlot;
                        bool valid = root >= 0 && root < count && facts[root].Active && fact.SharedFamily != 0 && facts[root].SharedFamily == fact.SharedFamily &&
                            (fact.Role != EntitySegmentRole.SharedMember || root == Main.wofNPCIndex && Main.npc[root].type == 113);
                        if (!valid || !SameObservedRoot(i, root)) { fact.Excluded = true; FailedObjects++; }
                    }
                    facts[i] = fact;
                }
            }
            observation = new EntityObservation(facts, centerX, centerY); return true;
        }
        private bool SameObservedRoot(int slot, int root)
        {
            NPC member = Main.npc[slot], owner = Main.npc[root]; Relation relation = relations[slot];
            if (relation == null || !relation.MemberMatches(member)) relations[slot] = relation = new Relation(member, owner);
            if (!relation.RootMatches(owner)) relation.Rejected = true;
            // Reopening the feature cannot forget a root replacement. Only a new
            // member identity or real Session exit permits another association.
            // A first observation cannot reconstruct native birth history.
            return !relation.Rejected;
        }
        private static void SetFamily(int type, ref EntityFact fact)
        {
            if (type == 113 || type == 114)
            { fact.SharedFamily = 113; fact.Role = type == 113 ? EntitySegmentRole.SharedOwner : EntitySegmentRole.SharedMember; return; }
            foreach (int[] family in Worms)
                for (int i = 0; i < family.Length; i++)
                    if (family[i] == type)
                    { fact.SharedFamily = family[0]; fact.Role = i == 0 ? EntitySegmentRole.Head : i == family.Length - 1 ? EntitySegmentRole.Tail : EntitySegmentRole.Body; return; }
        }
        private static int Slot(float value, int count)
        { return Finite(value) && value >= 0 && value < count && value == (int)value ? (int)value : -1; }
        private static bool Finite(float value) { return !Single.IsNaN(value) && !Single.IsInfinity(value); }
        private sealed class Relation
        {
            private readonly NPC member, root;
            private readonly byte memberGeneration, rootGeneration;
            private readonly int memberType, memberNetId, rootType, rootNetId;
            internal bool Rejected;
            internal Relation(NPC member, NPC root)
            { this.member = member; this.root = root; memberGeneration = member.generation; rootGeneration = root.generation;
                memberType = member.type; memberNetId = member.netID; rootType = root.type; rootNetId = root.netID; }
            internal bool MemberMatches(NPC value) { return ReferenceEquals(member, value) && value.generation == memberGeneration && value.type == memberType && value.netID == memberNetId; }
            internal bool RootMatches(NPC value) { return ReferenceEquals(root, value) && value.generation == rootGeneration && value.type == rootType && value.netID == rootNetId; }
        }
    }
}
