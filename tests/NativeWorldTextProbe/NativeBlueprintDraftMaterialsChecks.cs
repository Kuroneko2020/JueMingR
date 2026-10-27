using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using HarmonyLib;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    // The same production capture/worker lifetime is consumed directly by the
    // CPU check and through the real visible Prepare in the graphics check.
    // Gates only fix asynchronous ordering; no Flow or material value is supplied.
    internal static class NativeBlueprintDraftMaterialsChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static object pausedWorker;
        private static LibraryState observedLibrary;
        private static Guid pausedOperation;
        private static string pauseKind;
        private static ManualResetEventSlim entered, proceed;
        private static List<ManualResetEventSlim> signals;
        private static long stageCalls;
        private static bool tracedMaterialRead;

        internal static void Cpu(object context) { Run(context, null, null); }
        internal static void Visual(object context, ProbeGraphics graphics, string output) { Run(context, graphics, output); }

        private static void Run(object context, ProbeGraphics graphics, string output)
        {
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell"); var input = Get(context, "Input");
            var ui = Get(shell, "BlueprintUi"); var editor = (BlueprintCaptureState)Get(host, "State");
            var flow = (BlueprintCaptureFlow)Get(host, "Flow"); var library = (LibraryState)Get(host, "Library");
            var worker = Get(library, "worker"); var audit = new Harmony("JueMingR.Tests.DraftMaterialLifetime");
            bool automatic = (bool)Get(host, "AutoBuildEnabled");
            signals = new List<ManualResetEventSlim>(); pausedWorker = worker; observedLibrary = library; stageCalls = 0; tracedMaterialRead = false;
            entered = new ManualResetEventSlim(); proceed = new ManualResetEventSlim(true); signals.Add(entered); signals.Add(proceed);
            Exception failure = null;
            string phase = "setup";
            Action<string, Func<bool>> wait = (label, done) =>
            { phase = label; Console.WriteLine("DRAFT material lifetime phase=" + label); NativeQuickItemChecks.Until(done); };
            bool preparing = false, tracedPrepare = false; int gameThread = Thread.CurrentThread.ManagedThreadId;
            var prepareFaults = new List<Exception>();
            EventHandler<FirstChanceExceptionEventArgs> observe = (sender, args) =>
            { if (preparing && Thread.CurrentThread.ManagedThreadId == gameThread && prepareFaults.Count < 8) prepareFaults.Add(args.Exception); };
            AppDomain.CurrentDomain.FirstChanceException += observe;
            audit.Patch(AccessTools.Method(worker.GetType(), "Execute"), prefix: new HarmonyMethod(typeof(NativeBlueprintDraftMaterialsChecks), nameof(BeforeCommand)));
            audit.Patch(AccessTools.Method(Get(worker, "files").GetType(), "PromoteStage"), prefix: new HarmonyMethod(typeof(NativeBlueprintDraftMaterialsChecks), nameof(BeforePromote)));
            audit.Patch(AccessTools.Method(typeof(LibraryState), "RequestMaterials"), prefix: new HarmonyMethod(typeof(NativeBlueprintDraftMaterialsChecks), nameof(BeforeRequest)));
            audit.Patch(AccessTools.Method(worker.GetType(), "ReadChunk"), prefix: new HarmonyMethod(typeof(NativeBlueprintDraftMaterialsChecks), nameof(BeforeMaterialRead)));
            using (NativeBlueprintWorldMatchChecks.EmptyWorld(host, 120, 120))
            try
            {
                Call(host, "SetAutoBuild", false); Call(shell, "CloseAndSubmitPosition");
                NativeBlueprintWorldMatchChecks.InitializeMetadata(); Framing.Initialize(); Main.SetupTileMerge();
                Main.tile[40, 40].wall = WallID.Wood; Main.tile[41, 40].wall = WallID.Stone;
                if (graphics != null) graphics.LoadItemTextures(new[] { (int)ItemID.WoodWall, (int)ItemID.StoneWall });
                Action prepare = () =>
                {
                    if (graphics != null)
                    {
                        // A visible page follows the real mapped-input stage.
                        // ProcessInput owns the Shell matrix; jumping directly
                        // to AfterUpdate in a fresh host leaves its default zero
                        // matrix and legitimately fails the readable viewport.
                        NativeBlueprintMaterialsUiChecks.Frame(shell, input, 1, 1, false);
                        if (!tracedPrepare)
                        {
                            tracedPrepare = true;
                            Console.WriteLine("DRAFT first Prepare shellMatrix=" + ((Microsoft.Xna.Framework.Matrix)Get(shell, "matrix")).M11 +
                                " actualMatrix=" + Main.UIScaleMatrix.M11 + " UiLoaded=" + Get(Get(shell, "preferences"), "UiLoaded") +
                                " StateReady=" + Get(Get(shell, "State"), "Ready") + " LayersReady=" + Get(shell, "LayersReady") +
                                " CanPresent=" + Get(shell, "CanPresentNow") + " Failed=" + Get(shell, "Failed") + " Visible=" + Get(ui, "Visible"));
                        }
                        preparing = true; try { Call(shell, "AfterUpdate"); } finally { preparing = false; }
                        if ((bool)Get(shell, "Failed") || !(bool)Get(ui, "ready"))
                        {
                            Console.Error.WriteLine("DRAFT Prepare unavailable phase=" + phase + " flow=" + flow.Status + " UiLoaded=" + Get(Get(shell, "preferences"), "UiLoaded") +
                                " StateReady=" + Get(Get(shell, "State"), "Ready") + " LayersReady=" + Get(shell, "LayersReady") +
                                " CanPresent=" + Get(shell, "CanPresentNow") + " Failed=" + Get(shell, "Failed") + " ready=" + Get(ui, "ready") + " Visible=" + Get(ui, "Visible"));
                            foreach (var fault in prepareFaults) Console.Error.WriteLine("DRAFT original synchronous Prepare exception: " + fault);
                        }
                        Require(!(bool)Get(shell, "Failed") && (bool)Get(ui, "ready"), "real visible capture Prepare remains available throughout its accepted operation");
                    }
                    else Call(host, "MaterialDraft");
                };
                Action step = () => { NativeQuickItemChecks.BeginWorldStep(); Call(context, "UpdateRuntime"); Call(host, "Poll"); if ((bool)Get(ui, "Visible")) prepare(); };
                Call(host, "SetMaterialViewDemand", true);
                wait("warm catalog", () => { step(); return GetOptional(host, "MaterialCatalog") != null; });
                var catalog = (BlueprintPlacementCatalog)Get(host, "MaterialCatalog");
                Call(host, "SetMaterialViewDemand", false); Call(host, "CancelStaticMaterialViews");

                Action begin = () =>
                {
                    Call(shell, "CloseAndSubmitPosition"); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
                    editor.Enter(); editor.Selection.Clear(); editor.Selection.ToggleRectangle(40, 40, 41, 40, 120, 120);
                    Require((bool)Call(host, "Save") && flow.Status == BlueprintCaptureStatus.Starting, "real Save opens a new Starting capture with its allocated handle");
                    pausedOperation = flow.ContentHandle.Id;
                };
                Action pending = () =>
                {
                    var value = (BlueprintMaterialSnapshot)Call(host, "MaterialDraft");
                    Require(value.Status == BlueprintMaterialStatus.Pending, "an unsealed/cancelled source stays honestly Pending, never cached Unresolved");
                    if (graphics != null) Require(((BlueprintMaterialSnapshot)Get(ui, "draftMaterials")).Status == BlueprintMaterialStatus.Pending,
                        "real RefreshMaterials shows Pending before the seal receipt");
                };
                Arm("BeginStage"); begin(); wait("Starting gate", () => entered.IsSet);
                long requests = (long)Get(worker, "MaterialRequests");
                for (int i = 0; i < 8; i++) { prepare(); pending(); }
                Require((long)Get(worker, "MaterialRequests") == requests && GetOptional(host, "materialDraftKey") == null,
                    "warm-catalog Starting Prepare never submits a stage material request or creates an early key");
                proceed.Set(); Arm("SealDraft");
                wait("Capturing", () => { step(); return flow.Status == BlueprintCaptureStatus.Capturing; });
                for (int i = 0; i < 8; i++) { prepare(); pending(); }
                Require((long)Get(worker, "MaterialRequests") == requests, "Capturing Prepare cannot read a half-written stage");
                wait("summary-Sealing gate", () => { step(); return entered.IsSet && flow.Status == BlueprintCaptureStatus.Sealing; });
                Require(flow.Summary != null, "Sealing already carries the fixed summary but not the completed seal receipt");
                for (int i = 0; i < 8; i++) { prepare(); pending(); }
                Require((long)Get(worker, "MaterialRequests") == requests, "even Summary plus Sealing does not authorize derived stage reads");
                var operation = flow.Token.OperationId; var handle = flow.ContentHandle; var summary = flow.Summary;
                proceed.Set(); pauseKind = null;
                BlueprintMaterialSnapshot exact = null;
                wait("sealed exact bill", () => { step(); exact = (BlueprintMaterialSnapshot)Call(host, "MaterialDraft"); return flow.Status == BlueprintCaptureStatus.DraftReady && exact.Status == BlueprintMaterialStatus.Exact; });
                Require(exact.KindCount == 2 && exact.Total == 2 && exact.Rows.Count == 2 && exact.Rows.All(r => r.Required == 1) &&
                    exact.Rows.Any(r => r.ItemType == ItemID.WoodWall) && exact.Rows.Any(r => r.ItemType == ItemID.StoneWall),
                    "the same open page naturally derives independently known one Wood Wall and one Stone Wall");
                Require(flow.Token.OperationId == operation && flow.ContentHandle.Equals(handle) && ReferenceEquals(flow.Summary, summary) &&
                    ReferenceEquals(catalog, Get(host, "MaterialCatalog")), "no close/reopen, exclusion change, resample or catalog change repairs the result");
                long slices = (long)Get(worker, "MaterialSlices"); requests = (long)Get(worker, "MaterialRequests");
                for (int i = 0; i < 120; i++) prepare();
                Require((long)Get(worker, "MaterialSlices") == slices && (long)Get(worker, "MaterialRequests") == requests,
                    "120 stable preparations reuse exact materials without body rereads or failed-read retries");

                wait("invalid-name command admission", () => { step(); return !library.Snapshot.Busy; });
                Call(host, "SetDraftName", ""); Call(host, "ConfirmSave");
                long calls = stageCalls;
                for (int i = 0; i < 8; i++) prepare();
                Require(flow.Status == BlueprintCaptureStatus.Saving && ReferenceEquals(exact, Call(host, "MaterialDraft")),
                    "Saving freezes the accepted key's available exact value without waiting for materials");
                Require(stageCalls == calls, "Saving does not even call RequestMaterials against a successful cached stage");
                wait("pre-retirement Create failure", () => { step(); return flow.Status == BlueprintCaptureStatus.DraftReady; });
                Require(flow.ContentHandle.Equals(handle) && ReferenceEquals(exact, Call(host, "MaterialDraft")),
                    "a real pre-retirement invalid-name Create failure restores the same readable draft and cached bill");

                wait("valid Create admission", () => { step(); return !library.Snapshot.Busy; });
                Call(host, "SetDraftName", "封存时序材料"); Arm("Promote"); Call(host, "ConfirmSave");
                wait("exact Saving promotion gate", () => entered.IsSet);
                Require(flow.Status == BlueprintCaptureStatus.Saving && GetOptional(worker, "materialRequest") == null && GetOptional(worker, "materialReading") == null,
                    "accepted Create has retired its derived reader before real file promotion");
                requests = (long)Get(worker, "MaterialRequests");
                calls = stageCalls;
                for (int i = 0; i < 16; i++) { prepare(); Require(ReferenceEquals(exact, Call(host, "MaterialDraft")), "Saving preserves the same accepted bill after reader retirement"); }
                Require((long)Get(worker, "MaterialRequests") == requests && stageCalls == calls, "Saving Prepare never calls or requeues the retired stage");
                proceed.Set(); pauseKind = null;
                wait("exact Saving completion", () => { step(); return flow.Status == BlueprintCaptureStatus.Saved; });
                Require(flow.ContentHandle.IsEmpty && ((BlueprintMaterialSnapshot)Call(host, "MaterialDraft")).Status == BlueprintMaterialStatus.Pending,
                    "Saved clears old stage admission instead of reviving it from a retained bill");
                Guid saved = flow.SelectedAssetId.Value; LibraryEntry entry = null; BlueprintMaterialSnapshot savedBill = null;
                wait("saved asset bill", () =>
                {
                    step(); entry = library.Snapshot.Page?.Entries.FirstOrDefault(e => e.AssetId == saved);
                    if (entry == null && !library.Snapshot.Busy) library.TryList(9127, 0, saved);
                    if (entry != null) savedBill = (BlueprintMaterialSnapshot)Call(host, "MaterialAsset", entry);
                    return savedBill?.Status == BlueprintMaterialStatus.Exact;
                });
                Require(savedBill.Total == 2 && savedBill.KindCount == 2 && savedBill.Rows.All(r => r.Required == 1),
                    "the saved asset's real content revision/hash yields the same two independent material counts");

                foreach (string stage in new[] { "BeginStage", "SealDraft" })
                {
                    Arm(stage); begin();
                    wait("cancel " + stage + " gate", () => { if (stage != "BeginStage") step(); return entered.IsSet; });
                    Guid cancelled = flow.Token.OperationId;
                    Call(host, "CancelCaptureOrDraft"); requests = (long)Get(worker, "MaterialRequests");
                    for (int i = 0; i < 8; i++) { prepare(); pending(); }
                    Require((long)Get(worker, "MaterialRequests") == requests, "cancelled " + stage + " handle cannot authorize fresh materials while discard is pending");
                    proceed.Set(); pauseKind = null;
                    wait("cancel " + stage + " retirement", () => { step(); return flow.ContentHandle.IsEmpty && !library.Snapshot.Busy; });
                    Require(flow.Status == BlueprintCaptureStatus.Cancelled && cancelled == flow.Token.OperationId,
                        "late completion retires only its cancelled operation and cannot publish a draft");
                }
                begin();
                Require(flow.Token.OperationId != operation, "a new real Save receives a fresh operation rather than the old stage identity");
                wait("new operation exact bill", () => { step(); return flow.Status == BlueprintCaptureStatus.DraftReady && ((BlueprintMaterialSnapshot)Call(host, "MaterialDraft")).Status == BlueprintMaterialStatus.Exact; });
                Require(((BlueprintMaterialSnapshot)Call(host, "MaterialDraft")).Total == 2, "late cancelled results cannot contaminate the new capture's independently exact bill");
                if (graphics != null)
                {
                    Directory.CreateDirectory(output);
                    graphics.Image(Path.Combine(output, "blueprint-draft-materials-sealed.png"), () => Call(ui, "Draw"), Main.UIScaleMatrix, Main.screenWidth, Main.screenHeight);
                }
                wait("cold reader Create admission", () => { step(); return !library.Snapshot.Busy; });
                Call(host, "SetExcludeUnreliable", true); Call(host, "SetDraftName", "材料未就绪仍可保存");
                Arm("MaterialRead"); pausedOperation = flow.ContentHandle.Id;
                Require(((BlueprintMaterialSnapshot)Call(host, "MaterialDraft")).Status == BlueprintMaterialStatus.Pending, "the changed exclusion key starts its real cold material reader");
                wait("cold stage reader gate", () => entered.IsSet);
                Require(GetOptional(worker, "materialReading") != null, "real stage material I/O is in flight when Create is accepted");
                calls = stageCalls; Call(host, "ConfirmSave");
                Require(flow.Status == BlueprintCaptureStatus.Saving && ((BlueprintMaterialSnapshot)Call(host, "MaterialDraft")).Status == BlueprintMaterialStatus.Pending,
                    "an accepted new exclusion key saves without Exact and never borrows the previous key's correct-looking numbers");
                for (int i = 0; i < 8; i++) prepare();
                Require(stageCalls == calls, "Saving never resubmits even while its cold stage reader awaits the accepted mutation");
                Arm("Promote"); wait("cold Saving promotion gate", () => entered.IsSet);
                Require(GetOptional(worker, "materialReading") == null && GetOptional(worker, "materialRequest") == null,
                    "accepted Create releases the genuinely in-flight material reader before promotion");
                for (int i = 0; i < 8; i++) prepare();
                Require(stageCalls == calls && ((BlueprintMaterialSnapshot)Call(host, "MaterialDraft")).Status == BlueprintMaterialStatus.Pending,
                    "retired cold Saving source remains frozen Pending without requests or wrong-key numbers");
                proceed.Set(); pauseKind = null;
                wait("cold Saving completion", () => { step(); return flow.Status == BlueprintCaptureStatus.Saved; });
                Console.WriteLine("PASS Blueprint draft material lifetime " + (graphics == null ? "CPU production consumer" : "actual visible Prepare") +
                    ": warm catalog; real Save Starting/Capturing/summary-Sealing no early requests; same-page exact two-wall bill; stable 120 preparations; pre-retirement Create recovery; Saving reader retirement/no requeue/same-key value; saved asset; Starting/Sealing cancellation and new operation. OS/gameplay/multiplayer/owner acceptance NOT_EXECUTED.");
            }
            catch (Exception error)
            {
                failure = error;
                var liveStage = GetOptional(worker, "stage"); var materialRequest = GetOptional(worker, "materialRequest");
                var materialKey = materialRequest == null ? null : GetOptional(materialRequest, "Key");
                Console.Error.WriteLine("DRAFT material lifetime failure phase=" + phase + " flow=" + flow.Status + " message=" + flow.Message +
                    " operation=" + flow.Token.OperationId + " handle=" + flow.ContentHandle.Id +
                    " workerStage=" + (liveStage == null ? null : GetOptional(liveStage, "Id")) + " sealed=" + (liveStage != null && GetOptional(liveStage, "Root") != null) +
                    " materialId=" + (materialKey == null ? null : GetOptional(materialKey, "Id")) + " reader=" + (GetOptional(worker, "materialReading") != null) +
                    " busy=" + library.Snapshot.Busy + " processed=" + Get(worker, "Processed") + " command=" + Get(host, "captureCommandId") +
                    " pause=" + pauseKind + " entered=" + entered.IsSet + " released=" + proceed.IsSet +
                    " ui=" + Get(ui, "Visible") + " controls=" + Get(host, "ControlsEnabled"));
                throw;
            }
            finally
            {
                proceed.Set(); pauseKind = null;
                try
                {
                    Call(host, "CancelCaptureOrDraft");
                    NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy && flow.ContentHandle.IsEmpty; });
                    Call(shell, "CloseAndSubmitPosition"); editor.Exit(); Call(host, "CancelStaticMaterialViews");
                    Call(host, "SetMaterialViewDemand", false); Call(host, "SetAutoBuild", automatic);
                }
                catch (Exception cleanup)
                {
                    if (failure == null) throw;
                    Console.Error.WriteLine("DRAFT MATERIAL CLEANUP supplemental; primary retained: " + cleanup);
                }
                finally
                {
                    AppDomain.CurrentDomain.FirstChanceException -= observe;
                    foreach (var method in audit.GetPatchedMethods().ToArray()) audit.Unpatch(method, HarmonyPatchType.All, audit.Id);
                    pausedWorker = null; observedLibrary = null; pausedOperation = Guid.Empty; foreach (var signal in signals) signal.Dispose(); signals = null;
                }
            }
        }
        private static void Arm(string kind)
        {
            var previous = proceed; pauseKind = kind;
            // Each paused call owns its captured release. Reusing one event
            // and resetting it could strand the previous worker at handoff.
            entered = new ManualResetEventSlim(); proceed = new ManualResetEventSlim(); signals.Add(entered); signals.Add(proceed);
            previous.Set();
        }
        private static void BeforeCommand(object __instance, object __0)
        {
            if (!ReferenceEquals(__instance, pausedWorker) || Get(__0, "Kind").ToString() != pauseKind) return;
            Pause();
        }
        private static void BeforePromote(Guid __1) { if (pauseKind == "Promote" && __1 == pausedOperation) Pause(); }
        private static void BeforeRequest(object __instance, BlueprintMaterialKey __0)
        { if (ReferenceEquals(__instance, observedLibrary) && __0 != null && __0.Stage && __0.Id == pausedOperation) stageCalls++; }
        private static void BeforeMaterialRead(object __instance, Guid __0, bool __1)
        {
            if (!ReferenceEquals(__instance, pausedWorker) || pauseKind != "MaterialRead" || !__1) return;
            var request = GetOptional(__instance, "materialRequest");
            var key = request == null ? null : (BlueprintMaterialKey)Get(request, "Key"); var live = GetOptional(__instance, "stage");
            if (!tracedMaterialRead)
            {
                tracedMaterialRead = true;
                Console.WriteLine("DRAFT cold ReadChunk logical=" + pausedOperation + " physicalRead=" + __0 +
                    " stagePhysical=" + (live == null ? null : GetOptional(live, "Physical")) + " requestId=" + key?.Id);
            }
            // Seal normalization rewrites the physical run. The request still
            // owns its logical operation; pause only that request's actual
            // current physical source, rather than comparing unrelated IDs.
            if (key?.Stage == true && key.Id == pausedOperation && live != null && (Guid)Get(live, "Id") == pausedOperation &&
                (Guid)Get(live, "Physical") == __0) Pause();
        }
        private static void Pause()
        { var release = proceed; entered.Set(); if (!release.Wait(10000)) throw new TimeoutException("The isolated draft lifecycle gate was not released."); }
    }
}
