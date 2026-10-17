using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // One native Shoot emits one random ring member. Item3787's12/4
    // animation emits THREE separate hand actions, followed by reuse18;
    // those future actions must not be invented as simultaneous siblings.
    internal static class HostSkyFractureAttack
    {
        internal static bool Handles(int type){return type==660;}
        internal static Projectile Copy(Projectile p){return HostNebulaBlazeAttack.Copy(p);}
        internal static void Birth(Vector2 origin,Vector2 cursor,float speed,Vector2 offset,out Vector2 center,out Vector2 velocity)
        {
            // At an exactly centred cursor, native ItemCheck_ChangeDir's
            // zero dot chooses-1 before num4's zero-vector fallback.
            center=origin+offset;var initial=(cursor-origin).SafeNormalize(-Vector2.UnitX)*speed;
            velocity=Vector2.Lerp((cursor-center).SafeNormalize(initial)*speed,initial,.25f);
            // The argument is normalized above, but NewProjectile's common
            // AI001 birth still owns this component limit for extreme inputs.
            while(velocity.X>=16 || velocity.X<=-16 || velocity.Y>=16 || velocity.Y< -16)velocity*=.97f;
        }
        private static void Kill(Projectile p){if(p.active){p.timeLeft=0;p.active=false;}}
        internal static bool Step(Projectile p,PredictionTerrain terrain)
        {return !p.active || Motor(p,terrain);}
        internal static bool RetiredStep(Projectile p,PredictionTerrain terrain){return Motor(p,terrain);}
        private static bool Motor(Projectile p,PredictionTerrain terrain)
        {
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y)){p.active=false;return true;}
            if(p.active)
            {
                if(p.alpha>0){p.alpha=0;p.scale=1.1f;p.frame=0;} // Native Next(14) selects only a visual frame.
                p.rotation=p.velocity.ToRotation()+MathHelper.PiOver4;if(p.velocity.Y>16)p.velocity.Y=16;
            }
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active)Kill(p);
            if(!p.ignoreWater)
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
                if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;if(p.wet && !wet && p.wetCount==0)p.wetCount=10;p.wet=wet;
                if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;if(p.wetCount>0)p.wetCount--;
            }
            if(p.shimmerWet)
            {
                int x=(int)(p.Center.X/16),y=(int)(p.position.Y/16);bool full=false;
                if(WorldGen.InWorld(x,y) && WorldGen.InWorld(x,y-1)){PredictionTile tile,above;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;if(tile.Liquid==255 && tile.ShimmerLiquid){if(!terrain.Tile(x,y-1,out above,out stop))return false;full=above.Liquid>0 && above.ShimmerLiquid;}}
                if(full)Kill(p);else if(p.velocity.Y>0){p.velocity.Y=-p.velocity.Y;if(p.timeLeft>600)p.timeLeft=600;p.timeLeft-=60;p.wet=p.shimmerWet=false;}
            }
            var position=p.position;var velocity=p.velocity;Vector2 wetVelocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;
            p.position=position;p.velocity=velocity;if(hit){p.position+=velocity;Kill(p);}p.position+=velocity;p.direction=p.velocity.X<0?-1:1;return true;
        }
        internal static void Finish(Projectile p){if(p.active && (--p.timeLeft<=0 || p.penetrate==0))Kill(p);}
        private sealed class Member
        {internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Reflected;}
        private static Projectile[] Capture(HostAttackObstacles root)
        {var rows=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && Handles(p.type))rows.Add(p);var scene=rows.ToArray();root.CaptureProjectileFamily(scene,660,660);return scene;}
        private static Member Make(Projectile p,HostAttackObstacles root,bool source)
        {return new Member{Shot=p,Source=source,Receive=p.owner==Main.myPlayer?root.ForkForSpawn(p):null};}
        private static List<Member> Prepare(Projectile[] scene,Projectile[] source,HostAttackObstacles root)
        {
            var rows=new List<Member>();foreach(var p in scene){bool own=false;if(source!=null)foreach(var member in source)if(ReferenceEquals(member,p)){own=true;break;}rows.Add(Make(Copy(p),root,own));}
            rows.Sort((left,right)=>left.Shot.whoAmI.CompareTo(right.Shot.whoAmI));return rows;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,Vector2 aim,AttackConfidence confidence,out AttackContact contact)
        {
            contact=null;
            foreach(var m in rows)if(m.Shot.active && m.Receive!=null && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height)){m.Shot.active=false;m.Reflected=true;}
            foreach(var m in rows)
            {
                var p=m.Shot;if(!p.active)continue;int updates=p.extraUpdates+1;
                for(int sub=0;sub<updates;sub++)
                {
                    if(!Motor(p,terrain))return false;if(!p.active){slots.Release(p.whoAmI);break;}
                    if(m.Receive!=null && p.friendly && !p.hostile && p.damage>0)
                    {
                        int hits=m.Receive.SelectedHits;bool passed=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true);p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,confidence);return true;}
                        if(!passed && p.penetrate!=0){p.active=false;m.Reflected=true;break;}
                    }
                    Finish(p);if(!p.active){if(sub+1<updates && !RetiredStep(p,terrain))return false;slots.Release(p.whoAmI);break;}
                }
            }
            return true;
        }
        private static Vector2 Cursor(Player player,Vector2 aim)
        {var pixel=aim-Main.screenPosition;if(player.gravDir==-1)pixel.Y=Main.screenHeight-pixel.Y;return new Vector2((int)pixel.X+Main.screenPosition.X,player.gravDir==-1?Main.screenPosition.Y+Main.screenHeight-(int)pixel.Y:(int)pixel.Y+Main.screenPosition.Y);}
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            if(ammo.WeaponType!=3787 || ammo.Projectile!=660 || ammo.Damage<=0 || delay<0 || age-delay<0 || age+1>=path.Count)return null;
            obstacles.CaptureShotFacing(player);var scene=Capture(obstacles);var pool=obstacles.CaptureBirthSlots();var inputs=new Vector2[41];int count=0;
            for(int seed=0;seed<41;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;
                var aim=seed<9?new Vector2(b.CenterX,b.CenterY):origin+((seed-9)*MathHelper.TwoPi/32).ToRotationVector2()*10000;var cursor=Cursor(player,aim);bool duplicate=false;for(int i=0;i<count;i++)if(inputs[i]==cursor){duplicate=true;break;}if(duplicate)continue;inputs[count++]=cursor;
                var direction=(cursor-origin).SafeNormalize(Vector2.UnitY);
                // Four legal ring positions share one fixed radius40. This
                // representative is not a claim that a later random draw
                // chooses that position, or that all three uses will hit.
                for(int ring=0;ring<4;ring++)
                {
                    var offset=direction.RotatedBy(ring*MathHelper.PiOver2)*40;var center=origin+offset;bool clear;PredictionStop stop;
                    if(!terrain.CanHit(new MotionRect(origin.X,origin.Y,0,0),new MotionRect(center.X+offset.X/5,center.Y+offset.Y/5,0,0),out clear,out stop))return null;if(!clear)continue;
                    obstacles.BeginCandidate();var rows=Prepare(scene,null,obstacles);var slots=pool.Copy();bool valid=true;
                    for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,path,terrain,age-delay+wait,wait,cursor,AttackConfidence.Representative,out ignored)){valid=false;break;}}if(!valid)continue;
                    int slot;if(!slots.TryAllocate(out slot))return null;var p=new Projectile();p.SetDefaults(660);p.active=true;p.whoAmI=slot;p.owner=player.whoAmI;Vector2 velocity;Birth(origin,cursor,ammo.Speed,offset,out center,out velocity);p.Center=center;p.velocity=velocity;p.damage=ammo.Damage;p.knockBack=player.HeldItem.knockBack;
                    bool wet;byte kind;if(!terrain.ProjectileWet(center.X,center.Y,p.width,p.height,out wet,out kind,true))return null;p.wet=wet && !p.ignoreWater;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;
                    rows.Add(Make(p,obstacles,true));rows.Sort((left,right)=>left.Shot.whoAmI.CompareTo(right.Shot.whoAmI));
                    for(int step=1;step<=Math.Min(120,path.Count-age-1);step++){AttackContact contact;if(!Outer(rows,slots,path,terrain,age+step,delay+step,cursor,AttackConfidence.Representative,out contact))break;if(contact!=null)return contact;}
                }
            }
            return null;
        }
        // The source is already autonomous: observation/oracle only, no hand
        // authorization. A partial projectile array needs a slot epoch, so
        // this whole-family entry explicitly declines Projectiles phase.
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(source==null || source.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;foreach(var p in source)if(p==null || !p.active || !Handles(p.type) || p.owner!=player.whoAmI)return null;
            var scene=Capture(obstacles);obstacles.BeginCandidate();var rows=Prepare(scene,source,obstacles);var slots=obstacles.CaptureBirthSlots().Copy();var aim=source[0].Center+source[0].velocity*1000;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,slots,path,terrain,clock.FirstTick+step,step+1,aim,AttackConfidence.Conditional,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
