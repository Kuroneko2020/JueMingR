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
        internal RecoverySource Quick, HeldSource, PlannedBuff;
        internal HostCompanions.ItemOnce Once, HeldOnce, ChatFinishingOnce;
        internal int RestoreSelection = -1;
        internal CompanionSupplyKind QuickKind;
        internal int GrowthValue, QuickBuffType, LifeDeficit, ManaCost;
        internal Item ManaWeapon;
        internal bool QuickObserved, QuickBuffTried, NativeMana;
        internal bool ManualQuick;
        internal readonly ulong[] Unconfirmed = new ulong[5];
        internal readonly ulong[] PaymentSlots = new ulong[5];
        internal NPC Nurse;
        internal int NurseSlot = -1;
        internal byte NurseGeneration;
        internal Microsoft.Xna.Framework.Rectangle NurseRegion;
        internal Microsoft.Xna.Framework.Point NurseBiomeCenter;
        internal long NurseBiomeGeneration = -1;
        internal uint NextNurseBiomeScan;
        internal HostCompanions.NurseBiome NurseBiomes;
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
                ReferenceEquals(state.HeldSource.Items, array) && state.HeldSource.Slot == slot) return true;
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
            var preferences = actor.Preferences;
            if (!preferences.AllowNurse && !preferences.HealthRecoveryEnabled || !SupplyActorReady(actor)) return;
            RefreshSupplyPreferences(actor);
            // Nurse treatment has its own opt-in and native quote, including
            // removable debuffs at full life. Potion scoring only applies after
            // this attempt, and a paid or uncertain visit cannot spend twice.
            if (preferences.AllowNurse)
            {
                var outcome = TryCompanionNurse(actor);
                if (outcome == CompanionNurseOutcome.Treated || outcome == CompanionNurseOutcome.Unconfirmed) return;
            }
            if (!preferences.HealthRecoveryEnabled) return;
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
            if ((!actor.Preferences.BuffEnabled || actor.Preferences.AllowedBuffTypes.Length == 0) &&
                !WantsCompanionSonar(actor) || !SupplyActorReady(actor)) return false;
            RefreshSupplyPreferences(actor);
            var body = actor.Body;
            if (body.cursed || body.CCed || body.spectating >= 0) return false;
            var state = actor.Supplies;
            var source = state.PlannedBuff;
            if (SourceCurrent(actor, source) && BuffRecovery.Missing(body, source.Item.buffType) &&
                CompanionBuffAllowed(actor, source.Item, source.Account, source.Slot) &&
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
            int amount = 0, foodPriority = 0;
            RecoverySource best = default(RecoverySource);
            RecoverySource firstOtherBuff = default(RecoverySource);
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
                            if (!CompanionBuffAllowed(actor, item, account, slot) || !BuffRecovery.Missing(body, item.buffType)) continue;
                            int priority = FoodPriority(item.buffType);
                            if (priority == 0)
                            {
                                if (!firstOtherBuff.Found) firstOtherBuff = SupplySource(actor, items, account, slot);
                                continue;
                            }
                            // Native QuickBuff handles food first by tier and
                            // duration. Other effects keep their source order.
                            if (!best.Found || priority > foodPriority ||
                                priority == foodPriority && item.buffTime > best.Item.buffTime)
                            {
                                best = SupplySource(actor, items, account, slot);
                                foodPriority = priority;
                            }
                            continue;
                    }
                    best = SupplySource(actor, items, account, slot);
                }
            }
            return best.Found ? best : firstOtherBuff;
        }

        private static bool WantsCompanionSonar(Actor a) => FishingActive(a) &&
            a.Preferences.Fishing.FilterMode != 0 && BuffRecovery.Missing(a.Body, BuffID.Sonar);

        private static bool CompanionBuffAllowed(Actor a, Item item, int account, int slot)
        {
            var options = a.Preferences;
            // The exception belongs to this actor's fishing activity and
            // main inventory only. Other potions use their normal consent.
            return options.BuffEnabled && Array.IndexOf(options.AllowedBuffTypes, item.type) >= 0 ||
                account == 0 && slot < 50 && item.type == ItemID.SonarPotion && WantsCompanionSonar(a);
        }

        private static int FoodPriority(int buff)
        {
            switch (buff)
            {
                case BuffID.WellFed: return 1;
                case BuffID.WellFed2: return 2;
                case BuffID.WellFed3: return 3;
                default: return 0;
            }
        }

        private void UseQuick(Actor actor, CompanionSupplyKind kind, RecoverySource source, bool manual = false,
            Action<string, string> chatComplete = null)
        {
            var state = actor.Supplies;
            if (!source.Found || state.Quick.Found || !SourceCurrent(actor, source))
            {
                chatComplete?.Invoke("rejected", "物品来源已变化或正在使用。");
                return;
            }
            state.Quick = source;
            state.QuickKind = kind;
            state.ManualQuick = manual;
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
            finally { FinishQuick(actor, returned, chatComplete); }
        }

        internal static Item QuickSupply(Actor actor, CompanionSupplyKind kind)
        {
            var state = actor.Supplies;
            return state.QuickKind == kind && SourceCurrent(actor, state.Quick) ? state.Quick.Item : null;
        }

        internal void FinishQuick(Actor actor, bool returned, Action<string, string> chatComplete = null)
        {
            var state = actor.Supplies;
            var source = state.Quick;
            if (!source.Found) return;
            var kind = state.QuickKind;
            bool manual = state.ManualQuick;
            state.Quick = default(RecoverySource);
            state.ManualQuick = false;
            var after = source.Items[source.Slot];
            bool changed = !ReferenceEquals(after, source.Item) || after.type != source.Type || after.stack != source.Stack;
            if (kind == CompanionSupplyKind.Buff) state.QuickObserved = BuffRecovery.HasEffect(actor.Body, state.QuickBuffType);
            if (changed || state.QuickObserved) RecordSupplyChange(actor, source, changed);
            if (!returned || changed && !state.QuickObserved)
                state.Unconfirmed[source.Account] |= 1UL << source.Slot;
            else if (!state.QuickObserved)
                state.Refusals[(int)kind] = new SupplyFailure { Source = source, Until = Main.GameUpdateCount + 120 };
            if (manual)
            {
                Feedback = !returned || changed && !state.QuickObserved ? "本次使用结果未确认。" :
                    state.QuickObserved ? "队友已使用一次。" : "原版用物条件未满足，本次未使用。";
                chatComplete?.Invoke(!returned || changed && !state.QuickObserved ? "unconfirmed" :
                    state.QuickObserved ? "succeeded" : "failed", Feedback);
            }
        }

        internal int CompanionHeldSlot(Actor actor, bool includeAutomatic = true)
        {
            if (actor.Supplies.Once is ItemOnce command && (!command.Kind.HasValue || command.Kind == CompanionSupplyKind.Growth))
                return command.Source.Slot;
            if (!includeAutomatic || !actor.Preferences.GrowthEnabled || actor.Preferences.AllowedGrowthTypes.Length == 0 ||
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

        internal void BeginCompanionHeldItem(Actor actor, int slot)
        {
            var state = actor.Supplies;
            if (state.HeldSource.Found || slot < 0 || slot >= 50) return;
            var command = state.Once;
            if (command?.ChatCancellation.IsCancellationRequested == true)
            {
                CancelChatItemOnce(actor, "已撤回尚未开始的用物请求。");
                actor.Body.controlUseItem = false;
                actor.Body.selectedItemState.Select(command.PreviousSelection);
                return;
            }
            var source = command?.Source ?? SupplySource(actor, actor.Body.inventory, 0, slot);
            if (source.Slot != slot || !SourceCurrent(actor, source) ||
                (command == null || command.Kind == CompanionSupplyKind.Growth) && !CompanionSupplyCatalog.GrowthEligible(actor.Body, source.Type) ||
                !SupplyAdmitted(actor, source.Items, 0, slot, ownsSelected: true))
            {
                if (command != null) CancelCompanionItemOnce(actor);
                actor.Body.controlUseItem = false;
                return;
            }
            if (command != null && !command.Kind.HasValue)
            {
                if (!ManualHeldAim(actor, command, out var aim, out string reason))
                {
                    state.Once = null;
                    actor.Body.controlUseItem = false;
                    actor.Body.selectedItemState.Select(command.PreviousSelection);
                    Feedback = reason;
                    command.ChatComplete?.Invoke("failed", Feedback);
                    return;
                }
                actor.Aim = aim;
                if (aim.X != actor.Body.Center.X) actor.Body.ChangeDir(aim.X > actor.Body.Center.X ? 1 : -1);
            }
            state.HeldSource = source;
            if (command == null || command.Kind == CompanionSupplyKind.Growth)
                state.GrowthValue = GrowthValue(actor.Body, source.Type);
            if (command != null)
            {
                state.HeldOnce = command;
                state.Once = null;
                state.RestoreSelection = command.PreviousSelection;
            }
        }

        internal void ValidateCompanionHeldItem(Actor actor)
        {
            var state = actor.Supplies;
            var source = state.HeldSource;
            if (!source.Found) return;
            var command = state.HeldOnce;
            string reason = null;
            bool allowed = command != null || actor.Preferences.GrowthEnabled &&
                Array.IndexOf(actor.Preferences.AllowedGrowthTypes, source.Type) >= 0;
            if (allowed && SourceCurrent(actor, source) && SupplyActorReady(actor) &&
                actor.Body.selectedItem == source.Slot && !actor.Body.noItems && !actor.Body.CCed && !actor.Body.cursed &&
                (command == null || source.Item.prefix == command.Prefix && ManualAmmoAvailable(actor, source.Item, out reason)) &&
                (command != null && !command.Kind.HasValue || CompanionSupplyCatalog.GrowthEligible(actor.Body, source.Type))) return;
            actor.Body.controlUseItem = false;
            state.HeldSource = default(RecoverySource);
            state.HeldOnce = null;
            if (command != null) Feedback = reason ?? "物品或队友状态已变化，本次使用已取消。";
            command?.ChatComplete?.Invoke("cancelled", Feedback);
        }

        private static int GrowthValue(Player body, int type)
        {
            if (type == ItemID.ManaCrystal) return body.statManaMax;
            if (type == ItemID.LifeCrystal || type == ItemID.LifeFruit) return body.statLifeMax;
            return CompanionSupplyCatalog.GrowthComplete(body, type) ? 1 : 0;
        }

        internal void EndCompanionHeldItem(Actor actor, bool returned = true)
        {
            var state = actor.Supplies;
            var source = state.HeldSource;
            if (!source.Found) return;
            state.HeldSource = default(RecoverySource);
            var command = state.HeldOnce;
            state.HeldOnce = null;
            if (command != null && IsPortableBankItem(source.Type))
            {
                var area = source.Type == ItemID.MoneyTrough ? CompanionInventoryArea.PiggyBank : CompanionInventoryArea.VoidVault;
                // The animation may precede the network receipt. The UI will
                // admit a bank only after observing its actual provider.
                nextBankSearch = 0;
                Feedback = !returned ? "储存道具的使用结果未确认。" :
                    SearchBank(actor, area) != null ? "队友附近的储存入口已就绪。" :
                    actor.Body.ItemAnimationJustStarted ? "正在等待可用的储存入口出现。" : "原版用物条件未满足，未召出储存入口。";
                if (command.ChatComplete != null)
                {
                    bool ready = returned && SearchBank(actor, area) != null;
                    if (ready) command.ChatComplete("succeeded", Feedback);
                    else if (returned && actor.Body.ItemAnimationJustStarted) WaitForChatHeldItem(actor, command, 0);
                    else command.ChatComplete(returned ? "failed" : "unconfirmed", Feedback);
                }
                return;
            }
            if (command != null && !command.Kind.HasValue)
            {
                // An accepted native start can finish later (a phone, burst,
                // charged throw or server request). Never claim that the
                // destination, impact or server result has already happened.
                bool started = actor.Body.ItemAnimationJustStarted || actor.Body.itemTime > 0;
                var remaining = source.Items[source.Slot];
                bool changed = remaining.type != source.Type || remaining.stack != source.Stack;
                if (started || changed) RecordSupplyChange(actor, source, changed);
                if (!returned) state.Unconfirmed[0] |= 1UL << source.Slot;
                Feedback = !returned ? "本次使用结果未确认。" :
                    started ? "队友已开始使用一次。" : "原版用物条件未满足，本次未使用。";
                if (returned && started)
                    WaitForChatHeldItem(actor, command, Math.Max(0, source.Stack - (remaining.type == source.Type ? remaining.stack : 0)));
                else command.ChatComplete?.Invoke(!returned || changed ? "unconfirmed" : "failed",
                    changed ? "观察到物品变化，原版用物效果未确认。" : Feedback);
                return;
            }
            bool effect = GrowthValue(actor.Body, source.Type) != state.GrowthValue;
            var after = source.Items[source.Slot];
            bool debit = after.type != source.Type || after.stack < source.Stack;
            if (debit || effect) RecordSupplyChange(actor, source, debit);
            if (!returned || debit != effect)
                state.Unconfirmed[0] |= 1UL << source.Slot;
            else if (!effect)
                state.Refusals[(int)CompanionSupplyKind.Growth] = new SupplyFailure { Source = source, Until = Main.GameUpdateCount + 60 };
            if (command != null)
            {
                Feedback = !returned || debit != effect ? "本次使用结果未确认。" :
                    effect ? "队友已使用一次。" : "原版用物条件未满足，本次未使用。";
                command.ChatComplete?.Invoke(!returned || debit != effect ? "unconfirmed" : effect ? "succeeded" : "failed", Feedback);
            }
        }

        internal void AbandonCompanionHeldItem(Actor actor, Exception failure)
        {
            if (failure != null) { EndCompanionHeldItem(actor, returned: false); return; }
            // No ItemCheck ran in this normal update; no source was used
            // and an accepted manual command is not eligible for replay.
            if (actor.Supplies.HeldOnce != null)
            {
                Feedback = "原版没有开始本次用物。";
                actor.Supplies.HeldOnce.ChatComplete?.Invoke("failed", Feedback);
            }
            actor.Supplies.HeldOnce = null;
            actor.Supplies.HeldSource = default(RecoverySource);
        }

        private static void InvalidateItemSources(Actor actor)
        {
            var state = actor.Supplies;
            state.NextLifeScan = state.NextManaScan = state.NextBuffScan = state.NextGrowthScan = 0;
            state.PlannedBuff = default(RecoverySource);
            Array.Clear(state.Refusals, 0, state.Refusals.Length);
            var combat = actor.Combat;
            if (combat.Choice.Weapon != null && !combat.Choice.SameWeapon(actor.Body)) combat.Choice = default(CompanionAttackChoice);
            if (combat.ReadyChoice.Weapon != null && !combat.ReadyChoice.SameWeapon(actor.Body)) combat.ReadyChoice = default(CompanionAttackChoice);
            if (combat.Probe.Weapon != null && !combat.Probe.SameWeapon(actor.Body)) combat.Probe = default(CompanionAttackChoice);
            if (combat.Pursuit.Weapon != null && !combat.Pursuit.SameWeapon(actor.Body)) combat.Pursuit = default(CompanionAttackChoice);
        }

        private void RecordSupplyChange(Actor actor, RecoverySource source, bool itemsChanged)
        {
            actor.Dirty = true;
            if (itemsChanged && Main.netMode == 1)
                QueueItemSync(actor.Body, actor, SupplyNetworkBases[source.Account] + source.Slot);
        }
    }
}
