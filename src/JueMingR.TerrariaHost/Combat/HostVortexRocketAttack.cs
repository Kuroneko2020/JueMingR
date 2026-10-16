using System;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // The Vortex controller emits this local ranged rocket in a finite
    // firing phase. AI001 autonomously acquires after twenty actions, then
    // keeps its lock even while that target becomes invulnerable.
    internal static class HostVortexRocketAttack
    {
        internal static bool Due(Projectile controller,int delay)
        {return controller.type==615 && (int)(controller.ai[0]+delay+1)/5%7==0;}
        internal static bool Ai(Projectile p,HostProjectileTargets acquire,HostProjectileTargets locked,NpcTrajectory path,int tick,PredictionTerrain terrain)
        {
            float speed=(float)Math.Sqrt(p.velocity.X*p.velocity.X+p.velocity.Y*p.velocity.Y);if(p.localAI[0]==0)p.localAI[0]=speed;speed=p.localAI[0];p.alpha=Math.Max(0,p.alpha-25);p.ai[0]++;
            if(p.ai[0]>20)
            {
                p.ai[0]--;Vector2 destination=Vector2.Zero;bool found=false;
                if(p.ai[1]==0)
                {
                    float nearest=800;
                    for(int i=0;i<acquire.Count;i++)
                    {
                        var b=acquire.Bounds(i,path,tick);var center=new Vector2(b.X+(int)b.Width/2,b.Y+(int)b.Height/2);float distance=Math.Abs(p.position.X+p.width/2-center.X)+Math.Abs(p.position.Y+p.height/2-center.Y);if(distance>=nearest)continue;
                        bool clear;PredictionStop stop;if(!terrain.CanHit(new MotionRect(p.position.X+p.width/2,p.position.Y+p.height/2,1,1),b,out clear,out stop))return false;if(!clear)continue;nearest=distance;p.ai[1]=acquire.Slot(i)+1;
                    }
                }
                if(p.ai[1]!=0)
                    for(int i=0;i<locked.Count;i++)if(locked.Slot(i)==(int)(p.ai[1]-1))
                    {
                        var b=locked.Bounds(i,path,tick);destination=new Vector2(b.X+(int)b.Width/2,b.Y+(int)b.Height/2);found=Math.Abs(p.position.X+p.width/2-destination.X)+Math.Abs(p.position.Y+p.height/2-destination.Y)<1000;break;
                    }
                if(found && p.friendly)
                {
                    float dx=destination.X-(p.position.X+p.width*.5f),dy=destination.Y-(p.position.Y+p.height*.5f);float length=(float)Math.Sqrt(dx*dx+dy*dy);float factor=speed/length;dx*=factor;dy*=factor;p.velocity.X=(p.velocity.X*7+dx)/8;p.velocity.Y=(p.velocity.Y*7+dy)/8;
                }
            }
            p.rotation=(float)Math.Atan2(p.velocity.Y,p.velocity.X)+1.57f;if(p.velocity.Y>16)p.velocity.Y=16;return true;
        }
        internal static void KillBody(Projectile p)
        {var center=p.Center;p.width=p.height=80;p.Center=center;}
    }
}
