using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked AI57 (325/344): shots and dust never feed the actor's motor.
    // Only the server's phase choice is random. Keep its last deterministic
    // prefix, then resume the observed chosen phase at the next real sample.
    internal static class NpcTreeBossMotion
    {
        internal static bool Known(NpcMotionState n){return n.Style==57 && (n.EffectiveType==325 || n.EffectiveType==344);}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b) && a.EffectiveType==b.EffectiveType;}
        internal static bool NeedsPlayer(NpcMotionState n){return true;}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e){return !e.Day;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;
            if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            n.NoGravity=n.NoTileCollide=true;
            if(!e.Day)NpcTargeting.Face(ref n,ref e,true,confused);
            if(n.TargetCaptured && !n.HasPlayer){stop=PredictionStop.MissingDependency;return false;}
            float speed=2;bool pause=false;
            if((double)n.Life<(double)n.LifeMax*.75)speed=3;
            if((double)n.Life<(double)n.LifeMax*.5)speed=4;
            if(e.Day){n.TimeLeft=Math.Min(n.TimeLeft,10);speed=8;}
            else if(n.A0==0)
            {
                n.A1++;
                if((double)n.Life<(double)n.LifeMax*.5)n.A1++;
                if((double)n.Life<(double)n.LifeMax*.25)n.A1++;
                if(n.A1>=300 && !e.Multiplayer){stop=PredictionStop.RandomDecision;return false;}
            }
            else if(n.A0==1 || n.A0==2)
            {
                pause=true;n.A1++;
                float end=n.A0==2?300:n.EffectiveType==344?180:120;
                if(n.A1>=end)n.A0=n.A1=0;
            }
            else if(n.A0==3 || n.A0==4)
            {
                // The chosen branch keeps speed 4 even on its reset action.
                speed=4;n.A1++;
                if(n.A1>=(n.A0==3?120:240))n.A0=n.A1=0;
            }
            else{stop=PredictionStop.UnsupportedMechanism;return false;}
            if(Math.Abs(n.Bounds.CenterX-e.PlayerX)<50)pause=true;
            if(pause){n.Vx*=.9f;if((double)n.Vx>-.1 && (double)n.Vx<.1)n.Vx=0;}
            else if(n.Direction>0)n.Vx=(n.Vx*20f+speed)/21f;
            else if(n.Direction<0)n.Vx=(n.Vx*20f-speed)/21f;
            float playerLeft=e.PlayerX-e.PlayerWidth*.5f,playerBottom=e.PlayerY+e.PlayerHeight*.5f;
            bool overPlayer=n.X<playerLeft && n.X+n.Width>playerLeft+e.PlayerWidth && n.Y+n.Height<playerBottom-16;
            if(overPlayer)n.Vy+=.5f;
            else
            {
                bool solid;if(terrain==null){stop=PredictionStop.MissingDependency;return false;}
                if(!terrain.Solid(new MotionRect(n.Bounds.CenterX-40,n.Y+n.Height-20,80,20),out solid,out stop))return false;
                if(solid)
                {if(n.Vy>0)n.Vy=0;n.Vy-=(double)n.Vy>-.2?.025f:.2f;if(n.Vy<-4)n.Vy=-4;}
                else
                {if(n.Vy<0)n.Vy=0;n.Vy+=(double)n.Vy<.1?.025f:.5f;}
            }
            if(n.Vy>10)n.Vy=10;return true;
        }
    }
}
