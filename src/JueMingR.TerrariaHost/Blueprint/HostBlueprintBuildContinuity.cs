using System;
using JueMingR.Features.Blueprint;
using Terraria;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        // One overtaken ordinary target owns the next preparation boundary.
        // Keep only immutable blueprint descriptions, never a flight, source
        // transport, world/support proof or inventory/consumption baseline.
        private sealed class DeferredBuildSeed
        {
            internal readonly BlueprintProjectionPlacement Owner;
            internal readonly BlueprintProjectionMatchRange Range;
            internal readonly BlueprintProjectionIdentity Identity;
            internal readonly BlueprintProjectionMask Mask;
            internal readonly int X, Y, Ordinal;
            internal readonly long Catalog;
            internal readonly CaptureCell Cell;
            internal readonly CaptureObject? Object;
            internal readonly BlueprintWorldObject Body;
            internal readonly BlueprintPlacementDescriptor Placement;

            internal DeferredBuildSeed(BlueprintBuildTarget target)
            {
                Owner = target.Range.Owner; Range = target.Range; Identity = target.Identity; Mask = target.Mask;
                X = target.OriginX; Y = target.OriginY; Ordinal = target.Ordinal; Catalog = target.PlacementRevision;
                Cell = target.Cell; Object = target.Body == null ? (CaptureObject?)null : target.Object;
                Body = target.Body; Placement = target.Placement;
            }
        }
        private DeferredBuildSeed deferredBuild;

        private BuildSource CurrentBuildSourceHint(Player p)
        {
            var current = activeBuild; var use = buildTools?.Use;
            if (current == null || use?.Building != true || current.Token <= 0 || current.Token != use.Operation ||
                !ReferenceEquals(p, player) || !ReferenceEquals(current.Player, p) || current.Target.Identity.SessionGeneration != generation)
                return null;
            var source = current.Source;
            // OwnsBuildMaterial checks the actual ToolUse session, selection,
            // cancellation and item identity without re-reading world geometry.
            if (source.Slot < 0 || source.Slot >= 50 || !ReferenceEquals(p.inventory[source.Slot], source.Item) ||
                source.Item.type != source.Type || source.Item.stack <= 0 || !use.OwnsBuildMaterial(source.Item) ||
                !buildTools.BuildInventoryEligible(source.Slot, source.Item)) return null;
            return source;
        }
        private static bool SameBuildSource(BuildFlight current, BuildFlight next, ulong materialSlots)
        {
            return current != null && next != null && next.Source.Slot == current.Source.Slot &&
                ReferenceEquals(next.Source.Item, current.Source.Item) && next.Source.Type == current.Source.Type &&
                next.DependencySlots == materialSlots;
        }
        private static bool SameBuildTarget(BlueprintBuildTarget left, BlueprintBuildTarget right)
        {
            return left != null && right != null && ReferenceEquals(left.Range, right.Range) && left.Identity.Equals(right.Identity) &&
                ReferenceEquals(left.Mask, right.Mask) && left.OriginX == right.OriginX && left.OriginY == right.OriginY &&
                left.PlacementRevision == right.PlacementRevision && left.Ordinal == right.Ordinal &&
                left.Placement.Kind == right.Placement.Kind && left.WorldX == right.WorldX && left.WorldY == right.WorldY;
        }
        private bool IsUnfinishedBuildTarget(BlueprintBuildTarget target)
        { return activeBuild != null && !activeBuild.Checked && !activeBuild.Rejected && SameBuildTarget(activeBuild.Target, target); }

        private bool PrepareBuildCandidate(BlueprintBuildTarget target, Player p, ref int allowance, out BuildFlight result)
        {
            result = null;
            if (allowance <= 0) return false;
            allowance--;
#if DEBUG
            BuildCandidateChecks++;
#endif
            return TryPrepareBuild(target, p, out result);
        }
        private void PreferContinuousBuild(BuildFlight fallback, Player p, ref int allowance)
        {
            preparedBuild = fallback;
            var hint = CurrentBuildSourceHint(p);
            if (deferredBuild != null || hint == null || SameBuildSource(activeBuild, fallback, buildTools.Use.Intent.MaterialSlots)) return;
            BuildFlight preferred;
            if (!TryPreferredBuild(p, hint, ref allowance, out preferred)) return;
            // The ordinary Candidate and its cursor have not been notified or
            // replaced by the transient preferred target. Service is owed until
            // Admitted/Refresh actually hands this identity to ToolUse.
            deferredBuild = new DeferredBuildSeed(fallback.Target); preparedBuild = preferred;
        }
        private bool TryPreferredBuild(Player p, BuildSource hint, ref int allowance, out BuildFlight result)
        {
            result = null;
            int visits = buildSources.Count;
            var node = buildSources.First;
            if (node == null) return false;
            for (int i = 0, at = buildSourceCursor % visits; i < at; i++) node = node.Next;
            for (int n = 0; n < visits && buildSources.Count > 0 && materialBuildSourceBudget > 0 && allowance > 0; n++)
            {
                var source = node.Value; node = node.Next ?? buildSources.First;
                var range = source.Range; var value = range.Materials;
                if (value == null || !ReferenceEquals(FindBuildSource(range), source.Part)) continue;
                long support;
                if (!range.Group.Ready || !ProjectionMatchReadCurrent(range, range.RawVersion) ||
                    !projectionChanges.TryGetBuildStamp(range.Watch, range.PartIndex, out support)) continue;
                var bounds = BuildNearBounds(source.Owner); var part = source.Part; var cells = part.Window.Cells;
                if (value.PreferredLease != buildTools.Use.Operation)
                {
                    value.PreferredLease = buildTools.Use.Operation;
                    value.PreferredCursor = part.LogicalObjectId > 0 ? 0 : (part.Offset + BuildCellLowerBound(cells, bounds.Left, bounds.Top)) * 2;
                }
                // The stable ordinal belongs to the range, not the retained
                // transport window. Moving/support changes never rewind it.
                if (part.LogicalObjectId == 0 && value.PreferredCursor / 2 < part.Offset)
                    value.PreferredCursor = (part.Offset + BuildCellLowerBound(cells, bounds.Left, bounds.Top)) * 2;
                int steps = BuildSourceSlice;
                while (materialBuildSourceBudget > 0 && steps > 0 && allowance > 0)
                {
                    int index = part.LogicalObjectId > 0 ? 0 : value.PreferredCursor / 2 - part.Offset;
                    if (index < 0 || index >= cells.Count || part.LogicalObjectId > 0 && value.PreferredCursor > 0) break;
                    var cell = cells[index];
                    if (part.LogicalObjectId == 0)
                    {
                        if (cell.Y >= bounds.Bottom) break;
                        if (cell.X < bounds.Left || cell.X >= bounds.Right || cell.Y < bounds.Top)
                        {
                            value.PreferredCursor = (part.Offset + BuildCellLowerBound(cells, bounds.Left,
                                cell.X >= bounds.Right ? cell.Y + 1 : Math.Max(cell.Y, bounds.Top))) * 2;
                            continue;
                        }
                        if ((value.PreferredCursor & 1) == 0 && (cell.Layers & CaptureLayer.Tile) == 0 && (cell.Layers & CaptureLayer.Wall) != 0)
                            value.PreferredCursor++;
                    }
                    int ordinal = value.PreferredCursor;
                    value.PreferredCursor = NextBuildSourceOrdinal(ordinal, cell, part.LogicalObjectId > 0);
                    materialBuildSourceBudget--; steps--;
#if DEBUG
                    ProjectionMatchSourceSteps++;
#endif
                    BlueprintBuildTarget target;
                    if (!TryCreateLocalBuildTarget(source, index, ordinal, support, out target, hint.Type) || IsUnfinishedBuildTarget(target)) continue;
                    BuildFlight next;
                    if (!PrepareBuildCandidate(target, p, ref allowance, out next)) continue;
                    if (!ReferenceEquals(CurrentBuildSourceHint(p), hint) || !SameBuildSource(activeBuild, next, buildTools.Use.Intent.MaterialSlots)) return false;
                    result = next; return true;
                }
            }
            return false;
        }
        private bool DeferredBuildIdentityCurrent(DeferredBuildSeed seed)
        {
            var owner = seed.Owner; var range = seed.Range;
            return HasSession && seed.Identity.SessionGeneration == generation && seed.Catalog == MaterialCatalog?.Revision &&
                projectionScene.Contains(owner) && owner.Identity.Equals(seed.Identity) && ReferenceEquals(owner.Mask, seed.Mask) &&
                owner.OriginX == seed.X && owner.OriginY == seed.Y && ProjectionQualificationCurrent(owner) &&
                owner.Summary.TargetVersion == MaterialCatalog.TargetVersion && owner.Match != null && range.Index >= 0 &&
                range.Index < owner.Match.Ranges.Length && ReferenceEquals(owner.Match.Ranges[range.Index], range);
        }
        private bool TryDeferredBuild(Player p, ref int allowance, out BuildFlight result)
        {
            result = null; var seed = deferredBuild;
            if (seed == null) return false;
            if (!DeferredBuildIdentityCurrent(seed)) { deferredBuild = null; return false; }
            var range = seed.Range; long support;
            if (!range.Group.Ready || !ProjectionMatchReadCurrent(range, range.RawVersion) ||
                !projectionChanges.TryGetBuildStamp(range.Watch, range.PartIndex, out support))
            {
                DemandProjectionMatchRange(range); return false;
            }
            var target = CreateMaterialBuildTarget(seed.Owner, range, seed.Cell, seed.Placement, seed.Object, seed.Body,
                seed.Ordinal, support, seed.Catalog);
            target.LocalProof = target.LocalPriority = true;
            var candidate = range.Materials?.Candidate;
            if (SameBuildTarget(candidate, target)) range.Materials.Candidate = target;
            if (PrepareBuildCandidate(target, p, ref allowance, out result)) return true;
            // Current proof really inspected this target and found it unable
            // to build. Existing waits/retries own its later recovery.
            NotifyBuildAttempt(target); deferredBuild = null; return false;
        }
        private void ServiceDeferredBuild(BlueprintBuildTarget target)
        {
            var seed = deferredBuild;
            if (seed != null && ReferenceEquals(seed.Range, target.Range) && seed.Ordinal == target.Ordinal &&
                seed.Identity.Equals(target.Identity) && ReferenceEquals(seed.Mask, target.Mask) && seed.X == target.OriginX &&
                seed.Y == target.OriginY && seed.Catalog == target.PlacementRevision) deferredBuild = null;
        }
    }
}
