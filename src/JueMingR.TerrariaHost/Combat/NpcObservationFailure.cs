using System;
using JueMingR.Platform.Combat;

namespace JueMingR.TerrariaHost.Combat
{
    // Only an explicitly validated per-NPC owned observation may use this
    // boundary. Unknown exceptions, terrain/global arrays and selection are
    // shared faults; never relabel them merely because a target was selected.
    internal sealed class NpcObservationFailure : Exception
    {
        internal readonly NpcIdentity Identity;
        internal NpcObservationFailure(NpcIdentity identity):base("NPC-owned observation arrays are unavailable."){Identity=identity;}
    }
}
