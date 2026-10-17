using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Locked NPC.cs AI117. Rotation feeds charge steering and spit recoil;
    // it is movement state, not a sampled drawing angle. Shot/help RNG has
    // no body feedback and is never executed in this private value world.
    internal static class NpcBloodNautilusMotion
    {
        private const float Pi=(float)Math.PI,TwoPi=Pi*2,Offset=.47123894f;
        internal static bool Known(NpcMotionState n){return n.EffectiveType==618 && n.Style==117;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b);}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e){return !n.TargetCaptured || !n.PlayerNativeDead || n.HasClosestPlayer;}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e)
        {return n.Target==255 || n.PlayerNativeDead || Distance(n.Bounds.CenterX,n.Bounds.CenterY,n.PlayerCenterX,n.PlayerCenterY)>2000 || n.A0==0 && n.A1+1>60 || n.A0==1 && n.A1+1>=270 || (n.A0==2 || n.A0==3) && n.A1+1>=180;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;
            if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(!n.MotorRotationCaptured){stop=PredictionStop.MissingDependency;return false;}
            if(n.L0==0){n.L0=1;n.Alpha=255;if(!e.Multiplayer)n.A0=-1;}
            // Conditional cosmetic translation can round a nonzero offset.
            // Preserve its real uncertainty instead of drawing or rolling RNG.
            if(n.A0!=-1 && (n.NetOffsetX!=0 || n.NetOffsetY!=0)){stop=PredictionStop.RandomDecision;return false;}
            if(n.Target==255){NpcTargeting.Face(ref n,ref e,true,confused);n.A2=n.Direction;}
            if(n.PlayerNativeDead || Distance(n.Bounds.CenterX,n.Bounds.CenterY,n.PlayerCenterX,n.PlayerCenterY)>2000)NpcTargeting.Face(ref n,ref e,true,confused);
            if(n.TargetChoiceUnavailable){stop=PredictionStop.MissingDependency;return false;}
            bool validTarget=!n.TargetCaptured || n.HasPlayer && n.PlayerActive && !n.PlayerNativeDead && !n.PlayerGhost;
            float tx=validTarget && !e.Day && e.BloodMoon?e.PlayerX:0,ty=validTarget && !e.Day && e.BloodMoon?e.PlayerY:0;
            bool reflecting=false;int next=-1;float x,y,angle,ux,uy;
            switch((int)n.A0)
            {
                case -1:
                    n.Vx*=.98f;n.Vy*=.98f;int direction=Math.Sign(tx-n.Bounds.CenterX);
                    if(direction!=0){n.Direction=direction;n.SpriteDirection=-direction;}
                    if(n.L1==0 && n.Alpha<100){n.L1=1;for(int i=0;i<36;i++)Translate(ref n);}
                    if(n.A2>5)
                    {
                        n.Vy=-2.5f;n.Alpha-=10;bool solid;
                        if(terrain==null){stop=PredictionStop.MissingDependency;return false;}
                        if(!terrain.Solid(n.Bounds,out solid,out stop))return false;
                        if(solid){n.Alpha+=15;if(n.Alpha>150)n.Alpha=150;}if(n.Alpha<0)n.Alpha=0;
                    }
                    n.A2++;if(n.A2>=50){n.A0=0;n.A1=n.A2=n.A3=0;}break;
                case 0:
                    x=tx-n.A2*300; y=ty-200;
                    if(Distance(n.Bounds.CenterX,n.Bounds.CenterY,x,y)>50){Unit(x-n.Bounds.CenterX,y-n.Bounds.CenterY,out ux,out uy);ux*=7.5f;uy*=7.5f;Fly(ref n,ux,uy);}
                    n.Direction=n.Bounds.CenterX<tx?1:-1;
                    angle=Aim(n,tx,ty,false);Flip(ref n,ref angle);n.MotorRotation=Towards(n.MotorRotation,angle,.02f);
                    if(++n.A1>60){int pattern=(int)n.A3;next=pattern%7==3?3:pattern%2==0?2:1;}break;
                case 1:
                    n.Direction=n.Bounds.CenterX>=tx?1:-1;angle=Aim(n,tx,ty,true);
                    if(n.A1<90){reflecting=true;Flip(ref n,ref angle);n.Vx*=.95f;n.Vy*=.95f;n.MotorRotation=AngleLerp(n.MotorRotation,angle,.02f);Translate(ref n);}
                    else if(n.A1<270)
                    {
                        n.X+=n.NetOffsetX;n.Y+=n.NetOffsetY;n.MotorRotation=AngleLerp(n.MotorRotation,angle,.05f);
                        if(Distance(n.Bounds.CenterX,n.Bounds.CenterY,tx,ty)>150)
                        {Mouth(n,out x,out y);x*=-16;y*=-16;Unit(tx-n.Bounds.CenterX,ty-n.Bounds.CenterY,out ux,out uy);ux*=1.5f;uy*=1.5f;n.Vx=x+ux;n.Vy=y+uy;}
                        n.X-=n.NetOffsetX;n.Y-=n.NetOffsetY;
                    }
                    if(++n.A1>=270)next=0;break;
                case 2:
                    n.Direction=n.Bounds.CenterX<tx?1:-1;angle=Aim(n,tx,ty,false);Flip(ref n,ref angle);n.MotorRotation=AngleLerp(n.MotorRotation,angle,.2f);
                    if(n.A1<90){n.Vx*=.95f;n.Vy*=.95f;Translate(ref n);}
                    else if(n.A1<180)
                    {
                        n.X+=n.NetOffsetX;n.Y+=n.NetOffsetY;n.Vx*=.9f;n.Vy*=.9f;
                        if((int)((n.A1-90)%30)==0){Mouth(n,out x,out y);x*=-8;y*=-8;n.Vx+=x;n.Vy+=y;}
                        n.X-=n.NetOffsetX;n.Y-=n.NetOffsetY;
                    }
                    if(++n.A1>=180)next=0;break;
                case 3:
                    n.Direction=n.Bounds.CenterX<tx?1:-1;n.SpriteDirection=n.Direction;
                    if(n.A1<180){n.Vx=n.Vy=0;n.MotorRotation=AngleLerp(n.MotorRotation,0,.02f);Translate(ref n);}
                    if(++n.A1>=180)next=0;break;
                default:stop=PredictionStop.UnsupportedMechanism;return false;
            }
            if(next!=-1){n.A0=next;n.A1=n.A2=0;NpcTargeting.Face(ref n,ref e,true,confused);if(next==0)n.A2=n.Direction;else n.A3++;}
            n.ReflectsProjectiles=reflecting;
            if(n.TargetChoiceUnavailable){stop=PredictionStop.MissingDependency;return false;}
            if(!Finite(n.Vx) || !Finite(n.Vy) || !Finite(n.MotorRotation)){stop=PredictionStop.InvalidState;return false;}return true;
        }
        private static float Aim(NpcMotionState n,float tx,float ty,bool from)
        {float ux,uy;Unit(from?n.Bounds.CenterX-tx:tx-n.Bounds.CenterX,from?n.Bounds.CenterY-ty:ty-n.Bounds.CenterY,out ux,out uy);float angle=(float)Math.Atan2(uy,ux)-Offset*n.SpriteDirection;if(n.SpriteDirection==-1)angle+=Pi;return angle;}
        private static void Flip(ref NpcMotionState n,ref float angle){if(n.SpriteDirection!=n.Direction){n.SpriteDirection=n.Direction;n.MotorRotation=-n.MotorRotation;angle=-angle;}}
        private static void Translate(ref NpcMotionState n){n.X+=n.NetOffsetX;n.Y+=n.NetOffsetY;n.X-=n.NetOffsetX;n.Y-=n.NetOffsetY;}
        private static void Mouth(NpcMotionState n,out float x,out float y){float angle=n.MotorRotation+Offset*n.SpriteDirection;if(n.SpriteDirection==-1)angle+=Pi;x=(float)Math.Cos(angle);y=(float)Math.Sin(angle);}
        private static float Length(float x,float y){return (float)Math.Sqrt((double)x*x+(double)y*y);}
        private static float Distance(float ax,float ay,float bx,float by){double x=(double)ax-bx,y=(double)ay-by;return (float)Math.Sqrt(x*x+y*y);}
        private static void Unit(float x,float y,out float dx,out float dy){double inverse=1d/Length(x,y);dx=(float)(x*inverse);dy=(float)(y*inverse);}
        // Original MathHelper.WrapAngle IL: double IEEERemainder followed by
        // float branches/add/subtract. Do not replace with a modulo shortcut.
        private static float Wrap(float angle){angle=(float)Math.IEEERemainder(angle,6.2831854820251465d);if(angle<=-Pi)angle+=TwoPi;else if(angle>Pi)angle-=TwoPi;return angle;}
        private static float Lerp(float from,float to,float amount){return (float)((double)from+((double)to-from)*amount);}
        private static float AngleLerp(float angle,float target,float amount)
        {if(target<angle){float other=target+TwoPi;angle=other-angle>angle-target?Lerp(angle,target,amount):Lerp(angle,other,amount);}else if(target>angle){float other=target-TwoPi;angle=target-angle>angle-other?Lerp(angle,other,amount):Lerp(angle,target,amount);}else return angle;return Wrap(angle);}
        private static float Towards(float angle,float target,float amount)
        {angle=Wrap(angle);target=Wrap(target);if(angle<target){if(target-angle>Pi)angle+=TwoPi;}else if(angle-target>Pi)angle-=TwoPi;angle+=Math.Max(-amount,Math.Min(amount,target-angle));return Wrap(angle);}
        private static void Fly(ref NpcMotionState n,float x,float y)
        {if(n.Vx<x){n.Vx+=.15f;if(n.Vx<0 && x>0)n.Vx+=.15f;}else if(n.Vx>x){n.Vx-=.15f;if(n.Vx>0 && x<0)n.Vx-=.15f;}if(n.Vy<y){n.Vy+=.15f;if(n.Vy<0 && y>0)n.Vy+=.15f;}else if(n.Vy>y){n.Vy-=.15f;if(n.Vy>0 && y<0)n.Vy-=.15f;}}
        private static bool Finite(float value){return !float.IsNaN(value) && !float.IsInfinity(value);}
    }
}
