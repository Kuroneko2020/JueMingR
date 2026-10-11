using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Testing;

namespace JueMingR.TerrariaHost.Combat
{
    // Player.Update_HarpyCharm precedes ItemCheck_Shoot; AI001's homing tail
    // follows its motor on every projectile substep. Neither native method is
    // a getter (ApplyHarpyCharm sends packets/particles). This finite model
    // owns only copied scalars. Other NPCs retain their observed positions;
    // their future movement, damage effects and new attacks remain conditions.
    internal static class HostHarpyAttack
    {
        internal static bool Handles(Player player,AttackAmmoSnapshot ammo)
        {Projectile p;return player.accHarpyCharm && ammo!=null && ContentSamples.ProjectilesByType.TryGetValue(ammo.Projectile,out p) && p.arrow && p.aiStyle==1;}

        internal sealed class Arrow
        {
            internal Projectile Shot;
            internal int Slot,Type,Owner,Left,Penetrate,ExtraUpdates,Damage,BirthOuter;internal ProjectileKey Key;
            private readonly int width,height,updates,maxPenetrate,localCooldown;
            private readonly bool tileCollide,ignoreWater,friendly,localImmunity,staticImmunity;
            internal Vector2 Center,Velocity;internal float Counter,Second,Bits;
            internal bool Wet,Honey,Lava,Shimmer,Known,Primary,Burst,Alive=true,Bee,Killed,Representative,Reflected;
            internal Vector2 OldVelocity,DeathPosition;
            internal HostBeeAttack.State BeeState;
            internal HostAttackObstacles Receivers;internal Projectile ReceiverShot;
            internal AttackMotion Motion;internal int[] Local;
            internal Arrow(Projectile p,int slot)
            {
                Shot=p;Slot=slot;Type=p.type;Owner=p.owner;Key=p.key;Left=p.timeLeft;Penetrate=p.penetrate;ExtraUpdates=p.extraUpdates;Damage=p.damage;Center=p.Center;Velocity=p.velocity;Counter=p.ai[0];Second=p.ai[1];Bits=p.ai[2];Wet=p.wet;Honey=p.honeyWet;Lava=p.lavaWet;Shimmer=p.shimmerWet;
                width=p.width;height=p.height;updates=p.extraUpdates;maxPenetrate=p.maxPenetrate;localCooldown=p.localNPCHitCooldown;tileCollide=p.tileCollide;ignoreWater=p.ignoreWater;friendly=p.friendly;localImmunity=p.usesLocalNPCImmunity;staticImmunity=p.usesIDStaticNPCImmunity;
                Bee=HostBeeAttack.Handles(Type);BeeState=new HostBeeAttack.State(p);OldVelocity=p.oldVelocity;
                Known=Bee || HostAttackModels.TryResolved(Type,Math.Max(.0001f,Velocity.Length()),0,out Motion) || HostSpecialArrowAttack.TrySeedMotion(Type,Math.Max(.0001f,Velocity.Length()),0,out Motion);Local=(int[])p.localNPCImmunity.Clone();
            }
            internal Arrow Copy(){var a=(Arrow)MemberwiseClone();a.Local=(int[])Local.Clone();a.Receivers=null;a.ReceiverShot=null;return a;}
            internal bool MemberSame(Projectile p)
            {return ReferenceEquals(Shot,p) && p.active && Key.Equals(p.key) && Type==p.type && Owner==p.owner && (Bee?HostBeeAttack.Handles(p.type) && p.aiStyle==36:p.arrow && p.aiStyle==1);}
            internal bool Same(Projectile p)
            {return MemberSame(p) && (!Bee || BeeState.Alpha==p.alpha) && width==p.width && height==p.height && updates==p.extraUpdates && maxPenetrate==p.maxPenetrate && localCooldown==p.localNPCHitCooldown && tileCollide==p.tileCollide && ignoreWater==p.ignoreWater && friendly==p.friendly && localImmunity==p.usesLocalNPCImmunity && staticImmunity==p.usesIDStaticNPCImmunity && Left==p.timeLeft && Penetrate==p.penetrate && Damage==p.damage && Center==p.Center && Velocity==p.velocity && Counter==p.ai[0] && Second==p.ai[1] && Bits==p.ai[2] && Wet==p.wet && Honey==p.honeyWet && Lava==p.lavaWet && Shimmer==p.shimmerWet && SameLocal(p.localNPCImmunity);}
            private bool SameLocal(int[] values){if(values.Length!=Local.Length)return false;for(int i=0;i<values.Length;i++)if(values[i]!=Local[i])return false;return true;}
        }
        internal sealed class Target
        {
            internal NPC Npc;internal NpcIdentity Identity;internal MotionRect Bounds;internal Rectangle Damage;
            internal Vector2 Offset;internal bool Chase,DontTakeDamage,Friendly,Immortal;internal int LifeMax,OwnerImmune;
            internal Target(Player owner,NPC npc,long session)
            {Npc=npc;Identity=CombatSelection.Identity(npc,session);Bounds=new MotionRect(npc.position.X,npc.position.Y,npc.width,npc.height);Damage=CombatSelection.ReceiveBounds(npc);Offset=npc.netOffset;Chase=npc.CanBeChasedBy();DontTakeDamage=npc.dontTakeDamage;Friendly=npc.friendly;Immortal=npc.immortal;LifeMax=npc.lifeMax;OwnerImmune=npc.immune[owner.whoAmI];}
            internal bool Same(Player owner,long session)
            {return CombatSelection.Valid(Identity,session) && Bounds.X==Npc.position.X && Bounds.Y==Npc.position.Y && Bounds.Width==Npc.width && Bounds.Height==Npc.height && Damage==CombatSelection.ReceiveBounds(Npc) && Offset==Npc.netOffset && Chase==Npc.CanBeChasedBy() && DontTakeDamage==Npc.dontTakeDamage && Friendly==Npc.friendly && Immortal==Npc.immortal && LifeMax==Npc.lifeMax && OwnerImmune==Npc.immune[owner.whoAmI];}
        }
        internal sealed class State
        {
            private readonly Player owner;private readonly AttackAmmoSnapshot ammo;internal readonly PredictionPlayerMotion motion;
            private readonly bool harpy,strong,wallSourcePresent;internal readonly bool seraph,phoenix,randomSlots,quiver;internal readonly int cooldown;private readonly long session;
            internal readonly Arrow[] arrows;internal readonly Target[] targets;internal readonly bool[] occupied;
            internal readonly HostAttackObstacles obstacles;internal readonly HostProjectileTargets beeTargets;private readonly HostProjectileEnvironment.State environment;
            internal int CandidateCount,MotorSteps,CharmChecks,CapturedArrowSlots,CapturedNpcSlots,BeeBirths,BeeSteps,ReceivePasses;internal PredictionStop Stop;
            internal State(Player owner,AttackAmmoSnapshot ammo,NpcTrajectory timeline,HostAttackObstacles obstacles)
            {
                this.owner=owner;this.ammo=ammo;this.obstacles=obstacles;session=timeline.Identity.Session;wallSourcePresent=timeline.WallSources!=null;motion=NpcPredictionSource.ReadPlayer(owner);harpy=owner.accHarpyCharm;strong=owner.strongBees;seraph=owner.accSeraphNecklace;phoenix=owner.hasPhoenixQuiver;quiver=owner.magicQuiver;randomSlots=DebugOptions.Shared_RandomizeProjectileSlots;cooldown=owner.accHarpyCharmCooldown;environment=new HostProjectileEnvironment.State(true);
                var list=new List<Arrow>();bool bees=ammo!=null && ammo.Projectile==469;occupied=new bool[Main.maxProjectiles];for(int i=0;i<occupied.Length;i++){CapturedArrowSlots++;var p=Main.projectile[i];occupied[i]=p!=null && p.active;if(Owned(owner,p) || OwnedBee(owner,p)){list.Add(new Arrow(p,i));bees|=p.type==469 || HostBeeAttack.Handles(p.type);}}arrows=list.ToArray();
                // Native Damage_PVE is gated by owner==Main.myPlayer. Foreign
                // bees still occupy captured birth slots, but their local AI
                // cannot write shared static181 or local owner immunity.
                if(bees){var parent=new Projectile();parent.SetDefaults(469);parent.owner=owner.whoAmI;beeTargets=HostBeeAttack.CaptureTargets(parent,obstacles);obstacles.CaptureBeeOwner();}
                var npcs=new List<Target>();for(int i=0;i<Main.maxNPCs;i++){CapturedNpcSlots++;if(Main.npc[i]!=null && Main.npc[i].active)npcs.Add(new Target(owner,Main.npc[i],session));}targets=npcs.ToArray();
            }
            // The renderer only checks owner scalars and already captured
            // identities. Full new-arrow/new-target set checks belong to actual
            // preparation/consumption, never Draw's same-update lease.
            internal bool MembersCurrent
            {get{if(owner.accHarpyCharm!=harpy || beeTargets!=null && owner.strongBees!=strong || owner.accSeraphNecklace!=seraph || owner.hasPhoenixQuiver!=phoenix || randomSlots!=DebugOptions.Shared_RandomizeProjectileSlots || owner.accHarpyCharmCooldown!=cooldown || owner.position.X!=motion.X || owner.position.Y!=motion.Y || owner.velocity.X!=motion.Vx || owner.velocity.Y!=motion.Vy || !ammo.MembersMatch(owner,owner.HeldItem) || !environment.Current)return false;foreach(var a in arrows)if(!a.MemberSame(Main.projectile[a.Slot]))return false;return true;}}
            internal bool Current
            // Match the enclosing fixed-input plan: only the unused no-Wall
            // regen clocks may advance. Wall, health and immunity stay exact.
            {get{if(!MembersCurrent || !PlayerMotionContinuation.SameFixedInput(motion,NpcPredictionSource.ReadPlayer(owner),wallSourcePresent) || !obstacles.Unchanged)return false;int a=0,n=0;for(int i=0;i<occupied.Length;i++){var p=Main.projectile[i];if(occupied[i]!=(p!=null && p.active))return false;if(Owned(owner,p) || OwnedBee(owner,p)){if(a>=arrows.Length || arrows[a].Slot!=i || !arrows[a++].Same(p))return false;}}for(int i=0;i<Main.maxNPCs;i++)if(Main.npc[i]!=null && Main.npc[i].active){if(n>=targets.Length || targets[n].Identity.Slot!=i || !targets[n++].Same(owner,session))return false;}return a==arrows.Length && n==targets.Length;}}
        }
        private static bool HasPrimary(List<Arrow> arrows)
        {foreach(var arrow in arrows)if(arrow.Primary && arrow.Alive)return true;return false;}
        private static void Receivers(Arrow a,State lease,HostAttackObstacles parent=null,bool copyLocal=false)
        {
            var p=a.Shot;var q=new Projectile();q.SetDefaults(a.Type);q.owner=a.Owner;q.whoAmI=a.Slot;q.active=true;q.damage=a.Damage;q.friendly=p.friendly;q.hostile=p.hostile;q.npcProj=p.npcProj;q.trap=p.trap;
            q.melee=p.melee;q.ranged=p.ranged;q.magic=p.magic;
            q.width=p.width;q.height=p.height;q.penetrate=a.Penetrate;q.maxPenetrate=p.maxPenetrate;q.usesOwnerMeleeHitCD=p.usesOwnerMeleeHitCD;
            q.usesLocalNPCImmunity=p.usesLocalNPCImmunity;q.usesIDStaticNPCImmunity=p.usesIDStaticNPCImmunity;q.immunityIdentity=p.immunityIdentity;q.localNPCHitCooldown=p.localNPCHitCooldown;q.idStaticNPCHitCooldown=p.idStaticNPCHitCooldown;q.appliesImmunityTimeOnSingleHits=p.appliesImmunityTimeOnSingleHits;
            Array.Copy(a.Local,q.localNPCImmunity,a.Local.Length);a.ReceiverShot=q;a.Receivers=(parent??lease.obstacles).ForkForSpawn(q,copyLocal);
        }
        private static bool Owned(Player owner,Projectile p){return p!=null && p.active && p.owner==owner.whoAmI && p.arrow && p.aiStyle==1;}
        private static bool OwnedBee(Player owner,Projectile p){return p!=null && p.active && p.owner==owner.whoAmI && HostBeeAttack.Handles(p.type) && p.aiStyle==36;}

        private static MotionRect Bounds(Target target,NpcTrajectory timeline,int tick,bool damage)
        {if(target.Identity.Slot==timeline.Identity.Slot)return damage?timeline[tick].ProjectileReceiveBounds:timeline[tick].Bounds;var b=target.Bounds;if(damage){var r=target.Damage;return target.Identity.Type==414?new MotionRect(r.X-8,r.Y-8,r.Width+16,r.Height+16):new MotionRect(r.X,r.Y,r.Width,r.Height);}return b;}
        private static bool Find(Arrow a,Target[] targets,NpcTrajectory timeline,int tick,PredictionTerrain terrain,out Vector2 point,out bool found)
        {
            point=Vector2.Zero;found=false;float best=1600;
            foreach(var target in targets)
            {
                int slot=target.Identity.Slot;if(!target.Chase || a.Local[slot]!=0 || slot==timeline.Identity.Slot && !timeline[tick].CanReceive)continue;
                var b=Bounds(target,timeline,tick,false);var center=new Vector2(b.CenterX,b.CenterY);float distance=Vector2.Distance(a.Center,center);if(distance>=best)continue;
                bool clear;PredictionStop stop;if(!terrain.CanHit(new MotionRect(a.Center.X-a.Shot.width*.5f,a.Center.Y-a.Shot.height*.5f,a.Shot.width,a.Shot.height),b,out clear,out stop))return false;
                if(clear){best=distance;point=center;found=true;}
            }
            return true;
        }
        private static void Turn(Arrow a,Vector2 target)
        {a.Velocity=Utils.rotateTowards(a.Center,a.Velocity,target,MathHelper.Pi);}
        private static void Kill(Arrow a)
        {if(!a.Alive)return;a.DeathPosition=a.Center-new Vector2(a.Shot.width,a.Shot.height)*.5f;a.Alive=false;a.Left=0;a.Killed=true;}
        private static bool Charm(List<Arrow> arrows,Target[] targets,NpcTrajectory timeline,int tick,PredictionTerrain terrain,Vector2 ownerCenter,bool seraph,bool phoenix,ref int cooldown,State lease)
        {
            if(cooldown>0){cooldown--;return true;}Arrow best=null;Vector2 destination=Vector2.Zero;float nearest=float.MaxValue;
            foreach(var a in arrows)
            {
                if(!a.Alive || a.Bee || new Projectile.AI_001_Features(a.Bits).HarpyCharmed)continue;
                float distance=Vector2.Distance(ownerCenter,a.Center);if(distance<400 || distance>1200 || distance>=nearest)continue;
                lease.CharmChecks++;bool found;Vector2 point;if(!Find(a,targets,timeline,tick,terrain,out point,out found))return false;
                if(found){nearest=distance;best=a;destination=point;}
            }
            if(best!=null){Turn(best,destination);var f=new Projectile.AI_001_Features(best.Bits);f.HarpyCharmed=true;f.PhoenixQuivered|=phoenix;f.SeraphNecklaced|=!phoenix && seraph;best.Bits=f.Bits;best.Left=Math.Min(best.Left,300);cooldown=seraph?72:90;}return true;
        }
        private static bool Step(Arrow a,Target[] targets,NpcTrajectory timeline,int tick,PredictionTerrain terrain)
        {
            if(a.Left<=0){a.Alive=false;return true;}
            if(!a.Known)return false;
            Vector2 half=new Vector2(a.Shot.width,a.Shot.height)*.5f,position=a.Center-half;
            if(position.X<=Main.leftWorld || position.X+a.Shot.width>=Main.rightWorld || position.Y<=Main.topWorld || position.Y+a.Shot.height>=Main.bottomWorld || float.IsNaN(position.X) || float.IsNaN(position.Y))
            {a.Alive=false;return true;} // Native free-flight retire does not call Kill.
            a.OldVelocity=a.Velocity;
            Vector2 wetVelocity=a.Velocity; // native Update captures before AI
            if(a.Type==469)
            {
                // A water AI Kill creates bees here, before the dead parent
                // completes HandleMovement. Keep that Kill-entry position.
                if(a.Wet && !a.Honey && !a.Shimmer)Kill(a);
                if(a.Counter>=15){a.Counter=15;a.Velocity.Y+=.1f;}if(a.Velocity.Y>16)a.Velocity.Y=16;
            }
            else if(HostSpecialArrowAttack.Handles(a.Type))
            {bool alive;if(!HostSpecialArrowAttack.Motor(a.Type,ref a.Counter,a.Second,a.Wet,a.Honey,a.Shimmer,ref a.Velocity,out alive)){a.Known=false;return false;}if(!alive){a.Alive=false;return true;}}
            else
            {
                a.Counter++;float x=0,y=0,vx=a.Velocity.X,vy=a.Velocity.Y;
                var motion=a.Motion;if(a.Type==639)motion=new AttackMotion(motion.Speed,a.Left<=(a.ExtraUpdates+1)*45-14?.1f:0,0,motion.Updates,motion.Width,motion.Height,motion.Lifetime,maxFallSpeed:16);
                motion.Advance(ref x,ref y,ref vx,ref vy,(int)a.Counter);a.Velocity=new Vector2(vx,vy);
            }
            var f=new Projectile.AI_001_Features(a.Bits);
            if(f.HarpyCharmed && !f.HarpyCharmConsumed)
            {Vector2 point;bool found;if(!Find(a,targets,timeline,tick,terrain,out point,out found))return false;if(found){Turn(a,point);if(a.Velocity.Length()<1)a.Velocity=a.Velocity.SafeNormalize(Vector2.UnitY);if(a.Velocity.Length()<12)a.Velocity*=1.05f;}else{f.HarpyCharmConsumed=true;a.Bits=f.Bits;}}
            float wind;if(!HostProjectileEnvironment.Impulse(a.Shot,terrain,a.Center.X,a.Center.Y,a.Velocity.X,out wind))return false;a.Velocity.X+=wind;
            if(a.Owner<255 && !Main.player[a.Owner].active)Kill(a);
            bool wet,lava;byte kind;if(!terrain.ProjectileWet(a.Center.X,a.Center.Y,a.Shot.width,a.Shot.height,out wet,out kind,a.Type==469) || a.Type!=469 && (wet && kind==3 || a.Shimmer))return false;
            if(!a.Shot.ignoreWater)
            {
                if(!terrain.ProjectileLava(a.Center.X,a.Center.Y,a.Shot.width,a.Shot.height,out lava,a.Type==469))return false;if(lava)a.Lava=true;if(wet && kind==2)a.Honey=true;if(wet && kind==3)a.Shimmer=true;
                // TransformType only changes type; it preserves AI/features,
                // dimensions and the already snapshotted extra-update clock.
                // This is before movement/Damage, after this substep's fire AI.
                if(a.Wet && !a.Lava){if(ProjectileID.Sets.IsDestroyedByWater[a.Type]){a.Alive=false;return true;}if(a.Type==2)a.Type=1;}
                a.Wet=wet;if(!wet)a.Lava=a.Honey=a.Shimmer=false;
            }
            if(a.Shimmer){a.Velocity.Y-=.2f;if(a.Velocity.Y<0)Kill(a);}
            Vector2 velocity=a.Velocity;bool wetMotion=a.Wet && !a.Shot.ignoreWater,hit=false;
            if(a.Shot.tileCollide && !terrain.ProjectileTileMotion(a.Shot,ref position,ref velocity,wetMotion,a.Honey,out wetVelocity,out hit,true,a.Shimmer,a.Type==469))return false;
            a.Center=position+half;a.Velocity=velocity;if(hit){if(a.Type==710){a.Center+=velocity;a.Burst=true;}else if(a.Type==469){a.Center+=velocity;Kill(a);a.Center+=wetMotion?wetVelocity:velocity;return true;}a.Alive=false;return true;}
            a.Center+=wetMotion?wetVelocity:velocity;
            // Damage precedes the native lifetime tail. In particular a
            // timeLeft1 BeeArrow can hit, then Kill at this moved body.
            if(a.Alive)a.Left--;return true;
        }
        private static void QuiverAfterMovement(Arrow arrow,bool quiver)
        {if(quiver && arrow.Shot.arrow && !arrow.Shot.npcProj && arrow.Shot.friendly && arrow.ExtraUpdates<1)arrow.ExtraUpdates=1;}
        private static int Allocate(bool[] occupied)
        {for(int i=0;i<occupied.Length;i++)if(!occupied[i]){occupied[i]=true;return i;}return -1;}
        private static bool PhoenixChildren(Arrow parent,List<Arrow> arrows,bool[] occupied,Player player,State lease,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,int birthOuter)
        {
            if(parent.Second==-1 || parent.Owner!=Main.myPlayer)return true;
            // A random-slot allocator needs native RNG which this private
            // model never runs. Only this unresolved spawning event retires;
            // enabling Harpy itself still retains its known motor/hand stage.
            if(lease.randomSlots){lease.Stop=PredictionStop.UnsupportedMechanism;return false;}
            for(int i=0;i<5;i++)
            {
                int slot=Allocate(occupied);if(slot<0){lease.Stop=PredictionStop.UnsupportedMechanism;return false;}
                var shot=new Projectile();shot.SetDefaults(710);shot.owner=parent.Owner;shot.damage=parent.Damage;
                var child=new Arrow(shot,slot){Center=parent.Center,Velocity=HostSpecialArrowAttack.PhoenixShardVelocity(i),Counter=0,Second=-1,Bits=0,Primary=parent.Primary,BirthOuter=birthOuter,Local=(int[])parent.Local.Clone()};
                bool wet;byte kind;if(!terrain.ProjectileWet(child.Center.X,child.Center.Y,shot.width,shot.height,out wet,out kind) || wet && kind==3){lease.Stop=PredictionStop.TerrainUnavailable;return false;}child.Wet=!shot.ignoreWater && wet;child.Honey=wet && kind==2;child.Lava=false;
                Receivers(child,lease,parent.Receivers,true);arrows.Add(child);
            }
            return true;
        }
        private static bool BeeChildren(Arrow parent,List<Arrow> arrows,bool[] occupied,State lease,PredictionTerrain terrain,int birthOuter)
        {
            if(parent.Owner!=Main.myPlayer)return true;
            if(lease.randomSlots){lease.Stop=PredictionStop.UnsupportedMechanism;return false;}
            var children=HostBeeAttack.BirthMembersRepresentative(parent.ReceiverShot,parent.DeathPosition,parent.OldVelocity,false,()=>Allocate(occupied),terrain,lease.obstacles);
            if(children==null){lease.Stop=PredictionStop.UnsupportedMechanism;return false;}
            lease.BeeBirths++;
            // Random future child count/velocity can also affect a later
            // source through shared static immunity. Its resulting contact
            // cannot retain the captured-source Conditional confidence.
            foreach(var member in arrows)member.Representative=true;
            foreach(var shot in children)
            {
                // Only descendants of the selected source are causal. Other
                // owned bees still write real shared181 immunity, never red.
                var child=new Arrow(shot,shot.whoAmI){Primary=parent.Primary,Representative=true,BirthOuter=birthOuter};
                Receivers(child,lease);arrows.Add(child);
            }
            return true;
        }
        private static bool Damage(Arrow a,List<Arrow> arrows,bool[] occupied,Player player,State lease,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,int step,int tick,int sub,Vector2 aim,AttackConfidence confidence,bool burst,out AttackContact contact)
        {
            contact=null;if(a.Damage<=0 || !a.Shot.friendly)return true;
            int width=burst?40:a.Shot.width,height=burst?40:a.Shot.height;
            AttackContact received=null;bool dependency=true;
            Func<int,int,bool> onHit=(slot,penetration)=>
            {
                if(a.Primary && slot==timeline.Identity.Slot && received==null)
                    received=AttackIntercept.BodyContact(timeline,tick,sub,aim.X,aim.Y,a.Center.X,a.Center.Y,width,height,a.Representative || lease.randomSlots?AttackConfidence.Representative:confidence);
                if(!a.Bee)
                {
                    var features=new Projectile.AI_001_Features(a.Bits);features.HarpyCharmed=features.HarpyCharmConsumed=true;a.Bits=features.Bits;
                    if(a.Shot.usesLocalNPCImmunity && a.Shot.localNPCHitCooldown!=-2)a.Local[slot]=a.Shot.localNPCHitCooldown;
                    // RecordHit already wrote the actual local/owner state.
                    // Native copies it to710 children BEFORE spending this hit.
                    if(a.Type==710 && !burst && !PhoenixChildren(a,arrows,occupied,player,lease,timeline,clock,terrain,step+1)){dependency=false;return false;}
                }
                return true;
            };
            lease.ReceivePasses++;
            // k2 never resets family history. Ambient selected hits continue
            // through higher NPC slots in this SAME native Damage call.
            bool zero=a.Penetrate<=0;
            bool pass=a.Receivers.PassCustomOuter(2,tick,step+1,a.Center.X,a.Center.Y,width,height,null,zero,a.Penetrate>0?a.Penetrate:0,true,onHit);
            if(!zero)a.Penetrate=a.Receivers.Remaining;
            if(received!=null){contact=received;return true;}
            if(!dependency)return false;
            if(!pass)
            {
                if(a.Receivers.Remaining>0){lease.Stop=PredictionStop.UnsupportedMechanism;return false;}
                if(!a.Bee){if(a.Type==710)a.Burst=!burst;Kill(a);}
            }
            return true;
        }
        private static void BeeValues(Arrow a)
        {
            var state=a.BeeState;a.Center=state.Position+new Vector2(a.Shot.width,a.Shot.height)*.5f;a.Velocity=state.Velocity;a.Counter=state.Counter;a.Left=state.Left;a.Penetrate=state.Penetration;a.ExtraUpdates=state.Extra;a.Alive=state.Alive;
            a.Wet=state.Wet;a.Honey=state.Honey;a.Lava=state.Lava;a.Shimmer=state.Shimmer;
        }
        private static bool Outer(List<Arrow> arrows,bool[] occupied,Player player,State state,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,int step,int tick,Vector2 aim,AttackConfidence confidence,out AttackContact contact,int afterSlot=-1)
        {
            contact=null;int lastSlot=afterSlot;
            // AI087's NPC scan precedes every projectile Update in this
            // outer. Children born later in projectile AI/Kill cannot be
            // retroactively reflected by a scan they were not present for.
            if(clock.Phase!=HostAttackPhase.Projectiles || step>0)
                foreach(var member in arrows)if(member.Alive && !member.Bee && member.BirthOuter<step+1 && !member.Receivers.BeforeNpc(tick,step+1,member.Center.X,member.Center.Y,member.Shot.width,member.Shot.height))
                {
                    // Reflection retires this member's useful friendly motor,
                    // not its native slot or the independent remaining family.
                    // It is not a Kill and must not manufacture death children.
                    member.Alive=false;member.Reflected=true;
                }
            for(int visits=0;visits<occupied.Length;visits++)
            {
                // Births reserve the first currently free native slot while
                // their parent is STILL occupied. Upper slots run this outer;
                // lower slots wait. Charm sees only actual AI001 arrows.
                Arrow a=null;foreach(var member in arrows)if(member.Alive && member.Slot>lastSlot && (a==null || member.Slot<a.Slot))a=member;if(a==null)break;lastSlot=a.Slot;
                if(a.Shot.usesLocalNPCImmunity)for(int i=0;i<a.Local.Length;i++)if(a.Local[i]>0)a.Local[i]--;
                int updates=a.ExtraUpdates+1;
                for(int sub=0;sub<updates;sub++)
                {
                    state.MotorSteps++;
                    if(a.Bee)
                    {
                        state.BeeSteps++;
                        if(!HostBeeAttack.Step(a.Shot,state.beeTargets,timeline,tick,terrain,ref a.BeeState)){state.Stop=PredictionStop.TerrainUnavailable;return false;}BeeValues(a);
                        if(a.Alive)
                        {
                            if(!Damage(a,arrows,occupied,player,state,timeline,clock,terrain,step,tick,sub,aim,confidence,false,out contact))return false;if(contact!=null)return true;
                            a.BeeState.Penetration=a.Penetrate;
                        }
                        HostBeeAttack.Tail(ref a.BeeState);BeeValues(a);
                        if(a.BeeState.Stopped)break;
                        // A tail Kill leaves one inactive HandleMovement in a
                        // pending entry subcycle. Step skips AI there, exactly
                        // as the existing complete Bee motor oracle requires.
                        continue;
                    }
                    if(!a.Alive)break;
                    if(!Step(a,state.targets,timeline,tick,terrain)){state.Stop=a.Known?PredictionStop.TerrainUnavailable:PredictionStop.UnsupportedMechanism;return false;}
                    if(a.Alive)
                    {
                        QuiverAfterMovement(a,state.quiver);
                        if(!Damage(a,arrows,occupied,player,state,timeline,clock,terrain,step,tick,sub,aim,confidence,false,out contact))return false;if(contact!=null)return true;
                        if(a.Alive && a.Left<=0)Kill(a);
                    }
                    if(a.Burst)
                    {a.Second=-1;if(!Damage(a,arrows,occupied,player,state,timeline,clock,terrain,step,tick,sub,aim,confidence,true,out contact))return false;if(contact!=null)return true;a.Burst=false;}
                    if(a.Killed && a.Type==469)
                    {if(!BeeChildren(a,arrows,occupied,state,terrain,step+1))return false;a.Killed=false;}
                    if(!a.Alive)break;
                }
                if(!a.Alive && !a.Reflected)occupied[a.Slot]=false;
            }
            return true;
        }
        // A born family is independent of unborn fan representatives. It uses
        // the same ordered motor/receive loop and captures real features/local
        // immunity rather than inventing a fresh launch or running native AI.
        internal static AttackContact KnownFamily(Player player,Projectile[] primary,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain)
        {return KnownCore(player,primary,timeline,clock,terrain,-1);}
        // Called only at an observed native parent Kill boundary in the
        // projectile loop. Lower born slots have already missed this outer;
        // upper births and preexisting later arrows still get their Update.
        // This entry observes a causal set and grants no new hand input.
        internal static AttackContact KnownAfterSlot(Player player,Projectile[] primary,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,int afterSlot)
        {return clock.Phase==HostAttackPhase.Projectiles && afterSlot>=0 && afterSlot<Main.maxProjectiles?KnownCore(player,primary,timeline,clock,terrain,afterSlot):null;}
        private static AttackContact KnownCore(Player player,Projectile[] primary,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,int firstAfterSlot)
        {
            if(primary==null || primary.Length==0 || clock.LaunchDelay!=0)return null;foreach(var shot in primary)if(!Owned(player,shot) && !OwnedBee(player,shot))return null;
            var ammo=AttackAmmoSnapshot.Capture(player,player.HeldItem);if(ammo==null)return null;
            var obstacles=new HostAttackObstacles(player,primary[0],timeline,0,clock.BeforeNpc,clock.NextWorld);var state=new State(player,ammo,timeline,obstacles);obstacles.BeginCandidate();var arrows=new List<Arrow>();
            foreach(var a in state.arrows){var row=a.Copy();foreach(var shot in primary)if(ReferenceEquals(row.Shot,shot)){row.Primary=true;break;}Receivers(row,state);arrows.Add(row);}
            var occupied=(bool[])state.occupied.Clone();var owner=state.motion;int cooldown=state.cooldown;var environment=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld,Expert=Main.expertMode,WallViewWidth=Main.MaxWorldViewSize.X};int limit=Math.Min(120,timeline.Count-clock.Age-1);
            NpcPredictionSource.TryBindPlayerGravity(timeline,(long)Main.GameUpdateCount,ref owner,ref environment);
            NpcPredictionSource.TryBindPlayerWall(timeline,(long)Main.GameUpdateCount,ref owner,ref environment);
            for(int step=0;step<limit && HasPrimary(arrows);step++)
            {
                int tick=clock.Age+step+(clock.Phase==HostAttackPhase.Projectiles?0:1);if(tick<0 || tick>=timeline.Count)return null;
                if(clock.MovePlayer(step)){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref owner,environment,terrain,out stop) || !Charm(arrows,state.targets,timeline,Math.Max(0,tick-1),terrain,new Vector2(owner.X+owner.Width*.5f,owner.Y+owner.Height*.5f),state.seraph,state.phoenix,ref cooldown,state))return null;}
                AttackContact contact;if(!Outer(arrows,occupied,player,state,timeline,clock,terrain,step,tick,primary[0].Center,AttackConfidence.Conditional,out contact,step==0?firstAfterSlot:-1))return null;if(contact!=null)return contact;
            }
            return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory timeline,Vector2 origin,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles,out State state)
        {
            state=new State(player,ammo,timeline,obstacles);AttackMotion primaryMotion;if(!Handles(player,ammo) || !(HostAttackModels.TryRead(ammo,out primaryMotion) || HostSpecialArrowAttack.TrySeedMotion(ammo,out primaryMotion)))return null;
            int baseAge=clock.Age-clock.LaunchDelay,limit=Math.Min(120,timeline.Count-baseAge-1);if(baseAge<0 || clock.LaunchDelay>=limit)return null;
            
            var sample=ContentSamples.ProjectilesByType[ammo.Projectile];int damage=ammo.Projectile==469?ammo.Damage:player.GetWeaponDamage(player.HeldItem);var directions=new Vector2[161];int count=0;var environment=new PredictionEnvironment{WorldWidth=Main.maxTilesX,WorldHeight=Main.maxTilesY,GravityWorldSurface=Main.worldSurface,Remix=Main.remixWorld,Expert=Main.expertMode,WallViewWidth=Main.MaxWorldViewSize.X};
            var initialOwner=state.motion;NpcPredictionSource.TryBindPlayerGravity(timeline,(long)Main.GameUpdateCount,ref initialOwner,ref environment);
            NpcPredictionSource.TryBindPlayerWall(timeline,(long)Main.GameUpdateCount,ref initialOwner,ref environment);
            for(int seed=ammo.Projectile==710?-(limit-clock.LaunchDelay):0;seed<41;seed++)
            {
                Vector2 direction;if(seed<0){int outer=limit-clock.LaunchDelay+seed+1,tick=clock.FirstTick+outer-1;if(tick<0 || tick>=timeline.Count || !HostSpecialArrowAttack.PhoenixSeed(ammo,timeline,origin,tick,primaryMotion.SubupdatesForOuters(outer),player.direction,out direction))continue;}else direction=seed<9?(new Vector2(timeline[Math.Min(timeline.Count-1,clock.FirstTick+seed*4)].Bounds.CenterX,timeline[Math.Min(timeline.Count-1,clock.FirstTick+seed*4)].Bounds.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();
                bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;state.CandidateCount++;
                var arrows=new List<Arrow>();state.obstacles.BeginCandidate();foreach(var a in state.arrows){var row=a.Copy();Receivers(row,state);arrows.Add(row);}var occupied=(bool[])state.occupied.Clone();var owner=initialOwner;int cooldown=state.cooldown;Arrow primary=null;Vector2 aim=origin+direction*10000;
                for(int step=0;step<limit;step++)
                {
                    int tick=baseAge+step+(clock.Phase==HostAttackPhase.Projectiles?0:1);if(tick<0 || tick>=timeline.Count)break;
                    if(clock.MovePlayer(step))
                    {PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref owner,environment,terrain,out stop)){state.Stop=stop;break;}if(!Charm(arrows,state.targets,timeline,Math.Max(0,tick-1),terrain,new Vector2(owner.X+owner.Width*.5f,owner.Y+owner.Height*.5f),state.seraph,state.phoenix,ref cooldown,state)){state.Stop=PredictionStop.TerrainUnavailable;break;}}
                    if(step==clock.LaunchDelay)
                    {
                        // PhantomPhoenix creates six actual unequal-speed
                        // arrows before the projectile loop. Capture all six
                        // so the following charm sees their true contention;
                        // the ordinary single-birth lease remains unchanged.
                        int births=ammo.WeaponType==3859?6:1;Vector2 raw=direction*ammo.Speed;
                        for(int born=0;born<births;born++)
                        {
                            int slot=Allocate(occupied);if(slot<0)break;
                            Vector2 launch=HostSpecialArrowAttack.BirthVelocity(ammo,raw,player.direction,-2.5f+born);var resolved=primaryMotion.ForLaunch(raw.X);resolved.Launch(ref launch.X,ref launch.Y);primary=new Arrow(sample,slot){Owner=player.whoAmI,Center=origin+new Vector2(owner.X-state.motion.X,owner.Y-state.motion.Y),Velocity=launch,Counter=ammo.WeaponType==3019?raw.X:0,Second=ammo.WeaponType==3019?raw.Y:0,Bits=0,Motion=primaryMotion,Known=true,Primary=true,Damage=damage};
                            bool wet;byte kind;if(!terrain.ProjectileWet(primary.Center.X,primary.Center.Y,sample.width,sample.height,out wet,out kind,sample.type==469) || wet && kind==3 && sample.type!=469)break;primary.Shimmer=wet && kind==3;primary.Wet=!sample.ignoreWater && wet;primary.Honey=wet && kind==2;primary.Lava=false;Receivers(primary,state);arrows.Add(primary);
                        }
                        arrows.Sort((a,b)=>a.Slot.CompareTo(b.Slot));if(primary==null)break;
                    }
                    AttackContact contact;if(!Outer(arrows,occupied,player,state,timeline,clock,terrain,step,tick,aim,primaryMotion.Confidence,out contact))break;if(contact!=null)return contact;if(primary!=null && !HasPrimary(arrows))break;
                }
            }
            return null;
        }
    }
}
