using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JueMingR.Features.Items;
using JueMingR.Platform.Items;

namespace JueMingR.ArchitectureTests
{
    internal static class FishingStorageChecks
    {
        internal static void Check(List<string> failures)
        {
            try
            {
                var port=new Boundary();var feature=new ItemAutomationFeature(port,port);feature.OnSessionStarted();
                var configure=typeof(ItemAutomationFeature).GetMethod("SetFishingStorage");var acquire=typeof(ItemAutomationFeature).GetMethod("RegisterFishingAcquisitions");
                Require(configure!=null && acquire!=null,"single item selector must own dedicated fishing storage");
                Action<long,int,int,bool> scope=(token,mode,quest,run)=>configure.Invoke(feature,new object[]{1L,token,mode,quest,run});
                Action<long> capture=token=>acquire.Invoke(feature,new object[]{token,new[]{new ItemIdentity(2290,0)},port.Inventory,0UL});
                port.Set(Slot(1,2290,8),Slot(2,2290,12),Slot(3,2290,1,1,1),Slot(4,2290,5,protect:true),Slot(5,2291,10));
                scope(1,1,0,true);Require(feature.Enabled,"fish-only demand enables shared owner");capture(1);feature.Update(0);feature.Update(6);
                Require(port.Stores.Count==2 && port.Stores[0].Sources.Select(s=>s.Slot).SequenceEqual(new[]{1,2}) && port.Stores[1].Sources.Single().Slot==3,"actual caught type includes eligible old stacks across separate prefixes, excludes protected and unrelated items");
                Require(!(bool)typeof(StoreItemsRequest).GetProperty("RequireStackable").GetValue(port.Stores[1]),"dedicated fish may store maxStack one without relaxing ordinary requests");
                port.Set(Slot(1,2290,8));feature.Update(12);Require(port.Stores.Count==2,"completed old member cannot revive from polling");
                scope(2,1,0,true);capture(1);feature.Update(18);Require(port.Stores.Count==2,"late previous fishing session product rejected");
                capture(2);scope(2,0,0,false);feature.Update(24);Require(port.Stores.Count==2 && !feature.Enabled,"off retires unsubmitted fish-only work");
                scope(3,2,2290,true);feature.Update(30);Require(port.Stores.Count==3,"quest mode evaluates current old inventory without fabricated acquisition");
                feature.Update(36);Require(port.Stores.Count==3,"same inventory quest snapshot does not resend");
                port.Set(Slot(1,2290,9),Slot(2,2290,3));scope(3,2,0,true);feature.Update(42);Require(port.Stores.Count==3,"completed or unavailable current quest gives no eligibility");
                scope(4,1,0,false);capture(4);feature.Update(48);Require(port.Stores.Count==3,"paused fishing intent does not execute");scope(4,1,0,true);feature.Update(54);Require(port.Stores.Count==4,"retained finite fish intent resumes after admission opens");
                foreach(bool sell in new[]{true,false})
                {
                    var next=new Boundary();var selector=new ItemAutomationFeature(next,next);selector.Configure(new ItemAutomationSettings(true,sell,true,new[]{2290},new[]{2290}));selector.OnSessionStarted();
                    next.Set(Slot(1,2290,10));configure.Invoke(selector,new object[]{1L,1L,1,0,true});acquire.Invoke(selector,new object[]{1L,new[]{new ItemIdentity(2290,0)},next.Inventory,0UL});selector.Update(0);
                    Require(next.Stores.Count==0 && (sell?next.Sales==1:next.Discards==1),"sale then discard remain ahead of dedicated fish storage");
                }
                foreach(int outcome in new[]{0,1,2,3})
                {
                    var next=new Boundary{Sale=outcome==0?ItemOperationState.Completed:outcome==3?ItemOperationState.Unconfirmed:ItemOperationState.NotApplicable,Discard=outcome==1?ItemOperationState.Completed:ItemOperationState.NotApplicable};
                    var selector=new ItemAutomationFeature(next,next);selector.Configure(new ItemAutomationSettings(true,true,true,new[]{2290},new[]{2290}));selector.OnSessionStarted();next.Set(Slot(1,2290,10));
                    selector.SetFishingStorage(1,1,1,0,true);var identities=new[]{new ItemIdentity(2290,0)};
                    selector.RegisterAcquisitions(identities,next.Inventory,0);selector.RegisterFishingAcquisitions(1,identities,next.Inventory,0);selector.Update(0);
                    Require(next.Sales==1 && next.Discards==(outcome==1 || outcome==2?1:0) && next.Stores.Count==(outcome==2?1:0),"dual eligibility uses one shared sale/discard/store decision and unknown never falls through");
                    selector.Configure(new ItemAutomationSettings(true,false,false,new int[0],new int[0]));selector.SetFishingStorage(1,1,0,0,false);next.Set(Slot(1,2290,10));selector.Update(6);
                    Require(next.Stores.Count==(outcome==2?1:0),"submitted dual member retires both acquisition permissions; disabling fish cannot expose an old ordinary replay");
                }
                ItemAutomationChecks.Check(failures);
                ulong simulation=0;var clockPort=new Boundary();var clocked=new ItemAutomationFeature(clockPort,clockPort,fishingClock:()=>simulation);clocked.OnSessionStarted();clockPort.Set(Slot(1,2290,5));clocked.SetFishingStorage(1,1,1,0,false);clocked.RegisterFishingAcquisitions(1,new[]{new ItemIdentity(2290,0)},clockPort.Inventory,1000);
                for(ulong outer=1001;outer<2000;outer++)clocked.Update(outer);
                simulation=599;clocked.SetFishingStorage(1,1,1,0,true);clocked.Update(2000);Require(clockPort.Stores.Count==1,"empty outer callbacks do not consume the finite simulation-time fish opportunity");
            }
            catch(Exception error){failures.Add("Fishing storage: "+error);}
        }
        private static ItemSlotObservation Slot(int slot,int type,int stack,int maximum=9999,byte prefix=0,bool protect=false)
        {return new ItemSlotObservation(slot,new ItemIdentity(type,prefix),stack,maximum,slot+1,protect);}
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        // Input/decision boundary only. Native conservation is checked separately.
        private sealed class Boundary:IItemObservationSource,IItemOperationPort
        {
            internal readonly List<StoreItemsRequest> Stores=new List<StoreItemsRequest>();
            internal int Sales,Discards;
            internal ItemOperationState Sale=ItemOperationState.Completed,Discard=ItemOperationState.Completed;
            internal ItemInventoryObservation Inventory;
            private long revision;
            public long SessionGeneration {get{return 1;}}
            public ItemOperationOwnership Ownership {get;}=new ItemOperationOwnership();
            internal void Set(params ItemSlotObservation[] slots){Ownership.SetSession(1);Inventory=new ItemInventoryObservation(1,++revision,true,1,slots);}
            public bool TryObserve(out ItemInventoryObservation value){value=Inventory;return value!=null;}
            public ItemOperationResult Execute(StoreItemsRequest request){Stores.Add(request);return new ItemOperationResult(ItemOperationState.Completed);}
            public ItemOperationResult Execute(SellItemRequest request){Sales++;return new ItemOperationResult(Sale);}
            public ItemOperationResult Execute(DiscardItemRequest request){Discards++;return new ItemOperationResult(Discard);}
        }
    }
}
