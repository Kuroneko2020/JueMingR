using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    // A derived receipt binds the immutable producer catalog and the exact raw
    // mask requested by Host. Neither value changes the template's saved key.
    public sealed class BlueprintProjectionQualificationReceipt
    {
        internal const long DescriptorBytes = 256;
        internal BlueprintProjectionQualificationReceipt(BlueprintProjectionIdentity identity, BlueprintPlacementCatalog catalog,
            BlueprintProjectionMask mask, long total, long effective, long unknown, IReadOnlyList<BlueprintProjectionSourceRange> sourceRanges,
            IReadOnlyList<BlueprintProjectionSourceRange> matchRanges, string error = null)
        {
            Identity = identity; Catalog = catalog; Mask = mask; TotalRequiredLayerCount = total;
            EffectiveRequiredLayerCount = effective; EffectiveUnknownLayerCount = unknown;
            SourceRanges = sourceRanges; MatchRanges = matchRanges; Error = error;
        }
        public BlueprintProjectionIdentity Identity { get; }
        public BlueprintPlacementCatalog Catalog { get; }
        public long CapabilityRevision { get { return Catalog.Revision; } }
        public int ClassifierVersion { get { return BlueprintPlacementCatalog.ClassifierVersion; } }
        public BlueprintProjectionMask Mask { get; }
        public long TotalRequiredLayerCount { get; }
        public long EffectiveRequiredLayerCount { get; }
        public long EffectiveUnknownLayerCount { get; }
        public IReadOnlyList<BlueprintProjectionSourceRange> SourceRanges { get; }
        public IReadOnlyList<BlueprintProjectionSourceRange> MatchRanges { get; }
        public string Error { get; }
    }
}
