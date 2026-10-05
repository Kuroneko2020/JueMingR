using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // One bounded synchronous calculation from a completed world observation.
    // Cache owns only demand/publication on this route. No timestamp-only reuse
    // and no retained future can survive hits, terrain edits or player motion.
    public sealed class RollingNpcPrediction
    {
        private readonly NpcMotionState[] work=new NpcMotionState[NpcPredictionCache.Capacity];
        private readonly NpcTrajectoryPoint[] points=new NpcTrajectoryPoint[NpcPredictionCache.Horizon+1];
        private NpcMotionState previous;
        private long priorTick=-1,version;
        public PredictionFailureLayer FailureLayer {get;private set;}
        public void Clear(){previous=default(NpcMotionState);priorTick=-1;Array.Clear(work,0,work.Length);}
        public NpcTrajectory Prepare(NpcMotionState[] source,int count,int selected,long tick,int required,long epoch,PredictionEnvironment environment,PredictionPlayerMotion player,IPredictionTerrain terrain,bool[] motionRoles=null)
        {
            FailureLayer=PredictionFailureLayer.Source;
            if(source==null || count<1 || count>work.Length || selected<0 || selected>=count || required<1 || required>NpcPredictionCache.Horizon)return null;
            var current=source[selected];
            bool needsPlayer=false,canObservePlayer=true;for(int i=0;i<count;i++)if((motionRoles==null || i<motionRoles.Length && motionRoles[i]) && NpcMotion.NeedsPlayerMotion(source[i],environment,required)){needsPlayer=true;canObservePlayer&=NpcMotion.CurrentPlayerPremise(source[i]);}
            if(!current.Active || !current.CanReceive || current.Life<=0 || !Valid(current) || needsPlayer && !Valid(player) || !Finite(environment.Wind) || !Finite(environment.WorldSurface) || !Finite(environment.RockLayer) || motionRoles!=null && motionRoles.Length<count){Clear();return null;}
            bool observed=priorTick+1==tick && previous.Identity.Equals(current.Identity) && SamePhase(previous,current) && !current.JustHit &&
                Math.Abs(current.X-previous.X)<512 && Math.Abs(current.Y-previous.Y)<512;
            if(observed)
            {
                current.ObservedAccelerationX=Clamp(current.Vx-previous.Vx,.2f);
                current.ObservedAccelerationY=Clamp(current.Vy-previous.Vy,.2f);
                if(current.NoGravity && previous.NoGravity)
                {
                    float cross=previous.Vx*current.Vy-previous.Vy*current.Vx,dot=previous.Vx*current.Vx+previous.Vy*current.Vy;
                    if(dot>0)current.ObservedTurn=Clamp((float)Math.Atan2(cross,dot),.03f);
                    if(current.ObservedTurn!=0)
                    {
                        float speed=(float)Math.Sqrt(current.Vx*current.Vx+current.Vy*current.Vy),priorSpeed=(float)Math.Sqrt(previous.Vx*previous.Vx+previous.Vy*previous.Vy);
                        float acceleration=Clamp(speed-priorSpeed,.2f)/Math.Max(.001f,speed);
                        current.ObservedAccelerationX=current.Vx*acceleration;current.ObservedAccelerationY=current.Vy*acceleration;
                    }
                }
            }
            previous=current;priorTick=tick;Array.Copy(source,work,count);work[selected]=current;
            terrain.Reset();playerSettled=false;points[0]=new NpcTrajectoryPoint(0,current);int length=1;PredictionStop stop=PredictionStop.None;
            FailureLayer=PredictionFailureLayer.None;bool observedPlayer=false,restarted=false;var initialPlayer=player;
            for(int future=1;future<=required;future++)
            {
                bool futureNeeds=false;canObservePlayer=true;
                // Rechecking a future phase must not move the horizon forward:
                // a home clock beyond the requested endpoint is irrelevant.
                for(int i=0;i<count;i++)if((motionRoles==null || motionRoles[i]) && NpcMotion.NeedsPlayerMotion(work[i],environment,required-future+1)){futureNeeds=true;canObservePlayer&=NpcMotion.CurrentPlayerPremise(work[i]);}
                if(futureNeeds && !needsPlayer)
                {
                    // A modeled phase acquired a new necessary premise. Start
                    // its whole player timeline at point zero, once; never
                    // begin simulating that player halfway through the path.
                    if(restarted || !Valid(initialPlayer)){stop=PredictionStop.PhaseBoundary;FailureLayer=PredictionFailureLayer.PlayerPremise;break;}
                    needsPlayer=true;restarted=true;player=initialPlayer;terrain.Reset();playerSettled=false;
                    Array.Copy(source,work,count);work[selected]=current;length=1;future=0;continue;
                }
                if(needsPlayer && !observedPlayer && !AdvancePlayer(ref player,environment,terrain,out stop))
                {
                    // Only these bounded structural models can use the real
                    // current target-player premise. Invalid numeric state
                    // and unknown NPC/root/wall geometry remain hard stops.
                    if(!restarted && canObservePlayer && (stop==PredictionStop.TerrainUnavailable || stop==PredictionStop.TerrainLimit || stop==PredictionStop.LiquidEffect || stop==PredictionStop.Slope))
                    {
                        // Discard the conditional prefix and restart at most
                        // once with one consistent real-observation premise.
                        // Switching back to the real origin halfway through
                        // a retained path would create an artificial turn.
                        observedPlayer=restarted=true;stop=PredictionStop.None;terrain.Reset();playerSettled=false;
                        Array.Copy(source,work,count);work[selected]=current;length=1;future=0;continue;
                    }
                    else{FailureLayer=PredictionFailureLayer.PlayerPremise;break;}
                }
                var env=environment;
                if(needsPlayer && !observedPlayer){env.PlayerX=player.X+player.Width*.5f;env.PlayerY=player.Y+player.Height*.5f;env.PlayerWet=playerBody.Wet;}
                bool advanced=true;
                for(int i=0;i<count;i++)
                {
                    var state=work[i];
                    // Supply the same bounded timeline to a known first
                    // retarget. Preserve the old numbered target until its
                    // own native phase actually selects the captured closest.
                    if(state.HasClosestPlayer && state.ClosestPlayerIndex==env.PlayerIndex)
                    {state.ClosestPlayerArea=new MotionRect(env.PlayerX-env.PlayerWidth*.5f,env.PlayerY-env.PlayerHeight*.5f,env.PlayerWidth,env.PlayerHeight);state.ClosestPlayerWet=env.PlayerWet;}
                    bool playerNeededThisAction=(motionRoles==null || motionRoles[i]) && NpcMotion.NeedsPlayerMotion(state,env,1);
                    // A shared-life owner is a health dependency, not permission
                    // to replay that otherwise unrelated actor's AI/geometry.
                    bool valid=motionRoles!=null && !motionRoles[i]?NpcHealth.Step(ref state,work,count,env,out stop):NpcMotion.Step(ref state,work,count,env,terrain,future,true,out stop);
                    if(!valid || !Valid(state)){if(valid)stop=PredictionStop.InvalidState;advanced=false;break;}
                    if(playerNeededThisAction && state.TargetCaptured && state.PlayerIndex!=environment.PlayerIndex)
                    {stop=PredictionStop.MissingDependency;advanced=false;break;}
                    work[i]=state;
                }
                if(!advanced){FailureLayer=PredictionFailureLayer.NpcMotion;break;}
                points[length++]=new NpcTrajectoryPoint(future,work[selected]);
            }
            var assumptions=(NpcMotion.Assumptions(current)&~PredictionAssumption.TargetPlayerStationary)|PredictionAssumption.HeldPlayerControls|PredictionAssumption.ApproximateMechanism;
            if(environment.Multiplayer)assumptions|=PredictionAssumption.NetworkObservation;
            if(current.UnmodeledDamageTicks>0)assumptions|=PredictionAssumption.UnmodeledDamageEffects;
            if(observedPlayer)assumptions=(assumptions&~PredictionAssumption.HeldPlayerControls)|PredictionAssumption.CurrentPlayerObservation;
            if(!needsPlayer)assumptions=(assumptions&~PredictionAssumption.HeldPlayerControls)|PredictionAssumption.NoPlayerMotionNeeded;
            var quality=NpcMotion.StructuredModel(current)?PredictionQuality.StructuredApproximation:observed?PredictionQuality.ObservedTrend:PredictionQuality.LimitedObservation;
            return new NpcTrajectory(current.Identity,tick,++version,assumptions,stop,points,length,PredictionStrategy.RollingConditional,epoch,quality);
        }
        private NpcMotionState playerBody;
        private bool playerSettled;
        private bool AdvancePlayer(ref PredictionPlayerMotion p,PredictionEnvironment e,IPredictionTerrain terrain,out PredictionStop stop)
        {
            stop=PredictionStop.None;
            // Within this one forecast the sampled local cells are immutable.
            // A grounded idle fixed point needs one collision/liquid query,
            // not the same rectangle 120 times. Reset on EVERY real sample.
            if(playerSettled)return true;
            float oldX=p.X,oldY=p.Y;
            if(!p.Complex || p.Hover)
            {
                if(p.Left && p.Vx>-p.MaxSpeed){if(p.Vx>p.Slowdown)p.Vx-=p.Slowdown;p.Vx-=p.Acceleration;}
                else if(p.Right && p.Vx<p.MaxSpeed){if(p.Vx< -p.Slowdown)p.Vx+=p.Slowdown;p.Vx+=p.Acceleration;}
                else p.Vx=NpcMotion.Approach(p.Vx,0,p.Vy==0?p.Slowdown:p.Slowdown*.5f);
                if(p.Hover)
                {
                    float target=p.Up || p.HoldJump?-p.MaxSpeed:p.Down?p.MaxSpeed:0;
                    p.Vy=NpcMotion.Approach(p.Vy,target,p.Acceleration);
                }
                else
                {
                    if(p.HoldJump)
                    {
                        if(p.Jump>0){p.Vy=-p.JumpSpeed*p.GravityDirection;p.Jump--;}
                        else if(p.Vy==0 && (p.ReleaseJump || p.AutoJump)){p.Vy=-p.JumpSpeed*p.GravityDirection;p.Jump=p.JumpHeight;}
                    }
                    else p.Jump=0;
                    p.ReleaseJump=!p.HoldJump;
                    p.Vy+=p.Gravity*p.GravityDirection;
                    if(p.Vy*p.GravityDirection>p.MaxFall)p.Vy=p.MaxFall*p.GravityDirection;
                }
            }
            // Geometry is shared with NPC local acquisition, but player health
            // and AI are never simulated. Liquid/complex mount motion remains
            // an explicit approximation corrected by the next real observation.
            playerBody=new NpcMotionState{X=p.X,Y=p.Y,Vx=p.Vx,Vy=p.Vy,Width=p.Width,Height=p.Height,Active=true,Friendly=true,
                Health=new NpcHealthState{Immortal=true,DontTakeDamage=true,LavaImmune=true,ShimmerImmune=true},WaterSpeed=1,HoneySpeed=1,LavaSpeed=1,ShimmerSpeed=1};
            var playerTerrain=terrain as IPredictionPlayerTerrain;
            if(playerTerrain!=null)
            {if(!playerTerrain.MovePlayer(ref playerBody,e,ref p,out stop))return false;}
            else if(p.WaterWalk)
            {
                var surface=terrain as IPredictionWaterSurfaceTerrain;
                if(surface==null){stop=PredictionStop.TerrainUnavailable;return false;}
                if(!surface.MoveWaterWalkingPlayer(ref playerBody,e,p.Down,p.LavaWalk,out stop))return false;
            }
            else if(!terrain.Move(ref playerBody,e,out stop))return false;
            if(!Valid(playerBody)){stop=PredictionStop.InvalidState;return false;}
            p.X=playerBody.X;p.Y=playerBody.Y;p.Vx=playerBody.Vx;p.Vy=playerBody.Vy;
            playerSettled=!p.Complex && !p.Hover && !p.Left && !p.Right && !p.Up && !p.Down && !p.HoldJump && p.Vx==0 && p.Vy==0 && p.X==oldX && p.Y==oldY;
            return true;
        }
        private static bool Valid(NpcMotionState n)
        {return Finite(n.X)&&Finite(n.Y)&&Finite(n.Vx)&&Finite(n.Vy)&&Finite(n.A0)&&Finite(n.A1)&&Finite(n.A2)&&Finite(n.A3)&&Finite(n.L0)&&Finite(n.L1)&&Finite(n.L2)&&Finite(n.L3)&&Finite(n.Scale)&&Finite(n.NetOffsetX)&&Finite(n.NetOffsetY)&&Finite(n.Health.DamageMultiplier)&&Finite(n.Health.ShimmerTransparency)&&Finite(n.WaterSpeed)&&Finite(n.HoneySpeed)&&Finite(n.LavaSpeed)&&Finite(n.ShimmerSpeed)&&n.WaterSpeed>=0&&n.HoneySpeed>=0&&n.LavaSpeed>=0&&n.ShimmerSpeed>=0&&n.Width>0&&n.Width<=1024&&n.Height>0&&n.Height<=1024&&Math.Abs(n.Vx)<=512&&Math.Abs(n.Vy)<=512;}
        private static bool Valid(PredictionPlayerMotion p)
        {return Finite(p.X)&&Finite(p.Y)&&Finite(p.Vx)&&Finite(p.Vy)&&Finite(p.Gravity)&&Finite(p.Acceleration)&&Finite(p.Slowdown)&&Finite(p.MaxSpeed)&&Finite(p.MaxFall)&&Finite(p.JumpSpeed)&&p.MaxFall>=0&&p.JumpSpeed>=0&&Math.Abs(p.Vx)<=512&&Math.Abs(p.Vy)<=512&&p.GravityDirection*p.GravityDirection==1&&p.Width>0&&p.Height>0&&p.Width<=512&&p.Height<=512&&p.Acceleration>=0&&p.Slowdown>=0&&p.MaxSpeed>=0;}
        private static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
        private static float Clamp(float value,float limit){return Math.Max(-limit,Math.Min(limit,value));}
        private static bool SamePhase(NpcMotionState a,NpcMotionState b)
        {return a.Style==b.Style && a.PositionRelation==b.PositionRelation && a.PositionOwner.Equals(b.PositionOwner) && a.PositionParameter==b.PositionParameter && (a.A0==b.A0 || Math.Abs(a.A0)>8 && Math.Abs(b.A0)>8 && Math.Sign(a.A0)==Math.Sign(b.A0) && Math.Abs(a.A0-b.A0)<=4);}
    }
}
