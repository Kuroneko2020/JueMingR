using System;
using System.Collections.Generic;
using JueMingR.Features.Movement;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;

namespace JueMingR.ArchitectureTests
{
    internal static class MovementJumpChecks
    {
        internal static void Check(List<string> failures)
        {
            try
            {
                foreach(bool held in new[]{false,true})foreach(bool down in new[]{false,true})foreach(int span in new[]{-1,0,1,45})foreach(bool opportunity in new[]{false,true})
                    Require(MovementJumpRules.Release(held,down,span,opportunity)==(held && !down && span<=0 && opportunity),"Only a completed span with current held intent and a native opportunity may release.");
                var ownership=new MovementJumpOwnership();
                Require(ownership.TryAcquire(MovementJumpOwner.SimulatedJump),"Current update accepts continuous intent.");
                Require(ownership.TryAcquire(MovementJumpOwner.FallProtection),"Protection can reserve before any writer.");
                Require(!ownership.TryAcquire(MovementJumpOwner.SimulatedJump) && !ownership.MarkWritten(MovementJumpOwner.SimulatedJump),"Lower owner cannot overwrite protection.");
                Require(ownership.MarkWritten(MovementJumpOwner.FallProtection),"Protection writes under its own owner without pretending held input.");
                Require(!ownership.TryAcquire(MovementJumpOwner.FallProtection),"A consumed/written update cannot be reacquired or rolled back.");
                ownership.Reset();Require(ownership.TryAcquire(MovementJumpOwner.SimulatedJump) && ownership.MarkWritten(MovementJumpOwner.SimulatedJump) && !ownership.TryAcquire(MovementJumpOwner.FallProtection),"Priority is decided before a contribution can be consumed.");
                ownership.Reset();Require(ownership.Owner==MovementJumpOwner.None && !ownership.Written,"Scope completion has no queued action.");
                var original=new PredictionPlayerMotion{AssistedJump=true,AssistedJumpVersion=7};var changed=original;changed.AssistedJump=false;
                Require(!original.SameSample(changed),"Released or disabled intent retires an old premise.");changed=original;changed.AssistedJumpVersion++;
                Require(!original.SameSample(changed),"Owner/session revision retires an old premise even with Jump held.");
                foreach(float gravity in new[]{1f,-1f})foreach(bool down in new[]{false,true})
                {
                    var p=new PredictionPlayerMotion{HoldJump=true,AssistedJump=true,Down=down,Jump=0,JumpHeight=15,JumpSpeed=5.01f,GravityDirection=gravity,Gravity=.4f,MaxFall=10};
                    PlayerVerticalMotion.Step(ref p,default(PredictionEnvironment));
                    Require((p.Jump==15)==!down && (down || Math.Abs(p.Vy-(-5.01f+.4f)*gravity)<.0001f),"Ordinary future ground jump preserves gravity and Down escape.");
                }
                var ongoing=new PredictionPlayerMotion{HoldJump=true,AssistedJump=true,Jump=45,JumpSpeed=5,GravityDirection=1,Gravity=.4f,MaxFall=10,Vy=-2};
                PlayerVerticalMotion.Step(ref ongoing,default(PredictionEnvironment));Require(ongoing.Jump==44 && Math.Abs(ongoing.Vy+4.6f)<.0001f,"Continuous intent cannot shorten a long native span.");
            }
            catch(Exception e){failures.Add("Movement jump: "+e);}
        }
        private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
    }
}
