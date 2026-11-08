using System;
using System.Linq;
using HarmonyLib;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static partial class NativeBlueprintBuildChecks
    {
        private static SchedulerFreshnessObservation schedulerFreshness;
        private static SchedulerTimeline schedulerTimeline;
        private sealed class SchedulerTimeEvent
        {
            internal string Kind;
            internal uint World;
            internal long Input, Host, Token, Native, Flight;
            internal int Type, Slot, Time, Animation, ToolTime, Reuse;
            internal bool CanSelect, Buffered, Override, Release;
        }
        // Test-only observations surround the genuine selection callback and
        // Admitted delegate. No observation writes native input or timers.
        private sealed class SchedulerTimeline : IDisposable
        {
            private readonly Harmony patch = new Harmony("JueMingR.Tests.BlueprintSchedulerTimeline");
            internal readonly object host;
            private readonly object tools, input;
            private long preparedGeneration;
            internal readonly System.Collections.Generic.List<SchedulerTimeEvent> Events = new System.Collections.Generic.List<SchedulerTimeEvent>();
            internal SchedulerTimeline(object host, object tools, object input)
            {
                Require(schedulerTimeline == null, "scheduler timeline has one bounded owner");
                this.host = host; this.tools = tools; this.input = input; schedulerTimeline = this;
                try
                {
                    patch.Patch(typeof(Player.SelectedItemState).GetMethod("Update", F),
                        prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerSelectionGate)),
                        postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerSelectionResult)));
                    patch.Patch(host.GetType().GetMethod("AdvanceAutomaticBuild", F),
                        postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerPreparedObserved)));
                    patch.Patch(host.GetType().GetMethod("RememberBuildContinuation", F),
                        prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerAdmissionObserved)));
                }
                catch { Dispose(); throw; }
            }
            internal void Add(string kind, object flight = null)
            {
                Require(Events.Count < 8192, "scheduler timing observation is bounded");
                var p = Main.LocalPlayer; var use = Get(tools, "Use");
                object source = flight == null ? null : Get(flight, "Source");
                Events.Add(new SchedulerTimeEvent { Kind = kind, World = Main.GameUpdateCount,
                    Input = (long)Get(input, "Frame"), Host = checked((long)(ulong)Get(host, "projectionMatchTick")),
                    Token = (long)Get(use, "Operation"), Native = (long)Get(use, "NativeGeneration"),
                    Flight = flight == null ? 0 : (long)Get(flight, "Generation"),
                    Type = source == null ? p.HeldItem.type : (int)Get(source, "Type"), Slot = source == null ? p.selectedItem : (int)Get(source, "Slot"),
                    Time = p.itemTime, Animation = p.itemAnimation, ToolTime = p.toolTime, Reuse = p.reuseDelay,
                    CanSelect = p.selectedItemState.CanChangeSelectedItemImmediately, Buffered = p.selectedItemState.HasBufferedChange,
                    Override = p.selectedItemState.HasActiveOverride, Release = p.releaseUseItem });
            }
            internal void Report(string name)
            {
                Console.WriteLine("BlueprintSchedulerTimeline " + name + " events=kind/world/input/host/type@slot/time-animation-tool-reuse/canSelect-buffered-override-release/token-native-flight " +
                    string.Join(";", Events.Select(value => value.Kind + "/" + value.World + "/" + value.Input + "/" + value.Host + "/" + value.Type + "@" + value.Slot + "/" +
                        value.Time + "-" + value.Animation + "-" + value.ToolTime + "-" + value.Reuse + "/" + value.CanSelect + "-" + value.Buffered + "-" + value.Override + "-" + value.Release +
                        "/" + value.Token + "-" + value.Native + "-" + value.Flight)));
            }
            public void Dispose() { patch.UnpatchAll(patch.Id); if (ReferenceEquals(schedulerTimeline, this)) schedulerTimeline = null; }
            internal void Prepared(object owner)
            {
                if (!ReferenceEquals(owner, host)) return;
                var flight = GetOptional(host, "preparedBuild"); if (flight == null) return;
                long generation = (long)Get(flight, "Generation"); if (generation == preparedGeneration) return;
                preparedGeneration = generation; Add("prepared", flight);
            }
        }
        private static void SchedulerSelectionGate() { schedulerTimeline?.Add("selection-before"); }
        private static void SchedulerSelectionResult() { schedulerTimeline?.Add("selection-after"); }
        private static void SchedulerPreparedObserved(object __instance) { schedulerTimeline?.Prepared(__instance); }
        private static void SchedulerAdmissionObserved(object __instance, object __0)
        { if (schedulerTimeline != null && ReferenceEquals(__instance, schedulerTimeline.host)) schedulerTimeline.Add("admitted", __0); }
        private sealed class SchedulerFreshnessObservation
        {
            internal object Host, Use;
            internal BusinessScope Scope;
            internal BusinessSource Current, Foreign;
            internal BusinessGoal Restored;
            internal object RestoredOwner;
            internal object PreparedForeign;
            internal bool Written, ForbiddenAdmission;
            internal long WriterHost;
            internal uint WriterWorld;
            internal int AdmissionsAfterWriter;
        }

        // The adversarial event is a real hooked writer, immediately before
        // Acquire's ordinary Valid/lease/Admitted chain. This controls event
        // order without setting a product proof, cursor, stamp or dirty flag.
        private static void SchedulerFreshnessBeforeAcquire(object __instance, Player __0, object __1)
        {
            var observation = schedulerFreshness;
            if (observation == null || observation.Written || !ReferenceEquals(observation.Use, __instance) ||
                !ReferenceEquals(__0, Main.LocalPlayer) || (int)Get(__1, "Slot") != observation.Foreign.Slot) return;
            Require(observation.Scope.Events.Count == 1 && observation.Scope.Goals[0].Complete &&
                Main.tile[observation.Restored.X, observation.Restored.Y].active() &&
                Main.tile[observation.Restored.X, observation.Restored.Y].type == TileID.Dirt,
                "the first real A establishes preference while the second A is still physically occupied before the foreign Acquire");
            observation.WriterHost = checked((long)(ulong)Get(observation.Host, "projectionMatchTick"));
            observation.WriterWorld = Main.GameUpdateCount;
            var restoredRange = ((System.Collections.IEnumerable)Get(Get(observation.RestoredOwner, "Match"), "Ranges")).Cast<object>().Single();
            var foreignRange = Get(Get(observation.PreparedForeign, "Target"), "Range");
            var state=NearRangeState(observation.Host,restoredRange);var coverage=Get(state,"Covered");
            var negatives=NearRangeFacts(observation.Host,restoredRange).Where(f=>!(bool)Get(f,"Ready") && !(bool)Get(f,"Pending")).ToArray();
            Require((long)Get(restoredRange,"Watch")!=(long)Get(foreignRange,"Watch"),
                "restored A and prepared B own different genuine observer watches");
            Require((bool)Get(state,"Complete") && (int)Get(coverage,"Right")>(int)Get(coverage,"Left") &&
                (int)Get(coverage,"Bottom")>(int)Get(coverage,"Top") && negatives.Length>0,
                "the genuine blocked A path has published complete shared near coverage and cached negative facts before the writer stimulus");
            Console.WriteLine("BlueprintSchedulerFreshness prior-negative "+NearRangeSummary(observation.Host,restoredRange)+
                " AHostVersion="+Get(restoredRange,"Version")+" BHostVersion="+Get(foreignRange,"Version")+" separateWatch=true supportRegionsSeparated=true");
            WorldGen.KillTile(observation.Restored.X, observation.Restored.Y, fail: false, effectOnly: false, noItem: true);
            observation.Written = true;
            bool currentAProof = (bool)Call(observation.Host, "ProjectionMatchReadCurrent", restoredRange, Get(restoredRange, "RawVersion"));
            bool currentBProof = (bool)Call(observation.Host, "ProjectionMatchReadCurrent", foreignRange, Get(foreignRange, "RawVersion"));
            Require(!currentAProof && currentBProof, "the real writer immediately invalidates A's native watch while the spatially separate B native watch remains current before Host drain");
            Require(!Main.tile[observation.Restored.X, observation.Restored.Y].active() &&
                SchedulerBlockRunnable(observation.Restored, observation.Current),
                "the genuine KillTile writer restores the exact supplied reachable A destination before candidate.Valid and before Admitted");
            Console.WriteLine("BlueprintSchedulerFreshness writer world=" + observation.WriterWorld + " host=" + observation.WriterHost +
                " xy=" + observation.Restored.X + "," + observation.Restored.Y + " currentRunnable=true AProofCurrent=" + currentAProof +
                " BProofCurrent=" + currentBProof + " beforeForeignAdmitted=true noHostDrain=true");
        }

        private static void SchedulerFreshnessAdmitted(object __instance, object __0)
        {
            var observation = schedulerFreshness;
            if (observation == null || !observation.Written || !ReferenceEquals(observation.Host, __instance)) return;
            var source = Get(__0, "Source"); int type = (int)Get(source, "Type");
            bool runnable = SchedulerBlockRunnable(observation.Restored, observation.Current);
            long hostTick = checked((long)(ulong)Get(observation.Host, "projectionMatchTick"));
            observation.AdmissionsAfterWriter++;
            observation.ForbiddenAdmission |= type == observation.Foreign.Type && runnable;
            Console.WriteLine("BlueprintSchedulerFreshness admission world=" + Main.GameUpdateCount + " host=" + hostTick +
                " nextType=" + type + " currentRunnable=" + runnable + " writerHost=" + observation.WriterHost +
                " noHostDrain=" + (hostTick == observation.WriterHost) + " token=" + Get(__0, "Token"));
        }

        internal static void SchedulerImmediateWriterFreshness(object context, object host, object tools, object input)
        {
            using (var scope = new BusinessScope(context, host, tools, input))
            {
                var p = Reset(context, host, tools, input);
                p.position.X = 42 * 16;
                var wood = BusinessStock(p, 12, ItemID.Wood, 4);
                var stone = new BusinessSource { Slot = 13, Type = ItemID.StoneBlock, Item = p.inventory[13] };
                scope.Sources = new[] { wood, stone };
                scope.RunnableOracle = goal => SchedulerBlockRunnable(goal, scope.Sources.Single(source => source.Type == goal.Type));
                scope.Goals = new[] { BusinessTileGoal(44, 42, wood), BusinessTileGoal(46, 42, wood),
                    new BusinessGoal { X = 39, Y = 42, Type = ItemID.StoneBlock, TileType = TileID.Stone } };
                NativeToolsChecks.Tile(46, 42, TileID.Dirt);
                var woodAsset = BusinessTileAsset(host, wood);
                Place(context, host, input, woodAsset, 44, 42); var restoredOwner = Place(context, host, input, woodAsset, 46, 42);
                Place(context, host, input, Asset(host, new JueMingR.Features.Blueprint.CaptureCell {
                    Layers = JueMingR.Features.Blueprint.CaptureLayer.Tile, TileType = TileID.Stone }), 39, 42);
                var patch = new Harmony("JueMingR.Tests.BlueprintSchedulerFreshness");
                try
                {
                    scope.Begin(true); SetAutomatic(host, input, true);
                    NativeQuickItemChecks.Until(() => { scope.Step(); return scope.Events.Count != 0; });
                    Require(scope.Events.Count == 1 && scope.Goals[0].Complete && !scope.Goals[1].Complete,
                        "a genuine first Wood action creates current A while the second A remains wrong-block occupied");
                    var refill = new Item(); refill.SetDefaults(ItemID.StoneBlock); refill.stack = 3;
                    Require(p.GetItem(refill, new Terraria.GetItemSettings(NoText: true, NoSound: true)).IsAir,
                        "native GetItem supplies the legal competing material only after actual A consumption");
                    stone.Slot = Enumerable.Range(0, 50).Single(slot => p.inventory[slot].type == ItemID.StoneBlock);
                    stone.Item = p.inventory[stone.Slot];
                    object preparedForeign = null;
                    NativeQuickItemChecks.Until(() => { scope.Step(); preparedForeign = GetOptional(host, "preparedBuild");
                        return preparedForeign != null && ReferenceEquals(Get(Get(preparedForeign, "Source"), "Item"), stone.Item); });
                    Require(scope.Events.Count == 1 && !SchedulerBlockRunnable(scope.Goals[1], wood) && SchedulerBlockRunnable(scope.Goals[2], stone),
                        "foreign B is genuinely prepared and legal, current A is physically blocked, and B has not consumed");
                    schedulerFreshness = new SchedulerFreshnessObservation { Host = host, Use = Get(tools, "Use"), Scope = scope,
                        Current = wood, Foreign = stone, Restored = scope.Goals[1], RestoredOwner = restoredOwner, PreparedForeign = preparedForeign };
                    patch.Patch(Get(tools, "Use").GetType().GetMethod("Acquire", F),
                        prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerFreshnessBeforeAcquire)));
                    patch.Patch(host.GetType().GetMethod("RememberBuildContinuation", F),
                        prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerFreshnessAdmitted)));
                    NativeQuickItemChecks.Until(() => { scope.Step();
                        Require(!schedulerFreshness.ForbiddenAdmission, "a pre-Admitted original writer invalidates the old A negative proof before foreign B can acquire; current real A is runnable");
                        return scope.Goals.All(goal => goal.Complete) && !(bool)Get(Get(tools, "Use"), "Active"); });
                    Require(schedulerFreshness.Written && schedulerFreshness.AdmissionsAfterWriter > 0 &&
                        scope.Events.Select(value => value.Type).SequenceEqual(new int[] { ItemID.Wood, ItemID.Wood, ItemID.StoneBlock }) &&
                        wood.Item.stack == 2 && stone.Item.stack == 2,
                        "restored A actually consumes before prepared B with exact remaining stock; the writer stimulus was exercised");
                    scope.Report("scheduler immediate writer freshness");
                }
                catch { scope.Report("scheduler immediate writer freshness FAILURE"); throw; }
                finally { patch.UnpatchAll(patch.Id); schedulerFreshness = null; }
            }
        }

        private static SchedulerDeferredSelectionObservation schedulerDeferredSelection;
        private sealed class SchedulerDeferredSelectionObservation
        {
            internal object Host, Target, Fact, Bounds, Covered;
            internal int Advance, ObservedAdvance, DeferredCalls, DeferredNotifies, Cursor, Ordinal;
            internal int UnavailableRightReads;
            internal long BeforeUnavailable, UnavailableDelta;
            internal bool InPlan;
            internal bool Local, CandidateWasExact, CandidatePreserved = true, ForwardPreserved = true;
        }
        private static void SchedulerDeferredAdvanceBefore(object __instance)
        { if (schedulerDeferredSelection != null && ReferenceEquals(__instance, schedulerDeferredSelection.Host)) schedulerDeferredSelection.Advance++; }
        private static void SchedulerDeferredPlanBefore(object __instance, object __0)
        {
            var state = schedulerDeferredSelection;
            if (state == null || !ReferenceEquals(__instance, state.Host)) return;
            state.InPlan = (int)Get(__0, "WorldX") == 44 && (int)Get(__0, "WorldY") == 40;
            state.BeforeUnavailable = (long)Get(state.Host, "buildUnavailableReads");
        }
        private static void SchedulerDeferredCellAfter(object __instance, int __0, int __1, bool __result)
        {
            var state = schedulerDeferredSelection;
            if (state != null && state.InPlan && ReferenceEquals(__instance, state.Host) && __0 == 45 && __1 == 40 && !__result)
                state.UnavailableRightReads++;
        }
        private static void SchedulerDeferredPlanAfter(object __instance, object __0, bool __result)
        {
            var state = schedulerDeferredSelection;
            if (state == null || !ReferenceEquals(__instance, state.Host)) return;
            state.InPlan = false;
            if (__result || (int)Get(__0, "WorldX") != 44 ||
                (int)Get(__0, "WorldY") != 40 || Get(__0, "Readiness").ToString() != "Deferred") return;
            state.UnavailableDelta += (long)Get(state.Host, "buildUnavailableReads") - state.BeforeUnavailable;
            var range=Get(__0,"Range");var near=NearRangeState(state.Host,range);
            state.Target=__0;state.DeferredCalls++;state.ObservedAdvance=state.Advance;
            state.Ordinal=(int)Get(__0,"Ordinal");state.Cursor=near==null?0:(int)Get(near,"Cursor");
            state.Bounds=near==null?null:Get(near,"Bounds");state.Covered=near==null?null:Get(near,"Covered");
            state.Fact=NearRangeFacts(state.Host,range).SingleOrDefault(f=>(int)Get(Get(f,"Seed"),"Ordinal")==state.Ordinal);
            state.Local=(bool)Get(__0,"LocalPriority");state.CandidateWasExact=state.Fact!=null &&
                ReferenceEquals(Get(Get(state.Fact,"Seed"),"Range"),range);
            Console.WriteLine("BlueprintSchedulerDeferred selected-plan world=" + Main.GameUpdateCount + " host=" + Get(state.Host, "projectionMatchTick") +
                " readiness=Deferred ordinal=" + state.Ordinal + " forward=" + state.Cursor + " localPriority=" + state.Local +
                " candidateExact=" + state.CandidateWasExact + " unavailableRightNeighbor=" + (Main.tile[45, 40] == null) +
                " actualPlanRightReadFalse=" + state.UnavailableRightReads + " actualPlanUnavailableDelta=" + state.UnavailableDelta);
        }
        private static void SchedulerDeferredNotify(object __instance, object __0)
        {
            var state = schedulerDeferredSelection;
            if (state != null && ReferenceEquals(__instance, state.Host) && ReferenceEquals(__0, state.Target) && Get(__0, "Readiness").ToString() == "Deferred")
                state.DeferredNotifies++;
        }
        private static void SchedulerDeferredAdvanceAfter(object __instance)
        {
            var state = schedulerDeferredSelection;
            if (state == null || !ReferenceEquals(__instance, state.Host) || state.Target == null || state.ObservedAdvance != state.Advance) return;
            var range=Get(state.Target,"Range");var near=NearRangeState(state.Host,range);
            state.CandidatePreserved &= state.Fact!=null && NearRangeFacts(state.Host,range).Contains(state.Fact) &&
                (int)Get(Get(state.Fact,"Seed"),"Ordinal")==state.Ordinal && (bool)Get(state.Fact,"Pending");
            if(!state.Local)state.ForwardPreserved &= near!=null &&
                Equals(Get(near,"Bounds"),state.Bounds) && Equals(Get(near,"Covered"),state.Covered) && (int)Get(near,"Cursor")==state.Cursor;

        }

        internal static void SchedulerDeferredSelectionReadRecovery(object context, object host, object tools, object input)
        {
            using (var scope = new BusinessScope(context, host, tools, input))
            {
                var p = SubstitutionReset(context, host, tools, input); SubstitutionCatalog(context, host, input);
                Call(host, "SetMaterialViewDemand", false);
                var wall = BusinessStock(p, 12, ItemID.WoodWall, 3); scope.Sources = new[] { wall };
                scope.Goals = new[] { BusinessWallGoal(44, 40, wall) };
                Place(context, host, input, BusinessWallAsset(host, wall), 44, 40);
                var saved = Main.tile[45, 40]; var patch = new Harmony("JueMingR.Tests.BlueprintDeferredSelection");
                schedulerDeferredSelection = new SchedulerDeferredSelectionObservation { Host = host };
                try
                {
                    // The one unavailable support is outside the player. The
                    // target and all other cardinal reads are real empty Tiles;
                    // Reset's floor at y43 cannot support the wall at y40.
                    Main.tile[45, 40] = null;
                    Require(new[] { Main.tile[43, 40], Main.tile[44, 39], Main.tile[44, 41] }.All(tile => tile != null && !tile.active() && tile.wall == 0),
                        "only the right wall-neighbor read is unknown; the other three are definitely empty");
                    patch.Patch(host.GetType().GetMethod("AdvanceAutomaticBuild", F),
                        prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerDeferredAdvanceBefore)),
                        postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerDeferredAdvanceAfter)));
                    // Semantic classification and selection share this actual
                    // evaluator; observing only the outer selector misses a
                    // Deferred fact before it can become a ready head.
                    patch.Patch(host.GetType().GetMethod("EvaluateBuildPlan", F),
                        prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerDeferredPlanBefore)),
                        postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerDeferredPlanAfter)));
                    patch.Patch(host.GetType().GetMethod("ReadBuildCell", F),
                        postfix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerDeferredCellAfter)));
                    patch.Patch(host.GetType().GetMethod("NotifyBuildAttempt", F),
                        prefix: new HarmonyMethod(typeof(NativeBlueprintBuildChecks), nameof(SchedulerDeferredNotify)));
                    scope.Begin(true); SetAutomatic(host, input, true);
                    NativeQuickItemChecks.Until(() => { scope.Step(); return schedulerDeferredSelection.DeferredCalls > 0; });
                    Require(schedulerDeferredSelection.UnavailableRightReads > 0 && schedulerDeferredSelection.UnavailableDelta > 0,
                        "the actual semantic planning evaluation reads the unavailable right neighbor through original ReadBuildCell, rather than failing in an earlier matcher/identity gate");
                    Require(schedulerDeferredSelection.CandidateWasExact && schedulerDeferredSelection.DeferredNotifies == 0 &&
                        schedulerDeferredSelection.CandidatePreserved && schedulerDeferredSelection.ForwardPreserved,
                        "a real fresh-selection planning Deferred retains its exact semantic fact/ordinal without NotifyBuildAttempt; forward advancement is checked only for nonlocal candidates");
                    Require(Main.tile[45, 40] == null && scope.Events.Count == 0 && wall.Item.stack == 3,
                        "unavailable support is not allocated, treated as known rejection, or submitted to a native consumer");
                    Require(WorldGen.PlaceTile(45, 40, TileID.Stone, true, true), "a real original writer restores the missing wall support");
                    NativeQuickItemChecks.Until(() => { scope.Step(); return scope.Goals[0].Complete && !(bool)Get(Get(tools, "Use"), "Active"); });
                    Require(scope.Events.Count == 1 && scope.Events[0].Type == wall.Type && wall.Item.stack == 2 && Scene(host).Length == 0,
                        "the restored same target naturally consumes one exact wall and retires through complete Match");
                    scope.Report("scheduler deferred selection recovery");
                    Console.WriteLine("BlueprintSchedulerDeferred result freshSelectionCalls=" + schedulerDeferredSelection.DeferredCalls +
                        " deferredNotifies=" + schedulerDeferredSelection.DeferredNotifies + " candidatePreserved=" + schedulerDeferredSelection.CandidatePreserved +
                        " observedLocalPriority=" + schedulerDeferredSelection.Local + " forwardClaim=" + (schedulerDeferredSelection.Local ? "NOT_EVALUATED_LOCAL_CANDIDATE" : "UNCHANGED"));
                }
                catch { scope.Report("scheduler deferred selection FAILURE"); throw; }
                finally
                {
                    patch.UnpatchAll(patch.Id); schedulerDeferredSelection = null;
                    if (Main.tile[45, 40] == null) Main.tile[45, 40] = saved;
                    Call(host, "SetSameTypeReplacement", false);
                }
            }
        }
    }
}
