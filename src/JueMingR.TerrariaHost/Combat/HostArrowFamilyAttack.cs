using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Holy/Night/Bone arrows own their Kill descendants. A finite private
    // family preserves native slot ordering and shared immunity; it never
    // runs Kill/AI or consumes RNG against the live projectile array.
    internal static class HostArrowFamilyAttack
    {
        internal static bool Handles(int type){return type==91 || type==639 || type==117;}
        internal static bool Family(int type){return Handles(type) || type==92 || type==640 || type==1124;}
        private static readonly int[] Types={91,92,117,639,640,1124};
        internal sealed class Snapshot
        {internal Projectile[] Scene;internal HostProjectileBirthSlots Slots;}
        private sealed class Member
        {internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Representative;}
        private static Projectile Copy(Projectile p)
        {var q=HostNebulaBlazeAttack.Copy(p);if(p.oldPos!=null)q.oldPos=(Vector2[])p.oldPos.Clone();return q;}
        private static Member Make(Projectile p,HostAttackObstacles root,bool source,bool representative=false)
        {return new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=source,Representative=representative};}
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {var rows=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Family(p.type))rows.Add(p);var scene=rows.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,Types);return scene;}
        internal static Snapshot CaptureForSolve(Player player,HostAttackObstacles root)
        {return new Snapshot{Scene=Capture(player,root),Slots=root.CaptureBirthSlots()};}
        private static List<Member> Prepare(Projectile[] scene,Projectile[] source,HostAttackObstacles root)
        {var rows=new List<Member>();foreach(var p in scene)rows.Add(Make(Copy(p),root,source!=null && Array.IndexOf(source,p)>=0));return rows;}
        internal static bool Ai(Projectile p)
        {
            if(HostAmmoImpactAttack.Family(p.type))return HostAmmoImpactAttack.Ai(p);
            if(p.type==92){if(p.position.Y>p.ai[1])p.tileCollide=true;return true;}
            if(p.type==639)
            {
                if(p.localAI[0]==0 && p.localAI[1]==0){p.localAI[0]=p.Center.X;p.localAI[1]=p.Center.Y;p.ai[0]=p.velocity.X;p.ai[1]=p.velocity.Y;}
                p.alpha=Math.Max(0,p.alpha-25);if(p.timeLeft<=(p.extraUpdates+1)*45-14)p.velocity.Y+=.1f;
            }
            else
            {
                if(p.type==640)
                {
                    p.alpha=Math.Max(0,p.alpha-25);
                    if(p.velocity==Vector2.Zero)
                    {p.ai[0]=0;bool stopped=true;for(int i=1;i<p.oldPos.Length;i++)if(p.oldPos[i]!=p.oldPos[0]){stopped=false;break;}if(stopped)return false;}
                }
                if(p.type==1124)p.localAI[0]=1;
                p.ai[0]++;
                int start=p.type==91?20:p.type==117?35:15;float gravity=p.type==91?.07f:p.type==117?.06f:.1f;
                if(p.ai[0]>=start){p.ai[0]=start;p.velocity.Y+=gravity;}
            }
            if(p.velocity.Y>16)p.velocity.Y=16;return true;
        }
        // One original Update action through movement, before Damage and its
        // trail/lifetime tail. 640's delay skips the WHOLE action, including
        // lifetime and AI; it is measured in subupdates, not world frames.
        internal static bool Step(Projectile p,PredictionTerrain terrain,out bool killed,out bool skipped,Func<Projectile,bool> afterAi=null)
        {return StepMotor(p,terrain,out killed,out skipped,q=>Ai(q)?0:1,afterAi);}
        // Motor injection shares the original Update ordering with a finite
        // autonomous rocket without running live AI. Existing arrow/Harpy
        // callers retain the same five-argument Step contract.
        internal static bool StepMotor(Projectile p,PredictionTerrain terrain,out bool killed,out bool skipped,Func<Projectile,int> motor,Func<Projectile,bool> afterAi=null,Func<Projectile,Vector2,int> tileResponse=null)
        {return StepMotorCore(p,terrain,out killed,out skipped,motor,afterAi,tileResponse,false,null);}
        internal static bool StepFamilyMotor(Projectile p,PredictionTerrain terrain,out bool killed,out bool skipped,Func<Projectile,int> motor,Func<Projectile,bool> afterAi,Func<Projectile,Vector2,int> tileResponse,Func<bool> killNow)
        {return StepMotorCore(p,terrain,out killed,out skipped,motor,afterAi,tileResponse,false,killNow);}
        // Update's remaining substep skips AI once inactive, but still runs
        // environment/movement before its inactive return. No Damage, Kill,
        // lifetime or new quiver update follows that return.
        internal static bool RetiredMotor(Projectile p,PredictionTerrain terrain,Func<Projectile,Vector2,int> tileResponse=null)
        {bool killed,skipped;return StepMotorCore(p,terrain,out killed,out skipped,q=>0,q=>true,tileResponse,true,null);}
        private static bool RetireAtStage(Func<bool> killNow,ref bool retired)
        {if(retired)return true;if(killNow==null || !killNow())return false;retired=true;return true;}
        private static bool StepMotorCore(Projectile p,PredictionTerrain terrain,out bool killed,out bool skipped,Func<Projectile,int> motor,Func<Projectile,bool> afterAi,Func<Projectile,Vector2,int> tileResponse,bool retired,Func<bool> killNow)
        {
            killed=skipped=false;
            if(p.type==640 && p.ai[1]>0){p.ai[1]--;skipped=true;return true;}
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y)){p.active=false;return true;}
            var wetVelocity=p.velocity;
            int action=motor(p);if(action<0)return false;if(action>0){killed=true;if(killNow==null && !retired)return true;if(!RetireAtStage(killNow,ref retired))return false;}
            // Already enchanted captured arrows retain the native autonomous
            // tail. Its target closure is handled by the Harpy family route;
            // this finite family must not silently linearize such a source.
            if(action==0 && !retired)
            {if(afterAi!=null){if(!afterAi(p))return false;}else if(p.aiStyle==1){var features=new Projectile.AI_001_Features(p.ai[2]);if(features.HarpyCharmed && !features.HarpyCharmConsumed)return false;}}
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active){killed=true;if(killNow==null && !retired)return true;if(!RetireAtStage(killNow,ref retired))return false;}
            if(p.type==242 || p.type==302 || p.type==638)p.wet=false;
            if(p.wet && !p.lavaWet)
            {if(Terraria.ID.ProjectileID.Sets.IsDestroyedByWater[p.type]){killed=true;if(killNow==null && !retired)return true;if(!RetireAtStage(killNow,ref retired))return false;}if(p.type==2){p.type=1;p.light=0;}}
            if(!p.ignoreWater)
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
                if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;p.wet=wet;if(!wet)p.lavaWet=p.honeyWet=p.shimmerWet=false;
            }
            if(p.shimmerWet)
            {
                if(p.type==1 || p.type==2 || p.type==4 || p.type==5 || p.type==41 || p.type==91 || p.type==103 || p.type==117 || p.type==172 || p.type==225 || p.type==278 || p.type==282 || p.type==639)p.velocity.Y-=.4f;
                else if(p.aiStyle==1 || p.type==90){bool shimmerKill;if(!HostAmmoImpactAttack.Shimmer(p,terrain,out shimmerKill))return false;if(shimmerKill){killed=true;if(killNow==null && !retired)return true;if(!RetireAtStage(killNow,ref retired))return false;}}
                else return false;
            }
            var prior=p.velocity;var position=p.position;var velocity=p.velocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet && !p.ignoreWater,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;
            p.position=position;p.velocity=velocity;
            if(hit)
            {
                if(tileResponse!=null){int response=tileResponse(p,prior);if(response<0)return false;if(response==1){killed=true;if(killNow!=null && !RetireAtStage(killNow,ref retired))return false;return true;}if(response==2){killed=true;if(killNow!=null && !RetireAtStage(killNow,ref retired))return false;}}
                else if(p.type==640){if(p.localAI[1]<1){p.localAI[1]+=2;p.position+=p.velocity;p.velocity=Vector2.Zero;}}
                else{p.position+=p.velocity;killed=true;if(killNow==null && !retired)return true;if(!RetireAtStage(killNow,ref retired))return false;}
            }
            p.position+=p.wet && !p.ignoreWater?wetVelocity:p.velocity;
            if(!retired && !killed && !p.npcProj && p.friendly && p.owner<255 && Main.player[p.owner].magicQuiver && p.extraUpdates<1 && p.arrow)p.extraUpdates=1;
            return true;
        }
        private static bool Add(Projectile p,Member parent,List<Member> rows,HostProjectileBirthSlots slots,PredictionTerrain terrain,HostAttackObstacles root)
        {
            int slot;if(!slots.TryAllocate(out slot))return false;p.whoAmI=slot;p.active=true;p.owner=parent.Shot.owner;
            bool wet;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true))return false;
            p.wet=wet && !p.ignoreWater;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;
            rows.Add(Make(p,root,parent.Source,true));return true;
        }
        private static bool Kill(Member m,List<Member> rows,HostProjectileBirthSlots slots,PredictionTerrain terrain,HostAttackObstacles root)
        {
            var p=m.Shot;if(!p.active)return true;
            // During native Kill the parent remains occupied until all its
            // children have been allocated. Release only after those births.
            int next=-1;if(p.type==639 && p.owner==Main.myPlayer){if(!slots.TryAllocate(out next))return false;slots.Release(next);}
            foreach(var child in KillChildren(p,next,p.whoAmI))if(!Add(child,m,rows,slots,terrain,root))return false;
            p.timeLeft=0;
            p.active=false;slots.Release(p.whoAmI);return true;
        }
        // A Harpy-modified parent uses the same native Kill births. Callers
        // supply its complete CURRENT scalars and the first free captured
        // slot; no live pool lookup, RNG or hit history lives in this helper.
        internal static Projectile[] KillChildren(Projectile parent,int nextSlot,int updateLoopIndex)
        {
            if(parent.owner!=Main.myPlayer)return new Projectile[0];
            if(parent.type==639)
            {
                var child=new Projectile();child.SetDefaults(640);child.owner=parent.owner;child.Center=new Vector2(parent.localAI[0],parent.localAI[1]);child.velocity=new Vector2(parent.ai[0],parent.ai[1]);child.damage=parent.damage;child.knockBack=parent.knockBack;
                child.ai[1]=parent.timeLeft+1+(updateLoopIndex!=-1 && updateLoopIndex<nextSlot?1:0);return new[]{child};
            }
            if(parent.type==91 || parent.type==92 && parent.ai[0]>0)
            {
                // Midpoints are legal members of the original discrete
                // x[-400,400), y[600,900) support, not live RNG samples.
                var child=new Projectile();child.SetDefaults(92);child.owner=parent.owner;child.Center=parent.position-new Vector2(0,750);child.velocity=(parent.Center-child.Center).SafeNormalize(Vector2.UnitY)*22;child.damage=parent.type==91?parent.damage/3:parent.damage;child.knockBack=parent.knockBack;child.ai[0]=parent.type==91?1:0;child.ai[1]=parent.position.Y;return new[]{child};
            }
            if(parent.type==117)
            {
                var children=new Projectile[2];for(int i=0;i<children.Length;i++){var child=new Projectile();child.SetDefaults(1124);child.owner=parent.owner;child.velocity=parent.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(Math.PI)*2;child.Center=parent.Center+child.velocity*5;child.damage=(int)(parent.damage*.25f);children[i]=child;}return children;
            }
            return new Projectile[0];
        }
        private static void Finish(Projectile p)
        {
            if(p.oldPos!=null && p.oldPos.Length>0){for(int i=p.oldPos.Length-1;i>0;i--)p.oldPos[i]=p.oldPos[i-1];p.oldPos[0]=p.position;}
            p.timeLeft--;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,PredictionTerrain terrain,HostAttackObstacles root,int tick,int outer,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var m in rows)if(m.Shot.active && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height))return false;
            int after=-1;
            for(int visit=0;visit<1000;visit++)
            {
                Member m=null;foreach(var row in rows)if(row.Shot.active && row.Shot.whoAmI>after && (m==null || row.Shot.whoAmI<m.Shot.whoAmI))m=row;
                if(m==null)return true;var p=m.Shot;after=p.whoAmI;int updates=p.extraUpdates+1;
                for(int sub=0;sub<updates && p.active;sub++)
                {
                    CombatPredictionWork.Check();bool killed,skipped;if(!Step(p,terrain,out killed,out skipped))return false;if(skipped)continue;
                    if(killed){if(!Kill(m,rows,slots,terrain,root))return false;break;}if(!p.active){slots.Release(p.whoAmI);break;}
                    if(p.friendly && !p.hostile && p.damage>0 && (p.type!=1124 || p.ai[0]>=15))
                    {
                        int hits=m.Receive.SelectedHits;bool passed=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true,onHit:(slot,remaining)=>{if(p.type==639 || p.type==640)p.damage=(int)(p.damage*.96);return true;});p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}
                        if(!passed && p.penetrate!=0)return false;
                    }
                    // Damage invokes Kill immediately at zero penetration;
                    // Night's child delay reads Left before Update's tail.
                    if(p.penetrate==0){if(!Kill(m,rows,slots,terrain,root))return false;}
                    else {Finish(p);if(p.timeLeft<=0 && !Kill(m,rows,slots,terrain,root))return false;}
                }
            }
            return false;
        }
        internal static AttackContact Explicit(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 aim,Vector2 origin,Vector2 velocity,int age,PredictionTerrain terrain,HostAttackObstacles root,int delay,Snapshot captured=null)
        {
            if(!Handles(ammo.Projectile) || age-delay<0 || age+1>=path.Count)return null;
            captured=captured??CaptureForSolve(player,root);var slots=captured.Slots.Copy();root.BeginCandidate();var rows=Prepare(captured.Scene,null,root);
            for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,path,terrain,root,age-delay+wait,wait,aim,out ignored))return null;}
            int slot;if(!slots.TryAllocate(out slot))return null;var p=new Projectile();p.SetDefaults(ammo.Projectile);p.active=true;p.whoAmI=slot;p.owner=player.whoAmI;p.Center=origin;p.velocity=velocity;p.damage=ammo.Damage;p.knockBack=player.HeldItem.knockBack;
            bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,p.width,p.height,out wet,out kind,true))return null;p.wet=wet && !p.ignoreWater;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;
            rows.Add(Make(p,root,true));
            for(int step=1;step<=Math.Min(120,path.Count-age-1);step++){AttackContact contact;if(!Outer(rows,slots,path,terrain,root,age+step,delay+step,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            AttackMotion motion;if(!HostAttackModels.TryRead(ammo,out motion))return null;var proposals=HostMotionAttackInputs.Generate(origin,motion,path,age);var directions=new List<Vector2>();var captured=CaptureForSolve(player,root);root.CaptureShotFacing(player);
            for(int input=0;input<proposals.Length+41;input++)
            {
                int seed=input-proposals.Length;var b=path[Math.Min(path.Count-1,age+1+Math.Max(0,Math.Min(seed,8))*4)].ProjectileReceiveBounds;
                var unit=seed<0?proposals[input]:seed<9?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();if(directions.Contains(unit))continue;directions.Add(unit);
                var aim=origin+unit*10000;var velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);motion.Launch(ref velocity.X,ref velocity.Y);
                var contact=Explicit(player,ammo,path,aim,origin,velocity,age,terrain,root,delay,captured);if(contact!=null)return contact;
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(source==null || source.Length==0 || clock.Age<0 || clock.Phase==HostAttackPhase.Projectiles)return null;
            var scene=Capture(player,root);var slots=root.CaptureBirthSlots().Copy();root.BeginCandidate();var rows=Prepare(scene,source,root);var aim=source[0].Center+source[0].velocity*1000;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,slots,path,terrain,root,clock.FirstTick+step,step+1,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
