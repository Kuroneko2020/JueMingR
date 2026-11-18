using System;
using System.Threading;
using JueMingR.Features.Recovery;
using JueMingR.Platform.Companions;
using JueMingR.TerrariaHost.Recovery;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed class CompanionSupplyState
    {
        internal uint LifeFrame = uint.MaxValue, ManaFrame = uint.MaxValue, NurseScan;
        internal uint NextBuffScan, NextGrowthScan, NextNurseAttempt, NextLifeScan, NextManaScan;
        internal CompanionBehavior LastPreferences;
        internal RecoverySource Quick, Growth, PlannedBuff;
        internal CompanionSupplyKind QuickKind;
        internal int GrowthValue, QuickBuffType, LifeDeficit, ManaCost;
        internal Item ManaWeapon;
        internal bool QuickObserved, QuickBuffTried, NativeMana;
        internal readonly ulong[] Unconfirmed = new ulong[5];
        internal readonly ulong[] PaymentSlots = new ulong[5];
        internal NPC Nurse;
        internal int NurseSlot = -1;
        internal byte NurseGeneration;
        internal Microsoft.Xna.Framework.Rectangle NurseRegion;
        internal readonly SupplyFailure[] Refusals = new SupplyFailure[4];
#if DEBUG
        internal long SourceReads, GrowthReads, NurseReads, WalletPlans;
#endif
    }

    internal struct SupplyFailure
    {
        internal RecoverySource Source;
        internal uint Until;
    }

    internal sealed partial class HostCompanions
    {
        private static readonly int[] SupplyNetworkBases =
        {
            PlayerItemSlotID.Inventory0, PlayerItemSlotID.Bank1_0, PlayerItemSlotID.Bank2_0,
            PlayerItemSlotID.Bank3_0, PlayerItemSlotID.Bank4_0
        };

        private bool SupplyActorReady(Actor actor)
        {
            Player body = actor?.Body;
            return Thread.CurrentThread.ManagedThreadId == itemThread &&
                body != null && actor.Ready && !actor.Closing && !actor.Faulted &&
                body.active && !body.dead && ReferenceEquals(Owned(body), actor) &&
                profiles.OwnsActivation(actor.Profile.Id, actor.Generation) &&
                ReferenceEquals(world, Main.ActiveWorldFileData) &&
                ReferenceEquals(owner, Main.ActivePlayerFileData?.Player);
        }

        private static void RefreshSupplyPreferences(Actor actor)
        {
            var state = actor.Supplies;
            if (ReferenceEquals(state.LastPreferences, actor.Preferences)) return;
            state.LastPreferences = actor.Preferences;
            state.NextBuffScan = state.NextGrowthScan = state.NurseScan = state.NextNurseAttempt =
                state.NextLifeScan = state.NextManaScan = 0;
            state.PlannedBuff = default(RecoverySource);
            Array.Clear(state.Refusals, 0, state.Refusals.Length);
        }

        private bool SupplySlotBlocked(Player body, Item[] array, int slot)
        {
            var actor = Owned(body);
            if (actor == null || array == null || slot < 0 || slot >= 58) return false;
            var state = actor.Supplies;
            if (ReferenceEquals(state.Quick.Items, array) && state.Quick.Slot == slot ||
                ReferenceEquals(state.Growth.Items, array) && state.Growth.Slot == slot) return true;
            for (int account = 0; account < 5; account++)
                if (ReferenceEquals(RecoverySource.AccountItems(body, account), array))
                    return ((state.Unconfirmed[account] | state.PaymentSlots[account]) & (1UL << slot)) != 0;
            return false;
        }

        private bool SupplyAdmitted(Actor actor, Item[] array, int account, int slot, bool ownsSelected = false)
        {
            var body = actor.Body;
            if (SupplySlotBlocked(body, array, slot) || itemDraft?.DependsOn(array, slot) == true ||
                ReferenceEquals(array[slot], Main.mouseItem)) return false;
            if (account != 0) return true;
            return !body.inventoryChestStack[slot] && (ownsSelected ||
                !CompanionItemRules.Blocked(body, CompanionInventoryArea.Inventory, slot, null) &&
                !CompanionProjectileMaterial(body, array[slot]));
        }

        private static RecoverySource SupplySource(Actor actor, Item[] array, int account, int slot)
        {
            Item item = array[slot];
            return new RecoverySource
            {
                Player = actor.Body, Items = array, Item = item, Account = account, Slot = slot,
                Type = item.type, Stack = item.stack, Session = actor.Generation
            };
        }

        private static bool SourceCurrent(Actor actor, RecoverySource source)
        {
            return source.Found && actor.Generation == source.Session && ReferenceEquals(actor.Body, source.Player) &&
                ReferenceEquals(source.Items, RecoverySource.AccountItems(source.Player, source.Account)) &&
                ReferenceEquals(source.Item, source.Items[source.Slot]) &&
                source.Item.type == source.Type && source.Item.stack == source.Stack &&
                (source.Account != 4 || source.Player.useVoidBag());
        }

        private static bool WasRefused(Actor actor, CompanionSupplyKind kind, Item item)
        {
            var refusal = actor.Supplies.Refusals[(int)kind];
            return Main.GameUpdateCount < refusal.Until && ReferenceEquals(refusal.Source.Item, item) &&
                refusal.Source.Type == item.type && refusal.Source.Stack == item.stack;
        }

        internal void TryCompanionLife(Actor actor)
        {
            if (!actor.Preferences.HealthRecoveryEnabled || !SupplyActorReady(actor)) return;
            RefreshSupplyPreferences(actor);
            if (actor.Preferences.AllowNurse)
            {
                var outcome = TryCompanionNurse(actor);
                if (outcome == CompanionNurseOutcome.Treated || outcome == CompanionNurseOutcome.Unconfirmed) return;
            }
            var body = actor.Body;
            int deficit = body.statLifeMax2 - body.statLife;
            if (body.cursed || body.CCed || deficit <= 0 || body.potionDelay > 0 ||
                actor.Supplies.LifeFrame == Main.GameUpdateCount) return;
            var state = actor.Supplies;
            if (deficit == state.LifeDeficit && Main.GameUpdateCount < state.NextLifeScan) return;
            state.LifeDeficit = deficit;
            state.NextLifeScan = Main.GameUpdateCount + 12;
            var source = ChooseQuick(actor, CompanionSupplyKind.Life, deficit);
            UseQuick(actor, CompanionSupplyKind.Life, source);
        }

        internal void TryCompanionMana(Actor actor, Item weapon)
        {
            if (!actor.Preferences.ManaRecoveryEnabled || !SupplyActorReady(actor)) return;
            RefreshSupplyPreferences(actor);
            var body = actor.Body;
            if (body.cursed || body.CCed || body.manaPotionDelay > 0 || body.statMana >= body.statManaMax2 ||
                actor.Supplies.ManaFrame == Main.GameUpdateCount) return;
            int cost = PotionRecovery.ManaCost(body, weapon);
            if (cost <= body.statMana || cost > body.statManaMax2) return;
            var state = actor.Supplies;
            if (ReferenceEquals(state.ManaWeapon, weapon) && cost == state.ManaCost && Main.GameUpdateCount < state.NextManaScan) return;
            state.ManaWeapon = weapon;
            state.ManaCost = cost;
            state.NextManaScan = Main.GameUpdateCount + 8;
            UseQuick(actor, CompanionSupplyKind.Mana, ChooseQuick(actor, CompanionSupplyKind.Mana));
        }

        internal bool CompanionBuffPending(Actor actor)
        {
            if (!actor.Preferences.ConsumablesEnabled || actor.Preferences.AllowedBuffTypes.Length == 0 ||
                !SupplyActorReady(actor)) return false;
            RefreshSupplyPreferences(actor);
            var body = actor.Body;
            if (body.cursed || body.CCed || body.spectating >= 0) return false;
            var state = actor.Supplies;
            var source = state.PlannedBuff;
            if (SourceCurrent(actor, source) && BuffRecovery.Missing(body, source.Item.buffType) &&
                SupplyAdmitted(actor, source.Items, source.Account, source.Slot)) return true;
            state.PlannedBuff = default(RecoverySource);
            if (Main.GameUpdateCount < state.NextBuffScan) return false;
            state.NextBuffScan = Main.GameUpdateCount + 30;
            if (body.CountBuffs() >= Player.maxBuffs) return false;
            state.PlannedBuff = ChooseQuick(actor, CompanionSupplyKind.Buff);
            return state.PlannedBuff.Found;
        }

        internal void TryCompanionBuff(Actor actor)
        {
            if (!CompanionBuffPending(actor)) return;
            var source = actor.Supplies.PlannedBuff;
            actor.Supplies.PlannedBuff = default(RecoverySource);
            UseQuick(actor, CompanionSupplyKind.Buff, source);
        }

        internal RecoverySource ChooseQuick(Actor actor, CompanionSupplyKind kind, int deficit = 0, bool nativeMana = false)
        {
            var body = actor.Body;
            var preferences = actor.Preferences;
            int mode = (int)preferences.HealthRecoveryMode;
            int amount = 0;
            RecoverySource best = default(RecoverySource);
            bool voidBag = body.useVoidBag();
            for (int account = 0; account <= (voidBag ? 4 : 0); account += 4)
            {
                Item[] items = RecoverySource.AccountItems(body, account);
                int count = account == 0 ? 58 : body.bank4.maxItems;
                for (int slot = 0; slot < count; slot++)
                {
#if DEBUG
                    actor.Supplies.SourceReads++;
#endif
                    Item item = items[slot];
                    if (!CompanionSupplyCatalog.Candidate(kind, item) || !SupplyAdmitted(actor, items, account, slot) ||
                        !nativeMana && WasRefused(actor, kind, item)) continue;
                    switch (kind)
                    {
                        case CompanionSupplyKind.Life:
                            int heal = PotionRecovery.Healing(item);
                            if (Array.IndexOf(preferences.ExcludedHealingTypes, item.type) >= 0 ||
                                RecoveryRules.LifeScore(mode, deficit, body.statLifeMax2, heal) < 0 ||
                                best.Found && !RecoveryRules.BetterLife(mode, deficit, body.statLifeMax2, heal, amount)) continue;
                            amount = heal;
                            break;
                        case CompanionSupplyKind.Mana:
                            if (body.potionDelay > 0 && item.potion) continue;
                            if (nativeMana) return SupplySource(actor, items, account, slot);
                            if (Array.IndexOf(preferences.ExcludedManaTypes, item.type) >= 0 || item.healMana <= amount) continue;
                            amount = item.healMana;
                            break;
                        case CompanionSupplyKind.Buff:
                            if (Array.IndexOf(preferences.AllowedBuffTypes, item.type) >= 0 && BuffRecovery.Missing(body, item.buffType))
                                return SupplySource(actor, items, account, slot);
                            continue;
                    }
                    best = SupplySource(actor, items, account, slot);
                }
            }
            return best;
        }

        private void UseQuick(Actor actor, CompanionSupplyKind kind, RecoverySource source)
        {
            var state = actor.Supplies;
            if (!source.Found || state.Quick.Found || !SourceCurrent(actor, source)) return;
            state.Quick = source;
            state.QuickKind = kind;
            state.QuickObserved = false;
            state.QuickBuffTried = false;
            state.QuickBuffType = source.Item.buffType;
            bool returned = false;
            try
            {
                using (CompanionHooks.Enter(actor, actor.Aim))
                {
                    switch (kind)
                    {
                        case CompanionSupplyKind.Life: actor.Body.QuickHeal(); break;
                        case CompanionSupplyKind.Mana: actor.Body.QuickMana(); break;
                        case CompanionSupplyKind.Buff: actor.Body.QuickBuff(); break;
                    }
                }
                returned = true;
            }
            catch (Exception) { Feedback = "队友使用补给的结果未确认。"; }
            finally { FinishQuick(actor, returned); }
        }

        internal static Item QuickSupply(Actor actor, CompanionSupplyKind kind)
        {
            var state = actor.Supplies;
            return state.QuickKind == kind && SourceCurrent(actor, state.Quick) ? state.Quick.Item : null;
        }

        internal void FinishQuick(Actor actor, bool returned)
        {
            var state = actor.Supplies;
            var source = state.Quick;
            if (!source.Found) return;
            var kind = state.QuickKind;
            state.Quick = default(RecoverySource);
            var after = source.Items[source.Slot];
            bool changed = !ReferenceEquals(after, source.Item) || after.type != source.Type || after.stack != source.Stack;
            if (kind == CompanionSupplyKind.Buff) state.QuickObserved = BuffRecovery.HasEffect(actor.Body, state.QuickBuffType);
            if (changed || state.QuickObserved) RecordSupplyChange(actor, source, changed);
            if (!returned || changed && !state.QuickObserved)
                state.Unconfirmed[source.Account] |= 1UL << source.Slot;
            else if (!state.QuickObserved)
                state.Refusals[(int)kind] = new SupplyFailure { Source = source, Until = Main.GameUpdateCount + 120 };
        }

        internal int CompanionGrowthSlot(Actor actor)
        {
            if (!actor.Preferences.ConsumablesEnabled || actor.Preferences.AllowedGrowthTypes.Length == 0 ||
                !SupplyActorReady(actor) || actor.Body.noItems || actor.Body.CCed || actor.Body.cursed) return -1;
            RefreshSupplyPreferences(actor);
            var state = actor.Supplies;
            if (Main.GameUpdateCount < state.NextGrowthScan) return -1;
            Item[] items = actor.Body.inventory;
            for (int slot = 0; slot < 50; slot++)
            {
#if DEBUG
                state.GrowthReads++;
#endif
                Item item = items[slot];
                if (item == null || item.IsAir || Array.IndexOf(actor.Preferences.AllowedGrowthTypes, item.type) < 0 ||
                    !CompanionSupplyCatalog.GrowthEligible(actor.Body, item.type) ||
                    WasRefused(actor, CompanionSupplyKind.Growth, item) ||
                    !SupplyAdmitted(actor, items, 0, slot, ownsSelected: slot == actor.Body.selectedItem)) continue;
                return slot;
            }
            state.NextGrowthScan = Main.GameUpdateCount + 15;
            return -1;
        }

        internal void BeginCompanionGrowth(Actor actor, int slot)
        {
            var state = actor.Supplies;
            if (state.Growth.Found || slot < 0 || slot >= 50) return;
            var source = SupplySource(actor, actor.Body.inventory, 0, slot);
            if (!SourceCurrent(actor, source) || !CompanionSupplyCatalog.GrowthEligible(actor.Body, source.Type) ||
                !SupplyAdmitted(actor, source.Items, 0, slot, ownsSelected: true))
            {
                actor.Body.controlUseItem = false;
                return;
            }
            state.Growth = source;
            state.GrowthValue = GrowthValue(actor.Body, source.Type);
        }

        internal void ValidateCompanionGrowth(Actor actor)
        {
            var source = actor.Supplies.Growth;
            if (!source.Found || SourceCurrent(actor, source) && SupplyActorReady(actor) &&
                actor.Body.selectedItem == source.Slot && actor.Preferences.ConsumablesEnabled &&
                Array.IndexOf(actor.Preferences.AllowedGrowthTypes, source.Type) >= 0 &&
                CompanionSupplyCatalog.GrowthEligible(actor.Body, source.Type)) return;
            actor.Body.controlUseItem = false;
            actor.Supplies.Growth = default(RecoverySource);
        }

        private static int GrowthValue(Player body, int type)
        {
            if (type == ItemID.ManaCrystal) return body.statManaMax;
            if (type == ItemID.LifeCrystal || type == ItemID.LifeFruit) return body.statLifeMax;
            return CompanionSupplyCatalog.GrowthComplete(body, type) ? 1 : 0;
        }

        internal void EndCompanionGrowth(Actor actor, bool returned = true)
        {
            var state = actor.Supplies;
            var source = state.Growth;
            if (!source.Found) return;
            state.Growth = default(RecoverySource);
            bool effect = GrowthValue(actor.Body, source.Type) != state.GrowthValue;
            var after = source.Items[source.Slot];
            bool debit = after.type != source.Type || after.stack < source.Stack;
            if (debit || effect) RecordSupplyChange(actor, source, debit);
            if (!returned || debit != effect)
                state.Unconfirmed[0] |= 1UL << source.Slot;
            else if (!effect)
                state.Refusals[(int)CompanionSupplyKind.Growth] = new SupplyFailure { Source = source, Until = Main.GameUpdateCount + 60 };
        }

        private void RecordSupplyChange(Actor actor, RecoverySource source, bool itemsChanged)
        {
            actor.Dirty = true;
            if (itemsChanged && Main.netMode == 1)
                QueueItemSync(actor.Body, actor, SupplyNetworkBases[source.Account] + source.Slot);
        }
    }
}
