using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // AI081's reappearance is the SAME projectile, with unchanged penetration.
    // Native OnHit stops the NPC loop before its ordinary immunity/decrement
    // tail. A random teleport angle is represented explicitly, never predicted.
    internal static class HostInfluxAttack
    {
        internal static bool Handles(int type){return type==451;}
        internal static bool Family(int type){return Handles(type);}
        private sealed class Member {internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Random;}
        private static Member Make(Projectile p,HostAttackObstacles root,bool source){return new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=source};}
        private static int Nearest(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick)
        {int best=-1;float distance=250;for(int i=0;i<targets.Count;i++){var b=targets.Bounds(i,path,tick);float d=Vector2.Distance(p.Center,new Vector2(b.CenterX,b.CenterY));if(d<distance){distance=d;best=i;}}return best;}
        internal static bool Ai(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick,out bool random)
        {
            random=false;if(p.penetrate<=0)return false;int n=p.penetrate;float speed=Math.Max(1,p.velocity.Length()/11);
            if(p.ai[0]==-1)
            {
                p.tileCollide=true;p.localAI[0]++;if(p.localAI[0]>Math.Min(7,(int)(7/speed)) && p.localAI[1]==0){p.scale-=.01f*speed;p.alpha+=(int)(5*speed);if(p.alpha>75){p.alpha=75;p.ai[0]=0;}}
            }
            else if(p.ai[0]==0)
            {
                p.tileCollide=true;p.localAI[0]++;if(p.localAI[0]>Math.Min(7,(int)(7*speed)))
                {if(p.localAI[1]==0){p.scale-=.01f;p.alpha+=5;if(p.alpha>75){p.alpha=75;p.localAI[1]=1;}}else if(p.localAI[1]==1){p.scale+=.01f;p.alpha-=5;if(p.alpha<=0){p.alpha=0;p.localAI[1]=0;}}}
            }
            else if(p.ai[0]>=1 && p.ai[0]<1+n)
            {
                p.tileCollide=false;p.alpha+=(int)(15*speed);p.velocity*=.98f/speed;p.localAI[0]=0;int nearest=Nearest(p,targets,path,tick);
                if(p.alpha>=255)
                {
                    p.alpha=255;if(p.ai[0]==1 || nearest<0){p.active=false;p.timeLeft=0;return true;}var b=targets.Bounds(nearest,path,tick);var center=new Vector2(b.CenterX,b.CenterY);p.ai[0]+=n;p.Center=center+Vector2.UnitX*100;p.velocity=-Vector2.UnitX*15;random=true;
                }
                if(p.active && nearest>=0)p.position+=targets.Velocity(nearest,path,tick);
            }
            else if(p.ai[0]>=1+n && p.ai[0]<1+n*2)
            {
                p.scale=.9f;p.tileCollide=false;p.ai[1]++;
                if(p.ai[1]>=15){p.alpha+=51;p.velocity*=.8f;if(p.alpha>=255){p.active=false;p.timeLeft=0;}}
                else{p.alpha=Math.Max(0,p.alpha-125);p.velocity*=.98f;}p.localAI[0]++;int nearest=Nearest(p,targets,path,tick);if(nearest>=0)p.position+=targets.Velocity(nearest,path,tick);
            }
            p.rotation=p.velocity.ToRotation()+MathHelper.PiOver4;return true;
        }
        internal static bool CanDamage(Projectile p)
        {return p.penetrate>0 && !(((int)(p.ai[0]-1)/p.penetrate==0 || p.ai[1]<5) && p.ai[0]!=0);}
        internal static bool Step(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain,out bool random)
        {
            random=false;if(HostRaySpellAttack.WorldRetired(p)){p.active=false;return true;}var wetVelocity=p.velocity;if(!Ai(p,targets,path,tick,out random))return false;if(!p.active)return true;float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(!p.ignoreWater){bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;p.wet=wet;if(!wet)p.lavaWet=p.honeyWet=p.shimmerWet=false;}
            if(p.shimmerWet){if(p.timeLeft>3000)p.timeLeft-=60;p.velocity.Y-=.5f;}
            var position=p.position;var velocity=p.velocity;var prior=velocity;bool hit=false;if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet && !p.ignoreWater,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;p.position=position;p.velocity=velocity;
            if(hit){p.ai[0]=1;p.ai[1]=0;p.velocity=prior/2;}p.position+=p.wet && !p.ignoreWater?wetVelocity:p.velocity;return true;
        }
        private static bool Outer(List<Member> rows,HostProjectileTargets targets,NpcTrajectory path,int tick,int outer,PredictionTerrain terrain,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var m in rows)if(m.Shot.active && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height))return false;
            foreach(var m in rows){var p=m.Shot;for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
            {
                CombatPredictionWork.Check();bool random;if(!Step(p,targets,path,tick,terrain,out random))return false;m.Random|=random;if(!p.active)break;
                if(CanDamage(p) && p.damage>0)
                {
                    int hits=m.Receive.SelectedHits;bool stopped=false;bool pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,unlimited:true,consumeSelected:true,onHit:(slot,left)=>{p.ai[0]=p.ai[0]==0?p.ai[0]+p.penetrate:p.ai[0]-p.penetrate-1;p.ai[1]=0;stopped=true;return false;});
                    if(m.Source && m.Receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,m.Random?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}if(!pass && !stopped)return false;
                }
                if(--p.timeLeft<=0){p.active=false;p.timeLeft=0;}
            }}return true;
        }
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {var list=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Handles(p.type))list.Add(p);var scene=list.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{451});return scene;}
        private static List<Member> Copy(Projectile[] scene,Projectile[] source,HostAttackObstacles root)
        {var list=new List<Member>();foreach(var p in scene)list.Add(Make(HostBetsyFlameAttack.Copy(p),root,source!=null && Array.IndexOf(source,p)>=0));return list;}
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(ammo.Projectile!=451 || age-delay<0 || age+1>=path.Count)return null;root.CaptureShotFacing(player);var scene=Capture(player,root);var targets=root.CaptureMovingChaseTargets(Terraria.ID.ContentSamples.ProjectilesByType[451]);var pool=root.CaptureBirthSlots();var directions=new List<Vector2>();
            for(int seed=0;seed<41;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;var unit=seed<9?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();if(directions.Contains(unit))continue;directions.Add(unit);var aim=origin+unit*10000;root.BeginCandidate();var rows=Copy(scene,null,root);bool valid=true;
                for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,targets,path,age-delay+wait,wait,terrain,aim,out ignored)){valid=false;break;}}if(!valid)continue;var slots=pool.Copy();foreach(var old in rows)if(!old.Shot.active)slots.Release(old.Shot.whoAmI);int slot;if(!slots.TryAllocate(out slot))return null;
                var shot=new Projectile();shot.SetDefaults(451);shot.active=true;shot.owner=player.whoAmI;shot.whoAmI=slot;shot.Center=origin;shot.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);shot.damage=ammo.Damage;shot.knockBack=player.HeldItem.knockBack;shot.ai[0]=-1;bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,shot.width,shot.height,out wet,out kind,true))return null;shot.wet=wet;shot.honeyWet=wet && kind==2;shot.shimmerWet=wet && kind==3;rows.Add(Make(shot,root,true));rows.Sort((a,c)=>a.Shot.whoAmI.CompareTo(c.Shot.whoAmI));
                for(int step=1;age+step<path.Count;step++){AttackContact contact;if(!Outer(rows,targets,path,age+step,delay+step,terrain,aim,out contact))break;if(contact!=null)return contact;}
            }return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(source==null || source.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0)return null;var scene=Capture(player,root);var targets=root.CaptureMovingChaseTargets(source[0]);root.BeginCandidate();var rows=Copy(scene,source,root);var aim=source[0].Center+source[0].velocity*1000;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,targets,path,clock.FirstTick+step,step+1,terrain,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
