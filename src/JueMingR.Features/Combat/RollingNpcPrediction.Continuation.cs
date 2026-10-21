using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    public sealed partial class RollingNpcPrediction
    {
        // An independently verified continuation retains two scalar states,
        // not a second world or 121 copies of every dependency. Only complete
        // motors without capture-relative clocks may supply this proof.
        public bool CaptureContinuation {get;set;}
        public CandidateContinuation Continuation {get;private set;}
        private readonly NpcMotionState[] continuationSample=new NpcMotionState[1];

        private static bool IndependentContinuation(NpcMotionState n,PredictionEnvironment e,PredictionPlayerMotion p)
        {
            // These complete motors do not consume an elapsed-since-capture
            // ordinal. Relations, trend, random and changing-player premises
            // keep the ordinary full calculation. A shared style by itself
            // never establishes that a variant has this property.
            // Ordinary slime jump/landing/shooting clocks are all in ai/local
            // state; unlike observed fighters they never read elapsed. Source
            // captures the prelude for EVERY type1, including ordinary slimes.
            // Exclude actual possession, not the presence of that read profile.
            bool motor=n.Style==1 && n.A0!=-999 || n.Style==2 && NpcEyeMotion.Known(n.EffectiveType) || n.Style==14 && NpcBatMotion.Known(n.EffectiveType);
            return motor && n.BuffFingerprint==0 && n.UnmodeledDamageTicks==0 && n.TrackingKind<2 && n.PositionRelation==0 &&
                (NpcMotion.Assumptions(n)&(PredictionAssumption.CurrentConnection|PredictionAssumption.RandomRepresentative|PredictionAssumption.ObservedTrackingMotion))==0 &&
                !p.GravitySourceExpected && p.ForcedGravityTicks==0 && !e.GravitySourceValid && e.GravitySources==null &&
                !p.WallSourceExpected && !p.WallGross && !p.WallTongued && p.WallHorrifiedTicks==0 && p.WallTonguedTicks==0 &&
                e.WallSources==null && e.WallPlayers==null && !e.WallSource.Captured;
        }
        private static NpcMotionState ContinuationState(NpcMotionState n)
        {
            // BeforeAi overwrites both values from type/environment/old wet
            // state before any motor reads them. Native capture stores zero.
            // These audited motors never consume the separate trend hints.
            n.Gravity=n.MaxFall=0;n.ObservedAccelerationX=n.ObservedAccelerationY=n.ObservedTurn=0;return n;
        }
        private static bool SameContinuationPlayer(PredictionPlayerMotion a,PredictionPlayerMotion b)
        {
            // IndependentContinuation excludes the only consumers of these
            // local timeline indices. Native resampling starts both at zero.
            a.GravityAction=b.GravityAction=0;a.WallAction=b.WallAction=0;
            return PlayerMotionContinuation.SameFixedInput(a,b,false);
        }
        private static bool NextContinuationPlayer(PredictionPlayerMotion previous,PredictionPlayerMotion current)
        {
            // Native Player.UpdateImmunity executes once between consecutive
            // observations in either completed-player phase. No Wall source,
            // flag or buff is admitted here, so these values are not an input
            // to the audited motion motors; still verify their EXACT natural
            // transition rather than discarding a timer from full equality.
            // A new hit/refresh, HP change or any other sampled input differs
            // from this one-action expectation and revokes the old suffix.
            if(previous.WallImmunityCaptured && previous.WallImmune && --previous.WallImmuneTime<=0)previous.WallImmune=false;
            return SameContinuationPlayer(previous,current);
        }
        public sealed class CandidateContinuation
        {
            private readonly NpcMotionState next,last;
            private readonly PredictionEnvironment environment;
            private readonly PredictionPlayerMotion player;
            private readonly bool playerAdvanced;
            private readonly NpcTrajectory path;
            internal CandidateContinuation(NpcMotionState next,NpcMotionState last,PredictionEnvironment environment,PredictionPlayerMotion player,bool advanced,NpcTrajectory path)
            {this.next=next;this.last=last;this.environment=environment;this.player=player;playerAdvanced=advanced;this.path=path;}
            public bool Matches(NpcMotionState current,long tick,int required,PredictionEnvironment e,PredictionPlayerMotion p,bool advanced)
            {
                return path.Count==required+1 && path.CaptureTick+1==tick && playerAdvanced==advanced &&
                    IndependentContinuation(current,e,p) && environment.Equals(e) && NextContinuationPlayer(player,p) &&
                    ContinuationState(next).SameSample(ContinuationState(current));
            }
            public NpcTrajectory Advance(RollingNpcPrediction owner,NpcMotionState current,long tick,int required,long epoch,PredictionEnvironment e,PredictionPlayerMotion p,IPredictionTerrain terrain,bool advanced)
            {
                if(!Matches(current,tick,required,e,p,advanced))return null;
                // The host has independently validated every old terrain cell.
                // Recompute the next checkpoint from the fresh sample, then
                // append one real tail action. The middle is deterministic from
                // that exact state under the unchanged complete input lease.
                owner.Clear();var one=owner.continuationSample;one[0]=current;
                var head=owner.Prepare(one,1,0,tick,1,epoch,e,p,terrain,null,advanced);var headProof=owner.Continuation;
                if(head==null || head.Count!=2 || headProof==null)return null;
                owner.Clear();one[0]=last;
                var tail=owner.Prepare(one,1,0,tick+required-1,1,epoch,e,p,terrain,null,false);var tailProof=owner.Continuation;
                if(tail==null || tail.Count!=2 || tailProof==null)return null;
                // Trajectory construction copies the publication. Reuse the
                // owner's already private scratch for this assembly instead
                // of allocating a second121-point intermediate every tick.
                var output=owner.points;output[0]=new NpcTrajectoryPoint(0,current);
                for(int i=1;i<required;i++)output[i]=path[i+1].AtOffset(i);
                output[required]=tail[1].AtOffset(required);
                // This is a newly verified sample plus a newly computed tail;
                // never Republish or a time waiver for an old conditional path.
                var origin=path.CalculationToken as ContinuationOrigin;
                var token=new ContinuationOrigin(origin?.OriginalCalculationToken??path.CalculationToken,origin?.OriginalCaptureTick??path.CaptureTick,path.CaptureTick);
                var result=new NpcTrajectory(current.Identity,tick,++owner.version,path.Assumptions,PredictionStop.None,output,required+1,PredictionStrategy.RollingConditional,epoch,path.Quality,calculationToken:token,playerCondition:head.PlayerCondition);
                owner.Continuation=new CandidateContinuation(headProof.next,tailProof.last,e,p,advanced,result);
                // The tail was computed at a future absolute tick, but it is
                // not a live observation. Restore history to the independently
                // sampled present before a later normal/fallback preparation.
                owner.resumable=false;owner.progress=default(Progress);owner.result=null;owner.resultTick=-1;
                owner.previous=owner.trend=current;owner.previousPlayer=p;owner.priorTick=tick;
                owner.previousPlayerAdvanced=advanced;owner.samplePlayerVx=p.Vx;owner.samplePlayerVy=p.Vy;
                return result;
            }
        }
        // New verified observations get their own calculation identity. Keep
        // the original calculation separately without chaining every retired
        // trajectory/token and retaining unbounded history across updates.
        public sealed class ContinuationOrigin
        {
            public object OriginalCalculationToken {get;}
            public long OriginalCaptureTick {get;}
            public long VerifiedFromTick {get;}
            internal ContinuationOrigin(object token,long originalTick,long verifiedFromTick)
            {OriginalCalculationToken=token;OriginalCaptureTick=originalTick;VerifiedFromTick=verifiedFromTick;}
        }
    }
}
