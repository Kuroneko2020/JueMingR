using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // AI001/002 impact bodies keep their own motors. Kill adds a second
    // receiving pass, and Stynger then emits AI002 fragments before freeing
    // the parent slot. Shared owner writes survive every one of those stages.
    internal static class HostImpactBurstAttack
    {
        internal static bool Handles(int type){return type==246 || type==312;}
        internal static bool Family(int type){return Handles(type) || type==249;}
        private sealed class Member {internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Random;internal Vector2 OldVelocity;}
        private static Member Make(Projectile p,HostAttackObstacles root,bool source,bool random){return new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=source,Random=random};}
        internal static bool Step(Projectile p,PredictionTerrain terrain,out bool killed)
        {
            killed=false;if(HostRaySpellAttack.WorldRetired(p)){p.active=false;return true;}var wetVelocity=p.velocity;
            p.ai[0]++;
            if(p.type==246){if(p.ai[0]>=60){p.ai[0]=60;p.velocity.Y+=.15f;}}
            else if(p.type==312){if(p.ai[0]>=20){p.ai[0]=30;p.velocity.Y+=.5f;}}
            else if(p.type==249){if(p.ai[0]>=0)p.velocity.Y+=.25f;}
            else return false;
            float cap=p.type==249?32:16;if(p.velocity.Y>cap)p.velocity.Y=cap;
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(!p.ignoreWater)
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
                if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;p.wet=wet;if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;
            }
            if(p.shimmerWet)
            {
                int x=(int)(p.Center.X/16),y=(int)(p.position.Y/16);bool full=false;
                if(WorldGen.InWorld(x,y) && WorldGen.InWorld(x,y-1)){PredictionTile tile,above;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;if(tile.Liquid==255 && tile.ShimmerLiquid){if(!terrain.Tile(x,y-1,out above,out stop))return false;full=above.Liquid>0 && above.ShimmerLiquid;}}
                if(full){killed=true;return true;}if(p.velocity.Y>0){p.velocity.Y=-p.velocity.Y;if(p.timeLeft>600)p.timeLeft=600;p.timeLeft-=60;p.wet=p.shimmerWet=false;}
            }
            var prior=p.velocity;Vector2 position=p.position,velocity=p.velocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,nativeClip:true,shimmer:p.shimmerWet))return false;p.position=position;p.velocity=velocity;
            if(hit)
            {
                if(p.type==312){if(p.velocity.X!=prior.X){p.velocity.X=-prior.X;p.ai[1]++;}if(p.velocity.Y!=prior.Y){p.velocity.Y=-prior.Y;p.ai[1]++;}}
                else{p.position+=p.velocity;killed=true;return true;}
            }
            p.position+=p.wet?wetVelocity:p.velocity;return true;
        }
        private static bool Kill(Member m,List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,int tick,int outer,int sub,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,out AttackContact contact)
        {
            contact=null;var p=m.Shot;if(!p.active)return true;p.timeLeft=0;
            var burst=HostBetsyFlameAttack.Copy(p);int side=p.type==246?150:p.type==249?100:128;var center=p.Center;burst.width=burst.height=side;burst.Center=center;
            if(p.type!=312){burst.penetrate=-1;if(p.type==246)burst.maxPenetrate=0;}
            // Capture the changed Kill policy on another private body. Mutating
            // the old policy would invalidate its original maxPenetrate lease.
            var receive=m.Receive.ForkForSpawn(burst,true);int hits=receive.SelectedHits;bool pass=receive.PassCustomOuter(2,tick,outer,center.X,center.Y,side,side,null,unlimited:burst.penetrate<=0,consumeSelected:true);
            if(m.Source && receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,center.X,center.Y,side,side,m.Random?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}if(!pass && receive.Remaining!=0)return false;
            if(p.type==246 && p.owner==Main.myPlayer)
            {
                // Two identical (100,0) choices are legal independent random
                // draws. Future fragments remain representatives, never exact
                // RNG forecasts. Their original birth uses saved oldVelocity.
                for(int index=0;index<2;index++)
                {
                    int slot;if(!slots.TryAllocate(out slot))return false;var child=new Projectile();child.SetDefaults(249);child.active=true;child.whoAmI=slot;child.owner=p.owner;child.Center=center-m.OldVelocity;child.velocity=new Vector2(8,0);child.damage=p.damage;child.knockBack=p.knockBack;child.maxPenetrate=0;
                    bool wet;byte kind;if(!terrain.ProjectileWet(child.Center.X,child.Center.Y,child.width,child.height,out wet,out kind,true))return false;child.wet=wet;child.honeyWet=wet && kind==2;child.shimmerWet=wet && kind==3;rows.Add(Make(child,root,m.Source,true));
                }
            }
            p.active=false;slots.Release(p.whoAmI);return true;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,int tick,int outer,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var m in rows)if(m.Shot.active && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height))return false;
            int after=-1;for(int visited=0;visited<1000;visited++)
            {
                Member m=null;foreach(var q in rows)if(q.Shot.active && q.Shot.whoAmI>after && (m==null || q.Shot.whoAmI<m.Shot.whoAmI))m=q;if(m==null)return true;var p=m.Shot;after=p.whoAmI;
                for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    CombatPredictionWork.Check();m.OldVelocity=p.velocity;bool killed;if(!Step(p,terrain,out killed))return false;if(!p.active){slots.Release(p.whoAmI);break;}
                    if(!killed)
                    {
                        int hits=m.Receive.SelectedHits;bool pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true);p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,m.Random?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}if(!pass && p.penetrate!=0)return false;
                        killed=--p.timeLeft<=0 || p.penetrate==0;
                    }
                    if(killed){if(!Kill(m,rows,slots,path,tick,outer,sub,terrain,root,aim,out contact))return false;if(contact!=null)return true;}
                }
            }
            return false;
        }
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {var rows=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Family(p.type))rows.Add(p);var scene=rows.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{246,249,312});return scene;}
        private static List<Member> Copy(Projectile[] scene,Projectile[] source,HostAttackObstacles root)
        {var rows=new List<Member>();foreach(var p in scene)rows.Add(Make(HostBetsyFlameAttack.Copy(p),root,source!=null && Array.IndexOf(source,p)>=0,false));return rows;}
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(!Handles(ammo.Projectile) || age-delay<0 || age+1>=path.Count)return null;root.CaptureShotFacing(player);var scene=Capture(player,root);var pool=root.CaptureBirthSlots();AttackMotion motion;var proposals=HostAttackModels.TryRead(ammo,out motion)?HostMotionAttackInputs.Generate(origin,motion,path,age):new Vector2[0];var directions=new List<Vector2>();
            for(int input=0;input<proposals.Length+41;input++)
            {
                int seed=input-proposals.Length;var b=path[Math.Min(path.Count-1,age+1+Math.Min(Math.Max(seed,0),8)*4)].ProjectileReceiveBounds;var unit=seed<0?proposals[input]:seed<9?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();if(directions.Contains(unit))continue;directions.Add(unit);var aim=origin+unit*10000;
                root.BeginCandidate();var rows=Copy(scene,null,root);var slots=pool.Copy();bool valid=true;for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,path,age-delay+wait,wait,terrain,root,aim,out ignored)){valid=false;break;}}if(!valid)continue;
                int slot;if(!slots.TryAllocate(out slot))return null;var shot=new Projectile();shot.SetDefaults(ammo.Projectile);shot.active=true;shot.whoAmI=slot;shot.owner=player.whoAmI;shot.Center=origin;shot.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);motion=new AttackMotion(ammo.Speed,0,0,1,shot.width,shot.height,shot.timeLeft,componentBirthLimit:true);motion.Launch(ref shot.velocity.X,ref shot.velocity.Y);shot.damage=ammo.Damage;shot.knockBack=player.HeldItem.knockBack;
                bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,shot.width,shot.height,out wet,out kind,true))return null;shot.wet=wet;shot.honeyWet=wet && kind==2;shot.shimmerWet=wet && kind==3;rows.Add(Make(shot,root,true,false));
                for(int step=1;age+step<path.Count;step++){AttackContact contact;if(!Outer(rows,slots,path,age+step,delay+step,terrain,root,aim,out contact))break;if(contact!=null)return contact;}
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(source==null || source.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;var scene=Capture(player,root);root.BeginCandidate();var rows=Copy(scene,source,root);var slots=root.CaptureBirthSlots().Copy();var aim=source[0].Center+source[0].velocity*1000;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,slots,path,clock.FirstTick+step,step+1,terrain,root,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
