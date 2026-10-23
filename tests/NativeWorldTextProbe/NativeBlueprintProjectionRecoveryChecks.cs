using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.Blueprint;
using JueMingR.Platform.WorldTargets;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Existing BlueprintCpu consumer, isolated real files plus a one-shot read
    // gate. Pointer calls are isolated Host inputs; no OS input/live world
    // mutation occurs and no production cache is reset.
    internal static class NativeBlueprintProjectionRecoveryChecks
    {
        private const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        internal static void Run(object context)
        {
            var failures=new List<Exception>();
            foreach(bool published in new[]{false,true}) try { CommitFailure(context,published); } catch(Exception error){failures.Add(error);}
            foreach(bool cancelBeforeOpen in new[]{false,true}) try { RestoreMoving(context,cancelBeforeOpen); } catch(Exception error){failures.Add(error);}
            if(failures.Count!=0)throw new AggregateException("Host commit failure ordering",failures);
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
                        Require(Mode(host)=="Moving","retained effective cell selects the unready restore through real Host pointer consumer");
                        if(cancelBeforeOpen)Call(host,"CancelProjection");
                    }
                    NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return (bool)Get(restored,"Opened");});
                    if(!cancelBeforeOpen)Call(host,"CancelProjection");
                    // No camera/window update after selection: unchanged source
                    // coverage must already have its rollback drawing commands.
                    Require(Mode(host)=="Fixed" && ReferenceEquals(identity,Get(restored,"Identity")) && ReferenceEquals(mask,Get(restored,"Mask")) && (int)Get(restored,"OriginX")==20 && (int)Get(restored,"OriginY")==20 && (long)Get(restored,"CommitOrder")==order && priorCommands.SequenceEqual(Commands(restored)),"restored Moving rollback keeps original identity/origin/order/mask and exact commands without camera change; cancelBeforeOpen="+cancelBeforeOpen);
                    Console.WriteLine("PASS Host restore Moving rollback cancelBeforeOpen="+cancelBeforeOpen+" identity/origin/order/mask/commands retained without camera update");Call(host,"ClearProjection");
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
                    // A complete different-template Open naturally displaces the
                    // old decoded blocks. Cancel drops only that temporary ref.
                    open(pressure);Call(host,"CancelProjection");
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
            Call(host,"UpdateInstanceWindow",opponent,new WorldTargetView(10,10,2,2,0));var identity=(BlueprintProjectionIdentity)Get(opponent,"Identity");var gate=Get(worker,"gate");
            NativeQuickItemChecks.Until(()=>{lock(gate)return ((IEnumerable)Get(worker,"projectionReceipts")).Cast<BlueprintProjectionReceipt>().Any(r=>r.Kind==BlueprintProjectionReceiptKind.Window && r.Identity.Equals(identity));});
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
