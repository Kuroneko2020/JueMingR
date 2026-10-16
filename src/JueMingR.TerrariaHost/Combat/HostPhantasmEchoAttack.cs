using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    //631 is a sealed echo of an already received arrow. It cannot damage
    // along its flight: native AI122 calls its one Damage only after its
    // PRE-movement body overlaps the original locked victim, even after Kill.
    // This observer grants neither a new hand nor an ongoing cursor lease.
    internal static class HostPhantasmEchoAttack
    {
        internal static bool Handles(int type){return type==631;}
        private sealed class Member{internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source;}
        internal static bool Ai(Projectile p,HostProjectileTargets targets,NpcTrajectory path,int tick,out bool burst)
        {
            burst=false;int slot=(int)p.ai[0];bool valid=false;MotionRect target=default(MotionRect);
            if(slot>=0 && slot<Main.maxNPCs)for(int i=0;i<targets.Count;i++)if(targets.Slot(i)==slot){target=targets.Bounds(i,path,tick);valid=true;break;}
            if(!valid && p.ai[0]!=-1)p.ai[0]=-1;
            if(valid && p.Hitbox.Intersects(new Rectangle((int)target.X,(int)target.Y,(int)target.Width,(int)target.Height)))
            {p.timeLeft=0;p.localAI[1]=1;burst=true;return true;}
            if(p.ai[1]>0){p.ai[1]--;p.velocity=Vector2.Zero;return true;}
            if(!valid)
            {if(p.velocity==Vector2.Zero){p.active=false;return true;}p.tileCollide=true;p.alpha+=10;if(p.alpha>255){p.active=false;return true;}}
            else{p.velocity=Vector2.Normalize(new Vector2(target.CenterX,target.CenterY)-p.Center)*12;p.alpha=Math.Max(0,p.alpha-15);}
            p.rotation=p.velocity.ToRotation()-MathHelper.PiOver2;return true;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0)return null;var scene=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.type==631 && p.owner==player.whoAmI)scene.Add(p);
            root.CaptureOwnedProjectileFamily(scene.ToArray(),player.whoAmI,new[]{631});var targets=root.CapturePhantasmTargets(sources[0]);root.BeginCandidate();var rows=new List<Member>();
            foreach(var p in scene){var q=HostThornAttack.Copy(p);rows.Add(new Member{Shot=q,Receive=root.ForkForSpawn(q),Source=Array.IndexOf(sources,p)>=0});}
            for(int action=0;action<Math.Min(120,path.Count-clock.FirstTick);action++)
            {
                int tick=clock.FirstTick+action,outer=action+1;
                foreach(var member in rows)
                {
                    var p=member.Shot;if(!p.active)continue;if(!member.Receive.BeforeNpc(tick,outer,p.Center.X,p.Center.Y,p.width,p.height))return null;
                    for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                    {
                        CombatPredictionWork.Check();if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld){p.active=false;break;}bool burst;if(!Ai(p,targets,path,tick,out burst))return null;
                        if(burst)
                        {
                            int hits=member.Receive.SelectedHits;bool passed=member.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true);
                            if(member.Source && member.Receive.SelectedHits>hits)return AttackIntercept.BodyContact(path,tick,sub,p.Center.X,p.Center.Y,p.Center.X,p.Center.Y,p.width,p.height,AttackConfidence.Conditional);
                            if(!passed && member.Receive.Remaining!=0)return null;p.active=false;break;
                        }
                        if(!p.active)break;float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return null;p.velocity.X+=wind;
                        var position=p.position;var velocity=p.velocity;Vector2 wetVelocity;bool hit=false;if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,false,false,out wetVelocity,out hit,nativeClip:true))return null;p.position=position;p.velocity=velocity;if(hit){p.active=false;break;}p.position+=velocity;if(--p.timeLeft<=0)p.active=false;
                    }
                }
            }
            return null;
        }
    }
}
