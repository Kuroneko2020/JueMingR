using System;
using System.Collections.Generic;
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
        private sealed class ThroughputEvent
        {
            internal int Frame,Count,Animation,Time;
            internal long Token,InputFrame,HostTick,Flight;
            internal uint WorldTick;
        }
        private static void SustainedNativeThroughput(object context,object host,object tools,object input)
        {
            bool oldMouse=Main.SmartCursorWanted_Mouse,oldPad=Main.SmartCursorWanted_GamePad;
            try
            {
                foreach(int item in new[]{ItemID.DirtBlock,ItemID.WoodWall,ItemID.WoodPlatform})
                {
                    var reference=ThroughputSequence(context,host,tools,input,item,false,false,true);
                    var actual=ThroughputSequence(context,host,tools,input,item,true,false,true);
                    CompareThroughput(reference,actual,item,"contiguous smart");
                    if(item==ItemID.DirtBlock)
                        CompareThroughput(reference,ThroughputSequence(context,host,tools,input,item,true,true,true),item,"long missing/remote prefix and local reentry");
                    if(item==ItemID.WoodPlatform)
                    {
                        var manual=ThroughputSequence(context,host,tools,input,item,false,false,false);
                        CompareThroughput(manual,ThroughputSequence(context,host,tools,input,item,true,false,false),item,"platform manual cursor");
                    }
                    else
                    {
                        var accelerated=ThroughputSequence(context,host,tools,input,item,false,false,false,true);
                        CompareThroughput(accelerated,ThroughputSequence(context,host,tools,input,item,true,false,true,true),item,"native Builder buff, then natural expiry");
                    }
                }
            }
            finally{Main.LocalPlayer.ClearBuff(BuffID.Builder);Main.SmartCursorWanted_Mouse=oldMouse;Main.SmartCursorWanted_GamePad=oldPad;}
        }
        private static List<ThroughputEvent> ThroughputSequence(object context,object host,object tools,object input,int item,bool automatic,bool sparse,bool smart,bool accelerated=false)
        {
            var p=Reset(context,host,tools,input);p.position=new Vector2(47*16,646);p.velocity=Vector2.Zero;
            p.ClearBuff(BuffID.Builder);if(accelerated)p.AddBuff(BuffID.Builder,36000);
            p.inventory[12].SetDefaults(item);p.inventory[12].stack=64;
            bool wall=item==ItemID.WoodWall,platform=item==ItemID.WoodPlatform;
            int tile=platform?TileID.Platforms:TileID.Dirt;
            var points=new List<Point>();
            for(int y=38;y>=(platform?38:37);y--)for(int x=44;x<=52;x++)points.Add(new Point(x,y));
            for(int x=43;x<=53;x++)
            {
                // SmartCursor's wall lookup checks line of sight. A solid
                // row between player and target would choose a different cell;
                // native walls provide the same legal support without that occluder.
                if(wall)WorldGen.PlaceWall(x,39,WallID.Wood,true);
                else WorldGen.PlaceTile(x,39,TileID.Stone,true,true);
            }
            Main.SmartCursorWanted_Mouse=smart;Main.SmartCursorWanted_GamePad=false;
            if(automatic)
            {
                var cells=new List<CaptureCell>();
                int originX=sparse?10:44,originY=sparse?20:platform?38:37;
                // Real source order puts 1344 unavailable wall entries before
                // the local Tile frontier. No source cursor is seeded by tests.
                if(sparse)for(int y=0;y<16;y++)for(int x=0;x<84;x++)
                    cells.Add(new CaptureCell{X=x,Y=y,Layers=CaptureLayer.Wall,WallType=WallID.Wood});
                foreach(var point in points)cells.Add(new CaptureCell{X=point.X-originX,Y=point.Y-originY,Layers=wall?CaptureLayer.Wall:CaptureLayer.Tile,
                    TileType=wall?(ushort)0:(ushort)tile,WallType=wall?(ushort)WallID.Wood:(ushort)0});
                Place(context,host,input,Asset(host,cells.OrderBy(c=>c.Y).ThenBy(c=>c.X).ToArray()),originX,originY);
                SetAutomatic(host,input,true);
            }
            else{p.selectedItemState.Select(12);Step(context,input);}
            var result=new List<ThroughputEvent>();int total=0;
            long initialStarts=(long)Get(host,"BuildUsesStarted");
            long steps=(long)Get(host,"ProjectionMatchSourceSteps"),checks=(long)Get(host,"BuildCandidateChecks");
            int idleWithLegalFrontier=0,retainedFirst=0,retainedSuccessor=0;bool removedBuff=false;
            for(int frame=0;frame<1200 && total<points.Count;frame++)
            {
                Point next=points.First(point=>!ThroughputPresent(point,wall,tile));
                int before=p.inventory[12].stack;
                NativeToolExecutionChecks.Sample(context,input,new Vector2(automatic?480:next.X*16+8,automatic?540:next.Y*16+8),!automatic);
                NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");
                if(automatic)
                {
                    var prepared=GetOptional(host,"preparedBuild");var active=GetOptional(host,"activeBuild");
                    EmptyBuildOuter(context,host,tools,input,1+frame%2);
                    if(prepared!=null){if(active==null)retainedFirst++;else if((bool)Get(active,"Checked"))retainedSuccessor++;}
                }
                else NativeToolExecutionChecks.Outer(context,input,1+frame%2);
                int spent=before-p.inventory[12].stack;
                if(spent<=0){if(result.Count>0)idleWithLegalFrontier++;continue;}
                int present=points.Count(point=>ThroughputPresent(point,wall,tile));
                Require(present-total==spent,"native consumer spend equals actual target layers; wall collateral is one event with its real count; item="+item+" auto="+automatic+" frame="+frame+" spent="+spent+" changed="+(present-total)+" target="+Player.tileTargetX+","+Player.tileTargetY);
                total=present;
                result.Add(new ThroughputEvent{Frame=frame,Count=spent,Animation=p.itemAnimation,Time=p.itemTime,Token=automatic?(long)Get(Get(tools,"Use"),"Operation"):0,
                    WorldTick=Main.GameUpdateCount,InputFrame=(long)Get(input,"Frame"),HostTick=checked((long)(ulong)Get(host,"projectionMatchTick")),
                    Flight=automatic?(long)Get(Get(host,"activeBuild"),"Generation"):0});
                if(accelerated && !removedBuff && total>=6)
                {
                    Require(p.tileSpeed<1f && p.wallSpeed<1f,"the actual Builder buff affects native construction speed before it expires");
                    p.ClearBuff(BuffID.Builder);removedBuff=true;
                }
            }
            Require(total==points.Count && p.inventory[12].stack==64-points.Count && result.Count>4,
                "sustained original Player.Update consumes every local target across multiple animations");
            Require((long)Get(host,"BuildUnknownResults")==0,"sustained sequence has no unknown consumption");
            if(automatic)Require(retainedFirst>0 && retainedSuccessor>0 && result.Select(e=>e.Flight).Distinct().Count()==result.Count,
                "empty outer preserves both startup and Checked-successor preparation; each actual consumer owns a different flight across animation boundaries");
            Console.WriteLine("BlueprintThroughput item="+item+" auto="+automatic+" sparse="+sparse+" smart="+smart+" builderThenExpiry="+accelerated+" startupUpdates="+result[0].Frame+" events="+string.Join(",",result.Select(e=>e.Frame+":"+e.Count))+
                " sourceSteps="+((long)Get(host,"ProjectionMatchSourceSteps")-steps)+" candidateChecks="+((long)Get(host,"BuildCandidateChecks")-checks)+" betweenEventNeutral="+idleWithLegalFrontier+
                " nativeUseTime="+p.inventory[12].useTime+" nativeAnimation="+p.inventory[12].useAnimation+" tileSpeed="+p.tileSpeed+" wallSpeed="+p.wallSpeed+" actualStarts="+((long)Get(host,"BuildUsesStarted")-initialStarts)+
                " emptyRetainedStartup="+retainedFirst+" emptyRetainedSuccessor="+retainedSuccessor);
            Console.WriteLine("BlueprintThroughput states item="+item+" auto="+automatic+" entries=frame/world/input/host/animation/itemTime/token "+
                string.Join(";",result.Select(e=>e.Frame+"/"+e.WorldTick+"/"+e.InputFrame+"/"+e.HostTick+"/"+e.Animation+"/"+e.Time+"/"+e.Token)));
            if(automatic && !sparse)Until(context,input,()=>Scene(host).Length==0 && !(bool)Get(Get(tools,"Use"),"Active"));
            if(sparse)
            {
                Require(Scene(host).Length==1,"missing wall responsibilities remain in the sparse projection");
                Require((int)Get(Get(host,"buildSources"),"Count")<=4 && (long)Get(host,"buildSourceBytes")<=4*BlueprintProjectionSourceReceipt.TargetBytes,
                    "the local supply uses the existing four-part retained transport bound");
            }
            SetAutomatic(host,input,false);for(int i=0;i<40;i++)Step(context,input);
            long closedSteps=(long)Get(host,"ProjectionMatchSourceSteps"),closedChecks=(long)Get(host,"BuildCandidateChecks");
            long closedReach=(long)Get(host,"BuildReachSourceReads");
            for(int i=0;i<20;i++)Step(context,input);
            Require((long)Get(host,"BuildCandidateChecks")==closedChecks && (long)Get(host,"ProjectionMatchSourceSteps")==closedSteps && (long)Get(host,"BuildReachSourceReads")==closedReach,
                "disabled construction performs no candidate/source work after finite in-flight cleanup");
            return result;
        }
        private static bool ThroughputPresent(Point point,bool wall,int tile)
        {var value=Main.tile[point.X,point.Y];return wall?value.wall==WallID.Wood:value.active() && value.type==tile;}
        private static void EmptyBuildOuter(object context,object host,object tools,object input,int count)
        {
            var p=Main.LocalPlayer;var prepared=GetOptional(host,"preparedBuild");
            long revision=(long)Get(host,"MaterialBuildEligibilityRevision"),uses=(long)Get(host,"BuildUsesStarted"),checks=(long)Get(host,"BuildNativeChecks");
            uint world=Main.GameUpdateCount;int animation=p.itemAnimation,time=p.itemTime,reuse=p.reuseDelay;
            int inventory=p.inventory.Where(item=>!item.IsAir).Sum(item=>item.stack);
            NativeToolExecutionChecks.Outer(context,input,count);
            Require(ReferenceEquals(prepared,GetOptional(host,"preparedBuild")) && (long)Get(host,"MaterialBuildEligibilityRevision")==revision,
                "stationary empty outer is not a cancellation or a persistent eligibility change");
            Require(Main.GameUpdateCount==world && p.itemAnimation==animation && p.itemTime==time && p.reuseDelay==reuse &&
                uses==(long)Get(host,"BuildUsesStarted") && checks==(long)Get(host,"BuildNativeChecks") && inventory==p.inventory.Where(item=>!item.IsAir).Sum(item=>item.stack),
                "empty outer performs no native use, consumption, world advance or artificial timer progress");
        }
        private static void CompareThroughput(List<ThroughputEvent> expected,List<ThroughputEvent> actual,int item,string scene)
        {
            var reference=expected.Skip(1).Select((e,i)=>unchecked(e.WorldTick-expected[i].WorldTick)).ToArray();
            var intervals=actual.Skip(1).Select((e,i)=>unchecked(e.WorldTick-actual[i].WorldTick)).ToArray();
            // Original FillEmptySpace can legitimately cover several target
            // layers in one wall consumer. Keep that event and its real count;
            // compare each actual opportunity, never insert artificial zero gaps.
            // FillEmptySpace may merge two layers into one event in either
            // sequence, including across a speed transition. Every actual gap
            // must still hit that preceding native event's exact itemTime;
            // both sequences must expose the same ordered native speed phases.
            var expectedPhases=expected.Where((e,i)=>i==0 || e.Time!=expected[i-1].Time).Select(e=>e.Time).ToArray();
            var actualPhases=actual.Where((e,i)=>i==0 || e.Time!=actual[i-1].Time).Select(e=>e.Time).ToArray();
            bool wallOpportunities=item==ItemID.WoodWall && expectedPhases.SequenceEqual(actualPhases) &&
                reference.Select((gap,i)=>gap==expected[i].Time).All(equal=>equal) && intervals.Select((gap,i)=>gap==actual[i].Time).All(equal=>equal);
            bool sameCadence=reference.SequenceEqual(intervals) || wallOpportunities;
            Require(expected.Sum(e=>e.Count)==actual.Sum(e=>e.Count) && sameCadence,
                "all sustained legal consumption intervals equal the paired original held baseline: "+scene+" item="+item+" expected="+string.Join(",",reference)+" actual="+string.Join(",",intervals));
            Require(actual.Select(e=>e.Token).Distinct().Count()==1 && actual[0].Token>0,"same live source continues with one use lease across the whole sequence");
            Console.WriteLine("PASS BlueprintThroughput "+scene+" item="+item+" originalEvents="+expected.Count+" automaticEvents="+actual.Count+" eventConsumption="+string.Join(",",actual.Select(e=>e.Count))+" allIntervals="+string.Join(",",intervals)+" longest="+intervals.Max()+" consumed="+actual.Sum(e=>e.Count)+" oneLease=true");
        }
        private static int pendingBlockPreparations,pendingNeutralChecks;
        private static bool pendingArm;
        private static object pendingHost,pendingUse;
        private static bool BlockPendingDiscovery(ref bool __result)
        {
            if(pendingBlockPreparations<=0)return true;
            pendingBlockPreparations--;__result=false;return false;
        }
        private static void ArmPendingGap()
        {
            if(!pendingArm)return;
            var flight=GetOptional(pendingHost,"activeBuild");
            if(flight==null || !(bool)Get(flight,"Checked"))return;
            pendingArm=false;pendingBlockPreparations=2;
        }
        [HarmonyPriority(Priority.Last)]
        private static void ObservePendingInput(Player __instance)
        {
            if(pendingUse==null || !(bool)Get(pendingUse,"Active") || GetOptional(pendingHost,"activeBuild")==null ||
                !(bool)Get(Get(pendingHost,"activeBuild"),"Checked") || GetOptional(pendingHost,"preparedBuild")!=null)return;
            Require(!__instance.controlUseItem && !(bool)Get(pendingUse,"nativeReady"),"temporary absent ready is a neutral ItemCheck before vanilla can start an empty animation");
            pendingNeutralChecks++;
        }
        private static void PendingBuildInput(object context,object host,object tools,object input)
        {
            var patch=new Harmony("JueMingR.Tests.BlueprintPendingInput");pendingHost=host;pendingUse=Get(tools,"Use");pendingNeutralChecks=0;
            patch.Patch(host.GetType().GetMethod("TryGetBuildTarget",F),prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(BlockPendingDiscovery)));
            patch.Patch(host.GetType().GetMethod("CheckedAutomaticBuild",F),postfix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(ArmPendingGap)));
            patch.Patch(typeof(Player).GetMethod("ItemCheck",F),prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(ObservePendingInput)));
            try
            {
                var p=Reset(context,host,tools,input);p.position=new Vector2(42*16,646);p.inventory[12].SetDefaults(ItemID.DirtBlock);p.inventory[12].stack=10;
                Main.SmartCursorWanted_Mouse=false;
                Place(context,host,input,Asset(host,Enumerable.Range(0,5).Select(x=>new CaptureCell{X=x,Layers=CaptureLayer.Tile,TileType=TileID.Dirt}).ToArray()),44,42);
                pendingArm=true;SetAutomatic(host,input,true);
                // A finite source-delivery gap is injected only at discovery;
                // every Ready target still comes from production preparation.
                Until(context,input,()=>p.inventory[12].stack<10);
                long token=(long)Get(pendingUse,"Operation"),starts=(long)Get(host,"BuildUsesStarted"),native=(long)Get(host,"BuildNativeChecks");
                for(int i=0;i<2;i++)Step(context,input);
                Require((long)Get(pendingUse,"Operation")==token && (long)Get(host,"BuildUsesStarted")==starts && (long)Get(host,"BuildNativeChecks")==native && pendingNeutralChecks>0,
                    "short discovery Pending keeps the source without a new animation, old-target consumption or native rejection");
                Until(context,input,()=>p.inventory[12].stack==5 && Scene(host).Length==0 && !(bool)Get(pendingUse,"Active"));
                Require((long)Get(host,"BuildUnknownResults")==0,"neutral waiting does not create unknown consumption");
                Console.WriteLine("PASS BlueprintThroughput Pending neutralChecks="+pendingNeutralChecks+" sameLease="+token+" finalConsumed=5 naturalRelease=true");
            }
            finally
            {
                pendingArm=false;pendingBlockPreparations=0;pendingHost=pendingUse=null;
                foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyPatchType.All,patch.Id);
                SetAutomatic(host,input,false);
            }
        }
    }
}
