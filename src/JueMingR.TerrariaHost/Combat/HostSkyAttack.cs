using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostSkyAttack
    {
        internal static bool Handles(int type){return type==3029 || type==4381 || type==2750 || type==65 || type==3065 || type==3570;}
        internal static bool RawCursor(int type){return type==3029 || type==4381 || type==3065 || type==3570;}
        internal static AttackContact Solve(Player player,Item item,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 mounted,int age,PredictionTerrain terrain,bool beforeNpc,HostAttackObstacles obstacles)
        {
            Projectile sample;if(!ContentSamples.ProjectilesByType.TryGetValue(ammo.Projectile,out sample))return null;
            var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc);
            AttackMotion ordinary;float gravity=0;int start=0;
            if(HostAttackModels.TryRead(ammo,out ordinary)){gravity=ordinary.Gravity;start=ordinary.GravityStart;}
            else if(item.type==4381){gravity=.1f;start=15;}
            float speed=ammo.Speed*(item.type==2750?.75f:item.type==3570?.5f:1);
            var motion=new AttackMotion(speed,gravity,start,sample.extraUpdates+1,sample.width,sample.height,sample.timeLeft,AttackConfidence.Representative,componentBirthLimit:sample.aiStyle==1);
            // Random birth X, spread, count and type/scale cannot be known
            // without consuming game RNG. One middle member is a representative
            // trajectory; no native random stream or ContentSamples is changed.
            for(int tick=age+1;tick<timeline.Count;tick++)
            {
                var b=timeline[tick].ProjectileReceiveBounds;var aim=new Vector2(b.CenterX,b.CenterY);
                Vector2 birth,velocity;Launch(player,item.type,mounted,aim,speed,out birth,out velocity);
                // Horizontal cursor correction uses the actual sky origin
                // relation, rather than a gun's far direction point. Three
                // finite refinements compensate arrow gravity/slow blood X.
                for(int adjust=0;adjust<3;adjust++)
                {
                    float x=birth.X,y=birth.Y,vx=velocity.X,vy=velocity.Y;motion.Launch(ref vx,ref vy);
                    int steps=(tick-age)*motion.Updates;
                    for(int k=1;k<=steps;k++)motion.Advance(ref x,ref y,ref vx,ref vy,k);
                    aim.X+=b.CenterX-x;Launch(player,item.type,mounted,aim,speed,out birth,out velocity);
                }
                float gate=aim.Y;
                if(item.type==3065)gate=Math.Min(gate,player.Center.Y-200);
                if(item.type==65)
                {
                    Vector2 scan=aim,step=(birth-aim).SafeNormalize(new Vector2(0,-1))*16;bool blocked;PredictionStop stop;
                    for(int k=0;k<120 && scan.Y>birth.Y;k++)
                    {if(!terrain.Solid(new MotionRect(scan.X,scan.Y,1,1),out blocked,out stop))return null;if(!blocked)break;scan+=step;}
                    gate=scan.Y;
                }
                bool collides=sample.tileCollide;
                AttackPassage passage=(x,y,nx,ny,w,h)=>
                {
                    if(item.type==2750)collides|=y-h/2>player.position.Y-300 || y-h/2<Main.worldSurface*16;
                    else if(item.type==65)collides|=y+h/2>=gate;
                    else if(item.type==3065)collides|=y>gate;
                    else if(item.type==3570)collides|=y-h/2>gate;
                    return HostProjectileEnvironment.Dry(sample,terrain,x,y,nx-x) && (!collides || terrain.ProjectilePassage(x,y,nx,ny,(int)w,(int)h));
                };
                var contact=AttackIntercept.Replay(aim.X,aim.Y,birth.X,birth.Y,velocity.X,velocity.Y,motion,timeline,age,passage,receive.Allows,obstacles.Pass);
                if(contact!=null)return contact;
            }
            return null;
        }
        private static void Launch(Player p,int type,Vector2 mounted,Vector2 aim,float speed,out Vector2 birth,out Vector2 velocity)
        {
            float x;
            if(type==3065)x=p.Center.X-200*p.direction+(mounted.X-p.MountedCenter.X);
            else
            {
                float shift=type==4381?30:100;
                if(type==65)shift=aim.X<p.Left.X?100:-100;
                else shift*=-p.direction;
                x=aim.X+p.width/2+shift;
                if(type==3029 || type==4381)x=(x*10+p.Center.X)/11;
                else if(type!=65)x=(x+p.Center.X)/2;
            }
            birth=new Vector2(x,mounted.Y-600-(type==4381?75:0));
            velocity=aim-birth;if(type!=65)velocity.Y=Math.Max(20,Math.Abs(velocity.Y));
            velocity=velocity.SafeNormalize(Vector2.UnitY)*speed;if(type==4381)velocity.X*=.675f;
        }
    }
}
