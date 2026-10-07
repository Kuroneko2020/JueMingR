using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // AI_005's finite common motor. Shot/cosmetic RNG is not motion replay.
    internal static class NpcFlyingMotion
    {
        internal static bool Bee(int type){return type==210 || type==211;}
        internal static bool Known(int type){return type==5 || type==6 || type==23 || type==42 || type==94 || type==139 || type==173 || type==176 || type==205 || Bee(type) || type==252 || type==619 || type>=231 && type<=235;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,bool confused=false)
        {
            stop=PredictionStop.None;int type=n.EffectiveType;bool bee=Bee(type),eater=type==6 || type==173,hornet=type==42 || type>=231 && type<=235;
            n.NoGravity=true;
            if(!bee)NpcTargeting.Face(ref n,ref e,true,confused);
            if(bee && n.TargetCaptured)
            {
                if(!n.BeeTargetValid){stop=PredictionStop.MissingDependency;return false;}
                if(!SelectBee(ref n,e,terrain,elapsed,out stop))return false;
                // Permission follows THIS action's winner, including a first
                // action switch caused by the prepared player's movement.
                if(n.BeeFoundTarget && (n.BeeFaceForced || !e.PlayerDead && !(n.TargetNoAggro && n.Direction!=0) && !(e.PlayerIdleWithNegativeAggro && Distance(n.Bounds,new MotionRect(e.PlayerX-e.PlayerWidth*.5f,e.PlayerY-e.PlayerHeight*.5f,e.PlayerWidth,e.PlayerHeight))>800+(e.PlayerWidth+e.PlayerHeight+n.Width+n.Height)/4f && n.BeeOldTarget>=0 && n.BeeOldTarget<255)))
                {
                    var facing=n.TrackingKind==3?NpcTargeting.Area(n,e,Math.Min(Math.Max(elapsed-1,0),12)):n.BeeFaceForced?Pet(n,elapsed):new MotionRect(e.PlayerX-e.PlayerWidth*.5f,e.PlayerY-e.PlayerHeight*.5f,e.PlayerWidth,e.PlayerHeight);
                    n.Direction=(int)facing.X+(int)facing.Width/2<n.Bounds.CenterX?-1:1;n.DirectionY=(int)facing.Y+(int)facing.Height/2<n.Bounds.CenterY?-1:1;
                }
                n.BeeOldTarget=n.Target;
            }
            MotionRect target;
            if(bee && n.TrackingKind==3)
            {
                // One actual required NPC, twelve observed-motion updates,
                // then hold. No future whole-field selection or background AI.
                int span=Math.Min(Math.Max(0,elapsed-1),12);
                target=new MotionRect(n.TrackingArea.X+n.TrackingVx*span,n.TrackingArea.Y+n.TrackingVy*span,n.TrackingArea.Width,n.TrackingArea.Height);
            }
            else target=new MotionRect(e.PlayerX-e.PlayerWidth*.5f,e.PlayerY-e.PlayerHeight*.5f,e.PlayerWidth,e.PlayerHeight);
            float speed=6,acc=.05f;
            if(eater){speed=e.Remix?5:4;acc=e.Remix?.06f:type==6 && e.Expert?.035f:.02f;}
            else if(type==94){speed=4.2f;acc=.022f;}
            else if(type==619){speed=6;acc=.1f;if(e.Day){n.Vy-=.3f;n.TimeLeft=Math.Min(n.TimeLeft,60);}if(n.Alpha==255)n.Vy=-6;n.Alpha=Math.Max(0,n.Alpha-15);}
            else if(type==252)
            {bool clear;if(!terrain.CanHit(n.Bounds,target,out clear,out stop))return false;speed=clear?6:2;acc=clear?.1f:.01f;}
            else if(hornet){speed=(type==231?3:3.5f)*(2-n.Scale);acc=(type==231?.017f:.021f)*(2-n.Scale);if(n.Y/16<e.WorldSurface && (target.Y-n.Y>300 && n.Vy<0 || target.Y-n.Y<80 && n.Vy>0))n.Vy*=.97f;}
            else if(type==205){speed=3.25f;acc=.018f;}
            else if(type==176){speed=4;acc=.017f;}
            else if(type==23){speed=1;acc=.03f;}
            else if(type==5){speed=5;acc=.03f;}
            else if(bee)
            {
                n.A1++;float birth=(n.A1-60)/60;
                // The negative newborn coefficient is native behavior. Only
                // the upper side is clamped, after the early speed clipping.
                if(birth>1)birth=1;else{n.Vx=Math.Max(-6,Math.Min(6,n.Vx));n.Vy=Math.Max(-6,Math.Min(6,n.Vy));}
                speed=5;acc=.1f*birth;
            }
            float dx=(int)((target.X+(int)target.Width/2)/8)*8-(int)(n.Bounds.CenterX/8)*8,dy=(int)((target.Y+(int)target.Height/2)/8)*8-(int)(n.Bounds.CenterY/8)*8;
            float distance=(float)Math.Sqrt(dx*dx+dy*dy);
            if(distance==0){dx=n.Vx;dy=n.Vy;}else{dx*=speed/distance;dy*=speed/distance;}
            bool periodic=eater || type==139 || type==205 || type==94 || type==619 || type==176 || hornet || bee;
            bool always=type==94 || type==619 || type==176 || hornet || bee;
            if(periodic && (distance>100 || always)){n.A0++;n.Vy+=n.A0>0?.023f:-.023f;n.Vx+=n.A0<-100 || n.A0>100?.023f:-.023f;if(n.A0>200)n.A0=-200;}
            if((eater || type==94 || type==619) && distance<150){n.Vx+=dx*.007f;n.Vy+=dy*.007f;}
            if(!bee && e.PlayerDead){dx=n.Direction*speed/2;dy=-speed/2;}else if(type==619 && n.Bounds.CenterY>target.CenterY-200)n.Vy-=.3f;
            if(type==139 && n.A3!=0 && e.MechQueenUp){stop=PredictionStop.MissingDependency;return false;}
            if(type==139 && n.A3!=0)n.A3=0;
            bool reverse=type!=173 && type!=6 && type!=42 && !hornet && type!=94 && type!=139 && type!=619;
            Axis(ref n.Vx,dx,acc,reverse);Axis(ref n.Vy,dy,acc,reverse);
            bool bounce=eater || type==23 || type==42 || type==94 || type==139 || type==176 || type==205 || bee || type==619 || hornet;
            if(bounce)
            {float factor=eater?.4f:.7f;if(n.CollideX){n.Vx=-n.OldVx*factor;if(n.Direction==-1 && n.Vx>0 && n.Vx<2)n.Vx=2;if(n.Direction==1 && n.Vx<0 && n.Vx>-2)n.Vx=-2;}if(n.CollideY){n.Vy=-n.OldVy*factor;if(n.Vy>0 && n.Vy<1.5f)n.Vy=2;if(n.Vy<0 && n.Vy>-1.5f)n.Vy=-2;}}
            if(n.Wet && (eater || type==94 || type==619 || type==205 || type==176 || hornet))
            {if(n.Vy>0)n.Vy*=.95f;bool small=eater || type==94 || type==619;n.Vy=Math.Max(small?-2:-4,n.Vy-(small?.3f:.5f));if(!small)NpcTargeting.Retarget(ref n,ref e);}
            if(type==139 && distance>600){if(n.Vx*dx>0){if(Math.Abs(n.Vx)<(e.MechQueenUp?5:12))n.Vx*=1.05f;}else n.Vx*=.9f;}
            return true;
        }
        private static void Axis(ref float value,float desired,float amount,bool reverse)
        {if(value<desired){value+=amount;if(reverse && value<0 && desired>0)value+=amount;}else if(value>desired){value-=amount;if(reverse && value>0 && desired<0)value-=amount;}}
        private static MotionRect Pet(NpcMotionState n,int elapsed)
        {int span=Math.Min(Math.Max(0,elapsed-1),12);return new MotionRect(n.BeeFacingArea.X+n.BeeFacingVx*span,n.BeeFacingArea.Y+n.BeeFacingVy*span,n.BeeFacingArea.Width,n.BeeFacingArea.Height);}
        private static float Distance(MotionRect a,MotionRect b)
        {float dx=a.CenterX-b.CenterX,dy=a.CenterY-b.CenterY;return (float)Math.Sqrt(dx*dx+dy*dy);}
        private static bool SelectBee(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(!n.BeeFoundTarget){n.Target=n.BeeTarget;return true;}
            var owner=new MotionRect(e.PlayerX-e.PlayerWidth*.5f,e.PlayerY-e.PlayerHeight*.5f,e.PlayerWidth,e.PlayerHeight);
            float tank=n.HasClosestPlayer?Distance(n.Bounds,owner)-n.BeeTankAggro+(n.ClosestPlayerNoAggro && n.Direction==0?1000:0):float.MaxValue;
            bool pet=false;
            if(n.BeeTankPet && !n.ClosestPlayerNoAggro)
            {
                var facing=Pet(n,elapsed);float score=Distance(n.Bounds,facing)-200;
                if(score<tank && score<200){bool clear;if(!terrain.CanHit(new MotionRect(n.Bounds.CenterX,n.Bounds.CenterY,0,0),new MotionRect(facing.CenterX,facing.CenterY,0,0),out clear,out stop))return false;if(clear){tank=score;pet=true;}}
            }
            float npc=n.BeeHasNpcCandidate?Distance(n.Bounds,NpcTargeting.Area(new NpcMotionState{TrackingKind=3,TrackingArea=n.TrackingArea,TrackingVx=n.TrackingVx,TrackingVy=n.TrackingVy},e,Math.Min(Math.Max(0,elapsed-1),12))):float.MaxValue;
            if(npc<tank){n.Target=n.TrackingIdentity.Slot+300;n.TrackingKind=3;n.BeeFaceForced=true;}
            else{n.Target=n.ClosestPlayerIndex;n.TrackingKind=1;n.BeeFaceForced=pet;}
            return true;
        }
    }
}
