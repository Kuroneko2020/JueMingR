using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // The hand owns only the Wasp Gun/Bat Scepter's birth direction. Native
    // AI036 owns target acquisition thereafter. Unborn count/jitter are legal
    // representatives; captured births keep their own actual state. Wasp189
    // has shared static189, whereas Bat316's single hit uses ordinary immunity.
    internal static class HostWaspBatAttack
    {
        internal static bool Handles(int type){return type==189 || type==316;}
        private sealed class Member
        {
            internal Projectile Shot;internal HostBeeAttack.State State;
            internal HostAttackObstacles Receivers;internal bool Contributes;
        }
        private static Projectile Copy(Projectile p)
        {
            var q=new Projectile();q.SetDefaults(p.type);q.active=p.active;q.whoAmI=p.whoAmI;q.owner=p.owner;q.position=p.position;q.velocity=p.velocity;q.damage=p.damage;q.knockBack=p.knockBack;
            q.width=p.width;q.height=p.height;q.penetrate=p.penetrate;q.maxPenetrate=p.maxPenetrate;q.tileCollide=p.tileCollide;q.ignoreWater=p.ignoreWater;q.correctSlopeCollision=p.correctSlopeCollision;q.friendly=p.friendly;q.hostile=p.hostile;
            q.magic=p.magic;q.ranged=p.ranged;q.melee=p.melee;q.usesLocalNPCImmunity=p.usesLocalNPCImmunity;q.usesIDStaticNPCImmunity=p.usesIDStaticNPCImmunity;q.immunityIdentity=p.immunityIdentity;
            q.localNPCHitCooldown=p.localNPCHitCooldown;q.idStaticNPCHitCooldown=p.idStaticNPCHitCooldown;q.appliesImmunityTimeOnSingleHits=p.appliesImmunityTimeOnSingleHits;
            Array.Copy(p.localNPCImmunity,q.localNPCImmunity,p.localNPCImmunity.Length);return q;
        }
        private static Projectile[] CaptureAmbient(HostAttackObstacles obstacles,int type)
        {
            if(type!=189)return new Projectile[0];var scene=new List<Projectile>();
            // Capture qualification across every owner once. Foreign native
            // Damage cannot write PVE static189; only local members below are
            // motors/history dependencies. No scan is performed per direction.
            foreach(var p in Main.projectile)if(p!=null && p.active && p.type==189)scene.Add(p);
            var captured=scene.ToArray();obstacles.CaptureProjectileFamily(captured,189,189);return captured;
        }
        private static void Add(List<Member> members,Projectile shot,bool contributes,HostAttackObstacles obstacles)
        {var copy=Copy(shot);members.Add(new Member{Shot=copy,State=new HostBeeAttack.State(shot),Receivers=obstacles.ForkForSpawn(copy),Contributes=contributes});}
        private static AttackContact Family(Projectile[] births,Projectile[] ambient,HostProjectileTargets targets,NpcTrajectory timeline,int age,int delay,PredictionTerrain terrain,HostAttackObstacles obstacles,bool representative,HostProjectileBirthSlots slots=null)
        {
            var members=new List<Member>(births.Length+ambient.Length);
            foreach(var existing in ambient)
            {
                bool isBirth=false;foreach(var birth in births)if(ReferenceEquals(birth,existing)){isBirth=true;break;}
                if(!isBirth)Add(members,existing,false,obstacles);
            }
            // An idle cooldown advances already born wasps before this hand
            // creates its two new representatives. The caller supplies a
            // future hand origin, but never a future ambient projectile state.
            for(int outer=1;outer<=delay;outer++)
            {
                int tick=age-delay+outer;if(tick<0 || tick>=timeline.Count)return null;
                members.Sort((a,b)=>a.Shot.whoAmI.CompareTo(b.Shot.whoAmI));
                foreach(var member in members)
                {
                    bool alive=member.State.Alive;if(!Advance(member,targets,timeline,tick,outer,terrain,false,Vector2.Zero,representative,out _))return null;
                    if(alive && !member.State.Alive && slots!=null)slots.Release(member.Shot.whoAmI);
                }
            }
            foreach(var birth in births)
            {if(slots!=null){int slot;if(!slots.TryAllocate(out slot))return null;birth.whoAmI=slot;}Add(members,birth,true,obstacles);}
            members.Sort((a,b)=>a.Shot.whoAmI.CompareTo(b.Shot.whoAmI));
            Vector2 aim=births[0].Center+births[0].velocity*1000;
            int limit=Math.Min(120,timeline.Count-age-1);
            for(int step=0;step<limit;step++)
            {
                int tick=age+step+1,outer=delay+step+1;bool alive=false;
                foreach(var member in members)
                {
                    // Foreign PVE Damage is gated by Main.myPlayer. After
                    // the last birth allocation it cannot affect this source
                    // family's shared static history or slot ordering.
                    if(member.Shot.owner!=Main.myPlayer)continue;
                    AttackContact contact;if(!Advance(member,targets,timeline,tick,outer,terrain,member.Contributes,aim,representative,out contact))return null;
                    if(contact!=null)return contact;alive|=member.State.Alive;
                }
                if(!alive)break;
            }
            return null;
        }
        private static bool Advance(Member m,HostProjectileTargets targets,NpcTrajectory timeline,int tick,int outer,PredictionTerrain terrain,bool contributes,Vector2 aim,bool representative,out AttackContact contact)
        {
            contact=null;if(!m.State.Alive)return true;int updates=m.State.Extra+1;
            for(int sub=0;sub<updates;sub++)
            {
                if(!HostBeeAttack.StepWaspBat(m.Shot,targets,timeline,tick,terrain,ref m.State))return false;
                if(m.State.Alive && m.Shot.friendly && !m.Shot.hostile && m.Shot.damage>0 && m.Shot.owner==Main.myPlayer)
                {
                    bool zero=m.State.Penetration==0;int before=m.Receivers.SelectedHits;
                    // Ambient and contributed siblings both consume selected
                    // in native NPC-slot order. Ambient writes can block a new
                    // wasp, but can never manufacture this hand's contact.
                    bool passed=m.Receivers.PassCustomOuter(2,tick,outer,m.State.Position.X+m.Shot.width*.5f,m.State.Position.Y+m.Shot.height*.5f,m.Shot.width,m.Shot.height,null,zero,m.State.Penetration,true);
                    if(contributes && m.Receivers.SelectedHits>before)
                    {
                        contact=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,m.State.Position.X+m.Shot.width*.5f,m.State.Position.Y+m.Shot.height*.5f,m.Shot.width,m.Shot.height,representative?AttackConfidence.Representative:AttackConfidence.Conditional);
                        if(contact!=null)return true;
                    }
                    if(!zero)m.State.Penetration=m.Receivers.Remaining;
                    if(!passed && m.State.Penetration!=0)return false;
                }
                HostBeeAttack.Tail(ref m.State);if(m.State.Stopped)break;
            }
            return true;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            if(!Handles(ammo.Projectile) || ammo.WeaponType!=(ammo.Projectile==189?1155:1801) || ammo.Damage<=0 || player.whoAmI!=Main.myPlayer || age+1>=timeline.Count || delay<0 || age<delay)return null;
            if(ammo.Projectile==189)obstacles.CaptureBeeOwner();
            var policy=new Projectile();policy.SetDefaults(ammo.Projectile);policy.owner=player.whoAmI;policy.damage=ammo.Damage;
            var targets=obstacles.CaptureWetChaseTargets(policy);var ambient=CaptureAmbient(obstacles,ammo.Projectile);var pool=obstacles.CaptureBirthSlots();
            var directions=new Vector2[41];int count=0;float knockBack=player.GetWeaponKnockback(player.HeldItem,player.HeldItem.knockBack);
            for(int seed=0;seed<41;seed++)
            {
                Vector2 direction;if(seed<9){var b=timeline[Math.Min(timeline.Count-1,age+1+seed*4)].ProjectileReceiveBounds;direction=(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction);}else direction=((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();
                bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;
                // Both native counts admit two; zero per-axis jitter is legal.
                // Never invoke native random draws, or alter a live projectile.
                obstacles.BeginCandidate();var slots=pool.Copy();var births=new Projectile[2];
                for(int i=0;i<births.Length;i++)
                {
                    var q=new Projectile();q.SetDefaults(ammo.Projectile);q.active=true;q.owner=player.whoAmI;q.damage=ammo.Damage;q.knockBack=knockBack;q.Center=origin;q.velocity=direction*ammo.Speed;
                    bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,q.width,q.height,out wet,out kind,true))return null;q.wet=wet;q.honeyWet=wet && kind==2;q.shimmerWet=wet && kind==3;births[i]=q;
                }
                var contact=Family(births,ambient,targets,timeline,age,delay,terrain,obstacles,true,slots);
                if(contact!=null){var aim=origin+direction*10000;return AttackIntercept.WithAimInput(contact,aim.X,aim.Y);}
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] children,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(children==null || children.Length==0 || clock.LaunchDelay!=0 || player.whoAmI!=Main.myPlayer)return null;
            int type=children[0].type;foreach(var q in children)if(q==null || !q.active || q.type!=type || !Handles(q.type) || q.owner!=player.whoAmI)return null;
            if(type==189)obstacles.CaptureBeeOwner();var targets=obstacles.CaptureWetChaseTargets(children[0]);var ambient=CaptureAmbient(obstacles,type);
            if(type!=189)obstacles.CaptureProjectileFamily(children);obstacles.BeginCandidate();
            return Family(children,ambient,targets,timeline,clock.FirstTick-1,0,terrain,obstacles,false);
        }
    }
}
