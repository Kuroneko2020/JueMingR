using System;
using System.Collections.Generic;
using JueMingR.TerrariaHost.Fishing;
using Terraria;
using Terraria.ID;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal sealed class CompanionFishingGear
        {
            internal readonly List<FishingGearLoan> Loans = new List<FishingGearLoan>();
            internal int Strategy, OriginalLoadout = -1, AppliedLoadout = -1, ManualLoadout = -1, ProtectedSlots;
            internal int Liquid = -2;
            internal uint NextPlan;
            internal bool Active => Strategy != 0 || Loans.Count != 0;
        }

        internal sealed class FishingGearLoan
        {
            internal Item[] Source;
            internal Item Borrowed, Displaced;
            internal int SourceSlot, WearSlot, Loadout;
            internal int BorrowedType, BorrowedPrefix, DisplacedType, DisplacedPrefix, DisplacedStack;
            internal bool BorrowedFavorite, DisplacedFavorite;
        }

        internal bool TrySwitchCompanionLoadout(Guid profile, int index)
        {
            if (Find(profile) == null || !actors.TryGetValue(profile, out var a) || !ItemActorCurrent(profile, a) ||
                Main.ServerSideCharacter || index < 0 || index >= a.Body.Loadouts.Length) return false;
            var gear = a.FishingLine.Gear;
            if (index == a.Body.CurrentLoadoutIndex) { gear.ManualLoadout = -1; return true; }
            gear.ManualLoadout = index;
            TryManualFishingLoadout(a);
            return true;
        }

        private bool TryManualFishingLoadout(Actor a)
        {
            var gear = a.FishingLine.Gear;
            if (gear.ManualLoadout < 0 || !FishingHandFree(a.Body) || a.Body.IsStackingItems()) return false;
            int target = gear.ManualLoadout;
            if (!RestoreFishingGear(a, switchBack: false)) return false;
            if (!SwitchFishingLoadout(a, target)) return false;
            gear.ManualLoadout = -1;
            return true;
        }

        private bool SwitchFishingLoadout(Actor a, int target, bool retiring = false)
        {
            var p = a.Body;
            if (target == p.CurrentLoadoutIndex) return true;
            var scope = CompanionHooks.CurrentActor;
            if (target < 0 || target >= p.Loadouts.Length || !retiring && (p.UsingOrReusingItem || p.CCed || p.dead)) return false;
            // Use native remote-body swapping to preserve the human's mouse,
            // recipe and client equipment snapshot. The actual native 147
            // serializes the actor's new index and accessory visibility.
            int previous = Main.myPlayer;
            if (p.whoAmI == previous)
            {
                if (scope != a || owner == null || owner.whoAmI == previous) return false;
                Main.myPlayer = owner.whoAmI;
            }
            try { p.TrySwitchingLoadout(target); }
            finally { Main.myPlayer = previous; }
            if (p.CurrentLoadoutIndex != target) return false;
            a.Dirty = true;
            a.TileReachPending = true;
            a.FishingLine.NextRodScan = 0;
            InvalidateItemSources(a);
            if (a.Connection != null)
                using (CompanionHooks.Enter(a, a.Aim))
                {
                    NetMessage.SendData(147, -1, -1, null, p.whoAmI, target);
                    PublishSlots(a, false);
                }
            return true;
        }

        private bool CanPrepareLavaFishing(Actor a)
        {
            var p = a.Body;
            var options = a.Preferences.Fishing;
            if (options.Loadout)
            {
                for (int i = 0; i < p.Loadouts.Length; i++)
                    if (FishingEquipmentPlan.LavaLoadout(p, i)) return true;
            }
            if (!options.Equipment) return false;
            var move = FishingEquipmentPlan.Next(p, true, (array, slot) => FishingGearSource(a, array, slot),
                a.FishingLine.Gear.ProtectedSlots, requireLava: true);
            return move != null;
        }

        private bool UpdateFishingGear(Actor a, bool wanted, int liquid)
        {
            var gear = a.FishingLine.Gear;
            var p = a.Body;
            var options = a.Preferences.Fishing;
            int desired = wanted && liquid >= 0 ? options.Equipment ? 2 : options.Loadout ? 1 : 0 : 0;
            if (gear.Active && (desired != gear.Strategy || liquid != gear.Liquid))
            {
                if (p.UsingOrReusingItem || !RestoreFishingGear(a)) return false;
                if (desired != 0) return false;
            }
            if (desired == 0) return !a.TileReachPending;
            if (Main.ServerSideCharacter || p.IsStackingItems() || p.UsingOrReusingItem || gear.ManualLoadout >= 0) return false;
            if (Main.GameUpdateCount < gear.NextPlan) return !a.TileReachPending;
            gear.NextPlan = Main.GameUpdateCount + 60;
            gear.Strategy = desired;
            gear.Liquid = liquid;
            bool lava = liquid == 1;
            FishingNativeBait(p, out _, out int bait);
            bool needsLava = lava && !LavaFishingRod(a.FishingLine.Rod) && !ItemID.Sets.IsLavaBait[bait];
            if (desired == 1)
            {
                int selected = p.CurrentLoadoutIndex;
                int best = selected;
                int score = FishingEquipmentPlan.Score(p, selected, lava);
                bool legal = !needsLava || FishingEquipmentPlan.LavaLoadout(p, selected);
                for (int i = 0; i < p.Loadouts.Length; i++)
                {
                    if (i == selected) continue;
                    int candidateScore = FishingEquipmentPlan.Score(p, i, lava);
                    bool candidateLegal = !needsLava || FishingEquipmentPlan.LavaLoadout(p, i);
                    if (!candidateLegal || legal && candidateScore <= score) continue;
                    score = candidateScore;
                    best = i;
                    legal = true;
                }
                if (best == selected) return !a.TileReachPending;
                if (gear.OriginalLoadout < 0) gear.OriginalLoadout = selected;
                if (SwitchFishingLoadout(a, best)) gear.AppliedLoadout = best;
                return false;
            }
            RecheckFishingLoans(p, gear);
            for (int i = 0; i < 10; i++)
            {
                int occupied = gear.ProtectedSlots;
                foreach (var loan in gear.Loans) occupied |= 1 << loan.WearSlot;
                bool requireLava = needsLava && !FishingEquipmentPlan.LavaLoadout(p, p.CurrentLoadoutIndex);
                var move = FishingEquipmentPlan.Next(p, lava, (array, slot) => FishingGearSource(a, array, slot), occupied, requireLava);
                if (move == null || !ApplyFishingGear(a, move)) break;
            }
            return !a.TileReachPending;
        }

        private bool FishingGearSource(Actor a, Item[] array, int slot)
        {
            var p = a.Body;
            foreach (var loan in a.FishingLine.Gear.Loans)
                if (ReferenceEquals(array, loan.Source) && slot == loan.SourceSlot) return false;
            if (ReferenceEquals(array, p.armor))
                return slot >= 10 && p.IsItemSlotUnlockedAndUsable(slot) && SupplyAdmitted(a, array, -1, slot);
            if (ReferenceEquals(array, p.inventory)) return slot < 50 && SupplyAdmitted(a, array, 0, slot);
            return ReferenceEquals(array, p.bank4.item) && p.useVoidBag() && SupplyAdmitted(a, array, 4, slot);
        }

        private bool ApplyFishingGear(Actor a, FishingEquipmentPlan.Move move)
        {
            var p = a.Body;
            if (!FishingGearSource(a, move.Source, move.Slot) || !SupplyAdmitted(a, p.armor, -1, move.Target) ||
                !ReferenceEquals(move.Item, move.Source[move.Slot]) ||
                !FishingEquipmentPlan.CanSwap(p, move.Source, move.Slot, move.Target, planSplit: true)) return false;
            if (move.Item.stack > 1 && !SplitFishingGear(a, move)) return false;
            if (!FishingEquipmentPlan.CanSwap(p, move.Source, move.Slot, move.Target)) return false;
            var original = p.armor[move.Target];
            var loan = new FishingGearLoan
            {
                Source = move.Source, SourceSlot = move.Slot, WearSlot = move.Target, Loadout = p.CurrentLoadoutIndex,
                Borrowed = move.Item, Displaced = original, BorrowedType = move.Item.type, BorrowedPrefix = move.Item.prefix,
                DisplacedType = original.type, DisplacedPrefix = original.prefix, DisplacedStack = original.stack,
                BorrowedFavorite = move.Item.favorited, DisplacedFavorite = original.favorited
            };
            // Physical references carry their original favorites. The player
            // profile and network both serialize these same real slot objects.
            a.FishingLine.Gear.Loans.Add(loan);
            Utils.Swap(ref move.Source[move.Slot], ref p.armor[move.Target]);
            FishingGearChanged(a, move.Source, move.Slot, move.Target);
            return true;
        }

        private bool SplitFishingGear(Actor a, FishingEquipmentPlan.Move move)
        {
            var p = a.Body;
            bool inventory = ReferenceEquals(move.Source, p.inventory);
            if (!inventory && !ReferenceEquals(move.Source, p.bank4.item)) return false;
            for (int i = 0; i < (inventory ? 50 : p.bank4.maxItems); i++)
            {
                if (!move.Source[i].IsAir || !FishingGearSource(a, move.Source, i)) continue;
                using (CompanionHooks.Enter(a, a.Aim))
                {
                    Main.mouseItem = new Item();
                    try { ItemSlot.PickupItemIntoMouse(move.Source, inventory ? 0 : 32, move.Slot, p); }
                    finally
                    {
                        // Return any real native output to the actor even if
                        // the split throws after mutating its input stack.
                        if (!Main.mouseItem.IsAir) move.Source[i] = Main.mouseItem;
                    }
                }
                int baseSlot = inventory ? PlayerItemSlotID.Inventory0 : PlayerItemSlotID.Bank4_0;
                if (!move.Source[i].IsAir)
                {
                    a.Dirty = true;
                    if (a.Connection != null)
                    {
                        QueueItemSync(p, a, baseSlot + move.Slot);
                        QueueItemSync(p, a, baseSlot + i);
                    }
                }
                if (move.Source[i].stack != 1 || move.Source[i].type != move.Item.type) return false;
                move.Slot = i;
                move.Item = move.Source[i];
                return true;
            }
            return false;
        }

        private static void RecheckFishingLoans(Player p, CompanionFishingGear gear)
        {
            for (int i = gear.Loans.Count - 1; i >= 0; i--)
            {
                var loan = gear.Loans[i];
                Item[] wear = loan.Loadout == p.CurrentLoadoutIndex ? p.armor : p.Loadouts[loan.Loadout].Armor;
                var item = wear[loan.WearSlot];
                var source = ReferenceEquals(loan.Source, p.armor) ? wear : loan.Source;
                var displaced = source[loan.SourceSlot];
                // A later concrete manual edit owns the affected item/slot.
                // A matching item type alone cannot revive this obligation.
                if (ReferenceEquals(item, loan.Borrowed) && item.type == loan.BorrowedType && item.prefix == loan.BorrowedPrefix &&
                    item.stack == 1 && item.favorited == loan.BorrowedFavorite &&
                    ReferenceEquals(displaced, loan.Displaced) && displaced.type == loan.DisplacedType &&
                    displaced.prefix == loan.DisplacedPrefix && displaced.stack == loan.DisplacedStack &&
                    displaced.favorited == loan.DisplacedFavorite) continue;
                if (loan.Loadout == p.CurrentLoadoutIndex) gear.ProtectedSlots |= 1 << loan.WearSlot;
                gear.Loans.RemoveAt(i);
            }
        }

        private bool RestoreFishingGear(Actor a, bool switchBack = true, bool retiring = false)
        {
            var p = a.Body;
            var gear = a.FishingLine.Gear;
            if (!gear.Active && gear.OriginalLoadout < 0) return true;
            RecheckFishingLoans(p, gear);
            for (int i = gear.Loans.Count - 1; i >= 0; i--)
            {
                var loan = gear.Loans[i];
                bool current = loan.Loadout == p.CurrentLoadoutIndex;
                Item[] wear = current ? p.armor : p.Loadouts[loan.Loadout].Armor;
                Item[] source = ReferenceEquals(loan.Source, p.armor) ? wear : loan.Source;
                int account = ReferenceEquals(source, p.inventory) ? 0 : ReferenceEquals(source, p.bank4.item) ? 4 : -1;
                if (!retiring && (!SupplyAdmitted(a, source, account, loan.SourceSlot, ownsSelected: true) ||
                    !SupplyAdmitted(a, wear, -1, loan.WearSlot))) return false;
                Utils.Swap(ref source[loan.SourceSlot], ref wear[loan.WearSlot]);
                gear.Loans.RemoveAt(i);
                if (current) FishingGearChanged(a, source, loan.SourceSlot, loan.WearSlot);
                else
                {
                    a.Dirty = true;
                    if (a.Connection != null)
                        using (CompanionHooks.Enter(a, a.Aim)) PublishSlots(a, false);
                }
            }
            if (switchBack && gear.OriginalLoadout >= 0 && gear.AppliedLoadout == p.CurrentLoadoutIndex &&
                !SwitchFishingLoadout(a, gear.OriginalLoadout, retiring)) return false;
            gear.OriginalLoadout = gear.AppliedLoadout = -1;
            gear.Strategy = 0;
            gear.NextPlan = 0;
            if (!switchBack || !FishingActive(a)) gear.ProtectedSlots = 0;
            return true;
        }

        private void FishingGearChanged(Actor a, Item[] source, int slot, int wear)
        {
            var p = a.Body;
            a.Dirty = true;
            a.TileReachPending = true;
            a.FishingLine.NextRodScan = 0;
            InvalidateItemSources(a);
            if (a.Connection == null) return;
            int sourceBase = ReferenceEquals(source, p.inventory) ? PlayerItemSlotID.Inventory0 :
                ReferenceEquals(source, p.bank4.item) ? PlayerItemSlotID.Bank4_0 : PlayerItemSlotID.Armor0;
            QueueItemSync(p, a, sourceBase + slot);
            QueueItemSync(p, a, PlayerItemSlotID.Armor0 + wear);
        }
    }
}
