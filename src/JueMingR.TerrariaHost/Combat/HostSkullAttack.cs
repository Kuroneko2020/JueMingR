using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // 1313 emits one837 directly. Its AI001 slot lock and solid-center
    // lifetime tax are physical; neither the skull's frame dust nor its Kill
    // creates damage/children. Root owns the prepared hand pose and cursor.
    internal static class HostSkullAttack
    {
        internal static bool Handles(int type){return type==837;}
        internal static Projectile Copy(Projectile p)
        {var q=HostNebulaBlazeAttack.Copy(p);q.oldPosition=p.oldPosition;q.oldVelocity=p.oldVelocity;q.numHits=p.numHits;return q;}
        private static void Kill(Projectile p){if(p.active){p.timeLeft=0;p.active=false;}}
        private static bool Target(HostProjectileTargets targets,NpcTrajectory path,int tick,int index,out Vector2 center)
        {
            center=Vector2.Zero;if(targets.Slot(index)==path.Identity.Slot && !path[tick].CanReceive)return false;
            var box=targets.Bounds(index,path,tick);center=new Vector2(box.X+(int)box.Width/2,box.Y+(int)box.Height/2);return true;
        }
        private static bool Ai(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain)
        {
            p.ai[0]++;p.alpha=Math.Max(0,p.alpha-50);if(++p.frame>2)p.frame=0;
            if(p.timeLeft>33)
            {
                PredictionTile tile;PredictionStop stop;
                if(!terrain.Tile((int)(p.Center.X/16),(int)(p.Center.Y/16),out tile,out stop))return false;
                // WorldGen.SolidTile, not Collision.IsWorldPointSolid: half
                // bricks and EVERY slope are excluded even on their solid half.
                if(tile.Active && tile.RawSolid && !tile.SolidTop && !tile.Half && tile.Slope==0)p.timeLeft-=33;
            }
            float speed=(float)Math.Sqrt(p.velocity.X*p.velocity.X+p.velocity.Y*p.velocity.Y);
            if(p.localAI[0]==0)p.localAI[0]=speed;p.alpha=Math.Max(0,p.alpha-25);
            if(p.ai[1]==0)
            {
                float best=300;int slot=-1;
                for(int i=0;i<targets.Count;i++)
                {
                    Vector2 center;if(!Target(targets,path,tick,i,out center))continue;
                    float distance=Math.Abs(p.Center.X-center.X)+Math.Abs(p.Center.Y-center.Y);if(distance>=best)continue;
                    bool clear;PredictionStop stop;
                    if(!terrain.CanHit(new MotionRect(p.Center.X,p.Center.Y,1,1),targets.Bounds(i,path,tick),out clear,out stop))return false;
                    if(clear){best=distance;slot=targets.Slot(i);}
                }
                if(slot>=0)p.ai[1]=slot+1;
            }
            bool follows=false;Vector2 goal=Vector2.Zero;
            if(p.ai[1]>0)
            {
                bool valid=false;int slot=(int)(p.ai[1]-1);
                for(int i=0;i<targets.Count;i++)if(targets.Slot(i)==slot){valid=Target(targets,path,tick,i,out goal);break;}
                if(!valid)p.ai[1]=0;
                else follows=Math.Abs(p.Center.X-goal.X)+Math.Abs(p.Center.Y-goal.Y)<1000;
                // A live target outside1000 keeps the slot lock. Native only
                // clears it for failed chase qualification, not for distance
                // or a wall that appears AFTER acquisition.
            }
            if(p.friendly && follows)
            {
                float x=goal.X-p.Center.X,y=goal.Y-p.Center.Y;
                float scale=p.localAI[0]/(float)Math.Sqrt(x*x+y*y);x*=scale;y*=scale;
                p.velocity.X=(p.velocity.X*19+x)/20;p.velocity.Y=(p.velocity.Y*19+y)/20;
            }
            if(p.velocity.Y>16)p.velocity.Y=16;p.spriteDirection=p.direction;
            p.rotation=p.direction<0?(float)Math.Atan2(-p.velocity.Y,-p.velocity.X):(float)Math.Atan2(p.velocity.Y,p.velocity.X);return true;
        }
        internal static bool Step(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain)
        {
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y)){p.active=false;return true;}
            Vector2 wetVelocity=p.velocity;p.oldVelocity=p.velocity;if(p.active && !Ai(p,targets,path,tick,terrain))return false;
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
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;
            p.position=position;p.velocity=velocity;if(hit){p.position+=velocity;Kill(p);}p.position+=p.wet?wetVelocity:p.velocity;p.direction=p.velocity.X<0?-1:1;return true;
        }
        internal static void Finish(Projectile p){if(p.active && (--p.timeLeft<=0 || p.penetrate==0))Kill(p);}
        private sealed class Member{internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source;}
        private static Projectile[] Capture(Player p,HostAttackObstacles root)
        {var values=new List<Projectile>();foreach(var q in Main.projectile)if(q.active && q.owner==p.whoAmI && Handles(q.type))values.Add(q);var scene=values.ToArray();root.CaptureOwnedProjectileFamily(scene,p.whoAmI,new[]{837});return scene;}
        private static List<Member> Prepare(Projectile[] scene,Projectile[] sources,HostAttackObstacles root)
        {var source=new HashSet<Projectile>(sources??new Projectile[0]);var rows=new List<Member>();foreach(var q in scene){var copy=Copy(q);rows.Add(new Member{Shot=copy,Source=source.Contains(q),Receive=root.ForkForSpawn(copy)});}rows.Sort((left,right)=>left.Shot.whoAmI.CompareTo(right.Shot.whoAmI));return rows;}
        private static bool Outer(List<Member> rows,HostProjectileTargets targets,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var m in rows)
            {
                var p=m.Shot;if(!p.active || !p.friendly || p.hostile)continue;if(p.ai[2]!=0)return false;
                if(!m.Receive.BeforeNpc(tick,outer,p.Center.X,p.Center.Y,p.width,p.height)){p.friendly=false;continue;}
                int updates=p.extraUpdates+1;for(int sub=0;sub<updates;sub++)
                {
                    if(!Step(p,targets,path,tick,terrain))return false;if(!p.active)break;
                    if(p.damage>0)
                    {
                        int hits=m.Receive.SelectedHits;bool pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true,onHit:(slot,time)=>{p.numHits++;return true;});p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,AttackConfidence.Conditional);return true;}
                        if(!pass && p.penetrate!=0){p.friendly=false;break;}
                    }
                    Finish(p);if(!p.active)break;
                }
            }
            return true;
        }
        private static Vector2 Cursor(Player p,Vector2 aim)
        {var pixel=aim-Main.screenPosition;if(p.gravDir==-1)pixel.Y=Main.screenHeight-pixel.Y;return new Vector2((int)pixel.X+Main.screenPosition.X,p.gravDir==-1?Main.screenPosition.Y+Main.screenHeight-(int)pixel.Y:(int)pixel.Y+Main.screenPosition.Y);}
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(ammo.WeaponType!=1313 || ammo.Projectile!=837 || ammo.Damage<=0 || delay<0 || age-delay<0 || age+1>=path.Count)return null;
            root.CaptureShotFacing(player);var policy=new Projectile();policy.SetDefaults(837);policy.owner=player.whoAmI;
            var targets=root.CaptureChaseTargets(policy);var scene=Capture(player,root);var pool=root.CaptureBirthSlots();var inputs=new HashSet<Vector2>();
            for(int seed=0;seed<41;seed++)
            {
                var box=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;var candidate=seed<9?new Vector2(box.CenterX,box.CenterY):origin+((seed-9)*MathHelper.TwoPi/32).ToRotationVector2()*10000;
                var aim=Cursor(player,candidate);if(!inputs.Add(aim))continue;root.BeginCandidate();var rows=Prepare(scene,null,root);var slots=pool.Copy();bool valid=true;
                for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,targets,path,terrain,age-delay+wait,wait,aim,out ignored)){valid=false;break;}foreach(var m in rows)if(!m.Shot.active)slots.Release(m.Shot.whoAmI);}if(!valid)continue;
                int slot;if(!slots.TryAllocate(out slot))return null;var q=new Projectile();q.SetDefaults(837);q.active=true;q.owner=player.whoAmI;q.whoAmI=slot;q.Center=origin;q.velocity=HostSonicBoomAttack.LimitLaunch((aim-origin).SafeNormalize(new Vector2(player.direction,0))*ammo.Speed);q.damage=ammo.Damage;q.knockBack=player.HeldItem.knockBack;
                bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,q.width,q.height,out wet,out kind,true))return null;q.wet=wet && !q.ignoreWater;q.honeyWet=wet && kind==2;q.shimmerWet=wet && kind==3;
                rows.Add(new Member{Shot=q,Source=true,Receive=root.ForkForSpawn(q)});rows.Sort((left,right)=>left.Shot.whoAmI.CompareTo(right.Shot.whoAmI));
                for(int step=1;step<=Math.Min(120,path.Count-age-1);step++){AttackContact result;if(!Outer(rows,targets,path,terrain,age+step,delay+step,aim,out result))break;if(result!=null)return result;}
            }
            return null;
        }
        internal static AttackContact Known(Player p,Projectile shot,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {return shot==null?null:KnownFamily(p,new[]{shot},path,clock,terrain,root);}
        internal static AttackContact KnownFamily(Player p,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;foreach(var q in sources)if(q==null || !q.active || !Handles(q.type) || q.owner!=p.whoAmI || q.ai[2]!=0)return null;
            var targets=root.CaptureChaseTargets(sources[0]);var scene=Capture(p,root);root.BeginCandidate();var rows=Prepare(scene,sources,root);var aim=sources[0].Center+sources[0].velocity*1000;
            for(int action=0;action<Math.Min(120,path.Count-clock.FirstTick);action++){AttackContact result;if(!Outer(rows,targets,path,terrain,clock.FirstTick+action,action+1,aim,out result))return null;if(result!=null)return result;}return null;
        }
    }
}
