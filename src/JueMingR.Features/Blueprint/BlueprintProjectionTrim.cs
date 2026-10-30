using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    // A complete preview is an immutable proposed delta, never a scene fact.
    // Object extensions contain SOURCE footprints; only Tile is expanded.
    public sealed class BlueprintProjectionTrimPlan
    {
        internal BlueprintProjectionTrimPlan(BlueprintProjectionMask original, BlueprintProjectionMask mask,
            BlueprintProjectionBounds displayBounds, IList<BlueprintProjectionBounds> objectBounds)
        {
            OriginalMask = original; Mask = mask; DisplayBounds = displayBounds;
            var copy = new BlueprintProjectionBounds[objectBounds.Count]; objectBounds.CopyTo(copy, 0);
            ObjectBounds = Array.AsReadOnly(copy);
        }
        public BlueprintProjectionMask OriginalMask { get; }
        public BlueprintProjectionMask Mask { get; }
        public BlueprintProjectionBounds DisplayBounds { get; }
        public IReadOnlyList<BlueprintProjectionBounds> ObjectBounds { get; }
        public long RemovedLayerCount { get { return Mask.RemovedLayerCount - OriginalMask.RemovedLayerCount; } }
        public bool HasContent { get { return RemovedLayerCount > 0; } }
    }
}
