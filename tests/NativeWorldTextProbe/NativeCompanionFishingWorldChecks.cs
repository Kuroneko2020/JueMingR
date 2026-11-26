using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionFishingWorldChecks
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private const string Prefix = "JueMingR.TerrariaHost.Companions.";
        private static Assembly hostAssembly;

        internal static void Run()
        {
            Program.RequireOriginalGame(typeof(Player).Assembly);
            string configuration = Program.ProductionConfiguration;
            hostAssembly = Assembly.LoadFrom(Path.Combine(Program.Repository, "artifacts", "build", configuration, "work", "bin",
                "JueMingR.TerrariaHost", "x86", configuration, "net472", "JueMingR.TerrariaHost.dll"));
            NativeFishingChecks.InitializeNativeEnvironment();
            var tiles = Main.tile;
            int width = Main.maxTilesX, height = Main.maxTilesY, mode = Main.netMode;
            var previousSeats = Main.sittingManager;
            try
            {
                Main.maxTilesX = Main.maxTilesY = 180;
                Main.tile = new Tile[180, 180];
                Main.netMode = 0;
                Main.sittingManager = new AnchoredEntitiesCollection();
                ClearWorld();
                CheckPonds();
                ClearWorld();
                CheckOpening();
                ClearWorld();
                CheckSeat();
                CheckFrozenFeet();
            }
            finally
            {
                Main.tile = tiles;
                Main.maxTilesX = width;
                Main.maxTilesY = height;
                Main.netMode = mode;
                Main.sittingManager = previousSeats;
            }
        }

        private static void ClearWorld()
        {
            for (int x = 0; x < Main.maxTilesX; x++) for (int y = 0; y < Main.maxTilesY; y++) Main.tile[x, y] = new Tile();
        }

        private static void Water(int left, int right, int top, int bottom)
        {
            for (int x = left; x <= right; x++) for (int y = top; y <= bottom; y++) Main.tile[x, y].liquid = 255;
        }

        private static Type HostType(string type) => hostAssembly.GetType(Prefix + type, true);

        private static object Make(Type type, params object[] args) => Activator.CreateInstance(type, Flags, null, args, null);

        private static int FieldInt(object instance, string name) => (int)Get(instance, name);

        private static void CheckPonds()
        {
            var field = HostType("CompanionFishingWater");
            Vector2 center = new Vector2(80 * 16 + 8, 82 * 16);
            object search = Make(field, center);
            Water(86, 87, 84, 125);
            Water(94, 101, 84, 100);
            Water(99, 103, 99, 100);
            var read = (Func<int, int, Tile>)((x, y) => Main.tile[x, y]);
            var advance = field.GetMethod("Advance", Flags);
            var circle = field.GetMethod("WithinCircle", Flags);
            Require((bool)circle.Invoke(null, new object[] { center, center + new Vector2(320, 0) }) &&
                !(bool)circle.Invoke(null, new object[] { center, center + new Vector2(320, 320) }), "20-tile boundary is round and inclusive");
            int scanTurns = 0;
            string result;
            do
            {
                object[] input = { 160, read };
                result = advance.Invoke(search, input).ToString();
                Require((int)input[0] >= 0, "candidate reads stay within the granted budget");
                scanTurns++;
            } while (result == "Pending" && scanTurns < 30);
            Require(result == "Ready" && scanTurns > 1, "candidate circle advances cooperatively across game steps");
            var entries = ((IEnumerable)Get(search, "Entries")).Cast<object>().ToArray();
            Point near = (Point)Get(entries[0], "Cell");
            Require(near == new Point(86, 84), "nearest high narrow pool wins against a larger pool on the same row");
            Require(entries.All(entry => Vector2.DistanceSquared(center, (Vector2)Get(entry, "Surface")) <= 320 * 320),
                "every exposed entry is inside the circle in world pixels");

            Type pond = field.GetNestedType("Pond", Flags);
            object measured = Make(pond, near);
            var next = pond.GetMethod("Advance", Flags);
            var missing = Main.tile[86, 106];
            Main.tile[86, 106] = null;
            object[] budget = { 160, read };
            Require(next.Invoke(measured, budget).ToString() == "Unknown", "unknown deep tile suspends counting instead of declaring insufficient water");
            Main.tile[86, 106] = missing;
            object[] tail = { 160, read };
            Require(next.Invoke(measured, tail).ToString() == "Ready", "loading the same pending tile resumes pond counting");
            var native = typeof(Projectile).GetMethod("GetFishingPondState", Flags);
            object[] query = { near.X, near.Y, false, false, 0, 0 };
            native.Invoke(null, query);
            Require((int)query[4] == 84 && FieldInt(measured, "Count") == (int)query[4] && (bool)Get(measured, "Eligible"),
                "native count includes high narrow water continuing outside the starting circle");
            var validate = pond.GetMethod("ValidateMinimum", Flags);
            object healthy = Make(pond, near);
            object[] healthyBudget = { 160, read };
            Require(validate.Invoke(healthy, healthyBudget).ToString() == "Ready" && (bool)Get(healthy, "Eligible") &&
                FieldInt(healthy, "Count") == 75, "steady recheck stops at a real sufficient amount");

            for (int x = 86; x <= 87; x++) for (int y = 120; y <= 125; y++) Main.tile[x, y].liquid = 0;
            var unknown = Main.tile[86, 113];
            Main.tile[86, 113] = null;
            object draining = Make(pond, near);
            object[] beforeSection = { 160, read };
            Require(validate.Invoke(draining, beforeSection).ToString() == "Unknown", "deep recheck waits for an unknown tile instead of changing pools");
            Main.tile[86, 113] = unknown;
            object[] afterSection = { 160, read };
            Require(validate.Invoke(draining, afterSection).ToString() == "Ready" && !(bool)Get(draining, "Eligible"),
                "known deep drainage invalidates the old amount while the casting row stays wet");
            object[] reduced = { near.X, near.Y, false, false, 0, 0 };
            native.Invoke(null, reduced);
            Require((int)reduced[4] == 72 && FieldInt(draining, "Count") == (int)reduced[4], "rechecked volume agrees with the actual vanilla result after draining");
            Console.WriteLine("PASS AI fishing candidate circle, stable nearest entry, native deep-pool volume, resumable missing terrain ({0} scan turns).", scanTurns);
        }

        private static void CheckOpening()
        {
            var actor = new Player { whoAmI = 6, direction = 1, gravDir = 1, width = 20, height = 42 };
            actor.Bottom = new Vector2(80 * 16 + 8, 90 * 16);
            var rod = new Item(); rod.SetDefaults(ItemID.WoodFishingPole);
            for (int y = 86; y <= 112; y++)
            {
                Main.tile[84, y].active(true);
                Main.tile[84, y].type = TileID.Stone;
            }
            Water(89, 95, 92, 109);
            Type entry = HostType("CompanionFishingWater").GetNestedType("Entry", Flags);
            Type castType = HostType("CompanionFishingCast");
            object cast = Make(castType, actor, rod, Make(entry, new Point(91, 92), new Vector2(91 * 16 + 8, 92 * 16 + 1), 0f));
            var read = (Func<int, int, Tile>)((x, y) => Main.tile[x, y]);
            var advance = castType.GetMethod("Advance", Flags);
            advance.Invoke(cast, new object[] { read, 1 });
            Require(!(bool)Get(cast, "Found"), "direct bobber aim cannot pass through the stone wall");
            int turns = 1, longestTurn = FieldInt(cast, "LastSteps");
            while (!(bool)Get(cast, "Found") && !(bool)Get(cast, "Exhausted") && turns < 60)
            {
                advance.Invoke(cast, new object[] { read, 2 });
                turns++;
                longestTurn = Math.Max(longestTurn, FieldInt(cast, "LastSteps"));
            }
            Require((bool)Get(cast, "Found") && (int)Get(cast, "Facing") == 1,
                "bounded native collision prediction finds a higher path into the intended pool");
            Require(longestTurn <= 2 * 180, "active planning caps projectile simulation work per actor turn");
            var current = castType.GetMethod("TerrainCurrent", Flags);
            Require((bool)current.Invoke(cast, new object[] { read }), "successful prediction retains its measured terrain dependency");
            Point source = actor.MountedCenter.ToTileCoordinates();
            Main.tile[source.X, source.Y].active(true);
            Main.tile[source.X, source.Y].type = TileID.Stone;
            Require(!(bool)current.Invoke(cast, new object[] { read }), "terrain changes invalidate the cached flight path");
            Console.WriteLine("PASS AI fishing opening prediction uses native collision, bounded steps and terrain validation ({0} turns, peak {1} simulated steps).", turns, longestTurn);
        }

        private static void CheckSeat()
        {
            var actor = new Player { whoAmI = 6, direction = 1, gravDir = 1, width = 20, height = 42 };
            bool day = Main.dayTime, raining = Main.raining;
            int moon = Main.moonPhase;
            double time = Main.time;
            float clouds = Main.cloudBGAlpha;
            try
            {
                Main.dayTime = true; Main.time = 10000; Main.moonPhase = 2; Main.raining = false; Main.cloudBGAlpha = 0;
                actor.inventory[0].SetDefaults(ItemID.WoodFishingPole);
                actor.inventory[54].SetDefaults(ItemID.Worm);
                Point cell = new Point(78, 80);
                var chair = Main.tile[cell.X, cell.Y];
                chair.active(true); chair.type = TileID.Chairs; chair.frameX = 18;
                var chairBottom = Main.tile[cell.X, cell.Y + 1];
                chairBottom.active(true); chairBottom.type = TileID.Chairs; chairBottom.frameX = 18; chairBottom.frameY = 18;
                Require(Terraria.GameContent.PlayerSittingHelper.GetSittingTargetInfo(actor, cell.X, cell.Y, out int direction,
                    out Vector2 seat, out _, out _), "native Terraria identifies the exact chair and its pose");
                actor.Bottom = seat + new Vector2(-16, 0);
                int standing = actor.GetFishingConditions().FinalFishingLevel;
                actor.sitting.SitDown(actor, cell.X, cell.Y);
                Require(actor.sitting.isSitting && actor.direction == direction && actor.Bottom == seat, "native SitDown commits its exact pose");
                Require(actor.GetFishingConditions().FinalFishingLevel == standing + 5, "real native chair adds its own five fishing power");
                Main.sittingManager.ClearPlayerAnchors();
                actor.sitting.UpdateSitting(actor);
                Require(actor.sitting.isSitting, "sitting survives a normal native step with no movement controls");
                actor.controlRight = true;
                actor.sitting.UpdateSitting(actor);
                Require(!actor.sitting.isSitting && actor.GetFishingConditions().FinalFishingLevel == standing,
                    "native movement leaves the seat and removes its fishing bonus");
                Console.WriteLine("PASS actual native chair pose, stable update, movement exit and fishing power.");
            }
            finally
            {
                Main.dayTime = day; Main.time = time; Main.moonPhase = moon; Main.raining = raining; Main.cloudBGAlpha = clouds;
            }
        }

        private static void CheckFrozenFeet()
        {
            var owner = new Player { gravDir = 1, width = 20, height = 42 };
            owner.Bottom = new Vector2(1024, 1280);
            object activity = Make(HostType("HostCompanions").GetNestedType("FishingActivity", Flags), owner);
            Vector2 feet = owner.Bottom;
            owner.Bottom += new Vector2(430, 200);
            Require((Vector2)Get(activity, "RequestedFeet") == feet && (Vector2)Get(activity, "FeetGoal") == feet,
                "owner motion after activation cannot change the fishing feet target");
            Console.WriteLine("PASS AI fishing activity freezes owner feet at activation.");
        }
    }
}
