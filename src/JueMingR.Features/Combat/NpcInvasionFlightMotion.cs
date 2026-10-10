using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked AI108 shares targeting, but its motor reads the precise aimed
    // body while FaceTarget reads the separately truncated target rectangle.
    // All same-type peers advance in native slot order in the private group.
    internal static class NpcInvasionFlightMotion
    {
        internal static bool Known(NpcMotionState n)
        {int type=n.EffectiveType;return n.Style==108 && (type==558 || type==559 || type==560 || type==574 || type==575);}
        internal static bool NeedsPlayer(NpcMotionState n){return NpcInvasionTargeting.NeedsPlayer(n);}
        internal static bool Retargets(NpcMotionState n){return true;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return a.EffectiveType==b.EffectiveType && a.Style==b.Style;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;
            if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(!n.CrowdCaptured || !n.CrowdComplete || group==null || count<1 || count>group.Length){stop=PredictionStop.MissingDependency;return false;}
            NpcInvasionTarget target;
            if(!NpcInvasionTargeting.TryTarget(ref n,e,terrain,elapsed,group,count,true,confused,out target,out stop))return false;
            int type=n.EffectiveType;bool kobold=type==574 || type==575;
            if(n.L0==0)n.Alpha=255;
            if(n.L0<60){n.L0++;n.Alpha=Math.Max(0,n.Alpha-5);}
            for(int i=0;i<count;i++)
            {
                var peer=group[i];if(!peer.Active || peer.Identity.Equals(n.Identity) || peer.EffectiveType!=type || Math.Abs(n.X-peer.X)+Math.Abs(n.Y-peer.Y)>=n.Width)continue;
                n.Vx+=n.X<peer.X?-.05f:.05f;n.Vy+=n.Y<peer.Y?-.05f:.05f;
            }
            if(n.Vx!=0)n.SpriteDirection=-Math.Sign(n.Vx);
            float x=target.Area.CenterX-n.Bounds.CenterX,y=target.Area.CenterY-n.Bounds.CenterY;
            if(n.A0==0)
            {
                float speed=kobold?4:3,dx,dy,hx,hy;
                if(!Unit(x,y,out dx,out dy) || !Unit(x,y-400,out hx,out hy)){stop=PredictionStop.InvalidState;return false;}
                dx*=speed;dy*=speed;hx*=speed;hy*=speed;
                bool clear;if(!terrain.CanHit(new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,1,1),new MotionRect(target.Area.CenterX,target.Area.CenterY,1,1),out clear,out stop))return false;
                if(n.A3>=120)clear=true;
                float angle=(float)Math.Atan2(dy,dx);clear=clear && angle>(float)Math.PI/8 && angle<(float)Math.PI-(float)Math.PI/8;
                if(Length(x,y)>500 || !clear)
                {
                    n.Vx=(n.Vx*89+hx)/90;n.Vy=(n.Vy*89+hy)/90;
                    if(target.Area.CenterY<n.Bounds.CenterY)n.Vy=Math.Max(-10,n.Vy-.2f);
                    if(!clear)n.A3++;else n.A3=0;
                }
                else{n.A0=1;n.A2=dx;n.A3=dy;}
            }
            else if(n.A0==1)
            {
                n.Vx*=.95f;n.Vy*=.95f;if(kobold)n.Vy-=.1f;n.A1++;
                if(n.A1>=(kobold?30:20))
                {
                    // Wyvern calls Next(0,1): the consumed RNG always adds
                    // zero. Kobold's +/-3 spread changes its actual velocity.
                    if(kobold){stop=PredictionStop.RandomDestination;return false;}
                    float dx,dy;if(!Unit(n.A2,n.A3,out dx,out dy)){stop=PredictionStop.InvalidState;return false;}
                    n.A0=2;n.A1=0;n.Vx=dx*8;n.Vy=dy*8;
                }
            }
            else if(n.A0==2)
            {
                n.A1++;float speed=Length(n.Vx,n.Vy);
                bool passed=!kobold && Length(x,y)>150 && n.Bounds.CenterY>target.Area.CenterY;
                if(n.A1>=30 && passed || speed<(kobold?0:6))
                {n.A0=4;n.A1=45;n.A2=n.A3=0;n.Vx/=2;n.Vy/=2;}
                else
                {
                    float dx,dy;if(!Unit(x,y,out dx,out dy)){dx=n.Direction;dy=0;}
                    float inertia=kobold?10:60,accelerated=speed+(kobold?.5f:3);
                    n.Vx=(n.Vx*(inertia-1)+dx*accelerated)/inertia;n.Vy=(n.Vy*(inertia-1)+dy*accelerated)/inertia;
                }
                if(kobold)
                {bool solid;if(!terrain.Solid(n.Bounds,out solid,out stop))return false;if(solid){n.A0=3;n.A1=n.A2=n.A3=0;}}
            }
            else if(n.A0==4)
            {n.A1-=3;if(n.A1<=0){n.A0=0;n.A1=0;}n.Vx*=.95f;n.Vy*=.95f;}
            else if(n.A0!=3){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(kobold && n.A0!=3 && Length(x,y)<64){n.A0=3;n.A1=n.A2=n.A3=0;}
            if(n.A0==3)
            {
                float cx=n.Bounds.CenterX,cy=n.Bounds.CenterY;n.Width=n.Height=192;n.X=cx-96;n.Y=cy-96;n.Vx=n.Vy=0;n.Alpha=255;
                n.A1++;if(n.A1>=3){n.Life=0;n.Active=false;stop=PredictionStop.Despawn;return false;}
            }
            return true;
        }
        private static float Length(float x,float y){return (float)Math.Sqrt(x*x+y*y);}
        private static bool Unit(float x,float y,out float dx,out float dy)
        {
            // Preserve the locked x86 XNA Normalize arithmetic: extended
            // squared sum, float sqrt result and reciprocal until each float
            // component assignment. Separate float intermediates differ.
            double squared=(double)x*x+(double)y*y;
            float root=(float)Math.Sqrt(squared);double inverse=1d/root;
            dx=(float)(x*inverse);dy=(float)(y*inverse);
            return !float.IsNaN(dx) && !float.IsNaN(dy);
        }
    }
}
