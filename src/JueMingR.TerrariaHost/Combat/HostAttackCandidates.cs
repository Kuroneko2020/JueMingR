using JueMingR.Platform.Combat;

namespace JueMingR.TerrariaHost.Combat
{
    // Enumeration does not reinterpret LOS, range or missing models as receiver
    // qualification. The finite actual solver owns clear-line selection proofs.
    internal sealed class HostAttackCandidates
    {
        private int cursor;
        private long tick=-1;
        private HostAttackPhase phase;
        internal NpcIdentity Reachable;
        internal bool Begin(long sampleTick,HostAttackPhase currentPhase)
        {if(tick==sampleTick && phase==currentPhase)return false;tick=sampleTick;phase=currentPhase;return true;}
        internal int Next(int count,int excluded)
        {
            if(count<1 || count==1 && excluded==0)return -1;
            if(cursor>=count)cursor=0;int index=cursor++;
            if(index==excluded){if(cursor>=count)cursor=0;index=cursor++;}
            return index;
        }
        internal void Clear(){cursor=0;tick=-1;Reachable=default(NpcIdentity);}
#if DEBUG
        internal int Queries {get{return 0;}}
        internal int Profiles {get{return 0;}}
#endif
    }
}
