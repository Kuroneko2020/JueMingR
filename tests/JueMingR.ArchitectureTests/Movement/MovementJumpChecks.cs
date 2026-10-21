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
                changed=original;changed.AssistedOrdinarySpan=true;
                Require(!original.SameSample(changed),"A changed future equipment handoff cannot reuse the old player premise.");
                changed=original;changed.AssistedJumpPhase=MovementJumpPhase.Coast;
                Require(!original.SameSample(changed),"Owned natural tail is an exact captured premise, independent of physical held input.");
                Require(MovementJumpRules.Before(MovementJumpPhase.Coast,true,false,.1f)==MovementJumpPhase.None && MovementJumpRules.Before(MovementJumpPhase.Coast,true,false,-.1f)==MovementJumpPhase.Coast,"A tail ends on the gravity-relative sign boundary, without exact-zero waiting.");
                Require(MovementJumpRules.After(MovementJumpPhase.None,false,1,0,-5)==MovementJumpPhase.None,"An external held ascent cannot acquire an unregistered tail.");
                var lastTick=new PredictionPlayerMotion{AssistedJump=true,AssistedJumpPhase=MovementJumpPhase.Held,HoldJump=true,Jump=1,Vy=-2,JumpSpeed=5,Gravity=.4f,GravityDirection=1,MaxFall=10};
                PlayerVerticalMotion.Step(ref lastTick,default(PredictionEnvironment));
                Require(lastTick.Jump==0 && lastTick.AssistedJumpPhase==MovementJumpPhase.Coast && Math.Abs(lastTick.Vy+4.6f)<.0001f && lastTick.HoldJump,"Last held tick remains native and physical input remains held.");
                PlayerVerticalMotion.Step(ref lastTick,default(PredictionEnvironment));
                Require(lastTick.AssistedJumpPhase==MovementJumpPhase.Coast && Math.Abs(lastTick.Vy+4.2f)<.0001f && lastTick.ReleaseJump && lastTick.HoldJump,"Ordinary tail advances gravity under effective release without rewriting sampled hold.");
                var prefix=new PredictionPlayerMotion{Complex=true,AssistedJump=true,AssistedOrdinarySpan=true,HoldJump=true,Jump=4,Vy=-2,JumpSpeed=5,Gravity=.4f,GravityDirection=1,MaxFall=10};
                Require(PlayerMotionContinuation.OrdinaryStep(prefix),"A future equipment handoff does not erase an already-started ordinary span.");
                var boundary=prefix;boundary.Jump=1;Require(!PlayerMotionContinuation.OrdinaryStep(boundary),"Last held action retains the conditional future handoff.");
                boundary=prefix;boundary.Vy=0;Require(!PlayerMotionContinuation.OrdinaryStep(boundary),"Early collision ends the supported prefix.");
                boundary=prefix;boundary.ObservationMechanism=16;Require(!PlayerMotionContinuation.OrdinaryStep(boundary),"A real dash mechanism cannot borrow the ordinary prefix.");
                boundary=prefix;boundary.AssistedOrdinarySpan=false;Require(!PlayerMotionContinuation.OrdinaryStep(boundary),"Active thrust or pending fuel consumers cannot borrow an ordinary prefix.");
                prefix.Width=20;prefix.Height=40;prefix.X=100;prefix.Y=100;
                prefix.Right=true;prefix.Vx=.25f;prefix.Acceleration=.08f;prefix.MaxSpeed=prefix.FastMaxSpeed=3;prefix.Slowdown=.2f;
                var terrain=new PrefixTerrain();PredictionStop prefixStop;var origin=prefix;
                Require(PlayerMotionContinuation.Advance(ref origin,default(PredictionEnvironment),terrain,out prefixStop) && Math.Abs(origin.Vx-.33f)<.0001f && Math.Abs(origin.Vy+4.6f)<.0001f && origin.Jump==3,
                    "Actual future-origin consumer advances the supported held prefix, not its old sampled velocity.");
                var enemy=new NpcMotionState{Identity=new NpcIdentity(1,new object(),4,2,2,2),Style=2,X=150,Y=150,Width=20,Height=20,Life=100,LifeMax=100,TimeLeft=750,Active=true,CanReceive=true,NoGravity=true,NoTileCollide=true,Health=new NpcHealthState{RealLife=-1}};
                var environment=new PredictionEnvironment{PlayerX=110,PlayerY=120,PlayerWidth=20,PlayerHeight=40,WorldWidth=4200,WorldHeight=1200,WorldSurface=400};
                terrain.Reset();var path=new RollingNpcPrediction().Prepare(new[]{enemy},1,0,100,2,1,environment,prefix,terrain);
                Require(path!=null && path.Count==3 && terrain.PlayerMoves>0 && Math.Abs(terrain.FirstPlayerVx-.33f)<.0001f && Math.Abs(terrain.FirstPlayerVy+4.6f)<.0001f,
                    "Actual NPC chase consumer receives the same supported near-player motion.");
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
        private sealed class PrefixTerrain : IPredictionTerrain
        {
            internal int PlayerMoves;internal float FirstPlayerVx,FirstPlayerVy;
            public bool Unchanged=>true;
            public void Reset(){PlayerMoves=0;FirstPlayerVx=FirstPlayerVy=0;}
            public bool Move(ref NpcMotionState n,PredictionEnvironment e,out PredictionStop stop)
            {stop=PredictionStop.None;if(n.Friendly && PlayerMoves++==0){FirstPlayerVx=n.Vx;FirstPlayerVy=n.Vy;}n.X+=n.Vx;n.Y+=n.Vy;return true;}
            public bool Solid(MotionRect r,out bool solid,out PredictionStop stop){solid=false;stop=PredictionStop.None;return true;}
            public bool CanHit(MotionRect a,MotionRect b,out bool clear,out PredictionStop stop){clear=true;stop=PredictionStop.None;return true;}
            public bool Tile(int x,int y,out PredictionTile tile,out PredictionStop stop){tile=default(PredictionTile);stop=PredictionStop.None;return true;}
        }
    }
}
