using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // The hand owns one dart's launch. Random AI births/retargets are finite
    // legal representatives, never live RNG draws. Spawned members share the
    // parent's receiver history and native slot order, not a damage world.
    internal static class HostDartAttack
    {
        internal static bool Handles(int type){return type==477 || type==478 || type==479;}
        internal static bool FlightMember(int type){return Handles(type) || type==480;}
        internal static Vector2 BirthVelocity(Vector2 velocity)
        {
            // NewProjectileSetup limits AI001 components, preserving the
            // launch ray and the native asymmetric negative-Y endpoint.
            if(float.IsNaN(velocity.X) || float.IsNaN(velocity.Y) || float.IsInfinity(velocity.X) || float.IsInfinity(velocity.Y))return new Vector2(float.NaN,float.NaN);
            while(velocity.X>=16 || velocity.X<=-16 || velocity.Y>=16 || velocity.Y< -16)velocity*=.97f;
            return velocity;
        }
        internal struct Birth
        {internal int Type,Count,Damage;internal Vector2 Center,Velocity;internal float Second,KnockBack;}
        internal sealed class State
        {
            internal Vector2 Position,Velocity;internal float Counter,Second,Visual;
            internal int Left,Alpha,Extra,Penetration,MaximumPenetration,Damage;
            internal bool Alive,Wet,Honey,Lava,Shimmer,Stopped,Random;
            internal Birth AiBirth,DeathBirth;
            internal State(Projectile p)
            {Position=p.position;Velocity=p.velocity;Counter=p.ai[0];Second=p.ai[1];Visual=p.localAI[0];Left=p.timeLeft;Alpha=p.alpha;Extra=p.extraUpdates;Penetration=p.penetrate;MaximumPenetration=p.maxPenetrate;Damage=p.damage;Alive=p.active;Wet=p.wet;Honey=p.honeyWet;Lava=p.lavaWet;Shimmer=p.shimmerWet;}
        }
        private sealed class Member
        {internal Projectile Shot;internal State State;internal HostAttackObstacles Receivers;internal int Policy,FirstOuter;internal bool Reflected;}
        private static Birth Cursed(Projectile shot,State s,Vector2 center)
        {return new Birth{Type=480,Count=1,Center=center,Velocity=Vector2.Zero,Damage=(int)((double)s.Damage*.8),KnockBack=shot.knockBack*.5f};}
        internal static void Kill(Projectile shot,State s)
        {
            if(!s.Alive)return;
            // Native remains occupied throughout its Kill births. Releasing
            // the parent before allocating would shift lower/higher clocks.
            if(shot.type==478 && shot.owner==Main.myPlayer)s.DeathBirth=Cursed(shot,s,s.Position+new Vector2(shot.width,shot.height)*.5f);
            s.Alive=false;s.Left=0;
        }
        private static bool Retarget(Projectile shot,State s,HostProjectileTargets targets,NpcTrajectory timeline,int tick,PredictionTerrain terrain,int excluded)
        {
            Vector2 center=s.Position+new Vector2(shot.width,shot.height)*.5f,chosen=Vector2.Zero;int count=0;
            for(int i=0;i<targets.Count;i++)
            {
                int slot=targets.Slot(i);if(slot==excluded || slot==timeline.Identity.Slot && !timeline[tick].CanReceive)continue;
                var b=targets.Bounds(i,timeline,tick);Vector2 point=new Vector2(b.CenterX,b.CenterY);float distance=(center-point).Length();if(!(distance>20 && distance<700))continue;
                bool clear;PredictionStop stop;
                if(!terrain.CanHitLine(new MotionRect(center.X,center.Y,1,1),new MotionRect(point.X,point.Y,1,1),out clear,out stop))return false;
                if(!clear)continue;if(count==0 || slot==timeline.Identity.Slot)chosen=point;
                if(++count==9)break;
            }
            if(count>0)
            {
                Vector2 next=chosen-center;next.Normalize();s.Velocity=next*s.Velocity.Length();
                // One eligible target is deterministic; multiple targets use
                // one legal member of the native first-nine prefix.
                s.Random|=count>1;
            }
            return true;
        }
        internal static bool OnHit(Projectile shot,State s,HostProjectileTargets targets,NpcTrajectory timeline,int tick,PredictionTerrain terrain,int victim,int beforePenetration)
        {return shot.type!=477 || beforePenetration<=1 || Retarget(shot,s,targets,timeline,tick,terrain,victim);}
        internal static bool Step(Projectile shot,State s,HostProjectileTargets targets,NpcTrajectory timeline,int tick,PredictionTerrain terrain,int threshold=5)
        {
            s.AiBirth=s.DeathBirth=default(Birth);s.Stopped=false;
            if(s.Position.X<=Main.leftWorld || s.Position.X+shot.width>=Main.rightWorld || s.Position.Y<=Main.topWorld || s.Position.Y+shot.height>=Main.bottomWorld || float.IsNaN(s.Position.X) || float.IsNaN(s.Position.Y))
            {s.Alive=false;s.Stopped=true;return true;}
            Vector2 half=new Vector2(shot.width,shot.height)*.5f,wetVelocity=s.Velocity;
            if(s.Alive)
            {
                s.Counter++;
                if(shot.type==480)
                {
                    if(s.Counter>5)
                    {
                        s.Counter=5;if(s.Velocity.Y==0 && s.Velocity.X!=0){s.Velocity.X*=.97f;if((double)s.Velocity.X>-.01 && (double)s.Velocity.X<.01)s.Velocity.X=0;}
                        s.Velocity.Y+=.2f;
                    }
                    s.Alpha=255;
                }
                else
                {
                    s.Visual++;if(s.Visual>3)s.Alpha=0;
                    if(s.Counter>=20){s.Counter=20;if(shot.type!=477)s.Velocity.Y+=.075f;}
                    if(shot.owner==Main.myPlayer && shot.type==478)
                    {
                        if(threshold<5 || threshold>=20)return false;
                        if(++s.Second>threshold){if(s.Left>40)s.Left-=20;s.Second=0;s.Random=true;s.AiBirth=Cursed(shot,s,s.Position+half);}
                    }
                    else if(shot.owner==Main.myPlayer && shot.type==479)
                    {
                        if(s.Second>=0)s.MaximumPenetration=s.Penetration=-1;else if(s.Penetration<0)s.MaximumPenetration=s.Penetration=1;
                        if(s.Second>=0)s.Second++;
                        if(threshold<5 || threshold>=30)return false;
                        if(s.Second>threshold)
                        {s.Second=-1000;s.Random=true;s.AiBirth=new Birth{Type=479,Count=2,Center=s.Position+half,Velocity=s.Velocity,Second=-1000,Damage=(int)((float)s.Damage*.9f),KnockBack=shot.knockBack};}
                    }
                }
                if(s.Velocity.Y>16)s.Velocity.Y=16;
            }
            Vector2 center=s.Position+half;float wind;if(!HostProjectileEnvironment.Impulse(shot,terrain,center.X,center.Y,s.Velocity.X,out wind))return false;s.Velocity.X+=wind;
            if(shot.owner<255 && !Main.player[shot.owner].active)Kill(shot,s);
            if(!shot.ignoreWater)
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileWet(center.X,center.Y,shot.width,shot.height,out wet,out kind,true) || !terrain.ProjectileLava(center.X,center.Y,shot.width,shot.height,out lava,true))return false;
                if(lava)s.Lava=true;if(wet && kind==2)s.Honey=true;if(wet && kind==3)s.Shimmer=true;s.Wet=wet;if(!wet)s.Honey=s.Lava=s.Shimmer=false;
            }
            if(s.Shimmer)
            {
                if(shot.type==480){if(s.Left>3000)s.Left-=60;s.Velocity.Y-=.5f;}
                else
                {
                    int x=(int)(center.X/16f),y=(int)(s.Position.Y/16f);bool full=false;
                    if(WorldGen.InWorld(x,y) && WorldGen.InWorld(x,y-1))
                    {
                        PredictionTile tile,above;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;
                        if(tile.Liquid==255 && tile.ShimmerLiquid){if(!terrain.Tile(x,y-1,out above,out stop))return false;full=above.Liquid>0 && above.ShimmerLiquid;}
                    }
                    if(full)Kill(shot,s);else if(s.Velocity.Y>0){s.Velocity.Y=-s.Velocity.Y;if(s.Left>600)s.Left=600;s.Left-=60;s.Shimmer=s.Wet=false;}
                }
            }
            Vector2 last=s.Velocity;bool hit=false;
            if(shot.tileCollide && !terrain.ProjectileTileMotion(shot,ref s.Position,ref s.Velocity,s.Wet,s.Honey,out wetVelocity,out hit,true,s.Shimmer,true))return false;
            if(hit)
            {
                if(shot.type==477)
                {
                    if(s.Velocity.X!=last.X || s.Velocity.Y!=last.Y)
                    {if(--s.Penetration<=0)Kill(shot,s);if(s.Velocity.X!=last.X)s.Velocity.X=-last.X;if(s.Velocity.Y!=last.Y)s.Velocity.Y=-last.Y;s.Damage=(int)((float)s.Damage*.85f);}
                    if(s.Penetration>0 && shot.owner==Main.myPlayer && !Retarget(shot,s,targets,timeline,tick,terrain,-1))return false;
                }
                // Locked HandleMovement excludes 480 from AI014's ordinary
                // half-speed bounce. Keep the collision-clipped velocity;
                // this child settles against tiles instead of reflecting.
                else if(shot.type!=480){s.Position+=s.Velocity;Kill(shot,s);}
            }
            s.Position+=s.Wet?wetVelocity:s.Velocity;s.Stopped=!s.Alive;return true;
        }
        internal static void Tail(Projectile shot,State s)
        {if(s.Stopped)return;if(--s.Left<=0 || s.Penetration==0)Kill(shot,s);}
        private static Projectile Copy(Projectile p)
        {
            var q=new Projectile();q.SetDefaults(p.type);q.active=p.active;q.owner=p.owner;q.whoAmI=p.whoAmI;q.position=p.position;q.velocity=p.velocity;q.damage=p.damage;q.knockBack=p.knockBack;q.width=p.width;q.height=p.height;
            q.penetrate=p.penetrate;q.maxPenetrate=p.maxPenetrate;q.friendly=p.friendly;q.hostile=p.hostile;q.melee=p.melee;q.ranged=p.ranged;q.magic=p.magic;q.trap=p.trap;q.npcProj=p.npcProj;q.usesOwnerMeleeHitCD=p.usesOwnerMeleeHitCD;
            q.tileCollide=p.tileCollide;q.ignoreWater=p.ignoreWater;q.correctSlopeCollision=p.correctSlopeCollision;q.usesLocalNPCImmunity=p.usesLocalNPCImmunity;q.usesIDStaticNPCImmunity=p.usesIDStaticNPCImmunity;q.immunityIdentity=p.immunityIdentity;q.localNPCHitCooldown=p.localNPCHitCooldown;q.idStaticNPCHitCooldown=p.idStaticNPCHitCooldown;q.appliesImmunityTimeOnSingleHits=p.appliesImmunityTimeOnSingleHits;
            q.timeLeft=p.timeLeft;q.alpha=p.alpha;q.extraUpdates=p.extraUpdates;q.wet=p.wet;q.honeyWet=p.honeyWet;q.lavaWet=p.lavaWet;q.shimmerWet=p.shimmerWet;
            Array.Copy(p.ai,q.ai,p.ai.Length);Array.Copy(p.localAI,q.localAI,p.localAI.Length);Array.Copy(p.localNPCImmunity,q.localNPCImmunity,p.localNPCImmunity.Length);return q;
        }
        private static Member Add(Projectile shot,HostAttackObstacles obstacles,int firstOuter,bool random)
        {var m=new Member{Shot=Copy(shot),State=new State(shot),FirstOuter=firstOuter};m.State.Random=random;Policy(m,obstacles);return m;}
        private static void Policy(Member m,HostAttackObstacles obstacles)
        {
            // Do not mutate a shot held by an earlier receive lease. Each real
            // max-penetration transition gets an immutable private policy;
            // all of them still reference this candidate's single history.
            var policy=Copy(m.Shot);policy.penetrate=m.State.Penetration;policy.maxPenetrate=m.State.MaximumPenetration;policy.damage=m.State.Damage;
            m.Policy=policy.maxPenetrate;m.Receivers=obstacles.ForkForSpawn(policy);if(m.State.Penetration<0)m.Receivers.MakeUnlimited();
        }
        private static void Insert(List<int> order,int slot)
        {int at=order.BinarySearch(slot);if(at<0)order.Insert(~at,slot);}
        private static bool Spawn(Birth birth,Member parent,Member[] members,List<int> order,HostProjectileBirthSlots slots,PredictionTerrain terrain,HostAttackObstacles obstacles,int outer)
        {
            for(int i=0;i<birth.Count;i++)
            {
                int slot;if(!slots.TryAllocate(out slot))return false;
                var p=new Projectile();p.SetDefaults(birth.Type);p.active=true;p.whoAmI=slot;p.owner=parent.Shot.owner;p.Center=birth.Center;p.velocity=birth.Velocity;p.damage=birth.Damage;p.knockBack=birth.KnockBack;p.ai[1]=birth.Second;
                if(birth.Type==479)
                {
                    // A nonzero native integer random vector nearest the parent
                    // direction is legal. Every minimum-count member may draw
                    // it independently; actual captured births bypass this.
                    Vector2 unit=birth.Velocity;float speed=unit.Length();unit.Normalize();
                    if(float.IsNaN(unit.X) || float.IsNaN(unit.Y))p.velocity=new Vector2(float.NaN,float.NaN);
                    else{var jitter=new Vector2((int)Math.Round(unit.X*100),(int)Math.Round(unit.Y*100));jitter.Normalize();var next=jitter+unit*2;next.Normalize();p.velocity=BirthVelocity(next*speed);}
                    p.penetrate=p.maxPenetrate=1;
                }
                bool wet;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true))return false;p.wet=wet;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;
                members[slot]=Add(p,obstacles,slot>parent.Shot.whoAmI?outer:outer+1,parent.State.Random);Insert(order,slot);
            }
            return true;
        }
        private static AttackContact Family(Projectile[] births,NpcTrajectory timeline,int age,int delay,PredictionTerrain terrain,HostAttackObstacles obstacles,HostProjectileTargets targets,HostProjectileBirthSlots captured,bool known,Vector2 aim)
        {
            var slots=captured.Copy();var members=new Member[1000];var order=new List<int>();
            foreach(var birth in births)
            {
                int slot=birth.whoAmI;if(!known && !slots.TryAllocate(out slot))return null;if(slot<0 || slot>=members.Length || members[slot]!=null)return null;
                var numbered=Copy(birth);numbered.whoAmI=slot;members[slot]=Add(numbered,obstacles,1,false);Insert(order,slot);
            }
            int limit=Math.Min(120,timeline.Count-age-1);
            for(int step=0;step<limit;step++)
            {
                int tick=age+step+1,outer=step+1;
                // NPC AI87 scans before the entire projectile pool. Children
                // born later in this outer do not retroactively enter it.
                foreach(int slot in order)
                {
                    var m=members[slot];if(m==null || m.Reflected || !m.State.Alive || m.FirstOuter>outer)continue;
                    if(!m.Receivers.BeforeNpc(tick,delay+outer,m.State.Position.X+m.Shot.width*.5f,m.State.Position.Y+m.Shot.height*.5f,m.Shot.width,m.Shot.height))m.Reflected=true;
                }
                bool active=false;int previous=-1;
                // Visit only this causal family's occupied slots. Searching
                // after the last visited slot handles higher births now and
                // lower births next outer, without rescanning the live pool.
                while(previous<999)
                {
                    int at=order.BinarySearch(previous+1);if(at<0)at=~at;if(at==order.Count)break;int slot=order[at];previous=slot;
                    var m=members[slot];if(m==null || m.Reflected || !m.State.Alive || m.FirstOuter>outer)continue;active=true;int updates=m.State.Extra+1;
                    for(int sub=0;sub<updates;sub++)
                    {
                        if(!Step(m.Shot,m.State,targets,timeline,tick,terrain))return null;
                        if(m.Policy!=m.State.MaximumPenetration)Policy(m,obstacles);
                        if(!Spawn(m.State.AiBirth,m,members,order,slots,terrain,obstacles,outer) || !Spawn(m.State.DeathBirth,m,members,order,slots,terrain,obstacles,outer))return null;
                        if(m.State.Alive && m.State.Damage>0 && m.Shot.friendly && !m.Shot.hostile && m.Shot.owner==Main.myPlayer)
                        {
                            AttackContact contact=null;bool dependency=true;int selected=m.Receivers.SelectedHits;var s=m.State;
                            bool passed=m.Receivers.PassCustomOuter(2,tick,delay+outer,s.Position.X+m.Shot.width*.5f,s.Position.Y+m.Shot.height*.5f,m.Shot.width,m.Shot.height,null,s.Penetration<0,s.Penetration,true,(victim,remaining)=>
                            {
                                if(victim==timeline.Identity.Slot){contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,s.Position.X+m.Shot.width*.5f,s.Position.Y+m.Shot.height*.5f,m.Shot.width,m.Shot.height,s.Random?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}
                                return dependency=OnHit(m.Shot,s,targets,timeline,tick,terrain,victim,remaining);
                            });
                            if(contact!=null && m.Receivers.SelectedHits>selected)return contact;if(!dependency)return null;
                            if(s.Penetration>=0)s.Penetration=m.Receivers.Remaining;
                            if(!passed && s.Penetration!=0){m.Reflected=true;break;}
                        }
                        if(m.State.Stopped)break;
                        m.State.DeathBirth=default(Birth);Tail(m.Shot,m.State);
                        if(!Spawn(m.State.DeathBirth,m,members,order,slots,terrain,obstacles,outer))return null;
                    }
                    if(!m.State.Alive){slots.Release(slot);members[slot]=null;order.Remove(slot);}
                }
                if(!active)break;
            }
            return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            if(!Handles(ammo.Projectile) || ammo.Damage<=0 || ammo.Speed<=0 || float.IsInfinity(ammo.Speed) || float.IsNaN(ammo.Speed) || player.whoAmI!=Main.myPlayer || delay<0 || age<delay || age+1>=timeline.Count)return null;
            var policy=new Projectile();policy.SetDefaults(ammo.Projectile);policy.owner=player.whoAmI;policy.damage=ammo.Damage;
            var targets=obstacles.CaptureChaseTargets(policy);var slots=obstacles.CaptureBirthSlots();var directions=new Vector2[41];int count=0;
            for(int seed=0;seed<41;seed++)
            {
                Vector2 direction;if(seed<9){var b=timeline[Math.Min(timeline.Count-1,age+1+seed*4)].ProjectileReceiveBounds;direction=(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction);}else direction=((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();
                bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;
                obstacles.BeginCandidate();var birth=Copy(policy);birth.active=true;birth.Center=origin;birth.velocity=BirthVelocity(direction*ammo.Speed);birth.knockBack=player.GetWeaponKnockback(player.HeldItem,player.HeldItem.knockBack);
                bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,birth.width,birth.height,out wet,out kind,true))return null;birth.wet=wet;birth.honeyWet=wet && kind==2;birth.shimmerWet=wet && kind==3;
                var aim=origin+direction*10000;var contact=Family(new[]{birth},timeline,age,delay,terrain,obstacles,targets,slots,false,aim);if(contact!=null)return contact;
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] members,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(members==null || members.Length==0 || clock.LaunchDelay!=0)return null;foreach(var member in members)if(member==null || !member.active || !FlightMember(member.type) || member.owner!=player.whoAmI || member.owner!=Main.myPlayer)return null;
            obstacles.CaptureProjectileFamily(members);var targets=obstacles.CaptureChaseTargets(members[0]);var slots=obstacles.CaptureBirthSlots();obstacles.BeginCandidate();
            return Family(members,timeline,clock.FirstTick-1,0,terrain,obstacles,targets,slots,true,members[0].Center+members[0].velocity*1000);
        }
    }
}
