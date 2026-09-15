using System;
using JueMingR.Platform.Guidance;
using Terraria;

namespace JueMingR.TerrariaHost.Npcs
{
    // One completed-game-update cache shared by real consumers. Optional facts
    // are filled on demand; directions never build label names/health groups.
    internal sealed class NativeNpcObservation : IGuidanceNpcSource
    {
        private readonly GuidanceNpc[] facts = new GuidanceNpc[Main.maxNPCs];
        private readonly NpcDemand[] demands = new NpcDemand[Main.maxNPCs];
        private readonly int[] epochs = new int[Main.maxNPCs];
        private int epoch = 1;
#if DEBUG
        internal int BasicReads { get; private set; }
        internal int DirectionReads { get; private set; }
        internal int DangerReads { get; private set; }
        internal int HousingReads { get; private set; }
#endif
        internal bool Readable { get { return Main.npc != null && Main.npc.Length >= Main.maxNPCs; } }
        public int Count { get { return Main.npc == null ? 0 : Math.Min(Main.maxNPCs, Main.npc.Length); } }
        internal void BeginTick()
        {
            if (epoch == int.MaxValue) { Array.Clear(epochs, 0, epochs.Length); epoch = 1; } else epoch++;
        }
        internal void Clear() { Array.Clear(facts, 0, facts.Length); BeginTick(); }
        internal NPC Active(int slot)
        { GuidanceNpc n; return TryRead(slot, NpcDemand.Basic, out n) && n.Active ? n.Identity as NPC : null; }
        public bool TryRead(int slot, NpcDemand demand, out GuidanceNpc value)
        {
            value = default(GuidanceNpc);
            if (slot < 0 || slot >= Count) return false;
            if (epochs[slot] != epoch)
            {
                NPC npc = Main.npc[slot];
                facts[slot] = new GuidanceNpc { Slot = slot, Identity = npc, Active = npc != null && npc.active, Type = npc == null ? 0 : npc.type };
                demands[slot] = NpcDemand.Basic; epochs[slot] = epoch;
#if DEBUG
                BasicReads++;
#endif
            }
            var f = facts[slot]; var native = f.Identity as NPC;
            if (f.Active && native != null)
            {
                NpcDemand missing = demand & ~demands[slot];
                if ((missing & NpcDemand.Direction) != 0)
                {
                    f.NetId = native.netID; f.Generation = native.generation; f.StableIndex = native.whoAmI;
                    f.Hidden = native.hide; f.Rarity = native.rarity;
                    f.X = native.Center.X; f.Y = native.Center.Y; f.Life = native.life;
                    f.DrawX = f.X + native.netOffset.X; f.DrawY = f.Y + native.netOffset.Y;
#if DEBUG
                    DirectionReads++;
#endif
                }
                if ((missing & NpcDemand.Danger) != 0)
                {
                    f.Boss = native.boss; f.Life = native.life;
#if DEBUG
                    DangerReads++;
#endif
                }
                if ((missing & NpcDemand.Housing) != 0)
                {
                    f.Town = native.townNPC; f.Homeless = native.homeless; f.HomeX = native.homeTileX; f.HomeY = native.homeTileY;
                    if ((demand & NpcDemand.Direction) == 0 && (demands[slot] & NpcDemand.Direction) == 0) { f.X = native.Center.X; f.Y = native.Center.Y; }
#if DEBUG
                    HousingReads++;
#endif
                }
                demands[slot] |= demand; facts[slot] = f;
            }
            value = f; return true;
        }
    }
}
