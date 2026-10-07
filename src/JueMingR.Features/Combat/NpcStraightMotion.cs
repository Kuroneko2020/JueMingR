using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    internal static class NpcStraightMotion
    {
        internal static bool Known(int type){return type==25 || type==30 || type==33 || type==112 || type==516 || type==665 || type==666;}
        internal static bool NeedsPlayer(NpcMotionState n){return n.EffectiveType==516?n.A0!=0 && (!n.TargetCaptured || n.HasPlayer):n.Target==255;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,bool confused,out PredictionStop stop)
        {
            stop=PredictionStop.None;int type=n.EffectiveType;
            if(type==516)
            {
                if(n.Alpha<220)n.Alpha+=40;
                // Birth's randomized direction/speed has not happened yet.
                // An observed initialized projectile-NPC does not reroll it.
                if(n.A0==0){stop=PredictionStop.RandomDecision;return false;}
                // AI516 reads its OLD numbered player before the common 255
                // initialization. Do not fabricate that missing player slot.
                if(n.Target<0 || n.Target>=255 || n.TargetCaptured && !n.HasPlayer){stop=PredictionStop.MissingDependency;return false;}
                float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY;
                if(n.CollideX || n.CollideY || dx*dx+dy*dy<400){n.Active=false;stop=PredictionStop.Despawn;return false;}
            }
            if(n.Target==255)
            {
                NpcTargeting.Face(ref n,ref e,true,confused);float speed=type==25?5:type==112 || type==666?7:6;
                if(e.GoodWorld){if(type==33 && e.SkeletronUp)speed=n.A3==1?8:10;else if(type==25 && e.WallBossUp)speed=14;else if(type==666)speed=10;}
                float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY,length=(float)Math.Sqrt(dx*dx+dy*dy);if(length<=0)length=1;float scale=speed/length;n.Vx=dx*scale;n.Vy=dy*scale;
            }
            if(e.GoodWorld && (type==33 && e.SkeletronUp || type==25 && e.WallBossUp || type==666 && (double)(n.Bounds.CenterY/16)<e.GravityWorldSurface))
            {n.Health.DontTakeDamage=true;n.CanReceive=false;}
            if(type==112 || type==666)
            {
                n.A0=Math.Min(3,n.A0+1);
                // Native's known initialization performs an extra displacement
                // BEFORE the solid termination test and common movement.
                if(n.A0==2){n.X+=n.Vx;n.Y+=n.Vy;}
                bool solid;if(!terrain.Solid(n.Bounds,out solid,out stop))return false;
                if(solid){n.Active=false;stop=PredictionStop.Despawn;return false;}
            }
            n.TimeLeft=Math.Min(n.TimeLeft,100);
            if(type==516)
            {
                float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY,length=(float)Math.Sqrt(dx*dx+dy*dy);
                if(length==0){dx=n.Direction;dy=0;}else{float inv=1/length;dx*=inv;dy*=inv;}
                float speed=(float)Math.Sqrt(n.Vx*n.Vx+n.Vy*n.Vy)+1f/12;
                n.Vx=(n.Vx*14+dx*speed)/15;n.Vy=(n.Vy*14+dy*speed)/15;
                if(n.Vx*n.Vx+n.Vy*n.Vy<36){n.Vx*=1.05f;n.Vy*=1.05f;}
            }
            return true;
        }
    }
}
