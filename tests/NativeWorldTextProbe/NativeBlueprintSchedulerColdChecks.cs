using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
        private sealed class SchedulerCostRow
        {
            internal long Calls, Ticks, MaximumTicks, Allocated, MaximumAllocated;
        }
        private struct SchedulerCostStart
        {
            internal long Ticks, Allocated;
            internal bool Watching;
        }
        private static SchedulerCostScope schedulerCost;

        // Test-process only. Method timings are inclusive, so nested rows must
        // never be summed. Thread allocation excludes workers; the explicitly
        // labelled AppDomain fallback includes them. No FPS claim follows.
        private sealed class SchedulerCostScope : IDisposable
        {
            private readonly Harmony patch = new Harmony("JueMingR.Tests.SchedulerCost");
            private readonly Func<long> allocated;
            internal readonly Dictionary<string, SchedulerCostRow> Rows = new Dictionary<string, SchedulerCostRow>();
            private readonly object host;
            private readonly string name, allocationKind;
            private readonly long started, allocationStart;
            private long reportedTicks, reportedAllocation;
            private readonly Dictionary<string, SchedulerCostRow> reported = new Dictionary<string, SchedulerCostRow>();
            internal SchedulerCostScope(object host, string name)
            {
                Require(schedulerCost == null, "scheduler cost observations have one scoped test owner");
                this.host = host; this.name = name;
                var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                {
                    allocated = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
                    allocationKind = "current-thread";
                }
                else
                {
                    AppDomain.MonitoringIsEnabled = true;
                    allocated = () => AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                    allocationKind = "appdomain-includes-workers";
                }
                schedulerCost = this;
                try
                {
                    foreach (string entry in new[] { "AdvanceAutomaticBuild", "TryPreferredBuild", "PrepareBuildCandidate", "NativeBuildGeometry", "ContinueBuildSupplyDiscovery" })
                    {
                        var target = host.GetType().GetMethod(entry, F);
                        Require(target != null, "scheduler cost samples the real current owner method: " + entry);
                        Patch(target);
                    }
                    started = Stopwatch.GetTimestamp(); allocationStart = allocated();
                }
                catch { Dispose(); throw; }
            }
            private void Patch(MethodBase method)
            {
                patch.Patch(method, prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerCostBefore)),
                    postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerCostAfter)));
            }
            internal long Calls(string method)
            { SchedulerCostRow row; return Rows.TryGetValue(method, out row) ? row.Calls : 0; }
            internal void Report(string phase)
            {
                long ticks = Stopwatch.GetTimestamp() - started, bytes = allocated() - allocationStart;
                Console.WriteLine("BlueprintSchedulerCost name=" + name + " phase=" + phase + " allocation=" + allocationKind +
                    " cumulativeWindowMs=" + (ticks * 1000.0 / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture) +
                    " cumulativeWindowAllocated=" + bytes + " phaseDeltaWindowMs=" + ((ticks-reportedTicks)*1000.0/Stopwatch.Frequency).ToString("F3",CultureInfo.InvariantCulture) +
                    " phaseDeltaWindowAllocated=" + (bytes-reportedAllocation) + " retainedTransportBytes=" + Get(host, "buildSourceBytes") +
                    " currentFocusBytes=" + Get(host, "BuildFocusDirectoryBytes") + " sharedDirectoryBytes=" + Get(host, "BuildFocusDirectorySharedBytes") +
                    " timing=instrumented-inclusive-cpu-probe;FPS=NOT_EVALUATED");
                foreach (var pair in Rows)
                {
                    SchedulerCostRow prior; if(!reported.TryGetValue(pair.Key,out prior))prior=new SchedulerCostRow();
                    Console.WriteLine("BlueprintSchedulerCostMethod phase=" + phase + " method=" + pair.Key + " cumulativeCalls=" + pair.Value.Calls +
                        " cumulativeInclusiveMs=" + (pair.Value.Ticks * 1000.0 / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture) +
                        " cumulativeMaxUs=" + (pair.Value.MaximumTicks * 1000000.0 / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture) +
                        " cumulativeAllocated=" + pair.Value.Allocated + " cumulativeMaxAllocated=" + pair.Value.MaximumAllocated +
                        " phaseDeltaCalls="+(pair.Value.Calls-prior.Calls)+" phaseDeltaInclusiveMs="+((pair.Value.Ticks-prior.Ticks)*1000.0/Stopwatch.Frequency).ToString("F3",CultureInfo.InvariantCulture)+
                        " phaseDeltaAllocated="+(pair.Value.Allocated-prior.Allocated));
                    reported[pair.Key]=new SchedulerCostRow{Calls=pair.Value.Calls,Ticks=pair.Value.Ticks,Allocated=pair.Value.Allocated};
                }
                reportedTicks=ticks;reportedAllocation=bytes;
            }
            public void Dispose()
            {
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                if (ReferenceEquals(schedulerCost, this)) schedulerCost = null;
            }
            internal SchedulerCostStart Before()
            { return new SchedulerCostStart { Watching = true, Allocated = allocated(), Ticks = Stopwatch.GetTimestamp() }; }
            internal void After(MethodBase method, object instance, SchedulerCostStart start)
            {
                if (!start.Watching) return;
                long elapsed = Stopwatch.GetTimestamp() - start.Ticks, bytes = allocated() - start.Allocated;
                string key = method.Name;
                SchedulerCostRow row;
                if (!Rows.TryGetValue(key, out row)) Rows.Add(key, row = new SchedulerCostRow());
                row.Calls++; row.Ticks += elapsed; row.MaximumTicks = Math.Max(row.MaximumTicks, elapsed);
                row.Allocated += bytes; row.MaximumAllocated = Math.Max(row.MaximumAllocated, bytes);
            }
        }
        private static void SchedulerCostBefore(out SchedulerCostStart __state)
        { __state = schedulerCost == null ? default(SchedulerCostStart) : schedulerCost.Before(); }
        private static void SchedulerCostAfter(MethodBase __originalMethod, object __instance, SchedulerCostStart __state)
        { schedulerCost?.After(__originalMethod, __instance, __state); }

        internal static void SchedulerColdDirectoryCost(object host)
        {
            // These are immutable descriptor component inputs, not invented
            // native Match members. Build the descriptors before sampling;
            // the first directory allocation itself is never warmed away.
            var constructor = typeof(BlueprintProjectionSourceRange).GetConstructors(F).Single(value => value.GetParameters().Length == 9);
            var dependencyType = constructor.GetParameters()[6].ParameterType.GetGenericArguments()[0];
            foreach (int groups in new[] { 33, 4096 })
            {
                var ranges = Enumerable.Range(0, groups).Select(index => (BlueprintProjectionSourceRange)constructor.Invoke(new object[] {
                    (long)index, new BlueprintProjectionBounds(index, 0, 1, 1), 1L, new[] { (long)(uint)index }, new ushort[] { 1 }, new int[] { 0 },
                    Array.CreateInstance(dependencyType, 0), index, 0 })).ToArray();
                var builderType=typeof(BlueprintProjectionGeometryDirectory).GetNestedType("Builder",BindingFlags.NonPublic);
                using(var cost=new SchedulerCostScope(host,"worker-directory-component-groups-"+groups))
                {
                    var builder=Activator.CreateInstance(builderType,F,null,new object[]{ranges},null);
                    cost.Report("first-cold-builder-allocation-before-any-Advance");
                    Require(!(bool)Get(builder,"Ready"),"cold allocation observation does not hide construction work");
                    long steps=0;int batches=0;
                    while(!(bool)Get(builder,"Ready"))
                    {int count=(int)Call(builder,"Advance",64);Require(count>0 && count<=64,"production builder batches remain bounded by 64");steps+=count;batches++;}
                    var directory=(BlueprintProjectionGeometryDirectory)Call(builder,"Freeze",ranges);
                    Require(steps==2L*groups-1 && directory.NodeCount==steps && ReferenceEquals(directory.Sources,ranges),
                        "real production builder freezes every physical ordinal, leaf and union exactly once");
                    Console.WriteLine("BlueprintSchedulerColdDirectory groups="+groups+" ownerBytes="+directory.Bytes+" nodeCount="+directory.NodeCount+
                        " workerConstructionSteps="+steps+" boundedBatches="+batches+" hostConstruction=0 nativeConsumption=NOT_EVALUATED");
                    cost.Report("bounded-build-and-transfer-complete");
                }
            }
        }

        internal static void SchedulerNativeCostExit(object context, object host, object tools, object input)
        {
            using (var scope = new BusinessScope(context, host, tools, input))
            {
                var p = Reset(context, host, tools, input); p.position = new Vector2(42 * 16, 646);
                var source = BusinessStock(p, 12, ItemID.DirtBlock, 6);
                scope.Sources = new[] { source };
                scope.Goals = Enumerable.Range(0, 4).Select(index => BusinessTileGoal(44 + index, 42, source)).ToArray();
                using (var cost = new SchedulerCostScope(host, "native-four-bodies-cold-hot-off"))
                {
                    Place(context, host, input, Asset(host, scope.Goals.Select(goal => new CaptureCell { X = goal.X - 44,
                        Layers = CaptureLayer.Tile, TileType = TileID.Dirt }).ToArray()), 44, 42);
                    scope.Step(); scope.Begin(true); SetAutomatic(host, input, true);
                    for (int frame = 0; frame < 1600 && scope.Events.Count == 0; frame++) scope.Step();
                    Require(scope.Events.Count > 0, "cold runtime sampling reaches the first genuine consumer without preheating it away");
                    cost.Report("cold-start-through-first-consume");
                    for (int frame = 0; frame < 400 && (Scene(host).Length != 0 || (bool)Get(Get(tools, "Use"), "Active")); frame++) scope.Step();
                    Require(scope.Goals.All(goal => goal.Complete) && scope.Events.Count == 4 && source.Item.stack == 2 && Scene(host).Length == 0,
                        "cost sample still proves four real native effects, exact debit and independent Match retirement");
                    cost.Report("hot-through-complete");
                    SetAutomatic(host, input, false);
                    for (int frame = 0; frame < 160 && ((bool)Get(Get(tools, "Use"), "Active") || (bool)Get(Get(host, "buildMaterialPreparation"), "HasWork")); frame++) scope.Step();
                    Require(!(bool)Get(Get(tools, "Use"), "Active") && !(bool)Get(Get(host, "buildMaterialPreparation"), "HasWork"),
                        "OFF cost is sampled only after the actually owned finite return tail finishes");
                    var before = new[] { "TryPreferredBuild", "PrepareBuildCandidate", "ContinueBuildSupplyDiscovery" }.Select(cost.Calls).ToArray();
                    long directory = (long)Get(host, "BuildFocusDirectorySteps") + (long)Get(host, "BuildFocusDirectoryConstructionSteps");
                    for (int frame = 0; frame < 80; frame++) scope.Step();
                    Require(new[] { "TryPreferredBuild", "PrepareBuildCandidate", "ContinueBuildSupplyDiscovery" }.Select(cost.Calls).SequenceEqual(before) &&
                        (long)Get(host, "BuildFocusDirectorySteps") + (long)Get(host, "BuildFocusDirectoryConstructionSteps") == directory &&
                        GetOptional(host, "buildContinuation") == null && GetOptional(host, "preparedBuild") == null,
                        "without owned tail OFF creates no preferred discovery, candidate preparation, supply search or retained focus; shared Match is outside this assertion");
                    cost.Report("off-80-no-owned-tail");
                }
            }
        }

        internal static void SchedulerKnownPositiveWhileSourcePending(object context, object host, object tools, object input)
        {
            using(var scope=new BusinessScope(context,host,tools,input))
            using(var timeline=new SchedulerTimeline(host,tools,input))
            {
                var p=Reset(context,host,tools,input);p.position.X=42*16;
                var wood=BusinessStock(p,12,ItemID.Wood,5);var stone=BusinessStock(p,13,ItemID.StoneBlock,2);
                scope.Sources=new[]{wood,stone};
                scope.Goals=new[]{BusinessTileGoal(44,42,wood),BusinessTileGoal(45,42,wood),BusinessTileGoal(47,42,wood),BusinessTileGoal(39,42,stone)};
                scope.RunnableOracle=goal=>SchedulerBlockRunnable(goal,scope.Sources.Single(source=>source.Type==goal.Type));
                var pair=Place(context,host,input,Asset(host,new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.WoodBlock},
                    new CaptureCell{X=1,Layers=CaptureLayer.Tile,TileType=TileID.WoodBlock}),44,42);
                NativeToolsChecks.Tile(47,42,TileID.Dirt);
                var late=Place(context,host,input,BusinessTileAsset(host,wood),47,42);
                Place(context,host,input,BusinessTileAsset(host,stone),39,42);
                scope.Begin(true);SetAutomatic(host,input,true);
                NativeQuickItemChecks.Until(()=>{scope.Step();return scope.Events.Count>0;});
                Require(scope.Events.Count==1 && scope.Goals[0].Complete && !scope.Goals[1].Complete && scope.Events[0].Type==wood.Type,
                    "known-positive pending fixture establishes A by a real first consumer while its same-range successor is still unbuilt");
                var knownRange=EligibilityRanges(pair).Single();
                Require(((IEnumerable)Get(host,"buildSources")).Cast<object>().Any(value=>ReferenceEquals(Get(value,"Range"),knownRange)) && scope.Runnable(scope.Goals[1]),
                    "the second A has an actual retained immutable transport and independently legal body, floor, reach and exact stock");
                var patch=new Harmony("JueMingR.Tests.SchedulerKnownPositiveSource");
                eligibilityWorker=Get(Get(host,"Library"),"worker");
                eligibilitySourceEntered=new System.Threading.ManualResetEventSlim();eligibilitySourceContinue=new System.Threading.ManualResetEventSlim();
                try
                {
                    patch.Patch(AccessTools.Method(eligibilityWorker.GetType(),"ProjectionSourceStep"),prefix:new HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(PauseEligibilitySource)));
                    Call(host,"RemoveBuildSources",Get(late,"Identity"),EligibilityRanges(late).Single());
                    WorldGen.KillTile(47,42,false,false,true);
                    NativeQuickItemChecks.Until(()=>{scope.Step(preparationOnly:true);return eligibilitySourceEntered.IsSet;});
                    uint pendingWorld=Main.GameUpdateCount;
                    long requests=(long)Get(host,"ProjectionMatchSourceRequests");
                    Require(scope.Events.Count==1 && !eligibilitySourceContinue.IsSet && scope.Runnable(scope.Goals[1]) &&
                        ((IEnumerable)Get(host,"buildSources")).Cast<object>().Any(value=>ReferenceEquals(Get(value,"Range"),knownRange)),
                        "one original different-A worker request is genuinely held while the local known A remains retained and runnable");
                    NativeQuickItemChecks.Until(()=>{scope.Step();return scope.Goals[1].Complete;});
                    Require(!eligibilitySourceContinue.IsSet && scope.Events.Count==2 && scope.Events[1].ChangedGoals.SequenceEqual(new[]{1}) &&
                        scope.Events[1].Type==wood.Type && wood.Item.stack==3 && stone.Item.stack==2 && !scope.Goals[2].Complete && !scope.Goals[3].Complete &&
                        (long)Get(host,"ProjectionMatchSourceRequests")==requests,
                        "the retained known A actually consumes before releasing the other A source, with no foreign action or duplicate request");
                    Console.WriteLine("BlueprintSchedulerKnownPositiveSource workerPendingFromWorld="+pendingWorld+" retainedAConsumeWorld="+scope.Events[1].World+
                        " workerReleased=false exactToken="+scope.Events[1].Token+" requests="+requests);
                    eligibilitySourceContinue.Set();
                    NativeQuickItemChecks.Until(()=>{scope.Step();return scope.Goals.All(goal=>goal.Complete) && !(bool)Get(Get(tools,"Use"),"Active");});
                    Require(scope.Events.Select(entry=>entry.Type).SequenceEqual(new[]{wood.Type,wood.Type,wood.Type,stone.Type}) && wood.Item.stack==2 && stone.Item.stack==1,
                        "release resumes the original late A then the truly eligible foreign B with exact total costs");
                    scope.Report("known positive while another current source pending");timeline.Report("known positive while another current source pending");
                }
                catch {scope.Report("known positive source FAILURE");timeline.Report("known positive source FAILURE");throw;}
                finally {eligibilitySourceContinue.Set();patch.UnpatchAll(patch.Id);eligibilityWorker=null;}
            }
        }

        private static LibraryEntry SchedulerColdRealAsset(object host,int groups,bool handoff=false)
        {
            const int width=1024;var library=(LibraryState)Get(host,"Library");var token=new CaptureToken(Guid.NewGuid(),1,1,1);var handle=new ContentHandle(token.OperationId);
            var clock=Stopwatch.StartNew();
            Require(library.TryBeginStage(token),"real cold-template stage starts");CadenceReceipt(library);
            for(int block=0;block<groups;block++)
            {
                var cells=new CaptureCell[CaptureChunk.MaximumRecords];
                for(int index=0;index<cells.Length;index++)
                {
                    int ordinal=block*cells.Length+index,x=ordinal%width,y=ordinal/width;
                    cells[index]=y==2 && (x==8 || x==10)?new CaptureCell{X=x,Y=y,Layers=CaptureLayer.Wall,WallType=handoff && x==10?WallID.Stone:WallID.Wood}:
                        new CaptureCell{X=x,Y=y,Layers=CaptureLayer.Tile,TileType=TileID.Dirt};
                }
                Require(library.TryAppendChunk(handle,new CaptureChunk(cells,cells.Length)),"one bounded real cold-template chunk is accepted");CadenceReceipt(library);
            }
            int passes=0;long runs=groups;do {passes++;runs=(runs+15)/16;}while(runs>1);
            Console.WriteLine("BlueprintSchedulerColdAsset phase=append-complete blocks="+groups+" records="+((long)groups*CaptureChunk.MaximumRecords)+" elapsedMs="+clock.ElapsedMilliseconds);
            Require(library.TrySealDraft(handle,new CaptureSummary{Width=width,Height=groups*2,ContentCount=(long)groups*CaptureChunk.MaximumRecords}),"real cold-template draft seals");
            // Normalize writes and validates every output block once per
            // fan-in-16 pass, then SealDraft validates the final full source.
            // Renew only on that real worker block progress, never Host ticks.
            SchedulerColdReceipt(library,"seal-normalize",checked((2L*passes+1)*groups));
            Require(library.TryCreate(handle,"冷目录原生正路径样本",token),"real cold-template save is accepted");
            var saved=SchedulerColdReceipt(library,"create-measure-copy-stage-and-committed-validate",checked(4L*groups));
            Require(saved.Status==LibraryStatus.Saved && library.TryList(91037,0,saved.AssetId),"saved cold-template identity lists");
            Console.WriteLine("BlueprintSchedulerColdAsset phase=create-complete blocks="+groups+" normalizationPasses="+passes+" totalElapsedMs="+clock.ElapsedMilliseconds);
            return NativeBlueprintLibraryWait.AcceptedList(library,"cold template selected="+saved.AssetId).Page.Entries.Single(entry=>entry.AssetId==saved.AssetId);
        }

        private static LibraryReceipt SchedulerColdReceipt(LibraryState library,string phase,long blockProgressBound)
        {
            long command=library.AcceptedCommandId,previous=0,lastProgress=0;var clock=Stopwatch.StartNew();LibraryReceipt receipt;
            Require(command>0 && library.Snapshot.Busy,"cold asset wait owns its already accepted real command");
            while(true)
            {
                library.Pump();long progress=library.Snapshot.ProcessedItems,now=clock.ElapsedMilliseconds;
                Require(library.AcceptedCommandId==command && progress>=previous && progress<=blockProgressBound,
                    "cold asset worker progresses monotonically within the fixed input block/pass bound: "+phase);
                if(progress!=previous){previous=progress;lastProgress=now;}
                if(library.TryTakeReceipt(out receipt))
                {
                    Require(receipt.CommandId==command && receipt.Status!=LibraryStatus.Failed && receipt.Status!=LibraryStatus.CommitUnconfirmed && receipt.Status!=LibraryStatus.Cancelled,
                        "cold asset takes its exact real successful receipt; errors cannot renew its wait: "+phase+" "+receipt.Status+" "+receipt.Error);
                    Console.WriteLine("BlueprintSchedulerColdAsset phase="+phase+" command="+command+" processedBlocks="+progress+" bound="+blockProgressBound+
                        " elapsedMs="+now+" status="+receipt.Status+" wait=bounded-progress-no-native-update");
                    return receipt;
                }
                if(now-lastProgress>8000 || now>60000)
                    throw new Exception("Cold asset command stalled or exceeded facility guard: phase="+phase+" command="+command+
                        " progress="+progress+"/"+blockProgressBound+" elapsedMs="+now+" lastReceipt="+library.Snapshot.LastReceipt?.Status+":"+library.Snapshot.LastReceipt?.Error);
                System.Threading.Thread.Sleep(2);
            }
        }

        internal static void SchedulerColdKnownPositiveRuntime(object context,object host,object tools,object input)
        {
            const int groups=257;
            var screen=Main.screenPosition;
            using(NativeBlueprintWorldMatchChecks.EmptyWorld(host,2200,600))
            using(var scope=new BusinessScope(context,host,tools,input))
            using(var timeline=new SchedulerTimeline(host,tools,input))
            {
                try
                {
                    var p=Reset(context,host,tools,input);p.position.X=42*16;
                    var wall=BusinessStock(p,12,ItemID.WoodWall,6);scope.Sources=new[]{wall};
                    scope.Goals=new[]{BusinessWallGoal(44,42,wall),BusinessWallGoal(46,42,wall)};
                    var entry=SchedulerColdRealAsset(host,groups);var worker=Get(Get(host,"Library"),"worker");
                    long builds=(long)Get(worker,"projectionGeometryDirectoryBuilds"),nodes=(long)Get(worker,"projectionGeometryDirectoryNodes"),
                        yields=(long)Get(worker,"projectionGeometryDirectoryYields");
                    uint enabled=Main.GameUpdateCount;var clock=Stopwatch.StartNew();
                    var owner=Place(context,host,input,entry,36,40);
                    // Qualification already owns the only mandatory preparation
                    // window. Enable immediately; never hide startup behind a
                    // WaitReady, nor add a new Host gate before retained A.
                    scope.Begin(true);SetAutomatic(host,input,true);
                    NativeQuickItemChecks.Until(()=>{scope.Step();return scope.Goals.All(goal=>goal.Complete);});
                    var qualification=(BlueprintProjectionQualificationReceipt)Get(owner,"Qualification");
                    var directory=qualification.GeometryDirectory;var first=scope.Events[0];var second=scope.Events[1];
                    Require(qualification.SourceRanges.Count==groups && directory.NodeCount==2*groups-1 &&
                        ReferenceEquals(directory.Sources,qualification.SourceRanges) && scope.Events.Count==2 && wall.Item.stack==4 &&
                        (long)Get(worker,"projectionGeometryDirectoryBuilds")==builds+1 && (long)Get(worker,"projectionGeometryDirectoryNodes")==nodes+2*groups-1 &&
                        (long)Get(worker,"projectionGeometryDirectoryMaximumBatch")<=64 && (long)Get(worker,"projectionGeometryDirectoryYields")>yields &&
                        (long)Get(host,"BuildFocusDirectoryConstructionSteps")==0 && (long)Get(host,"BuildFocusDirectoryBuilds")==0,
                        "the real qualification publishes one complete 513-node directory built in existing worker yields, with no Host construction or extra first-use gate");
                    Require(unchecked(second.World-first.World)==first.Time,
                        "the retained local A still consumes on the next natural same-wall itemTime opportunity");
                    SetAutomatic(host,input,false);
                    for(int frame=0;frame<80 && (bool)Get(Get(tools,"Use"),"Active");frame++)scope.Step();
                    p.position=new Vector2(1104*16,646);p.velocity=Vector2.Zero;
                    for(int x=1090;x<1120;x++)NativeToolsChecks.Tile(x,43,TileID.Stone);
                    var secondOwner=Place(context,host,input,entry,1100,40);
                    scope.Goals=new[]{BusinessWallGoal(1108,42,wall),BusinessWallGoal(1110,42,wall)};
                    scope.Begin(true);SetAutomatic(host,input,true);
                    NativeQuickItemChecks.Until(()=>{scope.Step();return scope.Goals.All(goal=>goal.Complete);});
                    var next=(BlueprintProjectionQualificationReceipt)Get(secondOwner,"Qualification");
                    Require(wall.Item.stack==2 && scope.Events.Count==2 && ReferenceEquals(qualification.SourceRanges,next.SourceRanges) &&
                        ReferenceEquals(directory,next.GeometryDirectory) && (long)Get(worker,"projectionGeometryDirectoryBuilds")==builds+1,
                        "the second genuine same-template instance consumes both new walls using the same frozen directory with no extra worker or Host construction");
                    Console.WriteLine("BlueprintSchedulerColdRuntime physicalGroups="+groups+" records="+((long)groups*CaptureChunk.MaximumRecords)+
                        " firstWorld="+first.World+" secondWorld="+second.World+" enableToFirst="+unchecked(first.World-enabled)+
                        " elapsedMs="+clock.ElapsedMilliseconds+" originalWallGap="+first.Time+" workerDirectoryBuilds=1 workerNodes=513"+
                        " workerYields="+((long)Get(worker,"projectionGeometryDirectoryYields")-yields)+" maxNodesPerMoveNext="+Get(worker,"projectionGeometryDirectoryMaximumBatch")+
                        " directoryBytes="+directory.Bytes+" hostConstruction=0 reentryActualEffects=2 sharedGeometry=true");
                    scope.Report("qualified-directory retained positive and same-template reentry");timeline.Report("qualified-directory retained positive and same-template reentry");
                }
                catch {scope.Report("qualified-directory positive FAILURE");timeline.Report("qualified-directory positive FAILURE");throw;}
                finally
                {
                    SetAutomatic(host,input,false);Call(host,"ClearProjection");
                    Main.screenPosition=screen;Main.LocalPlayer.position=new Vector2(42*16,646);Main.LocalPlayer.velocity=Vector2.Zero;
                }
            }
        }
    }
}
