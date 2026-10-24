using System;
using System.Collections;
using System.Linq;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintPlacementChecks
    {
        internal static LibraryEntry Seed(LibraryState library, int width, int height, string name)
        {
            Guid id = Guid.NewGuid(); var token = new CaptureToken(id, 1, 1, 1); var handle = new ContentHandle(id);
            Action<bool> complete = accepted => { Require(accepted, "isolated projection asset command accepted"); LibraryReceipt receipt = null; NativeQuickItemChecks.Until(() => { library.Pump(); return library.TryTakeReceipt(out receipt); }); Require(receipt.Status == LibraryStatus.Completed || receipt.Status == LibraryStatus.Saved, "isolated reliable asset: " + receipt.Error); };
            complete(library.TryBeginStage(token));
            var cells = new[] { new CaptureCell { X = 30, Y = 40, Layers = CaptureLayer.Tile, TileType = 1 }, new CaptureCell { X = 30 + width - 1, Y = 40 + height - 1, Layers = CaptureLayer.Wire, Wires = 1 } };
            complete(library.TryAppendChunk(handle, new CaptureChunk(cells, 2)));
            complete(library.TrySealDraft(handle, new CaptureSummary { Left = 30, Top = 40, Width = width, Height = height, ContentCount = 2, StartTick = 1, EndTick = 2 }));
            complete(library.TryCreate(handle, name, token)); complete(library.TryList(7700, 0, id)); return library.Snapshot.Page.Entries.Single(e => e.AssetId == id);
        }
        internal static void Run(object context)
        {
            var host = Get(context, "Blueprint"); var input = Get(context, "Input"); var shell = Get(context, "Shell");
            var state = (BlueprintCaptureState)Get(host, "State"); var library = (LibraryState)Get(host, "Library");
            Call(shell, "CloseAndSubmitPosition"); Call(host, "ClearProjection"); Call(host, "CancelCaptureOrDraft");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
            state.Exit(); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
            var entry = Seed(library, 89, 32, "中心锚点稀疏图"); var second = Seed(library, 4, 2, "独立图"); var unrelated = Seed(library, 2, 2, "非关联删除图");
            Func<object> current = () => GetOptional(host, "CurrentProjection"); Func<string> mode = () => Get(host, "ProjectionMode").ToString();
            Func<object[]> scene = () => ((IEnumerable)Get(host, "ProjectionScene")).Cast<object>().ToArray();
            Action<bool, bool, bool, int, int> pointer = (trusted, left, right, x, y) => Call(host, "AcceptWorldPointer", trusted, left, right, x, y, Main.maxTilesX, Main.maxTilesY);
            Action poll = () => Call(host, "Poll");
            Action windows = () => { Call(host, "UpdateProjectionWindow", new JueMingR.Platform.WorldTargets.WorldTargetView(0, 0, Main.maxTilesX, Main.maxTilesY, 0)); poll(); };
            Action<LibraryEntry> place = e => { NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Require((bool)Call(host, "Place", e), "actual Host accepts new instance without requiring an old one"); NativeQuickItemChecks.Until(() => { poll(); return mode() == "Following"; }); };
            Action<int,int> fix = (x,y) => { pointer(true,false,false,x,y); pointer(true,true,false,x,y); NativeQuickItemChecks.Until(() => { poll(); return mode() == "Fixed"; }); };
            try
            {
                // Real UI consumer cancellation: Back/Suspend retires the in-flight
                // command before any late Opened can close F5 or enter Following.
                Call(Get(shell, "BlueprintUi"), "OpenLibrary"); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
                Require((bool)Call(host,"Place",entry),"pending Open accepted"); Require((bool)Get(host,"ProjectionEditing"),"Open waiting owns world gate");
                Call(Get(shell,"BlueprintUi"),"Suspend");
                for(int i=0;i<12;i++){poll();System.Threading.Thread.Sleep(1);}
                Require(current()==null && GetOptional(host,"pendingProjection")==null && scene().Length==0,"Back then late Opened cannot revive candidate or scene");
                Call(shell,"CloseAndSubmitPosition"); place(entry); pointer(true,true,false,100,100);
                Require(mode()=="Following" && (int)Get(current(),"OriginX")==56 && (int)Get(current(),"OriginY")==85,"center-air89x32 anchor and held startup cannot confirm");
                pointer(false,false,false,0,0); pointer(true,true,false,100,100); Require(mode()=="Following","unfocused pseudo-release cannot rearm"); fix(60,60);
                var first=scene().Single(); long firstId=((BlueprintProjectionIdentity)Get(first,"Identity")).ProjectionGeneration;
                Require((int)Get(first,"OriginX")==16 && (int)Get(first,"OriginY")==45,"complete center bounds fixed inside isolated world");
                NativeQuickItemChecks.Sample(input,Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());
                PlayerInput.Triggers.Current.MouseLeft=PlayerInput.Triggers.Current.MouseRight=true; PlayerInput.Triggers.Current.CopyInto(Main.LocalPlayer);
                Require(Main.LocalPlayer.controlUseItem && Main.LocalPlayer.controlUseTile && !(bool)Get(host,"ProjectionEditing"),"Fixed releases actual native world use");
                place(entry); fix(70,60); place(second); fix(40,40);
                Require(scene().Length==3 && (int)Call(host,"ProjectionReferenceCount",entry.AssetId)==2,"same template twice plus different template coexist independently");
                Require(scene().Select(p=>((BlueprintProjectionIdentity)Get(p,"Identity")).ProjectionGeneration).Distinct().Count()==3,"instance ids independent");
                NativeQuickItemChecks.Until(()=>{windows();return scene().All(p=>GetOptional(p,"Window")!=null);});
                NativeQuickItemChecks.Sample(input,Array.Empty<Keys>()); Call(host,"MoveProjection");
                Require(mode()=="SelectingMove","Move starts target selection"); pointer(true,true,false,16,45); Require(mode()=="SelectingMove","startup press cannot select");
                pointer(true,false,false,50,50); pointer(true,true,false,50,50); Require(mode()=="SelectingMove","air hole cannot seize a bounding box");
                pointer(true,false,false,16,45); pointer(true,true,false,16,45);
                Require(mode()=="Moving" && (int)Get(current(),"OriginX")==16 && (int)Get(current(),"OriginY")==45,"new target click preserves exact grab offset");
                pointer(true,true,false,19,47); Require(mode()=="Moving" && (int)Get(current(),"OriginX")==19 && (int)Get(current(),"OriginY")==47,"target click cannot also confirm; grab follows delta");
                pointer(false,false,false,0,0); pointer(true,true,false,1,1); Require((int)Get(current(),"OriginX")==19,"takeover freezes movement");
                pointer(true,false,false,70,70); Require((int)Get(current(),"OriginX")==19,"trusted neutral rebases without catchup"); pointer(true,false,true,71,71);
                Require(mode()=="Fixed" && (int)Get(first,"OriginX")==16 && (int)Get(first,"OriginY")==45,"cancel leaves committed origin and all masks untouched");
                NativeQuickItemChecks.Sample(input,Array.Empty<Keys>()); Call(host,"MoveProjection"); pointer(true,false,false,16,45); pointer(true,true,false,16,45);
                pointer(true,false,false,17,45); pointer(true,true,false,17,45); NativeQuickItemChecks.Until(()=>{poll();return mode()=="Fixed";});
                var moved=scene().Single(p=>((BlueprintProjectionIdentity)Get(p,"Identity")).ProjectionGeneration==firstId);
                Require((int)Get(moved,"OriginX")==17 && scene().Length==3,"self-intersect move replaces one identity rather than cloning or self-erasing");
                state.Enter(); state.Selection.ToggleRectangle(1,1,2,2,Main.maxTilesX,Main.maxTilesY); long revision=state.Selection.Revision;
                Require(!(bool)Get(host,"CanPlaceProjection") && !(bool)Get(host,"CanMoveProjection") && !(bool)Get(host,"CanRemoveProjection"),"Place Move Remove share unsaved capture gate");
                Call(host,"RemoveProjection"); Require(state.Selection.Revision==revision && scene().Length==3,"blocked removal cannot discard selection or instances"); state.Exit();
                NativeQuickItemChecks.Until(()=>{windows();return GetOptional(moved,"Window")!=null;}); NativeQuickItemChecks.Sample(input,Array.Empty<Keys>()); Call(host,"RemoveProjection"); pointer(true,false,false,17,45); pointer(true,true,false,17,45);
                Require(scene().Length==2 && (int)Call(host,"ProjectionReferenceCount",entry.AssetId)==1,"selected world removal retires exactly one same-template instance");
                Require((bool)Call(host,"DeleteAsset",unrelated),"unrelated delete accepted"); NativeQuickItemChecks.Until(()=>{poll();return !library.Snapshot.Busy;}); Require(scene().Length==2,"unrelated template delete preserves scene");
                Require((bool)Call(host,"DeleteAsset",entry),"referenced template delete accepted"); NativeQuickItemChecks.Until(()=>{poll();return !library.Snapshot.Busy && !(bool)Get(host,"deletePending");});
                Require(scene().Length==1 && ((BlueprintProjectionIdentity)Get(scene()[0],"Identity")).AssetId==second.AssetId,"template deletion retires all its refs and preserves other template");
                Call(host,"ClearProjection");
                var baseCell = new CaptureCell { X=0,Y=0,Layers=(CaptureLayer)31,TileType=1,WallType=7,Wires=3,Actuator=true,LiquidAmount=80,LiquidKind=0 };
                var baseWindow = new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(),Guid.NewGuid(),"fixture",1,1),1,new CaptureSummary{Width=2,Height=1},new BlueprintProjectionBounds(0,0,2,1),new[]{baseCell,new CaptureCell{X=1,Y=0,Layers=CaptureLayer.Tile,TileType=1}},Array.Empty<CaptureObject>());
                var baseEntry=NativeBlueprintProjectionVisualChecks.Save(library,baseWindow);
                var coverWindow=new BlueprintProjectionWindow(baseWindow.Identity,1,new CaptureSummary{Width=1,Height=1},new BlueprintProjectionBounds(0,0,1,1),new[]{new CaptureCell{X=0,Y=0,Layers=CaptureLayer.Tile|CaptureLayer.Wire,TileType=1,Wires=1}},Array.Empty<CaptureObject>());
                var coverEntry=NativeBlueprintProjectionVisualChecks.Save(library,coverWindow);
                place(baseEntry);fix(80,20);var layered=scene().Single();
                NativeQuickItemChecks.Until(()=>{windows();return GetOptional(layered,"Window")!=null;});
                place(coverEntry);pointer(true,false,false,80,20);
                Require(((BlueprintProjectionMask)Get(layered,"Mask")).RemovedLayerCount==0,"Following preview never erases committed masks");
                fix(80,20);var masked=(BlueprintProjectionMask)Get(layered,"Mask"); var remaining=masked.Apply(baseCell);
                Require(scene().Length==2 && (remaining.Layers&CaptureLayer.Tile)==0 && remaining.Wires==2 && (remaining.Layers&(CaptureLayer.Wall|CaptureLayer.Liquid|CaptureLayer.Actuator))==(CaptureLayer.Wall|CaptureLayer.Liquid|CaptureLayer.Actuator),"actual Host atomic commit masks only Tile and matching wire color");
                var layerRenderer=Get(layered,"Renderer"); var drawn=((IEnumerable)Get(layerRenderer,"commands")).Cast<object>().ToArray();
                Require(!drawn.Any(c=>Get(c,"Texture").ToString()=="Tile" && ((Microsoft.Xna.Framework.Vector2)Get(c,"Position")).X==0) && drawn.Any(c=>Get(c,"Texture").ToString()=="Wall"),"masked actual renderer removes covered Tile commands while retaining wall consumer");
                NativeQuickItemChecks.Until(()=>{windows();return scene().All(p=>GetOptional(p,"Window")!=null);});
                NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Call(host,"MoveProjection");pointer(true,false,false,80,20);pointer(true,true,false,80,20);pointer(true,false,false,85,20);pointer(true,true,false,85,20);
                NativeQuickItemChecks.Until(()=>{poll();return mode()=="Fixed";});Require(ReferenceEquals(masked,Get(layered,"Mask")) && masked.Apply(baseCell).Wires==2,"moving cover away never revives erased old layers");
                NativeQuickItemChecks.Until(()=>{windows();return scene().All(p=>GetOptional(p,"Window")!=null);});NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Call(host,"RemoveProjection");pointer(true,false,false,85,20);pointer(true,true,false,85,20);
                Require(scene().Length==1 && ReferenceEquals(masked,Get(layered,"Mask")),"removing cover leaves permanent old mask unchanged");
                Call(host,"ClearProjection"); var wide=Seed(library,1024,1,"连续移动读取图");place(wide);
                pointer(true,false,false,511,0);int frame=0;
                NativeQuickItemChecks.Until(()=>{Call(host,"UpdateProjectionWindow",new JueMingR.Platform.WorldTargets.WorldTargetView(frame++%16,0,64,32,0));poll();return GetOptional(current(),"Window")!=null;});
                Require((long)Get(current(),"WindowGeneration")==1 && (int)Get(Get(current(),"Renderer"),"CommandCount")>0,"continuously changing exact view consumes useful first core without serial cancellation or waiting for mouse stop");
                long reads=library.DebugProjectionStatistics.BlockReads,builds=(long)Get(Get(current(),"Renderer"),"SegmentBuildCount");
                for(int i=0;i<40;i++){Call(host,"UpdateProjectionWindow",new JueMingR.Platform.WorldTargets.WorldTargetView(i%16,0,64,32,0));poll();}
                Require(library.DebugProjectionStatistics.BlockReads==reads && (long)Get(Get(current(),"Renderer"),"SegmentBuildCount")==builds,"small view motion within retained coverage performs zero new IO and segment builds");
                Require((long)Get(host,"ProjectionWindowBytes")<=32L*1024*1024 && (long)Get(host,"ProjectionCommandBytes")<=64L*1024*1024,"actual consumer retained windows/commands respect total scene budgets");
                Call(host,"CancelProjection");
                var recoveryFailures=new System.Collections.Generic.List<Exception>();
                try { DeleteRecovery(context); } catch(Exception error) { recoveryFailures.Add(error); }
                try { NativeBlueprintProjectionRecoveryChecks.Run(context); } catch(Exception error) { recoveryFailures.Add(error); }
                if(recoveryFailures.Count!=0) throw new AggregateException("Host projection recovery contract",recoveryFailures);
                var registry=(HotkeyRegistry)Get(Get(shell,"hotkeys"),"Registry"); Require(registry.Find("blueprint.move")?.CanConfigure==true && registry.Find("blueprint.remove")?.CanConfigure==true,"actual configurable entry registry retained");
                Call(host,"OnSessionEnded"); for(int i=0;i<5;i++)poll(); Require(scene().Length==0 && current()==null && mode()=="None","real session end retires all identities and late receipts"); Call(host,"OnSessionStarted");
                Console.WriteLine("PASS Blueprint actual Host multi-instance: real Back lateOpen cancellation, startup/select/confirm stages, center/even sparse anchor, independent identity/count, air hole hit, grab offset/takeover/cancel/self move, capture gate including remove, exact world remove/template delete, Fixed native actions/session. OS and certified physical pointer remain separate.");
            }
            finally { NativeQuickItemChecks.Sample(input,Array.Empty<Keys>()); Call(host,"ClearProjection"); state.Exit(); }
        }
        private static void DeleteRecovery(object context)
        {
            var host=Get(context,"Blueprint");var input=Get(context,"Input");var binding=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            var libraryField=host.GetType().GetField("<Library>k__BackingField",binding);var original=libraryField.GetValue(host);
            try
            {
                foreach(string fault in new[]{"precommit","unknown"})
                {
                    Call(host,"ClearProjection");
                    var type=typeof(NativeBlueprintRepairRecoveryChecks).GetNestedType("FaultFiles",System.Reflection.BindingFlags.NonPublic);
                    var files=Activator.CreateInstance(type,binding,null,new object[]{System.IO.Path.Combine(Terraria.Program.SavePath,"projection-delete-"+fault)},null);
                    using(var state=new LibraryState((JueMingR.Platform.Blueprint.IBlueprintLibraryFiles)files))
                    {
                        libraryField.SetValue(host,state);LibraryEntry asset=Seed(state,2,2,"删除目标");
                        var other=NativeBlueprintProjectionVisualChecks.Save(state,new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(),Guid.NewGuid(),"restore-cover",1,1),1,new CaptureSummary{Width=1,Height=1},new BlueprintProjectionBounds(0,0,1,1),new[]{new CaptureCell{X=0,Y=0,Layers=CaptureLayer.Tile,TileType=1}},Array.Empty<CaptureObject>()));
                        Action<LibraryEntry,int> fix=(entry,x)=>{NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Require((bool)Call(host,"Place",entry),"delete fixture instance accepted");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return Get(host,"ProjectionMode").ToString()=="Following";});Call(host,"AcceptWorldPointer",true,false,false,x,20,120,120);Call(host,"AcceptWorldPointer",true,true,false,x,20,120,120);NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return Get(host,"ProjectionMode").ToString()=="Fixed";});};
                        int count=fault=="precommit"?40:2;
                        for(int i=0;i<count;i++)fix(asset,20+i*2);
                        fix(other,20);
                        NativeQuickItemChecks.Until(()=>{Call(host,"UpdateProjectionWindow",new JueMingR.Platform.WorldTargets.WorldTargetView(0,0,120,120,0));Call(host,"Poll");return ((IEnumerable)Get(host,"ProjectionScene")).Cast<object>().All(p=>GetOptional(p,"Window")!=null);});
                        NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Require((bool)Call(host,"Place",other),"independent candidate begins before template deletion");NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return Get(host,"ProjectionMode").ToString()=="Following";});
                        var before=((IEnumerable)Get(host,"ProjectionScene")).Cast<object>().Where(p=>((BlueprintProjectionIdentity)Get(p,"Identity")).AssetId==asset.AssetId).ToDictionary(p=>((BlueprintProjectionIdentity)Get(p,"Identity")).ProjectionGeneration,p=>new object[]{Get(p,"OriginX"),Get(p,"OriginY"),Get(p,"CommitOrder"),Get(p,"Mask")});
                        Require(before.Count==count && before.Values.Any(v=>((BlueprintProjectionMask)v[3]).RemovedLayerCount>0),"restore fixture includes independent original masks, including a real permanent erased layer");
                        Set(files,"Fault",fault);Require((bool)Call(host,"DeleteAsset",asset),"real template delete accepted");
                        if(fault=="precommit")
                        {
                            var worker=Get(state,"worker");var gate=Get(worker,"gate");
                            NativeQuickItemChecks.Until(()=>{lock(gate)return GetOptional(worker,"result")!=null;});
                            lock(gate)
                            {
                                for(int attempt=0;attempt<16;attempt++)Call(host,"Poll");
                                Require((int)Call(host,"ProjectionReferenceCount",asset.AssetId)==40,"Open false during real 32-unfinished backpressure cannot drop any of 40 restore qualifications");
                                int unready=((IEnumerable)Get(worker,"projectionReferences")).Cast<object>().Select(p=>Get(p,"Value")).Count(p=>!(bool)Get(p,"Opened"));
                                Require(unready==32 && (int)Get(Get(host,"projectionRestoreAdmissions"),"Count")==8,"restoration reaches actual 32 request budget and preserves eight deferred refs");
                                Call(host,"AcceptWorldPointer",true,false,false,20,20,120,120);Call(host,"AcceptWorldPointer",true,true,false,20,20,120,120);
                                Require(Get(host,"ProjectionMode").ToString()=="Following","confirmation waits for restored opponent Open without entering an unfulfillable transaction");
                            }
                        }
                        NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !state.Snapshot.Busy && !(bool)Get(host,"deletePending");});
                        Require(Get(host,"ProjectionMode").ToString()=="Following" && ((BlueprintProjectionIdentity)Get(Get(host,"ProjectionCandidate"),"Identity")).AssetId==other.AssetId,"deletion result preserves unrelated candidate transaction");
                        if(fault=="precommit")
                        {
                            NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return ((IEnumerable)Get(host,"ProjectionScene")).Cast<object>().All(p=>(bool)Get(p,"Opened"));});
                            var restored=((IEnumerable)Get(host,"ProjectionScene")).Cast<object>().Where(p=>((BlueprintProjectionIdentity)Get(p,"Identity")).AssetId==asset.AssetId).ToArray();
                            Require(restored.Length==40 && restored.All(p=>{var values=before[((BlueprintProjectionIdentity)Get(p,"Identity")).ProjectionGeneration];return (int)Get(p,"OriginX")== (int)values[0] && (int)Get(p,"OriginY")== (int)values[1] && (long)Get(p,"CommitOrder")== (long)values[2] && ReferenceEquals(Get(p,"Mask"),values[3]) && (int)Get(Get(p,"Renderer"),"CommandCount")>0;}),"all forty original origins/orders/masks and retained rendered windows restore after backpressure");
                            Require((int)Call(host,"ProjectionReferenceCount",other.AssetId)==1 && state.DebugProjectionStatistics.PendingRequests<=32 && state.DebugProjectionStatistics.ReceiptBytes<=32L*1024*1024 && state.DebugProjectionStatistics.ActiveTemplates==2,"unrelated fixed and independent candidate survive bounded restoration");
                            Console.WriteLine("PASS Host restore count=40 initialOpen=32 deferred=8 all origins/orders/masks preserved; independent Fixed/Following retained");
                        }
                        else Require((int)Call(host,"ProjectionReferenceCount",asset.AssetId)==0,"commit unknown does not reopen/restore retired template refs");
                        Call(host,"ClearProjection");
                    }
                }
            }
            finally{Call(host,"ClearProjection");libraryField.SetValue(host,original);}
        }
    }
}
