using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // Skeletron Prime supplies the MechQueen's moving anchor. Arms do not feed
    // back into its own motor; its target, global identity and retirement writes
    // do. This kernel advances scalar state only, never native AI or births.
    internal static class NpcMechanicalBossMotion
    {
        internal static bool Known(NpcMotionState n){return n.EffectiveType==127 && n.Style==32;}
        internal static bool SamePhase(NpcMotionState a,NpcMotionState b){return Known(a) && Known(b);}
        internal static bool NeedsPlayer(NpcMotionState n,PredictionEnvironment e)
        {return n.A1!=3 && (!n.TargetCaptured || !n.PlayerNativeDead || n.HasClosestPlayer);}
        internal static bool Retargets(NpcMotionState n)
        {return n.A0==0 || n.TargetCaptured && n.PlayerNativeDead;}
        internal static bool Step(ref NpcMotionState n,PredictionEnvironment e,IPredictionTerrain terrain,int elapsed,out PredictionStop stop,NpcMotionState[] group=null,int count=0,bool confused=false)
        {
            stop=PredictionStop.None;n.ReflectsProjectiles=false;
            if(!n.EyeRelationsCaptured || !n.EyeRelationsComplete || !n.MechFactsCaptured || group==null)
            {stop=PredictionStop.MissingDependency;return false;}
            if(n.A3!=0)
            {
                n.MechQueenIdentity=n.Identity;
                for(int i=0;i<count;i++)if(EyeFamily(group[i]))group[i].MechQueenIdentity=n.Identity;
            }
            bool queen=false;
            if(n.MechQueenIdentity.Token!=null)
            {
                bool found=false;for(int i=0;i<count;i++)if(group[i].Identity.Equals(n.MechQueenIdentity))
                {found=true;queen=group[i].Active && group[i].EffectiveType==127;break;}
                if(!found){stop=PredictionStop.MissingDependency;return false;}
                if(!queen){n.MechQueenIdentity=default(NpcIdentity);for(int i=0;i<count;i++)if(EyeFamily(group[i]))group[i].MechQueenIdentity=default(NpcIdentity);}
            }
            n.Health.Defense=n.Health.DefaultDefense;
            if(n.A0==0 && !e.Multiplayer){Query(ref n,ref e,confused);n.A0=1;}
            if(!n.HasPlayer && n.TargetCaptured){stop=PredictionStop.MissingDependency;return false;}
            float px=e.PlayerX-e.PlayerWidth/2,py=e.PlayerY-e.PlayerHeight/2;
            bool dead=n.TargetCaptured?n.PlayerNativeDead:e.PlayerDead;
            if(dead || Math.Abs(n.X-px)>6000 || Math.Abs(n.Y-py)>6000)
            {
                Query(ref n,ref e,confused);px=e.PlayerX-e.PlayerWidth/2;py=e.PlayerY-e.PlayerHeight/2;dead=n.TargetCaptured?n.PlayerNativeDead:e.PlayerDead;
                if(dead || Math.Abs(n.X-px)>6000 || Math.Abs(n.Y-py)>6000)n.A1=3;
            }
            if(e.Day && n.A1!=3 && n.A1!=2)n.A1=2;
            if(n.A1!=3 && n.TargetCaptured && (n.PlayerGhost || !n.PlayerActive))
            {stop=PredictionStop.MissingDependency;return false;}
            if(n.A1==0)
            {
                // Native keeps its already fetched Player object when the
                // hover clock calls TargetClosest; that action's motor still
                // reads the old player and the next action reads the new one.
                float motorPlayerX=e.PlayerX;
                n.A2++;if(n.A2>=600){n.A2=0;n.A1=1;Query(ref n,ref e,confused);}
                float ay=e.Expert?.03f:.1f,vy=e.Expert?4:2,ax=e.Expert?.07f:.1f,vx=e.Expert?9.5f:8;
                int top=queen?300:200,bottom=queen?350:500,side=motorPlayerX<n.Bounds.CenterX?-1:1;float offset=queen?-450f*side:0;
                if(n.Y>py-top){if(n.Vy>0)n.Vy*=.98f;n.Vy-=ay;if(n.Vy>vy)n.Vy=vy;}
                else if(n.Y<py-bottom){if(n.Vy<0)n.Vy*=.98f;n.Vy+=ay;if(n.Vy<-vy)n.Vy=-vy;}
                if(n.Bounds.CenterX>motorPlayerX+100+offset){if(n.Vx>0)n.Vx*=.98f;n.Vx-=ax;if(n.Vx>vx)n.Vx=vx;}
                if(n.Bounds.CenterX<motorPlayerX-100+offset){if(n.Vx<0)n.Vx*=.98f;n.Vx+=ax;if(n.Vx<-vx)n.Vx=-vx;}
            }
            else if(n.A1==1)
            {
                n.Health.Defense*=2;n.A2++;if(n.A2>=400){n.A2=0;n.A1=0;}
                float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY,d=NpcEyeBossMotion.Length(dx,dy);if(d<=0)d=1;
                float speed=e.Expert?6:2;
                if(e.Expert){if(d>150)speed*=1.05f;for(int threshold=200;threshold<=600;threshold+=50)if(d>threshold)speed*=1.1f;}
                if(queen)
                {
                    if(!n.MechDestroyerCaptured){stop=PredictionStop.MissingDependency;return false;}
                    speed*=n.MechDestroyerPresent?.6f:.75f;
                }
                float factor=speed/d;n.Vx=dx*factor;n.Vy=dy*factor;
                if(queen)
                {
                    float actual=NpcEyeBossMotion.Length(dx,dy);if(actual<.1f)actual=0;
                    if(actual<speed)
                    {
                        double squared=(double)n.Vx*n.Vx+(double)n.Vy*n.Vy;
                        // SafeNormalize(Vector2.Zero) preserves its exact
                        // zero/NaN fallback before the final distance scaling.
                        if(squared==0 || float.IsNaN(n.Vx) || float.IsNaN(n.Vy)){n.Vx=n.Vy=0;}else{float root=(float)Math.Sqrt(squared);double inverse=1d/root;n.Vx=(float)(n.Vx*inverse);n.Vy=(float)(n.Vy*inverse);n.Vx*=actual;n.Vy*=actual;}
                    }
                }
            }
            else if(n.A1==2)
            {
                n.Health.Defense=9999;float dx=e.PlayerX-n.Bounds.CenterX,dy=e.PlayerY-n.Bounds.CenterY,d=NpcEyeBossMotion.Length(dx,dy);if(d<=0)d=1;
                float speed=10;speed+=d/100;speed=Math.Max(8,Math.Min(32,speed));float factor=speed/d;n.Vx=dx*factor;n.Vy=dy*factor;
            }
            else if(n.A1==3)
            {
                if(queen)
                {
                    bool any=false,one=false,two=false;
                    for(int i=0;i<count;i++)if(group[i].Active)
                    {
                        int type=group[i].EffectiveType;
                        if(type==125){any=true;if(!one){one=true;if(group[i].TimeLeft>5)group[i].TimeLeft=5;}}
                        if(type==126){any=true;if(!two){two=true;if(group[i].TimeLeft>5)group[i].TimeLeft=5;}}
                    }
                    if(!any)
                    {
                        // Original transforms the first live Destroyer head.
                        // Changing its type invalidates the worm motor closure.
                        for(int i=0;i<count;i++)if(group[i].Active && group[i].EffectiveType==134)
                        {stop=PredictionStop.PhaseBoundary;return false;}
                        if(n.TimeLeft>5)n.TimeLeft=5;
                    }
                }
                else if(n.TimeLeft>500)n.TimeLeft=500;
                n.Vy+=.1f;if(n.Vy<0)n.Vy*=.95f;n.Vx*=.95f;if(queen && n.Vy>13)n.Vy=13;
            }
            return true;
        }
        // Global mechQueen writes are visible to later native slots, including
        // the Destroyer head anchor and the first nine body constraints.
        private static bool EyeFamily(NpcMotionState n){return n.EffectiveType==125 || n.EffectiveType==126 || n.EffectiveType==127 || n.Style==37 && n.EffectiveType>=134 && n.EffectiveType<=136;}
        private static void Query(ref NpcMotionState n,ref PredictionEnvironment e,bool confused){NpcTargeting.Face(ref n,ref e,confused:confused);}
    }
}
