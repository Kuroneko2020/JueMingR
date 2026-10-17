using JueMingR.Platform.Combat;

namespace JueMingR.Features.Combat
{
    public sealed partial class RollingNpcPrediction
    {
        // One exact sample owns the mutable tail. Only complete world steps
        // are resumable; published trajectories always copy the prefix.
        private bool resumable;
        private Progress progress;
        private object calculationToken;
        private struct Progress
        {
            internal int Length;
            internal bool NeedsPlayer,CanObservePlayer,BoundedPlayer,ObservedPlayer,Restarted;
            internal bool FixedInputs,GravityNeeded,WallNeeded,Retain,ConditionalVisibility;
            internal PredictionPlayerMotion Player,InitialPlayer,FirstPlayer;
            internal PredictionEnvironment GravityEnvironment;
            internal NpcMotionState FirstState;
            internal NpcCelestialTowerMotion.ConsumerWork[] TowerConsumers;
            internal NpcMoonLeechMotion.LeechWork[] MoonLeeches;
        }
    }
}
