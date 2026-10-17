using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // AI093 keeps its captured NPC attachment even when it cannot receive.
    // The small entry hit and the later 112px Kill burst are different native
    // Damage calls. Their owner history and penetration remain one family.
    internal static class HostNailAttack
    {
        internal static bool Handles(int type){return type==514;}
        internal static bool Family(int type){return Handles(type);}
        private sealed class Member {internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Random;}
        private static Member Make(Projectile p,HostAttackObstacles root,bool source){return new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=source};}
        private static MotionRect? Attached(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick)
        {int slot=(int)-p.ai[0]-1;for(int i=0;i<targets.Count;i++)if(targets.Slot(i)==slot && targets.Alive(i))return targets.Bounds(i,path,tick);return null;}
        internal static bool Step(Projectile p,PredictionTerrain terrain,MotionRect? attached,out bool killed,out Vector2 death,out bool random)
        {
            killed=random=false;death=p.Center;if(HostRaySpellAttack.WorldRetired(p)){p.active=false;return true;}Vector2 wetVelocity=p.velocity;p.alpha=Math.Max(0,p.alpha-25);if(p.velocity.Y>18)p.velocity.Y=18;
            if(p.ai[0]==0){if(++p.ai[1]>20){p.velocity.Y+=.1f;p.velocity.X*=.992f;}}
            else
            {
                p.tileCollide=false;
                if(p.ai[0]==1)p.velocity*=.6f;
                else
                {
                    if(attached.HasValue)p.Center=new Vector2(attached.Value.CenterX,attached.Value.CenterY)-p.velocity;
                    if(!attached.HasValue){p.tileCollide=true;p.ai[0]=0;p.ai[1]=20;p.velocity=Vector2.UnitX*6;random=true;}
                    else if(p.velocity.Length()>(int)(attached.Value.Width+attached.Value.Height)/3)p.velocity*=.99f;
                }
                if(p.ai[0]!=0 && ++p.ai[1]>90){killed=true;death=p.Center;return true;}
            }
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            bool wet,lava;byte kind;if(!p.ignoreWater)
            {
                if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
                if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;p.wet=wet;if(!wet)p.lavaWet=p.honeyWet=p.shimmerWet=false;
            }
            if(p.shimmerWet){if(p.timeLeft>3000)p.timeLeft-=60;p.velocity.Y-=.5f;}
            var position=p.position;var velocity=p.velocity;var prior=velocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet && !p.ignoreWater,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;p.position=position;p.velocity=velocity;
            if(hit && velocity!=prior){p.ai[1]=0;p.ai[0]=1;p.tileCollide=false;p.position+=velocity;p.velocity=prior.SafeNormalize(Vector2.Zero)*3;}
            // An attached nail explicitly skips UpdatePosition; its center is
            // NPC.Center minus its retained attachment vector, not NPC.Center.
            if(p.ai[0]>=0)p.position+=p.wet && !p.ignoreWater?wetVelocity:p.velocity;return true;
        }
        private static bool Burst(Member m,NpcTrajectory path,int tick,int outer,int sub,Vector2 center,Vector2 aim,out AttackContact contact)
        {
            contact=null;var p=m.Shot;var burst=HostBetsyFlameAttack.Copy(p);burst.width=burst.height=112;burst.Center=center;burst.penetrate=-1;burst.ai[0]=2;
            var receive=m.Receive.ForkForSpawn(burst,true);int hits=receive.SelectedHits;bool pass=receive.PassCustomOuter(2,tick,outer,center.X,center.Y,112,112,null,unlimited:true,consumeSelected:true);
            if(m.Source && receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,center.X,center.Y,112,112,m.Random?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}return pass;
        }
        private static bool Outer(List<Member> rows,HostProjectileTargets targets,NpcTrajectory path,int tick,int outer,PredictionTerrain terrain,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var m in rows)if(m.Shot.active && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height))return false;
            foreach(var m in rows)
            {
                var p=m.Shot;for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    CombatPredictionWork.Check();bool killed,random;Vector2 death;if(!Step(p,terrain,p.ai[0]<0?Attached(p,targets,path,tick):null,out killed,out death,out random))return false;m.Random|=random;if(!p.active)break;
                    if(!killed && (p.ai[0]==0 || p.ai[0]==2) && (int)(p.damage*(p.ai[0]==2?1.35:.15))>0)
                    {
                        int hits=m.Receive.SelectedHits;bool pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true,onHit:(slot,left)=>
                        {if(p.ai[0]==0){MotionRect b=slot==path.Identity.Slot?path[tick].Bounds:default(MotionRect);if(slot!=path.Identity.Slot)for(int i=0;i<targets.Count;i++)if(targets.Slot(i)==slot){b=targets.Bounds(i,path,tick);break;}p.ai[0]=-slot-1;p.ai[1]=0;p.velocity=new Vector2(b.CenterX,b.CenterY)-p.Center;}return true;});p.penetrate=m.Receive.Remaining;
                        // A proved selected Strike survives any later unknown
                        // event in this same ordered native Damage pass.
                        if(m.Source && m.Receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,m.Random?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}if(!pass && p.penetrate!=0)return false;killed=p.penetrate==0;
                    }
                    if(!killed)killed=--p.timeLeft<=0;
                    if(killed){if(!Burst(m,path,tick,outer,sub,p.ai[1]>90?death:p.Center,aim,out contact))return false;if(contact!=null)return true;p.active=false;p.timeLeft=0;}
                }
            }
            return true;
        }
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {var list=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Handles(p.type))list.Add(p);var scene=list.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{514});return scene;}
        private static List<Member> Copy(Projectile[] scene,Projectile[] source,HostAttackObstacles root)
        {var list=new List<Member>();foreach(var p in scene)list.Add(Make(HostBetsyFlameAttack.Copy(p),root,source!=null && Array.IndexOf(source,p)>=0));return list;}
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(ammo.Projectile!=514 || age-delay<0 || age+1>=path.Count)return null;root.CaptureShotFacing(player);var scene=Capture(player,root);var targets=root.CaptureNailTargets(Terraria.ID.ContentSamples.ProjectilesByType[514]);var pool=root.CaptureBirthSlots();var directions=new List<Vector2>();
            // Its free motor is separable through the first attachment. Use
            // the shared rectangle inverse only to propose inputs; native cap,
            // tile-stick, attachment and bursts still decide every Contact.
            var free=new AttackMotion(ammo.Speed,.1f,21,1,10,10,3600,acceleration:.992f,maxFallSpeed:18.1f,accelerationStart:21,horizontalAccelerationOnly:true);var proposals=HostMotionAttackInputs.Generate(origin,free,path,age);
            for(int input=0;input<proposals.Length+41;input++)
            {
                int seed=input-proposals.Length;var b=path[Math.Min(path.Count-1,age+1+Math.Min(Math.Max(seed,0),8)*4)].ProjectileReceiveBounds;var unit=seed<0?proposals[input]:seed<9?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();if(directions.Contains(unit))continue;directions.Add(unit);var aim=origin+unit*10000;root.BeginCandidate();var rows=Copy(scene,null,root);bool valid=true;
                for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,targets,path,age-delay+wait,wait,terrain,aim,out ignored)){valid=false;break;}}if(!valid)continue;
                var slots=pool.Copy();foreach(var old in rows)if(!old.Shot.active)slots.Release(old.Shot.whoAmI);int slot;if(!slots.TryAllocate(out slot))return null;var shot=new Projectile();shot.SetDefaults(514);shot.active=true;shot.owner=player.whoAmI;shot.whoAmI=slot;shot.Center=origin;shot.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);shot.damage=ammo.Damage;shot.knockBack=player.HeldItem.knockBack;bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,shot.width,shot.height,out wet,out kind,true))return null;shot.wet=wet;shot.honeyWet=wet && kind==2;shot.shimmerWet=wet && kind==3;rows.Add(Make(shot,root,true));rows.Sort((a,c)=>a.Shot.whoAmI.CompareTo(c.Shot.whoAmI));
                for(int step=1;age+step<path.Count;step++){AttackContact contact;if(!Outer(rows,targets,path,age+step,delay+step,terrain,aim,out contact))break;if(contact!=null)return contact;}
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(source==null || source.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0)return null;var scene=Capture(player,root);var targets=root.CaptureNailTargets(source[0]);root.BeginCandidate();var rows=Copy(scene,source,root);var aim=source[0].Center+source[0].velocity*1000;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,targets,path,clock.FirstTick+step,step+1,terrain,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
