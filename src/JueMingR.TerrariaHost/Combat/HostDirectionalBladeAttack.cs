using System;
using System.Reflection;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    // Arkhalis/Terragrim's AI075 resets to the owner each action, reads a
    // channelled direction, then ordinary movement adds that velocity once.
    // It damages with its native68x64 body, not a free-flight sword ray.
    internal static class HostDirectionalBladeAttack
    {
        private static readonly MethodInfo copy=typeof(object).GetMethod("MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        internal static bool Handles(int type){return type==595 || type==735;}
        internal static AttackContact Solve(Player player,Projectile source,NpcTrajectory path,HostAttackClock clock,PredictionTerrain terrain,HostAttackPlayerFrame frame,Vector2 aim,out HostAttackObstacles obstacles)
        {
            obstacles=new HostAttackObstacles(player,source,path,clock.LaunchDelay,clock.BeforeNpc,clock.NextWorld);
            aim=HostDirectionalControl.Cursor(player,aim);
            obstacles.CaptureArcOwner(player,player.HeldItem);obstacles.BeginCandidate();
            obstacles.CaptureHomingInputs(player,false,true);
            bool observed=source.whoAmI>=0 && source.whoAmI<Main.projectile.Length && ReferenceEquals(Main.projectile[source.whoAmI],source);
            obstacles.CaptureOwnedProjectileFamily(observed?new[]{source}:new Projectile[0],player.whoAmI,new[]{source.type});
            var body=(Player)copy.Invoke(player,null);var shot=HostThornAttack.Copy(source);shot.ownerHitCheckDistance=source.ownerHitCheckDistance;var motion=frame.Motion;
            for(int step=0;step<120 && clock.FirstTick+step<path.Count;step++)
            {
                CombatPredictionWork.Check();PredictionStop stop;if(clock.MovePlayer(step) && !PlayerMotionContinuation.Advance(ref motion,frame.Environment,terrain,out stop))return null;
                HostAttackPlayerFrame.ApplyPose(body,motion);var mounted=body.RotatedRelativePoint(body.MountedCenter);
                shot.velocity=(aim-mounted).SafeNormalize(Vector2.UnitX*body.direction)*(body.HeldItem.shoot==shot.type?body.HeldItem.shootSpeed*shot.scale:1);
                shot.Center=mounted+shot.velocity;int tick=clock.FirstTick+step,outer=clock.LaunchDelay+step+1;
                if(!obstacles.BeforeNpc(tick,outer,shot.Center.X,shot.Center.Y,shot.width,shot.height))return null;
                int hits=obstacles.SelectedHits;bool passed=obstacles.PassArcOuter(2,tick,outer,shot.Center.X,shot.Center.Y,shot.width,shot.height,null,
                    (bounds,noTile)=>HostMeleeAttack.OwnerAllows(body,shot,bounds,noTile,terrain),0);
                if(obstacles.SelectedHits>hits)return AttackIntercept.BodyContact(path,tick,0,aim.X,aim.Y,shot.Center.X,shot.Center.Y,shot.width,shot.height,AttackConfidence.Conditional);
                if(!passed)return null;
            }
            return null;
        }
    }
}
