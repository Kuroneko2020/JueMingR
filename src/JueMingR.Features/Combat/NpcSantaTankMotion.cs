using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked AI61: the four life bands, receiving firing pause and foot-height
    // controller own this actor's motor. Random projectile clocks do not feed
    // its geometry; this scalar model neither advances them nor creates shots.
    internal static class NpcSantaTankMotion
    {
        internal static bool Known(NpcMotionState n){return n.Style==61 && n.EffectiveType==346;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b);}
        internal static bool NeedsPlayer(NpcMotionState n){return true;}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e){return !e.Day;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;
            if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            n.NoGravity=n.NoTileCollide=true;
            if(!e.Day)NpcTargeting.Face(ref n,ref e,true,confused);
            if(n.TargetCaptured && !n.HasPlayer){stop=PredictionStop.MissingDependency;return false;}
            float speed=2;
            if((double)n.Life<n.LifeMax*.75)speed=3;
            if((double)n.Life<n.LifeMax*.5)speed=4;
            if((double)n.Life<n.LifeMax*.25)speed=5;
            bool pause=false;
            if(e.Day)
            {n.TimeLeft=Math.Min(n.TimeLeft,10);speed=8;if(n.Vx==0)n.Vx=.1f;}
            else if(n.A0==0)
            {n.A1++;if(n.A1>=300 && !e.Multiplayer){NpcTargeting.Face(ref n,ref e,true,confused);n.A1=0;n.A0=1;}}
            else if(n.A0==1)
            {n.A1++;pause=true;if(n.A1>240){n.A0=n.A1=0;}}
            if(Math.Abs(n.Bounds.CenterX-e.PlayerX)<50)pause=true;
            if(pause)
            {n.Vx*=.9f;if((double)n.Vx>-.1 && (double)n.Vx<.1)n.Vx=0;}
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
                {if(n.Vy>0)n.Vy=0;n.Vy-=n.Vy>-.2?.025f:.2f;if(n.Vy<-4)n.Vy=-4;}
                else
                {if(n.Vy<0)n.Vy=0;n.Vy+=n.Vy<.1?.025f:.5f;}
            }
            if(n.Vy>10)n.Vy=10;return true;
        }
    }
}
