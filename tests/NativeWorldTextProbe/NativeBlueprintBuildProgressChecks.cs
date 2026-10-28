using System;
using System.Linq;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.Items;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static bool ProgressDormant(object item)
        {
            return GetOptional(item, "Match") != null && EligibilityRanges(item).All(range =>
            {
                var value = GetOptional(range, "Materials");
                return (bool)Get(range, "Current") && !(bool)Get(range, "Demand") && !(bool)Get(range, "Reading") &&
                    value != null && (bool)Get(value, "Current") && (bool)Get(value, "BuildExhausted");
            });
        }
        private static void ProgressStep(object context, object host, object input, Player p, int update, bool move, bool unrelated)
        {
            // Fixture movement crosses a tile boundary on every actual player
            // update, while the later stone remains inside the original range.
            if (move) { p.position = new Vector2(640 + update % 2 * 16, 646); p.velocity = Vector2.Zero; }
            if (unrelated) { p.inventory[20].SetDefaults(ItemID.Wood); p.inventory[20].stack = 1 + update % 2; }
            int beforeX = (int)p.position.X;
            long reads = (long)Get(host, "ProjectionMatchCellReads"), source = (long)Get(host, "ProjectionMatchSourceSteps");
            long checks = (long)Get(host, "BuildCandidateChecks"), requests = (long)Get(host, "ProjectionMatchSourceRequests");
            Step(context, input);
            Require((long)Get(host, "ProjectionMatchCellReads") - reads <= 256 && (long)Get(host, "ProjectionMatchSourceSteps") - source <= 256 &&
                (long)Get(host, "BuildCandidateChecks") - checks <= 64 && (long)Get(host, "ProjectionMatchSourceRequests") - requests <= 4,
                "continuously changing qualifications share the original world/source/search/admission budgets");
            if (move)
                Require((int)p.position.X == beforeX && p.IsInTileInteractionRange(44, 42, TileReachCheckSettings.Simple, p.blockRange),
                    "every real Player.Update retains the alternating integer-pixel position and legal stone reach");
        }
        private static void ContinuousCandidateProgress(object context, object host, object tools, object input, bool move, bool unrelated, bool earlySupply)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 5;
            var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Dirt },
                new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = TileID.Stone });
            var instance = Place(context, host, input, entry, 42, 42);
            long firstCheck = (long)Get(host, "BuildCandidateChecks"), firstUses = (long)Get(host, "BuildUsesStarted");
            SetAutomatic(host, input, true);
            int updates = 0, stoneAt = -1, dirtAt = -1; bool supplied = false;
            NativeQuickItemChecks.Until(() =>
            {
                // Continuous movement is the stone's counterexample. Once it
                // succeeds, stand clear of the earlier dirt footprint: crossing
                // that newly built body would add an unrelated native collision.
                if (earlySupply && stoneAt >= 0) { p.position = new Vector2(640, 646); p.velocity = Vector2.Zero; }
                ProgressStep(context, host, input, p, updates++, move && (!earlySupply || stoneAt < 0), unrelated);
                if (earlySupply && !supplied && (long)Get(host, "BuildCandidateChecks") > firstCheck && !Main.tile[44, 42].active())
                {
                    // The first missing item has actually yielded. This is a
                    // resource input, never a refund or a destination write.
                    p.inventory[13].SetDefaults(ItemID.DirtBlock); p.inventory[13].stack = 1; supplied = true;
                }
                if (stoneAt < 0 && Main.tile[44, 42].active()) stoneAt = updates;
                if (dirtAt < 0 && Main.tile[42, 42].active()) dirtAt = updates;
                return stoneAt >= 0 && (earlySupply ? Scene(host).Length == 0 : ProgressDormant(instance) && !(bool)Get(Get(tools, "Use"), "Active"));
            });
            Require(updates > 1 && stoneAt > 0 && Main.tile[44, 42].type == TileID.Stone && p.inventory[12].stack == 4,
                "continuous move/inventory changes cannot starve the later real stone placement or create extra material consumption");
            if (earlySupply)
            {
                Require(supplied && dirtAt > stoneAt && p.inventory[13].IsAir && Scene(host).Length == 0,
                    "material restored before the walk tail is latched: stone keeps its turn, then dirt spends once and full Match retires naturally");
            }
            else
            {
                Require(!Main.tile[42, 42].active() && Scene(host).Length == 1 && (long)Get(host, "BuildUsesStarted") == firstUses + 1,
                    "the missing dirt remains missing while only the independently supplied stone is consumed");
                long reads = (long)Get(host, "ProjectionMatchCellReads"), requests = (long)Get(host, "ProjectionMatchSourceRequests"), checks = (long)Get(host, "BuildCandidateChecks");
                for (int i = 0; i < 80; i++) ProgressStep(context, host, input, p, updates++, move, unrelated);
                Require(reads == (long)Get(host, "ProjectionMatchCellReads") && requests == (long)Get(host, "ProjectionMatchSourceRequests") && checks == (long)Get(host, "BuildCandidateChecks"),
                    "a pure material wait adds no source/world/candidate work under ongoing movement and unrelated inventory changes");
                p.position = new Vector2(640, 646); p.velocity = Vector2.Zero; Step(context, input);
                p.inventory[13].SetDefaults(ItemID.DirtBlock); p.inventory[13].stack = 1;
                Until(context, input, () => Scene(host).Length == 0);
                Require(Main.tile[42, 42].type == TileID.Dirt && p.inventory[13].IsAir && p.inventory[12].stack == 4,
                    "a static refill after exhaustion resumes without editing, opening materials or toggling, and retires through full Match");
            }
            Console.WriteLine("PASS BlueprintBuild changing qualifications move=" + move + " unrelatedInventory=" + unrelated + " earlySupply=" + earlySupply +
                " actualUpdates=" + updates + " firstStoneUpdate=" + stoneAt + " firstDirtUpdate=" + dirtAt + " stoneConsumed=1");
        }
        private static void RefillBeforeMissingTail(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input);
            // Missing supply is now rejected before a native candidate. A real
            // finite tail longer than the unchanged 64-step slice exposes that
            // earlier wait without depending on the removed preflight counter.
            var cells=Enumerable.Range(0,300).Select(i=>new CaptureCell{X=(i==0?0:i+1)%30,Y=(i==0?0:i+1)/30*2,
                Layers=CaptureLayer.Tile,TileType=i==0?TileID.Dirt:TileID.Stone}).ToArray();
            var entry = Asset(host,cells);
            var instance = Place(context, host, input, entry, 42, 42);
            Require(42+cells.Max(cell=>cell.X)<Main.maxTilesX && 42+cells.Max(cell=>cell.Y)<Main.maxTilesY && cells.All(cell=>cell.Y!=1),
                "the complete finite missing tail fits the original 200-wide Build world and skips its existing y43 support row");
            long checks = (long)Get(host, "BuildCandidateChecks"), uses = (long)Get(host, "BuildUsesStarted");
            object material=null;int recoveryCursor=0;
            SetAutomatic(host, input, true); Until(context, input, () =>
            {
                if(GetOptional(instance,"Match")==null)return false;
                material=GetOptional(EligibilityRanges(instance).Single(),"Materials");if(material==null)return false;
                recoveryCursor=(int)Get(material,"CandidateCursor");
                return Get(material,"BuildWaits").ToString().Contains("Material") && !(bool)Get(material,"BuildExhausted") && recoveryCursor>2 && recoveryCursor<cells.Length*2;
            });
            Require(!Main.tile[42, 42].active() && !Main.tile[44, 42].active() && (long)Get(host,"BuildUsesStarted")==uses,
                "the real walk has observed early missing supply and still owns an unvisited tail, with no native use or world write");
            p.inventory[13].SetDefaults(ItemID.DirtBlock); p.inventory[13].stack = 1;
            Until(context, input, () => Main.tile[42, 42].active() && ProgressDormant(instance) && !(bool)Get(Get(tools, "Use"), "Active"));
            Require(!Main.tile[44, 42].active() && Main.tile[42, 42].type == TileID.Dirt && p.inventory[13].IsAir && Scene(host).Length == 1 &&
                (long)Get(host, "BuildUsesStarted") == uses + 1 && (long)Get(host, "BuildCandidateChecks") > checks,
                "an early relevant recovery survives the missing tail without any intervening world change; the next walk consumes the restored dirt exactly once");
            Console.WriteLine("PASS BlueprintBuild refill before all-missing tail: actualWorld="+Main.maxTilesX+"x"+Main.maxTilesY+" actualRecoveryCursor="+recoveryCursor+" tailOrdinals="+cells.Length*2+" no pre-recovery world/native write, original dirt consumption=1, stone remains missing");
        }
        private static void StableRangesIgnoreSupplyNoise(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input);
            var dirt = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Dirt });
            var stable = new[] { Place(context, host, input, dirt, 80, 42), Place(context, host, input, dirt, 90, 42) };
            p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 5;
            SetAutomatic(host, input, true); Until(context, input, () => stable.All(ProgressDormant));
            var states = stable.Select(item => Get(item, "Match")).ToArray();
            var reads = states.Select(state => (long)Get(state, "WorldReads")).ToArray();
            var requests = states.Select(state => (long)Get(state, "SourceRequests")).ToArray();
            long inventoryRevision = ((BlueprintMaterialInventory)Get(host, "MaterialInventory")).Revision;
            StableRangeSnapshot("baseline",host,p,states);
            var stone = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone });
            Place(context, host, input, stone, 44, 42);
            Until(context, input, () => Main.tile[44, 42].active() && !(bool)Get(Get(tools, "Use"), "Active"));
            Require(p.inventory[12].stack == 4, "the isolated real successful stone action spends one while unrelated dirt ranges stay intact");
            StableRangeSnapshot("first-stone",host,p,states);
            for (int i = 0; i < 100; i++)
            {
                // All observations retain a positive same-type source. This
                // exercises split, move and merge, as well as unrelated counts.
                if (i % 2 == 0) { p.inventory[13].SetDefaults(ItemID.StoneBlock); p.inventory[13].stack = 2; p.inventory[12].stack = 2; }
                else { p.inventory[12].stack = 4; p.inventory[13].TurnToAir(); }
                p.inventory[20].SetDefaults(ItemID.Wood); p.inventory[20].stack = i + 1; Step(context, input);
            }
            StableRangeSnapshot("100-inventory-noise",host,p,states);
            Place(context, host, input, Asset(host, new[] { 0, 1, 3, 4 }.Select(x => new CaptureCell { X = x, Layers = CaptureLayer.Tile, TileType = TileID.Stone }).ToArray()), 42, 42);
            Until(context, input, () => new[] { 42, 43, 45, 46 }.All(x => Main.tile[x, 42].active()) && !(bool)Get(Get(tools, "Use"), "Active"));
            StableRangeSnapshot("last-four-stones",host,p,states);
            Require(((BlueprintMaterialInventory)Get(host, "MaterialInventory")).Revision > inventoryRevision &&
                p.inventory[12].IsAir && p.inventory[13].IsAir &&
                Enumerable.Range(0, states.Length).All(i => (long)Get(states[i], "WorldReads") == reads[i] && (long)Get(states[i], "SourceRequests") == requests[i]),
                "normal spend through the last positive source, splits/merges/moves and unrelated changes update held inventory without re-reading stable unrelated world proofs");
            Console.WriteLine("PASS BlueprintBuild two unrelated stable ranges: all 5 original stones consumed including last 1->0, 100 split/merge/unrelated updates, extraWorldReads=0 extraSourceRequests=0");
        }
        private static void StableRangeSnapshot(string phase,object host,Player player,object[] states)
        {
            Console.WriteLine("STABLE RANGE phase="+phase+" inventoryRevision="+((BlueprintMaterialInventory)Get(host,"MaterialInventory")).Revision+
                " eligibilityRevision="+Get(host,"MaterialBuildEligibilityRevision")+" direction="+player.direction+
                " slots12/13="+player.inventory[12].type+":"+player.inventory[12].stack+":"+player.inventory[12].IsAir+"/"+player.inventory[13].type+":"+player.inventory[13].stack+":"+player.inventory[13].IsAir+
                " ranges="+string.Join(";",states.Select(state=>{var range=((Array)Get(state,"Ranges")).Cast<object>().Single();var material=Get(range,"Materials");
                    return Get(state,"OriginX")+":reads="+Get(state,"WorldReads")+",requests="+Get(state,"SourceRequests")+",waits="+Get(material,"BuildWaits")+",playerRevision="+Get(material,"BuildPlayerRevision");})));
        }
        private static void ProtectedSourceRecovery(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 2;
            var ownership = (ItemOperationOwnership)Get(Get(tools, "Items"), "Ownership");
            long session = ownership.Session, token = ownership.NewUseToken();
            Require(ownership.TryBeginCoins(session, 1UL << 12, token), "a separate real owner protects the exact construction material source");
            try
            {
                var item = Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone }), 44, 42);
                SetAutomatic(host, input, true); Until(context, input, () => ProgressDormant(item));
                Require(!Main.tile[44, 42].active() && p.inventory[12].stack == 2, "an exhausted protected source neither spends nor manufactures availability");
                Vector2 position = p.position; var source = p.inventory[12];
                ownership.EndCoins(session, token, false);
                Until(context, input, () => Scene(host).Length == 0);
                Require(p.position == position && ReferenceEquals(source, p.inventory[12]) && source.stack == 1 && Main.tile[44, 42].active(),
                    "the exact owner's release alone restores qualified supply and real construction without any stack, object or position change");
            }
            finally { ownership.EndCoins(session, token, false); }
        }
        private static void ProgressWhileSourceWaiting(object context, object host, object tools, object input)
        {
            var patch = new Harmony("JueMingR.Tests.BlueprintProgressSource");
            try
            {
                var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 3;
                var entry = Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Dirt },
                    new CaptureCell { X = 2, Layers = CaptureLayer.Tile, TileType = TileID.Stone });
                Place(context, host, input, entry, 42, 42);
                eligibilityWorker = Get(Get(host, "Library"), "worker"); eligibilitySourceEntered = new ManualResetEventSlim(); eligibilitySourceContinue = new ManualResetEventSlim();
                patch.Patch(AccessTools.Method(eligibilityWorker.GetType(), "ProjectionSourceStep"), prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(PauseEligibilitySource)));
                SetAutomatic(host, input, true); int updates = 0;
                NativeQuickItemChecks.Until(() => { ProgressStep(context, host, input, p, updates++, true, true); return eligibilitySourceEntered.IsSet; });
                long requests = (long)Get(host, "ProjectionMatchSourceRequests"), uses = (long)Get(host, "BuildUsesStarted"), reads = (long)Get(host, "ProjectionMatchCellReads");
                for (int i = 0; i < 300; i++) ProgressStep(context, host, input, p, updates++, true, true);
                Require(requests == (long)Get(host, "ProjectionMatchSourceRequests") && uses == (long)Get(host, "BuildUsesStarted") && reads == (long)Get(host, "ProjectionMatchCellReads"),
                    "300 distinct player/supply updates retain the outstanding source request without resubmitting, restarting or inventing native work");
                eligibilitySourceContinue.Set();
                NativeQuickItemChecks.Until(() => { ProgressStep(context, host, input, p, updates++, true, true); return Main.tile[44, 42].active(); });
                Require(!Main.tile[42, 42].active() && p.inventory[12].stack == 2, "the original source response continues to the later stone through real ItemCheck after continuous qualification changes");
                Console.WriteLine("PASS BlueprintBuild source waiting: blockedActualUpdates=300 duplicateRequests=0 nativeUses=0, original response later places stone");
            }
            finally
            {
                eligibilitySourceContinue?.Set();
                foreach (var method in patch.GetPatchedMethods().ToArray()) patch.Unpatch(method, HarmonyPatchType.All, patch.Id);
                eligibilityWorker = null; Call(host, "SetAutoBuild", false); Call(host, "ClearProjection");
            }
        }
        private static void AccessorySourceRecovery(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input);
            try
            {
                p.armor[3].SetDefaults(ItemID.PaintSprayer); p.builderAccStatus[3] = 0;
                p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 2;
                p.inventory[54].SetDefaults(ItemID.RedPaint); p.inventory[54].stack = 2;
                var item = Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone, TilePaint = 1 }), 44, 42);
                // inventoryChestStack would close HasLockedInventory globally,
                // before this local dependency gate. Instead begin one actual
                // manual swing: original active-material protection includes
                // the sprayer's paint while that native animation remains.
                NativeToolExecutionChecks.Sample(context, input, new Vector2(36 * 16, 38 * 16), true);
                NativeQuickItemChecks.BeginWorldStep(); p.Update(0); Call(context, "UpdateRuntime");
                Require(p.itemAnimation > 0 && p.autoPaint, "a real manual pickaxe swing supplies the native accessory protection interval");
                SetAutomatic(host, input, true);
                NativeQuickItemChecks.Until(() =>
                {
                    // Release real input, retaining the original animation
                    // while asynchronous source/material work becomes ready.
                    NativeToolExecutionChecks.Sample(context, input, new Vector2(480, 540), false);
                    NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime");
                    return ProgressDormant(item);
                });
                Require(p.itemAnimation > 0 && (bool)Call(tools, "ProtectedAfterYield", p.inventory[54]) &&
                    (bool)Call(host, "CanBuildAutomatically", p, true) && !Main.tile[44, 42].active() && p.inventory[12].stack == 2 && p.inventory[54].stack == 2,
                    "the real active-material guard exhausts this local paint dependency while global preparation stays admitted and spends nothing");
                Vector2 position = p.position;
                var primary = p.inventory.Take(50).Select(source => Tuple.Create(source.type, source.stack)).ToArray();
                // Only natural Player.Update is allowed to end this animation.
                // CopperPickaxe's own eligibility can recover, but that type is
                // unrelated to the range's StoneBlock/RedPaint demand.
                Until(context, input, () => GetOptional(host, "preparedBuild") != null);
                Require(p.position == position && p.inventory.Take(50).Select((source, slot) => source.type == primary[slot].Item1 && source.stack == primary[slot].Item2).All(equal => equal) &&
                    !Main.tile[44, 42].active() && p.inventory[54].stack == 2,
                    "natural accessory protection release resumes preparation with all primary item types/stacks and position unchanged");
                Until(context, input, () => Scene(host).Length == 0);
                Require(Main.tile[44, 42].active() && Main.tile[44, 42].color() == 1 && p.inventory[12].stack == 1 && p.inventory[54].stack == 1,
                    "the restored accessory source participates in the one real native build token and full painted Match retires naturally");
                Console.WriteLine("PASS BlueprintBuild accessory recovery: real manual swing/physical release/natural animation end, primary types/stacks unchanged, actual stone=1 paint=1 consumed");
            }
            finally { p.armor[3].TurnToAir(); }
        }
        private static void ImmediateReachAndAdmissionRecovery(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 2;
            Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone }), 44, 42);
            SetAutomatic(host, input, true); AwaitPrepared(context, host, input);
            long uses = (long)Get(host, "BuildUsesStarted"); p.position = new Vector2(60 * 16, 646); p.velocity = Vector2.Zero;
            Step(context, input);
            Require(!Main.tile[44, 42].active() && p.inventory[12].stack == 2 && uses == (long)Get(host, "BuildUsesStarted"),
                "the ready candidate cannot use yesterday's reach when the real next Player.Update is out of range");
            Until(context, input, () => ProgressDormant(Scene(host).Single()));
            Main.blockInput = true;
            try
            {
                p.position = new Vector2(640, 646); p.velocity = Vector2.Zero;
                for (int i = 0; i < 12; i++) Step(context, input);
                Require(!Main.tile[44, 42].active() && p.inventory[12].stack == 2, "global native admission keeps an in-range recovered target paused");
            }
            finally { Main.blockInput = false; }
            Vector2 recovered = p.position;
            Until(context, input, () => Scene(host).Length == 0);
            Require(p.position == recovered && p.inventory[12].stack == 1 && Main.tile[44, 42].active(),
                "admission restoration alone continues the exhausted range with current reach and true consumption");
        }
        private static void PulleyDirectionRecovery(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.WoodenChair); p.inventory[12].stack = 2;
            Require(WorldGen.PlaceObject(60, 42, TileID.Chairs, true, direction: -1), "the direction fixture captures a genuine original left chair");
            var entry = NativeBlueprintWorldMatchChecks.CaptureCompletionAsset(context, host, input, (LibraryState)Get(host, "Library"),
                () => Step(context, input), 60, 41, 1, 2, "滑轮方向资格");
            Place(context, host, input, entry, 44, 41);
            for (int y = 36; y <= 42; y++) Require(WorldGen.PlaceTile(41, y, TileID.Rope, true, true), "original rope supplies the actual pulley dependency");
            p.position = new Vector2(41 * 16 + 8 - p.width / 2, 646); p.velocity = Vector2.Zero;
            p.pulley = true; p.pulleyDir = 2; p.direction = 1;
            SetAutomatic(host, input, true); Until(context, input, () => ProgressDormant(Scene(host).Single()));
            Require(p.pulley && p.pulleyDir == 2 && !Main.tile[44, 41].active() && p.inventory[12].stack == 2,
                "original rope idle retains the opposing pulley direction and no automatic ChangeDir side effect or spend occurs");
            Vector2 position = p.position; p.direction = -1;
            AwaitPrepared(context, host, input);
            Require(p.position == position && p.inventory[12].stack == 2,
                "direction alone, before another original Player.Update, wakes the same exhausted rope range without moving or replenishing");
            // Locked .8 Player.Update's rope alignment (Player.cs:26651-26658)
            // uses center + 6*direction for pulleyDir 2. Its natural correction
            // after a direction change is distinct from ToolUse moving a player.
            float nativeX = (int)(position.X + p.width / 2) / 16 * 16 + 8 - p.width / 2 - 6;
            Until(context, input, () => Scene(host).Length == 0);
            Require(p.pulley && p.pulleyDir == 2 && p.position.X == nativeX && p.position.Y == position.Y && Main.tile[44, 41].active() && p.inventory[12].stack == 1,
                "original rope alignment and true chair consumption follow the direction-only recovery, without an automatic ChangeDir relocation");
            Console.WriteLine("PASS BlueprintBuild pulley direction alone resumes Host preparation; real Player.Update aligns X=" + position.X + "->" + nativeX + " and consumes one chair");
        }
        private static void SubpixelReachRecovery(object context, object host, object tools, object input)
        {
            var p = Reset(context, host, tools, input); p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 2;
            p.position = new Vector2(42 * 16 - p.width, 646); p.velocity = Vector2.Zero;
            Step(context, input);
            int left, top, right, bottom;
            TileReachCheckSettings.Simple.GetTileRegion(p, out left, out top, out right, out bottom, p.inventory[12].tileBoost + p.blockRange);
            int targetX = right + 1;
            Require(!p.IsInTileInteractionRange(targetX, 42, TileReachCheckSettings.Simple, p.inventory[12].tileBoost + p.blockRange),
                "the independent original reach consumer excludes the supported cell just beyond its exact integer body edge");
            var item = Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Tile, TileType = TileID.Stone }), targetX, 42);
            SetAutomatic(host, input, true); Until(context, input, () => ProgressDormant(item));
            Require(!Main.tile[targetX, 42].active() && p.inventory[12].stack == 2,
                "the original out-of-range cell exhausts its walk without any consumption");
            float before = p.position.X; int integerPixel = (int)before;
            p.position.X += 0.25f; p.velocity = Vector2.Zero;
            Require((int)p.position.X == integerPixel && p.IsInTileInteractionRange(targetX, 42, TileReachCheckSettings.Simple, p.inventory[12].tileBoost + p.blockRange),
                "a quarter pixel changes the original Ceiling body extent and true reach while both old integer coordinates stay unchanged");
            float recovered = p.position.X;
            Until(context, input, () => Scene(host).Length == 0);
            Require(p.position.X == recovered && (int)p.position.X == integerPixel && Main.tile[targetX, 42].active() &&
                Main.tile[targetX, 42].type == TileID.Stone && p.inventory[12].stack == 1,
                "the same integer pixel now wakes the exhausted player wait, spends one original stone and naturally retires from full Match");
            Console.WriteLine("PASS BlueprintBuild native subpixel reach: X=" + before + "->" + recovered + " intX=" + integerPixel +
                " originalRight=" + right + " targetX=" + targetX + " actualStoneConsumed=1");
        }
    }
}
