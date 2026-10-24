using System;
using System.Collections.Generic;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Combat
{
    internal static class HostMotionAttackInputs
    {
        private struct Proposal
        {
            internal Vector2 Direction;internal bool IsCenter;
        }
        // Separable gravity stages can propose every receive-time interval
        // without first replaying a dry projectile to the earliest contact.
        // Nonseparable acceleration/birth limiting keeps its existing solver.
        // None of these proposals supplies a Contact or an input permission.
        internal static Vector2[] Generate(Vector2 origin,AttackMotion motion,NpcTrajectory timeline,int age)
        {
            var directions=new List<Proposal>(8);float speed=motion.Speed;
            // AI001 limits components together, leaving a faster diagonal
            // class legal. A vector-length clamp would lose that branch.
            do
            {
                var branch=motion.WithSpeed(speed);
                if(AttackMotionCandidates.Supports(branch))
                    foreach(var candidate in AttackMotionCandidates.Generate(timeline,age,origin.X,origin.Y,branch))
                    {
                        var direction=new Vector2(candidate.X,candidate.Y);float vx=direction.X*motion.Speed,vy=direction.Y*motion.Speed;motion.Launch(ref vx,ref vy);
                        if(Math.Abs((float)Math.Sqrt(vx*vx+vy*vy)-speed)>.002f)continue;
                        bool duplicate=false;
                        for(int i=0;i<directions.Count;i++)if(Vector2.Dot(directions[i].Direction,direction)>.99999f)
                        {
                            // Promotion keeps the later, birth-validated centre
                            // vector itself, never a centre label on an edge.
                            if(candidate.IsCenter && !directions[i].IsCenter)
                            {directions.RemoveAt(i);directions.Add(new Proposal{Direction=direction,IsCenter=true});}
                            duplicate=true;break;
                        }
                        if(!duplicate)directions.Add(new Proposal{Direction=direction,IsCenter=candidate.IsCenter});
                    }
                if(!motion.ComponentBirthLimit || speed<16)break;speed*=.97f;
            }while(true);
            if(directions.Count>0)
            {
                // The first reachable replay is the consumer's result. Preserve
                // centre margin across ALL legal speed classes, with stable
                // order inside each class and the same finite candidate set.
                var result=new Vector2[directions.Count];int at=0;
                foreach(var row in directions)if(row.IsCenter)result[at++]=row.Direction;
                foreach(var row in directions)if(!row.IsCenter)result[at++]=row.Direction;
                return result;
            }
            // Supports describes the separable inverse only. Nonseparable
            // acceleration and launch-X clocks retain their finite solver.
            var contact=AttackIntercept.Solve(origin.X,origin.Y,motion,timeline,age,0);
            return contact==null?new Vector2[0]:new[]{new Vector2(contact.AimX-origin.X,contact.AimY-origin.Y).SafeNormalize(Vector2.UnitX)};
        }
    }
}
