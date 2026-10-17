namespace JueMingR.TerrariaHost.Combat
{
    internal sealed partial class HostAttackObstacles
    {
        private HostHarpyAttack.State harpyBatchState;
        // One explicit held-ammo capture owns the native charm/player prefix.
        // Candidate forks share that immutable premise, not a second scene or
        // a newly acquired input. Draw only checks captured scalar members.
        internal HostHarpyAttack.State CaptureHarpyBatch(Terraria.Player player)
        {
            // A launch pose can be a private future body. The lease binds
            // actual current identity/input; the batch alone advances poses.
            harpyBatchState=HostHarpyAttack.CaptureBatchState(Terraria.Main.player[player.whoAmI],timeline,this);return harpyBatchState;
        }
        private bool HarpyBatchMembersCurrent {get{return harpyBatchState==null || harpyBatchState.MembersCurrent;}}
        private bool HarpyBatchDependenciesCurrent {get{return harpyBatchState==null || harpyBatchState.DependenciesCurrent;}}
    }
}
