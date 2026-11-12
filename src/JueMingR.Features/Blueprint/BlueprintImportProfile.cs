namespace JueMingR.Features.Blueprint
{
    public interface IBlueprintEntityImportProfile
    { bool ValidEntityItem(int tileType, int itemType, int frameY); }

    // Published on the game thread with the mirror layout snapshot. All calls
    // after admission use copied metadata only, including ordinary-tile classification.
    public interface IBlueprintImportProfile
    {
        bool Ready { get; }
        int TileCount { get; }
        int WallCount { get; }
        BlueprintObjectShape DescribeImport(CaptureCell cell);
    }
}
