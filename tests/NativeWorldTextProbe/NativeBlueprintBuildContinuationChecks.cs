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
        private static void ExecutionSearchFairness(object context,object host,object tools,object input,bool moving)
        {
            var p=Reset(context,host,tools,input);p.position=new Vector2(48*16,646);
            p.inventory[12].SetDefaults(ItemID.WoodWall);p.inventory[12].stack=4;
            p.inventory[14].SetDefaults(ItemID.StoneBlock);p.inventory[14].stack=2;
            // All 77 wall records fit vanilla reach. The last WoodWall is
            // behind 76 missing StoneWall records: beyond even a whole 64-step
            // budget AFTER wall-only ordinal holes have been removed.
            var cells=Enumerable.Range(0,77).Select(i=>new CaptureCell{X=i%11,Y=i/11,Layers=CaptureLayer.Wall,
                WallType=i==76?(ushort)WallID.Wood:(ushort)WallID.Stone}).ToArray();
            var instance=Place(context,host,input,Asset(host,cells),43,36);
            Place(context,host,input,Asset(host,new CaptureCell{Layers=CaptureLayer.Tile,TileType=TileID.Stone}),52,42);
            WorldGen.PlaceWall(53,43,WallID.Wood,true);
            Require(Main.tile[53,43].wall==WallID.Wood,"late wall has an existing legal native support");
            SetAutomatic(host,input,true);
            int updates=0,maxNear=0,maxForward=0,maxSources=0,boundsChanges=0;bool right=true;
            object priorBounds=null;float startX=p.position.X,minX=startX,maxX=startX;bool moved=false;
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
                foreach(var source in sources.Where(s=>ReferenceEquals(Get(s,"Owner"),instance)))
                {
                    var value=Get(Get(source,"Range"),"Materials");int cursor=(int)Get(value,"CandidateCursor");
                    maxNear=Math.Max(maxNear,(int)Get(source,"NearCursor"));maxForward=Math.Max(maxForward,cursor);
                    var bounds=Get(source,"NearBounds");if(priorBounds!=null && !priorBounds.Equals(bounds))boundsChanges++;priorBounds=bounds;
                    Require(!(bool)Get(value,"BuildExhausted") || cursor>=cells.Length*2,
                        "a finite local/forward slice is not mistaken for source exhaustion");
                }
                NativeToolExecutionChecks.Outer(context,input,1+updates%2);
                return Main.tile[53,42].wall==WallID.Wood && Main.tile[52,42].active() && ProgressDormant(instance) && !(bool)Get(Get(tools,"Use"),"Active") &&
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
            Require(p.inventory[12].stack==3 && p.inventory[14].stack==1 && maxNear>128 && maxForward>0 && maxSources>=2 &&
                (!moving || moved && boundsChanges>0),
                "beyond-capacity local tail and another retained source both really build under stationary/continuous native movement");
            p.controlLeft=p.controlRight=false;
            for(int i=0;i<40;i++)Step(context,input);
            long settled=(long)Get(host,"ProjectionMatchSourceSteps");
            for(int i=0;i<20;i++)Step(context,input);
            Require(settled==(long)Get(host,"ProjectionMatchSourceSteps"),"completed near/forward passes sleep while their real signals remain unchanged");
            p.inventory[13].SetDefaults(ItemID.StoneWall);p.inventory[13].stack=1;
            Until(context,input,()=>p.inventory[13].IsAir);
            Require(cells.Take(76).Any(c=>Main.tile[43+c.X,36+c.Y].wall==WallID.Stone) && Main.tile[53,42].wall==WallID.Wood &&
                p.inventory[12].stack==3 && p.inventory[14].stack==1 && (long)Get(host,"BuildUnknownResults")==0,
                "a later real refill revisits the already passed prefix without duplicate spending or an automatic toggle");
            Console.WriteLine("PASS BlueprintExecutionSearch moving="+moving+" records=77 missingPrefix=76 updates="+updates+" maxNear="+maxNear+
                " maxForward="+maxForward+" retainedSources="+maxSources+" boundsChanges="+boundsChanges+" minX="+minX+" maxX="+maxX+
                " nativeWoodWall=1 nativeStone=1 refillStoneWall=1 stablePassSleeping=true");
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
            internal int Frame, Animation, X, Slot, Type;
            internal long Token, Native, Flight;
        }
        private static void BuildSourceFullContinuation(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 8;
            WorldGen.PlaceTile(44, 41, TileID.Stone, true, true); WorldGen.PlaceTile(45, 41, TileID.Stone, true, true);
            var stone = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone });
            var dirt = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Dirt });
            Place(context, host, input, stone, 44, 42);
            foreach (int x in new[] { 42, 43, 45 }) Place(context, host, input, dirt, x, 42);
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
                "four unfinished source parts really fill the cache before the six-instance construction can consume");
            long requests = (long)Get(host, "ProjectionMatchSourceRequests"), steps = (long)Get(host, "ProjectionMatchSourceSteps");
            Until(context, input, () => Scene(host).Length == 3 && Main.tile[44, 42].active() && Main.tile[44, 40].active() && Main.tile[45, 40].active() &&
                Scene(host).All(ProgressDormant) && !(bool)Get(Get(tools, "Use"), "Active"));
            Require(p.inventory[12].stack == 5 && new[] { 42, 43, 45 }.All(x => !Main.tile[x, 42].active()) &&
                (long)Get(host, "ProjectionMatchSourceRequests") - requests <= 12 && (long)Get(host, "ProjectionMatchSourceSteps") - steps <= 128 &&
                (int)Get(Get(host, "buildSources"), "Count") <= 4,
                "finishing pinned walks releases capacity for later real sources; all available goals consume once, absent material stays absent and source work remains finite");
            Console.WriteLine("PASS BlueprintBuild full source continuation pinned=4 instances=6 actualStone=3 missingDirt=3 sourceRequests=" +
                ((long)Get(host, "ProjectionMatchSourceRequests") - requests) + " sourceSteps=" + ((long)Get(host, "ProjectionMatchSourceSteps") - steps));
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
        private static bool continuousWatch, continuousAutomatic, staleCheckIssued;
        private static int continuousFrame;
        private static void ContinuousPlaced(Player __instance)
        {
            if (!continuousWatch || !ReferenceEquals(__instance, Main.LocalPlayer) || !Main.tile[Player.tileTargetX, Player.tileTargetY].active()) return;
            var flight = continuousAutomatic ? GetOptional(continuousHost, "activeBuild") : null;
            continuousPlacements.Add(new ContinuousPlacement { Frame = continuousFrame, Animation = __instance.itemAnimation, X = Player.tileTargetX,
                Token = continuousAutomatic ? (long)Get(continuousUse, "Operation") : 0, Native = continuousAutomatic ? (long)Get(continuousUse, "NativeGeneration") : 0,
                Flight = flight == null ? 0 : (long)Get(flight, "Generation"),
                Slot = flight == null ? __instance.selectedItem : (int)Get(Get(flight, "Source"), "Slot"),
                Type = flight == null ? __instance.HeldItem.type : (int)Get(Get(flight, "Source"), "Type") });
        }
        private static void MaterialContinuityAndYield(object context, object host, object tools, object input)
        {
            var patch = new Harmony("JueMingR.Tests.BlueprintMaterialContinuity");
            patch.Patch(typeof(Player).GetMethod("PlaceThing_Tiles_PlaceIt", F), postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(ContinuousPlaced)));
            continuousHost = host; continuousUse = Get(tools, "Use"); continuousAutomatic = true;
            try
            {
                foreach (bool blocked in new[] { false, true })
                {
                    var p = Reset(context, host, tools, input); p.position = new Vector2(42 * 16, 646);
                    p.inventory[12].SetDefaults(ItemID.DirtBlock); p.inventory[12].stack = 8;
                    p.inventory[13].SetDefaults(ItemID.StoneBlock); p.inventory[13].stack = 2;
                    var cells = Enumerable.Range(0, 4).Select(x => new CaptureCell { X = x, Layers = CaptureLayer.Tile,
                        TileType = x == 1 ? TileID.Stone : TileID.Dirt }).ToArray();
                    var instance = Place(context, host, input, Asset(host, cells), 44, 42);
                    if (blocked) foreach (int x in new[] { 46, 47 }) Require(WorldGen.PlaceTile(x, 42, TileID.Stone, true, true), "preference obstacles are real original world bodies");
                    continuousPlacements.Clear(); continuousWatch = true; SetAutomatic(host, input, true);
                    bool earlier = false, preparedDebt = false, invalidatedDebt = false, reboundDebt = false;
                    object seed = null, debtTarget = null;
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
                        var before = GetOptional(host, "preparedBuild");
                        if (!blocked && continuousPlacements.Count == 2 && before != null && (int)Get(Get(before, "Target"), "WorldX") == 45 &&
                            !(bool)Call(host, "IsBuildTargetCurrent", Get(before, "Target")))
                        {
                            invalidatedDebt = true;
                            Require(ReferenceEquals(seed, GetOptional(host, "deferredBuild")), "actual preferred write invalidates the prepared fallback without forgiving its service debt");
                        }
                        Call(context, "UpdateRuntime");
                        Require((long)Get(host, "ProjectionMatchSourceSteps") - steps <= 256 && (int)Get(host, "materialBuildSourceBudget") >= 0 &&
                            (long)Get(host, "BuildCandidateChecks") - checks <= 64,
                            "ordinary, preference and deferred work share the original source and full-preflight allowances");
                        var pendingSeed = GetOptional(host, "deferredBuild"); var prepared = GetOptional(host, "preparedBuild");
                        if (pendingSeed != null && seed == null) seed = pendingSeed;
                        if (pendingSeed != null && prepared != null && (int)Get(Get(prepared, "Target"), "WorldX") == 45)
                        {
                            preparedDebt = true;
                            if (invalidatedDebt && debtTarget != null && !ReferenceEquals(debtTarget, Get(prepared, "Target"))) reboundDebt = true;
                            debtTarget = Get(prepared, "Target");
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
                        long outerChecks = (long)Get(host, "BuildCandidateChecks");
                        var cursorOwners = GetOptional(instance, "Match") == null ? Array.Empty<object>() :
                            EligibilityRanges(instance).Select(r => GetOptional(r, "Materials")).Where(v => v != null).ToArray();
                        var preferenceCursors = cursorOwners.Select(v => (int)Get(v, "PreferredCursor")).ToArray();
                        NativeToolExecutionChecks.Outer(context, input, 2);
                        Require(continuousPlacements.Count == count && ReferenceEquals(kept, GetOptional(host, "deferredBuild")) &&
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
                        Require(continuousPlacements.Count == 4 && continuousPlacements[1].Type == ItemID.DirtBlock && continuousPlacements[1].Slot == 12 &&
                            continuousPlacements[0].Token == continuousPlacements[1].Token && continuousPlacements[0].Flight != continuousPlacements[1].Flight &&
                            continuousPlacements[2].X == 45 && continuousPlacements[2].Type == ItemID.StoneBlock && continuousPlacements[2].Token != continuousPlacements[1].Token,
                            "one active-source overtake really continues the same exact stack/lease, then the ordinary Stone target receives finite native service");
                        Require(seed != null && preparedDebt && invalidatedDebt && reboundDebt && GetOptional(host, "deferredBuild") == null &&
                            p.inventory[11].stack + p.inventory[12].stack == 8 && p.inventory[13].stack == 1,
                            "the single debt survives a stale prepared proof, rebinds current support and is cleared by actual admission with exact real consumption");
                        Until(context, input, () => Scene(host).Length == 0);
                    }
                    Console.WriteLine("PASS BlueprintBuild material continuity blocked=" + blocked + " placements=" +
                        string.Join(",", continuousPlacements.Select(v => v.X + ":" + v.Type + "@" + v.Slot + "#" + v.Token)) +
                        " preparedDebt=" + preparedDebt + " staleDebt=" + invalidatedDebt + " reboundDebt=" + reboundDebt + " originalBudgets=true");
                    SetAutomatic(host, input, false);
                    Require(GetOptional(host, "deferredBuild") == null && (int)Get(Get(host, "buildSources"), "Count") == 0,
                        "turning construction off releases preference transports and its sole deferred description");
                }
            }
            finally
            {
                continuousWatch = false; continuousHost = continuousUse = null;
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                SetAutomatic(host, input, false);
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
                for (continuousFrame = 0; continuousFrame < 100 && continuousPlacements.Count < 2; continuousFrame++)
                {
                    int x = 44 + continuousPlacements.Count;
                    NativeToolExecutionChecks.Sample(context, input, new Vector2(x * 16 + 8, 42 * 16 + 8), true);
                    NativeQuickItemChecks.BeginWorldStep(); p.Update(0); Call(context, "UpdateRuntime");
                }
                continuousWatch = false;
                Require(continuousPlacements.Count == 2 && p.inventory[12].stack == 4, "independent original held input actually places and consumes two Dirt blocks");
                int reference = continuousPlacements[1].Frame - continuousPlacements[0].Frame;
                Require(reference > 0 && continuousPlacements[1].Animation > 0, "the locked original item exposes a second placement inside its first animation");

                p = Reset(context, host, tools, input); p.position = new Vector2(42 * 16, 646);
                p.inventory[12].SetDefaults(ItemID.DirtBlock); p.inventory[12].stack = 6;
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
                Require(second.Frame - first.Frame == reference && second.Animation > 0 && first.Token > 0 && first.Token == second.Token && first.Flight != second.Flight && first.Native != second.Native,
                    "same-source Refresh reaches the same original held-input opportunity inside one animation, with distinct target and native-consumer generations");
                Console.WriteLine("PASS BlueprintBuild legal continuous Dirt referenceInterval=" + reference + " automaticInterval=" + (second.Frame - first.Frame) +
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
                    continuousPlacements[1].Frame - continuousPlacements[0].Frame == reference && continuousPlacements[0].Token == continuousPlacements[1].Token,
                    "natural retirement of the checked instance keeps the same legal held-input opportunity for another qualified instance, without retaining old completion proof");
                Console.WriteLine("PASS BlueprintBuild continuous instance retirement instances=4 actualConsumed=4 referenceInterval=" + reference + " sameLease=true");
                foreach (bool different in new[] { false, true })
                {
                    p = Reset(context, host, tools, input); p.position = new Vector2(42 * 16, 646);
                    p.inventory[12].SetDefaults(ItemID.DirtBlock); p.inventory[12].stack = 2;
                    p.inventory[13].SetDefaults(different ? ItemID.StoneBlock : ItemID.DirtBlock); p.inventory[13].stack = 4;
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
