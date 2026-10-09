using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // AI_002 movement: collision uses the previous facing, acquisition follows,
    // then type-specific control and the wet tail. No cosmetic RNG is replayed.
    internal static class NpcEyeMotion
    {
        internal static bool Known(int t){return t==2 || t==116 || t==133 || t==170 || t==171 || t==180 || t>=190 && t<=194 || t==317 || t==318;}
        internal static bool Escape(NpcMotionState n,PredictionEnvironment e)
        {int t=n.EffectiveType;return (t==2 || t==133 || t>=190 && t<=194 || t==317 || t==318) && e.Day && !e.Remix && !e.Graveyard && n.Y<=e.WorldSurface*16;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;int t=n.EffectiveType;bool pig=t==170 || t==171 || t==180;n.NoGravity=true;
            if(!n.NoTileCollide)
            {
                if(n.CollideX){n.Vx=-n.OldVx*.5f;if(n.Direction==-1 && n.Vx>0 && n.Vx<2)n.Vx=2;if(n.Direction==1 && n.Vx<0 && n.Vx>-2)n.Vx=-2;}
                if(n.CollideY){n.Vy=-n.OldVy*.5f;if(n.Vy>0 && n.Vy<1)n.Vy=1;if(n.Vy<0 && n.Vy>-1)n.Vy=-1;}
            }
            if(Escape(n,e)){n.TimeLeft=Math.Min(n.TimeLeft,10);n.Direction=n.Vx>0?1:-1;n.DirectionY=-1;}
            else NpcTargeting.Face(ref n,ref e,true,confused);
            if(pig)
            {
                var player=new MotionRect(e.PlayerX-e.PlayerWidth*.5f,e.PlayerY-e.PlayerHeight*.5f,e.PlayerWidth,e.PlayerHeight);bool clear;
                if(!terrain.CanHit(n.Bounds,player,out clear,out stop))return false;
                if(clear && n.A1>0){bool solid;if(!terrain.Solid(n.Bounds,out solid,out stop))return false;if(!solid)n.A0=n.A1=0;}
                else if(!clear && n.A1==0)n.A0++;
                if(n.A0>=300){n.A1=1;n.A0=0;}
                n.NoTileCollide=n.A1!=0;n.Alpha=n.NoTileCollide?200:0;if(n.NoTileCollide)n.Wet=false;
                NpcTargeting.Face(ref n,ref e,true,confused);
                if(n.Direction==-1 && n.X>player.X+player.Width || n.Direction==1 && n.X+n.Width<player.X)Axis(ref n.Vx,n.Direction,4,4,.08f,.04f,.2f);
                if(n.DirectionY==-1 && n.Y>player.Y+player.Height || n.DirectionY==1 && n.Y+n.Height<player.Y)Axis(ref n.Vy,n.DirectionY,2.5f,2.5f,.1f,.05f,.15f);
            }
            else if(t==116)
            {Axis(ref n.Vx,n.Direction,6,6,.1f,.1f,.2f);Axis(ref n.Vy,n.DirectionY,n.DirectionY==1?1.5f:2.5f,2.5f,.04f,.05f,.15f);}
            else
            {
                bool wander=t==133,damaged=wander && n.Life<n.LifeMax*.5f;float sx=wander?(damaged?6:4):4*(2-n.Scale),sy=wander?(damaged?4:1.5f):1.5f*(2-n.Scale);
                Axis(ref n.Vx,n.Direction,sx,sx,.1f,.1f,-.05f);Axis(ref n.Vy,n.DirectionY,sy,sy,damaged?.1f:.04f,damaged?.1f:.05f,damaged?-.05f:-.03f);
            }
            if(n.Wet && !pig){if(n.Vy>0)n.Vy*=.95f;n.Vy=Math.Max(-4,n.Vy-.5f);NpcTargeting.Face(ref n,ref e,true,confused);}
            return true;
        }
        private static void Axis(ref float value,int direction,float gate,float limit,float acceleration,float over,float reverse)
        {float toward=value*direction;if(toward>=gate)return;toward+=acceleration;if(toward<-limit)toward+=over;else if(toward<0)toward+=reverse;value=Math.Min(limit,toward)*direction;}
    }
}
