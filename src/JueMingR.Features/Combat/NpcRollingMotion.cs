using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked .8 motion phases only: future attacks/RNG are conditions, never
    // replayed. Unknown mechanisms use bounded observed acceleration/curvature
    // with real gravity/contact instead of claiming deterministic native AI.
    internal static class NpcRollingMotion
    {
        internal static void Derpling(ref NpcMotionState n,PredictionEnvironment e,int direction,bool confused=false)
        {
            if(n.A2>1)n.A2--;
            if(n.A2==0){n.A0=-100;n.A2=1;NpcTargeting.Face(ref n,ref e,true,confused);direction=n.Direction;}
            if(n.Vy==0)
            {
                if(n.A3==n.X){n.Direction=-n.Direction;n.A2=300;}n.A3=0;
                n.Vx*=.8f;if(Math.Abs(n.Vx)<.1f)n.Vx=0;
                float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY;
                float distance=(float)Math.Sqrt(dx*dx+dy*dy);
                n.A0+=2+(int)Math.Min(30,2000/Math.Max(.001f,distance));
                if(n.A0>=0)
                {
                    if(n.A2==1){NpcTargeting.Face(ref n,ref e,true,confused);direction=n.Direction;}
                    bool big=n.A1==2;n.Vy=big?-11.5f:-7.5f;n.Vx+=(big?2:4)*n.Direction;
                    if(distance>200 && distance<350)n.Vx+=n.Direction;
                    n.A0=big?-200:-120;n.A1=big?0:n.A1+1;if(big)n.A3=n.X;
                }
            }
            else
            {
                bool above=n.Y+n.Height<e.PlayerY-e.PlayerHeight/2 && n.X+n.Width>e.PlayerX-e.PlayerWidth/2 && n.X<e.PlayerX+e.PlayerWidth/2;
                if(above){n.Vx*=.92f;if(n.Vy<0)n.Vy=n.Vy*.9f+.1f;}
                else if(n.Direction==1 && n.Vx<4 || n.Direction==-1 && n.Vx>-4)
                {if(n.Direction==-1 && n.Vx<.1f || n.Direction==1 && n.Vx>-.1f)n.Vx+=.2f*n.Direction;else n.Vx*=.93f;}
            }
        }
        internal static bool Tortoise(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int direction,int vertical,out PredictionStop stop,bool confused=false)
        {
            stop=PredictionStop.None;
            if(!NpcGroundMotion.StepUp(ref n,terrain,out stop,true))return false;
            if(n.Direction==0 || n.Target<0 || e.PlayerDead){NpcTargeting.Face(ref n,ref e,true,confused);direction=n.Direction;vertical=n.DirectionY;}
            if(n.JustHit){n.A0=n.A1=0;NpcTargeting.Face(ref n,ref e,true,confused);direction=n.Direction;vertical=n.DirectionY;}
            float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-e.PlayerHeight/2-n.Bounds.CenterY;
            float distance=(float)Math.Sqrt(dx*dx+dy*dy);
            if(n.A0==0)
            {
                bool clear;if(!terrain.CanHit(n.Bounds,new MotionRect(e.PlayerX-e.PlayerWidth/2,e.PlayerY-e.PlayerHeight/2,e.PlayerWidth,e.PlayerHeight),out clear,out stop))return false;
                if(n.Vx!=0)n.Direction=n.Vx>0?1:-1;
                if(distance>200 && clear)n.A1+=4;
                if(distance>600 && (clear || n.Y+n.Height>e.PlayerY-e.PlayerHeight/2-200))n.A1+=10;
                if(n.Wet)n.A1=1000;
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
                float speed=distance<400?1:1.5f;
                if(Math.Abs(n.Vx)>speed){if(n.Vy==0){n.Vx*=.8f;n.Vy*=.8f;}}
                else n.Vx=NpcMotion.Approach(n.Vx,n.Direction*speed,.07f);
            }
            else if(n.A0==1)
            {n.Vx*=.5f;if(++n.A1>=30){n.A1=n.A2=0;n.A0=3;}}
            else if(n.A0==3)
            {
                if(++n.A1==1)
                {
                    NpcTargeting.Face(ref n,ref e,true,confused);direction=n.Direction;vertical=n.DirectionY;
                    dx=e.PlayerX-n.Bounds.CenterX;dy=e.PlayerY-e.PlayerHeight/2-n.Bounds.CenterY;
                    n.A1++;n.A2+=.3f;
                    bool clear;if(!terrain.CanHit(n.Bounds,new MotionRect(e.PlayerX-e.PlayerWidth/2,e.PlayerY-e.PlayerHeight/2,e.PlayerWidth,e.PlayerHeight),out clear,out stop))return false;
                    dy-=vertical>0?0:Math.Abs(dx)*.2f;
                    float length=Math.Max(.001f,(float)Math.Sqrt(dx*dx+dy*dy)),speed=clear?10:6;
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
            else if(n.A0==5){n.Vx=0;if(++n.A1>=30){NpcTargeting.Face(ref n,ref e,true,confused);n.A0=n.A1=0;}if(n.Wet){n.A0=3;n.A1=0;}}
            return true;
        }
        internal static void Trend(ref NpcMotionState n,int elapsed)
        {
            // Recent correction is a bounded displacement, not a permanent
            // added speed/heading extrapolated for the remaining two seconds.
            // Contacts revoke it and retain the collision owner's velocity.
            if(elapsed==1){n.TrendBaseVx=n.Vx;n.TrendBaseVy=n.Vy;}
            if(n.CollideX || n.CollideY){n.ObservedTurn=n.ObservedAccelerationX=n.ObservedAccelerationY=0;n.TrendBaseVx=n.Vx;n.TrendBaseVy=n.Vy;}
            float curve=elapsed<=12?elapsed*(13-elapsed)/12f:0;
            float boost=elapsed<=8?elapsed*(9-elapsed)/8f:0;
            float angle=n.NoGravity?n.ObservedTurn*curve:0,c=(float)Math.Cos(angle),sin=(float)Math.Sin(angle);
            n.Vx=n.TrendBaseVx*c-n.TrendBaseVy*sin+n.ObservedAccelerationX*boost;
            if(n.NoGravity)n.Vy=n.TrendBaseVx*sin+n.TrendBaseVy*c+n.ObservedAccelerationY*boost;
        }
    }
}
