using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked .8 motion phases only: future attacks/RNG are conditions, never
    // replayed. Unknown mechanisms use bounded observed acceleration/curvature
    // with real gravity/contact instead of claiming deterministic native AI.
    internal static class NpcRollingMotion
    {
        internal static bool Hopper(int type){return type==177 || type==174 || type==378;}
        internal static bool TortoiseType(int type){return type==153 || type==154 || type==417 || type==496 || type==497;}
        internal static bool HopperRetargets(NpcMotionState n,PredictionEnvironment e)
        {
            // Share the exact next action's targeting gates with Source. The
            // jump clock first reads the old target, then the jump reselects.
            if(n.A2>1)n.A2--;if(n.A2==0)return true;
            int type=n.EffectiveType;if(type==378 && (n.A1==5 || n.Wet || Distance(n,e)<64))return false;
            if(n.Wet && type!=177 && type!=378){if(n.CollideY)return true;if(n.Vy>4)n.Vy*=.95f;n.Vy=Math.Max(-4,n.Vy-.3f);}
            if(n.Vy!=0 || n.A3==n.X || n.A2!=1)return false;
            return n.A0+(type==177?2:5)+(int)Math.Min(30,(type==177?2000:4000)/Math.Max(.001f,Distance(n,e)))>=0;
        }
        private static float Distance(NpcMotionState n,PredictionEnvironment e)
        {float x=e.PlayerX-n.Bounds.CenterX,y=e.PlayerY-n.Bounds.CenterY;return (float)Math.Sqrt(x*x+y*y);}
        internal static void Derpling(ref NpcMotionState n,PredictionEnvironment e,int direction,bool confused=false)
        {
            int type=n.EffectiveType;bool derpling=type==177;
            if(n.A2>1)n.A2--;
            if(n.A2==0){n.A0=-100;n.A2=1;NpcTargeting.Face(ref n,ref e,true,confused);direction=n.Direction;n.SpriteDirection=direction;}
            if(type==378 && n.A1==5)
            {
                n.Vx=n.Vy=0;n.X+=n.Width/2-80;n.Y+=n.Height/2-80;n.Width=n.Height=160;n.CanReceive=false;n.Health.DontTakeDamage=true;
                if(n.A2==1){n.Life=-1;n.Active=false;}return;
            }
            float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY,distance=(float)Math.Sqrt(dx*dx+dy*dy);
            if(type==378 && (n.Wet || distance<64)){n.A1=5;n.A2=10;return;}
            if(n.Wet && !derpling && type!=378)
            {
                if(n.CollideX){n.Direction*=-n.Direction;n.SpriteDirection=n.Direction;}
                if(n.CollideY){NpcTargeting.Face(ref n,ref e,true,confused);if(n.OldVy<0)n.Vy=5;else n.Vy-=2;n.SpriteDirection=n.Direction;}
                if(n.Vy>4)n.Vy*=.95f;n.Vy=Math.Max(-4,n.Vy-.3f);
            }
            if(n.Vy==0)
            {
                if(n.A3==n.X){n.Direction=-n.Direction;n.A2=300;}n.A3=0;
                n.Vx*=.8f;if(Math.Abs(n.Vx)<.1f)n.Vx=0;
                n.A0+=(derpling?2:5)+(int)Math.Min(30,(derpling?2000:4000)/Math.Max(.001f,distance));
                if(n.A0>=0)
                {
                    if(n.A2==1){NpcTargeting.Face(ref n,ref e,true,confused);direction=n.Direction;}
                    bool big=n.A1==(derpling?2:3);n.Vy=big?(derpling?-11.5f:-9):(derpling?-7.5f:-5);n.Vx+=(big?(derpling?2:3):(derpling?4:5))*n.Direction;
                    if(distance>200 && distance<350)n.Vx+=n.Direction;
                    n.A0=big?-200:-120;n.A1=big?0:n.A1+1;if(big)n.A3=n.X;
                }
                n.SpriteDirection=n.Direction;
            }
            else
            {
                bool above=n.Y+n.Height<e.PlayerY-e.PlayerHeight/2 && n.X+n.Width>e.PlayerX-e.PlayerWidth/2 && n.X<e.PlayerX+e.PlayerWidth/2;
                if(derpling && above){n.Vx*=.92f;if(n.Vy<0)n.Vy=n.Vy*.9f+.1f;}
                else if(n.Direction==1 && n.Vx<(derpling?4:3) || n.Direction==-1 && n.Vx>-(derpling?4:3))
                {if(n.Direction==-1 && n.Vx<.1f || n.Direction==1 && n.Vx>-.1f)n.Vx+=.2f*n.Direction;else n.Vx*=.93f;}
            }
        }
        internal static bool Tortoise(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int direction,int vertical,out PredictionStop stop,bool confused=false)
        {
            stop=PredictionStop.None;int type=n.EffectiveType;bool small=type==496 || type==497;
            if(n.Direction==0 || n.Target<0 || e.PlayerDead){NpcTargeting.Face(ref n,ref e,true,confused);direction=n.Direction;vertical=n.DirectionY;}
            if(!NpcGroundMotion.StepUp(ref n,terrain,out stop,true))return false;
            if(n.JustHit && type!=417){n.A0=n.A1=0;NpcTargeting.Face(ref n,ref e,true,confused);direction=n.Direction;vertical=n.DirectionY;}
            float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-e.PlayerHeight/2-n.Bounds.CenterY;
            float distance=(float)Math.Sqrt(dx*dx+dy*dy);
            if(n.A0==0)
            {
                bool clear;if(!terrain.CanHit(n.Bounds,new MotionRect(e.PlayerX-e.PlayerWidth/2,e.PlayerY-e.PlayerHeight/2,e.PlayerWidth,e.PlayerHeight),out clear,out stop))return false;
                if(n.Vx!=0)n.Direction=n.Vx>0?1:-1;
                if(distance>200 && clear)n.A1+=small?2:4;
                if(distance>600 && (clear || n.Y+n.Height>e.PlayerY-e.PlayerHeight/2-200))n.A1+=small?4:10;
                if(n.Wet && !small)n.A1=1000;
                if(++n.A1>=400){n.A1=0;n.A0=1;}
                if(!n.JustHit && n.Vx!=n.OldVx)n.Direction=-n.Direction;
                // AI39 checks the forward four-by-five support rectangle
                // after contact direction changes, before walk acceleration.
                // Unknown cells end the prefix; they are never an empty pit.
                if(n.Vy==0 && e.PlayerY-e.PlayerHeight/2<n.Y+n.Height)
                {
                    int center=(int)(((double)n.X+n.Width*.5)/16),left=n.Direction>0?center:center-3,right=n.Direction>0?center+3:center;
                    int top=(int)((n.Y+n.Height+2)/16)-1;bool supported=false;
                    for(int x=left;x<=right;x++)for(int y=top;y<=top+4;y++)
                    {PredictionTile cell;if(!terrain.Tile(x,y,out cell,out stop))return false;if(cell.Active && cell.Solid)supported=true;}
                    if(!supported){n.Direction=-n.Direction;n.Vx=.1f*n.Direction;}
                }
                float speed=small?.5f:distance<400?1:1.5f;
                if(Math.Abs(n.Vx)>speed){if(n.Vy==0){n.Vx*=.8f;n.Vy*=.8f;}}
                else n.Vx=NpcMotion.Approach(n.Vx,n.Direction*speed,.07f);
            }
            else if(n.A0==1)
            {
                n.Vx*=.5f;n.A1+=small?.5f:1;
                if(n.A1>=30)
                {
                    NpcTargeting.Face(ref n,ref e,true,confused);n.A1=n.A2=0;n.A0=3;
                    if(type==417){n.Y+=n.Height-32;n.Height=32;n.A0=6;n.A2=2;n.UncertainBounceCount=true;}
                }
            }
            else if(n.A0==3)
            {
                if(++n.A1==1)
                {
                    NpcTargeting.Face(ref n,ref e,true,confused);direction=n.Direction;vertical=n.DirectionY;
                    dx=e.PlayerX-n.Bounds.CenterX;dy=e.PlayerY-e.PlayerHeight/2-n.Bounds.CenterY;
                    n.A1++;n.A2+=.3f;
                    bool clear;if(!terrain.CanHit(n.Bounds,new MotionRect(e.PlayerX-e.PlayerWidth/2,e.PlayerY-e.PlayerHeight/2,e.PlayerWidth,e.PlayerHeight),out clear,out stop))return false;
                    dy-=vertical>0?0:Math.Abs(dx)*.2f;
                    float length=Math.Max(.001f,(float)Math.Sqrt(dx*dx+dy*dy)),speed=(clear?10:6)*(small?.75f:1);
                    n.Vx=dx*speed/length;n.Vy=clear?dy*speed/length:-10;n.A3=n.Vx;
                }
                else
                {
                    if(n.X+n.Width>e.PlayerX-e.PlayerWidth/2 && n.X<e.PlayerX+e.PlayerWidth/2 && n.Y<e.PlayerY+e.PlayerHeight/2)
                    {n.Vx*=.8f;n.A3=0;if(n.Vy<0)n.Vy+=.2f;}
                    if(n.A3!=0){n.Vx=n.A3;n.Vy-=.22f;}
                    if(n.A1>=90){n.NoGravity=false;n.A1=0;n.A0=4;}
                }
                if(n.Wet && n.DirectionY<0)n.Vy-=.3f;
            }
            else if(n.A0==4)
            {
                if(n.Wet && n.DirectionY<0)n.Vy-=.3f;n.Vx*=.96f;
                if(n.A2>0)n.A2-=.01f;
                if(n.A2<=0 && (n.Vy==0 || n.Wet)){n.A0=5;n.A1=n.A2=0;}
            }
            else if(n.A0==6 && type==417)
            {
                n.A1++;if(n.A3>0){n.A3++;if(n.A3>=10)n.A3=0;}
                if(n.A1==1)
                {
                    NpcTargeting.Face(ref n,ref e,true,confused);bool clear;if(!terrain.CanHit(n.Bounds,new MotionRect(e.PlayerX-e.PlayerWidth/2,e.PlayerY-e.PlayerHeight/2,e.PlayerWidth,e.PlayerHeight),out clear,out stop))return false;
                    dx=e.PlayerX-n.Bounds.CenterX;dy=e.PlayerY-e.PlayerHeight/2-n.Bounds.CenterY-(n.DirectionY>0?0:Math.Abs(dx)*.2f);float length=Math.Max(.001f,(float)Math.Sqrt(dx*dx+dy*dy)),speed=clear?16:10;n.Vx=dx*speed/length;n.Vy=clear?dy*speed/length:-12;
                }
                else{if(n.X+n.Width>e.PlayerX-e.PlayerWidth/2 && n.X<e.PlayerX+e.PlayerWidth/2 && n.Y<e.PlayerY+e.PlayerHeight/2){n.Vx*=.9f;if(n.Vy<0)n.Vy+=.2f;}if(n.A2==0 || n.A1>=1200){n.A1=0;n.A0=5;}}
                if(n.Wet && n.DirectionY<0)n.Vy-=.3f;
            }
            else if(n.A0==5){if(type==417){n.Y+=n.Height-52;n.Height=52;}n.Vx=0;n.A1+=small?.5f:1;if(n.A1>=30){NpcTargeting.Face(ref n,ref e,true,confused);n.A0=n.A1=0;}if(n.Wet){n.A0=3;n.A1=0;}}
            return true;
        }
        internal static void Trend(ref NpcMotionState n,int elapsed)
        {
            // Recent correction is a bounded displacement, not a permanent
            // added speed/heading extrapolated for the remaining two seconds.
            // Contacts revoke it and retain the collision owner's velocity.
            if(!n.TrendInitialized){n.TrendBaseVx=n.Vx;n.TrendBaseVy=n.Vy;n.TrendInitialized=true;}
            if(n.CollideX || n.CollideY){n.ObservedTurn=n.ObservedAccelerationX=n.ObservedAccelerationY=0;n.TrendBaseVx=n.Vx;n.TrendBaseVy=n.Vy;}
            float curve=elapsed<=12?elapsed*(13-elapsed)/12f:0;
            float boost=elapsed<=8?elapsed*(9-elapsed)/8f:0;
            float angle=n.NoGravity?n.ObservedTurn*curve:0,c=(float)Math.Cos(angle),sin=(float)Math.Sin(angle);
            n.Vx=n.TrendBaseVx*c-n.TrendBaseVy*sin+n.ObservedAccelerationX*boost;
            if(n.NoGravity)n.Vy=n.TrendBaseVx*sin+n.TrendBaseVy*c+n.ObservedAccelerationY*boost;
            n.TrendApplied=true;n.TrendOutputVx=n.Vx;n.TrendOutputVy=n.Vy;
        }
        internal static void Complete(ref NpcMotionState n)
        {
            // A trend's temporary correction must not feed its own baseline.
            // A real motor/contact/liquid result does own the next baseline,
            // including dry exits without CollideX/Y. Retire the correction
            // once so later steps cannot resurrect the pre-physics velocity.
            // Ordinary gravity remains an independent vertical integrator.
            if(n.TrendInitialized && (!n.TrendApplied || n.Vx!=n.TrendOutputVx || n.NoGravity && n.Vy!=n.TrendOutputVy || n.CollideX || n.CollideY))
            {
                n.TrendBaseVx=n.Vx;n.TrendBaseVy=n.Vy;
                n.ObservedTurn=n.ObservedAccelerationX=n.ObservedAccelerationY=0;
            }
        }
    }
}
