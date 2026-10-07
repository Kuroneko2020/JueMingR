using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // AI23 keeps a directed velocity during its timed coast and stop. A new
    // launch reads the same numbered player's prepared future; no future AI.
    internal static class NpcChargeMotion
    {
        internal static bool Known(int t){return t==83 || t==84 || t==179;}
        internal static bool Retargets(NpcMotionState n){return n.Target<0 || n.Target==255 || n.PlayerDead;}
        internal static bool NeedsPlayer(NpcMotionState n,int remaining)
        {
            // TargetClosest can update current facing during an independent
            // coast; only an actual launch consumes prepared future geometry.
            if(n.A0==0)return true;
            if(n.JustHit)return remaining>120; // Hit starts a fresh coast clock.
            return n.A0==1?remaining>100-n.A1+120:remaining>120-n.A1;
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;n.NoGravity=n.NoTileCollide=true;
            if(Retargets(n))NpcTargeting.Face(ref n,ref e,true,confused);
            if(n.A0==0)
            {
                float x=e.PlayerX-n.Bounds.CenterX,y=e.PlayerY-n.Bounds.CenterY,length=(float)Math.Sqrt(x*x+y*y);
                if(length==0){stop=PredictionStop.InvalidState;return false;}n.Vx=x*9/length;n.Vy=y*9/length;n.A0=1;n.A1=0;
            }
            else if(n.A0==1)
            {
                if(n.JustHit){n.A0=2;n.A1=0;}n.Vx*=.99f;n.Vy*=.99f;
                if(++n.A1>=100){n.A0=2;n.A1=0;n.Vx=n.Vy=0;}
            }
            else
            {if(n.JustHit){n.A0=2;n.A1=0;}n.Vx*=.96f;n.Vy*=.96f;if(++n.A1>=120)n.A0=n.A1=0;}
            return true;
        }
    }
}
