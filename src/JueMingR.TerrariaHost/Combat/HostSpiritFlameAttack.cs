using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostSpiritFlameAttack
    {
        internal static bool Handles(int type){return type==659;}
        internal static bool Family(int type){return Handles(type);}
        private sealed class Member
        {
            internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Representative;
            internal bool Hit(int slot,int remaining){Shot.timeLeft=0;return true;}
        }
        private static bool Bounds(HostProjectileTargets targets,NpcTrajectory path,int tick,int slot,out MotionRect bounds)
        {bounds=default(MotionRect);for(int i=0;i<targets.Count;i++)if(targets.Slot(i)==slot){bounds=targets.Bounds(i,path,tick);return slot!=path.Identity.Slot || path[tick].CanReceive;}return false;}
        internal static bool Ai(Projectile p,PredictionTerrain terrain,HostProjectileTargets targets,NpcTrajectory path,int tick)
        {
            if(p.localAI[0]>0)p.localAI[0]--;
            if(p.localAI[0]==0 && p.ai[0]<0 && p.owner==Main.myPlayer)
            {
                p.localAI[0]=5;
                for(int i=0;i<targets.Count;i++)
                {
                    int slot=targets.Slot(i);var b=targets.Bounds(i,path,tick);var center=new Vector2(b.CenterX,b.CenterY);MotionRect old;bool nearer=p.ai[0]<0 || Bounds(targets,path,tick,(int)p.ai[0],out old) && Vector2.Distance(new Vector2(old.CenterX,old.CenterY),p.Center)>Vector2.Distance(center,p.Center);
                    if(!nearer || Vector2.Distance(center,p.Center)>=400)continue;
                    bool clear,top=false;PredictionStop stop;var origin=new MotionRect(p.Center.X,p.Center.Y,0,0);
                    if(!terrain.CanHitLine(origin,new MotionRect(center.X,center.Y,0,0),out clear,out stop) || !clear && !terrain.CanHitLine(origin,new MotionRect(center.X,b.Y,0,0),out top,out stop))return false;
                    if(clear || top)p.ai[0]=slot;
                }
                if(p.ai[0]>=0)p.timeLeft=420;
            }
            if(p.timeLeft>30 && p.alpha>0)p.alpha-=12;if(p.alpha<0)p.alpha=0;
            bool solid;PredictionStop failure;if(!terrain.Solid(new MotionRect(p.position.X,p.position.Y,p.width,p.height),out solid,out failure))return false;
            if(p.timeLeft>2 && solid)p.timeLeft=2;
            MotionRect target;if(p.ai[0]>=0 && Bounds(targets,path,tick,(int)p.ai[0],out target))
            {
                var center=new Vector2(target.CenterX,target.CenterY);
                if(Vector2.Distance(p.Center,center)>1)p.velocity=(p.velocity*2+(center-p.Center).SafeNormalize(Vector2.UnitY)*7.5f)/3;
                return true;
            }
            if(p.ai[0]==-1 && p.timeLeft>5)p.timeLeft=5;
            if(p.ai[0]==-2 && p.timeLeft>240)p.timeLeft=240;
            if(p.ai[0]>=0)p.ai[0]=-1;return true;
        }
        internal static bool Step(Projectile p,PredictionTerrain terrain,HostProjectileTargets targets,NpcTrajectory path,int tick)
        {
            if(HostRaySpellAttack.WorldRetired(p)){p.active=false;return true;}
            if(!Ai(p,terrain,targets,path,tick))return false;
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            // Native ignoreWater and tileCollide=false leave solid retirement
            // to AI129's two-action clock rather than immediate tile collision.
            p.position+=p.velocity;return true;
        }
        private static Member Make(Projectile p,HostAttackObstacles root,bool source,bool representative)
        {return new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=source,Representative=representative};}
        private static AttackContact Receive(Member m,NpcTrajectory path,int tick,int outer,Vector2 aim)
        {
            var p=m.Shot;int prior=m.Receive.SelectedHits;bool pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true,onHit:m.Hit);p.penetrate=m.Receive.Remaining;
            if(m.Source && m.Receive.SelectedHits>prior)return AttackIntercept.BodyContact(path,tick,0,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);
            if(!pass && p.penetrate!=0)p.active=false;return null;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,HostProjectileTargets targets,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,Vector2 aim,out AttackContact contact)
        {
            contact=null;
            foreach(var m in rows)
            {
                var p=m.Shot;if(!p.active)continue;
                if(!m.Receive.BeforeNpc(tick,outer,p.Center.X,p.Center.Y,p.width,p.height) || !Step(p,terrain,targets,path,tick))return false;
                if(!p.active){slots.Release(p.whoAmI);continue;}
                contact=Receive(m,path,tick,outer,aim);if(contact!=null)return true;if(!p.active)return false;
                p.timeLeft--;
                if(p.timeLeft<=0)
                {
                    if(p.ai[0]>=0)
                    {
                        // The SAME body's Kill Damage expands to80 after the
                        // flight's OnHit writes/timeLeft=0. Keep that history;
                        // clearing it would invent a second selected hit.
                        var center=p.Center;p.width=p.height=80;p.Center=center;m.Receive.MakeUnlimited();
                        contact=Receive(m,path,tick,outer,aim);if(contact!=null)return true;
                    }
                    p.active=false;slots.Release(p.whoAmI);
                }
            }
            return true;
        }
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {
            var scene=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.type==659 && p.owner==player.whoAmI)scene.Add(p);
            var result=scene.ToArray();root.CaptureOwnedProjectileFamily(result,player.whoAmI,new[]{659});return result;
        }
        private static List<Member> Copy(Projectile[] scene,Projectile[] sources,HostAttackObstacles root)
        {
            var rows=new List<Member>();foreach(var p in scene){bool source=false;if(sources!=null)foreach(var q in sources)if(ReferenceEquals(p,q)){source=true;break;}rows.Add(Make(HostBetsyFlameAttack.Copy(p),root,source,false));}return rows;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(ammo.Projectile!=659 || age-delay<0 || age+1>=path.Count)return null;root.CaptureShotFacing(player);var scene=Capture(player,root);var pool=root.CaptureBirthSlots();var sample=new Projectile();sample.SetDefaults(659);sample.owner=player.whoAmI;var targets=root.CaptureChaseTargets(sample);
            var inputs=new List<Vector2>();
            for(int seed=0;seed<41;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;var unit=seed<9?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();
                if(inputs.Contains(unit))continue;inputs.Add(unit);root.BeginCandidate();var rows=Copy(scene,null,root);var slots=pool.Copy();var aim=origin+unit*10000;bool valid=true;
                for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,targets,path,terrain,age-delay+wait,wait,aim,out ignored)){valid=false;break;}}if(!valid)continue;
                int slot;if(!slots.TryAllocate(out slot))return null;var p=new Projectile();p.SetDefaults(659);p.owner=player.whoAmI;p.whoAmI=slot;p.active=true;p.damage=ammo.Damage;p.knockBack=player.HeldItem.knockBack;p.ai[0]=-2;
                // Zero birth rotation and scale1 are legal future RNG values.
                // Shoot's first CanHit retry still has to admit that rotation.
                var velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);bool clear;PredictionStop stop;if(!terrain.CanHit(new MotionRect(origin.X,origin.Y,0,0),new MotionRect(origin.X+velocity.X*100,origin.Y+velocity.Y*100,0,0),out clear,out stop) || !clear)continue;
                p.Center=origin+velocity*30;p.velocity=Vector2.Zero;rows.Add(Make(p,root,true,true));rows.Sort((a,c)=>a.Shot.whoAmI.CompareTo(c.Shot.whoAmI));
                for(int step=1;step<=Math.Min(120,path.Count-age-1);step++){AttackContact contact;if(!Outer(rows,slots,targets,path,terrain,age+step,delay+step,aim,out contact))break;if(contact!=null)return contact;}
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(source==null || source.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;
            foreach(var p in source)if(p==null || !p.active || p.type!=659 || p.owner!=player.whoAmI)return null;
            var scene=Capture(player,root);root.BeginCandidate();var rows=Copy(scene,source,root);var slots=root.CaptureBirthSlots().Copy();var targets=root.CaptureChaseTargets(source[0]);var aim=source[0].Center;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,slots,targets,path,terrain,clock.FirstTick+step,step+1,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
