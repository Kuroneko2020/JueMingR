using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostMotionAttackInputs
    {
        // Separable gravity stages can propose every receive-time interval
        // without first replaying a dry projectile to the earliest contact.
        // Nonseparable acceleration/birth limiting keeps its existing solver.
        // None of these proposals supplies a Contact or an input permission.
        internal static Vector2[] Generate(Vector2 origin,AttackMotion motion,NpcTrajectory timeline,int age)
        {
            if(AttackMotionCandidates.Supports(motion) && motion.Gravity!=0)
            {
                var candidates=AttackMotionCandidates.Generate(timeline,age,origin.X,origin.Y,motion);
                var directions=new Vector2[candidates.Length];for(int i=0;i<directions.Length;i++)directions[i]=new Vector2(candidates[i].X,candidates[i].Y);
                return directions;
            }
            var contact=AttackIntercept.Solve(origin.X,origin.Y,motion,timeline,age,0);
            return contact==null?new Vector2[0]:new[]{new Vector2(contact.AimX-origin.X,contact.AimY-origin.Y).SafeNormalize(Vector2.UnitX)};
        }
    }
}
