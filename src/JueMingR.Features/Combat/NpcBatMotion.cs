using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked AI14 movement, including its deliberate second motor pass and
    // same-object 158/159 form. Attack RNG does not choose these velocities.
    internal static class NpcBatMotion
    {
        internal static bool Known(int t)
        {return t==48 || t==121 || t==156 || t==158 || t==226 || t==660 || Twice(t);}
        private static bool Twice(int t)
        {return t==49 || t==51 || t==60 || t==62 || t==66 || t==93 || t==137 || t==150 || t==151 || t==152 || t==634;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;int type=n.EffectiveType;n.NoGravity=true;
            // Collision uses the previous direction before TargetClosest.
            if(n.CollideX){n.Vx=-n.OldVx*.5f;if(n.Direction==-1 && n.Vx>0 && n.Vx<2)n.Vx=2;if(n.Direction==1 && n.Vx<0 && n.Vx>-2)n.Vx=-2;}
            if(n.CollideY){n.Vy=-n.OldVy*.5f;if(n.Vy>0 && n.Vy<1)n.Vy=1;if(n.Vy<0 && n.Vy>-1)n.Vy=-1;}
            int signX=n.Vx<0?-1:1,signY=n.Vy<0?-1:1;NpcTargeting.Face(ref n,ref e,true,confused);
            var player=new MotionRect(e.PlayerX-e.PlayerWidth/2,e.PlayerY-e.PlayerHeight/2,e.PlayerWidth,e.PlayerHeight);
            if(type==226)
            {bool clear;if(!terrain.CanHit(n.Bounds,player,out clear,out stop))return false;if(!clear){n.Direction=signX;n.DirectionY=signY;}}
            if(type==158)
            {
                if(n.Y<e.WorldSurface*16 && e.Day && !e.Eclipse){n.Direction*=-1;n.DirectionY=-1;}
                Axis(ref n.Vx,n.Direction,7,.2f,.1f,.05f,4);Axis(ref n.Vy,n.DirectionY,7,.2f,.1f,.05f,4);
            }
            else if(type==226){Axis(ref n.Vx,n.Direction,4,.2f,.1f,.05f);Axis(ref n.Vy,n.DirectionY,2.5f,.1f,.05f,.03f);}
            else if(type==660){Axis(ref n.Vx,n.Direction,6,.35f,.35f,.175f);Axis(ref n.Vy,n.DirectionY,5,.3f,.3f,.225f);}
            else{Axis(ref n.Vx,n.Direction,4,.1f,.1f,.05f);Axis(ref n.Vy,n.DirectionY,1.5f,.04f,.05f,.03f);}
            if(Twice(type))
            {
                if(n.Wet)Wet(ref n,ref e,confused);
                Axis(ref n.Vx,n.Direction,4,.1f,type==60?.07f:.1f,type==60?.03f:.05f);
                Axis(ref n.Vy,n.DirectionY,1.5f,.04f,type==60?.03f:.05f,type==60?.02f:.03f);
            }
            if(type==48 && n.Wet)Wet(ref n,ref e,confused);
            if(type==158 && !e.Multiplayer)
            {
                float x=e.PlayerX-n.Bounds.CenterX,y=e.PlayerY-n.Bounds.CenterY;
                if(x*x+y*y<40000 && n.Y+n.Height<player.Y+player.Height)
                {bool clear;if(!terrain.CanHit(n.Bounds,player,out clear,out stop))return false;if(clear)Transform(ref n,159,ref e,confused);}
            }
            n.A1++;if(n.EffectiveType==158)n.A1++;
            if(n.A1>200)
            {
                if(!e.PlayerWet){bool clear;if(!terrain.CanHit(n.Bounds,player,out clear,out stop))return false;if(clear)n.A1=0;}
                bool slow=n.EffectiveType==48 || n.EffectiveType==62 || n.EffectiveType==66;
                float ax=slow?.12f:.2f,ay=slow?.07f:.1f,lx=slow?3:4,ly=slow?1.25f:1.5f;
                if(n.A1>1000)n.A1=0;n.A2++;
                if(n.A2>0){if(n.Vy<ly)n.Vy+=ay;}else if(n.Vy>-ly)n.Vy-=ay;
                if(n.A2<-150 || n.A2>150){if(n.Vx<lx)n.Vx+=ax;}else if(n.Vx>-lx)n.Vx-=ax;
                if(n.A2>300)n.A2=-300;
            }
            return true;
        }
        private static void Wet(ref NpcMotionState n,ref PredictionEnvironment e,bool confused)
        {if(n.Vy>0)n.Vy*=.95f;n.Vy=Math.Max(-4,n.Vy-.5f);NpcTargeting.Face(ref n,ref e,true,confused);}
        private static void Axis(ref float v,int direction,float limit,float acceleration,float over,float reverse,float overGate=0)
        {float toward=v*direction;if(toward>=limit)return;toward+=acceleration;if(toward<-(overGate==0?limit:overGate))toward+=over;else if(toward<0)toward-=reverse;v=Math.Min(limit,toward)*direction;}
        internal static void Transform(ref NpcMotionState n,int type,ref PredictionEnvironment e,bool confused)
        {
            // SetDefaults preserves velocity and X, and Transform preserves
            // Bottom rather than Center. The real origin identity stays intact.
            int height=type==159?40:22;n.Y+=n.Height-height;n.Width=type==159?18:22;n.Height=height;
            n.MotionType=type;n.Style=type==159?3:14;n.NoGravity=n.NoTileCollide=false;n.Scale=1;n.Alpha=0;n.TimeLeft=750;
            n.A0=n.A1=n.A2=n.A3=n.L0=n.L1=n.L2=n.L3=0;n.CollideX=n.CollideY=n.JustHit=false;n.WetCount=0;n.ConfusedTicks=0;
            n.Health= new NpcHealthState{Defense=type==159?24:32,RealLife=-1,DamageMultiplier=1};n.BuffFingerprint=0;n.BuffExpires=0;
            NpcTargeting.Face(ref n,ref e,true,false);n.NewSegment=true;
        }
    }
}
