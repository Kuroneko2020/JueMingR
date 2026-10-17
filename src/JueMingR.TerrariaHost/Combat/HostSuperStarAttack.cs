using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    // One native728 OnHit owns one finite729 descent. In particular729's
    // SetDefaults consumes RNG for scale: private copies/births must clone
    // initialized values instead, with an explicit legal discrete scale.
    internal static class HostSuperStarAttack
    {
        internal static bool Handles(int type){return type==728;}
        internal static bool Family(int type){return type==728 || type==729;}
        private static readonly MethodInfo clone=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        internal static Projectile Copy(Projectile p)
        {var q=(Projectile)clone.Invoke(p,null);q.ai=(float[])p.ai.Clone();q.localAI=(float[])p.localAI.Clone();q.localNPCImmunity=(int[])p.localNPCImmunity.Clone();q.oldPos=(Vector2[])p.oldPos.Clone();q.oldRot=(float[])p.oldRot.Clone();q.oldSpriteDirection=(int[])p.oldSpriteDirection.Clone();return q;}
        private sealed class Member{internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Representative;}
        private static Member Make(Projectile p,HostAttackObstacles root,bool source,bool random=false)
        {return new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=source,Representative=random};}
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {var list=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Family(p.type))list.Add(p);var scene=list.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{728,729});return scene;}
        private static List<Member> CopyScene(Projectile[] scene,Projectile[] sources,HostAttackObstacles root)
        {var rows=new List<Member>();foreach(var p in scene)rows.Add(Make(Copy(p),root,sources!=null && Array.IndexOf(sources,p)>=0));return rows;}
        internal static void Ai(Projectile p)
        {p.alpha=Math.Max(100,p.alpha-10);if(p.type==728)p.rotation+=(Math.Abs(p.velocity.X)+Math.Abs(p.velocity.Y))*.005f*p.direction;else{if(p.ai[0]!=0)p.velocity=p.velocity.RotatedBy(p.ai[0]/(10*(p.extraUpdates+1)));p.rotation=p.velocity.ToRotation()+MathHelper.PiOver2;p.tileCollide=false;}}
        internal static Projectile Birth(Projectile parent,Vector2 victim)
        {var p=Copy(ContentSamples.ProjectilesByType[729]);p.active=true;p.owner=parent.owner;p.velocity=Vector2.UnitY*6;p.Center=victim-p.velocity*20;p.damage=(int)(parent.damage*.75);p.knockBack=0;p.scale=1.14f;p.ai[0]=0;p.ai[1]=victim.Y;return p;}
        internal static bool Origin(Player player,Vector2 origin,Vector2 velocity,PredictionTerrain terrain,out Vector2 birth)
        {birth=origin;bool clear;PredictionStop stop;if(!terrain.CanHitLine(new MotionRect(player.Center.X,player.Center.Y,0,0),new MotionRect(origin.X+velocity.X*2,origin.Y+velocity.Y*2,0,0),out clear,out stop))return false;if(!clear)birth=player.Center-velocity*.25f;return true;}
        private static void Kill(Projectile p){p.active=false;p.timeLeft=0;}
        internal static bool Step(Projectile p,PredictionTerrain terrain)
        {
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld){p.active=false;return true;}
            Ai(p);float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;if(p.owner<255 && !Main.player[p.owner].active)Kill(p);
            if(!p.ignoreWater)
            {bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;p.wet=wet;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;if(lava)p.lavaWet=true;if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;}
            if(p.shimmerWet)
            {
                int x=(int)p.Center.X/16,y=(int)p.position.Y/16;bool full=false;if(WorldGen.InWorld(x,y) && WorldGen.InWorld(x,y-1)){PredictionTile tile,above;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;if(tile.Liquid==255 && tile.ShimmerLiquid){if(!terrain.Tile(x,y-1,out above,out stop))return false;full=above.Liquid>0 && above.ShimmerLiquid;}}
                if(full)Kill(p);else if(p.velocity.Y>0){p.velocity.Y=-p.velocity.Y;p.shimmerWet=p.wet=false;}
            }
            var position=p.position;var velocity=p.velocity;Vector2 wetVelocity=velocity;bool hit=false;if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet && !p.ignoreWater,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;p.position=position;p.velocity=velocity;
            if(hit){p.position+=velocity;Kill(p);}p.position+=p.wet && !p.ignoreWater?wetVelocity:velocity;if(!p.manualDirectionChange)p.direction=p.velocity.X<0?-1:1;return true;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,int tick,int outer,PredictionTerrain terrain,HostAttackObstacles root,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var row in rows)if(row.Shot.active && !row.Receive.BeforeNpc(tick,outer,row.Shot.Center.X,row.Shot.Center.Y,row.Shot.width,row.Shot.height))return false;int after=-1;
            for(int visit=0;visit<1000;visit++)
            {
                Member m=null;foreach(var row in rows)if(row.Shot.active && row.Shot.whoAmI>after && (m==null || row.Shot.whoAmI<m.Shot.whoAmI))m=row;if(m==null)return true;var p=m.Shot;after=p.whoAmI;
                for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    CombatPredictionWork.Check();if(!Step(p,terrain))return false;if(!p.active){slots.Release(p.whoAmI);break;}int hits=m.Receive.SelectedHits;bool failed=false;
                    bool passed=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true,onHit:(slot,remaining)=>
                    {
                        if(p.type!=728)return true;int born;if(!slots.TryAllocate(out born)){failed=true;return false;}var b=slot==path.Identity.Slot?path[tick].Bounds:new MotionRect(Main.npc[slot].position.X,Main.npc[slot].position.Y,Main.npc[slot].width,Main.npc[slot].height);var child=Birth(p,new Vector2(b.CenterX,b.CenterY));child.whoAmI=born;bool wet;byte kind;if(!terrain.ProjectileWet(child.Center.X,child.Center.Y,child.width,child.height,out wet,out kind,true)){failed=true;return false;}child.wet=wet && !child.ignoreWater;child.honeyWet=wet && kind==2;child.shimmerWet=wet && kind==3;rows.Add(Make(child,root,m.Source,true));return true;
                    });p.penetrate=m.Receive.Remaining;
                    if(m.Source && m.Receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,m.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}
                    if(failed || !passed && p.penetrate!=0)return false;if(!passed || --p.timeLeft<=0){Kill(p);slots.Release(p.whoAmI);}
                }
            }
            return true;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.Phase==HostAttackPhase.Projectiles || clock.LaunchDelay!=0 || clock.Age<0)return null;var scene=Capture(player,root);root.BeginCandidate();var rows=CopyScene(scene,sources,root);var slots=root.CaptureBirthSlots().Copy();var aim=sources[0].Center+sources[0].velocity*1000;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,slots,path,clock.FirstTick+step,step+1,terrain,root,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(ammo.Damage<=0 || age-delay<0 || age+1>=path.Count)return null;var prototype=ContentSamples.ProjectilesByType[728];var motion=new AttackMotion(ammo.Speed,0,0,prototype.extraUpdates+1,24,24,prototype.timeLeft,AttackConfidence.Conditional);var proposals=HostMotionAttackInputs.Generate(origin,motion,path,age);var inputs=new List<Vector2>();var scene=Capture(player,root);var pool=root.CaptureBirthSlots();root.CaptureShotFacing(player);
            for(int input=0;input<proposals.Length+41;input++)
            {
                int seed=input-proposals.Length;var b=path[Math.Min(path.Count-1,age+1+Math.Max(0,Math.Min(seed,8))*4)].ProjectileReceiveBounds;var unit=seed<0?proposals[input]:seed<9?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();if(inputs.Contains(unit))continue;inputs.Add(unit);var aim=origin+unit*10000;var velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);Vector2 born;if(!Origin(player,origin,velocity,terrain,out born))return null;root.BeginCandidate();var rows=CopyScene(scene,null,root);var slots=pool.Copy();bool valid=true;
                for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,path,age-delay+wait,wait,terrain,root,aim,out ignored)){valid=false;break;}}if(!valid)continue;int slot;if(!slots.TryAllocate(out slot))return null;var p=Copy(prototype);p.active=true;p.whoAmI=slot;p.owner=player.whoAmI;p.Center=born;p.velocity=velocity;p.damage=ammo.Damage;p.knockBack=player.HeldItem.knockBack;rows.Add(Make(p,root,true));
                for(int step=1;step<=Math.Min(120,path.Count-age-1);step++){AttackContact contact;if(!Outer(rows,slots,path,age+step,delay+step,terrain,root,aim,out contact))break;if(contact!=null)return contact;}
            }
            return null;
        }
    }
}
