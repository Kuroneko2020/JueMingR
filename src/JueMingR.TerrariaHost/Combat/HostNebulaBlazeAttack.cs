using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // 635 is an alternative native hand birth, not a child of a flying634.
    // Both bodies acquire the first qualifying NPC slot (not the nearest)
    // and Kill synchronously damages their SAME body after resizing to50.
    internal static class HostNebulaBlazeAttack
    {
        internal static bool Handles(int type){return type==634;}
        private static bool Family(int type){return type==634 || type==635;}
        internal static Projectile Copy(Projectile p)
        {
            var q=new Projectile();q.SetDefaults(p.type);q.active=p.active;q.whoAmI=p.whoAmI;q.owner=p.owner;q.position=p.position;q.velocity=p.velocity;q.width=p.width;q.height=p.height;q.scale=p.scale;q.rotation=p.rotation;q.direction=p.direction;q.spriteDirection=p.spriteDirection;
            q.damage=p.damage;q.knockBack=p.knockBack;q.timeLeft=p.timeLeft;q.extraUpdates=p.extraUpdates;q.alpha=p.alpha;q.penetrate=p.penetrate;q.maxPenetrate=p.maxPenetrate;q.frame=p.frame;q.frameCounter=p.frameCounter;
            q.friendly=p.friendly;q.hostile=p.hostile;q.magic=p.magic;q.ranged=p.ranged;q.melee=p.melee;q.npcProj=p.npcProj;q.trap=p.trap;q.tileCollide=p.tileCollide;q.ignoreWater=p.ignoreWater;q.correctSlopeCollision=p.correctSlopeCollision;
            q.wet=p.wet;q.honeyWet=p.honeyWet;q.lavaWet=p.lavaWet;q.shimmerWet=p.shimmerWet;q.wetCount=p.wetCount;
            q.usesLocalNPCImmunity=p.usesLocalNPCImmunity;q.localNPCHitCooldown=p.localNPCHitCooldown;q.usesIDStaticNPCImmunity=p.usesIDStaticNPCImmunity;q.immunityIdentity=p.immunityIdentity;q.idStaticNPCHitCooldown=p.idStaticNPCHitCooldown;q.appliesImmunityTimeOnSingleHits=p.appliesImmunityTimeOnSingleHits;
            Array.Copy(p.ai,q.ai,p.ai.Length);Array.Copy(p.localAI,q.localAI,p.localAI.Length);Array.Copy(p.localNPCImmunity,q.localNPCImmunity,p.localNPCImmunity.Length);return q;
        }
        internal static Vector2 BirthVelocity(Vector2 direction,float baseSpeed,int type,float angle=0,float scale=1.1f)
        {
            if(!Family(type) || angle<-(float)Math.PI/8*.7f || angle>=(float)Math.PI/8*.7f || scale<.95f || scale>=1.25f)return new Vector2(float.NaN);
            var v=direction.SafeNormalize(Vector2.UnitX)*(baseSpeed-(type==634?1f:0f));v=v.RotatedBy(angle)*scale;
            while(v.X>=16 || v.X<=-16 || v.Y>=16 || v.Y< -16)v*=.97f;return v;
        }
        internal static void Kill(Projectile p){KillCore(p,null);}
        private static void KillCore(Projectile p,Action onKill)
        {
            if(!p.active)return;p.timeLeft=0;p.position=p.Center;p.width=p.height=50;p.Center=p.position;p.maxPenetrate=p.penetrate=-1;
            if(onKill!=null)onKill();p.active=false;
        }
        private static bool CenterAt(HostProjectileTargets targets,NpcTrajectory path,int tick,int index,out Vector2 center)
        {center=Vector2.Zero;if(targets.Slot(index)==path.Identity.Slot && !path[tick].CanReceive)return false;var b=targets.Bounds(index,path,tick);center=new Vector2(b.X+(int)b.Width/2,b.Y+(int)b.Height/2);return true;}
        private static bool TargetAt(HostProjectileTargets targets,NpcTrajectory path,int tick,int slot,out Vector2 center)
        {center=Vector2.Zero;for(int i=0;i<targets.Count;i++)if(targets.Slot(i)==slot)return CenterAt(targets,path,tick,i,out center);return false;}
        private static bool Ai(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain,Action onKill)
        {
            if(p.ai[1]==0){p.ai[1]=1;p.localAI[0]=0;} // A legal visual-only -Next(48) representative.
            else if(p.ai[1]==1 && p.owner==Main.myPlayer)
            {
                int found=-1;float distance=p.type==635?500:250;
                for(int i=0;i<targets.Count;i++)
                {
                    Vector2 center;if(!CenterAt(targets,path,tick,i,out center))continue;float d=Vector2.Distance(center,p.Center);
                    if(d>=distance || found!=-1)continue;bool clear;PredictionStop stop;
                    if(!terrain.CanHitLine(new MotionRect(p.Center.X,p.Center.Y,1,1),new MotionRect(center.X,center.Y,1,1),out clear,out stop))return false;
                    if(clear){distance=d;found=targets.Slot(i);}
                }
                if(distance<20){KillCore(p,onKill);return true;}
                if(found!=-1){p.ai[1]=6;p.ai[0]=found;}
            }
            else if(p.ai[1]>5)
            {
                p.ai[1]++;Vector2 center;
                if(!TargetAt(targets,path,tick,(int)p.ai[0],out center)){p.ai[1]=1;p.ai[0]=0;}
                else
                {
                    var desired=center-p.Center;if(desired.Length()<20){KillCore(p,onKill);return true;}
                    if(desired!=Vector2.Zero){desired.Normalize();desired*=6;}p.velocity=(p.velocity*29+desired)/30;
                }
            }
            if(p.ai[1]>=1 && p.ai[1]<5){p.ai[1]++;if(p.ai[1]==5)p.ai[1]=1;}
            p.alpha=Math.Max(0,p.alpha-40);p.spriteDirection=p.direction;p.frameCounter++;
            if(p.frameCounter>=(p.type==635?3:4)*(p.extraUpdates+1)){p.frameCounter=0;if(++p.frame>=4)p.frame=0;}
            p.localAI[0]++;if(p.localAI[0]==48)p.localAI[0]=0;
            // The family block's ToRotation is overwritten by the common
            // AI001 tail (58540+). Kill's early return skips this tail.
            p.rotation=(float)Math.Atan2(p.velocity.Y,p.velocity.X)+1.57f;
            if(p.velocity.Y>16)p.velocity.Y=16;return true;
        }
        internal static bool Step(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain)
        {return !p.active || Motor(p,targets,path,tick,terrain,null);}
        internal static bool RetiredStep(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain)
        {return Motor(p,targets,path,tick,terrain,null);}
        private static bool Motor(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain,Action onKill)
        {
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y)){p.active=false;return true;}
            Vector2 wetVelocity=p.velocity;if(p.active && !Ai(p,targets,path,tick,terrain,onKill))return false;
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active)KillCore(p,onKill);
            if(!p.ignoreWater)
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
                if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;if(p.wet && !wet && p.wetCount==0)p.wetCount=10;p.wet=wet;
                if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;if(p.wetCount>0)p.wetCount--;
            }
            // Both roles are AI001, so the generic Shimmer AI1 branch
            // applies when the captured shimmer bit is set, even for634 if
            // NewProjectile initialized that bit before ignoreWater cleared wet.
            if(p.shimmerWet)
            {
                int x=(int)(p.Center.X/16),y=(int)(p.position.Y/16);bool full=false;
                if(WorldGen.InWorld(x,y) && WorldGen.InWorld(x,y-1)){PredictionTile tile,above;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;if(tile.Liquid==255 && tile.ShimmerLiquid){if(!terrain.Tile(x,y-1,out above,out stop))return false;full=above.Liquid>0 && above.ShimmerLiquid;}}
                if(full)KillCore(p,onKill);else if(p.velocity.Y>0){p.velocity.Y=-p.velocity.Y;if(p.timeLeft>600)p.timeLeft=600;p.timeLeft-=60;p.wet=p.shimmerWet=false;}
            }
            Vector2 position=p.position,velocity=p.velocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;
            p.position=position;p.velocity=velocity;if(hit){p.position+=velocity;KillCore(p,onKill);}p.position+=p.wet?wetVelocity:velocity;p.direction=p.velocity.X<0?-1:1;return true;
        }
        internal static void Finish(Projectile p){Tail(p,null);}
        private static void Tail(Projectile p,Action onKill){p.timeLeft--;if(p.timeLeft<=0 || p.penetrate==0)KillCore(p,onKill);}
        private sealed class Member
        {
            internal Projectile Shot;internal HostAttackObstacles Receive,BurstReceive;internal bool Source,Representative,Reflected;
            internal NpcTrajectory Path;internal int Tick,Outer,Sub;internal Vector2 Aim;internal AttackContact KillContact;internal readonly Action OnKill;
            private void Killed(){Receive=BurstReceive;Receive?.MakeUnlimited();KillContact=HostNebulaBlazeAttack.Receive(this,Path,Tick,Outer,Sub,Aim);Reflected=false;}
            internal Member(){OnKill=Killed;}
        }
        private static Member MakeMember(Projectile q,HostAttackObstacles root,bool source,bool representative=false)
        {
            // maxPen1 flight ignores pre-existing owner immunity. Kill sets
            // maxPen=-1 before Damage and restores that gate. Capture both
            // receive policies once; their owner history is still shared.
            HostAttackObstacles receive=null,burst=null;if(q.owner==Main.myPlayer){receive=root.ForkForSpawn(q);var policy=Copy(q);policy.maxPenetrate=policy.penetrate=-1;burst=root.ForkForSpawn(policy);}
            return new Member{Shot=q,Source=source,Representative=representative,Receive=receive,BurstReceive=burst};
        }
        private static Projectile[] Capture(HostAttackObstacles root)
        {var rows=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && Family(p.type))rows.Add(p);var scene=rows.ToArray();root.CaptureProjectileFamily(scene,634,635);return scene;}
        private static List<Member> Prepare(Projectile[] scene,Projectile[] source,HostAttackObstacles root)
        {
            var rows=new List<Member>();foreach(var p in scene){bool own=false;if(source!=null)foreach(var s in source)if(ReferenceEquals(p,s)){own=true;break;}var q=Copy(p);rows.Add(MakeMember(q,root,own));}rows.Sort((left,right)=>left.Shot.whoAmI.CompareTo(right.Shot.whoAmI));return rows;
        }
        private static AttackContact Receive(Member m,NpcTrajectory path,int tick,int outer,int sub,Vector2 aim)
        {
            var p=m.Shot;if(m.Receive==null || p.damage<=0 || !p.friendly || p.hostile)return null;int hits=m.Receive.SelectedHits;
            bool passed=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true);p.penetrate=m.Receive.Remaining;
            if(m.Source && m.Receive.SelectedHits>hits)return AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);
            if(!passed && p.penetrate!=0){p.active=false;m.Reflected=true;}return null;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,HostProjectileTargets targets,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,bool beforeNpc,Vector2 aim,out AttackContact contact)
        {
            contact=null;if(beforeNpc)foreach(var m in rows)if(m.Shot.active && m.Receive!=null && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height)){m.Shot.active=false;m.Reflected=true;}
            foreach(var m in rows)
            {
                var p=m.Shot;if(!p.active)continue;int updates=p.extraUpdates+1;
                for(int sub=0;sub<updates;sub++)
                {
                    m.Path=path;m.Tick=tick;m.Outer=outer;m.Sub=sub;m.Aim=aim;m.KillContact=null;
                    if(!Motor(p,targets,path,tick,terrain,m.OnKill))return false;contact=m.KillContact;if(contact!=null)return true;
                    if(!p.active){if(!m.Reflected)slots.Release(p.whoAmI);break;}
                    contact=Receive(m,path,tick,outer,sub,aim);if(contact!=null)return true;if(m.Reflected)break;
                    Tail(p,m.OnKill);contact=m.KillContact;if(contact!=null)return true;
                    if(!p.active && sub+1<updates && !RetiredStep(p,targets,path,tick,terrain))return false;
                    if(!p.active){if(!m.Reflected)slots.Release(p.whoAmI);break;}
                }
            }
            return true;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            if(ammo.Projectile!=634 || ammo.WeaponType!=3542 || ammo.Damage<=0 || delay<0 || age-delay<0 || age+1>=timeline.Count)return null;
            obstacles.CaptureShotFacing(player);var policy=new Projectile();policy.SetDefaults(634);policy.owner=player.whoAmI;var targets=obstacles.CaptureChaseTargets(policy);var scene=Capture(obstacles);var pool=obstacles.CaptureBirthSlots();var directions=new Vector2[41];int count=0;
            for(int seed=0;seed<41;seed++)
            {
                var b=timeline[Math.Min(timeline.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;Vector2 direction=seed<9?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;
                var aim=origin+direction*10000;var screen=aim-Main.screenPosition;if(player.gravDir==-1)screen.Y=Main.screenHeight-screen.Y;var cursor=new Vector2((int)screen.X+Main.screenPosition.X,player.gravDir==-1?Main.screenPosition.Y+Main.screenHeight-(int)screen.Y:(int)screen.Y+Main.screenPosition.Y);direction=(cursor-origin).SafeNormalize(Vector2.UnitX*player.direction);
                // Each trial is ONE legal future birth, never a fictitious
                // simultaneous normal/super shot. Neither trial draws RNG.
                for(int variant=0;variant<2;variant++)
                {
                    int type=634+variant;
                    obstacles.BeginCandidate();var rows=Prepare(scene,null,obstacles);var slots=pool.Copy();bool valid=true;
                    for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,targets,timeline,terrain,age-delay+wait,wait,true,aim,out ignored)){valid=false;break;}}if(!valid)continue;
                    int slot;if(!slots.TryAllocate(out slot))return null;var p=new Projectile();p.SetDefaults(type);p.active=true;p.whoAmI=slot;p.owner=player.whoAmI;p.Center=origin;p.velocity=BirthVelocity(direction,ammo.Speed,type);p.damage=type==635?ammo.Damage*3:ammo.Damage;p.knockBack=player.HeldItem.knockBack;
                    bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,p.width,p.height,out wet,out kind,true))return null;p.wet=wet && !p.ignoreWater;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;
                    rows.Add(MakeMember(p,obstacles,true,true));rows.Sort((left,right)=>left.Shot.whoAmI.CompareTo(right.Shot.whoAmI));
                    for(int step=1;step<=Math.Min(120,timeline.Count-age-1);step++){AttackContact contact;if(!Outer(rows,slots,targets,timeline,terrain,age+step,delay+step,true,aim,out contact))break;if(contact!=null)return contact;}
                }
            }
            return null;
        }
        // Observed autonomous input is an oracle/observation only. A partial
        // projectile array has no slot epoch, so this whole-array replay
        // cannot borrow it or grant another hand use/control receipt.
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(source==null || source.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;foreach(var p in source)if(p==null || !p.active || !Family(p.type) || p.owner!=player.whoAmI)return null;
            var scene=Capture(obstacles);var targets=obstacles.CaptureChaseTargets(source[0]);obstacles.BeginCandidate();var rows=Prepare(scene,source,obstacles);var slots=obstacles.CaptureBirthSlots().Copy();var aim=source[0].Center+source[0].velocity*1000;
            for(int step=0;step<Math.Min(120,timeline.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,slots,targets,timeline,terrain,clock.FirstTick+step,step+1,true,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
