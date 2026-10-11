using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // All six flare rounds use AI33. A tile collision sticks the live body,
    // but its damage becomes zero only on the NEXT AI action. That last real
    // receive is distinct from the long-lived light left in the world.
    internal static class HostFlareAttack
    {
        internal static bool Handles(int type){return type==163 || type==310 || type>=1008 && type<=1011;}
        internal static Projectile Copy(Projectile p){return HostNoteAttack.Copy(p);}
        private static void Kill(Projectile p){p.active=false;p.timeLeft=0;}
        internal static bool Step(Projectile p,PredictionTerrain terrain)
        {
            if(!p.active)return true;
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y)){p.active=false;return true;}
            if(!Handles(p.type) || p.aiStyle!=33 || p.correctSlopeCollision || p.ownerHitCheck || p.usesOwnerMeleeHitCD)return false;
            p.oldVelocity=p.velocity;p.alpha=Math.Max(0,p.alpha-50);
            if(p.localAI[0]==0)
            {
                p.ai[0]=p.velocity.X;p.ai[1]=p.velocity.Y;p.localAI[1]++;
                if(p.localAI[1]>=30){p.velocity.Y+=.09f;p.localAI[1]=30;}
            }
            else
            {
                bool solid;PredictionStop stop;
                if(!terrain.Solid(new MotionRect(p.position.X,p.position.Y,p.width,p.height),out solid,out stop))return false;
                if(!solid){p.localAI[0]=0;p.localAI[1]=30;}
                // Detaching never restores the damage removed by the native
                // stuck action. Spelunker/light/dust work has no attack state.
                p.damage=0;
            }
            p.velocity.Y=Math.Min(16,p.velocity.Y);p.rotation=(float)Math.Atan2(p.ai[1],p.ai[0])+1.57f;
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active)Kill(p);
            if(!p.ignoreWater)
            {
                bool wet,lava;byte kind;
                if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
                if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;
                if(p.wet && !wet && p.wetCount==0)p.wetCount=10;p.wet=wet;
                if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;if(p.wetCount>0)p.wetCount--;
            }
            if(p.shimmerWet)
            {
                int x=(int)(p.Center.X/16),y=(int)(p.position.Y/16);bool full=false;
                if(WorldGen.InWorld(x,y) && WorldGen.InWorld(x,y-1))
                {PredictionTile tile,above;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;if(tile.Liquid==255 && tile.ShimmerLiquid){if(!terrain.Tile(x,y-1,out above,out stop))return false;full=above.Liquid>0 && above.ShimmerLiquid;}}
                if(full)Kill(p);else if(p.velocity.Y>0){p.velocity.Y=-p.velocity.Y;p.wet=p.shimmerWet=false;}
            }
            p.oldPosition=p.position;var previous=p.velocity;var position=p.position;var velocity=p.velocity;var wetVelocity=velocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;
            p.position=position;p.velocity=velocity;
            if(hit && p.localAI[0]==0){p.position+=p.wet?previous/2:previous;p.velocity*=0;p.localAI[0]=1;}
            p.position+=p.wet?wetVelocity:p.velocity;p.direction=p.velocity.X<0?-1:1;return true;
        }
        internal static void Finish(Projectile p){if(p.active && (--p.timeLeft<=0 || p.penetrate==0))Kill(p);}
        private static AttackContact Replay(Projectile source,NpcTrajectory path,int first,int outerFirst,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim)
        {
            var p=Copy(source);root.BeginCandidate();var receive=root.ForkForSpawn(p);
            for(int step=0;step<Math.Min(120,path.Count-first) && p.active;step++)
            {
                int tick=first+step,outer=outerFirst+step;if(!receive.BeforeNpc(tick,outer,p.Center.X,p.Center.Y,p.width,p.height))return null;
                int updates=p.extraUpdates+1;
                for(int sub=0;sub<updates && p.active;sub++)
                {
                    if(!Step(p,terrain) || !p.active || p.damage<=0)return null;
                    int hits=receive.SelectedHits;bool passed=receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,unlimited:p.penetrate<0,consumeSelected:true);
                    if(receive.SelectedHits>hits)return AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,AttackConfidence.Conditional);
                    p.penetrate=receive.Remaining;if(!passed)return null;Finish(p);
                }
            }
            return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            if(!Handles(ammo.Projectile) || ammo.Damage<=0 || age+1>=path.Count)return null;
            obstacles.CaptureShotFacing(player);obstacles.CaptureCursorMapping(player);var directions=new Vector2[41];int count=0;
            for(int seed=0;seed<41;seed++)
            {
                var box=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;
                var direction=seed<9?(new Vector2(box.CenterX,box.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();
                bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;
                var aim=origin+direction*10000;var p=new Projectile();p.SetDefaults(ammo.Projectile);p.active=true;p.owner=player.whoAmI;p.Center=origin;p.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);p.damage=ammo.Damage;
                bool wet;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true))return null;p.wet=wet;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;
                var contact=Replay(p,path,age+1,delay+1,terrain,obstacles,aim);if(contact!=null)return contact;
            }
            return null;
        }
        internal static AttackContact Known(Player player,Projectile p,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(p==null || !p.active || !Handles(p.type) || p.owner!=player.whoAmI || !p.friendly || p.hostile || p.damage<=0 || clock.LaunchDelay!=0)return null;
            obstacles.CaptureProjectileFamily(new[]{p});return Replay(p,path,clock.FirstTick,1,terrain,obstacles,p.Center+p.velocity*1000);
        }
    }
}
