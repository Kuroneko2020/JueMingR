using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintWorldMatchChecks
    {
        private static bool holdCompletionV2Sources;
        private static bool CompletionV2SourceTake() { return !holdCompletionV2Sources; }
        private static object completionV2PublicationTarget;
        private static long completionV2FirstVisibleTick;
        private static object completionReceiptContext;
        private static void CompletionV2Published(object __instance,object item)
        {
            if(!ReferenceEquals(completionV2PublicationTarget,item) || completionV2FirstVisibleTick>=0)return;
            var match=GetOptional(item,"Match");if(match==null)return;
            foreach(DictionaryEntry pair in (IDictionary)Get(match,"Visible"))
            {
                long key=(long)pair.Key;
                if(Result(item,(int)(uint)key,(int)(key>>32)).MatchBits==0)continue;
                completionV2FirstVisibleTick=checked((long)(ulong)Get(__instance,"projectionMatchTick"));break;
            }
        }

        private static void CompletionV2Progress(object context, object host, object input, LibraryState library, Action step)
        {
            using (EmptyWorld(host, 1024, 160))
            {
                CapturedCompletionPair(context, host, input, library, step, 0);
                CapturedCompletionPair(context, host, input, library, step, 32);
                SmallObjectThroughput(context, host, input, library, step, 32);
                BatchedRawInvalidation(context, host, input, library, step);
                PriorityCompletionFairness(host, input, library, step, 4, 0);
                PriorityCompletionFairness(host, input, library, step, 4, 2);
                PriorityCompletionFairness(host, input, library, step, 6, 0);
            }
        }

        // The fixture builds original furniture on real supports, then enters
        // the production capture/save flow. No CaptureCell/Object array is
        // passed to Save, and both placement arms reuse the same saved asset.
        internal static LibraryEntry CaptureCompletionAsset(object context, object host, object input, LibraryState library,
            Action step, int ox, int oy, int width, int height, string name)
        {
            var shell = Get(context, "Shell");
            var editor = (BlueprintCaptureState)Get(host, "State");
            var flow = (BlueprintCaptureFlow)Get(host, "Flow");
            Call(shell, "CloseAndSubmitPosition");
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            editor.Exit(); editor.Enter();
            editor.Selection.ToggleRectangle(ox, oy, ox + width - 1, oy + height - 1, Main.maxTilesX, Main.maxTilesY);
            var receiptProbe = new HarmonyLib.Harmony("JueMingR.Tests.Blueprint.CaptureFailureReceipt");
            completionReceiptContext = context;
            var applyReceipt = host.GetType().GetMethod("ApplyReceipt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            receiptProbe.Patch(applyReceipt, prefix: new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintWorldMatchChecks), nameof(CompletionFailureReceipt)));
            try
            {
                Require((bool)Call(host, "Save"), "completion pair enters the real Host capture admission");
                NativeQuickItemChecks.Until(() => { step(); return flow.Status == BlueprintCaptureStatus.DraftReady || flow.Status == BlueprintCaptureStatus.Failed; });
                Require(flow.Status == BlueprintCaptureStatus.DraftReady, "completion pair reaches actual DraftReady: " + flow.Status + " " + flow.Message);
            }
            finally { receiptProbe.Unpatch(applyReceipt, HarmonyLib.HarmonyPatchType.All, receiptProbe.Id); completionReceiptContext = null; }
            Call(host, "SetDraftName", name); Call(host, "ConfirmSave");
            NativeQuickItemChecks.Until(() => { step(); return flow.Status == BlueprintCaptureStatus.Saved || flow.Status == BlueprintCaptureStatus.CommitUnconfirmed; });
            Require(flow.Status == BlueprintCaptureStatus.Saved && flow.SelectedAssetId.HasValue, "completion pair requires reliable Saved, never uncertain content");
            Guid id = flow.SelectedAssetId.Value;
            Call(shell, "CloseAndSubmitPosition"); editor.Exit();
            RequireListedAsset(library, id, "captured completion asset is obtained through the actual library list");
            var entry = library.Snapshot.Page.Entries.Single(e => e.AssetId == id);
            NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            Console.WriteLine("BlueprintCompletionV2 captured asset=" + id.ToString("N") + " revision=" + entry.ContentRevision + " hash=" + entry.ContentHash + " dimensions=" + entry.Width + "x" + entry.Height + " contentRecords=" + entry.ContentCount + " capture=Capture-DraftReady-ConfirmSave-Saved library=real-list");
            return entry;
        }

        private static void CompletionFailureReceipt(object __instance, LibraryReceipt __0)
        {
            if (__0.Status == LibraryStatus.Completed || __0.Status == LibraryStatus.Saved) return;
            Console.Error.WriteLine("BLUEPRINT CAPTURE RECEIPT kind=" + __0.Kind + " status=" + __0.Status + " retired=" + __0.OperationRetired +
                " operation=" + __0.OperationId + " command=" + __0.CommandId + " code=" + __0.FailureCode + " error=" + __0.Error);
            var summary = ((BlueprintCaptureFlow)Get(__instance, "Flow")).Summary;
            if (summary != null) Console.Error.WriteLine("BLUEPRINT CAPTURE SUMMARY xy=" + summary.Left + "," + summary.Top + " size=" + summary.Width + "x" + summary.Height +
                " content=" + summary.ContentCount + " unreliable=" + summary.UnreliableObjectCount + " version=" + summary.TargetVersion +
                " start=" + summary.StartTick + " end=" + summary.EndTick + " hostLatest=" + Get(__instance, "latestTick") +
                " runtimeTick=" + Get(completionReceiptContext, "updateTick") + " nativeEntry=" + Main.GameUpdateCount);
        }

        private static void BuildCompletionLamps(int ox, int oy, int lamps, bool includeOrdinary)
        {
            int width = Math.Max(6, lamps * 3);
            for (int y = 0; y < 5; y++) for (int x = 0; x < width; x++) Main.tile[ox + x, oy + y] = new Tile();
            for (int i = 0; i < lamps; i++)
            {
                int x = ox + i * 3;
                Require(WorldGen.PlaceTile(x, oy + 3, 1, true, true), "small lamp has original solid support");
                Require(WorldGen.PlaceTile(x, oy + 2, 93, true, true), "small lamp is placed through the original legal furniture entry");
                Require(Main.tile[x, oy].active() && Main.tile[x, oy].type == 93 && Main.tile[x, oy + 2].active(), "original placement produced the complete 1x3 lamp");
            }
            if (includeOrdinary)
            {
                Require(WorldGen.PlaceTile(ox + width - 1, oy, 1, true, true), "paired last ordinary target does not support furniture");
                for (int i = 0; i < 6; i++) WorldGen.PlaceWall(ox + i, oy + 4, 7, true);
                WorldGen.PlaceWire(ox + width - 1, oy);
            }
        }

        private static void CapturedCompletionPair(object context, object host, object input, LibraryState library, Action step, int lamps)
        {
            const int ox = 20, oy = 25;
            BuildCompletionLamps(ox, oy, lamps, true);
            int width = Math.Max(6, lamps * 3);
            var entry = CaptureCompletionAsset(context, host, input, library, step, ox, oy, width, 5, "完成成对 " + lamps);
            for (int arm = 0; arm < 2; arm++)
            {
                if (arm == 1) WorldGen.KillTile(ox + width - 1, oy, false, false, true);
                string before = Snapshot(ox, oy, width, 5);
                long reads = (long)Get(host, "ProjectionMatchCellReads"), requests = (long)Get(host, "ProjectionMatchSourceRequests"), batches = (long)Get(host, "ProjectionMatchSourceBatches");
                long invalidations=(long)Get(host,"ProjectionMatchInvalidations");var sourceBefore=library.DebugProjectionSourceStatistics;
                var clock = Stopwatch.StartNew();
                var item = ConfirmCompletion(host, input, entry, ox, oy, step);
                long placeToConfirm=clock.ElapsedMilliseconds;var confirmedClock=Stopwatch.StartNew();
                long confirmTick=checked((long)(ulong)Get(host,"projectionMatchTick"));
                var costs=new HarmonyLib.Harmony("JueMingR.Tests.Blueprint.CompletionV2PairCosts");completionUpdateCosts.Clear();
                completionV2PublicationTarget=item;completionV2FirstVisibleTick=-1;
                costs.Patch(host.GetType().GetMethod("AdvanceProjectionMatch",Flags),prefix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintWorldMatchChecks),nameof(CompletionUpdatePrefix)),postfix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintWorldMatchChecks),nameof(CompletionUpdatePostfix)));
                costs.Patch(host.GetType().GetMethod("PublishProjectionMatchView",Flags),postfix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintWorldMatchChecks),nameof(CompletionV2Published)));
                int updates = 0, fixedAt = -1, coveredAt = -1, supplementAt = -1, retiredAt = -1;
                int productive=0,sourceWaiting=0,otherZeroReads=0;
                try
                {
                while (Mode(host) == "Committing" || Scene(host).Contains(item))
                {
                    long priorReads=(long)Get(host,"ProjectionMatchCellReads");
                    step(); updates++;
                    if((long)Get(host,"ProjectionMatchCellReads")>priorReads)productive++;
                    else if(((IDictionary)Get(host,"projectionMatchWork")).Values.Cast<object>().Any(w=>(bool)Get(w,"Pending")))sourceWaiting++;else otherZeroReads++;
                    if (Mode(host) != "Committing" && fixedAt < 0) fixedAt = updates;
                    var state = GetOptional(item, "Match");
                    if (state != null && (int)Get(state, "Current") == ((Array)Get(state, "Ranges")).Length && coveredAt < 0)
                    {
                        coveredAt = updates;
                        if (arm == 1)
                        {
                            Require(Scene(host).Contains(item) && (long)Get(state, "Matches") == (long)Get(state, "Targets") - 1 && (long)Get(state, "Unknown") == 0,
                                "same captured asset keeps exactly its missing ordinary Tile and does not retire early");
                            Require((Result(item, width - 1, 0).MatchBits & 1) == 0, "missing ordinary Tile never receives a false Match");
                            Require(WorldGen.PlaceTile(ox + width - 1, oy, 1, true, true), "paired last Tile is supplied by the real original writer");
                            supplementAt = updates;
                        }
                    }
                    if (!Scene(host).Contains(item) && Mode(host) != "Committing") retiredAt = updates;
                    if (clock.ElapsedMilliseconds > 15000) throw new Exception("Captured completion pair failed its finite facility guard: lamps=" + lamps + " arm=" + arm + " updates=" + updates + " mode=" + Mode(host));
                    System.Threading.Thread.Sleep(2);
                }
                Require(retiredAt > 0 && (arm == 0 || supplementAt > 0 && retiredAt > supplementAt), "captured paired placement naturally retires after complete current coverage without later pointer input");
                Require(GetOptional(item, "Match") == null && GetOptional(item, "SourceRanges") == null && ((IDictionary)Get(item, "Windows")).Count == 0 && (int)Get(Get(item, "Renderer"), "CommandCount") == 0 && (int)Get(host, "ProjectionMatchWatchCount") == 0 && !(bool)Get(host, "ProjectionMatchReading"),
                    "paired retirement releases its scene, source, watch, renderer and matching references");
                Require(Mode(host)=="None" && (int)Get(Get(host,"Toolbar"),"ProjectionMode")==0 && GetOptional(host,"ProjectionHover")==null,
                    "the actual toolbar derives ordinary mode from the retired scene and retains no old hit target");
                if (arm == 0) Require(before == Snapshot(ox, oy, width, 5), "initial-complete observation leaves the real captured building unchanged");
                var sourceAfter=library.DebugProjectionSourceStatistics;double[] ms=completionUpdateCosts.Select(t=>t*1000.0/Stopwatch.Frequency).OrderBy(v=>v).ToArray();
                Require(completionV2FirstVisibleTick>=confirmTick && ms.Length==updates,"each declared paired Update measures the real match method and its actual published renderer result");
                double median=(ms[(ms.Length-1)/2]+ms[ms.Length/2])/2.0;
                double p95=ms[Math.Max(0,(int)Math.Ceiling(ms.Length*0.95)-1)];
                Console.WriteLine("PASS BlueprintCompletionV2 pair lamps=" + lamps + " arm=" + (arm == 0 ? "initial-complete" : "missing-one-then-built") + " asset=" + entry.AssetId.ToString("N") + " origin=" + ox + "," + oy + " initialMask=empty fixedUpdate=" + fixedAt + " firstCompleteCoverageUpdate=" + (coveredAt < 0 ? retiredAt : coveredAt) + " firstVisibleMatchUpdate="+(completionV2FirstVisibleTick-confirmTick)+" supplementUpdate=" + supplementAt + " retirementUpdate=" + retiredAt + " updatesFromConfirm=" + updates + " updatesFromSupplement=" + (supplementAt < 0 ? -1 : retiredAt - supplementAt) + " productiveUpdates="+productive+" sourceWaitingUpdates="+sourceWaiting+" otherZeroReadUpdates="+otherZeroReads+" worldReads=" + ((long)Get(host, "ProjectionMatchCellReads") - reads) + " sourceRequests=" + ((long)Get(host, "ProjectionMatchSourceRequests") - requests) + " arrivals=" + ((long)Get(host, "ProjectionMatchSourceBatches") - batches) + " memberBlockScans="+(sourceAfter.MemberBlocks-sourceBefore.MemberBlocks)+" memberRecordScans="+(sourceAfter.MemberRecords-sourceBefore.MemberRecords)+" invalidations="+((long)Get(host,"ProjectionMatchInvalidations")-invalidations)+" sourceBytesPeak="+Get(host,"ProjectionMatchSourceBytesPeak")+" hostRetainedPeak="+Get(host,"ProjectionMatchRetainedBytesPeak")+" workerWorkingPeak="+sourceAfter.WorkingPeakBytes+" queuedReceiptPeak="+sourceAfter.ReceiptPeakBytes+" elapsedMs=" + clock.ElapsedMilliseconds + " placeToConfirmMs="+placeToConfirm+" confirmToRetireMs="+confirmedClock.ElapsedMilliseconds+" matchUpdateMedianMs="+median.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" p95Ms="+p95.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" maxMs="+ms[ms.Length-1].ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" supply=natural-runtime-worker no-followup-pointer=true component-estimates-not-process-peak=true no-FPS-conversion=true");
                }
                finally{completionV2PublicationTarget=null;costs.UnpatchAll(costs.Id);}
                WorldGen.KillTile(ox + width - 1, oy, false, false, true); for (int n = 0; n < 3; n++) step();
                Require(!Scene(host).Contains(item), "the paired completed identity cannot revive after original demolition");
                RequireListedAsset(library, entry.AssetId, "paired completion keeps its original saved template");
                Require(WorldGen.PlaceTile(ox + width - 1, oy, 1, true, true), "restore same building semantics for the second same-asset arm");
                Call(host, "ClearProjection");
            }
        }

        private static bool AwaitCompletionV2Supply(object host,object item,LibraryState library,Action step)
        {
            // Fixed is a commit result, not qualification or source readiness.
            // Taking stays held while real Updates establish this actual work.
            NativeQuickItemChecks.Until(()=>{step();return Mode(host)!="Committing" && (bool)Call(host,"ProjectionQualificationCurrent",item) &&
                GetOptional(item,"Match")!=null && ((IDictionary)Get(host,"projectionMatchWork")).Contains(Get(item,"Identity"));});
            bool ready=AwaitSourceSupply(host,library);
            Require(ready,"controlled next Update has actual qualified work and real production worker output");return ready;
        }
        private static void SmallObjectThroughput(object context, object host, object input, LibraryState library, Action step, int count)
        {
            const int ox = 20, oy = 50;
            BuildCompletionLamps(ox, oy, count, false);
            var entry = CaptureCompletionAsset(context, host, input, library, step, ox, oy, count * 3, 3, "小对象吞吐 " + count);
            var timing = new HarmonyLib.Harmony("JueMingR.Tests.Blueprint.CompletionV2Throughput");
            try
            {
                holdCompletionV2Sources = true;
                timing.Patch(host.GetType().GetMethod("TakeProjectionMatchSources", Flags), prefix: new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintWorldMatchChecks), nameof(CompletionV2SourceTake)));
                var item = ConfirmCompletion(host, input, entry, ox, oy, step);
                bool realWorkerReady=AwaitCompletionV2Supply(host,item,library,step);
                Require(Scene(host).Contains(item), "controlled throughput begins with the real committed identity and unconsumed real worker output");
                var sampled = new HashSet<int>();
                objectSample = (x, y) => { if (x >= ox && x < ox + count * 3 && (x - ox) % 3 == 0 && y >= oy && y < oy + 3) sampled.Add((x - ox) / 3); };
                timing.Patch(host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldMatchSource", true).GetMethod("TryRead", Flags), postfix: new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintWorldMatchChecks), nameof(AfterObjectSample)));
                long before = (long)Get(host, "ProjectionMatchCellReads"), sourceBefore = (long)Get(host, "ProjectionMatchSourceSteps");
                holdCompletionV2Sources = false; step();
                long used = (long)Get(host, "ProjectionMatchCellReads") - before, sourceUsed = (long)Get(host, "ProjectionMatchSourceSteps") - sourceBefore;
                var state = GetOptional(item, "Match");
                int completed = state == null ? count : ((IEnumerable)Get(state, "Ranges")).Cast<object>().Count(r => ((BlueprintProjectionSourceRange)Get(r, "Source")).ObjectId > 0 && (bool)Get(r, "Current") && (long)Get(r, "Matches") == (long)Get(r, "Targets") && (long)Get(r, "Targets") == 3);
                Console.WriteLine("BlueprintCompletionV2 throughput declaredUpdates=1 objects=" + count + " independentlySampled=" + sampled.Count + " independentlyCompleted=" + completed + " worldReads=" + used + " sourceSteps=" + sourceUsed + " realWorkerReady="+realWorkerReady+" injectedDto=false source=real-captured-asset retired=" + !Scene(host).Contains(item));
                Require(used <= 256 && sourceUsed <= 256 && sampled.Count >= 2 && completed >= 2,
                    "one real Update with ready real production source completes more than one independent small object within its existing world/source allowance");
                ulong token = (ulong)Get(host, "projectionMatchTick"); long same = (long)Get(host, "ProjectionMatchCellReads");
                Call(host, "Poll"); Call(host, "Update", token); Call(host, "Poll");
                Require(same == (long)Get(host, "ProjectionMatchCellReads"), "batch consumption and Poll cannot grant a second allowance for the same Update token");
            }
            finally { holdCompletionV2Sources = false; objectSample = null; timing.UnpatchAll(timing.Id); Call(host, "ClearProjection"); }
        }

        private static void BatchedRawInvalidation(object context, object host, object input, LibraryState library, Action step)
        {
            const int ox=20,oy=70;
            BuildCompletionLamps(ox,oy,2,false);
            for(int x=0;x<252;x++)Require(WorldGen.PlaceTile(ox+x,oy+5,1,true,true),"finite ordinary prefix uses real original construction");
            WorldGen.PlaceWire4(ox+300,oy);
            var entry=CaptureCompletionAsset(context,host,input,library,step,ox,oy,301,6,"批内独立失效");
            WorldGen.KillWire4(ox+300,oy);
            var timing=new HarmonyLib.Harmony("JueMingR.Tests.Blueprint.CompletionV2Raw");
            try
            {
                holdCompletionV2Sources=true;
                timing.Patch(host.GetType().GetMethod("TakeProjectionMatchSources",Flags),prefix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintWorldMatchChecks),nameof(CompletionV2SourceTake)));
                var item=ConfirmCompletion(host,input,entry,ox,oy,step);
                AwaitCompletionV2Supply(host,item,library,step);
                long requests=(long)Get(host,"ProjectionMatchSourceRequests");
                WorldGen.paintTile(ox,oy,1,false,false);WorldGen.paintTile(ox,oy,0,false,false);
                holdCompletionV2Sources=false;step();
                var partial=GetOptional(host,"projectionMatchRead");
                Require(partial!=null && GetOptional(partial,"ObjectTarget")!=null && (bool)Get(partial,"Sampling") && (int)Get(partial,"CellIndex")==0,
                    "255 actual ordinary reads leave a real object's anchor sample crossing the Update boundary");
                var ranges=((IEnumerable)Get(Get(item,"Match"),"Ranges")).Cast<object>().ToArray();
                var sibling=ranges.Single(r=>((BlueprintProjectionSourceRange)Get(r,"Source")).ObjectId==2);
                long siblingVersion=(long)Get(sibling,"Version");int siblingReads=0;
                objectSample=(x,y)=>{if(x==ox+3 && y>=oy && y<oy+3)siblingReads++;};
                timing.Patch(host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldMatchSource",true).GetMethod("TryRead",Flags),postfix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintWorldMatchChecks),nameof(AfterObjectSample)));
                WorldGen.paintTile(ox,oy,1,false,false);
                for(int n=0;n<4 && !(bool)Get(sibling,"Current");n++)step();
                Require((bool)Get(sibling,"Current") && (long)Get(sibling,"Matches")==3 && (long)Get(sibling,"Version")==siblingVersion && siblingReads==4,
                    "a same-batch stable sibling obtains its independent complete current proof after another object's partial raw sample is revoked");
                Require((long)Get(host,"ProjectionMatchSourceRequests")==requests && Scene(host).Contains(item),
                    "world changes before delivery and during partial sampling preserve the one immutable batch, without cancelling or rerequesting siblings");
                Require(((IEnumerable)Get(Get(item,"Match"),"Ranges")).Cast<object>().Where(r=>((BlueprintProjectionSourceRange)Get(r,"Source")).ObjectId==1).All(r=>!(bool)Get(r,"Current") || (long)Get(r,"Matches")<3),
                    "the changed object's historical anchor cannot become a complete current Match");
                Console.WriteLine("PASS BlueprintCompletionV2 batchRaw ordinaryReadsBeforeObject=255 sourceRequestsForBothObjects=1 staleObjectRetry=true stableSiblingActualReads="+siblingReads+" stableSiblingVersionDelta="+((long)Get(sibling,"Version")-siblingVersion)+" sourceRetained="+Get(host,"ProjectionMatchSourceRetainedBytes")+" reserved="+Get(host,"ProjectionMatchReservedBytes")+" retainedPeak="+Get(host,"ProjectionMatchRetainedBytesPeak"));
            }
            finally{holdCompletionV2Sources=false;objectSample=null;timing.UnpatchAll(timing.Id);Call(host,"ClearProjection");}
            for(int n=0;n<3;n++)step();
            Require((int)Get(host,"ProjectionMatchWorkCount")==0 && (long)Get(host,"ProjectionMatchSourceRetainedBytes")==0 && (long)Get(host,"ProjectionMatchReservedBytes")==0,
                "identity cancellation releases pending/ready/partial source reservations and late output cannot revive it");
        }

        private static int v2ServiceCursor;
        private static readonly List<long> v2OrdinaryServices=new List<long>();
        private static void BeforeV2Service(object __instance){v2ServiceCursor=(int)Get(__instance,"projectionMatchCursor");}
        private static void AfterV2Service(object __instance,bool __result)
        {
            if(!__result)return;var read=Get(__instance,"projectionMatchRead");var item=Get(read,"Owner");var scene=Scene(__instance);int at=Array.IndexOf(scene,item);
            if(!(bool)Get(__instance,"projectionMatchPriorityTurn"))Require((int)Get(__instance,"projectionMatchCursor")==v2ServiceCursor,"actual priority service never advances the ordinary cursor");
            else
            {
                Require((int)Get(__instance,"projectionMatchCursor")==((at+1)%scene.Length),"actual ordinary service advances to the actual chosen successor, including n=0/skipped unavailable work");
                v2OrdinaryServices.Add(((BlueprintProjectionIdentity)Get(item,"Identity")).ProjectionGeneration);
            }
        }

        private static void PriorityCompletionFairness(object host, object input, LibraryState library, Action step,int count,int initialCursor)
        {
            const int width=128,height=16,oy=105;
            var cells=Enumerable.Range(0,width*height).Select(n=>new CaptureCell{X=n%width,Y=n/width,Layers=CaptureLayer.Tile,TileType=1}).ToArray();
            var entry=NativeBlueprintBudgetChecks.Save(library,cells,width,height);
            var items=new object[count];var origins=Enumerable.Range(0,count).Select(i=>20+i*160).ToArray();int changes=0,cReads=0;
            for(int i=0;i<count;i++)
            {
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)Main.tile[origins[i]+x,oy+y]=new Tile();
                Require(WorldGen.PlaceTile(origins[i],oy,1,true,true) && WorldGen.PlaceTile(origins[i]+1,oy,1,true,true),"each persistent owner has original target cells for native paint and a Match locking movement");
            }
            for(int y=0;y<height;y++)for(int x=0;x<width;x++){var tile=Main.tile[origins[2]+x,oy+y];tile.active(true);tile.type=1;}
            WorldGen.KillTile(origins[2]+width-1,oy+height-1,false,false,true);
            Action dirty=()=>
            {
                for(int i=0;i<count;i++)if(i!=2)WorldGen.paintTile(origins[i],oy,(byte)(1+(changes&1)),false,false);
                changes++;AwaitSourceSupply(host,library);step();
            };
            var timing=new HarmonyLib.Harmony("JueMingR.Tests.Blueprint.CompletionV2Priority"+count+"-"+initialCursor);
            try
            {
                for(int i=0;i<count;i++)items[i]=Place(host,input,entry,origins[i],oy,dirty);
                Call(host,"UpdateProjectionWindow",new JueMingR.Platform.WorldTargets.WorldTargetView(origins[0],oy,32,16,0));
                NativeQuickItemChecks.Until(()=>{dirty();return GetOptional(items[0],"Window")!=null;});
                int align=0;while((int)Get(host,"projectionMatchCursor")!=initialCursor && align++<128)dirty();
                Require((int)Get(host,"projectionMatchCursor")==initialCursor,"ordinary starting cursor is established by actual production service, never injected");
                StartSelection(host,input,false);Pointer(host,false,origins[0]+1,oy);Pointer(host,true,origins[0]+1,oy);
                Require(Mode(host)=="SelectingMove" && GetOptional(host,"ProjectionCandidate")==null && ReferenceEquals(GetOptional(host,"projectionMatchPriority"),items[0]),
                    "one actual Move target pointer query refuses Checking/Matched A and retains the real priority");
                Require(WorldGen.PlaceTile(origins[2]+width-1,oy+height-1,1,true,true),"C becomes fully complete only after the real priority condition exists");
                v2OrdinaryServices.Clear();
                timing.Patch(host.GetType().GetMethod("StartProjectionMatchRead",Flags),prefix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintWorldMatchChecks),nameof(BeforeV2Service)),postfix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintWorldMatchChecks),nameof(AfterV2Service)));
                objectSample=(x,y)=>{if(x>=origins[2] && x<origins[2]+width && y>=oy && y<oy+height)cReads++;};
                timing.Patch(host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintWorldMatchSource",true).GetMethod("TryRead",Flags),postfix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintWorldMatchChecks),nameof(AfterObjectSample)));
                int updates=0;var clock=Stopwatch.StartNew();
                while(Scene(host).Contains(items[2]) && updates++<192){dirty();if(clock.ElapsedMilliseconds>12000)break;}
                Require(!Scene(host).Contains(items[2]) && cReads>=width*height && Scene(host).Length==count-1 && items.Where((p,i)=>i!=2).All(p=>Scene(host).Contains(p)),
                    "C receives actual complete sampling and naturally retires while priority A and all dirty siblings remain incomplete; count="+count+" cursor="+initialCursor+" updates="+updates+" CReads="+cReads+" ordinary="+string.Join(",",v2OrdinaryServices));
                int removed=initialCursor==2?1:0;
                Call(host,"CancelProjection");Call(host,"UpdateProjectionWindow",new JueMingR.Platform.WorldTargets.WorldTargetView(origins[removed],oy,32,16,0));
                NativeQuickItemChecks.Until(()=>{dirty();return GetOptional(items[removed],"Window")!=null;});
                StartSelection(host,input,true);Pointer(host,false,origins[removed]+1,oy);Pointer(host,true,origins[removed]+1,oy);
                Require(!Scene(host).Contains(items[removed]) && Scene(host).Length==count-2,"actual Remove retires priority or ordinary adjacent identity without skipping the shifted successor");
                if(removed==0)Require(GetOptional(host,"projectionMatchPriority")==null,"retiring the priority identity clears its strong reference");
                int nx=origins[removed]+10,ny=oy+25;WorldGen.PlaceWire4(nx,ny);
                var extra=Place(host,input,Small(library,new CaptureCell{Layers=CaptureLayer.Wire,Wires=8}),nx,ny,dirty);
                for(int n=0;n<64 && Scene(host).Contains(extra);n++)dirty();
                Require(!Scene(host).Contains(extra) && Scene(host).Length==count-2,"a newly added complete identity still progresses after removal while persistent dirty siblings continue");
                Require((int)Get(host,"ProjectionMatchWorkCount")<=4 && (long)Get(host,"ProjectionMatchReservedBytes")<=2*BlueprintProjectionSourceReceipt.MaximumEstimatedBytes+16384,
                    "large or restarted batches retain finite scene admissions and reserve worst-case in-flight bytes");
                Console.WriteLine("PASS BlueprintCompletionV2 priority instances="+count+" naturallyEstablishedCursor="+initialCursor+" explicitMoveQueries=1 nativePaintUpdates="+changes+" CActualReads="+cReads+" CRetiredAfterUpdates="+updates+" otherOwnersStillIncomplete="+(count-1)+" ordinaryServices="+v2OrdinaryServices.Count+" sourceWorkSlots="+Get(host,"ProjectionMatchWorkCount")+" removed="+removed+" thenAddedCompleteRetired=true elapsedMs="+clock.ElapsedMilliseconds);
            }
            finally{objectSample=null;timing.UnpatchAll(timing.Id);Call(host,"CancelProjection");Call(host,"ClearProjection");}
        }
    }
}
