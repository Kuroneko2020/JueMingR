using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // AI092 competition is a whole captured cloud scene, across all owners.
    // Causal membership is separate: ambient movement and local static writes
    // affect this weapon, but an ambient hit never becomes its Contact.
    internal static class HostPoisonCloudAttack
    {
        internal static bool Handles(int type){return type==510;}
        internal static bool Cloud(int type){return type>=511 && type<=513;}
#if DEBUG
        internal static int Captures,Candidates,MotorSteps,CompetitionQueries,Families;
        internal static void ResetCounters(){Captures=Candidates=MotorSteps=CompetitionQueries=Families=0;}
#endif
        private sealed class Member
        {internal Projectile Shot;internal HostAttackObstacles Receivers;internal bool Source,Representative;}
        private static void Kill(Projectile p){if(p.active){p.active=false;p.timeLeft=0;}}
        private static bool Retired(Projectile p)
        {return p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y);}
        internal static Projectile Copy(Projectile p)
        {
            var c=new Projectile();c.SetDefaults(p.type);c.whoAmI=p.whoAmI;c.owner=p.owner;c.position=p.position;c.velocity=p.velocity;c.width=p.width;c.height=p.height;c.damage=p.damage;c.knockBack=p.knockBack;c.penetrate=p.penetrate;c.maxPenetrate=p.maxPenetrate;c.timeLeft=p.timeLeft;c.extraUpdates=p.extraUpdates;c.alpha=p.alpha;c.active=p.active;c.friendly=p.friendly;c.hostile=p.hostile;c.tileCollide=p.tileCollide;c.ignoreWater=p.ignoreWater;c.wet=p.wet;c.honeyWet=p.honeyWet;c.lavaWet=p.lavaWet;c.shimmerWet=p.shimmerWet;c.direction=p.direction;c.rotation=p.rotation;c.scale=p.scale;c.correctSlopeCollision=p.correctSlopeCollision;c.immunityIdentity=p.immunityIdentity;c.usesIDStaticNPCImmunity=p.usesIDStaticNPCImmunity;c.idStaticNPCHitCooldown=p.idStaticNPCHitCooldown;c.ai=(float[])p.ai.Clone();c.localAI=(float[])p.localAI.Clone();return c;
        }
        private static bool Wet(Projectile p,PredictionTerrain terrain)
        {
            if(p.ignoreWater){p.wet=false;return true;}
            bool wet,lava;byte kind;
            if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
            if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;p.wet=wet;if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;return true;
        }
        internal static bool Advance(Projectile[] scene,int index,PredictionTerrain terrain,out bool stopped)
        {
#if DEBUG
            MotorSteps++;
#endif
            var p=scene[index];stopped=false;if(!Cloud(p.type))return false;if(Retired(p)){p.active=false;stopped=true;return true;}
            Vector2 wetVelocity=p.velocity;
            if(p.active)
            {
                p.tileCollide=false;p.ai[0]++;if(p.ai[1]>=1)p.ai[0]+=2;
                if(p.ai[0]>260){Kill(p);p.ai[0]=260;}
                else
                {
                    float age=p.ai[0]/260;p.Opacity=Utils.Remap(age,0,.3f,0,1)*Utils.Remap(age,.3f,1,1,0)*.7f;p.localAI[0]+=p.direction;p.rotation=p.whoAmI*.4002029f+p.localAI[0]*MathHelper.TwoPi/480;p.velocity*=.96f;var body=p.Hitbox;
                    // Ascending native slots; mutate both private velocities.
                    // Earlier slots have moved already, later slots have not.
                    foreach(var other in scene)
                    {
#if DEBUG
                        CompetitionQueries++;
#endif
                        if(other==p || !other.active || !Cloud(other.type) || !body.Intersects(other.Hitbox))continue;
                        Vector2 delta=other.Center-p.Center;if(delta==Vector2.Zero)delta=other.whoAmI<p.whoAmI?new Vector2(-1,1):new Vector2(1,-1);Vector2 impulse=delta.SafeNormalize(Vector2.UnitX)*.005f;p.velocity=Vector2.Lerp(p.velocity,p.velocity-impulse,.6f);other.velocity=Vector2.Lerp(other.velocity,other.velocity+impulse,.6f);
                    }
                    Vector2 unit=p.velocity.SafeNormalize(Vector2.Zero);bool solid;if(!terrain.ProjectileWorldPointSolid(p.Center+unit*16,out solid))return false;if(solid)p.velocity=Vector2.Lerp(p.velocity,p.velocity-unit,.5f);
                }
            }
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;if(p.owner<255 && !Main.player[p.owner].active)Kill(p);if(!Wet(p,terrain))return false;
            // tileCollide=false keeps the PRE-AI wet velocity, without the
            // ordinary .5/.25/.375 liquid factors. AI092 has no Shimmer branch.
            p.position+=p.wet?wetVelocity:p.velocity;stopped=!p.active;return true;
        }
        internal static bool AdvanceParent(Projectile p,PredictionTerrain terrain,out bool stopped,out bool killed,out Vector2 deathCenter)
        {
#if DEBUG
            MotorSteps++;
#endif
            stopped=killed=false;deathCenter=Vector2.Zero;if(Retired(p)){p.active=false;stopped=true;return true;}
            Vector2 wetVelocity=p.velocity;
            if(p.active)
            {
                if(Main.windPhysics)p.velocity.X+=Main.windSpeedCurrent*Main.windPhysicsStrength;
                p.rotation+=Math.Abs(p.velocity.X)*.04f*p.direction;p.ai[0]++;if(p.ai[0]>=20){p.velocity.Y+=.4f;p.velocity.X*=.97f;}if(p.velocity.Y>32)p.velocity.Y=32;
            }
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active){deathCenter=p.Center;Kill(p);killed=true;}
            if(!Wet(p,terrain))return false;if(p.shimmerWet){p.velocity.Y-=.7f;p.timeLeft-=20;}
            Vector2 position=p.position,velocity=p.velocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;
            p.position=position;p.velocity=velocity;if(hit){p.position+=velocity;deathCenter=p.Center;Kill(p);killed=true;}
            p.position+=p.wet?wetVelocity:velocity;stopped=!p.active;return true;
        }
        internal static void Tail(Projectile p,bool stopped)
        {if(stopped)return;p.timeLeft--;if(p.timeLeft<=0 || p.penetrate==0)Kill(p);}
        private static Projectile[] CaptureScene(HostAttackObstacles obstacles)
        {
#if DEBUG
            Captures++;
#endif
            var scene=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && Cloud(p.type))scene.Add(p);scene.Sort((a,b)=>a.whoAmI.CompareTo(b.whoAmI));var bodies=scene.ToArray();obstacles.CaptureProjectileFamily(bodies,511,513);return bodies;
        }
        private static List<Member> Prepare(Projectile[] scene,Projectile[] sources,HostAttackObstacles obstacles)
        {
            var members=new List<Member>(scene.Length+31);
            foreach(var p in scene){bool source=false;if(sources!=null)foreach(var s in sources)if(ReferenceEquals(s,p)){source=true;break;}var copy=Copy(p);members.Add(new Member{Shot=copy,Source=source,Receivers=copy.owner==Main.myPlayer?obstacles.ForkForSpawn(copy):null});}
            return members;
        }
        private static List<Member> ReplayMembers(List<Member> prepared)
        {
            var members=new List<Member>(prepared.Count+31);foreach(var m in prepared){m.Receivers?.BeginCandidate();members.Add(new Member{Shot=Copy(m.Shot),Source=m.Source,Representative=m.Representative,Receivers=m.Receivers});}return members;
        }
        private static bool Birth(Member parent,Vector2 center,List<Member> members,HostProjectileBirthSlots slots,PredictionTerrain terrain)
        {
            if(parent.Shot.owner!=Main.myPlayer)return true;
#if DEBUG
            Families++;
#endif
            // Twenty-four legal random outcomes cover all three native bodies.
            // Count, angle and speed remain Representative, never sampled RNG.
            var rays=new[]{new Vector2(100,0),new Vector2(71,71),new Vector2(0,100),new Vector2(-71,71),new Vector2(-100,0),new Vector2(-71,-71),new Vector2(0,-100),new Vector2(71,-71)};
            for(int i=0;i<24;i++)
            {
                int slot;if(!slots.TryAllocate(out slot))return false;var p=new Projectile();p.SetDefaults(511+i%3);p.whoAmI=slot;p.owner=parent.Shot.owner;p.Center=center;p.velocity=Vector2.Normalize(rays[i/3])*2;p.damage=parent.Shot.damage;p.knockBack=1;p.active=true;p.direction=p.velocity.X<0?-1:1;if(!Wet(p,terrain))return false;
                members.Add(new Member{Shot=p,Source=true,Representative=true,Receivers=parent.Receivers.ForkForSpawn(p)});
            }
            return true;
        }
        private static bool Outer(List<Member> members,HostProjectileBirthSlots slots,NpcTrajectory timeline,PredictionTerrain terrain,int tick,int outer,Vector2 aim,out AttackContact contact)
        {
            contact=null;int after=-1;var scene=new Projectile[members.Count];for(int i=0;i<scene.Length;i++)scene[i]=members[i].Shot;
            // Reflection removes only this member from the friendly family.
            // It does not Kill the bottle, birth clouds, or free the native
            // slot; already existing clouds still take their ordered turns.
            foreach(var existing in members)if(existing.Shot.active && existing.Receivers!=null && !existing.Receivers.BeforeNpc(tick,outer,existing.Shot.Center.X,existing.Shot.Center.Y))existing.Shot.active=false;
            for(int visit=0;visit<1000;visit++)
            {
                Member m=null;foreach(var candidate in members)if(candidate.Shot.active && candidate.Shot.whoAmI>after && (m==null || candidate.Shot.whoAmI<m.Shot.whoAmI))m=candidate;if(m==null)return true;after=m.Shot.whoAmI;int updates=m.Shot.extraUpdates+1;
                for(int sub=0;sub<updates;sub++)
                {
                    var p=m.Shot;bool alive=p.active,stopped,killed=false;Vector2 death=Vector2.Zero;
                    if(p.type==510){if(!AdvanceParent(p,terrain,out stopped,out killed,out death))return false;}
                    else {if(scene.Length!=members.Count){scene=new Projectile[members.Count];for(int i=0;i<scene.Length;i++)scene[i]=members[i].Shot;}if(!Advance(scene,members.IndexOf(m),terrain,out stopped))return false;}
                    if(p.active && p.friendly && !p.hostile && p.damage>0 && p.owner==Main.myPlayer && (p.type==510 || p.ai[1]<1))
                    {
                        bool pass=m.Receivers.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:!m.Source,onHit:(slot,remaining)=>{if(Cloud(p.type))p.ai[1]++;return true;});
                        if(!pass){if(m.Receivers.Remaining!=0)return false;death=p.Center;Kill(p);killed=true;}
                        else if(m.Source && m.Receivers.SelectedAllows(outer))
                        {contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);if(contact!=null)return true;}
                    }
                    if(p.active && !stopped){bool tail=p.timeLeft<=1 || p.penetrate==0;if(tail)death=p.Center;Tail(p,false);if(tail)killed=true;}
                    if(alive && !p.active){if(p.type==510 && killed && !Birth(m,death,members,slots,terrain))return false;slots.Release(p.whoAmI);}
                    if(stopped)break;
                }
            }
            return false;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            int limit=Math.Min(120,timeline.Count-age-1);if(limit<=0 || ammo.Damage<=0)return null;var scene=CaptureScene(obstacles);var prepared=Prepare(scene,null,obstacles);var pool=obstacles.CaptureBirthSlots();var directions=new Vector2[41];int count=0;
            for(int seed=0;seed<41;seed++)
            {
                Vector2 direction;if(seed<9){var b=timeline[Math.Min(timeline.Count-1,age+1+seed*4)].ProjectileReceiveBounds;direction=(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction);}else direction=((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;
#if DEBUG
                Candidates++;
#endif
                obstacles.BeginCandidate();var members=ReplayMembers(prepared);var slots=pool.Copy();int slot;if(!slots.TryAllocate(out slot))return null;var p=new Projectile();p.SetDefaults(510);p.whoAmI=slot;p.owner=player.whoAmI;p.damage=ammo.Damage;p.knockBack=player.HeldItem.knockBack;p.Center=origin;p.velocity=direction*ammo.Speed;p.active=true;p.direction=direction.X<0?-1:1;if(!Wet(p,terrain))continue;members.Add(new Member{Shot=p,Source=true,Receivers=obstacles});var aim=origin+direction*10000;
                for(int step=1;step<=limit;step++){AttackContact contact;if(!Outer(members,slots,timeline,terrain,age+step,delay+step,aim,out contact))break;if(contact!=null)return contact;}
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] children,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(children==null || children.Length==0 || children.Length>30 || clock.LaunchDelay!=0)return null;
            foreach(var p in children)if(p==null || !p.active || !Cloud(p.type) || p.owner!=player.whoAmI)return null;
            var scene=CaptureScene(obstacles);obstacles.BeginCandidate();var members=Prepare(scene,children,obstacles);var slots=obstacles.CaptureBirthSlots().Copy();var aim=children[0].Center+children[0].velocity*1000;int limit=Math.Min(120,timeline.Count-clock.FirstTick);
            for(int step=0;step<limit;step++){AttackContact contact;if(!Outer(members,slots,timeline,terrain,clock.FirstTick+step,step+1,aim,out contact))return null;if(contact!=null)return contact;}
            return null;
        }
    }
}
