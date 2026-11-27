using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionServiceTaskChecks
    {
        private static void TraceFixtureBodyFault(Exception failure) => Console.WriteLine("Service fixture native body fault: " + failure);
        private static void TraceFixtureNativeService(Exception __exception)
        {
            if (__exception != null) Console.WriteLine("Service fixture native operation fault: " + __exception);
        }

        internal static void Run(object host, Player human, Player body, Action tick)
        {
            var actor = Call(host, "Owned", body);
            var profile = (CompanionProfile)Get(actor, "Profile");
            var identity = new CompanionTaskIdentity(profile.OwnerId, profile.Id, (long)Get(host, "session"),
                (long)Get(actor, "Generation"), profile.DataEpoch);
            var anchor = host.GetType().GetMethod("CaptureFishingAnchor", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { human });
            CompanionTaskOutcome Query(string action, params (string Key, object Value)[] args) =>
                (CompanionTaskOutcome)Call(host, "TryGetTaskObservation", identity, action,
                    args.ToDictionary(pair => pair.Key, pair => pair.Value is short number ? (object)(int)number : pair.Value), anchor);
            CompanionTaskSnapshot Step(string action, params (string Key, object Value)[] arguments)
            {
                var result = (CompanionTaskSnapshot)Call(host, "SubmitTask", identity, "真实 NPC 服务",
                    new[] { new CompanionTaskStep(action, arguments.ToDictionary(pair => pair.Key,
                        pair => pair.Value is short number ? (object)(int)number : pair.Value)) }, anchor, Guid.NewGuid());
                Require(result.State == "accepted", action + " admitted: " + result.Detail +
                    " body active/dead=" + body.active + "/" + body.dead +
                    "; actor ready/closing/faulted=" + GetOptional(actor, "Ready") + "/" + GetOptional(actor, "Closing") + "/" + GetOptional(actor, "Faulted") +
                    "; previous terminal=" + GetOptional(actor, "TerminalFailure") + "; host=" + GetOptional(host, "Feedback"));
                for (int i = 0; i < 120 && result.IsActive; i++)
                {
                    Main.sittingManager?.ClearPlayerAnchors();
                    tick();
                    result = (CompanionTaskSnapshot)Call(host, "GetTask", profile.OwnerId, profile.Id, result.TaskId);
                }
                Require(!result.IsActive, action + " reached a terminal receipt: " + result.Detail);
                return result;
            }

            var oldInventory = body.inventory.ToArray();
            var oldQuest = Main.anglerQuest;
            var oldFinished = Main.anglerQuestFinished;
            var oldNames = Main.anglerWhoFinishedToday;
            int oldCount = body.anglerQuestsFinished;
            var oldHumanCount = human.anglerQuestsFinished;
            var oldHumanName = human.name;
            var oldBodyName = body.name;
            var cursor = Main.mouseItem;
            var text = Main.npcChatText;
            var originalNpcs = Main.npc.ToArray();
            var roomTiles = WorldGen.roomTiles;
            var previousSeats = Main.sittingManager;
            bool oldDay = Main.dayTime;
            double oldTime = Main.time;
            Point chairPoint = body.Bottom.ToTileCoordinates();
            chairPoint.Y--;
            Tile originalChairBottom = Main.tile[chairPoint.X, chairPoint.Y];
            Tile originalChairTop = Main.tile[chairPoint.X, chairPoint.Y - 1];
            NPC AtNpc(int type)
            {
                var npc = new NPC();
                npc.SetDefaults(type);
                npc.whoAmI = 199;
                npc.active = true;
                npc.Center = body.Center + new Vector2(30f, 0f);
                Main.npc[199] = npc;
                return npc;
            }
            string NpcRef(NPC npc)
            {
                var observed = Query("observe_npcs", ("filter", npc.FullName));
                Require(observed.State == "succeeded" && !String.IsNullOrEmpty(observed.Value),
                    "actor observes one actual service NPC: " + observed.Detail);
                return observed.Value;
            }
            var faults = new Harmony("JMR.native.service.fixture.fault");
            var faultMethod = host.GetType().GetMethod("CloseFaultedBody", BindingFlags.NonPublic | BindingFlags.Instance);
            faults.Patch(faultMethod, prefix: new HarmonyMethod(typeof(NativeCompanionServiceTaskChecks)
                .GetMethod(nameof(TraceFixtureBodyFault), BindingFlags.NonPublic | BindingFlags.Static)));
            var nativeServiceMethods = new[] { "UseTaskFurniture", "ReadTaskHousing", "TriggerTaskEvent" }.Select(name =>
                host.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)).ToArray();
            foreach (var method in nativeServiceMethods) faults.Patch(method, finalizer: new HarmonyMethod(typeof(NativeCompanionServiceTaskChecks)
                .GetMethod(nameof(TraceFixtureNativeService), BindingFlags.NonPublic | BindingFlags.Static)));
            try
            {
                if (NPCInteractions.All.Count == 0) NPCInteractions.Initialize();
                Main.sittingManager = new AnchoredEntitiesCollection();
                for (int i = 0; i < 58; i++) body.inventory[i] = new Item();
                // The regular client has a native lighting engine. This CPU fixture lets
                // inventory settle without changing the currently held item to a torch.
                body.inventory[body.selectedItem] = new Item(ItemID.Wood);
                body.inventory[52] = new Item(ItemID.GoldCoin) { stack = 20 };
                long humanCopper = Copper(human);
                long copper = Copper(body);
                var vendor = AtNpc(NPCID.Merchant);
                var refused = Step("buy_item", ("item_type", ItemID.Torch), ("amount", 3), ("budget_copper", 0), ("npc", NpcRef(vendor)));
                Require(refused.State == "failed" && Copper(body) == copper && Amount(body, ItemID.Torch) == 0,
                    "insufficient explicit budget cannot buy or charge: " + refused.Detail);
                var purchase = Step("buy_item", ("item_type", ItemID.Torch), ("amount", 3), ("budget_copper", 9999), ("npc", NpcRef(vendor)));
                Require(purchase.State == "succeeded" && Amount(body, ItemID.Torch) == 3 && Copper(body) < copper &&
                    Copper(human) == humanCopper, "native shop inventory and wallet complete exactly three purchases: " + purchase.Detail);

                copper = Copper(body);
                var sale = Step("sell_item", ("item_type", ItemID.Torch), ("amount", 2), ("npc", NpcRef(vendor)));
                Require(sale.State == "succeeded" && Amount(body, ItemID.Torch) == 1 && Copper(body) > copper &&
                    Copper(human) == humanCopper, "native sale pays the actual actor for exactly two torches: " + sale.Detail);

                var goblin = AtNpc(NPCID.GoblinTinkerer);
                body.inventory[21] = new Item(ItemID.CopperBroadsword);
                copper = Copper(body);
                var reforge = Step("reforge_item", ("item_type", ItemID.CopperBroadsword), ("slot", 21),
                    ("budget_copper", 15000), ("npc", NpcRef(goblin)));
                Require(reforge.State == "succeeded" && body.inventory[21].prefix != 0 && Copper(body) < copper &&
                    Copper(human) == humanCopper, "actor pays actual native roll price and item gets real prefix: " + reforge.Detail);

                var angler = AtNpc(NPCID.Angler);
                Main.anglerQuest = 10;
                Main.anglerQuestFinished = true; // The human already turned in today.
                Main.anglerWhoFinishedToday = new List<string> { human.name };
                body.name = "verified-service-companion";
                int fishType = Main.anglerQuestItemNetIDs[Main.anglerQuest];
                body.anglerQuestsFinished = 4;
                var missing = Step("turn_in_angler", ("npc", NpcRef(angler)));
                Require(missing.State == "failed" && body.anglerQuestsFinished == 4,
                    "absence of actor's actual fish rejects submission: " + missing.Detail);
                body.inventory[22] = new Item(fishType) { stack = 2 };
                var turn = Step("turn_in_angler", ("npc", NpcRef(angler)));
                Require(turn.State == "succeeded" && body.anglerQuestsFinished == 5 && Amount(body, fishType) == 1 &&
                    Main.anglerWhoFinishedToday.Contains(body.name) && human.anglerQuestsFinished == oldHumanCount && human.name == oldHumanName &&
                    ReferenceEquals(cursor, Main.mouseItem) && Main.npcChatText == text && Main.anglerQuestFinished,
                    "human already done does not block actor; actor receives native reward with no human UI/state change: " + turn.Detail);
                Require(body.inventory.Any(item => item.type == ItemID.FuzzyCarrot), "native fifth-quest milestone puts the reward in the actor's bag");
                var replay = Step("turn_in_angler", ("npc", NpcRef(angler)));
                Require(replay.State == "failed" && body.anglerQuestsFinished == 5 && Amount(body, fishType) == 1,
                    "native per-name day rule prevents a second fish debit: " + replay.Detail);
                Console.WriteLine("PASS original NPC services: real shop stock, per-actor expenses/payout, bounded reforge, angler milestone reward and unavailable/replay controls.");

                // Furniture lookup expects the object metadata the regular game initialized before entering a world.
                if (!(bool)typeof(TileObjectData).GetField("readOnlyData", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null))
                    TileObjectData.Initialize();
                var chairBottom = new Tile { type = TileID.Chairs, frameY = 18, frameX = 18 };
                chairBottom.active(true);
                var chairTop = new Tile { type = TileID.Chairs, frameX = 18 };
                chairTop.active(true);
                Main.tile[chairPoint.X, chairPoint.Y] = chairBottom;
                Main.tile[chairPoint.X, chairPoint.Y - 1] = chairTop;
                var chair = Query("observe_tiles", ("radius", 2), ("tile_type", (int)TileID.Chairs));
                var selection = chair.Detail.Split('；').FirstOrDefault(line => line.StartsWith("(" + chairPoint.X + "," +
                    chairPoint.Y + ")", StringComparison.Ordinal))?.Split(' ').Last();
                Require(!String.IsNullOrEmpty(selection), "actor observes the native chair tile: " + chair.Detail);
                var sit = Step("use_furniture", ("action", "sit"), ("target", selection));
                Require(sit.State == "succeeded" && body.sitting.isSitting && !human.sitting.isSitting,
                    "native seat anchor confirms this body and no human sitting: " + sit.Detail +
                    " actor/human=" + body.sitting.isSitting + "/" + human.sitting.isSitting + " feet=" + body.Bottom +
                    " direction=" + body.direction + " controls=" + String.Join(",", body.controlLeft, body.controlRight,
                        body.controlUp, body.controlDown, body.controlJump) + " anchors=" + Main.sittingManager.AnchoredPlayersAmount);
                Require(Step("use_furniture", ("action", "stand")).State == "succeeded" && !body.sitting.isSitting,
                    "native stand retires the body seat");
                var housing = Query("housing_status", ("target", selection));
                Require(housing.State == "succeeded" && housing.Value == "false" && ReferenceEquals(WorldGen.roomTiles, roomTiles),
                    "unwalled area cannot pass native housing and borrows no persistent global scratch: " + housing.Detail);

                Main.dayTime = false;
                Main.time = 1000;
                body.inventory[24] = new Item(ItemID.SuspiciousLookingEye);
                var summon = Step("trigger_event", ("item_type", ItemID.SuspiciousLookingEye), ("slot", 24));
                Require(summon.State == "succeeded" && body.inventory[24].IsAir && NPC.AnyNPCs(NPCID.EyeofCthulhu),
                    "real item use consumes one eye and sees original boss spawn: " + summon.Detail);
                Console.WriteLine("PASS original world services: observed native chair, housing negative and one actual night summon.");
            }
            finally
            {
                faults.Unpatch(faultMethod, HarmonyPatchType.All, faults.Id);
                foreach (var method in nativeServiceMethods) faults.Unpatch(method, HarmonyPatchType.All, faults.Id);
                Array.Copy(oldInventory, body.inventory, oldInventory.Length);
                Array.Copy(originalNpcs, Main.npc, originalNpcs.Length); Main.anglerQuest = oldQuest;
                Main.tile[chairPoint.X, chairPoint.Y] = originalChairBottom;
                Main.tile[chairPoint.X, chairPoint.Y - 1] = originalChairTop;
                Main.sittingManager = previousSeats;
                Main.dayTime = oldDay; Main.time = oldTime;
                Main.anglerQuestFinished = oldFinished; Main.anglerWhoFinishedToday = oldNames;
                Main.mouseItem = cursor; Main.npcChatText = text;
                body.anglerQuestsFinished = oldCount; body.name = oldBodyName;
            }
        }

        private static int Amount(Player player, int itemType) => player.inventory.Where(item => item.type == itemType).Sum(item => item.stack);
        private static long Copper(Player player) => Amount(player, ItemID.CopperCoin) +
            100L * Amount(player, ItemID.SilverCoin) + 10000L * Amount(player, ItemID.GoldCoin) +
            1000000L * Amount(player, ItemID.PlatinumCoin);
    }
}
