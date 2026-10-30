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
            internal int Direction, DirectionY, Routes, ResultSlope;
            internal SmartPlatformCell[] Cells;
            internal SmartPlatformWrite[] Writes;
        }
        private sealed class SmartPlatformWrite
        {
            internal BlueprintBuildTarget Target;
            internal int X, Y, Slope;
        }
        private struct SmartPlatformCell
        {
            internal Tile Tile;
            internal ushort Type, Wall;
            internal short FrameY;
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
            internal long Neutral;
            internal int Retries;
            internal SmartPlatformRouteState[] Route;
        }
        // Failure recovery needs only these nine pure values, not a retained
        // flight's 7x7 read proof or any Tile/transport references.
        private struct SmartPlatformRouteState
        {
            internal ushort Type;
            internal short FrameY;
            internal byte Slope;
            internal bool Active, Half, Inactive;
        }
        private sealed class SmartPlatformFallbackRange
        {
            internal readonly List<SmartPlatformFallback> Entries = new List<SmartPlatformFallback>();
            internal bool PreciseOnly;
        }
        // Resident ranges bound failed recipes. A failure can reopen once,
        // only after an observed native neutral Lookup or a changed immediate
        // route dependency. Tokens, stock counts and broad dirty halos never
        // renew that allowance; a stable second failure stays Precise.
        private readonly ConditionalWeakTable<BlueprintProjectionMatchRange, SmartPlatformFallbackRange> smartPlatformFallbacks =
            new ConditionalWeakTable<BlueprintProjectionMatchRange, SmartPlatformFallbackRange>();
        private const int SmartPlatformFallbackLimit = 16;
        private const int SmartPlatformRadius = 3;
        private const int SmartPlatformSide = SmartPlatformRadius * 2 + 1;
        private long smartPlatformNeutralEpoch;
        private long smartPlatformNeutralFrame = -1;
        private long smartPlatformFallbackGeneration = -1;

        private void PlanSmartPlatformInput(BlueprintBuildTarget target, Player p, BuildOperationPlan plan)
        {
            plan.CursorMode = ToolBuildCursorMode.Precise; plan.SmartDirection = plan.SmartDirectionY = 0; plan.SmartProof = null;
            SmartPlatformInputProof proof;
            if (plan.Operation != null || SmartPlatformFallbackCurrent(target, plan.Tool) ||
                !TryCreateSmartPlatformProof(target, p, plan.Tool, out proof)) return;
            plan.CursorMode = ToolBuildCursorMode.SmartPlatform;
            plan.SmartDirection = proof.Direction; plan.SmartDirectionY = proof.DirectionY; plan.SmartProof = proof;
        }
        private void ApplySmartPlatformInput(BuildFlight flight, BuildOperationPlan plan)
        {
            flight.CursorMode = ToolBuildCursorMode.Precise; flight.SmartDirection = flight.SmartDirectionY = 0; flight.SmartProof = null;
            if (plan == null)
            {
                plan = new BuildOperationPlan { Tool = flight.Source.Item, Operation = flight.Operation };
                PlanSmartPlatformInput(flight.Target, flight.Player, plan);
            }
            if (plan.CursorMode != ToolBuildCursorMode.SmartPlatform) return;
            flight.CursorMode = plan.CursorMode; flight.SmartDirection = plan.SmartDirection; flight.SmartDirectionY = plan.SmartDirectionY; flight.SmartProof = plan.SmartProof;
            // Route motion and the player's native facing are different facts.
            if (!SmartPlatformInputCurrent(flight))
            { flight.CursorMode = ToolBuildCursorMode.Precise; flight.SmartDirection = flight.SmartDirectionY = 0; flight.SmartProof = null; }
        }
        private bool TryCreateSmartPlatformProof(BlueprintBuildTarget target, Player p, Item item, out SmartPlatformInputProof proof)
        {
            proof = null;
            if (target == null || p == null || !ReferenceEquals(p, player) || item == null || item.IsAir || target.Operation != null ||
                BuildLane(target.Ordinal) != BlueprintBuildLane.TileBody || !BasicPlacement(target.Placement) ||
                !target.Placement.Platform || target.Cell.HalfBrick || target.Cell.Slope < 1 || target.Cell.Slope > 2 ||
                item.type != target.Placement.UseItemType || item.createTile != target.Placement.TileType || item.placeStyle != target.Placement.Style ||
                !BuildPlatformForm(target, item.createTile) || WorldGen.isGeneratingOrLoadingWorld || !IsBuildTargetCurrent(target)) return false;
            SmartPlatformCell[] cells; SmartPlatformWrite[] writes; int routes, direction, directionY, slope;
            if (!ReadSmartPlatformCells(target, out cells) ||
                !SmartPlatformRoute(target, cells, out routes, out direction, out directionY, out slope, out writes)) return false;
            proof = new SmartPlatformInputProof { Target = target, Source = item, Session = generation,
                Catalog = target.PlacementRevision, TargetVersion = target.RangeStamp, Direction = direction, DirectionY = directionY,
                Routes = routes, ResultSlope = slope, Cells = cells, Writes = writes };
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
            cell = new SmartPlatformCell { Tile = raw, Type = raw.type, Wall = raw.wall, FrameY = raw.frameY, Slope = raw.slope(), Liquid = raw.liquid,
                Active = raw.active(), Half = raw.halfBrick(), Inactive = raw.inActive(), CheckingLiquid = raw.checkingLiquid() };
            return true;
        }
        private static bool SmartPlatformCellSame(SmartPlatformCell a, SmartPlatformCell b, bool identity)
        {
            // The safety proof depends on body/shape/liquid, not paint or
            // connection frames. The allowed CanKillTile branch is frame-free.
            return (!identity || ReferenceEquals(a.Tile, b.Tile)) && a.Type == b.Type && a.Wall == b.Wall && a.FrameY == b.FrameY &&
                a.Active == b.Active && a.Slope == b.Slope && a.Half == b.Half && a.Inactive == b.Inactive &&
                a.Liquid == b.Liquid && a.CheckingLiquid == b.CheckingLiquid;
        }
        private static bool SmartPlatformPresent(SmartPlatformCell cell)
        { return cell.Active && cell.Type < TileID.Sets.Platforms.Length && TileID.Sets.Platforms[cell.Type]; }
        private static int SmartPlatformRouteBit(int x, int y) { return 1 << ((x + 1) * 3 + y + 1); }
        private static bool SmartPlatformRouteAllowed(SmartPlatformInputProof proof, int x, int y)
        { return proof != null && x >= -1 && x <= 1 && y >= -1 && y <= 1 && (proof.Routes & SmartPlatformRouteBit(x, y)) != 0; }
        private bool SmartPlatformRoute(BlueprintBuildTarget target, SmartPlatformCell[] cells, out int routes,
            out int direction, out int directionY, out int slope, out SmartPlatformWrite[] writes)
        {
            routes = 0; direction = directionY = slope = 0; writes = null;
            var center = cells[SmartPlatformIndex(0, 0)]; var above = cells[SmartPlatformIndex(0, -1)];
            if (center.Active || center.Wall == 350) return false;
            int type = target.Placement.TileType;
            if (type < 0 || type >= TileID.Sets.Platforms.Length || !TileID.Sets.Platforms[type] || TileID.Sets.Boulders[type]) return false;
            // The future center is not active yet, so CanPoundTile would read
            // residue. Prove its ordinary platform branch without writing a
            // trial Tile. Ordinary Stone above is legal; tree/container special
            // branches remain conservatively excluded, using their real type.
            if (above.Active && (WorldGen.ForbidsSloping(target.WorldX, target.WorldY - 1) ||
                TileID.Sets.IsATreeTrunk[above.Type] || above.Type == 323 || above.Type == 80 || above.Type == 72)) return false;
            slope = Player.GetSmartPlatformSlope(target.WorldX, target.WorldY);
            if (slope != 0 && slope != target.Cell.Slope) return false;
            // Step_Platforms enumerates endpoints in x/y order. Keep every
            // legal 2-D route to this exact empty destination; Lookup remains
            // the authority choosing among them and any other reachable cell.
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var endpoint = cells[SmartPlatformIndex(dx, dy)];
                if (!SmartPlatformPresent(endpoint)) continue;
                int x = -dx, y = -dy;
                int forbidden = x == y ? 2 : x == -y ? 1 : 0;
                if (forbidden != 0 && endpoint.Slope == forbidden || x == 0 &&
                    (SmartPlatformPresent(cells[SmartPlatformIndex(-1, 0)]) || SmartPlatformPresent(cells[SmartPlatformIndex(1, 0)]))) continue;
                routes |= SmartPlatformRouteBit(x, y);
                if (direction == 0 && directionY == 0) { direction = x; directionY = y; }
            }
            if (routes == 0) return false;
            var locked = Terraria.GameContent.SmartCursorHelper.LockedDesiredDirection;
            if (locked.HasValue && (routes & SmartPlatformRouteBit(locked.Value.X, locked.Value.Y)) != 0)
            { direction = locked.Value.X; directionY = locked.Value.Y; }
            var allowed = new List<SmartPlatformWrite>(3);
            // Mirror only SpinSmartPlatform's finite candidate writes. Each
            // SlopeTile still runs naturally; this is permission, not a command.
            if (slope != 0)
            {
                int upperX = slope == 1 ? -1 : 1;
                var upper = cells[SmartPlatformIndex(upperX, -1)];
                var side = cells[SmartPlatformIndex(2 * upperX, -1)];
                if (SmartPlatformPresent(upper) && upper.Slope == 0 && (!SmartPlatformPresent(side) || !side.Half))
                {
                    if (!AuthorizeSmartPlatformWrite(target, cells, upperX, -1, slope, allowed)) return false;
                    var second = cells[SmartPlatformIndex(2 * upperX, -2)];
                    if (SmartPlatformPresent(second) && second.Slope == 0 &&
                        !AuthorizeSmartPlatformWrite(target, cells, 2 * upperX, -2, slope, allowed)) return false;
                }
                int lowerX = -upperX;
                var lower = cells[SmartPlatformIndex(lowerX, 1)];
                if (SmartPlatformPresent(lower) && lower.Slope == 0 &&
                    !SmartPlatformPresent(cells[SmartPlatformIndex(lowerX + 1, 1)]) &&
                    !SmartPlatformPresent(cells[SmartPlatformIndex(lowerX - 1, 1)]) &&
                    !AuthorizeSmartPlatformWrite(target, cells, lowerX, 1, slope, allowed)) return false;
            }
            else for (int x = -1; x <= 1; x += 2)
            {
                var side = cells[SmartPlatformIndex(x, 0)];
                if (!SmartPlatformPresent(side) || side.Slope == 0) continue;
                var diagonal = cells[SmartPlatformIndex(x + (side.Slope == 1 ? 1 : -1), 1)];
                if ((!SmartPlatformPresent(diagonal) || diagonal.Slope == (side.Slope == 1 ? 2 : 1)) &&
                    !AuthorizeSmartPlatformWrite(target, cells, x, 0, 0, allowed)) return false;
            }
            writes = allowed.ToArray(); return true;
        }
        private bool AuthorizeSmartPlatformWrite(BlueprintBuildTarget center, SmartPlatformCell[] cells, int dx, int dy, int slope,
            List<SmartPlatformWrite> writes)
        {
            int x = center.WorldX + dx, y = center.WorldY + dy; var before = cells[SmartPlatformIndex(dx, dy)];
            // All CanPoundTile reads were gated, including the outer endpoints'
            // immediate vertical neighbors. A native refusal creates no write.
            BlueprintWorldMatchSample sample;
            if (!ReadBuildCell(x, y - 1, out sample) || !ReadBuildCell(x, y + 1, out sample)) return false;
            if (!WorldGen.CanPoundTile(x, y)) return true;
            // At most four already retained immutable source windows are
            // searched by coordinate. The read halo is never write permission.
            for (var node = buildSources.First; node != null; node = node.Next)
            {
                var source = node.Value; var owner = source.Owner;
                if (!ReferenceEquals(owner, center.Range.Owner) || !ReferenceEquals(source.Mask, owner.Mask) ||
                    source.X != owner.OriginX || source.Y != owner.OriginY || source.Catalog != MaterialCatalog.Revision ||
                    !ProjectionQualificationCurrent(owner)) continue;
                var part = source.Part; int index = BuildCellLowerBound(ProjectionSourceCells(part.Window), owner.View.SourceX(x - owner.OriginX), y - owner.OriginY);
                if (index >= part.Window.Cells.Count) continue;
                var raw = EffectiveProjectionCell(owner,part.Window,index);
                if (raw.X != x - owner.OriginX || raw.Y != y - owner.OriginY) continue;
                CaptureObject? obj = part.LogicalObjectId > 0 && part.Window.Objects.Count == 1 ? part.Window.Objects[0] : (CaptureObject?)null;
                var selection = SelectProjectionTarget(owner, raw, obj); var cell = selection.Apply(raw);
                cell = new BlueprintConstructionSelection((ushort)(selection.RequiredBits & ~selection.UnknownBits), 0, MaterialCatalog.Revision).Apply(cell);
                if ((cell.Layers & CaptureLayer.Tile) == 0 || cell.TileType != before.Type || cell.FrameY != before.FrameY ||
                    cell.Slope != slope || cell.HalfBrick || cell.Inactive != before.Inactive) continue;
                var descriptor = MaterialCatalog.Tile(cell, obj); BlueprintWorldObject body = null; long support;
                if (descriptor?.Platform != true || obj.HasValue &&
                    (!projectionMatchSource.TryDescribeTarget(obj.Value, cell, owner.OriginX, owner.OriginY, out body) || body == null || body.Width != 1 || body.Height != 1) ||
                    !projectionChanges.TryGetBuildStamp(source.Range.Watch, source.Range.PartIndex, out support)) continue;
                var target = CreateMaterialBuildTarget(owner, source.Range, cell, descriptor, obj, body,
                    BuildMemberOrdinal(part.Offset + index), support, source.Catalog);
                target.LocalProof = true;
                if (!IsBuildTargetCurrent(target)) continue;
                writes.Add(new SmartPlatformWrite { Target = target, X = dx, Y = dy, Slope = slope }); return true;
            }
            return false;
        }
        private bool SmartPlatformInputCurrent(BuildFlight flight)
        {
            if (flight?.CursorMode != ToolBuildCursorMode.SmartPlatform) return true;
            var proof = flight.SmartProof; var target = flight.Target;
            if (proof == null || proof.Session != generation || !ReferenceEquals(proof.Target, target) ||
                !ReferenceEquals(proof.Source, flight.Source.Item) || proof.Catalog != target.PlacementRevision ||
                proof.TargetVersion != target.RangeStamp || !SmartPlatformRouteAllowed(proof, flight.SmartDirection, flight.SmartDirectionY) ||
                !IsBuildTargetCurrent(target) || WorldGen.isGeneratingOrLoadingWorld) return false;
            for (int dx = -SmartPlatformRadius; dx <= SmartPlatformRadius; dx++)
                for (int dy = -SmartPlatformRadius; dy <= SmartPlatformRadius; dy++)
                {
                    SmartPlatformCell cell;
                    if (!ReadSmartPlatformCell(target.WorldX + dx, target.WorldY + dy, out cell) ||
                        !SmartPlatformCellSame(proof.Cells[SmartPlatformIndex(dx, dy)], cell, true)) return false;
                }
            foreach (var write in proof.Writes) if (!IsBuildTargetCurrent(write.Target)) return false;
            return true;
        }
        private bool SmartPlatformResultValid(BuildFlight flight)
        {
            if (flight?.CursorMode != ToolBuildCursorMode.SmartPlatform) return true;
            var proof = flight.SmartProof;
            if (proof == null) return false;
            // Connection frameX is vanilla framing, not shape permission.
            // Every actual neighbor shape change must match a pre-authorized
            // selected target. The normal write observer invalidates its old
            // candidate/material/Match proof; no second completion map exists.
            for (int dx = -SmartPlatformRadius; dx <= SmartPlatformRadius; dx++)
                for (int dy = -SmartPlatformRadius; dy <= SmartPlatformRadius; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var before = proof.Cells[SmartPlatformIndex(dx, dy)];
                    if (!before.Active || before.Type >= TileID.Sets.Platforms.Length || !TileID.Sets.Platforms[before.Type]) continue;
                    SmartPlatformCell after;
                    if (!ReadSmartPlatformCell(flight.Target.WorldX + dx, flight.Target.WorldY + dy, out after) ||
                        !ReferenceEquals(before.Tile, after.Tile) || !after.Active || before.Type != after.Type || before.FrameY != after.FrameY ||
                        before.Inactive != after.Inactive) return false;
                    if (before.Slope == after.Slope && before.Half == after.Half) continue;
                    bool allowed = false;
                    foreach (var write in proof.Writes)
                        if (write.X == dx && write.Y == dy && write.Slope == after.Slope && !after.Half) { allowed = true; break; }
                    if (!allowed) return false;
                }
            // Actual target shape remains the existing Match/Shape owner's
            // fact. A correctly placed flat body can still use real hammering.
            SmartPlatformCell actual;
            if (!ReadSmartPlatformCell(flight.Target.WorldX, flight.Target.WorldY, out actual)) return false;
            return !actual.Active || actual.Type == flight.Target.Placement.TileType &&
                actual.FrameY == flight.Target.Cell.FrameY && !actual.Half && (actual.Slope == 0 || actual.Slope == proof.ResultSlope);
        }
        private bool SmartPlatformFallbackCurrent(BlueprintBuildTarget target, Item item)
        {
            SmartPlatformFallbackRange range;
            if (target?.Range == null || item == null || !smartPlatformFallbacks.TryGetValue(target.Range, out range)) return false;
            if (range.PreciseOnly) return true;
            foreach (var stamp in range.Entries)
                if (SameSmartPlatformFallbackTarget(stamp, target, item))
                {
                    if (stamp.Retries != 0) return true;
                    bool restored = stamp.Neutral != smartPlatformNeutralEpoch;
                    if (!restored)
                    {
                        restored = false;
                        for (int dx = -1; dx <= 1 && !restored; dx++) for (int dy = -1; dy <= 1; dy++)
                        {
                            SmartPlatformCell actual;
                            if (!ReadSmartPlatformCell(target.WorldX + dx, target.WorldY + dy, out actual)) return true;
                            var old = stamp.Route[(dx + 1) * 3 + dy + 1];
                            // Paint, connection framing, stock and raw token
                            // changes do not explain a different Smart route.
                            if (old.Active != actual.Active || old.Type != actual.Type || old.FrameY != actual.FrameY ||
                                old.Slope != actual.Slope || old.Half != actual.Half || old.Inactive != actual.Inactive) { restored = true; break; }
                        }
                    }
                    if (!restored) return true;
                    return false;
                }
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
            smartPlatformFallbackGeneration = generation;
            var range = smartPlatformFallbacks.GetValue(target.Range, key => new SmartPlatformFallbackRange());
            if (range.PreciseOnly) return true;
            foreach (var entry in range.Entries)
                if (SameSmartPlatformFallbackTarget(entry, target, item))
                { entry.Retries = 1; entry.Neutral = smartPlatformNeutralEpoch; entry.Route = null; return true; }
            if (range.Entries.Count >= SmartPlatformFallbackLimit) { range.PreciseOnly = true; return true; }
            range.Entries.Add(new SmartPlatformFallback { Identity = target.Identity, Mask = target.Mask, Ordinal = target.Ordinal,
                X = target.WorldX, Y = target.WorldY, Material = flight.Source.Type, TileType = target.Placement.TileType,
                Style = target.Placement.Style, Catalog = target.PlacementRevision, Slope = target.Cell.Slope,
                Neutral = smartPlatformNeutralEpoch, Route = FreezeSmartPlatformRoute(flight.SmartProof.Cells) });
            return true;
        }
        private static SmartPlatformRouteState[] FreezeSmartPlatformRoute(SmartPlatformCell[] cells)
        {
            var route = new SmartPlatformRouteState[9];
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
            {
                var value = cells[SmartPlatformIndex(dx, dy)];
                route[(dx + 1) * 3 + dy + 1] = new SmartPlatformRouteState { Type = value.Type, FrameY = value.FrameY,
                    Slope = value.Slope, Active = value.Active, Half = value.Half, Inactive = value.Inactive };
            }
            return route;
        }
        private ToolInputGesture AutomaticBuildInputGesture(BuildFlight flight)
        {
            if (flight == null) return ToolInputGesture.NeutralRelease;
            return flight.CursorMode == ToolBuildCursorMode.SmartPlatform && flight.SmartNeutralRequired &&
                (flight.SmartNeutralFrame < 0 || buildTools.Input.Frame <= flight.SmartNeutralFrame)
                ? ToolInputGesture.NeutralRelease : flight.InputGesture;
        }
        private void ObserveAutomaticBuildNeutral(Player p)
        {
            var flight = activeBuild;
            if (!ReferenceEquals(p, player) || p.controlUseItem || projectionScene.Count == 0 ||
                flight?.SmartNeutralRequired != true && smartPlatformFallbackGeneration != generation) return;
            // Called only after the actual Lookup returned under unpressed use.
            // Lookup may already have chosen a new direction after clearing its
            // old lock; a null final lock is neither necessary nor fabricated.
            if (smartPlatformNeutralFrame != buildTools.Input.Frame)
            { smartPlatformNeutralFrame = buildTools.Input.Frame; smartPlatformNeutralEpoch++; }
            if (flight?.SmartNeutralRequired == true && flight.SmartNeutralFrame < 0) flight.SmartNeutralFrame = buildTools.Input.Frame;
        }
        private static bool SameSmartPlatformSegment(BuildFlight current, BuildFlight next)
        {
            if (next.CursorMode != ToolBuildCursorMode.SmartPlatform) return true;
            int x = next.Target.WorldX - current.Target.WorldX, y = next.Target.WorldY - current.Target.WorldY;
            return current.CursorMode == next.CursorMode && current.SmartDirection == next.SmartDirection &&
                current.SmartDirectionY == next.SmartDirectionY &&
                (x == next.SmartDirection && y == next.SmartDirectionY || x == 2 * next.SmartDirection && y == 2 * next.SmartDirectionY);
        }
    }
}
