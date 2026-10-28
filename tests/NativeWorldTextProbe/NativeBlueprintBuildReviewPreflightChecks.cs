using System;
using System.Collections;
using System.Linq;
using JueMingR.Features.Blueprint;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static void ReviewPlatformConnectivity(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.WoodPlatform); p.inventory[12].stack = 2;
            var entry = ReviewSource(context, host, input, p.inventory[12]);
            // Keep the already supported ungrouped/saved DTO path as well as
            // real capture's one-member Body; neither representation may
            // change the native attachment set or execution route.
            var ungrouped = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Platforms,
                FrameY = (short)(p.inventory[12].placeStyle * 18) });
            foreach (int mode in Enumerable.Range(9, 4).Concat(Enumerable.Range(0, 9)))
            {
                p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.WoodPlatform); p.inventory[12].stack = 2;
                ReviewPlatformSupport(70, mode); ReviewPlatformSupport(44, mode);
                bool legal = mode < 9;
                Require(ReviewPlayerOracle(p, 70, 40) == legal && Main.tile[70, 40].active() == legal,
                    "original Player+WorldGen platform oracle distinguishes current/cardinal wall, diagonal Tile and diagonal-only wall mode=" + mode);
                var placed = Place(context, host, input, legal ? entry : ungrouped, 44, 40);
                long uses = (long)Get(host, "BuildUsesStarted"), checks = (long)Get(host, "BuildCandidateChecks");
                SetAutomatic(host, input, true);
                if (!legal)
                {
                    Until(context, input, () => (long)Get(host, "BuildCandidateChecks") > checks);
                    for (int i = 0; i < 64; i++) Step(context, input);
                    Require(!Main.tile[44, 40].active() && p.inventory[12].stack == 2 && (long)Get(host, "BuildUsesStarted") == uses,
                        "a diagonal-only background wall must cause zero starts and zero consumption, mode=" + mode);
                    Require(WorldGen.PlaceTile(44, 41, TileID.Stone, true, true), "only a real native cardinal support is restored");
                }
                PreflightRecovered(context, input, () => !Scene(host).Contains(placed));
                Require(Main.tile[44, 40].active() && Main.tile[44, 40].type == TileID.Platforms && p.inventory[12].stack == 1 &&
                    (long)Get(host, "BuildUsesStarted") == uses + 1,
                    "all native legal platform attachments and restored invalid cases consume precisely once");
                Console.WriteLine("PASS Blueprint review platform mode=" + mode + " initiallyLegal=" + legal + " actualConsumed=1 refusedUses=" + (legal ? "NA" : "0"));
            }
        }
        private static void ReviewPlatformSupport(int x, int mode)
        {
            int[] dx = { -1, 1, 0, 0 }, dy = { 0, 0, -1, 1 };
            if (mode == 0) WorldGen.PlaceWall(x, 40, WallID.Stone, true);
            else if (mode < 5) WorldGen.PlaceWall(x + dx[mode - 1], 40 + dy[mode - 1], WallID.Stone, true);
            else
            {
                int d = (mode - 5) % 4, px = x + (d % 2 == 0 ? -1 : 1), py = 40 + (d / 2 == 0 ? -1 : 1);
                if (mode < 9) Require(WorldGen.PlaceTile(px, py, TileID.Stone, true, true), "native diagonal active Tile exists");
                else WorldGen.PlaceWall(px, py, WallID.Stone, true);
            }
        }

        private static void ReviewOrdinaryAdjacency(object context, object host, object tools, object input)
        {
            // Independent locked Item declarations for Player's entire special
            // branch. No catalog traversal manufactures the expected set.
            int[] tiles = { 51, 697, 330, 331, 332, 333, 336, 340, 341, 342, 343, 344, 379, 351 };
            int[] items = { 150, 5471, 71, 72, 73, 74, 2701, 2751, 2752, 2753, 2754, 2755, 3214, 2868 };
            for (int member = 0; member < tiles.Length; member++)
            {
                var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(items[member]); p.inventory[12].stack = 2;
                Require(p.inventory[12].createTile == tiles[member] && p.inventory[12].consumable && !Main.tileFrameImportant[tiles[member]],
                    "the locked direct item really belongs to the supported simple-placement branch");
                var entry = ReviewSource(context, host, input, p.inventory[12]);
                p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(items[member]); p.inventory[12].stack = 2;
                WorldGen.PlaceWall(44, 40, WallID.Stone, true); WorldGen.PlaceWall(70, 40, WallID.Stone, true);
                Require(!ReviewPlayerOracle(p, 70, 40), "the special native branch does not accept a wall only at the destination");
                var placed = Place(context, host, input, entry, 44, 40);
                long uses = (long)Get(host, "BuildUsesStarted"), checks = (long)Get(host, "BuildCandidateChecks");
                SetAutomatic(host, input, true); Until(context, input, () => (long)Get(host, "BuildCandidateChecks") > checks);
                for (int i = 0; i < 64; i++) Step(context, input);
                Require(!Main.tile[44, 40].active() && p.inventory[12].stack == 2 && (long)Get(host, "BuildUsesStarted") == uses,
                    "isolated member keeps zero starts even with a destination wall, tile=" + tiles[member]);
                ReviewChairSupport(69); Require(ReviewPlayerOracle(p, 70, 40), "only a non-solid furniture side neighbour is enough for the original consumer");
                ReviewChairSupport(43); AwaitPrepared(context, host, input);
                WorldGen.KillTile(43, 40, false, false, true);
                Require(!Main.tile[43, 40].active() && Main.tile[43, 41].active(), "native removal leaves only the diagonal floor, not a cardinal Tile");
                for (int i = 0; i < 64; i++) Step(context, input);
                Require(!Main.tile[44, 40].active() && p.inventory[12].stack == 2 && (long)Get(host, "BuildUsesStarted") == uses,
                    "a formerly Ready special member cannot use a removed furniture side anchor");
                long unknown = (long)Get(host, "BuildUnknownResults");
                ReviewChairSupport(43);
                bool falling = tiles[member] >= 330 && tiles[member] <= 333;
                if (falling)
                {
                    // Original TileID.Falling and WorldGen's 330..333 mapping
                    // create 411..414 after a legal unsupported placement.
                    // Do not turn a true transient consumption into a false
                    // stable-body/retirement expectation or suppress unknown.
                    PreflightRecovered(context, input, () => p.inventory[12].stack == 1 && !(bool)Get(Get(tools, "Use"), "Active"));
                    int projectile = tiles[member] - 330 + 411;
                    Require(!Main.tile[44, 40].active() && Scene(host).Contains(placed) && (long)Get(host, "BuildUnknownResults") == unknown + 1 &&
                        Main.projectile.Any(q => q.active && q.type == projectile && q.ai[0] == 1f && q.position.X >= 44 * 16 && q.position.X < 45 * 16),
                        "the original legal coin consumption creates its real falling projectile and retains unknown target/source protection");
                }
                else
                {
                    PreflightRecovered(context, input, () => !Scene(host).Contains(placed));
                    Require(Main.tile[44, 40].active() && Main.tile[44, 40].type == tiles[member] && (long)Get(host, "BuildUnknownResults") == unknown,
                        "the non-falling native body remains and retires through the unchanged current matcher");
                }
                Require(p.inventory[12].stack == 1 && (long)Get(host, "BuildUsesStarted") == uses + 1,
                    "restoring only furniture resumes this exact declared member through one native consumption");
                Console.WriteLine("PASS Blueprint review special adjacency tile=" + tiles[member] + " item=" + items[member] +
                    " isolatedUses=0 lateRemovedUses=0 actualConsumed=1 falling=" + falling + " bodyCompletion=" + (falling ? "UNKNOWN_RETAINED" : "CURRENT_MATCH"));
                if (falling) { Call(host, "SetAutoBuild", false); Call(host, "ClearProjection"); ReviewFreshPlayerSession(context); }
            }
        }
        private static void ReviewChairSupport(int x)
        {
            if (!Main.tile[x, 41].active()) Require(WorldGen.PlaceTile(x, 41, TileID.Stone, true, true), "native independent diagonal floor exists");
            Require(WorldGen.PlaceObject(x, 40, TileID.Chairs, true, 0, direction: -1) && Main.tile[x, 40].active() &&
                !Main.tileSolid[Main.tile[x, 40].type], "actual non-solid chair supplies the cardinal neighbour");
        }

        private static void ReviewLegacyLiquidEligibility(object context, object host, object tools, object input)
        {
            foreach (int item in new[] { ItemID.Torch, 150, 5471 })
                for (int transient = 0; transient < 2; transient++) ReviewWetLegacy(context, host, tools, input, item, transient != 0, false);
            // .8 WorldGen's three independently declared liquid-safe styles.
            foreach (int item in new[] { 523, 1333, 4384 })
                for (int transient = 0; transient < 2; transient++) ReviewWetLegacy(context, host, tools, input, item, transient != 0, true);
        }
        private static void ReviewWetLegacy(object context, object host, object tools, object input, int itemType, bool checkingOnly, bool allowed)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(itemType); p.inventory[12].stack = 2;
            var entry = ReviewSource(context, host, input, p.inventory[12]);
            p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(itemType); p.inventory[12].stack = 2;
            foreach (int x in new[] { 44, 70 })
                if (checkingOnly) Main.tile[x, 42].checkingLiquid(true); else PreflightLiquidStore(x, 42, 80, 0);
            Require(WorldGen.PlaceTile(70, 42, p.inventory[12].createTile, true, false, Main.myPlayer, p.inventory[12].placeStyle) == allowed &&
                Main.tile[70, 42].active() == allowed, "original legacy writer independently classifies liquid/checkingLiquid and native safe styles");
            var placed = Place(context, host, input, entry, 44, 42);
            long uses = (long)Get(host, "BuildUsesStarted"), checks = (long)Get(host, "BuildCandidateChecks"), unknown = (long)Get(host, "BuildUnknownResults");
            SetAutomatic(host, input, true);
            if (!allowed)
            {
                Until(context, input, () => (long)Get(host, "BuildCandidateChecks") > checks);
                for (int i = 0; i < 64; i++) Step(context, input);
                Require(!Main.tile[44, 42].active() && p.inventory[12].stack == 2 && (long)Get(host, "BuildUsesStarted") == uses,
                    "known legacy liquid refusal starts no use and consumes nothing, item=" + itemType + " checkingOnly=" + checkingOnly);
                if (checkingOnly)
                {
                    long before = ReviewSupportStamp(host, placed);
                    Main.tile[44, 42].checkingLiquid(false);
                    Require(Main.tile[44, 42].liquid == 0 && ReviewSupportStamp(host, placed) == before,
                        "only the original transient setter clears checkingLiquid, with no support event, material, movement or toggle change");
                }
                else PreflightLiquidStore(44, 42, 0, 0);
            }
            int updates = PreflightRecovered(context, input, () => !Scene(host).Contains(placed));
            Require(Main.tile[44, 42].active() && Main.tile[44, 42].type == p.inventory[12].createTile && p.inventory[12].stack == 1 &&
                (long)Get(host, "BuildUsesStarted") == uses + 1 && (long)Get(host, "BuildUnknownResults") == unknown &&
                (!allowed || checkingOnly || Main.tile[44, 42].liquid == 80),
                "only the qualified original target consumes once; safe wet styles stay legal and transient recovery does not empty-swing");
            Console.WriteLine("PASS Blueprint review legacy liquid item=" + itemType + " checkingOnly=" + checkingOnly + " initiallyAllowed=" + allowed +
                " refusedUses=" + (allowed ? "NA" : "0") + " actualConsumed=1 updates=" + updates);
        }
        private static long ReviewSupportStamp(object host, object placed)
        {
            var range = ((IEnumerable)Get(Get(placed, "Match"), "Ranges")).Cast<object>().First();
            var observer = Get(host, "projectionChanges"); object[] args = { Get(range, "Watch"), Get(range, "PartIndex"), 0L };
            Require((bool)observer.GetType().GetMethod("TryGetBuildStamp", F).Invoke(observer, args), "current target support stamp is available for the no-event witness");
            return (long)args[2];
        }
        private static LibraryEntry ReviewSource(object context, object host, object input, Item item)
        {
            Require(WorldGen.PlaceTile(60, 42, item.createTile, true, false, Main.myPlayer, item.placeStyle), "source is written by the actual declared WorldGen consumer");
            Call(host, "SetMaterialViewDemand", true);
            try
            {
                Until(context, input, () => GetOptional(host, "MaterialCatalog") != null);
                var type = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintCaptureSource", true);
                var source = (IBlueprintCaptureSource)Activator.CreateInstance(type, F, null, new object[] { (Func<bool>)(() => true) }, null);
                CaptureCell cell; Require(source.Read(60, 42, out cell), "actual captured source is readable");
                var descriptor = ((BlueprintPlacementCatalog)Get(host, "MaterialCatalog")).Tile(cell, source.Describe(cell)?.Object);
                Require(descriptor != null && !descriptor.Flexible && descriptor.UseItemType == item.type && descriptor.ConsumeItemType == item.type &&
                    descriptor.Units == 1 && descriptor.TileType == item.createTile && descriptor.Style == item.placeStyle,
                    "current real catalog already supports this exact source; the fixture adds no descriptor or new category");
            }
            finally { Call(host, "SetMaterialViewDemand", false); }
            return NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, (LibraryState)Get(host, "Library"),
                () => Step(context, input), 60, 42, 1, 1, "原版资格返修 " + item.type);
        }
        private static bool ReviewPlayerOracle(Player p, int x, int y)
        {
            int oldX = Player.tileTargetX, oldY = Player.tileTargetY, selected = p.selectedItem;
            try
            {
                p.selectedItemState.Select(12); p.selectedItemState.Update(); Player.tileTargetX = x; Player.tileTargetY = y;
                bool allowed = (bool)typeof(Player).GetMethod("PlaceThing_Tiles_BlockPlacementForAssortedThings", F).Invoke(p, new object[] { false });
                return allowed && WorldGen.PlaceTile(x, y, p.inventory[12].createTile, true, false, Main.myPlayer, p.inventory[12].placeStyle);
            }
            finally { Player.tileTargetX = oldX; Player.tileTargetY = oldY; p.selectedItemState.Select(selected); p.selectedItemState.Update(); }
        }
    }
}
