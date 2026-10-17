using System;

namespace JueMingR.Features.Combat
{
    public enum AttackConfidence { Conditional, Representative }
    public enum AttackSolveStatus { Unsolved, Contact, Unreachable }
    // All time parameters use actual projectile AI subupdates. Gravity acts
    // before translation, matching native Update; it is not a tick parabola.
    public struct AttackMotion
    {
        public readonly float Speed,Gravity,Width,Height,Acceleration,MaxSpeed,MoveScale,WindX,MaxFallSpeed,MaxRiseSpeed,PreMotorWindX;
        public readonly int Updates,FirstUpdates,GravityStart,Lifetime,AccelerationStart;
        public readonly AttackConfidence Confidence;
        public readonly bool ComponentBirthLimit,GravityClockUsesLaunchX;
        public readonly bool ComponentAccelerationLimit,DragAfterGravity,StopSmallVelocity,HorizontalAccelerationOnly;
        public AttackMotion(float speed,float gravity,int gravityStart,int updates,float width,float height,int lifetime,
            AttackConfidence confidence=AttackConfidence.Conditional,float acceleration=1,float maxSpeed=0,bool componentBirthLimit=false,
            bool componentAccelerationLimit=false,bool dragAfterGravity=false,bool stopSmallVelocity=false,float moveScale=1,float windX=0,float maxFallSpeed=0,bool gravityClockUsesLaunchX=false,float maxRiseSpeed=0,int accelerationStart=0,bool horizontalAccelerationOnly=false,float preMotorWindX=0,int firstUpdates=0)
        {
            if(!(speed>0) || float.IsInfinity(speed) || updates<1 || updates>16 || firstUpdates<0 || firstUpdates>updates || width<=0 || height<=0 || lifetime<1 ||
                float.IsNaN(gravity) || float.IsInfinity(gravity) || acceleration<=0 || float.IsInfinity(acceleration) || !(moveScale>0) || float.IsInfinity(moveScale) || float.IsNaN(windX) || float.IsInfinity(windX) || maxFallSpeed<0 || float.IsNaN(maxFallSpeed) || float.IsInfinity(maxFallSpeed) || maxRiseSpeed<0 || float.IsNaN(maxRiseSpeed) || float.IsInfinity(maxRiseSpeed) || accelerationStart<0 || float.IsNaN(preMotorWindX) || float.IsInfinity(preMotorWindX))throw new ArgumentOutOfRangeException();
            Speed=speed;Gravity=gravity;GravityStart=gravityStart;Updates=updates;FirstUpdates=firstUpdates;Width=width;Height=height;Lifetime=lifetime;
            Confidence=confidence;Acceleration=acceleration;MaxSpeed=maxSpeed;ComponentBirthLimit=componentBirthLimit;
            ComponentAccelerationLimit=componentAccelerationLimit;DragAfterGravity=dragAfterGravity;StopSmallVelocity=stopSmallVelocity;MoveScale=moveScale;WindX=windX;MaxFallSpeed=maxFallSpeed;GravityClockUsesLaunchX=gravityClockUsesLaunchX;MaxRiseSpeed=maxRiseSpeed;AccelerationStart=accelerationStart;HorizontalAccelerationOnly=horizontalAccelerationOnly;PreMotorWindX=preMotorWindX;
        }
        // Native Update snapshots extraUpdates before AI/movement. Quiver
        // adds one only near that Update's end: a newborn's first outer can
        // contain one substep while all subsequent outers contain two.
        public int OuterAtSubupdate(int subupdate)
        {int first=FirstUpdates==0?Updates:FirstUpdates;return subupdate<=first?1:2+(subupdate-first-1)/Updates;}
        public int SubupdateInOuter(int subupdate)
        {int first=FirstUpdates==0?Updates:FirstUpdates;return subupdate<=first?subupdate-1:(subupdate-first-1)%Updates;}
        public int SubupdatesForOuters(int outers)
        {return outers<=0?0:(FirstUpdates==0?Updates:FirstUpdates)+(outers-1)*Updates;}
        public AttackMotion WithUpdates(int updates,int firstUpdates=0)
        {return new AttackMotion(Speed,Gravity,GravityStart,updates,Width,Height,Lifetime,Confidence,Acceleration,MaxSpeed,ComponentBirthLimit,ComponentAccelerationLimit,DragAfterGravity,StopSmallVelocity,MoveScale,WindX,MaxFallSpeed,GravityClockUsesLaunchX,MaxRiseSpeed,AccelerationStart,HorizontalAccelerationOnly,PreMotorWindX,firstUpdates);}
        // A component-limited birth has a finite set of .97 speed classes.
        // Inversion uses each class without limiting it a second time; the
        // host must still verify the actual cursor-dependent birth velocity.
        public AttackMotion WithSpeed(float speed,bool componentBirthLimit=false)
        {return new AttackMotion(speed,Gravity,GravityStart,Updates,Width,Height,Lifetime,Confidence,Acceleration,MaxSpeed,componentBirthLimit,ComponentAccelerationLimit,DragAfterGravity,StopSmallVelocity,MoveScale,WindX,MaxFallSpeed,GravityClockUsesLaunchX,MaxRiseSpeed,AccelerationStart,HorizontalAccelerationOnly,PreMotorWindX,FirstUpdates);}
        public void Launch(ref float vx,ref float vy)
        {
            // NewProjectile limits AI_001 components together, repeatedly.
            // This preserves its angle; a length clamp would change .8 shots.
            if(ComponentBirthLimit)while(vx>=16 || vx<=-16 || vy>=16 || vy< -16){vx*=.97f;vy*=.97f;}
        }
        public void Advance(ref float x,ref float y,ref float vx,ref float vy,int subupdate)
        {
            // AI_002 has an unconditional wind impulse before its own motor,
            // separate from Update's later gated normal wind impulse.
            vx+=PreMotorWindX;
            if(!DragAfterGravity && subupdate>=AccelerationStart && Acceleration!=1 && (MaxSpeed<=0 || (ComponentAccelerationLimit?Math.Abs(vx)<MaxSpeed && Math.Abs(vy)<MaxSpeed:vx*vx+vy*vy<MaxSpeed*MaxSpeed))){vx*=Acceleration;if(!HorizontalAccelerationOnly)vy*=Acceleration;}
            if(Gravity!=0 && subupdate>=GravityStart)vy+=Gravity;
            if(DragAfterGravity && subupdate>=AccelerationStart){vx*=Acceleration;if(!HorizontalAccelerationOnly)vy*=Acceleration;}
            if(StopSmallVelocity){if(Math.Abs(vx)<.1f)vx=0;if(Math.Abs(vy)<.1f)vy=0;}
            // AI_001 caps falling velocity after its type-specific motor.
            // Other AI families leave this fact unset instead of inheriting it.
            if(MaxFallSpeed>0 && vy>MaxFallSpeed)vy=MaxFallSpeed;
            if(MaxRiseSpeed>0 && vy< -MaxRiseSpeed)vy=-MaxRiseSpeed;
            vx+=WindX;
            // Native wetVelocity scales translation, not the stored velocity.
            // Host passage verifies the same liquid and full-velocity collision
            // before accepting this uniform-environment continuation.
            x+=vx*MoveScale;y+=vy*MoveScale;
        }
        public AttackMotion WithEnvironment(float scale,float windX)
        {return new AttackMotion(Speed,Gravity,GravityStart,Updates,Width,Height,Lifetime,Confidence,Acceleration,MaxSpeed,ComponentBirthLimit,ComponentAccelerationLimit,DragAfterGravity,StopSmallVelocity,scale,windX,MaxFallSpeed,GravityClockUsesLaunchX,MaxRiseSpeed,AccelerationStart,HorizontalAccelerationOnly,PreMotorWindX,FirstUpdates);}
        public AttackMotion WithConfidence(AttackConfidence confidence)
        {return new AttackMotion(Speed,Gravity,GravityStart,Updates,Width,Height,Lifetime,confidence,Acceleration,MaxSpeed,ComponentBirthLimit,ComponentAccelerationLimit,DragAfterGravity,StopSmallVelocity,MoveScale,WindX,MaxFallSpeed,GravityClockUsesLaunchX,MaxRiseSpeed,AccelerationStart,HorizontalAccelerationOnly,PreMotorWindX,FirstUpdates);}
        public AttackMotion WithLifetime(int lifetime)
        {return new AttackMotion(Speed,Gravity,GravityStart,Updates,Width,Height,lifetime,Confidence,Acceleration,MaxSpeed,ComponentBirthLimit,ComponentAccelerationLimit,DragAfterGravity,StopSmallVelocity,MoveScale,WindX,MaxFallSpeed,GravityClockUsesLaunchX,MaxRiseSpeed,AccelerationStart,HorizontalAccelerationOnly,PreMotorWindX,FirstUpdates);}
        public AttackMotion WithGravityStart(int start)
        {return new AttackMotion(Speed,Gravity,start,Updates,Width,Height,Lifetime,Confidence,Acceleration,MaxSpeed,ComponentBirthLimit,ComponentAccelerationLimit,DragAfterGravity,StopSmallVelocity,MoveScale,WindX,MaxFallSpeed,GravityClockUsesLaunchX,MaxRiseSpeed,AccelerationStart,HorizontalAccelerationOnly,PreMotorWindX,FirstUpdates);}
        public AttackMotion ForLaunch(float vx)
        {
            // Hellwing stores the unbounded launch X in ai0, which AI_001
            // then advances as its gravity clock. Resolve before Launch's
            // component limiter; the representative birth has zero spread.
            if(!GravityClockUsesLaunchX)return this;
            int start=Math.Max(1,(int)Math.Ceiling(GravityStart-vx));
            return new AttackMotion(Speed,Gravity,start,Updates,Width,Height,Lifetime,Confidence,Acceleration,MaxSpeed,ComponentBirthLimit,ComponentAccelerationLimit,DragAfterGravity,StopSmallVelocity,MoveScale,WindX,MaxFallSpeed,false,MaxRiseSpeed,AccelerationStart,HorizontalAccelerationOnly,PreMotorWindX,FirstUpdates);
        }
    }
}
