namespace JueMingR.Features.Blueprint
{
    public enum BlueprintAppearanceAction
    {
        None, BlockedForeground, ScrapeTilePaint, ScrapeTileCoatings,
        ScrapeWallPaint, ScrapeWallCoatings, PaintTile, PaintWall, CoatTile, CoatWall
    }

    public readonly struct BlueprintAppearanceStep
    {
        public BlueprintAppearanceStep(BlueprintAppearanceAction action, byte mode = 0, bool restoreForeground = false)
        { Action = action; Mode = mode; RequiresForegroundRestoration = restoreForeground; }
        public BlueprintAppearanceAction Action { get; }
        public byte Mode { get; }
        public bool RequiresForegroundRestoration { get; }
        public bool IsWall { get { return Action == BlueprintAppearanceAction.ScrapeWallPaint || Action == BlueprintAppearanceAction.ScrapeWallCoatings || Action == BlueprintAppearanceAction.PaintWall || Action == BlueprintAppearanceAction.CoatWall; } }
        public bool IsScrape { get { return Action == BlueprintAppearanceAction.ScrapeTilePaint || Action == BlueprintAppearanceAction.ScrapeTileCoatings || Action == BlueprintAppearanceAction.ScrapeWallPaint || Action == BlueprintAppearanceAction.ScrapeWallCoatings; } }
        public bool HasAction { get { return Action != BlueprintAppearanceAction.None && Action != BlueprintAppearanceAction.BlockedForeground; } }
    }

    // A next step is derived from the current cell, never retained as a second
    // completion ledger. The native scraper has no layer selector: it clears
    // Tile paint, both Tile coatings, Wall paint, then both Wall coatings.
    public static class BlueprintConstructionAppearance
    {
        public static BlueprintAppearanceStep Next(CaptureCell target, CaptureCell actual, bool tileSame, bool wallSame)
        {
            bool tile = tileSame && (target.Layers & CaptureLayer.Tile) != 0;
            bool wall = wallSame && (target.Layers & CaptureLayer.Wall) != 0;
            bool wallScrape = wall && NeedsScrape(target.WallPaint, target.InvisibleWall, target.FullbrightWall,
                actual.WallPaint, actual.InvisibleWall, actual.FullbrightWall);
            bool foreground = (actual.Layers & CaptureLayer.Tile) != 0 &&
                HasAppearance(actual.TilePaint, actual.InvisibleTile, actual.FullbrightTile);
            if (wallScrape)
            {
                // An unselected or different foreground has no authorized
                // restoration contract. Do not destroy it to reach the wall.
                if (foreground && !tile) return new BlueprintAppearanceStep(BlueprintAppearanceAction.BlockedForeground);
                bool restore = tile && HasAppearance(target.TilePaint, target.InvisibleTile, target.FullbrightTile);
                if (foreground)
                    return new BlueprintAppearanceStep(actual.TilePaint != 0 ? BlueprintAppearanceAction.ScrapeTilePaint : BlueprintAppearanceAction.ScrapeTileCoatings, 0, restore);
                return new BlueprintAppearanceStep(actual.WallPaint != 0 ? BlueprintAppearanceAction.ScrapeWallPaint : BlueprintAppearanceAction.ScrapeWallCoatings, 0, restore);
            }
            // Finish the wall before restoring its selected foreground. An
            // early foreground repaint would make the next wall scrape clear
            // that foreground again and consume its restoration repeatedly.
            if (wall)
            {
                var step = Layer(target.WallPaint, target.InvisibleWall, target.FullbrightWall,
                    actual.WallPaint, actual.InvisibleWall, actual.FullbrightWall, true);
                if (step.HasAction) return step;
            }
            return tile ? Layer(target.TilePaint, target.InvisibleTile, target.FullbrightTile,
                actual.TilePaint, actual.InvisibleTile, actual.FullbrightTile, false) : default(BlueprintAppearanceStep);
        }

        public static bool NeedsScrape(byte paint, bool invisible, bool fullbright, byte actualPaint, bool actualInvisible, bool actualFullbright)
        { return paint == 0 && actualPaint != 0 || actualInvisible && !invisible || actualFullbright && !fullbright; }

        private static bool HasAppearance(byte paint, bool invisible, bool fullbright)
        { return paint != 0 || invisible || fullbright; }

        private static BlueprintAppearanceStep Layer(byte paint, bool invisible, bool fullbright,
            byte actualPaint, bool actualInvisible, bool actualFullbright, bool wall)
        {
            bool clear = actualInvisible && !invisible || actualFullbright && !fullbright;
            if ((paint == 0 || clear) && actualPaint != 0)
                return new BlueprintAppearanceStep(wall ? BlueprintAppearanceAction.ScrapeWallPaint : BlueprintAppearanceAction.ScrapeTilePaint);
            if (clear)
                return new BlueprintAppearanceStep(wall ? BlueprintAppearanceAction.ScrapeWallCoatings : BlueprintAppearanceAction.ScrapeTileCoatings);
            if (paint != actualPaint)
                return new BlueprintAppearanceStep(wall ? BlueprintAppearanceAction.PaintWall : BlueprintAppearanceAction.PaintTile, paint);
            if (fullbright && !actualFullbright)
                return new BlueprintAppearanceStep(wall ? BlueprintAppearanceAction.CoatWall : BlueprintAppearanceAction.CoatTile, 1);
            if (invisible && !actualInvisible)
                return new BlueprintAppearanceStep(wall ? BlueprintAppearanceAction.CoatWall : BlueprintAppearanceAction.CoatTile, 2);
            return default(BlueprintAppearanceStep);
        }

        // Billing uses the same clear/restore semantics as execution. Tools
        // are reusable capabilities; only paint and individual coatings count.
        public static void AddMaterials(CaptureCell target, CaptureCell actual, bool tileSame, bool wallSame,
            BlueprintPlacementCatalog catalog, BlueprintMaterialAccumulator into, bool tileOnly = false, bool foregroundHandledElsewhere = false)
        {
            bool wallScrape = wallSame && (target.Layers & CaptureLayer.Wall) != 0 &&
                NeedsScrape(target.WallPaint, target.InvisibleWall, target.FullbrightWall, actual.WallPaint, actual.InvisibleWall, actual.FullbrightWall);
            bool foreground = (actual.Layers & CaptureLayer.Tile) != 0 && HasAppearance(actual.TilePaint, actual.InvisibleTile, actual.FullbrightTile);
            bool restore = wallScrape && foreground && tileSame && (target.Layers & CaptureLayer.Tile) != 0;
            if (wallScrape && foreground && (target.Layers & CaptureLayer.Tile) == 0 && !foregroundHandledElsewhere)
                into.Unresolved("墙面刮除的前景步骤尚未确定");
            if ((target.Layers & CaptureLayer.Tile) != 0)
                AddLayerMaterials(target.TilePaint, target.InvisibleTile, target.FullbrightTile,
                    tileSame && !restore ? actual.TilePaint : (byte)0, tileSame && !restore && actual.InvisibleTile,
                    tileSame && !restore && actual.FullbrightTile, catalog, into);
            if (!tileOnly && (target.Layers & CaptureLayer.Wall) != 0)
                AddLayerMaterials(target.WallPaint, target.InvisibleWall, target.FullbrightWall,
                    wallSame ? actual.WallPaint : (byte)0, wallSame && actual.InvisibleWall, wallSame && actual.FullbrightWall, catalog, into);
        }

        public static void AddLayerMaterials(byte paint, bool invisible, bool fullbright, byte actualPaint, bool actualInvisible, bool actualFullbright,
            BlueprintPlacementCatalog catalog, BlueprintMaterialAccumulator into)
        {
            bool clear = actualInvisible && !invisible || actualFullbright && !fullbright;
            if (paint > 0 && (paint != actualPaint || clear && actualPaint > 0)) into.Add(catalog.Paint(paint), 1);
            if (invisible && (clear || !actualInvisible)) into.Add(catalog.Coating(2), 1);
            if (fullbright && (clear || !actualFullbright)) into.Add(catalog.Coating(1), 1);
        }
    }
}
