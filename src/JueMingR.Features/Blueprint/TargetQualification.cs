using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    public enum BlueprintTargetDisposition { Construction, DisplayOnly, Unresolved }
    public enum BlueprintPlacementResolutionKind { Exact, Ambiguous, Unsupported, None }
    public enum BlueprintTargetReason
    {
        None, Liquid, NoPlayerProducer, CatalogPending, VersionMismatch, ProducerCoverageIncomplete,
        SourceAbiFailure, DeclaredOutputUnsupported, AmbiguousConsumption, TargetShapeUnresolved, UnreliableObject
    }
    public enum BlueprintProducerSource { Direct, TileWand, Flexible, Special }

    // A declaration survives even if its frame selector/grid cannot be compiled.
    // Absence of a compiled rule is therefore never a negative capability proof.
    public sealed class BlueprintPlacementDeclaration
    {
        public BlueprintPlacementDeclaration(BlueprintPlacementKind kind, int useItemType, int tileType, int style,
            int wallType, BlueprintProducerSource source, bool compiled)
        { Kind = kind; UseItemType = useItemType; TileType = tileType; Style = style; WallType = wallType; Source = source; Compiled = compiled; }
        public BlueprintPlacementKind Kind { get; }
        public int UseItemType { get; }
        public int TileType { get; }
        public int Style { get; }
        public int WallType { get; }
        public BlueprintProducerSource Source { get; }
        public bool Compiled { get; }
    }

    // Host freezes coverage only after the canonical item pass has finished.
    // Unimplemented special producers taint their output domains, not unrelated
    // natural targets. An unlocatable source/ABI failure prevents all negatives.
    public sealed class BlueprintProducerCoverage
    {
        private readonly HashSet<int> unknownTiles, unknownWalls;
        public BlueprintProducerCoverage(bool tilesComplete, bool wallsComplete, bool sourceAbiValid,
            IEnumerable<int> unknownTiles = null, IEnumerable<int> unknownWalls = null, string evidence = null,
            int tileTypeCount = int.MaxValue, int wallTypeCount = int.MaxValue)
        {
            TilesComplete = tilesComplete; WallsComplete = wallsComplete; SourceAbiValid = sourceAbiValid;
            this.unknownTiles = new HashSet<int>(unknownTiles ?? Array.Empty<int>());
            this.unknownWalls = new HashSet<int>(unknownWalls ?? Array.Empty<int>()); Evidence = evidence ?? "";
            TileTypeCount = tileTypeCount; WallTypeCount = wallTypeCount;
        }
        public bool TilesComplete { get; }
        public bool WallsComplete { get; }
        public bool SourceAbiValid { get; }
        public string Evidence { get; }
        public int TileTypeCount { get; }
        public int WallTypeCount { get; }
        public bool TileComplete(int tile) { return SourceAbiValid && TilesComplete && tile >= 0 && tile < TileTypeCount && !unknownTiles.Contains(tile); }
        public bool WallComplete(int wall) { return SourceAbiValid && WallsComplete && wall > 0 && wall < WallTypeCount && !unknownWalls.Contains(wall); }
    }

    public readonly struct BlueprintTargetQualification
    {
        public BlueprintTargetQualification(BlueprintTargetDisposition disposition, BlueprintPlacementResolutionKind resolution,
            BlueprintPlacementDescriptor placement, BlueprintTargetReason reason, long capabilityRevision)
        { Disposition = disposition; Resolution = resolution; Placement = placement; Reason = reason; CapabilityRevision = capabilityRevision; }
        public BlueprintTargetDisposition Disposition { get; }
        public BlueprintPlacementResolutionKind Resolution { get; }
        public BlueprintPlacementDescriptor Placement { get; }
        public BlueprintTargetReason Reason { get; }
        public long CapabilityRevision { get; }
        public string MaterialReason
        {
            get
            {
                switch (Reason)
                {
                    case BlueprintTargetReason.CatalogPending: return "放置资料尚未就绪";
                    case BlueprintTargetReason.VersionMismatch: return "目标版本的放置资料尚未确认";
                    case BlueprintTargetReason.AmbiguousConsumption: return "目标存在多种不同的材料解释";
                    case BlueprintTargetReason.UnreliableObject: return "对象尚未完整确认";
                    case BlueprintTargetReason.DeclaredOutputUnsupported: return "可摆目标的形态或材料步骤尚未确定";
                    case BlueprintTargetReason.TargetShapeUnresolved: return "目标形态的放置解释尚未确认";
                    default: return "目标的玩家放置能力尚未确认";
                }
            }
        }
    }

    public readonly struct BlueprintConstructionSelection
    {
        public BlueprintConstructionSelection(ushort requiredBits, ushort unknownBits, long capabilityRevision)
        { RequiredBits = (ushort)(requiredBits & ~4); UnknownBits = (ushort)(unknownBits & RequiredBits); CapabilityRevision = capabilityRevision; }
        // Unknown is retained in RequiredBits. It must reach the world proof's
        // Unknown lane even when a same-looking world object already exists.
        public ushort RequiredBits { get; }
        public ushort UnknownBits { get; }
        public long CapabilityRevision { get; }
        public CaptureCell Apply(CaptureCell cell) { return BlueprintConstructionTargets.Filter(cell, this); }
    }
}
