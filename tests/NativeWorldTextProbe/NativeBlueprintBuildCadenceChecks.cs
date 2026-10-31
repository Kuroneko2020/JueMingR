using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using JueMingR.Platform.WorldTargets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private sealed class CadenceEvent
        {
            internal uint World;
            internal long Input, Host, Token, Flight, Block;
            internal int Frame, Count, Time, Animation, X, Y, Slot, Type, Range;
            internal Item Item;
        }
        private sealed class CadenceResult
        {
            internal readonly List<CadenceEvent> Events = new List<CadenceEvent>();
            internal readonly List<string> Waiting = new List<string>();
            internal long SubstitutionChecks, WallPositions, SourceSteps;
            internal int PendingUpdates, EmptyOpportunities;
            internal bool PartialSourceReceipt;
        }
        private static CadenceEvent CadenceConsumed(object host, object tools, object input, Player p, int frame, int count, bool automatic)
        {
            var flight = automatic ? Get(host, "activeBuild") : null;
            var target = flight == null ? null : Get(flight, "Target");
            var range = target == null ? null : Get(target, "Range");
            var source = flight == null ? null : Get(flight, "Source");
            return new CadenceEvent { Frame = frame, Count = count, World = Main.GameUpdateCount, Input = (long)Get(input, "Frame"),
                Host = checked((long)(ulong)Get(host, "projectionMatchTick")), Time = p.itemTime, Animation = p.itemAnimation,
                Token = automatic ? (long)Get(Get(tools, "Use"), "Operation") : 0, Flight = flight == null ? 0 : (long)Get(flight, "Generation"),
                X = target == null ? Player.tileTargetX : (int)Get(target, "WorldX"), Y = target == null ? Player.tileTargetY : (int)Get(target, "WorldY"),
                Slot = source == null ? p.selectedItem : (int)Get(source, "Slot"),
                Type = source == null ? p.HeldItem.type : (int)Get(source, "Type"), Item = source == null ? p.HeldItem : (Item)Get(source, "Item"),
                Block = range == null ? -1 : (long)Get(Get(range, "Source"), "Block"), Range = range == null ? -1 : (int)Get(range, "Index") };
        }
        private static void ReportCadence(string name, CadenceResult value)
        {
            Console.WriteLine("BlueprintCadence " + name + " events=frame/world/input/host/anim/time/token/flight/count/xy/slot/type/block/range " +
                string.Join(";", value.Events.Select(e => e.Frame + "/" + e.World + "/" + e.Input + "/" + e.Host + "/" + e.Animation + "/" + e.Time + "/" + e.Token + "/" + e.Flight + "/" + e.Count + "/" + e.X + "," + e.Y + "/" + e.Slot + "/" + e.Type + "/" + e.Block + "/" + e.Range)) +
                " sourceSteps=" + value.SourceSteps + " substitutionChecks=" + value.SubstitutionChecks + " wallPositions=" + value.WallPositions +
                " pendingUpdates=" + value.PendingUpdates + " emptyNativeOpportunities=" + value.EmptyOpportunities + " partialSourceReceipt=" + value.PartialSourceReceipt);
            if (value.Waiting.Count != 0) Console.WriteLine("BlueprintCadence opportunity-state " + name + " " + string.Join(";", value.Waiting));
        }
        private static string CadenceState(object host, Player p)
        {
            var ready = GetOptional(host, "preparedBuild"); var active = GetOptional(host, "activeBuild");
            var sources = ((IEnumerable)Get(host, "buildSources")).Cast<object>().Take(4).Select(s => {
                var r = Get(s, "Range"); var m = GetOptional(r, "Materials");
                return Get(r, "Index") + ":" + Get(s, "NearCursor") + "/" + (m == null ? "none" : Get(m, "CandidateCursor") + "/" + Get(m, "PreferredCursor") + "/" + Get(m, "BuildExhausted")); });
            var works = ((IDictionary)Get(host, "projectionMatchWork")).Values.Cast<object>().Take(4).Select(w => {
                var r = GetOptional(w, "Read"); return Get(w, "Pending") + ":" + (r == null ? "none" : Get(Get(r, "Range"), "Index") + "/" + Get(r, "CellIndex") + "/" + Get(Get(r, "Range"), "RawVersion")); });
            return "t=" + p.itemTime + "/" + p.itemAnimation + "/" + p.reuseDelay + " prepared=" + (ready == null ? "none" : Get(Get(Get(ready, "Target"), "Range"), "Index").ToString()) +
                " active=" + (active == null ? "none" : Get(active, "Checked") + "/" + Get(Get(Get(active, "Target"), "Range"), "Index")) +
                " candidateBudget=" + Get(host, "substitutionCandidateBudget") + " pending=" + Get(host, "substitutionPlanningPending") +
                " sourceBudget=" + Get(host, "materialBuildSourceBudget") + " pins=[" + string.Join(",", sources) + "] work=[" + string.Join(",", works) + "]";
        }
        private static void EqualCadence(CadenceResult original, CadenceResult actual, string name, bool oneLease = true)
        {
            ReportCadence(name, actual);
            var expected = original.Events.Skip(1).Select((e, i) => unchecked(e.World - original.Events[i].World)).ToArray();
            var gaps = actual.Events.Skip(1).Select((e, i) => unchecked(e.World - actual.Events[i].World)).ToArray();
            Require(original.Events.Sum(e => e.Count) == actual.Events.Sum(e => e.Count) && expected.SequenceEqual(gaps),
                "paired same-item native held opportunities remain exact: " + name + " original=" + string.Join(",", expected) + " actual=" + string.Join(",", gaps));
            if (oneLease) Require(actual.Events.Select(e => e.Token).Distinct().Count() == 1 && actual.Events[0].Token > 0,
                "a finite available same-source sequence retains one actual use lease: " + name);
        }

        // These are anonymous ordinary source values, passed through the real
        // bounded stage/save/normalization/index/worker readers. The resulting
        // physical ranges are checked after real normalization; input chunks
        // are not mistaken for on-disk ranges. No prepared/source cursor is
        // injected, and only natural Player.Update writes destination bodies.
        private static LibraryEntry CadenceAsset(object host, int width, int height, params CaptureCell[][] chunks)
        {
            var library = (LibraryState)Get(host, "Library"); var token = new CaptureToken(Guid.NewGuid(), 1, 1, 1);
            var handle = new ContentHandle(token.OperationId);
            Require(library.TryBeginStage(token), "cadence real stage starts"); CadenceReceipt(library);
            foreach (var chunk in chunks)
            {
                Require(chunk.Length <= CaptureChunk.MaximumRecords && library.TryAppendChunk(handle, new CaptureChunk(chunk, chunk.Length)), "cadence bounded raw chunk accepted");
                CadenceReceipt(library);
            }
            Require(library.TrySealDraft(handle, new CaptureSummary { Width = width, Height = height, ContentCount = chunks.Sum(c => (long)c.Length) }), "cadence draft seals complete source");
            CadenceReceipt(library);
            Require(library.TryCreate(handle, "匿名施工节奏样本", token), "cadence source save accepted"); var saved = CadenceReceipt(library);
            Require(saved.Status == LibraryStatus.Saved && library.TryList(91031, 0, saved.AssetId), "cadence exact saved asset lists");
            return NativeBlueprintLibraryWait.AcceptedList(library, "cadence selected=" + saved.AssetId).Page.Entries.Single(e => e.AssetId == saved.AssetId);
        }
        private static LibraryReceipt CadenceReceipt(LibraryState library)
        {
            LibraryReceipt result = null;
            NativeQuickItemChecks.Until(() => { library.Pump(); return library.TryTakeReceipt(out result); });
            Require(result.Status != LibraryStatus.Failed && result.Status != LibraryStatus.CommitUnconfirmed, "cadence real library operation: " + result.Error);
            return result;
        }

        private static void SubstitutionContinuationCadence(object context, object host, object tools, object input)
        {
            foreach (bool mirror in new[] { false, true })
            {
                var p = SubstitutionReset(context, host, tools, input); p.position = new Vector2(42 * 16, 646); p.velocity = Vector2.Zero;
                SubstitutionCatalog(context, host, input); Call(host, "SetMaterialViewDemand", false);
                p.inventory[12].SetDefaults(ItemID.WoodWall); p.inventory[12].stack = 8; var wood = p.inventory[12];
                // The far sentinel owns its own real raw range. Under mirror,
                // the right raw range is displayed beside the player on left.
                // Normalization packs 2048 records per physical block. This
                // complete left prefix puts the three right-side records in
                // their own raw block, rather than merely another input chunk.
                var left = Enumerable.Range(0, CaptureChunk.MaximumRecords).Select(i => new CaptureCell {
                    X = i < 2016 ? i % 96 : i - 2016, Y = i < 2016 ? i / 96 : 21,
                    Layers = CaptureLayer.Tile, TileType = TileID.Dirt }).ToArray();
                var right = new[] {
                    new CaptureCell { X = 96, Y = 21, Layers = CaptureLayer.Tile, TileType = TileID.Stone },
                    new CaptureCell { X = 97, Y = 21, Layers = CaptureLayer.Wall, WallType = WallID.Wood },
                    new CaptureCell { X = 99, Y = 21, Layers = CaptureLayer.Wall, WallType = WallID.Stone } };
                object instance;
                if (mirror)
                {
                    instance = Place(context, host, input, CadenceAsset(host, 100, 22, left, right), 44, 21);
                    NativeBlueprintWorldMatchChecks.AwaitMovable(context, host, instance);
                    NativeQuickItemChecks.Until(() => { Call(host, "UpdateProjectionWindow", new WorldTargetView(139, 42, 8, 2, 0)); Step(context, input); return GetOptional(instance, "Window") != null; });
                    var identity = (BlueprintProjectionIdentity)Get(instance, "Identity");
                    NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); Call(host, "MirrorProjection");
                    Call(host, "AcceptWorldPointer", true, false, false, 141, 42, Main.maxTilesX, Main.maxTilesY);
                    Call(host, "AcceptWorldPointer", true, true, false, 141, 42, Main.maxTilesX, Main.maxTilesY);
                    Until(context, input, () => !(bool)Get(host, "ProjectionEditing"));
                    instance = Scene(host).Single(s => identity.Equals((BlueprintProjectionIdentity)Get(s, "Identity")));
                    Require((bool)Get(instance, "MirroredHorizontally"), "real mirror publishes a new current owner before cadence starts");
                    WorldGen.PlaceWall(45, 42, WallID.Wood, true); // first Wood target is display x2 / world46
                }
                else
                {
                    instance = Place(context, host, input, Asset(host,
                        new CaptureCell { Layers = CaptureLayer.Wall, WallType = WallID.Wood },
                        new CaptureCell { X = 2, Layers = CaptureLayer.Wall, WallType = WallID.Stone },
                        new CaptureCell { X = 3, Layers = CaptureLayer.Tile, TileType = TileID.Stone }), 44, 42);
                    WorldGen.PlaceWall(43, 42, WallID.Wood, true); WorldGen.PlaceWall(47, 42, WallID.Wood, true);
                }
                Main.SmartCursorWanted_Mouse = Main.SmartCursorWanted_GamePad = false;
                SetAutomatic(host, input, true); var events = new CadenceResult(); bool suppliedOriginal = false;
                for (int frame = 0; frame < 1200 && events.Events.Count < 3; frame++)
                {
                    int before = p.inventory.Take(50).Sum(i => i.stack);
                    NativeToolExecutionChecks.Sample(context, input, new Vector2(480, 540), false);
                    NativeQuickItemChecks.BeginWorldStep(); p.Update(0);
                    int spent = before - p.inventory.Take(50).Sum(i => i.stack);
                    if (spent > 0)
                    {
                        events.Events.Add(CadenceConsumed(host, tools, input, p, frame, spent, true));
                        if (!suppliedOriginal)
                        {
                            // Publish A before the production update prepares
                            // the successor; A and current B are both legal.
                            p.inventory[13].SetDefaults(ItemID.StoneWall); p.inventory[13].stack = 4;
                            p.inventory[14].SetDefaults(ItemID.StoneBlock); p.inventory[14].stack = 2;
                            suppliedOriginal = true;
                        }
                    }
                    Call(context, "UpdateRuntime");
                }
                ReportCadence("continuation mirror=" + mirror, events);
                Require(events.Events.Count == 3 && events.Events[0].Type == ItemID.WoodWall && events.Events[1].Type == ItemID.WoodWall &&
                    events.Events.Take(2).All(e => e.Slot == 12 && ReferenceEquals(e.Item, wood)) && events.Events[0].Token == events.Events[1].Token &&
                    events.Events[2].Type == ItemID.StoneBlock && p.inventory[13].stack == 4,
                    "exact current B continues despite supplied original A, before a different-source fallback receives finite ordinary service; mirror=" + mirror);
                if (mirror)
                {
                    var ranges = EligibilityRanges(instance);
                    var rawBounds = (BlueprintProjectionBounds)Get(Get(ranges[events.Events[0].Range], "Source"), "Bounds");
                    var near = (BlueprintProjectionBounds)Call(host, "BuildNearBounds", instance);
                    var sourceNear = (BlueprintProjectionBounds)Call(Get(instance, "View"), "SourceBounds", near);
                    Console.WriteLine("BlueprintCadence mirror raw=" + CadenceBounds(rawBounds) + " displayNear=" + CadenceBounds(near) + " sourceNear=" + CadenceBounds(sourceNear));
                    Require(ranges.Length == 2 && events.Events.Take(2).All(e => e.Range == events.Events[0].Range) && rawBounds.Contains(97, 21) &&
                        !(rawBounds.Left < near.Right && near.Left < rawBounds.Right && rawBounds.Top < near.Bottom && near.Top < rawBounds.Bottom) &&
                        rawBounds.Left < sourceNear.Right && sourceNear.Left < rawBounds.Right && rawBounds.Top < sourceNear.Bottom && sourceNear.Top < rawBounds.Bottom,
                        "actual mirrored consumers belong to the right raw range whose coordinates differ from displayed nearby reach");
                }
                SetAutomatic(host, input, false);
            }
            // A fresh selection owns a different responsibility from a probe.
            var fresh = SubstitutionReset(context, host, tools, input);
            fresh.inventory[12].SetDefaults(ItemID.WoodWall); fresh.inventory[12].stack = 3;
            fresh.inventory[13].SetDefaults(ItemID.StoneWall); fresh.inventory[13].stack = 3;
            Place(context, host, input, Asset(host, new CaptureCell { Layers = CaptureLayer.Wall, WallType = WallID.Stone }), 44, 42);
            WorldGen.PlaceWall(43, 42, WallID.Wood, true); SetAutomatic(host, input, true);
            Until(context, input, () => Scene(host).Length == 0 && !(bool)Get(Get(tools, "Use"), "Active"));
            Require(fresh.inventory[12].stack == 3 && fresh.inventory[13].stack == 2 && Main.tile[44, 42].wall == WallID.Stone,
                "genuine new material selection still consumes the available legal original A first");
            Call(host, "SetSameTypeReplacement", false);
        }

        private static CadenceResult WallRefusalCadence(object context, object host, object tools, object input, int materials, bool substitution, bool automatic)
        {
            var p = SubstitutionReset(context, host, tools, input); p.position = new Vector2(48 * 16, 646); p.velocity = Vector2.Zero;
            var catalog = SubstitutionCatalog(context, host, input); Call(host, "SetMaterialViewDemand", false);
            Call(host, "SetSameTypeReplacement", substitution);
            p.inventory[12].SetDefaults(ItemID.WoodWall); p.inventory[12].stack = 32;
            var alternatives = catalog.Substitution.Members.Where(m => m.Category == BlueprintSubstitutionCategory.Wall && m.Placement.UseItemType != ItemID.WoodWall)
                .Select(m => m.Placement.UseItemType).Distinct().Take(materials - 1).ToArray();
            for (int i = 0; i < alternatives.Length; i++) { p.inventory[13 + i].SetDefaults(alternatives[i]); p.inventory[13 + i].stack = 32; }
            WorldGen.PlaceWall(42, 37, WallID.Wood, true); WorldGen.PlaceWall(53, 43, WallID.Wood, true);
            var points = new[] { new Point(43, 37), new Point(53, 42) };
            Require(points.All(v => p.IsInTileInteractionRange(v.X, v.Y, Terraria.DataStructures.TileReachCheckSettings.Simple, p.inventory[12].tileBoost + p.blockRange)), "both distant legal wall clusters stay in native reach");
            if (automatic)
            {
                var cells = new List<CaptureCell>();
                // Keep every refusal two cells above the real player floor;
                // the floor itself would support a wall on row42.
                for (int y = 0; y < 6; y++) for (int x = 2; x <= 8; x++) cells.Add(new CaptureCell { X = x, Y = y, Layers = CaptureLayer.Wall, WallType = WallID.Wood });
                cells.Add(new CaptureCell { X = 0, Y = 1, Layers = CaptureLayer.Wall, WallType = WallID.Wood });
                cells.Add(new CaptureCell { X = 10, Y = 6, Layers = CaptureLayer.Wall, WallType = WallID.Wood });
                var instance = Place(context, host, input, Asset(host, cells.OrderBy(c => c.Y).ThenBy(c => c.X).ToArray()), 43, 36);
                Until(context, input, () => GetOptional(instance, "Match") != null && EligibilityRanges(instance).All(r => (bool)Get(r, "Current")));
                SetAutomatic(host, input, true);
            }
            else { p.selectedItemState.Select(12); Step(context, input); }
            Main.SmartCursorWanted_Mouse = Main.SmartCursorWanted_GamePad = false;
            long checks = (long)Get(host, "SubstitutionCandidateChecks"), source = (long)Get(host, "ProjectionMatchSourceSteps");
            long positions = host.GetType().GetProperty("SubstitutionWallPositionChecks", F) == null ? -1 : (long)Get(host, "SubstitutionWallPositionChecks");
            var result = new CadenceResult();
            for (int frame = 0; frame < 1200 && points.Any(v => Main.tile[v.X, v.Y].wall == 0); frame++)
            {
                var next = points.First(v => Main.tile[v.X, v.Y].wall == 0); int before = p.inventory[12].stack;
                bool opportunity = result.Events.Count > 0 && p.itemTime <= 1;
                string beforeState = automatic && opportunity ? CadenceState(host, p) : null;
                NativeToolExecutionChecks.Sample(context, input, new Vector2(automatic ? 480 : next.X * 16 + 8, automatic ? 540 : next.Y * 16 + 8), !automatic);
                NativeQuickItemChecks.BeginWorldStep(); p.Update(0);
                int spent = before - p.inventory[12].stack;
                if (spent > 0) result.Events.Add(CadenceConsumed(host, tools, input, p, frame, spent, automatic));
                else if (automatic && opportunity && GetOptional(host, "preparedBuild") == null) result.EmptyOpportunities++;
                Call(context, "UpdateRuntime");
                if (automatic && (bool)Get(host, "substitutionPlanningPending")) result.PendingUpdates++;
                if (automatic && opportunity && spent == 0 && result.Waiting.Count < 64) result.Waiting.Add(frame + "/" + Main.GameUpdateCount + "/" + Get(input, "Frame") + "/" + Get(host, "projectionMatchTick") + " before{" + beforeState + "} after{" + CadenceState(host, p) + "}");
            }
            result.SubstitutionChecks = (long)Get(host, "SubstitutionCandidateChecks") - checks;
            result.WallPositions = positions < 0 ? -1 : (long)Get(host, "SubstitutionWallPositionChecks") - positions;
            result.SourceSteps = (long)Get(host, "ProjectionMatchSourceSteps") - source;
            ReportCadence("wall materials=" + materials + " policy=" + substitution + " automatic=" + automatic, result);
            Require(result.Events.Count == 2 && p.inventory[12].stack == 30 && Enumerable.Range(45, 7).All(x => Enumerable.Range(36, 6).All(y => Main.tile[x, y].wall == 0)),
                "both real consumers use the original, while all42 unsupported targets remain unsupported and unbuilt; events=" + result.Events.Count + " stock=" + p.inventory[12].stack);
            SetAutomatic(host, input, false); Call(host, "SetSameTypeReplacement", false);
            return result;
        }
        private static void SharedWallRefusalCadence(object context, object host, object tools, object input)
        {
            var original = WallRefusalCadence(context, host, tools, input, 1, false, false);
            var off = WallRefusalCadence(context, host, tools, input, 20, false, true);
            var single = WallRefusalCadence(context, host, tools, input, 1, true, true);
            var many = WallRefusalCadence(context, host, tools, input, 20, true, true);
            EqualCadence(original, off, "wall OFF supported successor after shared refusals");
            EqualCadence(original, single, "wall ON one material");
            EqualCadence(original, many, "wall ON twenty safe materials");
            Require(many.PendingUpdates == 0 && many.SubstitutionChecks == single.SubstitutionChecks && many.SubstitutionChecks < 42 && many.WallPositions > 0,
                "42 material-independent refusals spend no candidate budget; twenty materials do not multiply real preflights or postpone the legal successor");
        }

        private static string CadenceBounds(BlueprintProjectionBounds value)
        { return value.Left + "," + value.Top + "," + value.Width + "," + value.Height; }

        private static object cadencePrefetchLibrary;
        private static long cadencePrefetchToken;
        private static int cadencePrefetchRefusals;
        private static bool HoldCadencePrefetch(object __instance, long __1, IReadOnlyList<BlueprintProjectionSourceDemand> __2)
        {
            if (!ReferenceEquals(__instance, cadencePrefetchLibrary)) return true;
            if (cadencePrefetchToken == 0 && __2.Count > 1) { cadencePrefetchToken = __1; return true; }
            if (__1 != cadencePrefetchToken) return true;
            cadencePrefetchRefusals++; return false;
        }
        private static void BuildPrefetchExit(object context, object host, object tools, object input, bool close)
        {
            var patch = new Harmony("JueMingR.Tests.BlueprintCadencePrefetchExit");
            try
            {
                var p = SubstitutionReset(context, host, tools, input); SubstitutionCatalog(context, host, input);
                Call(host, "SetMaterialViewDemand", false);
                p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 64;
                p.position = new Vector2(1068 * 16, 86 * 16 - p.height); p.velocity = Vector2.Zero;
                for (int x = 1060; x < 1072; x++) WorldGen.PlaceTile(x, 86, TileID.WoodBlock, true, true);
                var chunks = Enumerable.Range(0, 4).Select(y => Enumerable.Range(0, 1024).Select(x => new CaptureCell {
                    X = x, Y = y, Layers = CaptureLayer.Tile, TileType = TileID.Dirt }).ToArray()).ToArray();
                var instance = Place(context, host, input, CadenceAsset(host, 1024, 4, chunks), 44, 80);
                Until(context, input, () => GetOptional(instance, "Match") != null && EligibilityRanges(instance).All(r => (bool)Get(r, "Current")));
                cadencePrefetchLibrary = Get(host, "Library"); cadencePrefetchToken = 0; cadencePrefetchRefusals = 0;
                // Only this control test supplies ordinary request backpressure
                // after the real first receipt. The timing comparisons above
                // never patch admission, source delivery, timers or consumers.
                patch.Patch(typeof(LibraryState).GetMethod("RequestProjectionSources", F),
                    prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(HoldCadencePrefetch)));
                SetAutomatic(host, input, true);
                object work = null;
                Until(context, input, () => {
                    work = ((IDictionary)Get(host, "projectionMatchWork")).Values.Cast<object>().FirstOrDefault(w => (long)Get(w, "Token") == cadencePrefetchToken);
                    return work != null && !(bool)Get(work, "Pending") && (int)Get(Get(work, "Requested"), "Count") == 1 && cadencePrefetchRefusals > 0;
                });
                Require((bool)Get(work, "BuildPrefetch") && (int)Get(work, "RequestedPartCount") == 2,
                    "a real two-source request retains only its unreceived suffix and the original two-part container charge");
                long token = cadencePrefetchToken;
                if (close) SetAutomatic(host, input, false);
                else { p.position = new Vector2(42 * 16, 16 * 16); p.velocity = Vector2.Zero; }
                Until(context, input, () => close ? !(bool)Get(work, "BuildPrefetch") : (int)Get(Get(work, "Requested"), "Count") == 0);
                int stopped = cadencePrefetchRefusals;
                for (int n = 0; n < 4; n++) Step(context, input);
                Require(cadencePrefetchRefusals == stopped && p.inventory[12].stack == 64,
                    "closing construction or leaving source-near stops the old-token suffix without an idle use or native consumption");
                Console.WriteLine("PASS BlueprintCadence prefetch exit=" + (close ? "automatic OFF" : "native player movement outside source-near") +
                    " sameWorkToken=" + token + " originalPartCharge=2 remaining=" + Get(Get(work, "Requested"), "Count") +
                    " repeatRequestsAfterExit=0 noConsumption=true fixture=request-backpressure-only");
            }
            finally
            {
                cadencePrefetchLibrary = null; patch.UnpatchAll(patch.Id);
                SetAutomatic(host, input, false); Call(host, "ClearProjection");
                Require((int)Get(host, "ProjectionMatchWorkCount") == 0 && (long)Get(host, "ProjectionMatchSourceRetainedBytes") == 0,
                    "prefetch identity exit releases the original work and retained source references");
                Main.LocalPlayer.position = new Vector2(42 * 16, 646); Main.LocalPlayer.velocity = Vector2.Zero;
            }
        }

        private static CadenceResult LongRangeCadence(object context, object host, object tools, object input, int height, bool automatic, bool substitution)
        {
            const int width = 1024, ox = 44, oy = 80;
            // Four rows need the player beside, rather than inside, the last
            // row. Five columns remain in that same native reach rectangle.
            int start = height == 4 ? 1019 : 1018;
            var p = SubstitutionReset(context, host, tools, input); SubstitutionCatalog(context, host, input);
            Call(host, "SetMaterialViewDemand", false); Call(host, "SetSameTypeReplacement", substitution);
            for (int x = ox + start - 4; x <= ox + width + 2; x++) for (int y = oy - 4; y <= oy + height + 5; y++) Main.tile[x, y].ClearEverything();
            for (int x = ox + start - 4; x <= ox + width + 2; x++) Require(WorldGen.PlaceTile(x, oy + 6, TileID.WoodBlock, true, true), "long cadence independent native player floor");
            // Original walls support these ordinary blocks without putting a
            // solid fixture row through the player's head or a target body.
            for (int y = 0; y < height; y++) for (int x = start; x < width; x++) WorldGen.PlaceWall(ox + x, oy + y, WallID.Wood, true);
            p.position = new Vector2((height == 4 ? ox + width : ox + start + 2) * 16, (oy + 6) * 16 - p.height); p.velocity = Vector2.Zero;
            Main.screenPosition = new Vector2((ox + start - 8) * 16, (oy - 8) * 16);
            p.inventory[12].SetDefaults(ItemID.StoneBlock); p.inventory[12].stack = 64; var item = p.inventory[12];
            var points = new List<Point>(); for (int y = height - 1; y >= 0; y--) for (int x = start; x < width; x++) points.Add(new Point(ox + x, oy + y));
            Require(points.All(v => p.IsInTileInteractionRange(v.X, v.Y, Terraria.DataStructures.TileReachCheckSettings.Simple, item.tileBoost + p.blockRange)), "every long-source consumption point is in the same actual native reach envelope");
            if (automatic)
            {
                var chunks = Enumerable.Range(0, height).Select(y => Enumerable.Range(0, width).Select(x => new CaptureCell { X = x, Y = y, Layers = CaptureLayer.Tile,
                    TileType = x >= start ? (ushort)TileID.Stone : (ushort)TileID.Dirt }).ToArray()).ToArray();
                var instance = Place(context, host, input, CadenceAsset(host, width, height, chunks), ox, oy);
                Until(context, input, () => GetOptional(instance, "Match") != null && EligibilityRanges(instance).All(r => (bool)Get(r, "Current")));
                Require(!(bool)Get(instance, "MirroredHorizontally") && EligibilityRanges(instance).Length == 2 &&
                    EligibilityRanges(instance).Select(r => (long)Get(Get(r, "Source"), "Block")).Distinct().Count() == 2,
                    (width * height) + " real normalized source records expose two distinct physical/logical ranges, without mirror");
                SetAutomatic(host, input, true);
            }
            else { p.selectedItemState.Select(12); Step(context, input); }
            Main.SmartCursorWanted_Mouse = Main.SmartCursorWanted_GamePad = false;
            var result = new CadenceResult(); long source = (long)Get(host, "ProjectionMatchSourceSteps"), checks = (long)Get(host, "SubstitutionCandidateChecks");
            for (int frame = 0; frame < 2000 && points.Any(v => !Main.tile[v.X, v.Y].active()); frame++)
            {
                var next = points.First(v => !Main.tile[v.X, v.Y].active()); int before = item.stack;
                bool opportunity = automatic && result.Events.Count > 0 && p.itemTime <= 1;
                string beforeState = opportunity ? CadenceState(host, p) : null;
                NativeToolExecutionChecks.Sample(context, input, new Vector2(automatic ? 480 : next.X * 16 + 8, automatic ? 540 : next.Y * 16 + 8), !automatic);
                NativeQuickItemChecks.BeginWorldStep(); p.Update(0); int spent = before - item.stack;
                if (spent > 0) result.Events.Add(CadenceConsumed(host, tools, input, p, frame, spent, automatic));
                Call(context, "UpdateRuntime");
                if (automatic) foreach (object work in ((IDictionary)Get(host, "projectionMatchWork")).Values)
                    if (((ICollection)Get(work, "Parts")).Count > 0 && GetOptional(work, "Read") != null && (bool)Get(work, "Pending") &&
                        (int)Get(Get(work, "Requested"), "Count") > 0) result.PartialSourceReceipt = true;
                if (automatic && (bool)Get(host, "substitutionPlanningPending")) result.PendingUpdates++;
                if (opportunity && spent == 0)
                {
                    result.EmptyOpportunities++;
                    if (result.Waiting.Count < 64) result.Waiting.Add(frame + "/" + Main.GameUpdateCount + "/" + Get(input, "Frame") + "/" + Get(host, "projectionMatchTick") + " before{" + beforeState + "} after{" + CadenceState(host, p) + "}");
                }
                Require((int)Get(Get(host, "buildSources"), "Count") <= 4 && (int)Get(host, "materialBuildSourceBudget") >= 0, "long range cadence preserves four-source and shared work budgets");
                Require((int)Get(host, "ProjectionMatchWorkCount") <= 4 && (long)Get(host, "ProjectionMatchReservedBytes") <= 2 * BlueprintProjectionSourceReceipt.MaximumEstimatedBytes + 16384 &&
                    (long)Get(host, "buildSourceBytes") <= 4 * BlueprintProjectionSourceReceipt.TargetBytes, "long source prefetch shares the unchanged work, receipt and retained-byte limits");
            }
            result.SourceSteps = (long)Get(host, "ProjectionMatchSourceSteps") - source; result.SubstitutionChecks = (long)Get(host, "SubstitutionCandidateChecks") - checks;
            ReportCadence("long ordinary records=" + (width * height) + " auto=" + automatic + " policy=" + substitution, result);
            Require(result.Events.Count == points.Count && item.stack == 64 - points.Count && result.Events.All(e => e.Slot == 12 && e.Type == ItemID.StoneBlock && ReferenceEquals(e.Item, item)) &&
                points.All(v => Main.tile[v.X, v.Y].active() && Main.tile[v.X, v.Y].type == TileID.Stone), "every destination is a true native consume from the same exact Stone source");
            if (automatic) Require(result.Events.Select(e => e.Block).Distinct().Count() == 2 && result.Events.Select(e => e.Range).Distinct().Count() == 2,
                "actual native consumption, rather than only metadata, crosses the real normalized source/range boundary");
            if (automatic && height == 4) Require(result.PartialSourceReceipt, "two full2048-record windows really cross the256KiB receipt prefix while the first world sample remains active");
            SetAutomatic(host, input, false); Call(host, "SetSameTypeReplacement", false); return result;
        }
        private static void LongRangeNativeCadence(object context, object host, object tools, object input)
        {
            var screen = Main.screenPosition;
            using (NativeBlueprintWorldMatchChecks.EmptyWorld(host, 1280, 200))
            try
            {
                foreach (int height in new[] { 3, 4 })
                {
                    var original = LongRangeCadence(context, host, tools, input, height, false, false);
                    var off = LongRangeCadence(context, host, tools, input, height, true, false);
                    var on = LongRangeCadence(context, host, tools, input, height, true, true);
                    EqualCadence(original, off, (1024 * height) + "-record / two-range OFF");
                    EqualCadence(original, on, (1024 * height) + "-record / two-range ON ordinary original");
                    Require(on.SubstitutionChecks == 0 && off.SubstitutionChecks == 0, "non-category Stone retains its exact planning path under ON and OFF");
                }
                BuildPrefetchExit(context, host, tools, input, true);
                BuildPrefetchExit(context, host, tools, input, false);
            }
            finally
            {
                SetAutomatic(host, input, false); Call(host, "ClearProjection"); Main.screenPosition = screen;
                Main.LocalPlayer.position = new Vector2(42 * 16, 646); Main.LocalPlayer.velocity = Vector2.Zero;
                Player.tileTargetX = 44; Player.tileTargetY = 42;
            }
        }
    }
}
