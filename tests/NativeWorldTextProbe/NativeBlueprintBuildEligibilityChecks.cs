using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.NetModules;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.Net;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static object eligibilityWorker;
        private static ManualResetEventSlim eligibilitySourceEntered, eligibilitySourceContinue;
        private static void PauseEligibilitySource(object __instance, bool __0)
        {
            if (!__0 || !ReferenceEquals(__instance, eligibilityWorker) || eligibilitySourceEntered.IsSet) return;
            lock (Get(__instance, "gate")) if (((ICollection)Get(__instance, "projectionSourceRequests")).Count == 0) return;
            eligibilitySourceEntered.Set();
            if (!eligibilitySourceContinue.Wait(8000)) throw new InvalidOperationException("Eligibility fixture did not release the original source worker");
        }
        private static object[] EligibilityRanges(object item)
        { return ((IEnumerable)Get(Get(item, "Match"), "Ranges")).Cast<object>().ToArray(); }
        private static bool EligibilityDormant(object item)
        {
            var state = GetOptional(item, "Match");
            return state != null && EligibilityRanges(item).All(range =>
            {
                var value = GetOptional(range, "Materials");
                return (bool)Get(range, "Current") && !(bool)Get(range, "Demand") && !(bool)Get(range, "Reading") && value != null &&
                    (bool)Get(value, "Current") && (bool)Get(value, "BuildExhausted") && (bool)Get(value, "EligibilityRetry");
            });
        }
        private static void GlobalPylonEligibility(object context, object host, object tools, object input)
        {
            var oldPylons = Main.PylonSystem; var oldManager = TileEntity.manager;
            var oldById = TileEntity.ByID; var oldByPosition = TileEntity.ByPosition; var oldUpdates = TileEntity.UpdateEntities; int oldNext = TileEntity.TileEntitiesNextID;
            var patch = new Harmony("JueMingR.Tests.BlueprintEligibilitySource");
            try
            {
                var p = Reset(context, host, tools, input); p.position = new Vector2(44 * 16, 646);
                Main.PylonSystem = new TeleportPylonsSystem();
                if (TileEntity.manager == null) TileEntity.InitializeAll();
                TileEntity.ByID = new Dictionary<int, TileEntity>(); TileEntity.ByPosition = new Dictionary<Point16, TileEntity>(); TileEntity.UpdateEntities = new List<TileEntity>(); TileEntity.TileEntitiesNextID = 0;
                if (NetManager.Instance.GetModule<NetTeleportPylonModule>() == null) NetManager.Instance.Register<NetTeleportPylonModule>();
                var instances = new object[2]; var data = TileObjectData.GetTileData(TileID.TeleportationPylon, 0);
                Require(data.Width == 3 && data.Height == 4, "locked pylon metadata has its complete original footprint");
                for (int n = 0; n < 2; n++)
                {
                    int item = TETeleportationPylon.GetPylonItemTypeFromTileStyle(n), sx = 80 + n * 10;
                    p.inventory[12 + n].SetDefaults(item); p.inventory[12 + n].stack = 2;
                    Require(WorldGen.PlaceObject(sx, 42, TileID.TeleportationPylon, true, n), "independent source uses original pylon CanPlace/Place");
                    var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, (LibraryState)Get(host, "Library"),
                        () => Step(context, input), sx - data.Origin.X, 42 - data.Origin.Y, data.Width, data.Height, "远处晶塔资格" + n);
                    Require(TETeleportationPylon.PlacementPreviewHook_AfterPlacement(sx, 42) >= 0 && Main.PylonSystem.HasPylonOfType((TeleportPylonType)n),
                        "original post-placement hook registers the remote same-type tile entity");
                    instances[n] = Place(context, host, input, entry, 44 + n * 5, 39);
                }
                p.inventory[14].SetDefaults(ItemID.StoneBlock); p.inventory[14].stack = 2;
                Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone }), 43, 42);
                SetAutomatic(host, input, true);
                Until(context, input, () => instances.All(EligibilityDormant) && Main.tile[43, 42].active() && !(bool)Get(Get(tools, "Use"), "Active"));
                Require(p.inventory[12].stack == 2 && p.inventory[13].stack == 2 && p.inventory[14].stack == 1,
                    "two distinct native global rejections consume nothing and yield to the independent ready stone instance");
                long retryUses = (long)Get(host, "BuildUsesStarted"), retryReads = (long)Get(host, "ProjectionMatchCellReads"), retryRequests = (long)Get(host, "ProjectionMatchSourceRequests");
                for (int i = 0; i < 620; i++)
                {
                    long before = (long)Get(host, "ProjectionMatchCellReads"), steps = (long)Get(host, "ProjectionMatchSourceSteps");
                    p.position = new Vector2(44 * 16 + i % 2, 646); p.velocity = Vector2.Zero;
                    p.inventory[20].SetDefaults(ItemID.Wood); p.inventory[20].stack = 1 + i % 2;
                    Step(context, input);
                    Require((long)Get(host, "ProjectionMatchCellReads") - before <= 256 && (long)Get(host, "ProjectionMatchSourceSteps") - steps <= 256,
                        "eligibility recovery retains the original per-update world/source budgets");
                }
                long repeatUses = (long)Get(host, "BuildUsesStarted") - retryUses, repeatReads = (long)Get(host, "ProjectionMatchCellReads") - retryReads;
                Require(repeatUses == 0 && repeatReads > 0 && repeatReads <= 192 && (long)Get(host, "ProjectionMatchSourceRequests") - retryRequests <= 16 && p.inventory[12].stack == 2 && p.inventory[13].stack == 2,
                    "unchanged remote qualification is rechecked at bounded intervals before any new use starts; movement and inventory noise cannot produce periodic empty animations");
                Until(context, input, () => instances.All(EligibilityDormant) && !(bool)Get(Get(tools, "Use"), "Active"));
                SetAutomatic(host, input, false);
                long closedReads = (long)Get(host, "ProjectionMatchCellReads"), closedRequests = (long)Get(host, "ProjectionMatchSourceRequests"), closedUses = (long)Get(host, "BuildUsesStarted"), closedWakes = (long)Get(host, "BuildEligibilityRetryWakes");
                for (int i = 0; i < 500; i++) Step(context, input);
                Require(closedReads == (long)Get(host, "ProjectionMatchCellReads") && closedRequests == (long)Get(host, "ProjectionMatchSourceRequests") && closedUses == (long)Get(host, "BuildUsesStarted") && closedWakes == (long)Get(host, "BuildEligibilityRetryWakes"),
                    "disabled automatic work does not retry formerly rejected ranges even beyond two recovery intervals");
                SetAutomatic(host, input, true);
                Until(context, input, () => instances.All(EligibilityDormant) && !(bool)Get(Get(tools, "Use"), "Active"));
                long uses = (long)Get(host, "BuildUsesStarted"), reads = (long)Get(host, "ProjectionMatchCellReads"), requests = (long)Get(host, "ProjectionMatchSourceRequests");
                ulong now = (ulong)Get(host, "materialTick");
                ulong due = instances.SelectMany(EligibilityRanges).Min(range => (ulong)Get(Get(range, "Materials"), "EligibilityRetryAt"));
                int quiet = (int)Math.Min(80UL, due > now ? due - now - 1 : 0);
                for (int i = 0; i < quiet; i++) Step(context, input);
                Require(quiet > 0 && uses == (long)Get(host, "BuildUsesStarted") && reads == (long)Get(host, "ProjectionMatchCellReads") && requests == (long)Get(host, "ProjectionMatchSourceRequests"),
                    "stable no-spend backoff performs no native use, source request or world reread before its per-range deadline");
                Vector2 position = p.position; long epoch = (long)Get(host, "MaterialBuildEligibilityRevision"), inventory = ((BlueprintMaterialInventory)Get(host, "MaterialInventory")).Revision;
                long invalidations = (long)Get(host, "ProjectionMatchInvalidations"), wakes = (long)Get(host, "BuildEligibilityRetryWakes");
                // Exercise the cold-source arm deliberately. A valid retained
                // immutable window now correctly bypasses the worker entirely;
                // waiting for its old request would misclassify successful
                // reuse as a timeout. Evict only transport references, just as
                // bounded cache pressure may, without touching the target,
                // ordinal, world proof or eligibility deadline.
                Call(host, "RemoveBuildSources", null, null);
                eligibilityWorker = Get(Get(host, "Library"), "worker"); eligibilitySourceEntered = new ManualResetEventSlim(); eligibilitySourceContinue = new ManualResetEventSlim();
                patch.Patch(AccessTools.Method(eligibilityWorker.GetType(), "ProjectionSourceStep"), prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(PauseEligibilitySource)));
                // These remote native removals update the real PylonSystem.
                // They are well outside both destination watches; no player,
                // inventory, target, mask or automatic preference is changed.
                WorldGen.KillTile(79, 39, noItem: true); WorldGen.KillTile(89, 39, noItem: true);
                Require(!Main.PylonSystem.HasPylonOfType(TeleportPylonType.SurfacePurity) && !Main.PylonSystem.HasPylonOfType((TeleportPylonType)1), "original remote destruction removes both global blockers");
                Until(context, input, () => eligibilitySourceEntered.IsSet);
                for (int i = 0; i < 300; i++) Step(context, input);
                Require((long)Get(host, "BuildEligibilityRetryWakes") - wakes == 2 && (long)Get(host, "BuildUsesStarted") == uses &&
                    (long)Get(host, "ProjectionMatchCellReads") == reads && (long)Get(host, "ProjectionMatchInvalidations") == invalidations &&
                    p.position == position && (long)Get(host, "MaterialBuildEligibilityRevision") == epoch && ((BlueprintMaterialInventory)Get(host, "MaterialInventory")).Revision == inventory,
                    "both due ranges wake through the ordinary finite cursor, without a global 1/240 cap or repeatedly renewing work while actual source is unavailable");
                eligibilitySourceContinue.Set();
                Until(context, input, () => Scene(host).Length == 0);
                Require(p.position == position && p.inventory[12].stack == 1 && p.inventory[13].stack == 1 && Main.tile[44, 39].active() && Main.tile[49, 39].active() &&
                    Main.PylonSystem.HasPylonOfType(TeleportPylonType.SurfacePurity) && Main.PylonSystem.HasPylonOfType((TeleportPylonType)1),
                    "real ItemCheck/CanPlace/post-hook spend one each and full Match naturally retires both unchanged waiting diagrams after remote eligibility changes");
                long stableReads = (long)Get(host, "ProjectionMatchCellReads"), stableWakes = (long)Get(host, "BuildEligibilityRetryWakes"), stableUses = (long)Get(host, "BuildUsesStarted");
                for (int i = 0; i < 300; i++) Step(context, input);
                Require(stableReads == (long)Get(host, "ProjectionMatchCellReads") && stableWakes == (long)Get(host, "BuildEligibilityRetryWakes") && stableUses == (long)Get(host, "BuildUsesStarted"),
                    "retired scene has zero eligibility retry, source/world sampling and native action growth");
                Console.WriteLine("PASS BlueprintBuild remote global eligibility: two real pylon types, independent ready target, movingUnrelatedInventoryTokens=620 repeatUses=" + repeatUses + " repeatWorldReads=" + repeatReads + ", quietTokens=" + quiet + ", bothRangeWakes=2, testEvictedImmutableTransport=true sourceBlockedTokens=300, naturalRetirement=true");
            }
            finally
            {
                eligibilitySourceContinue?.Set();
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                eligibilityWorker = null; Call(host, "SetAutoBuild", false); Call(host, "ClearProjection");
                TileEntity.ByID = oldById; TileEntity.ByPosition = oldByPosition; TileEntity.UpdateEntities = oldUpdates; TileEntity.TileEntitiesNextID = oldNext; TileEntity.manager = oldManager; Main.PylonSystem = oldPylons;
            }
        }
        private static long EligibilityMaterialStamp(object host, object range)
        {
            object[] args = { Get(range, "Watch"), Get(range, "PartIndex"), 0L };
            Require((bool)Call(Get(host, "projectionChanges"), "TryGetMaterialStamp", args), "real material part stamp is current after its native writes drain");
            return (long)args[2];
        }
        private static void MaterialOnlyBuildEligibility(object context, object host, object tools, object input)
        {
            var oldPylons = Main.PylonSystem; var oldManager = TileEntity.manager;
            var oldById = TileEntity.ByID; var oldByPosition = TileEntity.ByPosition; var oldUpdates = TileEntity.UpdateEntities; int oldNext = TileEntity.TileEntitiesNextID;
            try
            {
                var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(TETeleportationPylon.GetPylonItemTypeFromTileStyle(0)); p.inventory[12].stack = 2;
                Main.PylonSystem = new TeleportPylonsSystem(); if (TileEntity.manager == null) TileEntity.InitializeAll();
                TileEntity.ByID = new Dictionary<int, TileEntity>(); TileEntity.ByPosition = new Dictionary<Point16, TileEntity>(); TileEntity.UpdateEntities = new List<TileEntity>(); TileEntity.TileEntitiesNextID = 0;
                if (NetManager.Instance.GetModule<NetTeleportPylonModule>() == null) NetManager.Instance.Register<NetTeleportPylonModule>();
                Require(WorldGen.PlaceObject(80, 42, TileID.TeleportationPylon, true), "material-only seam uses a real remote pylon source");
                for (int x = 79; x < 82; x++) for (int y = 39; y < 43; y++) WorldGen.PlaceWall(x, y, WallID.Wood, true);
                var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, (LibraryState)Get(host, "Library"), () => Step(context, input), 79, 39, 3, 4, "晶塔墙漆资格接缝");
                Require(TETeleportationPylon.PlacementPreviewHook_AfterPlacement(80, 42) >= 0, "original remote entity really blocks the same pylon type");
                for (int x = 44; x < 47; x++) for (int y = 39; y < 43; y++) WorldGen.PlaceWall(x, y, WallID.Wood, true);
                var item = Place(context, host, input, entry, 44, 39); object range = null;
                SetAutomatic(host, input, true);
                Until(context, input, () =>
                {
                    if (GetOptional(item, "Match") == null) return false;
                    range = EligibilityRanges(item).Single(r => (int)Get(Get(r, "Source"), "ObjectId") > 0);
                    var material = GetOptional(range, "Materials");
                    return material != null && (bool)Get(material, "Current") && (bool)Get(material, "BuildExhausted") && (bool)Get(material, "EligibilityRetry") &&
                        !(bool)Get(range, "Demand") && !(bool)Get(range, "Reading") && !(bool)Get(Get(tools, "Use"), "Active");
                });
                Require((bool)Get(range, "MaterialCrossLayers") && p.inventory[12].stack == 2 && !Main.tile[44, 39].active(), "captured furniture/wall part retains its actual no-spend global rejection");
                long version = (long)Get(range, "Version"), wake = (long)Get(Get(range, "Materials"), "BuildWakeRevision"), stamp = EligibilityMaterialStamp(host, range);
                long epoch = (long)Get(host, "MaterialBuildEligibilityRevision"), inventory = ((BlueprintMaterialInventory)Get(host, "MaterialInventory")).Revision, uses = (long)Get(host, "BuildUsesStarted");
                Vector2 position = p.position;
                Require(WorldGen.paintWall(44, 39, PaintID.BluePaint, false, false), "actual native wall-paint store changes only the material context of the object part");
                Until(context, input, () => (bool)Get(Get(range, "Materials"), "Current") && !(bool)Get(range, "Demand") && !(bool)Get(range, "Reading"));
                bool waiting = (bool)Get(Get(range, "Materials"), "EligibilityRetry");
                Require((bool)Get(range, "Current") && version == (long)Get(range, "Version") && wake == (long)Get(Get(range, "Materials"), "BuildWakeRevision") && stamp != EligibilityMaterialStamp(host, range) &&
                    epoch == (long)Get(host, "MaterialBuildEligibilityRevision") && inventory == ((BlueprintMaterialInventory)Get(host, "MaterialInventory")).Revision && p.position == position && (long)Get(host, "BuildUsesStarted") == uses,
                    "paintWall uses the independent material channel: object proof, build wake, epoch, inventory, position and native use remain unchanged");
                WorldGen.KillTile(79, 39, noItem: true); TileObject original;
                Require(TileObject.CanPlace(45, 42, TileID.TeleportationPylon, 0, 1, out original), "after the real remote demolition, the original complete placement selector permits this unchanged target");
                for (int i = 0; i < 600; i++) Step(context, input);
                Console.WriteLine("MATERIAL ONLY ELIGIBILITY waitingAfterPaint=" + waiting + " objectVersion=" + version + "->" + Get(range, "Version") + " buildWake=" + wake + "->" + Get(Get(range, "Materials"), "BuildWakeRevision") +
                    " epoch=" + epoch + "->" + Get(host, "MaterialBuildEligibilityRevision") + " nativeUses=" + uses + "->" + Get(host, "BuildUsesStarted") + " active=" + Main.tile[44, 39].active() + " stack=" + p.inventory[12].stack);
                Require(waiting && Main.tile[44, 39].active() && p.inventory[12].stack == 1 && p.position == position && Scene(host).Contains(item),
                    "material-only contribution invalidation must preserve the rejected range's bounded recovery while pending wall appearance still prevents full retirement");
                Require(WorldGen.paintWall(44, 39, 0, false, false), "independent original writer restores the final captured wall appearance");
                Until(context, input, () => Scene(host).Length == 0);
                Console.WriteLine("PASS BlueprintBuild material-only paint invalidation preserves non-local wait without a build wake; actual remote release resumes body, full appearance still gates retirement");
            }
            finally
            {
                Call(host, "SetAutoBuild", false); Call(host, "ClearProjection");
                TileEntity.ByID = oldById; TileEntity.ByPosition = oldByPosition; TileEntity.UpdateEntities = oldUpdates; TileEntity.TileEntitiesNextID = oldNext; TileEntity.manager = oldManager; Main.PylonSystem = oldPylons;
            }
        }
        private static void GlobalChestEligibility(object context, object host, object tools, object input)
        {
            var chests = Main.chest; var byCoords = AccessTools.Field(typeof(Chest), "_chestsByCoords").GetValue(null);
            try
            {
                var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.Chest); p.inventory[12].stack = 2;
                Main.chest = new Chest[8000]; AccessTools.Field(typeof(Chest), "_chestsByCoords").SetValue(null, new Dictionary<Point, Chest>());
                Require(WorldGen.PlaceObject(60, 42, TileID.Containers, true), "capacity source is a real original chest body");
                var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, (LibraryState)Get(host, "Library"), () => Step(context, input), 60, 41, 2, 2, "远处容器容量");
                for (int i = 0; i < 8000; i++) Chest.Assign(Chest.CreateOutOfArray(i, 120 + i % 70, 5 + i / 70, 0));
                Require(Chest.FindEmptyChest(44, 41) == -1, "the original global capacity selector genuinely rejects a full 8000-slot registry");
                var item = Place(context, host, input, entry, 44, 41); SetAutomatic(host, input, true);
                Until(context, input, () => EligibilityDormant(item) && !(bool)Get(Get(tools, "Use"), "Active"));
                Require(p.inventory[12].stack == 2 && !Main.tile[44, 41].active(), "full global chest capacity rejects actual native placement without spending");
                Vector2 position = p.position; long epoch = (long)Get(host, "MaterialBuildEligibilityRevision"), inventory = ((BlueprintMaterialInventory)Get(host, "MaterialInventory")).Revision;
                Require(Chest.DestroyChest(120, 5) && Chest.FindEmptyChest(44, 41) == 0, "the original remote chest removal frees its genuine global capacity slot");
                Until(context, input, () => Scene(host).Length == 0);
                Require(p.position == position && p.inventory[12].stack == 1 && Chest.FindChest(44, 41) == 0 && Main.tile[44, 41].active(),
                    "the same bounded recovery also handles global chest capacity through actual CanPlace/AfterPlacement/Match");
                Console.WriteLine("PASS BlueprintBuild remote chest-capacity recovery: original 8000-slot rejection/free/consume, startingEpoch=" + epoch + " startingInventory=" + inventory);
            }
            finally { Call(host, "SetAutoBuild", false); Call(host, "ClearProjection"); Main.chest = chests; AccessTools.Field(typeof(Chest), "_chestsByCoords").SetValue(null, byCoords); }
        }
        private static void GlobalBiomeEligibility(object context, object host, object tools, object input)
        {
            using (NativeBlueprintWorldMatchChecks.EmptyWorld(host, 200, 400))
            {
                var p = Reset(context, host, tools, input); bool favor = p.unlockedBiomeTorches; int toggle = p.builderAccStatus[11];
                try
                {
                    p.inventory[12].SetDefaults(ItemID.Torch); p.inventory[12].stack = 2; p.unlockedBiomeTorches = true; p.UsingBiomeTorches = true;
                    Require(WorldGen.PlaceTile(60, 42, TileID.Torches, true, true), "biome source is a genuine ordinary torch with captured style zero");
                    var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, (LibraryState)Get(host, "Library"), () => Step(context, input), 60, 42, 1, 1, "生态派生火把");
                    for (int x = 10; x < 91; x++) for (int y = 75; y < 101; y++) { Main.tile[x, y].active(true); Main.tile[x, y].type = TileID.SnowBlock; }
                    Main.PlayerSceneMetrics.Reset(); p.UpdateSceneMetrics(); p.UpdateBiomes(); Require(p.ZoneSnow, "original SceneMetrics/UpdateBiomes derive Snow from real remote tiles");
                    var item = Place(context, host, input, entry, 44, 42); SetAutomatic(host, input, true);
                    Until(context, input, () => EligibilityDormant(item));
                    Require(p.inventory[12].stack == 2 && !Main.tile[44, 42].active(), "derived native Snow torch style cannot silently replace the captured ordinary style");
                    Vector2 position = p.position; long uses = (long)Get(host, "BuildUsesStarted"), epoch = (long)Get(host, "MaterialBuildEligibilityRevision");
                    for (int x = 10; x < 91; x++) for (int y = 75; y < 101; y++) WorldGen.KillTile(x, y, noItem: true);
                    Main.PlayerSceneMetrics.Reset(); p.UpdateSceneMetrics(); p.UpdateBiomes(); Require(!p.ZoneSnow, "original remote ecology removal and scene scan clear the derived biome");
                    Require(epoch == (long)Get(host, "MaterialBuildEligibilityRevision") && position == p.position && p.inventory[12].stack == 2, "ecology refresh does not alter the existing local eligibility inputs");
                    Until(context, input, () => Scene(host).Length == 0);
                    Require(position == p.position && p.inventory[12].stack == 1 && Main.tile[44, 42].type == TileID.Torches && Main.tile[44, 42].frameY == 0 && (long)Get(host, "BuildUsesStarted") > uses,
                        "unmodified ordinary style resumes through original biome helper and ItemCheck after remote ecology changes");
                    Console.WriteLine("PASS BlueprintBuild original remote SceneMetrics -> biome style rejection -> unchanged target recovery, no direct Zone flag writes");
                }
                finally { Call(host, "SetAutoBuild", false); Call(host, "ClearProjection"); p.unlockedBiomeTorches = favor; p.builderAccStatus[11] = toggle; Main.PlayerSceneMetrics.Reset(); p.UpdateSceneMetrics(); p.UpdateBiomes(); }
            }
        }
        private static bool removeEligibilityBody;
        private static void RemoveEligibilityBody(Player __instance)
        {
            if (!removeEligibilityBody || faultTools == null || !(bool)Get(Get(faultTools, "Use"), "Building") ||
                Player.tileTargetX != 44 || Player.tileTargetY != 42 || !Main.tile[44, 42].active()) return;
            // A controlled external native write removes the body after the
            // real PlaceIt succeeded. The original action still owns its real
            // consumption; the fixture neither refunds nor supplies a receipt.
            removeEligibilityBody = false; WorldGen.KillTile(44, 42, noItem: true);
        }
        private static void UnknownEligibilityProtection(object context, object host, object tools, object input)
        {
            var patch = new Harmony("JueMingR.Tests.BlueprintEligibilityUnknown");
            try
            {
                var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 3;
                p.inventory[13].SetDefaults(ItemID.DirtBlock); p.inventory[13].stack = 2;
                var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone }, new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = TileID.Dirt });
                var item = Place(context, host, input, entry, 44, 42);
                patch.Patch(AccessTools.Method(typeof(Player), "PlaceThing_Tiles_PlaceIt"), postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(RemoveEligibilityBody)));
                long unknown = (long)Get(host, "BuildUnknownResults"); removeEligibilityBody = true; SetAutomatic(host, input, true);
                Until(context, input, () => (long)Get(host, "BuildUnknownResults") > unknown && Main.tile[46, 42].active() && !(bool)Get(Get(tools, "Use"), "Active"));
                Require(!Main.tile[44, 42].active() && p.inventory[12].stack == 2 && p.inventory[13].stack == 1 && Scene(host).Contains(item),
                    "spent-but-empty original action becomes unknown while the independent dirt target still completes");
                long uses = (long)Get(host, "BuildUsesStarted"), wakes = (long)Get(host, "BuildEligibilityRetryWakes");
                for (int i = 0; i < 600; i++)
                {
                    ProgressStep(context, host, input, p, i, true, true);
                    if (i == 120) { p.inventory[14].SetDefaults(ItemID.StoneBlock); p.inventory[14].stack = 2; }
                }
                Require(uses == (long)Get(host, "BuildUsesStarted") && wakes == (long)Get(host, "BuildEligibilityRetryWakes") && p.inventory[12].stack == 2 &&
                    p.inventory[14].stack == 2 && (bool)Get(Get(Get(tools, "Items"), "Ownership"), "AnyProtected") &&
                    EligibilityRanges(item).All(range => GetOptional(range, "Materials") == null || !(bool)Get(Get(range, "Materials"), "EligibilityRetry")),
                    "an unknown/spent target never enters the no-spend recovery mechanism across multiple intervals");
                Console.WriteLine("PASS BlueprintBuild consumed/empty unknown stays protected for 600 moving/unrelated-inventory tokens plus new matching supply, independent target advances, no eligibility retry permission");
            }
            finally
            {
                removeEligibilityBody = false;
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                Call(host, "SetAutoBuild", false); Call(host, "ClearProjection");
                // Tools intentionally retain unknown sources when the same
                // player/world merely re-enters from menu. Publish an actual
                // different player identity, then restore the fixture player;
                // the normal session owner clears old-source protection.
                var previous = Main.player[Main.myPlayer];
                try { Main.player[Main.myPlayer] = new Player { whoAmI = Main.myPlayer, active = true }; Call(context, "UpdateRuntime"); }
                finally { Main.player[Main.myPlayer] = previous; Call(context, "UpdateRuntime"); }
            }
        }
    }
}
