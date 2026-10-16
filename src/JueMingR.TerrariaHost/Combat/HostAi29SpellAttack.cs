using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // AI29 is not a straight-shot contract: Serpent's dying parent births
    // finite bouncing splinters, whereas Thunder has a random clock jump on
    // each odd step. They share slot/history machinery, not an invented AI.
    internal static class HostAi29SpellAttack
    {
        internal static bool Handles(int type){return type==521 || type==731;}
        private static bool Family(int type){return Handles(type) || type==522 || type==620;}
        internal static Projectile Copy(Projectile p){return HostNoteAttack.Copy(p);}
        internal static Vector2 ChildVelocity(int x,int y,int speedTenths)
        {
            if(x< -100 || x>100 || y< -100 || y>100 || x==0 && y==0 || speedTenths<70 || speedTenths>100)return new Vector2(float.NaN);
            var v=new Vector2(x,y);v.Normalize();return v*(speedTenths*.1f);
        }
        internal static Rectangle DamageBox(Projectile p){var b=p.Hitbox;b.Inflate(4,4);return b;}
        private static void Kill(Projectile p,Action onKill)
        {if(!p.active)return;p.timeLeft=0;if(onKill!=null)onKill();p.active=false;}
        private static void Ai(Projectile p,bool skipClock,Action onKill)
        {
            if(p.type==521)p.alpha=Math.Max(0,p.alpha-50);
            else if(p.type==522 || p.type==620)
            {
                p.ai[1]++;if(p.ai[1]>40)Kill(p,onKill);
                // Native AI continues these velocity writes even after Kill.
                p.velocity.Y=Math.Min(18,p.velocity.Y+.2f);p.velocity.X*=.98f;
            }
            else
            {
                if(++p.frameCounter>=4){p.frameCounter=0;if(++p.frame>=Main.projFrames[p.type])p.frame=0;}
                p.alpha=Math.Max(0,p.alpha-15);p.ai[0]++;
                if((int)p.ai[0]%2!=0 && skipClock)p.ai[0]++;
                switch((int)p.ai[0]){case 10:p.velocity.Y-=5;break;case 12:p.velocity.Y+=5;break;case 18:p.velocity.Y+=5;break;case 20:p.velocity.Y-=5;p.ai[0]=0;break;}
            }
        }
        // skipClock is a legal native Next(4)==0 result, supplied by an
        // actual oracle trace or a declared representative; never draw RNG.
        internal static bool Step(Projectile p,PredictionTerrain terrain,bool skipClock)
        {return !p.active || Family(p.type) && p.aiStyle==29 && Motor(p,terrain,skipClock,null);}
        internal static bool RetiredStep(Projectile p,PredictionTerrain terrain)
        {return Motor(p,terrain,false,null);}
        private static bool Motor(Projectile p,PredictionTerrain terrain,bool skipClock,Action onKill)
        {
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y)){p.active=false;return true;}
            var wetVelocity=p.velocity;p.oldVelocity=p.velocity;if(p.active)Ai(p,skipClock,onKill);
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active)Kill(p,onKill);
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
                // AI29 reflects downwards without AI1's lifetime subtraction.
                if(full)Kill(p,onKill);else if(p.velocity.Y>0){p.velocity.Y=-p.velocity.Y;p.wet=p.shimmerWet=false;}
            }
            p.oldPosition=p.position;var previous=p.velocity;var position=p.position;var velocity=p.velocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;
            p.position=position;p.velocity=velocity;
            if(hit)
            {
                if(p.type==522 || p.type==620){if(p.velocity.X!=previous.X)p.velocity.X=-previous.X;if(p.velocity.Y!=previous.Y)p.velocity.Y=-previous.Y;}
                else{p.position+=p.velocity;Kill(p,onKill);}
            }
            p.position+=p.wet?wetVelocity:p.velocity;p.direction=p.velocity.X<0?-1:1;return true;
        }
        internal static void Finish(Projectile p){Tail(p,null);}
        private static void Tail(Projectile p,Action onKill)
        {if(p.active && (--p.timeLeft<=0 || p.penetrate==0))Kill(p,onKill);}
        private sealed class Member
        {
            internal Projectile Shot;internal HostAttackObstacles Receive;
            internal bool Source,Random,Reflected,Available;internal List<Member> Rows;
            internal HostProjectileBirthSlots Slots;internal PredictionTerrain Terrain;
            internal Vector2 Aim;internal readonly Action OnKill;
            internal Member(){OnKill=Killed;}
            private void Killed()
            {
                if(Shot.type!=521 || Shot.owner!=Main.myPlayer)return;
                // Any random sibling can change shared owner writes and
                // occupancy. A source then cannot keep Conditional precision.
                foreach(var m in Rows)if(m.Source)m.Random=true;
                var direction=(Aim-(Shot.oldPosition+new Vector2(Shot.width/2,Shot.height/2))).SafeNormalize(Vector2.UnitX);
                for(int i=0;i<3;i++)
                {
                    int slot;if(!Slots.TryAllocate(out slot)){Available=false;return;}
                    var d=direction.RotatedBy((i-1)*MathHelper.PiOver4);int x=(int)Math.Round(d.X*100),y=(int)Math.Round(d.Y*100);
                    var q=new Projectile();q.SetDefaults(522);q.active=true;q.whoAmI=slot;q.owner=Shot.owner;q.Center=Shot.oldPosition+new Vector2(Shot.width/2,Shot.height/2);q.velocity=ChildVelocity(x,y,80);q.damage=(int)((double)Shot.damage*.8);q.knockBack=Shot.knockBack*.8f;
                    bool wet;byte kind;if(!Terrain.ProjectileWet(q.Center.X,q.Center.Y,q.width,q.height,out wet,out kind,true)){Available=false;return;}q.wet=wet;q.honeyWet=wet && kind==2;q.shimmerWet=wet && kind==3;
                    Rows.Add(Make(q,Receive,Source,true));
                }
            }
        }
        private static Member Make(Projectile p,HostAttackObstacles root,bool source,bool random)
        {return new Member{Shot=p,Receive=p.owner==Main.myPlayer?root.ForkForSpawn(p):null,Source=source,Random=random || p.type==731};}
        private static Projectile[] Capture(HostAttackObstacles root,int type)
        {
            var rows=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && (type==731?p.type==731:p.type==521 || p.type==522))rows.Add(p);
            var scene=rows.ToArray();root.CaptureProjectileFamily(scene,type==731?731:521,type==731?731:522);return scene;
        }
        private static List<Member> Prepare(Projectile[] scene,Projectile[] source,HostAttackObstacles root)
        {
            var rows=new List<Member>();foreach(var p in scene){bool own=false;if(source!=null)foreach(var q in source)if(ReferenceEquals(p,q)){own=true;break;}rows.Add(Make(Copy(p),root,own,false));}return rows;
        }
        private static AttackContact Receive(Member m,NpcTrajectory path,int tick,int outer,int sub,Vector2 aim)
        {
            var p=m.Shot;if(m.Receive==null || !p.friendly || p.hostile || p.damage<=0)return null;var box=DamageBox(p);int hits=m.Receive.SelectedHits;
            bool passed=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,b=>box.Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height)),consumeSelected:true);p.penetrate=m.Receive.Remaining;
            if(!passed && p.penetrate!=0){p.active=false;m.Reflected=true;}
            return m.Source && m.Receive.SelectedHits>hits?AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,box.Center.X,box.Center.Y,box.Width,box.Height,m.Random?AttackConfidence.Representative:AttackConfidence.Conditional):null;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,Vector2 aim,bool skipClock,Func<Projectile,bool> clock,out AttackContact contact)
        {
            contact=null;
            foreach(var m in rows)if(m.Shot.active && m.Receive!=null && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height)){m.Shot.active=false;m.Reflected=true;}
            int after=-1;
            for(int visit=0;visit<1000;visit++)
            {
                Member m=null;foreach(var q in rows)if(q.Shot.active && q.Shot.whoAmI>after && (m==null || q.Shot.whoAmI<m.Shot.whoAmI))m=q;if(m==null)return true;var p=m.Shot;after=p.whoAmI;
                int updates=p.extraUpdates+1;
                for(int sub=0;sub<updates;sub++)
                {
                    m.Rows=rows;m.Slots=slots;m.Terrain=terrain;m.Aim=aim;m.Available=true;
                    bool skip=p.type==731 && (clock==null?skipClock:clock(p));
                    if(!Motor(p,terrain,skip,m.OnKill) || !m.Available)return false;bool tailDeath=false;
                    if(p.active){contact=Receive(m,path,tick,outer,sub,aim);if(contact!=null)return true;if(m.Reflected)break;Tail(p,m.OnKill);tailDeath=!p.active;if(!m.Available)return false;}
                    if(!p.active){if(!m.Reflected){if(tailDeath && sub+1<updates && !RetiredStep(p,terrain))return false;slots.Release(p.whoAmI);}break;}
                }
            }
            return false;
        }
        private static Vector2 Cursor(Player player,Vector2 aim)
        {var pixel=aim-Main.screenPosition;if(player.gravDir==-1)pixel.Y=Main.screenHeight-pixel.Y;return new Vector2((int)pixel.X+Main.screenPosition.X,player.gravDir==-1?Main.screenPosition.Y+Main.screenHeight-(int)pixel.Y:(int)pixel.Y+Main.screenPosition.Y);}
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            if(!Handles(ammo.Projectile) || ammo.Damage<=0 || delay<0 || age-delay<0 || age+1>=path.Count)return null;
            obstacles.CaptureShotFacing(player);var scene=Capture(obstacles,ammo.Projectile);var pool=obstacles.CaptureBirthSlots();var inputs=new Vector2[41];int count=0;
            for(int seed=0;seed<41;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;var aim=seed<9?new Vector2(b.CenterX,b.CenterY):origin+((seed-9)*MathHelper.TwoPi/32).ToRotationVector2()*10000;var cursor=Cursor(player,aim);bool duplicate=false;for(int i=0;i<count;i++)if(inputs[i]==cursor){duplicate=true;break;}if(duplicate)continue;inputs[count++]=cursor;
                for(int branch=0;branch<(ammo.Projectile==731?2:1);branch++)
                {
                    obstacles.BeginCandidate();var rows=Prepare(scene,null,obstacles);var slots=pool.Copy();bool valid=true;
                    for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,path,terrain,age-delay+wait,wait,cursor,branch==1,null,out ignored)){valid=false;break;}}if(!valid)continue;
                    int slot;if(!slots.TryAllocate(out slot))return null;var p=new Projectile();p.SetDefaults(ammo.Projectile);p.active=true;p.whoAmI=slot;p.owner=player.whoAmI;p.Center=origin;p.velocity=(cursor-origin).SafeNormalize(-Vector2.UnitX)*ammo.Speed;p.damage=ammo.Damage;p.knockBack=player.HeldItem.knockBack;
                    if(p.type==521)
                    {
                        // Full Shimmer can Kill before Update stores oldPosition.
                        // NewProjectile retains this field only when pooling. A
                        // slot released during private preroll has its private
                        // latest value, never a future value read from live.
                        Vector2 retained;if(!slots.TryRetainedPosition(slot,out retained))return null;p.oldPosition=retained;
                        if(slots.PoolsInstances)foreach(var prior in rows)if(prior.Shot.whoAmI==slot)p.oldPosition=prior.Shot.oldPosition;
                    }
                    bool wet;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true))return null;p.wet=wet;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;rows.Add(Make(p,obstacles,true,ammo.Projectile==731));
                    for(int step=1;step<=Math.Min(120,path.Count-age-1);step++){AttackContact contact;if(!Outer(rows,slots,path,terrain,age+step,delay+step,cursor,branch==1,null,out contact))break;if(contact!=null)return contact;}
                }
            }
            return null;
        }
        private static AttackContact Replay(Player player,Projectile[] source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles,bool skipClock,Func<Projectile,bool> trace)
        {
            var scene=Capture(obstacles,source[0].type);obstacles.BeginCandidate();var rows=Prepare(scene,source,obstacles);var slots=obstacles.CaptureBirthSlots().Copy();var aim=source[0].Center+source[0].velocity*1000;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,slots,path,terrain,clock.FirstTick+step,step+1,aim,skipClock,trace,out contact))return null;if(contact!=null)return contact;}return null;
        }
        // A captured Thunder still has unknown future clock draws; its legal
        // future remains Representative. Observed splinters are Conditional,
        // unless a not-yet-born parent's random death influences their history.
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(source==null || source.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;foreach(var p in source)if(p==null || !p.active || !Family(p.type) || p.owner!=player.whoAmI || (p.type==731)!=(source[0].type==731))return null;
            var contact=Replay(player,source,path,clock,terrain,obstacles,false,null);return contact ?? (source[0].type==731?Replay(player,source,path,clock,terrain,obstacles,true,null):null);
        }
        // Only an explicit actual per-subupdate clock trace can compare a
        // later Thunder contact exactly. This oracle conveys no input right.
        internal static AttackContact KnownClock(Player player,Projectile source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles,bool[] skips)
        {
            if(source==null || !source.active || source.type!=731 || source.owner!=player.whoAmI || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles || skips==null)return null;
            int index=0;bool missing=false;Func<Projectile,bool> trace=p=>{if(index>=skips.Length){missing=true;return false;}return skips[index++];};var contact=Replay(player,new[]{source},path,clock,terrain,obstacles,false,trace);return missing?null:contact;
        }
    }
}
