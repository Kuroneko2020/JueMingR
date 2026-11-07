using System;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.UI;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static object conversionCancelOwner;
        private static int conversionCancelAfter;
        private static void ConversionCancelInsideClick()
        {
            if(conversionCancelOwner==null || !(bool)Get(conversionCancelOwner,"NativeMutation"))return;
            if(--conversionCancelAfter!=0)return;
            Call(conversionCancelOwner,"RequestReturn");
            Require((bool)Get(conversionCancelOwner,"tailOpen") && (bool)Get(conversionCancelOwner,"returnRequested"),
                "ordinary cancellation inside original LeftClick retains the unconfirmed tail before reversing");
            conversionCancelOwner=null;
        }

        private static void SupplyConversionPrograms(object context,object host,object tools,object input)
        {
            var patch=new Harmony("JueMingR.Tests.SupplyConversionPrefixes");
            patch.Patch(AccessTools.Method(typeof(ItemSlot),"LeftClick",new[]{typeof(Item[]),typeof(int),typeof(int)}),
                postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(ConversionCancelInsideClick)));
            try
            {
                foreach(int steps in new[]{5,3,4})
                {
                    for(int prefix=0;prefix<=steps;prefix++)
                        SupplyConversionPrefix(context,host,tools,input,steps,prefix,false);
                    SupplyConversionPrefix(context,host,tools,input,steps,Math.Min(3,steps),true);
                }
                SupplyConversionRounds(context,host,tools,input);
                SupplyConversionManualSelection(context,host,tools,input);
                SupplyConversionPresentation(context,host,tools,input);
                SupplyDeferredReceiptRelease(context,host,tools,input);
            }
            finally
            {
                conversionCancelOwner=null;
                foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyPatchType.All,patch.Id);
            }
        }

        private static Player SupplyConversionStart(object context,object host,object tools,object input,bool full,
            out object preparation,out Item red,out Item blue,out Item green,out Item original,out Item q)
        {
            var p=Reset(context,host,tools,input);preparation=Get(host,"buildMaterialPreparation");
            if(full)for(int slot=0;slot<50;slot++){p.inventory[slot].SetDefaults(ItemID.Wood);p.inventory[slot].stack=1;}
            p.inventory[20].SetDefaults(ItemID.RedPaint);p.inventory[20].stack=5;red=p.inventory[20];
            p.inventory[21].SetDefaults(ItemID.BluePaint);p.inventory[21].stack=3;blue=p.inventory[21];
            p.inventory[22].SetDefaults(ItemID.GreenPaint);p.inventory[22].stack=4;green=p.inventory[22];
            p.inventory[54].SetDefaults(ItemID.BluePaint);p.inventory[54].stack=7;original=p.inventory[54];q=p.inventory[0];
            Step(context,input);
            Call(preparation,"TryPrepare",new object[]{p,20,54,LifecycleIdentity(tools),-1});
            var receipt=preparation;
            Until(context,input,()=>(bool)Get(receipt,"Ready"));
            Require(ReferenceEquals(p.inventory[54],red) && ReferenceEquals(p.inventory[20],original) && Main.mouseItem.IsAir,
                "conversion starts from the real three-click prepared red receipt with displaced original blue");
            return p;
        }

        private static void SupplyConversionPrefix(object context,object host,object tools,object input,int program,int prefix,bool inside)
        {
            object preparation;Item red,blue,green,original,q;
            var p=SupplyConversionStart(context,host,tools,input,program==4,out preparation,out red,out blue,out green,out original,out q);
            if(program!=5)
            {
                Require((bool)Call(preparation,"TryConvertSupply",p,21,LifecycleIdentity(tools)),"first finite conversion is admitted");
                Until(context,input,()=>(bool)Get(preparation,"Ready"));
            }
            long clicks=(long)Get(preparation,"NativeClicks"),neutral=(long)Get(preparation,"NativeNeutralTails");
            int source=program==5?21:22;
            Require((bool)Call(preparation,"TryConvertSupply",p,source,LifecycleIdentity(tools)),"chosen exact next material admits its finite native program");
            Require((int)Get(preparation,"forwardCount")==program,"the actual selected layout uses the specified 5/3/4 native clicks");
            if(inside){conversionCancelOwner=preparation;conversionCancelAfter=prefix;}
            if(prefix>0)Until(context,input,()=>(long)Get(preparation,"NativeClicks")>=clicks+prefix);
            if(!inside)Call(preparation,"RequestReturn");
            Until(context,input,()=>!(bool)Get(preparation,"HasWork"));
            Require(ReferenceEquals(p.inventory[20],red) && red.stack==5 && ReferenceEquals(p.inventory[21],blue) && blue.stack==3 &&
                ReferenceEquals(p.inventory[22],green) && green.stack==4 && ReferenceEquals(p.inventory[54],original) && original.stack==7 &&
                (q.IsAir?p.inventory[0].IsAir:ReferenceEquals(p.inventory[0],q) && q.stack==1) && Main.mouseItem.IsAir,
                "every confirmed conversion prefix and in-click cancellation returns all exact material/displaced/nonempty-buffer identities and counts");
            Console.WriteLine("PASS SupplyConversion prefix program="+program+" prefix="+prefix+" insideClick="+inside+
                " actualClicksIncludingReturn="+((long)Get(preparation,"NativeClicks")-clicks)+" neutralIncludingReturn="+((long)Get(preparation,"NativeNeutralTails")-neutral));
        }

        private static void SupplyConversionRounds(object context,object host,object tools,object input)
        {
            object preparation;Item red,blue,green,original,q;
            var p=SupplyConversionStart(context,host,tools,input,false,out preparation,out red,out blue,out green,out original,out q);
            foreach(int source in new[]{21,22,20,21,22,20})
            {
                long clicks=(long)Get(preparation,"NativeClicks"),neutral=(long)Get(preparation,"NativeNeutralTails");
                Require((bool)Call(preparation,"TryConvertSupply",p,source,LifecycleIdentity(tools)),"each new exact source can reuse the finite displaced stack isolation");
                int steps=(int)Get(preparation,"forwardCount");
                Until(context,input,()=>(bool)Get(preparation,"Ready"));
                Require((long)Get(preparation,"NativeClicks")==clicks+steps && (int)Get(preparation,"endpointCount")==3 &&
                    (int)Get(preparation,"historyCount")==4 && original.stack==7 && ReferenceEquals(p.inventory[0],original),
                    "each native conversion folds to three endpoints/four cleanup steps without growing history or consuming original54");
                Console.WriteLine("PASS SupplyConversion stableRound source="+source+" clicks="+steps+" neutral="+((long)Get(preparation,"NativeNeutralTails")-neutral)+" endpoints=3 cleanup=4");
            }
            Call(preparation,"RequestReturn");Until(context,input,()=>!(bool)Get(preparation,"HasWork"));
            Require(ReferenceEquals(p.inventory[20],red) && ReferenceEquals(p.inventory[21],blue) && ReferenceEquals(p.inventory[22],green) &&
                ReferenceEquals(p.inventory[54],original) && red.stack==5 && blue.stack==3 && green.stack==4 && original.stack==7 && Main.mouseItem.IsAir,
                "many finite conversions return all original references and unchanged stacks after one final cleanup");
        }
        private static void SupplyConversionManualSelection(object context,object host,object tools,object input)
        {
            object preparation;Item red,blue,green,original,q;
            var p=SupplyConversionStart(context,host,tools,input,false,out preparation,out red,out blue,out green,out original,out q);
            long clicks=(long)Get(preparation,"NativeClicks");
            Require((bool)Call(preparation,"TryConvertSupply",p,21,LifecycleIdentity(tools)),"manual takeover representative starts the real finite conversion");
            Until(context,input,()=>(long)Get(preparation,"NativeClicks")==clicks+2);
            clicks=(long)Get(preparation,"NativeClicks");
            var slots=p.inventory.Take(58).ToArray();var counts=slots.Select(i=>i.stack).ToArray();var cursor=Main.mouseItem;
            long selection=(long)Get(tools,"SelectionIntent");p.selectedItemState.Select(2);p.selectedItemState.Update();
            Require((long)Get(tools,"SelectionIntent")>selection,"actual Select reaches the hidden carried conversion's original manual handoff");
            for(int i=0;i<8;i++)Step(context,input);
            Require(!(bool)Get(preparation,"HasWork") && (long)Get(preparation,"NativeClicks")==clicks && ReferenceEquals(Main.mouseItem,cursor) &&
                Enumerable.Range(0,58).All(i=>ReferenceEquals(p.inventory[i],slots[i]) && p.inventory[i].stack==counts[i]),
                "manual selection takes the exact carried layout immediately, without another forward or inverse click");
            // The player explicitly returns O through a main-bag delivery;
            // its guard is retired by that actual command before reuse.
            ManualPreparationClick(context,input,p,54);
            ManualPreparationClick(context,input,p,54);ManualPreparationClick(context,input,p,23);
            ManualPreparationClick(context,input,p,23);ManualPreparationClick(context,input,p,54);
            if(Main.playerInventory)p.ToggleInv();
            Require(ReferenceEquals(p.inventory[20],red) && ReferenceEquals(p.inventory[21],blue) && ReferenceEquals(p.inventory[54],original) && Main.mouseItem.IsAir,
                "only the explicit new manual commands restore the abandoned conversion layout");
        }
        private static object conversionPresentationOwner;
        private static Item[] conversionPresentationOriginals;
        private static Item conversionPresentationCursor;
        private static bool conversionPresentationObserved;
        private static bool ConversionPresentationEntry()
        {
            var p=Main.LocalPlayer;var live=p.inventory;
            var view=(Item[])Call(conversionPresentationOwner,"PresentHotbarInventory",new object[]{live});
            Require(!Main.playerInventory && Main.MouseDisplayItem.IsAir && ReferenceEquals(Main.mouseItem,conversionPresentationCursor) &&
                !ReferenceEquals(view,live) && new[]{0,20,21,54}.All(slot=>ReferenceEquals(view[slot],conversionPresentationOriginals[slot])) &&
                ReferenceEquals(p.inventory,live),
                "actual interface scope maps all four conversion endpoints and hides only the real owned cursor without replacing inventory");
            conversionPresentationObserved=true;return false;
        }
        private static void SupplyConversionPresentation(object context,object host,object tools,object input)
        {
            using(var probe=new PostProcessingProbe(context,host,tools,input))
            {
                var p=Reset(context,host,tools,input);p.position=new Vector2(42*16,646);
                for(int slot=0;slot<50;slot++){p.inventory[slot].SetDefaults(ItemID.Wood);p.inventory[slot].stack=1;}
                p.inventory[0].SetDefaults(ItemID.Paintbrush);var brush=p.inventory[0];
                p.inventory[20].SetDefaults(ItemID.RedPaint);p.inventory[20].stack=5;var red=p.inventory[20];
                p.inventory[21].SetDefaults(ItemID.BluePaint);p.inventory[21].stack=3;var blue=p.inventory[21];
                p.inventory[54].SetDefaults(ItemID.BluePaint);p.inventory[54].stack=7;var original=p.inventory[54];
                conversionPresentationOriginals=p.inventory.ToArray();
                var goals=new[]{PostProcessingTarget("TilePaint",44,42,red.paint,ItemID.RedPaint),PostProcessingTarget("TilePaint",45,42,blue.paint,ItemID.BluePaint)};
                foreach(var goal in goals)NativeToolsChecks.Tile(goal.X,goal.Y,TileID.Stone);
                probe.Goals=goals;probe.ToolItems=new[]{brush};probe.Supplies=new[]{red,blue};
                PostProcessingPlace(context,host,input,goals);probe.Advance();probe.Begin(true);SetAutomatic(host,input,true);
                var preparation=Get(host,"buildMaterialPreparation");
                NativeQuickItemChecks.Until(()=>{probe.Advance();return (bool)Get(preparation,"converting") &&
                    (int)Get(preparation,"forwardCursor")==3 && !Main.mouseItem.IsAir;});
                Require((int)Get(preparation,"endpointCount")==4 && ReferenceEquals(Main.mouseItem,brush),
                    "the real first five-click conversion carries the exact slot0 brush from its fourth endpoint");
                conversionPresentationOwner=preparation;conversionPresentationCursor=Main.mouseItem;conversionPresentationObserved=false;
                var draw=typeof(Main).GetMethod("DrawInterface",F,null,new[]{typeof(GameTime)},null);
                var target=(Main)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Main));GC.SuppressFinalize(target);
                var patch=new Harmony("JueMingR.Tests.ConversionPresentation");
                patch.Patch(draw,prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(ConversionPresentationEntry)){priority=Priority.Last});
                try{draw.Invoke(target,new object[]{new GameTime()});}
                finally{foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyPatchType.All,patch.Id);}
                Require(conversionPresentationObserved && Main.playerInventory && ReferenceEquals(Main.mouseItem,brush),
                    "four-endpoint actual interface exit restores the owned logical inventory and cursor");
                NativeQuickItemChecks.Until(()=>{probe.Advance();return (bool)Get(preparation,"Ready");});
                var live=p.inventory;var ready=(Item[])Call(preparation,"PresentHotbarInventory",new object[]{live});
                Require((int)Get(preparation,"endpointCount")==3 && ReferenceEquals(live[21],brush) && ReferenceEquals(live[0],original) &&
                    ReferenceEquals(live[54],blue) && ReferenceEquals(ready[0],brush) && ReferenceEquals(ready[21],blue) && ReferenceEquals(ready[54],original) &&
                    ReferenceEquals(live[20],red) && ReferenceEquals(ready[20],red),
                    "folded three-endpoint Ready view keeps original tool/material identities while the restored old source is released");
                NativeQuickItemChecks.Until(()=>{probe.Advance();return probe.Events.Count==2;});
                var view=(Item[])Call(preparation,"PresentHotbarInventory",new object[]{live});
                int shown=(int)Call(preparation,"PresentHotbarSelection",p.selectedItem);
                var held=(Item)Call(preparation,"PresentHotbarHeldItem",p.HeldItem);
                bool temporary=(bool)Call(preparation,"PresentHotbarOverride",p.selectedItemState.HasActiveOverride);
                Require((bool)Get(preparation,"Using") && p.selectedItem==21 && shown==0 && temporary && ReferenceEquals(held,brush) &&
                    ReferenceEquals(view[shown],brush) && ReferenceEquals(view[21],blue) && view[21].stack==2 &&
                    ReferenceEquals(probe.Events[1].Tool,brush) && probe.Events[1].ToolSlot==21 && ReferenceEquals(live,p.inventory),
                    "actual new ToolUse selects migrated brush21 while title/highlight/extra-slot consumers show original brush0 and current blue remainder");
                NativeQuickItemChecks.Until(()=>{probe.Advance();return !(bool)Get(preparation,"HasWork") && !(bool)Get(Get(tools,"Use"),"Active");});
                Require(ReferenceEquals(p.inventory[0],brush) && ReferenceEquals(p.inventory[20],red) && red.stack==4 && ReferenceEquals(p.inventory[21],blue) &&
                    blue.stack==2 && ReferenceEquals(p.inventory[54],original) && original.stack==7 && Main.mouseItem.IsAir,
                    "original native final cleanup restores the moved brush and both exact remainders after display scopes");
                Console.WriteLine("PASS SupplyConversionPresentation fourthEndpoint=true foldedEndpoints=3 actualTool=21 displayedTool=0 exactRemainder=true fullDrawPixels=NOT_RUN");
            }
            conversionPresentationOwner=null;conversionPresentationOriginals=null;conversionPresentationCursor=null;
        }
        private static void SupplyDeferredReceiptRelease(object context,object host,object tools,object input)
        {
            var patch=new Harmony("JueMingR.Tests.SupplyDeferredReceipt");
            var p=Reset(context,host,tools,input);var preparation=Get(host,"buildMaterialPreparation");
            p.inventory[12].SetDefaults(ItemID.Paintbrush);
            p.inventory[13].SetDefaults(ItemID.WoodWall);p.inventory[13].stack=3;
            p.inventory[20].SetDefaults(ItemID.RedPaint);p.inventory[20].stack=4;var red=p.inventory[20];
            p.inventory[54].SetDefaults(ItemID.BluePaint);p.inventory[54].stack=7;var original=p.inventory[54];
            Place(context,host,input,Asset(host,new CaptureCell{Layers=CaptureLayer.Wall,WallType=WallID.Wood}),44,40);
            var right=Main.tile[45,40];
            try
            {
                Main.tile[45,40]=null;
                patch.Patch(host.GetType().GetMethod("AdvanceAutomaticBuild",F),prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(FreezeSupplyDiscoveryAdvance)));
                supplyDiscoveryFrozen=true;SetAutomatic(host,input,true);
                object target=null;
                NativeQuickItemChecks.Until(()=>
                {
                    Step(context,input);object[] chosen={null};bool found=(bool)Call(host,"TryGetBuildTarget",chosen);
                    if(found)target=chosen[0];return found;
                });
                Call(preparation,"TryPrepare",new object[]{p,20,54,LifecycleIdentity(tools),-1});
                Until(context,input,()=>(bool)Get(preparation,"Ready"));
                Require(GetOptional(host,"buildSupplyTarget")==null && GetOptional(host,"buildSupplyDiscovery")==null && target!=null,
                    "the selection boundary starts with a real exact Ready receipt and no remaining bound successor/discovery duty");
                schedulerDeferredSelection=new SchedulerDeferredSelectionObservation{Host=host};
                patch.Patch(host.GetType().GetMethod("TryPlanBuild",F),prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(SchedulerDeferredPlanBefore)),
                    postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(SchedulerDeferredPlanAfter)));
                patch.Patch(host.GetType().GetMethod("ReadBuildCell",F),postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(SchedulerDeferredCellAfter)));
                object[] selection={p,64};Call(host,"AdvanceAutomaticBuildSelection",selection);
                Require(schedulerDeferredSelection.DeferredCalls>0 && schedulerDeferredSelection.UnavailableRightReads>0 &&
                    (bool)Get(preparation,"Returning") && Main.tile[45,40]==null && p.inventory[13].stack==3,
                    "ordinary selection really returns Deferred from an unavailable native support read and starts finite receipt return in that same boundary");
                supplyDiscoveryFrozen=false;
                Until(context,input,()=>!(bool)Get(preparation,"HasWork"));
                Require(ReferenceEquals(p.inventory[20],red) && red.stack==4 && ReferenceEquals(p.inventory[54],original) && original.stack==7 && Main.mouseItem.IsAir,
                    "source waiting never prolongs an unbound Ready receipt or invents paint consumption");
                Console.WriteLine("PASS SupplyDeferredReceipt realUnavailableSupport=true deferredCalls="+schedulerDeferredSelection.DeferredCalls+" sameBoundaryReturn=true");
            }
            finally
            {
                supplyDiscoveryFrozen=false;schedulerDeferredSelection=null;
                foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyPatchType.All,patch.Id);
                Main.tile[45,40]=right;SetAutomatic(host,input,false);
            }
        }
    }
}
