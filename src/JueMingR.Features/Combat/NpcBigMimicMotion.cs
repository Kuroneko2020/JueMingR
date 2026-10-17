using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked AI87 shares these action motors across all four large chests.
    // Random next-action selection ends before its draw; an observed choice
    // resumes its real jump/flight motor rather than a velocity tangent.
    internal static class NpcBigMimicMotion
    {
        internal static bool Known(NpcMotionState n){return n.Style==87 && n.EffectiveType>=473 && n.EffectiveType<=476;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return a.A0==b.A0;}
        internal static bool NeedsPlayer(NpcMotionState n){return n.A0!=7;}
        internal static bool Retargets(NpcMotionState n)
        {return n.A0!=7 && n.PlayerDead || n.A0==0 || n.A0==2 && n.Vy==0 || n.A0==4 || n.A0==6 && n.Vy==0;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(terrain==null){stop=PredictionStop.MissingDependency;return false;}
            n.ReflectsProjectiles=false;n.NoTileCollide=n.NoGravity=false;n.Health.DontTakeDamage=false;n.CanReceive=!n.Health.Immortal;
            if(n.A0!=7 && e.PlayerDead)
            {
                NpcTargeting.Face(ref n,ref e,true,confused);
                if(e.PlayerDead){n.A0=7;n.A1=n.A2=n.A3=0;}
            }
            if(n.A0==0)
            {
                NpcTargeting.Face(ref n,ref e,true,confused);
                if(!e.Multiplayer && (n.Vx!=0 || n.Vy>100 || n.JustHit || Distance(n,e)<80)){n.A0=1;n.A1=0;}
            }
            else if(n.A0==1){if(++n.A1>36 && !e.Multiplayer){n.A0=2;n.A1=0;}}
            else if(n.A0==2)
            {
                if(!e.Multiplayer && Distance(n,e)>600){n.A0=5;n.A1=n.A2=n.A3=0;}
                if(n.Vy==0)
                {
                    NpcTargeting.Face(ref n,ref e,true,confused);n.Vx*=.85f;n.A1++;
                    float delay=15+30*((float)n.Life/n.LifeMax),speed=3+4*(1-(float)n.Life/n.LifeMax),jump=4;bool clear;
                    if(!Clear(n,e,terrain,out clear,out stop))return false;if(!clear)jump+=2;
                    if(!e.Multiplayer && n.A1>delay)
                    {if(++n.A3>=3){n.A3=0;jump*=2;speed/=2;}n.A1=0;n.Vy-=jump;n.Vx=speed*n.Direction;}
                }
                else{n.Vx*=.99f;if(n.Direction<0 && n.Vx>-1)n.Vx=-1;if(n.Direction>0 && n.Vx<1)n.Vx=1;}
                n.A2++;
                if(n.A2>210 && n.Vy==0 && !e.Multiplayer){stop=PredictionStop.RandomDecision;return false;}
            }
            else if(n.A0==3)
            {n.ReflectsProjectiles=e.Expert;n.Vx*=.85f;n.Health.DontTakeDamage=true;n.CanReceive=false;if(++n.A1>=180 && !e.Multiplayer){n.A0=2;n.A1=0;}}
            else if(n.A0==4)
            {
                n.NoTileCollide=n.NoGravity=true;n.Direction=n.Vx<0?-1:1;n.SpriteDirection=n.Direction;
                NpcTargeting.Face(ref n,ref e,true,confused);float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-350-n.Bounds.CenterY;
                if(n.A2==1)
                {
                    n.A1++;dy+=350;if(!Normalize(ref dx,ref dy,8,out stop))return false;n.Vx=(n.Vx*4+dx)/5;n.Vy=(n.Vy*4+dy)/5;
                    if(!e.Multiplayer && n.A1>6){n.A1=0;n.A0=4.1f;n.A2=0;n.Vx=dx;n.Vy=dy;}
                }
                else if(Math.Abs(dx)<40 && n.Bounds.CenterY<e.PlayerY-300){if(!e.Multiplayer){n.A1=0;n.A2=1;}}
                else{if(!Normalize(ref dx,ref dy,12,out stop))return false;n.Vx=(n.Vx*5+dx)/6;n.Vy=(n.Vy*5+dy)/6;}
            }
            else if(n.A0==4.1f)
            {
                bool clear,solid;
                if(n.A2==0)
                {if(!Clear(n,e,terrain,out clear,out stop))return false;if(clear){if(!terrain.Solid(n.Bounds,out solid,out stop))return false;if(!solid)n.A2=1;}}
                if(n.Y+n.Height>=e.PlayerY-e.PlayerHeight*.5f || n.Vy<=0)
                {
                    if(++n.A1>10 && !e.Multiplayer)
                    {n.A0=2;n.A1=n.A2=n.A3=0;if(!terrain.Solid(n.Bounds,out solid,out stop))return false;if(solid)n.A0=5;}
                }
                else if(n.A2==0)n.NoTileCollide=n.NoGravity=true;
                n.Vy=Math.Min(16,n.Vy+.2f);
            }
            else if(n.A0==5)
            {
                n.Direction=n.Vx>0?1:-1;n.SpriteDirection=n.Direction;n.NoTileCollide=n.NoGravity=true;
                float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY-4,distance=(float)Math.Sqrt(dx*dx+dy*dy);
                if(!e.Multiplayer && distance<200)
                {bool solid;if(!terrain.Solid(n.Bounds,out solid,out stop))return false;if(!solid){n.A0=2;n.A1=n.A2=n.A3=0;}}
                if(distance>10 && !Normalize(ref dx,ref dy,10,out stop))return false;n.Vx=(n.Vx*4+dx)/5;n.Vy=(n.Vy*4+dy)/5;
            }
            else if(n.A0==6)
            {
                if(n.Vy==0)
                {
                    NpcTargeting.Face(ref n,ref e,true,confused);n.Vx*=.8f;
                    if(++n.A1>5)
                    {
                        n.A1=0;n.Vy-=4;float bottom=e.PlayerY+e.PlayerHeight*.5f,center=n.Bounds.CenterY;
                        if(bottom<center)n.Vy-=1.25f;if(bottom<center-40)n.Vy-=1.5f;if(bottom<center-80)n.Vy-=1.75f;
                        if(bottom<center-120)n.Vy-=2;if(bottom<center-160)n.Vy-=2.25f;if(bottom<center-200)n.Vy-=2.5f;
                        bool clear;if(!Clear(n,e,terrain,out clear,out stop))return false;if(!clear)n.Vy-=2;n.Vx=12*n.Direction;n.A2++;
                    }
                }
                else{n.Vx*=.98f;if(n.Direction<0 && n.Vx>-8)n.Vx=-8;if(n.Direction>0 && n.Vx<8)n.Vx=8;}
                if(!e.Multiplayer && n.A2>=3 && n.Vy==0){n.A0=2;n.A1=n.A2=n.A3=0;}
            }
            else if(n.A0==7)
            {n.CanHarm=false;n.Life=n.LifeMax;n.Health.Defense=9999;n.NoTileCollide=true;n.Alpha=Math.Min(255,n.Alpha+7);n.Vx*=.98f;}
            else if(n.A0==8)
            {n.Vx*=.85f;n.A1++;if(!e.Multiplayer && (!e.Anniversary || n.A1>=180)){n.A0=2;n.A1=0;}}
            // Native's unknown action returns with only the reset flags above.
            // No representative next phase or cannon/projectile is generated.
            return true;
        }
        private static float Distance(NpcMotionState n,PredictionEnvironment e)
        {float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY;return (float)Math.Sqrt(dx*dx+dy*dy);}
        private static bool Clear(NpcMotionState n,PredictionEnvironment e,IPredictionTerrain t,out bool clear,out PredictionStop stop)
        {return t.CanHit(new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,1,1),new MotionRect(e.PlayerX,e.PlayerY,1,1),out clear,out stop);}
        private static bool Normalize(ref float x,ref float y,float speed,out PredictionStop stop)
        {// Optimized x86 XNA retains the squared sum and inverse in extended
            // precision, but rounds sqrt and each stored component to float.
            // Explicit stages reproduce this in Debug and Release alike.
            double squared=(double)x*x+(double)y*y;stop=PredictionStop.None;if(squared==0){stop=PredictionStop.InvalidState;return false;}float root=(float)Math.Sqrt(squared);double inverse=1d/root;x=(float)(x*inverse);y=(float)(y*inverse);x*=speed;y*=speed;return true;}
    }
}
