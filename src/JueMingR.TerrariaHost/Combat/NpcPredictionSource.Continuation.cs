using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;

namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class NpcPredictionSource
    {
        // BeforeNpc and completed-world observations have different player
        // ordering. Each retains one bounded proof; neither owns input rights
        // or replaces the ordinary Source observation/trend history.
        private readonly SelectedContinuation[] selectedContinuations=new SelectedContinuation[2];
        internal int SelectedContinuations {get;private set;}
        private sealed class SelectedContinuation
        {
            internal NpcMotionState Sample;
            internal PredictionEnvironment Environment;
            internal PredictionPlayerMotion Player;
            internal PredictionTerrain.CellLease Terrain;
            internal NpcTrajectory Path;
            internal RollingNpcPrediction.CandidateContinuation Continuation;
            internal int PlannedHorizon;
            internal bool PlayerAdvanced;
            internal long Epoch;
        }
        private void ClearSelectedContinuations()
        {selectedContinuations[0]=selectedContinuations[1]=null;}
        private NpcTrajectory ContinueSelected(long tick,bool beforeNpc,bool playerAdvanced,int count,int selected,int required,PredictionEnvironment environment,PredictionPlayerMotion player)
        {
            if(candidateOnly || count!=1 || selected!=0 || !motionRoles[0])return null;
            var prior=selectedContinuations[beforeNpc?1:0];
            if(prior==null || prior.Epoch!=epoch || prior.PlannedHorizon!=sampleHorizon || prior.PlayerAdvanced!=playerAdvanced || prior.Path.Count<=required)return null;
            // A shorter reader can consume the existing longer immutable
            // publication only after this call resampled the full premises.
            if(prior.Path.CaptureTick==tick && prior.Sample.SameSample(states[0]) && prior.Environment.Equals(environment) && prior.Player.SameSample(player) && prior.Terrain.Current)
                return prior.Path;
            int horizon=prior.Path.Count-1;
            if(!prior.Continuation.Matches(states[0],tick,horizon,environment,player,playerAdvanced) || !IncludeIceContinuation(prior.Continuation) || !Terrain.BeginContinuation(prior.Terrain))return null;
            try
            {
                var result=prior.Continuation.Advance(rolling,states[0],tick,horizon,epoch,environment,player,Terrain,playerAdvanced);
                if(result!=null)SelectedContinuations++;return result;
            }
            finally{Terrain.EndContinuation();}
        }
        private void RememberSelected(bool beforeNpc,bool playerAdvanced,int count,int selected,PredictionEnvironment environment,PredictionPlayerMotion player,NpcTrajectory path)
        {
            if(candidateOnly)return;
            int key=beforeNpc?1:0;var continuation=rolling.Continuation;
            if(count!=1 || selected!=0 || !motionRoles[0] || continuation==null || path==null || path.Stop!=PredictionStop.None || path.BudgetPaused)
            {selectedContinuations[key]=null;return;}
            var prior=selectedContinuations[key];
            if(prior!=null && ReferenceEquals(prior.Path,path))return;
            selectedContinuations[key]=new SelectedContinuation{Sample=states[0],Environment=environment,Player=player,Terrain=Terrain.FreezeCells(),Path=path,Continuation=continuation,PlannedHorizon=sampleHorizon,PlayerAdvanced=playerAdvanced,Epoch=epoch};
        }
    }
}
