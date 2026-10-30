using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.WorldTargets;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Real Host/worker/scene owners with controlled trusted pointer samples and
    // certificate consumption. No OS input, normal game startup or GPU is implied.
    internal static class NativeBlueprintMirrorTrimChecks
    {
        private const BindingFlags F=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
        internal static void Run(object context)
        {
            var host=Get(context,"Blueprint");var input=Get(context,"Input");var shell=Get(context,"Shell");
            var oldLibrary=(LibraryState)Get(host,"Library");var oldHistory=(BlueprintSceneHistory)Get(host,"projectionHistory");
            var oldWorld=Main.ActiveWorldFileData;bool oldMenu=Main.gameMenu;bool oldAuto=(bool)Get(host,"AutoBuildEnabled");
            string root=Path.Combine(Terraria.Program.SavePath,"blueprint-mirror-trim-"+Guid.NewGuid().ToString("N"));
            var library=new LibraryState(new FileBlueprintLibrary(Path.Combine(root,"templates")));
            var history=new BlueprintSceneHistory(scope=>new AtomicFileDocument(Path.Combine(root,"instances",scope,"scene.bin"),int.MaxValue,true,".schema1-original"),
                (file,value)=>{if(value.SourceVersion==1)((AtomicFileDocument)file).RetainLoadedSource();});
            Action step=()=>{NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());NativeQuickItemChecks.BeginWorldStep();Call(context,"UpdateRuntime");Call(host,"Poll");};
            Main.gameMenu=true;Call(context,"UpdateRuntime");var finite=NativeBlueprintWorldMatchChecks.EmptyWorld(host,512,160);
            try
            {
                Set(host,"<Library>k__BackingField",library);Set(host,"projectionHistory",history);
                Main.ActiveWorldFileData=new Terraria.IO.WorldFileData(Path.Combine(root,"world.wld"),false){UniqueId=Guid.NewGuid()};Main.gameMenu=false;
                NativeQuickItemChecks.Until(()=>{step();return (bool)Get(host,"ProjectionPersistenceReady");});
                NativeBlueprintWorldMatchChecks.InitializeMetadata();Framing.Initialize();Main.SetupTileMerge();Call(host,"SetAutoBuild",false);Call(shell,"CloseAndSubmitPosition");
                var first=new CaptureCell{X=0,Y=0,Layers=CaptureLayer.Tile|CaptureLayer.Wall|CaptureLayer.Wire|CaptureLayer.Actuator|CaptureLayer.Liquid,TileType=1,Slope=1,WallType=7,Wires=5,Actuator=true,LiquidAmount=80,LiquidKind=0};
                var last=new CaptureCell{X=4,Y=1,Layers=CaptureLayer.Tile,TileType=1,Slope=2};
                var entry=NativeBlueprintBudgetChecks.Save(library,new[]{first,last},5,2);
                object target=NativeBlueprintWorldMatchChecks.Place(host,input,entry,100,40,step);
                object sibling=NativeBlueprintWorldMatchChecks.Place(host,input,entry,130,40,step);
                long identity=((BlueprintProjectionIdentity)Get(target,"Identity")).ProjectionGeneration;long order=(long)Get(target,"CommitOrder");
                // Superseding an accepted cold pick must discard that request,
                // even if its exact source chunk arrives during the new editor.
                Call(host,"DropProjectionWindows",target);NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Call(host,"MirrorProjection");
                Pointer(host,false,100,40);Pointer(host,true,100,40);
                Require((bool)Get(host,"editorPickPending"),"cold mirror pick freezes one request before data arrival");
                Call(host,"CancelProjection");NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Call(host,"TrimProjection");
                for(int i=0;i<8;i++)step();
                Require(Get(host,"ProjectionMode").ToString()=="SelectingTrim" && GetOptional(host,"editorTarget")==null && !(bool)Get(target,"MirroredHorizontally"),"late mirror pick cannot select or publish in the next trim request");
                Call(host,"CancelProjection");
                // This controlled scene publication changes only the epoch;
                // it is sufficient to distinguish a stale worker point reply.
                Call(host,"DropProjectionWindows",target);NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Call(host,"TrimProjection");
                Pointer(host,false,100,40);Pointer(host,true,100,40);
                Require((bool)Get(host,"editorPickPending"),"cold trim pick freezes one request before data arrival");
                Set(host,"projectionSceneRevision",(long)Get(host,"projectionSceneRevision")+1);
                NativeQuickItemChecks.Until(()=>{step();return !(bool)Get(host,"ProjectionEditing");});
                Require(!(bool)Get(target,"MirroredHorizontally") && ((BlueprintProjectionMask)Get(target,"Mask")).RemovedLayerCount==0,"scene publication invalidates the old cold trim request without mutation");
                // Evict the clicked core: one accepted click must continue its
                // own finite point load, rather than ask the user to click again.
                Call(host,"DropProjectionWindows",target);NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Call(host,"MirrorProjection");
                Pointer(host,false,100,40);Pointer(host,true,100,40);
                NativeQuickItemChecks.Until(()=>{step();return !(bool)Get(host,"ProjectionEditing");});
                target=Scene(host).Single(p=>((BlueprintProjectionIdentity)Get(p,"Identity")).ProjectionGeneration==identity);
                Require((bool)Get(target,"MirroredHorizontally") && (int)Get(target,"OriginX")==100 && (int)Get(target,"OriginY")==40 && (long)Get(target,"CommitOrder")>order,"one cold click mirrors same instance at original bbox and takes latest order");
                Require(!(bool)Get(sibling,"MirroredHorizontally"),"same-template sibling pose is independent");
                long mirroredOrder=(long)Get(target,"CommitOrder");
                Load(host,target,step);var summary=(CaptureSummary)Get(target,"Summary");Require(summary.Width==5 && summary.Height==2,"odd mirror retains original effective size");
                BeginTrim(host,input,target,104,40,step,true);Pointer(host,false,104,40);Pointer(host,true,104,40);Pointer(host,false,104,40);
                NativeQuickItemChecks.Until(()=>{step();return (bool)Get(host,"CanInvokeConfirmTrim");});
                var before=(BlueprintProjectionMask)Get(target,"Mask");long revision=(long)Get(host,"projectionSceneRevision");
                Require(before.RemovedLayerCount==0 && Get(host,"ProjectionMode").ToString()=="TrimPreview","release leaves immutable preview without erasing or dirtying scene");
                Call(host,"CancelProjection");Require(ReferenceEquals(Get(target,"Mask"),before) && (long)Get(host,"projectionSceneRevision")==revision,"cancel discards preview and leaves scene facts exact");
                BeginTrim(host,input,target,104,40,step);Pointer(host,false,104,40);Pointer(host,true,104,40);Pointer(host,false,104,40);
                NativeQuickItemChecks.Until(()=>{step();return (bool)Get(host,"CanInvokeConfirmTrim");});
                Certificate(host,input,true,true);Require(ReferenceEquals(Get(target,"Mask"),before) && (bool)Get(host,"CanInvokeConfirmTrim"),"current native UI ownership rejects matching Mouse1 confirmation without replacement drag");
                Certificate(host,input,false,false);Require(ReferenceEquals(Get(target,"Mask"),before) && (bool)Get(host,"CanInvokeConfirmTrim"),"missing Draw certificate rejects rather than replaying a confirmation");
                Certificate(host,input,false,true);
                Require(!(bool)Get(host,"ProjectionEditing") && (long)Get(host,"projectionSceneRevision")==revision+1,"certified independent Mouse1 confirmation publishes exactly once and exits");
                var mask=(BlueprintProjectionMask)Get(target,"Mask");Require(mask.RemovedLayerCount==6 && !mask.Apply(first).HasContent && mask.Apply(last).HasContent,"mirrored display column four permanently trims source zero all six layers, preserving other corner");
                Require((long)Get(target,"CommitOrder")==mirroredOrder,"trim does not acquire another cover order");
                Require(((BlueprintProjectionMask)Get(sibling,"Mask")).RemovedLayerCount==0,"trim never masks same-id sibling");
                NativeQuickItemChecks.Until(()=>{step();return history.Saved;});
                string scope=(string)Get(history,"requestedScope");var doc=BlueprintSceneCodec.Decode(File.ReadAllBytes(Path.Combine(root,"instances",scope,"scene.bin")),scope,512,160);
                var record=doc.Records.Single(r=>r.Order==(long)Get(target,"CommitOrder"));Require(record.MirroredHorizontally && record.Mask.RemovedLayerCount==6,"normal reliable snapshot saves committed pose and source trim only");
                Main.gameMenu=true;Call(context,"UpdateRuntime");Main.gameMenu=false;
                NativeQuickItemChecks.Until(()=>{step();return Scene(host).Length==2 && Scene(host).All(p=>(bool)Get(p,"Opened"));});
                target=Scene(host).Single(p=>(long)Get(p,"CommitOrder")==mirroredOrder);
                NativeQuickItemChecks.Until(()=>{step();var match=GetOptional(target,"Match");return (bool)Call(host,"ProjectionQualificationCurrent",target) && match!=null && (int)Get(match,"Current")==((Array)Get(match,"Ranges")).Length;});
                Require((bool)Get(target,"MirroredHorizontally") && ((BlueprintProjectionMask)Get(target,"Mask")).RemovedLayerCount==6 &&
                    GetOptional(target,"MirrorProfile")!=null && (long)Get(Get(target,"Match"),"Unknown")==0 && !(bool)Get(host,"ProjectionEditing"),"real same-scope session reentry binds a fresh profile and rebuilds complete non-Unknown effective targets from saved mirrored pose/source mask");
                var oldIdentity=(BlueprintProjectionIdentity)Get(target,"Identity");
                Require(library.RequestProjectionWindow(oldIdentity,99003,new BlueprintProjectionBounds(0,0,5,2)),"old scope has an accepted source request before the real session switch");
                Main.gameMenu=true;Call(context,"UpdateRuntime");
                Main.ActiveWorldFileData=new Terraria.IO.WorldFileData(Path.Combine(root,"other.wld"),false){UniqueId=Guid.NewGuid()};
                var identityType=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Map.LocalWorldIdentity",true);
                string v1Scope=(string)identityType.GetMethod("Observe",F).Invoke(null,null);
                string v1Path=Path.Combine(root,"instances",v1Scope,"scene.bin");Directory.CreateDirectory(Path.GetDirectoryName(v1Path));
                byte[] v1=FrozenV1(v1Scope,new BlueprintSceneRecord(record.Template,170,40,1,record.Mask));File.WriteAllBytes(v1Path,v1);
                Main.gameMenu=false;
                NativeQuickItemChecks.Until(()=>{step();return Scene(host).Length==1 && (bool)Get(Scene(host)[0],"Opened");});
                var legacy=Scene(host)[0];
                NativeQuickItemChecks.Until(()=>{step();var match=GetOptional(legacy,"Match");return (bool)Call(host,"ProjectionQualificationCurrent",legacy) && match!=null && (int)Get(match,"Current")==((Array)Get(match,"Ranges")).Length;});
                Require(!(bool)Get(legacy,"MirroredHorizontally") && (int)Get(legacy,"OriginX")==170 && ((BlueprintProjectionMask)Get(legacy,"Mask")).RemovedLayerCount==6 &&
                    (long)Get(Get(legacy,"Match"),"Unknown")==0 && ((BlueprintProjectionIdentity)Get(legacy,"Identity")).SessionGeneration!=oldIdentity.SessionGeneration &&
                    File.ReadAllBytes(v1Path).SequenceEqual(v1) && !File.Exists(v1Path+".schema1-original"),"hand-written old v1 restores normal pose and exact source mask through the same ready profile; load does not rewrite, and late old-scope source cannot resurrect old instances");
                Console.WriteLine("PASS Blueprint mirror/trim Host: cold single click in both editors, stale request/scene rejection, same instance/order/bbox, independent sibling, release preview/cancel exactness, UI/missing-Draw rejection and unique certified Mouse1 commit, all-layer source mask, v2 snapshot, same-scope mirrored reentry/current Match, hand-written v1 normal qualification and old-scope retirement. Controlled CPU certificates are not real GPU/OS input.");
            }
            finally
            {
                Call(host,"SetAutoBuild",oldAuto);Main.gameMenu=true;Call(context,"UpdateRuntime");history.Stop(3000);library.Stop(3000);
                Set(host,"<Library>k__BackingField",oldLibrary);Set(host,"projectionHistory",oldHistory);finite.Dispose();Main.ActiveWorldFileData=oldWorld;Main.gameMenu=oldMenu;
                if(!oldMenu)step();
            }
        }
        private static void Certificate(object host,object input,bool nativeUi,bool complete)
        {
            HotkeyChord chord;string reason;Require(HotkeyChord.TryParse("Mouse1",out chord,out reason),"public Mouse1 syntax");
            NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Call(host,"RequestTrimConfirm",chord);
            var pointer=Get(host,"Pointer");long mapping=(long)Get(input,"Mapping");
            Set(pointer,"pending",true);Set(pointer,"mapping",mapping);Set(pointer,"completed",complete);Set(pointer,"drawn",complete);Set(pointer,"ui",nativeUi);Set(pointer,"left",true);Set(pointer,"right",false);
            NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Call(pointer,"Consume",true);
        }
        private static void BeginTrim(object host,object input,object target,int x,int y,Action step,bool cold=false)
        {
            if(cold)Call(host,"DropProjectionWindows",target);else Load(host,target,step);
            NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Call(host,"TrimProjection");Pointer(host,false,x,y);Pointer(host,true,x,y);
            if(cold)Require((bool)Get(host,"editorPickPending"),"cold trim waits on the original accepted pick");
            NativeQuickItemChecks.Until(()=>{step();return Get(host,"ProjectionMode").ToString()=="TrimReady";});
            Require(((BlueprintProjectionMask)Get(target,"Mask")).RemovedLayerCount==0,"target selection alone cannot delete");
        }
        private static void Load(object host,object item,Action step)
        { NativeQuickItemChecks.Until(()=>{Call(host,"UpdateProjectionWindow",new WorldTargetView((int)Get(item,"OriginX"),(int)Get(item,"OriginY"),16,16,0));step();return GetOptional(item,"Window")!=null;}); }
        private static void Pointer(object host,bool down,int x,int y) { Call(host,"AcceptWorldPointer",true,down,false,x,y,Main.maxTilesX,Main.maxTilesY); }
        private static object[] Scene(object host) { return ((IEnumerable)Get(host,"ProjectionScene")).Cast<object>().ToArray(); }
        private static void Set(object target,string field,object value) { target.GetType().GetField(field,F).SetValue(target,value); }
        // Independent legacy writer: deliberately does not use current Encode,
        // so upgrading the production codec cannot silently upgrade this oracle.
        private static byte[] FrozenV1(string scope,BlueprintSceneRecord record)
        {
            using(var stream=new MemoryStream())
            {
                using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
                {
                    writer.Write(0x3130435342524d4aUL);writer.Write(1);
                    for(int i=0;i<32;i++)writer.Write(Convert.ToByte(scope.Substring(i*2,2),16));
                    writer.Write(512);writer.Write(160);writer.Write(1L);writer.Write(1);
                    writer.Write(record.Template.AssetId.ToByteArray());writer.Write(record.Template.ContentRevision.ToByteArray());writer.Write(Convert.FromBase64String(record.Template.ContentHash));
                    writer.Write(record.OriginX);writer.Write(record.OriginY);writer.Write(record.Order);writer.Write(record.Mask.RemovedLayerCount);writer.Write(record.Mask.RemovedConstructionLayerCount);
                    // Literal old source mask at (0,0): Tile1, Wall2,
                    // Liquid4, Actuator8, red16 and blue64; no object expansion.
                    writer.Write(1);writer.Write(0L);writer.Write((ushort)95);writer.Write(0);
                }
                byte[] payload=stream.ToArray();using(var sha=SHA256.Create())stream.Write(sha.ComputeHash(payload),0,32);return stream.ToArray();
            }
        }
    }
}
