using System;
using System.Linq;
using HarmonyLib;
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
    }
}
