using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Inferno1445 hand295 dies into one real296 while its native slot
    // remains occupied. The child is a136-update area, not a single
    // instant explosion and not the adjacent AI051 staff projectile.
    internal static class HostInfernoAttack
    {
        internal static bool Handles(int type){return type==295 || type==296;}
        internal static Projectile Copy(Projectile p)
        {var q=HostNebulaBlazeAttack.Copy(p);q.oldPosition=p.oldPosition;q.oldVelocity=p.oldVelocity;q.numHits=p.numHits;return q;}
        internal static Vector2 BirthVelocity(Vector2 origin,Vector2 cursor,float speed,int facing)
        {
            float x=cursor.X-origin.X,y=cursor.Y-origin.Y;
            if(x==0 && y==0)x=facing;
            float scale=speed/(float)Math.Sqrt(x*x+y*y);
            // NewProjectile's .97 component clamp belongs only to aiStyle1.
            return new Vector2(x*scale,y*scale);
        }
        internal static Rectangle DamageBox(Projectile p)
        {return p.Hitbox;}
        internal static bool WorldRetired(Projectile p)
        {return p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y);}
        private static void Kill(Projectile p,Action onKill)
        {if(!p.active)return;p.timeLeft=0;if(onKill!=null)onKill();p.active=false;}
        internal static bool Step(Projectile p,PredictionTerrain terrain){return Motor(p,terrain,null);}
        private static bool Motor(Projectile p,PredictionTerrain terrain,Action onKill)
        {
            if(WorldRetired(p)){p.active=false;return true;}
            Vector2 wetVelocity=p.velocity;p.oldVelocity=p.velocity;
            if(p.active && p.type==296)
            {
                if(p.localAI[0]==0)p.localAI[0]=1;p.ai[0]+=4;
                // First value greater than540 is544: the136th native AI
                // kills before movement's active gate and before Damage.
                if(p.ai[0]>540)Kill(p,onKill);
            }
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active)Kill(p,onKill);
            if(!p.ignoreWater)
            {
                bool wet,lava;byte kind;
                if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
                if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;
                if(p.wet && !wet && p.wetCount==0)p.wetCount=10;p.wet=wet;
                if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;if(p.wetCount>0)p.wetCount--;
            }
            // Neither AI050 role appears in Shimmer's reversal/Kill list.
            p.oldPosition=p.position;var position=p.position;var velocity=p.velocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;
            p.position=position;p.velocity=velocity;
            if(hit){p.position+=velocity;Kill(p,onKill);}p.position+=p.wet?wetVelocity:p.velocity;p.direction=p.velocity.X<0?-1:1;return true;
        }
        internal static void Finish(Projectile p){Tail(p,null);}
        private static void Tail(Projectile p,Action onKill)
        {if(p.active && (--p.timeLeft<=0 || p.penetrate==0))Kill(p,onKill);}
        internal static Projectile ChildBirth(Projectile parent,int slot)
        {
            var q=new Projectile();q.SetDefaults(296);q.active=true;q.owner=parent.owner;q.whoAmI=slot;q.Center=parent.Center;q.velocity=Vector2.Zero;
            q.damage=(int)((double)parent.damage*.75);q.knockBack=parent.knockBack;return q;
        }
        private sealed class Member
        {
            internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Available;
            internal List<Member> Rows;internal HostProjectileBirthSlots Slots;internal PredictionTerrain Terrain;
            internal readonly Action OnKill;
            internal Member(){OnKill=Killed;}
            private void Killed()
            {
                if(Shot.type!=295 || Shot.owner!=Main.myPlayer)return;
                // Kill owns timeLeft0 already, but active/slot occupancy are
                // still true until NewProjectile completes. Do not Release
                // the parent early or overwrite the source's private body.
                int slot;if(!Slots.TryAllocate(out slot)){Available=false;return;}
                var q=ChildBirth(Shot,slot);
                bool wet;byte kind;if(!Terrain.ProjectileWet(q.Center.X,q.Center.Y,q.width,q.height,out wet,out kind,true)){Available=false;return;}
                q.wet=wet && !q.ignoreWater;q.honeyWet=wet && kind==2;q.shimmerWet=wet && kind==3;
                Rows.Add(new Member{Shot=q,Receive=Receive.ForkForSpawn(q),Source=Source});
            }
        }
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {
            var rows=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Handles(p.type))rows.Add(p);
            var scene=rows.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{295,296});return scene;
        }
        private static List<Member> Prepare(Projectile[] scene,Projectile[] source,HostAttackObstacles root)
        {
            var rows=new List<Member>();foreach(var p in scene){bool own=false;if(source!=null)foreach(var q in source)if(ReferenceEquals(p,q)){own=true;break;}var copy=Copy(p);rows.Add(new Member{Shot=copy,Receive=root.ForkForSpawn(copy),Source=own});}return rows;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,PredictionTerrain terrain,int tick,int outer,Vector2 aim,out AttackContact contact)
        {
            contact=null;int after=-1;
            // Birth can append a lower or higher slot while the parent is
            // being processed. Visit only higher slots this world; lower
            // births start on the next complete projectile-array pass.
            for(int visit=0;visit<1000;visit++)
            {
                Member m=null;foreach(var candidate in rows)if(candidate.Shot.active && candidate.Shot.whoAmI>after && (m==null || candidate.Shot.whoAmI<m.Shot.whoAmI))m=candidate;
                if(m==null)return true;var p=m.Shot;after=p.whoAmI;m.Rows=rows;m.Slots=slots;m.Terrain=terrain;m.Available=true;
                for(int sub=0;sub<=p.extraUpdates;sub++)
                {
                    if(!Motor(p,terrain,m.OnKill) || !m.Available)return false;
                    if(p.active && p.friendly && !p.hostile && p.damage>0)
                    {
                        var box=DamageBox(p);int hits=m.Receive.SelectedHits;
                        bool passed=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,
                            b=>box.Intersects(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height)),consumeSelected:true,onHit:(slot,pen)=>{p.numHits++;return true;});p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>hits)
                        {contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,box.Center.X,box.Center.Y,box.Width,box.Height,AttackConfidence.Conditional);return true;}
                        // AI050 cannot be reflected. A remaining-budget
                        // refusal here is an unknown receive dependency,
                        // not a fictitious harmless retirement/slot release.
                        if(!passed && p.penetrate!=0)return false;
                    }
                    if(p.active)Tail(p,m.OnKill);if(!m.Available)return false;
                    if(!p.active){slots.Release(p.whoAmI);break;}
                }
            }
            return false;
        }
        private static Vector2 Cursor(Player player,Vector2 aim)
        {var pixel=aim-Main.screenPosition;if(player.gravDir==-1)pixel.Y=Main.screenHeight-pixel.Y;return new Vector2((int)pixel.X+Main.screenPosition.X,player.gravDir==-1?Main.screenPosition.Y+Main.screenHeight-(int)pixel.Y:(int)pixel.Y+Main.screenPosition.Y);}
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles obstacles,int delay)
        {
            if(ammo.Projectile!=295 || ammo.WeaponType!=1445 || ammo.Damage<=0 || delay<0 || age-delay<0 || age+1>=path.Count)return null;
            // The root already owns future hand pose preparation. AI050's
            // source/child never reads future owner motion after birth.
            obstacles.CaptureShotFacing(player);var scene=Capture(player,obstacles);var pool=obstacles.CaptureBirthSlots();var inputs=new List<Vector2>();
            for(int seed=0;seed<41;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+Math.Min(seed,8)*4)].ProjectileReceiveBounds;
                var aim=seed<9?new Vector2(b.CenterX,b.CenterY):origin+((seed-9)*MathHelper.TwoPi/32).ToRotationVector2()*10000;var cursor=Cursor(player,aim);if(inputs.Contains(cursor))continue;inputs.Add(cursor);
                obstacles.BeginCandidate();var rows=Prepare(scene,null,obstacles);var slots=pool.Copy();bool valid=true;
                for(int wait=1;wait<=delay;wait++){AttackContact ignored;if(!Outer(rows,slots,path,terrain,age-delay+wait,wait,cursor,out ignored)){valid=false;break;}}if(!valid)continue;
                int slot;if(!slots.TryAllocate(out slot))return null;var p=new Projectile();p.SetDefaults(295);p.active=true;p.owner=player.whoAmI;p.whoAmI=slot;p.Center=origin;p.velocity=BirthVelocity(origin,cursor,ammo.Speed,player.direction);p.damage=ammo.Damage;p.knockBack=player.GetWeaponKnockback(player.HeldItem,player.HeldItem.knockBack);
                bool wet;byte kind;if(!terrain.ProjectileWet(origin.X,origin.Y,p.width,p.height,out wet,out kind,true))return null;p.wet=wet && !p.ignoreWater;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;
                rows.Add(new Member{Shot=p,Receive=obstacles.ForkForSpawn(p),Source=true});
                for(int step=1;step<=Math.Min(120,path.Count-age-1);step++){AttackContact contact;if(!Outer(rows,slots,path,terrain,age+step,delay+step,cursor,out contact))break;if(contact!=null)return contact;}
            }
            return null;
        }
        // Whole-array family observation/oracle only; a partially processed
        // Projectiles array needs a slot epoch rather than restarting slot0.
        internal static AttackContact KnownFamily(Player player,Projectile[] source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(source==null || source.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;
            foreach(var p in source)if(p==null || !p.active || !Handles(p.type) || p.owner!=player.whoAmI)return null;
            var scene=Capture(player,obstacles);obstacles.BeginCandidate();var rows=Prepare(scene,source,obstacles);var slots=obstacles.CaptureBirthSlots().Copy();var aim=source[0].Center+source[0].velocity*1000;
            for(int step=0;step<Math.Min(120,path.Count-clock.FirstTick);step++){AttackContact contact;if(!Outer(rows,slots,path,terrain,clock.FirstTick+step,step+1,aim,out contact))return null;if(contact!=null)return contact;}return null;
        }
    }
}
