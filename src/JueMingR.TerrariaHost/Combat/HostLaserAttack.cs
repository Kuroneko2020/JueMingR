using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostLaserAttack
    {
        private static readonly System.Reflection.MethodInfo copy=typeof(object).GetMethod("MemberwiseClone",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        internal static bool Consumes(Player player,Projectile parent)
        {
            if(!player.channel)return false;
            float phase=parent.ai[0]+1;int tier=(phase>=40?1:0)+(phase>=80?1:0)+(phase>=120?1:0);
            return parent.ai[1]+player.GetSlowMagicUseRate()>=24-6*tier;
        }
        // NewProjectile searches the real pool. A private idle controller
        // takes the first free slot itself, then its first child takes the
        // next one. Existing controllers may have an earlier free child slot.
        private static bool Later(Projectile parent,out bool later)
        {
            later=false;if(Terraria.Testing.DebugOptions.Shared_RandomizeProjectileSlots)return false;
            bool live=parent.whoAmI>=0 && parent.whoAmI<Main.maxProjectiles && ReferenceEquals(Main.projectile[parent.whoAmI],parent);
            int parentSlot=live?parent.whoAmI:-1;
            for(int i=0;i<Main.maxProjectiles;i++)if(!Main.projectile[i].active)
            {if(parentSlot<0){parentSlot=i;continue;}later=i>parentSlot;return true;}
            return false;
        }
        internal static AttackContact Solve(Player player,Projectile parent,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,out Vector2 point,out HostAttackObstacles obstacles)
        {return SolvePrepared(player,parent,timeline,clock,terrain,out point,out obstacles,HostAttackPlayerFrame.Capture(player,timeline));}
        internal static AttackContact SolvePrepared(Player player,Projectile parent,NpcTrajectory timeline,HostAttackClock clock,PredictionTerrain terrain,out Vector2 point,out HostAttackObstacles obstacles,HostAttackPlayerFrame frame)
        {
            point=HostHeldAttack.DirectionPoint(player,timeline,clock.FirstTick);obstacles=null;
            if(!player.channel || player.dead || player.noItems || player.CCed)return null;
            bool later;if(!Later(parent,out later))return null;
            var child=ContentSamples.ProjectilesByType[440];var fees=new HostAttackResources.FeeClock(player,parent);
            var motion=frame.Motion;var environment=frame.Environment;
            var body=(Player)copy.Invoke(player,null);Vector2 center=parent.Center,velocity=parent.velocity;
            var motor=new AttackMotion(14,0,0,child.extraUpdates+1,child.width,child.height,child.timeLeft,AttackConfidence.Representative,componentBirthLimit:true);
            var receive=HostAttackReceive.Capture(player,child,timeline.Identity.Slot,clock.BeforeNpc,clock.NextWorld || !later);
            // Random muzzle offsets and spread are represented by their
            // central member, without drawing RNG. Timing, holder movement,
            // mana fees and the old parent-center birth order remain native.
            for(int step=0;step<120 && clock.FirstTick+step<timeline.Count;step++)
            {
                if(clock.MovePlayer(step)){PredictionStop stop;if(!PlayerMotionContinuation.Advance(ref motion,environment,terrain,out stop))return null;}
                HostAttackPlayerFrame.ApplyPose(body,motion);Vector2 anchor=body.RotatedRelativePoint(body.MountedCenter);
                float phase;bool fires;if(!fees.Advance(out phase,out fires))return null;
                if(fires)
                {
                    int tick=clock.FirstTick+step+(later?0:1);if(tick>=timeline.Count)return null;
                    var direct=AttackIntercept.Solve(center.X,center.Y,motor,timeline,tick,0,null,0);
                    Vector2 unit=direct==null?(HostHeldAttack.DirectionPoint(player,timeline,tick)-center).SafeNormalize(Vector2.UnitX*player.direction):new Vector2(direct.AimX-center.X,direct.AimY-center.Y).SafeNormalize(Vector2.UnitX*player.direction);
                    point=anchor+unit*10000;
                    obstacles=new HostAttackObstacles(player,child,timeline,clock.LaunchDelay+step,clock.BeforeNpc,clock.NextWorld || !later);
                    var contact=HostPulseAttack.Replay(player,child,new HostPulseAttack.State(child,center,unit*14),timeline,tick,clock.LaunchDelay+step+1+(later?0:1),terrain,obstacles,receive,point,AttackConfidence.Representative);
                    if(contact!=null)return contact;
                    velocity=unit*(player.HeldItem.shootSpeed*parent.scale);
                }
                // AI075 emits before reanchoring; UpdatePosition then adds
                // the controller's retained velocity. Using this frame's
                // mounted center for the child skips the real holder offset.
                center=anchor+velocity;
            }
            return null;
        }
    }
}
