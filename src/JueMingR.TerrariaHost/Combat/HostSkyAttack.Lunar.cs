using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal static partial class HostSkyAttack
    {
        // AI_001's lunar flag is set before movement by proximity, or after
        // collision/Damage by native OnHit. Only the NEXT AI freezes/enlarges
        // the body. The trigger uses NPC.Center, not the damage rectangle.
        private static AttackContact LunarReplay(Vector2 aim,Vector2 birth,Vector2 velocity,AttackMotion motion,NpcTrajectory timeline,int age,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            float x=birth.X,y=birth.Y,vx=velocity.X,vy=velocity.Y;motion.Launch(ref vx,ref vy);
            bool flagged=false,expanded=false,collides=false;int animation=0;
            int limit=Math.Min(motion.Lifetime,motion.SubupdatesForOuters(timeline.Count-age-1));
            int life=Main.projFrames[645]*motion.Updates*3;
            obstacles.BeginCandidate();
            for(int k=1;k<=limit;k++)
            {
                CombatPredictionWork.Check();int outer=motion.OuterAtSubupdate(k),tick=age+outer,sub=motion.SubupdateInOuter(k);
                int width=expanded?140:10;
                if(x-width*.5f<=Main.leftWorld || x+width*.5f>=Main.rightWorld || y-width*.5f<=Main.topWorld || y+width*.5f>=Main.bottomWorld)return null;
                if(sub==0 && !obstacles.BeforeNpc(tick,delay+outer,x,y,width,width))return null;
                // The native loop decrements numUpdates BEFORE AI. With six
                // updates it reads 4,3,2,1,0,-1; proximity belongs to index4.
                if(flagged)
                {
                    expanded=true;vx=vy=0;collides=false;width=140;
                    if(++animation>=life)return null;
                }
                else
                {
                    collides|=y-5>aim.Y;
                    int count=outer==1 && motion.FirstUpdates>0?motion.FirstUpdates:motion.Updates;
                    if(sub==count-2 && obstacles.LunarNear(tick,x,y,terrain))flagged=true;
                    float nx=x,ny=y;motion.Advance(ref nx,ref ny,ref vx,ref vy,k);
                    if(collides)
                    {
                        float rx,ry;if(!terrain.ProjectileCollision(x,y,vx,vy,10,10,out rx,out ry))return null;
                        float sx=x,sy=y,cx=rx,cy=ry;if(!terrain.ProjectileSlope(ref sx,ref sy,ref cx,ref cy,10,10))return null;
                        if(sx!=x || sy!=y || cx!=vx || cy!=vy){flagged=true;vx=cx;vy=cy;}
                        x=sx+vx;y=sy+vy;
                    }
                    else{x=nx;y=ny;}
                }
                int prior=obstacles.SelectedHits;
                // Existing ordered receiver/immunity ownership is reused for
                // both sizes. A nearby immune NPC can trigger AI, but cannot
                // grant Damage; direct hits write local immunity exactly once.
                bool passed=obstacles.PassCustomOuter(k,tick,delay+outer,x,y,width,width,null,true,consumeSelected:true,onHit:(slot,remaining)=>{flagged=true;return true;});
                if(obstacles.SelectedHits>prior)
                    return AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,x,y,width,width,AttackConfidence.Representative);
                if(!passed)return null;
            }
            return null;
        }
    }
}
