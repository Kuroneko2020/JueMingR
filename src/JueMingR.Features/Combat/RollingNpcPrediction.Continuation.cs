using System;
using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    public sealed partial class RollingNpcPrediction
    {
        // Candidate-only continuation retains two scalar states, not a second
        // world or 121 copies of every dependency. The ordinary selected
        // predictor keeps its observation/trend owner and never enters here.
        public bool CaptureContinuation {get;set;}
        public CandidateContinuation Continuation {get;private set;}

        private static bool IndependentContinuation(NpcMotionState n,PredictionEnvironment e,PredictionPlayerMotion p)
        {
            // These complete motors do not consume an elapsed-since-capture
            // ordinal. Relations, trend, random and changing-player premises
            // keep the ordinary full calculation. A shared style by itself
            // never establishes that a variant has this property.
            bool motor=n.Style==2 && NpcEyeMotion.Known(n.EffectiveType) || n.Style==14 && NpcBatMotion.Known(n.EffectiveType);
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
                    IndependentContinuation(current,e,p) && environment.Equals(e) && SameContinuationPlayer(player,p) &&
                    ContinuationState(next).SameSample(ContinuationState(current));
            }
            public NpcTrajectory Advance(RollingNpcPrediction owner,NpcMotionState current,long tick,int required,long epoch,PredictionEnvironment e,PredictionPlayerMotion p,IPredictionTerrain terrain,bool advanced)
            {
                if(!Matches(current,tick,required,e,p,advanced))return null;
                // The host has independently validated every old terrain cell.
                // Recompute the next checkpoint from the fresh sample, then
                // append one real tail action. The middle is deterministic from
                // that exact state under the unchanged complete input lease.
                owner.Clear();var one=new[]{current};
                var head=owner.Prepare(one,1,0,tick,1,epoch,e,p,terrain,null,advanced);var headProof=owner.Continuation;
                if(head==null || head.Count!=2 || headProof==null)return null;
                owner.Clear();one[0]=last;
                var tail=owner.Prepare(one,1,0,tick+required-1,1,epoch,e,p,terrain,null,false);var tailProof=owner.Continuation;
                if(tail==null || tail.Count!=2 || tailProof==null)return null;
                var output=new NpcTrajectoryPoint[required+1];output[0]=new NpcTrajectoryPoint(0,current);
                for(int i=1;i<required;i++)output[i]=path[i+1].AtOffset(i);
                output[required]=tail[1].AtOffset(required);
                // This is a newly verified sample plus a newly computed tail;
                // never Republish or a time waiver for an old conditional path.
                var result=new NpcTrajectory(current.Identity,tick,++owner.version,path.Assumptions,PredictionStop.None,output,output.Length,PredictionStrategy.RollingConditional,epoch,path.Quality);
                owner.Continuation=new CandidateContinuation(headProof.next,tailProof.last,e,p,advanced,result);
                return result;
            }
        }
    }
}
