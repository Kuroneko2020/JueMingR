using System;
using System.Collections.Generic;
using System.Reflection;
using JueMingR.Platform.Items;
using JueMingR.TerrariaHost.Fishing;
using JueMingR.TerrariaHost.Items;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal sealed partial class HostCompanions
    {
        internal sealed class CompanionFishStorage
        {
            internal readonly List<FishSource> Queued = new List<FishSource>();
            internal FishSource[] Pending;
            internal FishSource[] TaskObserved;
            internal Dictionary<int, FishSource> ManualSources;
            internal readonly Dictionary<int, uint> CapacityBackoff = new Dictionary<int, uint>();
            internal CompanionConnection Connection;
            internal int Mode, TaskFish = -1, Moved;
            internal uint NextTaskRead;
            internal Vector2 RequestPosition, CapacityPosition;
            internal uint RequestedAt;
            internal bool UnknownReply, RecallAfterReceipt;
            internal void CancelQueued() { Queued.Clear(); }
        }

        internal sealed class FishSource
        {
            internal int Slot, Type, Prefix, Stack;
            internal Item Item;
            internal bool Replied;
        }

        internal sealed class FishingGrant
        {
            internal Actor Actor;
            internal int Type, Prefix, Mode;
            internal long Generation;
            internal bool Produced;
        }

        private Player fishStorageClientSnapshot;

        private static bool FishingStorageAwaiting(Actor a) => a.FishingLine.Storage?.Pending != null &&
            a.Connection != null && !a.Connection.Finished;

        private bool QueueRecallForFishStorage(Actor a)
        {
            if (!FishingStorageAwaiting(a)) return false;
            var state = a.FishingLine.Storage;
            state.RecallAfterReceipt = true;
            state.CancelQueued();
            EndFishingActivity(a);
            return true;
        }

        internal static bool FishingSourceHeld(Actor a, int slot)
        {
            var state = a.FishingLine.Storage;
            if (state?.Pending == null || !ReferenceEquals(state.Connection, a.Connection)) return false;
            foreach (var entry in state.Pending) if (!entry.Replied && entry.Slot == slot) return true;
            return false;
        }

        private bool CompleteUnconfirmedFishingRecall(Actor a)
        {
            var state = a.FishingLine.Storage;
            if (state?.Pending == null || !state.RecallAfterReceipt ||
                !state.UnknownReply && Main.GameUpdateCount - state.RequestedAt < 600) return false;
            // Player Control must not detach its own body inside Player.Update.
            // The connection pump runs outside it and has just drained replies.
            CloseActor(a, "存鱼结果尚未确认，已保留待核对库存。");
            return true;
        }

        private static ulong FishingDepositEvidence(Actor a)
        {
            var pending = a.FishingLine.Storage?.Pending;
            if (pending == null) return 0;
            ulong affected = 0;
            foreach (var entry in pending)
                if (!entry.Replied) affected |= 1UL << entry.Slot;
            if (affected != 0)
            {
                const string reason = "断开前未收到存鱼回执，涉及的背包格已保留待核对。";
                a.TerminalFailure = a.TerminalFailure == null ? reason : a.TerminalFailure + " " + reason;
            }
            // The archive keeps those exact player-slot bytes under the
            // unresolved marker. A fresh non-SSC logon cannot recover the
            // previous actor connection's remaining server-owned quantities.
            return affected;
        }

        private static void RetireDroppedFishStorage(Actor a)
        {
            var state = a.FishingLine.Storage;
            if (state?.Pending == null) return;
            // Before native death the server received the transfer request,
            // then settles death against its authoritative remaining slots.
            // A late pre-death slot must not resurrect dropped fish locally.
            foreach (var entry in state.Pending) a.Body.inventoryChestStack[entry.Slot] = false;
            state.Pending = null;
            state.Connection = null;
            state.RecallAfterReceipt = false;
        }

        internal FishingGrant BeforeFishingGrant(Actor a, Projectile bobber, int type)
        {
            int mode = a.Preferences.Fishing.StoreMode;
            var line = a.FishingLine;
            if (mode == 0 || mode == 2 && ActorQuestType(a) != type || Main.ServerSideCharacter ||
                !line.PullReceived || !FishingActivityOwns(a, bobber, out var tracked) || !tracked.Pullable ||
                !FishingObservation.Live(tracked.Body, a.Body) || type <= 0 || type >= ItemID.Count ||
                type >= ItemID.CopperCoin && type <= ItemID.PlatinumCoin)
                return null;
            return new FishingGrant { Actor = a, Type = type, Mode = mode, Generation = a.Generation };
        }

        internal void FinishFishingGrant(FishingGrant grant)
        {
            if (grant == null || !grant.Produced || grant.Actor.Closing || grant.Generation != grant.Actor.Generation) return;
            var a = grant.Actor;
            a.Dirty = true;
            if (grant.Mode != a.Preferences.Fishing.StoreMode) return;
            var line = a.FishingLine;
            if (line.Storage == null) line.Storage = new CompanionFishStorage();
            var state = line.Storage;
            if (state.Mode != grant.Mode) { state.CancelQueued(); state.Mode = grant.Mode; }
            for (int slot = 0; slot < 50; slot++)
            {
                Item item = a.Body.inventory[slot];
                if (item == null || item.IsAir || item.type != grant.Type || item.prefix != grant.Prefix ||
                    !CanStoreFishSource(a, slot)) continue;
                // Match the human all-catch rule: an actual native product,
                // even one that overflowed, permits current matching stacks.
                // A later pickup or replacement cannot join that opportunity.
                state.Queued.RemoveAll(source => source.Slot == slot);
                state.Queued.Add(new FishSource { Slot = slot, Item = item, Type = item.type, Prefix = item.prefix, Stack = item.stack });
            }
        }

        private bool CanStoreFishSource(Actor a, int slot)
        {
            var p = a.Body;
            var item = p.inventory[slot];
            if (slot == p.selectedItem || item.favorited || !SupplyAdmitted(a, p.inventory, 0, slot)) return false;
            var manual = a.FishingLine.Storage?.ManualSources;
            if (manual != null && manual.TryGetValue(slot, out var touched))
            {
                if (ReferenceEquals(item, touched.Item) && item.type == touched.Type &&
                    item.prefix == touched.Prefix && item.stack == touched.Stack) return false;
                manual.Remove(slot);
            }
            return !FishingGearSourceHeld(a, p.inventory, slot);
        }

        private static void FishingManualItemCommitted(Actor a, int slot, Item actual)
        {
            if (slot < 0 || slot >= 50 || !FishingActive(a) || a.Preferences.Fishing.StoreMode == 0) return;
            var line = a.FishingLine;
            if (line.Storage == null) line.Storage = new CompanionFishStorage { Mode = a.Preferences.Fishing.StoreMode };
            var state = line.Storage;
            state.Queued.RemoveAll(entry => entry.Slot == slot);
            if (actual == null || actual.IsAir)
            {
                state.ManualSources?.Remove(slot);
                return;
            }
            if (state.ManualSources == null) state.ManualSources = new Dictionary<int, FishSource>();
            // The native quest check may rediscover old stock. This actual
            // player handoff owns its concrete stack until it changes, so a
            // 30-step rescan cannot immediately undo the owner's interaction.
            state.ManualSources[slot] = new FishSource
            { Slot = slot, Item = actual, Type = actual.type, Prefix = actual.prefix, Stack = actual.stack };
        }

        private void AdvanceFishStorage(Actor a)
        {
            var state = a.FishingLine.Storage;
            var p = a.Body;
            if (state.Mode != a.Preferences.Fishing.StoreMode)
            {
                state.CancelQueued();
                state.Mode = a.Preferences.Fishing.StoreMode;
                state.TaskObserved = null;
                state.TaskFish = -1;
                state.ManualSources?.Clear();
            }
            if (state.Pending != null) return;
            if (Main.ServerSideCharacter ||
                Main.netMode == 1 && (a.Connection == null || a.Connection.Finished) || p.dead || a.Closing) return;
            if (state.Mode == 2) ObserveQuestStock(a);
            if (state.Queued.Count == 0 || p.IsStackingItems()) return;
            for (int i = state.Queued.Count - 1; i >= 0; i--)
            {
                var source = state.Queued[i];
                var actual = p.inventory[source.Slot];
                if (state.Mode == 2 && ActorQuestType(a) != source.Type || !CanStoreFishSource(a, source.Slot) ||
                    !ReferenceEquals(actual, source.Item) || actual.type != source.Type ||
                    actual.prefix != source.Prefix || actual.stack != source.Stack) state.Queued.RemoveAt(i);
            }
            if (state.Queued.Count == 0) return;
            if (Vector2.DistanceSquared(state.CapacityPosition, p.position) > 16f * 16f)
            {
                state.CapacityBackoff.Clear();
                state.CapacityPosition = p.position;
            }
            FishSource first = null;
            foreach (var source in state.Queued)
                if (!state.CapacityBackoff.TryGetValue(StorageKey(source), out uint until) || Main.GameUpdateCount >= until)
                { first = source; break; }
            if (first == null) return;
            var batch = new List<FishSource>();
            foreach (var source in state.Queued)
                if (source.Type == first.Type && source.Prefix == first.Prefix && Item.CanStack(first.Item, source.Item))
                    batch.Add(source);
            // Some unique quest fish have maxStack 1. A native nonstackable
            // singleton still has a legal same-type destination.
            if (batch.Count == 0) batch.Add(first);
            foreach (var source in batch) state.Queued.Remove(source);
            var items = new Item[batch.Count];
            var slots = new PlayerItemSlotID.SlotReference[batch.Count];
            for (int i = 0; i < slots.Length; i++)
            {
                var source = batch[i];
                items[i] = source.Item;
                slots[i] = new PlayerItemSlotID.SlotReference(p, PlayerItemSlotID.Inventory0 + source.Slot);
            }
            var native = CompanionHooks.FishingChestStack;
            if (native == null) return;
            try
            {
                using (var scope = CompanionHooks.Enter(a, a.Aim, inventory: true))
                {
                    var identity = new ItemIdentity(first.Type, (byte)first.Prefix);
                    int bankMoved = native.ExecuteBanks(p, identity, items, slots, ulong.MaxValue,
                        bank => FishBankAdmitted(a, bank, first.Type)).GetValueOrDefault();
                    if (bankMoved != 0)
                    {
                        // Private slots are saved and synchronized by this
                        // actor. Only a remaining stack belongs to message 85.
                        a.Dirty = true;
                        int remaining = 0;
                        for (int i = 0; i < batch.Count; i++)
                        {
                            FishSource previous = batch[i];
                            RememberTaskSlot(state, p, previous.Slot);
                            Item item = slots[i].Item;
                            if (item.IsAir) continue;
                            items[remaining] = item; slots[remaining] = slots[i];
                            batch[remaining++] = new FishSource
                            { Slot = previous.Slot, Item = item, Type = item.type, Prefix = item.prefix, Stack = item.stack };
                        }
                        if (remaining == 0) return;
                        batch.RemoveRange(remaining, batch.Count - remaining);
                        Array.Resize(ref items, remaining); Array.Resize(ref slots, remaining);
                    }
                    if (Main.netMode == 0 && !HasLocalFishChest(p, first.Type))
                    {
                        if (bankMoved == 0) RememberFullFishChests(state, first, p.position);
                        return;
                    }
                    var request = native.Prepare(p, items, slots);
                    if (Main.netMode == 1)
                    {
                        state.Pending = batch.ToArray();
                        state.Connection = a.Connection;
                        state.RequestPosition = p.position;
                        state.RequestedAt = Main.GameUpdateCount;
                        state.Moved = 0;
                    }
                    native.Execute(p, request);
                    // Original QuickStack ends with exactly one 85 after the
                    // source frames. A normal return with no accepted 85
                    // leaves the source unchanged on both client and server.
                    if (state.Pending != null && !scope.PacketAccepted) CancelUnsentFishStorage(a);
                }
                if (Main.netMode == 0)
                {
                    bool moved = false;
                    foreach (var source in batch)
                    {
                        Item item = p.inventory[source.Slot];
                        moved |= item.IsAir || item.type == source.Type && item.prefix == source.Prefix && item.stack < source.Stack;
                        RememberTaskSlot(state, p, source.Slot);
                    }
                    if (!moved) RememberFullFishChests(state, first, p.position);
                }
            }
            catch (TargetInvocationException e) when (e.InnerException is CompanionConnection.SendQueueFullException)
            {
                // The direct FIFO can prove rejection before transmission.
                // Earlier 5s only mirrored the unchanged source quantities.
                CancelUnsentFishStorage(a);
            }
            catch (Exception)
            {
                // A server may already own the transfer. Retain only the
                // original expected slots for late replies, never retry.
                if (state.Pending != null) state.UnknownReply = true;
            }
        }

        private bool FishBankAdmitted(Actor actor, Chest bank, int type)
        {
            // A loan's empty return cell has the same owner as a held
            // supply or draft cell. Native Transfer can also fill partial stacks.
            int account = -bank.index - 1;
            for (int slot = 0; slot < bank.item.Length; slot++)
            {
                Item item = bank.item[slot];
                if ((item.IsAir || item.type == type && item.stack < item.maxStack) &&
                    (!SupplyAdmitted(actor, bank.item, account, slot) || FishingGearSourceHeld(actor, bank.item, slot))) return false;
            }
            return true;
        }

        private static void CancelUnsentFishStorage(Actor a)
        {
            var state = a.FishingLine.Storage;
            if (state?.Pending == null) return;
            foreach (var source in state.Pending)
                if (ReferenceEquals(a.Body.inventory[source.Slot], source.Item)) a.Body.inventoryChestStack[source.Slot] = false;
            state.Pending = null;
            state.Connection = null;
        }

        private void ObserveQuestStock(Actor a)
        {
            var line = a.FishingLine;
            var state = line.Storage;
            int type = FishingActive(a) && line.Confirmed && Main.GameUpdateCount - line.LastWet < 60 ? ActorQuestType(a) : -1;
            if (type != state.TaskFish)
            {
                state.TaskFish = type;
                state.TaskObserved = null;
                state.NextTaskRead = 0;
            }
            if (type <= 0 || Main.GameUpdateCount < state.NextTaskRead) return;
            state.NextTaskRead = Main.GameUpdateCount + 30;
            if (state.TaskObserved == null) state.TaskObserved = new FishSource[50];
            for (int slot = 0; slot < 50; slot++)
            {
                Item item = a.Body.inventory[slot];
                if (item.IsAir || item.type != type || !CanStoreFishSource(a, slot))
                {
                    state.TaskObserved[slot] = null;
                    continue;
                }
                var prior = state.TaskObserved[slot];
                if (prior != null && ReferenceEquals(prior.Item, item) && prior.Prefix == item.prefix && prior.Stack == item.stack) continue;
                RememberTaskSlot(state, a.Body, slot);
                state.Queued.RemoveAll(source => source.Slot == slot);
                state.Queued.Add(state.TaskObserved[slot]);
            }
        }

        private static void RememberTaskSlot(CompanionFishStorage state, Player player, int slot)
        {
            if (state.TaskObserved == null) return;
            Item item = player.inventory[slot];
            state.TaskObserved[slot] = !item.IsAir && item.type == state.TaskFish
                ? new FishSource { Slot = slot, Item = item, Type = item.type, Prefix = item.prefix, Stack = item.stack } : null;
        }

        private static bool HasLocalFishChest(Player player, int type)
        {
            foreach (var target in NearbyChests.GetChestsInRangeOf(player.position))
            {
                var chest = target.chest;
                if (chest.bankChest || chest.IsLockedOrInUse()) continue;
                for (int i = 0; i < chest.maxItems; i++)
                    if (!chest.item[i].IsAir && chest.item[i].type == type) return true;
            }
            return false;
        }

        private static int StorageKey(FishSource source) => (source.Type << 8) | source.Prefix;

        private static void RememberFullFishChests(CompanionFishStorage state, FishSource source, Vector2 position)
        {
            if (state.CapacityBackoff.Count == 64 || Vector2.DistanceSquared(state.CapacityPosition, position) > 16f * 16f)
                state.CapacityBackoff.Clear();
            state.CapacityPosition = position;
            state.CapacityBackoff[StorageKey(source)] = Main.GameUpdateCount + 120;
        }

        private void DecodeFishStorageReply(Actor a, byte[] packet)
        {
            var state = a.FishingLine.Storage;
            if (state?.Pending == null || !ReferenceEquals(state.Connection, a.Connection) || packet.Length != 10 ||
                !a.Ready || a.Closing || packet[1] != a.Body.whoAmI) return;
            int slot = BitConverter.ToInt16(packet, 2) - PlayerItemSlotID.Inventory0;
            FishSource source = null;
            foreach (var member in state.Pending)
                if (!member.Replied && member.Slot == slot) { source = member; break; }
            var p = a.Body;
            if (source == null || !p.inventoryChestStack[slot] || !ReferenceEquals(p.inventory[slot], source.Item) ||
                source.Item.stack != source.Stack || source.Item.type != source.Type || source.Item.prefix != source.Prefix) return;
            int count = BitConverter.ToInt16(packet, 4);
            int prefix = packet[6];
            int type = BitConverter.ToInt16(packet, 7);
            if (count < 0 || count > source.Stack || (type == 0 ? count != 0 : type != source.Type || prefix != source.Prefix || count == 0))
            {
                state.UnknownReply = true;
                return;
            }
            Buffer.BlockCopy(packet, 0, decoder.readBuffer, 0, packet.Length);
            decoder.ResetReader();
            Decoding = a;
            var humanSnapshot = Main.clientPlayer;
            try
            {
                if (fishStorageClientSnapshot == null) fishStorageClientSnapshot = new Player();
                Main.clientPlayer = fishStorageClientSnapshot;
                // Native case 5 owns the actual item replacement and unlock.
                using (CompanionHooks.Enter(a, a.Aim)) decoder.GetData(0, packet.Length, out _);
            }
            finally { Main.clientPlayer = humanSnapshot; Decoding = null; }
            var actual = p.inventory[slot];
            if (p.inventoryChestStack[slot] || actual.type != type || actual.stack != count || actual.prefix != prefix)
            { state.UnknownReply = true; return; }
            source.Replied = true;
            a.Dirty |= count != source.Stack;
            state.Moved += source.Stack - count;
            RememberTaskSlot(state, p, slot);
            // The server already owns this state. Update the actor's share
            // baseline without sending the same slot back across the wire.
            int index = Array.BinarySearch(a.NetworkSlotIds, PlayerItemSlotID.Inventory0 + slot);
            if (index >= 0 && a.SyncType != null)
            {
                a.SyncType[index] = type; a.SyncStack[index] = count;
                a.SyncPrefix[index] = prefix; a.SyncFavorite[index] = actual.favorited;
            }
            foreach (var member in state.Pending) if (!member.Replied) return;
            if (state.Moved == 0 && Vector2.DistanceSquared(state.RequestPosition, p.position) <= 16f * 16f)
                RememberFullFishChests(state, source, state.RequestPosition);
            state.Pending = null;
            state.Connection = null;
            state.UnknownReply = false;
            if (state.RecallAfterReceipt)
            {
                state.RecallAfterReceipt = false;
                CloseActor(a, null);
            }
        }
    }
}
