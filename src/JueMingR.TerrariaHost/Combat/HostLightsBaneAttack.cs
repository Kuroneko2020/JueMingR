using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Item46 ignores Shoot's mouse velocity: native facing/nearby-target
    // selection creates this sealed slash. Observe the actual random birth;
    // an arbitrary distant cursor must never become an enhanced launch.
    internal static class HostLightsBaneAttack
    {
        internal static bool Family(int type){return type==974;}
        internal static bool Step(Projectile p,PredictionTerrain terrain)
        {
            if(HostRaySpellAttack.WorldRetired(p)){p.active=false;p.timeLeft=0;return true;}
            p.scale=p.ai[0];p.localAI[0]++;
            if(++p.frameCounter>=3){p.frameCounter=0;if(++p.frame>=12){p.active=false;p.timeLeft=0;return true;}}
            p.rotation=p.velocity.ToRotation();
            // This ignoreWater/noTile body still follows the normal wind and
            // owner-retirement outlets. Unknown shimmer remains unresolved.
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;
            p.velocity.X+=wind;if(p.shimmerWet)return false;
            if(p.owner<255 && !Main.player[p.owner].active){p.active=false;p.timeLeft=0;return true;}
            p.position+=p.velocity;if(!p.manualDirectionChange)p.direction=p.velocity.X<0?-1:1;
            return true;
        }
        internal static bool Collides(Projectile p,MotionRect target)
        {
            var box=new Rectangle((int)target.X,(int)target.Y,(int)target.Width,(int)target.Height);
            var bound=p.Hitbox;float length=46*p.scale,at=0;bound.Inflate((int)length,(int)length);
            var axis=p.rotation.ToRotationVector2();
            return bound.Intersects(box) && Collision.CheckAABBvLineCollision(box.TopLeft(),box.Size(),p.Center-axis*length,p.Center+axis*length,8*p.scale,ref at);
        }
        private sealed class Member{internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source;}
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0 || clock.Age<0)return null;
            var scene=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Family(p.type))scene.Add(p);
            root.CaptureOwnedProjectileFamily(scene.ToArray(),player.whoAmI,new[]{974});root.BeginCandidate();var rows=new List<Member>();
            foreach(var p in scene){var q=HostNoteAttack.Copy(p);rows.Add(new Member{Shot=q,Receive=root.ForkForSpawn(q),Source=Array.IndexOf(sources,p)>=0});}
            var aim=sources[0].Center+sources[0].velocity*1000;
            for(int action=0;action<Math.Min(120,path.Count-clock.FirstTick);action++)
            {
                int tick=clock.FirstTick+action,outer=action+1;
                foreach(var m in rows)if(m.Shot.active && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height))return null;
                foreach(var m in rows)
                {
                    var p=m.Shot;
                    for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                    {
                        CombatPredictionWork.Check();if(!Step(p,terrain))return null;if(!p.active)break;
                        int before=m.Receive.SelectedHits;
                        bool pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,b=>Collides(p,b),consumeSelected:true);
                        p.penetrate=m.Receive.Remaining;
                        // Native Strike precedes the exhausted-penetration
                        // stop flag. Never erase that already proved receive.
                        if(m.Source && m.Receive.SelectedHits>before)return AttackIntercept.ProjectileShapeContact(path,tick,sub,true,aim.X,aim.Y,p.Center.X,p.Center.Y,AttackConfidence.Conditional);
                        if(!pass && p.penetrate!=0)return null;
                        if(!pass || --p.timeLeft<=0){p.active=false;p.timeLeft=0;}
                    }
                }
            }
            return null;
        }
    }
}
