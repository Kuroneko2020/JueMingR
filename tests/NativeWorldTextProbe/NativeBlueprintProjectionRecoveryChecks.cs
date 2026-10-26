using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Platform.Blueprint;
using JueMingR.Platform.Settings;
using JueMingR.Platform.WorldTargets;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Existing BlueprintCpu consumer, isolated real files plus a one-shot read
    // gate. Pointer calls are isolated Host inputs; original writes target
    // only the disposable Tile world, with no OS input/owner-world mutation
    // or production cache reset.
    internal static class NativeBlueprintProjectionRecoveryChecks
    {
        private const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(object context)
        {
            using (NativeBlueprintWorldMatchChecks.EmptyWorld(Get(context,"Blueprint"),1024,1024))
            {
            var failures=new List<Exception>();
            foreach(bool partial in new[]{false,true}) try { DeleteRestoreReordersLiveWork(context,partial); } catch(Exception error){failures.Add(error);}
            foreach(bool published in new[]{false,true}) try { CommitFailure(context,published); } catch(Exception error){failures.Add(error);}
            foreach(bool cancelBeforeOpen in new[]{false,true}) try { RestoreMoving(context,cancelBeforeOpen); } catch(Exception error){failures.Add(error);}
            foreach(bool viewport in new[]{false,true}) foreach(bool budgetCode in new[]{false,true}) try { using(NativeBlueprintWorldMatchChecks.EmptyWorld(Get(context,"Blueprint"),1024,1024)) IdentityReadFailure(context,viewport,budgetCode); } catch(Exception error){failures.Add(error);}
            if(failures.Count!=0)throw new AggregateException("Host projection identity and commit failure ordering",failures);
            }
        }
        private static void DeleteRestoreReordersLiveWork(object context,bool partial)
        {
            var host=Get(context,"Blueprint");var input=Get(context,"Input");var field=host.GetType().GetField("<Library>k__BackingField",F);var original=field.GetValue(host);
            var type=typeof(NativeBlueprintRepairRecoveryChecks).GetNestedType("FaultFiles",BindingFlags.NonPublic);
            var files=Activator.CreateInstance(type,F,null,new object[]{Path.Combine(Terraria.Program.SavePath,"projection-delete-reorder-"+(partial?"partial":"pending"))},null);
            var release=(ManualResetEvent)Get(files,"ReadRelease");
            using(NativeBlueprintWorldMatchChecks.EmptyWorld(host,1024,1024))
            try
            {
                using(var library=new LibraryState((IBlueprintLibraryFiles)files))
                {
                    try
                    {
                        field.SetValue(host,library);var worker=Get(library,"worker");
                        var assetA=SaveOrdinary(library,1);var assetB=SaveOrdinary(library,300);var assetC=SaveOrdinary(library,1);
                        for(int x=300;x<600;x++)Require(WorldGen.PlaceTile(x,600,1,true,true),"delete-reorder B is a real complete native row");
                        var a=PlaceAt(host,input,assetA,20,600);var b=PlaceAt(host,input,assetB,300,600);var c=PlaceAt(host,input,assetC,800,600);
                        var identityA=Get(a,"Identity");var maskA=Get(a,"Mask");long orderA=(long)Get(a,"CommitOrder");
                        var identityB=(BlueprintProjectionIdentity)Get(b,"Identity");var works=(IDictionary)Get(host,"projectionMatchWork");
                        int maxReads=0,maxSteps=0,maxServices=0,maxAttempts=0,maxRequests=0;
                        Action step=()=>
                        {
                            string[] counters={"ProjectionMatchCellReads","ProjectionMatchSourceSteps","ProjectionMatchServices","ProjectionMatchAdmissionAttempts","ProjectionMatchSourceRequests"};
                            var before=counters.Select(name=>(long)Get(host,name)).ToArray();NativeQuickItemChecks.BeginWorldStep();Call(context,"UpdateRuntime");
                            var used=counters.Select((name,index)=>checked((int)((long)Get(host,name)-before[index]))).ToArray();
                            maxReads=Math.Max(maxReads,used[0]);maxSteps=Math.Max(maxSteps,used[1]);maxServices=Math.Max(maxServices,used[2]);maxAttempts=Math.Max(maxAttempts,used[3]);maxRequests=Math.Max(maxRequests,used[4]);
                            Require(used[0]<=256 && used[1]<=256 && used[2]<=64 && used[3]<=64 && used[4]<=4 && works.Count<=4,"delete-reorder real Update retains world/source/schedule/admission/work budgets");
                        };
                        // The first real Update admits immutable source requests
                        // after taking receipts. Poll below never consumes those
                        // sources or samples the world; no worker read is held.
                        step();object work=works[identityB];
                        Require(work!=null && (bool)Get(work,"Pending") && GetOptional(work,"Read")==null,"delete-reorder actual B request is admitted and Host remains Pending");
                        if(partial)NativeQuickItemChecks.Until(()=>
                        {
                            step();work=works[identityB];var current=work==null?null:GetOptional(work,"Read");
                            return current!=null && (bool)Get(current,"Sampling") && (int)Get(current,"CellIndex")>0 && (int)Get(current,"CellIndex")<((BlueprintProjectionSourcePart)Get(current,"Batch")).Window.Cells.Count;
                        });
                        // Use the real query to retain a live explicit priority,
                        // never supply scheduler positions or proof fields.
                        Call(host,"CheckProjectionMove",b);
                        var state=Get(b,"Match");var progress=MatchProgress(state);long readsBefore=(long)Get(state,"WorldReads"),arrivalsBefore=(long)Get(state,"SourceArrivals");
                        long token=(long)Get(work,"Token");bool pending=(bool)Get(work,"Pending");int partCursor=(int)Get(work,"PartCursor");
                        var parts=((IEnumerable)Get(work,"Parts")).Cast<object>().ToArray();var requested=((IEnumerable)Get(work,"Requested")).Cast<int>().OrderBy(value=>value).ToArray();
                        var read=GetOptional(work,"Read");int cellIndex=read==null?0:(int)Get(read,"CellIndex");var batch=read==null?null:Get(read,"Batch");
                        var range=read==null?null:Get(read,"Range");var rangeProgress=range==null?null:RangeProgress(range);int results=read==null?0:(int)Get(Get(read,"Results"),"Count");
                        bool samplingTurn=(bool)Get(host,"projectionMatchPriorityTurn"),admissionTurn=(bool)Get(host,"projectionMatchAdmissionPriorityTurn");var priority=GetOptional(host,"projectionMatchPriority");
                        Set(files,"Fault","precommit");Require((bool)Call(host,"DeleteAsset",assetA),"delete-reorder real precommit Delete accepted");
                        Require(Scene(host).SequenceEqual(new[]{b,c}),"delete-reorder removes only earlier A before its receipt");
                        var samplingNext=CursorOwner(host,"projectionMatchCursor");var admissionNext=CursorOwner(host,"projectionMatchAdmissionCursor");
                        Require(!ReferenceEquals(samplingNext,admissionNext),"delete-reorder interleaving gives the two ordinary cursors different actual successors");
                        NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !(bool)Get(host,"deletePending");});
                        var restored=Scene(host).Single(item=>((BlueprintProjectionIdentity)Get(item,"Identity")).AssetId==assetA.AssetId);
                        Require(!ReferenceEquals(restored,a) && ReferenceEquals(identityA,Get(restored,"Identity")) && ReferenceEquals(maskA,Get(restored,"Mask")) && (long)Get(restored,"CommitOrder")==orderA && (int)Get(restored,"OriginX")==20 && (int)Get(restored,"OriginY")==600,"delete-reorder proven precommit restores A copy with exact identity/origin/order/mask");
                        Require(Scene(host).SequenceEqual(new[]{restored,b,c}),"delete-reorder actual recovery Add and CommitOrder sort restores A before B");
                        Require(ReferenceEquals(work,works[identityB]) && (long)Get(work,"Token")==token && (bool)Get(work,"Pending")==pending && (int)Get(work,"PartCursor")==partCursor && parts.SequenceEqual(((IEnumerable)Get(work,"Parts")).Cast<object>()) && requested.SequenceEqual(((IEnumerable)Get(work,"Requested")).Cast<int>().OrderBy(value=>value)),"delete-reorder retains the same live work/token/Pending/parts/requested/part cursor before another match Update");
                        Require(ReferenceEquals(state,Get(b,"Match")) && progress.SequenceEqual(MatchProgress(state)) && ReferenceEquals(read,GetOptional(work,"Read")) && (long)Get(state,"WorldReads")==readsBefore && (long)Get(state,"SourceArrivals")==arrivalsBefore,"delete-reorder Poll preserves match proofs/demands and never consumes source or samples world");
                        if(partial)Require(ReferenceEquals(batch,Get(read,"Batch")) && ReferenceEquals(range,Get(read,"Range")) && (bool)Get(read,"Sampling") && (int)Get(read,"CellIndex")==cellIndex && results==(int)Get(Get(read,"Results"),"Count") && rangeProgress.SequenceEqual(RangeProgress(range)),"delete-reorder retains real partial read/batch/range/samples and exact progress");
                        Require(ReferenceEquals(priority,GetOptional(host,"projectionMatchPriority")) && samplingTurn==(bool)Get(host,"projectionMatchPriorityTurn") && admissionTurn==(bool)Get(host,"projectionMatchAdmissionPriorityTurn"),"delete-reorder keeps priority owner and both priority-turn states");
                        Console.WriteLine("RECOVERY delete-reorder state="+(partial?"partial":"pending")+" token="+token+" workIndex="+Get(work,"SceneIndex")+" actualIndex="+Array.IndexOf(Scene(host),b)+" cellIndex="+cellIndex+" sourceComplete="+(batch==null?"pending":((BlueprintProjectionSourcePart)batch).Complete.ToString())+" samplingNext="+Array.IndexOf(Scene(host),samplingNext)+"/"+Get(host,"projectionMatchCursor")+" admissionNext="+Array.IndexOf(Scene(host),admissionNext)+"/"+Get(host,"projectionMatchAdmissionCursor"));
                        Require(ReferenceEquals(Scene(host)[(int)Get(work,"SceneIndex")],Get(work,"Owner")),"delete-reorder B work SceneIndex must name its actual surviving Owner after recovery sort");
                        Require(ReferenceEquals(samplingNext,CursorOwner(host,"projectionMatchCursor")) && ReferenceEquals(admissionNext,CursorOwner(host,"projectionMatchAdmissionCursor")),"delete-reorder independently preserves both ordinary successor identities");
                        Set(files,"Fault",null);
                        NativeQuickItemChecks.Until(()=>{step();return !Scene(host).Contains(b);});
                        Require((long)Get(state,"WorldReads")>readsBefore && (partial || (long)Get(state,"SourceArrivals")>arrivalsBefore),"delete-reorder B consumes its actual source and continues real world reads to natural retirement");
                        Require(GetOptional(b,"Match")==null && !works.Contains(identityB) && !((IEnumerable)Get(host,"projectionMatchWatches")).Cast<object>().Any(pair=>ReferenceEquals(Get(Get(pair,"Value"),"Owner"),b)),"delete-reorder natural completion releases B work and observer owners");
                        NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !WorkerReferences(worker,identityB);});
                        Require(Scene(host).Contains(restored) && Scene(host).Contains(c) && Main.tile[300,600].active() && Main.tile[599,600].active(),"delete-reorder natural completion preserves both incomplete peers and world building");
                        Console.WriteLine("PASS Host DeleteProvenUncommitted recovery sort state="+(partial?"partial":"pending")+" sameWork/token/read/progress; two distinct ordinary successors; actual continuedReads="+((long)Get(state,"WorldReads")-readsBefore)+" sourceArrivals="+((long)Get(state,"SourceArrivals")-arrivalsBefore)+" natural retirement/work/watch/reference release; maxUpdate reads="+maxReads+" sourceSteps="+maxSteps+" services="+maxServices+" admissionAttempts="+maxAttempts+" requests="+maxRequests);
                        Call(host,"ClearProjection");Require(works.Count==0 && (int)Get(host,"projectionMatchCursor")==0 && (int)Get(host,"projectionMatchAdmissionCursor")==0,"delete-reorder scene clear releases work and resets both empty-scene cursors");
                    }
                    finally{release.Set();Call(host,"ClearProjection");}
                }
            }
            finally{try{release.Set();}catch(ObjectDisposedException){}Call(host,"ClearProjection");field.SetValue(host,original);}
        }
        private static object CursorOwner(object host,string name)
        {var scene=Scene(host);int cursor=(int)Get(host,name);Require(scene.Length>0 && cursor>=0 && cursor<scene.Length,"ordinary cursor is a valid actual scene position: "+name);return scene[cursor];}
        private static object[] MatchProgress(object state)
        {return new[]{"OriginX","OriginY","Cursor","Current","Demands","Matches","Targets","Unknown","ViewDirty"}.Select(name=>Get(state,name)).ToArray();}
        private static object[] RangeProgress(object range)
        {return new[]{"Version","RawVersion","Stamp","ProgressStamp","Targets","Matches","Unknown","ProgressTargets","ProgressMatches","ProgressUnknown","Offset","PartIndex","Current","Reading","Demand"}.Select(name=>Get(range,name)).ToArray();}
        private static void IdentityReadFailure(object context,bool viewport,bool budgetCode)
        {
            var host=Get(context,"Blueprint");var input=Get(context,"Input");var field=host.GetType().GetField("<Library>k__BackingField",F);var original=field.GetValue(host);
            string directory=Path.Combine(Terraria.Program.SavePath,"projection-identity-failure-"+(viewport?"viewport":"source")+(budgetCode?"-budget-code":"-hash"));
            var files=new ProjectionReadFiles(directory);var entered=files.ReadEntered;var release=files.ReadRelease;
            string sourcePath=null;byte[] sourceBytes=null;
            Call(host,"ClearProjection");
            try
            {
                using(var library=new LibraryState(files))
                {
                    try
                    {
                    field.SetValue(host,library);var worker=Get(library,"worker");var gate=Get(worker,"gate");
                    LibraryEntry asset=SaveOrdinary(library,260),healthy=SaveOrdinary(library,1);var pressure=SeedDense(library,"身份失败缓存压力");
                    var moving=PlaceAt(host,input,asset,20,600);var other=PlaceAt(host,input,healthy,800,800);
                    NativeBlueprintWorldMatchChecks.AwaitMovable(context,host,moving);
                    NativeBlueprintWorldMatchChecks.AwaitMovable(context,host,other);
                    Require((long)Get(Get(moving,"Match"),"Matches")==0,"failure fixture earns complete zero-Match qualification in the actual empty world");
                    NativeQuickItemChecks.Until(()=>{Call(host,"UpdateProjectionWindow",new WorldTargetView(20,600,8,8,0));Call(host,"Poll");return (int)Get(Get(moving,"Renderer"),"CommandCount")>0;});
                    // Open the existing 65-block fixture to displace the one
                    // source block naturally. Neither the cache nor source
                    // progress is cleared or supplied by this test.
                    OpenForRecovery(host,input,pressure);Call(host,"CancelProjection");
                    NativeQuickItemChecks.Until(()=>library.DebugProjectionStatistics.ActiveTemplates==2 && library.DebugProjectionStatistics.PendingRequests==0);
                    Require(!Cached(worker,asset.AssetId,0),"failed read targets a genuinely nonresident source block after 65-block pressure");
                    NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Call(host,"MoveProjection");
                    Call(host,"AcceptWorldPointer",true,false,false,20,600,1024,1024);Call(host,"AcceptWorldPointer",true,true,false,20,600,1024,1024);
                    Require(Mode(host)=="Moving" && ReferenceEquals(Get(host,"ProjectionMovingSource"),moving),"actual target click enters Moving with the original restore still in scene");
                    var candidate=Get(host,"ProjectionCandidate");var identity=(BlueprintProjectionIdentity)Get(moving,"Identity");var mask=Get(moving,"Mask");var commands=Commands(candidate);long order=(long)Get(moving,"CommitOrder");
                    long transaction=(long)Get(host,"projectionTransaction"),sceneRevision=(long)Get(host,"projectionSceneRevision");
                    var staleCommit=new BlueprintProjectionCommitRequest(identity.SessionGeneration,transaction,sceneRevision,
                        (JueMingR.Features.Blueprint.BlueprintProjectionPlacement)Call(candidate,"Snapshot"),Array.Empty<JueMingR.Features.Blueprint.BlueprintProjectionPlacement>());
                    var otherMatch=Get(other,"Match");long otherReads=(long)Get(otherMatch,"WorldReads");
                    files.HoldAsset=asset.AssetId;files.HoldBlock=0;entered.Reset();release.Reset();
                    Action step=()=>{NativeQuickItemChecks.BeginWorldStep();Call(context,"UpdateRuntime");};
                    if(viewport)
                    {
                        Call(host,"UpdateProjectionWindow",new WorldTargetView(276,600,4,4,0));
                        Require(entered.WaitOne(10000),"moving candidate's actual new viewport reaches the nonresident real reader");
                        lock(gate)Require(((BlueprintProjectionIdentity)Get(Get(worker,"projectionActiveRequest"),"Identity")).Equals(identity),"held viewport belongs to the moving identity");
                    }
                    else
                    {
                        Require(WorldGen.PlaceTile(20,600,1,true,true),"real original Tile mutation dirties the moving source origin");
                        NativeQuickItemChecks.Until(()=>{step();return entered.WaitOne(0);});
                        lock(gate)
                        {
                            var request=Get(worker,"projectionSourceActive");
                            Require(((BlueprintProjectionIdentity)Get(request,"Identity")).Equals(identity),"held source request belongs to the moving restore identity");
                            var work=((IDictionary)Get(host,"projectionMatchWork"))[identity];
                            Require(work!=null && ReferenceEquals(Get(work,"Owner"),moving) && (bool)Get(work,"Pending") && (long)Get(work,"Token")== (long)Get(request,"Generation") && GetOptional(work,"Read")==null,
                                "held source transport retains the restore-owned work, exact worker generation and pending state before any world sample");
                        }
                    }
                    Require(WorldGen.PlaceTile(800,800,1,true,true),"other template gets a real final construction while failed reader is held");
                    step();Require(Scene(host).Contains(other),"different template still needs its actual post-mutation proof");
                    if(budgetCode)files.ReadFailureCode="blueprint-projection-window-budget";
                    else
                    {
                        sourcePath=Path.Combine(directory,asset.AssetId.ToString("N"),"content","chunks",0L.ToString("D20")+".bin");
                        sourceBytes=File.ReadAllBytes(sourcePath);var damaged=(byte[])sourceBytes.Clone();damaged[0]^=1;File.WriteAllBytes(sourcePath,damaged);
                    }
                    release.Set();
                    BlueprintProjectionReceipt failed=null;
                    NativeQuickItemChecks.Until(()=>{lock(gate){failed=FailureReceipts(worker).FirstOrDefault(r=>r.Identity.Equals(identity));return failed!=null;}});
                    bool recoverable=viewport && budgetCode;
                    Require(failed.IdentityRetired!=recoverable && failed.FailureCode==(budgetCode?"blueprint-projection-window-budget":"blueprint-chunk-hash") && (viewport?failed.WindowGeneration>0:failed.WindowGeneration==0),"actual reader failure carries precise identity lifetime independently of overlapping error code");
                    if(recoverable)
                    {
                        Require(WorkerReferences(worker,identity),"viewport demand budget failure retains the real worker identity");Call(host,"Poll");
                        Require(Mode(host)=="Moving" && Scene(host).Contains(moving) && (bool)Get(candidate,"Opened") && (int)Get(candidate,"CoreSize")==64,"recoverable viewport budget shrinks the core and preserves both Moving aliases");
                        NativeQuickItemChecks.Until(()=>{Call(host,"UpdateProjectionWindow",new WorldTargetView(276,600,4,4,0));Call(host,"Poll");return (int)Get(Get(candidate,"Renderer"),"CommandCount")>0;});
                        NativeQuickItemChecks.Until(()=>{step();return !Scene(host).Contains(other);});
                        Call(host,"CancelProjection");
                        Require(Mode(host)=="Fixed" && Scene(host).Single()==moving && (bool)Get(moving,"Opened") && ReferenceEquals(mask,Get(moving,"Mask")) && (int)Get(moving,"OriginX")==20 && (int)Get(moving,"OriginY")==600 && (long)Get(moving,"CommitOrder")==order && commands.SequenceEqual(Commands(moving)),"normal cancel after viewport budget retry restores exact origin/order/mask/window commands");
                        Console.WriteLine("PASS Host Moving viewport budget-code at real reader boundary: worker identity retained, actual Host core128->64/retry, other template natural completion, ordinary cancel exact rollback; controlled error branch, not DTO capacity measurement");Call(host,"ClearProjection");return;
                    }
                    Require(!WorkerReferences(worker,identity),"worker has actually retired the failed identity before Host consumes Failed");
                    Call(host,"Poll");
                    Require(Mode(host)=="Fixed" && GetOptional(host,"ProjectionCandidate")==null && GetOptional(host,"ProjectionMovingSource")==null && !Scene(host).Contains(moving) && Scene(host).Contains(other),"one Failed ends Moving and retires both aliases while preserving the different template");
                    Require((long)Get(host,"projectionTransaction")>transaction && !library.RequestProjectionCommit(staleCommit),"old transaction is revoked and a delayed real commit request cannot re-admit the retired identity");
                    Require(ReferenceEquals(mask,Get(moving,"Mask")),"failure does not mutate the permanent restore mask");
                    AssertDisposed(moving);AssertDisposed(candidate);
                    var active=GetOptional(host,"projectionMatchRead");Require(active==null || !((BlueprintProjectionIdentity)Get(Get(active,"Owner"),"Identity")).Equals(identity),"failed restore leaves no orphan scene-wide source slot");
                    Require(!((IDictionary)Get(host,"projectionMatchWork")).Contains(identity),"fatal identity releases every pending/ready transport and partial sample alias");
                    Require(!((IEnumerable)Get(host,"projectionMatchWatches")).Cast<object>().Any(pair=>((BlueprintProjectionIdentity)Get(Get(Get(pair,"Value"),"Owner"),"Identity")).Equals(identity)),"failed identity leaves no match watch owner");
                    if(sourceBytes!=null){File.WriteAllBytes(sourcePath,sourceBytes);Require(File.ReadAllBytes(sourcePath).SequenceEqual(sourceBytes),"isolated fault bytes are restored exactly; template is not consumed as empty");}
                    NativeQuickItemChecks.Until(()=>{step();return !Scene(host).Contains(other);});
                    Require((long)Get(otherMatch,"WorldReads")>otherReads,"different template resumes actual world reads and naturally completes without another click");
                    for(int i=0;i<12;i++)step();
                    Require(Scene(host).Length==0 && Mode(host)=="None" && !(bool)Get(host,"ProjectionMatchReading") && (int)Get(host,"ProjectionMatchWatchCount")==0,"late processing cannot revive either retired instance or retain a source/watch");
                    NativeQuickItemChecks.Until(()=>library.DebugProjectionStatistics.ActiveReferences==0 && library.DebugProjectionStatistics.ActiveTemplates==0 && library.DebugProjectionStatistics.PendingRequests==0 && library.DebugProjectionStatistics.ReceiptBytes==0);
                    Require(library.TryList(91003,0,asset.AssetId),"failed template remains in the actual library");Require(Complete(library).Page.Entries.Any(e=>e.AssetId==asset.AssetId),"identity failure preserves library metadata and original source");
                    Console.WriteLine("PASS Host Moving identity failure lane="+(viewport?"viewport":"source")+" error="+failed.FailureCode+" actual nonresident block0/reader/worker Failed; source qualification=zero Match; both aliases/source slot/watch/windows/commands released; other template actual reads and natural retirement; old commit refused; library retained");
                    Call(host,"ClearProjection");
                    }
                    catch(Exception error){Console.Error.WriteLine("RECOVERY first failure lane="+(viewport?"viewport":"source")+" budgetCode="+budgetCode+": "+error);throw;}
                    finally
                    {
                        // This test's read gate is independent of worker.Stop.
                        // Release it before using calls Dispose, so a failed
                        // assertion cannot be hidden by its bounded file Join.
                        release.Set();
                    }
                }
            }
            finally
            {
                try{release.Set();}catch(ObjectDisposedException){}
                if(sourcePath!=null && sourceBytes!=null)File.WriteAllBytes(sourcePath,sourceBytes);
                Call(host,"ClearProjection");field.SetValue(host,original);
            }
        }
        private static IEnumerable<BlueprintProjectionReceipt> FailureReceipts(object worker)
        { return ((IEnumerable)Get(worker,"projectionFailures")).Cast<BlueprintProjectionReceipt>().Concat(((IEnumerable)Get(worker,"projectionReceipts")).Cast<BlueprintProjectionReceipt>()).Where(r=>r.Kind==BlueprintProjectionReceiptKind.Failed); }
        private static bool WorkerReferences(object worker,BlueprintProjectionIdentity identity)
        {var gate=Get(worker,"gate");lock(gate)return ((IDictionary)Get(worker,"projectionReferences")).Contains(identity);}
        private static void AssertDisposed(object item)
        {Require(!(bool)Get(item,"Opened") && GetOptional(item,"Match")==null && GetOptional(item,"SourceRanges")==null && GetOptional(item,"MatchRanges")==null && GetOptional(item,"Window")==null && ((IDictionary)Get(item,"Windows")).Count==0 && ((IDictionary)Get(item,"Denied")).Count==0 && (int)Get(Get(item,"Renderer"),"CommandCount")==0,"failed Host alias releases Opened/match/source/window/render references");}
        private static void OpenForRecovery(object host,object input,LibraryEntry entry)
        {NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Require((bool)Call(host,"Place",entry),"identity recovery real Open admitted");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return Mode(host)=="Following";});}
        private static object PlaceAt(object host,object input,LibraryEntry entry,int x,int y)
        {
            OpenForRecovery(host,input,entry);var candidate=Get(host,"ProjectionCandidate");var summary=(CaptureSummary)Get(candidate,"Summary");int px=x+(summary.Width-1)/2,py=y+(summary.Height-1)/2;
            Call(host,"AcceptWorldPointer",true,false,false,px,py,1024,1024);Call(host,"AcceptWorldPointer",true,true,false,px,py,1024,1024);
            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return Mode(host)=="Fixed";});Require(Scene(host).Contains(candidate),"actual confirmation fixes the incomplete identity");return candidate;
        }
        private static LibraryEntry SaveOrdinary(LibraryState library,int width)
        {
            var cells=Enumerable.Range(0,width).Select(x=>new CaptureCell{X=x,Layers=CaptureLayer.Tile,TileType=1}).ToArray();
            return NativeBlueprintProjectionVisualChecks.Save(library,new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(),Guid.NewGuid(),"identity-read-fixture",1,1),1,new CaptureSummary{Width=width,Height=1},new BlueprintProjectionBounds(0,0,width,1),cells,Array.Empty<CaptureObject>()));
        }
        // One finite read gate wraps the real file adapter. Hash cases damage
        // actual isolated bytes; the overlapping-code cases throw only after
        // a real ReadBlock to exercise worker lifetime versus demand semantics.
        private sealed class ProjectionReadFiles : IBlueprintLibraryFiles
        {
            private readonly FileBlueprintLibrary inner;
            internal Guid HoldAsset;internal long HoldBlock=-1;internal string ReadFailureCode;
            internal readonly ManualResetEvent ReadEntered=new ManualResetEvent(false),ReadRelease=new ManualResetEvent(true);
            internal ProjectionReadFiles(string directory){inner=new FileBlueprintLibrary(directory);}
            public byte[] ReadBlock(Guid id,bool stage,long number)
            {
                if(!stage && id==HoldAsset && number==HoldBlock){HoldBlock=-1;ReadEntered.Set();if(!ReadRelease.WaitOne(10000))throw new TimeoutException("isolated identity read gate was not released");}
                var bytes=inner.ReadBlock(id,stage,number);string code=ReadFailureCode;
                if(code!=null){ReadFailureCode=null;throw new InvalidDataException(code);}return bytes;
            }
            public void Open(){inner.Open();}
            public IEnumerable<Guid> EnumerateAssets(){return inner.EnumerateAssets();}
            public PreferenceReadResult OpenMetadata(Guid id,bool ownedUnpublishedContent=false){return inner.OpenMetadata(id,ownedUnpublishedContent);}
            public PreferenceWriteResult WriteMetadata(string identity,byte[] bytes){return inner.WriteMetadata(identity,bytes);}
            public void CloseMetadata(){inner.CloseMetadata();}
            public void BeginStage(Guid id){inner.BeginStage(id);}
            public void WriteStageBlock(Guid id,long number,byte[] bytes,byte[] hash){inner.WriteStageBlock(id,number,bytes,hash);}
            public Stream OpenIndex(Guid id,bool stage){return inner.OpenIndex(id,stage);}
            public void SealStage(Guid id,byte[] root){inner.SealStage(id,root);}
            public byte[] ReadRoot(Guid id,bool stage){return inner.ReadRoot(id,stage);}
            public void ValidateLayout(Guid id,bool stage,long blocks){inner.ValidateLayout(id,stage,blocks);}
            public void PromoteStage(Guid stage,Guid asset){inner.PromoteStage(stage,asset);}
            public void PrepareCleanup(Guid id,BlueprintCleanupProof proof){inner.PrepareCleanup(id,proof);}
            public bool CleanupStep(Guid id,bool stage,long blocks,int budget,BlueprintCleanupProof proof=null){return inner.CleanupStep(id,stage,blocks,budget,proof);}
            public void CloseCleanup(){inner.CloseCleanup();}
            public void CloseStageWriter(){inner.CloseStageWriter();}
            public void Dispose(){ReadRelease.Set();inner.Dispose();ReadEntered.Dispose();ReadRelease.Dispose();}
        }
        private static void RestoreMoving(object context,bool cancelBeforeOpen)
        {
            var host=Get(context,"Blueprint");var input=Get(context,"Input");var field=host.GetType().GetField("<Library>k__BackingField",F);var original=field.GetValue(host);
            var type=typeof(NativeBlueprintRepairRecoveryChecks).GetNestedType("FaultFiles",BindingFlags.NonPublic);
            var files=Activator.CreateInstance(type,F,null,new object[]{Path.Combine(Terraria.Program.SavePath,"projection-restore-moving-"+cancelBeforeOpen)},null);
            Call(host,"ClearProjection");
            try
            {
                using(var library=new LibraryState((IBlueprintLibraryFiles)files))
                {
                    field.SetValue(host,library);var worker=Get(library,"worker");var gate=Get(worker,"gate");var asset=NativeBlueprintPlacementChecks.Seed(library,2,2,"恢复移动原图");
                    NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Require((bool)Call(host,"Place",asset),"restore-moving original Open accepted");
                    NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return Mode(host)=="Following";});
                    Call(host,"AcceptWorldPointer",true,false,false,20,20,120,120);Call(host,"AcceptWorldPointer",true,true,false,20,20,120,120);
                    NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return Mode(host)=="Fixed";});
                    NativeQuickItemChecks.Until(()=>{Call(host,"UpdateProjectionWindow",new WorldTargetView(0,0,120,120,0));Call(host,"Poll");return (int)Get(Get(Scene(host).Single(),"Renderer"),"CommandCount")>0;});
                    var prior=Scene(host).Single();var mask=Get(prior,"Mask");var identity=Get(prior,"Identity");long order=(long)Get(prior,"CommitOrder");var priorCommands=Commands(prior);
                    Set(files,"Fault","precommit");Require((bool)Call(host,"DeleteAsset",asset),"restore-moving real delete failure admitted");
                    NativeQuickItemChecks.Until(()=>{lock(gate)return GetOptional(worker,"result")!=null;});
                    object restored;
                    lock(gate)
                    {
                        for(int i=0;i<4;i++)Call(host,"Poll");restored=Scene(host).Single();
                        Require(!(bool)Get(restored,"Opened") && (int)Get(Get(restored,"Windows"),"Count")>0 && (int)Get(Get(restored,"Renderer"),"CommandCount")==0,"restore qualification retains windows while actual Open is not ready");
                        NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Call(host,"MoveProjection");
                        Call(host,"AcceptWorldPointer",true,false,false,21,21,120,120);Call(host,"AcceptWorldPointer",true,true,false,21,21,120,120);
                        Require(Mode(host)=="SelectingMove" && Get(host,"ProjectionHoverMoveEligibility").ToString()=="Checking" && GetOptional(host,"ProjectionCandidate")==null,"unready restored source remains Unknown and cannot enter Moving before its complete world proof");
                        if(cancelBeforeOpen)Call(host,"CancelProjection");
                    }
                    NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Get(restored,"Opened");});
                    if(!cancelBeforeOpen)
                    {
                        NativeBlueprintWorldMatchChecks.AwaitMovable(context,host,restored);
                        Call(host,"AcceptWorldPointer",true,false,false,21,21,120,120);Call(host,"AcceptWorldPointer",true,true,false,21,21,120,120);
                        Require(Mode(host)=="Moving","fresh click selects the restored source after actual Open and complete zero-Match proof"); Call(host,"CancelProjection");
                    }
                    // No camera/window update after selection: unchanged source
                    // coverage must already have its rollback drawing commands.
                    Require(Mode(host)=="Fixed" && ReferenceEquals(identity,Get(restored,"Identity")) && ReferenceEquals(mask,Get(restored,"Mask")) && (int)Get(restored,"OriginX")==20 && (int)Get(restored,"OriginY")==20 && (long)Get(restored,"CommitOrder")==order && priorCommands.SequenceEqual(Commands(restored)),"restored Moving rollback keeps original identity/origin/order/mask and exact commands without camera change; cancelBeforeOpen="+cancelBeforeOpen);
                    Console.WriteLine("PASS Host restore eligibility and Moving rollback cancelBeforeOpen="+cancelBeforeOpen+" unready Checking; identity/origin/order/mask/commands retained without camera update");Call(host,"ClearProjection");
                }
            }
            finally{Call(host,"ClearProjection");field.SetValue(host,original);}
        }
        private static void CommitFailure(object context,bool published)
        {
            var host=Get(context,"Blueprint");var input=Get(context,"Input");var field=host.GetType().GetField("<Library>k__BackingField",F);var original=field.GetValue(host);
            var type=typeof(NativeBlueprintRepairRecoveryChecks).GetNestedType("FaultFiles",BindingFlags.NonPublic);
            var files=Activator.CreateInstance(type,F,null,new object[]{Path.Combine(Terraria.Program.SavePath,"projection-commit-"+published)},null);
            var entered=(ManualResetEvent)Get(files,"ReadEntered");var release=(ManualResetEvent)Get(files,"ReadRelease");
            Call(host,"ClearProjection");
            try
            {
                using(var library=new LibraryState((IBlueprintLibraryFiles)files))
                {
                    field.SetValue(host,library);var worker=Get(library,"worker");var gate=Get(worker,"gate");
                    LibraryEntry dense=SeedDense(library,"移动源"),pressure=SeedDense(library,"有限缓存压力");
                    var badSample=new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(),Guid.NewGuid(),"isolated",1,1),1,new CaptureSummary{Width=1,Height=1},new BlueprintProjectionBounds(0,0,1,1),new[]{new CaptureCell{X=0,Y=0,Layers=CaptureLayer.Tile,TileType=ushort.MaxValue}},Array.Empty<CaptureObject>());
                    var bad=NativeBlueprintProjectionVisualChecks.Save(library,badSample);var fresh=NativeBlueprintPlacementChecks.Seed(library,2,2,"后续健康图");
                    Action<LibraryEntry> open=entry=>{NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Require((bool)Call(host,"Place",entry),"recovery fixture actual Host open accepted");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return Mode(host)=="Following";});};
                    Action<int,int> fixedAt=(x,y)=>{Call(host,"AcceptWorldPointer",true,false,false,x,y,1000,1000);Call(host,"AcceptWorldPointer",true,true,false,x,y,1000,1000);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return Mode(host)=="Fixed";});};
                    open(dense);fixedAt(255,129);var moving=Scene(host).Single();
                    open(bad);fixedAt(10,10);var opponent=Scene(host).Single(p=>((BlueprintProjectionIdentity)Get(p,"Identity")).AssetId==bad.AssetId);
                    // The later one-cell template permanently covers one raw
                    // Tile of the moving source and changes its mask identity.
                    // Earn qualification for that actual mask before cache
                    // pressure; pressure itself must not revoke world proof.
                    Require(((BlueprintProjectionMask)Get(moving,"Mask")).RemovedLayerCount==1,"recovery source has the literal one-cell permanent cover");
                    NativeBlueprintWorldMatchChecks.AwaitMovable(context,host,moving,512*260,65,512*260-1);
                    // A complete different-template Open naturally displaces the
                    // old decoded blocks. Cancel drops only that temporary ref.
                    open(pressure);Call(host,"CancelProjection");
                    BeginWindowFrame(host, moving, "moving-source");
                    NativeQuickItemChecks.Until(()=>{Call(host,"UpdateInstanceWindow",moving,new WorldTargetView(500,250,4,4,0));Call(host,"Poll");return GetOptional(moving,"Window")!=null;});
                    Require(!Cached(worker,dense.AssetId,2),"commit source block2 is truly absent after bounded real cache pressure");
                    NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Call(host,"MoveProjection");
                    Call(host,"AcceptWorldPointer",true,false,false,500,250,1000,1000);Call(host,"AcceptWorldPointer",true,true,false,500,250,1000,1000);
                    Require(Mode(host)=="Moving","real target selection begins moving the original instance");
                    Call(host,"AcceptWorldPointer",true,false,false,501,250,1000,1000);
                    var oldMask=Get(moving,"Mask");long oldRevision=(long)Get(host,"projectionSceneRevision");
                    QueueBadWindow(host,opponent,worker);
                    if(!published)
                    {
                        Set(files,"HoldAsset",dense.AssetId);Set(files,"HoldBlock",2L);release.Reset();
                    }
                    Call(host,"AcceptWorldPointer",true,true,false,501,250,1000,1000);
                    Require(Mode(host)=="Committing","new gesture reaches actual Host commit");
                    if(published)
                    {
                        NativeQuickItemChecks.Until(()=>{lock(gate)return GetOptional(worker,"projectionCommitReceipt")!=null;});
                    }
                    else Require(entered.WaitOne(10000),"actual overlap computation reaches held source block before publication");
                    // Host consumes the real saved bad-native-type window first,
                    // then its failure retires the opponent read reference.
                    Call(host,"Poll");release.Set();
                    NativeQuickItemChecks.Until(()=>{lock(gate)return GetOptional(worker,"projectionActiveCommit")==null && GetOptional(worker,"projectionCommit")==null;});
                    Require((long)Get(host,"projectionSceneRevision")>oldRevision && !Scene(host).Contains(opponent),"real renderer window failure retired the fixed opponent");
                    Require(Mode(host)=="Fixed" && GetOptional(host,"ProjectionCandidate")==null && Scene(host).Contains(moving) && (int)Get(moving,"OriginX")==0 && (int)Get(moving,"OriginY")==0 && ReferenceEquals(oldMask,Get(moving,"Mask")),"opponent failure terminates Committing, restores Moving origin and never half-applies masks; published="+published);
                    for(int i=0;i<12;i++)Call(host,"Poll");
                    Require(Mode(host)=="Fixed" && ReferenceEquals(oldMask,Get(moving,"Mask")),"late commit cannot revive or mutate the restored scene");
                    open(fresh);fixedAt(800,800);Require(Scene(host).Length==2 && Mode(host)=="Fixed","a new legitimate operation completes after failed confirmation");
                    Console.WriteLine("PASS Host Committing recovery published="+published+" actual bad-native window retirement; old mask/origin retained and next operation Fixed");
                    Call(host,"ClearProjection");
                }
            }
            finally{try{release.Set();}catch(ObjectDisposedException){}Call(host,"ClearProjection");field.SetValue(host,original);}
        }
        private static string Mode(object host){return Get(host,"ProjectionMode").ToString();}
        private static object[] Commands(object placement){return (GetOptional(Get(placement,"Renderer"),"commands") as IEnumerable)?.Cast<object>().ToArray() ?? Array.Empty<object>();}
        private static object[] Scene(object host){return ((IEnumerable)Get(host,"ProjectionScene")).Cast<object>().ToArray();}
        private static bool Cached(object worker,Guid asset,long block)
        {foreach(var item in ((IEnumerable)Get(worker,"projectionCache")).Cast<object>())if((long)Get(item,"Number")==block && ((BlueprintProjectionIdentity)Get(Get(item,"Owner"),"Identity")).AssetId==asset)return true;return false;}
        private static void QueueBadWindow(object host,object opponent,object worker)
        {
            BeginWindowFrame(host, opponent, "bad-native-opponent");
            Call(host,"UpdateInstanceWindow",opponent,new WorldTargetView(10,10,2,2,0));var identity=(BlueprintProjectionIdentity)Get(opponent,"Identity");var gate=Get(worker,"gate");
            NativeQuickItemChecks.Until(()=>{lock(gate)return ((IEnumerable)Get(worker,"projectionReceipts")).Cast<BlueprintProjectionReceipt>().Any(r=>r.Kind==BlueprintProjectionReceiptKind.Window && r.Identity.Equals(identity));});
        }
        private static void BeginWindowFrame(object host, object instance, string label)
        {
            Console.WriteLine("RECOVERY direct-window-frame=" + label + " priorAdmissions=" + Get(host, "projectionWindowAdmissions") + " pending=" + Get(instance, "WindowPending") + " core=" + Get(instance, "CoreSize") + " denied=" + Get(Get(instance, "Denied"), "Count"));
            // This specialist intentionally isolates one instance's real request
            // and worker ordering. Its private per-instance entry skips the
            // public frame-start reset; restore only that frame admission count,
            // never worker/cache contents, pending windows, or scene expectations.
            Set(host, "projectionWindowAdmissions", 0);
        }
        private static LibraryEntry SeedDense(LibraryState library,string name)
        {
            var token=new CaptureToken(Guid.NewGuid(),1,1,1);var handle=new ContentHandle(token.OperationId);
            Require(library.TryBeginStage(token),"dense isolated stage accepted");Complete(library);
            const int width=512,height=260;
            for(int block=0;block<65;block++)
            {
                var cells=new CaptureCell[2048];for(int i=0;i<cells.Length;i++){int n=block*2048+i;cells[i]=new CaptureCell{X=n%width,Y=n/width,Layers=CaptureLayer.Tile,TileType=1};}
                Require(library.TryAppendChunk(handle,new CaptureChunk(cells,cells.Length)),"bounded dense source chunk accepted");Complete(library);
            }
            Require(library.TrySealDraft(handle,new CaptureSummary{Width=width,Height=height,ContentCount=width*height}),"dense isolated draft sealed");Complete(library);
            Require(library.TryCreate(handle,name,token),"dense isolated asset create accepted");var saved=Complete(library);
            Require(library.TryList(91002,0,saved.AssetId),"dense isolated asset lists");return Complete(library).Page.Entries.Single(e=>e.AssetId==saved.AssetId);
        }
        private static LibraryReceipt Complete(LibraryState state)
        {LibraryReceipt value=null;NativeQuickItemChecks.Until(()=>{state.Pump();return state.TryTakeReceipt(out value);});Require(value.Status==LibraryStatus.Completed || value.Status==LibraryStatus.Saved,"isolated recovery library command reliable: "+value.Error);return value;}
    }
}
