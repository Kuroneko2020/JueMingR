using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ObjectData;
using Terraria.Utilities;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        // Read-only snapshots of the shared planner; never call its currentness
        // or selection methods from an observer (those may spend real work).
        private static object NearRangeState(object host, object range)
        { return ((IDictionary)Get(host,"nearBuildRanges"))[range]; }
        private static object[] NearRangeFacts(object host, object range)
        {
            var state=NearRangeState(host,range);
            return state==null?Array.Empty<object>():((IDictionary)Get(state,"Facts")).Values.Cast<object>().ToArray();
        }
        private static bool NearRangeHasPendingWork(object host,object range)
        {
            var state=NearRangeState(host,range);
            return state==null || !(bool)Get(state,"Complete") || NearRangeFacts(host,range).Any(f=>(bool)Get(f,"Ready") || (bool)Get(f,"Pending"));
        }
        private static string NearRangeSummary(object host,object range)
        {
            var state=NearRangeState(host,range);
            return state==null?"unclassified":"cursor="+Get(state,"Cursor")+" complete="+Get(state,"Complete")+
                " bounds="+Get(state,"Bounds")+" covered="+Get(state,"Covered")+" facts="+Get(Get(state,"Facts"),"Count")+
                " waits="+string.Join(",",NearRangeFacts(host,range).Take(8).Select(f=>Get(f,"Wait")+":"+Get(f,"Ready")+":"+Get(f,"Pending")));
        }
        private sealed class PreferredWalkObservation
        {
            internal object State, Bounds, Covered;
            internal int Cursor;
            internal bool Complete, BudgetPaused;
        }
        private static void ObservePreferredContinuation(object host, IEnumerable<object> ranges,
            Dictionary<object, PreferredWalkObservation> prior, ref bool pending, ref bool resumed, ref int maxCursor)
        {
            foreach(var range in ranges)
            {
                var value=NearRangeState(host,range);if(value==null)continue;
                var current=new PreferredWalkObservation{State=value,Cursor=(int)Get(value,"Cursor"),
                    Bounds=Get(value,"Bounds"),Covered=Get(value,"Covered"),Complete=(bool)Get(value,"Complete")};
                current.BudgetPaused=!current.Complete && (bool)Get(value,"Initialized") && current.Cursor>0;
                PreferredWalkObservation previous;
                // Cursor resets only for a newly entered strip. Compare exactly
                // the same outstanding strip/coverage across transport and leases.
                if(prior.TryGetValue(range,out previous) && ReferenceEquals(previous.State,current.State) &&
                    Equals(previous.Bounds,current.Bounds) && Equals(previous.Covered,current.Covered) &&
                    !previous.Complete && !current.Complete)
                {
                    Require(current.Cursor>=previous.Cursor,"movement, source transport and a new lease cannot rewind an unfinished shared near strip");
                    resumed|=previous.BudgetPaused && current.Cursor>previous.Cursor;
                }
                pending|=current.BudgetPaused;maxCursor=Math.Max(maxCursor,current.Cursor);prior[range]=current;
            }
        }
        private static void ExecutionSearchFairness(object context,object host,object tools,object input,bool moving,bool denseEdge=false)
        {
            var p=Reset(context,host,tools,input);p.position=new Vector2(48*16,646);
            // Set up just before the next real vanilla reach edge. Faster
            // legal service must not wait for movement to travel twelve pixels
            // merely to satisfy this entered-strip fixture's premise.
            if(denseEdge)p.position.X=(float)(Math.Ceiling((p.position.X+p.width)/16d)*16d-p.width)-0.04f;
            p.inventory[12].SetDefaults(ItemID.WoodWall);p.inventory[12].stack=4;
            p.inventory[14].SetDefaults(ItemID.StoneBlock);p.inventory[14].stack=2;
            Item stoneSource=p.inventory[14];if(denseEdge)p.inventory[14].TurnToAir();
            // Both WoodWall endpoints fit vanilla reach. After the first real
            // use, its successor lies behind 64 missing StoneWall records:
            // beyond one shared 64-step source allowance even without holes.
            // Row 37 is inside native Y reach 3; row 36 is not an early goal.
            int sourceX=43,width=11,firstIndex=11,lastIndex=76;
            if(denseEdge)
            {
                int left,top,rightEdge,bottom;
                TileReachCheckSettings.Simple.GetTileRegion(p,out left,out top,out rightEdge,out bottom,p.inventory[12].tileBoost+p.blockRange);
                sourceX=left-2;width=17;firstIndex=width+2;lastIndex=6*width+12;
            }
            int firstX=denseEdge?sourceX+2:sourceX,lateX=denseEdge?sourceX+12:sourceX+10,stoneX=lateX-1;
            var cells=Enumerable.Range(0,width*7).Select(i=>new CaptureCell{X=i%width,Y=i/width,
                Layers=denseEdge && i/width<4?CaptureLayer.Tile|CaptureLayer.Wall:CaptureLayer.Wall,
                TileType=denseEdge && i/width<4?(ushort)TileID.Stone:(ushort)0,
                WallType=i==firstIndex || i==lastIndex?(ushort)WallID.Wood:(ushort)WallID.Stone}).ToArray();
            if(denseEdge)foreach(var cell in cells.Where(cell=>(cell.Layers&CaptureLayer.Tile)!=0))NativeToolsChecks.Tile(sourceX+cell.X,36+cell.Y,TileID.Stone);
            int endOrdinal = (int)Call(host, "BuildMemberOrdinal", cells.Length);
            var instance=Place(context,host,input,Asset(host,cells),sourceX,36);
            Place(context,host,input,Asset(host,new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.Stone}),stoneX,42);
            WorldGen.PlaceWall(lateX,43,WallID.Wood,true);
            WorldGen.PlaceWall(firstX-1,37,WallID.Wood,true);
            Require(Main.tile[lateX,43].wall==WallID.Wood && Main.tile[firstX-1,37].wall==WallID.Wood,"both separated wall endpoints have legal native supports");
            long classificationsBefore=(long)Get(host,"NearBuildClassifications");
            SetAutomatic(host,input,true);
            int updates=0,maxNear=0,maxForward=0,maxSources=0,boundsChanges=0,nativeBoundsChanges=0;bool right=true;
            object priorBounds=null;float startX=p.position.X,minX=startX,maxX=startX;bool moved=false;
            Rectangle? priorNativeBounds=null;
            var preferredWalks = new Dictionary<object, PreferredWalkObservation>();
            bool preferredPending = false, preferredResumed = false; int maxPreferred = 0;
            var actualOrder = new List<string>(); bool firstWallSeen = false, lateWallSeen = false, stoneSeen = false;
            try{NativeQuickItemChecks.Until(()=>
            {
                NativeToolExecutionChecks.Sample(context,input,new Vector2(480,540),false);
                // Sample initializes neutral keyboard state. Apply real native
                // movement AFTER it, and feed both original controls/triggers.
                if(moving && (!denseEdge || firstWallSeen))
                {
                    // Alternate real controls across an interior tile edge.
                    // Staying near vanilla's 640px world clamp could stop all
                    // motion at the left limit and never change broad bounds.
                    if(denseEdge)
                    {if(right && p.position.X>=49*16-1)right=false;else if(!right && p.position.X<=48*16-1)right=true;}
                    else right=p.position.X<48*16;
                    p.controlLeft=!right;p.controlRight=right;
                    PlayerInput.Triggers.Current.Left=!right;PlayerInput.Triggers.Current.Right=right;
                }
                long before=(long)Get(host,"ProjectionMatchSourceSteps");
                float beforeNativeX=p.position.X;
                NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");updates++;
                int nativeLeft,nativeTop,nativeRight,nativeBottom;
                TileReachCheckSettings.Simple.GetTileRegion(p,out nativeLeft,out nativeTop,out nativeRight,out nativeBottom,p.inventory[12].tileBoost+p.blockRange);
                var nativeBounds=new Rectangle(nativeLeft,nativeTop,nativeRight-nativeLeft+1,nativeBottom-nativeTop+1);
                if(priorNativeBounds.HasValue && priorNativeBounds.Value!=nativeBounds)nativeBoundsChanges++;
                priorNativeBounds=nativeBounds;
                bool firstWall = Main.tile[firstX,37].wall==WallID.Wood, lateWall = Main.tile[lateX,42].wall==WallID.Wood;
                bool stone = Main.tile[stoneX,42].active();
                if(firstWall && !firstWallSeen)
                {
                    actualOrder.Add("wall:first@"+Main.GameUpdateCount);
                    if(denseEdge)
                    {
                        var incoming=new Item();incoming.SetDefaults(ItemID.StoneBlock);incoming.stack=2;
                        Require(p.GetItem(incoming,new GetItemSettings(NoText:true,NoSound:true)).IsAir,"only after the first real WoodWall does original GetItem enable the competing Stone");
                        stoneSource=p.inventory.Take(50).Single(item=>item.type==ItemID.StoneBlock && item.stack==2);
                        Console.WriteLine("BlueprintExecutionSearch dense competing Stone real pickup world="+Main.GameUpdateCount+" firstWall=true movementStartsNextUpdate=true");
                    }
                }
                if(lateWall && !lateWallSeen)actualOrder.Add("wall:late@"+Main.GameUpdateCount);
                if(stone && !stoneSeen)
                {
                    actualOrder.Add("stone@"+Main.GameUpdateCount);
                    // The fixture's two wall positions have independently
                    // installed native supports and remain within exact reach.
                    // Final completion used to accept wall -> stone -> wall;
                    // the owner's continuous-material contract forbids it.
                    Require(!firstWallSeen || lateWall,
                        "a known reachable, supplied WoodWall successor must precede another material; actual="+string.Join(",",actualOrder));
                    if(denseEdge)Require(firstWallSeen && lateWall && nativeBoundsChanges>0 && moved && p.position.X!=beforeNativeX,
                        "the first competing Stone actually consumes during native movement, after changed near bounds and both current-group walls");
                }
                firstWallSeen=firstWall;lateWallSeen=lateWall;stoneSeen=stone;
                moved|=p.position.X!=startX;minX=Math.Min(minX,p.position.X);maxX=Math.Max(maxX,p.position.X);
                Require(p.IsInTileInteractionRange(lateX,42,TileReachCheckSettings.Simple,p.inventory[12].tileBoost+p.blockRange),
                    "the late target stays inside actual native reach during the complete moving walk");
                Require((long)Get(host,"ProjectionMatchSourceSteps")-before<=256 && (int)Get(host,"materialBuildSourceBudget")>=0,
                    "local, forward and match still share the original per-update source budget");
                var sources=((IEnumerable)Get(host,"buildSources")).Cast<object>().ToArray();maxSources=Math.Max(maxSources,sources.Length);
                // A standalone cold Cadence scope can precede publication of
                // this instance's Match. Observe it only once it exists; the
                // world/input sequence and source discovery remain unmodified.
                if(GetOptional(instance,"Match")!=null)
                    ObservePreferredContinuation(host, EligibilityRanges(instance), preferredWalks, ref preferredPending, ref preferredResumed, ref maxPreferred);
                if(GetOptional(instance,"Match")!=null)foreach(var range in EligibilityRanges(instance))
                {
                    var state=NearRangeState(host,range);if(state==null)continue;
                    int cursor=(int)Get(state,"Cursor");maxNear=Math.Max(maxNear,cursor);
                    maxForward=checked((int)((long)Get(host,"NearBuildClassifications")-classificationsBefore));
                    var bounds=Get(state,"Bounds");if(priorBounds!=null && !priorBounds.Equals(bounds))boundsChanges++;priorBounds=bounds;
                    var covered=Get(state,"Covered");
                    Require(!(bool)Get(state,"Complete") ||
                        (int)Get(covered,"Right")>(int)Get(covered,"Left") && (int)Get(covered,"Bottom")>(int)Get(covered,"Top"),
                        "a finite shared source slice cannot claim complete near coverage before a nonempty strip has completed");
                }
                NativeToolExecutionChecks.Outer(context,input,1+updates%2);
                return Main.tile[firstX,37].wall==WallID.Wood && Main.tile[lateX,42].wall==WallID.Wood && Main.tile[stoneX,42].active() &&
                    (denseEdge || ProgressDormant(host,instance)) && !(bool)Get(Get(tools,"Use"),"Active") &&
                    (denseEdge?maxPreferred>128 && nativeBoundsChanges>0:maxNear>128 && maxForward>0 && (!moving || boundsChanges>0));
            });}
            catch
            {
                Console.Error.WriteLine("EXECUTION SEARCH state moving="+moving+" updates="+updates+" position="+p.position+" velocity="+p.velocity+
                    " maxNear="+maxNear+" maxForward="+maxForward+" boundsChanges="+boundsChanges+" maxSources="+maxSources+" moved="+moved+
                    " woodWall="+Main.tile[lateX,42].wall+" stone="+Main.tile[stoneX,42].active()+" dormant="+ProgressDormant(host,instance)+" use="+Get(Get(tools,"Use"),"Active")+
                    " minX="+minX+" maxX="+maxX+" nativeBoundsChanges="+nativeBoundsChanges+" stocks="+p.inventory[12].stack+"/"+stoneSource.stack);
                foreach(var source in ((IEnumerable)Get(host,"buildSources")).Cast<object>())
                {
                    var value=Get(Get(source,"Range"),"Materials");
                    Console.Error.WriteLine("EXECUTION SEARCH source sharedPlanner="+NearRangeSummary(host,Get(source,"Range"))+
                        " forward="+Get(value,"CandidateCursor")+" exhausted="+Get(value,"BuildExhausted")+
                        " sharedNear="+NearRangeSummary(host,Get(source,"Range")));
                }
                throw;
            }
            Console.WriteLine("BlueprintExecutionSearch completion moving="+moving+" denseEdge="+denseEdge+" sourceX="+sourceX+" updates="+updates+" stocks="+p.inventory[12].stack+"/"+stoneSource.stack+
                " near="+maxNear+" forward="+maxForward+" sources="+maxSources+" preferred="+maxPreferred+" pending="+preferredPending+" resumed="+preferredResumed+
                " boundsChanges="+boundsChanges+" nativeBoundsChanges="+nativeBoundsChanges+" order="+string.Join(",",actualOrder));
            Require(p.inventory[12].stack==2 && stoneSource.stack==1 && (denseEdge || maxNear>128 && maxForward>0) && maxSources>=2 &&
                preferredPending && preferredResumed && maxPreferred>128 &&
                (!moving || moved && (denseEdge?nativeBoundsChanges>0:boundsChanges>0)),
                "the preferred tail resumes across real Updates without cursor rewind, while both wall endpoints and another source really consume under stationary/continuous movement");
            p.controlLeft=p.controlRight=false;
            PlayerInput.Triggers.Current.Left=PlayerInput.Triggers.Current.Right=false;
            int settling=0;
            if(denseEdge)Until(context,input,()=>{settling++;return ProgressDormant(host,instance);});
            for(int i=0;i<40;i++)Step(context,input);
            Require(ProgressDormant(host,instance),"after genuine movement stops, the complete remaining unsupplied scene becomes dormant");
            long settled=(long)Get(host,"ProjectionMatchSourceSteps");
            for(int i=0;i<20;i++)Step(context,input);
            Require(settled==(long)Get(host,"ProjectionMatchSourceSteps"),"completed near/forward passes sleep while their real signals remain unchanged");
            p.inventory[13].SetDefaults(ItemID.StoneWall);p.inventory[13].stack=1;
            Until(context,input,()=>p.inventory[13].IsAir);
            Require(cells.Where(c=>c.WallType==WallID.Stone).Any(c=>Main.tile[sourceX+c.X,36+c.Y].wall==WallID.Stone) && Main.tile[firstX,37].wall==WallID.Wood && Main.tile[lateX,42].wall==WallID.Wood &&
                p.inventory[12].stack==2 && stoneSource.stack==1 && (long)Get(host,"BuildUnknownResults")==0,
                "a later real refill revisits the already passed prefix without duplicate spending or an automatic toggle");
            Console.WriteLine("PASS BlueprintExecutionSearch moving="+moving+" denseEdge="+denseEdge+" records="+cells.Length+" missingStone="+(cells.Length-2)+" betweenWood="+(lastIndex-firstIndex-1)+" updates="+updates+" maxNear="+maxNear+
                " maxForward="+maxForward+" retainedSources="+maxSources+" boundsChanges="+boundsChanges+" minX="+minX+" maxX="+maxX+
                " maxPreferred="+maxPreferred+" preferredBudgetPaused="+preferredPending+" preferredResumed="+preferredResumed+
                " nativeWoodWall=2 nativeStone=1 refillStoneWall=1 stablePassSleeping=true settling="+settling+" order="+string.Join(",",actualOrder));
        }
        private static void BudgetPrefixContinuation(object context, object host, object tools, object input, bool warm, bool shared)
        {
            var p = Reset(context, host, tools, input);
            p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 8;
            var cells = new List<CaptureCell>();
            if (shared)
            {
                for (int i = 0; i < 190; i++) cells.Add(new CaptureCell { X = i % 19, Y = i / 19, Layers = CaptureLayer.Tile, TileType = TileID.Dirt });
                p.position = new Vector2(72 * 16, 646); p.velocity = Vector2.Zero;
            }
            else for (int i = 0; i < 300; i++) cells.Add(new CaptureCell { X = i % 30, Y = i / 30,
                Layers = CaptureLayer.Tile, TileType = i == 2 || i == 3 || i == 4 ? TileID.Stone : TileID.Dirt });
            var instance = Place(context, host, input, Asset(host, cells.ToArray()), 42, 42);
            object smaller = null;
            if (shared)
            {
                // Physical library chunks, rather than arbitrary 32-tile
                // geometry, own these logical ranges. Separate real assets
                // share the same Host source/read budget: 190 leaves two of
                // its reserved 192 steps, and index 2 belongs to the next slice.
                var small = Enumerable.Range(0, 100).Select(i => new CaptureCell { X = i % 20, Y = i / 20,
                    Layers = CaptureLayer.Tile, TileType = i == 2 ? TileID.Stone : TileID.Dirt }).ToArray();
                smaller = Place(context, host, input, Asset(host, small), 74, 42);
            }
            // Commit completion does not imply that the new bounded catalog
            // qualification has published Match. Stop at its first presence,
            // retaining the cold arm's incomplete world/source proof below.
            NativeQuickItemChecks.Until(()=>{Step(context,input);return (bool)Call(host,"ProjectionQualificationCurrent",instance) && GetOptional(instance,"Match")!=null &&
                (smaller==null || (bool)Call(host,"ProjectionQualificationCurrent",smaller) && GetOptional(smaller,"Match")!=null);});
            var ranges = EligibilityRanges(instance).Concat(smaller == null ? Array.Empty<object>() : EligibilityRanges(smaller)).ToArray();
            Require(ranges.Length == (shared ? 2 : 1), "independent sparse records really occupy the intended ordinary logical ranges");
            if (warm) NativeQuickItemChecks.Until(() => { Step(context, input); return ranges.All(r => (bool)Get(r, "Current")); });
            else Require(ranges.Any(r => !(bool)Get(r, "Current")), "the cold entry genuinely begins before complete cross-update world coverage");
            long requests = (long)Get(host, "ProjectionMatchSourceRequests"), source = (long)Get(host, "ProjectionMatchSourceSteps"), checks = (long)Get(host, "BuildCandidateChecks");
            SetAutomatic(host, input, true);
            int ticks = 0; bool partial = false;
            Until(context, input, () =>
            {
                ticks++; partial |= ranges.Any(r => (bool)Get(r, "Reading"));
                return (shared ? Main.tile[76, 42].active() : Enumerable.Range(44, 3).All(x => Main.tile[x, 42].active())) && ProgressDormant(host,instance) &&
                    (smaller == null || ProgressDormant(host,smaller)) && !(bool)Get(Get(tools, "Use"), "Active");
            });
            int spent = shared ? 1 : 3;
            Require(!Main.tile[42, 42].active() && !Main.tile[43, 42].active() && Scene(host).Length == (shared ? 2 : 1) && p.inventory[12].stack == 8 - spent,
                "the available index-2 prefix actually consumes and builds through Player.Update; missing Dirt stays missing without any supply or position wake");
            Console.WriteLine("BlueprintBuild finite source metrics warm=" + warm + " shared=" + shared + " sourceRequests=" + ((long)Get(host, "ProjectionMatchSourceRequests") - requests) +
                " sourceSteps=" + ((long)Get(host, "ProjectionMatchSourceSteps") - source) + " candidateChecks=" + ((long)Get(host, "BuildCandidateChecks") - checks));
            Require((long)Get(host, "ProjectionMatchSourceRequests") - requests <= 8 && (long)Get(host, "ProjectionMatchSourceSteps") - source <= (shared ? 1600 : 2200) &&
                (long)Get(host, "BuildCandidateChecks") - checks <= (shared ? 450 : 650),
                "retained source and exact ordinal finish the finite pass without whole-chunk transport for every skipped material or each successful target");
            Console.WriteLine("PASS BlueprintBuild cross-update source warm=" + warm + " sharedBudgetSmallRange=" + shared + " records=" + cells.Count + " actualStone=" + spent +
                " updates=" + ticks + " partialObserved=" + partial + " sourceRequests=" + ((long)Get(host, "ProjectionMatchSourceRequests") - requests) +
                " sourceSteps=" + ((long)Get(host, "ProjectionMatchSourceSteps") - source) + " candidateChecks=" + ((long)Get(host, "BuildCandidateChecks") - checks));
        }

        private sealed class ContinuousPlacement
        {
            internal int Frame, Animation, Time, X, Slot, Type, Mode, SupplySlot = -1;
            internal uint WorldTick;
            internal long Token, Native, Flight, PreparationClicks;
            internal string Kind, Consumer;
            internal Item Item, Supply;
            internal object Owner;
        }
        private static void BuildSourceFullContinuation(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 8;
            var original = p.inventory[12];
            var worker=Get(Get(host,"Library"),"worker");
            long workerBuilds=(long)Get(worker,"projectionGeometryDirectoryBuilds"),workerNodes=(long)Get(worker,"projectionGeometryDirectoryNodes");
            WorldGen.PlaceTile(44, 41, TileID.Stone, true, true); WorldGen.PlaceTile(45, 41, TileID.Stone, true, true);
            var stone = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone });
            Place(context, host, input, stone, 44, 42);
            foreach (int x in new[] { 42, 43, 45 }) Place(context, host, input, stone, x, 42);
            Place(context, host, input, stone, 44, 40); Place(context, host, input, stone, 45, 40);
            var instances=Scene(host);
            SetAutomatic(host, input, true);
            NativeQuickItemChecks.Until(() =>
            {
                NativeQuickItemChecks.Sample(input, Array.Empty<Microsoft.Xna.Framework.Input.Keys>()); NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime");
                return GetOptional(host, "preparedBuild") != null && (int)Get(Get(host, "buildSources"), "Count") == 4;
            });
            var pinned = ((IEnumerable)Get(host, "buildSources")).Cast<object>().Select(v => Get(v, "Range")).ToArray();
            Require(Scene(host).Length == 6 && p.inventory[12].stack == 8 && pinned.Length == 4 &&
                pinned.All(r => NearRangeHasPendingWork(host,r)),
                "four unfinished source parts fill the cache while six supported same-material instances are already admitted; at least two available instances are outside that cache");
            // The first flight is already bound to12. Publishing an earlier
            // twin cannot turn cache replacement into a different Item source.
            p.inventory[11].SetDefaults(ItemID.StoneBlock); p.inventory[11].stack = 3; var earlier = p.inventory[11];
            long requests = (long)Get(host, "ProjectionMatchSourceRequests"), steps = (long)Get(host, "ProjectionMatchSourceSteps");
            var qualification=(BlueprintProjectionQualificationReceipt)instances.Select(owner=>GetOptional(owner,"Qualification")).First(value=>value!=null);
            var directory=qualification.GeometryDirectory;
            object[] geometries;long chargedGeometryBytes;
            lock(Get(worker,"gate"))
            {
                var template=((IDictionary)Get(worker,"projectionOwners"))[qualification.Identity.TemplateKey];
                geometries=((IDictionary)Get(template,"QualificationViews")).Values.Cast<object>().ToArray();
                chargedGeometryBytes=(long)Get(template,"QualificationGeometryBytes");
            }
            long sharedDirectoryBytes=directory.Bytes;
            long expectedGeometryBytes=qualification.SourceRanges.Sum(range=>range.SourceIndexBytes)+qualification.MatchRanges.Sum(range=>range.SourceIndexBytes)+
                directory.Bytes+128L+(qualification.SourceRanges.Count+qualification.MatchRanges.Count)*64L;
            Require(directory.NodeCount==1 && qualification.SourceRanges.Count==1 && ReferenceEquals(directory.Sources,qualification.SourceRanges) &&
                directory.GetNode(0).Group==0 && directory.GetNode(0).Left==-1 && directory.GetNode(0).Right==-1 &&
                directory.GetNode(0).Bounds.Equals(qualification.SourceRanges[0].Bounds) && sharedDirectoryBytes==72+32 &&
                geometries.Length==1 && ReferenceEquals(Get(geometries[0],"Directory"),directory) &&
                chargedGeometryBytes==(long)Get(geometries[0],"Bytes") && chargedGeometryBytes==expectedGeometryBytes,
                "the actual template owner charges one complete one-node directory once with its shared qualified source geometry");
            long directoryBuilds=(long)Get(host,"BuildFocusDirectoryBuilds"),directoryConstruction=(long)Get(host,"BuildFocusDirectoryConstructionSteps"),
                directoryQueries=(long)Get(host,"BuildFocusDirectorySteps"),directoryRoutes=(long)Get(host,"BuildFocusDirectoryRoutes");
            long peakDirectoryBytes=0;int observedQueries=0;
            var initiallyRetained = pinned.Select(r => Get(r, "Owner")).ToArray();
            var patch = new Harmony("JueMingR.Tests.BlueprintFullSourceContinuation");
            patch.Patch(typeof(Player).GetMethod("PlaceThing_Tiles_PlaceIt", F), postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(ContinuousPlaced)));
            continuousPlacements.Clear(); continuousFrame = 0; continuousHost = host; continuousUse = Get(tools, "Use");
            continuousAutomatic = continuousWatch = true;
            long previousSteps = steps, previousChecks = (long)Get(host, "BuildCandidateChecks"),previousQueries=directoryQueries,previousConstruction=directoryConstruction;
            try
            {
                Until(context, input, () => {
                    continuousFrame++;
                    long currentSteps = (long)Get(host, "ProjectionMatchSourceSteps"), currentChecks = (long)Get(host, "BuildCandidateChecks");
                    Require((int)Get(Get(host, "buildSources"), "Count") <= 4 && (int)Get(host, "materialBuildSourceBudget") >= 0 &&
                        currentSteps - previousSteps <= 256 && currentChecks - previousChecks <= 64 &&
                        (long)Get(host,"BuildFocusDirectorySteps")-previousQueries+(long)Get(host,"BuildFocusDirectoryConstructionSteps")-previousConstruction<=64,
                        "six-instance same-group discovery and execution keep the shared per-Update budgets and four retained transports");
                    previousSteps = currentSteps; previousChecks = currentChecks;previousQueries=(long)Get(host,"BuildFocusDirectorySteps");
                    previousConstruction=(long)Get(host,"BuildFocusDirectoryConstructionSteps");
                    peakDirectoryBytes=Math.Max(peakDirectoryBytes,(long)Get(host,"BuildFocusDirectoryBytes"));
                    var focus=GetOptional(host,"buildContinuation");
                    if(focus!=null)foreach(DictionaryEntry query in (IDictionary)Get(Get(host,"nearBuildDiscovery"),"Owners"))
                    {
                        Require(instances.Contains(query.Key) && ReferenceEquals(Get(query.Value,"Directory"),directory),
                            "live per-instance Host queries retain the template's same frozen directory instead of building private copies");
                        observedQueries++;
                    }
                    return Scene(host).Length == 0 && !(bool)Get(continuousUse, "Active");
                });
                Require(continuousPlacements.Count == 6 && continuousPlacements.Select(v => v.Owner).Distinct().Count() == 6 &&
                    continuousPlacements.Select(v => v.Owner).Distinct().Count(owner => !initiallyRetained.Contains(owner)) >= 2 &&
                    continuousPlacements.All(v => v.Slot == 12 && ReferenceEquals(v.Item, original)) &&
                    ReferenceEquals(p.inventory[12], original) && original.stack == 2 && ReferenceEquals(p.inventory[11], earlier) && earlier.stack == 3 &&
                    Enumerable.Range(42, 4).All(x => Main.tile[x, 42].active() && Main.tile[x, 42].type == TileID.Stone) &&
                    new[] { 44, 45 }.All(x => Main.tile[x, 40].active() && Main.tile[x, 40].type == TileID.Stone) &&
                    (long)Get(host, "ProjectionMatchSourceRequests") - requests <= 12 && (long)Get(host, "ProjectionMatchSourceSteps") - steps <= 128 &&
                    (int)Get(Get(host, "buildSources"), "Count") <= 4,
                    "all six native goals consume once from the original exact source despite a four-part transport cache and an earlier identical type; discovery work stays finite");
                Console.WriteLine("PASS BlueprintBuild full source continuation pinned=4 instances=6 actualStone=6 originalSource=12 earlierTwinConsumed=0 sourceRequests=" +
                    ((long)Get(host, "ProjectionMatchSourceRequests") - requests) + " sourceSteps=" + ((long)Get(host, "ProjectionMatchSourceSteps") - steps));
                var gaps = continuousPlacements.Skip(1).Select((e, i) => unchecked(e.WorldTick - continuousPlacements[i].WorldTick)).ToArray();
                Console.WriteLine("BlueprintCadence six-instance four-retained events=world/time/owner/token " + string.Join(";", continuousPlacements.Select(v =>
                    v.WorldTick + "/" + v.Time + "/" + ((BlueprintProjectionIdentity)Get(v.Owner, "Identity")).ProjectionGeneration + "/" + v.Token)) + " gaps=" + string.Join(",", gaps));
                Require(gaps.Select((gap, i) => gap == continuousPlacements[i].Time).All(equal => equal),
                    "all six exact-source native consumers, including both initially nonresident instances, use each preceding native itemTime opportunity");
                long built=(long)Get(worker,"projectionGeometryDirectoryBuilds")-workerBuilds,
                    construction=(long)Get(worker,"projectionGeometryDirectoryNodes")-workerNodes;
                // Six instances consume one immutable qualified source. The
                // worker owns its sole node allocation; Host owns only the
                // independently bounded query/coverage/stack state above.
                Require(built==1 && construction==1 && (long)Get(worker,"projectionGeometryDirectoryMaximumBatch")<=64 && observedQueries>0 &&
                    instances.All(owner=>ReferenceEquals(((BlueprintProjectionQualificationReceipt)Get(owner,"Qualification")).GeometryDirectory,directory)) &&
                    peakDirectoryBytes<=1024+6*1024 && (long)Get(host,"BuildFocusDirectoryBuilds")==directoryBuilds &&
                    (long)Get(host,"BuildFocusDirectoryConstructionSteps")==directoryConstruction && (long)Get(host,"BuildFocusDirectoryTemporaryBytes")==0,
                    "all six real consumers share one worker-built one-node directory, with separately bounded Host queries and zero Host construction");
                long stableBuilds=(long)Get(worker,"projectionGeometryDirectoryBuilds"),stableConstruction=(long)Get(worker,"projectionGeometryDirectoryNodes");
                for(int i=0;i<12;i++)Step(context,input);
                Require(stableBuilds==(long)Get(worker,"projectionGeometryDirectoryBuilds") && stableConstruction==(long)Get(worker,"projectionGeometryDirectoryNodes"),
                    "stable callbacks do not rebuild the geometry directory");
                Console.WriteLine("BlueprintFocusDirectory instances=6 workerBuilds="+built+" workerConstructionSteps="+construction+" hostConstruction=0"+
                    " queryNodes="+((long)Get(host,"BuildFocusDirectorySteps")-directoryQueries)+" routedLogicalParts="+((long)Get(host,"BuildFocusDirectoryRoutes")-directoryRoutes)+
                    " activeEstimatedBytesPeak="+peakDirectoryBytes+" workerSharedDirectoryBytes="+sharedDirectoryBytes+" workerChargedQualifiedGeometryBytes="+chargedGeometryBytes+
                    " temporaryOrderBytesPeak="+Get(host,"BuildFocusDirectoryTemporaryBytes")+" stableRebuilds=0");
            }
            finally
            {
                continuousWatch = false; continuousHost = continuousUse = null; continuousPlacements.Clear();
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                SetAutomatic(host, input, false);
            }
        }
        private static void BuildSourceWorldIdentity(object context, object host, object tools, object input)
        {
            foreach (bool arrays in new[] { false, true })
            {
                var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 8;
                var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone });
                for (int i = 0; i < 4; i++) Place(context, host, input, entry, 42 + i, 42);
                SetAutomatic(host, input, true);
                NativeQuickItemChecks.Until(() =>
                {
                    NativeQuickItemChecks.Sample(input, Array.Empty<Microsoft.Xna.Framework.Input.Keys>()); NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime");
                    return GetOptional(host, "preparedBuild") != null && (int)Get(Get(host, "buildSources"), "Count") == 4;
                });
                var oldTiles = Main.tile; var oldSections = Main.sectionManager;
                try
                {
                    Require(p.inventory[12].stack == 8 && Enumerable.Range(42, 4).All(x => !Main.tile[x, 42].active()) &&
                        Scene(host).SelectMany(EligibilityRanges).All(r => NearRangeHasPendingWork(host,r)),
                        "all four immutable source slots are genuinely pinned before any original use");
                    if (arrays) Main.tile = (Tile[,])oldTiles.Clone();
                    else { Main.sectionManager = new WorldSections((Main.maxTilesX + 199) / 200, (Main.maxTilesY + 149) / 150); Main.sectionManager.SetAllSectionsLoaded(); }
                    Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"));
                    Require(p.inventory[12].stack == 4 && Enumerable.Range(42, 4).All(x => Main.tile[x, 42].active() && Main.tile[x, 42].type == TileID.Stone) &&
                        (int)Get(Get(host, "buildSources"), "Count") == 0 && (long)Get(host, "BuildUnknownResults") == 0,
                        "replacing the actual world array/section identity releases old range pins and reacquires exact sources without stalls, duplicate spend or unknown results");
                    Console.WriteLine("PASS BlueprintBuild source owner replacement arrays=" + arrays + " pinnedSources=4 actualConsumed=4 finalPins=0");
                }
                finally { SetAutomatic(host, input, false); Call(host, "ClearProjection"); Main.tile = oldTiles; Main.sectionManager = oldSections; }
            }
        }
        private static readonly List<ContinuousPlacement> continuousPlacements = new List<ContinuousPlacement>();
        private static object continuousHost, continuousUse;
        // Only the two finite failure probes attach this observer. It reads
        // the existing native receipt and is restored by each fixture.
        private static Action<ContinuousPlacement> continuousObserved;
        private static bool continuousWatch, continuousAutomatic, staleCheckIssued;
        private static int continuousFrame;
        private static void ContinuousPlaced(Player __instance)
        {
            if (!continuousWatch || !ReferenceEquals(__instance, Main.LocalPlayer) || !Main.tile[Player.tileTargetX, Player.tileTargetY].active()) return;
            RecordContinuousPlacement(__instance, Player.tileTargetX);
        }
        private static void ContinuousPainted(int __0, int __1, bool __result)
        {
            if (!continuousWatch || !continuousAutomatic || !__result) return;
            var flight = GetOptional(continuousHost, "activeBuild"); var operation = flight == null ? null : GetOptional(flight, "Operation");
            if (operation == null || Get(operation, "Kind").ToString() != "PaintTile" ||
                (int)Get(Get(flight, "Target"), "WorldX") != __0 || (int)Get(Get(flight, "Target"), "WorldY") != __1) return;
            RecordContinuousPlacement(Main.LocalPlayer, __0);
        }
        private static void RecordContinuousPlacement(Player p, int x)
        {
            var flight = continuousAutomatic ? GetOptional(continuousHost, "activeBuild") : null;
            var operation = flight == null ? null : GetOptional(flight, "Operation");
            var primary = flight == null ? null : ((IEnumerable)Get(flight, "Dependencies")).Cast<object>().FirstOrDefault(value => (bool)Get(value, "Primary"));
            var observation = new ContinuousPlacement { Frame = continuousFrame, Animation = p.itemAnimation, Time = p.itemTime, WorldTick = Main.GameUpdateCount, X = x,
                Token = continuousAutomatic ? (long)Get(continuousUse, "Operation") : 0, Native = continuousAutomatic ? (long)Get(continuousUse, "NativeGeneration") : 0,
                Flight = flight == null ? 0 : (long)Get(flight, "Generation"),
                Slot = flight == null ? p.selectedItem : (int)Get(Get(flight, "Source"), "Slot"),
                Type = flight == null ? p.HeldItem.type : (int)Get(Get(flight, "Source"), "Type"),
                Item = flight == null ? p.HeldItem : (Item)Get(Get(flight, "Source"), "Item"),
                Owner = flight == null ? null : Get(Get(Get(flight, "Target"), "Range"), "Owner"),
                Kind = operation == null ? "Body" : Get(operation, "Kind").ToString(),
                Consumer = operation == null ? "Tiles" : Get(operation, "Consumer").ToString(),
                Mode = operation == null ? 0 : (int)Get(operation, "Mode"),
                Supply = primary == null ? null : (Item)Get(primary, "Item"), SupplySlot = primary == null ? -1 : (int)Get(primary, "Slot"),
                PreparationClicks = continuousAutomatic ? (long)Get(Get(continuousHost, "buildMaterialPreparation"), "NativeClicks") : 0 };
            continuousPlacements.Add(observation);
            continuousObserved?.Invoke(observation);
        }
        private static void MaterialContinuityAndYield(object context, object host, object tools, object input)
        {
            var patch = new Harmony("JueMingR.Tests.BlueprintMaterialContinuity");
            patch.Patch(typeof(Player).GetMethod("PlaceThing_Tiles_PlaceIt", F), postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(ContinuousPlaced)));
            patch.Patch(typeof(WorldGen).GetMethod("paintTile", F, null, new[] { typeof(int), typeof(int), typeof(byte), typeof(bool), typeof(bool) }, null),
                postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(ContinuousPainted)));
            continuousHost = host; continuousUse = Get(tools, "Use"); continuousAutomatic = true;
            try
            {
                foreach (bool blocked in new[] { false, true })
                {
                    var p = Reset(context, host, tools, input); p.position = new Vector2(42 * 16, 646);
                    p.inventory[12].SetDefaults(ItemID.DirtBlock); p.inventory[12].stack = 8;
                    var dirt = p.inventory[12];
                    p.inventory[13].SetDefaults(ItemID.StoneBlock); p.inventory[13].stack = 2;
                    var cells = Enumerable.Range(0, 4).Select(x => new CaptureCell { X = x, Layers = CaptureLayer.Tile,
                        TileType = x == 1 ? TileID.Stone : TileID.Dirt }).ToArray();
                    var instance = Place(context, host, input, Asset(host, cells), 44, 42);
                    if (blocked) foreach (int x in new[] { 46, 47 }) Require(WorldGen.PlaceTile(x, 42, TileID.Stone, true, true), "preference obstacles are real original world bodies");
                    continuousPlacements.Clear(); continuousWatch = true; SetAutomatic(host, input, true);
                    bool earlier = false, focusAcrossWrites = false, preparedFallbackCurrent = false;
                    object focus = null;
                    for (continuousFrame = 0; continuousFrame < 300 && continuousPlacements.Count < (blocked ? 2 : 4); continuousFrame++)
                    {
                        NativeToolExecutionChecks.Sample(context, input, new Vector2(480, 540), false);
                        long steps = (long)Get(host, "ProjectionMatchSourceSteps"), checks = (long)Get(host, "BuildCandidateChecks");
                        NativeQuickItemChecks.BeginWorldStep(); p.Update(0);
                        if (!blocked && continuousPlacements.Count > 0 && !earlier)
                        {
                            // A genuine new earlier same-type stack must not
                            // steal this exact ongoing source or its reach.
                            p.inventory[11].SetDefaults(ItemID.DirtBlock); p.inventory[11].stack = 3; p.inventory[11].tileBoost = 1;
                            earlier = true;
                        }
                        Call(context, "UpdateRuntime");
                        Require((long)Get(host, "ProjectionMatchSourceSteps") - steps <= 256 && (int)Get(host, "materialBuildSourceBudget") >= 0 &&
                            (long)Get(host, "BuildCandidateChecks") - checks <= 64,
                            "ordinary, preference and deferred work share the original source and full-preflight allowances");
                        var currentFocus = GetOptional(host, "buildContinuation"); var prepared = GetOptional(host, "preparedBuild");
                        if (currentFocus != null && focus == null) focus = currentFocus;
                        if (!blocked && continuousPlacements.Count >= 2 && continuousPlacements.All(v => v.Type == ItemID.DirtBlock) && currentFocus != null)
                        {
                            Require(ReferenceEquals(focus, currentFocus), "the business material preference survives multiple real writes while each target receives fresh world proof");
                            focusAcrossWrites = true;
                        }
                        if (prepared != null && (int)Get(Get(prepared, "Target"), "WorldX") == 45)
                        {
                            Require((bool)Call(host, "IsBuildTargetCurrent", Get(prepared, "Target")), "the later ordinary fallback is prepared with a newly current world/support proof");
                            preparedFallbackCurrent = true;
                        }
                        if (!blocked && continuousPlacements.Count == 1)
                        {
                            var reach = (IDictionary<int, int>)Get(host, "buildItemReach"); int actualBoost;
                            Require(reach.TryGetValue(ItemID.DirtBlock, out actualBoost) && actualBoost == p.inventory[12].tileBoost && (int)Get(host, "buildEnvelopeBoost") == 1,
                                "one capability observation retains the exact active source reach while the earlier stack only expands the broad envelope");
                        }
                        // Empty outer callbacks preserve business intent and do no
                        // new preparation, preference scanning or native spend.
                        int count = continuousPlacements.Count;
                        var keptGroup = GetOptional(host, "buildContinuation");
                        long outerChecks = (long)Get(host, "BuildCandidateChecks");
                        var cursorOwners = GetOptional(instance, "Match") == null ? Array.Empty<object>() :
                            EligibilityRanges(instance).Select(r => NearRangeState(host,r)).Where(v => v != null).ToArray();
                        var preferenceCursors = cursorOwners.Select(v => (int)Get(v, "Cursor")).ToArray();
                        NativeToolExecutionChecks.Outer(context, input, 2);
                        Require(continuousPlacements.Count == count &&
                            ReferenceEquals(keptGroup, GetOptional(host, "buildContinuation")) &&
                            outerChecks == (long)Get(host, "BuildCandidateChecks") && cursorOwners.Select((v, i) => (int)Get(v, "Cursor") == preferenceCursors[i]).All(v => v),
                            "empty outer does not reset the business preference or spend another preparation allowance");
                    }
                    continuousWatch = false;
                    Require(continuousPlacements.Count >= 2 && continuousPlacements[0].X == 44 && continuousPlacements[0].Type == ItemID.DirtBlock,
                        "mixed sequence starts by the ordinary real Dirt placement");
                    if (blocked)
                    {
                        Require(continuousPlacements[1].X == 45 && p.inventory[13].stack == 1,
                            "when current-material targets are blocked, the first prepared legal Stone fallback actually consumes next");
                        foreach (int x in new[] { 46, 47 }) WorldGen.KillTile(x, 42, false, false, true);
                        Until(context, input, () => Scene(host).Length == 0);
                        Require(p.inventory[12].stack == 5, "real obstacle removal recovers all original Dirt work without toggling build");
                    }
                    else
                    {
                        var same = continuousPlacements.TakeWhile(v => v.Type == ItemID.DirtBlock).ToArray();
                        bool continuous = continuousPlacements.Count == 4 && same.Length == 3 && same.All(v => v.Slot == 12 && ReferenceEquals(v.Item, dirt)) &&
                            same.Select(v => v.Flight).Distinct().Count() == 3 && same.All(v => v.Token == same[0].Token) &&
                            continuousPlacements.Last().X == 45 && continuousPlacements.Last().Type == ItemID.StoneBlock && continuousPlacements.Last().Token != same[0].Token;
                        if (!continuous)
                        {
                            var group = GetOptional(host, "buildContinuation"); var source = group == null ? null : Get(group, "Source");
                            Console.Error.WriteLine("MATERIAL CONTINUITY events=" + string.Join(",", continuousPlacements.Select(v =>
                                v.Frame + ":" + v.X + "/" + v.Type + "@" + v.Slot + "#" + v.Token + "/" + v.Flight + " itemIsDirt=" + ReferenceEquals(v.Item, dirt))) +
                                " groupSource=" + (source == null ? "none" : Get(source, "Slot").ToString()) +
                                " group/pending=" + (group == null ? "none" : Get(group, "Group") + "/" + Get(group, "Pending")) +
                                " dirt/earlier/stone=" + dirt.stack + "/" + p.inventory[11].stack + "/" + p.inventory[13].stack);
                        }
                        Require(continuous,
                            "all current legal Dirt successors continue on the exact stack/lease before actual ordinary Stone service");
                        Require(focus != null && focusAcrossWrites && preparedFallbackCurrent &&
                            p.inventory[11].stack == 3 && ReferenceEquals(p.inventory[12], dirt) && dirt.stack == 5 &&
                            p.inventory[11].stack + p.inventory[12].stack == 8 && p.inventory[13].stack == 1,
                            "the material preference survives actual writes; the fallback binds current support and an earlier same-type Item never steals the ongoing exact source");
                        Until(context, input, () => Scene(host).Length == 0);
                    }
                    Console.WriteLine("PASS BlueprintBuild material continuity blocked=" + blocked + " placements=" +
                        string.Join(",", continuousPlacements.Select(v => v.X + ":" + v.Type + "@" + v.Slot + "#" + v.Token)) +
                        " focusAcrossWrites=" + focusAcrossWrites + " reboundCurrentFallback=" + preparedFallbackCurrent + " originalBudgets=true");
                    SetAutomatic(host, input, false);
                    Require(GetOptional(host, "buildContinuation") == null && (int)Get(Get(host, "buildSources"), "Count") == 0,
                        "turning construction off releases the preference and source transports");
                }
                ContinuousMixedAppearance(context, host, tools, input);
                ContinuousPreparedPaint(context, host, tools, input);
                RetainedPaintSuccessorRevoked(context, host, tools, input, false);
                RetainedPaintSuccessorRevoked(context, host, tools, input, true);
            }
            finally
            {
                continuousWatch = false; continuousHost = continuousUse = null;
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                SetAutomatic(host, input, false);
            }
        }
        private static void ContinuousMixedAppearance(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.position = new Vector2(42 * 16, 646);
            p.inventory[12].SetDefaults(ItemID.DirtBlock); p.inventory[12].stack = 8; var body = p.inventory[12];
            p.inventory[14].SetDefaults(ItemID.Paintbrush); var brush = p.inventory[14];
            p.inventory[20].SetDefaults(ItemID.RedPaint); p.inventory[20].stack = 6; var paint = p.inventory[20];
            var cells = Enumerable.Range(0, 4).Select(x => new CaptureCell { X = x, Layers = CaptureLayer.Tile,
                TileType = TileID.Dirt, TilePaint = paint.paint }).ToArray();
            Place(context, host, input, Asset(host, cells), 44, 42);
            long previousChecks = (long)Get(host, "BuildCandidateChecks"), previousSteps = (long)Get(host, "ProjectionMatchSourceSteps"),
                initialClicks = (long)Get(Get(host, "buildMaterialPreparation"), "NativeClicks");
            continuousPlacements.Clear(); continuousFrame = 0; continuousWatch = true; SetAutomatic(host, input, true);
            Until(context, input, () => {
                continuousFrame++;
                long checks = (long)Get(host, "BuildCandidateChecks"), steps = (long)Get(host, "ProjectionMatchSourceSteps"),
                    clicks = (long)Get(Get(host, "buildMaterialPreparation"), "NativeClicks");
                Require(continuousPlacements.Count(v => v.Kind == "Body") == 4 || paint.stack == 6 && clicks == initialClicks,
                    "the unrelated ready paint candidate cannot start supply preparation or spend paint while this body group still has successors");
                Require((int)Get(host, "materialBuildSourceBudget") >= 0 && checks - previousChecks <= 64 && steps - previousSteps <= 256,
                    "mixed native consumers retain the shared preparation and source budgets");
                previousChecks = checks; previousSteps = steps;
                return Scene(host).Length == 0 && !(bool)Get(continuousUse, "Active");
            });
            continuousWatch = false;
            var bodies = continuousPlacements.Where(v => v.Kind == "Body").ToArray();
            var colors = continuousPlacements.Where(v => v.Kind == "PaintTile").ToArray();
            Require(continuousPlacements.Count == 8 && bodies.Length == 4 && colors.Length == 4 &&
                continuousPlacements.Take(4).All(v => v.Kind == "Body") && bodies.All(v => v.Consumer == "Tiles" && v.Slot == 12 && ReferenceEquals(v.Item, body)) &&
                colors.All(v => v.Consumer == "Paintbrush" && v.Mode == paint.paint && ReferenceEquals(v.Item, brush) && ReferenceEquals(v.Supply, paint) && v.SupplySlot == 20) &&
                body.stack == 4 && paint.stack == 2 && Enumerable.Range(44, 4).All(x => Main.tile[x, 42].type == TileID.Dirt && Main.tile[x, 42].color() == paint.paint),
                "the first appearance lane is skipped during body search, all four exact-source bodies continue, and the finite boundary gives real native paint service with exact consumption");
            Console.WriteLine("PASS BlueprintBuild mixed continuous consumers=" + string.Join(",", continuousPlacements.Select(v =>
                v.Frame + ":" + v.Kind + "/" + v.Consumer + "/" + v.Mode + "@" + v.Slot + "+" + v.SupplySlot + "#" + v.Token + "/" + v.Native)));
        }
        private static void ContinuousPreparedPaint(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.position = new Vector2(42 * 16, 646);
            p.inventory[12].SetDefaults(ItemID.Paintbrush); var brush = p.inventory[12];
            p.inventory[20].SetDefaults(ItemID.RedPaint); p.inventory[20].stack = 5; var red = p.inventory[20];
            p.inventory[21].SetDefaults(ItemID.BluePaint); p.inventory[21].stack = 3; var blue = p.inventory[21];
            p.inventory[54].SetDefaults(ItemID.BluePaint); p.inventory[54].stack = 7; var priority = p.inventory[54];
            for (int x = 44; x < 48; x++) NativeToolsChecks.Tile(x, 42, TileID.Stone);
            var cells = Enumerable.Range(0, 4).Select(x => new CaptureCell { X = x, Layers = CaptureLayer.Tile,
                TileType = TileID.Stone, TilePaint = x == 1 ? blue.paint : red.paint }).ToArray();
            Place(context, host, input, Asset(host, cells), 44, 42);
            var preparation = Get(host, "buildMaterialPreparation"); var tail = preparation.GetType().GetMethod("NativeInputTailForChecks", F);
            long initialClicks = (long)Get(preparation, "NativeClicks");
            long initialNeutral = (long)Get(preparation, "NativeNeutralTails");
            Require(tail != null, "continuous supply uses the existing locked original inventory input tail");
            bool priorChecks = constructionChecksActive; var priorTail = constructionInputTail;
            var priorObservation = continuousObserved;
            Func<object, string> targetState = target => target == null ? "none" :
                Get(target, "WorldX") + "," + Get(target, "WorldY") + ":" + Get(target, "Ordinal") + "@" + Get(Get(target, "Range"), "Index");
            Func<string, string> flightState = name =>
            {
                var flight = GetOptional(host, name);
                return flight == null ? "none" : targetState(Get(flight, "Target")) + "#" + Get(flight, "Generation") + "/" + Get(flight, "Token");
            };
            Func<string> receiptState = () => "phase/epoch/token=" + Get(preparation, "phase") + "/" + Get(preparation, "receiptEpoch") + "/" + Get(preparation, "token") +
                " source/destination=" + Get(preparation, "sourceSlot") + "/" + Get(preparation, "destination") +
                " materialRed/Blue=" + ReferenceEquals(GetOptional(preparation, "Material"), red) + "/" + ReferenceEquals(GetOptional(preparation, "Material"), blue) +
                " forward/history=" + Get(preparation, "forwardCursor") + "/" + Get(preparation, "forwardCount") + "/" + Get(preparation, "historyCount") +
                " nativeCalls/tailPending=" + Get(preparation, "nativeCalls") + "/" + Get(preparation, "tailPending") +
                " clicks/neutral=" + ((long)Get(preparation, "NativeClicks") - initialClicks) + "/" + ((long)Get(preparation, "NativeNeutralTails") - initialNeutral) +
                " red/blue/old54=" + red.stack + "/" + blue.stack + "/" + priority.stack +
                " liveSlots=" + Array.IndexOf(p.inventory, red) + "/" + Array.IndexOf(p.inventory, blue) + "/" + Array.IndexOf(p.inventory, priority) +
                " active=" + flightState("activeBuild") + " prepared=" + flightState("preparedBuild") +
                " supplyTarget=" + targetState(GetOptional(host, "buildSupplyTarget"));
            int receiptLines = 0, observedEvents = 0; string priorReceipt = null;
            continuousObserved = observation =>
            {
                if (observedEvents++ >= 8) return;
                Console.WriteLine("BlueprintPreparedPaint native frame=" + observation.Frame + " x=" + observation.X +
                    " kind/consumer/mode=" + observation.Kind + "/" + observation.Consumer + "/" + observation.Mode +
                    " toolSlot/brush=" + observation.Slot + "/" + ReferenceEquals(observation.Item, brush) +
                    " primarySlot/red/blue/old54=" + observation.SupplySlot + "/" + ReferenceEquals(observation.Supply, red) + "/" +
                    ReferenceEquals(observation.Supply, blue) + "/" + ReferenceEquals(observation.Supply, priority) +
                    " primaryStackAtCall=" + observation.Supply?.stack + " token/native/flight=" + observation.Token + "/" + observation.Native + "/" + observation.Flight +
                    " " + receiptState());
            };
            constructionChecksActive = true; constructionInputTail = () => tail.Invoke(null, null);
            continuousPlacements.Clear(); continuousFrame = 0; continuousWatch = true;
            try
            {
                SetAutomatic(host, input, true);
                Until(context, input, () => {
                    continuousFrame++;
                    // Observe after the real Update as well as inside the
                    // writer: the paint debit may follow the WorldGen call.
                    string receipt = receiptState();
                    if (receipt != priorReceipt)
                    {
                        if (receiptLines++ < 64) Console.WriteLine("BlueprintPreparedPaint update=" + continuousFrame + " events=" + continuousPlacements.Count + " " + receipt);
                        priorReceipt = receipt;
                    }
                    return Scene(host).Length == 0 && !(bool)Get(continuousUse, "Active") && !(bool)Get(preparation, "HasWork") &&
                        ReferenceEquals(p.inventory[20], red) && ReferenceEquals(p.inventory[21], blue) && ReferenceEquals(p.inventory[54], priority) && Main.mouseItem.IsAir;
                });
                var same = continuousPlacements.TakeWhile(v => v.Mode == red.paint).ToArray();
                Console.WriteLine("BlueprintPreparedPaint components count/redPrefix=" + continuousPlacements.Count + "/" + same.Length +
                    " redSource=" + same.All(v => v.Kind == "PaintTile" && v.Consumer == "Paintbrush" && ReferenceEquals(v.Item, brush) && ReferenceEquals(v.Supply, red) && v.SupplySlot == 54) +
                    " redClicksEqual=" + (same.Length > 0 && same.All(v => v.PreparationClicks == same[0].PreparationClicks)) +
                    " redNativeValid/prepared=" + same.All(v => v.Token > 0 && v.Native > 0) + "/" + (same.Length > 0 && same[0].PreparationClicks > initialClicks) +
                    " redFlights/native=" + same.Select(v => v.Flight).Distinct().Count() + "/" + same.Select(v => v.Native).Distinct().Count() +
                    " lastBlueSource=" + (continuousPlacements.Count > 0 && continuousPlacements.Last().Mode == blue.paint && ReferenceEquals(continuousPlacements.Last().Supply, blue)) +
                    " events=" + string.Join(",", continuousPlacements.Take(8).Select(v => v.Frame + ":" + v.X + "/" + v.Mode + "@" + v.SupplySlot +
                        "#" + v.Token + "/" + v.Native + "/" + v.Flight + " clicks=" + (v.PreparationClicks - initialClicks))) +
                    " consumedRed/Blue/old54=" + (5 - red.stack) + "/" + (3 - blue.stack) + "/" + (7 - priority.stack) +
                    " brushStack=" + brush.stack +
                    " returnedRefs=" + ReferenceEquals(p.inventory[20], red) + "/" + ReferenceEquals(p.inventory[21], blue) + "/" + ReferenceEquals(p.inventory[54], priority) +
                    " worldColors=" + string.Join(",", Enumerable.Range(44, 4).Select(x => x + ":" + Main.tile[x, 42].color())) +
                    " receiptLines=" + receiptLines + " " + receiptState());
                Require(continuousPlacements.Count == 4 && same.Length == 3 && same.All(v => v.Kind == "PaintTile" && v.Consumer == "Paintbrush" &&
                    ReferenceEquals(v.Item, brush) && ReferenceEquals(v.Supply, red) && v.SupplySlot == 54 && v.PreparationClicks == same[0].PreparationClicks) &&
                    same.All(v => v.Token > 0 && v.Native > 0) && same.Select(v => v.Flight).Distinct().Count() == 3 &&
                    same.Select(v => v.Native).Distinct().Count() == 3 && same[0].PreparationClicks > initialClicks &&
                    continuousPlacements.Last().Mode == blue.paint && ReferenceEquals(continuousPlacements.Last().Supply, blue) &&
                    red.stack == 2 && blue.stack == 2 && priority.stack == 7 && brush.stack == 1 &&
                    Enumerable.Range(44, 4).All(x => Main.tile[x, 42].color() == (x == 45 ? blue.paint : red.paint)),
                    "one real receipt keeps the same red Item at54 through three native paints without extra clicks; blue receives finite service, and only both true main-bag sources are consumed before exact return");
                Console.WriteLine("PASS BlueprintBuild prepared paint continuous=" + string.Join(",", continuousPlacements.Select(v =>
                    v.Frame + ":" + v.Mode + "@" + v.SupplySlot + "#" + v.Token + "/" + v.Native + " clicks=" + v.PreparationClicks)) +
                    " redConsumed=3 blueConsumed=1 displaced54Consumed=0");
            }
            catch
            {
                Console.Error.WriteLine("BlueprintPreparedPaint failure events=" + continuousPlacements.Count + " scene=" + Scene(host).Length + " " + receiptState());
                throw;
            }
            finally
            {
                continuousObserved = priorObservation;
                continuousWatch = false; SetAutomatic(host, input, false);
                constructionChecksActive = priorChecks; constructionInputTail = priorTail;
            }
        }
        private static void RetainedPaintSuccessorRevoked(object context, object host, object tools, object input,bool cancelledExit)
        {
            var p = Reset(context, host, tools, input); p.position = new Vector2(42 * 16, 646);
            p.inventory[12].SetDefaults(ItemID.Paintbrush); var shortBrush = p.inventory[12];
            p.inventory[13].SetDefaults(ItemID.SpectrePaintbrush); var longBrush = p.inventory[13];
            p.inventory[20].SetDefaults(ItemID.RedPaint); p.inventory[20].stack = 3; var red = p.inventory[20];
            p.inventory[54].SetDefaults(ItemID.BluePaint); p.inventory[54].stack = 7; var priority = p.inventory[54];
            for (int x = 44; x < 46; x++) NativeToolsChecks.Tile(x, 42, TileID.Stone);
            Place(context, host, input, Asset(host, Enumerable.Range(0, 2).Select(x => new CaptureCell {
                X = x, Layers = CaptureLayer.Tile, TileType = TileID.Stone, TilePaint = red.paint }).ToArray()), 44, 42);
            var preparation = Get(host, "buildMaterialPreparation");
            bool priorChecks=constructionChecksActive;var priorTail=constructionInputTail;
            var tail=preparation.GetType().GetMethod("NativeInputTailForChecks",F);
            Require(tail!=null,"retained receipt case uses the existing locked original native input tail in every existing caller");
            constructionChecksActive=true;constructionInputTail=()=>tail.Invoke(null,null);
            continuousPlacements.Clear(); continuousFrame = 0; continuousWatch = true;
            try
            {
                SetAutomatic(host, input, true);
                Until(context, input, () => {
                    continuousFrame++;
                    var current = GetOptional(host, "activeBuild"); var next = GetOptional(host, "preparedBuild");
                    return continuousPlacements.Count == 1 && current != null && (bool)Get(current, "Checked") && next != null &&
                        (bool)Get(preparation, "Using") && !(bool)Get(continuousUse, "InNativeUse");
                });
                var successor = Get(host, "preparedBuild"); var target = Get(successor, "Target");
                Require(ReferenceEquals(Get(Get(successor, "Source"), "Item"), shortBrush) &&
                    (bool)Call(host, "BuildFlightCurrent", successor, false) && (bool)Call(host,"BuildContinuationCurrent",p) &&
                    ReferenceEquals(p.inventory[54], red) && red.stack == 2,
                    "the revocation case starts with one real native debit, its real54 receipt and a current exact short-brush successor");
                // Stimulate the existing use-retirement exit after ItemCheck
                // returned. This verifies the lifecycle edge; the preceding
                // fixture separately measures natural consecutive intervals.
                if(cancelledExit)Call(continuousUse,"Cancel");
                Call(continuousUse,"Retire");
                if(cancelledExit)
                {
                    Require(!(bool)Get(continuousUse,"Active") && (bool)Get(preparation,"Returning") &&
                        (long)Get(host,"buildSupplyReadyFlight")==0 && continuousPlacements.Count==1 && red.stack==2,
                        "an already cancelled checked use keeps the original Returning path and cannot retain a Ready successor receipt");
                    SetAutomatic(host,input,false);Until(context,input,()=>!(bool)Get(preparation,"HasWork"));
                    Require(ReferenceEquals(p.inventory[20],red) && ReferenceEquals(p.inventory[54],priority) && Main.mouseItem.IsAir &&
                        red.stack==2 && priority.stack==7 && Main.tile[45,42].color()==0,
                        "the cancelled use returns only its real remainder through native ItemSlot and never consumes the prepared successor");
                    Console.WriteLine("PASS BlueprintBuild cancelled checked exit: original Returning, no Ready receipt, native remainder return");
                    return;
                }
                Require(!(bool)Get(continuousUse, "Active") && (bool)Get(preparation, "Ready") &&
                    (long)Get(host, "buildSupplyReadyFlight") == (long)Get(successor, "Generation"),
                    "the real checked use exit releases ToolUse and retains only its already proven successor receipt");
                p.position = new Vector2(52 * 16, 646);
                int xTarget = (int)Get(target, "WorldX"), yTarget = (int)Get(target, "WorldY");
                Require(!p.IsInTileInteractionRange(xTarget, yTarget, TileReachCheckSettings.Simple, shortBrush.tileBoost + p.blockRange) &&
                    p.IsInTileInteractionRange(xTarget, yTarget, TileReachCheckSettings.Simple, longBrush.tileBoost + p.blockRange) &&
                    !(bool)Call(host, "BuildFlightCurrent", successor, false),
                    "actual movement revokes the exact short brush while the real long brush can still reach the unchanged target");
                long checks = (long)Get(host, "BuildCandidateChecks"), clicks = (long)Get(preparation, "NativeClicks");
                Require((bool)Call(host, "ContinueBuildSupply", new object[]{p,64}) && (bool)Get(preparation, "Returning") &&
                    GetOptional(host, "buildSupplyTarget") == null && GetOptional(host, "buildSupplyPlan") == null &&
                    (long)Get(host, "buildSupplyReadyFlight") == 0 && (long)Get(host, "BuildCandidateChecks") == checks &&
                    (long)Get(preparation, "NativeClicks") == clicks && continuousPlacements.Count == 1 && red.stack == 2,
                    "a revoked retained successor starts return immediately without replanning another reachable tool or spending its receipt");
                SetAutomatic(host, input, false);
                Until(context, input, () => !(bool)Get(preparation, "HasWork"));
                Require(ReferenceEquals(p.inventory[20], red) && ReferenceEquals(p.inventory[54], priority) && Main.mouseItem.IsAir &&
                    red.stack == 2 && priority.stack == 7 && Main.tile[44, 42].color() == red.paint && Main.tile[45, 42].color() == 0,
                    "the revoked finite receipt returns through actual native ItemSlot inputs and leaves the unexecuted target unchanged");
                Console.WriteLine("PASS BlueprintBuild retained successor revoked: checked real paint, Retire exit, exact short out-of-reach, long reachable, no reselect, native receipt return; natural intervals measured separately");
            }
            finally
            {
                continuousWatch = false; SetAutomatic(host, input, false);
                // Preserve a first assertion and its real receipt. Only drain
                // a still legal return through the existing native input tail;
                // never reset a protected/Unknown owner to prepare a sibling.
                try { for(int i=0;i<80 && (bool)Get(preparation,"Returning");i++)Step(context,input); }
                finally { constructionChecksActive=priorChecks;constructionInputTail=priorTail; }
            }
        }
        [HarmonyPriority(Priority.Last)]
        private static void LateContinuousCheck(Player __instance)
        {
            if (!continuousWatch || !continuousAutomatic || staleCheckIssued || continuousPlacements.Count != 1 || !(bool)Get(continuousUse, "InNativeUse")) return;
            var old = continuousPlacements[0]; var current = GetOptional(continuousHost, "activeBuild");
            if (current == null || (long)Get(current, "Generation") == old.Flight) return;
            staleCheckIssued = true;
            long unknown = (long)Get(continuousHost, "BuildUnknownResults");
            Call(continuousUse, "End", __instance, old.Token, new InvalidOperationException("isolated stale native finalizer"), old.Native);
            Require((bool)Get(continuousUse, "InNativeUse") && (long)Get(continuousHost, "BuildUnknownResults") == unknown && ReferenceEquals(current, Get(continuousHost, "activeBuild")),
                "a late finalizer of an older ItemCheck cannot return the new scope, mark the successor checked, or protect its source as unknown");
        }
        private static void NativeContinuousOpportunity(object context, object host, object tools, object input)
        {
            var patch = new Harmony("JueMingR.Tests.BlueprintContinuousOpportunity");
            patch.Patch(typeof(Player).GetMethod("PlaceThing_Tiles_PlaceIt", F), postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(ContinuousPlaced)));
            patch.Patch(typeof(Player).GetMethod("ItemCheck", F), prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(LateContinuousCheck)));
            continuousHost = host; continuousUse = Get(tools, "Use");
            try
            {
                var p = Reset(context, host, tools, input); p.position = new Vector2(42 * 16, 646);
                p.inventory[12].SetDefaults(ItemID.DirtBlock); p.inventory[12].stack = 6; p.selectedItemState.Select(12);
                Step(context, input);
                continuousPlacements.Clear(); continuousAutomatic = false; continuousWatch = true;
                for (continuousFrame = 0; continuousFrame < 100 && continuousPlacements.Count < 4; continuousFrame++)
                {
                    int x = 44 + continuousPlacements.Count;
                    NativeToolExecutionChecks.Sample(context, input, new Vector2(x * 16 + 8, 42 * 16 + 8), true);
                    NativeQuickItemChecks.BeginWorldStep(); p.Update(0); Call(context, "UpdateRuntime");
                }
                continuousWatch = false;
                Require(continuousPlacements.Count == 4 && p.inventory[12].stack == 2, "independent original held input actually places and consumes all four Dirt blocks");
                var referenceIntervals = Enumerable.Range(1, 3).Select(i => continuousPlacements[i].Frame - continuousPlacements[i - 1].Frame).ToArray();
                int reference = referenceIntervals[0];
                Require(reference > 0 && continuousPlacements[1].Animation > 0, "the locked original item exposes a second placement inside its first animation");

                p = Reset(context, host, tools, input); p.position = new Vector2(42 * 16, 646);
                p.inventory[12].SetDefaults(ItemID.DirtBlock); p.inventory[12].stack = 6;
                var originalSource = p.inventory[12];
                Place(context, host, input, Asset(host, Enumerable.Range(0, 4).Select(x => new CaptureCell { X = x, Layers = CaptureLayer.Tile, TileType = TileID.Dirt }).ToArray()), 44, 42);
                continuousPlacements.Clear(); continuousAutomatic = true; staleCheckIssued = false; continuousWatch = true;
                SetAutomatic(host, input, true); bool preparedInAnimation = false;
                AwaitFirstContinuousPlacement(context,input);
                for (int opportunities = 0; opportunities < 200 && (Scene(host).Length > 0 || (bool)Get(continuousUse, "Active")); opportunities++)
                {
                    continuousFrame++;
                    Step(context, input);
                    preparedInAnimation |= continuousPlacements.Count > 0 && p.itemAnimation > 0 && GetOptional(host, "preparedBuild") != null;
                }
                continuousWatch = false;
                Console.WriteLine("BlueprintContinuous actual placements="+continuousPlacements.Count+" stack="+p.inventory[12].stack+" scene="+Scene(host).Length+
                    " preparedDuringAnimation="+preparedInAnimation+" staleCheckIssued="+staleCheckIssued+
                    " startupFrames="+continuousPlacements[0].Frame+" events="+string.Join(",",continuousPlacements.Take(4).Select(v=>v.Frame+":"+v.X)));
                if(continuousPlacements.Count!=4 || p.inventory[12].stack!=2 || Scene(host).Length!=0)
                {
                    Console.Error.WriteLine("BlueprintContinuous events="+string.Join(",",continuousPlacements.Take(4).Select(v=>v.Frame+":"+v.X+"@"+v.Animation+"#"+v.Token+"/"+v.Flight+"/"+v.Native))+
                        " tick="+Get(host,"materialTick")+" player="+p.position+" anim/time="+p.itemAnimation+"/"+p.itemTime);
                    for(int x=44;x<=47;x++)Console.Error.WriteLine("BlueprintContinuous cell="+x+" active/type="+Main.tile[x,42].active()+"/"+Main.tile[x,42].type+
                        " reach="+p.IsInTileInteractionRange(x,42,TileReachCheckSettings.Simple,p.inventory[12].tileBoost+p.blockRange));
                    foreach(string name in new[]{"activeBuild","preparedBuild","deferredBuild"})
                    {
                        var flight=GetOptional(host,name);var target=flight==null?null:GetOptional(flight,"Target");
                        Console.Error.WriteLine("BlueprintContinuous "+name+"="+(flight==null?"none":target==null?"finite seed ordinal="+GetOptional(flight,"Ordinal"):
                            "target="+GetOptional(target,"WorldX")+" ordinal="+GetOptional(target,"Ordinal")+
                            " checked/entered/rejected="+GetOptional(flight,"Checked")+"/"+GetOptional(flight,"Entered")+"/"+GetOptional(flight,"Rejected")));
                    }
                    foreach(var scene in Scene(host))
                    {
                        var match=GetOptional(scene,"Match");if(match==null)continue;
                        foreach(var range in ((IEnumerable)Get(match,"Ranges")).Cast<object>())
                        {
                            var material=GetOptional(range,"Materials");if(material==null)continue;
                            Console.Error.WriteLine("BlueprintContinuous range=current:"+Get(range,"Current")+" demand:"+Get(range,"Demand")+" reading:"+Get(range,"Reading")+
                                " cursor/exhausted="+Get(material,"CandidateCursor")+"/"+Get(material,"BuildExhausted")+" waits="+Get(material,"BuildWaits")+
                                " retry/ordinal/due="+Get(material,"EligibilityRetry")+"/"+Get(material,"EligibilityRetryOrdinal")+"/"+Get(material,"EligibilityRetryAt"));
                        }
                    }
                }
                Require(continuousPlacements.Count == 4 && p.inventory[12].stack == 2 && Scene(host).Length == 0 && preparedInAnimation && staleCheckIssued,
                    "actual source accounting, early successor preparation, stale callback protection and full-Match retirement all hold for the continuous sequence");
                var first = continuousPlacements[0]; var second = continuousPlacements[1];
                Require(second.Animation > 0 && first.Token > 0 && continuousPlacements.All(v => v.Token == first.Token && v.Slot == 12 && ReferenceEquals(v.Item, originalSource)) &&
                    continuousPlacements.Select(v => v.Flight).Distinct().Count() == 4 && continuousPlacements.Select(v => v.Native).Distinct().Count() == 4 &&
                    Enumerable.Range(1, 3).All(i => continuousPlacements[i].Frame - continuousPlacements[i - 1].Frame == referenceIntervals[i - 1]),
                    "all three same-source successor opportunities match independent original held input, with exact Item/slot and distinct target/native generations");
                Console.WriteLine("PASS BlueprintBuild legal continuous Dirt referenceInterval=" + reference + " automaticInterval=" + (second.Frame - first.Frame) +
                    " originalIntervals=" + string.Join(",", referenceIntervals) + " actualIntervals=" +
                    string.Join(",", Enumerable.Range(1, 3).Select(i => continuousPlacements[i].Frame - continuousPlacements[i - 1].Frame)) +
                    " sharedLease=" + first.Token + " distinctFlights=" + first.Flight + "/" + second.Flight + " secondAnimation=" + second.Animation + " preparedDuringAnimation=true staleFinalizerIgnored=true actualConsumed=4");
                p = Reset(context, host, tools, input); p.position = new Vector2(42 * 16, 646);
                p.inventory[12].SetDefaults(ItemID.DirtBlock); p.inventory[12].stack = 6;
                var separate = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Dirt });
                for (int i = 0; i < 4; i++) Place(context, host, input, separate, 44 + i, 42);
                continuousPlacements.Clear(); staleCheckIssued = true; continuousWatch = true; SetAutomatic(host, input, true);
                AwaitFirstContinuousPlacement(context,input);
                for (int opportunities = 0; opportunities < 240 && (Scene(host).Length > 0 || (bool)Get(continuousUse, "Active")); opportunities++)
                {continuousFrame++;Step(context,input);}
                continuousWatch = false;
                Require(continuousPlacements.Count == 4 && p.inventory[12].stack == 2 && Scene(host).Length == 0 &&
                    Enumerable.Range(1, 3).All(i => continuousPlacements[i].Frame - continuousPlacements[i - 1].Frame == referenceIntervals[i - 1]) &&
                    continuousPlacements.All(v => v.Token == continuousPlacements[0].Token),
                    "natural retirement of each checked instance preserves every original held-input successor opportunity, without retaining old completion proof");
                Console.WriteLine("PASS BlueprintBuild continuous instance retirement instances=4 actualConsumed=4 referenceInterval=" + reference + " sameLease=true");
                foreach (bool different in new[] { false, true })
                {
                    p = Reset(context, host, tools, input); p.position = new Vector2(42 * 16, 646);
                    p.inventory[12].SetDefaults(ItemID.DirtBlock); p.inventory[12].stack = 2;
                    p.inventory[13].SetDefaults(different ? ItemID.StoneBlock : ItemID.DirtBlock); p.inventory[13].stack = 4;
                    var depletedSource = p.inventory[12]; var successorSource = p.inventory[13];
                    var targets = Enumerable.Range(0, 4).Select(x => new CaptureCell { X = x, Layers = CaptureLayer.Tile,
                        TileType = different && x >= 2 ? TileID.Stone : TileID.Dirt }).ToArray();
                    Place(context, host, input, Asset(host, targets), 44, 42);
                    continuousPlacements.Clear(); staleCheckIssued = true; continuousWatch = true; SetAutomatic(host, input, true);
                    AwaitFirstContinuousPlacement(context,input);
                    for (int opportunities = 0; opportunities < 240 && (Scene(host).Length > 0 || (bool)Get(continuousUse, "Active")); opportunities++)
                    {continuousFrame++;Step(context,input);}
                    continuousWatch = false;
                    Require(continuousPlacements.Count == 4 && Scene(host).Length == 0 && p.inventory[12].IsAir && p.inventory[13].stack == 2 &&
                        continuousPlacements[0].Token == continuousPlacements[1].Token && continuousPlacements[2].Token != continuousPlacements[1].Token &&
                        continuousPlacements.Take(2).All(v => v.Slot == 12 && ReferenceEquals(v.Item, depletedSource)) &&
                        continuousPlacements.Skip(2).All(v => v.Slot == 13 && ReferenceEquals(v.Item, successorSource)) &&
                        Enumerable.Range(44, 4).All(x => Main.tile[x, 42].type == (different && x >= 46 ? TileID.Stone : TileID.Dirt)),
                        "exhaustion and a successor stack/material use a new original selection lease, while distinct targets retain exact materials and no extra spend");
                    Console.WriteLine("PASS BlueprintBuild continuous source handoff differentMaterial=" + different + " exhaustedStack=2 successorConsumed=2 naturalRetirement=true");
                }
            }
            finally
            {
                continuousWatch = false; continuousHost = continuousUse = null;
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                SetAutomatic(host, input, false);
            }
        }
        private static void AwaitFirstContinuousPlacement(object context,object input)
        {
            // Source opening/worker publication is asynchronous and has no
            // gameplay cadence SLA. Establish the first real placement with
            // the existing bounded worker wait, retaining its event, lease,
            // consumption and cumulative Frame. Only then measure the original
            // 200/240 native-update window; never reset the first event to zero.
            continuousFrame=0;
            Until(context,input,()=>{if(continuousPlacements.Count>0)return true;continuousFrame++;return false;});
        }
        private static void PlacementQueryIsolation(object context, object host, object tools, object input)
        {
            Reset(context, host, tools, input);
            var query = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.Blueprint.BlueprintPlacementQuery", true);
            var placementField = typeof(TileObjectPreviewData).GetField("placementCache", F); var randomField = typeof(TileObjectPreviewData).GetField("randomCache", F);
            var oldPreview = TileObject.objectPreview; var oldPlacement = placementField.GetValue(null); var oldRandom = randomField.GetValue(null); var oldRng = Main.rand;
            try
            {
                int type = Enumerable.Range(0, TileID.Count).First(t => { var d = TileObjectData.GetTileData(t, 0); return d != null && d.RandomStyleRange > 0 && d.Width < 6 && d.Height < 6; });
                var previews = new[] { new TileObjectPreviewData(), new TileObjectPreviewData(), new TileObjectPreviewData() };
                foreach (var preview in previews) { preview.Active = true; preview.Type = 111; preview.Size = new Point16(3, 3); preview[1, 1] = 2; }
                TileObject.objectPreview = previews[0]; placementField.SetValue(null, previews[1]); randomField.SetValue(null, previews[2]);
                Main.rand = new UnifiedRandom(442011); var reference = new UnifiedRandom(442011); var tile = Main.tile[90, 40];
                object[] args = { 90, 40, type, 0, 1, default(TileObject), false, null };
                query.GetMethod("CanPlace", F).Invoke(null, args);
                Require(Main.rand.Next() == reference.Next() && ReferenceEquals(TileObject.objectPreview, previews[0]) && ReferenceEquals(placementField.GetValue(null), previews[1]) &&
                    ReferenceEquals(randomField.GetValue(null), previews[2]) && previews.All(v => v.Active && v.Type == 111 && v.Size == new Point16(3, 3) && v[1, 1] == 2) &&
                    ReferenceEquals(tile, Main.tile[90, 40]) && !tile.active(),
                    "the real CanPlace query copy exercises a random object while preserving original RNG stream, all three preview states, and the actual world Tile");
                Console.WriteLine("PASS BlueprintBuild private native query: randomType=" + type + " unchangedRng=true threePreviewStatesUnchanged=true noWorldWrite=true");
            }
            finally { Main.rand = oldRng; TileObject.objectPreview = oldPreview; placementField.SetValue(null, oldPlacement); randomField.SetValue(null, oldRandom); }
        }
    }
}
