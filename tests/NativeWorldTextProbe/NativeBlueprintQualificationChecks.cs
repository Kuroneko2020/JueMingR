using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Platform.WorldTargets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // Source setup uses the original frame writer; only production capture,
    // file reads, qualification, observer and Host consumers provide evidence.
    // Natural source setup is not a claim that a player can place that output.
    internal static class NativeBlueprintQualificationChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static void Run(object context)
        {
            var host = Get(context,"Blueprint"); var tools = Get(context,"Tools"); var input = Get(context,"Input");
            var originalLibrary = (LibraryState)Get(host,"Library");
            var libraryField = host.GetType().GetField("<Library>k__BackingField",F);
            bool automatic = (bool)Get(host,"AutoBuildEnabled"), materialDemand = (bool)Get(host,"materialViewDemand");
            var player = Main.LocalPlayer; var inventory = player.inventory; int selected = player.selectedItem;
            var position = player.position; var velocity = player.velocity; var camera = Main.screenPosition;
            var zoom = Main.GameViewMatrix.Zoom; int direction = player.direction; float gravity = player.gravDir;
            double surface = Main.worldSurface, time = Main.time; bool day = Main.dayTime, raining = Main.raining;
            string language = Language.ActiveCulture.Name; var lighting = Lighting.Mode;
            var outlets = new Harmony("JueMingR.Tests.BlueprintQualificationOutlets");
            LibraryState library = null;
            try
            {
                Call(Get(context,"Shell"),"CloseAndSubmitPosition");
                Call(Get(Get(context,"Shell"),"BlueprintMaterialsUi"),"Close");
                Call(host,"SetAutoBuild",false); Call(host,"SetMaterialViewDemand",false);
                Call(host,"CancelCaptureOrDraft"); Call(host,"ClearProjection");
                NativeQuickItemChecks.Until(()=>{Call(host,"Poll");return !originalLibrary.Snapshot.Busy && !(bool)Get(host,"discardPending");});
                ((BlueprintCaptureState)Get(host,"State")).Exit();
                // The runner already isolates Program.SavePath per process.
                // Keep this leaf short: a full GUID here pushes production
                // immutable chunk/metadata paths beyond the native MAX_PATH.
                library = new LibraryState(new FileBlueprintLibrary(Path.Combine(Terraria.Program.SavePath,"qualification")));
                libraryField.SetValue(host,library);
                using(NativeBlueprintWorldMatchChecks.EmptyWorld(host,1024,160))
                {
                    player.inventory = Enumerable.Range(0,inventory.Length).Select(_=>new Item()).ToArray();
                    NativeToolExecutionChecks.Initialize(context,null);
                    Framing.Initialize(); Main.SetupTileMerge(); TorchID.Initialize();
                    Lighting.Mode = Terraria.Graphics.Light.LightMode.Color;
                    foreach(string name in new[]{"HandleSpecialEvent","HandleMining","HandleRunning"})
                        outlets.Patch(typeof(Terraria.GameContent.Achievements.AchievementsHelper).GetMethod(name,F),prefix:new HarmonyMethod(typeof(NativeBlueprintQualificationChecks),nameof(SkipOutlet)));
                    NativeToolExecutionChecks.Reset(context,tools,input,ItemID.CopperPickaxe,0,0);
                    player.releaseUseItem = true; player.builderAccStatus[10] = 0;
                    Action step = ()=>{NativeQuickItemChecks.BeginWorldStep();Call(context,"UpdateRuntime");};
                    NativeQuickItemChecks.Sample(input,Array.Empty<Keys>()); step();
                    NativeQuickItemChecks.Until(()=>{step();return (bool)Get(host,"ControlsEnabled");});

                    WriteObject(20,20,187,15); WriteObject(24,20,624,0);
                    WriteObject(28,20,648,15); WriteObject(32,20,700,0);
                    WorldGen.PlaceWall(20,20,WallID.Wood,true);
                    WorldGen.PlaceWire(20,20); WorldGen.PlaceWire2(20,20); WorldGen.PlaceWire3(20,20); WorldGen.PlaceWire4(20,20); WorldGen.PlaceActuator(20,20);
                    var natural = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,library,step,20,20,5,2,"原生展示与有效同格层");
                    var replicas = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,library,step,28,20,5,2,"可摆复制品");
                    var pure = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,library,step,24,20,1,1,"仅展示原生花");
                    var source = CaptureSource(host);
                    var native = Get(Get(host,"materialKnowledge"),"Native");
                    ColdDisplay(context,host,input,native,pure,language,step);
                    var catalog = (BlueprintPlacementCatalog)Get(host,"MaterialCatalog");
                    NativePairs(host,source,catalog,natural,replicas,step);
                    MixedMaskAndRevision(host,input,natural,library,language,step);
                    ReplicaWorld(host,input,replicas,step);
                    ForeignVersion(context,host,tools,input,library,source,step);
                    Console.WriteLine("PASS BlueprintQualification Host: real cold catalog with views/build OFF, exact native originals/replicas, production capture/files, sibling layers, display move, catalog/mask stale receipts, complete current retirement and foreign-version Unknown with real automatic positive control. Original large asset/GPU/FPS/multiplayer/owner experience NOT_RUN by this leaf.");
                }
            }
            finally
            {
                NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());
                Call(host,"SetAutoBuild",false); Call(host,"SetMaterialViewDemand",false); Call(host,"ClearProjection");
                Call(Get(tools,"Use"),"Retire");
                ((BlueprintCaptureState)Get(host,"State")).Exit();
                if(Language.ActiveCulture.Name!=language){LanguageManager.Instance.SetLanguage(language);Lang.InitializeLegacyLocalization();}
                libraryField.SetValue(host,originalLibrary); library?.Dispose();
                foreach(var method in outlets.GetPatchedMethods().ToArray())outlets.Unpatch(method,HarmonyPatchType.All,outlets.Id);
                player.inventory=inventory;player.position=position;player.velocity=velocity;player.direction=direction;player.gravDir=gravity;
                player.selectedItemState.Select(selected);player.selectedItemState.Update();
                Main.screenPosition=camera;Main.GameViewMatrix.Zoom=zoom;Main.worldSurface=surface;Main.time=time;Main.dayTime=day;Main.raining=raining;
                Lighting.Clear();Lighting.Mode=lighting;
                Call(host,"SetMaterialViewDemand",materialDemand);Call(host,"SetAutoBuild",automatic);
            }
        }
        private static bool SkipOutlet(){return false;}
        private static object[] Scene(object host){return ((IEnumerable)Get(host,"ProjectionScene")).Cast<object>().ToArray();}
        private static string Mode(object host){return Get(host,"ProjectionMode").ToString();}
        private static BlueprintProjectionQualificationReceipt Qualification(object item){return (BlueprintProjectionQualificationReceipt)GetOptional(item,"Qualification");}
        private static long Matches(object item){var value=GetOptional(item,"Match");return value==null?0:(long)Get(value,"Matches");}
        private static void Ready(object host,object item,Action step)
        {
            NativeQuickItemChecks.Until(()=>
            {
                step();var value=GetOptional(item,"Match");return Scene(host).Contains(item) && (bool)Call(host,"ProjectionQualificationCurrent",item) && value!=null &&
                    (int)Get(value,"Current")==((Array)Get(value,"Ranges")).Length;
            });
        }
        private static void Window(object host,object item,Action step)
        {
            int x=(int)Get(item,"OriginX"),y=(int)Get(item,"OriginY");
            NativeQuickItemChecks.Until(()=>{Call(host,"UpdateProjectionWindow",new WorldTargetView(Math.Max(0,x-8),Math.Max(0,y-8),48,32,0));step();return GetOptional(item,"Window")!=null;});
        }
        private static void Pointer(object host,bool held,int x,int y){Call(host,"AcceptWorldPointer",true,held,false,x,y,Main.maxTilesX,Main.maxTilesY);}
        private static void WriteObject(int x,int y,int tile,int style)
        {
            Require(TileObject.Place(new TileObject{xCoord=x,yCoord=y,type=tile,style=style,alternate=0,random=0}),"original frame writer seeds exact native source/output tile="+tile+" style="+style);
        }
        private static IBlueprintCaptureSource CaptureSource(object host)
        {
            var type=host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintCaptureSource",true);
            return (IBlueprintCaptureSource)Activator.CreateInstance(type,F,null,new object[]{(Func<bool>)(()=>true)},null);
        }
        private static BlueprintTargetQualification NativeQualification(IBlueprintCaptureSource source,BlueprintPlacementCatalog catalog,int x,int y,int tile,int style)
        {
            CaptureCell cell;Require(source.Read(x,y,out cell),"real native source read");var shape=source.Describe(cell);
            Require(cell.TileType==tile && shape!=null && shape.Object.Reliable && shape.Object.Style==style,"real native capture proves exact output/object style="+tile+"/"+style);
            return catalog.QualifyTile(cell,shape.Object);
        }
        private static BlueprintMaterialSnapshot Materials(object host,LibraryEntry entry,Action step)
        {
            BlueprintMaterialSnapshot value=null;
            NativeQuickItemChecks.Until(()=>{step();value=(BlueprintMaterialSnapshot)Call(host,"MaterialAsset",entry);return value.Status!=BlueprintMaterialStatus.Pending;});
            Require(value.Status==BlueprintMaterialStatus.Exact,"real static file material query: "+value.Status+" "+value.Error);return value;
        }
        private static void ColdDisplay(object context,object host,object input,object native,LibraryEntry pure,string language,Action step)
        {
            string other=language=="zh-Hans"?"en-US":"zh-Hans";
            LanguageManager.Instance.SetLanguage(other);Lang.InitializeLegacyLocalization();
            Require(GetOptional(host,"MaterialCatalog")==null && !(bool)Get(host,"AutoBuildEnabled") && !(bool)Get(host,"materialViewDemand"),"real language invalidation establishes cold producer data with both user demands OFF");
            long reads=(long)Get(native,"PlacementReads"),inventoryReads=(long)Get(host,"MaterialInventoryReads");int steps=0;
            Action coldStep=()=>
            {
                long before=(long)Get(native,"PlacementReads");step();steps++;long after=(long)Get(native,"PlacementReads");
                Require(after-before<=64,"cold qualification retains the existing 64-item update budget");
                // Only probe a same-tick repeat after Host actually requested
                // work. Calling StepPlacement during Following would warm the
                // catalog from the test and invalidate the cold-demand proof.
                if(after>before){Call(native,"StepPlacement");Require((long)Get(native,"PlacementReads")==after,"same native update cannot compile placement declarations twice");}
            };
            // Existing natural content at the destination must not lock move.
            WriteObject(100,30,624,0);
            var item=NativeBlueprintWorldMatchChecks.Place(host,input,pure,100,30,coldStep);Ready(host,item,coldStep);
            Require((long)Get(native,"PlacementReads")>reads && (long)Get(host,"MaterialInventoryReads")==inventoryReads &&
                Qualification(item).EffectiveRequiredLayerCount==0 && Qualification(item).EffectiveUnknownLayerCount==0 &&
                Call(host,"ProjectionMoveEligibility",item).ToString()=="Movable","fixed display alone drives real cold qualification without material inventory work or a natural-object move lock");
            Window(host,item,coldStep);var raw=(BlueprintProjectionWindow)Get(item,"Window");
            Require(raw.Cells.Any(c=>c.TileType==624) && (long)Get(item,"TotalLayerCount")==1,"qualified zero-target display keeps its actual raw source layer");
            NativeQuickItemChecks.Sample(input,Array.Empty<Keys>());Call(host,"MoveProjection");Pointer(host,false,100,30);Pointer(host,true,100,30);
            Require(Mode(host)=="Moving","zero-target display enters real move selection");
            Pointer(host,false,110,30);for(int i=0;i<3;i++)coldStep();Pointer(host,true,110,30);
            NativeQuickItemChecks.Until(()=>{coldStep();return Mode(host)=="Fixed";});item=Scene(host).Single();Ready(host,item,coldStep);
            for(int i=0;i<12;i++)coldStep();
            Require((int)Get(item,"OriginX")==110 && Scene(host).Contains(item) && Qualification(item).EffectiveRequiredLayerCount==0 && Main.tile[100,30].type==624,
                "real zero-target move commits safely and does not 0==0 retire or alter the natural world source");
            Console.WriteLine("BlueprintQualification cold updates="+steps+" placementReads="+((long)Get(native,"PlacementReads")-reads)+" inventoryReads="+((long)Get(host,"MaterialInventoryReads")-inventoryReads)+" displayMove=100->110 required=0");
            Call(host,"ClearProjection");
        }
        private static void NativePairs(object host,IBlueprintCaptureSource source,BlueprintPlacementCatalog catalog,LibraryEntry natural,LibraryEntry replicas,Action step)
        {
            var flower=NativeQualification(source,catalog,24,20,624,0);var pile=NativeQualification(source,catalog,20,20,187,15);
            var flowerCopy=NativeQualification(source,catalog,32,20,700,0);var pileCopy=NativeQualification(source,catalog,28,20,648,15);
            Require(flower.Disposition==BlueprintTargetDisposition.DisplayOnly && pile.Disposition==BlueprintTargetDisposition.DisplayOnly &&
                flowerCopy.Disposition==BlueprintTargetDisposition.Construction && pileCopy.Disposition==BlueprintTargetDisposition.Construction &&
                flowerCopy.Resolution==BlueprintPlacementResolutionKind.Exact && pileCopy.Resolution==BlueprintPlacementResolutionKind.Exact &&
                flowerCopy.Placement.Flexible && pileCopy.Placement.Flexible && flowerCopy.Placement.ConsumeItemType==5114 && pileCopy.Placement.ConsumeItemType==ItemID.StoneBlock,
                "actual locked native producer options separate 624/187 originals from 700/648 replicas and retain flexible player material responsibility");
            Require(catalog.Coverage!=null && catalog.Coverage.SourceAbiValid && catalog.Coverage.TileComplete(624) && catalog.Coverage.TileComplete(187),"negative outcomes carry complete relevant locked producer coverage");
            var a=Materials(host,natural,step);var b=Materials(host,replicas,step);
            Require(a.Total==6 && a.KindCount==3 && a.Rows.Single(r=>r.ItemType==ItemID.Wire).Required==4 &&
                b.Total==2 && b.KindCount==2 && b.Rows.Single(r=>r.ItemType==5114).Required==1 && b.Rows.Single(r=>r.ItemType==ItemID.StoneBlock).Required==1,
                "production file reader excludes seven natural Tile members, keeps valid wall/four-wire/actuator totals, and bills both complete replicas once");
            Console.WriteLine("BlueprintQualification native catalog="+catalog.Revision+" declarations="+catalog.DeclarationCount+" uncompiled="+catalog.UncompiledDeclarationCount+" coverage="+catalog.Coverage.Evidence+" naturalMaterials="+a.Total+" replicaMaterials="+b.Total);
        }
        private static void MixedMaskAndRevision(object host,object input,LibraryEntry entry,LibraryState library,string language,Action step)
        {
            var item=NativeBlueprintWorldMatchChecks.Place(host,input,entry,100,60,step);Ready(host,item,step);Window(host,item,step);
            var raw=(BlueprintProjectionWindow)Get(item,"Window");var old=Qualification(item);
            Require(old.EffectiveRequiredLayerCount==6 && (long)Get(item,"TotalLayerCount")==13 && raw.Cells.Count(c=>c.TileType==187)==6 && raw.Cells.Count(c=>c.TileType==624)==1,
                "raw seven display Tile members remain while only six independent sibling targets need construction");
            WorldGen.PlaceWall(100,60,WallID.Wood,true);NativeQuickItemChecks.Until(()=>{step();return Matches(item)==1;});
            Require(Call(host,"ProjectionMoveEligibility",item).ToString()=="Matched","real sibling wall match still locks movement");
            LanguageManager.Instance.SetLanguage(language);Lang.InitializeLegacyLocalization();step();
            Require(GetOptional(host,"MaterialCatalog")==null && GetOptional(item,"Match")==null && Call(host,"ProjectionMoveEligibility",item).ToString()=="Checking",
                "actual catalog invalidation withdraws old proof, move lock and denominator before the replacement directory is ready");
            Ready(host,item,step);var current=Qualification(item);var match=Get(item,"Match");
            Require(current.CapabilityRevision>old.CapabilityRevision,"real native catalog rebuild publishes a new capability identity");
            Call(host,"AcceptProjectionQualification",item,old);
            Require(ReferenceEquals(Qualification(item),current) && ReferenceEquals(Get(item,"Match"),match),"late real old-catalog receipt cannot replace current Host qualification or proof");
            var coverEntry=Save(library,new CaptureSummary{Width=1,Height=1},new[]{new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.Stone}});
            var cover=NativeBlueprintWorldMatchChecks.Place(host,input,coverEntry,100,60,step);
            NativeQuickItemChecks.Until(()=>{step();return ((BlueprintProjectionMask)Get(item,"Mask")).RemovedLayerCount==6;});Ready(host,item,step);
            var masked=Qualification(item);var mask=(BlueprintProjectionMask)Get(item,"Mask");
            Require(masked.EffectiveRequiredLayerCount==6 && raw.Cells.Where(c=>c.TileType==187).All(c=>(mask.Apply(c).Layers&CaptureLayer.Tile)==0) &&
                (mask.Apply(raw.Cells.Single(c=>c.X==0 && c.Y==0)).Layers&(CaptureLayer.Wall|CaptureLayer.Wire|CaptureLayer.Actuator))==(CaptureLayer.Wall|CaptureLayer.Wire|CaptureLayer.Actuator),
                "one overlapping Tile masks the entire display object, preserves raw geometry/sibling layers and does not deduct construction targets");
            Call(host,"AcceptProjectionQualification",item,current);
            Require(ReferenceEquals(Qualification(item),masked),"late real same-catalog old-mask receipt is rejected by the Host");
            WorldGen.PlaceWire(100,60);WorldGen.PlaceWire2(100,60);WorldGen.PlaceWire3(100,60);WorldGen.PlaceActuator(100,60);
            NativeQuickItemChecks.Until(()=>{step();return Matches(item)==5;});
            Require(Scene(host).Contains(item) && Qualification(item).EffectiveRequiredLayerCount==6,"missing final wire remains a real completion responsibility");
            WorldGen.PlaceWire4(100,60);NativeQuickItemChecks.Until(()=>{step();return !Scene(host).Contains(item);});
            Require(Scene(host).Contains(cover) && !Main.tile[104,60].active() && Main.tile[100,60].wall==WallID.Wood,
                "last valid sibling wire naturally retires the mixed instance despite absent display flower, keeping the independent covering instance and real wall");
            Console.WriteLine("BlueprintQualification mixed displayTiles=7 rawLayers=13 required=6 wholeDisplayMask=6 matchedBeforeLast=5 catalogLateRejected=true maskLateRejected=true finalRetirement=true hash="+entry.ContentHash);
            Call(host,"ClearProjection");
        }
        private static void ReplicaWorld(object host,object input,LibraryEntry entry,Action step)
        {
            var item=NativeBlueprintWorldMatchChecks.Place(host,input,entry,140,30,step);Ready(host,item,step);
            Require(Qualification(item).EffectiveRequiredLayerCount==7 && Call(host,"ProjectionMoveEligibility",item).ToString()=="Movable","empty replica destination retains all seven construction Tile requirements");
            WriteObject(140,30,648,15);NativeQuickItemChecks.Until(()=>{step();return Matches(item)==6;});
            Require(Scene(host).Contains(item) && Call(host,"ProjectionMoveEligibility",item).ToString()=="Matched","a complete real replica body locks movement while the other replica remains missing");
            WriteObject(144,30,700,0);NativeQuickItemChecks.Until(()=>{step();return !Scene(host).Contains(item);});
            Console.WriteLine("BlueprintQualification replicas required=7 nativeBodyMatch=6+1 naturalRetirement=true");
        }
        private static void ForeignVersion(object context,object host,object tools,object input,LibraryState library,IBlueprintCaptureSource source,Action step)
        {
            Call(host,"ClearProjection");Call(host,"SetAutoBuild",false);
            var p=NativeToolExecutionChecks.Reset(context,tools,input,ItemID.CopperPickaxe,0,0);p.releaseUseItem=true;p.builderAccStatus[10]=0;
            p.inventory[12].SetDefaults(ItemID.DirtBlock);p.inventory[12].stack=2;
            Require(WorldGen.PlaceTile(60,40,TileID.Stone,true,true) && WorldGen.PlaceTile(61,40,TileID.Dirt,true,true),"real native block source for independent old-version v1 fixture");
            WorldGen.PlaceWire(60,40);WorldGen.PlaceActuator(60,40);
            CaptureCell first,second;Require(source.Read(60,40,out first),"root-version fixture uses first real captured source value");
            Require(source.Read(61,40,out second),"root-version fixture uses second real captured source value");
            first.X=first.Y=0;second.X=1;second.Y=0;
            var entry=Save(library,new CaptureSummary{Width=2,Height=1,TargetVersion="1.4.5.7"},new[]{first,second});
            Require(WorldGen.PlaceTile(44,42,TileID.Stone,true,true),"old-version first body really matches");WorldGen.PlaceWire(44,42);WorldGen.PlaceActuator(44,42);
            var item=NativeBlueprintWorldMatchChecks.Place(host,input,entry,44,42,step);Ready(host,item,step);Window(host,item,step);
            var qualification=Qualification(item);var state=Get(item,"Match");
            Require(qualification.EffectiveRequiredLayerCount==4 && qualification.EffectiveUnknownLayerCount==4 && (long)Get(state,"Unknown")==4 && Matches(item)==0 &&
                Call(host,"ProjectionMoveEligibility",item).ToString()=="Checking","foreign root version keeps Tile/Wire/Actuator unknown even where the actual world already matches");
            var renderer=Get(item,"Renderer");object[] resultArgs={0,0,default(BlueprintWorldMatchResult)};
            var readMatch=renderer.GetType().GetMethod("TryGetMatch",F);bool published=false;
            // The logical offscreen summary can be current before the raw
            // display window arrives. Its admission demands another bounded
            // detail pass; wait for presence, then assert the actual bits.
            try
            {
                NativeQuickItemChecks.Until(()=>
                {
                    Call(host,"UpdateProjectionWindow",new WorldTargetView((int)Get(item,"OriginX")-8,(int)Get(item,"OriginY")-8,48,32,0));step();
                    return published=(bool)readMatch.Invoke(renderer,resultArgs);
                });
            }
            finally
            {
                var detail=(BlueprintWorldMatchResult)resultArgs[2];
                Console.WriteLine("BlueprintQualification foreign renderer published="+published+" targetBits="+detail.TargetBits+" unknownBits="+detail.UnknownBits+" matchBits="+detail.MatchBits+" current="+Get(state,"Current")+" visible="+((IDictionary)Get(state,"Visible")).Count);
            }
            Require(published && ((BlueprintWorldMatchResult)resultArgs[2]).UnknownBits==25 && ((BlueprintWorldMatchResult)resultArgs[2]).MatchBits==0,
                "actual renderer receives unknown Tile/Wire/Actuator with no hidden Match bits");
            var command=((IEnumerable)Get(renderer,"commands")).Cast<object>().First(c=>(int)Get(c,"CellX")==0 && (int)Get(c,"CellY")==0 && (ushort)Get(c,"SemanticBits")==1);
            object[] commandArgs={command,default(BlueprintWorldMatchResult)};
            Require((bool)renderer.GetType().GetMethod("ResolveMatch",F).Invoke(renderer,commandArgs),"foreign-version target stays visible through the actual draw-command Match consumer");
            long started=(long)Get(host,"BuildUsesStarted");Call(host,"SetAutoBuild",true);
            Action nativeStep=()=>{NativeToolExecutionChecks.Sample(context,input,new Vector2(480,540),false);NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");};
            for(int i=0;i<48;i++)nativeStep();
            Require(p.inventory[12].stack==2 && !Main.tile[45,42].active() && (long)Get(host,"BuildUsesStarted")==started && GetOptional(host,"preparedBuild")==null &&
                ((IEnumerable)Get(Get(item,"Match"),"Ranges")).Cast<object>().All(r=>GetOptional(r,"Materials")==null || GetOptional(Get(r,"Materials"),"Candidate")==null),
                "unknown root qualification never becomes an automatic candidate or native use despite real source material/support/reach");
            // Reset installs a real Stone floor at y=43. The positive control
            // uses that support while leaving the protected x=45 target empty.
            Require(Main.tile[46,43].active() && Main.tile[46,43].type==TileID.Stone && !Main.tile[45,42].active(),
                "valid control has its own original Stone support directly below, without filling the foreign-version target");
            var valid=Save(library,new CaptureSummary{Width=1,Height=1},new[]{new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.Dirt}});
            var control=NativeBlueprintWorldMatchChecks.Place(host,input,valid,46,42,nativeStep);
            NativeQuickItemChecks.Until(()=>{nativeStep();return !Scene(host).Contains(control) && !(bool)Get(Get(tools,"Use"),"Active");});
            Require(Main.tile[46,42].active() && Main.tile[46,42].type==TileID.Dirt && p.inventory[12].stack==1 && Scene(host).Contains(item) && !Main.tile[45,42].active(),
                "same actor/material/reach/support really consumes one valid-version target while the old-version target remains protected");
            Call(host,"SetAutoBuild",false);Call(host,"ClearProjection");
            Console.WriteLine("BlueprintQualification foreignVersion=1.4.5.7 unknownLayers=4 visible=true unknownNativeUses=0 validControlConsumed=1");
        }
        private static LibraryEntry Save(LibraryState library,CaptureSummary summary,CaptureCell[] cells)
        {
            return NativeBlueprintProjectionVisualChecks.Save(library,new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(),Guid.NewGuid(),"qualification-root-fixture",1,1),1,
                summary,new BlueprintProjectionBounds(0,0,summary.Width,summary.Height),cells,Array.Empty<CaptureObject>()));
        }
    }
}
