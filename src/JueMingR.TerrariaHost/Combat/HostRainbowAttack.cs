using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // The invisible front is a motor/emitter, not a damage body. Its stationary
    // trail is a real 251 family with shared static immunity and native slots.
    internal static class HostRainbowAttack
    {
        internal static bool Handles(int type){return type==250;}
        internal static bool Family(int type){return type==250 || type==251;}
        private sealed class Member {internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source;}
        // Native's large AI method spills both normalization factors as
        // Single. Keep those stores explicit on the x86 JIT; an extended x87
        // factor changes the direction-dependent clock on a later update.
        [MethodImpl(MethodImplOptions.NoInlining)]private static float Single(float value){return value;}
        private static Member Make(Projectile p,HostAttackObstacles root,bool source){return new Member{Shot=p,Receive=root.ForkForSpawn(p),Source=source};}
        internal static bool Ai(Projectile p,PredictionTerrain terrain,out bool child)
        {
            child=false;int x=(int)(p.Center.X/16),y=(int)(p.Center.Y/16);
            if(WorldGen.InWorld(x,y))
            {PredictionTile tile;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;if(tile.Liquid>0 && tile.ShimmerLiquid){p.active=false;p.timeLeft=0;}}
            if(p.type==250)
            {
                if(!WorldGen.InWorld(x,y,2)){p.active=false;p.timeLeft=0;return true;}
                // Native Kill does not return from this AI's shimmer branch.
                // A due trail can therefore reuse the just-released front slot.
                if(p.owner==Main.myPlayer){if(++p.localAI[0]>4){p.localAI[0]=3;child=true;}if(p.timeLeft>2400)p.timeLeft=2400;}
                float increment=1f;if(p.velocity.Y<0)increment-=p.velocity.Y/3f;p.ai[0]+=increment;
                if(p.ai[0]>30){p.velocity.Y+=.5f;p.velocity.X*=p.velocity.Y>0?.95f:1.05f;}
                // Preserve the native scalar local/store boundaries on x86:
                // combining the fields into Vector2 operations can keep a
                // different x87 intermediate across the clock threshold.
                float vx=p.velocity.X,vy=p.velocity.Y;float factor=Single((float)Math.Sqrt(vx*vx+vy*vy));
                if(factor==0)return false;factor=Single(15.95f*p.scale/factor);vx*=factor;vy*=factor;p.velocity.X=vx;p.velocity.Y=vy;
                p.rotation=(float)Math.Atan2(p.velocity.Y,p.velocity.X)-1.57f;return true;
            }
            if(p.localAI[0]==0){p.rotation=(float)Math.Atan2(p.velocity.Y,p.velocity.X)-1.57f;p.localAI[0]=1;p.timeLeft=2400;}
            p.velocity.X*=.98f;p.velocity.Y*=.98f;return true;
        }
        private static bool Move(Projectile p,PredictionTerrain terrain)
        {
            if(!p.active)return true;float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            Vector2 position=p.position,velocity=p.velocity,wetVelocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,false,false,out wetVelocity,out hit,nativeClip:true))return false;
            p.position=position;p.velocity=velocity;p.position+=velocity;if(hit)p.active=false;return true;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,PredictionTerrain terrain,HostAttackObstacles root,int tick,int outer,Vector2 aim,out AttackContact contact)
        {
            contact=null;foreach(var m in rows)if(m.Shot.active && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height))return false;
            int after=-1;for(int visited=0;visited<1000;visited++)
            {
                Member m=null;foreach(var q in rows)if(q.Shot.active && q.Shot.whoAmI>after && (m==null || q.Shot.whoAmI<m.Shot.whoAmI))m=q;if(m==null)return true;var p=m.Shot;after=p.whoAmI;
                for(int sub=0;sub<=p.extraUpdates && p.active;sub++)
                {
                    CombatPredictionWork.Check();if(HostRaySpellAttack.WorldRetired(p)){p.active=false;slots.Release(p.whoAmI);break;}
                    var center=p.Center;var velocity=p.velocity;bool spawn;if(!Ai(p,terrain,out spawn))return false;if(!p.active)slots.Release(p.whoAmI);
                    if(spawn)
                    {
                        int slot;if(!slots.TryAllocate(out slot))return false;var child=new Projectile();child.SetDefaults(251);child.active=true;child.owner=p.owner;child.whoAmI=slot;child.Center=center;child.velocity=velocity*.001f;child.damage=p.damage;child.knockBack=p.knockBack;rows.Add(Make(child,root,m.Source));
                    }
                    if(!Move(p,terrain))return false;if(!p.active){slots.Release(p.whoAmI);break;}
                    if(p.friendly && p.damage>0)
                    {
                        int hits=m.Receive.SelectedHits;bool pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true);p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,AttackConfidence.Conditional);return true;}if(!pass && p.penetrate!=0)return false;
                    }
                    if(--p.timeLeft<=0 || p.penetrate==0){p.active=false;slots.Release(p.whoAmI);}
                }
            }
            return false;
        }
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {var rows=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && (p.type==250 || p.type==251))rows.Add(p);var scene=rows.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{250,251});return scene;}
        private static List<Member> Copy(Projectile[] scene,Projectile[] source,HostAttackObstacles root)
        {var rows=new List<Member>();foreach(var p in scene)rows.Add(Make(HostBetsyFlameAttack.Copy(p),root,source!=null && Array.IndexOf(source,p)>=0));return rows;}
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            if(ammo.Projectile!=250 || age-delay<0 || age+1>=path.Count)return null;root.CaptureShotFacing(player);var scene=Capture(player,root);var pool=root.CaptureBirthSlots();var directions=new List<Vector2>();
            for(int seed=0;seed<41;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;var unit=seed<9?(new Vector2(b.CenterX,b.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();if(directions.Contains(unit))continue;directions.Add(unit);var aim=origin+unit*10000;
                root.BeginCandidate();var rows=Copy(scene,null,root);var slots=pool.Copy();bool valid=true;for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,path,terrain,root,age-delay+wait,wait,aim,out ignored)){valid=false;break;}}if(!valid)continue;
                int slot;if(!slots.TryAllocate(out slot))return null;var shot=new Projectile();shot.SetDefaults(250);shot.active=true;shot.whoAmI=slot;shot.owner=player.whoAmI;shot.Center=origin;shot.velocity=HostThornAttack.BirthVelocity(player,origin,aim,ammo.Speed);shot.damage=ammo.Damage;shot.knockBack=player.HeldItem.knockBack;rows.Add(Make(shot,root,true));
                for(int step=1;age+step<path.Count;step++){AttackContact contact;if(!Outer(rows,slots,path,terrain,root,age+step,delay+step,aim,out contact))break;if(contact!=null)return contact;}
            }
            return null;
        }
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root)
        {
            if(source==null || source.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;var scene=Capture(player,root);root.BeginCandidate();var rows=Copy(scene,source,root);var slots=root.CaptureBirthSlots().Copy();var aim=source[0].Center+source[0].velocity*1000;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,slots,path,terrain,root,clock.FirstTick+step,step+1,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
