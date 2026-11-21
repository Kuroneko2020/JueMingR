using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Platform.Companions;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeCompanionVoidPickupChecks
    {
        private const int Bag = 46, Container = 45;
        private static Player recipient;
        private static readonly Dictionary<int,int> grants = new Dictionary<int,int>();
        private static bool observing;

        // Observe the original random grant amounts, without fixing its roll or replacing GetItem.
        private static void ObserveGrant(Player __instance, Item newItem)
        {
            if (!observing || !ReferenceEquals(__instance, recipient)) return;
            Require(ReferenceEquals(Main.LocalPlayer, recipient), "native loot ran under a different body");
            grants.TryGetValue(newItem.type, out int previous);
            grants[newItem.type] = previous + newItem.stack;
        }

        private static bool Gesture(object host, Guid id, int slot, string name, bool human = false)
        {
            var gesture = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Companions.CompanionItemGesture", true);
            return (bool)Call(host, "ItemGesture", id, human, CompanionInventoryArea.Inventory, slot, Enum.Parse(gesture, name));
        }

        private static void FullInventory(Player body)
        {
            for (int i = 0; i < 58; i++)
            {
                var item = new Item(i < 50 ? ItemID.DirtBlock : i < 54 ? ItemID.PlatinumCoin : ItemID.WoodenArrow);
                item.stack = item.maxStack;
                body.inventory[i] = item;
            }
            for (int i = 0; i < body.bank4.item.Length; i++) body.bank4.item[i] = new Item();
            body.inventory[Container] = new Item(ItemID.HerbBag) { stack = 3 };
        }

        private static Player ArchiveAndJoin(object host, Guid id)
        {
            Require((bool)Call(host, "Recall", id), "recall the current body into its actual archive");
            NativeQuickItemChecks.Until(() => { Call(host, "Update", 0UL); return !(bool)Call(host, "IsJoined", id); });
            var profiles = Get(host, "profiles");
            var saved = (CompanionProfile)Call(profiles, "Find", id);
            var raw = (Player)Call(profiles, "LoadBody", saved);
            Require(raw.voidVaultInfo[7] && raw.IsVoidVaultEnabled != raw.HasItem(ItemID.VoidLens),
                "fixture restored an authentic archive with a stale pickup bit and separate retained bits");
            Require((bool)Call(host, "Join", id), "join the saved void-bag body");
            var active = (Player)Call(host, "GetBody", id);
            Require((bool)Call(host, "CanTransfer", id) && (byte)active.voidVaultInfo ==
                (raw.HasItem(ItemID.VoidLens) ? 0xA5 : 0xA4),
                "first usable actor must derive just bit 0 from its own inventory before the first native update; " +
                "raw=" + (byte)raw.voidVaultInfo + ", active=" + (byte)active.voidVaultInfo + ", openBag=" + raw.HasItem(ItemID.VoidLens));
            return active;
        }

        private static void Pickup(Player body, int type, int stack, bool accepted)
        {
            int slot = Main.maxItems - 1;
            var old = Main.item[slot];
            int before = body.bank4.item.Where(i => i.type == type).Sum(i => i.stack);
            var drop = new WorldItem(new Item(type) { stack = stack }) { whoAmI = slot, position = body.position,
                playerIndexTheItemIsReservedFor = body.whoAmI };
            try
            {
                Main.item[slot] = drop;
                var space = body.ItemSpace(drop);
                Require(space.CanTakeItem == accepted && space.ItemIsGoingToVoidVault == accepted,
                    "native ItemSpace must classify the actual pickup at a full inventory");
                Call(body, "GrabItems", body.whoAmI);
                Require(body.bank4.item.Where(i => i.type == type).Sum(i => i.stack) == before + (accepted ? stack : 0) &&
                    (accepted ? !drop.active : drop.active && drop.stack == stack),
                    "native GrabItems/PickupItem/GetItem must conserve this world's reserved stack on the correct side");
            }
            finally { Main.item[slot] = old; }
        }

        private static int Count(Item[] items, int type) { return items.Where(i => i.type == type).Sum(i => i.stack); }

        private static void Open(object host, Guid id, Player body, bool vault)
        {
            var before = body.bank4.item.GroupBy(i => i.type).ToDictionary(g => g.Key, g => g.Sum(i => i.stack));
            var world = Main.item.Take(Main.maxItems).Where(i => i.active).GroupBy(i => i.type).ToDictionary(g => g.Key, g => g.Sum(i => i.stack));
            int boxes = body.inventory[Container].stack;
            bool opened = OpenGesture(host, id);
            Require(opened && body.inventory[Container].stack == boxes - 1 && grants.Count > 0 && grants.Values.Sum() >= 4,
                "exactly one real stacked herb bag grants its native rolls; accepted=" + opened + ", boxes=" + boxes + "->" +
                body.inventory[Container].stack + ", rolls=" + grants.Values.Sum() + ", feedback=" + Get(host,"Feedback"));
            foreach (var pair in grants)
            {
                before.TryGetValue(pair.Key, out int bankBefore);
                world.TryGetValue(pair.Key, out int worldBefore);
                int worldAfter = Main.item.Take(Main.maxItems).Where(i => i.active && i.type == pair.Key).Sum(i => i.stack);
                Require(Count(body.bank4.item, pair.Key) - bankBefore == (vault ? pair.Value : 0) &&
                    worldAfter - worldBefore == (vault ? 0 : pair.Value) && Count(body.inventory, pair.Key) == 0,
                    "every rolled item lands exactly once in the actual open bank or the original closed-bag overflow, type " + pair.Key);
            }
        }

        private static bool OpenGesture(object host, Guid id)
        {
            grants.Clear(); observing = true;
            try { return Gesture(host, id, Container, "Right"); }
            finally { observing = false; }
        }

        internal static void Run(object host, Player human)
        {
            Require((bool)Call(host, "Create", "虚空拾取验例"), "create independent void-bag archive");
            Guid id = (Guid)Get(host, "LastCreatedId");
            var profiles = Get(host, "profiles");
            NativeQuickItemChecks.Until(() => { Call(profiles, "Pump"); return (bool)Call(profiles, "IsSaved", id); });
            Require((bool)Call(host, "Join", id), "join real void-bag actor");
            var body = (Player)Call(host, "GetBody", id);
            var oldHumanSlot = human.inventory[Bag];
            byte humanBits = human.voidVaultInfo;
            var humanInventory = human.inventory.Select(i => i.Clone()).ToArray();
            var humanVault = human.bank4.item.Select(i => i.Clone()).ToArray();
            var wasActive = Main.item.Take(Main.maxItems).Select(i => i.active).ToArray();
            var patch = new Harmony("JueMingR.Tests.CompanionVoidLoot");
            patch.Patch(AccessTools.Method(typeof(Player), nameof(Player.GetItem), new[] { typeof(Item), typeof(GetItemSettings) }),
                prefix: new HarmonyMethod(typeof(NativeCompanionVoidPickupChecks), nameof(ObserveGrant)));
            try
            {
                FullInventory(body);
                body.inventory[Bag] = new Item(ItemID.VoidLens);
                body.voidVaultInfo = 0xA4;
                body = ArchiveAndJoin(host, id);
                recipient = body;
                Pickup(body, ItemID.Wood, 7, true);
                body.LockNetSlot(PlayerItemSlotID.Bank4_0);
                Require(!OpenGesture(host, id) && body.inventory[Container].stack == 3 &&
                    grants.Count == 0 && Count(body.bank4.item, ItemID.Wood) == 7,
                    "locked possible bank destination rejects before native randomization or source debit");
                body.disableVoidBag = -1;
                Open(host, id, body, true);
                Require(Gesture(host, id, Bag, "Right") && body.inventory[Bag].type == ItemID.ClosedVoidBag &&
                    !body.IsVoidVaultEnabled && !body.useVoidBag(), "right-close updates passive pickup before another window action");
                Pickup(body, ItemID.StoneBlock, 11, false);
                Open(host, id, body, false);
                Require(Gesture(host, id, Bag, "Right") && body.IsVoidVaultEnabled && body.useVoidBag(), "right-reopen restores the actual body's two native predicates");
                human.inventory[Bag] = new Item();
                Require(Gesture(host,id,Bag,"Left") && Gesture(host,id,Bag,"Left",true) && human.inventory[Bag].type == ItemID.VoidLens &&
                    !body.IsVoidVaultEnabled, "moving out the only open bag immediately retires actor pickup; the owner's bag cannot grant it");
                body.inventory[Bag] = new Item(ItemID.DirtBlock) { stack = 9999 };
                Pickup(body, ItemID.Gel, 13, false);
                body.inventory[Bag] = new Item();
                Require(Gesture(host,id,Bag,"Left",true) && Gesture(host,id,Bag,"Left") && body.IsVoidVaultEnabled,
                    "moving the only bag back reinstates the actor's passive vault in the same frame");
                // A native character can pick up the open bag late in Update,
                // after the original pickup bit was sampled. Open the stacked
                // container immediately, before another Update gets a chance.
                Require(Gesture(host,id,Bag,"Left") && Gesture(host,id,Bag,"Left",true) && !body.IsVoidVaultEnabled,
                    "put the only bag in the human's handoff inventory before reserving it as a world drop");
                var groundBag = human.inventory[Bag];
                human.inventory[Bag] = oldHumanSlot;
                int worldSlot = Main.maxItems - 1;
                var oldWorld = Main.item[worldSlot];
                body.position.X += 240; // Stay clear of the previous legal closed-bag overflow.
                try
                {
                    Main.item[worldSlot] = new WorldItem(groundBag) { whoAmI = worldSlot, position = body.position,
                        playerIndexTheItemIsReservedFor = body.whoAmI };
                    Call(body, "GrabItems", body.whoAmI);
                    Require(!Main.item[worldSlot].active && body.inventory[Bag].type == ItemID.VoidLens &&
                        body.useVoidBag(), "world pickup acquired the actual bag without a management commit");
                }
                finally { Main.item[worldSlot] = oldWorld; }
                Open(host, id, body, true);
                // Direct world/load mutations bypass management commits. Only the real native body step may repair this stale value.
                body.IsVoidVaultEnabled = false;
                NativeQuickItemChecks.BeginWorldStep(); body.Update(body.whoAmI);
                Require(body.IsVoidVaultEnabled && (byte)body.voidVaultInfo == 0xA5, "native Player.Update refreshes the companion itself");
                Require(body.inventory[Container].IsAir, "the last native container left its source slot empty");
                body.inventory[Container] = new Item(ItemID.DirtBlock) { stack = 9999 };
                var excluded = new Item(ItemID.Angelfish);
                var ungifted = new Item(ItemID.Gel) { stack = 6 };
                Require(excluded.questItem && !body.ItemSpace(excluded).CanTakeItem &&
                    ReferenceEquals(body.GetItem(ungifted, GetItemSettings.GiftRecieved), ungifted) &&
                    Count(body.bank4.item, ItemID.Gel) == 0, "native quest and per-GetItemSettings exceptions still apply on a full open-bag body");
                body.inventory[Bag] = new Item(ItemID.ClosedVoidBag);
                body.voidVaultInfo = 0xA5;
                body = ArchiveAndJoin(host, id);
                Require(!body.IsVoidVaultEnabled && !body.useVoidBag(), "stale enabled archive cannot empower closed bag after rejoin");
                Require(human.voidVaultInfo == humanBits && !human.inventory.Where((v,i) => v.IsNetStateDifferent(humanInventory[i])).Any() &&
                    !human.bank4.item.Where((v,i) => v.IsNetStateDifferent(humanVault[i])).Any(), "all loot and eligibility stay off the human inventory and void vault");
                Console.WriteLine("PASS companion void pickup: native body update, stale archives on both activations, protected bank, three stacked native opens, full-inventory GrabItems and transfers conserve actual loot.");
            }
            finally
            {
                recipient = null; grants.Clear(); patch.UnpatchAll(patch.Id);
                human.inventory[Bag] = oldHumanSlot; Call(host, "CancelItemGesture"); Call(host, "Recall", id);
                for (int i = 0; i < wasActive.Length; i++) if (!wasActive[i] && Main.item[i].active) Main.item[i].TurnToAir();
            }
        }

        internal static void NetworkLoot(object host, Guid id, Player body, List<byte[]> secondary, List<byte[]> primary)
        {
            var backpack = (Item[])body.inventory.Clone();
            var bank = (Item[])body.bank4.item.Clone();
            byte bits = body.voidVaultInfo;
            try
            {
                FullInventory(body); body.inventory[Bag] = new Item(ItemID.ClosedVoidBag);
                Require(Gesture(host,id,Bag,"Right"), "activate actual chest-limited bag for secondary publication");
                secondary.Clear(); primary.Clear();
                Require(Gesture(host,id,Container,"Right") && body.inventory[Container].stack == 2, "one native container supplies only its own multiplayer recipient");
                var received = Enumerable.Range(0, bank.Length).Where(i => !body.bank4.item[i].IsAir).ToArray();
                Require(received.Length > 0 && received.All(i => secondary.Any(p => p[2] == 5 && p[3] == body.whoAmI &&
                    BitConverter.ToInt16(p,4) == PlayerItemSlotID.Bank4_0+i && BitConverter.ToInt16(p,6) == body.bank4.item[i].stack &&
                    BitConverter.ToInt16(p,9) == body.bank4.item[i].type)) && secondary.Any(p => p[2] == 5 && p[3] == body.whoAmI &&
                    BitConverter.ToInt16(p,4) == PlayerItemSlotID.Inventory0+Container && BitConverter.ToInt16(p,6) == 2) &&
                    primary.All(p => p[2] != 5), "bank4 grant and source debit reach their exact native slot 5 frames on the companion route only");
                Console.WriteLine("PASS companion void publication: every occupied bank4 result and the stacked source debit match the companion's own native slot frames.");
            }
            finally { Array.Copy(backpack, body.inventory, backpack.Length); Array.Copy(bank,body.bank4.item,bank.Length); body.voidVaultInfo = bits; }
        }
    }
}
