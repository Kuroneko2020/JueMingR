using System;
using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class NpcPredictionSource
    {
        // Entries are indexed by native slot and capture phase, not a rotating
        // two-candidate ring: three static misses must not evict one another on
        // every pass. The TOTAL copied dependencies/cells remain bounded. No
        // entry owns a Source, rolling history or mutable prediction world.
        private const int CandidateStateBudget=1024,CandidateCellBudget=8192;
        private CandidateInputs[] candidateInputs;
        private int candidateStateCount,candidateCellCount,candidateMissReuses,candidateProofBudgetMisses,candidateContinuations;
        private bool candidateReuseMiss;
        internal int CandidateMissReuses {get{return candidateSource?.candidateMissReuses??0;}}
        internal int CandidateProofBudgetMisses {get{return candidateSource?.candidateProofBudgetMisses??0;}}
        internal int CandidateContinuations {get{return candidateSource?.candidateContinuations??0;}}

        private sealed class CandidateInputs
        {
            internal readonly NpcIdentity Identity;
            internal readonly NpcMotionState[] States;
            internal readonly bool[] Roles;
            internal readonly PredictionEnvironment Environment;
            internal readonly PredictionPlayerMotion Player;
            internal readonly PredictionTerrain.CellLease Terrain;
            internal readonly int Selected,Required;
            internal readonly bool PlayerAdvanced;
            internal readonly double Priority;
            internal readonly RollingNpcPrediction.CandidateContinuation Continuation;
            internal int StateCost {get{return States.Length+(Continuation==null?0:2);}}
            internal CandidateInputs(NpcIdentity identity,NpcMotionState[] states,bool[] roles,int count,int selected,int required,
                PredictionEnvironment environment,PredictionPlayerMotion player,bool playerAdvanced,PredictionTerrain.CellLease terrain,double priority,RollingNpcPrediction.CandidateContinuation continuation)
            {
                Identity=identity;States=new NpcMotionState[count];Roles=new bool[count];Array.Copy(states,States,count);Array.Copy(roles,Roles,count);
                Selected=selected;Required=required;Environment=environment;Player=player;PlayerAdvanced=playerAdvanced;Terrain=terrain;Priority=priority;Continuation=continuation;
            }
            internal bool Same(NpcIdentity identity,NpcMotionState[] states,bool[] roles,int count,int selected,int required,
                PredictionEnvironment environment,PredictionPlayerMotion player,bool playerAdvanced)
            {
                if(!Identity.Equals(identity) || States.Length!=count || Selected!=selected || Required!=required || PlayerAdvanced!=playerAdvanced ||
                    !Environment.Equals(environment) || !PlayerMotionContinuation.SameFixedInput(Player,player,environment.WallPlayers!=null || environment.WallSources!=null || environment.WallSource.Captured))return false;
                for(int i=0;i<count;i++)if(Roles[i]!=roles[i] || !States[i].SameSample(states[i]))return false;
                return Terrain.Current;
            }
        }
        private void ClearCandidateProofs()
        {
            if(candidateInputs!=null)Array.Clear(candidateInputs,0,candidateInputs.Length);
            candidateStateCount=candidateCellCount=0;
        }
        private static int CandidateKey(NpcIdentity identity,bool beforeNpc)
        {return identity.Slot*2+(beforeNpc?1:0);}
        private bool ReuseCandidateInputs(NpcIdentity identity,bool beforeNpc,bool playerAdvanced,int count,int selected,int required,PredictionEnvironment environment,PredictionPlayerMotion player)
        {
            if(candidateInputs==null)return false;
            int key=CandidateKey(identity,beforeNpc);if(key<0 || key>=candidateInputs.Length)return false;
            var prior=candidateInputs[key];
            if(prior==null || !prior.Same(identity,states,motionRoles,count,selected,required,environment,player,playerAdvanced))return false;
            candidateMissReuses++;return true;
        }
        private void DropCandidate(int key)
        {
            var prior=candidateInputs[key];if(prior==null)return;
            candidateStateCount-=prior.StateCost;candidateCellCount-=prior.Terrain.Count;candidateInputs[key]=null;
        }
        private NpcTrajectory ContinueCandidate(NpcIdentity identity,long tick,bool beforeNpc,bool playerAdvanced,int count,int selected,int required,PredictionEnvironment environment,PredictionPlayerMotion player)
        {
            if(count!=1 || selected!=0 || !motionRoles[0] || candidateInputs==null)return null;
            int key=CandidateKey(identity,beforeNpc);if(key<0 || key>=candidateInputs.Length)return null;
            var prior=candidateInputs[key];var continuation=prior?.Continuation;
            if(continuation==null || !continuation.Matches(states[0],tick,required,environment,player,playerAdvanced) || !IncludeIceContinuation(continuation) || !Terrain.BeginContinuation(prior.Terrain))return null;
            try
            {
                var path=continuation.Advance(rolling,states[0],tick,required,epoch,environment,player,Terrain,playerAdvanced);
                if(path!=null)candidateContinuations++;return path;
            }
            finally{Terrain.EndContinuation();}
        }
        private void RememberCandidateInputs(NpcIdentity identity,bool beforeNpc,bool playerAdvanced,int count,int selected,int required,PredictionEnvironment environment,PredictionPlayerMotion player)
        {
            if(candidateInputs==null)candidateInputs=new CandidateInputs[states.Length*2];
            int key=CandidateKey(identity,beforeNpc);if(key<0 || key>=candidateInputs.Length)return;
            // A successful forecast consumed every necessary cell. Failed or
            // short work never reaches this call: absent/null cells have no
            // complete unchanged lease and cannot become a remembered miss.
            var lease=Terrain.FreezeCells();DropCandidate(key);var continuation=rolling.Continuation;int stateCost=count+(continuation==null?0:2);
            if(stateCost>CandidateStateBudget || lease.Count>CandidateCellBudget){candidateProofBudgetMisses++;return;}
            var observer=Terraria.Main.LocalPlayer;
            double dx=states[selected].Bounds.CenterX-observer.Center.X,dy=states[selected].Bounds.CenterY-observer.Center.Y;
            double priority=dx*dx+dy*dy;
            while(candidateStateCount+stateCost>CandidateStateBudget || candidateCellCount+lease.Count>CandidateCellBudget)
            {
                int farthest=-1;double distance=priority;
                for(int i=0;i<candidateInputs.Length;i++)if(candidateInputs[i]!=null && candidateInputs[i].Priority>=distance){farthest=i;distance=candidateInputs[i].Priority;}
                // Prefer already-proved nearer candidates. Budget rejection
                // affects work only, never the caller's distance-first order.
                if(farthest<0){candidateProofBudgetMisses++;return;}
                DropCandidate(farthest);
            }
            candidateInputs[key]=new CandidateInputs(identity,states,motionRoles,count,selected,required,environment,player,playerAdvanced,lease,priority,continuation);
            candidateStateCount+=stateCost;candidateCellCount+=lease.Count;
        }
    }
}
