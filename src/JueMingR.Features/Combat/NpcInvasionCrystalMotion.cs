using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Crystal548 is a moving target body, not a health-only attachment. Its
    // hostile protection differs from ordinary player damage eligibility.
    internal static class NpcInvasionCrystalMotion
    {
        internal static bool Known(NpcMotionState n){return n.EffectiveType==548 && n.Style==105;}
        internal static bool NeedsPlayer(NpcMotionState n){return false;}
        internal static bool Retargets(NpcMotionState n){return false;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return a.Style==b.Style;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;
            if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(n.A1==0)
            {
                if(n.A0>0)n.A0--;
                if(n.A0==0){n.A0=180;if(!e.Multiplayer && n.L0==0)n.L0=1;}
                return true;
            }
            if(n.A1!=1 && n.A1!=2)return true;
            n.HostileProtected=true;n.Life=n.LifeMax;n.A0++;
            if(n.A1==2)
            {n.NoGravity=true;n.Vy=n.A0<=120?(float)Math.Cos(n.A0/120f*((float)Math.PI*2f))*.25f-.25f:0;}
            else
            {
                if(n.A0<96)n.Vy=-n.A0/96f;
                if(n.A0>=96)n.Alpha=Math.Min(255,n.Alpha+50);
            }
            if(n.A0<600)return true;
            // StopInvasion changes the entire shared event and can wipe all
            // DD2 actors at this slot. Bound the whole private group before
            // that action, rather than continue enemies against a false event.
            if(!e.DD2OngoingCaptured){stop=PredictionStop.MissingDependency;return false;}
            if(e.DD2Ongoing)
            {if(!e.Multiplayer)n.Active=false;stop=e.Multiplayer?PredictionStop.PhaseBoundary:PredictionStop.Despawn;return false;}
            if(n.A1==2){n.A1=1;n.A0=0;n.Life=n.LifeMax;return true;}
            n.Active=false;n.Life=0;stop=PredictionStop.Despawn;return false;
        }
    }
}
