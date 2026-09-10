using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using JueMingR.Platform.Operations;

namespace JueMingR.Platform.Items
{
    public enum ItemOperationState { NotApplicable, Rejected, Executing, Completed, PartiallyCompleted, Failed, Cancelled, TimedOut, Unconfirmed }

    public sealed class SellItemRequest : IGameOperationRequest
    {
        public long Session { get; }
        public long ShopIdentity { get; }
        public ItemSlotObservation Source { get; }
        public SellItemRequest(long session, long shopIdentity, ItemSlotObservation source)
        { Session = session; ShopIdentity = shopIdentity; Source = source; }
    }
    public sealed class DiscardItemRequest : IGameOperationRequest
    {
        public long Session { get; }
        public ItemSlotObservation Source { get; }
        public DiscardItemRequest(long session, ItemSlotObservation source) { Session = session; Source = source; }
    }
    public sealed class StoreItemsRequest : IGameOperationRequest
    {
        public long Session { get; }
        public ReadOnlyCollection<ItemSlotObservation> Sources { get; }
        public StoreItemsRequest(long session, IEnumerable<ItemSlotObservation> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            var copy = new List<ItemSlotObservation>();
            var slots = new HashSet<int>();
            foreach (ItemSlotObservation source in sources)
            {
                if (copy.Count == 58 || !slots.Add(source.Slot) || !source.IsCandidate || source.MaximumStack <= 1)
                    throw new ArgumentException("invalid-store-input");
                copy.Add(source);
            }
            if (copy.Count == 0) throw new ArgumentException("empty-store-input");
            Session = session; Sources = copy.AsReadOnly();
        }
    }
    public sealed class ItemOperationResult
    {
        public ItemOperationState State { get; }
        public int ConfirmedQuantity { get; }
        public long ConfirmedCopper { get; }
        public string Reason { get; }
        public ItemOperationResult(ItemOperationState state, int confirmedQuantity = 0, long confirmedCopper = 0, string reason = null)
        {
            if (state < ItemOperationState.NotApplicable || state > ItemOperationState.Unconfirmed || confirmedQuantity < 0 || confirmedCopper < 0)
                throw new ArgumentOutOfRangeException(nameof(state));
            State = state; ConfirmedQuantity = confirmedQuantity; ConfirmedCopper = confirmedCopper; Reason = reason;
        }
    }
    // Typed exits have distinct actual resource ranges. The Host owns pending
    // native receipts and conflict checks; business priority never lives here.
    public interface IItemOperationPort
    {
        ItemOperationOwnership Ownership { get; }
        ItemOperationResult Execute(SellItemRequest request);
        ItemOperationResult Execute(DiscardItemRequest request);
        ItemOperationResult Execute(StoreItemsRequest request);
    }
}
