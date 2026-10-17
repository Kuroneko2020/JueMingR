using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // These four real ammo types share AI001/environment/receive ordering.
    // Homing and tile retargeting consume one captured chase set; they never
    // steer toward an arbitrarily selected receiver or read live NPCs in AI.
    internal static class HostAmmoSteeringAttack
    {
        internal static bool Handles(int type){return type==207 || type==225 || type==285 || type==36;}
        internal static bool Family(int type){return Handles(type);}
        private static readonly int[] Types={36,207,225,285};
        internal sealed class Snapshot{internal Projectile[] Scene;internal HostProjectileBirthSlots Slots;internal HostProjectileTargets Targets;internal AttackConfidence LaunchConfidence;}
        private sealed class Member{internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source;internal AttackConfidence Confidence;}
        private static Vector2 Center(MotionRect b){return new Vector2(b.CenterX,b.CenterY);}
        private static Vector2 IntegerCenter(MotionRect b){return new Vector2(b.X+(int)b.Width/2,b.Y+(int)b.Height/2);}
        private static int Index(HostProjectileTargets targets,int slot)
        {for(int i=0;i<targets.Count;i++)if(targets.Slot(i)==slot)return i;return -1;}
        internal static int Ai(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain)
        {
            if(!Handles(p.type) || p.aiStyle!=1 || targets==null || tick<0 || tick>=path.Count)return -1;
            if(p.type==207)
            {
                p.ai[0]++;float speed=(float)Math.Sqrt(p.velocity.X*p.velocity.X+p.velocity.Y*p.velocity.Y);
                if(p.localAI[0]==0)p.localAI[0]=speed;
                p.alpha=Math.Max(0,p.alpha-25);
                if(p.ai[1]==0)
                {
                    float nearest=300;int found=-1;
                    for(int i=0;i<targets.Count;i++)
                    {
                        var b=targets.Bounds(i,path,tick);var c=IntegerCenter(b);float distance=Math.Abs(p.position.X+p.width/2-c.X)+Math.Abs(p.position.Y+p.height/2-c.Y);
                        if(distance>=nearest)continue;bool clear;PredictionStop stop;
                        if(!terrain.CanHit(new MotionRect(p.position.X+p.width/2,p.position.Y+p.height/2,1,1),b,out clear,out stop))return -1;
                        if(clear){nearest=distance;found=targets.Slot(i);}
                    }
                    if(found>=0)p.ai[1]=found+1;
                }
                if(p.ai[1]>0)
                {
                    int slot=(int)(p.ai[1]-1);if(slot<0 || slot>=Main.maxNPCs)return -1;int index=Index(targets,slot);
                    if(index<0)p.ai[1]=0;
                    else
                    {
                        var c=IntegerCenter(targets.Bounds(index,path,tick));
                        if(p.friendly && Math.Abs(p.position.X+p.width/2-c.X)+Math.Abs(p.position.Y+p.height/2-c.Y)<1000)
                        {
                            float x=c.X-(p.position.X+p.width*.5f),y=c.Y-(p.position.Y+p.height*.5f);
                            float factor=p.localAI[0]/(float)Math.Sqrt(x*x+y*y);x*=factor;y*=factor;
                            p.velocity.X=(p.velocity.X*7+x)/8;p.velocity.Y=(p.velocity.Y*7+y)/8;
                        }
                    }
                }
            }
            else
            {
                // Flag3 deliberately excludes the bullets, even though a
                // captured nonzero ai0 still enters the native gravity tail.
                if(p.type==225)p.ai[0]++;else p.alpha=Math.Max(0,p.alpha-15);
                if(p.ai[0]>=15){p.ai[0]=15;p.velocity.Y+=.1f;}
            }
            p.rotation=(float)Math.Atan2(p.velocity.Y,p.velocity.X)+1.57f;
            if(p.velocity.Y>16)p.velocity.Y=16;return 0;
        }
        private static int Target(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain,out Vector2 goal,out float targetHeight)
        {
            goal=Vector2.Zero;targetHeight=0;float nearest=800;int found=-1;
            for(int i=0;i<targets.Count;i++)
            {
                int slot=targets.Slot(i);if(p.localNPCImmunity[slot]!=0)continue;var b=targets.Bounds(i,path,tick);var c=Center(b);float distance=Vector2.Distance(p.Center,c);
                if(distance>=nearest)continue;bool clear;PredictionStop stop;
                if(!terrain.CanHit(new MotionRect(p.position.X,p.position.Y,p.width,p.height),b,out clear,out stop))return -2;
                if(clear){nearest=distance;found=slot;goal=c;targetHeight=b.Height;}
            }
            return found;
        }
        // -1 unresolved; 0 reflected movement; 1 immediate native Kill;
        // 2 Kill followed by the original UpdatePosition wet/dry tail.
        internal static int Tile(Projectile p,Vector2 prior,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain)
        {
            if(p.type==225 || p.type==285)
            {
                p.ai[1]++;if(p.ai[1]==1)p.damage=(int)(p.damage*.66f);
                if(p.ai[1]>=(p.type==225?4:2))return 1;
                if(p.type==225)p.velocity=-prior;
                else{if(p.velocity.X!=prior.X)p.velocity.X=-prior.X;if(p.velocity.Y!=prior.Y)p.velocity.Y=-prior.Y;}
                Vector2 goal;float height;int target=Target(p,targets,path,tick,terrain,out goal,out height);if(target==-2)return -1;
                if(target>=0)
                {
                    if(p.type==225){float t=Vector2.Distance(p.Center,goal);goal.Y-=MathHelper.Lerp(height*.1f,height*.5f,Utils.GetLerpValue(0,300,t));}
                    p.velocity=(goal-p.Center).SafeNormalize(-Vector2.UnitY)*p.velocity.Length();
                }
                return 0;
            }
            if(p.type==36)
            {
                if(p.penetrate<=1)return 2;p.penetrate--;
                if(p.velocity.X!=prior.X)p.velocity.X=-prior.X;if(p.velocity.Y!=prior.Y)p.velocity.Y=-prior.Y;return 0;
            }
            if(p.type==207){p.position+=p.velocity;return 2;}
            return -1;
        }
        internal static bool Step(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain,out bool killed,out bool skipped,Func<Projectile,bool> afterAi=null)
        {return HostArrowFamilyAttack.StepMotor(p,terrain,out killed,out skipped,q=>Ai(q,targets,path,tick,terrain),afterAi,(q,v)=>Tile(q,v,targets,path,tick,terrain));}
        internal static Snapshot CaptureForSolve(Player player,HostAttackObstacles root,int birthType=0)
        {
            var rows=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Family(p.type))rows.Add(p);
            var scene=rows.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,Types);
            var sample=Terraria.ID.ContentSamples.ProjectilesByType[birthType==0?207:birthType];
            return new Snapshot{Scene=scene,Slots=root.CaptureBirthSlots(),Targets=root.CaptureChaseTargets(sample)};
        }
        private static List<Member> Copy(Projectile[] scene,Projectile[] sources,HostAttackObstacles root)
        {var rows=new List<Member>();foreach(var p in scene){var q=HostNoteAttack.Copy(p);rows.Add(new Member{Shot=q,Receive=root.ForkForSpawn(q),Source=sources!=null && Array.IndexOf(sources,p)>=0});}return rows;}
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,Snapshot state,NpcTrajectory path,int tick,int outer,PredictionTerrain terrain,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var m in rows)if(m.Shot.active && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height))return false;
            foreach(var m in rows)
            {
                var p=m.Shot;if(!p.active)continue;
                if(p.usesLocalNPCImmunity)for(int i=0;i<p.localNPCImmunity.Length;i++)if(p.localNPCImmunity[i]>0)p.localNPCImmunity[i]--;
                int updates=p.extraUpdates+1;
                for(int sub=0;sub<updates && p.active;sub++)
                {
                    CombatPredictionWork.Check();int before=p.penetrate;bool killed,skipped;if(!Step(p,state.Targets,path,tick,terrain,out killed,out skipped))return false;
                    if(killed || !p.active){p.active=false;p.timeLeft=0;slots.Release(p.whoAmI);break;}if(skipped)continue;
                    // Meteor spends its real penetration at the tile BEFORE
                    // Damage. Rebind that policy, retaining all shared/local
                    // histories; a fresh independent history would hit twice.
                    if(p.penetrate!=before)m.Receive=m.Receive.ForkForSpawn(p,true);
                    if(p.owner==Main.myPlayer && p.friendly && !p.hostile && p.damage>0)
                    {
                        int hits=m.Receive.SelectedHits;bool pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true,onHit:(slot,remaining)=>{if(p.usesLocalNPCImmunity && p.localNPCHitCooldown!=-2)p.localNPCImmunity[slot]=p.localNPCHitCooldown;return true;});p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,m.Confidence);return true;}
                        if(!pass && p.penetrate!=0)return false;
                    }
                    if(--p.timeLeft<=0 || p.penetrate==0){p.active=false;p.timeLeft=0;slots.Release(p.whoAmI);}
                }
            }
            return true;
        }
        internal static AttackContact Explicit(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 aim,Vector2 origin,Vector2 velocity,int age,PredictionTerrain terrain,HostAttackObstacles root,int delay,Snapshot captured=null)
        {
            if(!Handles(ammo.Projectile) || ammo.Damage<=0 || age-delay<0 || age+1>=path.Count)return null;
            captured=captured??CaptureForSolve(player,root,ammo.Projectile);root.BeginCandidate();var rows=Copy(captured.Scene,null,root);var slots=captured.Slots.Copy();
            for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,captured,path,age-delay+wait,wait,terrain,aim,out ignored))return null;}
            int slot;if(!slots.TryAllocate(out slot))return null;var p=HostNoteAttack.Copy(Terraria.ID.ContentSamples.ProjectilesByType[ammo.Projectile]);p.active=true;p.owner=player.whoAmI;p.whoAmI=slot;p.Center=origin;p.velocity=velocity;p.damage=ammo.Damage;p.knockBack=player.HeldItem.knockBack;
            if(ammo.ExtraArrowUpdates && p.arrow && p.extraUpdates<1)p.extraUpdates=1;bool wet;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true))return null;p.wet=wet && !p.ignoreWater;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;
            rows.Add(new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=true,Confidence=captured.LaunchConfidence});rows.Sort((a,b)=>a.Shot.whoAmI.CompareTo(b.Shot.whoAmI));
            for(int step=1;step<=Math.Min(120,path.Count-age-1);step++){AttackContact contact;if(!Outer(rows,slots,captured,path,age+step,delay+step,terrain,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            AttackMotion motion;
            if(!HostAttackModels.TryRead(ammo,out motion))
            {
                // 207 homing and36 reflections are deliberately absent from
                // the public linear-motor admission table. This local straight
                // descriptor proposes inputs only; Ai/Tile/Receive still prove
                // every contact. It cannot promote them to motionKnown.
                if(!Handles(ammo.Projectile))return null;var sample=Terraria.ID.ContentSamples.ProjectilesByType[ammo.Projectile];
                // The proposal motor is local, but weapon spread remains the
                // existing resolved launch contract. A deterministic homing
                // replay must not upgrade a random gun's lawful representative.
                AttackMotion launch;HostAttackModels.TryResolved(14,ammo.Speed,ammo.WeaponType,out launch);
                motion=new AttackMotion(ammo.Speed,0,0,sample.extraUpdates+1,sample.width,sample.height,sample.timeLeft,launch.Confidence,componentBirthLimit:true,maxFallSpeed:16);
            }
            var proposals=HostMotionAttackInputs.Generate(origin,motion,path,age);var directions=new List<Vector2>();var state=CaptureForSolve(player,root,ammo.Projectile);state.LaunchConfidence=motion.Confidence;root.CaptureShotFacing(player);
            for(int i=0;i<proposals.Length+41;i++)
            {
                int seed=i-proposals.Length;var b=path[Math.Min(path.Count-1,age+1+Math.Min(8,Math.Max(0,seed))*4)].ProjectileReceiveBounds;var unit=seed<0?proposals[i]:seed<9?(Center(b)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();if(directions.Contains(unit))continue;directions.Add(unit);
                var aim=origin+unit*10000;var velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);motion.Launch(ref velocity.X,ref velocity.Y);var result=Explicit(player,ammo,path,aim,origin,velocity,age,terrain,root,delay,state);if(result!=null)return result;
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0)return null;
            var state=CaptureForSolve(player,root);root.BeginCandidate();var rows=Copy(state.Scene,sources,root);var slots=state.Slots.Copy();var aim=sources[0].Center+sources[0].velocity*1000;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,slots,state,path,clock.FirstTick+step,step+1,terrain,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
