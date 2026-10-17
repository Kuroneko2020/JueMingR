using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // AI110 has its own unlimited NPCUtils target search. Emitting attacks or
    // summoning Wyverns consumes native RNG without feeding Betsy's motor;
    // the cycle-five optional circle selection is the one motor RNG boundary.
    internal static class NpcBetsyMotion
    {
        internal static bool Known(NpcMotionState n){return n.Style==110 && n.EffectiveType==551;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b);}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e){return NpcInvasionTargeting.NeedsPlayer(n);}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e){return true;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;NpcInvasionTarget target;
            if(!NpcInvasionTargeting.TryTarget(ref n,e,terrain,elapsed,group,count,false,confused,out target,out stop))return false;
            float px=target.Area.CenterX,py=target.Area.CenterY,animation=1;
            if(n.A0==0){if(++n.A1>=10){n.A1=0;n.A0=1;n.A2=0;}}
            else if(n.A0==1)
            {
                if(n.A2==0)n.A2=n.Bounds.CenterX<px?1:-1;
                float tx,ty;RawDirection(n,px-n.A2*300,py-200,out tx,out ty);
                Fly(ref n,tx*7.5f,ty*7.5f,.45f);n.Direction=n.SpriteDirection=n.Bounds.CenterX<px?1:-1;
                if(++n.A1>=30)
                {
                    // Both client and server execute this actual random call.
                    // Stop before choosing a different future attack motor.
                    if(n.A3==5){stop=PredictionStop.RandomDecision;return false;}
                    int phase=1;switch((int)n.A3){case 0:case 1:case 3:phase=2;break;case 2:case 6:phase=3;break;case 4:phase=4;break;case 5:phase=5;break;case 7:phase=6;break;}
                    n.A0=phase;n.A1=n.A2=0;if(++n.A3>=8)n.A3=0;
                    if(phase==2 || phase==5)
                    {if(!Direction(n,px,py,out tx,out ty,out stop))return false;n.SpriteDirection=tx>0?1:-1;n.Vx=tx*(phase==2?23:32);n.Vy=ty*(phase==2?23:32);}
                    else if(phase==3){n.SpriteDirection=px>n.Bounds.CenterX?1:-1;n.Vx=n.SpriteDirection*-2;n.Vy=0;}
                }
            }
            else if(n.A0==2){if(++n.A1>=30){n.A0=1;n.A1=n.A2=0;}}
            else if(n.A0==3)
            {
                n.A1++;int sign=n.Bounds.CenterX<px?1:-1;n.A2=sign;
                if(n.A1<40)
                {
                    float x=px-sign*600,y=py-250,tx,ty;
                    if(Distance(n,x,y)<12){n.X=x-n.Width*.5f;n.Y=y-n.Height*.5f;}
                    else{if(!Direction(n,x,y,out tx,out ty,out stop))return false;n.X+=tx*12;n.Y+=ty*12;}
                    if(Distance(n,x,y)<16)n.A1=39;animation=1.5f;
                }
                if(n.A1==40){n.Direction=n.SpriteDirection=px>n.Bounds.CenterX?1:-1;n.Vx=n.Direction*10;n.Vy=0;}
                if(n.A1>=40){animation=1.5f;if(Math.Abs(px-n.Bounds.CenterX)>550 && Math.Abs(n.Vx)<20)n.Vx+=Math.Sign(n.Vx)*.5f;}
                if(n.A1>=120){n.A0=1;n.A1=n.A2=0;}
            }
            else if(n.A0==4)
            {
                int sign=n.Bounds.CenterX<px?1:-1;n.A2=sign;
                if(n.A1<60)
                {
                    float x=px-sign*1500,y=py-350,tx,ty;if(!Direction(n,x,y,out tx,out ty,out stop))return false;
                    n.Vx=Lerp(n.Vx,tx*13,1f/30);n.Vy=Lerp(n.Vy,ty*13,1f/30);n.Direction=n.SpriteDirection=n.Bounds.CenterX<px?1:-1;
                    if(Distance(n,x,y)<16)n.A1=59;animation=1.5f;
                }
                else if(n.A1==60)
                {
                    float tx,ty;RawDirection(n,px,py,out tx,out ty);ty*=.25f;
                    if(!Unit(tx,ty,out tx,out ty)){tx=n.Direction;ty=0;}
                    n.SpriteDirection=tx>0?1:-1;n.Vx=tx*12;n.Vy=ty*12;
                }
                else
                {
                    // Native's second DirectionTo observes the position after
                    // its X-only translation, not the old Center.
                    float tx,ty;if(!Direction(n,px,py,out tx,out ty,out stop))return false;n.X+=tx*7;
                    if(!Direction(n,px,py-400,out tx,out ty,out stop))return false;n.Y+=ty*6;
                    if(n.A1<=120)animation=1.5f;
                }
                if(n.A1>120)n.Vy-=.1f;if(++n.A1>=180){n.A0=1;n.A1=n.A2=0;}
            }
            else if(n.A0==5)
            {
                float angular=(float)Math.PI*2*(1f/60);double angle=-angular*n.Direction;float cos=(float)Math.Cos(angle),sin=(float)Math.Sin(angle);
                float vx=(float)((double)n.Vx*cos-(double)n.Vy*sin),vy=(float)((double)n.Vx*sin+(double)n.Vy*cos);n.Vx=vx;n.Vy=vy;n.Y-=.1f;
                float tx,ty;if(!Direction(n,px,py,out tx,out ty,out stop))return false;n.X+=tx*10;n.Y+=ty*10;animation*=.7f;
                if(++n.A1>=60){n.A0=1;n.A1=n.A2=0;n.Vx/=2;n.Vy/=2;}
            }
            else if(n.A0==6)
            {
                if(n.A1==0)
                {
                    float tx,ty;RawDirection(n,px,py-200,out tx,out ty);Fly(ref n,(tx*7.5f)*2,(ty*7.5f)*2,.45f*2);n.Direction=n.SpriteDirection=n.Bounds.CenterX<px?1:-1;n.A2++;
                    if(Distance(n,px,py)<350 || n.A2>=180)n.A1=1;
                }
                else
                {
                    float damping=n.A1<20?.95f:.98f;n.Vx*=damping;n.Vy*=damping;if(n.A1==20){if(n.Vy>0)n.Vy/=3;n.Vy-=3;}animation*=.85f;n.A1++;
                }
                if(n.A1>=90){n.A0=1;n.A1=n.A2=0;}
            }
            n.L0+=animation;if(n.L0>=36)n.L0=0;if(++n.L1>=60)n.L1=0;
            // Native rotation and tracked sound IDs only position attacks/art;
            // none is read by a later self motor, including the circle's turn.
            return true;
        }
        private static bool Direction(NpcMotionState n,float x,float y,out float dx,out float dy,out PredictionStop stop)
        {stop=PredictionStop.None;if(Unit(x-n.Bounds.CenterX,y-n.Bounds.CenterY,out dx,out dy))return true;stop=PredictionStop.InvalidState;return false;}
        private static void RawDirection(NpcMotionState n,float x,float y,out float dx,out float dy)
        {
            // DirectionTo itself is not SafeNormalize. A coincident hover
            // destination yields NaNs that native Fly merely compares against;
            // those comparisons do not replace the observed finite velocity.
            if(!Unit(x-n.Bounds.CenterX,y-n.Bounds.CenterY,out dx,out dy))dx=dy=float.NaN;
        }
        private static bool Unit(float x,float y,out float dx,out float dy)
        {
            if(x==0 && y==0 || float.IsNaN(x) || float.IsNaN(y)){dx=dy=0;return false;}
            // Original XNA optimized Normalize keeps the square sum and
            // inverse in extended precision until each Vector2 field store.
            double squared=(double)x*x+(double)y*y;float root=(float)Math.Sqrt(squared);double inverse=1d/root;dx=(float)(x*inverse);dy=(float)(y*inverse);return true;
        }
        private static float Distance(NpcMotionState n,float x,float y)
        {double dx=(double)x-n.Bounds.CenterX,dy=(double)y-n.Bounds.CenterY;return (float)Math.Sqrt(dx*dx+dy*dy);}
        private static float Lerp(float from,float to,float amount){return (float)((double)from+((double)to-from)*amount);}
        private static void Fly(ref NpcMotionState n,float x,float y,float speed)
        {
            if(n.Vx<x){n.Vx+=speed;if(n.Vx<0 && x>0)n.Vx+=speed;}else if(n.Vx>x){n.Vx-=speed;if(n.Vx>0 && x<0)n.Vx-=speed;}
            if(n.Vy<y){n.Vy+=speed;if(n.Vy<0 && y>0)n.Vy+=speed;}else if(n.Vy>y){n.Vy-=speed;if(n.Vy>0 && y<0)n.Vy-=speed;}
        }
    }
}
