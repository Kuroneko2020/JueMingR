using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // 3827 emits a transverse line, not its 16x16 drawing/body rectangle.
    // Root owns the prepared mounted pose; this kernel owns only the native
    // direction-dependent 24px hand offset and the actual finite AI001 shot.
    internal static class HostSonicBoomAttack
    {
        internal static bool Handles(int type){return type==684;}
        internal static Projectile Copy(Projectile p)
        {var q=HostNebulaBlazeAttack.Copy(p);q.oldPosition=p.oldPosition;q.oldVelocity=p.oldVelocity;q.numHits=p.numHits;return q;}
        internal static Vector2 BirthVelocity(Vector2 origin,Vector2 cursor,float speed,int facing)
        {float x=cursor.X-origin.X,y=cursor.Y-origin.Y;if(x==0 && y==0)x=facing;float scale=speed/(float)Math.Sqrt(x*x+y*y);return new Vector2(x*scale,y*scale);}
        internal static int ShotDirection(Player player,Vector2 towardCursor)
        {return Vector2.Dot(Vector2.UnitX.RotatedBy(player.fullRotation),towardCursor.SafeNormalize(Vector2.Zero))>0?1:-1;}
        internal static Vector2 BirthOrigin(Vector2 mounted,Vector2 towardCursor,int facing)
        {return mounted+towardCursor.SafeNormalize(Vector2.Zero).RotatedBy((float)facing*(-(float)Math.PI/2f))*24f;}
        internal static Vector2 LimitLaunch(Vector2 velocity)
        {while(velocity.X>=16 || velocity.X<=-16 || velocity.Y>=16 || velocity.Y< -16)velocity*=.97f;return velocity;}
        internal static bool Collides(Projectile p,MotionRect target)
        {
            float distance=0;var normal=p.velocity.SafeNormalize(Vector2.UnitY).RotatedBy(-1.5707963705062866)*p.scale;
            var box=new Rectangle((int)target.X,(int)target.Y,(int)target.Width,(int)target.Height);
            return Collision.CheckAABBvLineCollision(box.TopLeft(),box.Size(),p.Center-normal*40f,p.Center+normal*40f,16f*p.scale,ref distance);
        }
        private static void Kill(Projectile p){if(p.active){p.timeLeft=0;p.active=false;}}
        internal static bool Step(Projectile p,PredictionTerrain terrain)
        {
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y)){p.active=false;return true;}
            Vector2 wetVelocity=p.velocity;p.oldVelocity=p.velocity;
            if(p.active)
            {
                p.alpha=Math.Max(0,p.alpha-40);p.spriteDirection=p.direction;p.localAI[0]++;
                // AI001 flag3=false for684: no ai0 advance or gravity. Its
                // common positive-Y ceiling and visual rotation still run.
                if(p.velocity.Y>16)p.velocity.Y=16;p.rotation=p.velocity.ToRotation()+(float)Math.PI/2;
            }
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active)Kill(p);
            if(!p.ignoreWater)
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
                if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;
                if(p.wet && !wet && p.wetCount==0)p.wetCount=10;p.wet=wet;if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;if(p.wetCount>0)p.wetCount--;
            }
            if(p.shimmerWet)
            {
                int x=(int)(p.Center.X/16),y=(int)(p.position.Y/16);bool full=false;
                if(WorldGen.InWorld(x,y) && WorldGen.InWorld(x,y-1))
                {PredictionTile tile,above;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;if(tile.Liquid==255 && tile.ShimmerLiquid){if(!terrain.Tile(x,y-1,out above,out stop))return false;full=above.Liquid>0 && above.ShimmerLiquid;}}
                if(full)Kill(p);else if(p.velocity.Y>0){p.velocity.Y=-p.velocity.Y;if(p.timeLeft>600)p.timeLeft=600;p.timeLeft-=60;p.wet=p.shimmerWet=false;}
            }
            p.oldPosition=p.position;var position=p.position;var velocity=p.velocity;bool hit=false;
            // Actual684 never tiles. Preserve a captured tileCollide override
            // without inventing collisions for its ordinary native hand role.
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;
            p.position=position;p.velocity=velocity;if(hit){p.position+=velocity;Kill(p);}p.position+=p.wet?wetVelocity:p.velocity;p.direction=p.velocity.X<0?-1:1;return true;
        }
        internal static void Finish(Projectile p){if(p.active && (--p.timeLeft<=0 || p.penetrate==0))Kill(p);}
        private sealed class Member{internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source;internal bool Collision(MotionRect bounds){return Collides(Shot,bounds);}}
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {var list=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Handles(p.type))list.Add(p);var scene=list.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{684});return scene;}
        private static List<Member> Prepare(Projectile[] scene,Projectile[] source,HostAttackObstacles root)
        {var rows=new List<Member>();var sources=source==null?null:new HashSet<Projectile>(source);foreach(var p in scene){var copy=Copy(p);rows.Add(new Member{Shot=copy,Source=sources!=null && sources.Contains(p),Receive=root.ForkForSpawn(copy)});}rows.Sort((left,right)=>left.Shot.whoAmI.CompareTo(right.Shot.whoAmI));return rows;}
        private static bool Outer(List<Member> rows,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var m in rows)
            {
                var p=m.Shot;if(!p.active)continue;
                // Ordinary3827 supplies ai2=0. Do not silently interpret a
                // foreign feature-flag override as this no-feature AI001 motor.
                if(p.ai[2]!=0)return false;
                // NPC reflection tests the raw body before projectiles, never
                // the later transverse Damage capsule. Retire only that shot;
                // a reflected still-live native slot is not freed by this proof.
                if(!m.Receive.BeforeNpc(tick,outer,p.Center.X,p.Center.Y,p.width,p.height)){p.friendly=false;continue;}
                int updates=p.extraUpdates+1;for(int sub=0;sub<updates;sub++)
                {
                    if(!Step(p,terrain))return false;if(!p.active)break;
                    if(p.friendly && !p.hostile && p.damage>0)
                    {
                        int hits=m.Receive.SelectedHits;bool passed=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,m.Collision,consumeSelected:true,onHit:(slot,time)=>{p.numHits++;return true;});p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>hits){contact=AttackIntercept.ProjectileShapeContact(path,tick,sub,Collides(p,path[tick].ProjectileReceiveBounds),aim.X,aim.Y,p.Center.X,p.Center.Y,AttackConfidence.Conditional);return true;}
                        if(!passed && p.penetrate!=0){p.friendly=false;break;}
                    }
                    Finish(p);if(!p.active)break;
                }
            }
            return true;
        }
        private static Vector2 Cursor(Player player,Vector2 aim)
        {var pixel=aim-Main.screenPosition;if(player.gravDir==-1)pixel.Y=Main.screenHeight-pixel.Y;return new Vector2((int)pixel.X+Main.screenPosition.X,player.gravDir==-1?Main.screenPosition.Y+Main.screenHeight-(int)pixel.Y:(int)pixel.Y+Main.screenPosition.Y);}
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            if(ammo.Projectile!=684 || ammo.WeaponType!=3827 || ammo.Damage<=0 || delay<0 || age-delay<0 || age+1>=path.Count)return null;
            obstacles.CaptureShotFacing(player);var scene=Capture(player,obstacles);var pool=obstacles.CaptureBirthSlots();var inputs=new List<Vector2>();
            for(int seed=0;seed<41;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;var aim=seed<9?new Vector2(b.CenterX,b.CenterY):origin+((seed-9)*MathHelper.TwoPi/32).ToRotationVector2()*10000;var cursor=Cursor(player,aim);if(inputs.Contains(cursor))continue;inputs.Add(cursor);
                obstacles.BeginCandidate();var rows=Prepare(scene,null,obstacles);var slots=pool.Copy();bool valid=true;
                for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,path,terrain,age-delay+wait,wait,cursor,out ignored)){valid=false;break;}foreach(var m in rows)if(!m.Shot.active)slots.Release(m.Shot.whoAmI);}if(!valid)continue;
                // ItemCheck_Shoot first changes facing from the cursor/full
                // rotation dot, then offsets its point, then re-aims velocity
                // FROM that point. A stale pose direction or pre-offset vector
                // yields the wrong native birth even for a deterministic shot.
                int slot;if(!slots.TryAllocate(out slot))return null;int facing=ShotDirection(player,cursor-origin);var center=BirthOrigin(origin,cursor-origin,facing);var velocity=BirthVelocity(center,cursor,ammo.Speed,facing);
                var p=new Projectile();p.SetDefaults(684);p.active=true;p.owner=player.whoAmI;p.whoAmI=slot;p.Center=center;p.velocity=LimitLaunch(velocity);p.damage=ammo.Damage;p.knockBack=player.GetWeaponKnockback(player.HeldItem,player.HeldItem.knockBack);p.timeLeft=60;
                bool wet;byte kind;if(!terrain.ProjectileWet(center.X,center.Y,p.width,p.height,out wet,out kind,true))return null;p.wet=wet && !p.ignoreWater;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;
                rows.Add(new Member{Shot=p,Source=true,Receive=obstacles.ForkForSpawn(p)});rows.Sort((left,right)=>left.Shot.whoAmI.CompareTo(right.Shot.whoAmI));
                for(int step=1;step<=Math.Min(60,path.Count-age-1);step++){AttackContact result;if(!Outer(rows,path,terrain,age+step,delay+step,cursor,out result))break;if(result!=null)return result;}
            }
            return null;
        }
        // Existing autonomous shot oracle, not new control or a receipt. A
        // partial projectile-array epoch cannot restart every lower slot.
        internal static AttackContact Known(Player player,Projectile shot,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {return shot==null?null:KnownFamily(player,new[]{shot},path,clock,terrain,obstacles);}
        // The visible preview considers all already-born sources together.
        // One family capture and ordered replay owns their shared receiving
        // history; querying each source separately would repeat that work.
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(sources==null || sources.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;
            foreach(var shot in sources)if(shot==null || !shot.active || !Handles(shot.type) || shot.owner!=player.whoAmI || shot.ai[2]!=0)return null;
            var scene=Capture(player,obstacles);obstacles.BeginCandidate();var rows=Prepare(scene,sources,obstacles);var aim=sources[0].Center+sources[0].velocity*1000;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){AttackContact result;if(!Outer(rows,path,terrain,clock.FirstTick+step,step+1,aim,out result))return null;if(result!=null)return result;}return null;
        }
    }
}
