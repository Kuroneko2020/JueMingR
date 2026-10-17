using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Blade of Grass chooses a native nearby target BEFORE drawing its curve.
    // Shoot's computed mouse velocity is not consumed by that branch. Observe
    // the sealed born curve; never pretend an arbitrary distant cursor aims it.
    internal static class HostGrassCurveAttack
    {
        internal static bool Family(int type){return type==976;}
        internal static bool Step(Projectile p,PredictionTerrain terrain)
        {
            if(HostRaySpellAttack.WorldRetired(p)){p.active=false;return true;}
            if(p.ai[0]!=0)p.velocity=p.velocity.RotatedBy(p.ai[0]);
            bool lava;if(!terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
            // AI152 explicitly checks lava even though ignoreWater is true.
            if(lava){p.active=false;p.timeLeft=0;return true;}
            float from=60-p.timeLeft;
            float envelope=Utils.Remap(from,p.ai[1],p.ai[1]+20,0,1)*Utils.Remap(from,p.ai[1],p.ai[1]+60,1,0);
            p.velocity=p.velocity.SafeNormalize(Vector2.UnitY)*(4+12*(1-envelope));
            p.rotation=p.velocity.ToRotation();
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active){p.active=false;p.timeLeft=0;return true;}
            // AI152 reflects at shimmer's surface and dies in a full pool;
            // it does not use the default upward acceleration branch.
            if(p.shimmerWet)
            {
                int x=(int)(p.Center.X/16),y=(int)(p.position.Y/16);bool full=false;
                if(WorldGen.InWorld(x,y) && WorldGen.InWorld(x,y-1)){PredictionTile tile,above;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;if(tile.Liquid==255 && tile.ShimmerLiquid){if(!terrain.Tile(x,y-1,out above,out stop))return false;full=above.Liquid>0 && above.ShimmerLiquid;}}
                if(full){p.active=false;p.timeLeft=0;}else if(p.velocity.Y>0){p.velocity.Y=-p.velocity.Y;p.wet=p.shimmerWet=false;}
            }
            p.position+=p.velocity;if(!p.manualDirectionChange)p.direction=p.velocity.X<0?-1:1;return true;
        }
        private sealed class Member {internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source;}
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(source==null || source.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0 || clock.Age<0)return null;
            var scene=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Family(p.type))scene.Add(p);
            root.CaptureOwnedProjectileFamily(scene.ToArray(),player.whoAmI,new[]{976});root.BeginCandidate();var rows=new List<Member>();
            foreach(var p in scene){var q=HostNoteAttack.Copy(p);rows.Add(new Member{Shot=q,Receive=root.ForkForSpawn(q),Source=Array.IndexOf(source,p)>=0});}
            var aim=source[0].Center+source[0].velocity*1000;
            for(int action=0;action<Math.Min(120,path.Count-clock.FirstTick);action++)
            {
                int tick=clock.FirstTick+action,outer=action+1;
                foreach(var m in rows)if(m.Shot.active && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height))return null;
                foreach(var m in rows){var p=m.Shot;for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    CombatPredictionWork.Check();if(!Step(p,terrain))return null;if(!p.active)break;
                    int before=m.Receive.SelectedHits;bool pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true);p.penetrate=m.Receive.Remaining;
                    if(m.Source && m.Receive.SelectedHits>before)return AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,AttackConfidence.Conditional);
                    if(!pass && p.penetrate!=0)return null;if(!pass || --p.timeLeft<=0){p.active=false;p.timeLeft=0;}
                }}
            }
            return null;
        }
    }
}
