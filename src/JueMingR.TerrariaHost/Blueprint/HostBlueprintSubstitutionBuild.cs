using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Tools;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private int substitutionCandidateBudget;
        private bool substitutionPlanningPending;
#if DEBUG
        internal long SubstitutionCandidateChecks { get; private set; }
        internal long SubstitutionWallPositionChecks { get; private set; }
#endif

        private void AddSubstitutionCandidates(List<BlueprintPlacementDescriptor> result, BlueprintBuildTarget target, int itemType)
        {
            if (itemType <= 0 || itemType == target.Placement.UseItemType || !buildSupply.ContainsKey(itemType)) return;
            var candidates = MaterialCatalog?.Substitution?.CandidatesForItem(target.Placement, itemType, substitutionPolicy);
            if (candidates != null) foreach (var candidate in candidates)
                if (BasicPlacement(candidate)) result.Add(candidate);
        }

        // New selection prefers the original, then the real source hint and
        // qualified stock. A continuation probe asks only about that exact
        // Item/slot; choosing another original cannot answer that question.
        // These separate finite cursors survive a budget yield independently.
        // Neither plan reserves stock or carries geometry across an Update.
        private bool TryPlanSubstitutionBuild(BlueprintBuildTarget target, Player p, BuildSource preferred,
            BuildPlanningIntent intent, out BuildOperationPlan plan, out BlueprintBuildWait wait)
        {
            plan = null; wait = BlueprintBuildWait.None; substitutionPlanningPending = false;
            bool continuing = intent == BuildPlanningIntent.ContinuationProbe;
            if (continuing && (preferred == null || preferred.Slot < 0 || preferred.Slot >= 50 ||
                !ReferenceEquals(p.inventory[preferred.Slot], preferred.Item) || preferred.Item.type != preferred.Type ||
                preferred.Item.IsAir || !buildTools.BuildInventoryEligible(preferred.Slot, preferred.Item))) return false;
            bool wall = target.Placement.Kind == BlueprintPlacementKind.Wall;
            // Empty/readable/cardinal support is common to all wall materials
            // at this current target. A definite refusal is not budget Pending.
            // Final admission and the real writer recheck it without this hint.
            if (wall)
            {
#if DEBUG
                SubstitutionWallPositionChecks++;
#endif
                if (!NativeWallBuildPosition(target)) return false;
            }
            var candidates = new List<BlueprintPlacementDescriptor>();
            if ((!continuing || preferred.Type == target.Placement.UseItemType) && buildSupply.ContainsKey(target.Placement.UseItemType))
                candidates.Add(target.Placement);
            if (preferred != null) AddSubstitutionCandidates(candidates, target, preferred.Type);
            if (!continuing) foreach (var entry in buildSupply)
                    if (preferred == null || entry.Key != preferred.Type) AddSubstitutionCandidates(candidates, target, entry.Key);
            if (candidates.Count == 0) { wait = BlueprintBuildWait.Material; return false; }

            var materials = target.Range.Materials;
            var progress = continuing ? materials.SubstitutionContinuation : materials.SubstitutionSelection;
            if (progress == null)
            {
                progress = new BlueprintSubstitutionBuildCursor();
                if (continuing) materials.SubstitutionContinuation = progress; else materials.SubstitutionSelection = progress;
            }
            long inventoryRevision = MaterialInventory?.Revision ?? -1;
            if (progress.Ordinal != target.Ordinal || progress.Range != target.RangeStamp ||
                progress.Support != target.SupportStamp || progress.Supply != inventoryRevision ||
                progress.Player != MaterialBuildEligibilityRevision || progress.Policy != substitutionRevision ||
                !ReferenceEquals(progress.Source, preferred?.Item) || progress.Slot != (preferred?.Slot ?? -1) || progress.Type != (preferred?.Type ?? 0))
            {
                progress.Ordinal = target.Ordinal; progress.Cursor = 0;
                progress.Range = target.RangeStamp; progress.Support = target.SupportStamp;
                progress.Supply = inventoryRevision; progress.Player = MaterialBuildEligibilityRevision; progress.Policy = substitutionRevision;
                progress.Source = preferred?.Item; progress.Slot = preferred?.Slot ?? -1; progress.Type = preferred?.Type ?? 0;
            }
            while (progress.Cursor < candidates.Count)
            {
                if (substitutionCandidateBudget <= 0) { substitutionPlanningPending = true; return false; }
                substitutionCandidateBudget--;
                var candidate = candidates[progress.Cursor++];
#if DEBUG
                SubstitutionCandidateChecks++;
#endif
                int slot = preferred != null && preferred.Type == candidate.UseItemType && preferred.Slot >= 0 && preferred.Slot < 50 &&
                    ReferenceEquals(p.inventory[preferred.Slot], preferred.Item) && buildTools.BuildInventoryEligible(preferred.Slot, preferred.Item)
                    ? preferred.Slot : ConstructionItemSlot(p, candidate.UseItemType);
                if (slot < 0) { wait |= BlueprintBuildWait.Material; continue; }
                var item = p.inventory[slot]; BlueprintBuildTarget execution;
                if (!TryCreateSubstitutionExecution(target, candidate, p, item, out execution)) continue;
                int direction = execution.Placement.Direction == (int)TileObjectDirection.PlaceLeft ? -1 :
                    execution.Placement.Direction == (int)TileObjectDirection.PlaceRight ? 1 : p.direction;
                if (p.pulley && p.pulleyDir == 2 && direction != p.direction ||
                    !p.IsInTileInteractionRange(execution.WorldX, execution.WorldY, TileReachCheckSettings.Simple, item.tileBoost + p.blockRange))
                { wait |= BlueprintBuildWait.Player; continue; }
                if (!NativeBuildGeometry(execution, p, item, direction, wall)) continue;
                plan = new BuildOperationPlan { ExecutionTarget = execution, Tool = item, ToolType = item.type,
                    ToolSlot = slot, HeldMaximum = execution.Placement.Kind == BlueprintPlacementKind.Wall ? 5 : 1 };
                PlanSmartPlatformInput(execution, p, plan);
                progress.Cursor = 0;
                return true;
            }
            progress.Cursor = 0;
            return false;
        }

        private bool TryCreateSubstitutionExecution(BlueprintBuildTarget target, BlueprintPlacementDescriptor source,
            Player p, Item item, out BlueprintBuildTarget execution)
        {
            execution = null;
            var output = source;
            if (source.Kind == BlueprintPlacementKind.Tile && source.TileType == TileID.Torches)
            {
                int type = item.createTile, style = item.placeStyle;
                p.BiomeTorchPlaceStyle(ref type, ref style);
                if (type != source.TileType || style != source.Style)
                {
                    if (MaterialCatalog.Substitution == null ||
                        !MaterialCatalog.Substitution.TryEffectiveTile(source,type,style,substitutionPolicy,out output)) return false;
                }
            }
            CaptureCell cell; CaptureObject? obj;
            var originalObject = target.Body == null ? (CaptureObject?)null : target.Object;
            if (ReferenceEquals(output, target.Placement)) { cell = target.Cell; obj = originalObject; }
            else if (MaterialCatalog.Substitution == null ||
                !MaterialCatalog.Substitution.TryCreateExecution(target.Cell, originalObject, output, substitutionPolicy, out cell, out obj)) return false;
            BlueprintWorldObject body = null;
            if (obj.HasValue && !projectionMatchSource.TryDescribeTarget(obj.Value, cell, target.OriginX, target.OriginY, out body)) return false;
            execution = CreateMaterialBuildTarget(target.Range.Owner, target.Range, cell, output, obj, body,
                target.Ordinal, target.SupportStamp, target.PlacementRevision);
            execution.Requirement = target.Requirement ?? target; execution.SourcePlacement = source;
            execution.LocalProof = target.LocalProof; execution.LocalPriority = target.LocalPriority;
            return IsBuildTargetCurrent(execution);
        }

        // A particular native debit is proved against its particular output.
        // Final world acceptance may later recognize any allowed material, but
        // it cannot certify which source this ToolUse actually consumed.
        private bool DirectBuildApplied(BuildFlight flight)
        {
            var target = flight.Target; BlueprintWorldMatchSample actual;
            if (target.Placement.Kind == BlueprintPlacementKind.Wall)
                return ReadBuildCell(target.WorldX, target.WorldY, out actual) &&
                    (actual.Cell.Layers & CaptureLayer.Wall) != 0 && actual.Cell.WallType == target.Placement.WallType;
            if (target.Body == null)
                return ReadBuildCell(target.WorldX, target.WorldY, out actual) &&
                    BlueprintMaterialSteps.SameBody(target.Cell, actual, MaterialCatalog) == BlueprintBodyState.Same;
            if (!ReadBuildCell(target.Body.AnchorX, target.Body.AnchorY, out actual) || actual.ObjectUnknown || actual.Object == null ||
                !target.Body.SamePlacement(actual.Object)) return false;
            var observed = actual.Object; var proof = new BlueprintObjectBodyProof(target.Body);
            for (int row = 0; row < observed.Height; row++) for (int column = 0; column < observed.Width; column++)
            {
                if (!ReadBuildCell(observed.WorldX + column, observed.WorldY + row, out actual)) return false;
                proof.Observe(actual);
            }
            return proof.Complete() == BlueprintBodyState.Same;
        }
    }
}
