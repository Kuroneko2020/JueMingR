using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostProjectileEnvironment
    {
        // A special hand has already fixed both origin and velocity. Apply the
        // same uniform fluid/wind read set as Solve without replacing that
        // birth by a second muzzle-direction inversion. Every replay checks
        // the actual gate before its impulse and rejects a changed phase.
        internal static AttackContact Replay(Projectile sample,PredictionTerrain terrain,Vector2 aim,Vector2 origin,Vector2 velocity,AttackMotion motion,NpcTrajectory timeline,int age,Func<int,bool> receives,Func<int,int,float,float,bool> damagePassage,Func<int,int,float,float,bool> beforeMotion)
        {
            float scale;if(!Factor(sample,terrain,origin.X,origin.Y,out scale))return null;
            float impulse=sample.ShouldUseWindPhysics()?Main.windSpeedCurrent*Main.windPhysicsStrength:0;
            for(int mode=0;mode<(impulse==0?1:2);mode++)
            {
                var environment=new HostProjectileEnvironment(sample,terrain,scale,mode==0?0:impulse);
                var contact=AttackIntercept.Replay(aim.X,aim.Y,origin.X,origin.Y,velocity.X,velocity.Y,motion.WithEnvironment(scale,environment.WindX),timeline,age,environment.Passage,receives,damagePassage,beforeMotion);
                if(contact!=null)return contact;
            }
            return null;
        }
    }
}
