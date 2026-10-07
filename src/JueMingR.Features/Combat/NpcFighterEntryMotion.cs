using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Finite AI3 actions which precede the common blocked counter/motor.
    // Reveal has no target vector; dry fluid exit resumes the common action.
    internal static class NpcFighterEntryMotion
    {
        internal static bool Known(int type){return type==466 || type==461 || type==586;}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain)
        {
            if(n.EffectiveType==466)return n.A2>=0;
            if(!n.Wet || terrain==null)return true;
            // A currently blocked wet action uses its own patrol vector.
            // Rolling prefers the available player timeline for later LOS;
            // unavailable future facts only retain a labeled fixed-target
            // observation prefix, not player-independent future visibility.
            Prelude(ref n,ref e,n.ConfusedTicks>0);
            if(n.TargetCaptured && !n.HasPlayer)return true;
            bool clear;PredictionStop stop;
            return !terrain.CanHit(n.Bounds,new MotionRect(e.PlayerX,e.PlayerY,1,1),out clear,out stop) || clear;
        }
        internal static bool Prelude(ref NpcMotionState n,ref PredictionEnvironment e,bool confused)
        {
            int type=n.EffectiveType;
            if(type==466)
            {
                if(n.A2==0){n.Alpha=200;NpcTargeting.Face(ref n,ref e,true,confused);float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY;if(!e.PlayerDead && dx*dx+dy*dy<28900 || n.Vx!=0 || n.Vy<0 || n.Vy>2 || n.JustHit)n.A2=-16;return true;}
                if(n.A2<0){n.Alpha=Math.Max(0,n.Alpha-12);if(++n.A2==0){n.A2=1;n.Vx=n.Direction*2;}return true;}
                n.Alpha=0;return false;
            }
            if(type==586)
            {if(n.Alpha==255){NpcTargeting.Face(ref n,ref e,true,confused);n.SpriteDirection=n.Direction;n.Vy=-6;}n.Alpha=Math.Max(0,n.Alpha-15);}
            if(type==461)Resize(ref n,n.Wet?34:18,n.Wet?24:40);
            n.NoGravity=n.Wet;
            if(n.Wet){n.A3=-.10101f;NpcTargeting.Face(ref n,ref e,true,confused);return true;}
            if(n.A3==-.10101f)
            {
                n.A3=0;float length=(float)Math.Sqrt(n.Vx*n.Vx+n.Vy*n.Vy),speed=Math.Min(type==461?10:15,length*2);
                if(length>0){n.Vx=n.Vx/length*speed;n.Vy=n.Vy/length*speed;}
                else{n.Vx=float.NaN;n.Vy=float.NaN;} // Native zero Normalize is not invented motion.
                if(n.Vx<0)n.Direction=-1;else if(n.Vx>0)n.Direction=1;n.SpriteDirection=n.Direction;
            }
            return false;
        }
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,out bool handled,out PredictionStop stop)
        {
            stop=PredictionStop.None;handled=Prelude(ref n,ref e,confused);int type=n.EffectiveType;
            if(float.IsNaN(n.Vx) || float.IsNaN(n.Vy)){stop=PredictionStop.InvalidState;return false;}
            if(!handled || type==466)return true;
            if(n.CollideX)n.Vx=-n.OldVx;if(n.Vx<0)n.Direction=-1;else if(n.Vx>0)n.Direction=1;
            bool clear;if(!terrain.CanHit(n.Bounds,new MotionRect(e.PlayerX,e.PlayerY,1,1),out clear,out stop))return false;
            float dx,dy,speed,weight;
            if(clear)
            {dx=e.PlayerX-n.Bounds.CenterX;dy=e.PlayerY-n.Bounds.CenterY;speed=type==461?5:Math.Max(5,Math.Min(20,1+Math.Abs(dy)/40));weight=type==461?19:n.Vy>0?29:4;}
            else
            {dx=n.Direction;dy=-1;speed=n.Vy>0?3:n.Vy<0?8:5;weight=speed<5?24:9;}
            float length=(float)Math.Sqrt(dx*dx+dy*dy);if(length==0){stop=PredictionStop.InvalidState;return false;}
            n.Vx=(n.Vx*weight+dx/length*speed)/(weight+1);n.Vy=(n.Vy*weight+dy/length*speed)/(weight+1);return true;
        }
        private static void Resize(ref NpcMotionState n,int width,int height)
        {float x=n.Bounds.CenterX,y=n.Bounds.CenterY;n.Width=width;n.Height=height;n.X=x-width/2;n.Y=y-height/2;}
    }
}
