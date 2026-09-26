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
                CurrentInventorySalesAndDiscards();
                StorageRequiresReliableSource();
                FiniteMembersDoNotAdoptWithdrawals();
                InputLifetimeAndRetirement();
                FallbackAndRemainingGroup();
                FeedbackDoesNotChangeOpportunity();
                RepeatedLowSlotDoesNotStarve();
                var port = new Boundary();
                var feature = new ItemAutomationFeature(port, port);
                feature.OnSessionStarted();
                port.Set(false, Slot(0, 9, 20));
                feature.Configure(ItemAutomationSettings.Default.WithEnabled(ItemActionKind.Stack, true));
                feature.Update(0);
                Require(port.Calls.Count == 0, "enable/withdrawal alone must not invent acquisition");
                port.Set(false, Slot(54, 9, 23), Slot(2, 9, 7, true));
                Acquire(feature, port, 1);
                feature.Update(1);
                Require(port.Calls.Count == 1 && port.Calls[0] == "store:54:23", "new source permits eligible old plus new total, per-stack favorite protected");
                feature.Update(100);
                Require(port.Calls.Count == 1, "one acquisition cannot authorize forever retry");
                var settings = ItemAutomationSettings.Default.WithTypes(ItemListKind.Sell, new[] { 9 })
                    .WithTypes(ItemListKind.Discard, new[] { 9 }).WithEnabled(ItemActionKind.Stack, true)
                    .WithEnabled(ItemActionKind.Sell, true).WithEnabled(ItemActionKind.Discard, true);
                feature.Configure(settings);
                port.Set(false, Slot(3, 9, 2, false, 1));
                Acquire(feature, port, 101);
                feature.Update(101);
                Require(port.Calls[1] == "discard:3:2", "closed shop falls through to discard, including non-stackable input");
                port.Set(true, Slot(4, 9, 3));
                Acquire(feature, port, 107);
                feature.Update(107);
                Require(port.Calls[2] == "sell:4:3", "open shop takes current priority for source-qualified inventory");
                port.Next = new ItemOperationResult(ItemOperationState.Unconfirmed);
                port.Set(true, Slot(5, 9, 4)); Acquire(feature, port, 113); feature.Update(113); feature.Update(119);
                Require(port.Calls.Count == 4 && port.Calls[3] == "sell:5:4", "unknown sale must not become a retry or discard of unchanged source");
                port.Set(true, Slot(5, 9, 4), Slot(6, 10, 17)); feature.Update(125);
                feature.Configure(settings.WithEnabled(ItemActionKind.Sell, false)); feature.Update(126);
                Require(port.Calls.Count == 4, "unrelated inventory revision or disabling sale cannot release unknown resource ownership");
                feature.Configure(settings.WithEnabled(ItemActionKind.Sell, false).WithEnabled(ItemActionKind.Discard, false));
                Acquire(feature, port, 127);
                ItemInventoryObservation oldSession = port.Observation;
                feature.OnSessionEnded(); port.SessionGeneration = 2; feature.OnSessionStarted();
                port.Set(false, Slot(4, 9, 23)); feature.Update(121);
                Require(port.Calls.Count == 4, "old-session acquisition cannot replay");
                feature.RegisterAcquisitions(new[] { new ItemIdentity(9, 0) }, oldSession, 122); feature.Update(122);
                Require(port.Calls.Count == 4, "late origin with old generation rejected");
                foreach (int coin in new[] { 71, 72, 73, 74 })
                    Require(!Slot(0, coin, 50).IsCandidate, "every coin excluded independently of lists");
                Require(!Slot(58, 9, 1).IsCandidate && !Slot(51, 9, 1).IsCandidate, "mouse and illegal coin slot excluded");
            }
            catch (Exception e) { failures.Add("item automation decisions: " + e.Message); }
        }
        private static void CurrentInventorySalesAndDiscards()
        {
            foreach (ItemActionKind action in new[] { ItemActionKind.Sell, ItemActionKind.Discard })
            {
                var port = new Boundary(); var feature = new ItemAutomationFeature(port, port);
                var settings = ItemAutomationSettings.Default.WithEnabled(action, true)
                    .WithTypes(ItemListKind.Sell, new[] { 9 }).WithTypes(ItemListKind.Discard, new[] { 9 });
                feature.Configure(settings); feature.OnSessionStarted();
                port.Set(true, Slot(1, 9, 20), Slot(2, 9, 8, true), Slot(3, 10, 6));
                feature.Update(0);
                Require(port.Calls.Count == 1 && port.Calls[0].EndsWith(":1:20"), "current listed inventory needs no acquisition: " + action);
                for (ulong tick = 1; tick < 600; tick++) feature.Update(tick);
                Require(port.Calls.Count == 1 && port.ObservationReads == 100, "stable inventory has bounded cadence and no repeated unchanged attempt");
                port.Set(true, Slot(1, 9, 7), Slot(2, 9, 8, true)); feature.Update(600);
                Require(port.Calls.Count == 2 && port.Calls[1].EndsWith(":1:7"), "later withdrawal is eligible current stock, not reuse of acquisition");
                port.Set(true, Slot(2, 9, 8)); feature.Update(606);
                Require(port.Calls.Count == 3 && port.Calls[2].EndsWith(":2:8"), "removing favorite makes current listed inventory eligible");
                feature.Configure(settings.WithEnabled(action, false)); int reads = port.ObservationReads;
                feature.Update(612); Require(port.ObservationReads == reads, "disabled processing does not observe inventory");
                feature.Configure(settings.WithTypes(ItemListKind.Sell, new int[0]).WithTypes(ItemListKind.Discard, new int[0]));
                feature.Update(613); Require(port.ObservationReads == reads, "empty lists without stack demand do not observe inventory");
            }
            var boundary = new Boundary(); var selector = new ItemAutomationFeature(boundary, boundary);
            selector.Configure(new ItemAutomationSettings(true, true, true, new[] { 9 }, new[] { 9 })); selector.OnSessionStarted();
            boundary.Set(true, Slot(1, 9, 20)); selector.Update(0);
            Require(boundary.Calls.Count == 1 && boundary.Calls[0] == "sell:1:20", "current inventory retains sale priority");
            boundary.Set(false, Slot(1, 9, 7)); selector.Update(6);
            Require(boundary.Calls.Count == 2 && boundary.Calls[1] == "discard:1:7", "closed shop permits listed discard without acquisition");
            boundary.Set(false, Slot(1, 10, 5)); selector.Update(12);
            Require(boundary.Calls.Count == 2, "inventory scan never fabricates stack acquisition");
        }
        private static void StorageRequiresReliableSource()
        {
            foreach (ItemActionKind action in new[] { ItemActionKind.Stack })
            {
                var port = new Boundary();
                var feature = new ItemAutomationFeature(port, port);
                feature.OnSessionStarted();
                var settings = ItemAutomationSettings.Default.WithTypes(ItemListKind.Sell, new[] { 9 })
                    .WithTypes(ItemListKind.Discard, new[] { 9 }).WithEnabled(action, true);
                feature.Configure(settings); port.Set(true, Slot(1, 9, 20)); feature.Update(0);
                Require(port.Calls.Count == 0, "lists/enabling/shop must not authorize old inventory: " + action);
                port.Set(true, Slot(1, 9, 23));
                Acquire(feature, port, 1); feature.Update(1);
                Require(port.Calls.Count == 1 && port.Calls[0].EndsWith(":1:23"), "only-enabled action receives whole-stack source: " + action);
                // Refill before the next observation: the old opportunity must
                // already have ended when its final member completed.
                port.Set(true, Slot(1, 9, 20)); feature.Update(7);
                Require(port.Calls.Count == 1, "completed source cannot lend permission to immediate withdrawal: " + action);
                feature.Configure(settings.WithEnabled(action, false));
                Acquire(feature, port, 8);
                feature.Configure(settings); feature.Update(8);
                Require(port.Calls.Count == 1, "all-off acquisition cannot replay on enable: " + action);
            }
        }
        private static void FiniteMembersDoNotAdoptWithdrawals()
        {
            var port = new Boundary(); var feature = new ItemAutomationFeature(port, port);
            feature.OnSessionStarted();
            var settings = ItemAutomationSettings.Default.WithEnabled(ItemActionKind.Stack, true);
            feature.Configure(settings);
            port.Set(false, Slot(1, 9, 10), Slot(2, 9, 13), Slot(3, 9, 8, true));
            Acquire(feature, port, 0);
            Require(port.Ownership.TryBeginUse(1, 2, 1), "temporary second member protection admitted");
            feature.Update(0); port.Ownership.EndUse(1, 1);
            port.Set(false, Slot(1, 9, 40), Slot(2, 9, 13), Slot(3, 9, 8, true)); feature.Update(6);
            Require(port.Calls.Count == 2 && port.Calls[1] == "store:2:13", "remaining original member survives; consumed slot refill is excluded from storage");
            port.Set(false, Slot(1, 9, 40), Slot(3, 9, 8)); feature.Update(12);
            Require(port.Calls.Count == 2, "favorite-only remainder cannot keep a completed opportunity alive");
            // Re-enabling storage is not another acquisition.
            feature.Configure(settings.WithEnabled(ItemActionKind.Stack, false));
            Acquire(feature, port, 13); feature.Update(13);
            feature.Configure(settings); feature.Update(14);
            Require(port.Calls.Count == 2, "no-target/list-change cannot resurrect old members");
        }
        private static void FallbackAndRemainingGroup()
        {
            var port = new Boundary { Next = new ItemOperationResult(ItemOperationState.NotApplicable) };
            var feature = new ItemAutomationFeature(port, port);
            feature.OnSessionStarted();
            var settings = ItemAutomationSettings.Default.WithTypes(ItemListKind.Sell, new[] { 9 })
                .WithEnabled(ItemActionKind.Sell, true).WithEnabled(ItemActionKind.Stack, true);
            feature.Configure(settings);
            port.Set(true, Slot(0, 9, 23)); Acquire(feature, port, 0); feature.Update(0);
            Require(port.Calls.Count == 2 && port.Calls[1] == "store:0:23", "actual not-applicable sale must reach legal storage");
            port.Next = new ItemOperationResult(ItemOperationState.Completed);
            port.Set(true, Slot(0, 9, 10), Slot(1, 9, 13)); Acquire(feature, port, 10); feature.Update(10);
            Require(port.Calls[port.Calls.Count-1]=="sell:1:13","fair cursor continues after the prior slot zero attempt");
            port.Set(false, Slot(0, 9, 10)); feature.Update(16);
            Require(port.Calls[port.Calls.Count - 1] == "store:0:10", "selling one source stack must not retire the other eligible stack's acquisition");
        }
        private static void InputLifetimeAndRetirement()
        {
            bool ready = false;
            var port = new Boundary(); var feature = new ItemAutomationFeature(port, port, () => ready);
            var settings = ItemAutomationSettings.Default.WithEnabled(ItemActionKind.Stack, true);
            feature.Configure(settings); feature.OnSessionStarted();
            port.Set(false, Slot(1, 9, 23)); Acquire(feature, port, 0); feature.Update(1);
            Require(port.Calls.Count == 0, "source capture does not bypass current input permission");
            feature.Configure(settings.WithDiscardFeedbackEnabled(false));
            ready = true; feature.Update(2);
            Require(port.Calls.Count == 1, "actual feedback change preserves pending source while input is temporarily closed");
            ready = false; port.Set(false, Slot(1, 9, 10)); Acquire(feature, port, 10);
            port.Set(false, Slot(1, 9, 11)); Acquire(feature, port, 500);
            ready = true; feature.Update(610);
            Require(port.Calls.Count == 1, "repeated genuine gains do not extend first pending source age");
            foreach (bool replacement in new[] { false, true })
            {
                port.Set(false, Slot(1, 9, 10)); Acquire(feature, port, 620);
                port.Set(false, replacement ? new ItemSlotObservation(1, new ItemIdentity(9, 0), 10, 9999, 999, false) : Slot(1, 9, 11));
                feature.Update(621);
                Require(port.Calls.Count == 1, "unexplained quantity growth or equal-value replacement cancels association");
            }
            foreach (ItemOperationState state in new[] { ItemOperationState.Executing, ItemOperationState.Unconfirmed, ItemOperationState.PartiallyCompleted, ItemOperationState.NotApplicable })
            {
                var boundary = new Boundary { Next = new ItemOperationResult(state) };
                var selector = new ItemAutomationFeature(boundary, boundary);
                selector.Configure(ItemAutomationSettings.Default.WithEnabled(ItemActionKind.Stack, true)); selector.OnSessionStarted();
                boundary.Set(false, Slot(1, 9, 23)); Acquire(selector, boundary, 0); selector.Update(0);
                // Isolate permission retirement from the independently tested
                // resource owner: even releasing ownership cannot revive it.
                boundary.Ownership.SetSession(2); boundary.Ownership.SetSession(1);
                boundary.Set(false, Slot(1, 9, 23)); selector.Update(6);
                Require(boundary.Calls.Count == 1, "submitted member retired independently of resource/result lifetime: " + state);
            }
        }
        private static void FeedbackDoesNotChangeOpportunity()
        {
            var port = new Boundary { Next = new ItemOperationResult(ItemOperationState.Rejected) };
            var feature = new ItemAutomationFeature(port, port);
            var settings = ItemAutomationSettings.Default.WithEnabled(ItemActionKind.Stack, true);
            feature.Configure(settings); feature.OnSessionStarted(); port.Set(false, Slot(1, 9, 23));
            Acquire(feature, port, 0); feature.Update(0);
            Require(port.Calls.Count == 1, "initial rejected attempt is visible to this boundary");
            int reads = port.ObservationReads;
            feature.Configure(settings.WithDiscardFeedbackEnabled(false)); feature.Update(1);
            Require(port.ObservationReads == reads, "feedback change cannot wake an otherwise idle observation cadence");
            feature.Update(6);
            Require(port.Calls.Count == 1, "feedback change cannot reset attempted revision into repeated rejection");
            port.Next = new ItemOperationResult(ItemOperationState.Completed);
            port.Set(false, Slot(1, 9, 23), Slot(2, 10, 1)); feature.Update(12);
            Require(port.Calls.Count == 2, "feedback change preserves actual pending member until legitimate observation changes");
            port.Set(false, Slot(1, 9, 23)); feature.Configure(settings); feature.Update(18);
            Require(port.Calls.Count == 2, "feedback re-enable cannot resurrect the completed opportunity");
        }
        private static void RepeatedLowSlotDoesNotStarve()
        {
            var port=new Boundary();var feature=new ItemAutomationFeature(port,port);
            feature.Configure(new ItemAutomationSettings(false,false,true,new int[0],new[]{9,10}));feature.OnSessionStarted();
            port.Set(false,Slot(1,9,1),Slot(49,10,23));
            feature.RegisterAcquisitions(new[]{new ItemIdentity(9,0)},port.Observation,0);feature.Update(0);
            for(ulong tick=1;tick<4;tick++)
            {
                port.Set(false,Slot(1,9,1),Slot(49,10,23));
                feature.RegisterAcquisitions(new[]{new ItemIdentity(9,0)},port.Observation,tick);feature.Update(tick);
            }
            Require(port.Calls.Contains("discard:49:23"),"repeated low-slot native gains must not starve old high-slot inventory with no acquisition");
        }
        private static void Acquire(ItemAutomationFeature feature, Boundary port, ulong tick)
        { feature.RegisterAcquisitions(new[] { new ItemIdentity(9, 0) }, port.Observation, tick); }
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
            internal ItemInventoryObservation Observation { get { return snapshot; } }
            private long revision;
            internal int ObservationReads;
            public long SessionGeneration { get; set; } = 1;
            internal void Set(bool shop, params ItemSlotObservation[] slots)
            { Ownership.SetSession(SessionGeneration); snapshot = new ItemInventoryObservation(SessionGeneration, ++revision, shop, shop ? 1 : 0, slots); }
            public bool TryObserve(out ItemInventoryObservation observation) { ObservationReads++; observation = snapshot; return snapshot != null; }
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
