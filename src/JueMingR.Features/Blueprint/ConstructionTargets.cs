namespace JueMingR.Features.Blueprint
{
    // CaptureLayer and projection/observer bits are different domains: Liquid
    // is 16 in CaptureLayer, but 4 in the permanent mask. Display and overlap
    // keep both full domains. A target-aware selection is required to exclude
    // natural output identities; the bit-only helpers only normalize Liquid.
    public static class BlueprintConstructionTargets
    {
        public static CaptureCell Filter(CaptureCell cell)
        { cell.Layers &= ~CaptureLayer.Liquid; return cell; }

        public static CaptureCell Filter(CaptureCell cell, BlueprintConstructionSelection selection)
        {
            ushort bits = selection.RequiredBits;
            if ((bits & 1) == 0) cell = cell.WithoutTile();
            if ((bits & 2) == 0) cell.Layers &= ~CaptureLayer.Wall;
            cell.Layers &= ~CaptureLayer.Liquid;
            cell.Wires &= (byte)(bits >> 4);
            if (cell.Wires == 0) cell.Layers &= ~CaptureLayer.Wire;
            if ((bits & 8) == 0) { cell.Layers &= ~CaptureLayer.Actuator; cell.Actuator = false; }
            return cell;
        }

        public static BlueprintConstructionSelection Select(CaptureCell cell, BlueprintPlacementCatalog catalog, CaptureObject? obj = null)
        {
            ushort bits = FilterBits(BlueprintProjectionMask.Layers(cell)), unknown = 0;
            if ((bits & 1) != 0)
            {
                var tile = catalog == null ? Pending : catalog.QualifyTile(cell, obj);
                if (tile.Disposition == BlueprintTargetDisposition.DisplayOnly) bits &= 0xfe;
                else if (tile.Disposition == BlueprintTargetDisposition.Unresolved) unknown |= 1;
            }
            if ((bits & 2) != 0)
            {
                var wall = catalog == null ? Pending : catalog.QualifyWall(cell.WallType);
                if (wall.Disposition == BlueprintTargetDisposition.DisplayOnly) bits &= 0xfd;
                else if (wall.Disposition == BlueprintTargetDisposition.Unresolved) unknown |= 2;
            }
            return new BlueprintConstructionSelection(bits, unknown, catalog?.Revision ?? 0);
        }
        internal static BlueprintTargetQualification Pending { get { return new BlueprintTargetQualification(BlueprintTargetDisposition.Unresolved,
            BlueprintPlacementResolutionKind.None, null, BlueprintTargetReason.CatalogPending, 0); } }

        public static ushort FilterBits(ushort layerBits)
        { return (ushort)(layerBits & ~4); }

        public static ushort Layers(CaptureCell cell)
        { return FilterBits(BlueprintProjectionMask.Layers(cell)); }

        public static int Count(CaptureCell cell)
        { return BlueprintProjectionMask.Count(Layers(cell)); }

        public static int Count(CaptureCell cell, BlueprintConstructionSelection selection)
        { return BlueprintProjectionMask.Count((ushort)(BlueprintProjectionMask.Layers(cell) & selection.RequiredBits)); }
    }
}
