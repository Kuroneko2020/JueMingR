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
            IReadOnlyList<BlueprintProjectionSourceRange> matchRanges, BlueprintProjectionGeometryDirectory directory,
            string error = null, BlueprintProjectionView view = null)
            : this(identity, catalog, mask, total, effective, unknown, sourceRanges, matchRanges, directory, error, view, BlueprintProjectionFailureScope.None, null, null) { }
        internal BlueprintProjectionQualificationReceipt(BlueprintProjectionIdentity identity, BlueprintPlacementCatalog catalog,
            BlueprintProjectionMask mask, long total, long effective, long unknown, IReadOnlyList<BlueprintProjectionSourceRange> sourceRanges,
            IReadOnlyList<BlueprintProjectionSourceRange> matchRanges, BlueprintProjectionGeometryDirectory directory,
            string error, BlueprintProjectionView view, BlueprintProjectionFailureScope failureScope, string failureCode, string failureDetail)
        {
            Identity = identity; Catalog = catalog; Mask = mask; TotalRequiredLayerCount = total;
            EffectiveRequiredLayerCount = effective; EffectiveUnknownLayerCount = unknown;
            SourceRanges = sourceRanges; MatchRanges = matchRanges; Error = error; View = view;
            FailureScope = failureScope; FailureCode = failureCode; FailureDetail = failureDetail;
            if(error==null && (directory==null || !ReferenceEquals(directory.Sources,sourceRanges)))throw new ArgumentException("blueprint-directory-source-identity",nameof(directory));
            GeometryDirectory=directory;
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
        public BlueprintProjectionGeometryDirectory GeometryDirectory { get; }
        public string Error { get; }
        public BlueprintProjectionFailureScope FailureScope { get; }
        public string FailureCode { get; }
        public string FailureDetail { get; }
        internal long EstimatedBytes { get { return DescriptorBytes + 2L * ((Error?.Length ?? 0) + (FailureCode?.Length ?? 0) + (FailureDetail?.Length ?? 0)); } }
        public BlueprintProjectionView View { get; }
    }
}
