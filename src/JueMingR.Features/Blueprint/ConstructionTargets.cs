namespace JueMingR.Features.Blueprint
{
    // CaptureLayer and projection/observer bits are different domains: Liquid
    // is 16 in CaptureLayer, but 4 in the permanent mask. Display and overlap
    // keep both full domains; only construction/material/Match use this view.
    public static class BlueprintConstructionTargets
    {
        public static CaptureCell Filter(CaptureCell cell)
        { cell.Layers &= ~CaptureLayer.Liquid; return cell; }

        public static ushort FilterBits(ushort layerBits)
        { return (ushort)(layerBits & ~4); }

        public static ushort Layers(CaptureCell cell)
        { return FilterBits(BlueprintProjectionMask.Layers(cell)); }

        public static int Count(CaptureCell cell)
        { return BlueprintProjectionMask.Count(Layers(cell)); }
    }
}
