using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    public enum BlueprintBodyState { Unknown, Missing, Conflict, Same }
    public static class BlueprintMaterialSteps
    {
        public static BlueprintBodyState SameBody(CaptureCell target, BlueprintWorldMatchSample world, bool wall = false)
        {
            if (!world.Available || !wall && target.UnreliableObject) return BlueprintBodyState.Unknown;
            CaptureLayer layer = wall ? CaptureLayer.Wall : CaptureLayer.Tile;
            if ((world.Cell.Layers & layer) == 0) return BlueprintBodyState.Missing;
            if (wall) return target.WallType == world.Cell.WallType ? BlueprintBodyState.Same : BlueprintBodyState.Conflict;
            if (world.ObjectUnknown || target.ObjectId > 0) return BlueprintBodyState.Unknown;
            short x, y; BlueprintWorldMatchComparer.NormalizeFrame(target, world.FrameKind, out x, out y);
            return target.TileType == world.Cell.TileType && (!world.CompareFrames || x == world.SemanticFrameX && y == world.SemanticFrameY) ? BlueprintBodyState.Same : BlueprintBodyState.Conflict;
        }
        public static BlueprintBodyState SameBody(CaptureCell target, BlueprintWorldMatchSample world, BlueprintPlacementCatalog catalog, bool wall = false)
        {
            if (wall) return SameBody(target, world, true);
            var descriptor = catalog?.Tile(target);
            if (descriptor?.RequiresObject == true && target.ObjectId == 0) return BlueprintBodyState.Unknown;
            if (descriptor?.Platform == true && world.Available && (world.Cell.Layers & CaptureLayer.Tile) != 0 && target.TileType == world.Cell.TileType)
                return target.FrameY == world.Cell.FrameY ? BlueprintBodyState.Same : BlueprintBodyState.Conflict;
            return SameBody(target, world);
        }

        public static BlueprintBodyState SameBody(BlueprintWorldObject target, IReadOnlyList<CaptureCell> members,
            IReadOnlyList<BlueprintWorldMatchSample> world, bool current)
        {
            if (!current || target == null || members == null || members.Count != target.MemberCount || world == null) return BlueprintBodyState.Unknown;
            var proof = new BlueprintObjectBodyProof(target);
            foreach (var sample in world) proof.Observe(sample);
            return proof.Complete();
        }
        public static bool EmptyObjectFootprint(BlueprintWorldObject target, IReadOnlyList<BlueprintWorldMatchSample> world)
        {
            if (target == null || !Rectangle(target, world)) return false;
            foreach (var sample in world) if (!sample.Available || sample.ObjectUnknown || (sample.Cell.Layers & CaptureLayer.Tile) != 0) return false;
            return true;
        }
        private static bool Rectangle(BlueprintWorldObject value, IReadOnlyList<BlueprintWorldMatchSample> cells)
        {
            if (value == null || cells == null || cells.Count != value.MemberCount) return false;
            for (int i = 0; i < cells.Count; i++) if (cells[i].Cell.X != value.WorldX + i % value.Width || cells[i].Cell.Y != value.WorldY + i / value.Width) return false;
            return true;
        }

        public static void StaticCell(CaptureCell target, BlueprintPlacementCatalog catalog, BlueprintMaterialAccumulator into)
        {
            if (catalog == null) { into.Unresolved("放置资料尚未就绪"); return; }
            if ((target.Layers & CaptureLayer.Tile) != 0 && target.ObjectId == 0)
            {
                var descriptor = catalog.Tile(target);
                if (target.UnreliableObject || descriptor?.RequiresObject == true) into.Unresolved("对象尚未完整确认"); else into.Add(descriptor);
            }
            if ((target.Layers & CaptureLayer.Wall) != 0) into.Add(catalog.Wall(target.WallType));
            Appearance(target, default(CaptureCell), false, false, catalog, into);
            if ((target.Layers & CaptureLayer.Wire) != 0) into.Add(catalog.WireItemType, BitCount(target.Wires));
            if ((target.Layers & CaptureLayer.Actuator) != 0 && target.Actuator) into.Add(catalog.ActuatorItemType, 1);
            if ((target.Layers & CaptureLayer.Liquid) != 0 && target.LiquidAmount > 0) into.Unresolved("精确液体量尚无可靠的容器步骤解释");
        }
        public static void StaticObject(CaptureObject obj, CaptureCell first, BlueprintPlacementCatalog catalog, BlueprintMaterialAccumulator into)
        { if (!obj.Reliable) into.Unresolved("对象尚未完整确认"); else into.Add(catalog.Tile(first)); }

        public static void RequiredSteps(CaptureCell target, BlueprintWorldMatchSample world, BlueprintPlacementCatalog catalog,
            BlueprintMaterialAccumulator into, BlueprintBodyState? objectBody = null)
        {
            if (!world.Available) { into.Unresolved("世界内容尚未可读"); return; }
            bool tileSame = false, wallSame = false; CaptureCell appearanceTarget = target;
            if ((target.Layers & CaptureLayer.Tile) != 0)
            {
                var body = objectBody ?? SameBody(target, world, catalog);
                if (body == BlueprintBodyState.Unknown) { into.Unresolved("本体尚未完整确认"); appearanceTarget.Layers &= ~CaptureLayer.Tile; }
                else if (target.ObjectId == 0 && body != BlueprintBodyState.Same) into.Add(catalog.Tile(target));
                tileSame = body == BlueprintBodyState.Same;
            }
            if ((target.Layers & CaptureLayer.Wall) != 0)
            {
                var body = SameBody(target, world, true);
                if (body == BlueprintBodyState.Unknown) { into.Unresolved("墙体尚未可读"); appearanceTarget.Layers &= ~CaptureLayer.Wall; }
                else if (body != BlueprintBodyState.Same) into.Add(catalog.Wall(target.WallType));
                wallSame = body == BlueprintBodyState.Same;
            }
            Appearance(appearanceTarget, world.Cell, tileSame, wallSame, catalog, into);
            if ((target.Layers & CaptureLayer.Wire) != 0) into.Add(catalog.WireItemType, BitCount((byte)(target.Wires & ~world.Cell.Wires)));
            if ((target.Layers & CaptureLayer.Actuator) != 0 && target.Actuator && !world.Cell.Actuator) into.Add(catalog.ActuatorItemType, 1);
            if ((target.Layers & CaptureLayer.Liquid) != 0 && (target.LiquidKind != world.Cell.LiquidKind || target.LiquidAmount != world.Cell.LiquidAmount))
                into.Unresolved("精确液体量尚无可靠的容器步骤解释");
        }
        public static void ObjectBody(BlueprintBodyState body, CaptureCell first, BlueprintPlacementCatalog catalog, BlueprintMaterialAccumulator into)
        { if (body == BlueprintBodyState.Unknown) into.Unresolved("对象尚未完整确认"); else if (body != BlueprintBodyState.Same) into.Add(catalog.Tile(first)); }

        // Paint and both coatings consume separately on each actual member and
        // layer. Scraping is a reusable tool step; clearing coatings clears
        // both flags, so a desired retained coating must then be reapplied.
        private static void Appearance(CaptureCell target, CaptureCell actual, bool tileSame, bool wallSame,
            BlueprintPlacementCatalog catalog, BlueprintMaterialAccumulator into)
        {
            bool wallScrape = wallSame && (target.Layers & CaptureLayer.Wall) != 0 &&
                (target.WallPaint == 0 && actual.WallPaint != 0 || actual.InvisibleWall && !target.InvisibleWall || actual.FullbrightWall && !target.FullbrightWall);
            bool foreground = (actual.Layers & CaptureLayer.Tile) != 0 && (actual.TilePaint != 0 || actual.InvisibleTile || actual.FullbrightTile);
            // The native scraper short-circuits Tile paint, Tile coatings,
            // Wall paint, Wall coatings in that order. Reaching a wall scrape
            // first clears an existing foreground appearance; restore a saved
            // foreground only after the wall is finished. An unselected
            // foreground has no captured restoration contract to infer here.
            bool restoreForeground = wallScrape && foreground && tileSame && (target.Layers & CaptureLayer.Tile) != 0;
            if (wallScrape && foreground && (target.Layers & CaptureLayer.Tile) == 0)
                into.Unresolved("墙面刮除的前景步骤尚未确定");
            if ((target.Layers & CaptureLayer.Tile) != 0)
            {
                LayerAppearance(target.TilePaint, target.InvisibleTile, target.FullbrightTile,
                    tileSame && !restoreForeground ? actual.TilePaint : (byte)0,
                    tileSame && !restoreForeground && actual.InvisibleTile, tileSame && !restoreForeground && actual.FullbrightTile, catalog, into);
                // Hammer shape and actuation are reusable tool operations in
                // locked .8. Their zero direct consumption does not mean Match.
                if (target.Slope > 4 || target.HalfBrick && target.Slope != 0) into.Unresolved("形状步骤尚未确认");
                if (target.Inactive != (tileSame && actual.Inactive) && !target.Actuator && !(tileSame && actual.Actuator))
                    into.Unresolved("此致动状态的临时材料步骤尚未确定");
            }
            if ((target.Layers & CaptureLayer.Wall) != 0)
                LayerAppearance(target.WallPaint, target.InvisibleWall, target.FullbrightWall,
                    wallSame ? actual.WallPaint : (byte)0, wallSame && actual.InvisibleWall, wallSame && actual.FullbrightWall, catalog, into);
        }
        private static void LayerAppearance(byte paint, bool invisible, bool fullbright, byte actualPaint, bool actualInvisible, bool actualFullbright,
            BlueprintPlacementCatalog catalog, BlueprintMaterialAccumulator into)
        {
            bool clear = actualInvisible && !invisible || actualFullbright && !fullbright;
            // A coating clear is reached only after paint was scraped to 0.
            // Reapplying a retained paint is a real consumable step as well.
            if (paint > 0 && (paint != actualPaint || clear && actualPaint > 0)) into.Add(catalog.Paint(paint), 1);
            if (invisible && (clear || !actualInvisible)) into.Add(catalog.Coating(2), 1);
            if (fullbright && (clear || !actualFullbright)) into.Add(catalog.Coating(1), 1);
        }
        private static int BitCount(byte bits) { int result = 0; bits &= 15; while (bits != 0) { result += bits & 1; bits >>= 1; } return result; }
    }

    // Incremental structural proof rides the existing bounded object reads.
    // Appearance is deliberately absent; final world Match still checks it.
    public sealed class BlueprintObjectBodyProof
    {
        private readonly BlueprintWorldObject target;
        private BlueprintWorldObject observed;
        private bool unknown, conflict, missing, targetRectangle = true, observedRectangle = true;
        private int count, active;
        public BlueprintObjectBodyProof(BlueprintWorldObject target) { this.target = target; }
        public void Observe(BlueprintWorldMatchSample sample)
        {
            int index = count++;
            if (target == null) { unknown = true; return; }
            targetRectangle &= sample.Cell.X == target.WorldX + index % target.Width && sample.Cell.Y == target.WorldY + index / target.Width;
            if (!sample.Available || sample.ObjectUnknown) { unknown = true; return; }
            if ((sample.Cell.Layers & CaptureLayer.Tile) == 0) { missing = true; return; }
            active++;
            if (sample.Object == null || !target.SamePlacement(sample.Object) ||
                (sample.Object.Family == BlueprintWorldObjectFamily.Door ? sample.Cell.TileType != 10 && sample.Cell.TileType != 11 : sample.Cell.TileType != sample.Object.Kind)) { conflict = true; return; }
            if (observed == null) observed = sample.Object;
            else if (!observed.SameObservedObject(sample.Object)) conflict = true;
            observedRectangle &= sample.Cell.X == observed.WorldX + index % observed.Width && sample.Cell.Y == observed.WorldY + index / observed.Width;
        }
        public BlueprintBodyState Complete()
        {
            if (conflict) return BlueprintBodyState.Conflict;
            if (unknown || target == null) return BlueprintBodyState.Unknown;
            if (active == 0) return count == target.MemberCount && targetRectangle ? BlueprintBodyState.Missing : BlueprintBodyState.Unknown;
            if (missing) return BlueprintBodyState.Conflict;
            return observed != null && count == observed.MemberCount && observedRectangle ? BlueprintBodyState.Same : BlueprintBodyState.Unknown;
        }
    }
}
