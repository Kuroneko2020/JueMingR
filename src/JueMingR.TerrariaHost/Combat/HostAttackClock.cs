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
        internal readonly int Age,LaunchDelay;
        // Projectiles normally means the caller is already at its parent AI.
        // Only the real world-pass preparation runs before every slot. Keep
        // that origin explicit so existing partial/readonly callers never
        // advance their lower slots for a second time.
        internal readonly bool BeforeProjectilePass;
        internal HostAttackClock(NpcTrajectory timeline,HostAttackPhase phase):this(timeline,phase,0){}
        internal HostAttackClock(NpcTrajectory timeline,HostAttackPhase phase,int launchDelay):this(timeline,phase,launchDelay,false){}
        internal HostAttackClock(NpcTrajectory timeline,HostAttackPhase phase,int launchDelay,bool beforeProjectilePass)
        {Phase=phase;LaunchDelay=launchDelay;BeforeProjectilePass=beforeProjectilePass && phase==HostAttackPhase.Projectiles;Age=(int)((long)Main.GameUpdateCount-timeline.SampleTick)-(phase==HostAttackPhase.BeforeNpc?1:0)+launchDelay;}
        internal bool BeforeNpc {get{return Phase==HostAttackPhase.BeforeNpc;}}
        internal bool NextWorld {get{return Phase==HostAttackPhase.CompletedWorld;}}
        internal int FirstTick {get{return Age+(Phase==HostAttackPhase.Projectiles?0:1);}}
        internal bool MovePlayer(int step){return NextWorld || step>0;}
    }
}
