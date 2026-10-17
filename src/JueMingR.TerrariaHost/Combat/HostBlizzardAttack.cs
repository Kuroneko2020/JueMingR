using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Item1931 emits two simultaneous members at each genuine Shoot. Its
    // animation10/time5 performs two separate hand actions, not four at once.
    // Birth spread is Representative; already born bolts have no steering.
    internal static class HostBlizzardAttack
    {
        private static readonly MethodInfo clone=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        internal static bool Handles(int type){return type==337;}
        internal static Projectile Copy(Projectile p){return HostNebulaBlazeAttack.Copy(p);}
        internal static void Birth(Player owner,Vector2 cursor,float speed,int member,int offset,int jitter,Vector2 spread,out Vector2 center,out Vector2 velocity)
        {
            // Preserve Player48092's stores and operation order. Algebraically
            // cancelling position.X changes Single rounding on x86.
            center=new Vector2(owner.position.X+owner.width*.5f+offset*-owner.direction+(cursor.X-owner.position.X),owner.MountedCenter.Y-600);
            center.X=(center.X+owner.Center.X)/2+jitter;center.Y-=100*member;
            velocity=cursor-center;if(velocity.Y<0)velocity.Y*=-1;if(velocity.Y<20)velocity.Y=20;
            float factor=speed/(float)Math.Sqrt(velocity.X*velocity.X+velocity.Y*velocity.Y);velocity.X*=factor;velocity.Y*=factor;velocity+=spread;
            while(velocity.X>=16 || velocity.X<=-16 || velocity.Y>=16 || velocity.Y< -16)velocity*=.97f;
        }
        private static void Kill(Projectile p){if(p.active){p.timeLeft=0;p.active=false;}}
        internal static bool Step(Projectile p,float ownerY,double surface,PredictionTerrain terrain)
        {return !p.active || Motor(p,ownerY,surface,terrain);}
        internal static bool RetiredStep(Projectile p,float ownerY,double surface,PredictionTerrain terrain)
        {return Motor(p,ownerY,surface,terrain);}
        private static bool Motor(Projectile p,float ownerY,double surface,PredictionTerrain terrain)
        {
            if(p.position.X<=Main.leftWorld || p.position.X+p.width>=Main.rightWorld || p.position.Y<=Main.topWorld || p.position.Y+p.height>=Main.bottomWorld || float.IsNaN(p.position.X) || float.IsNaN(p.position.Y)){p.active=false;return true;}
            var wetVelocity=p.velocity;p.oldVelocity=p.velocity;
            if(p.active)
            {
                p.ai[0]++;
                // This dedicated AI001 branch skips the ordinary .1 gravity,
                // but still reaches the common downward16 clamp afterwards.
                if(p.position.Y>ownerY-300 || p.position.Y<surface*16)p.tileCollide=true;
                p.frame=(int)p.ai[1];p.rotation=p.velocity.ToRotation()+1.57f;if(p.velocity.Y>16)p.velocity.Y=16;
            }
            float wind;if(!HostProjectileEnvironment.Impulse(p,terrain,p.Center.X,p.Center.Y,p.velocity.X,out wind))return false;p.velocity.X+=wind;
            if(p.owner<255 && !Main.player[p.owner].active)Kill(p);
            if(!p.ignoreWater)
            {
                bool wet,lava;byte kind;if(!terrain.ProjectileWet(p.Center.X,p.Center.Y,p.width,p.height,out wet,out kind,true) || !terrain.ProjectileLava(p.Center.X,p.Center.Y,p.width,p.height,out lava,true))return false;
                if(lava)p.lavaWet=true;if(wet && kind==2)p.honeyWet=true;if(wet && kind==3)p.shimmerWet=true;if(p.wet && !wet && p.wetCount==0)p.wetCount=10;p.wet=wet;
                if(!wet)p.honeyWet=p.lavaWet=p.shimmerWet=false;if(p.wetCount>0)p.wetCount--;
            }
            if(p.shimmerWet)
            {
                int x=(int)(p.Center.X/16),y=(int)(p.position.Y/16);bool full=false;
                if(WorldGen.InWorld(x,y) && WorldGen.InWorld(x,y-1)){PredictionTile tile,above;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;if(tile.Liquid==255 && tile.ShimmerLiquid){if(!terrain.Tile(x,y-1,out above,out stop))return false;full=above.Liquid>0 && above.ShimmerLiquid;}}
                if(full)Kill(p);else if(p.velocity.Y>0){p.velocity.Y=-p.velocity.Y;if(p.timeLeft>600)p.timeLeft=600;p.timeLeft-=60;p.wet=p.shimmerWet=false;}
            }
            p.oldPosition=p.position;var position=p.position;var velocity=p.velocity;bool hit=false;
            if(p.tileCollide && !terrain.ProjectileTileMotion(p,ref position,ref velocity,p.wet,p.honeyWet,out wetVelocity,out hit,true,p.shimmerWet,true))return false;
            p.position=position;p.velocity=velocity;if(hit){p.position+=velocity;Kill(p);}
            // Wet displacement is reduced only inside native tileCollide;
            // a still noncolliding sky bolt retains the initial wetVelocity.
            p.position+=p.wet?wetVelocity:p.velocity;p.direction=p.velocity.X<0?-1:1;return true;
        }
        internal static void Finish(Projectile p){if(p.active && (--p.timeLeft<=0 || p.penetrate==0))Kill(p);}
        private sealed class Member {internal Projectile Shot;internal HostAttackObstacles Receive;internal bool Source,Reflected;}
        private static Projectile[] Capture(Player player,HostAttackObstacles root)
        {
            var rows=new List<Projectile>();foreach(var p in Main.projectile)if(p.active && p.owner==player.whoAmI && Handles(p.type))rows.Add(p);
            var scene=rows.ToArray();root.CaptureOwnedProjectileFamily(scene,player.whoAmI,new[]{337});return scene;
        }
        private static Member Make(Projectile p,HostAttackObstacles root,bool source){return new Member{Shot=p,Source=source,Receive=root.ForkForSpawn(p)};}
        private static List<Member> Prepare(Projectile[] scene,Projectile[] source,HostAttackObstacles root)
        {
            var rows=new List<Member>();foreach(var p in scene){bool own=false;if(source!=null)foreach(var q in source)if(ReferenceEquals(q,p)){own=true;break;}rows.Add(Make(Copy(p),root,own));}
            rows.Sort((left,right)=>left.Shot.whoAmI.CompareTo(right.Shot.whoAmI));return rows;
        }
        private static bool Outer(List<Member> rows,HostProjectileBirthSlots slots,NpcTrajectory path,PredictionTerrain terrain,float ownerY,double surface,int tick,int outer,Vector2 aim,AttackConfidence confidence,out AttackContact contact)
        {
            contact=null;foreach(var m in rows)if(m.Shot.active && !m.Receive.BeforeNpc(tick,outer,m.Shot.Center.X,m.Shot.Center.Y,m.Shot.width,m.Shot.height)){m.Shot.active=false;m.Reflected=true;}
            foreach(var m in rows)
            {
                var p=m.Shot;if(!p.active)continue;int updates=p.extraUpdates+1;
                for(int sub=0;sub<updates;sub++)
                {
                    if(!Motor(p,ownerY,surface,terrain))return false;if(!p.active){slots.Release(p.whoAmI);break;}
                    if(p.friendly && !p.hostile && p.damage>0)
                    {
                        int hits=m.Receive.SelectedHits;bool pass=m.Receive.PassCustomOuter(2,tick,outer,p.Center.X,p.Center.Y,p.width,p.height,null,consumeSelected:true);p.penetrate=m.Receive.Remaining;
                        if(m.Source && m.Receive.SelectedHits>hits){contact=AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,p.Center.X,p.Center.Y,p.width,p.height,confidence);return true;}
                        if(!pass && p.penetrate!=0){p.active=false;m.Reflected=true;break;}
                    }
                    Finish(p);if(!p.active){if(sub+1<updates && !RetiredStep(p,ownerY,surface,terrain))return false;slots.Release(p.whoAmI);break;}
                }
            }
            return true;
        }
        private static Vector2 Cursor(Player player,Vector2 aim)
        {var pixel=aim-Main.screenPosition;if(player.gravDir==-1)pixel.Y=Main.screenHeight-pixel.Y;return new Vector2((int)pixel.X+Main.screenPosition.X,player.gravDir==-1?Main.screenPosition.Y+Main.screenHeight-(int)pixel.Y:(int)pixel.Y+Main.screenPosition.Y);}
        private static bool Advance(ref PredictionPlayerMotion motion,PredictionEnvironment environment,PredictionTerrain terrain)
        {PredictionStop stop;return PlayerMotionContinuation.Advance(ref motion,environment,terrain,out stop);}
        // frame is the sole player continuation. Prepared CompletedWorld has
        // already advanced the birth's player update in Preview. Never ReadPlayer
        // on that clone or advance that same first update for a second time.
        internal static AttackContact Solve(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay,HostAttackPlayerFrame frame,bool launchPlayerAdvanced=false)
        {
            if(ammo.WeaponType!=1931 || ammo.Projectile!=337 || ammo.Damage<=0 || delay<0 || age-delay<0 || age+1>=path.Count)return null;
            root.CaptureShotFacing(player);var scene=Capture(player,root);var pool=root.CaptureBirthSlots();var inputs=new Vector2[9];int count=0;
            var waitFrame=delay>0?HostAttackPlayerFrame.Capture(Main.player[player.whoAmI],path):frame;
            for(int seed=0;seed<9;seed++)
            {
                var b=path[Math.Min(path.Count-1,age+1+seed*4)].ProjectileReceiveBounds;var aim=Cursor(player,new Vector2(b.CenterX,b.CenterY));bool duplicate=false;for(int i=0;i<count;i++)if(inputs[i]==aim){duplicate=true;break;}if(duplicate)continue;inputs[count++]=aim;
                // Use three legal birth X offsets; do not sample game RNG or
                // promise that every member in an actual random pair will hit.
                foreach(int jitter in new[]{0,-200,200})
                {
                    root.BeginCandidate();var rows=Prepare(scene,null,root);var slots=pool.Copy();var motion=waitFrame.Motion;bool valid=true;
                    for(int wait=0;wait<delay;wait++){if((!beforeNpc || wait>0) && !Advance(ref motion,waitFrame.Environment,terrain)){valid=false;break;}AttackContact ignored;if(!Outer(rows,slots,path,terrain,motion.Y,waitFrame.Environment.GravityWorldSurface,age-delay+wait+1,wait+1,aim,AttackConfidence.Representative,out ignored)){valid=false;break;}}if(!valid)continue;
                    var body=(Player)clone.Invoke(player,null);motion=frame.Motion;
                    if(!beforeNpc && !launchPlayerAdvanced && !Advance(ref motion,frame.Environment,terrain))continue;HostAttackPlayerFrame.ApplyPose(body,motion);
                    // ChangeDir uses this cursor before Shoot. The birth formula
                    // consumes the plain owner width/Center/MountedCenter, not
                    // RotatedRelativePoint's supplied gun origin.
                    float facing=Vector2.Dot(Vector2.UnitX.RotatedBy(body.fullRotation),aim-body.RotatedRelativePoint(body.MountedCenter));body.direction=facing>0?1:-1;
                    for(int member=0;member<2;member++)
                    {
                        int slot;if(!slots.TryAllocate(out slot))return null;Vector2 center,velocity;Birth(body,aim,ammo.Speed,member,100,jitter,Vector2.Zero,out center,out velocity);
                        var p=new Projectile();p.SetDefaults(337);p.active=true;p.whoAmI=slot;p.owner=body.whoAmI;p.Center=center;p.velocity=velocity;p.damage=ammo.Damage;p.knockBack=body.HeldItem.knockBack;
                        bool wet;byte kind;if(!terrain.ProjectileWet(center.X,center.Y,p.width,p.height,out wet,out kind,true))return null;p.wet=wet;p.honeyWet=wet && kind==2;p.shimmerWet=wet && kind==3;rows.Add(Make(p,root,true));
                    }
                    rows.Sort((left,right)=>left.Shot.whoAmI.CompareTo(right.Shot.whoAmI));
                    for(int action=0;action<Math.Min(120,path.Count-age-1);action++)
                    {
                        if(action>0 && !Advance(ref motion,frame.Environment,terrain))break;AttackContact contact;
                        if(!Outer(rows,slots,path,terrain,motion.Y,frame.Environment.GravityWorldSurface,age+action+1,delay+action+1,aim,AttackConfidence.Representative,out contact))break;if(contact!=null)return contact;
                    }
                }
            }
            return null;
        }
        // Autonomous observation/oracle only. A whole array replay explicitly
        // declines Projectiles and future launch delay without a slot epoch.
        internal static AttackContact KnownFamily(Player player,Projectile[] sources,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles root,HostAttackPlayerFrame frame)
        {
            if(sources==null || sources.Length==0 || clock.LaunchDelay!=0 || clock.Phase==HostAttackPhase.Projectiles)return null;foreach(var p in sources)if(p==null || !p.active || p.type!=337 || p.owner!=player.whoAmI)return null;
            var scene=Capture(player,root);root.BeginCandidate();var rows=Prepare(scene,sources,root);var slots=root.CaptureBirthSlots().Copy();var motion=frame.Motion;var aim=sources[0].Center+sources[0].velocity*1000;
            for(int action=0;action<Math.Min(120,path.Count-clock.FirstTick);action++)
            {
                if(clock.MovePlayer(action) && !Advance(ref motion,frame.Environment,terrain))break;AttackContact contact;
                if(!Outer(rows,slots,path,terrain,motion.Y,frame.Environment.GravityWorldSurface,clock.FirstTick+action,action+1,aim,AttackConfidence.Conditional,out contact))break;if(contact!=null)return contact;
            }
            return null;
        }
    }
}
