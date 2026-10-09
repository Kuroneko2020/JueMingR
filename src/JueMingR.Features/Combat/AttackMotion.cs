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
        public readonly bool ComponentBirthLimit;
        public readonly bool ComponentAccelerationLimit,DragAfterGravity,StopSmallVelocity;
        public AttackMotion(float speed,float gravity,int gravityStart,int updates,float width,float height,int lifetime,
            AttackConfidence confidence=AttackConfidence.Conditional,float acceleration=1,float maxSpeed=0,bool componentBirthLimit=false,
            bool componentAccelerationLimit=false,bool dragAfterGravity=false,bool stopSmallVelocity=false)
        {
            if(!(speed>0) || float.IsInfinity(speed) || updates<1 || updates>16 || width<=0 || height<=0 || lifetime<1 ||
                float.IsNaN(gravity) || float.IsInfinity(gravity) || acceleration<=0 || float.IsInfinity(acceleration))throw new ArgumentOutOfRangeException();
            Speed=speed;Gravity=gravity;GravityStart=gravityStart;Updates=updates;Width=width;Height=height;Lifetime=lifetime;
            Confidence=confidence;Acceleration=acceleration;MaxSpeed=maxSpeed;ComponentBirthLimit=componentBirthLimit;
            ComponentAccelerationLimit=componentAccelerationLimit;DragAfterGravity=dragAfterGravity;StopSmallVelocity=stopSmallVelocity;
        }
        public void Launch(ref float vx,ref float vy)
        {
            // NewProjectile limits AI_001 components together, repeatedly.
            // This preserves its angle; a length clamp would change .8 shots.
            if(ComponentBirthLimit)while(vx>=16 || vx<=-16 || vy>=16 || vy< -16){vx*=.97f;vy*=.97f;}
        }
        public void Advance(ref float x,ref float y,ref float vx,ref float vy,int subupdate)
        {
            if(!DragAfterGravity && Acceleration!=1 && (MaxSpeed<=0 || (ComponentAccelerationLimit?Math.Abs(vx)<MaxSpeed && Math.Abs(vy)<MaxSpeed:vx*vx+vy*vy<MaxSpeed*MaxSpeed))){vx*=Acceleration;vy*=Acceleration;}
            if(Gravity!=0 && subupdate>=GravityStart)vy+=Gravity;
            if(DragAfterGravity){vx*=Acceleration;vy*=Acceleration;}
            if(StopSmallVelocity){if(Math.Abs(vx)<.1f)vx=0;if(Math.Abs(vy)<.1f)vy=0;}
            x+=vx;y+=vy;
        }
    }
}
