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
        private static object supplyTraceHost,supplyTraceTools;
        private static bool supplyTraceDiscovery;
        private static bool supplyTraceFinalize;
        private static int supplyTraceFinalizeCount;
        private sealed class SupplyColorTrace : IDisposable
        {
            private readonly Harmony patch=new Harmony("JueMingR.Tests.SupplyColorHandoffTrace");
            internal SupplyColorTrace(object host,object tools)
            {
                supplyTraceHost=host;supplyTraceTools=tools;supplyTraceFinalizeCount=0;
                patch.Patch(host.GetType().GetMethod("ReturnPreparedBuildLease",F),prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(SupplyTraceReturnBefore)),
                    postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(SupplyTraceReturnAfter)));
                patch.Patch(host.GetType().GetMethod("ContinueBuildSupplyDiscovery",F),prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(SupplyTraceDiscoveryBefore)),
                    postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(SupplyTraceDiscoveryAfter)));
                patch.Patch(host.GetType().GetMethod("TryPreferredBuild",F),postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(SupplyTracePreferredAfter)));
                patch.Patch(host.GetType().GetMethod("TryPlanBuildSupply",F),postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(SupplyTracePlanAfter)));
                patch.Patch(host.GetType().GetMethod("TryGetBuildTarget",F),postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(SupplyTraceTargetAfter)));
                patch.Patch(host.GetType().GetMethod("FinalizeReadyBuildSupply",F),prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(SupplyTraceFinalizeBefore)),
                    postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(SupplyTraceFinalizeAfter)));
                foreach(string name in new[]{"CanBuildAutomatically","PreferredBuildAbsenceCurrent","BuildFocusNearCurrent","TryPrepareAppearanceBuild","BuildFlightCurrent"})
                    patch.Patch(host.GetType().GetMethod(name,F),postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(SupplyTraceFinalizeGate)));
                patch.Patch(Get(host,"buildMaterialPreparation").GetType().GetMethod("ReadyTicketCurrent",F),
                    postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(SupplyTraceFinalizeGate)));
            }
            public void Dispose(){foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyPatchType.All,patch.Id);supplyTraceHost=supplyTraceTools=null;supplyTraceDiscovery=supplyTraceFinalize=false;}
        }
        private static void SupplyTraceFinalizeBefore(object __instance,Player __0)
        {
            supplyTraceFinalize=ReferenceEquals(__instance,supplyTraceHost) && (bool)Get(Get(__instance,"buildMaterialPreparation"),"Ready") &&
                GetOptional(__instance,"preparedBuild")==null && supplyTraceFinalizeCount<8;
            if(!supplyTraceFinalize)return;
            supplyTraceFinalizeCount++;
            var target=GetOptional(__instance,"buildSupplyTarget");var operation=target==null?null:GetOptional(target,"Operation");
            Console.WriteLine("SupplyTrace actual-finalize world="+Main.GameUpdateCount+" timers="+__0.itemAnimation+"/"+__0.itemTime+"/"+__0.reuseDelay+
                " release/override/buffer/cursor/inventory="+__0.releaseUseItem+"/"+__0.selectedItemState.HasActiveOverride+"/"+__0.selectedItemState.HasBufferedChange+"/"+Main.mouseItem.type+"/"+Main.playerInventory+
                " target/plan/readyFlight="+(target!=null)+"/"+(GetOptional(__instance,"buildSupplyPlan")!=null)+"/"+Get(__instance,"buildSupplyReadyFlight")+
                " operation="+(operation==null?"none":Get(operation,"Kind")+":"+Get(operation,"SupplyType")));
        }
        private static void SupplyTraceFinalizeGate(System.Reflection.MethodBase __originalMethod,bool __result)
        {
            if(!supplyTraceFinalize)return;
            Console.WriteLine("SupplyTrace actual-finalize-gate "+__originalMethod.Name+"="+__result);
            if(__originalMethod.Name!="PreferredBuildAbsenceCurrent" || __result)return;
            var group=GetOptional(supplyTraceHost,"buildContinuation");if(group==null)return;
            foreach(var range in (System.Collections.IEnumerable)Get(Get(group,"Discovery"),"Ranges"))
            {
                var value=GetOptional(range,"Materials");if(value==null)continue;
                Console.WriteLine("SupplyTrace actual-absence range="+Get(range,"Index")+" global="+Get(supplyTraceHost,"buildSupplyRevision")+
                    " proofs="+string.Join("/",new[]{"PreferredSupply","PreferredWaits","PreferredRevisit","PreferredFrontier","PreferredVersion","PreferredWake","BuildWakeRevision","PreferredSupport","PreferredRetryAt"}.Select(n=>n+":"+Get(value,n))));
            }
        }
        private static void SupplyTraceFinalizeAfter(object __instance)
        {
            if(supplyTraceFinalize)Console.WriteLine("SupplyTrace actual-finalized prepared="+(GetOptional(__instance,"preparedBuild")!=null));
            supplyTraceFinalize=false;
        }
        private static void SupplyTraceReturnBefore(object __instance,object __0,bool __1)
        {
            if(!ReferenceEquals(__instance,supplyTraceHost))return;
            var prep=Get(__instance,"buildMaterialPreparation");var operation=GetOptional(__0,"Operation");
            object[] read={Main.LocalPlayer,operation==null?0:(int)Get(operation,"SupplyType"),operation==null?-1:(int)Get(operation,"SupplyDestination"),-1};
            bool supply=(bool)Call(prep,"TryReadSupply",read);var next=GetOptional(__instance,"preparedBuild");
            Console.WriteLine("SupplyTrace return world="+Main.GameUpdateCount+" checked/settled/rejected/unknown="+Get(__0,"Checked")+"/"+Get(__0,"SettledConsumption")+"/"+Get(__0,"Rejected")+"/"+__1+
                " can/group/unconfirmed="+Call(__instance,"CanBuildAutomatically",Main.LocalPlayer,false)+"/"+Call(__instance,"BuildContinuationCurrent",Main.LocalPlayer)+"/"+Call(__instance,"IsUnconfirmedTarget",Get(__0,"Target"))+
                " sourceRead/slot="+supply+"/"+read[3]+" next="+(next==null?"none":Get(next,"Generation")+":"+Get(Get(next,"Target"),"WorldX"))+
                " cancelled="+Get(Get(supplyTraceTools,"Use"),"cancelled"));
        }
        private static void SupplyTraceReturnAfter(object __instance)
        {if(ReferenceEquals(__instance,supplyTraceHost))Console.WriteLine("SupplyTrace returned phase="+Get(Get(__instance,"buildMaterialPreparation"),"phase")+" discovery="+(GetOptional(__instance,"buildSupplyDiscovery")!=null));}
        private static void SupplyTraceDiscoveryBefore(object __instance)
        {supplyTraceDiscovery=ReferenceEquals(__instance,supplyTraceHost);if(supplyTraceDiscovery)Console.WriteLine("SupplyTrace discovery world="+Main.GameUpdateCount+" can/group="+Call(__instance,"CanBuildAutomatically",Main.LocalPlayer,false)+"/"+Call(__instance,"BuildContinuationCurrent",Main.LocalPlayer));}
        private static void SupplyTracePreferredAfter(object __instance,object __result)
        {if(supplyTraceDiscovery && ReferenceEquals(__instance,supplyTraceHost))Console.WriteLine("SupplyTrace preferred="+__result);}
        private static void SupplyTraceDiscoveryAfter(object __instance)
        {if(supplyTraceDiscovery && ReferenceEquals(__instance,supplyTraceHost))Console.WriteLine("SupplyTrace discovery-end phase="+Get(Get(__instance,"buildMaterialPreparation"),"phase"));supplyTraceDiscovery=false;}
        private static void SupplyTracePlanAfter(object __instance,int __2,object __5,bool __result)
        {if(ReferenceEquals(__instance,supplyTraceHost) && __2==ItemID.BluePaint && !__result)Console.WriteLine("SupplyTrace blue-plan phase="+Get(Get(__instance,"buildMaterialPreparation"),"phase")+" wait="+__5+" tick="+Get(__instance,"materialTick"));}
        private static void SupplyTraceTargetAfter(object __instance,bool __result)
        {
            if(!ReferenceEquals(__instance,supplyTraceHost) || __result || !(bool)Get(Get(__instance,"buildMaterialPreparation"),"Ready"))return;
            foreach(var owner in Scene(__instance))foreach(var range in EligibilityRanges(owner))
            {
                var value=GetOptional(range,"Materials");if(value==null)continue;
                Console.WriteLine("SupplyTrace no-target tick="+Get(__instance,"materialTick")+" waits="+Get(value,"BuildWaits")+" dynamicAt="+Get(value,"DynamicRetryAt")+
                    " exhausted="+Get(value,"BuildExhausted")+" retry="+Get(value,"BuildRetryPending")+" candidate="+(GetOptional(value,"Candidate")!=null));
            }
        }
        private static void SupplyColorHandoff(object context,object host,object tools,object input,bool diagnostic=false)
        {
            using(var probe=new PostProcessingProbe(context,host,tools,input))
            using(var timeline=new SchedulerTimeline(host,tools,input))
            using(var trace=diagnostic?new SupplyColorTrace(host,tools):null)
            {
                var p=Reset(context,host,tools,input);p.position=new Vector2(42*16,646);
                p.inventory[12].SetDefaults(ItemID.Paintbrush);var brush=p.inventory[12];
                p.inventory[20].SetDefaults(ItemID.RedPaint);p.inventory[20].stack=5;var red=p.inventory[20];
                p.inventory[21].SetDefaults(ItemID.BluePaint);p.inventory[21].stack=3;var blue=p.inventory[21];
                p.inventory[54].SetDefaults(ItemID.BluePaint);p.inventory[54].stack=7;var original=p.inventory[54];
                var goals=Enumerable.Range(0,4).Select(i=>PostProcessingTarget("TilePaint",44+i,42,i==1?blue.paint:red.paint,i==1?ItemID.BluePaint:ItemID.RedPaint)).ToArray();
                foreach(var goal in goals)NativeToolsChecks.Tile(goal.X,goal.Y,TileID.Stone);
                probe.Goals=goals;probe.ToolItems=new[]{brush};probe.Supplies=new[]{red,blue};probe.TraceSupply=true;
                PostProcessingPlace(context,host,input,goals);probe.Advance();probe.Begin(true);
                var preparation=Get(host,"buildMaterialPreparation");long neutral=(long)Get(preparation,"NativeNeutralTails");
                SetAutomatic(host,input,true);
                NativeQuickItemChecks.Until(()=>{probe.Advance();return goals.All(g=>g.Complete) && !(bool)Get(preparation,"HasWork") && !(bool)Get(Get(tools,"Use"),"Active");});
                probe.Report("supply-leftover-red-to-blue");timeline.Report("supply-leftover-red-to-blue");
                var a=probe.Events.TakeWhile(e=>e.SupplyType==ItemID.RedPaint).ToArray();var b=probe.Events.Last();
                Console.WriteLine("SupplyColorHandoff firstA="+(a.Length>0?a[0].World:0)+" lastA="+(a.Length>0?a.Last().World:0)+" firstB="+b.World+
                    " gap="+(a.Length>0?unchecked(b.World-a.Last().World):0)+" conversionClicks="+(a.Length>0?b.PreparationClicks-a.Last().PreparationClicks:-1)+
                    " totalNeutralIncludingPrepareReturn="+((long)Get(preparation,"NativeNeutralTails")-neutral));
                Require(probe.Events.Count==4 && a.Length==3 && b.SupplyType==ItemID.BluePaint && ReferenceEquals(b.Supply,blue) &&
                    a.All(e=>ReferenceEquals(e.Supply,red) && e.PreparationClicks==a[0].PreparationClicks) &&
                    red.stack==2 && blue.stack==2 && original.stack==7 && ReferenceEquals(p.inventory[20],red) &&
                    ReferenceEquals(p.inventory[21],blue) && ReferenceEquals(p.inventory[54],original) && Main.mouseItem.IsAir,
                    "leftover-color handoff consumes only the two exact main-bag sources and restores the untouched original54 by native inputs");
                Require(b.PreparationClicks-a.Last().PreparationClicks==5,"the normal leftover-red to blue path uses exactly five real conversion clicks");
            }
        }
        private static void SupplyConsumerHandoff(object context,object host,object tools,object input)
        {
            var automatic=SupplyConsumerHandoffRun(context,host,tools,input,null);
            var original=SupplyConsumerHandoffRun(context,host,tools,input,automatic);
            uint actual=unchecked(automatic[1].World-automatic[0].World),expected=unchecked(original[1].World-original[0].World);
            Console.WriteLine("SupplyConsumerHandoff actualGap="+actual+" originalGap="+expected+
                " extraMaterialClicks="+(automatic[1].PreparationClicks-automatic[0].PreparationClicks)+
                " tokens="+automatic[0].Token+"/"+automatic[1].Token+" flights="+automatic[0].Flight+"/"+automatic[1].Flight);
            Require(actual==expected,"same prepared paint brush-to-roller uses the first original legal consumer opportunity");
            Require(automatic[0].PreparationClicks==automatic[1].PreparationClicks && automatic[0].Token!=automatic[1].Token &&
                automatic[0].Flight!=automatic[1].Flight && automatic[0].Kind=="TilePaint" && automatic[1].Kind=="WallPaint" &&
                automatic[0].ToolType==ItemID.Paintbrush && automatic[1].ToolType==ItemID.PaintRoller,
                "the real material receipt crosses consumers with zero extra click and newly authorized ToolUse/flight");
        }
        private static PostProcessingEvent[] SupplyConsumerHandoffRun(object context,object host,object tools,object input,PostProcessingEvent[] replay)
        {
            bool automatic=replay==null;
            using(var probe=new PostProcessingProbe(context,host,tools,input))
            using(var timeline=new SchedulerTimeline(host,tools,input))
            {
                var p=Reset(context,host,tools,input);p.position=new Vector2(42*16,646);
                p.inventory[12].SetDefaults(ItemID.Paintbrush);p.inventory[13].SetDefaults(ItemID.PaintRoller);
                var brush=p.inventory[12];var roller=p.inventory[13];
                p.inventory[20].SetDefaults(ItemID.RedPaint);p.inventory[20].stack=5;var red=p.inventory[20];
                p.inventory[54].SetDefaults(ItemID.BluePaint);p.inventory[54].stack=7;var original=p.inventory[54];
                var goals=new[]{PostProcessingTarget("TilePaint",44,42,red.paint,ItemID.RedPaint),
                    PostProcessingTarget("WallPaint",45,42,red.paint,ItemID.RedPaint)};
                NativeToolsChecks.Tile(44,42,TileID.Stone);Main.tile[45,42].wall=WallID.Stone;
                probe.Goals=goals;probe.ToolItems=new[]{brush,roller};probe.Supplies=new[]{red};
                PostProcessingPlace(context,host,input,goals);probe.Advance();
                var preparation=Get(host,"buildMaterialPreparation");
                if(!automatic)
                {
                    Call(preparation,"TryPrepare",new object[]{p,20,54,LifecycleIdentity(tools),-1});
                    NativeQuickItemChecks.Until(()=>{probe.Advance();return (bool)Get(preparation,"Ready");});
                    // The reference trajectory starts from the very same real
                    // prepared layout, handed to the player by actual UI calls.
                    p.ToggleInv();p.ToggleInv();
                    Require(!(bool)Get(preparation,"HasWork") && ReferenceEquals(p.inventory[54],red),
                        "the original reference owns the same actually prepared red via native inventory takeover");
                }
                probe.Begin(automatic);if(automatic)SetAutomatic(host,input,true);
                for(int frame=0;frame<1000 && !goals.All(g=>g.Complete);frame++)
                {
                    if(automatic)probe.Advance();
                    else
                    {
                        var target=replay[probe.Events.Count];int slot=target.ToolType==brush.type?12:13;
                        if(p.selectedItem!=slot)
                        {
                            if(!p.selectedItemState.HasBufferedChange)p.selectedItemState.Select(slot);
                            if(!p.selectedItemState.CanChangeSelectedItemImmediately){probe.Advance();continue;}
                        }
                        probe.Advance(new Vector2(target.X*16+8,target.Y*16+8),p.itemAnimation>0 || p.releaseUseItem || p.inventory[slot].autoReuse);
                    }
                }
                probe.Report("supply-cross-consumer automatic="+automatic);timeline.Report("supply-cross-consumer automatic="+automatic);
                Require(probe.Events.Count==2 && goals.All(g=>g.Complete) && red.stack==3 && original.stack==7 &&
                    probe.Events.All(e=>ReferenceEquals(e.Supply,red)) && ReferenceEquals(probe.Events[0].Tool,brush) && ReferenceEquals(probe.Events[1].Tool,roller),
                    "both actual native paint consumers use the exact same red remainder and correct real tools without spending original54");
                var result=probe.Events.ToArray();
                if(automatic)NativeQuickItemChecks.Until(()=>{probe.Advance();return !(bool)Get(preparation,"HasWork") && !(bool)Get(Get(tools,"Use"),"Active");});
                else
                {
                    NativeQuickItemChecks.Until(()=>{probe.Advance();return p.itemAnimation==0 && p.itemTime==0 && !p.selectedItemState.HasBufferedChange;});
                    if(!Main.playerInventory)p.ToggleInv();
                    ManualPreparationClick(context,input,p,54);ManualPreparationClick(context,input,p,20);ManualPreparationClick(context,input,p,54);
                    // Deliver the old54 object through a real main-bag slot
                    // before returning it to54, retiring its manual provenance
                    // guard before another fixture may reuse live Item objects.
                    ManualPreparationClick(context,input,p,54);ManualPreparationClick(context,input,p,22);
                    ManualPreparationClick(context,input,p,22);ManualPreparationClick(context,input,p,54);
                    if(Main.playerInventory)p.ToggleInv();
                }
                Require(ReferenceEquals(p.inventory[20],red) && ReferenceEquals(p.inventory[54],original) && Main.mouseItem.IsAir,
                    "automatic and original reference trajectories both finish with genuine reference-preserving native return");
                return result;
            }
        }
    }
}
