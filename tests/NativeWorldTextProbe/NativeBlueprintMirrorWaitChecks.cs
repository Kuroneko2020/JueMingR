using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.WorldTargets;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Localization;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Real capture/file/profile, common dispatcher, Host and native section
    // availability. Controlled CPU pointer samples certify neither OS input nor
    // Main.Run; Unknown/Demand and source completion are never injected here.
    internal static class NativeBlueprintMirrorWaitChecks
    {
        private const int OriginX=199,OriginY=42,ObservationUpdates=64;
        internal static void Run(object context)
        {
            var host=Get(context,"Blueprint");var input=Get(context,"Input");var shell=Get(context,"Shell");
            var oldLibrary=(LibraryState)Get(host,"Library");var oldHistory=(BlueprintSceneHistory)Get(host,"projectionHistory");
            var oldWorld=Main.ActiveWorldFileData;bool oldMenu=Main.gameMenu,oldAutomatic=(bool)Get(host,"AutoBuildEnabled");
            var connection=Netplay.Connection;var client=Main.clientPlayer;var buffer=NetMessage.buffer[256];
            var inventory=Main.LocalPlayer.inventory;bool playerInventory=Main.playerInventory,mouseInterface=Main.LocalPlayer.mouseInterface;
            string language=Language.ActiveCulture.Name;
            // SavePath is already isolated by the runner. A short leaf avoids
            // adding another long GUID above immutable asset file names.
            string root=Path.Combine(Terraria.Program.SavePath,"mirror-wait-"+Guid.NewGuid().ToString("N").Substring(0,8));
            var library=new LibraryState(new FileBlueprintLibrary(Path.Combine(root,"templates")));
            var history=new BlueprintSceneHistory(scope=>new AtomicFileDocument(Path.Combine(root,"instances",scope,"scene.bin"),int.MaxValue,true,".schema1-original"));
            Action step=()=>{NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());NativeQuickItemChecks.BeginWorldStep();Call(context,"UpdateRuntime");Call(host,"Poll");};
            Main.gameMenu=true;Call(context,"UpdateRuntime");var finite=NativeBlueprintWorldMatchChecks.EmptyWorld(host,512,300);
            try
            {
                Set(host,"<Library>k__BackingField",library);Set(host,"projectionHistory",history);
                Main.ActiveWorldFileData=new Terraria.IO.WorldFileData(Path.Combine(root,"world.wld"),false){UniqueId=Guid.NewGuid()};
                Main.LocalPlayer.inventory=Enumerable.Range(0,inventory.Length).Select(_=>new Item()).ToArray();
                Main.playerInventory=false;Main.LocalPlayer.mouseInterface=false;Main.gameMenu=false;
                NativeQuickItemChecks.Until(()=>{step();return (bool)Get(host,"ProjectionPersistenceReady") && (bool)Get(host,"ControlsEnabled");});
                Call(host,"SetAutoBuild",false);Call(shell,"CloseAndSubmitPosition");
                ((BlueprintCaptureState)Get(host,"State")).Exit();
                NativeBlueprintWorldMatchChecks.InitializeMetadata();Framing.Initialize();Main.SetupTileMerge();
                WorldGen.PlaceWall(30,40,7,true);WorldGen.PlaceWall(31,40,7,true);WorldGen.PlaceWire(31,40);
                Require(Main.tile[30,40].wall==7 && Main.tile[31,40].wall==7 && Main.tile[31,40].wire(),"literal original source is two walls plus one red wire");
                var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,library,step,30,40,2,1,"未加载区域镜像等待");
                Require(entry.Width==2 && entry.Height==1 && entry.ContentCount==2,"production capture/file retains the independently listed two source records");

                var registry=new HotkeyRegistry();Call(host,"Register",registry,null);
                using(var bindings=new HotkeyBindings(registry,new AtomicFileDocument(Path.Combine(root,"hotkeys.json"),65536)))
                {
                    NativeQuickItemChecks.Until(()=>{bindings.Poll();return bindings.Loaded;});
                    HotkeyChord chord;string reason;Require(HotkeyChord.TryParse("F16",out chord,out reason),"independent public mirror chord parses");
                    long command;Require(bindings.TrySet("blueprint.mirror",chord,null,out command,out reason),"production mirror registration accepts its public chord: "+reason);
                    NativeQuickItemChecks.Until(()=>{bindings.Poll();return !bindings.Busy;});
                    Require(bindings.CompletionSucceeded && bindings.Get("blueprint.mirror").Equals(chord),"only acknowledged real binding is dispatched");
                    Netplay.Connection=new RemoteServer{PendingTermination=true,State=10};Main.clientPlayer=new Player();NetMessage.buffer[256]=new MessageBuffer();
                    Main.netMode=1;step();
                    NativeQuickItemChecks.Until(()=>{step();return (bool)Get(host,"ProjectionPersistenceReady") && (bool)Get(host,"ControlsEnabled");});
                    long uses=(long)Get(host,"BuildUsesStarted");
                    StableThenRecover(host,input,library,bindings,entry,step,false,true,false);
                    StableThenRecover(host,input,library,bindings,entry,step,true,false,true);
                    CancelledWait(host,input,library,bindings,entry,step);
                    Require((long)Get(host,"BuildUsesStarted")==uses && Main.LocalPlayer.inventory.All(item=>item==null || item.IsAir || item.stack==0),"wait/recovery/cancel start no native use and consume no inventory, with AutoBuild preference both OFF and ON");
                }
                Console.WriteLine("PASS Blueprint mirror wait: real saved two-wall/red-wire source across received 199/unreceived 200, Current+Unknown with actual queues drained; stable source requests/worker requests/source record steps/world reads/raw stamps, AutoBuild OFF and retained-source ON, actual catalog revocation/rebuild with same intent and stale receipt rejection, native section wake to automatic mirror or real-Match refusal, same-action cancellation/finite tail/no resurrection. Cold source single-click coverage remains in NativeBlueprintMirrorTrimChecks. Controlled native CPU stages only; normal game/OS input/FPS/multiplayer acceptance NOT_EXECUTED.");
            }
            finally
            {
                Call(host,"SetAutoBuild",oldAutomatic);Main.gameMenu=true;Call(context,"UpdateRuntime");
                Require(history.Stop(3000),"mirror-wait scene worker stops before ownership restoration");
                Require(library.Stop(3000),"mirror-wait library worker stops before ownership restoration");
                Set(host,"<Library>k__BackingField",oldLibrary);Set(host,"projectionHistory",oldHistory);finite.Dispose();
                Netplay.Connection=connection;Main.clientPlayer=client;NetMessage.buffer[256]=buffer;Main.ActiveWorldFileData=oldWorld;
                Main.LocalPlayer.inventory=inventory;Main.playerInventory=playerInventory;Main.LocalPlayer.mouseInterface=mouseInterface;
                if(Language.ActiveCulture.Name!=language){LanguageManager.Instance.SetLanguage(language);Lang.InitializeLegacyLocalization();}
                Main.gameMenu=oldMenu;if(!oldMenu)step();
            }
        }

        private static void StableThenRecover(object host,object input,LibraryState library,HotkeyBindings bindings,LibraryEntry entry,Action step,bool automatic,bool replaceCatalog,bool exposeMatch)
        {
            var item=PlaceUnknown(host,input,library,entry,step,automatic,exposeMatch);
            var original=(BlueprintProjectionMask)Get(item,"Mask");long scene=(long)Get(host,"projectionSceneRevision");
            var identity=(BlueprintProjectionIdentity)Get(item,"Identity");
            DispatchMirror(host,input,bindings);
            Require((bool)Get(host,"OwnMirrorCancellation") && Get(host,"ProjectionMode").ToString()=="SelectingMirror","real common dispatcher enters mirror selection before the one target click");
            Pointer(host,false);Pointer(host,true);Pointer(host,false);
            NativeQuickItemChecks.Until(()=>{step();return ReferenceEquals(GetOptional(host,"editorTarget"),item) && CurrentAndDrained(host,item,library);});
            AssertUnknown(host,item);
            if(automatic)Require((int)Get(Get(host,"buildSources"),"Count")>0,"AutoBuild ON actually retained this match source; worker request counts alone cannot detect its replay");
            ObserveStable(host,item,library,step,"waiting AutoBuild="+automatic);

            if(replaceCatalog)
            {
                var prior=(BlueprintProjectionQualificationReceipt)Get(item,"Qualification");
                string other=Language.ActiveCulture.Name=="zh-Hans"?"en-US":"zh-Hans";
                LanguageManager.Instance.SetLanguage(other);Lang.InitializeLegacyLocalization();step();
                Require(GetOptional(host,"MaterialCatalog")==null && GetOptional(item,"Match")==null && (bool)Get(host,"OwnMirrorCancellation") && ReferenceEquals(GetOptional(host,"editorTarget"),item),"real catalog invalidation withdraws the old proof without discarding the already selected mirror intent");
                NativeQuickItemChecks.Until(()=>{step();return CurrentAndDrained(host,item,library);});
                AssertUnknown(host,item);
                var current=(BlueprintProjectionQualificationReceipt)Get(item,"Qualification");var proof=Get(item,"Match");
                Require(current.CapabilityRevision>prior.CapabilityRevision,"actual catalog rebuild binds a newer native qualification identity");
                Call(host,"AcceptProjectionQualification",item,prior);
                Require(ReferenceEquals(Get(item,"Qualification"),current) && ReferenceEquals(Get(item,"Match"),proof),"late genuine old-catalog qualification cannot replace the current same-intent proof");
                ObserveStable(host,item,library,step,"waiting after real catalog replacement");
            }

            var before=Observe(host,item,library);
            Main.sectionManager.SetSectionLoaded(1,0);
            Require(Main.sectionManager.TileLoaded(200,OriginY),"original section receipt makes only the genuinely missing section readable");
            NativeQuickItemChecks.Until(()=>{step();return !(bool)Get(host,"OwnMirrorCancellation");});
            var currentItem=Scene(host).Single(value=>((BlueprintProjectionIdentity)Get(value,"Identity")).ProjectionGeneration==identity.ProjectionGeneration);
            Require((bool)Get(currentItem,"MirroredHorizontally")==!exposeMatch,"the same click automatically mirrors known missing targets, or refuses a newly visible real Match");
            Require((int)Get(currentItem,"OriginX")==OriginX && (int)Get(currentItem,"OriginY")==OriginY && ReferenceEquals(Get(currentItem,"Mask"),original),"section recovery preserves the original origin and source mask");
            if(exposeMatch)Require((long)Get(host,"projectionSceneRevision")==scene,"real-Match refusal publishes no scene change");
            Require((long)Get(host,"ProjectionMatchSourceSteps")>before.Steps && (long)Get(host,"ProjectionMatchCellReads")>before.Reads,"native section receipt actually wakes source record consumption and world sampling");
            Require((bool)Get(host,"AutoBuildEnabled")==automatic,"editing/recovery preserves the requested automatic-build preference");
            Require(Main.tile[199,OriginY].wall==0 && Main.tile[200,OriginY].wall==(exposeMatch?7:0) && !Main.tile[200,OriginY].wire(),"mirror and refusal leave the literal destination world unchanged");
            Console.WriteLine("BlueprintMirrorWait recovered automatic="+automatic+" catalogReplacement="+replaceCatalog+" actualMatch="+exposeMatch+" sourceStepsDelta="+((long)Get(host,"ProjectionMatchSourceSteps")-before.Steps)+" worldReadsDelta="+((long)Get(host,"ProjectionMatchCellReads")-before.Reads));
            Call(host,"ClearProjection");
        }

        private static void CancelledWait(object host,object input,LibraryState library,HotkeyBindings bindings,LibraryEntry entry,Action step)
        {
            var item=PlaceUnknown(host,input,library,entry,step,false,false);var original=(BlueprintProjectionMask)Get(item,"Mask");
            long scene=(long)Get(host,"projectionSceneRevision");DispatchMirror(host,input,bindings);
            Require((bool)Get(host,"OwnMirrorCancellation"),"real common dispatcher enters the cancellable mirror operation");
            Pointer(host,false);Pointer(host,true);Pointer(host,false);
            NativeQuickItemChecks.Until(()=>{step();return ReferenceEquals(GetOptional(host,"editorTarget"),item) && CurrentAndDrained(host,item,library);});
            AssertUnknown(host,item);
            // A real required-wall write revokes this range and may already
            // authorize finite normal Match work. Cancel does not need to undo
            // that work; only the retired editor must stop granting new demand.
            WorldGen.PlaceWall(199,OriginY,7,true);step();DispatchMirror(host,input,bindings);
            Require(!(bool)Get(host,"ProjectionEditing") && ReferenceEquals(Get(item,"Mask"),original) && (long)Get(host,"projectionSceneRevision")==scene,"same production mirror binding cancels the waiting operation without a permanent mutation");
            NativeQuickItemChecks.Until(()=>{step();return CurrentAndDrained(host,item,library);});
            ObserveStable(host,item,library,step,"cancelled after allowed finite Match tail",false);
            Main.sectionManager.SetSectionLoaded(1,0);
            NativeQuickItemChecks.Until(()=>{step();return CurrentAndDrained(host,item,library);});
            Require((long)Get(Get(item,"Match"),"Unknown")==0 && !(bool)Get(item,"MirroredHorizontally") && !(bool)Get(host,"ProjectionEditing"),"late real section recovery cannot resurrect the cancelled click");
            ObserveStable(host,item,library,step,"cancelled after late section arrival",false);
            Require(ReferenceEquals(Get(item,"Mask"),original) && (long)Get(host,"projectionSceneRevision")==scene,"cancel tail and late section receipt leave exact scene facts intact");
            Call(host,"ClearProjection");
        }

        private static object PlaceUnknown(object host,object input,LibraryState library,LibraryEntry entry,Action step,bool automatic,bool exposeMatch)
        {
            Call(host,"SetAutoBuild",false);Call(host,"ClearProjection");
            // Setup uses original writers before observing the new proof. The
            // right wall is unreadable until its section arrives in the ON arm.
            if(Main.tile[200,OriginY].wall!=0)WorldGen.KillWall(200,OriginY);
            if(Main.tile[199,OriginY].wire())WorldGen.KillWire(199,OriginY);
            if(exposeMatch)WorldGen.PlaceWall(200,OriginY,7,true);
            Main.netMode=1;Main.sectionManager=new WorldSections(3,2);Main.sectionManager.SetSectionLoaded(0,0);step();
            Require(Main.sectionManager.TileLoaded(199,OriginY) && !Main.sectionManager.TileLoaded(200,OriginY),"literal target straddles a genuinely received/unreceived native section boundary");
            Call(host,"SetAutoBuild",automatic);
            var item=NativeBlueprintWorldMatchChecks.Place(host,input,entry,OriginX,OriginY,step);
            NativeQuickItemChecks.Until(()=>{Call(host,"UpdateProjectionWindow",new WorldTargetView(190,34,32,20,0));step();return GetOptional(item,"Window")!=null && CurrentAndDrained(host,item,library);});
            AssertUnknown(host,item);Require(GetOptional(item,"MirrorProfile")!=null && (bool)Get(Get(item,"MirrorProfile"),"Ready"),"real locked native horizontal profile participates in the formal source/view path");
            return item;
        }
        private static void DispatchMirror(object host,object input,HotkeyBindings bindings)
        {
            NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());NativeQuickItemChecks.Sample(input,new[]{Keys.F16});
            bindings.Dispatch((HotkeyInput)Get(input,"Hotkeys"),HotkeyContext.Multiplayer,true,onlyAction:"blueprint.mirror");
            // Preserve the real dispatcher-before-Host input order. Pointer
            // samples below use the existing trusted CPU harness entry.
            Call(host,"ProcessInput");NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());
        }
        private static void Pointer(object host,bool left)
        { Call(host,"AcceptWorldPointer",true,left,false,OriginX,OriginY,Main.maxTilesX,Main.maxTilesY); }
        private static object[] Scene(object host)
        { return ((IEnumerable)Get(host,"ProjectionScene")).Cast<object>().ToArray(); }
        private static bool CurrentAndDrained(object host,object item,LibraryState library)
        {
            var state=GetOptional(item,"Match");if(state==null || !(bool)Call(host,"ProjectionQualificationCurrent",item))return false;
            var ranges=((IEnumerable)Get(state,"Ranges")).Cast<object>().ToArray();var sources=library.DebugProjectionSourceStatistics;
            return ranges.Length>0 && (int)Get(state,"Current")==ranges.Length && ranges.All(range=>(bool)Get(range,"Current") && !(bool)Get(range,"Demand") && !(bool)Get(range,"Reading")) &&
                !(bool)Get(host,"ProjectionMatchReading") && sources.PendingRequests==0 && sources.ReceiptBytes==0 && sources.WorkingBytes==0 && library.DebugProjectionStatistics.PendingRequests==0;
        }
        private static void AssertUnknown(object host,object item)
        {
            var state=Get(item,"Match");
            Require((long)Get(state,"Targets")==3 && (long)Get(state,"Unknown")==2 && (long)Get(state,"Matches")==0 && !(bool)Get(item,"MirroredHorizontally"),"independent three-layer target: received wall is missing, unreceived wall/red-wire are Unknown, and no Match authorizes refusal");
            Require(Call(host,"ProjectionMoveEligibility",item).ToString()=="Checking","Current+Unknown remains ineligible instead of being treated as zero");
        }
        private sealed class WorkObservation
        {
            internal long Requests,WorkerRequests,Steps,Reads;
            internal string Stamps;
            public override string ToString(){return Requests+"/"+WorkerRequests+"/"+Steps+"/"+Reads+" stamps="+Stamps;}
        }
        private static WorkObservation Observe(object host,object item,LibraryState library)
        {
            var ranges=((IEnumerable)Get(Get(item,"Match"),"Ranges")).Cast<object>();
            return new WorkObservation{Requests=(long)Get(host,"ProjectionMatchSourceRequests"),WorkerRequests=library.DebugProjectionSourceStatistics.Requests,
                Steps=(long)Get(host,"ProjectionMatchSourceSteps"),Reads=(long)Get(host,"ProjectionMatchCellReads"),
                Stamps=string.Join(";",ranges.Select(range=>Get(range,"RawVersion")+"/"+Get(range,"Version")+"/"+Get(Get(range,"Group"),"NativeStamp")))};
        }
        private static void ObserveStable(object host,object item,LibraryState library,Action step,string label,bool editing=true)
        {
            Require(CurrentAndDrained(host,item,library),"observation starts only after actual current proof/source queues drain: "+label);
            var before=Observe(host,item,library);
            // This is a finite post-drain observation, not a guessed completion
            // delay. Two records fit one normal source slice; 64 real Updates
            // with worker opportunities exceed two old repeating cycles.
            for(int i=0;i<ObservationUpdates;i++)
            {
                step();Thread.Sleep(2);var after=Observe(host,item,library);
                Require(CurrentAndDrained(host,item,library) && after.Requests==before.Requests && after.WorkerRequests==before.WorkerRequests && after.Steps==before.Steps && after.Reads==before.Reads && after.Stamps==before.Stamps,
                    "stable same-version mirror wait must converge: "+label+" update="+i+" before="+before+" after="+after);
                Require((bool)Get(host,"OwnMirrorCancellation")==editing && !(bool)Get(item,"MirroredHorizontally"),"waiting/cancelled editor ownership remains exact without another click: "+label);
            }
            Console.WriteLine("BlueprintMirrorWait stable "+label+" updates="+ObservationUpdates+" sourceRequests/workerRequests/sourceSteps/worldReads="+before);
        }
    }
}
