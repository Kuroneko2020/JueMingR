using System;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Native homing is an independent closed motion stage. It consumes the
    // captured chase set and the replay's shared owner-immunity writes; a
    // training dummy being a Damage receiver does not make it chaseable.
    internal static class HostTyphoonAttack
    {
        internal static bool Ai(Projectile p,PredictionTerrain terrain,HostProjectileTargets targets,NpcTrajectory path,int tick,int outer,HostAttackObstacles receive)
        {
            p.localAI[1]++;if(p.localAI[0]>0)p.localAI[0]--;
            Vector2 goal=p.Center;float nearest=500;
            if(p.ai[0]==0 && p.localAI[0]==0 && targets!=null)
            {
                int found=-1;
                for(int i=0;i<targets.Count;i++)
                {
                    int slot=targets.Slot(i);var b=targets.Bounds(i,path,tick);var center=new Vector2(b.CenterX,b.CenterY);float distance=Vector2.Distance(center,goal);bool clear;PredictionStop stop;
                    // Native updates the comparison origin on each accepted
                    // slot. Preserve that order-dependent chain, not a newly
                    // invented nearest-from-projectile query.
                    if(distance<nearest && terrain.CanHit(new MotionRect(p.position.X,p.position.Y,p.width,p.height),b,out clear,out stop) && clear){nearest=distance;goal=center;found=slot;}
                }
                if(found>=0)p.ai[0]=found+1;
            }
            if(p.localAI[0]==0 && p.ai[0]==0)p.localAI[0]=30;
            bool locked=false;
            if(p.ai[0]!=0)
            {
                int slot=(int)p.ai[0]-1,index=-1;if(targets!=null)for(int i=0;i<targets.Count;i++)if(targets.Slot(i)==slot){index=i;break;}
                bool clear=index>=0 && (receive==null?targets.OwnerImmune(index)==0:receive.HomingOwnerClear(slot,targets.OwnerImmune(index),outer));
                if(clear)
                {
                    var b=targets.Bounds(index,path,tick);var center=new Vector2(b.X+(int)b.Width/2,b.Y+(int)b.Height/2);
                    if(Math.Abs(p.position.X+p.width/2-center.X)+Math.Abs(p.position.Y+p.height/2-center.Y)<1000){goal=new Vector2(b.CenterX,b.CenterY);locked=true;}
                }
                else p.ai[0]=0;
            }
            if(locked)
            {double angle=(goal-p.Center).ToRotation()-p.velocity.ToRotation();if(angle>Math.PI)angle-=Math.PI*2;if(angle< -Math.PI)angle+=Math.PI*2;p.velocity=p.velocity.RotatedBy(angle*.10000000149011612);}
            float speed=p.velocity.Length();p.velocity.Normalize();p.velocity*=speed+.0025f;return true;
        }
    }
}
