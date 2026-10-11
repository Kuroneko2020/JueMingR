using JueMingR.Platform.Combat;

namespace JueMingR.TerrariaHost.Combat
{
    // Enumeration does not reinterpret LOS, range or missing models as receiver
    // qualification. The finite actual solver owns clear-line selection proofs.
    internal sealed class HostAttackCandidates
    {
        internal NpcIdentity Reachable;
        internal void Clear(){Reachable=default(NpcIdentity);}
#if DEBUG
        internal int Queries {get{return 0;}}
        internal int Profiles {get{return 0;}}
#endif
    }
}
