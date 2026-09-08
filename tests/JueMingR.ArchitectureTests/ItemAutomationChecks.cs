using System;
using System.Collections.Generic;
using JueMingR.Features.Items;
using JueMingR.Platform.Items;

namespace JueMingR.ArchitectureTests
{
    internal static class ItemAutomationChecks
    {
        internal static void Check(IList<string> failures)
        {
            try
            {
                FallbackAndRemainingGroup();
                var port = new Boundary();
                var feature = new ItemAutomationFeature(port, port);
                feature.OnSessionStarted();
                port.Set(false, Slot(0, 9, 20));
                feature.Configure(ItemAutomationSettings.Default.WithEnabled(ItemActionKind.Stack, true));
                feature.Update(0);
                Require(port.Calls.Count == 0, "enable/withdrawal alone must not invent acquisition");
                feature.RegisterAcquisition(new ItemIdentity(9, 0), 1, 1);
                port.Set(false, Slot(54, 9, 23), Slot(2, 9, 7, true));
                feature.Update(1);
                Require(port.Calls.Count == 1 && port.Calls[0] == "store:54:23", "new source permits eligible old plus new total, per-stack favorite protected");
                feature.Update(100);
                Require(port.Calls.Count == 1, "one acquisition cannot authorize forever retry");
                var settings = ItemAutomationSettings.Default.WithTypes(ItemListKind.Sell, new[] { 9 })
                    .WithTypes(ItemListKind.Discard, new[] { 9 }).WithEnabled(ItemActionKind.Stack, true)
                    .WithEnabled(ItemActionKind.Sell, true).WithEnabled(ItemActionKind.Discard, true);
                feature.Configure(settings);
                port.Set(false, Slot(3, 9, 2, false, 1));
                feature.Update(101);
                Require(port.Calls[1] == "discard:3:2", "closed shop falls through to discard, including non-stackable input");
                port.Set(true, Slot(4, 9, 3));
                feature.Update(107);
                Require(port.Calls[2] == "sell:4:3", "open shop takes current priority for existing inventory");
                port.Next = new ItemOperationResult(ItemOperationState.Unconfirmed);
                port.Set(true, Slot(5, 9, 4)); feature.Update(113); feature.Update(119);
                Require(port.Calls.Count == 4 && port.Calls[3] == "sell:5:4", "unknown sale must not become a retry or discard of unchanged source");
                port.Set(true, Slot(5, 9, 4), Slot(6, 10, 17)); feature.Update(125);
                feature.Configure(settings.WithEnabled(ItemActionKind.Sell, false)); feature.Update(126);
                Require(port.Calls.Count == 4, "unrelated inventory revision or disabling sale cannot release unknown resource ownership");
                feature.Configure(settings.WithEnabled(ItemActionKind.Sell, false).WithEnabled(ItemActionKind.Discard, false));
                feature.RegisterAcquisition(new ItemIdentity(9, 0), 1, 120);
                feature.OnSessionEnded(); port.SessionGeneration = 2; feature.OnSessionStarted();
                port.Set(false, Slot(4, 9, 23)); feature.Update(121);
                Require(port.Calls.Count == 4, "old-session acquisition cannot replay");
                feature.RegisterAcquisition(new ItemIdentity(9, 0), 1, 122); feature.Update(122);
                Require(port.Calls.Count == 4, "late origin with old generation rejected");
                foreach (int coin in new[] { 71, 72, 73, 74 })
                    Require(!Slot(0, coin, 50).IsCandidate, "every coin excluded independently of lists");
                Require(!Slot(58, 9, 1).IsCandidate && !Slot(51, 9, 1).IsCandidate, "mouse and illegal coin slot excluded");
            }
            catch (Exception e) { failures.Add("item automation decisions: " + e.Message); }
        }
        private static void FallbackAndRemainingGroup()
        {
            var port = new Boundary { Next = new ItemOperationResult(ItemOperationState.NotApplicable) };
            var feature = new ItemAutomationFeature(port, port);
            feature.OnSessionStarted();
            var settings = ItemAutomationSettings.Default.WithTypes(ItemListKind.Sell, new[] { 9 })
                .WithEnabled(ItemActionKind.Sell, true).WithEnabled(ItemActionKind.Stack, true);
            feature.Configure(settings);
            feature.RegisterAcquisition(new ItemIdentity(9, 0), 1, 0);
            port.Set(true, Slot(0, 9, 23)); feature.Update(0);
            Require(port.Calls.Count == 2 && port.Calls[1] == "store:0:23", "actual not-applicable sale must reach legal storage");
            port.Next = new ItemOperationResult(ItemOperationState.Completed);
            feature.RegisterAcquisition(new ItemIdentity(9, 0), 1, 10);
            port.Set(true, Slot(0, 9, 10), Slot(1, 9, 13)); feature.Update(10);
            port.Set(false, Slot(1, 9, 13)); feature.Update(16);
            Require(port.Calls[port.Calls.Count - 1] == "store:1:13", "selling one source stack must not retire the other eligible stack's acquisition");
        }
        private static ItemSlotObservation Slot(int slot, int type, int count, bool protect = false, int max = 9999)
        { return new ItemSlotObservation(slot, new ItemIdentity(type, 0), count, max, slot + 1, protect); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        // This boundary only tests the real selector's input and state transitions.
        // It is not a simulated transaction, resource-conservation or network test.
        private sealed class Boundary : IItemObservationSource, IItemOperationPort
        {
            internal readonly List<string> Calls = new List<string>();
            internal ItemOperationResult Next = new ItemOperationResult(ItemOperationState.Completed);
            public ItemOperationOwnership Ownership { get; } = new ItemOperationOwnership();
            private ItemInventoryObservation snapshot;
            private long revision;
            public long SessionGeneration { get; set; } = 1;
            internal void Set(bool shop, params ItemSlotObservation[] slots)
            { Ownership.SetSession(SessionGeneration); snapshot = new ItemInventoryObservation(SessionGeneration, ++revision, shop, shop ? 1 : 0, slots); }
            public bool TryObserve(out ItemInventoryObservation observation) { observation = snapshot; return snapshot != null; }
            public ItemOperationResult Execute(SellItemRequest r)
            {
                if (!Ownership.TryBeginSale(r.Session)) throw new InvalidOperationException("selector retried conflicting sale");
                Calls.Add("sell:" + r.Source.Slot + ":" + r.Source.Stack); Ownership.FinishSale(r.Session, Next); return Next;
            }
            public ItemOperationResult Execute(DiscardItemRequest r)
            {
                if (!Ownership.TryBeginDiscard(r.Session, r.Source.Slot)) throw new InvalidOperationException("selector retried conflicting discard");
                Calls.Add("discard:" + r.Source.Slot + ":" + r.Source.Stack); Ownership.FinishDiscard(r.Session, Next); return Next;
            }
            public ItemOperationResult Execute(StoreItemsRequest r)
            {
                ulong slots = 0; foreach (ItemSlotObservation slot in r.Sources) slots |= 1UL << slot.Slot;
                if (!Ownership.TryBeginStore(r.Session, slots)) throw new InvalidOperationException("selector retried conflicting store");
                Calls.Add("store:" + r.Sources[0].Slot + ":" + r.Sources[0].Stack); Ownership.FinishStore(r.Session, Next); return Next;
            }
        }
    }
}
