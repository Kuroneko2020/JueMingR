using JueMingR.Features.Combat;
using JueMingR.Platform.Combat;

namespace JueMingR.TerrariaHost.Combat
{
    // One solve, one fixed owner-motion premise. Directions keep independent
    // native pose/projectile/history objects; only direction-independent value
    // motion is shared. No real-update cache or input/Contact lives here.
    internal sealed class HostAttackPlayerPrefix
    {
        private readonly PredictionPlayerMotion[] values;
        private readonly PredictionEnvironment environment;
        private readonly IPredictionTerrain terrain;
        private int completed;
        private bool stopped;
        internal HostAttackPlayerPrefix(HostAttackPlayerFrame frame,IPredictionTerrain terrain,int maximumActions)
        {values=new PredictionPlayerMotion[System.Math.Min(NpcPredictionCache.Horizon,maximumActions)+1];values[0]=frame.Motion;environment=frame.Environment;this.terrain=terrain;}
        internal bool TryRead(int actions,out PredictionPlayerMotion motion)
        {
            motion=default(PredictionPlayerMotion);
            if(actions<0 || actions>=values.Length)return false;
            while(completed<actions && !stopped)
            {
                // Advance may throw from deep terrain after changing scratch.
                // Commit only a complete action; the prior value remains intact.
                var next=values[completed];PredictionStop stop;
                if(!PlayerMotionContinuation.Advance(ref next,environment,terrain,out stop)){stopped=true;break;}
                values[++completed]=next;
            }
            if(actions>completed)return false;motion=values[actions];return true;
        }
    }
}
