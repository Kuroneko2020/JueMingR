using System;
using System.Collections.Generic;
using JueMingR.Platform.Companions;
using Terraria;
using Terraria.ID;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        private struct ObservedRightClickSlot
        {
            internal CompanionInventoryArea Area;
            internal Item[] Array;
            internal int Slot, Type, Stack;
            internal byte Prefix;
            internal bool Favorite;
            internal Item Original;

            internal bool Changed
            {
                get
                {
                    var result = Array[Slot];
                    return !ReferenceEquals(Original, result) || result.type != Type || result.stack != Stack ||
                        result.prefix != Prefix || result.favorited != Favorite;
                }
            }
        }

        // These actions run once on the actual body. In particular, opening a
        // bag can consume a key, grant random loot and drop inventory overflow.
        // Never preview/replay it to discover which slots changed.
        private bool RunNativeRightClick(Actor actor, bool human, CompanionInventoryArea area, int slot, CompanionSlotRightClick.Kind action)
        {
            if (itemDraft != null) return Result(false, "请先放下手中的物品。");
            var body = human ? owner : actor.Body;
            var source = CompanionResources.Items(body, area);
            var item = source[slot];
            var watched = new List<ObservedRightClickSlot>(action == CompanionSlotRightClick.Kind.Open ? 98 : 12);
            bool Watch(CompanionInventoryArea account, int index)
            {
                var array = CompanionResources.Items(body, account);
                var before = array[index];
                if (ItemSlotBlocked(human, body, account, index)) return false;
                watched.Add(new ObservedRightClickSlot { Area = account, Array = array, Slot = index, Original = before,
                    Type = before.type, Stack = before.stack, Prefix = before.prefix, Favorite = before.favorited });
                return true;
            }
            if (!Watch(area, slot)) return Result(false, "该物品正在使用或等待同步。");
            if (action == CompanionSlotRightClick.Kind.Open)
            {
                bool paysBankKey = item.type == ItemID.LockBox && body.useVoidBag() && !body.HasItem(ItemID.GoldenKey);
                // GetItem chooses actual stack/coin/ammo/void destinations only
                // after rolling. Full non-coin stacks cannot receive loot.
                bool WatchLoot(CompanionInventoryArea account, int count, bool receivesLoot)
                {
                    var items = CompanionResources.Items(body, account);
                    for (int i = 0; i < count; i++)
                    {
                        if (account == area && i == slot) continue;
                        var value = items[i];
                        bool key = item.type == ItemID.LockBox && value.type == ItemID.GoldenKey;
                        if ((key || receivesLoot && (value.IsAir || value.stack < value.maxStack || value.IsACoin)) && !Watch(account, i))
                            return false;
                    }
                    return true;
                }
                if (!WatchLoot(CompanionInventoryArea.Inventory, 58, true) ||
                    (body.IsVoidVaultEnabled || paysBankKey) && !WatchLoot(CompanionInventoryArea.VoidVault, body.bank4.item.Length, body.IsVoidVaultEnabled))
                    return Result(false, "背包中有物品正在使用或等待同步，请稍后再打开。");
            }
            else if (action == CompanionSlotRightClick.Kind.Vanity)
            {
                if (!CompanionItemRules.Available(body, area, slot - 10) || !Watch(area, slot - 10))
                    return Result(false, "对应装备格当前不可交换。");
            }
            else if (!WatchEquipTarget(body, item, actor, human, Watch))
                return Result(false, "目标装备格当前不可用。");

            int context = CompanionItemRules.SlotContext(area, slot);
            int split = Main.stackSplit, equipPage = Main.EquipPageSelected;
            bool released = Main.mouseRightRelease;
            bool borrowDyeCounter = !human && action == CompanionSlotRightClick.Kind.Equip && item.dye > 0;
            int humanDyeCounter = 0;
            bool borrowedDyeCounter = false, failed = false;
            try
            {
                using (human ? null : CompanionHooks.EnterWithPersonalItems(actor))
                {
                    if (borrowDyeCounter)
                    {
                        humanDyeCounter = (int)CompanionSlotRightClick.DyeCounter.GetValue(null);
                        CompanionSlotRightClick.DyeCounter.SetValue(null, actor.DyeSwapCounter);
                        borrowedDyeCounter = true;
                    }
                    if (action == CompanionSlotRightClick.Kind.Open) CompanionSlotRightClick.Open(source, context, slot, body);
                    else if (action == CompanionSlotRightClick.Kind.Vanity) CompanionSlotRightClick.SwapVanity(source, context, slot, body);
                    else ItemSlot.SwapEquip(source, context, slot);
                }
            }
            catch (Exception)
            {
                // TryOpenContainer starts a process-wide pickup logger before
                // granting loot. An interrupted native grant never reaches Stop.
                if (action == CompanionSlotRightClick.Kind.Open) Player.GetItemLogger.Stop();
                failed = true;
            }
            finally
            {
                Main.stackSplit = split;
                Main.mouseRightRelease = released;
                Main.EquipPageSelected = equipPage;
                if (borrowedDyeCounter)
                {
                    actor.DyeSwapCounter = (int)CompanionSlotRightClick.DyeCounter.GetValue(null);
                    CompanionSlotRightClick.DyeCounter.SetValue(null, humanDyeCounter);
                }
            }
            var changes = new List<CompanionItemChange>();
            foreach (var cell in watched)
                if (cell.Changed) changes.Add(new CompanionItemChange { Owner = human, Area = cell.Area, Slot = cell.Slot,
                    Array = cell.Array, Before = cell.Original, After = cell.Array[cell.Slot] });
            if (changes.Count == 0) return Result(false, failed ? "原版物品操作未完成。" :
                action == CompanionSlotRightClick.Kind.Open ? "当前无法打开这个物品，请检查所需钥匙。" : "当前无法交换这件装备。");
            ItemGestureRevision++;
            return FinishItemCommit(actor, changes, failed ? "原版操作中断，已保留实际发生的物品变化。" : "", nativeEquip: true);
        }

        private static bool WatchEquipTarget(Player body, Item item, Actor actor, bool human, Func<CompanionInventoryArea, int, bool> watch)
        {
            if (item.dye > 0)
            {
                int target = human ? (int)CompanionSlotRightClick.DyeCounter.GetValue(null) : actor.DyeSwapCounter;
                // Follow DyeSwap's first free unlocked slot, otherwise its
                // per-inventory rotating replacement. Other dyes are untouched.
                while (!body.IsItemSlotUnlockedAndUsable(target)) target = (target + 1) % 10;
                for (int i = 0; i < 10; i++)
                    if (body.IsItemSlotUnlockedAndUsable(i) && body.dye[i].IsAir) { target = i; break; }
                return watch(CompanionInventoryArea.Dye, target);
            }
            if (Main.projHook[item.shoot]) return watch(CompanionInventoryArea.Misc, 4);
            if (item.mountType != -1) return watch(CompanionInventoryArea.Misc, MountID.Sets.Cart[item.mountType] ? 2 : 3);
            if (item.buffType > 0 && Main.lightPet[item.buffType]) return watch(CompanionInventoryArea.Misc, 1);
            if (item.buffType > 0 && Main.vanityPet[item.buffType]) return watch(CompanionInventoryArea.Misc, 0);
            int offset = item.vanity ? 10 : 0;
            int armor = item.headSlot >= 0 ? 0 : item.bodySlot >= 0 ? 1 : item.legSlot >= 0 ? 2 : -1;
            if (armor >= 0) return watch(CompanionInventoryArea.Armor, offset + armor);
            var accessories = item.vanity ? ItemSlot.VanityAccSlots(body) : ItemSlot.AccSlots(body);
            if (!ItemSlot.HasIncompatibleAccessory(item, item.vanity, accessories, out int targetSlot))
            {
                targetSlot = accessories.Offset;
                for (int i = accessories.Offset; i < accessories.Offset + accessories.Count; i++)
                    if (body.IsItemSlotUnlockedAndUsable(i) && body.armor[i].IsAir) { targetSlot = i; break; }
            }
            return body.IsItemSlotUnlockedAndUsable(targetSlot) && watch(CompanionInventoryArea.Armor, targetSlot);
        }
    }
}
