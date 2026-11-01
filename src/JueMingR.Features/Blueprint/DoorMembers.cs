using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    // Door operation poses change occupied Tile members, not the saved raw
    // object or coincident Wall/Wire/Actuator coordinates. All consumers use
    // this one hinge/wing interpretation, including impossible closed rows.
    public static class BlueprintDoorMembers
    {
        public static bool TryRequirement(BlueprintWorldObject source, IReadOnlyList<CaptureCell> members,
            BlueprintWorldObject observed, int x, int y, out CaptureCell expected,
            BlueprintPlacementCatalog catalog = null, BlueprintSubstitutionPolicy policy = default(BlueprintSubstitutionPolicy))
        {
            expected = default(CaptureCell);
            if (source == null || observed == null || members == null || members.Count != source.MemberCount ||
                source.Family != BlueprintWorldObjectFamily.Door || observed.Family != BlueprintWorldObjectFamily.Door ||
                !(source.SamePlacement(observed) || catalog?.Substitution.AllowsObject(source, observed, policy) == true) || source.Height != 3 || observed.Height != 3 ||
                source.Width < 1 || source.Width > 2 || observed.Width < 1 || observed.Width > 2 ||
                !observed.Contains(x, y)) return false;
            int row = y - observed.WorldY, column = x - observed.WorldX;
            int sourceHinge = source.AnchorX - source.WorldX, actualHinge = observed.AnchorX - observed.WorldX;
            if (sourceHinge < 0 || sourceHinge >= source.Width || actualHinge < 0 || actualHinge >= observed.Width) return false;
            int index = row * source.Width;
            if (source.Width == 2 && observed.Width == 2)
                index += column == actualHinge ? sourceHinge : 1 - sourceHinge;
            expected = members[index];
            // A closed actual Tile cannot realize two different saved paints,
            // coatings, shape or inactive requirements. Never alternate them.
            if (observed.Width == 1 && source.Width == 2 && !SameAppearance(expected, members[index + 1])) return false;
            return (expected.Layers & CaptureLayer.Tile) != 0;
        }

        public static bool SameAppearance(CaptureCell expected, CaptureCell actual)
        { return expected.HalfBrick == actual.HalfBrick && expected.Slope == actual.Slope && expected.Inactive == actual.Inactive &&
            expected.TilePaint == actual.TilePaint && expected.InvisibleTile == actual.InvisibleTile && expected.FullbrightTile == actual.FullbrightTile; }
    }
}
