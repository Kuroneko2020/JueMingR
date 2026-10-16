using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // LifeDrain and Princess share the native finite placement ray, followed
    // by stationary finite Damage bodies. Placement is not itself a Contact.
    internal static class HostPlacedPulseAttack
    {
        internal static bool Handles(int type){return type==476 || type==950;}
        internal static bool Placement(int item){return item==3006 || item==5065;}
        internal static bool Birth(Player player,Vector2 origin,Vector2 aim,float speed,PredictionTerrain terrain,out Vector2 position)
        {
            position=origin;if(!(speed>0))return false;var screen=aim-Main.screenPosition;if(player.gravDir==-1)screen.Y=Main.screenHeight-screen.Y;
            var point=new Vector2((int)screen.X+Main.screenPosition.X,player.gravDir==-1?Main.screenPosition.Y+Main.screenHeight-(int)screen.Y:(int)screen.Y+Main.screenPosition.Y);
            player.LimitPointToPlayerReachableArea(ref point);var velocity=HostThornAttack.BirthVelocity(player,origin,aim,speed);float distance=Vector2.Distance(point,origin),length=velocity.Length();
            var owner=player.Center;owner.X+=(aim.X<origin.X?-1:aim.X>origin.X?1:player.direction)*16;
            int steps=0;
            while(true)
            {
                CombatPredictionWork.Check();bool clear;PredictionStop stop;if(!terrain.CanHitLine(new MotionRect(owner.X,owner.Y,0,0),new MotionRect(position.X,position.Y,0,0),out clear,out stop))return false;if(!clear)return true;
                steps++;position+=velocity;
                if(Vector2.Distance(position,point)<20+Math.Abs(velocity.X)+Math.Abs(velocity.Y) || length*steps>=distance){position=point;return true;}
            }
        }
        internal static void Ai(Projectile p)
        {
            if(p.type!=950)return;
            p.ai[0]++;if(p.ai[0]>=60){p.active=false;return;}
            float t=p.ai[0]/60,quadratic=1-(1-t)*(1-t),quartic=1-(1-quadratic)*(1-quadratic),fade=Utils.Remap(p.ai[0],45,60,0,1);fade=1-fade*fade;
            p.scale=(.4f+.6f*quartic)*fade;
            float later=Utils.Remap(p.ai[0],20,60,0,1),later2=1-(1-later)*(1-later),later4=1-(1-later2)*(1-later2);p.localAI[0]=(.4f+.6f*later4)*fade;
        }
        private sealed class Member
        {
            internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source;
            internal bool Hit(int slot,int remaining){if(Shot.type==950)Shot.damage=(int)((double)Shot.damage*.98);return Shot.damage>0;}
        }
        private static Member Make(Projectile p,HostAttackObstacles root,bool source){return new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=source};}
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,Vector2 aim,out AttackContact contact)
        {
            contact=null;
            foreach(var m in rows)
            {
                var p=m.Shot;if(!p.active)continue;
                if(!m.Receive.BeforeNpc(tick,outer,p.Center.X,p.Center.Y,p.width,p.height))return false;
                if(HostRaySpellAttack.WorldRetired(p)){p.active=false;slots.Release(p.whoAmI);continue;}
                Ai(p);if(!p.active){slots.Release(p.whoAmI);continue;}
                float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;p.position+=p.velocity;
                if(p.damage>0)
                {
                    int hits=m.Receive.SelectedHits;bool passed=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true,onHit:m.Hit);p.penetrate=m.Receive.Remaining;
                    if(m.Source && m.Receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,0,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,AttackConfidence.Conditional);return true;}
                    if(!passed && p.penetrate!=0 && p.damage>0)return false;
                }
                if(--p.timeLeft<=0 || p.penetrate==0){p.active=false;slots.Release(p.whoAmI);}
            }
            return true;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(sources==null || sources.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;
            int type=sources[0].type;foreach(var p in sources)if(p==null || !p.active || p.type!=type || !Handles(p.type) || p.owner!=player.whoAmI)return null;
            var live=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && p.type==type)live.Add(p);
            var scene=live.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{type});var pool=root.CaptureBirthSlots();root.BeginCandidate();
            var rows=new List<Member>();foreach(var p in scene){bool source=false;foreach(var q in sources)if(ReferenceEquals(p,q)){source=true;break;}rows.Add(Make(HostBetsyFlameAttack.Copy(p),root,source));}
            rows.Sort((a,b)=>a.Shot.whoAmI.CompareTo(b.Shot.whoAmI));var slots=pool.Copy();var aim=sources[0].Center;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,slots,path,terrain,clock.FirstTick+step,step+1,aim,out contact))break;if(contact!=null)return contact;}
            return null;
        }
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(!Handles(ammo.Projectile) || age-delay<0 || age+1>=path.Count)return null;root.CaptureShotFacing(player);var live=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.type==ammo.Projectile && p.owner==player.whoAmI)live.Add(p);
            var scene=live.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{ammo.Projectile});var pool=root.CaptureBirthSlots();var inputs=new List<Vector2>();
            for(int seed=0;seed<17;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+seed*4)].ProjectileReceiveBounds;var aim=new Vector2(b.CenterX,b.CenterY);if(inputs.Contains(aim))continue;inputs.Add(aim);
                Vector2 position;if(!Birth(player,origin,aim,ammo.Speed,terrain,out position))continue;root.BeginCandidate();var rows=new List<Member>();foreach(var p in scene)rows.Add(Make(HostBetsyFlameAttack.Copy(p),root,false));var slots=pool.Copy();bool valid=true;
                for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,path,terrain,age-delay+wait,wait,aim,out ignored)){valid=false;break;}}if(!valid)continue;
                int slot;if(!slots.TryAllocate(out slot))return null;var shot=new Projectile();shot.SetDefaults(ammo.Projectile);shot.active=true;shot.owner=player.whoAmI;shot.whoAmI=slot;shot.Center=position;shot.velocity=Vector2.Zero;shot.damage=ammo.Damage;rows.Add(Make(shot,root,true));rows.Sort((a,c)=>a.Shot.whoAmI.CompareTo(c.Shot.whoAmI));
                for(int step=1;step<=Math.Min(120,path.Count-age-1);step++){AttackContact contact;if(!Outer(rows,slots,path,terrain,age+step,delay+step,aim,out contact))break;if(contact!=null)return contact;}
            }
            return null;
        }
    }
}
