using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // AI024 crystal roles retain their actual scale clock and collision phase.
    // CrystalStorm reflects; its shimmer variant dies on a wall or preceding
    // wet state. Neither phase may be replaced by a longer straight trajectory.
    internal static class HostCrystalAttack
    {
        internal static bool Handles(int type){return type==94 || type==1114;}
        internal struct State
        {
            internal float X,Y,Vx,Vy,Age,Scale;internal int Left;internal bool Alive,Wet,Honey,Lava,Shimmer;
            internal State(Projectile shot,Vector2 origin,Vector2 velocity)
            {X=origin.X;Y=origin.Y;Vx=velocity.X;Vy=velocity.Y;Age=shot.ai[1];Scale=shot.scale;Left=shot.timeLeft;Alive=true;Wet=shot.wet;Honey=shot.honeyWet;Lava=shot.lavaWet;Shimmer=shot.shimmerWet;}
        }
        internal static bool Step(Projectile sample,PredictionTerrain terrain,ref State s)
        {
            if(!s.Alive || s.Left<=0){s.Alive=false;return true;}
            Vector2 wetVelocity=new Vector2(s.Vx,s.Vy);
            if(s.Shimmer)return false;
            s.Age++;if(sample.type==1114 && s.Wet){s.Alive=false;return true;}
            s.Vx*=.985f;s.Vy*=.985f;
            if(s.Age>130){s.Scale-=.05f;if((double)s.Scale<=.2){s.Scale=.2f;s.Alive=false;return true;}}
            float wind;if(!HostProjectileEnvironment.Impulse(sample,terrain,s.X,s.Y,s.Vx,out wind))return false;s.Vx+=wind;
            if(!sample.ignoreWater)
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileWet(s.X,s.Y,sample.width,sample.height,out wet,out kind) || wet && kind==3 || !terrain.ProjectileLava(s.X,s.Y,sample.width,sample.height,out lava))return false;
                if(lava)s.Lava=true;if(wet && kind==2)s.Honey=true;s.Wet=wet;if(!wet)s.Honey=s.Lava=false;
            }
            float lastX=s.Vx,lastY=s.Vy;Vector2 half=new Vector2(sample.width,sample.height)*.5f,position=new Vector2(s.X,s.Y)-half,velocity=new Vector2(s.Vx,s.Vy);bool hit=false;
            if(sample.tileCollide && !terrain.ProjectileTileMotion(sample,ref position,ref velocity,s.Wet,s.Honey,out wetVelocity,out hit))return false;
            s.X=position.X+half.X;s.Y=position.Y+half.Y;s.Vx=velocity.X;s.Vy=velocity.Y;
            if(hit)
            {
                if(sample.type!=94){s.X+=s.Vx;s.Y+=s.Vy;s.Alive=false;return true;}
                if(s.Vx!=lastX)s.Vx=-lastX;if(s.Vy!=lastY)s.Vy=-lastY;
            }
            float moveX=wetVelocity.X,moveY=wetVelocity.Y;
            s.X+=s.Wet?moveX:s.Vx;s.Y+=s.Wet?moveY:s.Vy;s.Left--;return true;
        }
        internal static AttackContact Replay(Projectile shot,PredictionTerrain terrain,NpcTrajectory timeline,Vector2 aim,Vector2 origin,Vector2 velocity,int age)
        {
            var s=new State(shot,origin,velocity);int updates=shot.extraUpdates+1,limit=Math.Min(180,timeline.Count-age-1);
            for(int outer=1;outer<=limit && s.Alive && s.Left>0;outer++)for(int sub=0;sub<updates && s.Alive && s.Left>0;sub++)
            {if(!Step(shot,terrain,ref s) || !s.Alive)return null;var contact=AttackIntercept.BodyContact(timeline,age+outer,sub,aim.X,aim.Y,s.X,s.Y,shot.width,shot.height,AttackConfidence.Conditional);if(contact!=null)return contact;}
            return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            var sample=ContentSamples.ProjectilesByType[ammo.Projectile];bool bornWet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,sample.width,sample.height,out bornWet,out kind) || bornWet && kind==3)return null;
            var receive=HostAttackReceive.Capture(player,sample,timeline.Identity.Slot,beforeNpc,!beforeNpc);int limit=Math.Min(120,timeline.Count-age-1);if(limit<=0)return null;
            AttackMotion motion;if(!HostAttackModels.TryRead(ammo,out motion))return null;var direct=AttackIntercept.Solve(origin.X,origin.Y,motion,timeline,age,0);var directions=new Vector2[42];int count=0;
            for(int seed=-1;seed<41;seed++)
            {
                Vector2 direction;if(seed<0){if(direct==null)continue;direction=new Vector2(direct.AimX-origin.X,direct.AimY-origin.Y).SafeNormalize(Vector2.UnitX*player.direction);}
                else if(seed<9){var b=timeline[Math.Min(timeline.Count-1,age+1+seed*4)].ProjectileReceiveBounds;direction=(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction);}
                else direction=((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;
                var s=new State(sample,origin,direction*ammo.Speed);s.Wet=!sample.ignoreWater && bornWet;s.Honey=bornWet && kind==2;var aim=origin+direction*10000;obstacles.BeginCandidate();int k=0;
                for(int outer=1;outer<=limit && s.Alive && s.Left>0;outer++)for(int sub=0;sub<sample.extraUpdates+1 && s.Alive && s.Left>0;sub++)
                {
                    if(sub==0 && !obstacles.BeforeNpc(age+outer,delay+outer,s.X,s.Y)){s.Alive=false;break;}
                    k++;if(!Step(sample,terrain,ref s) || !s.Alive){s.Alive=false;break;}int tick=age+outer;if(!obstacles.PassOuter(k+1,tick,delay+outer,s.X,s.Y)){s.Alive=false;break;}
                    if(receive.Allows(delay+outer)){var contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,s.X,s.Y,sample.width,sample.height,motion.Confidence);if(contact!=null)return contact;}
                }
            }
            return null;
        }
    }
}
