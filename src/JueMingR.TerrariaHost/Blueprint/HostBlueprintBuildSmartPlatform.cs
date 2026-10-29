using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JueMingR.Features.Blueprint;
using JueMingR.TerrariaHost.Tools;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        // This certificate permits one ordinary platform input, never a slope
        // write. Vanilla Lookup must still resolve the exact cell and vanilla
        // PlaceIt alone performs placement and its Smart-platform stage.
        private sealed class SmartPlatformInputProof
        {
            internal BlueprintBuildTarget Target;
            internal Item Source;
            internal long Session, Catalog, TargetVersion;
            internal int Direction;
            internal SmartPlatformCell[] Cells;
        }
        private struct SmartPlatformCell
        {
            internal Tile Tile;
            internal ushort Type, Wall;
            internal byte Slope, Liquid;
            internal bool Active, Half, Inactive, CheckingLiquid;
        }
        private sealed class SmartPlatformFallback
        {
            internal BlueprintProjectionIdentity Identity;
            internal BlueprintProjectionMask Mask;
            internal int Ordinal, X, Y, Material, TileType, Style;
            internal long Catalog;
            internal byte Slope;
        }
        private sealed class SmartPlatformFallbackRange
        {
            internal readonly List<SmartPlatformFallback> Entries = new List<SmartPlatformFallback>();
            internal bool PreciseOnly;
        }
        // Entries belong to resident range objects, not world coordinates or
        // disposable use tokens. One failure keeps the same target/recipe on
        // Precise for this resident range even if support later changes. This
        // optimization need not reopen after world changes; conservative
        // fallback must not mistake a broad safety halo for recovery evidence.
        // Retired ranges release their entries. A full range keeps Precise
        // instead of evicting failures and reopening alternating targets.
        private readonly ConditionalWeakTable<BlueprintProjectionMatchRange, SmartPlatformFallbackRange> smartPlatformFallbacks =
            new ConditionalWeakTable<BlueprintProjectionMatchRange, SmartPlatformFallbackRange>();
        private const int SmartPlatformFallbackLimit = 16;
        private const int SmartPlatformRadius = 2;
        private const int SmartPlatformSide = SmartPlatformRadius * 2 + 1;

        private void PlanSmartPlatformInput(BlueprintBuildTarget target, Player p, BuildOperationPlan plan)
        {
            plan.CursorMode = ToolBuildCursorMode.Precise; plan.SmartDirection = 0; plan.SmartProof = null;
            SmartPlatformInputProof proof;
            if (plan.Operation != null || SmartPlatformFallbackCurrent(target, plan.Tool) ||
                !TryCreateSmartPlatformProof(target, p, plan.Tool, out proof)) return;
            plan.CursorMode = ToolBuildCursorMode.SmartPlatform;
            plan.SmartDirection = proof.Direction; plan.SmartProof = proof;
        }
        private void ApplySmartPlatformInput(BuildFlight flight, BuildOperationPlan plan)
        {
            flight.CursorMode = ToolBuildCursorMode.Precise; flight.SmartDirection = 0; flight.SmartProof = null;
            if (plan == null)
            {
                plan = new BuildOperationPlan { Tool = flight.Source.Item, Operation = flight.Operation };
                PlanSmartPlatformInput(flight.Target, flight.Player, plan);
            }
            if (plan.CursorMode != ToolBuildCursorMode.SmartPlatform) return;
            flight.CursorMode = plan.CursorMode; flight.SmartDirection = plan.SmartDirection; flight.SmartProof = plan.SmartProof;
            if (SmartPlatformInputCurrent(flight)) flight.Direction = flight.SmartDirection;
            else { flight.CursorMode = ToolBuildCursorMode.Precise; flight.SmartDirection = 0; flight.SmartProof = null; }
        }
        private bool TryCreateSmartPlatformProof(BlueprintBuildTarget target, Player p, Item item, out SmartPlatformInputProof proof)
        {
            proof = null;
            if (target == null || p == null || !ReferenceEquals(p, player) || item == null || item.IsAir || target.Operation != null ||
                BuildLane(target.Ordinal) != BlueprintBuildLane.TileBody || !BasicPlacement(target.Placement) ||
                !target.Placement.Platform || target.Cell.HalfBrick || target.Cell.Slope < 1 || target.Cell.Slope > 2 ||
                item.type != target.Placement.UseItemType || item.createTile != target.Placement.TileType || item.placeStyle != target.Placement.Style ||
                !BuildPlatformForm(target, item.createTile) || WorldGen.isGeneratingOrLoadingWorld || !IsBuildTargetCurrent(target)) return false;
            SmartPlatformCell[] cells;
            if (!ReadSmartPlatformCells(target, out cells) || !SmartPlatformNarrowRoute(target, cells)) return false;
            int direction = target.Cell.Slope == 1 ? 1 : -1;
            if (p.pulley && p.pulleyDir == 2 && p.direction != direction) return false;
            proof = new SmartPlatformInputProof { Target = target, Source = item, Session = generation,
                Catalog = target.PlacementRevision, TargetVersion = target.RangeStamp, Direction = direction, Cells = cells };
            return true;
        }
        private static int SmartPlatformIndex(int dx, int dy)
        { return (dx + SmartPlatformRadius) * SmartPlatformSide + dy + SmartPlatformRadius; }
        private bool ReadSmartPlatformCells(BlueprintBuildTarget target, out SmartPlatformCell[] cells)
        {
            cells = new SmartPlatformCell[SmartPlatformSide * SmartPlatformSide];
            for (int dx = -SmartPlatformRadius; dx <= SmartPlatformRadius; dx++)
                for (int dy = -SmartPlatformRadius; dy <= SmartPlatformRadius; dy++)
                {
                    SmartPlatformCell cell;
                    if (!ReadSmartPlatformCell(target.WorldX + dx, target.WorldY + dy, out cell)) { cells = null; return false; }
                    cells[SmartPlatformIndex(dx, dy)] = cell;
                }
            return true;
        }
        private bool ReadSmartPlatformCell(int x, int y, out SmartPlatformCell cell)
        {
            cell = default(SmartPlatformCell); BlueprintWorldMatchSample sample;
            // ReadBuildCell carries the existing session/section/null gate.
            // Do not use GetTileSafely or CanPoundTile on an empty destination:
            // those may create Tiles and CanPound inspects its stale type.
            if (!ReadBuildCell(x, y, out sample)) return false;
            var raw = Main.tile[x, y];
            if (raw == null) return false;
            cell = new SmartPlatformCell { Tile = raw, Type = raw.type, Wall = raw.wall, Slope = raw.slope(), Liquid = raw.liquid,
                Active = raw.active(), Half = raw.halfBrick(), Inactive = raw.inActive(), CheckingLiquid = raw.checkingLiquid() };
            return true;
        }
        private static bool SmartPlatformCellSame(SmartPlatformCell a, SmartPlatformCell b, bool identity)
        {
            // The safety proof depends on body/shape/liquid, not paint or
            // connection frames. The allowed CanKillTile branch is frame-free.
            return (!identity || ReferenceEquals(a.Tile, b.Tile)) && a.Type == b.Type && a.Wall == b.Wall &&
                a.Active == b.Active && a.Slope == b.Slope && a.Half == b.Half && a.Inactive == b.Inactive &&
                a.Liquid == b.Liquid && a.CheckingLiquid == b.CheckingLiquid;
        }
        private static bool SmartPlatformNarrowRoute(BlueprintBuildTarget target, SmartPlatformCell[] cells)
        {
            var center = cells[SmartPlatformIndex(0, 0)];
            // With an inactive destination, calling CanKillTile would test the
            // old residue and return false. Instead prove the placed platform
            // type takes vanilla's ordinary branch: no active tile above and
            // no unbreakable wall, special pound type or boulder. Native body
            // geometry retains reach, liquid, entity and anchoring checks.
            if (center.Active || center.Wall == 350 || cells[SmartPlatformIndex(0, -1)].Active) return false;
            int type = target.Placement.TileType;
            if (type < 0 || type >= TileID.Sets.Platforms.Length || !TileID.Sets.Platforms[type] || TileID.Sets.Boulders[type]) return false;
            switch (type)
            {
                case 10: case 48: case 137: case 232: case 380: case 387: case 388: case 476: case 484: return false;
            }
            int endpointX = target.Cell.Slope == 1 ? -1 : 1;
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var cell = cells[SmartPlatformIndex(dx, dy)];
                bool platform = cell.Active && cell.Type < TileID.Sets.Platforms.Length && TileID.Sets.Platforms[cell.Type];
                if (dx == endpointX && dy == -1)
                {
                    if (!platform || cell.Slope != target.Cell.Slope || cell.Half || cell.Inactive) return false;
                }
                else if (platform) return false;
            }
            // In SpinSmartPlatform the sole upper endpoint already has the
            // desired nonzero slope, so its slope==0 branch (including the
            // second neighbour) is unreachable; the lower endpoint is absent.
            // Compare vanilla's result rather than supplying a slope command.
            return Player.GetSmartPlatformSlope(target.WorldX, target.WorldY) == target.Cell.Slope;
        }
        private bool SmartPlatformInputCurrent(BuildFlight flight)
        {
            if (flight?.CursorMode != ToolBuildCursorMode.SmartPlatform) return true;
            var proof = flight.SmartProof; var target = flight.Target;
            if (proof == null || proof.Session != generation || !ReferenceEquals(proof.Target, target) ||
                !ReferenceEquals(proof.Source, flight.Source.Item) || proof.Catalog != target.PlacementRevision ||
                proof.TargetVersion != target.RangeStamp || proof.Direction != flight.SmartDirection ||
                !IsBuildTargetCurrent(target) || WorldGen.isGeneratingOrLoadingWorld) return false;
            for (int dx = -SmartPlatformRadius; dx <= SmartPlatformRadius; dx++)
                for (int dy = -SmartPlatformRadius; dy <= SmartPlatformRadius; dy++)
                {
                    SmartPlatformCell cell;
                    if (!ReadSmartPlatformCell(target.WorldX + dx, target.WorldY + dy, out cell) ||
                        !SmartPlatformCellSame(proof.Cells[SmartPlatformIndex(dx, dy)], cell, true)) return false;
                }
            return SmartPlatformNarrowRoute(target, proof.Cells);
        }
        private bool SmartPlatformResultValid(BuildFlight flight)
        {
            if (flight?.CursorMode != ToolBuildCursorMode.SmartPlatform) return true;
            var proof = flight.SmartProof;
            if (proof == null) return false;
            // Framing may legally change connection frameX. Extra platform
            // slope/half changes are never authorized, including outside mask.
            for (int dx = -SmartPlatformRadius; dx <= SmartPlatformRadius; dx++)
                for (int dy = -SmartPlatformRadius; dy <= SmartPlatformRadius; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var before = proof.Cells[SmartPlatformIndex(dx, dy)];
                    if (!before.Active || before.Type >= TileID.Sets.Platforms.Length || !TileID.Sets.Platforms[before.Type]) continue;
                    SmartPlatformCell after;
                    if (!ReadSmartPlatformCell(flight.Target.WorldX + dx, flight.Target.WorldY + dy, out after) ||
                        !ReferenceEquals(before.Tile, after.Tile) || !after.Active || before.Type != after.Type ||
                        before.Slope != after.Slope || before.Half != after.Half || before.Inactive != after.Inactive) return false;
                }
            // Actual target shape remains the existing Match/Shape owner's
            // fact. A correctly placed flat body can still use real hammering.
            SmartPlatformCell actual;
            if (!ReadSmartPlatformCell(flight.Target.WorldX, flight.Target.WorldY, out actual)) return false;
            return !actual.Active || actual.Type == flight.Target.Placement.TileType && !actual.Half &&
                (actual.Slope == 0 || actual.Slope == flight.Target.Cell.Slope);
        }
        private bool SmartPlatformFallbackCurrent(BlueprintBuildTarget target, Item item)
        {
            SmartPlatformFallbackRange range;
            if (target?.Range == null || item == null || !smartPlatformFallbacks.TryGetValue(target.Range, out range)) return false;
            if (range.PreciseOnly) return true;
            foreach (var stamp in range.Entries)
                if (SameSmartPlatformFallbackTarget(stamp, target, item)) return true;
            return false;
        }
        private static bool SameSmartPlatformFallbackTarget(SmartPlatformFallback stamp, BlueprintBuildTarget target, Item item)
        {
            return stamp.Identity.Equals(target.Identity) && ReferenceEquals(stamp.Mask, target.Mask) && stamp.Ordinal == target.Ordinal &&
                stamp.X == target.WorldX && stamp.Y == target.WorldY && stamp.Material == item.type && stamp.TileType == target.Placement.TileType &&
                stamp.Style == target.Placement.Style && stamp.Catalog == target.PlacementRevision && stamp.Slope == target.Cell.Slope;
        }
        private bool RememberSmartPlatformFallback(BuildFlight flight)
        {
            if (flight?.CursorMode != ToolBuildCursorMode.SmartPlatform || flight.Target.Range == null) return false;
            var target = flight.Target; var item = flight.Source.Item;
            var range = smartPlatformFallbacks.GetValue(target.Range, key => new SmartPlatformFallbackRange());
            if (range.PreciseOnly || SmartPlatformFallbackCurrent(target, item)) return true;
            if (range.Entries.Count >= SmartPlatformFallbackLimit) { range.PreciseOnly = true; return true; }
            range.Entries.Add(new SmartPlatformFallback { Identity = target.Identity, Mask = target.Mask, Ordinal = target.Ordinal,
                X = target.WorldX, Y = target.WorldY, Material = flight.Source.Type, TileType = target.Placement.TileType,
                Style = target.Placement.Style, Catalog = target.PlacementRevision, Slope = target.Cell.Slope });
            return true;
        }
    }
}
