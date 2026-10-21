using Terraria;

namespace JueMingR.TerrariaHost.Movement
{
    // Seven finite resource families, owned by the exact local player/runtime
    // epoch. Return, menus and loadout changes never replenish this ledger.
    internal sealed class SmartFallResourceCycle
    {
        private Player player;
        private long epoch, cycle, snapshotCycle;
        private int spent, jumps;
        private int rockets,rocketDelay,carpetTime;
        private bool carpet, sampled;
        internal long Revision { get; private set; }
        internal int Spent { get { return spent; } }
        internal long Cycle { get { return cycle; } }
        internal void Bind(Player p,long generation)
        { if(ReferenceEquals(player,p) && epoch==generation)return;player=p;epoch=generation;spent=0;sampled=false;cycle++;Revision++; }
        internal void Refresh(Player p,long generation)
        { Bind(p,generation);spent=0;sampled=false;cycle++;Revision++; }
        internal int Available(int provided,int functional)
        { return provided & ~functional & ~spent & 127; }
        internal void Grant(int bit) { int next=spent|bit;if(next!=spent){spent=next;Revision++;} }
        internal void Before(Player p,long generation)
        { Bind(p,generation);jumps=SmartFallCapabilities.UnusedJumps(p)&31;rockets=p.rocketTime;rocketDelay=p.rocketDelay;carpet=p.canCarpet;carpetTime=p.carpetTime;sampled=true;snapshotCycle=cycle; }
        internal void After(Player p,long generation)
        {
            Bind(p,generation);
            if(!sampled || snapshotCycle!=cycle)return;
            int used=jumps & ~SmartFallCapabilities.UnusedJumps(p);
            if(p.rocketTime<rockets || rocketDelay==0 && p.rocketDelay>0)used|=32;
            if(carpet && !p.canCarpet && p.carpetTime>0 || p.carpetFrame>=0 && p.carpetTime<carpetTime)used|=64;
            Grant(used);
        }
        internal void Clear() { player=null;epoch=0;spent=0;sampled=false;cycle++;Revision++; }
    }
}
