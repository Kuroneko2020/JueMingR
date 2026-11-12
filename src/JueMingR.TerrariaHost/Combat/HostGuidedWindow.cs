using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // A solve receipt, not a cursor capability or a hit. Binding to the exact
    // immutable view prevents another candidate/shorter retry from borrowing
    // the navigator's previous completion. Through counts world ticks only.
    internal readonly struct HostGuidedWindow
    {
        internal readonly NpcTrajectory Timeline;
        internal readonly HostAttackPhase Phase;
        internal readonly uint Tick;
        internal readonly bool Complete,BudgetPaused;
        internal readonly int Through;
        internal HostGuidedWindow(NpcTrajectory timeline,HostAttackPhase phase,bool complete=false,int through=-1,bool budgetPaused=false)
        {Timeline=timeline;Phase=phase;Tick=Main.GameUpdateCount;Complete=complete;Through=through;BudgetPaused=budgetPaused;}
        internal HostGuidedWindow AtPhase(HostAttackPhase phase)
        {return new HostGuidedWindow(Timeline,phase,Complete,Through,BudgetPaused);}
        internal bool Covers(NpcTrajectory timeline,NpcIdentity identity,HostAttackPhase phase,uint tick,int required)
        {return Complete && !BudgetPaused && ReferenceEquals(Timeline,timeline) && timeline!=null && timeline.Identity.Equals(identity) && Phase==phase && Tick==tick && Through>=required;}
        // All relevant live sources must finish. An unqualified/early-returned
        // source is unknown, never an empty set of completed misses.
        internal HostGuidedWindow Merge(HostGuidedWindow other,NpcTrajectory timeline,HostAttackPhase phase)
        {return new HostGuidedWindow(timeline,phase,Complete && other.Complete && ReferenceEquals(Timeline,timeline) && ReferenceEquals(other.Timeline,timeline) && Phase==phase && other.Phase==phase && Tick==Main.GameUpdateCount && other.Tick==Main.GameUpdateCount,Math.Min(Through,other.Through),BudgetPaused || other.BudgetPaused);}
    }
}
