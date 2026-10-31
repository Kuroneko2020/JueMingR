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
        private sealed class PreferredWalkObservation
        {
            internal long Group, CompletedBatch;
            internal int Cursor;
            internal bool Complete, BudgetPaused;
        }
        private static void ObservePreferredContinuation(object host, IEnumerable<object> ranges,
            Dictionary<object, PreferredWalkObservation> prior, ref bool pending, ref bool resumed, ref int maxCursor)
        {
            var group = GetOptional(host, "buildContinuation");
            var prepared = GetOptional(host, "preparedBuild");
            var source = group == null ? null : Get(group, "Source");
            bool preparedSameSource = prepared != null && source != null &&
                ReferenceEquals(Get(Get(prepared, "Source"), "Item"), Get(source, "Item")) &&
                (int)Get(Get(prepared, "Source"), "Slot") == (int)Get(source, "Slot");
            foreach (var range in ranges)
            {
                var value = GetOptional(range, "Materials"); if (value == null) continue;
                long identity = (long)Get(value, "PreferredGroup"); if (identity <= 0) continue;
                var current = new PreferredWalkObservation { Group = identity, Cursor = (int)Get(value, "PreferredCursor"),
                    CompletedBatch = (long)Get(value, "PreferredCompletedBatch"), Complete = (bool)Get(value, "PreferredComplete") };
                // Found also retains a resumable intent. A budget pause needs
                // an unfinished saved ordinal, the actual shared allowance at
                // zero and no ready successor from this exact wall source.
                current.BudgetPaused = group != null && (long)Get(group, "Group") == identity && (bool)Get(group, "Pending") &&
                    !current.Complete && current.Cursor > 0 && (int)Get(host, "materialBuildSourceBudget") == 0 && !preparedSameSource;
                PreferredWalkObservation previous;
                if (prior.TryGetValue(range, out previous) && previous.Group == current.Group && previous.CompletedBatch == current.CompletedBatch &&
                    !previous.Complete && !current.Complete)
                {
                    Require(current.Cursor >= previous.Cursor, "movement, source transport and a new lease cannot rewind an unfinished preferred member walk");
                    resumed |= previous.BudgetPaused && current.Cursor > previous.Cursor;
                }
                pending |= current.BudgetPaused; maxCursor = Math.Max(maxCursor, current.Cursor); prior[range] = current;
            }
        }
        private static void ExecutionSearchFairness(object context,object host,object tools,object input,bool moving)
        {
            var p=Reset(context,host,tools,input);p.position=new Vector2(48*16,646);
            p.inventory[12].SetDefaults(ItemID.WoodWall);p.inventory[12].stack=4;
            p.inventory[14].SetDefaults(ItemID.StoneBlock);p.inventory[14].stack=2;
            // Both WoodWall endpoints fit vanilla reach. After the first real
            // use, its successor lies behind 64 missing StoneWall records:
            // beyond one shared 64-step source allowance even without holes.
            // Row 37 is inside native Y reach 3; row 36 is not an early goal.
            var cells=Enumerable.Range(0,77).Select(i=>new CaptureCell{X=i%11,Y=i/11,Layers=CaptureLayer.Wall,
                WallType=i==11 || i==76?(ushort)WallID.Wood:(ushort)WallID.Stone}).ToArray();
            int endOrdinal = (int)Call(host, "BuildMemberOrdinal", cells.Length);
            var instance=Place(context,host,input,Asset(host,cells),43,36);
            Place(context,host,input,Asset(host,new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.Stone}),52,42);
            WorldGen.PlaceWall(53,43,WallID.Wood,true);
            WorldGen.PlaceWall(42,37,WallID.Wood,true);
            Require(Main.tile[53,43].wall==WallID.Wood && Main.tile[42,37].wall==WallID.Wood,"both separated wall endpoints have legal native supports");
            SetAutomatic(host,input,true);
            int updates=0,maxNear=0,maxForward=0,maxSources=0,boundsChanges=0;bool right=true;
            object priorBounds=null;float startX=p.position.X,minX=startX,maxX=startX;bool moved=false;
            var preferredWalks = new Dictionary<object, PreferredWalkObservation>();
            bool preferredPending = false, preferredResumed = false; int maxPreferred = 0;
            try{NativeQuickItemChecks.Until(()=>
            {
                NativeToolExecutionChecks.Sample(context,input,new Vector2(480,540),false);
                // Sample initializes neutral keyboard state. Apply real native
                // movement AFTER it, and feed both original controls/triggers.
                if(moving)
                {
                    // Alternate real controls across an interior tile edge.
                    // Staying near vanilla's 640px world clamp could stop all
                    // motion at the left limit and never change broad bounds.
                    right=p.position.X<48*16;
                    p.controlLeft=!right;p.controlRight=right;
                    PlayerInput.Triggers.Current.Left=!right;PlayerInput.Triggers.Current.Right=right;
                }
                long before=(long)Get(host,"ProjectionMatchSourceSteps");
                NativeQuickItemChecks.BeginWorldStep();p.Update(0);Call(context,"UpdateRuntime");updates++;
                moved|=p.position.X!=startX;minX=Math.Min(minX,p.position.X);maxX=Math.Max(maxX,p.position.X);
                Require(p.IsInTileInteractionRange(53,42,TileReachCheckSettings.Simple,p.inventory[12].tileBoost+p.blockRange),
                    "the late target stays inside actual native reach during the complete moving walk");
                Require((long)Get(host,"ProjectionMatchSourceSteps")-before<=256 && (int)Get(host,"materialBuildSourceBudget")>=0,
                    "local, forward and match still share the original per-update source budget");
                var sources=((IEnumerable)Get(host,"buildSources")).Cast<object>().ToArray();maxSources=Math.Max(maxSources,sources.Length);
                ObservePreferredContinuation(host, EligibilityRanges(instance), preferredWalks, ref preferredPending, ref preferredResumed, ref maxPreferred);
                foreach(var source in sources.Where(s=>ReferenceEquals(Get(s,"Owner"),instance)))
                {
                    var value=Get(Get(source,"Range"),"Materials");int cursor=(int)Get(value,"CandidateCursor");
                    maxNear=Math.Max(maxNear,(int)Get(source,"NearCursor"));maxForward=Math.Max(maxForward,cursor);
                    var bounds=Get(source,"NearBounds");if(priorBounds!=null && !priorBounds.Equals(bounds))boundsChanges++;priorBounds=bounds;
                    Require(!(bool)Get(value,"BuildExhausted") || cursor>=endOrdinal,
                        "a finite local/forward slice is not mistaken for source exhaustion");
                }
                NativeToolExecutionChecks.Outer(context,input,1+updates%2);
                return Main.tile[43,37].wall==WallID.Wood && Main.tile[53,42].wall==WallID.Wood && Main.tile[52,42].active() && ProgressDormant(instance) && !(bool)Get(Get(tools,"Use"),"Active") &&
                    maxNear>128 && maxForward>0 && (!moving || boundsChanges>0);
            });}
            catch
            {
                Console.Error.WriteLine("EXECUTION SEARCH state moving="+moving+" updates="+updates+" position="+p.position+" velocity="+p.velocity+
                    " maxNear="+maxNear+" maxForward="+maxForward+" boundsChanges="+boundsChanges+" maxSources="+maxSources+" moved="+moved+
                    " woodWall="+Main.tile[53,42].wall+" stone="+Main.tile[52,42].active()+" dormant="+ProgressDormant(instance)+" use="+Get(Get(tools,"Use"),"Active")+
                    " minX="+minX+" maxX="+maxX+" stocks="+p.inventory[12].stack+"/"+p.inventory[14].stack);
                foreach(var source in ((IEnumerable)Get(host,"buildSources")).Cast<object>())Console.Error.WriteLine("EXECUTION SEARCH source near="+Get(source,"NearCursor")+
                    " nearComplete="+Get(source,"NearComplete")+" revisit="+Get(source,"NearRevisit")+" bounds="+Get(source,"NearBounds")+
                    " forward="+Get(Get(Get(source,"Range"),"Materials"),"CandidateCursor")+" exhausted="+Get(Get(Get(source,"Range"),"Materials"),"BuildExhausted"));
                throw;
            }
            Require(p.inventory[12].stack==2 && p.inventory[14].stack==1 && maxNear>128 && maxForward>0 && maxSources>=2 &&
                preferredPending && preferredResumed && maxPreferred>128 &&
                (!moving || moved && boundsChanges>0),
                "the preferred tail resumes across real Updates without cursor rewind, while both wall endpoints and another source really consume under stationary/continuous movement");
            p.controlLeft=p.controlRight=false;
            for(int i=0;i<40;i++)Step(context,input);
            long settled=(long)Get(host,"ProjectionMatchSourceSteps");
            for(int i=0;i<20;i++)Step(context,input);
            Require(settled==(long)Get(host,"ProjectionMatchSourceSteps"),"completed near/forward passes sleep while their real signals remain unchanged");
            p.inventory[13].SetDefaults(ItemID.StoneWall);p.inventory[13].stack=1;
            Until(context,input,()=>p.inventory[13].IsAir);
            Require(cells.Where(c=>c.WallType==WallID.Stone).Any(c=>Main.tile[43+c.X,36+c.Y].wall==WallID.Stone) && Main.tile[43,37].wall==WallID.Wood && Main.tile[53,42].wall==WallID.Wood &&
                p.inventory[12].stack==2 && p.inventory[14].stack==1 && (long)Get(host,"BuildUnknownResults")==0,
                "a later real refill revisits the already passed prefix without duplicate spending or an automatic toggle");
            Console.WriteLine("PASS BlueprintExecutionSearch moving="+moving+" records=77 missingStone=75 betweenWood=64 updates="+updates+" maxNear="+maxNear+
                " maxForward="+maxForward+" retainedSources="+maxSources+" boundsChanges="+boundsChanges+" minX="+minX+" maxX="+maxX+
                " maxPreferred="+maxPreferred+" preferredBudgetPaused="+preferredPending+" preferredResumed="+preferredResumed+
                " nativeWoodWall=2 nativeStone=1 refillStoneWall=1 stablePassSleeping=true");
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
                return (shared ? Main.tile[76, 42].active() : Enumerable.Range(44, 3).All(x => Main.tile[x, 42].active())) && ProgressDormant(instance) &&
                    (smaller == null || ProgressDormant(smaller)) && !(bool)Get(Get(tools, "Use"), "Active");
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
            WorldGen.PlaceTile(44, 41, TileID.Stone, true, true); WorldGen.PlaceTile(45, 41, TileID.Stone, true, true);
            var stone = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone });
            Place(context, host, input, stone, 44, 42);
            foreach (int x in new[] { 42, 43, 45 }) Place(context, host, input, stone, x, 42);
            Place(context, host, input, stone, 44, 40); Place(context, host, input, stone, 45, 40);
            SetAutomatic(host, input, true);
            NativeQuickItemChecks.Until(() =>
            {
                NativeQuickItemChecks.Sample(input, Array.Empty<Microsoft.Xna.Framework.Input.Keys>()); NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime");
                return GetOptional(host, "preparedBuild") != null && (int)Get(Get(host, "buildSources"), "Count") == 4;
            });
            var pinned = ((IEnumerable)Get(host, "buildSources")).Cast<object>().Select(v => Get(v, "Range")).ToArray();
            Require(Scene(host).Length == 6 && p.inventory[12].stack == 8 && pinned.Length == 4 &&
                pinned.All(r => GetOptional(r, "Materials") != null && !(bool)Get(Get(r, "Materials"), "BuildExhausted")),
                "four unfinished source parts fill the cache while six supported same-material instances are already admitted; at least two available instances are outside that cache");
            // The first flight is already bound to12. Publishing an earlier
            // twin cannot turn cache replacement into a different Item source.
            p.inventory[11].SetDefaults(ItemID.StoneBlock); p.inventory[11].stack = 3; var earlier = p.inventory[11];
            long requests = (long)Get(host, "ProjectionMatchSourceRequests"), steps = (long)Get(host, "ProjectionMatchSourceSteps");
            var initiallyRetained = pinned.Select(r => Get(r, "Owner")).ToArray();
            var patch = new Harmony("JueMingR.Tests.BlueprintFullSourceContinuation");
            patch.Patch(typeof(Player).GetMethod("PlaceThing_Tiles_PlaceIt", F), postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(ContinuousPlaced)));
            continuousPlacements.Clear(); continuousFrame = 0; continuousHost = host; continuousUse = Get(tools, "Use");
            continuousAutomatic = continuousWatch = true;
            long previousSteps = steps, previousChecks = (long)Get(host, "BuildCandidateChecks");
            try
            {
                Until(context, input, () => {
                    continuousFrame++;
                    long currentSteps = (long)Get(host, "ProjectionMatchSourceSteps"), currentChecks = (long)Get(host, "BuildCandidateChecks");
                    Require((int)Get(Get(host, "buildSources"), "Count") <= 4 && (int)Get(host, "materialBuildSourceBudget") >= 0 &&
                        currentSteps - previousSteps <= 256 && currentChecks - previousChecks <= 64,
                        "six-instance same-group discovery and execution keep the shared per-Update budgets and four retained transports");
                    previousSteps = currentSteps; previousChecks = currentChecks;
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
                        Scene(host).SelectMany(EligibilityRanges).All(r => !(bool)Get(Get(r, "Materials"), "BuildExhausted")),
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
                    bool earlier = false, debtAcrossWrites = false, preparedDebtCurrent = false;
                    object seed = null;
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
                        var pendingSeed = GetOptional(host, "deferredBuild"); var prepared = GetOptional(host, "preparedBuild");
                        if (pendingSeed != null && seed == null) seed = pendingSeed;
                        if (!blocked && continuousPlacements.Count >= 2 && continuousPlacements.All(v => v.Type == ItemID.DirtBlock) && pendingSeed != null)
                        {
                            Require(ReferenceEquals(seed, pendingSeed), "the earliest ordinary debt survives multiple real same-source writes while its world proof becomes stale");
                            debtAcrossWrites = true;
                        }
                        if (pendingSeed != null && prepared != null && (int)Get(Get(prepared, "Target"), "WorldX") == 45)
                        {
                            Require((bool)Call(host, "IsBuildTargetCurrent", Get(prepared, "Target")), "ordinary debt is prepared with a newly current world/support proof");
                            preparedDebtCurrent = true;
                        }
                        if (!blocked && continuousPlacements.Count == 1)
                        {
                            var reach = (IDictionary<int, int>)Get(host, "buildItemReach"); int actualBoost;
                            Require(reach.TryGetValue(ItemID.DirtBlock, out actualBoost) && actualBoost == p.inventory[12].tileBoost && (int)Get(host, "buildEnvelopeBoost") == 1,
                                "one capability observation retains the exact active source reach while the earlier stack only expands the broad envelope");
                        }
                        // Empty outer callbacks preserve one debt and do no
                        // new preparation, preference scanning or native spend.
                        int count = continuousPlacements.Count; object kept = GetOptional(host, "deferredBuild");
                        var keptGroup = GetOptional(host, "buildContinuation");
                        long outerChecks = (long)Get(host, "BuildCandidateChecks");
                        var cursorOwners = GetOptional(instance, "Match") == null ? Array.Empty<object>() :
                            EligibilityRanges(instance).Select(r => GetOptional(r, "Materials")).Where(v => v != null).ToArray();
                        var preferenceCursors = cursorOwners.Select(v => (int)Get(v, "PreferredCursor")).ToArray();
                        NativeToolExecutionChecks.Outer(context, input, 2);
                        Require(continuousPlacements.Count == count && ReferenceEquals(kept, GetOptional(host, "deferredBuild")) &&
                            ReferenceEquals(keptGroup, GetOptional(host, "buildContinuation")) &&
                            outerChecks == (long)Get(host, "BuildCandidateChecks") && cursorOwners.Select((v, i) => (int)Get(v, "PreferredCursor") == preferenceCursors[i]).All(v => v),
                            "empty outer does not reset fairness or spend another preparation allowance");
                    }
                    continuousWatch = false;
                    Require(continuousPlacements.Count >= 2 && continuousPlacements[0].X == 44 && continuousPlacements[0].Type == ItemID.DirtBlock,
                        "mixed sequence starts by the ordinary real Dirt placement");
                    if (blocked)
                    {
                        Require(continuousPlacements[1].X == 45 && p.inventory[13].stack == 1 && GetOptional(host, "deferredBuild") == null,
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
                                " group/batch/remaining/debt/pending=" + (group == null ? "none" : Get(group, "Group") + "/" + Get(group, "Batch") + "/" +
                                    Get(group, "Remaining") + "/" + Get(group, "DebtDue") + "/" + Get(group, "Pending")) +
                                " dirt/earlier/stone=" + dirt.stack + "/" + p.inventory[11].stack + "/" + p.inventory[13].stack);
                        }
                        Require(continuous,
                            "the finite ready local group continues beyond one overtake on the exact stack/lease, then really serves ordinary Stone");
                        Require(seed != null && debtAcrossWrites && preparedDebtCurrent && GetOptional(host, "deferredBuild") == null &&
                            p.inventory[11].stack == 3 && ReferenceEquals(p.inventory[12], dirt) && dirt.stack == 5 &&
                            p.inventory[11].stack + p.inventory[12].stack == 8 && p.inventory[13].stack == 1,
                            "one debt survives actual world invalidations, rebinds current support and clears on native admission; an earlier same-type Item never steals the ongoing source");
                        Until(context, input, () => Scene(host).Length == 0);
                    }
                    Console.WriteLine("PASS BlueprintBuild material continuity blocked=" + blocked + " placements=" +
                        string.Join(",", continuousPlacements.Select(v => v.X + ":" + v.Type + "@" + v.Slot + "#" + v.Token)) +
                        " debtAcrossWrites=" + debtAcrossWrites + " reboundCurrentDebt=" + preparedDebtCurrent + " originalBudgets=true");
                    SetAutomatic(host, input, false);
                    Require(GetOptional(host, "deferredBuild") == null && (int)Get(Get(host, "buildSources"), "Count") == 0,
                        "turning construction off releases preference transports and its sole deferred description");
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
                    "the unrelated ready paint candidate cannot start supply preparation or spend paint while this finite body group still has successors");
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
