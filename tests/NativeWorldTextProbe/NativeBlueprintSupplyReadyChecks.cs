using System;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static object supplyReadyHost,supplyReadyTools,supplyReadyPreparation;
        private static bool supplyReadyTracking;
        private static int supplyReadyOpportunities;
        private static void SupplyReadyBeforePick(Player __instance,out bool __state)
        {
            __state=supplyReadyTracking && ReferenceEquals(__instance,Main.LocalPlayer) && (bool)Get(supplyReadyPreparation,"Ready") &&
                !(bool)Get(Get(supplyReadyTools,"Use"),"Active") && (bool)Get(Get(supplyReadyTools,"Input"),"CanRunAutomaticActions") &&
                __instance.itemAnimation==0 && __instance.itemTime==0 && __instance.reuseDelay==0 && !__instance.channel && __instance.releaseUseItem &&
                !__instance.selectedItemState.HasActiveOverride && !__instance.selectedItemState.HasBufferedChange &&
                Main.mouseItem.IsAir && !Main.playerInventory;
            if(__state)supplyReadyOpportunities++;
        }
        [HarmonyPriority(Priority.Last)]
        private static void SupplyReadyAfterPick(bool __state,bool __result)
        {
            if(!__state)return;
            Console.WriteLine("SupplyReady firstLegalPick world="+Main.GameUpdateCount+" acquired="+Get(Get(supplyReadyTools,"Use"),"Building")+
                " opportunities="+supplyReadyOpportunities+" epoch="+Get(supplyReadyPreparation,"ReceiptEpoch"));
            Require(__result && (bool)Get(Get(supplyReadyTools,"Use"),"Building") && GetOptional(supplyReadyHost,"activeBuild")!=null,
                "the first genuine original Pick after Ready and legal native timers acquires the exact new flight before another Host postfix");
        }
        private static void SupplyReadyEntrance(object context,object host,object tools,object input)
        {
            var patch=new Harmony("JueMingR.Tests.SupplyReadyPick");
            patch.Patch(typeof(Player).GetMethod("PickItemSelectionOverride",F),
                prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(SupplyReadyBeforePick)),
                postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(SupplyReadyAfterPick)));
            supplyReadyHost=host;supplyReadyTools=tools;supplyReadyPreparation=Get(host,"buildMaterialPreparation");
            try
            {
                foreach(bool invalidate in new[]{false,true})
                using(var probe=new PostProcessingProbe(context,host,tools,input))
                {
                    var p=Reset(context,host,tools,input);p.position=new Vector2(42*16,646);
                    p.inventory[12].SetDefaults(ItemID.Paintbrush);
                    p.inventory[20].SetDefaults(ItemID.RedPaint);p.inventory[20].stack=5;var red=p.inventory[20];
                    p.inventory[54].SetDefaults(ItemID.BluePaint);p.inventory[54].stack=7;var original=p.inventory[54];
                    NativeToolsChecks.Tile(44,42,TileID.Stone);
                    var goal=PostProcessingTarget("TilePaint",44,42,red.paint,ItemID.RedPaint);
                    probe.Goals=new[]{goal};probe.ToolItems=new[]{p.inventory[12]};probe.Supplies=new[]{red};
                    PostProcessingPlace(context,host,input,probe.Goals);probe.Advance();probe.Begin(true);SetAutomatic(host,input,true);
                    NativeQuickItemChecks.Until(()=>{probe.Advance();return (bool)Get(supplyReadyPreparation,"Ready");});
                    Require(probe.Events.Count==0 && GetOptional(host,"preparedBuild")==null,
                        "the actual last original Draw input tail publishes Ready before any world effect or postfix flight");
                    long clicks=(long)Get(supplyReadyPreparation,"NativeClicks");
                    supplyReadyTracking=true;supplyReadyOpportunities=0;
                    if(invalidate)p.ToggleInv();
                    if(!invalidate)
                    {
                        NativeQuickItemChecks.Until(()=>{probe.Advance();return goal.Complete;});
                        Require(supplyReadyOpportunities==1 && probe.Events.Count==1 && red.stack==4 && original.stack==7,
                            "Ready finalization and the following postfix cannot publish or consume a second flight");
                    }
                    else
                    {
                        for(int i=0;i<8;i++)probe.Advance();
                        Require(supplyReadyOpportunities==0 && !goal.Complete && probe.Events.Count==0 &&
                            (long)Get(supplyReadyPreparation,"NativeClicks")==clicks && red.stack==5 && original.stack==7,
                            "real inventory takeover between Ready and Pick revokes the old ticket without consumption or inverse click");
                        SetAutomatic(host,input,false);
                        ManualPreparationClick(context,input,p,54);ManualPreparationClick(context,input,p,20);ManualPreparationClick(context,input,p,54);
                        ManualPreparationClick(context,input,p,54);ManualPreparationClick(context,input,p,22);
                        ManualPreparationClick(context,input,p,22);ManualPreparationClick(context,input,p,54);
                        if(Main.playerInventory)p.ToggleInv();
                    }
                    supplyReadyTracking=false;
                    Console.WriteLine("PASS SupplyReady invalidated="+invalidate+" legalPicks="+supplyReadyOpportunities+" actualConsumers="+probe.Events.Count);
                }
            }
            finally
            {
                supplyReadyTracking=false;supplyReadyHost=supplyReadyTools=supplyReadyPreparation=null;
                foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyPatchType.All,patch.Id);
            }
        }
    }
}
