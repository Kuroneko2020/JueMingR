using System;
using System.Collections;
using System.Linq;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static object[] QueueRecoveryFacts(object host)
        {return ((IDictionary)Get(host,"nearBuildRanges")).Values.Cast<object>().SelectMany(range=>((IDictionary)Get(range,"Facts")).Values.Cast<object>()).ToArray();}

        // Real native GetItem restores a matching tool while a DIFFERENT long
        // tool fixes the broad envelope. No private planner state is written.
        private static void QueueReachRecovery(object context,object host,object tools,object input,bool wall)
        {
            using(var probe=new PostProcessingProbe(context,host,tools,input))
            {
                var p=Reset(context,host,tools,input);p.position.X=42*16;
                p.inventory[12].SetDefaults(wall?ItemID.PaintRoller:ItemID.Paintbrush);var shortTool=p.inventory[12];
                p.inventory[13].SetDefaults(wall?ItemID.SpectrePaintbrush:ItemID.SpectrePaintRoller);
                var longTool=new Item();longTool.SetDefaults(wall?ItemID.SpectrePaintRoller:ItemID.SpectrePaintbrush);
                Require(longTool.tileBoost>shortTool.tileBoost,"locked native short and spectre tools have discriminating reach");
                p.inventory[20].SetDefaults(ItemID.RedPaint);p.inventory[20].stack=9999;var paint=p.inventory[20];
                int x=Enumerable.Range(44,20).First(value=>!p.IsInTileInteractionRange(value,42,TileReachCheckSettings.Simple,shortTool.tileBoost+p.blockRange) &&
                    p.IsInTileInteractionRange(value,42,TileReachCheckSettings.Simple,longTool.tileBoost+p.blockRange));
                var goal=PostProcessingTarget(wall?"WallPaint":"TilePaint",x,42,paint.paint,ItemID.RedPaint);
                if(wall)WorldGen.PlaceWall(x,42,WallID.Stone,true);else NativeToolsChecks.Tile(x,42,TileID.Stone);
                probe.Goals=new[]{goal};probe.Supplies=new[]{paint};probe.ToolItems=new[]{shortTool,p.inventory[13]};
                PostProcessingPlace(context,host,input,probe.Goals);probe.Begin(true);SetAutomatic(host,input,true);
                NativeQuickItemChecks.Until(()=>{probe.Advance();return QueueRecoveryFacts(host).Any(fact=>
                    Convert.ToString(Get(fact,"Wait")).Contains("Player") && !(bool)Get(fact,"Pending"));});
                var position=p.position;int selected=p.selectedItem;var envelope=(BlueprintProjectionBounds)Call(host,"BuildNearOrdinaryWorldBounds",p);
                Require(envelope.Contains(x,42) && !goal.Complete && probe.Events.Count==0,"cold fact genuinely waits only for matching reach inside the existing broad domain");
                long classes=(long)Get(host,"NearBuildClassifications");
                NearQueueNativeRefill(p,ItemID.SnowBlock,3);for(int i=0;i<8;i++)probe.Advance();
                Require((long)Get(host,"NearBuildClassifications")==classes && !goal.Complete,"unrelated native acquisition does not reopen the stable reach negative");
                var acquired=NearQueueNativeRefill(p,longTool.type,1);probe.ToolItems=new[]{shortTool,p.inventory[13],acquired.Item};
                NativeQuickItemChecks.Until(()=>{probe.Advance();return goal.Complete && !(bool)Get(Get(tools,"Use"),"Active");});
                Require(p.position==position && p.selectedItem==selected && envelope.Equals((BlueprintProjectionBounds)Call(host,"BuildNearOrdinaryWorldBounds",p)) &&
                    paint.stack==9998 && probe.Events.Count==1,"matching native long tool restores in place with unchanged envelope and exact single paint consumption");
                probe.Report("queue short-to-long native reach recovery wall="+wall);
            }
        }

        private static void QueueMirrorCellMovement(object context,object host,object tools,object input)
        {
            using(var probe=new PostProcessingProbe(context,host,tools,input))
            {
                var p=Reset(context,host,tools,input);p.inventory[20].SetDefaults(ItemID.RedPaint);p.inventory[20].stack=9999;var paint=p.inventory[20];
                NativeToolsChecks.Tile(69,42,TileID.Stone);NativeToolsChecks.Tile(132,42,TileID.Stone);
                Require(WorldGen.paintTile(69,42,(byte)paint.paint),"native source writer supplies the requested paint");
                var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),()=>probe.Advance(),69,42,64,1,"镜像单元移动恢复");
                Place(context,host,input,entry,19,42);
                NativeQuickItemChecks.Sample(input,Array.Empty<Microsoft.Xna.Framework.Input.Keys>());Call(host,"MirrorProjection");
                Call(host,"AcceptWorldPointer",true,false,false,19,42,Main.maxTilesX,Main.maxTilesY);
                Call(host,"AcceptWorldPointer",true,true,false,19,42,Main.maxTilesX,Main.maxTilesY);
                NativeQuickItemChecks.Until(()=>{probe.Advance();return !(bool)Get(host,"ProjectionEditing");});
                Require((bool)Get(Scene(host).Single(),"MirroredHorizontally"),"actual mirror publishes the single-cell recovery pose");
                NativeToolsChecks.Tile(82,42,TileID.Stone);p.position.X=77*16;p.velocity=Vector2.Zero;
                var goal=PostProcessingTarget("TilePaint",82,42,paint.paint,ItemID.RedPaint);
                probe.Goals=new[]{goal};probe.Supplies=new[]{paint};probe.ToolItems=Array.Empty<Item>();probe.Begin(true);SetAutomatic(host,input,true);
                NativeQuickItemChecks.Until(()=>{probe.Advance();return QueueRecoveryFacts(host).Any(fact=>Convert.ToString(Get(fact,"Wait")).Contains("Capability") && !(bool)Get(fact,"Pending")) &&
                    ((IDictionary)Get(host,"nearBuildRanges")).Values.Cast<object>().All(state=>(bool)Get(state,"Complete"));});
                var domain=((IDictionary)Get(host,"nearBuildRanges")).Values.Cast<object>().Select(state=>(BlueprintProjectionBounds)Get(state,"RetirementBounds")).ToArray();
                float end=p.position.X+16;
                NativeQuickItemChecks.Until(()=>{probe.Advance(horizontal:p.position.X<end?1:0);return p.position.X>=end;});
                Require(!domain.SequenceEqual(((IDictionary)Get(host,"nearBuildRanges")).Values.Cast<object>().Select(state=>(BlueprintProjectionBounds)Get(state,"RetirementBounds"))),
                    "real motion changes the source retirement corridor while the painted cell remains near");
                var brush=NearQueueNativeRefill(p,ItemID.Paintbrush,1);probe.ToolItems=new[]{brush.Item};
                NativeQuickItemChecks.Until(()=>{probe.Advance();return goal.Complete && !(bool)Get(Get(tools,"Use"),"Active");});
                Require(paint.stack==9998 && probe.Events.Count==1,"mirrored covered-interior cell survives movement and consumes one native paint after real tool recovery");
                probe.Report("queue mirrored single cell movement");
            }
        }

        private static void QueueMissingWiringToolRecovery(object context,object host,object tools,object input)
        {
            using(var probe=new PostProcessingProbe(context,host,tools,input))
            {
                var p=Reset(context,host,tools,input);p.position.X=42*16;
                p.inventory[20].SetDefaults(ItemID.Wire);p.inventory[20].stack=9999;var wire=p.inventory[20];
                var goal=PostProcessingTarget("Wire",44,42,2,ItemID.Wire);
                probe.Goals=new[]{goal};probe.Supplies=new[]{wire};probe.ToolItems=Array.Empty<Item>();
                PostProcessingPlace(context,host,input,probe.Goals);probe.Begin(true);SetAutomatic(host,input,true);
                NativeQuickItemChecks.Until(()=>{probe.Advance();return QueueRecoveryFacts(host).Any(fact=>Convert.ToString(Get(fact,"Wait")).Contains("Material") && !(bool)Get(fact,"Pending"));});
                Require(!goal.Complete && wire.stack==9999 && probe.Events.Count==0,"known supplied wire waits for an actually absent matching tool");
                var wrench=NearQueueNativeRefill(p,850,1);probe.ToolItems=new[]{wrench.Item};
                NativeQuickItemChecks.Until(()=>{probe.Advance();return goal.Complete && !(bool)Get(Get(tools,"Use"),"Active");});
                Require(wire.stack==9998 && wrench.Item.stack==1 && probe.Events.Count==1,"native GetItem of a matching Blue Wrench wakes the cached missing-tool fact and spends exactly one wire");
                probe.Report("queue missing wiring tool native recovery");
            }
        }

        // A discovery-retirement control, NOT the abundant-stock furniture
        // scenario: withhold Stone until the native out-and-back trip ends.
        // The immutable captured world and every destination anchor stay fixed
        // throughout the trip; GetItem is the only recovery stimulus.
        private static void QueueOrdinaryCellReturn(object context,object host,object tools,object input)
        {
            using(var scope=new BusinessScope(context,host,tools,input))
            using(var observation=new NearQueueObservation(host,true))
            {
                var p=Reset(context,host,tools,input);p.position.X=42*16;p.velocity=Vector2.Zero;
                var model=new Item();model.SetDefaults(ItemID.StoneBlock);
                Require(!p.inventory.Take(50).Any(item=>!item.IsAir && item.type==ItemID.StoneBlock),"retirement control starts without Stone so no initial native consumption can hide the target");
                foreach(int x in new[]{70,82,133})
                {
                    NativeToolsChecks.Tile(x,43,TileID.Stone);
                    Require(WorldGen.PlaceTile(x,42,TileID.Stone,true,true),"original writer supplies a captured ordinary Stone source cell");
                }
                var asset=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),()=>scope.Step(),70,42,64,1,"普通单格无世界改动往返");
                var owner=Place(context,host,input,asset,39,42);
                var goal=new BusinessGoal{X=39,Y=42,Type=ItemID.StoneBlock,TileType=TileID.Stone};
                scope.Goals=new[]{goal};scope.Sources=Array.Empty<BusinessSource>();
                Func<string> world=()=>string.Join(";",new[]{39,51,102}.SelectMany(x=>new[]{42,43}.Select(y=>
                {
                    var tile=Main.tile[x,y];return x+","+y+":"+tile.active()+","+tile.type+","+tile.wall+","+tile.frameX+","+tile.frameY+","+tile.slope()+","+tile.halfBrick();
                })));
                string fixedWorld=world();
                Require(!goal.Complete && WorldGen.SolidTile(39,43) &&
                    p.IsInTileInteractionRange(39,42,TileReachCheckSettings.Simple,model.tileBoost+p.blockRange),"the missing single-cell goal has native reach and its complete permanent support before automation");
                scope.Begin(true);SetAutomatic(host,input,true);
                NativeQuickItemChecks.Until(()=>
                {
                    observation.Step(scope);
                    return QueueRecoveryFacts(host).Any(fact=>(int)Get(Get(fact,"Seed"),"Ordinal")>=0 &&
                        (int)Get(Get(Get(fact,"Seed"),"Cell"),"X")==0 && Convert.ToString(Get(fact,"Wait")).Contains("Material") && !(bool)Get(fact,"Pending")) &&
                        ((IDictionary)Get(host,"nearBuildRanges")).Values.Cast<object>().All(state=>(bool)Get(state,"Complete"));
                });
                var match=Get(owner,"Match");var qualification=Get(owner,"Qualification");var planner=Get(host,"nearBuildDiscovery");
                var ranges=((Array)Get(match,"Ranges")).Cast<object>().ToArray();
                var target=ranges.Single(range=>((BlueprintProjectionSourceRange)Get(range,"Source")).Contains(0,0));
                var source=(BlueprintProjectionSourceRange)Get(target,"Source");
                Require(source.ObjectId==0 && (source.TargetBits&0xf8)==0 && source.Contains(12,0),"actual qualified ordinary range contains both raw0 and raw12 with no wiring or object proof");
                Func<int> targetFacts=()=>QueueRecoveryFacts(host).Count(fact=>ReferenceEquals(Get(Get(fact,"Seed"),"Range"),target) &&
                    (int)Get(Get(Get(fact,"Seed"),"Cell"),"X")==0);
                Console.WriteLine("QueueOrdinaryReturn source="+source.Bounds.Left+","+source.Bounds.Top+".."+source.Bounds.Right+","+source.Bounds.Bottom+
                    " ranges="+ranges.Length+" initialFacts0="+targetFacts());
                NativeQuickItemChecks.Until(()=>{observation.Step(scope,()=>scope.Step(direction:p.position.X<54*16?1:0));return p.position.X>=54*16;});
                NativeQuickItemChecks.Until(()=>{observation.Step(scope);return p.velocity.X==0;});
                var away=(BlueprintProjectionBounds)Call(host,"BuildNearOrdinaryWorldBounds",p);
                var query=((IDictionary)Get(planner,"Owners"))[owner];query=GetOptional(query,"Ordinary")??query;
                var covered=(BlueprintProjectionBounds)Get(query,"Covered");
                Require(!away.Contains(39,42) && away.Contains(51,42) && covered.Contains(0,0) &&
                    ReferenceEquals(match,Get(owner,"Match")) && ReferenceEquals(qualification,Get(owner,"Qualification")) && ReferenceEquals(planner,Get(host,"nearBuildDiscovery")) &&
                    fixedWorld==world() && scope.Events.Count==0,"native departure leaves raw0 outside ordinary reach while the same owner retains raw12, completed discovery coverage and the unchanged world");
                Console.WriteLine("QueueOrdinaryReturn away="+p.position+" facts0="+targetFacts()+" covered="+covered.Left+","+covered.Top+".."+covered.Right+","+covered.Bottom);
                long directory=(long)Get(host,"BuildFocusDirectorySteps");
                // Brake naturally before the initial left edge. Returning to
                // the old covered domain must not enter an unrelated new strip
                // through subpixel overshoot and accidentally rediscover it.
                NativeQuickItemChecks.Until(()=>{observation.Step(scope,()=>scope.Step(direction:p.position.X>45*16?-1:0));return p.position.X<=45*16;});
                NativeQuickItemChecks.Until(()=>{observation.Step(scope);return p.velocity.X==0;});
                Require(p.IsInTileInteractionRange(39,42,TileReachCheckSettings.Simple,model.tileBoost+p.blockRange) && fixedWorld==world() &&
                    (long)Get(host,"BuildFocusDirectorySteps")==directory && ReferenceEquals(match,Get(owner,"Match")) &&
                    ReferenceEquals(qualification,Get(owner,"Qualification")) && ReferenceEquals(planner,Get(host,"nearBuildDiscovery")) && scope.Events.Count==0,
                    "native return reaches the unchanged supported goal without a new directory query, owner/qualification replacement, consumption or global plan reset");
                Console.WriteLine("QueueOrdinaryReturn returned="+p.position+" facts0="+targetFacts()+" directoryDelta="+((long)Get(host,"BuildFocusDirectorySteps")-directory));
                var stone=NearQueueNativeRefill(p,ItemID.StoneBlock,9999);scope.Sources=new[]{stone};
                NativeQuickItemChecks.Until(()=>{observation.Step(scope);return goal.Complete && !(bool)Get(Get(tools,"Use"),"Active");});
                Require(stone.Item.stack==9998 && scope.Events.Count==1 && scope.Events[0].Count==1 && !Main.tile[51,42].active() && !Main.tile[102,42].active() &&
                    ReferenceEquals(match,Get(owner,"Match")) && ReferenceEquals(qualification,Get(owner,"Qualification")) && ReferenceEquals(planner,Get(host,"nearBuildDiscovery")),
                    "original GetItem after the unchanged-world return restores exactly one original native Stone consumer with 9998 left and no owner/plan reset");
                scope.Report("ordinary single-cell return, withheld initial Stone is retirement control only");
            }
        }

        private static void QueueFurnitureMovement(object context,object host,object tools,object input,bool mirror)
        {
            using(var scope=new BusinessScope(context,host,tools,input))
            using(var observation=new NearQueueObservation(host,true))
            {
                var p=Reset(context,host,tools,input);var table=BusinessStock(p,12,ItemID.WoodenTable,9999);
                int click=mirror?81:40;
                scope.Sources=new[]{table};scope.Goals=new[]{NearQueueFurniture(table,70,42,click,42)};
                NativeToolsChecks.Tile(132,42,TileID.Stone);
                var asset=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),()=>scope.Step(),69,41,64,2,"宽图完整桌子移动");
                var owner=Place(context,host,input,asset,mirror?19:39,41);
                if(mirror)
                {
                    NativeQuickItemChecks.Sample(input,Array.Empty<Microsoft.Xna.Framework.Input.Keys>());
                    Call(host,"MirrorProjection");
                    Require((bool)Get(host,"ProjectionEditing"),"actual Host enters mirror editor");
                    Call(host,"AcceptWorldPointer",true,false,false,20,42,Main.maxTilesX,Main.maxTilesY);
                    Call(host,"AcceptWorldPointer",true,true,false,20,42,Main.maxTilesX,Main.maxTilesY);
                    NativeQuickItemChecks.Until(()=>{scope.Step();return !(bool)Get(host,"ProjectionEditing");});
                    owner=Scene(host).Single();Require((bool)Get(owner,"MirroredHorizontally"),"actual mirror transaction publishes a wide asymmetric pose");
                    // Put the true click at the native right boundary while
                    // the third member lies outside it. A small native left
                    // motion then shrinks the object's clipped source domain.
                    int l,t,r,b;int initial=0;
                    for(int x=65;x<82;x++)
                    {p.position.X=x*16;TileReachCheckSettings.Simple.GetTileRegion(p,out l,out t,out r,out b,table.Item.tileBoost+p.blockRange);if(r==click+1){initial=x;break;}}
                    Require(initial>0,"native table click boundary is present in the finite fixture");
                }
                else p.position.X=42*16;
                p.velocity=Vector2.Zero;
                for(int x=click-1;x<=click+1;x++)WorldGen.KillTile(x,43,false,false,true);
                scope.Begin(true);SetAutomatic(host,input,true);
                NativeQuickItemChecks.Until(()=>{observation.Step(scope);return QueueRecoveryFacts(host).Any(fact=>GetOptional(Get(fact,"Seed"),"Body")!=null &&
                    Convert.ToString(Get(fact,"Wait")).Contains("World") && !(bool)Get(fact,"Pending")) &&
                    ((IDictionary)Get(host,"nearBuildRanges")).Values.Cast<object>().Where(state=>((IDictionary)Get(state,"Facts")).Values.Cast<object>().Any(fact=>GetOptional(Get(fact,"Seed"),"Body")!=null)).All(state=>(bool)Get(state,"Complete"));});
                var identity=Get(owner,"Match");var planner=Get(host,"nearBuildDiscovery");
                var bodyState=((IDictionary)Get(host,"nearBuildRanges")).Values.Cast<object>().Single(state=>((IDictionary)Get(state,"Facts")).Values.Cast<object>().Any(fact=>GetOptional(Get(fact,"Seed"),"Body")!=null));
                var oldCovered=(BlueprintProjectionBounds)Get(bodyState,"Covered");
                var oldDomain=((IDictionary)Get(host,"nearBuildRanges")).Values.Cast<object>().Select(range=>(BlueprintProjectionBounds)Get(range,"RetirementBounds")).ToArray();
                float start=p.position.X;float end=mirror?start-8:54*16;
                NativeQuickItemChecks.Until(()=>{observation.Step(scope,()=>scope.Step(direction:mirror?(p.position.X>end?-1:0):(p.position.X<end?1:0)));return mirror?p.position.X<=end:p.position.X>=end;});
                Require(ReferenceEquals(identity,Get(owner,"Match")) && ReferenceEquals(planner,Get(host,"nearBuildDiscovery")),"movement retains the same owner qualification and shared plan");
                var newDomain=((IDictionary)Get(host,"nearBuildRanges")).Values.Cast<object>().Select(range=>(BlueprintProjectionBounds)Get(range,"RetirementBounds")).ToArray();
                Require(!mirror || !oldDomain.SequenceEqual(newDomain),"mirrored complete object actually crosses a retirement-domain boundary");
                var domain=(BlueprintProjectionBounds)Get(bodyState,"RetirementBounds");
                if(mirror)Require(oldCovered.Left<=domain.Left && oldCovered.Right>=domain.Right && oldCovered.Top<=domain.Top && oldCovered.Bottom>=domain.Bottom &&
                    p.IsInTileInteractionRange(click,42,TileReachCheckSettings.Simple,table.Item.tileBoost+p.blockRange),
                    "mirrored motion shrinks an already-covered object domain while the native click remains reachable; no new source strip can mask retirement");
                else
                {
                    var query=((IDictionary)Get(planner,"Owners"))[owner];query=GetOptional(query,"Ordinary")??query;
                    var coverage=(BlueprintProjectionBounds)Get(query,"Covered");var ordinary=(BlueprintProjectionBounds)Call(host,"BuildNearOrdinaryWorldBounds",p);
                    Require(coverage.Contains(0,0) && coverage.Contains(2,1) && ordinary.Left>=click+2,
                        "ordinary target is wholly outside native near reach while the same owner's genuine completed coverage still retains its source body");
                }
                Console.WriteLine("QueueMovement mirror="+mirror+" covered="+oldCovered.Left+","+oldCovered.Top+".."+oldCovered.Right+","+oldCovered.Bottom+
                    " retained="+domain.Left+","+domain.Top+".."+domain.Right+","+domain.Bottom+" player="+p.position);
                for(int x=click-1;x<=click+1;x++)Require(WorldGen.PlaceTile(x,43,TileID.Stone,true,true),"native external writer restores every actual table anchor");
                if(!mirror)NativeQuickItemChecks.Until(()=>{observation.Step(scope,()=>scope.Step(direction:p.position.X>start?-1:0));return p.position.X<=start;});
                NativeQuickItemChecks.Until(()=>{observation.Step(scope);return scope.Goals[0].Complete && !(bool)Get(Get(tools,"Use"),"Active");});
                Require(table.Item.stack==9998 && scope.Events.Count==1 && scope.Goals[0].Width==3 && scope.Goals[0].Height==2,
                    "same wide owner recovers one complete native six-member table with exact cost after ordinary return or mirrored small movement");
                scope.Report("queue wide complete table recovery mirror="+mirror);
            }
        }

        private static QueueSourceWait queueSourceWait;
        // Delay a real worker source receipt, without replacing its reader,
        // payload, native support, planner result or successful completion.
        private sealed class QueueSourceWait : IDisposable
        {
            internal readonly object Worker;
            internal readonly System.Threading.ManualResetEventSlim Entered=new System.Threading.ManualResetEventSlim(false);
            internal readonly System.Threading.ManualResetEventSlim Release=new System.Threading.ManualResetEventSlim(false);
            private readonly HarmonyLib.Harmony patch=new HarmonyLib.Harmony("JueMingR.Tests.QueueSourceWaiting");
            internal QueueSourceWait(object host)
            {
                Worker=Get(Get(host,"Library"),"worker");queueSourceWait=this;
                var iterator=Worker.GetType().GetNestedTypes(F).Single(type=>type.Name.StartsWith("<ReadProjectionSource>",StringComparison.Ordinal));
                patch.Patch(iterator.GetMethod("MoveNext",F),prefix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(QueueSourceBeforeMoveNext)));
            }
            public void Dispose()
            {
                Release.Set();foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyLib.HarmonyPatchType.All,patch.Id);
                if(ReferenceEquals(queueSourceWait,this))queueSourceWait=null;
                // Do not dispose events under a worker still returning from Wait.
            }
        }
        private static void QueueSourceBeforeMoveNext(object __instance)
        {
            var gate=queueSourceWait;if(gate==null || !ReferenceEquals(Get(__instance,"<>4__this"),gate.Worker))return;
            gate.Entered.Set();if(!gate.Release.Wait(10000))throw new TimeoutException("controlled queue source receipt was not released");
        }
        private static void QueueActualSourceWaiting(object context,object host,object tools,object input)
        {
            using(var scope=new BusinessScope(context,host,tools,input))
            {
                var p=Reset(context,host,tools,input);p.position.X=42*16;
                var wall=BusinessStock(p,12,ItemID.StoneWall,9999);scope.Sources=new[]{wall};scope.Goals=new[]{BusinessWallGoal(44,41,wall)};
                WorldGen.PlaceWall(70,41,wall.Item.createWall,true);WorldGen.PlaceWall(44,40,wall.Item.createWall,true);
                var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),()=>scope.Step(),70,41,1,1,"真实源回执等待");
                var owner=Place(context,host,input,entry,44,41);
                Console.WriteLine("QueueSourceWait phase=qualification materialView=true");
                Call(host,"SetMaterialViewDemand",true);
                NativeQuickItemChecks.Until(()=>{scope.Step();return ((BlueprintMaterialSnapshot)Call(host,"MaterialWorld",0L)).Status==BlueprintMaterialStatus.Exact &&
                    (bool)Call(host,"ProjectionQualificationCurrent",owner);});
                Call(host,"SetMaterialViewDemand",false);
                Console.WriteLine("QueueSourceWait phase=install-gate received="+Get(Get(host,"buildSources"),"Count"));
                using(var gate=new QueueSourceWait(host))
                {
                    scope.Begin(true);SetAutomatic(host,input,true);
                    int waitingSteps=0;
                    NativeQuickItemChecks.Until(()=>
                    {
                        scope.Step();waitingSteps++;
                        if(waitingSteps<=4 || waitingSteps==64)Console.WriteLine("QueueSourceWait phase=waiting frame="+waitingSteps+" gate="+gate.Entered.IsSet+
                            " ranges="+((IDictionary)Get(host,"nearBuildRanges")).Count+" received="+Get(Get(host,"buildSources"),"Count")+
                            " input="+Get(input,"CanRunAutomaticActions"));
                        return gate.Entered.IsSet && ((IDictionary)Get(host,"nearBuildRanges")).Count>0 &&
                            ((IDictionary)Get(host,"nearBuildRanges")).Values.Cast<object>().All(state=>(bool)Get(state,"ExtractionWaiting"));
                    });
                    Require((int)Get(Get(host,"buildSources"),"Count")==0,"real worker is awaiting release with no received source transport");
                    for(int frame=0;frame<8;frame++)
                    {
                        long before=(long)Get(host,"NearBuildSemanticSteps"),ranges=(long)Get(host,"NearBuildRangeChecks");scope.Step();
                        Require((long)Get(host,"NearBuildSemanticSteps")==before && (long)Get(host,"NearBuildRangeChecks")-ranges==1 &&
                            (int)Get(host,"materialBuildSourceBudget")==64 && !scope.Goals[0].Complete && QueueNoPreparedWork(host),
                            "all-source waiting ends after one idle range visit without burning budget, busy retry, false readiness or native consumption");
                    }
                    gate.Release.Set();
                    NativeQuickItemChecks.Until(()=>{scope.Step();return scope.Goals[0].Complete && !(bool)Get(Get(tools,"Use"),"Active");});
                    Require(wall.Item.stack==9998 && scope.Events.Count==1,"released original source reader resumes normal exact native wall consumption");
                    scope.Report("queue real source worker receipt delayed eight Updates");
                }
            }
        }
        private static bool QueueNoPreparedWork(object host)
        {return GetOptional(host,"preparedBuild")==null && GetOptional(host,"activeBuild")==null;}

        private static QueueSliceObservation queueSlices;
        private sealed class QueueSliceObservation : IDisposable
        {
            private readonly HarmonyLib.Harmony patch=new HarmonyLib.Harmony("JueMingR.Tests.QueueSliceOrder");
            internal readonly object Host;
            internal readonly System.Collections.Generic.List<object> Ranges=new System.Collections.Generic.List<object>();
            internal readonly System.Collections.Generic.List<long> Steps=new System.Collections.Generic.List<long>();
            internal QueueSliceObservation(object host,bool enabled)
            {
                Host=host;if(!enabled)return;queueSlices=this;
                patch.Patch(host.GetType().GetMethod("ExtractNearBuildRange",F),
                    prefix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(QueueSliceBefore)),
                    postfix:new HarmonyLib.HarmonyMethod(typeof(NativeBlueprintBuildChecks),nameof(QueueSliceAfter)));
            }
            public void Dispose()
            {foreach(var method in patch.GetPatchedMethods().ToArray())patch.Unpatch(method,HarmonyLib.HarmonyPatchType.All,patch.Id);if(ReferenceEquals(queueSlices,this))queueSlices=null;}
        }
        private static void QueueSliceBefore(object __instance,out long __state)
        {__state=(long)Get(__instance,"NearBuildSemanticSteps");}
        private static void QueueSliceAfter(object __instance,object __1,long __state)
        {
            var trace=queueSlices;if(trace==null || !ReferenceEquals(trace.Host,__instance))return;
            long steps=(long)Get(__instance,"NearBuildSemanticSteps")-__state;
            if(steps>0){trace.Ranges.Add(__1);trace.Steps.Add(steps);}
        }

        private static void QueueBudgetProgress(object context,object host,object tools,object input,bool warm,bool pair=false)
        {
            using(var scope=new BusinessScope(context,host,tools,input))
            using(var observation=new NearQueueObservation(host,true))
            using(var slices=new QueueSliceObservation(host,pair))
            {
                var p=Reset(context,host,tools,input);p.position.X=42*16;
                var wall=BusinessStock(p,12,ItemID.StoneWall,9999);scope.Sources=new[]{wall};
                // Twelve source records precede the missing wall. All source
                // geometry and destination walls come from the native writer.
                for(int x=0;x<4;x++)for(int y=0;y<3;y++)
                {WorldGen.PlaceWall(70+x,38+y,wall.Item.createWall,true);WorldGen.PlaceWall(42+x,38+y,wall.Item.createWall,true);}
                WorldGen.PlaceWall(73,41,wall.Item.createWall,true);
                scope.Goals=new[]{BusinessWallGoal(45,41,wall)};
                var entry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),()=>scope.Step(),70,38,4,4,"源片后段墙");
                var instance=Place(context,host,input,entry,42,38);
                object second=null;
                if(pair)
                {
                    WorldGen.PlaceWall(80,41,wall.Item.createWall,true);
                    var shortEntry=NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context,host,input,(LibraryState)Get(host,"Library"),()=>scope.Step(),80,41,1,1,"短墙公平来源");
                    second=Place(context,host,input,shortEntry,40,41);
                    WorldGen.PlaceWall(40,40,wall.Item.createWall,true);
                    scope.Goals=new[]{scope.Goals[0],BusinessWallGoal(40,41,wall)};
                }
                Require(((IEnumerable)Get(instance,"MatchRanges")).Cast<object>().Count()==1,"tail wall fixture has one actual normalized logical range");
                if(warm)
                {
                    Call(host,"SetMaterialViewDemand",true);
                    long generation=((BlueprintProjectionIdentity)Get(instance,"Identity")).ProjectionGeneration;
                    NativeQuickItemChecks.Until(()=>{scope.Step();return ((BlueprintMaterialSnapshot)Call(host,"MaterialWorld",pair?0L:generation)).Status==BlueprintMaterialStatus.Exact;});
                    Call(host,"SetMaterialViewDemand",false);
                    Require((int)Get(host,"NearBuildFacts")==0,"transport-only warm arm has no injected semantic facts");
                }
                scope.Begin(true);SetAutomatic(host,input,true);bool multiple=false,fair=false;
                if(pair)
                {
                    // The UI-only warm proof does not retain build transport:
                    // MaterialBuildDemand is actually OFF there. Enable demand
                    // but omit native input epochs while two genuine changed
                    // Match watches reread their original source bytes.
                    WorldGen.KillWall(42,38);WorldGen.PlaceWall(42,38,wall.Item.createWall,true);
                    WorldGen.PlaceWall(40,41,wall.Item.createWall,true);WorldGen.KillWall(40,41);
                    // These are setup drops from reversible isolated-world
                    // writers, before any native step can pick them up.
                    foreach(var dropped in Main.item)dropped.TurnToAir();
                    var longRange=((Array)Get(Get(instance,"Match"),"Ranges")).GetValue(0);
                    var shortRange=((Array)Get(Get(second,"Match"),"Ranges")).GetValue(0);
                    Require(!(bool)Call(host,"ProjectionMatchReadCurrent",longRange,Get(longRange,"RawVersion")) &&
                        !(bool)Call(host,"ProjectionMatchReadCurrent",shortRange,Get(shortRange,"RawVersion")),
                        "two real wall writers revoke both native watches before any Host drain");
                    NativeQuickItemChecks.Until(()=>{Call(input,"BeginUpdate");Call(context,"UpdateRuntime");return (int)Get(Get(host,"buildSources"),"Count")==2;});
                    Require((int)Get(host,"NearBuildFacts")==0,"both actual transports arrive with no native input epoch and no semantic planning");
                    slices.Ranges.Clear();slices.Steps.Clear();
                    Console.WriteLine("QueueBudget pair actualReceived=2 semanticFacts=0 before-first-input");
                }
                NativeQuickItemChecks.Until(()=>
                {
                    long before=(long)Get(host,"NearBuildSemanticSteps");
                    slices.Ranges.Clear();slices.Steps.Clear();observation.Step(scope);
                    long steps=(long)Get(host,"NearBuildSemanticSteps")-before;
                    if(steps>8){multiple=true;Console.WriteLine("QueueBudget warm="+warm+" steps="+steps+" remaining="+Get(host,"materialBuildSourceBudget"));}
                    if(pair && steps>8 && !fair)
                    {
                        var states=((IDictionary)Get(host,"nearBuildRanges")).Values.Cast<object>().ToArray();
                        fair=states.Length==2 && states.All(state=>((IDictionary)Get(state,"Facts")).Count>0) && states.Max(state=>((IDictionary)Get(state,"Facts")).Count)>16 &&
                            slices.Ranges.Count>=3 && !ReferenceEquals(slices.Ranges[0],slices.Ranges[1]) && slices.Steps.All(value=>value<=8) &&
                            slices.Ranges.Skip(2).Any(range=>ReferenceEquals(range,slices.Ranges[0]) || ReferenceEquals(range,slices.Ranges[1]));
                        Require(fair,"two arrived ranges both receive their first slice before a long range revisits within the same Update");
                    }
                    return scope.Goals.All(goal=>goal.Complete) && !(bool)Get(Get(tools,"Use"),"Active");
                });
                Console.WriteLine("QueueBudget final warm="+warm+" pair="+pair+" multiple="+multiple+" fair="+fair+" stock="+wall.Item.stack+
                    " goals="+scope.Goals.Length+" events="+scope.Events.Count+" nativeCounts="+string.Join(",",scope.Events.Select(value=>value.Count)));
                Require(multiple && (!pair || fair) && wall.Item.stack==9999-scope.Goals.Length && scope.Events.Count==scope.Goals.Length,"one available logical range uses multiple paid eight-step slices in one Update and consumes the tail exactly once");
                scope.Report("queue shared budget tail wall warm="+warm+" pair="+pair);
            }
        }
    }
}
