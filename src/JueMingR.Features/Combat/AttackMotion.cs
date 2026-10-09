using System;

namespace JueMingR.Features.Combat
{
    public enum AttackConfidence { Conditional, Representative }
    public enum AttackSolveStatus { Unsolved, Contact, Unreachable }
    // All time parameters use actual projectile AI subupdates. Gravity acts
    // before translation, matching native Update; it is not a tick parabola.
    public struct AttackMotion
    {
        public readonly float Speed,Gravity,Width,Height,Acceleration,MaxSpeed;
        public readonly int Updates,GravityStart,Lifetime;
        public readonly AttackConfidence Confidence;
        public AttackMotion(float speed,float gravity,int gravityStart,int updates,float width,float height,int lifetime,
            AttackConfidence confidence=AttackConfidence.Conditional,float acceleration=1,float maxSpeed=0)
        {
            if(!(speed>0) || float.IsInfinity(speed) || updates<1 || updates>16 || width<=0 || height<=0 || lifetime<1 ||
                float.IsNaN(gravity) || float.IsInfinity(gravity) || acceleration<=0 || float.IsInfinity(acceleration))throw new ArgumentOutOfRangeException();
            Speed=speed;Gravity=gravity;GravityStart=gravityStart;Updates=updates;Width=width;Height=height;Lifetime=lifetime;
            Confidence=confidence;Acceleration=acceleration;MaxSpeed=maxSpeed;
        }
        public void Advance(ref float x,ref float y,ref float vx,ref float vy,int subupdate)
        {
            if(Acceleration!=1 && (MaxSpeed<=0 || vx*vx+vy*vy<MaxSpeed*MaxSpeed)){vx*=Acceleration;vy*=Acceleration;}
            if(Gravity!=0 && subupdate>=GravityStart)vy+=Gravity;
            x+=vx;y+=vy;
        }
    }
}
