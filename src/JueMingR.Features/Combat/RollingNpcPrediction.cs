using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    // One bounded synchronous calculation from a completed world observation.
    // Cache owns demand/publication. A result may be consumed repeatedly only
    // for its exact sample, dependencies, revisions and verified local cells.
    // Its capture time is never advanced to imitate a fresh world observation.
    public sealed partial class RollingNpcPrediction
    {
        private readonly NpcMotionState[] work=new NpcMotionState[NpcPredictionCache.Capacity];
        private readonly NpcTrajectoryPoint[] points=new NpcTrajectoryPoint[NpcPredictionCache.Horizon+1];
        private readonly PredictionWallSource[] wallPoints=new PredictionWallSource[NpcPredictionCache.Horizon+1];
        private readonly PredictionGravitySource[] gravityPoints=new PredictionGravitySource[NpcPredictionCache.Horizon+1];
        private NpcMotionState previous;
        private PredictionPlayerMotion previousPlayer;
        private long priorTick=-1,version;
        private NpcMotionState trend;
        private float samplePlayerVx,samplePlayerVy;
        private readonly NpcMotionState[] resultSample=new NpcMotionState[NpcPredictionCache.Capacity];
        private readonly bool[] resultRoles=new bool[NpcPredictionCache.Capacity];
        private NpcTrajectory result;
        private PredictionEnvironment resultEnvironment;
        private PredictionPlayerMotion resultPlayer;
        private int resultCount,resultSelected,resultRequired;
        private long resultTick=-1,resultEpoch;
        private IPredictionTerrain resultTerrain;
        private bool resultObserved;
        private bool resultPlayerAdvanced,previousPlayerAdvanced;
        private PredictionFailureLayer resultFailure;
        public PredictionFailureLayer FailureLayer {get;private set;}
        // The host may avoid repeating a candidate miss only after resampling
        // ALL inputs and checking its independent terrain lease. This marker
        // excludes trend/history, observation fallback and timed source payloads;
        // it never permits retimestamping a conditional trajectory.
        public bool FixedInputResult {get;private set;}
        public void Clear(){Continuation=null;FixedInputResult=false;previous=default(NpcMotionState);previousPlayer=default(PredictionPlayerMotion);previousPlayerAdvanced=resultPlayerAdvanced=false;priorTick=resultTick=-1;result=null;resultTerrain=null;Array.Clear(work,0,work.Length);}
        public NpcTrajectory Prepare(NpcMotionState[] source,int count,int selected,long tick,int required,long epoch,PredictionEnvironment environment,PredictionPlayerMotion player,IPredictionTerrain terrain,bool[] motionRoles=null,bool firstPlayerStepCompleted=false)
        {
            FailureLayer=PredictionFailureLayer.Source;
            if(source==null || count<1 || count>work.Length || selected<0 || selected>=count || required<1 || required>NpcPredictionCache.Horizon)return null;
            var current=source[selected];
            // Parent-relative consumers must be decided at their actual actor
            // slot, after earlier owners moved. A frame-start predicate can
            // both miss an entering boundary and invent a departing one.
            bool needsPlayer=false,canObservePlayer=true;for(int i=0;i<count;i++)if((i==selected || !NpcWindyBalloonMotion.Known(source[i]) || source[i].Active && NpcWindyBalloonMotion.NeededWriter(source[i],source,count)) && source[i].PositionRelation!=6 && (motionRoles==null || i<motionRoles.Length && motionRoles[i]) && NpcMotion.NeedsPlayerMotion(source[i],environment,required,source,count,terrain)){needsPlayer=true;canObservePlayer&=NpcMotion.CurrentPlayerPremise(source[i]);}
            // Wet patrol does not read a target vector while currently blocked,
            // but later visibility still reads the player. Use a trustworthy
            // timeline whenever available, including held movement around a
            // wall. Missing future facts allow only a declared fixed-target
            // observation prefix, never a claim of player independence.
            bool conditionalVisibility=false;
            for(int i=0;i<count;i++)if((motionRoles==null || i<motionRoles.Length && motionRoles[i]) && source[i].Style==3 && (source[i].EffectiveType==461 || source[i].EffectiveType==586) && source[i].Wet)
            {if(!environment.PlayerDead && Valid(player)){needsPlayer=true;canObservePlayer=false;}else conditionalVisibility=true;}
            if(!current.Active || !current.CanReceive || current.Life<=0 || !Valid(current) || needsPlayer && !Valid(player) || double.IsNaN(environment.GravityWorldSurface) || double.IsInfinity(environment.GravityWorldSurface) || !Finite(environment.Wind) || !Finite(environment.WindTarget) || !Finite(environment.WorldSurface) || !Finite(environment.RockLayer) || motionRoles!=null && motionRoles.Length<count){Clear();return null;}
            bool sameObservation=priorTick==tick && previous.SameSample(current);
            bool sameResult=result!=null && resultPlayerAdvanced==firstPlayerStepCompleted && resultTick==tick && resultEpoch==epoch && resultCount==count && resultSelected==selected && resultRequired>=required && resultEnvironment.Equals(environment) && resultPlayer.SameSample(player) && ReferenceEquals(resultTerrain,terrain);
            if(sameResult)for(int i=0;i<count;i++)if(!resultSample[i].SameSample(source[i]) || resultRoles[i]!=(motionRoles==null || motionRoles[i])){sameResult=false;break;}
            if(sameResult && terrain.Unchanged){FailureLayer=resultFailure;return result;}
            Continuation=null;
            bool retain=CaptureContinuation && count==1 && IndependentContinuation(source[0],environment,player);
            var firstState=default(NpcMotionState);var firstPlayer=default(PredictionPlayerMotion);
            FixedInputResult=false;
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
            // A second solve (larger demand/environment correction) cannot
            // consume the same position twice or erase its confirmed trend.
            if(sameObservation){current=trend;observed=resultObserved;}
            bool boundedPlayer=BoundedPlayer(player);
            float observedVx=player.Vx,observedVy=player.Vy;
            // Consecutive samples of the same real player and mechanism can
            // correct sampled velocity (rope uses instant position changes).
            // A switch, teleport or generation replacement resets this input.
            if(!previousPlayerAdvanced && priorTick+1==tick && player.PlayerToken!=null && ReferenceEquals(previousPlayer.PlayerToken,player.PlayerToken) && previousPlayer.PlayerIndex==player.PlayerIndex && previousPlayer.ObservationMechanism==player.ObservationMechanism && Math.Abs(player.X-previousPlayer.X)<=32 && Math.Abs(player.Y-previousPlayer.Y)<=32)
            {observedVx=player.X-previousPlayer.X;observedVy=player.Y-previousPlayer.Y;}
            if(priorTick==tick && previousPlayer.SameSample(player)){observedVx=samplePlayerVx;observedVy=samplePlayerVy;}
            previousPlayer=player;previous=source[selected];trend=current;priorTick=tick;samplePlayerVx=observedVx;samplePlayerVy=observedVy;
            previousPlayerAdvanced=resultPlayerAdvanced=firstPlayerStepCompleted;
            Array.Copy(source,resultSample,count);for(int i=0;i<count;i++)resultRoles[i]=motionRoles==null || motionRoles[i];
            resultEnvironment=environment;resultPlayer=player;resultCount=count;resultSelected=selected;resultRequired=required;resultTick=tick;resultEpoch=epoch;resultTerrain=terrain;resultObserved=observed;
            Array.Copy(source,work,count);work[selected]=current;
            terrain.Reset();playerSettled=false;points[0]=new NpcTrajectoryPoint(0,current);int length=1;PredictionStop stop=PredictionStop.None;
            var towerConsumers=NpcCelestialTowerMotion.CreateConsumers(environment);
            var moonLeeches=NpcMoonLeechMotion.CreateLeeches(environment);
            var gravityEnvironment=environment;bool gravityNeeded=player.GravitySourceExpected || player.ForcedGravityTicks>0;
            for(int i=0;i<count;i++)if(work[i].EffectiveType==266 && environment.GoodWorld)gravityNeeded=true;
            bool wallNeeded=player.WallSourceExpected || environment.WallPlayers!=null || player.WallGross || player.WallTongued || player.WallHorrifiedTicks>0 || player.WallTonguedTicks>0;
            wallPoints[0]=WallPoint(work,count);
            gravityPoints[0]=GravityPoint(gravityEnvironment);
            FailureLayer=PredictionFailureLayer.None;bool observedPlayer=needsPlayer && boundedPlayer,restarted=false;var initialPlayer=player;
            bool fixedInputs=!gravityNeeded && !wallNeeded;
            for(int future=1;future<=required;future++)
            {
                // CheckBossIndexes precedes the player action. Keep its full
                // identity; an inactive/replaced owner clears the global.
                if(wallNeeded && !(firstPlayerStepCompleted && future==1))NpcWallBossMotion.BeginFrame(work,count);
                gravityEnvironment.WallSource=wallPoints[length-1]=WallPoint(work,count);
                if(gravityNeeded && !(firstPlayerStepCompleted && future==1))
                {
                    if(!RefreshGravity(ref gravityEnvironment,work,count,out stop)){FailureLayer=PredictionFailureLayer.PlayerPremise;break;}
                    gravityPoints[length-1]=GravityPoint(gravityEnvironment);
                }
                bool futureNeeds=false;canObservePlayer=true;
                // Rechecking a future phase must not move the horizon forward:
                // a home clock beyond the requested endpoint is irrelevant.
                for(int i=0;i<count;i++)if((i==selected || !NpcWindyBalloonMotion.Known(work[i]) || work[i].Active && NpcWindyBalloonMotion.NeededWriter(work[i],work,count)) && work[i].PositionRelation!=6 && (motionRoles==null || motionRoles[i]) && NpcMotion.NeedsPlayerMotion(work[i],environment,required-future+1,work,count,terrain)){futureNeeds=true;canObservePlayer&=NpcMotion.CurrentPlayerPremise(work[i]);}
                if(futureNeeds && !needsPlayer)
                {
                    // A modeled phase acquired a new necessary premise. Start
                    // its whole player timeline at point zero, once; never
                    // begin simulating that player halfway through the path.
                    if(restarted || !Valid(initialPlayer)){stop=PredictionStop.PhaseBoundary;FailureLayer=PredictionFailureLayer.PlayerPremise;break;}
                    needsPlayer=true;observedPlayer=boundedPlayer;restarted=true;player=initialPlayer;terrain.Reset();playerSettled=false;
                    Array.Copy(source,work,count);work[selected]=current;towerConsumers=NpcCelestialTowerMotion.CreateConsumers(environment);moonLeeches=NpcMoonLeechMotion.CreateLeeches(environment);gravityEnvironment=environment;gravityPoints[0]=GravityPoint(gravityEnvironment);wallPoints[0]=WallPoint(work,count);length=1;future=0;continue;
                }
                // Natural ItemCheck sees this player's completed move but the
                // NPC's previous completed position. First NPC action consumes
                // that player as-is; later steps retain normal player-before-
                // NPC ordering. This phase is part of cache reuse identity.
                if(needsPlayer && (observedPlayer || firstPlayerStepCompleted && future==1))
                    PlayerMotionContinuation.CompleteItemCheck(ref player);
                if(needsPlayer && !observedPlayer && !(firstPlayerStepCompleted && future==1) && !AdvancePlayer(ref player,gravityEnvironment,terrain,out stop))
                {
                    bool floatingFailure=player.FloatInWater && player.FloatingNow && (stop==PredictionStop.UnsupportedMechanism || stop==PredictionStop.TerrainUnavailable || stop==PredictionStop.TerrainLimit);
                    // Only these bounded structural models can use the real
                    // current target-player premise. Invalid numeric state
                    // and unknown NPC/root/wall geometry remain hard stops.
                    if(!restarted && (floatingFailure || canObservePlayer && (stop==PredictionStop.TerrainUnavailable || stop==PredictionStop.TerrainLimit || stop==PredictionStop.LiquidEffect || stop==PredictionStop.Slope)))
                    {
                        // Discard the conditional prefix and restart at most
                        // once with one consistent real-observation premise.
                        // Switching back to the real origin halfway through
                        // a retained path would create an artificial turn.
                        observedPlayer=restarted=true;boundedPlayer=floatingFailure;stop=PredictionStop.None;terrain.Reset();playerSettled=false;
                        Array.Copy(source,work,count);work[selected]=current;towerConsumers=NpcCelestialTowerMotion.CreateConsumers(environment);moonLeeches=NpcMoonLeechMotion.CreateLeeches(environment);gravityEnvironment=environment;gravityPoints[0]=GravityPoint(gravityEnvironment);wallPoints[0]=WallPoint(work,count);length=1;future=0;continue;
                    }
                    else{FailureLayer=PredictionFailureLayer.PlayerPremise;break;}
                }
                if(retain && !SameContinuationPlayer(initialPlayer,player))retain=false;
                var env=environment;
                if(wallNeeded && needsPlayer && !observedPlayer)
                {env.PlayerGross=player.WallGross;env.WallTimelineCaptured=true;env.WallTimelineIndex=player.PlayerIndex;env.WallTimelineArea=new MotionRect(player.X,player.Y,player.Width,player.Height);env.WallTimelineGross=player.WallGross;}
                env.GravitySourceCaptured=gravityEnvironment.GravitySourceCaptured;env.GravitySourceValid=gravityEnvironment.GravitySourceValid;
                env.PlayerTimelineActive=needsPlayer;
                if(needsPlayer){env.PlayerVx=player.Vx;env.PlayerVy=player.Vy;}
                if(needsPlayer && !observedPlayer){env.PlayerX=player.X+player.Width*.5f;env.PlayerY=player.Y+player.Height*.5f;env.PlayerWet=player.Wet;env.PlayerVx=player.Vx;env.PlayerVy=player.Vy;env.PlayerVelocityCaptured=true;}
                else if(needsPlayer && boundedPlayer)
                {
                    // Twelve observed updates, then hold the reached premise.
                    // This is a finite target estimate, never grapple/rope AI
                    // or a claim that special geometry is safe to replay.
                    // Keep direct native velocity as the current observation:
                    // rope position deltas are not Main.player.velocity reads.
                    int span=Math.Min(future-(firstPlayerStepCompleted?1:0),12);
                    env.PlayerX=initialPlayer.X+initialPlayer.Width*.5f+Clamp(observedVx,16)*span;
                    env.PlayerY=initialPlayer.Y+initialPlayer.Height*.5f+Clamp(observedVy,16)*span;
                }
                // Players update before the whole native NPC slot loop. A
                // lower-slot hook can read a later body's numbered target, so
                // propagate that player's geometry to every sampled row now,
                // while each NPC keeps its own slot-ordered retarget action.
                for(int i=0;i<count;i++)
                {
                    if(work[i].HasPlayer && work[i].PlayerIndex==env.PlayerIndex)
                    {if(work[i].PlayerPreciseAreaCaptured)work[i].PlayerPreciseArea=needsPlayer && !observedPlayer?new MotionRect(player.X,player.Y,player.Width,player.Height):new MotionRect(env.PlayerX-env.PlayerWidth*.5f,env.PlayerY-env.PlayerHeight*.5f,env.PlayerWidth,env.PlayerHeight);work[i].PlayerGross=needsPlayer && !observedPlayer?player.WallGross:work[i].PlayerGross;work[i].PlayerGravityDirection=needsPlayer && !observedPlayer?player.GravityDirection:work[i].PlayerGravityDirection;work[i].PlayerCenterX=env.PlayerX;work[i].PlayerCenterY=env.PlayerY;work[i].PlayerVx=env.PlayerVx;work[i].PlayerVy=env.PlayerVy;work[i].PlayerVelocityCaptured=env.PlayerVelocityCaptured;}
                    if(work[i].HasClosestPlayer && work[i].ClosestPlayerIndex==env.PlayerIndex)
                    {if(work[i].ClosestPlayerPreciseAreaCaptured)work[i].ClosestPlayerPreciseArea=needsPlayer && !observedPlayer?new MotionRect(player.X,player.Y,player.Width,player.Height):new MotionRect(env.PlayerX-env.PlayerWidth*.5f,env.PlayerY-env.PlayerHeight*.5f,env.PlayerWidth,env.PlayerHeight);work[i].ClosestPlayerGross=needsPlayer && !observedPlayer?player.WallGross:work[i].ClosestPlayerGross;work[i].ClosestPlayerGravityDirection=needsPlayer && !observedPlayer?player.GravityDirection:work[i].ClosestPlayerGravityDirection;work[i].ClosestPlayerArea=new MotionRect(env.PlayerX-env.PlayerWidth*.5f,env.PlayerY-env.PlayerHeight*.5f,env.PlayerWidth,env.PlayerHeight);work[i].ClosestPlayerWet=env.PlayerWet;work[i].ClosestPlayerCenterX=env.PlayerX;work[i].ClosestPlayerCenterY=env.PlayerY;work[i].ClosestPlayerVx=env.PlayerVx;work[i].ClosestPlayerVy=env.PlayerVy;work[i].ClosestPlayerVelocityCaptured=env.PlayerVelocityCaptured;}
                }
                // BuildActiveNPCs establishes the Destroyer flag before the
                // native actor loop. Retirement in an earlier slot cannot alter
                // that frame-start fact until the next whole NPC frame.
                bool destroyerPresent=false;for(int i=0;i<count;i++)if(work[i].Active && work[i].EffectiveType==135)destroyerPresent=true;
                if(future>=2)for(int i=0;i<count;i++)if(work[i].MechDestroyerCaptured)work[i].MechDestroyerPresent=destroyerPresent;
                NpcGolemMotion.BeginFrame(work,count);
                NpcBrainSwarmMotion.BeginFrame(work,count);
                bool advanced=true,actorRestart=false;
                for(int i=0;i<count;i++)
                {
                    var state=work[i];
                    // Released slaves no longer depend on an old balloon's AI.
                    // Keep retired values for raw consumers without replaying death.
                    if(i!=selected && (!state.Active && (work[selected].WindyRelationsCaptured || work[selected].EmpressFactsCaptured) || NpcWindyBalloonMotion.Known(state) && !NpcWindyBalloonMotion.NeededWriter(state,work,count)))continue;
                    // Inactive raw dependencies have no native actor action.
                    // Keep their identity/status for later original-slot reads,
                    // including Moon Lord's raw part slots and global owners.
                    if(!state.Active && i!=selected && (work[selected].RitualRelationsCaptured || work[selected].MoonRelationsCaptured || work[selected].TowerRelationsCaptured || work[selected].GolemRelationsCaptured || work[selected].BrainRelationsCaptured || wallNeeded || gravityNeeded) && motionRoles!=null && !motionRoles[i])continue;
                    // AI111's potential owner is observed before the player's
                    // next update is known. Once this actual action releases,
                    // that owner must not contribute an unrelated failing AI.
                    // Other position/life/link consumers still keep their role.
                    if(i!=selected && current.EffectiveType==111 && state.Identity.Equals(current.PositionOwner))
                    {
                        var follower=work[selected];bool needed=follower.PositionRelation==8 && NpcPositionMotion.Follows(follower,env) || follower.Health.RealLife==state.Identity.Slot || follower.ParentSlot==state.Identity.Slot || follower.ChildSlot==state.Identity.Slot;
                        for(int other=0;!needed && other<count;other++)if(other!=selected && other!=i && (work[other].PositionRelation!=0 && work[other].PositionOwner.Equals(state.Identity) || work[other].Health.RealLife==state.Identity.Slot || work[other].ParentSlot==state.Identity.Slot || work[other].ChildSlot==state.Identity.Slot))needed=true;
                        if(!needed)continue;
                    }
                    bool writesGravity=environment.GoodWorld && state.EffectiveType==266 && state.Style==54 && state.A0<0;
                    bool playerNeededThisAction=(motionRoles==null || motionRoles[i]) && NpcMotion.NeedsPlayerMotion(state,env,1,work,count,terrain);
                    if(playerNeededThisAction && !needsPlayer)
                    {
                        if(restarted || !Valid(initialPlayer)){stop=PredictionStop.PhaseBoundary;FailureLayer=PredictionFailureLayer.PlayerPremise;advanced=false;break;}
                        needsPlayer=restarted=true;observedPlayer=boundedPlayer;player=initialPlayer;terrain.Reset();playerSettled=false;
                        Array.Copy(source,work,count);work[selected]=current;towerConsumers=NpcCelestialTowerMotion.CreateConsumers(environment);moonLeeches=NpcMoonLeechMotion.CreateLeeches(environment);gravityEnvironment=environment;gravityPoints[0]=GravityPoint(gravityEnvironment);wallPoints[0]=WallPoint(work,count);length=1;future=0;actorRestart=true;break;
                    }
                    // A shared-life owner is a health dependency, not permission
                    // to replay that otherwise unrelated actor's AI/geometry.
                    if((motionRoles==null || motionRoles[i]) && !NpcMotion.StructuredModel(state))fixedInputs=false;
                    bool valid=motionRoles!=null && !motionRoles[i]?NpcHealth.Step(ref state,work,count,env,out stop):NpcMotion.Step(ref state,work,count,env,terrain,future,true,out stop);
                    if(state.TrendApplied)fixedInputs=false;
                    if(valid && motionRoles!=null && !motionRoles[i] && NpcBrainSwarmMotion.Creeper(state))valid=NpcBrainSwarmMotion.LifeOnly(ref state,out stop);
                    if(!valid || !Valid(state))
                    {
                        // An AI11 head can retire before a later hand's slot.
                        // The hand then executes its known native orphan clock;
                        // that necessary owner's retirement is not actor death.
                        // AI7 similarly stops considering a dangerous actor
                        // after its known retirement, without retiring itself.
                        if(!valid && stop==PredictionStop.Despawn && i!=selected && (work[selected].EmpressFactsCaptured || work[selected].WindyRelationsCaptured || work[selected].RitualRelationsCaptured || work[selected].MoonRelationsCaptured || work[selected].CultistRelationsCaptured || NpcPumpkingMotion.Known(state) || state.SkeletonRelationsCaptured || state.GolemRelationsCaptured || state.BrainRelationsCaptured || state.WallRelationsCaptured || work[selected].TowerRelationsCaptured || work[selected].TownRelationsCaptured))
                        {state.Active=false;work[i]=state;stop=PredictionStop.None;continue;}
                        if(valid)stop=PredictionStop.InvalidState;advanced=false;break;
                    }
                    // FindClosestPlayer does not write NPC.target. Its kernel
                    // verifies the separate Euclidean winner's timeline, while
                    // standard numbered-target motors retain this old gate.
                    // AI83 release validates each actual raw-slot winner in
                    // its model; the tablet's own target is not that consumer.
                    if(playerNeededThisAction && !(NpcMoonLeechMotion.Known(state) || state.EffectiveType==437 && state.Style==83 || NpcWallBossMotion.Known(state) || NpcTownCritterMotion.Known(state) || state.EffectiveType==399 && state.Style==80 || NpcDarkMageMotion.Known(state) || NpcBetsyMotion.Known(state) || NpcInvasionLightningMotion.Known(state) || NpcInvasionFlightMotion.Known(state) || NpcInvasionGroundMotion.Known(state)) && state.TargetCaptured && state.PlayerIndex!=environment.PlayerIndex)
                    {stop=PredictionStop.MissingDependency;advanced=false;break;}
                    work[i]=state;
                    if(writesGravity){gravityEnvironment.GravitySourceCaptured=true;gravityEnvironment.GravitySourceValid=true;gravityEnvironment.GravitySourceIdentity=state.Identity;}
                }
                if(actorRestart)continue;
                if(!advanced){if(FailureLayer!=PredictionFailureLayer.PlayerPremise)FailureLayer=PredictionFailureLayer.NpcMotion;break;}
                // Original 629 shield consumers run after ALL NPC slots. Their
                // writes affect the next actor action; current CanReceive is
                // retained exactly until that action recomputes the shield.
                if(!NpcCelestialTowerMotion.EndFrame(towerConsumers,work,count,environment,out stop)){FailureLayer=PredictionFailureLayer.NpcMotion;break;}
                // AI82 reads the preceding projectile anchor. Native 456 moves
                // after all NPC slots and writes only the next NPC action's
                // input, using this frame's unique completed player timeline.
                if(!NpcMoonLeechMotion.EndFrame(moonLeeches,work,count,env,player,firstPlayerStepCompleted,observedPlayer,future,terrain,out stop)){FailureLayer=PredictionFailureLayer.NpcMotion;break;}
                if(gravityNeeded && !RefreshGravity(ref gravityEnvironment,work,count,out stop)){FailureLayer=PredictionFailureLayer.PlayerPremise;break;}
                wallPoints[length]=WallPoint(work,count);gravityPoints[length]=GravityPoint(gravityEnvironment);points[length++]=new NpcTrajectoryPoint(future,work[selected]);
                if(retain && future==1){firstState=work[0];firstPlayer=player;}
            }
            var assumptions=(NpcMotion.Assumptions(current)&~PredictionAssumption.TargetPlayerStationary)|PredictionAssumption.HeldPlayerControls|PredictionAssumption.ApproximateMechanism;
            if(environment.Multiplayer)assumptions|=PredictionAssumption.NetworkObservation;
            if(current.UnmodeledDamageTicks>0)assumptions|=PredictionAssumption.UnmodeledDamageEffects;
            if(observedPlayer)assumptions=(assumptions&~PredictionAssumption.HeldPlayerControls)|PredictionAssumption.CurrentPlayerObservation;
            if(!needsPlayer)assumptions=(assumptions&~PredictionAssumption.HeldPlayerControls)|PredictionAssumption.NoPlayerMotionNeeded;
            if(conditionalVisibility && !needsPlayer)assumptions=(assumptions&~PredictionAssumption.NoPlayerMotionNeeded)|PredictionAssumption.TargetPlayerStationary|PredictionAssumption.CurrentPlayerObservation;
            var quality=NpcMotion.StructuredModel(current)?PredictionQuality.StructuredApproximation:observed?PredictionQuality.ObservedTrend:PredictionQuality.LimitedObservation;
            if(conditionalVisibility && !needsPlayer)quality=PredictionQuality.LimitedObservation;
            FixedInputResult=fixedInputs && !observedPlayer && quality==PredictionQuality.StructuredApproximation && stop==PredictionStop.None && length==required+1 &&
                (assumptions&(PredictionAssumption.RandomRepresentative|PredictionAssumption.CurrentPlayerObservation|PredictionAssumption.UnmodeledDamageEffects|PredictionAssumption.UnmodeledStatusEffects))==0;
            result=new NpcTrajectory(current.Identity,tick,++version,assumptions,stop,points,length,PredictionStrategy.RollingConditional,epoch,quality,gravityNeeded && initialPlayer.PlayerToken!=null?new PredictionGravitySources(gravityPoints,length,initialPlayer.PlayerToken,initialPlayer.PlayerIndex,firstPlayerStepCompleted,current.Identity,tick):null,wallNeeded && initialPlayer.PlayerToken!=null?new PredictionWallSources(wallPoints,length,initialPlayer.PlayerToken,initialPlayer.PlayerIndex,current.Identity,tick):null);resultFailure=FailureLayer;
            if(retain && FixedInputResult && SameContinuationPlayer(initialPlayer,firstPlayer) && SameContinuationPlayer(initialPlayer,player))
                Continuation=new CandidateContinuation(firstState,work[0],environment,initialPlayer,firstPlayerStepCompleted,result);
            return result;
        }
        private static PredictionWallSource WallPoint(NpcMotionState[] group,int count)
        {
            for(int i=0;i<count;i++)if(group[i].WallBossCaptured)
            {
                var fact=group[i];if(!fact.WallBossValid)return new PredictionWallSource(fact.WallRelationsComplete,false,default(NpcIdentity),default(MotionRect),0,0,0);
                for(int j=0;j<count;j++)if(group[j].Identity.Equals(fact.WallBossIdentity))
                {var owner=group[j];return new PredictionWallSource(fact.WallRelationsComplete,owner.Active && owner.EffectiveType==113,owner.Identity,owner.Bounds,owner.Direction,owner.Vx,owner.Vy);}
                return default(PredictionWallSource);
            }
            return default(PredictionWallSource);
        }
        private static PredictionGravitySource GravityPoint(PredictionEnvironment e)
        {return new PredictionGravitySource(e.GravitySourceCaptured,e.GravitySourceValid,e.GravitySourceIdentity,e.GravitySourceX,e.GravitySourceY);}
        private static bool RefreshGravity(ref PredictionEnvironment e,NpcMotionState[] group,int count,out PredictionStop stop)
        {
            stop=PredictionStop.None;if(!e.GravitySourceCaptured){stop=PredictionStop.MissingDependency;return false;}if(!e.GravitySourceValid)return true;
            for(int i=0;i<count;i++)if(group[i].Identity.Equals(e.GravitySourceIdentity))
            {
                if(!group[i].Active || group[i].EffectiveType!=266){e.GravitySourceValid=false;e.GravitySourceIdentity=default(NpcIdentity);e.GravitySourceX=e.GravitySourceY=0;return true;}
                e.GravitySourceX=group[i].Bounds.CenterX;e.GravitySourceY=group[i].Bounds.CenterY;return true;
            }
            stop=PredictionStop.MissingDependency;return false;
        }
        private NpcMotionState playerBody;
        private bool playerSettled;
        private bool AdvancePlayer(ref PredictionPlayerMotion p,PredictionEnvironment e,IPredictionTerrain terrain,out PredictionStop stop)
        {
            stop=PredictionStop.None;float priorGravity=p.GravityDirection;
            if(!PlayerMotionContinuation.BeginWallFall(ref p,e,terrain,out stop))return false;
            if(!PlayerMotionContinuation.BeginGravity(ref p,e,out stop))return false;
            if(priorGravity!=p.GravityDirection)playerSettled=false;
            bool wallHandled;if(!PlayerMotionContinuation.BeginWall(ref p,e,terrain,out wallHandled,out stop))return false;
            if(wallHandled){playerSettled=false;return true;}
            // Within this one forecast the sampled local cells are immutable.
            // A grounded idle fixed point needs one collision/liquid query,
            // not the same rectangle 120 times. Reset on EVERY real sample.
            if(playerSettled){if(!PlayerMotionContinuation.ClampBorders(ref p,out stop))return false;PlayerMotionContinuation.CompleteItemCheck(ref p);return true;}
            float oldX=p.X,oldY=p.Y;
            if(!p.Complex || p.Hover)
            {
                PlayerHorizontalMotion.Step(ref p);
                if(p.Hover)
                {
                    float target=p.Up || p.HoldJump?-p.MaxSpeed:p.Down?p.MaxSpeed:0;
                    p.Vy=NpcMotion.Approach(p.Vy,target,p.Acceleration);
                }
                else
                {
                    PlayerVerticalMotion.Step(ref p,e);
                }
            }
            // Geometry shares NPC local acquisition; no native Player/AI is
            // executed. Necessary Wall health is advanced by the preceding
            // pure helper; other complex premises retain their stated limits.
            playerBody=new NpcMotionState{X=p.X,Y=p.Y,Vx=p.Vx,Vy=p.Vy,Width=p.Width,Height=p.Height,Active=true,Friendly=true,
                Wet=p.Wet,Honey=p.Honey,Lava=p.Lava,Shimmer=p.Shimmer,
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
            PlayerVerticalMotion.AfterFluid(ref p,playerBody.Wet,playerBody.Honey,playerBody.Lava,playerBody.Shimmer);
            if(!PlayerMotionContinuation.ClampBorders(ref p,out stop))return false;
            PlayerMotionContinuation.CompleteItemCheck(ref p);
            playerSettled=!p.Complex && !p.Hover && !p.Left && !p.Right && !p.Up && !p.Down && !p.HoldJump && p.Vx==0 && p.Vy==0 && p.X==oldX && p.Y==oldY;
            return true;
        }
        private static bool Valid(NpcMotionState n)
        {return Finite(n.X)&&Finite(n.Y)&&Finite(n.Vx)&&Finite(n.Vy)&&Finite(n.A0)&&(Finite(n.A1) || NpcMoonLeechMotion.Known(n) && n.MoonLeechAnchorCaptured)&&Finite(n.A2)&&Finite(n.A3)&&Finite(n.L0)&&Finite(n.L1)&&Finite(n.L2)&&Finite(n.L3)&&Finite(n.Scale)&&Finite(n.NetOffsetX)&&Finite(n.NetOffsetY)&&Finite(n.Health.DamageMultiplier)&&Finite(n.Health.ShimmerTransparency)&&Finite(n.WaterSpeed)&&Finite(n.HoneySpeed)&&Finite(n.LavaSpeed)&&Finite(n.ShimmerSpeed)&&n.WaterSpeed>=0&&n.HoneySpeed>=0&&n.LavaSpeed>=0&&n.ShimmerSpeed>=0&&n.Width>0&&n.Width<=1024&&n.Height>0&&n.Height<=1024&&Math.Abs(n.Vx)<=512&&Math.Abs(n.Vy)<=512;}
        private static bool BoundedPlayer(PredictionPlayerMotion p)
        {return !p.InvalidMechanism && !(p.WallFactsCaptured && p.ObservationMechanism==128) && (p.Grappled || p.Rope || p.FloatingNow || p.ObservationMechanism!=0 && !p.Hover);}
        private static bool Valid(PredictionPlayerMotion p)
        {return !p.InvalidMechanism&&Finite(p.WindSpeed)&&Finite(p.TrackBoost)&&Finite(p.X)&&Finite(p.Y)&&Finite(p.Vx)&&Finite(p.Vy)&&Finite(p.Gravity)&&Finite(p.Acceleration)&&Finite(p.Slowdown)&&Finite(p.MaxSpeed)&&Finite(p.FastMaxSpeed)&&Finite(p.MaxFall)&&Finite(p.JumpSpeed)&&p.MaxFall>=0&&p.JumpSpeed>=0&&Math.Abs(p.Vx)<=512&&Math.Abs(p.Vy)<=512&&p.GravityDirection*p.GravityDirection==1&&p.Width>0&&p.Height>0&&p.Width<=512&&p.Height<=512&&p.Acceleration>=0&&p.Slowdown>=0&&p.MaxSpeed>=0&&p.FastMaxSpeed>=0;}
        private static bool Finite(float value){return !float.IsNaN(value)&&!float.IsInfinity(value);}
        private static float Clamp(float value,float limit){return Math.Max(-limit,Math.Min(limit,value));}
        private static bool SamePhase(NpcMotionState a,NpcMotionState b)
        {return a.Style==b.Style && a.PositionRelation==b.PositionRelation && a.PositionOwner.Equals(b.PositionOwner) && a.PositionParameter==b.PositionParameter && (NpcEmpressMotion.Known(a)?NpcEmpressMotion.SamePhase(a,b):NpcDeerclopsMotion.Known(a)?NpcDeerclopsMotion.SamePhase(a,b):NpcWindyBalloonMotion.Known(a)?NpcWindyBalloonMotion.SamePhase(a,b):NpcQueenSlimeMotion.Known(a)?NpcQueenSlimeMotion.SamePhase(a,b):NpcMoonLeechMotion.Known(a)?NpcMoonLeechMotion.SamePhase(a,b):NpcCultistRitualMotion.Known(a)?NpcCultistRitualMotion.SamePhase(a,b):NpcMoonLordMotion.Known(a)?NpcMoonLordMotion.SamePhase(a,b):NpcCultistBossMotion.Known(a)?NpcCultistBossMotion.SamePhase(a,b):NpcWallBossMotion.Known(a)?NpcWallBossMotion.SamePhase(a,b):NpcQueenBeeMotion.Known(a)?NpcQueenBeeMotion.SamePhase(a,b):NpcBrainSwarmMotion.Known(a)?NpcBrainSwarmMotion.SamePhase(a,b):NpcGolemMotion.Known(a)?NpcGolemMotion.SamePhase(a,b):NpcBloodNautilusMotion.Known(a)?NpcBloodNautilusMotion.SamePhase(a,b):NpcDarkMageMotion.Known(a)?NpcDarkMageMotion.SamePhase(a,b):NpcAttachedWeaponMotion.Known(a)?NpcAttachedWeaponMotion.SamePhase(a,b):NpcTownCritterMotion.Known(a)?NpcTownCritterMotion.SamePhase(a,b):NpcDiveFlightMotion.Known(a)?NpcDiveFlightMotion.SamePhase(a,b):NpcBetsyMotion.Known(a)?NpcBetsyMotion.SamePhase(a,b):NpcSkeletonBossMotion.Known(a)?NpcSkeletonBossMotion.SamePhase(a,b):NpcCelestialTowerMotion.Known(a)?NpcCelestialTowerMotion.SamePhase(a,b):NpcIceQueenMotion.Known(a)?NpcIceQueenMotion.SamePhase(a,b):NpcPumpkingMotion.Known(a)?NpcPumpkingMotion.SamePhase(a,b):NpcTreeBossMotion.Known(a)?NpcTreeBossMotion.SamePhase(a,b):NpcSantaTankMotion.Known(a)?NpcSantaTankMotion.SamePhase(a,b):NpcKingSlimeMotion.Known(a)?NpcKingSlimeMotion.SamePhase(a,b):NpcSnowmanMotion.Known(a)?NpcSnowmanMotion.SamePhase(a,b):NpcEyeBossMotion.Known(a)?NpcEyeBossMotion.SamePhase(a,b):NpcMechanicalBossMotion.Known(a)?NpcMechanicalBossMotion.SamePhase(a,b):NpcInvasionGroundMotion.Known(a)?NpcInvasionGroundMotion.SamePhase(a,b):NpcInvasionLightningMotion.Known(a)?NpcInvasionLightningMotion.SamePhase(a,b):NpcInvasionFlightMotion.Known(a)?NpcInvasionFlightMotion.SamePhase(a,b):NpcInvasionCrystalMotion.Known(a)?NpcInvasionCrystalMotion.SamePhase(a,b):NpcBigMimicMotion.Known(a)?NpcBigMimicMotion.SamePhase(a,b):NpcPlanteraMotion.Known(a)?NpcPlanteraMotion.SamePhase(a,b):NpcTerrainFlightMotion.Known(a)?NpcTerrainFlightMotion.SamePhase(a,b):NpcRelatedMotion.Known(a)?NpcRelatedMotion.SamePhase(a,b):NpcPhasedFlightMotion.Known(a)?NpcPhasedFlightMotion.SamePhase(a,b):NpcAmbientFlightMotion.Known(a)?NpcAmbientFlightMotion.SamePhase(a,b):NpcFiniteFlightMotion.Known(a)?NpcFiniteFlightMotion.SamePhase(a,b):(a.A0==b.A0 || Math.Abs(a.A0)>8 && Math.Abs(b.A0)>8 && Math.Sign(a.A0)==Math.Sign(b.A0) && Math.Abs(a.A0-b.A0)<=4));}
    }
}
