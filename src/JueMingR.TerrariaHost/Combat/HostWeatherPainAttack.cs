using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // AI177's same-owner retirement is an action in the shared slot order,
    // not a birth-time shortcut. A new storm still shares ID-static damage
    // history with its fading predecessor, and never reads a later cursor.
    internal static class HostWeatherPainAttack
    {
        internal static bool Handles(int type){return type==969;}
        private sealed class Member{internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source;}
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {var list=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Handles(p.type))list.Add(p);var scene=list.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{969});return scene;}
        private static List<Member> Copy(Projectile[] scene,Projectile[] sources,HostAttackObstacles root)
        {var list=new List<Member>();foreach(var p in scene){var q=HostNebulaBlazeAttack.Copy(p);list.Add(new Member{Shot=q,Receive=root.ForkForSpawn(q),Source=sources!=null && Array.IndexOf(sources,p)>=0});}return list;}
        internal static bool Ai(Projectile p,Projectile[] scene,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain)
        {
            p.ai[1]++;if(p.ai[1]>560)p.alpha=(int)MathHelper.Lerp(0,250,(p.ai[1]-560)/10);if(p.ai[1]>=570){p.active=false;p.timeLeft=0;}
            foreach(var other in scene)if(other!=p && other.active && other.owner==p.owner && other.type==p.type && p.timeLeft>other.timeLeft && other.ai[1]<555)other.ai[1]=555;
            int slot=(int)p.ai[0]-1,index=-1;
            for(int i=0;i<targets.Count;i++)if(targets.Slot(i)==slot){index=i;break;}
            if(p.owner==Main.myPlayer)
            {
                if(slot!=-1 && (index<0 || p.localNPCImmunity[slot]>0)){slot=-1;index=-1;p.ai[0]=0;}
                if(slot==-1)
                {
                    float nearest=400;
                    for(int i=0;i<targets.Count;i++)
                    {
                        int candidate=targets.Slot(i);if(p.localNPCImmunity[candidate]!=0)continue;var box=targets.Bounds(i,path,tick);if(candidate==path.Identity.Slot && !path[tick].CanReceive)continue;
                        bool clear;PredictionStop stop;if(!terrain.CanHitLine(new MotionRect(p.position.X,p.position.Y,p.width,p.height),box,out clear,out stop))return false;if(!clear)continue;
                        float distance=Vector2.Distance(p.Center,new Vector2(box.CenterX,box.CenterY));if(distance>=nearest)continue;nearest=distance;slot=candidate;index=i;
                    }
                    if(slot!=-1)p.ai[0]=slot+1;
                }
            }
            if(index>=0)
            {var box=targets.Bounds(index,path,tick);var delta=new Vector2(box.CenterX,box.CenterY)-p.Center;float distance=delta.Length();if(distance>=25 && distance>0)p.velocity=Vector2.Lerp(p.velocity,delta/distance*Math.Min(8,distance),.075f);}
            else if(p.velocity!=Vector2.Zero)p.velocity=Vector2.Lerp(p.velocity,Vector2.Normalize(p.velocity)*8,.075f);
            p.rotation=p.velocity.X*.0125f;if(++p.frameCounter>4){p.frameCounter=0;if(++p.frame>=Main.projFrames[p.type])p.frame=0;}return true;
        }
        internal static bool Step(Projectile p,Projectile[] scene,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain)
        {
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld){p.active=false;return true;}
            if(!Ai(p,scene,targets,path,tick,terrain))return false;
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active){p.active=false;p.timeLeft=0;}
            if(!p.ignoreWater)
            {bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;p.wet=wet;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;if(lava)p.lavaWet=true;if(!wet)p.honeyWet=p.shimmerWet=p.lavaWet=false;}
            var position=p.position;var prior=p.velocity;var velocity=prior;Vector2 wetVelocity=prior;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet && !p.ignoreWater,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;
            p.position=position;if(hit){if(velocity.X!=prior.X && Math.Abs(prior.X)>1)velocity.X=-prior.X*.75f;if(velocity.Y!=prior.Y && Math.Abs(prior.Y)>1)velocity.Y=-prior.Y*.75f;}
            // Native Update completes HandleMovement even after AI's Kill,
            // then checks active BEFORE Damage. Keep that retired tail exact.
            p.velocity=velocity;p.position+=p.wet && !p.ignoreWater?wetVelocity:velocity;return true;
        }
        private static bool Outer(List<Member> rows,Projectile[] scene,HostProjectileTargets targets,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,Vector2 aim,out AttackContact contact)
        {
            contact=null;
            foreach(var m in rows)
            {
                var p=m.Shot;if(!p.active)continue;for(int i=0;i<p.localNPCImmunity.Length;i++)if(p.localNPCImmunity[i]>0)p.localNPCImmunity[i]--;
                if(!m.Receive.BeforeNpc(tick,outer,p.Center.X,p.Center.Y,p.width,p.height))return false;
                for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    CombatPredictionWork.Check();if(!Step(p,scene,targets,path,tick,terrain))return false;if(!p.active)break;
                    int before=m.Receive.SelectedHits;bool pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true);p.penetrate=m.Receive.Remaining;
                    if(m.Source && m.Receive.SelectedHits>before){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,AttackConfidence.Conditional);return true;}
                    if(!pass || --p.timeLeft<=0)p.active=false;
                }
            }
            return true;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.Age<0 || clock.LaunchDelay!=0)return null;
            var scene=Capture(player,root);var targets=root.CaptureChaseTargets(sources[0]);root.BeginCandidate();var rows=Copy(scene,sources,root);var privateScene=rows.ConvertAll(m=>m.Shot).ToArray();var aim=sources[0].Center+sources[0].velocity*1000;
            for(int action=0;action<Math.Min(120,path.Count-clock.FirstTick);action++){AttackContact contact;if(!Outer(rows,privateScene,targets,path,terrain,clock.FirstTick+action,action+1,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(ammo.Damage<=0 || age-delay<0 || age+1>=path.Count)return null;
            var prototype=new Projectile();prototype.SetDefaults(969);prototype.owner=player.whoAmI;var targets=root.CaptureChaseTargets(prototype);var scene=Capture(player,root);var slots=root.CaptureBirthSlots();root.CaptureHomingInputs(player,false);root.CaptureShotFacing(player);
            for(int seed=0;seed<9;seed++)
            {
                var box=path[Math.Min(path.Count-1,age+1+seed*4)].ProjectileReceiveBounds;var aim=HostDirectionalControl.Cursor(player,new Vector2(box.CenterX,box.CenterY));root.BeginCandidate();var rows=Copy(scene,null,root);var privateScene=rows.ConvertAll(m=>m.Shot).ToArray();bool available=true;
                for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,privateScene,targets,path,terrain,age-delay+wait,wait,aim,out ignored)){available=false;break;}}if(!available)continue;
                int slot;if(!slots.Copy().TryAllocate(out slot))return null;var p=HostNebulaBlazeAttack.Copy(prototype);p.active=true;p.whoAmI=slot;p.Center=origin;p.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);p.damage=ammo.Damage;p.knockBack=player.HeldItem.knockBack;rows.Add(new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=true});rows.Sort((a,b)=>a.Shot.whoAmI.CompareTo(b.Shot.whoAmI));privateScene=rows.ConvertAll(m=>m.Shot).ToArray();
                for(int action=1;action<=Math.Min(120,path.Count-age-1);action++){AttackContact contact;if(!Outer(rows,privateScene,targets,path,terrain,age+action,delay+action,aim,out contact))break;if(contact!=null)return contact;}
            }
            return null;
        }
    }
}

