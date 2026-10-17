using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked .8 AI8 body/receiving motion. The original attack emissions and
    // cosmetic local clocks are separate owners; they do not change this motor.
    // Unknown server teleport selection ends before its actual consumer.
    internal static class NpcCasterMotion
    {
        internal static bool Known(NpcMotionState n)
        {
            if(n.Style!=8)return false;
            switch(n.EffectiveType)
            {case 24:case 29:case 32:case 45:case 172:case 281:case 282:case 283:case 284:case 285:case 286:case 533:case 693:return true;default:return false;}
        }
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e){return false;}
        // Native TargetClosest changes presentation/attack aim here, never the
        // body motor or its teleport decision clock. No foreign PC is frozen.
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e){return false;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b)
        {return (a.A2!=0 && a.A3!=0)==(b.A2!=0 && b.A3!=0) && a.L3==b.L3;}

        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;int type=n.EffectiveType;
            bool redHat=type==32 && n.L3==1,wall=false;
            bool skeletonQuery=type==32 && !redHat,wallQuery=type==24 && e.GoodWorld;
            if(skeletonQuery || wallQuery)
            {
                if(!n.CasterRelationsCaptured || !n.CasterRelationsComplete || group==null || count<1 || count>group.Length)
                {stop=PredictionStop.MissingDependency;return false;}
                int first=int.MaxValue;
                for(int i=0;i<count;i++)
                {
                    var actor=group[i];int slot=actor.Identity.Slot;
                    // Original FindFirstNPC/AnyNPCs ignore dummy200, do not
                    // filter active Life0, and use the current native slot order.
                    if(!actor.Active || slot<0 || slot>=NpcPredictionCache.Capacity)continue;
                    if(wallQuery && actor.EffectiveType==113)wall=true;
                    if(skeletonQuery && actor.EffectiveType==35 && slot<first)
                    {first=slot;redHat=actor.A3==1;}
                }
                if(redHat)n.L3=1; // This native latch survives the head's death.
            }
            n.Vx*=.93f;
            if(n.Vx>-.1f && n.Vx<.1f)n.Vx=0;
            if(n.A0==0)n.A0=500;
            if(type==172){if(n.Alpha<255)n.Alpha++;if(n.JustHit)n.Alpha=0;}
            if(n.A2!=0 && n.A3!=0)
            {
                n.X=n.A2*16-n.Width/2+8;n.Y=n.A3*16-n.Height;
                n.NetOffsetX=n.NetOffsetY=0;n.Vx=n.Vy=0;n.A2=n.A3=0;
                n.NewSegment=true;if(type==172)n.Alpha=255;
            }
            n.A0++;
            if(type==283 || type==284){if(n.A0>=450)n.A0=700;}
            else if(type==281 || type==282){if(n.A0>=540)n.A0=700;}
            else if(type!=172 && type!=533)
            {
                if(wall)SpeedClock(ref n.A0);
                if(redHat)SpeedClock(ref n.A0);
            }
            if((type==285 || type==286) && n.A0>400)n.A0=650;
            if(type==533 && n.A0>=360)n.A0=650;
            // .8 uses netMode != 1. e.Multiplayer is exactly netMode == 1;
            // both local/server refuse RNG, clients keep their observed motor.
            if(n.A0>=650 && !e.Multiplayer)
            {stop=PredictionStop.RandomDestination;return false;}
            return true;
        }
        private static void SpeedClock(ref float clock)
        {clock+=1;if(clock%2==1)clock-=1;}
    }
}
