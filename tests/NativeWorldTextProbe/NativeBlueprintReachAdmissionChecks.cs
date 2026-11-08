using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private sealed class QueueReachAdmission
        {
            internal object Host,Use;
            internal PostProcessingProbe Probe;
            internal PostProcessingGoal Restored;
            internal Item Paint,LongTool;
            internal bool Before,Written;
            internal int ForeignAdmissions;
            internal long WriterHost;
        }
        private static QueueReachAdmission queueReachAdmission;
        private static bool QueueLongBrushRunnable(QueueReachAdmission observation)
        {
            var p=Main.LocalPlayer;var goal=observation.Restored;var tool=observation.LongTool;
            return tool!=null && !tool.IsAir && Array.IndexOf(p.inventory,tool)>=0 && observation.Paint.stack>9000 && !goal.Complete &&
                Main.tile[goal.X,goal.Y].active() && Main.tile[goal.X,goal.Y].type==TileID.Stone &&
                p.IsInTileInteractionRange(goal.X,goal.Y,TileReachCheckSettings.Simple,tool.tileBoost+p.blockRange);
        }
        private static void QueueAdmitLongBrush(QueueReachAdmission observation,string stage)
        {
            Require(!observation.Written && observation.Probe.Events.Count==1 && !observation.Restored.Complete,
                "the true first A consumer is complete while its short-tool-rejected successor still waits before the controlled native acquisition");
            long clock=(long)Get(observation.Host,"nearCapabilityClock");
            observation.WriterHost=checked((long)(ulong)Get(observation.Host,"projectionMatchTick"));
            var acquired=NearQueueNativeRefill(Main.LocalPlayer,ItemID.SpectrePaintbrush,1);
            observation.LongTool=acquired.Item;observation.Probe.ToolItems=observation.Probe.ToolItems.Concat(new[]{acquired.Item}).ToArray();observation.Written=true;
            Require((long)Get(observation.Host,"nearCapabilityClock")==clock && checked((long)(ulong)Get(observation.Host,"projectionMatchTick"))==observation.WriterHost &&
                QueueLongBrushRunnable(observation),"GetItem restores actual A reach without another Host publication or planner update");
            Console.WriteLine("QueueReachFence stage="+stage+" world="+Main.GameUpdateCount+" host="+observation.WriterHost+
                " publishedClock="+clock+" liveA=true capKeys="+((IDictionary)Get(observation.Host,"nearProofCapabilities")).Count);
        }
        private static void QueueReachBeforeAcquire(object __instance,Player __0,object __1)
        {
            var observation=queueReachAdmission;
            if(observation==null || !observation.Before || observation.Written || !ReferenceEquals(observation.Use,__instance) || __1==null)return;
            int slot=(int)Get(__1,"Slot");
            if(slot>=0 && slot<50 && __0.inventory[slot].type==ItemID.SpectrePaintRoller)QueueAdmitLongBrush(observation,"before-foreign-Acquire");
        }
        private static void QueueReachAfterAdmitted(object __instance,object __0)
        {
            var observation=queueReachAdmission;if(observation==null || !ReferenceEquals(observation.Host,__instance))return;
            if((int)Get(Get(__0,"Source"),"Type")!=ItemID.SpectrePaintRoller)return;
            Require((long)Get(__0,"Token")>0,"foreign recovery observation occurs after the actual ToolUse token is admitted");
            observation.ForeignAdmissions++;
            if(!observation.Before && !observation.Written)QueueAdmitLongBrush(observation,"after-foreign-Admitted");
            if(observation.Before)Require(observation.Written && observation.Restored.Complete,
                "a matching long tool arriving before Acquire forbids admitting B until the actual A successor consumed");
            Console.WriteLine("QueueReachFence admitted beforeArm="+observation.Before+" count="+observation.ForeignAdmissions+
                " token="+Get(__0,"Token")+" restoredComplete="+observation.Restored.Complete);
        }
        private static void QueueReachAdmissionFence(object context,object host,object tools,object input,bool before)
        {
            using(var probe=new PostProcessingProbe(context,host,tools,input))
            {
                var p=Reset(context,host,tools,input);p.position.X=42*16;
                p.inventory[12].SetDefaults(ItemID.Paintbrush);p.inventory[13].SetDefaults(ItemID.SpectrePaintRoller);
                p.inventory[20].SetDefaults(ItemID.RedPaint);p.inventory[20].stack=9999;var red=p.inventory[20];
                var longModel=new Item();longModel.SetDefaults(ItemID.SpectrePaintbrush);var blueModel=new Item();blueModel.SetDefaults(ItemID.BluePaint);
                int far=Enumerable.Range(45,20).First(x=>!p.IsInTileInteractionRange(x,42,TileReachCheckSettings.Simple,p.inventory[12].tileBoost+p.blockRange) &&
                    p.IsInTileInteractionRange(x,42,TileReachCheckSettings.Simple,longModel.tileBoost+p.blockRange));
                var a0=PostProcessingTarget("TilePaint",44,42,red.paint,ItemID.RedPaint);var a1=PostProcessingTarget("TilePaint",far,42,red.paint,ItemID.RedPaint);
                var b0=PostProcessingTarget("WallPaint",40,42,blueModel.paint,ItemID.BluePaint);var b1=PostProcessingTarget("WallPaint",39,42,blueModel.paint,ItemID.BluePaint);
                NativeToolsChecks.Tile(a0.X,42,TileID.Stone);NativeToolsChecks.Tile(a1.X,42,TileID.Stone);
                WorldGen.PlaceWall(40,42,WallID.Stone,true);WorldGen.PlaceWall(39,42,WallID.Stone,true);
                probe.Goals=new[]{a0,a1,b0,b1};probe.Supplies=new[]{red};probe.ToolItems=new[]{p.inventory[12],p.inventory[13]};
                PostProcessingPlace(context,host,input,probe.Goals);probe.Begin(true);SetAutomatic(host,input,true);
                NativeQuickItemChecks.Until(()=>{probe.Advance();return probe.Events.Count==1 && a0.Complete && QueueRecoveryFacts(host).Any(fact=>
                    Convert.ToString(Get(fact,"Wait")).Contains("Player") && !(bool)Get(fact,"Pending"));});
                Require(!a1.Complete && !b0.Complete && !b1.Complete,"A preference is established by real paint, with a real short-tool rejection and no B material yet");
                var blue=NearQueueNativeRefill(p,ItemID.BluePaint,9999);probe.Supplies=new[]{red,blue.Item};
                NativeQuickItemChecks.Until(()=>{probe.Advance();var prepared=GetOptional(host,"preparedBuild");return prepared!=null &&
                    (int)Get(Get(prepared,"Source"),"Type")==ItemID.SpectrePaintRoller;});
                Require(probe.Events.Count==1,"B has a genuine prepared suggestion without consuming or acquiring its new token");
                var patch=new Harmony("JueMingR.Tests.QueueReachAdmission");
                try
                {
                    queueReachAdmission=new QueueReachAdmission{Host=host,Use=Get(tools,"Use"),Probe=probe,Restored=a1,Paint=red,Before=before};
                    patch.Patch(Get(tools,"Use").GetType().GetMethod("Acquire",F),prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(QueueReachBeforeAcquire)));
                    patch.Patch(host.GetType().GetMethod("RememberBuildContinuation",F),postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(QueueReachAfterAdmitted)));
                    NativeQuickItemChecks.Until(()=>{probe.Advance();return probe.Goals.All(goal=>goal.Complete) && !(bool)Get(Get(tools,"Use"),"Active");});
                    var observation=queueReachAdmission;
                    Require(observation.Written && observation.ForeignAdmissions>=2 && red.stack==9997 && blue.Item.stack==9997 && probe.Events.Count==4,
                        "both boundary arms reach real admission, all four effects consume once and every paint supply remains above 9000");
                    var order=probe.Events.Select(value=>value.Kind).ToArray();
                    Require(before?order.SequenceEqual(new[]{"TilePaint","TilePaint","WallPaint","WallPaint"}):order.SequenceEqual(new[]{"TilePaint","WallPaint","WallPaint","TilePaint"}),
                        "pre-admission recovery preserves A priority; post-admission recovery preserves the already-admitted B and its real same-group successor");
                    probe.Report("queue native long brush fence before="+before);
                }
                finally
                {queueReachAdmission=null;foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyPatchType.All,patch.Id);}
            }
        }
    }
}
