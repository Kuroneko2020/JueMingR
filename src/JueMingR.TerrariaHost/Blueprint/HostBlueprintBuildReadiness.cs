using JueMingR.Features.Blueprint;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        // All planning runs on the game thread. This serial only carries a
        // failed read across existing native predicates; it owns no world
        // state, completion, retry permission or cross-update cache.
        private long buildUnavailableReads;

        private bool BlockBuildTarget(BlueprintBuildTarget target, BlueprintBuildWait reason = BlueprintBuildWait.World)
        {
            target.Readiness=BlueprintBuildReadiness.BlockedNow;
            WaitForBuild(target,reason);
            return false;
        }
        private bool DeferBuildTarget(BlueprintBuildTarget target)
        {
            target.Readiness=BlueprintBuildReadiness.Deferred;
            DemandProjectionMatchRange(target.Range);
            return false;
        }
        private bool InvalidBuildTarget(BlueprintBuildTarget target)
        {target.Readiness=BlueprintBuildReadiness.DoneInvalid;return false;}

        // This adapter is limited to a fully evaluated current-world/native
        // predicate. Every world read below it reports unavailable separately;
        // a missing read cannot be cached as a geometry/support rejection.
        private bool FinishBuildPredicate(BlueprintBuildTarget target, bool accepted, long reads,
            BlueprintBuildWait reason = BlueprintBuildWait.World)
        {
            if(accepted)return true;
            if(reads!=buildUnavailableReads)return DeferBuildTarget(target);
            if(target.Readiness==BlueprintBuildReadiness.BlockedNow || target.Readiness==BlueprintBuildReadiness.DoneInvalid)return false;
            return BlockBuildTarget(target,reason);
        }
    }
}
