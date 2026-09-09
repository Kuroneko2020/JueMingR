using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using JueMingR.Platform.Items;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Items
{
    internal sealed class ItemNearbyStorage
    {
        private readonly ItemHostObservation world;
        private readonly ItemOperationOwnership ownership;
        private MethodInfo quickStack;
        private Type sourceType;
        private FieldInfo sourceItems, sourceCount, sourceSlots, sourceBlocked, sourcePosition;
        private readonly object receiptGate = new object();
        private Pending pending;
        private readonly Dictionary<ItemIdentity, CapacityWait> capacityWaits = new Dictionary<ItemIdentity, CapacityWait>();
        private ulong nextCapacityExpiry;
        private int capacityChanged;
        internal bool GuardsReady { get; set; }
        internal ulong Tick { get; set; }

        internal ItemNearbyStorage(ItemHostObservation world, ItemOperationOwnership ownership)
        {
            this.world = world; this.ownership = ownership;
        }
        internal void BindNativeOperation()
        {
            sourceType = typeof(QuickStacking).GetNestedType("SourceInventory", BindingFlags.NonPublic);
            if (sourceType == null || !sourceType.IsValueType) throw new MissingMemberException("selective-storage-source-abi");
            sourceItems = Field("items", typeof(Item[])); sourceCount = Field("numItems", typeof(int));
            sourceSlots = Field("slots", typeof(PlayerItemSlotID.SlotReference[])); sourceBlocked = Field("transferBlocked", typeof(bool[]));
            sourcePosition = Field("position", typeof(Vector2));
            quickStack = typeof(QuickStacking).GetMethod("QuickStackToNearbyChests", BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(Player), sourceType, typeof(bool) }, null);
            if (quickStack == null || quickStack.ReturnType != typeof(void)) throw new MissingMethodException("selective-storage-operation-abi");
        }
        private FieldInfo Field(string name, Type type)
        {
            FieldInfo field = sourceType.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field == null || field.FieldType != type) throw new MissingFieldException("selective-storage-" + name);
            return field;
        }
        internal ItemOperationResult Execute(StoreItemsRequest request)
        {
            Player player = world.Player;
            if (!GuardsReady || player == null || !world.CanStartActions || request.Session != world.SessionGeneration || world.Busy ||
                player.HasLockedInventory() || Main.ServerSideCharacter) return Result(ItemOperationState.Rejected, "storage-capability-or-native-operation-busy");
            int count = request.Sources.Count;
            ItemIdentity identity = request.Sources[0].Identity;
            CapacityWait wait;
            if (capacityWaits.TryGetValue(identity, out wait) && wait.Position == player.position && Tick < wait.Until)
                return Result(ItemOperationState.Rejected, "recent-no-capacity-awaiting-change");
            var items = new Item[count]; var slots = new PlayerItemSlotID.SlotReference[count];
            ulong mask = 0; int originalQuantity = 0;
            for (int i = 0; i < count; i++)
            {
                ItemSlotObservation source = request.Sources[i];
                if (!source.Identity.Equals(identity) || source.MaximumStack <= 1 || !world.Matches(source)) return Result(ItemOperationState.Rejected, "storage-source-changed");
                Item item = player.inventory[source.Slot];
                if (i > 0 && !Item.CanStack(items[0], item)) return Result(ItemOperationState.Rejected, "storage-incompatible-source");
                for (int j = 0; j < i; j++) if (ReferenceEquals(item, items[j])) return Result(ItemOperationState.Rejected, "storage-aliased-source");
                items[i] = item; slots[i] = new PlayerItemSlotID.SlotReference(player, PlayerItemSlotID.Inventory0 + source.Slot);
                mask |= 1UL << source.Slot; originalQuantity = checked(originalQuantity + item.stack);
            }
            object native = Activator.CreateInstance(sourceType);
            sourceItems.SetValue(native, items); sourceCount.SetValue(native, count); sourceSlots.SetValue(native, slots);
            sourceBlocked.SetValue(native, new bool[count]); sourcePosition.SetValue(native, player.Center);
            List<Chest> targets = null; long beforeTarget = 0;
            if (Main.netMode == 0)
            {
                // Copy this one native scratch result before the native operation
                // reuses it. This is an outcome check, never a private-bank precheck.
                targets = new List<Chest>();
                foreach (PositionedChest target in NearbyChests.GetChestsInRangeOf(player.position)) targets.Add(target.chest);
                beforeTarget = Quantity(targets, identity);
            }
            if (!ownership.TryBeginStore(request.Session, mask)) return Result(ItemOperationState.Rejected, "storage-range-busy");
            if (Main.netMode == 1)
            {
                lock (receiptGate) pending = new Pending(request, player, originalQuantity, Tick);
            }
            ItemOperationResult result;
            try
            {
                world.AutomaticOperation = true;
                // Only this already selected group reaches the original overload.
                // No real slot is masked, swapped, cleared, favorited or restored.
                quickStack.Invoke(null, new[] { (object)player, native, false });
                if (Main.netMode == 1) return ownership.StoreResult;
                int remaining = 0;
                foreach (ItemSlotObservation source in request.Sources)
                {
                    Item actual = player.inventory[source.Slot];
                    if (!actual.IsAir && (actual.type != identity.Type || actual.prefix != identity.Prefix)) throw new InvalidOperationException("storage-source-replaced");
                    if (!actual.IsAir) remaining = checked(remaining + actual.stack);
                }
                long added = Quantity(targets, identity) - beforeTarget;
                int moved = originalQuantity - remaining;
                result = moved >= 0 && remaining >= 0 && added == moved ?
                    new ItemOperationResult(moved == 0 ? ItemOperationState.NotApplicable : remaining == 0 ? ItemOperationState.Completed : ItemOperationState.PartiallyCompleted, moved) :
                    Result(ItemOperationState.Unconfirmed, "local-transfer-conservation-not-confirmed");
            }
            catch { result = Result(ItemOperationState.Unconfirmed, "storage-interrupted-after-admission"); }
            finally { world.AutomaticOperation = false; }
            ownership.FinishStore(request.Session, result);
            if (result.State == ItemOperationState.NotApplicable) RememberNoCapacity(identity, player.position);
            return result;
        }

        // Prefix/postfix observations bracket actual native message application.
        // A cleared lock alone is never a receipt and net85 is never a success ACK.
        internal Receipt BeforeMessage(MessageBuffer buffer, int start, int length)
        {
            lock (receiptGate)
            {
                if (Main.netMode == 1 && start >= 0 && length > 0 && start < buffer.readBuffer.Length && buffer.readBuffer[start] == 32) Interlocked.Exchange(ref capacityChanged, 1);
                Pending current = pending;
                if (current == null || Main.netMode != 1 || Main.ServerSideCharacter || current.Request.Session != world.SessionGeneration ||
                    !ReferenceEquals(world.Player, current.Player) || start < 0 || length < 10 || start > buffer.readBuffer.Length - 10) return default(Receipt);
                byte[] data = buffer.readBuffer;
                if (data[start] != 5 || data[start + 1] != current.Player.whoAmI) return default(Receipt);
                int slot = Signed(data, start + 2) - PlayerItemSlotID.Inventory0;
                for (int i = 0; i < current.Request.Sources.Count; i++)
                {
                    if (current.Request.Sources[i].Slot != slot || current.Received[i] || !current.Player.inventoryChestStack[slot]) continue;
                    return new Receipt(current, i, Signed(data, start + 7), data[start + 6], Signed(data, start + 4));
                }
                return default(Receipt);
            }
        }
        internal void AfterMessage(Receipt receipt)
        {
            if (receipt.Batch == null) return;
            lock (receiptGate)
            {
                Pending current = pending;
                if (!ReferenceEquals(current, receipt.Batch) || current.Request.Session != world.SessionGeneration ||
                    !ReferenceEquals(world.Player, current.Player) || Main.netMode != 1) return;
                int slot = current.Request.Sources[receipt.Index].Slot;
                Item actual = current.Player.inventory[slot];
                if (current.Player.inventoryChestStack[slot] || actual == null || actual.type != receipt.Type || actual.stack != receipt.Quantity || actual.prefix != receipt.Prefix) return;
                ItemIdentity identity = current.Request.Sources[0].Identity;
                if (receipt.Quantity < 0 || (receipt.Type != 0 && (receipt.Type != identity.Type || receipt.Prefix != identity.Prefix))) current.Invalid = true;
                current.Received[receipt.Index] = true; current.Remaining[receipt.Index] = receipt.Type == 0 ? 0 : receipt.Quantity;
                current.Changed = true;
            }
        }
        internal void Update()
        {
            lock (receiptGate)
            {
                if (Interlocked.Exchange(ref capacityChanged, 0) != 0) InvalidateCapacity();
                if (capacityWaits.Count != 0 && Tick >= nextCapacityExpiry)
                {
                    var expired = new List<ItemIdentity>(); nextCapacityExpiry = ulong.MaxValue;
                    foreach (var pair in capacityWaits)
                        if (Tick >= pair.Value.Until) expired.Add(pair.Key); else nextCapacityExpiry = Math.Min(nextCapacityExpiry, pair.Value.Until);
                    foreach (ItemIdentity identity in expired) capacityWaits.Remove(identity);
                    if (expired.Count != 0) world.InvalidateObservation();
                }
                Pending current = pending;
                if (current == null) return;
                if (current.Request.Session != world.SessionGeneration || !ReferenceEquals(world.Player, current.Player)) { pending = null; return; }
                if (current.Changed)
                {
                    current.Changed = false;
                    bool all = true; int remaining = 0;
                    for (int i = 0; i < current.Received.Length; i++) { all &= current.Received[i]; remaining = checked(remaining + current.Remaining[i]); }
                    if (current.Invalid || remaining > current.OriginalQuantity)
                    {
                        ownership.FinishStore(current.Request.Session, Result(ItemOperationState.Unconfirmed, "unexpected-source-reply"));
                        current.Invalid = true;
                    }
                    else if (all)
                    {
                        int moved = current.OriginalQuantity - remaining;
                        ownership.FinishStore(current.Request.Session, new ItemOperationResult(moved == 0 ? ItemOperationState.NotApplicable :
                            remaining == 0 ? ItemOperationState.Completed : ItemOperationState.PartiallyCompleted, moved));
                        if (moved == 0) RememberNoCapacity(current.Request.Sources[0].Identity, current.Player.position);
                        pending = null; return;
                    }
                }
                if (!current.Invalid && !current.TimedOut && unchecked(Tick - current.Started) >= 600)
                {
                    current.TimedOut = true;
                    ownership.FinishStore(current.Request.Session, Result(ItemOperationState.TimedOut, "source-replies-not-complete"));
                    // Stop deadline work. Keep only this finite source group for
                    // actual late replies; no resend and no forced native unlock.
                }
            }
        }
        internal void EndSession() { lock (receiptGate) { pending = null; capacityWaits.Clear(); } }
        internal void InvalidateCapacity()
        { lock (receiptGate) { if (capacityWaits.Count == 0) return; capacityWaits.Clear(); world.InvalidateObservation(); } }
        private void RememberNoCapacity(ItemIdentity identity, Vector2 position)
        {
            // A short backoff follows an actual zero-transfer result, including
            // complete authoritative client replies. Unknown local chest content
            // never creates it. New pickups coalesce while waiting; this stores
            // neither a permanent permission nor a quantity to replay.
            if (capacityWaits.Count == 64) capacityWaits.Clear();
            ulong until = Tick + 120;
            capacityWaits[identity] = new CapacityWait(position, until);
            if (capacityWaits.Count == 1 || until < nextCapacityExpiry) nextCapacityExpiry = until;
        }
        private readonly struct CapacityWait
        {
            internal readonly Vector2 Position;
            internal readonly ulong Until;
            internal CapacityWait(Vector2 position, ulong until) { Position = position; Until = until; }
        }
        private static int Signed(byte[] data, int offset) { return unchecked((short)(data[offset] | data[offset + 1] << 8)); }
        private static long Quantity(IEnumerable<Chest> targets, ItemIdentity identity)
        {
            long total = 0;
            foreach (Chest chest in targets) foreach (Item item in chest.item)
                if (item != null && item.type == identity.Type && item.prefix == identity.Prefix) total = checked(total + item.stack);
            return total;
        }
        private static ItemOperationResult Result(ItemOperationState state, string reason) { return new ItemOperationResult(state, reason: reason); }
        internal readonly struct Receipt
        {
            internal readonly Pending Batch;
            internal readonly int Index, Type, Quantity;
            internal readonly byte Prefix;
            internal Receipt(Pending batch, int index, int type, byte prefix, int quantity)
            { Batch = batch; Index = index; Type = type; Prefix = prefix; Quantity = quantity; }
        }
        internal sealed class Pending
        {
            internal readonly StoreItemsRequest Request;
            internal readonly Player Player;
            internal readonly int OriginalQuantity;
            internal readonly ulong Started;
            internal readonly bool[] Received;
            internal readonly int[] Remaining;
            internal bool Changed, Invalid, TimedOut;
            internal Pending(StoreItemsRequest request, Player player, int originalQuantity, ulong started)
            { Request = request; Player = player; OriginalQuantity = originalQuantity; Started = started;
                Received = new bool[request.Sources.Count]; Remaining = new int[request.Sources.Count]; }
        }
    }
}
