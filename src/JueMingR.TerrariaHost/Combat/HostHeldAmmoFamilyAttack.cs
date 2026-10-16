using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // One native firing event owns its ENTIRE selected-ammo batch. Its private
    // scene keeps NPC order, parent-occupied births and shared receive writes;
    // a lower child slot cannot act in the parent's current update loop.
    internal static class HostHeldAmmoFamilyAttack
    {
        private static readonly MethodInfo clone=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        private static int[] types;
        private static int[] Types
        {
            get
            {
                if(types!=null)return types;var set=new HashSet<int>{90,92,117,181,469,485,566,616,631,640,661,706,710,932,1124};
                foreach(var item in ContentSamples.ItemsByType.Values)if((item.ammo==AmmoID.Arrow || item.ammo==AmmoID.Bullet) && item.shoot>0)set.Add(item.shoot);types=set.OrderBy(t=>t).ToArray();return types;
            }
        }
        internal static bool Family(int type){return Array.IndexOf(Types,type)>=0;}
        internal sealed class Snapshot
        {
            internal Projectile[] Scene;internal HostProjectileBirthSlots Slots;
            internal HostProjectileTargets Steering,VortexLocked,Echo,Bee;
            internal HostHarpyAttack.State HarpyState;
            internal bool PhoenixApprentice,Quiver;
        }
        private sealed class Member
        {internal Projectile Shot;internal HostAttackObstacles Receive;internal HostPhoenixAttack.State Phoenix;internal HostHarpyAttack.Arrow Special;internal HostBeeAttack.State Bee;internal bool BeeReady;internal bool Source,Representative;internal int ReadyOuter,ImmuneOuter=-1;}
        private static Projectile Copy(Projectile p)
        {
            // Replay an already captured body, not another native spawn. Keep
            // every scalar (including its key) and detach all mutable arrays;
            // SetDefaults would repeat allocation/initialization per direction.
            var q=(Projectile)clone.Invoke(p,null);q.ai=(float[])p.ai.Clone();q.localAI=(float[])p.localAI.Clone();q.localNPCImmunity=(int[])p.localNPCImmunity.Clone();
            if(p.oldPos!=null)q.oldPos=(Vector2[])p.oldPos.Clone();if(p.oldRot!=null)q.oldRot=(float[])p.oldRot.Clone();if(p.oldSpriteDirection!=null)q.oldSpriteDirection=(int[])p.oldSpriteDirection.Clone();return q;
        }
        private static Member Make(Projectile p,HostAttackObstacles root,bool source,bool representative=false,int ready=0)
        {
            // The receive lease owns immutable INITIAL local counters. The
            // detached motor body advances native counters/OnHit separately;
            // otherwise its own future writes would invalidate consumption.
            var policy=p.usesLocalNPCImmunity?Copy(p):p;
            return new Member{Shot=p,Receive=root.ForkForSpawn(policy),Source=source,Representative=representative,ReadyOuter=ready};
        }
        internal static Snapshot CaptureForSolve(Player player,HostAttackObstacles root,int[] birthTypes=null)
        {
            root.CaptureHeldAmmoInputs(player);root.CaptureHomingInputs(player,false,true);
            var scene=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Array.IndexOf(Types,p.type)>=0){scene.Add(p);root.CaptureAmmoImpactStatic(p);}
            root.CaptureOwnedProjectileFamily(scene.ToArray(),player.whoAmI,Types);var s=new Snapshot{Scene=scene.ToArray(),Slots=root.CaptureBirthSlots(),Quiver=player.magicQuiver};
            bool Has(int type){return scene.Any(p=>p.type==type) || birthTypes!=null && Array.IndexOf(birthTypes,type)>=0;}
            if(Has(706))s.PhoenixApprentice=root.CapturePhoenixOwner(player);
            if(Has(469) || Has(181) || Has(566)){var bee=Copy(ContentSamples.ProjectilesByType[181]);bee.owner=player.whoAmI;s.Bee=HostBeeAttack.CaptureTargets(bee,root);root.CaptureBeeOwner();}
            var sample=new Projectile();sample.SetDefaults(14);sample.owner=player.whoAmI;
            if(Has(207) || Has(225) || Has(285) || Has(36) || Has(616))s.Steering=root.CaptureChaseTargets(sample);
            if(Has(616)){sample.SetDefaults(616);sample.owner=player.whoAmI;s.VortexLocked=root.CaptureSeedlerTargets(sample);}
            if(Has(631) || player.phantasmTime>0 || player.HeldItem.shoot==630)s.Echo=root.CapturePhantasmTargets(sample);
            bool arrows=scene.Any(p=>p.arrow && p.aiStyle==1) || birthTypes!=null && birthTypes.Any(t=>ContentSamples.ProjectilesByType[t].arrow && ContentSamples.ProjectilesByType[t].aiStyle==1);
            if(Has(469) || Has(485) || Has(710) || arrows && (player.accHarpyCharm || scene.Any(p=>p.arrow && p.aiStyle==1 && new Projectile.AI_001_Features(p.ai[2]).HarpyCharmed && !new Projectile.AI_001_Features(p.ai[2]).HarpyCharmConsumed)))
            {s.HarpyState=root.CaptureHarpyBatch(player);}
            if(Has(286)){sample.SetDefaults(286);root.CaptureAmmoImpactStatic(sample);}return s;
        }
        private static IEnumerable<Projectile> Bodies(List<Member> rows)
        {foreach(var member in rows)yield return member.Shot;}
        private static bool Charm(List<Member> rows,Snapshot captured,NpcTrajectory path,int tick,PredictionTerrain terrain,Vector2 owner,ref int cooldown)
        {return captured.HarpyState==null || HostHarpyAttack.BeforePlayerCharm(Bodies(rows),captured.HarpyState,path,Math.Max(0,tick-1),terrain,owner,ref cooldown);}
        // Explicit finite AI001 ammo kernels only. The runtime ammo inventory
        // supplies legal types, while TryResolved remains the admission list.
        // Autonomous and bouncing bodies use their actual dedicated kernels.
        internal static int Ai(Projectile p,Snapshot captured,NpcTrajectory path,int tick,PredictionTerrain terrain)
        {
            if(p.type==616)return HostVortexRocketAttack.Ai(p,captured.Steering,captured.VortexLocked,path,tick,terrain)?0:-1;
            if(p.type==661)return HostOnyxBlastAttack.Ai(p);
            if(HostAmmoSteeringAttack.Handles(p.type))return HostAmmoSteeringAttack.Ai(p,captured.Steering,path,tick,terrain);
            if(HostArrowFamilyAttack.Family(p.type))return HostArrowFamilyAttack.Ai(p)?0:1;
            if(HostAmmoImpactAttack.Family(p.type))return HostAmmoImpactAttack.Ai(p)?0:1;
            AttackMotion ignored;if(p.aiStyle!=1 || !HostAttackModels.TryResolved(p.type,Math.Max(.0001f,p.velocity.Length()),0,out ignored))return -1;
            bool bullet=p.type==14 || p.type==20 || p.type==36 || p.type==104 || p.type==207 || p.type==242 || p.type==279 || p.type>=283 && p.type<=287 || p.type==638 || p.type==981;
            if(!bullet && p.type!=5)p.ai[0]++;
            if(bullet && p.type!=242 && p.type!=638 && p.type!=981)p.alpha=Math.Max(0,p.alpha-15);
            if(p.type==242)p.alpha=Math.Max(0,p.alpha-(byte)(p.velocity.Length()*.9));
            if(p.type==638)p.alpha=Math.Max(0,p.alpha-(byte)(p.velocity.Length()*.3));
            int start=p.type==172?17:15;float gravity=p.type==172?.085f:p.type==1006?-.1f:.1f;
            if(p.ai[0]>=start){p.ai[0]=start;p.velocity.Y+=gravity;}
            p.rotation=(float)Math.Atan2(p.velocity.Y,p.velocity.X)+1.57f;if(p.velocity.Y>16)p.velocity.Y=16;if(p.type==1006 && p.velocity.Y< -16)p.velocity.Y=-16;return 0;
        }
        private static bool Add(Projectile child,Member parent,List<Member> rows,HostProjectileBirthSlots pool,PredictionTerrain terrain,HostAttackObstacles root,int outer)
        {
            int slot;if(!pool.TryAllocate(out slot))return false;child.whoAmI=slot;child.active=true;child.owner=parent.Shot.owner;
            bool wet;byte kind;if(!terrain.ProjectileWet(child.Center.X,child.Center.Y,child.width,child.height,out wet,out kind,true))return false;child.wet=wet && !child.ignoreWater;child.honeyWet=wet && kind==2;child.shimmerWet=wet && kind==3;
            rows.Add(Make(child,root,parent.Source,true,outer+(slot<=parent.Shot.whoAmI?1:0)));return true;
        }
        private static void BeeValues(Member m)
        {
            var p=m.Shot;var b=m.Bee;p.position=b.Position;p.velocity=b.Velocity;p.ai[0]=b.Counter;p.timeLeft=b.Left;p.alpha=b.Alpha;p.extraUpdates=b.Extra;p.penetrate=b.Penetration;p.active=b.Alive;p.wet=b.Wet;p.honeyWet=b.Honey;p.lavaWet=b.Lava;p.shimmerWet=b.Shimmer;
        }
        private static bool SpecialChildren(Member parent,List<Member> rows,HostProjectileBirthSlots pool,PredictionTerrain terrain,HostAttackObstacles root,int outer)
        {
            var p=parent.Shot;if(p.type!=710 || p.ai[1]==-1 || p.owner!=Main.myPlayer)return true;
            for(int i=0;i<5;i++)
            {
                var child=Copy(ContentSamples.ProjectilesByType[710]);child.owner=p.owner;child.Center=p.Center;child.velocity=HostSpecialArrowAttack.PhoenixShardVelocity(i);child.damage=p.damage;child.ai[1]=-1;child.localNPCImmunity=(int[])p.localNPCImmunity.Clone();
                if(!Add(child,parent,rows,pool,terrain,root,outer))return false;
            }
            return true;
        }
        private static bool SpecialDeath(Member member,List<Member> rows,HostProjectileBirthSlots pool,Snapshot captured,NpcTrajectory path,int tick,int outer,int sub,Vector2 aim,PredictionTerrain terrain,HostAttackObstacles root,out AttackContact contact)
        {
            contact=null;var p=member.Shot;
            if(p.type==710)
            {
                var burst=Copy(p);burst.width=burst.height=40;burst.Center=p.Center;burst.ai[1]=-1;var receive=member.Receive.ForkForSpawn(burst,true);int hits=receive.SelectedHits;
                bool pass=receive.PassCustomOuter(2,tick,outer,burst.Center.X,burst.Center.Y,40,40,null,unlimited:burst.penetrate<=0,maximumRemaining:burst.penetrate>0?burst.penetrate:0,consumeSelected:true);
                if(member.Source && receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,burst.Center.X,burst.Center.Y,40,40,AttackConfidence.Representative);return true;}if(!pass && receive.Remaining>0)return false;
                p.ai[1]=-1;p.width=p.height=40;p.Center=burst.Center;
            }
            else if(p.type==469 && p.owner==Main.myPlayer)
            {
                var death=member.Special;var position=death!=null && death.Killed?death.DeathPosition:p.position;var old=death!=null?death.OldVelocity:p.oldVelocity;
                var children=HostBeeAttack.BirthMembersRepresentative(p,position,old,false,()=>{int slot;return pool.TryAllocate(out slot)?slot:-1;},terrain,root);if(children==null)return false;
                foreach(var row in rows)row.Representative=true;
                foreach(var child in children)rows.Add(Make(child,root,member.Source,true,outer+(child.whoAmI<=p.whoAmI?1:0)));
            }
            p.active=false;p.timeLeft=0;pool.Release(p.whoAmI);return true;
        }
        private static bool Echo(Member parent,int victim,List<Member> rows,HostProjectileBirthSlots pool,Snapshot captured,NpcTrajectory path,int tick,int outer,Vector2 ownerCenter,PredictionTerrain terrain,HostAttackObstacles root)
        {
            Vector2 target=Vector2.Zero;bool found=false;for(int i=0;i<captured.Echo.Count;i++)if(captured.Echo.Slot(i)==victim){var b=captured.Echo.Bounds(i,path,tick);target=new Vector2(b.CenterX,b.CenterY);found=true;break;}if(!found)return false;
            for(int delay=0;delay<=30;delay+=15){var child=new Projectile();child.SetDefaults(631);child.Center=ownerCenter;child.velocity=Vector2.Normalize(target-ownerCenter)*6;child.damage=(int)(parent.Shot.damage*.3f);child.ai[0]=victim;child.ai[1]=delay;if(!Add(child,parent,rows,pool,terrain,root,outer))return false;}return true;
        }
        private static bool Kill(Member member,List<Member> rows,HostProjectileBirthSlots pool,Snapshot captured,NpcTrajectory path,int tick,int outer,int sub,Vector2 aim,PredictionTerrain terrain,HostAttackObstacles root,out AttackContact contact)
        {
            contact=null;var p=member.Shot;if(HostSpecialArrowAttack.Handles(p.type))return SpecialDeath(member,rows,pool,captured,path,tick,outer,sub,aim,terrain,root,out contact);if(!p.active)return true;int oldCooldown=p.idStaticNPCHitCooldown;Projectile burst=null;bool damage=false;
            // Ordinary deaths have no same-body Damage consumer. Descendant
            // births below still run with the parent occupied, in native order.
            if(p.type==616 || p.type==661 || p.owner==Main.myPlayer && (p.type==41 || p.type==286))
            {burst=Copy(p);damage=HostAmmoImpactAttack.PrepareKillBurst(burst);if(p.type==616){HostVortexRocketAttack.KillBody(burst);damage=true;}if(p.type==661)damage=HostOnyxBlastAttack.PrepareKillBurst(burst);}
            if(damage && burst.damage>0)
            {
                bool collisionKnown=true;Func<MotionRect,bool> collider=burst.type==661?(Func<MotionRect,bool>)(b=>{bool known;bool hit=HostOnyxBlastAttack.Collides(burst,b,terrain,out known);if(!known)collisionKnown=false;return hit;}):null;
                var receive=member.Receive.ForkForSpawn(burst,true);int hits=receive.SelectedHits;bool pass=receive.PassCustomOuter(2,tick,outer,burst.Center.X,burst.Center.Y,burst.width,burst.height,collider,unlimited:burst.penetrate<=0,consumeSelected:true);
                if(!collisionKnown)return false;
                if(member.Source && receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,burst.Center.X,burst.Center.Y,burst.width,burst.height,AttackConfidence.Representative);return true;}if(!pass && receive.Remaining!=0)return false;if(p.type==286)root.ResetAmmoImpactStaticMatching(outer,oldCooldown);
            }
            int next=-1;if(p.type==639){if(!pool.TryAllocate(out next))return false;pool.Release(next);}
            var children=HostArrowFamilyAttack.Family(p.type)?HostArrowFamilyAttack.KillChildren(p,next,p.whoAmI):HostAmmoImpactAttack.KillChildren(p);
            foreach(var child in children)if(!Add(child,member,rows,pool,terrain,root,outer))return false;
            // Native Kill resizes this same body's terminal geometry. Keep it
            // for the remaining inactive movement and strict prefix lease.
            if(damage){p.position=burst.position;p.width=burst.width;p.height=burst.height;p.velocity=burst.velocity;}
            p.active=false;p.timeLeft=0;pool.Release(p.whoAmI);return true;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots pool,Snapshot captured,NpcTrajectory path,int tick,int outer,Vector2 aim,PredictionTerrain terrain,HostAttackObstacles root,Vector2 ownerCenter,bool phantasm,out AttackContact contact,int after=-1,int through=999,bool beforeNpc=true)
        {
            contact=null;if(tick<0 || tick>=path.Count)return false;
            if(beforeNpc)foreach(var m in rows)if(m.Shot.active && m.ReadyOuter<=outer && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height))return false;
            for(int visit=0;visit<1000;visit++)
            {
                Member m=null;foreach(var row in rows)if(row.Shot.active && row.ReadyOuter<=outer && row.Shot.whoAmI>after && row.Shot.whoAmI<=through && (m==null || row.Shot.whoAmI<m.Shot.whoAmI))m=row;
                if(m==null)return true;var p=m.Shot;after=p.whoAmI;int updates=p.extraUpdates+1,killSub=0;AttackContact earlyContact=null;Func<bool> killAtStage=null;
                // Native Update decrements local immunity once BEFORE its
                // subupdate loop. Partial passes must not decrement twice.
                if(m.ImmuneOuter!=outer){m.ImmuneOuter=outer;if(p.usesLocalNPCImmunity)for(int i=0;i<p.localNPCImmunity.Length;i++)if(p.localNPCImmunity[i]>0)p.localNPCImmunity[i]--;}
                for(int sub=0;sub<updates && (p.active || m.BeeReady && !m.Bee.Stopped);sub++)
                {
                    CombatPredictionWork.Check();p.oldVelocity=p.velocity;bool killed,skipped,echoBurst=false;
                    if(HostBeeAttack.Handles(p.type))
                    {
                        if(!m.BeeReady){m.Bee=new HostBeeAttack.State(p);m.BeeReady=true;}
                        if(!HostBeeAttack.Step(p,captured.Bee,path,tick,terrain,ref m.Bee))return false;BeeValues(m);
                        if(p.active && p.damage>0)
                        {
                            int hits=m.Receive.SelectedHits;bool pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true);m.Bee.Penetration=m.Receive.Remaining;
                            if(m.Source && m.Receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}if(!pass && m.Receive.Remaining>0)return false;
                        }
                        HostBeeAttack.Tail(ref m.Bee);BeeValues(m);if(!p.active && m.Bee.Stopped)pool.Release(p.whoAmI);continue;
                    }
                    if(p.type==706)
                    {
                        // The third held use shares this ordered world, but
                        // retains its distinct AI, same-body burst and five-
                        // percent per-receiver damage truncation.
                        var state=m.Phoenix??(m.Phoenix=HostPhoenixAttack.Bind(p,captured.PhoenixApprentice));if(!HostPhoenixAttack.Step(state,terrain))return false;
                        if(!p.active && !state.Burst){pool.Release(p.whoAmI);break;}
                        Func<int,int,bool> phoenixHit=(slot,remaining)=>{state.Hit(slot,remaining);p.localNPCImmunity[slot]=p.localNPCHitCooldown;return true;};
                        var hit=HostPhoenixAttack.Receive(state,path,tick,outer,sub,aim,m.Receive,phoenixHit);if(m.Source && hit!=null){contact=hit;return true;}if(!state.ReceiveComplete)return false;
                        if(!p.active || state.Retired){pool.Release(p.whoAmI);break;}
                        HostPhoenixAttack.Finish(state);
                        if(state.Burst){hit=HostPhoenixAttack.Receive(state,path,tick,outer,sub,aim,m.Receive,phoenixHit);if(m.Source && hit!=null){contact=hit;return true;}if(!state.ReceiveComplete)return false;}
                        if(!p.active){pool.Release(p.whoAmI);break;}continue;
                    }
                    // Eventide central/wooden conversions share the native
                    // AI181 motor, but keep the SAME scene/slots/history as
                    // selected-ammo arrows. They are not AI001 charm targets.
                    bool specialTail=false;
                    if(HostSpecialArrowAttack.Handles(p.type))
                    {
                        if(!HostHarpyAttack.AdvanceSpecialBody(p,captured.HarpyState,path,tick,terrain,out m.Special))return false;killed=false;skipped=false;specialTail=true;
                        if(m.Special.Burst || m.Special.Killed){if(!SpecialDeath(m,rows,pool,captured,path,tick,outer,sub,aim,terrain,root,out contact))return false;if(contact!=null)return true;break;}
                    }
                    else if(p.type==932)
                    {if(!HostHomingEffectAttack.Step(p,null,path,tick,terrain))return false;if(captured.Quiver && p.arrow && !p.npcProj && p.friendly && p.extraUpdates<1)p.extraUpdates=1;killed=false;skipped=false;}
                    else if(p.type==631)
                    {
                        if(!HostPhantasmEchoAttack.Ai(p,captured.Echo,path,tick,out echoBurst))return false;
                        killed=false;skipped=false;if(!echoBurst && p.active)
                        {float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;var pos=p.position;var vel=p.velocity;Vector2 wet;bool hit=false;if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref pos,ref vel,false,false,out wet,out hit,nativeClip:true))return false;p.position=pos;p.velocity=vel;if(hit)p.active=false;else p.position+=vel;}
                    }
                    else
                    {
                        // AI/liquid/tile Kill runs while the parent slot is
                        // occupied, before this substep's remaining movement.
                        // A recorded primary receive survives an unknown tail.
                        earlyContact=null;killSub=sub;
                        if(killAtStage==null)killAtStage=()=>{AttackContact hit;bool complete=Kill(m,rows,pool,captured,path,tick,outer,killSub,aim,terrain,root,out hit);if(hit!=null)earlyContact=hit;return complete;};
                        bool stepped=HostArrowFamilyAttack.StepFamilyMotor(p,terrain,out killed,out skipped,q=>Ai(q,captured,path,tick,terrain),captured.HarpyState==null?null:(Func<Projectile,bool>)(q=>HostHarpyAttack.AfterAi(q,captured.HarpyState,path,tick,terrain)),HostAmmoSteeringAttack.Handles(p.type)?(Func<Projectile,Vector2,int>)((q,prior)=>HostAmmoSteeringAttack.Tile(q,prior,captured.Steering,path,tick,terrain)):null,killAtStage);
                        if(earlyContact!=null){contact=earlyContact;return true;}if(!stepped)return false;
                    }
                    if(skipped)continue;bool killedBeforeDamage=killed;if(!p.active){pool.Release(p.whoAmI);break;}
                    // Meteor spends a penetration in native tile response,
                    // before Damage. Rebind that policy to the same history.
                    if(p.penetrate!=m.Receive.Remaining)m.Receive=m.Receive.ForkForSpawn(p,true);
                    if(!killed && p.damage>0 && p.friendly && !p.hostile && (p.type!=631 || echoBurst) && (p.type!=1124 || p.ai[0]>=15))
                    {
                        int hits=m.Receive.SelectedHits;bool dependency=true,collisionKnown=true;Func<MotionRect,bool> collider=p.type==932?(Func<MotionRect,bool>)(b=>HostHomingEffectAttack.EventideCollides(p,b)):p.type==661?(Func<MotionRect,bool>)(b=>{bool known;bool hit=HostOnyxBlastAttack.Collides(p,b,terrain,out known);if(!known)collisionKnown=false;return hit;}):null;
                        bool pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,collider,consumeSelected:true,onHit:(slot,remaining)=>
                        {
                            // Preserve the already recorded primary Strike if
                            // its subsequent OnHit cannot allocate descendants.
                            if(m.Source && slot==path.Identity.Slot)return true;
                            if(p.arrow && p.aiStyle==1){var features=new Projectile.AI_001_Features(p.ai[2]);features.HarpyCharmed=features.HarpyCharmConsumed=true;p.ai[2]=features.Bits;}
                            if(p.arrow && p.type!=631 && phantasm && !Echo(m,slot,rows,pool,captured,path,tick,outer,ownerCenter,terrain,root)){dependency=false;return false;}
                            if(p.type==41 && p.timeLeft>1)p.timeLeft=1;if(p.type==638 || p.type==639 || p.type==640)p.damage=(int)(p.damage*.96);
                            if(p.usesLocalNPCImmunity && !p.usesIDStaticNPCImmunity && p.localNPCHitCooldown!=-2)p.localNPCImmunity[slot]=p.localNPCHitCooldown;
                            if(p.type==710 && !SpecialChildren(m,rows,pool,terrain,root,outer)){dependency=false;return false;}return true;
                        });p.penetrate=m.Receive.Remaining;if(!collisionKnown)return false;
                        if(m.Source && m.Receive.SelectedHits>hits){contact=p.type==932?AttackIntercept.ProjectileShapeContact(path,tick,sub,true,aim.X,aim.Y,p.Center.X,p.Center.Y,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional):AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}if(!dependency || !pass && p.penetrate!=0)return false;
                    }
                    if(echoBurst){p.active=false;pool.Release(p.whoAmI);break;}
                    // Native pen==0 invokes Kill INSIDE Damage, before its
                    // lifetime tail. Night's Kill-delay reads that exact Left.
                    if(!killed)killed=p.penetrate==0;
                    if(!killed)
                    {
                        if(p.oldPos!=null && p.oldPos.Length>0){for(int i=p.oldPos.Length-1;i>0;i--)p.oldPos[i]=p.oldPos[i-1];p.oldPos[0]=p.position;}
                        if(specialTail?p.timeLeft<=0:--p.timeLeft<=0)killed=true;
                    }
                    if(killed)
                    {
                        if(!Kill(m,rows,pool,captured,path,tick,outer,sub,aim,terrain,root,out contact))return false;if(contact!=null)return true;
                        if(!p.active && !killedBeforeDamage && sub+1<updates)
                        {p.oldVelocity=p.velocity;if(!HostArrowFamilyAttack.RetiredMotor(p,terrain,HostAmmoSteeringAttack.Handles(p.type)?(Func<Projectile,Vector2,int>)((q,prior)=>HostAmmoSteeringAttack.Tile(q,prior,captured.Steering,path,tick,terrain)):null))return false;}
                    }
                }
                // AI036 may die in its final lifetime tail without another
                // subupdate to mark Stopped. Release after the full native
                // member Update, including any admitted inactive movement.
                if(!p.active)pool.Release(p.whoAmI);
            }
            return false;
        }
        internal static AttackContact ExplicitBirthBatch(Player player,Projectile[] born,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,int fireDelay=0,Projectile controller=null,Snapshot captured=null,bool representative=true)
        {return ExplicitBirthBatchCore(player,born,path,clock,terrain,root,aim,fireDelay,controller,captured,representative,null);}
        // A finite solver may reuse its direction-independent owner motion.
        // This named entry keeps the historical explicit-birth reflection ABI.
        internal static AttackContact ExplicitPreparedBirthBatch(Player player,Projectile[] born,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,int fireDelay,Projectile controller,Snapshot captured,bool representative,HostAttackPlayerPrefix owners)
        {return ExplicitBirthBatchCore(player,born,path,clock,terrain,root,aim,fireDelay,controller,captured,representative,owners);}
        private static AttackContact ExplicitBirthBatchCore(Player player,Projectile[] born,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,int fireDelay,Projectile controller,Snapshot captured,bool representative,HostAttackPlayerPrefix sharedOwners)
        {
            if(born==null || born.Length==0 || clock.Age<0)return null;captured=captured??CaptureForSolve(player,root,Array.ConvertAll(born,p=>p.type));
            root.ResetAmmoImpactCandidate();root.BeginCandidate();var rows=new List<Member>();foreach(var p in captured.Scene)rows.Add(Make(Copy(p),root,false));var pool=captured.Slots.Copy();
            int parent=-1;bool hasController=controller!=null;if(hasController)
            {
                parent=controller.whoAmI;bool actual=parent>=0 && parent<1000 && Main.projectile[parent].active && Main.projectile[parent].key.Equals(controller.key) && Main.projectile[parent].type==controller.type;
                if(actual)root.CaptureProjectileFamily(new[]{Main.projectile[parent]});else if(!pool.TryAllocate(out parent))return null;
            }
            var owners=sharedOwners??new HostAttackPlayerPrefix(HostAttackPlayerFrame.Capture(Main.player[player.whoAmI],path),terrain,clock.LaunchDelay+121);int wait=clock.LaunchDelay+fireDelay;int start=clock.FirstTick-clock.LaunchDelay;int cooldown=captured.HarpyState?.cooldown??0;
            bool Pose(int at,out Vector2 center){PredictionPlayerMotion p;if(!owners.TryRead(at+(clock.NextWorld?1:0),out p)){center=Vector2.Zero;return false;}center=new Vector2(p.X+player.width*.5f,p.Y+player.height*.5f);return true;}
            bool Proc(int at){return hasController && controller.type==630 || player.phantasmTime-at-(clock.NextWorld?1:0)>0;}
            for(int action=0;action<wait;action++){Vector2 center;if(!Pose(action,out center) || (clock.NextWorld || action>0) && !Charm(rows,captured,path,start+action,terrain,center,ref cooldown))return null;AttackContact ignored;if(!Outer(rows,pool,captured,path,start+action,action+1,aim,terrain,root,center,Proc(action),out ignored,clock.Phase==HostAttackPhase.Projectiles && action==0?parent:-1,beforeNpc:clock.Phase!=HostAttackPhase.Projectiles || action>0))return null;}
            int tick=start+wait,outer=wait+1;Vector2 owner;if(!Pose(wait,out owner))return null;
            // Native Player charm precedes the controller's birth. The two
            // partial projectile passes below must not run it a second time.
            if((clock.NextWorld || wait>0) && !Charm(rows,captured,path,tick,terrain,owner,ref cooldown))return null;
            if(hasController && (!(clock.Phase==HostAttackPhase.Projectiles && wait==0) || clock.BeforeProjectilePass))
            {
                AttackContact ignored;if(!Outer(rows,pool,captured,path,tick,outer,aim,terrain,root,owner,Proc(wait),out ignored,through:parent,beforeNpc:clock.Phase!=HostAttackPhase.Projectiles || wait>0))return null;
                // World-prefix preparation precedes every slot; the public
                // explicit-birth API still denotes an already reached parent.
                // Keep retired rows so consumption can verify exact identities
                // and the slots their real earlier updates made available.
                if(clock.BeforeProjectilePass && wait==0)root.CaptureHeldControllerPrefix(captured.Scene,Array.ConvertAll(rows.ToArray(),m=>m.Shot),parent);
            }
            foreach(var prototype in born)
            {
                var p=Copy(prototype);int slot;if(!pool.TryAllocate(out slot))return null;p.active=true;p.owner=player.whoAmI;p.whoAmI=slot;bool wet;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true))return null;p.wet=wet && !p.ignoreWater;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;
                rows.Add(Make(p,root,true,representative,outer+(hasController && slot<=parent?1:0)));
            }
            for(int action=0;tick+action<path.Count && action<120;action++)
            {
                Vector2 center;if(!Pose(wait+action,out center) || action>0 && !Charm(rows,captured,path,tick+action,terrain,center,ref cooldown))return null;AttackContact contact;if(!Outer(rows,pool,captured,path,tick+action,outer+action,aim,terrain,root,center,Proc(wait+action),out contact,action==0 && hasController?parent:-1,beforeNpc:action>0 || !hasController && clock.Phase!=HostAttackPhase.Projectiles))return null;if(contact!=null)return contact;
                // Outer has completed every admitted Kill and descendant
                // birth. An active lower-slot child waiting for ReadyOuter
                // still keeps this alive; unrelated ambient bodies cannot
                // create another Source after its complete family retired.
                if(!rows.Any(m=>m.Source && m.Shot.active))return null;
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.Age<0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0)return null;
            var captured=CaptureForSolve(player,root);root.CaptureHeldAmmoInputs(player);root.BeginCandidate();root.ResetAmmoImpactCandidate();var rows=new List<Member>();foreach(var p in captured.Scene)rows.Add(Make(Copy(p),root,Array.IndexOf(sources,p)>=0));var pool=captured.Slots.Copy();var owners=new HostAttackPlayerPrefix(HostAttackPlayerFrame.Capture(player,path),terrain,121);var aim=sources[0].Center+sources[0].velocity*1000;int cooldown=captured.HarpyState?.cooldown??0;
            for(int action=0;action<Math.Min(120,path.Count-clock.FirstTick);action++)
            {
                PredictionPlayerMotion owner;if(!owners.TryRead(action+(clock.NextWorld?1:0),out owner))return null;AttackContact contact;var center=new Vector2(owner.X+player.width*.5f,owner.Y+player.height*.5f);
                if((clock.NextWorld || action>0) && !Charm(rows,captured,path,clock.FirstTick+action,terrain,center,ref cooldown))return null;
                if(!Outer(rows,pool,captured,path,clock.FirstTick+action,action+1,aim,terrain,root,center,player.phantasmTime-action-(clock.NextWorld?1:0)>0,out contact))return null;if(contact!=null)return contact;
            }
            return null;
        }
        internal static Vector2 BirthVelocity(Player pose,Projectile parent,Vector2 origin,Vector2 aim,float speed,bool rocket=false)
        {
            var cursor=HostDirectionalControl.Cursor(pose,aim);var held=Vector2.Normalize(cursor-origin);if(float.IsNaN(held.X) || float.IsNaN(held.Y))held=-Vector2.UnitY;held*=pose.HeldItem.shootSpeed*parent.scale;
            // NextDouble's middle integer sample is legal; unlike exact .5,
            // it also respects Random's odd Int32.MaxValue denominator.
            const double draw=1073741823*(1.0/int.MaxValue);
            if(parent.type==615)held=held.RotatedBy(draw*.13089969754219055-.06544984877109528);else held*=.55f;
            var velocity=Vector2.Normalize(held)*speed;if(parent.type==615)velocity=velocity.RotatedBy(rocket?draw*.39269909262657166-.19634954631328583:draw*.19634954631328583-.09817477315664291);
            if(float.IsNaN(velocity.X) || float.IsNaN(velocity.Y))velocity=-Vector2.UnitY;while(velocity.X>=16 || velocity.X<=-16 || velocity.Y>=16 || velocity.Y< -16)velocity*=.97f;return velocity;
        }
        private struct Proposal
        {internal Vector2 Direction;internal int Tick;}
        private static Vector2[] Proposals(Player pose,Projectile parent,AttackAmmoSnapshot ammo,int type,Vector2 origin,NpcTrajectory path,HostAttackClock clock,int delay)
        {
            AttackMotion motion;if(!HostAttackModels.TryResolved(type,ammo.Speed,ammo.WeaponType,out motion))return new Vector2[0];
            var sample=ContentSamples.ProjectilesByType[type];if(ammo.ExtraArrowUpdates && sample.arrow && sample.extraUpdates<1)motion=motion.WithUpdates(2,1);
            var rows=new List<Proposal>();float speed=motion.Speed;
            // Native AI001 limits components, not length. Each finite .97
            // branch can propose a different legal angle; keep diagonal full
            // speed and verify the actual cursor/representative birth below.
            for(;;)
            {
                CombatPredictionWork.Check();
                var candidate=new AttackMotion(speed,motion.Gravity,motion.GravityStart,motion.Updates,motion.Width,motion.Height,motion.Lifetime,motion.Confidence,motion.Acceleration,motion.MaxSpeed,false,motion.ComponentAccelerationLimit,motion.DragAfterGravity,motion.StopSmallVelocity,motion.MoveScale,motion.WindX,motion.MaxFallSpeed,motion.GravityClockUsesLaunchX,motion.MaxRiseSpeed,motion.AccelerationStart,motion.HorizontalAccelerationOnly,motion.PreMotorWindX,motion.FirstUpdates);
                // Generate's first receive is age+1. A lower newborn slot
                // waits one world; both proposals retain the exact NPC sample
                // indices, and neither supplies a Contact or a miss proof.
                for(int lower=0;AttackMotionCandidates.Supports(candidate) && lower<2;lower++)
                {
                    int first=clock.FirstTick+delay+lower;if(first<1)continue;
                    foreach(var proposal in AttackMotionCandidates.Generate(path,first-1,origin.X,origin.Y,candidate))
                    {
                        var direction=new Vector2(proposal.X,proposal.Y);
                        if(parent.type==615)
                        {const double draw=1073741823*(1.0/int.MaxValue);direction=direction.RotatedBy(-(draw*.13089969754219055-.06544984877109528+draw*.19634954631328583-.09817477315664291));}
                        var actual=BirthVelocity(pose,parent,origin,origin+direction*10000,ammo.Speed);
                        if(Math.Abs(actual.Length()-speed)>.001f || Vector2.Dot(actual.SafeNormalize(Vector2.UnitX),new Vector2(proposal.X,proposal.Y))<.99999f)continue;
                        rows.Add(new Proposal{Direction=direction,Tick=proposal.Tick});
                    }
                }
                if(!motion.ComponentBirthLimit || speed<16)break;speed*=.97f;
            }
            // Bound extra full-scene work even for a blocked receiver. Preserve
            // separate early and late proposals across ALL speed/slot classes;
            // the old complete angular fallback below is never removed.
            rows.Sort((a,b)=>a.Tick.CompareTo(b.Tick));var selected=new List<Proposal>(8);
            for(int i=0;i<rows.Count && selected.Count<4;i++)if(!selected.Any(p=>p.Direction==rows[i].Direction))selected.Add(rows[i]);
            for(int i=rows.Count-1;i>=0 && selected.Count<8;i--)if(!selected.Any(p=>p.Direction==rows[i].Direction))selected.Add(rows[i]);
            selected.Sort((a,b)=>a.Tick.CompareTo(b.Tick));return selected.Select(p=>p.Direction).ToArray();
        }
        internal static AttackContact SolvePrepared(Player player,Projectile parent,AttackAmmoSnapshot ammo,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackPlayerFrame frame,out Vector2 point,out HostAttackObstacles root)
        {
            point=HostHeldAttack.DirectionPoint(player,path,clock.FirstTick);root=null;if(ammo==null || ammo.Damage<=0)return null;int delay=Math.Max(0,(int)Math.Ceiling(parent.ai[1])-1);var motion=frame.Motion;var world=frame.Environment;
            for(int i=0;i<delay+(clock.NextWorld?1:0);i++){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,world,terrain,out stop))return null;}var pose=(Player)clone.Invoke(player,null);HostAttackPlayerFrame.ApplyPose(pose,motion);var origin=pose.RotatedRelativePoint(pose.MountedCenter);
            int type=parent.type==705 && ammo.Projectile==1?2:ammo.Projectile;var sample=new Projectile();sample.SetDefaults(type);sample.owner=player.whoAmI;sample.damage=ammo.Damage;root=new HostAttackObstacles(player,sample,path,clock.LaunchDelay+delay,clock.BeforeNpc,clock.NextWorld);root.CaptureHeldAmmoInputs(player);root.CaptureHomingInputs(player,false,true);root.CaptureShotFacing(player);if(parent.type==705)root.CapturePhoenixOwner(player);
            bool rocket=HostVortexRocketAttack.Due(parent,delay);var kinds=rocket?new[]{type,616}:new[]{type};var captured=CaptureForSolve(player,root,kinds);var directions=new List<Vector2>();
            Projectile rocketPrototype=null;if(rocket){rocketPrototype=new Projectile();rocketPrototype.SetDefaults(616);}
            // Every direction observes this solve's same real player/path/world.
            // Share only immutable value-motion prefixes, including a stopped
            // tail; shorter completed actions remain readable after a stop.
            // Scene bodies, slots, receive history and Harpy cooldown are still
            // reset independently inside each candidate. Never retain across GUCs.
            var owners=new HostAttackPlayerPrefix(HostAttackPlayerFrame.Capture(Main.player[player.whoAmI],path),terrain,clock.LaunchDelay+121);
            // Keep the established centre shot first: an earlier rectangle-
            // edge representative can lose its native random-spread margin.
            // Only a failed centre replay pays for extra inverse proposals.
            Vector2[] proposals=null;
            for(int input=-1;input<(proposals==null?0:proposals.Length)+40;input++)
            {
                if(input>=0 && proposals==null)proposals=Proposals(pose,parent,ammo,type,origin,path,clock,delay);
                int seed=input<0?0:input-proposals.Length+1;var b=path[Math.Min(path.Count-1,clock.FirstTick+delay+Math.Max(0,Math.Min(seed,8))*4)].ProjectileReceiveBounds;var direction=input>=0 && input<proposals.Length?proposals[input]:seed<9?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();if(directions.Contains(direction))continue;directions.Add(direction);var aim=origin+direction*10000;int count=parent.type==630?4:parent.type==705?2:1;var born=new List<Projectile>();
                for(int i=0;i<count;i++){var p=Copy(sample);p.owner=player.whoAmI;p.Center=origin;p.velocity=BirthVelocity(pose,parent,origin,aim,ammo.Speed);p.damage=ammo.Damage;p.knockBack=player.HeldItem.knockBack;born.Add(p);}
                if(rocket){var p=Copy(rocketPrototype);p.owner=player.whoAmI;p.Center=origin;p.velocity=BirthVelocity(pose,parent,origin,aim,8,true);p.damage=ammo.Damage+20;p.knockBack=player.HeldItem.knockBack*1.25f;born.Add(p);}
                var contact=ExplicitBirthBatchCore(player,born.ToArray(),path,clock,terrain,root,aim,delay,parent,captured,true,owners);if(contact!=null){point=aim;return contact;}
            }
            return null;
        }
    }
}
