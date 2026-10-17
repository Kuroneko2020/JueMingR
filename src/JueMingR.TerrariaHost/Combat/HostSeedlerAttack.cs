using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // A nut's random AI retirement creates seeds, not an explosion Damage.
    // Use legal finite random representatives; neither live RNG nor Kill runs.
    internal static class HostSeedlerAttack
    {
        internal static bool Handles(int type){return type==483;}
        internal static bool Family(int type){return type==483 || type==484;}
        private sealed class Member {internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Representative;}
        private static Member Make(Projectile p,HostAttackObstacles root,bool source,bool random){return new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=source,Representative=random};}
        internal static bool Step(Projectile p,PredictionTerrain terrain,int deathThreshold,out bool killed)
        {
            killed=false;if(HostRaySpellAttack.WorldRetired(p)){p.active=false;return true;}
            var wetVelocity=p.velocity;
            if(p.type==483)
            {
                p.ai[0]++;
                if(p.ai[0]>5)
                {
                    if(p.owner==Main.myPlayer && p.ai[0]>deathThreshold)killed=true;
                    if(p.velocity.Y==0 && p.velocity.X!=0){p.velocity.X*=.97f;if((double)p.velocity.X>-.01 && (double)p.velocity.X<.01)p.velocity.X=0;}
                    p.velocity.Y+=.3f;p.velocity.X*=.99f;
                }
            }
            else
            {
                // 484 is in AI001's flag3=false set. Its ai0 is not a
                // lifetime counter: native newborn zero stays straight.
                if(p.ai[0]>=15){p.ai[0]=15;p.velocity.Y+=.1f;}
            }
            if(p.velocity.Y>16)p.velocity.Y=16;
            if(killed)return true;
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(!p.ignoreWater)
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
                if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;p.wet=wet;if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;
            }
            if(p.shimmerWet)
            {
                if(p.type==483){if(p.timeLeft>3000)p.timeLeft-=60;p.velocity.Y-=.5f;}
                else
                {
                    int x=(int)(p.Center.X/16),y=(int)(p.position.Y/16);bool full=false;
                    if(WorldGen.InWorld(x,y) && WorldGen.InWorld(x,y-1)){PredictionTile tile,above;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;if(tile.Liquid==255 && tile.ShimmerLiquid){if(!terrain.Tile(x,y-1,out above,out stop))return false;full=above.Liquid>0 && above.ShimmerLiquid;}}
                    if(full){killed=true;return true;}if(p.velocity.Y>0){p.velocity.Y=-p.velocity.Y;if(p.timeLeft>600)p.timeLeft=600;p.timeLeft-=60;p.wet=p.shimmerWet=false;}
                }
            }
            var prior=p.velocity;Vector2 position=p.position,velocity=p.velocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;p.position=position;p.velocity=velocity;
            if(hit)
            {
                if(p.type==484){p.position+=p.velocity;killed=true;return true;}
                if(p.velocity.X!=prior.X)p.velocity.X=prior.X*-.5f;if(p.velocity.Y!=prior.Y && prior.Y>1)p.velocity.Y=prior.Y*-.5f;
            }
            p.position+=p.wet?wetVelocity:p.velocity;return true;
        }
        private static bool Kill(Member m,List<Member> rows,HostProjectileBirthSlots slots,HostProjectileTargets targets,NpcTrajectory path,int tick,PredictionTerrain terrain,HostAttackObstacles root)
        {
            var p=m.Shot;if(!p.active)return true;p.timeLeft=0;
            if(p.type==483 && p.owner==Main.myPlayer)
            {
                // Count4, speed8, identity after100 pair swaps and the random
                // vector(100,0) are all legal native values. Preserve the
                // exact first-four qualification and reverse target assignment.
                var chosen=new List<Vector2>();
                for(int i=0;i<targets.Count && chosen.Count<4;i++)
                {
                    var b=targets.Bounds(i,path,tick);var center=new Vector2(b.CenterX,b.CenterY);if(Vector2.Distance(p.Center,center)>2000)continue;bool clear;PredictionStop stop;
                    if(!terrain.CanHitLine(new MotionRect(p.position.X,p.position.Y,p.width,p.height),b,out clear,out stop))return false;if(clear)chosen.Add(center);
                }
                for(int index=0;index<4;index++)
                {
                    Vector2 velocity=Vector2.UnitX;if(chosen.Count>0){var center=chosen[chosen.Count-1];chosen.RemoveAt(chosen.Count-1);velocity=(center-p.Center).SafeNormalize(Vector2.UnitX);}velocity*=8;
                    int slot;if(!slots.TryAllocate(out slot))return false;var child=new Projectile();child.SetDefaults(484);child.active=true;child.whoAmI=slot;child.owner=p.owner;child.Center=p.Center;child.velocity=velocity;child.damage=(int)((double)p.damage*.7);child.knockBack=p.knockBack*.7f;
                    bool wet;byte kind;if(!terrain.ProjectileWet(child.Center.X,child.Center.Y,child.width,child.height,out wet,out kind,true))return false;child.wet=wet;child.honeyWet=wet && kind==2;child.shimmerWet=wet && kind==3;
                    rows.Add(Make(child,root,m.Source,true));
                }
            }
            p.active=false;slots.Release(p.whoAmI);return true;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,HostProjectileTargets targets,NpcTrajectory path,int tick,int outer,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var m in rows)if(m.Shot.active && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height))return false;
            int after=-1;for(int visit=0;visit<1000;visit++)
            {
                Member m=null;foreach(var q in rows)if(q.Shot.active && q.Shot.whoAmI>after && (m==null || q.Shot.whoAmI<m.Shot.whoAmI))m=q;if(m==null)return true;var p=m.Shot;after=p.whoAmI;
                for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    CombatPredictionWork.Check();bool killed;if(!Step(p,terrain,20,out killed))return false;
                    if(killed){if(!Kill(m,rows,slots,targets,path,tick,terrain,root))return false;break;}if(!p.active){slots.Release(p.whoAmI);break;}
                    if(p.damage>0)
                    {
                        int hits=m.Receive.SelectedHits;bool passed=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true);p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}
                        if(!passed && p.penetrate!=0)return false;
                    }
                    if(--p.timeLeft<=0 || p.penetrate==0)if(!Kill(m,rows,slots,targets,path,tick,terrain,root))return false;
                }
            }
            return false;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(ammo.Projectile!=483 || age-delay<0 || age+1>=path.Count)return null;root.CaptureShotFacing(player);var live=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && (p.type==483 || p.type==484) && p.owner==player.whoAmI)live.Add(p);
            var scene=live.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{483,484});var pool=root.CaptureBirthSlots();var sample=new Projectile();sample.SetDefaults(483);sample.owner=player.whoAmI;var targets=root.CaptureSeedlerTargets(sample);var directions=new List<Vector2>();
            for(int seed=0;seed<41;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;var unit=seed<9?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();if(directions.Contains(unit))continue;directions.Add(unit);var aim=origin+unit*10000;
                root.BeginCandidate();var rows=new List<Member>();foreach(var p in scene)rows.Add(Make(HostBetsyFlameAttack.Copy(p),root,false,false));var slots=pool.Copy();bool valid=true;
                for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,targets,path,age-delay+wait,wait,terrain,root,aim,out ignored)){valid=false;break;}}if(!valid)continue;
                int slot;if(!slots.TryAllocate(out slot))return null;var shot=new Projectile();shot.SetDefaults(483);shot.active=true;shot.owner=player.whoAmI;shot.whoAmI=slot;shot.Center=origin;shot.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);shot.damage=ammo.Damage;shot.knockBack=player.HeldItem.knockBack;
                bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,shot.width,shot.height,out wet,out kind,true))return null;shot.wet=wet;shot.honeyWet=wet && kind==2;shot.shimmerWet=wet && kind==3;rows.Add(Make(shot,root,true,false));
                for(int step=1;step<=Math.Min(120,path.Count-age-1);step++){AttackContact contact;if(!Outer(rows,slots,targets,path,age+step,delay+step,terrain,root,aim,out contact))break;if(contact!=null)return contact;}
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(source==null || source.Length==0 || clock.Age<0)return null;
            var live=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && (p.type==483 || p.type==484))live.Add(p);
            var scene=live.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{483,484});var slots=root.CaptureBirthSlots().Copy();var targets=root.CaptureSeedlerTargets(source[0]);root.BeginCandidate();var rows=new List<Member>();
            foreach(var p in scene){bool contributes=Array.IndexOf(source,p)>=0;rows.Add(Make(HostBetsyFlameAttack.Copy(p),root,contributes,false));}
            var aim=source[0].Center+source[0].velocity*1000;
            for(int outer=1;clock.Age+outer<path.Count;outer++){AttackContact contact;if(!Outer(rows,slots,targets,path,clock.Age+outer,outer,terrain,root,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
