using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using Terraria;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        // One real destructive native step may own one unfinished foreground
        // restoration. This is an authority/resource dependency, not a second
        // completion ledger or an Item reservation. No decoded window is copied.
        private sealed class AppearanceRestoration
        {
            internal BlueprintProjectionPlacement Owner;
            internal BlueprintProjectionIdentity Identity;
            internal BlueprintProjectionMask Mask;
            internal int OriginX, OriginY, X, Y, DoorColumn;
            internal long Catalog, Policy, Flight, Token;
            internal bool Mirrored, Readable;
            internal CaptureCell Expected;
            internal BlueprintWorldObject Body;
            internal BlueprintDoorBuildProof Door;
            // An operating door maps one saved member to at most two actual
            // columns. All other foregrounds retain exactly one coordinate.
            internal int Count, X0, Y0, X1, Y1;
            internal CaptureCell Expected0, Expected1, Actual0, Actual1;
            internal BlueprintAppearanceMaterials Need0, Need1;
            // Only currently blocked consumption thresholds are retained.
            // Two door columns contain at most three colors plus two coatings.
            internal readonly Dictionary<int, int> Thresholds = new Dictionary<int, int>();
        }

        // This pre-consumer sample grants no restoration responsibility. Only
        // the matching returned native generation and an actual lost effect
        // below can establish one; rejected and stale attempts cannot do so.
        private long appearanceScrapeFlight, appearanceScrapeToken;
        private CaptureCell appearanceScrapeBefore, appearanceScrapeExpected;
        private int appearanceScrapeDoorColumn;
        private BlueprintWorldObject appearanceScrapeBody;
        private BlueprintDoorBuildProof appearanceScrapeDoor;
        private AppearanceRestoration appearanceRestoration;
        private long appearanceRestorationRevision;
        private BlueprintMaterialInventory appearanceRestorationSupplyInventory;

        private static bool ForegroundRestorationDestruction(BlueprintAppearanceStep step, CaptureCell expected, CaptureCell actual)
        {
            return step.Action == BlueprintAppearanceAction.ScrapeTilePaint && actual.TilePaint != 0 && expected.TilePaint != 0 ||
                step.Action == BlueprintAppearanceAction.ScrapeTileCoatings &&
                (actual.InvisibleTile || actual.FullbrightTile) && (expected.InvisibleTile || expected.FullbrightTile);
        }

        private void CaptureAppearanceRestorationAttempt(BuildFlight flight)
        {
            if (flight.Operation?.Kind != BlueprintBuildOperationKind.Scrape) return;
            BlueprintWorldMatchSample actual; CaptureCell expected; bool same; BlueprintWorldObject form; BlueprintDoorBuildProof proof;
            if (!ReadBuildCell(flight.Target.WorldX, flight.Target.WorldY, out actual) ||
                !TryAppearanceTarget(flight.Target, actual, out expected, out same, out form, out proof)) return;
            var step = new BlueprintAppearanceStep((BlueprintAppearanceAction)flight.Operation.Mode);
            if (!ForegroundRestorationDestruction(step, expected, actual.Cell)) return;
            appearanceScrapeFlight = flight.Generation; appearanceScrapeToken = flight.Token;
            appearanceScrapeBefore = actual.Cell; appearanceScrapeExpected = expected;
            appearanceScrapeBody = proof?.Source ?? flight.Target.Body ?? actual.Object; appearanceScrapeDoor = proof;
            appearanceScrapeDoorColumn = actual.Object?.Family == BlueprintWorldObjectFamily.Door && actual.Object.Width == 2 ?
                (flight.Target.WorldX == actual.Object.AnchorX ? 0 : 1) : -1;
        }

        private void ObserveAppearanceRestorationApplied(BuildFlight flight)
        {
            if (flight.Operation?.Kind == BlueprintBuildOperationKind.Scrape && appearanceScrapeFlight == flight.Generation &&
                appearanceScrapeToken == flight.Token)
            {
                BlueprintWorldMatchSample actual;
                if (ReadBuildCell(flight.Target.WorldX, flight.Target.WorldY, out actual))
                {
                    bool lost = appearanceScrapeBefore.TilePaint != 0 && appearanceScrapeExpected.TilePaint != 0 && actual.Cell.TilePaint == 0 ||
                        appearanceScrapeBefore.InvisibleTile && appearanceScrapeExpected.InvisibleTile && !actual.Cell.InvisibleTile ||
                        appearanceScrapeBefore.FullbrightTile && appearanceScrapeExpected.FullbrightTile && !actual.Cell.FullbrightTile;
                    if (lost && appearanceRestoration == null)
                    {
                        var target = flight.Target;
                        appearanceRestoration = new AppearanceRestoration { Owner = target.Range.Owner, Identity = target.Identity, Mask = target.Mask,
                            OriginX = target.OriginX, OriginY = target.OriginY, X = target.WorldX, Y = target.WorldY,
                            Catalog = target.PlacementRevision, Policy = target.PolicyRevision, Flight = flight.Generation, Token = flight.Token,
                            Mirrored = target.Range.Owner.MirroredHorizontally, Expected = appearanceScrapeExpected,
                            Body = appearanceScrapeBody, Door = appearanceScrapeDoor, DoorColumn = appearanceScrapeDoorColumn };
                        appearanceRestorationRevision++;
                    }
                }
                appearanceScrapeFlight = appearanceScrapeToken = 0; appearanceScrapeBody = null; appearanceScrapeDoor = null;
            }
            RefreshAppearanceRestoration();
        }

        private void ClearAppearanceRestoration()
        {
            if (appearanceRestoration != null) appearanceRestorationRevision++;
            appearanceRestoration = null; appearanceRestorationSupplyInventory = null;
            appearanceScrapeFlight = appearanceScrapeToken = 0; appearanceScrapeBody = null; appearanceScrapeDoor = null;
        }

        private void RetireAppearanceRestoration(BlueprintProjectionIdentity identity)
        { if (appearanceRestoration?.Identity.Equals(identity) == true) ClearAppearanceRestoration(); }

        private bool AppearanceRestorationIdentityCurrent(AppearanceRestoration value)
        {
            var owner = value.Owner;
            return HasSession && value.Identity.SessionGeneration == generation && value.Catalog == MaterialCatalog?.Revision &&
                value.Policy == substitutionRevision && projectionScene.Contains(owner) && owner.Identity.Equals(value.Identity) &&
                ReferenceEquals(owner.Mask, value.Mask) && owner.OriginX == value.OriginX && owner.OriginY == value.OriginY &&
                owner.MirroredHorizontally == value.Mirrored;
        }

        private static bool RestoredForeground(CaptureCell expected, CaptureCell actual)
        {
            return actual.TilePaint == expected.TilePaint && actual.InvisibleTile == expected.InvisibleTile &&
                actual.FullbrightTile == expected.FullbrightTile;
        }

        private static bool SameRestorationAppearance(CaptureCell left, CaptureCell right)
        {
            return left.Layers == right.Layers && left.TileType == right.TileType && left.WallType == right.WallType &&
                RestoredForeground(left, right) && left.WallPaint == right.WallPaint && left.InvisibleWall == right.InvisibleWall &&
                left.FullbrightWall == right.FullbrightWall;
        }

        private BlueprintAppearanceMaterials AppearanceRestorationMaterials(CaptureCell expected, BlueprintWorldMatchSample actual)
        {
            bool wallSame = (expected.Layers & CaptureLayer.Wall) != 0 &&
                BlueprintMaterialSteps.SameBody(expected, actual, MaterialCatalog, true, substitutionPolicy) == BlueprintBodyState.Same;
            // A missing/different wall is not a prerequisite for restoring this
            // still valid foreground. Bill only the current legal wall chain.
            if (!wallSame) expected.Layers &= ~CaptureLayer.Wall;
            return BlueprintConstructionAppearance.RequiredMaterials(expected, actual.Cell, true, wallSame);
        }

        private void RestorationReadUnavailable(AppearanceRestoration value)
        {
            if (value.Readable) { value.Readable = false; appearanceRestorationRevision++; }
        }

        private void RefreshAppearanceRestoration()
        {
            var value = appearanceRestoration; if (value == null) return;
            if (!AppearanceRestorationIdentityCurrent(value)) { ClearAppearanceRestoration(); return; }
            int count = 0, x0 = value.X, y0 = value.Y, x1 = 0, y1 = 0;
            CaptureCell expected0 = value.Expected, expected1 = default(CaptureCell);
            BlueprintWorldMatchSample actual0 = default(BlueprintWorldMatchSample), actual1 = default(BlueprintWorldMatchSample);
            if (value.Door != null)
            {
                var source = value.Door.Source; BlueprintWorldMatchSample hinge;
                if (!ReadBuildCell(source.AnchorX, source.AnchorY, out hinge) || hinge.ObjectUnknown)
                { RestorationReadUnavailable(value); return; }
                // Destroying/replacing the original foreground revokes its
                // restoration authority; no old coordinate is rolled back.
                if (hinge.Object == null || !SameSubstitutionBody(source, hinge.Object)) { ClearAppearanceRestoration(); return; }
                BlueprintWorldObject observed;
                if (!TryReadCompleteDoor(source, out observed)) { RestorationReadUnavailable(value); return; }
                int row = value.OriginY + value.Expected.Y - source.WorldY;
                if (row < 0 || row >= observed.Height) { ClearAppearanceRestoration(); return; }
                for (int column = 0; column < observed.Width; column++)
                {
                    int x = observed.WorldX + column, y = observed.WorldY + row; CaptureCell mapped;
                    // A saved closed member can address both open columns,
                    // but a real scrape of an already open door damages only
                    // its hinge or wing. Do not authorize the untouched peer.
                    if (observed.Width == 2 && value.DoorColumn >= 0 && (x == observed.AnchorX ? 0 : 1) != value.DoorColumn) continue;
                    if (!BlueprintDoorMembers.TryRequirement(source, value.Door.Members, observed, x, y, out mapped, MaterialCatalog, substitutionPolicy))
                    { RestorationReadUnavailable(value); return; }
                    if (source.Width == 2 && observed.Width == 2 && value.DoorColumn >= 0 && mapped.X != value.Expected.X) continue;
                    mapped.Layers = CaptureLayer.Tile;
                    CaptureCell wall; bool known;
                    if (value.Door.WallKnown && value.Door.WallX == x && value.Door.WallY == y)
                    { if (value.Door.Wall.HasValue) mapped = WithCoordinateWall(mapped, value.Door.Wall.Value); }
                    else
                    {
                        bool selected = TryCoordinateWall(value.Owner, x, y, out wall, out known);
                        if (!known) { RestorationReadUnavailable(value); return; }
                        if (selected) mapped = WithCoordinateWall(mapped, wall);
                    }
                    BlueprintWorldMatchSample sample;
                    if (!ReadBuildCell(x, y, out sample)) { RestorationReadUnavailable(value); return; }
                    if (count++ == 0) { x0 = x; y0 = y; expected0 = mapped; actual0 = sample; }
                    else { x1 = x; y1 = y; expected1 = mapped; actual1 = sample; }
                }
            }
            else
            {
                if (!ReadBuildCell(x0, y0, out actual0) || actual0.ObjectUnknown) { RestorationReadUnavailable(value); return; }
                bool same = value.Body != null ? SameSubstitutionBody(value.Body, actual0.Object) :
                    BlueprintMaterialSteps.SameBody(expected0, actual0, MaterialCatalog, policy: substitutionPolicy) == BlueprintBodyState.Same;
                if (!same) { ClearAppearanceRestoration(); return; }
                count = 1;
            }
            if (count == 0) { RestorationReadUnavailable(value); return; }
            if (RestoredForeground(expected0, actual0.Cell) && (count == 1 || RestoredForeground(expected1, actual1.Cell)))
            { ClearAppearanceRestoration(); return; }
            bool changed = !value.Readable || value.Count != count || value.X0 != x0 || value.Y0 != y0 ||
                value.X1 != x1 || value.Y1 != y1 || !SameRestorationAppearance(value.Expected0, expected0) ||
                !SameRestorationAppearance(value.Actual0, actual0.Cell) || count == 2 &&
                (!SameRestorationAppearance(value.Expected1, expected1) || !SameRestorationAppearance(value.Actual1, actual1.Cell));
            value.Readable = true; value.Count = count; value.X0 = x0; value.Y0 = y0; value.X1 = x1; value.Y1 = y1;
            value.Expected0 = expected0; value.Expected1 = expected1; value.Actual0 = actual0.Cell; value.Actual1 = actual1.Cell;
            value.Need0 = AppearanceRestorationMaterials(expected0, actual0);
            value.Need1 = count == 2 ? AppearanceRestorationMaterials(expected1, actual1) : default(BlueprintAppearanceMaterials);
            if (changed) { value.Thresholds.Clear(); appearanceRestorationRevision++; }
        }

        private int GetAppearanceRestorationNeed(int type)
        {
            var value = appearanceRestoration;
            return value == null ? 0 : value.Need0.Count(type, MaterialCatalog) + value.Need1.Count(type, MaterialCatalog);
        }

        private bool ExactAppearanceRestorationStep(BlueprintBuildTarget target, BlueprintBuildOperation operation)
        {
            var value = appearanceRestoration;
            if (value == null || !value.Readable || target == null || !target.Identity.Equals(value.Identity) ||
                !ReferenceEquals(target.Mask, value.Mask) || target.OriginX != value.OriginX || target.OriginY != value.OriginY ||
                operation == null || operation.Kind != BlueprintBuildOperationKind.PaintTile && operation.Kind != BlueprintBuildOperationKind.PaintWall) return false;
            bool first = target.WorldX == value.X0 && target.WorldY == value.Y0;
            if (!first && (value.Count < 2 || target.WorldX != value.X1 || target.WorldY != value.Y1)) return false;
            var expected = first ? value.Expected0 : value.Expected1; var actual = first ? value.Actual0 : value.Actual1;
            bool wallSame = (expected.Layers & CaptureLayer.Wall) != 0 && (actual.Layers & CaptureLayer.Wall) != 0 && expected.WallType == actual.WallType;
            return CurrentAppearanceStep(operation, BlueprintConstructionAppearance.Next(expected, actual, true, wallSame));
        }

        private void WaitForAppearanceRestoration(BlueprintBuildTarget target, int type = 0, int required = 0)
        {
            var value = appearanceRestoration;
            if (value != null && type > 0 && required > 0)
            {
                int old;
                if (!value.Thresholds.TryGetValue(type, out old) || required < old) value.Thresholds[type] = required;
            }
            WaitForBuild(target, BlueprintBuildWait.Restoration);
        }

        private bool AppearanceRestorationConsumptionReady(BlueprintBuildTarget target, Player p, int type, int maximum,
            BlueprintBuildOperation operation = null)
        {
            RefreshAppearanceRestoration();
            var value = appearanceRestoration;
            if (value == null || maximum <= 0 || type <= 0) return true;
            if (ExactAppearanceRestorationStep(target, operation ?? target?.Operation)) return true;
            int need = GetAppearanceRestorationNeed(type);
            if (need == 0 || value.Readable && HasAppearanceRestoreSupply(p, type, checked(need + maximum))) return true;
            WaitForAppearanceRestoration(target, type, need == 0 ? 0 : checked(need + maximum)); return false;
        }

        private bool NewAppearanceRestorationReady(BlueprintBuildTarget target, CaptureCell expected,
            BlueprintWorldMatchSample actual, BlueprintAppearanceStep step)
        {
            if (!ForegroundRestorationDestruction(step, expected, actual.Cell)) return true;
            RefreshAppearanceRestoration(); var value = appearanceRestoration;
            if (value == null) return true;
            // One current owner may finish its own native prerequisite chain.
            // Another destructive foreground cannot borrow that owner's paint.
            if (value.Readable && target.Identity.Equals(value.Identity) && ReferenceEquals(target.Mask, value.Mask) &&
                (target.WorldX == value.X0 && target.WorldY == value.Y0 || value.Count == 2 && target.WorldX == value.X1 && target.WorldY == value.Y1)) return true;
            WaitForAppearanceRestoration(target); return false;
        }

        private void ObserveAppearanceRestorationSignals()
        {
            RefreshAppearanceRestoration(); var value = appearanceRestoration; var inventory = MaterialInventory;
            if (ReferenceEquals(appearanceRestorationSupplyInventory, inventory)) return;
            appearanceRestorationSupplyInventory = inventory;
            if (value == null || inventory?.Ready != true || value.Thresholds.Count == 0 || player == null) return;
            foreach (var threshold in value.Thresholds)
                if (HasAppearanceRestoreSupply(player, threshold.Key, threshold.Value))
                {
                    // One crossed real threshold wakes only ranges which
                    // registered this dependency. Their next bounded walk can
                    // register any still higher threshold it actually needs.
                    value.Thresholds.Clear(); appearanceRestorationRevision++; return;
                }
        }
    }
}
