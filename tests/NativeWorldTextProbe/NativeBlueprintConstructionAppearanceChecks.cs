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
            check("ordinary non-solid shapes and locked door preflight", () => ConstructionHammerEligibility(context, host, tools, input));
            check("natural smart platform slopes and fallback", () => ConstructionSmartPlatform(context, host, tools, input));
            check("captured platform identity and hammer", () => ConstructionCapturedPlatforms(context, host, tools, input));
            check("captured smart platform native paint", () => ConstructionCapturedPlatformPaint(context, host, tools, input));
            check("mixed paint", () => ConstructionMixedPaint(context, host, tools, input));
            check("missing foreground keeps wall paint", () => ConstructionMissingForegroundWallPaint(context, host, tools, input));
            check("last paint", () => ConstructionLastPaint(context, host, tools, input));
            check("coating restoration", () => ConstructionCoatingRestoration(context, host, tools, input));
            check("same color restoration refill", () => ConstructionSameColorRestorationRefill(context, host, tools, input));
            check("unselected foreground", () => ConstructionUnselectedForeground(context, host, tools, input));
            check("platform slope3 boundary", () => ConstructionPlatformBoundary(context, host, tools, input, 3));
            check("platform slope4 boundary", () => ConstructionPlatformBoundary(context, host, tools, input, 4));
            Console.WriteLine("PASS BlueprintConstructionAppearance: natural Lookup/PlaceIt smart slope1/2 without a hammer, bounded resolution fallback and precise half/existing-shape controls, native hammer repeat, all seven ordinary non-solid types and captured half/four-slope completion, upper/lower locked-door zero-start wait and unlock recovery, captured positive-ObjectId platform save/open and slope1/2/half completion, original smart platform capture and real native paint for existing-shape/empty destinations, strict raw/style/appearance boundaries, protected walls, mixed original paint preparation/return, coating and cross-layer restoration with same-stack refill, unselected foreground refusal, platform slope3/4 boundary; real multiplayer/GPU/FPS NOT_RUN");
        }

        private static object smartPlatformHost, smartPlatformInput;
        private static bool smartPlatformWatch, smartPlatformProofValid;
        private static int smartPlatformChoices, smartPlatformPreciseChoices, smartPlatformHammers, smartPlatformCorrectHammers;
        private static readonly System.Collections.Generic.Dictionary<long, long> smartPlatformLookups = new System.Collections.Generic.Dictionary<long, long>();
        private static readonly System.Collections.Generic.Dictionary<string, int> smartPlatformMissingLookups = new System.Collections.Generic.Dictionary<string, int>();
        private static string SmartPlatformPoint(object target)
        { return Get(target, "WorldX") + "," + Get(target, "WorldY"); }
        private static object WatchedSmartPlatformFlight()
        { return smartPlatformWatch ? GetOptional(smartPlatformHost, "activeBuild") : null; }
        private static bool SmartPlatformLookupBefore()
        {
            var flight = WatchedSmartPlatformFlight();
            if (flight == null || Get(flight, "CursorMode").ToString() != "SmartPlatform") return true;
            string key = SmartPlatformPoint(Get(flight, "Target")); int count;
            if (!smartPlatformMissingLookups.TryGetValue(key, out count)) return true;
            // Fault injection removes this one original lookup opportunity.
            // It writes neither Smart coordinates nor private continuity locks.
            // Successful arms below always execute the complete native Lookup.
            smartPlatformMissingLookups[key] = count + 1;
            if (count < 4) Console.WriteLine("SMART_PLATFORM injectedLookupMiss target=" + key + " attempt=" + (count + 1) +
                " generation=" + Get(flight, "Generation") + " frame=" + Get(smartPlatformInput, "Frame") +
                " oldNative=" + Main.SmartCursorX + "," + Main.SmartCursorY + " showing=" + Main.SmartCursorShowing);
            return false;
        }
        private static void SmartPlatformLookupAfter()
        {
            var flight = WatchedSmartPlatformFlight();
            if (flight == null || Get(flight, "CursorMode").ToString() != "SmartPlatform") return;
            var target = Get(flight, "Target"); var direction = Terraria.GameContent.SmartCursorHelper.LockedDesiredDirection;
            int x = (int)Get(target, "WorldX"), y = (int)Get(target, "WorldY"), dx = (int)Get(flight, "SmartDirection");
            if (Main.SmartCursorIsUsed && Main.SmartCursorShowing && Player.tileTargetX == x && Player.tileTargetY == y &&
                Main.SmartCursorX == x && Main.SmartCursorY == y && direction.HasValue && direction.Value.X == dx && direction.Value.Y == 1)
                smartPlatformLookups[(long)Get(flight, "Generation")] = (long)Get(smartPlatformInput, "Frame");
        }
        private static void SmartPlatformChoiceAfter(object __0, bool __result)
        {
            if (!smartPlatformWatch || !__result) return;
            if (smartPlatformMissingLookups.Count > 0) Console.WriteLine("SMART_PLATFORM fallbackChoice target=" + SmartPlatformPoint(Get(__0, "Target")) +
                " mode=" + Get(__0, "CursorMode") + " generation=" + Get(__0, "Generation") + " frame=" + Get(smartPlatformInput, "Frame"));
            if (Get(__0, "CursorMode").ToString() != "SmartPlatform") { smartPlatformPreciseChoices++; return; }
            long frame;
            smartPlatformProofValid &= smartPlatformLookups.TryGetValue((long)Get(__0, "Generation"), out frame) &&
                frame == (long)Get(smartPlatformInput, "Frame") && Main.SmartCursorIsUsed && Main.SmartCursorShowing &&
                Main.SmartCursorX == Player.tileTargetX && Main.SmartCursorY == Player.tileTargetY;
            smartPlatformChoices++;
        }
        private static void SmartPlatformHammerAfter(object __0, object __1, bool __result)
        {
            if (!smartPlatformWatch || !__result || __1.ToString() != "Hammer") return;
            smartPlatformHammers++;
            if (SmartPlatformPoint(Get(__0, "Target")) == "47,41") smartPlatformCorrectHammers++;
        }
        private static void ResetSmartPlatformCounts()
        {
            smartPlatformChoices = smartPlatformPreciseChoices = smartPlatformHammers = smartPlatformCorrectHammers = 0;
            smartPlatformProofValid = true; smartPlatformLookups.Clear(); smartPlatformMissingLookups.Clear();
        }
        private static void SeedSmartPlatform(Item material, int x, int y, int slope)
        {
            Require(WorldGen.PlaceTile(x, y, material.createTile, mute: true, forced: true, plr: 0, style: material.placeStyle),
                "initial slope endpoint is an original platform placement");
            Require(WorldGen.SlopeTile(x, y, slope), "initial world supplies one sloped endpoint before automatic input");
        }
        private static CaptureCell SmartPlatformTarget(Item material, int x, int y, int slope = 0, bool half = false)
        {
            return new CaptureCell { X = x, Y = y, Layers = CaptureLayer.Tile, TileType = (ushort)material.createTile,
                FrameY = (short)(material.placeStyle * 18), Slope = (byte)slope, HalfBrick = half };
        }
        private static void ConstructionSmartPlatform(object context, object host, object tools, object input)
        {
            var patch = new HarmonyLib.Harmony("JueMingR.Tests.NaturalSmartPlatform");
            var self = typeof(NativeBlueprintBuildChecks);
            patch.Patch(typeof(Terraria.GameContent.SmartCursorHelper).GetMethod("SmartCursorLookup", F),
                prefix: new HarmonyLib.HarmonyMethod(self, nameof(SmartPlatformLookupBefore)),
                postfix: new HarmonyLib.HarmonyMethod(self, nameof(SmartPlatformLookupAfter)));
            patch.Patch(host.GetType().GetMethod("NativeBuildChoice", F), postfix: new HarmonyLib.HarmonyMethod(self, nameof(SmartPlatformChoiceAfter)));
            patch.Patch(host.GetType().GetMethod("NativeAppearanceConsumer", F), postfix: new HarmonyLib.HarmonyMethod(self, nameof(SmartPlatformHammerAfter)));
            bool oldMouse = Main.SmartCursorWanted_Mouse, oldPad = Main.SmartCursorWanted_GamePad;
            smartPlatformHost = host; smartPlatformInput = input;
            try
            {
                foreach (int direction in new[] { 1, -1 })
                {
                    var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
                    p.inventory[12].SetDefaults(ItemID.WoodPlatform); p.inventory[12].stack = 7; var material = p.inventory[12];
                    int slope = direction == 1 ? 1 : 2, seedX = direction == 1 ? 43 : 47;
                    SeedSmartPlatform(material, seedX, 38, slope);
                    var cells = Enumerable.Range(0, 3).Select(i => SmartPlatformTarget(material, direction == 1 ? i : 2 - i, i, slope)).ToArray();
                    var entry = Asset(host, cells);
                    Place(context, host, input, entry, 44, 39);
                    Main.SmartCursorWanted_Mouse = direction == -1; Main.SmartCursorWanted_GamePad = direction == 1;
                    bool expectedMouse = Main.SmartCursorWanted_Mouse, expectedPad = Main.SmartCursorWanted_GamePad;
                    ResetSmartPlatformCounts(); smartPlatformWatch = true;
                    SetAutomatic(host, input, true);
                    Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"));
                    smartPlatformWatch = false;
                    Require(cells.All(cell => Main.tile[44 + cell.X, 39 + cell.Y].active() &&
                        Main.tile[44 + cell.X, 39 + cell.Y].type == material.createTile && Main.tile[44 + cell.X, 39 + cell.Y].slope() == slope &&
                        !Main.tile[44 + cell.X, 39 + cell.Y].halfBrick()) && material.stack == 4 &&
                        Main.tile[seedX, 38].slope() == slope && !Main.tile[seedX, 38].halfBrick(),
                        "natural smart extension reaches all three shapes and consumes exactly three real platforms, preserving the seed; direction=" + direction);
                    Require(smartPlatformChoices == 3 && smartPlatformHammers == 0 && smartPlatformPreciseChoices == 0 &&
                        smartPlatformLookups.Count >= 3 && smartPlatformProofValid && p.inventory.All(item => item.hammer == 0),
                        "every direct slope has its own natural same-frame Lookup before PlaceIt and no hammer capability/consumer; direction=" + direction);
                    Require(Main.SmartCursorWanted_Mouse == expectedMouse && Main.SmartCursorWanted_GamePad == expectedPad,
                        "both original Smart preference values return after the temporary input scope");
                    long settled = (long)Get(host, "BuildUsesStarted");
                    for (int i = 0; i < 20; i++) Step(context, input);
                    Require((long)Get(host, "BuildUsesStarted") == settled, "completed smart shapes retire without a later hammer or placement");
                    Console.WriteLine("SMART_PLATFORM direction=" + direction + " placed=3 Hammer=" + smartPlatformHammers +
                        " nativeLookupGenerations=" + smartPlatformLookups.Count + " materialConsumed=3");
                }
                ConstructionSmartPlatformFallback(context, host, tools, input);
            }
            finally
            {
                smartPlatformWatch = false;
                Call(host, "SetAutoBuild", false);
                for (int i = 0; i < 80 && (bool)Get(Get(tools, "Use"), "Active"); i++) Step(context, input);
                Main.SmartCursorWanted_Mouse = oldMouse; Main.SmartCursorWanted_GamePad = oldPad;
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyLib.HarmonyPatchType.All, patch.Id);
                smartPlatformHost = smartPlatformInput = null; smartPlatformLookups.Clear(); smartPlatformMissingLookups.Clear();
            }
        }
        private static void ConstructionSmartPlatformFallback(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.position.X = 44 * 16;
            p.inventory[12].SetDefaults(ItemID.WoodPlatform); p.inventory[12].stack = 7; var material = p.inventory[12];
            p.inventory[14].SetDefaults(ItemID.WoodenHammer);
            SeedSmartPlatform(material, 43, 38, 1); SeedSmartPlatform(material, 49, 38, 2);
            SeedSmartPlatform(material, 47, 41, 1);
            WorldGen.PlaceWall(46, 41, WallID.Stone);
            var cells = new[] { SmartPlatformTarget(material, 0, 0, 1), SmartPlatformTarget(material, 4, 0, 2),
                SmartPlatformTarget(material, 2, 2, half: true), SmartPlatformTarget(material, 3, 2, 1) };
            Place(context, host, input, Asset(host, cells), 44, 39);
            ResetSmartPlatformCounts(); smartPlatformMissingLookups.Add("44,39", 0); smartPlatformMissingLookups.Add("48,39", 0);
            bool oldMouse = Main.SmartCursorWanted_Mouse, oldPad = Main.SmartCursorWanted_GamePad;
            smartPlatformWatch = true; SetAutomatic(host, input, true);
            Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"));
            smartPlatformWatch = false;
            Console.WriteLine("SMART_PLATFORM fallbackActual attempts=" + string.Join(";", smartPlatformMissingLookups.Select(pair => pair.Key + ":" + pair.Value)) +
                " smartChoices=" + smartPlatformChoices + " preciseChoices=" + smartPlatformPreciseChoices + " Hammer=" + smartPlatformHammers +
                " correctShapeHammer=" + smartPlatformCorrectHammers + " materialRemaining=" + material.stack + " unknown=" + Get(host, "BuildUnknownResults") +
                " world=" + string.Join(";", cells.Select(cell =>
                {
                    int x = 44 + cell.X, y = 39 + cell.Y; var tile = Main.tile[x, y];
                    return x + "," + y + ":" + tile.active() + "/" + tile.type + "/" + tile.slope() + "/" + tile.halfBrick();
                })));
            Require(smartPlatformMissingLookups.Values.All(count => count == 1) && smartPlatformChoices == 0 && smartPlatformPreciseChoices == 3,
                "two separately rejected Smart targets retain their own bounded fallback and proceed through Precise without a new Smart attempt");
            Require(smartPlatformHammers > 0 && smartPlatformCorrectHammers == 0 && material.stack == 4 && cells.All(cell =>
            {
                var tile = Main.tile[44 + cell.X, 39 + cell.Y];
                return tile.active() && tile.type == material.createTile && tile.slope() == cell.Slope && tile.halfBrick() == cell.HalfBrick;
            }) && Main.tile[43, 38].slope() == 1 && Main.tile[49, 38].slope() == 2,
                "fallback slopes and unsupported half use actual hammering, the already correct slope is untouched, and no body material is charged twice");
            Require(Main.SmartCursorWanted_Mouse == oldMouse && Main.SmartCursorWanted_GamePad == oldPad,
                "failed Smart inputs and later precise/hammer scopes restore both original preferences");
            Console.WriteLine("SMART_PLATFORM fallbackTargets=2 smartAttempts=" + string.Join(",", smartPlatformMissingLookups.Values) +
                " precisePlaced=" + smartPlatformPreciseChoices + " Hammer=" + smartPlatformHammers + " correctShapeHammer=0 materialConsumed=3");
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

        private static void ConstructionCapturedPlatforms(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.position.X = 42 * 16;
            var material = new Item(); material.SetDefaults(ItemID.WoodPlatform);
            for (int x = 60; x < 63; x++)
                Require(WorldGen.PlaceTile(x, 42, material.createTile, mute: true, forced: true, plr: 0, style: material.placeStyle),
                    "captured platform fixture uses original material placement");
            WorldGen.SlopeTile(60, 42, 1); WorldGen.SlopeTile(61, 42, 2); WorldGen.PoundTile(62, 42);
            WorldGen.SquareTileFrame(61, 42);
            Require(Main.tile[60, 42].slope() == 1 && Main.tile[61, 42].slope() == 2 && Main.tile[62, 42].halfBrick(),
                "source fixture contains original slope1, slope2 and half platform states before capture");
            var library = (LibraryState)Get(host, "Library");
            var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, library,
                () => Step(context, input), 60, 42, 3, 1, "正式平台锤形");
            BlueprintProjectionWindow saved = OpenCapturedAppearance(library, entry);
            Require(saved.Cells.Count == 3 && saved.Objects.Count == 3 && saved.Objects.All(o => o.Id > 0 && o.Reliable && o.Width == 1 && o.Height == 1),
                "real CaptureJob and codec reopen three reliable positive-ObjectId one-cell platforms");
            for (int i = 0; i < 3; i++)
            {
                CaptureCell cell = saved.Cells.Single(c => c.X == i && c.Y == 0);
                CaptureObject obj = saved.Objects.Single(o => o.Id == cell.ObjectId);
                Tile original = Main.tile[60 + i, 42];
                Require(cell.ObjectId > 0 && cell.FrameX == original.frameX && cell.FrameY == original.frameY &&
                    cell.Slope == original.slope() && cell.HalfBrick == original.halfBrick() &&
                    obj.Style == material.placeStyle && obj.RandomVariant == cell.FrameX / 18 &&
                    obj.PlacementStyle == material.placeStyle * 27 + cell.FrameX / 18,
                    "Saved/open preserves each actual raw frame, shape and decoded object metadata instead of replacing it with ObjectId0");
            }
            Require(saved.Cells[0].FrameX > 0, "captured sloped platform is not the old literal FrameX0 shortcut fixture");
            CapturedPlatformSemantics(host, saved);

            // Both arms use that same immutable capture. One must place the
            // final bodies first; the other must not charge them a second time.
            for (int existing = 0; existing < 2; existing++)
            {
                p = Reset(context, host, tools, input); p.position.X = 42 * 16;
                p.inventory[12].SetDefaults(ItemID.WoodPlatform); p.inventory[12].stack = 5;
                p.inventory[14].SetDefaults(ItemID.WoodenHammer);
                for (int x = 44; x < 47; x++)
                {
                    Require(p.IsInTileInteractionRange(x, 42, Terraria.DataStructures.TileReachCheckSettings.Simple, p.inventory[14].tileBoost) &&
                        !p.Hitbox.Intersects(new Microsoft.Xna.Framework.Rectangle(x * 16, 42 * 16, 16, 16)),
                        "captured platform destinations are in actual hammer reach and outside the player's occupied cells");
                    if (existing != 0)
                        Require(WorldGen.PlaceTile(x, 42, material.createTile, mute: true, forced: true, plr: 0, style: material.placeStyle),
                            "existing-body arm starts with original flat same-style platforms");
                }
                WorldGen.PlaceWall(44, 42, WallID.Stone);
                var position = p.position;
                Place(context, host, input, entry, 44, 42);
                long checks = (long)Get(host, "BuildNativeChecks");
                SetAutomatic(host, input, true);
                Until(context, input, () => Scene(host).Length == 0);
                Require(Main.tile[44, 42].type == material.createTile && Main.tile[44, 42].slope() == 1 && !Main.tile[44, 42].halfBrick() &&
                    Main.tile[45, 42].type == material.createTile && Main.tile[45, 42].slope() == 2 && !Main.tile[45, 42].halfBrick() &&
                    Main.tile[46, 42].type == material.createTile && Main.tile[46, 42].slope() == 0 && Main.tile[46, 42].halfBrick() &&
                    Main.tile[44, 42].wall == WallID.Stone && p.position == position && p.inventory[12].stack == (existing == 0 ? 2 : 5) &&
                    p.inventory[14].type == ItemID.WoodenHammer && p.inventory[14].stack == 1 && (long)Get(host, "BuildNativeChecks") > checks + 3,
                    "positive-ObjectId capture reaches slope1/2/half through repeated native neutral/pound and full Match, preserving wall/player and exact body consumption; existing=" + existing);
                Until(context, input, () => !(bool)Get(Get(tools, "Use"), "Active"));
                long settled = (long)Get(host, "BuildUsesStarted");
                for (int frame = 0; frame < 80; frame++) Step(context, input);
                Require((long)Get(host, "BuildUsesStarted") == settled && p.inventory[12].stack == (existing == 0 ? 2 : 5) && Main.tile[44, 42].wall == WallID.Stone,
                    "complete platform Match retires without later body charges or empty hammer repeats");
            }
        }

        private static void ConstructionCapturedPlatformPaint(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input);
            p.inventory[12].SetDefaults(ItemID.WoodPlatform);
            var material = p.inventory[12];
            var color = new Item(); color.SetDefaults(ItemID.RedPaint);
            bool oldMouse = Main.SmartCursorWanted_Mouse, oldPad = Main.SmartCursorWanted_GamePad;
            int oldTargetX = Player.tileTargetX, oldTargetY = Player.tileTargetY;
            try
            {
                // Source setup calls the original placement routine, including
                // its smart-platform stage. The first slope is never assigned
                // by this fixture; its diagonal neighbour selects it in vanilla.
                Main.tile[61, 43].ClearEverything();
                Require(WorldGen.PlaceTile(61, 43, material.createTile, mute: true, forced: true, plr: 0, style: material.placeStyle),
                    "smart source has one original diagonal platform neighbour");
                p.selectedItemState.Select(12); p.selectedItemState.Update();
                Main.SmartCursorWanted_Mouse = Main.SmartCursorWanted_GamePad = true;
                Player.tileTargetX = 60; Player.tileTargetY = 42;
                Call(p, "PlaceThing_Tiles_PlaceIt", false, default(TileObject), material.createTile);
                Require(Main.tile[60, 42].active() && Main.tile[60, 42].type == material.createTile && Main.tile[60, 42].slope() == 1,
                    "original smart placement really creates slope1 before capture");
            }
            finally
            {
                Main.SmartCursorWanted_Mouse = oldMouse; Main.SmartCursorWanted_GamePad = oldPad;
                Player.tileTargetX = oldTargetX; Player.tileTargetY = oldTargetY;
                p.selectedItemState.Select(0); p.selectedItemState.Update();
            }
            for (int x = 61; x < 64; x++)
                Require(WorldGen.PlaceTile(x, 42, material.createTile, mute: true, forced: true, plr: 0, style: material.placeStyle),
                    "remaining source controls use original platform placement");
            WorldGen.SlopeTile(61, 42, 2); WorldGen.PoundTile(62, 42);
            for (int x = 60; x < 64; x++) Require(WorldGen.paintTile(x, 42, color.paint, false, false), "source setup paints before capture");
            var library = (LibraryState)Get(host, "Library");
            var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, library,
                () => Step(context, input), 60, 42, 4, 1, "正式平台刷漆");
            BlueprintProjectionWindow saved = OpenCapturedAppearance(library, entry);
            Require(saved.Cells.Count == 4 && saved.Objects.Count == 4 && saved.Objects.All(o => o.Id > 0 && o.Reliable),
                "paint targets retain real CaptureJob positive object identities after production save/open");
            for (int i = 0; i < 4; i++)
            {
                CaptureCell cell = saved.Cells.Single(c => c.X == i && c.Y == 0);
                CaptureObject obj = saved.Objects.Single(o => o.Id == cell.ObjectId);
                Tile original = Main.tile[60 + i, 42];
                Require(cell.ObjectId > 0 && cell.FrameX == original.frameX && cell.FrameY == original.frameY &&
                    cell.Slope == original.slope() && cell.HalfBrick == original.halfBrick() && cell.TilePaint == original.color() &&
                    cell.InvisibleTile == original.invisibleBlock() && cell.FullbrightTile == original.fullbrightBlock() &&
                    obj.Style == material.placeStyle && obj.RandomVariant == cell.FrameX / 18 &&
                    obj.PlacementStyle == material.placeStyle * 27 + cell.FrameX / 18,
                    "paint source raw frame/shape/color/coatings and decoded metadata round-trip without rewriting the target");
            }
            Require(saved.Cells.Single(c => c.X == 0).Slope == 1 && saved.Cells.Single(c => c.X == 0).FrameX > 0 &&
                saved.Cells.Single(c => c.X == 1).Slope == 2 && saved.Cells.Single(c => c.X == 2).HalfBrick &&
                saved.Cells.Single(c => c.X == 3).Slope == 0 && !saved.Cells.Single(c => c.X == 3).HalfBrick,
                "one smart-produced slope, opposite slope, half and flat controls keep their actual captured states");

            var patch = new HarmonyLib.Harmony("JueMingR.Tests.CapturedPlatformPaint");
            patch.Patch(host.GetType().GetMethod("CheckedAutomaticBuild", F),
                prefix: new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(CapturedPlatformReceiptBoundary)));
            patch.Patch(typeof(WorldGen).GetMethod("paintTile", F, null, new[] { typeof(int), typeof(int), typeof(byte), typeof(bool), typeof(bool) }, null),
                postfix: new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(ContinuousPainted)));
            continuousHost = host; continuousUse = Get(tools, "Use"); continuousAutomatic = true;
            try
            {
                // One immutable saved asset crosses both mechanisms. Existing
                // destinations have their final shape and have no hammer;
                // empty destinations must place/hammer before full Match.
                foreach (bool existing in new[] { true, false })
                {
                    p = Reset(context, host, tools, input); p.position.X = 42 * 16;
                    p.inventory[12].SetDefaults(ItemID.WoodPlatform); p.inventory[12].stack = 5; var bodies = p.inventory[12];
                    p.inventory[13].SetDefaults(ItemID.RedPaint); p.inventory[13].stack = 6; var paint = p.inventory[13];
                    p.inventory[14].SetDefaults(ItemID.Paintbrush); var brush = p.inventory[14];
                    if (!existing) p.inventory[15].SetDefaults(ItemID.WoodenHammer);
                    p.inventory[54].SetDefaults(ItemID.BluePaint); p.inventory[54].stack = 7; var priority = p.inventory[54];
                    if (existing)
                        foreach (var cell in saved.Cells)
                        {
                            int x = 44 + cell.X;
                            Require(WorldGen.PlaceTile(x, 42, material.createTile, mute: true, forced: true, plr: 0, style: material.placeStyle),
                                "paint-only arm has the original same-material body");
                            if (cell.HalfBrick) WorldGen.PoundTile(x, 42);
                            else if (cell.Slope != 0) WorldGen.SlopeTile(x, 42, cell.Slope);
                            Require(Main.tile[x, 42].color() == 0, "only paint is missing in existing-shape arm");
                        }
                    WorldGen.PlaceWall(44, 42, WallID.Stone);
                    var position = p.position;
                    var instance = Place(context, host, input, entry, 44, 42);
                    continuousPlacements.Clear(); continuousFrame = 0; continuousWatch = true;
                    capturedReceiptChecks = 0; capturedReceiptRejected = true;
                    long checks = (long)Get(host, "BuildNativeChecks");
                    SetAutomatic(host, input, true);
                    Until(context, input, () =>
                    {
                        continuousFrame++;
                        return Scene(host).Length == 0 && !(bool)Get(continuousUse, "Active") && Main.mouseItem.IsAir &&
                            ReferenceEquals(p.inventory[13], paint) && ReferenceEquals(p.inventory[54], priority);
                    });
                    continuousWatch = false;
                    Require(saved.Cells.All(cell =>
                    {
                        Tile actual = Main.tile[44 + cell.X, 42];
                        return actual.active() && actual.type == cell.TileType && actual.frameY == cell.FrameY &&
                            actual.slope() == cell.Slope && actual.halfBrick() == cell.HalfBrick && actual.color() == cell.TilePaint &&
                            actual.invisibleBlock() == cell.InvisibleTile && actual.fullbrightBlock() == cell.FullbrightTile;
                    }) && bodies.stack == (existing ? 5 : 1) && paint.stack == 2 && priority.stack == 7 &&
                        ReferenceEquals(p.inventory[12], bodies) && ReferenceEquals(p.inventory[14], brush) && brush.stack == 1 &&
                        p.position == position && Main.tile[44, 42].wall == WallID.Stone && (long)Get(host, "BuildNativeChecks") >= checks + 4,
                        "real native paint completes each saved shape with exact body/paint debits and preserves tools, priority paint, player and wall; existing=" + existing);
                    Require(continuousPlacements.Count == 4 && continuousPlacements.Select(v => v.X).OrderBy(x => x).SequenceEqual(new[] { 44, 45, 46, 47 }) &&
                        continuousPlacements.All(v => ReferenceEquals(v.Owner, instance) && v.Kind == "PaintTile" && v.Consumer == "Paintbrush" &&
                            v.Mode == color.paint && ReferenceEquals(v.Item, brush) && ReferenceEquals(v.Supply, paint) && v.SupplySlot == 54 &&
                            v.Token > 0 && v.Native > 0 && v.Flight > 0),
                        "each successful original paintTile is inside the exact production brush flight with its real prepared paint dependency");
                    Require(capturedReceiptChecks == 1 && capturedReceiptRejected,
                        "one actual native paint debit rejects a stale token and insufficient budget without advancing receipt endpoints");
                    long settled = (long)Get(host, "BuildUsesStarted");
                    for (int frame = 0; frame < 80; frame++) Step(context, input);
                    Require((long)Get(host, "BuildUsesStarted") == settled && paint.stack == 2 && bodies.stack == (existing ? 5 : 1),
                        "full current Match retires the capture without further empty brush/hammer uses or charges");
                    Console.WriteLine("PASS CapturedPlatformPaint existing=" + existing + " asset=" + entry.AssetId + " revision=" + entry.ContentRevision +
                        " positiveObjects=4 originalSmartSlope=1 nativePaint=4 actualPaintSpent=4 actualBodiesSpent=" + (existing ? 0 : 4) + " fullMatchRetired=true");
                }
            }
            finally
            {
                continuousWatch = false; continuousAutomatic = false; continuousHost = continuousUse = null; continuousPlacements.Clear();
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyLib.HarmonyPatchType.All, patch.Id);
            }
        }

        private static int capturedReceiptChecks;
        private static bool capturedReceiptRejected;
        private static void CapturedPlatformReceiptBoundary(object __instance, object flight)
        {
            if (!continuousWatch || capturedReceiptChecks != 0 || flight == null || (bool)Get(flight, "Checked") || !(bool)Get(flight, "Entered")) return;
            var operation = GetOptional(flight, "Operation");
            if (operation == null || Get(operation, "Kind").ToString() != "PaintTile") return;
            var preparation = Get(__instance, "buildMaterialPreparation");
            if (!(bool)Get(preparation, "Using")) return;
            var inventory = (Item[])Get(preparation, "inventory");
            var endpoints = ((Array)Get(preparation, "endpoints")).Cast<object>().Take((int)Get(preparation, "endpointCount")).ToArray();
            var primary = endpoints.Single(endpoint => (int)Get(endpoint, "Slot") == 54);
            int before = (int)Get(primary, "Stack");
            if (before - inventory[54].stack != 1) return;
            capturedReceiptChecks++;
            long token = (long)Get(flight, "Token");
            var maximum = (Func<int, int>)Get(preparation, "UseConsumptionMaximum");
            var beforeStacks = endpoints.Select(endpoint => (int)Get(endpoint, "Stack")).ToArray();
            try
            {
                capturedReceiptRejected &= !(bool)Call(preparation, "ObserveUseConsumption", token - 1);
                // This negative call owns no native action and writes no Item.
                // Restore the real flight budget before production acknowledges
                // that same one-unit native debit in the method below us.
                Set(preparation, "UseConsumptionMaximum", (Func<int, int>)(_ => 0));
                capturedReceiptRejected &= !(bool)Call(preparation, "ObserveUseConsumption", token);
                capturedReceiptRejected &= endpoints.Select(endpoint => (int)Get(endpoint, "Stack")).SequenceEqual(beforeStacks);
            }
            catch { capturedReceiptRejected = false; }
            finally { Set(preparation, "UseConsumptionMaximum", maximum); }
        }

        private static BlueprintProjectionWindow OpenCapturedAppearance(LibraryState library, LibraryEntry entry)
        {
            var identity = new BlueprintProjectionIdentity(entry.AssetId, Guid.Parse(entry.ContentRevision), entry.ContentHash, 91001, 1);
            Require(library.RequestProjectionOpen(identity), "saved appearance enters the production projection reader");
            try
            {
                AwaitPlatformReceipt(library, identity, BlueprintProjectionReceiptKind.Opened);
                Require(library.RequestProjectionWindow(identity, 1, new BlueprintProjectionBounds(0, 0, entry.Width, entry.Height)),
                    "saved appearance requests its real bounded source window");
                return AwaitPlatformReceipt(library, identity, BlueprintProjectionReceiptKind.Window).Window;
            }
            finally { library.ReleaseProjection(identity); }
        }
        private static BlueprintProjectionReceipt AwaitPlatformReceipt(LibraryState library, BlueprintProjectionIdentity identity, BlueprintProjectionReceiptKind kind)
        {
            BlueprintProjectionReceipt result = null;
            NativeQuickItemChecks.Until(() =>
            {
                BlueprintProjectionReceipt value;
                if (!library.TryTakeProjectionReceipt(out value) || !identity.Equals(value.Identity)) return false;
                Require(value.Kind != BlueprintProjectionReceiptKind.Failed, "saved platform reader remains reliable: " + value.FailureCode);
                if (value.Kind != kind) return false;
                result = value; return true;
            });
            Require(result != null && (kind != BlueprintProjectionReceiptKind.Window || result.Window != null), "only a complete real platform receipt is used");
            return result;
        }

        private static void CapturedPlatformSemantics(object host, BlueprintProjectionWindow saved)
        {
            CaptureCell member = saved.Cells.Single(c => c.X == 0 && c.Y == 0);
            CaptureObject metadata = saved.Objects.Single(o => o.Id == member.ObjectId);
            var type = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldMatchSource", true);
            object source = Activator.CreateInstance(type, F, null, new object[] { (Func<bool>)(() => true) }, null);
            object[] absentShape = { member, null, null };
            var semantics = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldObjectSemantics", true);
            Require(!(bool)semantics.GetMethod("TryCreate", F).Invoke(null, absentShape) && absentShape[2] == null,
                "a native platform Set member without metadata cannot fall back to an ordinary-cell proof");
            BlueprintWorldObject target;
            Require(DescribeCapturedPlatform(source, metadata, member, out target) && target.MatchesMemberFrame(member, 0),
                "reopened raw metadata compiles into a target with its original exact member layout");
            Require(WorldGen.PlaceTile(44, 42, metadata.Type, mute: true, forced: true, plr: 0, style: metadata.Style) &&
                WorldGen.PlaceTile(45, 42, metadata.Type, mute: true, forced: true, plr: 0, style: metadata.Style),
                "semantic neighbor fixture uses original platform placements");
            WorldGen.SlopeTile(44, 42, 1); WorldGen.SquareTileFrame(44, 42);
            BlueprintWorldMatchSample current = ReadCapturedPlatform(source, 44, 42);
            Require(current.Object != null && current.Cell.FrameX != member.FrameX && target.SamePlacement(current.Object) &&
                target.FrameGroup != current.Object.FrameGroup && !target.SameObservedObject(current.Object) &&
                BlueprintWorldMatchComparer.CompareObject(target, new[] { member }, new[] { current }, true) == BlueprintWorldMatchState.Match,
                "same-style same-shape original platforms Match across actual neighbor frames while raw observation groups remain different");
            CaptureCell selectedCell = member; selectedCell.ObjectId = 0;
            Require(current.CompareFrames && current.FrameKind == BlueprintWorldFrameKind.Platform18 &&
                BlueprintWorldMatchComparer.CompareCell(selectedCell, current).MatchBits == 1 &&
                BlueprintMaterialSteps.SameBody(selectedCell, current) == BlueprintBodyState.Same,
                "the shared proved native profile also gives a selected-cell platform equivalent neighbor frames and exact material identity");
            var body = new BlueprintObjectBodyProof(target); body.Observe(current);
            Require(body.Complete() == BlueprintBodyState.Same, "shared positive-object body proof does not demand another platform material for neighbor framing");
            object[] witness = { target, new[] { member }, new[] { current }, true, null };
            Require(!(bool)type.GetMethod("TryCreateFrameWitness", F).Invoke(source, witness) && witness[4] == null,
                "a complete platform Match does not invent an unproved operating-frame write witness");

            // These copied samples exercise the existing pure comparator; the
            // native consumer arms above separately prove actual shape writes.
            for (int difference = 0; difference < 6; difference++)
            {
                CaptureCell actual = current.Cell;
                if (difference == 0) actual.Slope = 0;
                else if (difference == 1) { actual.Slope = 0; actual.HalfBrick = true; }
                else if (difference == 2) actual.Inactive = !actual.Inactive;
                else if (difference == 3) actual.TilePaint = 1;
                else if (difference == 4) actual.InvisibleTile = true;
                else actual.FullbrightTile = true;
                Require(BlueprintWorldMatchComparer.CompareObject(target, new[] { member },
                    new[] { new BlueprintWorldMatchSample(true, actual, current.Object) }, true) == BlueprintWorldMatchState.Conflict,
                    "platform canonical body retains independent shape/half/inactive/paint/coating difference=" + difference);
            }
            for (byte slope = 3; slope <= 4; slope++)
            {
                CaptureCell limit = member; limit.Slope = slope;
                CaptureCell actual = current.Cell; actual.Slope = slope;
                Require(BlueprintWorldMatchComparer.CompareObject(target, new[] { limit },
                    new[] { new BlueprintWorldMatchSample(true, actual, current.Object) }, true) == BlueprintWorldMatchState.Match,
                    "already-correct special platform slope is still a valid Match: " + slope);
            }
            CaptureCell changed = member; changed.FrameX = (short)((member.FrameX + 18) % (27 * 18));
            BlueprintWorldObject rejected;
            Require(!DescribeCapturedPlatform(source, metadata, changed, out rejected) &&
                BlueprintWorldMatchComparer.CompareObject(target, new[] { changed }, new[] { current }, true) == BlueprintWorldMatchState.Unknown,
                "changed source raw frame cannot borrow old decoded metadata or old member layout");
            CaptureObject corrupt = metadata; corrupt.RandomVariant = (corrupt.RandomVariant + 1) % 27;
            Require(!DescribeCapturedPlatform(source, corrupt, member, out rejected), "raw RandomVariant remains exact after canonical body normalization");
            corrupt = metadata; corrupt.PlacementStyle++;
            Require(!DescribeCapturedPlatform(source, corrupt, member, out rejected), "raw PlacementStyle remains exact after canonical body normalization");
            changed = member; changed.FrameX = -18;
            Require(!DescribeCapturedPlatform(source, metadata, changed, out rejected), "negative platform frames cannot prove a target");
            changed = member; changed.FrameY++;
            Require(!DescribeCapturedPlatform(source, metadata, changed, out rejected), "unaligned platform style rows cannot prove a target");
            changed = member; changed.FrameX = 27 * 18;
            corrupt = metadata; corrupt.Style++; corrupt.PlacementStyle = metadata.Style * 27 + 27; corrupt.RandomVariant = 0;
            Require(!DescribeCapturedPlatform(source, corrupt, changed, out rejected), "an out-of-range X slot is not wrapped into another platform style");
            WorldGen.KillTile(44, 42, false, false, true);
            Require(WorldGen.PlaceTile(44, 42, metadata.Type, mute: true, forced: true, plr: 0, style: metadata.Style + 1),
                "different-style counterexample is an original platform");
            WorldGen.SlopeTile(44, 42, 1);
            BlueprintWorldMatchSample otherStyle = ReadCapturedPlatform(source, 44, 42);
            Require(otherStyle.Object != null && !target.SamePlacement(otherStyle.Object) &&
                BlueprintWorldMatchComparer.CompareObject(target, new[] { member }, new[] { otherStyle }, true) == BlueprintWorldMatchState.Conflict &&
                BlueprintWorldMatchComparer.CompareCell(selectedCell, otherStyle).ConflictBits == 1 &&
                BlueprintMaterialSteps.SameBody(selectedCell, otherStyle) == BlueprintBodyState.Conflict,
                "actual different material style stays a body and final Match Conflict for both positive and zero ObjectId");
            // Corruption is fixture setup only, before automatic construction.
            // It must not receive the profile that legal native framing gets.
            var tile = Main.tile[44, 42]; short frameX = tile.frameX;
            try
            {
                tile.frameX = 27 * 18;
                BlueprintWorldMatchSample invalid = ReadCapturedPlatform(source, 44, 42);
                Require(invalid.ObjectUnknown && invalid.Object == null && invalid.FrameKind != BlueprintWorldFrameKind.Platform18 &&
                    BlueprintWorldMatchComparer.CompareCell(selectedCell, invalid).UnknownBits == 1,
                    "out-of-grid native raw frame receives no platform profile and cannot gain selected-cell Match");
            }
            finally { tile.frameX = frameX; }
        }
        private static bool DescribeCapturedPlatform(object source, CaptureObject metadata, CaptureCell member, out BlueprintWorldObject target)
        {
            object[] args = { metadata, member, 44, 42, null };
            bool result = (bool)source.GetType().GetMethod("TryDescribeTarget", F).Invoke(source, args);
            target = (BlueprintWorldObject)args[4]; return result;
        }
        private static BlueprintWorldMatchSample ReadCapturedPlatform(object source, int x, int y)
        {
            object[] args = { x, y, null }; Require((bool)source.GetType().GetMethod("TryRead", F).Invoke(source, args), "platform world member is readable");
            return (BlueprintWorldMatchSample)args[2];
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

        private static void ConstructionMissingForegroundWallPaint(object context,object host,object tools,object input)
        {
            var p=Reset(context,host,tools,input);
            p.inventory[12].SetDefaults(ItemID.RedPaint);p.inventory[12].stack=2;var paint=p.inventory[12];
            p.inventory[14].SetDefaults(ItemID.PaintRoller);
            WorldGen.PlaceWall(44,42,WallID.Stone);
            Place(context,host,input,Asset(host,new CaptureCell { Layers=CaptureLayer.Tile|CaptureLayer.Wall,
                TileType=TileID.Stone,WallType=WallID.Stone,WallPaint=paint.paint }),44,42);
            var instance=Scene(host).Single();
            SetAutomatic(host,input,true);
            Until(context,input,()=>Main.tile[44,42].wallColor()==paint.paint && ProgressDormant(instance) && !(bool)Get(Get(tools,"Use"),"Active"));
            Require(!Main.tile[44,42].active() && Main.tile[44,42].wall==WallID.Stone && Main.tile[44,42].wallColor()==paint.paint &&
                ReferenceEquals(p.inventory[12],paint) && paint.stack==1 && Scene(host).Contains(instance),
                "a proved missing foreground skips only dependent work: the independent existing wall receives real paint and its exact debit while the unsupplied tile remains pending");
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

        private static void ConstructionPlatformBoundary(object context, object host, object tools, object input, byte slope)
        {
            var p = Reset(context, host, tools, input);
            // Both fixed cells must be reachable by the actual hammer, whose
            // native range does not include placement's extra blockRange.
            p.position.X = 42 * 16;
            p.inventory[12].SetDefaults(ItemID.WoodPlatform); p.inventory[12].stack = 2;
            p.inventory[13].SetDefaults(ItemID.StoneBlock); p.inventory[13].stack = 2; p.inventory[14].SetDefaults(ItemID.WoodenHammer);
            Require(p.IsInTileInteractionRange(44, 42, Terraria.DataStructures.TileReachCheckSettings.Simple, p.inventory[14].tileBoost) &&
                p.IsInTileInteractionRange(46, 42, Terraria.DataStructures.TileReachCheckSettings.Simple, p.inventory[14].tileBoost) &&
                !p.Hitbox.Intersects(new Microsoft.Xna.Framework.Rectangle(44 * 16, 42 * 16, 16, 16)) &&
                !p.Hitbox.Intersects(new Microsoft.Xna.Framework.Rectangle(46 * 16, 42 * 16, 16, 16)),
                "platform boundary fixture puts both cells in original hammer reach while the player occupies neither target");
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Platforms,
                    FrameY = (short)(p.inventory[12].placeStyle * 18), Slope = slope },
                new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = TileID.Stone, Slope = 1 });
            Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
            Until(context, input, () => Main.tile[44, 42].active() && Main.tile[46, 42].active() && Main.tile[46, 42].slope() == 1);
            Until(context, input, () => !(bool)Get(Get(tools, "Use"), "Active"));
            long uses = (long)Get(host, "BuildUsesStarted");
            for (int frame = 0; frame < 80; frame++) Step(context, input);
            Require(Main.tile[44, 42].type == TileID.Platforms && Main.tile[44, 42].slope() == 0 && !Main.tile[44, 42].halfBrick() &&
                p.inventory[12].stack == 1 && p.inventory[13].stack == 1 && Scene(host).Length == 1 && (long)Get(host, "BuildUsesStarted") == uses,
                "unreachable platform slope" + slope + " retains its pending goal without temporary blocks, replacement or repeated empty hammering; ordinary shape continues");
        }
    }
}
