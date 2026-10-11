using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Testing;

namespace JueMingR.TerrariaHost.Combat
{
    // 3852 has two hand births, not a712->704 child. A burst's later712
    // births consume the stored item rotation; moving the cursor cannot steer
    // them. Root owns the native hand clock and any actual input authority.
    internal static class HostBookStaffAttack
    {
        internal static bool Handles(int type){return type==712 || type==704;}
        internal static int BirthType(Player p){return p.altFunctionUse==2?704:712;}
        internal static bool LockedDirection(Player p,int delay)
        {return !(p.itemAnimation<=0 || delay>=p.itemAnimation || delay==0 && p.ItemAnimationJustStarted);}
        internal static bool CursorInput(Player p,int delay)
        {return !LockedDirection(p,delay) || p.gravDir==-1;}
        internal static bool CanBirth(int animation,int maximum,int useTime,bool alt)
        {return animation>0 && useTime>0 && animation>=maximum-useTime*4 && (!alt || animation==maximum-1);}
        internal static int UseTime(Player p)
        {return p.itemAnimation>0 && p.itemTimeMax>0?p.itemTimeMax:Math.Max(1,DebugOptions.ManaV2 && p.slowMagicUse?(int)(p.HeldItem.useTime*p.GetSlowMagicScalar()):p.HeldItem.useTime);}
        internal static Projectile Copy(Projectile p)
        {var q=HostNebulaBlazeAttack.Copy(p);q.oldPosition=p.oldPosition;q.oldVelocity=p.oldVelocity;q.numHits=p.numHits;return q;}
        internal static Vector2 BirthVelocity(Player p,Vector2 origin,Vector2 cursor,float speed,bool locked)
        {
            var vector=locked?((p.itemRotation.ToRotationVector2()*p.direction).ToRotation()+p.fullRotation).ToRotationVector2():cursor-origin;
            float x=vector.X,y=vector.Y;
            // The native inverted-gravity assignment occurs AFTER replacing
            // the burst vector. Preserve that unusual real consumer as well.
            if(p.gravDir==-1)y=cursor.Y-origin.Y;
            if(x==0 && y==0)x=p.direction;float scale=speed/(float)Math.Sqrt(x*x+y*y);return new Vector2(x*scale,y*scale);
        }
        internal static void Birth(Player p,Vector2 origin,Vector2 cursor,float speed,bool locked,out Vector2 center,out Vector2 velocity)
        {
            if(BirthType(p)==704){center=new Vector2(origin.X,p.Bottom.Y-100f);velocity=new Vector2(p.direction*speed,0);}
            else{center=origin;velocity=BirthVelocity(p,origin,cursor,speed,locked);}
        }
        private static void Kill(Projectile p){if(p.active){p.timeLeft=0;p.active=false;}}
        internal static bool Step(Projectile p,PredictionTerrain terrain)
        {
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y)){p.active=false;return true;}
            var wetVelocity=p.velocity;p.oldVelocity=p.velocity;
            if(p.type==704)
            {
                if(p.localAI[0]>=16 && p.ai[0]<285)p.ai[0]=285;p.ai[0]++;if(p.ai[0]>=300)Kill(p);
                var probe=new MotionRect(p.Center.X-8,p.position.Y+p.height-160,16,160);bool solid,wet;byte kind;PredictionStop stop;
                if(!terrain.Solid(probe,out solid,out stop))return false;
                // Original short-circuits WetCollision when SolidCollision
                // already returned true; lease only the queries actually read.
                wet=false;if(!solid && !terrain.ProjectileWet(probe.CenterX,probe.CenterY,16,160,out wet,out kind,true))return false;
                if(solid || wet)
                {if(p.velocity.Y>0)p.velocity.Y=0;if(p.velocity.Y> -4)p.velocity.Y-=2;else{p.velocity.Y-=4;p.localAI[0]+=2;}if(p.velocity.Y< -16)p.velocity.Y=-16;}
                else
                {p.localAI[0]=Math.Max(0,p.localAI[0]-1);if(p.velocity.Y<0)p.velocity.Y=0;p.velocity.Y+=p.velocity.Y<4?2:4;if(p.velocity.Y>16)p.velocity.Y=16;}
            }
            else
            {
                // AI001's712-only random frame/lighting is visual. The only
                // physical writes are scale1.1, alpha0, positive-Y ceiling;
                // flag3=false prevents generic gravity/ai0 advancement.
                if(p.alpha>0){p.alpha=0;p.scale=1.1f;}if(p.velocity.Y>16)p.velocity.Y=16;p.rotation=p.velocity.ToRotation()+(float)Math.PI/2;
            }
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active)Kill(p);
            if(!p.ignoreWater)
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
                if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;
                if(p.wet && !wet && p.wetCount==0)p.wetCount=10;p.wet=wet;if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;if(p.wetCount>0)p.wetCount--;
            }
            if(p.aiStyle==1 && p.shimmerWet)
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
        {var values=new List<Projectile>();foreach(var q in Main.projectile)if(q.active && q.owner==p.whoAmI && Handles(q.type))values.Add(q);var scene=values.ToArray();root.CaptureOwnedProjectileFamily(scene,p.whoAmI,new[]{712,704});return scene;}
        private static List<Member> Prepare(Projectile[] scene,Projectile[] sources,HostAttackObstacles root)
        {var source=new HashSet<Projectile>(sources??new Projectile[0]);var rows=new List<Member>();foreach(var q in scene){var copy=Copy(q);rows.Add(new Member{Shot=copy,Source=source.Contains(q),Receive=root.ForkForSpawn(copy)});}rows.Sort((left,right)=>left.Shot.whoAmI.CompareTo(right.Shot.whoAmI));return rows;}
        private static bool Outer(List<Member> rows,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var m in rows)
            {
                var p=m.Shot;if(!p.active)continue;if(p.type==712 && p.ai[2]!=0)return false;
                if(!m.Receive.BeforeNpc(tick,outer,p.Center.X,p.Center.Y,p.width,p.height)){p.friendly=false;continue;}
                int updates=p.extraUpdates+1;for(int sub=0;sub<updates;sub++)
                {
                    if(!Step(p,terrain))return false;if(!p.active)break;
                    if(p.friendly && !p.hostile && p.damage>0)
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
        // Root supplies the already-prepared launch pose/origin. Only the next
        // actually legal hand birth is added. Later burst stages are evaluated
        // when their real native hand clock becomes ready, not fabricated as
        // free future mana/input/owner updates by an autonomous flight kernel.
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(ammo.WeaponType!=3852 || !Handles(ammo.Projectile) || ammo.Damage<=0 || delay<0 || age-delay<0 || age+1>=path.Count)return null;
            bool locked=LockedDirection(player,delay),input=CursorInput(player,delay);int type=BirthType(player),useTime=UseTime(player);
            if(locked && !CanBirth(player.itemAnimation-delay,player.itemAnimationMax,useTime,type==704))return null;
            root.CaptureShotFacing(player);root.CaptureBookHand(player,locked);var scene=Capture(player,root);var pool=root.CaptureBirthSlots();var inputs=new HashSet<Vector2>();
            for(int seed=0;seed<(input?41:1);seed++)
            {
                var box=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;var candidate=seed<9?new Vector2(box.CenterX,box.CenterY):origin+((seed-9)*MathHelper.TwoPi/32).ToRotationVector2()*10000;
                var aim=Cursor(player,input?candidate:origin+BirthVelocity(player,origin,origin+Vector2.UnitX,ammo.Speed,true)*1000);if(!inputs.Add(aim))continue;
                root.BeginCandidate();var rows=Prepare(scene,null,root);var slots=pool.Copy();bool valid=true;
                for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,path,terrain,age-delay+wait,wait,aim,out ignored)){valid=false;break;}foreach(var m in rows)if(!m.Shot.active)slots.Release(m.Shot.whoAmI);}if(!valid)continue;
                int slot;if(!slots.TryAllocate(out slot))return null;Vector2 center,velocity;Birth(player,origin,aim,ammo.Speed,locked,out center,out velocity);
                if(type==704 && input)velocity.X=(Vector2.Dot(Vector2.UnitX.RotatedBy(player.fullRotation),(aim-origin).SafeNormalize(Vector2.Zero))>0?1:-1)*ammo.Speed;
                var q=new Projectile();q.SetDefaults(type);q.active=true;q.owner=player.whoAmI;q.whoAmI=slot;q.Center=center;q.velocity=type==712?HostSonicBoomAttack.LimitLaunch(velocity):velocity;q.damage=type==704?(int)((float)ammo.Damage*1.75f):ammo.Damage;q.knockBack=player.HeldItem.knockBack;
                bool wet;byte kind;if(!terrain.ProjectileWet(center.X,center.Y,q.width,q.height,out wet,out kind,true))return null;q.wet=wet && !q.ignoreWater;q.honeyWet=wet && kind==2;q.shimmerWet=wet && kind==3;
                rows.Add(new Member{Shot=q,Source=true,Receive=root.ForkForSpawn(q)});rows.Sort((left,right)=>left.Shot.whoAmI.CompareTo(right.Shot.whoAmI));
                for(int step=1;step<=Math.Min(120,path.Count-age-1);step++){AttackContact result;if(!Outer(rows,path,terrain,age+step,delay+step,aim,out result))break;if(result!=null)return result;}
            }
            return null;
        }
        internal static AttackContact Known(Player p,Projectile shot,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {return shot==null?null:KnownFamily(p,new[]{shot},path,clock,terrain,root);}
        internal static AttackContact KnownFamily(Player p,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;foreach(var q in sources)if(q==null || !q.active || !Handles(q.type) || q.owner!=p.whoAmI || q.type==712 && q.ai[2]!=0)return null;
            var scene=Capture(p,root);root.BeginCandidate();var rows=Prepare(scene,sources,root);var aim=sources[0].Center+sources[0].velocity*1000;
            for(int action=0;action<Math.Min(120,path.Count-clock.FirstTick);action++){AttackContact result;if(!Outer(rows,path,terrain,clock.FirstTick+action,action+1,aim,out result))return null;if(result!=null)return result;}return null;
        }
    }
}
