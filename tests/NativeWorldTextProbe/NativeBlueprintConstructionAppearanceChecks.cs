using System;
using System.Linq;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        // The construction runner supplies the original EmptyWorld and native
        // Player.Update fixture. Only setup writes the initial world below;
        // every destination change after SetAutomatic comes from production.
        internal static void ConstructionAppearance(object context, object host, object tools, object input)
        {
            Action<string, Action> check = (name, body) =>
            {
                try { body(); Console.WriteLine("PASS BlueprintConstructionAppearance case=" + name); }
                catch
                {
                    try { DumpAppearanceCase(name, host, tools); }
                    catch (Exception error) { Console.Error.WriteLine("APPEARANCE diagnostic failed: " + error.Message); }
                    throw;
                }
            };
            check("pure difference contract", AppearanceDifferenceContract);
            check("hammer", () => ConstructionHammer(context, host, tools, input));
            check("mixed paint", () => ConstructionMixedPaint(context, host, tools, input));
            check("last paint", () => ConstructionLastPaint(context, host, tools, input));
            check("coating restoration", () => ConstructionCoatingRestoration(context, host, tools, input));
            check("same color restoration refill", () => ConstructionSameColorRestorationRefill(context, host, tools, input));
            check("unselected foreground", () => ConstructionUnselectedForeground(context, host, tools, input));
            check("platform boundary", () => ConstructionPlatformBoundary(context, host, tools, input));
            Console.WriteLine("PASS BlueprintConstructionAppearance: native hammer repeat, protected walls, mixed original paint preparation/return, coating and cross-layer restoration with same-stack refill, unselected foreground refusal, platform boundary; real multiplayer/GPU/FPS NOT_RUN");
        }

        private static void DumpAppearanceCase(string name, object host, object tools)
        {
            var p = Main.LocalPlayer;
            Console.Error.WriteLine("APPEARANCE case=" + name + " scene=" + Scene(host).Length + " checked=" + Get(host, "BuildNativeChecks") +
                " started=" + Get(host, "BuildUsesStarted") + " unknown=" + Get(host, "BuildUnknownResults") +
                " position=" + p.position + " reach=" + Player.tileRangeX + "/" + Player.tileRangeY + "+" + p.blockRange);
            foreach (int x in new[] { 44, 46 })
            {
                var tile = Main.tile[x, 42];
                Console.Error.WriteLine("APPEARANCE cell=" + x + ",42 " + (tile == null ? "unavailable" :
                    "tile=" + tile.active() + ":" + tile.type + " shape=" + tile.slope() + "/" + tile.halfBrick() +
                    " tilePaint/glow/echo=" + tile.color() + "/" + tile.fullbrightBlock() + "/" + tile.invisibleBlock() +
                    " wall=" + tile.wall + " wallPaint/glow/echo=" + tile.wallColor() + "/" + tile.fullbrightWall() + "/" + tile.invisibleWall()));
            }
            Console.Error.WriteLine("APPEARANCE inventory=" + string.Join(";", new[] { 0, 10, 11, 12, 13, 14, 15, 16, 54 }.Select(slot =>
            {
                var item = p.inventory[slot];
                return slot + ":" + item.type + "x" + item.stack + " paint/coat=" + item.paint + "/" + item.paintCoating +
                    " eligible=" + (slot < 50 ? Call(tools, "BuildInventoryEligible", slot, item).ToString() : "priority-slot");
            })));
            var published = (BlueprintMaterialInventory)Get(host, "MaterialInventory");
            var cached = (BlueprintMaterialInventory)Get(host, "appearanceInventory");
            var counts = (int[])Get(host, "appearanceToolCounts");
            var indexedTools = (int[,])Get(host, "appearanceTools");
            var heads = (System.Collections.Generic.Dictionary<int, int>)Get(host, "appearancePaintHeads");
            var catalog = (BlueprintPlacementCatalog)Get(host, "MaterialCatalog");
            Console.Error.WriteLine("APPEARANCE cache samePublication=" + ReferenceEquals(cached, published) +
                " toolsHammer/brush/roller/scraper=" + string.Join("/", counts) +
                " toolSlots=" + string.Join(";", Enumerable.Range(0, 4).Select(kind => kind + ":" +
                    string.Join(",", Enumerable.Range(0, counts[kind]).Select(index => indexedTools[kind, index])))) +
                " heads=" + string.Join(";", new[] { ItemID.RedPaint, ItemID.BluePaint, ItemID.GlowPaint, ItemID.EchoCoating }.Select(type =>
                { int slot; return type + ":" + (heads.TryGetValue(type, out slot) ? slot.ToString() : "absent"); })) +
                " catalogRed/blue/glow/echo=" + catalog.Paint(1) + "/" + catalog.Paint(9) + "/" + catalog.Coating(1) + "/" + catalog.Coating(2));
            Console.Error.WriteLine("APPEARANCE snapshot=" + string.Join(";", published.Slots.Where(slot => slot.Slot >= 10 && slot.Slot <= 16)
                .Select(slot => slot.Slot + ":" + slot.ItemType + "x" + slot.Stack + " eligible=" + slot.Eligible)) +
                " toolReach44/46=" + string.Join(";", new[] { 14, 15, 16 }.Select(slot => slot + ":" +
                    p.IsInTileInteractionRange(44, 42, Terraria.DataStructures.TileReachCheckSettings.Simple, p.inventory[slot].tileBoost + p.blockRange) + "/" +
                    p.IsInTileInteractionRange(46, 42, Terraria.DataStructures.TileReachCheckSettings.Simple, p.inventory[slot].tileBoost + p.blockRange))));
            foreach (var scene in Scene(host))
            {
                var match = GetOptional(scene, "Match"); if (match == null) continue;
                foreach (var range in ((System.Collections.IEnumerable)Get(match, "Ranges")).Cast<object>())
                {
                    var material = GetOptional(range, "Materials"); if (material == null) continue;
                    Console.Error.WriteLine("APPEARANCE waits=" + GetOptional(material, "BuildWaits") + " retry=" + GetOptional(material, "EligibilityRetry") +
                        " retryOrdinal=" + GetOptional(material, "EligibilityRetryOrdinal") + " retryAt=" + GetOptional(material, "EligibilityRetryAt") +
                        " retryPending=" + GetOptional(material, "BuildRetryPending"));
                }
            }
        }

        private static void DumpAppearanceDifference(object host, CaptureCell target, int x)
        {
            var tile = Main.tile[x, 42]; if (tile == null) return;
            var actual = new CaptureCell { Layers = (tile.active() ? CaptureLayer.Tile : CaptureLayer.None) | (tile.wall > 0 ? CaptureLayer.Wall : CaptureLayer.None),
                TileType = tile.type, WallType = tile.wall, TilePaint = tile.color(), WallPaint = tile.wallColor(),
                FullbrightTile = tile.fullbrightBlock(), InvisibleTile = tile.invisibleBlock(), FullbrightWall = tile.fullbrightWall(), InvisibleWall = tile.invisibleWall() };
            bool tileSame = tile.active() && tile.type == target.TileType, wallSame = tile.wall > 0 && tile.wall == target.WallType;
            var next = BlueprintConstructionAppearance.Next(target, actual, tileSame, wallSame);
            var catalog = (BlueprintPlacementCatalog)Get(host, "MaterialCatalog");
            int supply = next.Action == BlueprintAppearanceAction.PaintTile || next.Action == BlueprintAppearanceAction.PaintWall ? catalog.Paint(next.Mode) :
                next.Action == BlueprintAppearanceAction.CoatTile || next.Action == BlueprintAppearanceAction.CoatWall ? catalog.Coating(next.Mode) : 0;
            Console.Error.WriteLine("APPEARANCE difference cell=" + x + ",42 bodySame=" + tileSame + "/" + wallSame +
                " next=" + next.Action + " mode=" + next.Mode + " restore=" + next.RequiresForegroundRestoration + " supply=" + supply +
                " targetTilePaint/glow/echo=" + target.TilePaint + "/" + target.FullbrightTile + "/" + target.InvisibleTile +
                " targetWallPaint/glow/echo=" + target.WallPaint + "/" + target.FullbrightWall + "/" + target.InvisibleWall);
        }

        private static void AppearanceDifferenceContract()
        {
            var target = new CaptureCell { Layers = CaptureLayer.Tile | CaptureLayer.Wall, TileType = TileID.Stone,
                WallType = WallID.Stone, TilePaint = 1, FullbrightTile = true, WallPaint = 9, InvisibleWall = true };
            var actual = target; actual.InvisibleTile = actual.FullbrightWall = true;
            var next = BlueprintConstructionAppearance.Next(target, actual, true, true);
            Require(next.Action == BlueprintAppearanceAction.ScrapeTilePaint && next.RequiresForegroundRestoration,
                "wall coating removal first reaches foreground paint and requires real foreground restoration supply");
            actual.TilePaint = 0;
            Require(BlueprintConstructionAppearance.Next(target, actual, true, true).Action == BlueprintAppearanceAction.ScrapeTileCoatings,
                "native scraper clears both foreground coatings before touching wall paint");
            actual.InvisibleTile = actual.FullbrightTile = false;
            Require(BlueprintConstructionAppearance.Next(target, actual, true, true).Action == BlueprintAppearanceAction.ScrapeWallPaint,
                "wall coating removal must clear even a retained wall paint before clearing both wall coatings");
            var wallOnly = target.WithoutTile();
            var unselected = target; unselected.FullbrightWall = true;
            Require(BlueprintConstructionAppearance.Next(wallOnly, unselected, false, true).Action == BlueprintAppearanceAction.BlockedForeground,
                "unselected painted foreground is an explicit local blocker, never inferred as authorized restoration");
            var oneLayer = target; oneLayer.Layers = CaptureLayer.Tile;
            var both = oneLayer; both.InvisibleTile = true;
            Require(BlueprintConstructionAppearance.Next(oneLayer, both, true, false).Action == BlueprintAppearanceAction.ScrapeTilePaint,
                "retaining one coating while removing another requires clearing paint then both coatings");
        }

        private static void ConstructionHammer(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input);
            p.inventory[12].SetDefaults(ItemID.WoodenHammer);
            NativeToolsChecks.Tile(44, 42, TileID.Stone);
            WorldGen.PlaceWall(44, 42, WallID.Stone);
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile | CaptureLayer.Wall,
                TileType = TileID.Stone, WallType = WallID.Stone, Slope = 3 });
            Place(context, host, input, entry, 44, 42);
            long checks = (long)Get(host, "BuildNativeChecks");
            SetAutomatic(host, input, true);
            Until(context, input, () => Scene(host).Length == 0);
            Require(Main.tile[44, 42].active() && Main.tile[44, 42].type == TileID.Stone &&
                Main.tile[44, 42].slope() == 3 && !Main.tile[44, 42].halfBrick() && Main.tile[44, 42].wall == WallID.Stone,
                "multiple true neutral/pound cycles reach captured ordinary slope without digging body or background wall");
            Require((long)Get(host, "BuildNativeChecks") > checks + 1 && p.inventory[12].type == ItemID.WoodenHammer && p.inventory[12].stack == 1,
                "shape changes use more than one actual consumer and consume no body or hammer");
            Until(context, input, () => !(bool)Get(Get(tools, "Use"), "Active"));
            long settled = (long)Get(host, "BuildUsesStarted");
            for (int frame = 0; frame < 80; frame++) Step(context, input);
            Require((long)Get(host, "BuildUsesStarted") == settled && Main.tile[44, 42].wall == WallID.Stone,
                "full Match retires shape work and settled background wall is not hit on a later held update");
        }

        private static void ConstructionMixedPaint(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input);
            p.inventory[12].SetDefaults(ItemID.RedPaint); p.inventory[12].stack = 2; var red = p.inventory[12];
            p.inventory[13].SetDefaults(ItemID.BluePaint); p.inventory[13].stack = 2; var blue = p.inventory[13];
            p.inventory[14].SetDefaults(ItemID.Paintbrush); p.inventory[15].SetDefaults(ItemID.PaintRoller);
            p.inventory[54].SetDefaults(ItemID.BluePaint); p.inventory[54].stack = 7; var priority = p.inventory[54];
            NativeToolsChecks.Tile(44, 42, TileID.Stone); WorldGen.PlaceWall(44, 42, WallID.Stone);
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile | CaptureLayer.Wall, TileType = TileID.Stone,
                WallType = WallID.Stone, TilePaint = red.paint, WallPaint = blue.paint });
            Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
            Until(context, input, () => Scene(host).Length == 0 && ReferenceEquals(p.inventory[12], red) &&
                ReferenceEquals(p.inventory[13], blue) && ReferenceEquals(p.inventory[54], priority) && Main.mouseItem.IsAir);
            Require(Main.tile[44, 42].color() == red.paint && Main.tile[44, 42].wallColor() == blue.paint && red.stack == 1 && blue.stack == 1,
                "two true main-inventory paint sources color their own layers and each spends one under original FindPaint priority");
            Require(priority.stack == 7 && p.inventory[14].type == ItemID.Paintbrush && p.inventory[15].type == ItemID.PaintRoller,
                "same-type ammo priority is isolated through native inventory preparation, not merged or consumed as new main supply");
        }

        private static void ConstructionCoatingRestoration(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input);
            p.inventory[10].SetDefaults(ItemID.RedPaint); p.inventory[10].stack = 4;
            p.inventory[11].SetDefaults(ItemID.BluePaint); p.inventory[11].stack = 3;
            p.inventory[12].SetDefaults(ItemID.GlowPaint); p.inventory[12].stack = 3;
            p.inventory[13].SetDefaults(ItemID.EchoCoating); p.inventory[13].stack = 4;
            p.inventory[14].SetDefaults(ItemID.Paintbrush); p.inventory[15].SetDefaults(ItemID.PaintRoller); p.inventory[16].SetDefaults(ItemID.PaintScraper);
            byte red = p.inventory[10].paint, blue = p.inventory[11].paint;
            foreach (int x in new[] { 44, 46 })
            {
                NativeToolsChecks.Tile(x, 42, TileID.Stone);
                WorldGen.paintTile(x, 42, red, false, false);
                WorldGen.paintCoatTile(x, 42, 1, false, false); WorldGen.paintCoatTile(x, 42, 2, false, false);
            }
            WorldGen.PlaceWall(44, 42, WallID.Stone); WorldGen.paintWall(44, 42, red, false, false);
            WorldGen.paintCoatWall(44, 42, 1, false, false); WorldGen.paintCoatWall(44, 42, 2, false, false);
            var firstTarget = new CaptureCell { Layers = CaptureLayer.Tile | CaptureLayer.Wall, TileType = TileID.Stone, WallType = WallID.Stone,
                TilePaint = red, FullbrightTile = true, WallPaint = blue, InvisibleWall = true };
            var secondTarget = new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = TileID.Stone, TilePaint = red, InvisibleTile = true };
            var entry = Asset(host, firstTarget, secondTarget);
            Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
            try
            {
                Until(context, input, () => Scene(host).Length == 0 && Main.mouseItem.IsAir &&
                    p.inventory[10].type == ItemID.RedPaint && p.inventory[11].type == ItemID.BluePaint &&
                    p.inventory[12].type == ItemID.GlowPaint && p.inventory[13].type == ItemID.EchoCoating);
            }
            catch
            {
                DumpAppearanceDifference(host, firstTarget, 44); DumpAppearanceDifference(host, secondTarget, 46);
                throw;
            }
            var first = Main.tile[44, 42]; var second = Main.tile[46, 42];
            Require(first.color() == red && first.fullbrightBlock() && !first.invisibleBlock() && first.wallColor() == blue &&
                first.invisibleWall() && !first.fullbrightWall() && second.color() == red && second.invisibleBlock() && !second.fullbrightBlock(),
                "native scraper clears both coatings, completes the wall and restores the selected foreground without a scrape/repaint loop");
            Require(p.inventory[10].stack == 2 && p.inventory[11].stack == 2 && p.inventory[12].stack == 2 && p.inventory[13].stack == 2,
                "retained paints and individual retained coatings each have exactly their real restoration cost");
        }

        private static void ConstructionLastPaint(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input);
            p.inventory[12].SetDefaults(ItemID.RedPaint); p.inventory[12].stack = 1; var finalPaint = p.inventory[12]; byte color = finalPaint.paint;
            p.inventory[14].SetDefaults(ItemID.Paintbrush);
            p.inventory[54].SetDefaults(ItemID.BluePaint); p.inventory[54].stack = 7; var oldPriority = p.inventory[54];
            NativeToolsChecks.Tile(44, 42, TileID.Stone);
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone, TilePaint = color });
            Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
            Until(context, input, () => Scene(host).Length == 0 && Main.mouseItem.IsAir && p.inventory[12].IsAir && ReferenceEquals(p.inventory[54], oldPriority));
            Require(Main.tile[44, 42].color() == color && finalPaint.IsAir && oldPriority.stack == 7 && p.inventory[14].type == ItemID.Paintbrush,
                "last original paint becomes native Air, cleanup returns only the unrelated priority item and cannot revive the spent source");
        }

        private static void ConstructionSameColorRestorationRefill(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input);
            p.inventory[10].SetDefaults(ItemID.RedPaint); p.inventory[10].stack = 1; var paint = p.inventory[10]; byte red = paint.paint;
            p.inventory[11].SetDefaults(ItemID.BluePaint); p.inventory[11].stack = 2; var blue = p.inventory[11];
            p.inventory[13].SetDefaults(ItemID.StoneBlock); p.inventory[13].stack = 1;
            p.inventory[14].SetDefaults(ItemID.Paintbrush); p.inventory[15].SetDefaults(ItemID.PaintRoller); p.inventory[16].SetDefaults(ItemID.PaintScraper);
            NativeToolsChecks.Tile(44, 42, TileID.Stone); WorldGen.PlaceWall(44, 42, WallID.Stone);
            WorldGen.paintTile(44, 42, red, false, false); WorldGen.paintWall(44, 42, red, false, false);
            WorldGen.paintCoatWall(44, 42, 1, false, false);
            var target = new CaptureCell { Layers = CaptureLayer.Tile | CaptureLayer.Wall, TileType = TileID.Stone,
                WallType = WallID.Stone, TilePaint = red, WallPaint = red };
            var entry = Asset(host, target, new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = TileID.Stone });
            Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
            object waitingMaterial = null;
            Until(context, input, () =>
            {
                if (!Main.tile[46, 42].active() || (bool)Get(Get(tools, "Use"), "Active")) return false;
                var match = Scene(host).Select(scene => GetOptional(scene, "Match")).FirstOrDefault(value => value != null);
                if (match == null) return false;
                foreach (var range in ((System.Collections.IEnumerable)Get(match, "Ranges")).Cast<object>())
                {
                    var material = GetOptional(range, "Materials");
                    if (material != null && (bool)Get(material, "BuildExhausted") && Get(material, "BuildWaits").ToString().Contains("Capability"))
                    { waitingMaterial = material; return true; }
                }
                return false;
            });
            long started = (long)Get(host, "BuildUsesStarted");
            for (int frame = 0; frame < 80; frame++) Step(context, input);
            var first = Main.tile[44, 42]; var position = p.position;
            Require(first.color() == red && first.wallColor() == red && first.fullbrightWall() &&
                ReferenceEquals(p.inventory[10], paint) && paint.stack == 1 && p.inventory[13].IsAir &&
                Main.tile[46, 42].type == TileID.Stone && Scene(host).Length == 1 && (long)Get(host, "BuildUsesStarted") == started &&
                !(bool)Get(waitingMaterial, "EligibilityRetry"),
                "one red paint cannot fund both selected restoration layers: original appearance stays intact, an independent body completes, and shortage has no native retry deadline");
            long supplyRevision = (long)Get(host, "buildSupplyRevision");
            var extraBlue = new Item(); extraBlue.SetDefaults(ItemID.BluePaint); extraBlue.stack = 1;
            Require(p.GetItem(extraBlue, new GetItemSettings(NoText: true, NoSound: true)).IsAir && ReferenceEquals(p.inventory[11], blue) && blue.stack == 3,
                "original GetItem merges a positive-stack noise control from two blue paints to three");
            for (int frame = 0; frame < 8; frame++) Step(context, input);
            first = Main.tile[44, 42];
            Require((long)Get(host, "buildSupplyRevision") == supplyRevision && paint.stack == 1 && first.color() == red && first.wallColor() == red && first.fullbrightWall() &&
                (long)Get(host, "BuildUsesStarted") == started && p.position == position,
                "two-to-three paint noise creates no recovery serial and cannot bypass the still insufficient same-color restoration");
            var incoming = new Item(); incoming.SetDefaults(ItemID.RedPaint); incoming.stack = 1;
            var remainder = p.GetItem(incoming, new GetItemSettings(NoText: true, NoSound: true));
            Require(remainder.IsAir && ReferenceEquals(p.inventory[10], paint) && paint.stack == 2 && p.position == position,
                "original GetItem merges exactly one picked-up paint into the existing qualified stack from one to two without moving the player");
            Until(context, input, () => Scene(host).Length == 0 && Main.mouseItem.IsAir && p.inventory[10].IsAir);
            first = Main.tile[44, 42];
            Require(first.color() == red && first.wallColor() == red && !first.invisibleBlock() && !first.fullbrightBlock() &&
                !first.invisibleWall() && !first.fullbrightWall() && p.inventory[10].IsAir && paint.IsAir && p.position == position &&
                ReferenceEquals(p.inventory[11], blue) && blue.stack == 3 &&
                p.inventory[14].type == ItemID.Paintbrush && p.inventory[15].type == ItemID.PaintRoller && p.inventory[16].type == ItemID.PaintScraper,
                "same-stack one-to-two pickup wakes the stationary restoration, spends exactly two paints and retires only after complete current Match");
        }

        private static void ConstructionUnselectedForeground(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input);
            p.inventory[12].SetDefaults(ItemID.PaintScraper); p.inventory[13].SetDefaults(ItemID.StoneBlock); p.inventory[13].stack = 2;
            NativeToolsChecks.Tile(44, 42, TileID.Stone); WorldGen.PlaceWall(44, 42, WallID.Stone);
            WorldGen.paintTile(44, 42, 9, false, false); WorldGen.paintWall(44, 42, 1, false, false);
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Wall, WallType = WallID.Stone },
                new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = TileID.Stone });
            Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
            Until(context, input, () => Main.tile[46, 42].active());
            for (int frame = 0; frame < 80; frame++) Step(context, input);
            Require(Main.tile[44, 42].color() == 9 && Main.tile[44, 42].wallColor() == 1 && Scene(host).Length == 1 && p.inventory[13].stack == 1,
                "wall-only scraper cannot erase an unselected painted foreground and the other legitimate body still receives service");
        }

        private static void ConstructionPlatformBoundary(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input);
            p.inventory[12].SetDefaults(ItemID.WoodPlatform); p.inventory[12].stack = 2;
            p.inventory[13].SetDefaults(ItemID.StoneBlock); p.inventory[13].stack = 2; p.inventory[14].SetDefaults(ItemID.WoodenHammer);
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Platforms,
                    FrameY = (short)(p.inventory[12].placeStyle * 18), Slope = 3 },
                new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = TileID.Stone, Slope = 1 });
            Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
            Until(context, input, () => Main.tile[44, 42].active() && Main.tile[46, 42].active() && Main.tile[46, 42].slope() == 1);
            Until(context, input, () => !(bool)Get(Get(tools, "Use"), "Active"));
            long uses = (long)Get(host, "BuildUsesStarted");
            for (int frame = 0; frame < 80; frame++) Step(context, input);
            Require(Main.tile[44, 42].type == TileID.Platforms && Main.tile[44, 42].slope() == 0 && !Main.tile[44, 42].halfBrick() &&
                p.inventory[12].stack == 1 && p.inventory[13].stack == 1 && Scene(host).Length == 1 && (long)Get(host, "BuildUsesStarted") == uses,
                "unreachable platform slope3 retains its pending goal without temporary blocks, replacement or repeated empty hammering; ordinary shape continues");
        }
    }
}
