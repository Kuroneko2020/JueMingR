using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintExchangeLifecycleChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static void Run(object context)
        { RunCore(context, null, null); }
        internal static void Visual(object context, ProbeGraphics graphics, string output)
        { RunCore(context, graphics, output); }
        private static void RunCore(object context, ProbeGraphics graphics, string output)
        {
            object host = Get(context, "Blueprint"), input = Get(context, "Input"), shell = Get(context, "Shell"), ui = Get(shell, "BlueprintUi");
            object state = Get(shell, "State"); bool focused = true, priorSelected = FocusHelper.IsSelectedApplication;
            Vector2 pointer = Vector2.Zero;
            int oldWidth = Main.screenWidth, oldHeight = Main.screenHeight; float oldScale = Main.UIScale;
            var oldLibrary = (LibraryState)Get(host, "Library"); object oldDialog = Get(host, "exchangeDialog"), oldWindow = Get(input, "gameWindow"), oldForeground = Get(input, "foregroundWindow");
            Call(shell, "CloseAndSubmitPosition"); Call(host, "CancelCaptureOrDraft"); Call(host, "ClearProjection");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !oldLibrary.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
            ((BlueprintCaptureState)Get(host, "State")).Exit();
            string area = Path.Combine(Terraria.Program.SavePath, graphics == null ? "exchange-lifecycle" : "exchange-lifecycle-visual"); Directory.CreateDirectory(area); string source = Path.Combine(area, "source.jmbp");
            string seedRoot = Path.Combine(area, "seed");
            using (var seed = new LibraryState(new FileBlueprintLibrary(seedRoot)))
            {
                var identity = new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "fixture", 1, 1);
                var window = new BlueprintProjectionWindow(identity, 1, new CaptureSummary { Width = 1, Height = 1, ContentCount = 1 }, new BlueprintProjectionBounds(0, 0, 1, 1), new[] { new CaptureCell { Layers = CaptureLayer.Tile, TileType = 1 } }, Array.Empty<CaptureObject>());
                var entry = NativeBlueprintProjectionVisualChecks.Save(seed, window); string content = Path.Combine(seedRoot, entry.AssetId.ToString("N"), "content");
                byte[] root = File.ReadAllBytes(Path.Combine(content, "root.bin")), index = File.ReadAllBytes(Path.Combine(content, "index.bin"));
                string chunk = Directory.EnumerateFiles(Path.Combine(content, "chunks"), "*.bin", SearchOption.AllDirectories).Single();
                using (var memory = new MemoryStream()) using (var writer = new BinaryWriter(memory, Encoding.UTF8, true))
                {
                    writer.Write(new byte[] { 0x4A, 0x4D, 0x52, 0x4A, 0x4D, 0x42, 0x50, 0 }); writer.Write(1); byte[] name = Encoding.UTF8.GetBytes(entry.Name); writer.Write((uint)name.Length); writer.Write(name); writer.Write((uint)root.Length); writer.Write(root); writer.Write((long)index.Length); writer.Write(index); byte[] bytes = File.ReadAllBytes(chunk); writer.Write((uint)bytes.Length); writer.Write(bytes); writer.Flush(); using (var hash = SHA256.Create()) writer.Write(hash.ComputeHash(memory.ToArray())); writer.Flush(); File.WriteAllBytes(source, memory.ToArray());
                }
            }
            var files = new HeldSource();
            var library = new LibraryState(new FileBlueprintLibrary(Path.Combine(area, "templates"))); library.ConfigureExchange(files.Io, new AtomicFileDocument(Path.Combine(area, "counter.json"), 65536, true));
            var dialog = new Dialog(source); host.GetType().GetField("<Library>k__BackingField", F).SetValue(host, library); Set(host, "exchangeDialog", dialog);
            Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(3))); Set(input, "foregroundWindow", (Func<IntPtr>)(() => focused ? new IntPtr(3) : new IntPtr(4)));
            try
            {
                Main.screenWidth = 960; Main.screenHeight = 760; Main.UIScale = 1; PlayerInput.CacheOriginalScreenDimensions();
                Action<Keys[], int> frame = (keys, mask) =>
                {
                    FocusHelper.IsSelectedApplication = focused;
                    Call(input, "BeginUpdate");
                    if (!(bool)Get(input, "IsFocused")) Call(shell, "CancelForFocusLoss");
                    Call(shell, "BeforeInput");
                    PlayerInput.MouseInfo = new MouseState((int)pointer.X, (int)pointer.Y, 0, (mask & 1) != 0 ? ButtonState.Pressed : ButtonState.Released,
                        (mask & 4) != 0 ? ButtonState.Pressed : ButtonState.Released, (mask & 2) != 0 ? ButtonState.Pressed : ButtonState.Released,
                        (mask & 8) != 0 ? ButtonState.Pressed : ButtonState.Released, (mask & 16) != 0 ? ButtonState.Pressed : ButtonState.Released);
                    PlayerInput.ScrollWheelDelta = PlayerInput.ScrollWheelDeltaForUI = 0;
                    PlayerInput.Triggers.Reset(); PlayerInput.Triggers.Current.Up = true;
                    Call(input, "AfterMapping"); Main.keyState = new KeyboardState(keys); Call(input, "AfterKeyboardRefresh");
                    Call(shell, "ProcessInput"); Call(context, "UpdateRuntime");
                    // CPU has no graphics device. It executes the actual entire
                    // unavailable cleanup path, but cannot certify active GPU
                    // preparation/drawing. Visual executes those same consumers
                    // on every frame, without replacing Ready or render methods.
                    bool unavailable = !(bool)Get(shell, "CanPresentNow") || (bool)Get(input, "ExternalModalBusy") ||
                        (bool)Get(ui, "TemporarilySuspended") && !(bool)Get(input, "CanUseInput");
                    if (graphics != null || unavailable)
                    {
                        Call(context, "UpdateShell"); Call(shell, "BeginPointerLayer");
                        if (graphics != null) graphics.Render(() => Call(shell, "DrawLayer"), Main.UIScaleMatrix);
                        else Call(shell, "DrawLayer");
                        Call(shell, "EndPointerLayer");
                    }
                    Require(!(bool)Get(shell, "Failed"), "complete Host/Shell consumer frame stays healthy");
                };
                Action step = () => frame(Array.Empty<Keys>(), 0);
                Action neutral = () => { step(); step(); };
                Action open = () =>
                {
                    focused = true; neutral(); Call(ui, "OpenLibrary");
                    NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy; });
                    Call(host, "BeginImport"); Require(dialog.Request != null, "real Host admits this exact import and acquires its native modal");
                };
                Action complete = () => NativeQuickItemChecks.Until(() => { step(); return !(bool)Get(host, "ExchangeBusy"); });
                open(); long presentation = (long)Get(ui, "ExchangePresentationGeneration"); focused = false; step();
                long read = (long)Get(ui, "readGeneration"); step(); step();
                Require(!(bool)Get(state, "Visible") && (long)Get(ui, "ExchangePresentationGeneration") == presentation && (long)Get(ui, "readGeneration") == read,
                    "actual lost focus survives AfterUpdate/pointer/draw cleanup without retiring intent or repeating read requests");
                dialog.Select(); complete();
                Require(library.TryList(901, 0), "completed import frees ordinary slot"); var page = NativeBlueprintLibraryWait.AcceptedList(library, "exchange import result").Page;
                Require(page.TotalCount == 1, "real import published one healthy asset: " + GetOptional(host, "projectionFeedback")); Guid imported = page.Entries.Single().AssetId;
                focused = true; frame(new[] { Keys.A }, 0);
                Require(!(bool)Get(state, "Visible") && !(bool)Get(input, "CanStartActions"), "successful import waits for actual held-key release after refocus");
                for (int button = 0; button < 5; button++)
                { frame(Array.Empty<Keys>(), 1 << button); Require(!(bool)Get(state, "Visible") && (bool)Get(input, "ExternalModalBusy"), "Mouse" + (button + 1) + " tail cannot reveal or admit new actions"); }
                step(); Require(!(bool)Get(state, "Visible") && !(bool)Get(input, "CanStartActions") && !PlayerInput.Triggers.Current.Up,
                    "real neutral sample itself remains consumed and does not reopen");
                step(); AssertSelected(state, ui, host, imported, "success before neutral reveals exact asset at next legal sample");
                neutral(); AssertSelected(state, ui, host, imported, "completed return remains stable across later complete frames");
                if (graphics != null) graphics.Image(Path.Combine(output, "exchange-return-success.png"), () => Call(shell, "DrawLayer"), Main.UIScaleMatrix, 960, 760);
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy; });
                Require(library.TryRename(imported, page.Entries.Single().MetadataIdentity, "保留的本地名称"), "fixture reliably renames the already imported local asset");
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy; });

                // Hold the real external OpenSource call, not a success mock:
                // the modal returns and its neutral tail finishes before IO.
                files.Hold(); open(); step();
                Require((bool)Get(input, "IsFocused") && !(bool)Get(state, "Visible"), "focused modal takes actual controls down without an OS focus-loss sample");
                dialog.Select(); step(); NativeQuickItemChecks.Until(() => files.Entered.IsSet);
                neutral(); Require((bool)Get(input, "CanStartActions") && (bool)Get(host, "ExchangeBusy") && !(bool)Get(state, "Visible"), "neutral can finish while real source IO remains held");
                files.Release(); complete(); step(); AssertSelected(state, ui, host, imported, "healthy duplicate after neutral returns to the original exact asset");
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy; });
                Require(library.TryList(902, 0), "duplicate leaves the ordinary list available");
                var duplicate = NativeBlueprintLibraryWait.AcceptedList(library, "healthy duplicate local name").Page;
                Require(duplicate.TotalCount == 1 && duplicate.Entries.Single().Name == "保留的本地名称", "healthy duplicate preserves local name and does not add an asset");
                foreach (bool failure in new[] { false, true })
                {
                    open(); step(); dialog.End(failure); complete(); neutral();
                    Require(!(bool)Get(state, "Visible"), "cancelled/failed import never opens library through later Shell consumers");
                }
                // With no intervening shell frame, success can already be
                // queued on a still-visible page. Its actual F5 close wins.
                open(); dialog.Select();
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !(bool)Get(host, "ExchangeBusy"); });
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
                frame(new[] { Keys.F5 }, 0); step();
                Require(!(bool)Get(state, "Visible") && !(bool)Get(ui, "Submenu"), "same-frame actual F5 close retires a queued successful result");
                var otherWindow = new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "fixture-other", 1, 2), 1,
                    new CaptureSummary { Width = 1, Height = 1, ContentCount = 1 }, new BlueprintProjectionBounds(0, 0, 1, 1), new[] { new CaptureCell { Layers = CaptureLayer.Tile, TileType = 2 } }, Array.Empty<CaptureObject>());
                var other = NativeBlueprintProjectionVisualChecks.Save(library, otherWindow);
                // The next-page counterexample must have a real second page,
                // otherwise the worker correctly clamps it back to zero.
                for (int tile = 3; tile <= 7; tile++)
                    NativeBlueprintProjectionVisualChecks.Save(library, new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "fixture-" + tile, 1, tile), 1,
                        new CaptureSummary { Width = 1, Height = 1, ContentCount = 1 }, new BlueprintProjectionBounds(0, 0, 1, 1), new[] { new CaptureCell { Layers = CaptureLayer.Tile, TileType = (ushort)tile } }, Array.Empty<CaptureObject>()));
                foreach (string intent in new[] { "F5", "Back", "Card", "Name", "Page", "PlacedList", "Session" })
                {
                    files.Hold(); open(); step(); dialog.Select(); step(); NativeQuickItemChecks.Until(() => files.Entered.IsSet); neutral();
                    frame(new[] { Keys.F5 }, 0); step();
                    Require((bool)Get(state, "Visible"), "real F5 reopens controls while selected-file IO is still running");
                    // Select a real visible page for the other-card gesture.
                    // This is read preparation; it does not perform navigation.
                    if (intent == "Card" || intent == "Name") Call(ui, "RequestList", 0, (Guid?)other.AssetId);
                    if (graphics != null) NativeQuickItemChecks.Until(() => { step(); return !(bool)Get(ui, "dirty") && (bool)Get(ui, "ready") && !library.Snapshot.Busy; });
                    long frozen = (long)Get(ui, "ExchangePresentationGeneration");
                    Guid sentinel = Guid.NewGuid(); ((BlueprintCaptureFlow)Get(host, "Flow")).SelectedAssetId = sentinel;
                    if (intent == "F5") frame(new[] { Keys.F5 }, 0);
                    else if (intent == "Back" || intent == "Card" || intent == "Name" || intent == "Page")
                    {
                        string command = intent == "Page" ? "Next" : intent;
                        var partType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintLibraryPart", true);
                        // Name editing is intentionally disabled during a live
                        // exchange. CPU exercises its owner-command retirement;
                        // do not invent an enabled native hit for that case.
                        if (graphics == null || intent == "Name")
                        {
                            object part = Activator.CreateInstance(partType, true); Set(part, "AssetId", other.AssetId);
                            Set(part, "Command", Enum.Parse(partType.GetField("Command", F).FieldType, command)); Call(ui, "Execute", part);
                        }
                        else
                        {
                            object part = ((IEnumerable)Get(ui, "parts")).Cast<object>().First(p => Get(p, "Command").ToString() == command && (bool)Get(p, "Enabled") &&
                                (intent != "Card" && intent != "Name" || (Guid)Get(p, "AssetId") == other.AssetId));
                            object rect = Get(Get(part, "Element"), "Rect"), view = Get(ui, "view");
                            pointer = new Vector2((float)Get(view, "X") + (float)Get(rect, "X") + (intent == "Card" ? 5 : (float)Get(rect, "Width") / 2),
                                (float)Get(view, "Y") + (float)Get(rect, "Y") + (float)Get(rect, "Height") / 2 - ((bool)Get(part, "Fixed") ? 0 : (float)Get(state, "Scroll")));
                            step(); frame(Array.Empty<Keys>(), 1); step(); pointer = Vector2.Zero;
                        }
                    }
                    else if (intent == "PlacedList") Call(host, "OpenPlacedList");
                    else if (intent == "Session")
                    {
                        Main.gameMenu = true; step(); Main.gameMenu = false; step();
                        NativeQuickItemChecks.Until(() => { Call(context, "UpdateRuntime"); return (bool)Get(host, "HasSession"); });
                    }
                    Require((long)Get(ui, "ExchangePresentationGeneration") > frozen, "later " + intent + " retires the frozen display intent");
                    files.Release(); complete(); step();
                    if (intent == "Card" || intent == "Name") Require((Guid)Get(ui, "selected") == other.AssetId, "late imported receipt cannot steal later " + intent);
                    else if (intent == "Page") Require((int)Get(ui, "requestedPage") == 1, "late imported receipt cannot override later page");
                    else Require(!(bool)Get(ui, "Submenu"), "late receipt cannot recreate retired library after " + intent);
                    Require(((BlueprintCaptureFlow)Get(host, "Flow")).SelectedAssetId != imported, "late success cannot overwrite newer world selection after " + intent);
                }
                Console.WriteLine("PASS exchange Host lifecycle (" + (graphics == null ? "CPU unavailable Shell consumers and direct view commands; active GPU path separate" : "complete Shell update/pointer/real Draw and enabled view clicks; Name owner command only") + "): observed focus loss, focused modal, both real-IO/neutral orders, success/healthy duplicate/local name, all Mouse1-5 tails, cancel/failure, actual F5 and newer Back/Card/Name/Page/PlacedList/session intent.");
            }
            finally
            {
                files.Release(); Call(ui, "Suspend"); FocusHelper.IsSelectedApplication = true;
                if (Get(input, "ExternalModalState").ToString() == "ModalOwned") Call(input, "ReturnExternalModal", (Guid)Get(input, "externalModalRequest"));
                NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
                Call(shell, "CloseAndSubmitPosition");
                Set(host, "exchangeDialog", oldDialog); host.GetType().GetField("<Library>k__BackingField", F).SetValue(host, oldLibrary); Set(input, "gameWindow", oldWindow); Set(input, "foregroundWindow", oldForeground); library.Dispose(); files.Dispose();
                FocusHelper.IsSelectedApplication = priorSelected; Main.screenWidth = oldWidth; Main.screenHeight = oldHeight; Main.UIScale = oldScale; PlayerInput.CacheOriginalScreenDimensions();
            }
        }
        private static void AssertSelected(object state, object ui, object host, Guid asset, string reason)
        { Require((bool)Get(state, "Visible") && ((BlueprintCaptureFlow)Get(host, "Flow")).SelectedAssetId == asset && (Guid?)GetOptional(ui, "selected") == asset, reason); }
        private sealed class HeldSource : IDisposable
        {
            internal readonly ManualResetEventSlim Entered = new ManualResetEventSlim();
            private readonly ManualResetEventSlim allowed = new ManualResetEventSlim(true);
            private volatile bool held;
            internal readonly IBlueprintExchangeIo Io;
            internal HeldSource()
            {
                Func<string, FileMode, FileAccess, FileShare, Stream> open = (path, mode, access, share) =>
                { if (held) { Entered.Set(); allowed.Wait(); } return new FileStream(path, mode, access, share, 65536, FileOptions.SequentialScan); };
                Io = (IBlueprintExchangeIo)Activator.CreateInstance(typeof(FileBlueprintExchange), F, null, new object[] { open }, null);
            }
            internal void Hold() { Entered.Reset(); allowed.Reset(); held = true; }
            internal void Release() { held = false; allowed.Set(); }
            public void Dispose() { Release(); Entered.Dispose(); allowed.Dispose(); }
        }
        private sealed class Dialog : IBlueprintFileDialog
        {
            private readonly string source; private BlueprintFileDialogResult result; internal BlueprintFileDialogRequest Request;
            internal Dialog(string source) { this.source = source; }
            public bool TryShow(BlueprintFileDialogRequest request) { if (Request != null) return false; Request = request; return true; }
            internal void Select()
            {
                var ticket = (IBlueprintExchangeTicket)typeof(WindowsBlueprintFileTicket).GetMethod("Source", F).Invoke(null, new object[] { Request.RequestId, source });
                result = new BlueprintFileDialogResult(Request, BlueprintFileDialogOutcome.Selected, ticket, 0, null);
            }
            public bool TryTake(out BlueprintFileDialogResult value) { value = result; if (value == null) return false; result = null; Request = null; return true; }
            internal void End(bool failure) { result = new BlueprintFileDialogResult(Request, failure ? BlueprintFileDialogOutcome.Failed : BlueprintFileDialogOutcome.Cancelled, null, 0, failure ? "fixture" : null); }
            public void RequestClose(Guid id) { if (Request != null && Request.RequestId == id) result = new BlueprintFileDialogResult(Request, BlueprintFileDialogOutcome.Cancelled, null, 0, null); }
            public void BeginStop() { }
        }
    }
}
