using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // AI047's orb cannot damage a receiver. Its ninth eligible AI emits a
    // separate AI048 bolt; the first twenty chase bodies, native slot order,
    // older-orb retirement and the child's own receive policy all matter.
    internal static class HostMagnetSphereAttack
    {
        internal static bool Handles(int type){return type==254;}
        internal static bool Family(int type){return type==254 || type==255;}
        private sealed class Member
        {internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Representative;}

        internal static bool StepParent(Projectile p,PredictionTerrain terrain)
        {
            if(!p.active)return true;
            if(HostRaySpellAttack.WorldRetired(p)){p.active=false;return true;}
            p.oldVelocity=p.velocity;
            if(p.ai[0]==0){p.ai[0]=p.velocity.X;p.ai[1]=p.velocity.Y;}
            p.rotation+=(p.velocity.X>0?1:-1)*(Math.Abs(p.velocity.X)+Math.Abs(p.velocity.Y))*.001f;
            if(++p.frameCounter>6){p.frameCounter=0;if(++p.frame>4)p.frame=0;}
            if(p.velocity.Length()>2)p.velocity*=.98f;
            // Emission and older-orb retirement occur after this motor and
            // BEFORE movement. Outer supplies them before calling MoveParent.
            return true;
        }
        private static bool Wet(Projectile p,PredictionTerrain terrain)
        {
            if(p.ignoreWater)return true;
            bool wet,lava;byte kind;
            if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
            if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;
            if(p.wet && !wet && p.wetCount==0)p.wetCount=10;p.wet=wet;
            if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;if(p.wetCount>0)p.wetCount--;
            return true;
        }
        internal static bool MoveParent(Projectile p,PredictionTerrain terrain)
        {
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active)p.active=false;
            if(!Wet(p,terrain))return false;
            if(p.shimmerWet)
            {
                PredictionTile tile,above;PredictionStop stop;int x=(int)(p.Center.X/16),y=(int)(p.position.Y/16);
                if(!terrain.Tile(x,y,out tile,out stop) || !terrain.Tile(x,y-1,out above,out stop))return false;
                if(tile.Liquid==255 && tile.ShimmerLiquid && above.Liquid>0 && above.ShimmerLiquid)p.active=false;
                else if(p.velocity.Y>0){p.velocity.Y=-p.velocity.Y;p.shimmerWet=p.wet=false;}
            }
            p.oldPosition=p.position;p.oldDirection=p.direction;
            var position=p.position;var velocity=p.velocity;var prior=p.velocity;Vector2 wetVelocity=p.oldVelocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;
            p.position=position;p.velocity=velocity;
            if(hit)
            {
                if(p.velocity.X!=prior.X)p.velocity.X=-prior.X;
                if(p.velocity.Y!=prior.Y)p.velocity.Y=-prior.Y;
            }
            p.position+=p.wet?wetVelocity:p.velocity;p.direction=p.velocity.X<0?-1:1;return true;
        }
        private static void Finish(Projectile p)
        {if(p.active && (--p.timeLeft<=0 || p.penetrate==0))p.active=false;}
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {
            var list=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Family(p.type))list.Add(p);
            var scene=list.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{254,255});return scene;
        }
        private static SortedList<int,Member> Prepare(Projectile[] scene,Projectile[] sources,HostAttackObstacles root)
        {
            var rows=new SortedList<int,Member>();
            foreach(var p in scene)
            {
                bool own=false;if(sources!=null)foreach(var s in sources)if(ReferenceEquals(p,s)){own=true;break;}
                var copy=HostRaySpellAttack.Copy(p);rows.Add(copy.whoAmI,new Member{Shot=copy,Receive=root.ForkForSpawn(copy),Source=own});
            }
            return rows;
        }
        private static int Next(SortedList<int,Member> rows,int after)
        {int low=0,high=rows.Count;while(low<high){int mid=(low+high)/2;if(rows.Keys[mid]<=after)low=mid+1;else high=mid;}return low;}
        private static bool Emit(Member parent,SortedList<int,Member> rows,HostProjectileBirthSlots slots,HostProjectileTargets targets,NpcTrajectory path,PredictionTerrain terrain,int tick,HostAttackObstacles root)
        {
            var p=parent.Shot;
            foreach(var other in rows.Values)
                if(other.Shot!=p && other.Shot.active && other.Shot.type==254 && p.timeLeft>other.Shot.timeLeft && other.Shot.timeLeft>30)other.Shot.timeLeft=30;
            if(p.owner!=Main.myPlayer || p.timeLeft<30)return true;
            int found=0,chosen=-1;Vector2 point=Vector2.Zero;
            // A legal random representative may choose the selected body only
            // when it is in the native first-twenty list. Other targets remain
            // captured dependencies even when they cannot receive damage.
            for(int i=0;i<targets.Count;i++)
            {
                if(targets.Slot(i)==path.Identity.Slot && !path[tick].CanReceive)continue;
                var box=targets.Bounds(i,path,tick);var center=new Vector2(box.X+(int)box.Width/2,box.Y+(int)box.Height/2);
                if(Math.Abs(p.Center.X-center.X)+Math.Abs(p.Center.Y-center.Y)>=300)continue;
                bool clear;PredictionStop stop;if(!terrain.CanHit(new MotionRect(p.Center.X,p.Center.Y,1,1),new MotionRect(center.X,center.Y,1,1),out clear,out stop))return false;if(!clear)continue;
                if(found<20)
                {
                    if(chosen<0 || targets.Slot(i)==path.Identity.Slot){chosen=i;point=center;}
                    found++;
                }
            }
            if(found==0)return true;
            if(++p.localAI[0]<=8)return true;p.localAI[0]=0;
            int slot;if(!slots.TryAllocate(out slot))return false;
            var child=new Projectile();child.SetDefaults(255);child.active=true;child.owner=p.owner;child.whoAmI=slot;
            child.Center=p.Center+p.velocity*4;var delta=point-child.Center;float length=(float)Math.Sqrt(delta.X*delta.X+delta.Y*delta.Y);
            if(length==0)return false;child.velocity=delta*(6/length);child.damage=p.damage;child.knockBack=p.knockBack;
            if(!Wet(child,terrain))return false;
            rows[slot]=new Member{Shot=child,Receive=root.ForkForSpawn(child),Source=parent.Source,Representative=parent.Representative || found>1};return true;
        }
        private static bool Outer(SortedList<int,Member> rows,HostProjectileBirthSlots slots,HostProjectileTargets targets,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,Vector2 aim,HostAttackObstacles root,out AttackContact contact)
        {
            contact=null;int after=-1;
            for(int visit=0;visit<1000;visit++)
            {
                int index=Next(rows,after);if(index==rows.Count)return true;var member=rows.Values[index];var p=member.Shot;after=p.whoAmI;
                if(!p.active){rows.Remove(after);slots.Release(after);continue;}
                if(!member.Receive.BeforeNpc(tick,outer,p.Center.X,p.Center.Y,p.width,p.height))return false;
                for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    if(p.type==254)
                    {
                        if(!StepParent(p,terrain))return false;
                        if(!p.active)break;
                        if(p.active && !Emit(member,rows,slots,targets,path,terrain,tick,root))return false;
                        if(!MoveParent(p,terrain))return false;
                    }
                    else if(!HostRaySpellAttack.Step(p,terrain))return false;
                    if(p.active && p.friendly && p.damage>0)
                    {
                        int before=member.Receive.SelectedHits;
                        bool passed=member.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true);p.penetrate=member.Receive.Remaining;
                        if(member.Source && member.Receive.SelectedHits>before)
                        {contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,member.Representative?AttackConfidence.Representative:AttackConfidence.Conditional);return true;}
                        if(!passed && p.penetrate!=0)return false;
                    }
                    Finish(p);
                }
                if(!p.active){rows.Remove(after);slots.Release(after);}
            }
            return Next(rows,after)==rows.Count;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;
            foreach(var p in sources)if(p==null || !p.active || !Family(p.type) || p.owner!=player.whoAmI)return null;
            var scene=Capture(player,root);var pool=root.CaptureBirthSlots();
            var policy=new Projectile();policy.SetDefaults(254);policy.owner=player.whoAmI;var targets=root.CaptureChaseTargets(policy);
            var aim=sources[0].Center+sources[0].velocity*1000;
            root.BeginCandidate();var rows=Prepare(scene,sources,root);var slots=pool.Copy();
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++)
            {AttackContact result;if(!Outer(rows,slots,targets,path,terrain,clock.FirstTick+step,step+1,aim,root,out result))break;if(result!=null)return result;}
            return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(!Handles(ammo.Projectile) || ammo.Damage<=0 || delay<0 || age-delay<0 || age+1>=path.Count)return null;
            root.CaptureShotFacing(player);var scene=Capture(player,root);var pool=root.CaptureBirthSlots();
            var policy=new Projectile();policy.SetDefaults(254);policy.owner=player.whoAmI;var targets=root.CaptureChaseTargets(policy);var directions=new List<Vector2>();
            int[] times={1,8,16,24,32,48,64,96,120};
            for(int seed=0;seed<41;seed++)
            {
                var box=path[Math.Min(path.Count-1,age+times[Math.Min(seed,8)])].ProjectileReceiveBounds;
                var direction=seed<9?(new Vector2(box.CenterX,box.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();
                if(directions.Contains(direction))continue;directions.Add(direction);var aim=origin+direction*10000;
                root.BeginCandidate();var rows=Prepare(scene,null,root);var slots=pool.Copy();bool valid=true;
                for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,targets,path,terrain,age-delay+wait,wait,aim,root,out ignored)){valid=false;break;}}
                if(!valid)continue;int slot;if(!slots.TryAllocate(out slot))return null;
                var p=new Projectile();p.SetDefaults(254);p.active=true;p.owner=player.whoAmI;p.whoAmI=slot;p.Center=origin;p.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);p.damage=ammo.Damage;p.knockBack=player.GetWeaponKnockback(player.HeldItem,player.HeldItem.knockBack);
                if(!Wet(p,terrain))return null;rows[slot]=new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=true};
                for(int step=1;step<=Math.Min(120,path.Count-age-1);step++){AttackContact result;if(!Outer(rows,slots,targets,path,terrain,age+step,delay+step,aim,root,out result))break;if(result!=null)return result;}
            }
            return null;
        }
    }
}
