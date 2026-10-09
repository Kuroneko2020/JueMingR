using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Current native parent phase and relative speed constraints. The parent
    // itself remains a qualified observed motion; attacks, RNG and full boss
    // phase selection are not replayed by this private scalar continuation.
    internal static class NpcParentMotion
    {
        // These phases select the captured closest player before their first
        // vector read. Raising/horizontal only consume it when the actual
        // parent-relative gate crosses; preparing a candidate is not a claim
        // that the current action needs it. Chase/coast phases retain the old
        // numbered target until their own modeled query.
        internal static bool QueriesBeforeVector(NpcMotionState n)
        {return n.Style==36 && (n.A2==0 || n.A2==3) || (n.Style==12 || n.Style==33 || n.Style==34) && (n.A2==1 || n.A2==4 || n.Style!=12 && (n.A2==0 || n.A2==3 || n.A2==99));}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e,int remaining,NpcMotionState parent)
        {
            // Match Step's action-before phase, including far-parent recovery.
            // AI12/33/34 raising and horizontal setup do not read a player
            // until their relative boundary actually enters Aim. Rolling
            // rechecks each action and restarts the whole timeline once when
            // that first real premise appears; a clock alone cannot predict it.
            int phase=(int)n.A2;
            if(n.Style==33 || n.Style==34)
            {
                float dx=parent.Bounds.CenterX-200*n.A0-n.Bounds.CenterX,dy=parent.Y+230-n.Bounds.CenterY;
                float distance=(float)Math.Sqrt(dx*dx+dy*dy);
                if(phase!=99 && distance>800 || phase==99 && distance>=400)return false;
                if(phase==99)phase=0;
            }
            if(n.Style==35)return n.A2==1 || n.A2==0 && n.PositionParameter==0 && n.A3+remaining>1100;
            if(n.Style==36)return n.A2==1 || (n.A2==0 || n.A2==3) && n.PositionParameter==0 && n.A3+remaining>800;
            if(phase==0 || phase==3)return n.Style!=12 && parent.A1!=0;
            if(phase==1)return n.Y<parent.Y-(n.Style==34?280:200);
            if(phase==4)return n.Style==33 || n.Bounds.CenterX<parent.Bounds.CenterX-500 || n.Bounds.CenterX>parent.Bounds.CenterX+500;
            return phase==2 || phase==5;
        }
        internal static bool Step(ref NpcMotionState n,NpcMotionState parent,PredictionEnvironment e,out PredictionStop stop)
        {
            stop=PredictionStop.None;int style=n.Style;float cx=n.X+n.Width/2,px=parent.X+parent.Width/2;
            bool red=style==12 && parent.A3==1;
            if(style==12)n.L3=parent.A3;
            n.SpriteDirection=-(int)n.A0;
            if(style==33 || style==34)
            {
                float dx=px-200*n.A0-n.Bounds.CenterX,dy=parent.Y+230-n.Bounds.CenterY;
                float distance=(float)Math.Sqrt(dx*dx+dy*dy);
                if(n.A2!=99 && distance>800)n.A2=99;else if(n.A2==99 && distance<400)n.A2=0;
                if(n.A2==99)
                {Band(ref n.Vy,n.Y,parent.Y,parent.Y,.1f,.96f,8,8);Band(ref n.Vx,cx,px,px,.5f,.96f,12,12);return true;}
            }
            bool home=style==35?n.A2==0:n.A2==0 || n.A2==3;
            if(home)
            {
                // Native retirement encouragement belongs to the home phase;
                // pursuit/recovery retains its own current lifetime.
                if(parent.A1==3)n.TimeLeft=Math.Min(n.TimeLeft,10);
                if(parent.A1!=0 && !red)
                {
                    if(style==33 || style==34)
                    {
                        NpcTargeting.Retarget(ref n,ref e,false);
                        if(e.PlayerDead)n.Vy=Math.Min(16,n.Vy+.1f);
                        else if(style==34){if(Math.Abs(n.Vx)+Math.Abs(n.Vy)<2)Aim(ref n,e.PlayerX,e.PlayerY,12);else{n.Vx*=.97f;n.Vy*=.97f;}}
                        else Chase(ref n,e.PlayerX,e.PlayerY,7,.05f,.05f,.97f);
                        if((style==33 || !e.PlayerDead) && ++n.A3>=600)n.A2=n.A3=0;
                    }
                    else
                    {
                        Band(ref n.Vy,n.Y,parent.Y-100,parent.Y-100,.07f,.96f,6,6);
                        Band(ref n.Vx,cx,px-120*n.A0,px-120*n.A0,.1f,.96f,8,8);
                        if(style==35)n.L0+=2;if(style==36)n.L0+=3;
                    }
                }
                else
                {
                    n.A3+=style==12?1+(red?1:0)+(e.Expert?.5f:0):1;
                    int period=style==35?1100:style==36?800:style==34?600:300;
                    if(n.A3>=period){n.A2++;n.A3=0;if(style==35)n.L0=0;}
                    if(style==12)
                    {
                        if(e.Expert)HomeHand(ref n,parent,cx,px);
                        HomeHand(ref n,parent,cx,px);
                    }
                    else if(style==33)
                    {Band(ref n.Vy,n.Y,parent.Y+260,parent.Y+320,.04f,.96f,3,3);Band(ref n.Vx,cx,px-250,px,.3f,.96f,12,12);}
                    else if(style==34)
                    {Band(ref n.Vy,n.Y,parent.Y+230,parent.Y+300,.1f,.96f,3,3);if(cx>px+250){if(n.Vx>0)n.Vx*=.94f;n.Vx=Math.Min(9,n.Vx-.3f);}if(cx<px){if(n.Vx<0)n.Vx*=.94f;n.Vx=Math.Max(-8,n.Vx+.2f);}}
                    else if(style==35)
                    {Band(ref n.Vy,n.Y,parent.Y-150,parent.Y-150,.04f,.96f,3,3);Band(ref n.Vx,cx,px+160,px+200,.2f,.96f,8,8);}
                    else
                    {Band(ref n.Vy,n.Y,parent.Y-100,parent.Y-100,.1f,.96f,3,3);Band(ref n.Vx,cx,px-180*n.A0,px-180*n.A0,.14f,.96f,8,8);}
                }
                if(style==36)NpcTargeting.Retarget(ref n,ref e,false);
                return true;
            }
            if(style==35 || style==36)
            {
                if(n.A2!=1)return true; // Native ignores other independent phases.
                if(++n.A3>=(style==35?300:200)){n.A2=n.A3=n.L0=0;}
                if(style==35)Chase(ref n,px,e.PlayerY-80,6,.04f,.08f,.9f);
                else Chase(ref n,e.PlayerX-350,e.PlayerY-20,7,.1f,.03f,.9f);
                NpcTargeting.Retarget(ref n,ref e,false);return true;
            }
            if(n.A2==1)
            {
                if(style==34){if(n.Vy>0)n.Vy*=.9f;n.Vx=(n.Vx*5+parent.Vx)/6+.5f;n.Vy=Math.Max(-9,n.Vy-.5f);}
                else{n.Vx*=.95f;n.Vy=Math.Max(style==12?(red?-15:e.Expert?-13:-8):-8,n.Vy-(style==12?(red?.19f:e.Expert?.16f:.1f):.1f));}
                if(n.Y<parent.Y-(style==34?280:200))
                {NpcTargeting.Retarget(ref n,ref e,false);n.A2=2;Aim(ref n,e.PlayerX,e.PlayerY,style==34?20:style==33?22:red?24:e.Expert?21:18);}
                return true;
            }
            if(n.A2==2)
            {
                if(n.Y>e.PlayerY-e.PlayerHeight/2 || n.Vy<0 || style==12 && Past(n,e))
                {if(style==34 && n.A3<4){n.A2=1;n.A3++;}else{n.A2=3;if(style==34)n.A3=0;}}
                return true;
            }
            if(n.A2==4)
            {
                if(style==33)
                {NpcTargeting.Retarget(ref n,ref e,false);Chase(ref n,e.PlayerX,e.PlayerY,7,.05f,.05f,.97f);if(++n.A3>=600)n.A2=n.A3=0;}
                else
                {
                    if(style==34){n.Vy=(n.Vy*5+parent.Vy)/6;n.Vx=Math.Min(12,n.Vx+.5f);}
                    else{n.Vy*=.95f;n.Vx=Math.Max(red?-15:e.Expert?-12:-8,Math.Min(red?15:e.Expert?12:8,n.Vx-n.A0*(red?.2f:e.Expert?.17f:.1f)));}
                    if(cx<px-500 || cx>px+500){NpcTargeting.Retarget(ref n,ref e,false);n.A2=5;Aim(ref n,e.PlayerX,e.PlayerY,style==34?17:red?25:e.Expert?22:17);}
                }
                return true;
            }
            if(n.A2==5)
            {
                if(style==34 && cx<e.PlayerX-100){if(n.A3>=4)n.A2=n.A3=0;else{n.A2=4;n.A3++;}}
                else if(style!=34 && (n.Vx>0 && cx>e.PlayerX || n.Vx<0 && cx<e.PlayerX || style==12 && Past(n,e)))n.A2=0;
            }
            return true;
        }
        private static void HomeHand(ref NpcMotionState n,NpcMotionState parent,float cx,float px)
        {Band(ref n.Vy,n.Y,parent.Y+230,parent.Y+230,.04f,.96f,3,3);Band(ref n.Vx,cx,px-200*n.A0,px-200*n.A0,.07f,.96f,8,8);}
        private static void Band(ref float v,float position,float low,float high,float acceleration,float damping,float positiveLimit,float negativeLimit)
        {if(position>high){if(v>0)v*=damping;v=Math.Min(positiveLimit,v-acceleration);}if(position<low){if(v<0)v*=damping;v=Math.Max(-negativeLimit,v+acceleration);}}
        private static void Aim(ref NpcMotionState n,float x,float y,float speed)
        {float dx=x-n.Bounds.CenterX,dy=y-n.Bounds.CenterY,distance=Math.Max(.01f,(float)Math.Sqrt(dx*dx+dy*dy));n.Vx=dx*speed/distance;n.Vy=dy*speed/distance;}
        private static void Chase(ref NpcMotionState n,float x,float y,float speed,float ax,float ay,float damping)
        {float dx=x-n.Bounds.CenterX,dy=y-n.Bounds.CenterY,distance=(float)Math.Sqrt(dx*dx+dy*dy);if(distance==0)return;Accelerate(ref n.Vx,dx*speed/distance,ax,damping);Accelerate(ref n.Vy,dy*speed/distance,ay,damping);}
        private static void Accelerate(ref float value,float target,float step,float damping)
        {if(value>target){if(value>0)value*=damping;value-=step;}if(value<target){if(value<0)value*=damping;value+=step;}}
        private static bool Past(NpcMotionState n,PredictionEnvironment e)
        {float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY;return n.Vx*dx+n.Vy*dy<=0 || Math.Sqrt(Math.Pow(e.PlayerX-e.PlayerWidth/2-n.Bounds.CenterX,2)+Math.Pow(e.PlayerY-e.PlayerHeight/2-n.Bounds.CenterY,2))>2000;}
    }
}
