using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // AI075's third use emits one 706 with doubled ammo damage. The phoenix
    // flies autonomously; only the held parent's next native cursor read is
    // controlled. Its wet/tile/lifetime Kill damages using the same local
    // history, and every preceding receive truncates damage by five percent.
    internal static class HostPhoenixAttack
    {
        private static readonly MethodInfo clone=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        internal sealed class State
        {
            internal readonly Projectile Shot;
            internal readonly bool Apprentice;
            internal bool Burst,Retired;
            internal Vector2 BurstCenter;
            internal int BurstSide;
            internal State(Projectile shot,bool apprentice){Shot=Copy(shot);Apprentice=apprentice;}
            // Damage_PVE checks positive damage once before the NPC loop.
            // Reaching zero in OnHit blocks the next Damage call, while later
            // receivers in this already-admitted pass can still take damage.
            internal bool Hit(int slot,int remaining){Shot.damage=(int)(Shot.damage*.95f);return true;}
        }
        internal static Projectile Copy(Projectile p)
        {
            var q=(Projectile)clone.Invoke(p,null);q.ai=(float[])p.ai.Clone();q.localAI=(float[])p.localAI.Clone();q.localNPCImmunity=(int[])p.localNPCImmunity.Clone();return q;
        }
        internal static void Kill(State s)
        {
            var q=s.Shot;if(!q.active)return;
            s.Burst=true;s.BurstCenter=q.Center;s.BurstSide=s.Apprentice?208:96;
            q.position=q.Center;q.width=q.height=s.BurstSide;q.Center=q.position;q.timeLeft=0;q.active=false;
        }
        internal static bool Step(State s,PredictionTerrain terrain)
        {
            var q=s.Shot;s.Burst=false;if(!q.active)return true;
            if(q.position.X<=Main.leftWorld || q.position.X+q.width>=Main.rightWorld || q.position.Y<=Main.topWorld || q.position.Y+q.height>=Main.bottomWorld || float.IsNaN(q.position.X) || float.IsNaN(q.position.Y))
            {q.active=false;s.Retired=true;return true;}
            // AI reads the previous update's wet flag, before this update's
            // liquid sample. No gravity clock is incremented for type 706.
            if(q.wet)Kill(s);
            else
            {
                q.localAI[1]=1;q.frameCounter++;if(q.frameCounter>=40)q.frameCounter=0;q.frame=q.frameCounter/5;
                q.alpha=Math.Max(0,q.alpha-55);q.spriteDirection=q.direction;q.rotation=q.direction<0?(float)Math.Atan2(-q.velocity.Y,-q.velocity.X):q.velocity.ToRotation();if(q.velocity.Y>16)q.velocity.Y=16;
            }
            float wind;if(!HostProjectileEnvironment.Impulse(q,terrain,q.Center.X,q.Center.Y,q.velocity.X,out wind))return false;q.velocity.X+=wind;
            if(q.owner<255 && !Main.player[q.owner].active)Kill(s);
            if(!q.ignoreWater)
            {
                bool wet,lava;byte kind;
                if(!terrain.ProjectileWet(q.Center.X,q.Center.Y,q.width,q.height,out wet,out kind,true) || !terrain.ProjectileLava(q.Center.X,q.Center.Y,q.width,q.height,out lava,true))return false;
                if(lava)q.lavaWet=true;if(wet && kind==2)q.honeyWet=true;if(wet && kind==3)q.shimmerWet=true;
                if(q.wet && !wet && q.wetCount==0)q.wetCount=10;q.wet=wet;
                if(!wet)q.honeyWet=q.lavaWet=q.shimmerWet=false;if(q.wetCount>0)q.wetCount--;
            }
            if(q.shimmerWet)
            {
                int x=(int)(q.Center.X/16),y=(int)(q.position.Y/16);bool full=false;
                if(WorldGen.InWorld(x,y) && WorldGen.InWorld(x,y-1))
                {PredictionTile tile,above;PredictionStop stop;if(!terrain.Tile(x,y,out tile,out stop))return false;if(tile.Liquid==255 && tile.ShimmerLiquid){if(!terrain.Tile(x,y-1,out above,out stop))return false;full=above.Liquid>0 && above.ShimmerLiquid;}}
                if(full)Kill(s);else if(q.velocity.Y>0){q.velocity.Y=-q.velocity.Y;if(q.timeLeft>600)q.timeLeft=600;q.timeLeft-=60;q.wet=q.shimmerWet=false;}
            }
            var position=q.position;var velocity=q.velocity;var wetVelocity=velocity;bool hit=false;
            if(q.tileCollide && !terrain.ProjectileTileMotion(q,ref position,ref velocity,q.wet,q.honeyWet,out wetVelocity,out hit,true,q.shimmerWet,true))return false;
            q.position=position;q.velocity=velocity;if(hit){q.position+=velocity;Kill(s);}q.position+=q.wet?wetVelocity:velocity;q.direction=q.velocity.X<0?-1:1;return true;
        }
        internal static void Finish(State s)
        {var q=s.Shot;if(q.active && (--q.timeLeft<=0 || q.penetrate==0))Kill(s);}
        private static AttackContact Receive(State s,NpcTrajectory path,int tick,int outer,int sub,Vector2 aim,HostAttackObstacles obstacles,Func<int,int,bool> onHit)
        {
            var q=s.Shot;if(q.damage<=0)return null;
            Vector2 center=s.Burst?s.BurstCenter:q.Center;int width=s.Burst?s.BurstSide:q.width,height=s.Burst?s.BurstSide:q.height;
            int hits=obstacles.SelectedHits;
            bool passed=obstacles.PassCustomOuter(2,tick,outer,center.X,center.Y,width,height,null,consumeSelected:true,onHit:onHit);
            if(obstacles.SelectedHits>hits)return AttackIntercept.BodyContact(path,tick,sub,aim.X,aim.Y,center.X,center.Y,width,height,AttackConfidence.Conditional);
            if(!passed){q.active=false;s.Retired=true;s.Burst=false;}return null;
        }
        private static AttackContact Replay(Projectile shot,bool apprentice,NpcTrajectory path,int first,int firstOuter,bool beforeFirst,PredictionTerrain terrain,HostAttackObstacles obstacles,Vector2 aim)
        {
            var state=new State(shot,apprentice);var q=state.Shot;Func<int,int,bool> onHit=state.Hit;
            obstacles.BeginCandidate();
            for(int step=0;step<Math.Min(120,path.Count-first) && q.active;step++)
            {
                int tick=first+step,outer=firstOuter+step;
                if((beforeFirst || step>0) && !obstacles.BeforeNpc(tick,outer,q.Center.X,q.Center.Y,q.width,q.height))return null;
                for(int sub=0;sub<=q.extraUpdates && q.active;sub++)
                {
                    if(!Step(state,terrain))return null;AttackContact contact;
                    if(state.Burst){contact=Receive(state,path,tick,outer,sub,aim,obstacles,onHit);if(contact!=null)return contact;break;}
                    if(!q.active)break;
                    contact=Receive(state,path,tick,outer,sub,aim,obstacles,onHit);if(contact!=null)return contact;if(state.Retired)break;
                    Finish(state);if(state.Burst){contact=Receive(state,path,tick,outer,sub,aim,obstacles,onHit);if(contact!=null)return contact;break;}
                }
            }
            return null;
        }
        internal static Vector2 BirthVelocity(Player player,Projectile parent,Vector2 origin,Vector2 aim)
        {
            var screen=aim-Main.screenPosition;if(player.gravDir==-1)screen.Y=Main.screenHeight-screen.Y;
            var target=new Vector2((int)screen.X+Main.screenPosition.X,Main.screenPosition.Y+(player.gravDir==-1?Main.screenHeight-(int)screen.Y:(int)screen.Y));
            var held=Vector2.Normalize(target-origin);if(float.IsNaN(held.X) || float.IsNaN(held.Y))held=-Vector2.UnitY;
            held*=player.HeldItem.shootSpeed*parent.scale;held*=.55f;var velocity=Vector2.Normalize(held)*16f;
            if(float.IsNaN(velocity.X) || float.IsNaN(velocity.Y))velocity=-Vector2.UnitY;
            while(velocity.X>=16 || velocity.X<=-16 || velocity.Y>=16 || velocity.Y< -16)velocity*=.97f;return velocity;
        }
        internal static AttackContact SolvePrepared(Player player,Projectile parent,AttackAmmoSnapshot ammo,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackPlayerFrame frame,out Vector2 point,out HostAttackObstacles obstacles)
        {
            point=HostHeldAttack.DirectionPoint(player,path,clock.FirstTick);obstacles=null;
            if(parent.type!=705 || player.phantomPhoneixCounter+1<3 || ammo.Damage<=0)return null;
            int delay=Math.Max(0,(int)Math.Ceiling(parent.ai[1])-1);
            var body=frame.Motion;var world=frame.Environment;
            for(int i=0;i<delay+(clock.NextWorld?1:0);i++){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref body,world,terrain,out stop))return null;}
            var pose=(Player)clone.Invoke(player,null);HostAttackPlayerFrame.ApplyPose(pose,body);Vector2 origin=pose.RotatedRelativePoint(pose.MountedCenter);
            var shot=new Projectile();shot.SetDefaults(706);shot.active=true;shot.owner=player.whoAmI;shot.damage=ammo.Damage*2;
            // NewProjectileSetup overrides the 300-tick type default for a
            // locally owned 706 before the first AI action.
            shot.timeLeft=120;
            obstacles=new HostAttackObstacles(player,shot,path,clock.LaunchDelay+delay,clock.BeforeNpc,clock.NextWorld);
            bool apprentice=obstacles.CapturePhoenixOwner(player);obstacles.CaptureCursorMapping(player);
            var pool=obstacles.CaptureBirthSlots().Copy();int parentSlot=parent.whoAmI,childSlot;
            bool liveParent=parentSlot>=0 && parentSlot<Main.projectile.Length && ReferenceEquals(Main.projectile[parentSlot],parent) && parent.active;
            if(!liveParent && !pool.TryAllocate(out parentSlot))return null;
            if(!pool.TryAllocate(out childSlot))return null;int later=childSlot>parentSlot?0:1;
            int first=clock.FirstTick+delay+later,firstOuter=clock.LaunchDelay+delay+later+1;if(first>=path.Count)return null;
            shot.whoAmI=childSlot;var directions=new Vector2[41];int count=0;
            for(int seed=0;seed<41;seed++)
            {
                var box=path[Math.Min(path.Count-1,first+Math.Min(seed,8)*4)].ProjectileReceiveBounds;
                Vector2 direction=seed<9?(new Vector2(box.CenterX,box.CenterY)-origin).SafeNormalize(Vector2.UnitX*player.direction):((seed-9)*MathHelper.TwoPi/32).ToRotationVector2();
                bool duplicate=false;for(int i=0;i<count;i++)if(directions[i]==direction){duplicate=true;break;}if(duplicate)continue;directions[count++]=direction;
                var aim=origin+direction*10000;shot.Center=origin;shot.velocity=BirthVelocity(pose,parent,origin,aim);bool wet;byte kind;
                if(!terrain.ProjectileWet(origin.X,origin.Y,shot.width,shot.height,out wet,out kind,true))return null;shot.wet=wet;
                var contact=Replay(shot,apprentice,path,first,firstOuter,clock.Phase!=HostAttackPhase.Projectiles || delay>0 || later>0,terrain,obstacles,aim);
                if(contact!=null){point=aim;return contact;}
            }
            return null;
        }
        // Actual-born oracle only. A phoenix is never registered as a cursor
        // consumer merely because its autonomous flight can be replayed.
        internal static AttackContact Known(Player player,Projectile shot,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackObstacles obstacles)
        {
            if(shot==null || !shot.active || shot.type!=706 || shot.owner!=player.whoAmI || !shot.friendly || shot.hostile || shot.damage<=0 || clock.LaunchDelay!=0)return null;
            bool apprentice=obstacles.CapturePhoenixOwner(player);obstacles.CaptureProjectileFamily(new[]{shot});
            return Replay(shot,apprentice,path,clock.FirstTick,1,clock.Phase!=HostAttackPhase.Projectiles,terrain,obstacles,shot.Center+shot.velocity*1000);
        }
    }
}
