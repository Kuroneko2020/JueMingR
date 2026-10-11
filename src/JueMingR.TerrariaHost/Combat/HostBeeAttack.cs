using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // AI036 bees are independent native children. Their common static181
    // immunity and slot order matter across local children of different body
    // sizes. Native Damage gates PVE to Main.myPlayer: a foreign bee still
    // occupies a birth slot but its local AI cannot write static181. Unborn
    // random choices remain explicitly representatives.
    internal static class HostBeeAttack
    {
        internal static bool Handles(int type){return type==181 || type==566;}
#if DEBUG
        internal static int MotorSteps,TargetQueries,Families,Births,TargetCaptures;
        internal static void ResetCounters(){MotorSteps=TargetQueries=Families=Births=TargetCaptures=0;}
#endif
        internal struct State
        {
            internal Vector2 Position,Velocity;internal float Counter;internal int Left,Alpha,Extra,Penetration,TargetSlot;
            internal bool Alive,Wet,Honey,Lava,Shimmer,Stopped;
            internal State(Projectile p)
            {Position=p.position;Velocity=p.velocity;Counter=p.ai[0];Left=p.timeLeft;Alpha=p.alpha;Extra=p.extraUpdates;Penetration=p.penetrate;TargetSlot=-1;Alive=p.active;Wet=p.wet;Honey=p.honeyWet;Lava=p.lavaWet;Shimmer=p.shimmerWet;Stopped=false;}
        }
        private static void Kill(ref State s){if(!s.Alive)return;s.Alive=false;s.Left=0;}
        private static float Approach(float value,float desired,float acceleration)
        {if(value<desired){value+=acceleration;if(value<0 && desired>0)value+=acceleration*2;}else if(value>desired){value-=acceleration;if(value>0 && desired<0)value-=acceleration*2;}return value;}
        internal static bool Step(Projectile sample,HostProjectileTargets targets,NpcTrajectory timeline,int tick,PredictionTerrain terrain,ref State s)
        {
#if DEBUG
            MotorSteps++;
#endif
            if(!Handles(sample.type))return false;s.Stopped=false;
            if(s.Position.X<=Main.leftWorld || s.Position.X+sample.width>=Main.rightWorld || s.Position.Y<=Main.topWorld || s.Position.Y+sample.height>=Main.bottomWorld || float.IsNaN(s.Position.X) || float.IsNaN(s.Position.Y))
            {s.Alive=false;s.Stopped=true;return true;}
            Vector2 half=new Vector2(sample.width,sample.height)*.5f,wetVelocity=s.Velocity;
            // AI Kill does not stop the rest of AI or HandleMovement. A wet
            // bee can steer/move once more, but cannot Damage or run the tail.
            Vector2 center=s.Position+half;
            // A tail Kill can leave a pending subupdate in this outer cycle.
            // Native skips AI there, but still performs one HandleMovement
            // before its inactive return. Do not advance the AI clock twice.
            if(s.Alive)
            {
            if(s.Wet && !s.Honey && !s.Shimmer)Kill(ref s);
            if(s.Alpha>0)s.Alpha-=50;else s.Extra=0;if(s.Alpha<0)s.Alpha=0;
            s.Counter++;s.TargetSlot=-1;Vector2 point=center+s.Velocity*100;
            if(s.Counter>30)
            {
                s.Counter=30;float best=100000;
                for(int i=0;i<targets.Count;i++)
                {
#if DEBUG
                    TargetQueries++;
#endif
                    bool wet=targets.Slot(i)==timeline.Identity.Slot?timeline[tick].Wet:targets.Wet(i);
                    if(wet && targets.Type(i)!=370)continue;
                    var b=targets.Bounds(i,timeline,tick);Vector2 target=new Vector2(b.X+(int)b.Width/2,b.Y+(int)b.Height/2);
                    float distance=Math.Abs(center.X-target.X)+Math.Abs(center.Y-target.Y);if(distance>=800 || distance>=best)continue;
                    bool clear;PredictionStop stop;if(!terrain.CanHit(new MotionRect(s.Position.X,s.Position.Y,sample.width,sample.height),b,out clear,out stop))return false;
                    if(!clear)continue;best=distance;point=target;s.TargetSlot=targets.Slot(i);
                }
            }
            Vector2 desired=point-center;float length=(float)Math.Sqrt(desired.X*desired.X+desired.Y*desired.Y),speed=sample.type==566?6.8f:6,acceleration=sample.type==566?.14f:.1f;
            float factor=speed/length;desired.X*=factor;desired.Y*=factor;
            // Native zero-vector comparisons leave the velocity unchanged;
            // SafeNormalize(UnitX) would invent motion and an eventual hit.
            s.Velocity.X=Approach(s.Velocity.X,desired.X,acceleration);s.Velocity.Y=Approach(s.Velocity.Y,desired.Y,acceleration);
            }
            float impulse;if(!HostProjectileEnvironment.Impulse(sample,terrain,center.X,center.Y,s.Velocity.X,out impulse))return false;s.Velocity.X+=impulse;
            if(sample.owner<255 && !Main.player[sample.owner].active)Kill(ref s);
            if(sample.ignoreWater)s.Wet=false;
            else
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileWet(center.X,center.Y,sample.width,sample.height,out wet,out kind,true) || !terrain.ProjectileLava(center.X,center.Y,sample.width,sample.height,out lava,true))return false;
                if(lava)s.Lava=true;if(wet && kind==2)s.Honey=true;if(wet && kind==3)s.Shimmer=true;s.Wet=wet;if(!wet)s.Honey=s.Lava=s.Shimmer=false;
            }
            if(s.Shimmer)s.Velocity.Y-=.2f;
            Vector2 position=s.Position,velocity=s.Velocity,last=velocity;bool hit=false;
            if(sample.tileCollide && !terrain.ProjectileTileMotion(sample,ref position,ref velocity,s.Wet,s.Honey,out wetVelocity,out hit,true,s.Shimmer,true))return false;
            s.Position=position;
            if(hit)
            {
                if(s.Penetration>0){s.Penetration--;if(velocity.X!=last.X)velocity.X=-last.X;if(velocity.Y!=last.Y)velocity.Y=-last.Y;}
                else {s.Position+=velocity;Kill(ref s);}
            }
            s.Velocity=velocity;s.Position+=s.Wet?wetVelocity:velocity;s.Stopped=!s.Alive;return true;
        }
        internal static void Tail(ref State s)
        {if(s.Stopped)return;s.Left--;if(s.Left<=0 || s.Penetration==0)Kill(ref s);}
        private sealed class Member
        {internal Projectile Shot;internal State State;internal HostAttackObstacles Receivers;}
        private static Projectile Copy(Projectile p)
        {
            var q=new Projectile();q.SetDefaults(p.type);q.active=p.active;q.whoAmI=p.whoAmI;q.owner=p.owner;q.position=p.position;q.velocity=p.velocity;q.damage=p.damage;q.knockBack=p.knockBack;
            q.width=p.width;q.height=p.height;q.penetrate=p.penetrate;q.maxPenetrate=p.maxPenetrate;q.tileCollide=p.tileCollide;q.ignoreWater=p.ignoreWater;q.correctSlopeCollision=p.correctSlopeCollision;q.friendly=p.friendly;
            q.magic=p.magic;q.ranged=p.ranged;q.melee=p.melee;
            q.usesLocalNPCImmunity=p.usesLocalNPCImmunity;q.usesIDStaticNPCImmunity=p.usesIDStaticNPCImmunity;q.immunityIdentity=p.immunityIdentity;q.localNPCHitCooldown=p.localNPCHitCooldown;q.idStaticNPCHitCooldown=p.idStaticNPCHitCooldown;q.appliesImmunityTimeOnSingleHits=p.appliesImmunityTimeOnSingleHits;
            Array.Copy(p.localNPCImmunity,q.localNPCImmunity,p.localNPCImmunity.Length);return q;
        }
        private static AttackContact Family(Projectile[] children,NpcTrajectory timeline,PredictionTerrain terrain,HostAttackObstacles obstacles,int firstTick,int firstOuter,int firstAfterSlot,bool representative,HostProjectileTargets preparedTargets=null)
        {
            if(children==null || children.Length==0 || children.Length>24)return null;
#if DEBUG
            Families++;
#endif
            var targets=preparedTargets??CaptureTargets(children[0],obstacles);var members=new List<Member>(children.Length);
            foreach(var child in children)
            {if(child==null || !child.active || !Handles(child.type))return null;var q=Copy(child);members.Add(new Member{Shot=q,State=new State(child),Receivers=obstacles.ForkForSpawn(q)});}
            members.Sort((a,b)=>a.Shot.whoAmI.CompareTo(b.Shot.whoAmI));Vector2 aim=children[0].Center+children[0].velocity*1000;
            int limit=Math.Min(120,timeline.Count-firstTick);
            for(int step=0;step<limit;step++)
            {
                int tick=firstTick+step,outer=firstOuter+step;
                foreach(var m in members)
                {
                    if(!m.State.Alive || step==0 && m.Shot.whoAmI<=firstAfterSlot)continue;
                    int updates=m.State.Extra+1;
                    for(int sub=0;sub<updates;sub++)
                    {
                        if(!Step(m.Shot,targets,timeline,tick,terrain,ref m.State))return null;
                        if(m.State.Alive && m.Shot.friendly && m.Shot.damage>0)
                        {
                            // A tile bounce may set penetration to zero. Native
                            // still executes this Damage, then kills at tail.
                            bool zero=m.State.Penetration==0;
                            if(!m.Receivers.PassShapeOuter(2,tick,outer,m.State.Position.X+m.Shot.width*.5f,m.State.Position.Y+m.Shot.height*.5f,m.Shot.width,m.Shot.height,zero,m.State.Penetration))
                            {if(m.Receivers.Remaining>0)return null;Kill(ref m.State);}
                            else if(m.Receivers.SelectedAllows(outer))
                            {
                                var contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,m.State.Position.X+m.Shot.width*.5f,m.State.Position.Y+m.Shot.height*.5f,m.Shot.width,m.Shot.height,representative?AttackConfidence.Representative:AttackConfidence.Conditional);
                                if(contact!=null)return contact;
                            }
                            if(!zero)m.State.Penetration=m.Receivers.Remaining;
                        }
                        Tail(ref m.State);if(m.State.Stopped)break;
                    }
                }
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] children,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(clock.LaunchDelay!=0 || children==null)return null;foreach(var child in children)if(child==null || child.owner!=player.whoAmI)return null;
            obstacles.CaptureProjectileFamily(children);
            obstacles.BeginCandidate();return Family(children,timeline,terrain,obstacles,clock.FirstTick,1,-1,false);
        }
        // A held Bee Gun creates all members before the projectile slot loop,
        // unlike a Kill birth's firstAfterSlot boundary. The caller owns the
        // candidate reset and passes its one captured chase snapshot.
        internal static AttackContact NewbornRepresentative(Projectile[] children,NpcTrajectory timeline,int firstTick,int firstOuter,PredictionTerrain terrain,HostAttackObstacles obstacles,HostProjectileTargets preparedTargets)
        {return Family(children,timeline,terrain,obstacles,firstTick,firstOuter,-1,true,preparedTargets);}
        // Native chase permission depends on the bee role, not the arrow or
        // grenade parent. Capture once before candidate replay and retain it
        // in the ordinary receiver consume lease; Draw never recaptures it.
        internal static HostProjectileTargets CaptureTargets(Projectile parent,HostAttackObstacles obstacles)
        {
#if DEBUG
            TargetCaptures++;
#endif
            var bee=new Projectile();bee.SetDefaults(181);bee.owner=parent.owner;bee.damage=parent.damage;
            return obstacles.CaptureWetChaseTargets(bee);
        }
        // Caller has already replayed the parent's real Kill and its earlier
        // receives. Do not reset that history. The parent remains occupied
        // throughout native NewProjectile calls, and higher birth slots run
        // in this same outer update. No live RNG or player bee flag is written.
        // Returns only private births: callers with other finite members must
        // interleave them at the real native slot/outer boundary, not run this
        // entire family's future before a later arrow gets its Update.
        internal static Projectile[] BirthMembersRepresentative(Projectile parent,Vector2 killPosition,Vector2 oldVelocity,bool giant,Func<int> allocate,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(parent.owner!=Main.myPlayer || parent.type!=469 && parent.type!=183 || allocate==null || giant && !obstacles.CaptureBeeOwner())return null;
#if DEBUG
            Births++;
#endif
            obstacles.CaptureBeeOwner();
            int count=parent.type==469?3:15;var children=new Projectile[count];
            for(int i=0;i<count;i++)
            {
                int slot=allocate();if(slot<0)return null;var q=new Projectile();q.SetDefaults(giant?566:181);q.active=true;q.whoAmI=slot;q.owner=parent.owner;
                Vector2 origin=killPosition,velocity=Vector2.Zero;
                if(parent.type==469){var unit=oldVelocity;unit.Normalize();origin-=unit*8*(i*2);velocity=oldVelocity/6;q.penetrate=2;q.magic=false;q.ranged=true;}
                // Native NewProjectile treats the Kill-entry top-left as its
                // center. Even offsets0/2/4 and zero random jitter are legal
                // representatives; real captured children bypass this factory.
                q.damage=(parent.type==469?parent.damage/3:parent.damage)+(giant?1:0);q.knockBack=giant?.5f:0;q.Center=origin;q.velocity=velocity;
                bool wet;byte kind;if(!terrain.ProjectileWet(q.Center.X,q.Center.Y,q.width,q.height,out wet,out kind,true))return null;q.wet=wet;q.honeyWet=wet && kind==2;q.shimmerWet=wet && kind==3;children[i]=q;
            }
            return children;
        }
        internal static AttackContact BirthRepresentative(Projectile parent,Vector2 killPosition,Vector2 oldVelocity,bool giant,HostProjectileBirthSlots slots,NpcTrajectory timeline,int tick,int outer,PredictionTerrain terrain,HostAttackObstacles obstacles,HostProjectileTargets preparedTargets=null)
        {
            var children=BirthMembersRepresentative(parent,killPosition,oldVelocity,giant,()=>{int slot;return slots.TryAllocate(out slot)?slot:-1;},terrain,obstacles);
            return children==null?null:Family(children,timeline,terrain,obstacles,tick,outer,parent.whoAmI,true,preparedTargets);
        }
    }
}
