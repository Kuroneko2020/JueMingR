using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    public enum BlueprintProjectionTrimPhase { Accumulate, Freeze }

    // An accumulation receipt carries counts and this step's SOURCE Tile
    // extensions only. The worker keeps its mutable overlay private; a complete
    // immutable Mask exists only after the one explicitly confirmed Freeze.
    public sealed class BlueprintProjectionTrimPlan
    {
        internal BlueprintProjectionTrimPlan(BlueprintProjectionMask original, BlueprintProjectionMask mask,
            long verifiedVersion, long removed, long removedConstruction, IList<BlueprintProjectionBounds> objectBounds)
        {
            OriginalMask = original; Mask = mask; VerifiedVersion = verifiedVersion;
            RemovedLayerCount = removed; RemovedConstructionLayerCount = removedConstruction;
            var copy = new BlueprintProjectionBounds[objectBounds.Count]; objectBounds.CopyTo(copy, 0);
            ObjectBounds = Array.AsReadOnly(copy);
        }
        public BlueprintProjectionMask OriginalMask { get; }
        public BlueprintProjectionMask Mask { get; }
        public long VerifiedVersion { get; }
        public IReadOnlyList<BlueprintProjectionBounds> ObjectBounds { get; }
        public long RemovedLayerCount { get; }
        public long RemovedConstructionLayerCount { get; }
        public bool Frozen { get { return Mask != null; } }
        public bool HasContent { get { return RemovedLayerCount > 0; } }
    }
}
