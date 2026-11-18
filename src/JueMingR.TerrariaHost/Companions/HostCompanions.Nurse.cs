using System;
using System.Collections.Generic;
using JueMingR.Platform.Companions;
using JueMingR.Platform.Items;
using JueMingR.TerrariaHost.Recovery;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal enum CompanionNurseOutcome { NotRun, Unpaid, Treated, Unconfirmed }

    internal sealed partial class HostCompanions
    {
        private ShopHelper companionNurseShop;
        private readonly Dictionary<Guid, long> pendingNurse = new Dictionary<Guid, long>();
        private long nurseSession = -1;

        private static bool NurseNeedsService(Player body)
        {
            if (body.statLife < body.statLifeMax2) return true;
            for (int i = 0; i < Player.maxBuffs; i++)
                if (body.buffTime[i] > 60 && NurseRemovable(body.buffType[i])) return true;
            return false;
        }

        private static bool NurseRemovable(int type)
        {
            return type > 0 && type < BuffID.Count && Main.debuff[type] &&
                !BuffID.Sets.NurseCannotRemoveDebuff[type];
        }

        private static bool NurseCurrent(Player body, NPC npc, int slot, byte generation)
        {
            return slot >= 0 && slot < Main.maxNPCs && ReferenceEquals(Main.npc[slot], npc) &&
                npc.generation == generation && NearbyServiceTarget.Eligible(body, npc, NPCID.Nurse);
        }

        private NPC FindNurse(Actor actor)
        {
            var state = actor.Supplies;
            if (state.Nurse != null && NurseCurrent(actor.Body, state.Nurse, state.NurseSlot, state.NurseGeneration))
                return state.Nurse;
            var region = TileReachCheckSettings.Simple.GetWorldRegion(actor.Body);
            if (Main.GameUpdateCount < state.NurseScan && region == state.NurseRegion) return null;
            state.Nurse = null;
            state.NurseSlot = -1;
            state.NurseScan = Main.GameUpdateCount + 60;
            state.NurseRegion = region;
            float distance = float.MaxValue;
            for (int slot = 0; slot < Main.maxNPCs; slot++)
            {
#if DEBUG
                state.NurseReads++;
#endif
                NPC candidate = Main.npc[slot];
                if (!NearbyServiceTarget.Eligible(actor.Body, candidate, NPCID.Nurse)) continue;
                float candidateDistance = Vector2.DistanceSquared(actor.Body.Center, candidate.Center + candidate.netOffset);
                if (candidateDistance >= distance) continue;
                distance = candidateDistance;
                state.Nurse = candidate;
                state.NurseSlot = slot;
                state.NurseGeneration = candidate.generation;
            }
            return state.Nurse;
        }

        private CompanionNurseOutcome TryCompanionNurse(Actor actor)
        {
            var body = actor.Body;
            var payer = owner;
            if (!SupplyActorReady(actor) || payer == null || !payer.active || ReferenceEquals(payer, body) ||
                Main.netMode == 1 && (actor.Connection == null || actor.Connection.Finished ||
                    Netplay.Connection == null || !Netplay.Connection.IsConnected()) || !NurseNeedsService(body))
                return CompanionNurseOutcome.NotRun;
            if (nurseSession != session)
            {
                nurseSession = session;
                pendingNurse.Clear();
            }
            if (pendingNurse.TryGetValue(actor.Profile.Id, out long epoch) && epoch == actor.Profile.DataEpoch)
                return CompanionNurseOutcome.NotRun;

            NPC nurse = FindNurse(actor);
            if (nurse == null || Main.GameUpdateCount < actor.Supplies.NextNurseAttempt)
                return CompanionNurseOutcome.NotRun;
            actor.Supplies.NextNurseAttempt = Main.GameUpdateCount + 30;

            if (companionNurseShop == null) companionNurseShop = new ShopHelper();
            ShoppingSettings before = body.currentShoppingSettings;
            ShoppingSettings terms = default(ShoppingSettings);
            bool shoppingApplied = false;
            var preferences = actor.Preferences;
            var quote = 0;
            NursePaymentAttempt attempt = null;
            try
            {
                using (CompanionHooks.Enter(actor, actor.Aim))
                {
                    // Both native consumers read the temporary scoped player. A
                    // shopping quote needs no talkNPC, so neither player opens or
                    // closes a chat and there is no talk packet to publish.
                    terms = companionNurseShop.GetShoppingSettings(body, nurse);
                    body.currentShoppingSettings = terms;
                    shoppingApplied = true;
                    quote = Main.GetNurseHealCost();
                    if (quote <= 0) return CompanionNurseOutcome.Unpaid;
                    if (!CompanionNurseWallet.TryBalance(body, out long patientBalance) ||
                        !CompanionNurseWallet.TryBalance(payer, out long ownerBalance) ||
                        patientBalance + ownerBalance < quote) return CompanionNurseOutcome.Unpaid;
                    long firstShare = Math.Min(patientBalance, quote);
#if DEBUG
                    actor.Supplies.WalletPlans++;
#endif
                    var first = CompanionNurseWallet.Prepare(body, patientBalance, firstShare);
                    var second = CompanionNurseWallet.Prepare(payer, ownerBalance, quote - firstShare);
                    if (first == null || second == null) return CompanionNurseOutcome.Unpaid;
                    attempt = new NursePaymentAttempt(this, actor, payer, nurse, quote, first, second, preferences, terms);
                    CompanionSupplyHooks.RunNurse(attempt);
                }
                if (!attempt.Committed) return CompanionNurseOutcome.Unpaid;

                bool treated = attempt.Paid && body.statLife == body.statLifeMax2;
                for (int i = 0; i < Player.maxBuffs && treated; i++)
                    if (body.buffTime[i] > 0 && NurseRemovable(body.buffType[i])) treated = false;
                if (!treated) return CompanionNurseOutcome.Unconfirmed;
                pendingNurse.Remove(actor.Profile.Id);
                actor.Supplies.LifeFrame = Main.GameUpdateCount;
                return CompanionNurseOutcome.Treated;
            }
            catch (Exception)
            {
                return attempt?.Committed == true ? CompanionNurseOutcome.Unconfirmed : CompanionNurseOutcome.Unpaid;
            }
            finally
            {
                if (shoppingApplied && body.currentShoppingSettings.Equals(terms)) body.currentShoppingSettings = before;
                if (attempt?.Committed == true) RecordNursePayment(attempt);
            }
        }

        private void RecordNursePayment(NursePaymentAttempt attempt)
        {
            // All the writes are already real. Queue actual owner and AI slot
            // values after the transaction so network and saves can retry
            // independently of payment. HealEffect supplies the visual 35.
            attempt.Actor.Dirty = true;
            if (Main.netMode == 1)
            {
                foreach (var write in attempt.PatientWallet.Writes)
                    QueueItemSync(attempt.Actor.Body, attempt.Actor, SupplyNetworkBases[write.Account] + write.Slot);
                foreach (var write in attempt.PayerWallet.Writes)
                    QueueItemSync(attempt.Payer, null, SupplyNetworkBases[write.Account] + write.Slot);
            }
            foreach (var write in attempt.PayerWallet.Writes)
            {
                try { OwnerItems?.NotifyManualItemReplacement(attempt.Payer, write.Items, write.Slot, write.Before, write.After); }
                catch (Exception) { Feedback = "护士已扣款，物品状态通知失败。"; }
            }
            if (pendingNurse.ContainsKey(attempt.ProfileId))
                Feedback = "护士扣款已提交，治疗结果未确认。";
        }

        internal sealed class NursePaymentAttempt
        {
            private readonly HostCompanions host;
            private readonly Player patient;
            private readonly NPC nurse;
            private readonly int slot;
            private readonly byte generation;
            private readonly long activation, dataEpoch, session;
            private readonly long ownerSession;
            private readonly int mode;
            private readonly ItemOperationOwnership ownerOperations;
            private readonly object world, connection, socket;
            private readonly CompanionConnection actorConnection;
            private readonly CompanionBehavior preferences;
            private readonly ShoppingSettings terms;
            internal readonly Guid ProfileId;
            internal readonly CompanionNurseWallet PatientWallet, PayerWallet;
            internal readonly Actor Actor;
            internal readonly Player Payer;
            internal readonly int Quote;
            internal bool Committed { get; private set; }
            internal bool Paid { get; private set; }
            private bool entered;

            internal NursePaymentAttempt(HostCompanions host, Actor actor, Player payer, NPC nurse,
                int quote, CompanionNurseWallet patientWallet, CompanionNurseWallet payerWallet,
                CompanionBehavior preferences, ShoppingSettings terms)
            {
                this.host = host;
                this.nurse = nurse;
                this.preferences = preferences;
                this.terms = terms;
                Actor = actor;
                Payer = payer;
                patient = actor.Body;
                slot = actor.Supplies.NurseSlot;
                generation = actor.Supplies.NurseGeneration;
                activation = actor.Generation;
                ProfileId = actor.Profile.Id;
                dataEpoch = actor.Profile.DataEpoch;
                world = host.world;
                session = host.session;
                ownerOperations = host.OwnerOperations;
                ownerSession = ownerOperations?.Session ?? 0;
                mode = Main.netMode;
                if (mode == 1)
                {
                    connection = Netplay.Connection;
                    socket = Netplay.Connection?.Socket;
                    actorConnection = actor.Connection;
                }
                Quote = quote;
                PatientWallet = patientWallet;
                PayerWallet = payerWallet;
            }

            internal bool Commit(Player buyer, long price, int currency)
            {
                if (entered) return false;
                entered = true;
                if (currency != -1 || price != Quote || !ReferenceEquals(buyer, patient) ||
                    !ReferenceEquals(Main.LocalPlayer, patient) || !ReferenceEquals(CompanionHooks.CurrentActor, Actor) ||
                    !ReferenceEquals(Actor.Body, patient) || !host.SupplyActorReady(Actor) ||
                    Actor.Generation != activation || Actor.Profile.Id != ProfileId || Actor.Profile.DataEpoch != dataEpoch ||
                    !ReferenceEquals(Actor.Preferences, preferences) || !preferences.HealthRecoveryEnabled || !preferences.AllowNurse ||
                    host.session != session || !ReferenceEquals(host.world, world) || Main.netMode != mode ||
                    !ReferenceEquals(Payer, host.owner) || !Payer.active ||
                    !ReferenceEquals(Payer, Main.ActivePlayerFileData?.Player) ||
                    Main.netMode == 1 && (!ReferenceEquals(connection, Netplay.Connection) ||
                        !ReferenceEquals(socket, Netplay.Connection?.Socket) || !ReferenceEquals(actorConnection, Actor.Connection)) ||
                    !NurseCurrent(patient, nurse, slot, generation) || !NurseNeedsService(patient) ||
                    !patient.currentShoppingSettings.Equals(terms) ||
                    host.companionNurseShop.GetShoppingSettings(patient, nurse).PriceAdjustment != terms.PriceAdjustment ||
                    Main.GetNurseHealCost() != Quote || !PatientWallet.Current() || !PayerWallet.Current())
                    return false;

                foreach (var write in PatientWallet.Writes)
                    if (!host.SupplyAdmitted(Actor, write.Items, write.Account, write.Slot)) return false;
                ItemOperationOwnership lease = ownerOperations;
                long token = 0;
                if (PayerWallet.Share > 0)
                {
                    if (lease == null || !ReferenceEquals(host.OwnerOperations, lease) || lease.Session != ownerSession) return false;
                    foreach (var write in PayerWallet.Writes)
                        if (host.itemDraft?.DependsOn(write.Items, write.Slot) == true ||
                            ReferenceEquals(write.Before, Main.mouseItem) ||
                            write.Account == 0 && CompanionItemRules.Blocked(Payer, CompanionInventoryArea.Inventory, write.Slot, lease) ||
                            host.OwnerItems?.CompanionPaymentSlotProtected(Payer, write.Items, write.Slot) == true)
                            return false;
                    token = lease.NewUseToken();
                    if (!lease.TryBeginRecovery(ownerSession, PayerWallet.Slots, token)) return false;
                }

                var held = Actor.Supplies.PaymentSlots;
                Array.Copy(PatientWallet.Slots, held, held.Length);
                try
                {
                    // Register uncertainty before publication. No external
                    // calls, scheduler or I/O between these array assignments.
                    host.pendingNurse[ProfileId] = dataEpoch;
                    Committed = true;
                    PatientWallet.Apply();
                    PayerWallet.Apply();
                    Paid = PatientWallet.Debited() && PayerWallet.Debited();
                    return Paid;
                }
                finally
                {
                    if (Committed && !Paid)
                        for (int account = 0; account < held.Length; account++)
                            Actor.Supplies.Unconfirmed[account] |= held[account];
                    Array.Clear(held, 0, held.Length);
                    if (token != 0) lease.EndRecovery(ownerSession, PayerWallet.Slots, token, Committed && !Paid);
                }
            }
        }
    }
}
