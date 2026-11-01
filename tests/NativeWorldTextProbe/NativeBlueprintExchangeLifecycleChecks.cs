using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Blueprint;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintExchangeLifecycleChecks
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        internal static void Run(object context)
        {
            object host = Get(context, "Blueprint"), input = Get(context, "Input"), shell = Get(context, "Shell"), ui = Get(shell, "BlueprintUi");
            var oldLibrary = (LibraryState)Get(host, "Library"); object oldDialog = Get(host, "exchangeDialog"), oldWindow = Get(input, "gameWindow"), oldForeground = Get(input, "foregroundWindow");
            Call(shell, "CloseAndSubmitPosition"); Call(host, "CancelCaptureOrDraft"); Call(host, "ClearProjection");
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !oldLibrary.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
            ((BlueprintCaptureState)Get(host, "State")).Exit();
            string area = Path.Combine(Terraria.Program.SavePath, "exchange-lifecycle"); Directory.CreateDirectory(area); string source = Path.Combine(area, "source.jmbp");
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
            var library = new LibraryState(new FileBlueprintLibrary(Path.Combine(area, "templates"))); library.ConfigureExchange(new FileBlueprintExchange(), new AtomicFileDocument(Path.Combine(area, "counter.json"), 65536, true));
            var dialog = new Dialog(source); host.GetType().GetField("<Library>k__BackingField", F).SetValue(host, library); Set(host, "exchangeDialog", dialog);
            Set(input, "gameWindow", (Func<IntPtr>)(() => new IntPtr(3))); Set(input, "foregroundWindow", (Func<IntPtr>)(() => new IntPtr(3)));
            try
            {
                Action neutral = () => { NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); };
                neutral(); Call(ui, "OpenLibrary"); Call(host, "BeginImport"); Require(dialog.Request != null, "real Host opens exactly one fake mechanical dialog");
                long presentation = (long)Get(ui, "ExchangePresentationGeneration"); Call(shell, "CancelForFocusLoss");
                Require(!(bool)Get(Get(shell, "State"), "Visible") && (long)Get(ui, "ExchangePresentationGeneration") == presentation, "native focus-loss hides without retiring this exact library intent");
                dialog.Select(); NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !(bool)Get(host, "ExchangeBusy"); });
                Require(library.TryList(901, 0), "completed import frees ordinary slot"); var page = NativeBlueprintLibraryWait.AcceptedList(library, "exchange import result").Page;
                Require(page.TotalCount == 1, "real import published one healthy asset: " + GetOptional(host, "projectionFeedback")); Guid imported = page.Entries.Single().AssetId;
                Action frame = () => Call(ui, "ProcessInput", true, Main.keyState, Vector2.Zero, false, true, false);
                NativeQuickItemChecks.Sample(input, new[] { Keys.A }); frame();
                Require(!(bool)Get(Get(shell, "State"), "Visible"), "successful import waits for trustworthy real release before opening");
                neutral(); frame();
                Require((bool)Get(Get(shell, "State"), "Visible") && ((BlueprintCaptureFlow)Get(host, "Flow")).SelectedAssetId == imported && (Guid)Get(ui, "selected") == imported, "real successful import automatically opens library and locates its asset after neutral");
                neutral(); Call(host, "BeginImport"); Call(shell, "CancelForFocusLoss"); dialog.Select();
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !(bool)Get(host, "ExchangeBusy"); }); neutral(); frame();
                Require((bool)Get(Get(shell, "State"), "Visible") && (Guid)Get(ui, "selected") == imported, "healthy duplicate import also opens library at the original healthy asset");
                foreach (bool failure in new[] { false, true })
                {
                    neutral(); Call(host, "BeginImport"); Call(shell, "CancelForFocusLoss"); dialog.End(failure);
                    NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !(bool)Get(host, "ExchangeBusy"); }); neutral(); frame();
                    Require(!(bool)Get(Get(shell, "State"), "Visible"), "cancelled/failed import never opens library");
                    Call(ui, "OpenLibrary");
                }
                // A success queued while visible cannot undo State.Update's
                // explicit F5 close before this presentation gets its turn.
                neutral(); Call(host, "BeginImport"); dialog.Select();
                NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !(bool)Get(host, "ExchangeBusy"); });
                Call(Get(shell, "State"), "Close"); neutral(); frame();
                Require(!(bool)Get(Get(shell, "State"), "Visible") && !(bool)Get(ui, "Submenu"), "same-frame explicit close retires a queued success instead of reopening F5");
                neutral(); Call(ui, "OpenLibrary"); Call(host, "BeginImport"); Require(dialog.Request != null, "second import starts after trustworthy neutral");
                Call(ui, "Suspend"); Guid sentinel = Guid.NewGuid(); ((BlueprintCaptureFlow)Get(host, "Flow")).SelectedAssetId = sentinel;
                dialog.Select(); NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !(bool)Get(host, "ExchangeBusy"); });
                Require(((BlueprintCaptureFlow)Get(host, "Flow")).SelectedAssetId == sentinel, "explicitly retired UI generation cannot be stolen by healthy duplicate receipt");
                neutral();
                var otherWindow = new BlueprintProjectionWindow(new BlueprintProjectionIdentity(Guid.NewGuid(), Guid.NewGuid(), "fixture-other", 1, 2), 1,
                    new CaptureSummary { Width = 1, Height = 1, ContentCount = 1 }, new BlueprintProjectionBounds(0, 0, 1, 1), new[] { new CaptureCell { Layers = CaptureLayer.Tile, TileType = 2 } }, Array.Empty<CaptureObject>());
                var other = NativeBlueprintProjectionVisualChecks.Save(library, otherWindow);
                foreach (string intent in new[] { "Card", "Name", "Page" })
                {
                    neutral(); Call(ui, "OpenLibrary"); Call(host, "BeginImport"); Require(dialog.Request != null, "new import starts for later " + intent + " intent");
                    long frozen = (long)Get(ui, "ExchangePresentationGeneration"); dialog.Select(); Call(host, "PumpExchange"); neutral();
                    if (intent == "Page") Call(ui, "NavigatePage", 1);
                    else
                    {
                        var partType = host.GetType().Assembly.GetType("JueMingR.TerrariaHost.F5.BlueprintLibraryPart", true);
                        object part = Activator.CreateInstance(partType, true); Set(part, "AssetId", other.AssetId);
                        Set(part, "Command", Enum.Parse(partType.GetField("Command", F).FieldType, intent)); Call(ui, "Execute", part);
                    }
                    Require((long)Get(ui, "ExchangePresentationGeneration") > frozen, "later " + intent + " retires the frozen display intent");
                    NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !(bool)Get(host, "ExchangeBusy"); });
                    frame();
                    Require(intent == "Page" ? (int)Get(ui, "requestedPage") == 1 : (Guid)Get(ui, "selected") == other.AssetId, "late imported receipt cannot steal later " + intent);
                }
                Console.WriteLine("PASS exchange Host lifecycle: real focus loss -> selected ticket -> streaming import; success and healthy duplicate reveal exact asset only after trustworthy neutral; cancel/failure/explicit close/new Card/Name/Page intent do not steal UI.");
            }
            finally
            {
                Call(ui, "Suspend"); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>());
                Set(host, "exchangeDialog", oldDialog); host.GetType().GetField("<Library>k__BackingField", F).SetValue(host, oldLibrary); Set(input, "gameWindow", oldWindow); Set(input, "foregroundWindow", oldForeground); library.Dispose();
            }
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
