using JueMingR.Platform.Combat;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal enum HostAttackPhase { BeforeNpc,Projectiles,CompletedWorld }
    // Source stamps the NPC position epoch. These three phases have different
    // first damage/player/immunity steps despite sharing GameUpdateCount.
    internal readonly struct HostAttackClock
    {
        internal readonly HostAttackPhase Phase;
        internal readonly int Age;
        internal HostAttackClock(NpcTrajectory timeline,HostAttackPhase phase)
        {Phase=phase;Age=(int)((long)Main.GameUpdateCount-timeline.SampleTick)-(phase==HostAttackPhase.BeforeNpc?1:0);}
        internal bool BeforeNpc {get{return Phase==HostAttackPhase.BeforeNpc;}}
        internal bool NextWorld {get{return Phase==HostAttackPhase.CompletedWorld;}}
        internal int FirstTick {get{return Age+(Phase==HostAttackPhase.Projectiles?0:1);}}
        internal bool MovePlayer(int step){return NextWorld || step>0;}
    }
}
