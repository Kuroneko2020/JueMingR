using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // AI11 counts every active hand in native slot order, independently of
    // this head's own pair. Skull/Caster RNG never feeds this head's motor.
    internal static class NpcSkeletonBossMotion
    {
        internal static bool Known(NpcMotionState n){return n.Style==11 && (n.EffectiveType==35 || n.EffectiveType==68);}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && a.EffectiveType==b.EffectiveType && b.Style==11;}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e){return n.A1!=3 && (!n.TargetCaptured || !n.PlayerNativeDead || n.HasClosestPlayer);}
        internal static bool Retargets(NpcMotionState n,PredictionEnvironment e)
        {return n.A0==0 && !e.Multiplayer || n.TargetCaptured && n.PlayerNativeDead || Far(n,e) || n.A1==0 && n.A2+(n.A3==1?1.5f:1)>=800;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;n.ReflectsProjectiles=false;
            if(!Known(n)){stop=PredictionStop.UnsupportedMechanism;return false;}
            if(n.A0==0 && !e.Multiplayer)
            {
                // The actual first server action allocates two native hands.
                // Their not-yet-observed identities are not fictitious rows.
                // Natural next-sample capture resumes after that real birth.
                if(n.EffectiveType==35){stop=PredictionStop.PhaseBoundary;return false;}
                Query(ref n,ref e,confused);n.A0=1;
            }
            if((n.EffectiveType==68 || e.Multiplayer) && n.L0==0)n.L0=1;
            bool dead=n.TargetCaptured?n.PlayerNativeDead:e.PlayerDead;
            if(dead || Far(n,e)){Query(ref n,ref e,confused);dead=n.TargetCaptured?n.PlayerNativeDead:e.PlayerDead;if(dead || Far(n,e))n.A1=3;}
            if((n.EffectiveType==68 || e.Day) && n.A1!=3 && n.A1!=2)n.A1=2;
            if(n.A1!=3 && n.TargetCaptured && (!n.HasPlayer || n.PlayerGhost || !n.PlayerActive)){stop=PredictionStop.MissingDependency;return false;}
            bool red=n.EffectiveType==35 && n.A3==1;int hands=0;n.Health.Defense=n.Health.DefaultDefense;
            if(e.Expert && n.EffectiveType==35)
            {
                if(!n.SkeletonRelationsCaptured || !n.SkeletonRelationsComplete || group==null || count<1 || count>group.Length){stop=PredictionStop.MissingDependency;return false;}
                for(int i=0;i<count;i++)if(group[i].Active && group[i].EffectiveType==36)hands++;
                n.Health.Defense+=hands*25;
            }
            if(n.A1==0)
            {
                // Native caches player2 BEFORE a same-action phase-switch
                // TargetClosest. The hover still reads that old player's pose.
                var motor=e;n.A2+=red?1.5f:1;
                if(n.A2>=800){n.A2=0;n.A1=1;Query(ref n,ref e,confused);}
                float ay=e.Expert?.03f:.02f,cy=e.Expert?4:2,ax=e.Expert?.07f:.05f,cx=e.Expert?9.5f:8;
                if(red){ay*=1.35f;cy*=1.35f;ax*=1.35f;cx*=1.35f;}
                else if(e.GoodWorld){ay+=.01f;cy+=1;ax+=.05f;cx+=2;}
                float top=motor.PlayerY-motor.PlayerHeight*.5f;
                if(n.Y>top-250){if(n.Vy>0)n.Vy*=.98f;n.Vy-=ay;if(n.Vy>cy)n.Vy=cy;}
                else if(n.Y<top-250){if(n.Vy<0)n.Vy*=.98f;n.Vy+=ay;if(n.Vy<-cy)n.Vy=-cy;}
                if(n.Bounds.CenterX>motor.PlayerX){if(n.Vx>0)n.Vx*=.98f;n.Vx-=ax;if(n.Vx>cx)n.Vx=cx;}
                if(n.Bounds.CenterX<motor.PlayerX){if(n.Vx<0)n.Vx*=.98f;n.Vx+=ax;if(n.Vx<-cx)n.Vx=-cx;}
            }
            else if(n.A1==1)
            {
                if((e.GoodWorld || red) && hands>0)n.ReflectsProjectiles=true;
                n.Health.Defense-=10;n.A2++;if(n.A2>=400){n.A2=0;n.A1=0;}
                float distance=Length(e.PlayerX-n.Bounds.CenterX,e.PlayerY-n.Bounds.CenterY),speed=1.5f;
                if(e.Expert)
                {
                    speed=3.5f;if(distance>150)speed*=1.05f;
                    for(int threshold=200;threshold<=600;threshold+=50)if(distance>threshold)speed*=1.1f;
                    if(hands==0)speed*=1.1f;else if(hands==1)speed*=1.05f;
                }
                if(red)speed*=1.4f;else if(e.GoodWorld)speed*=1.3f;
                Aim(ref n,e,speed,distance);
            }
            else if(n.A1==2){n.Health.Defense=9999;Aim(ref n,e,8,Length(e.PlayerX-n.Bounds.CenterX,e.PlayerY-n.Bounds.CenterY));}
            else if(n.A1==3){n.Vy+=.1f;if(n.Vy<0)n.Vy*=.95f;n.Vx*=.95f;n.TimeLeft=Math.Min(n.TimeLeft,50);}
            // Other observed phases receive no AI11 motor branch. This matches
            // native silence; it does not select an invented future phase.
            return true;
        }
        private static bool Far(NpcMotionState n,PredictionEnvironment e)
        {return Math.Abs(n.X-(e.PlayerX-e.PlayerWidth*.5f))>2000 || Math.Abs(n.Y-(e.PlayerY-e.PlayerHeight*.5f))>2000;}
        private static float Length(float x,float y){return (float)Math.Sqrt(x*x+y*y);}
        private static void Aim(ref NpcMotionState n,PredictionEnvironment e,float speed,float distance)
        {if(distance<=0)distance=1;float factor=speed/distance;n.Vx=(e.PlayerX-n.Bounds.CenterX)*factor;n.Vy=(e.PlayerY-n.Bounds.CenterY)*factor;}
        private static void Query(ref NpcMotionState n,ref PredictionEnvironment e,bool confused){NpcTargeting.Face(ref n,ref e,confused:confused);}
    }
}
