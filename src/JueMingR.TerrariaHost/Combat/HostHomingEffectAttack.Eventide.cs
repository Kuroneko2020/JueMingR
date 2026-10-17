using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal static partial class HostHomingEffectAttack
    {
        internal static bool EventideBirth(Player player,Vector2 origin,Vector2 cursor,float speed,int stage,PredictionTerrain terrain,out Vector2 center,out Vector2 velocity)
        {
            cursor=HostDirectionalControl.Cursor(player,cursor);
            var direction=(cursor-origin).SafeNormalize(-Vector2.UnitY);var offset=direction*40;
            bool clear;PredictionStop stop;center=origin;velocity=Vector2.Zero;
            if(!terrain.CanHit(new MotionRect(origin.X,origin.Y,0,0),new MotionRect(origin.X+offset.X,origin.Y+offset.Y,0,0),out clear,out stop))return false;
            int side=HostNativeArrowBirthAttack.Facing(player,origin,cursor,false)==1?4-stage:stage;var rotated=offset.RotatedBy(MathHelper.Pi/10*(side-2));if(!clear)rotated-=offset;
            center=origin+rotated;var fromBody=(center-player.Center).SafeNormalize(Vector2.Zero);
            var lineEnd=center-direction*speed;
            if(!terrain.CanHitLine(new MotionRect(player.MountedCenter.X,player.MountedCenter.Y,4,4),new MotionRect(lineEnd.X,lineEnd.Y,0,0),out clear,out stop))return false;
            if(!clear)center-=fromBody*15;
            var final=(cursor-center).SafeNormalize(-Vector2.UnitY);float blend=Utils.GetLerpValue(100,40,Vector2.Distance(cursor,player.Center),true);
            if(blend>0)final=Vector2.Lerp(final,direction,blend).SafeNormalize(direction);
            velocity=final*speed;return true;
        }
        private static AttackContact Eventide(Player player,AttackAmmoSnapshot ammo,NpcTrajectory path,Vector2 origin,int age,bool beforeNpc,PredictionTerrain terrain,HostAttackObstacles root,int delay)
        {
            // Birth adaptation and ammo semantics compose in one ordered world.
            // In particular 225 still reflects/retargets after a displaced
            // noncentral birth; central932 keeps its AI181 motor in that world.
            return HostNativeArrowBirthAttack.Solve(player,ammo,path,origin,new HostAttackClock(path,beforeNpc?HostAttackPhase.BeforeNpc:HostAttackPhase.CompletedWorld,delay),terrain,root);
        }
    }
}
