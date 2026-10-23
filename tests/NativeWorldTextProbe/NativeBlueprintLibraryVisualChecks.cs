using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JueMingR.Features.Blueprint;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintLibraryVisualChecks
    {
        internal static void Run(object context, ProbeGraphics graphics, string output)
        {
            var host = Get(context, "Blueprint"); var library = (LibraryState)Get(host, "Library");
            var shell = Get(context, "Shell"); var state = Get(shell, "State"); var layout = Get(state, "Layout");
            var renderer = Get(shell, "renderer"); var ui = Get(shell, "BlueprintUi");
            // Build representative entries through the real isolated repository,
            // rather than installing a synthetic page or writing the owner's data.
            for (int i = 0; i < 2; i++)
            {
                var token = new CaptureToken(Guid.NewGuid(), 7, 8, i + 1); var handle = new ContentHandle(token.OperationId);
                Require(library.TryBeginStage(token), "visual private stage accepted"); Complete(library);
                Require(library.TryAppendChunk(handle, new CaptureChunk(new[] { new CaptureCell { X = 60 + i, Y = 80, Layers = CaptureLayer.Wall | CaptureLayer.Wire, WallType = 2, Wires = 15 } }, 1)), "visual actual closed content accepted"); Complete(library);
                Require(library.TrySealDraft(handle, new CaptureSummary { Left = 60 + i, Top = 80, Width = 1, Height = 1, ContentCount = 1 }), "visual fixed draft sealed"); Complete(library);
                Require(library.TryCreate(handle, "蓝图库样本 " + (i + 1), token), "visual actual asset created"); Require(Complete(library).Status == LibraryStatus.Saved, "visual asset reliably published");
            }
            ulong tick = (ulong)Get(host, "latestTick") + 100; Capture(context, 51, 31, ref tick);
            Call(host, "SetDraftName", "箱子外观"); Call(host, "ConfirmSave");
            var flow = (BlueprintCaptureFlow)Get(host, "Flow");
            NativeQuickItemChecks.Until(() => { Call(host, "Update", tick++); Call(host, "Poll"); return flow.Status == BlueprintCaptureStatus.Saved; });
            Terraria.Localization.LanguageManager.Instance.SetLanguage("zh-Hans");
            Main.screenWidth = 1280; Main.screenHeight = 900;
            foreach (float scale in new[] { 1f, 1.5f })
            {
                Main.UIScale = scale; PlayerInput.CacheOriginalScreenDimensions();
                Call(ui, "OpenLibrary"); Call(layout, "SetBlueprintLibraryVisible", true);
                Call(renderer, "RefreshResources"); Call(renderer, "Prepare", state, 1280f, 900f, scale);
                NativeQuickItemChecks.Until(() =>
                {
                    Call(host, "Poll"); Call(ui, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, 900));
                    return library.Snapshot.Page != null && library.Snapshot.Page.TotalCount == 6 && !library.Snapshot.Busy && (int)Get(ui, "PreviewCount") >= Math.Min(4, library.Snapshot.Page.Entries.Count);
                });
                string suffix = scale.ToString(System.Globalization.CultureInfo.InvariantCulture);
                Action draw = () => { Call(renderer, "Draw", state, Main.UIScaleMatrix, false, false); Call(ui, "Draw"); };
                var pixels = graphics.Pixels(draw, Main.UIScaleMatrix, 1280, 900);
                Require(pixels.Any(c => c.A > 0 && c.R > 100 && c.G > 100), "library actual native resource draw produces visible pixels at UI " + suffix);
                graphics.Image(Path.Combine(output, "blueprint-library-" + suffix + ".png"), draw, Main.UIScaleMatrix, 1280, 900);
                var page = library.Snapshot.Page;
                Call(ui, "BeginEdit", page.Entries[0].AssetId, "中文名称编辑");
                Call(ui, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, 900));
                graphics.Image(Path.Combine(output, "blueprint-library-name-" + suffix + ".png"), draw, Main.UIScaleMatrix, 1280, 900);
                Call(ui, "CancelEdit"); Call(shell, "CloseAndSubmitPosition");
            }
            Main.UIScale = 1f; PlayerInput.CacheOriginalScreenDimensions();
            Main.tile[30, 30].active(false); Main.tile[32, 31].wall = 2;
            Capture(context, 32, 31, ref tick, 31);
            Require(flow.Summary.UnreliableObjectCount == 1, "draft visual contains a real incomplete object with confirmed wall");
            Call(layout, "SetBlueprintLibraryVisible", true); Call(renderer, "RefreshResources"); Call(renderer, "Prepare", state, 1280f, 900f, 1f);
            PreviewPairing(host, ui, library, flow);
            graphics.Image(Path.Combine(output, "blueprint-draft-unconfirmed.png"), () => { Call(renderer, "Draw", state, Main.UIScaleMatrix, false, false); Call(ui, "Draw"); }, Main.UIScaleMatrix, 1280, 900);
            Call(host, "CancelCaptureOrDraft");
            NativeQuickItemChecks.Until(() => { Call(host, "Update", tick++); Call(host, "Poll"); return !library.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
            Main.tile[32, 31].wall = 0; Capture(context, 32, 31, ref tick, 31); Call(host, "SetExcludeUnreliable", true);
            Call(layout, "SetBlueprintLibraryVisible", true); Call(renderer, "RefreshResources"); Call(renderer, "Prepare", state, 1280f, 900f, 1f);
            var failures = (IDictionary<string, CaptureSummary>)Get(ui, "failedSummaries"); CaptureSummary empty = null;
            NativeQuickItemChecks.Until(() => { Call(host, "Poll"); Call(ui, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, 900)); return !library.Snapshot.Busy && failures.TryGetValue((string)Call(ui, "DraftKey"), out empty); });
            Require(empty.ContentCount == 0 && flow.Summary.ContentCount == 2, "all-excluded empty projection retains its keyed zero summary without mutating the original draft");
            foreach (object part in (IEnumerable)Get(ui, "parts")) if (Get(part, "Command").ToString() == "ConfirmDraft") Require(!(bool)Get(part, "Enabled"), "empty filtered draft cannot be confirmed");
            graphics.Image(Path.Combine(output, "blueprint-draft-empty-excluded.png"), () => { Call(renderer, "Draw", state, Main.UIScaleMatrix, false, false); Call(ui, "Draw"); }, Main.UIScaleMatrix, 1280, 900);
            Call(shell, "CloseAndSubmitPosition"); NativeQuickItemChecks.Until(() => { Call(host, "Poll"); return !library.Snapshot.Busy; });
            Require((int)Get(ui, "PreviewCount") == 0 && (long)Get(ui, "PreviewBytes") == 0, "UI exit disposes its finite preview textures");
            Console.WriteLine("PASS Blueprint actual library cards/content previews/disabled operations/Chinese name buffer at 100/150% and resource exit. Offscreen native assets; OS IME/owner/multiplayer NOT_EXECUTED.");
        }
        private static LibraryReceipt Complete(LibraryState library)
        {
            LibraryReceipt result = null;
            NativeQuickItemChecks.Until(() => { library.Pump(); return library.TryTakeReceipt(out result); });
            Require(result.Status == LibraryStatus.Completed || result.Status == LibraryStatus.Saved, "visual private repository operation: " + result.Error); return result;
        }
        private static void PreviewPairing(object host, object ui, LibraryState library, BlueprintCaptureFlow flow)
        {
            var cache = Get(ui, "previews"); var original = flow.Summary;
            Func<CaptureSummary> summary = () => (CaptureSummary)Call(cache, "Summary", Call(ui, "DraftKey"));
            Action prepare = () => { Call(host, "Poll"); Call(ui, "Prepare", true, Main.UIScaleMatrix, new Vector2(1280, 900)); };
            NativeQuickItemChecks.Until(() => { prepare(); return !library.Snapshot.Busy && summary() != null; });
            var a = summary(); Require(a.ContentCount == 2 && a.Width == 2, "A preview has both real selected object tiles");
            Call(host, "SetExcludeUnreliable", true);
            NativeQuickItemChecks.Until(() => { prepare(); return !library.Snapshot.Busy && summary() != null; });
            Require(summary().ContentCount == 1 && summary().Width == 1 && original.ContentCount == 2, "B preview excludes only uncertain foreground and keeps confirmed wall with recomputed bounds");
            long uploads = (long)Get(cache, "Uploads"); long receipt = library.Snapshot.LastReceipt.CommandId;
            Call(host, "SetExcludeUnreliable", false); prepare(); prepare();
            Require(ReferenceEquals(summary(), a) && (long)Get(cache, "Uploads") == uploads && library.Snapshot.LastReceipt.CommandId == receipt && ReferenceEquals(flow.Summary, original), "A-B-A cache revisit restores its paired summary without world or file reread or stale B facts");

            // Discard derived GPU state, then accept read A and change to B
            // before pumping its receipt. That old receipt may be cached under
            // A, but must not become B's summary or mutate the sealed facts.
            Call(ui, "LoseFocus"); Call(ui, "ShowCapture");
            NativeQuickItemChecks.Until(() => { prepare(); return GetOptional(ui, "pendingPreviewKey") != null; });
            Call(host, "SetExcludeUnreliable", true);
            NativeQuickItemChecks.Until(() => { prepare(); return !library.Snapshot.Busy && summary() != null; });
            Require(summary().Width == 1 && summary().ContentCount == 1 && ReferenceEquals(flow.Summary, original) && original.Width == 2, "late A result cannot replace current B summary or original fixed capture");
            Call(host, "SetExcludeUnreliable", false); prepare();
            Require(summary().Width == 2 && summary().ContentCount == 2, "the late A cache remains correctly paired when explicitly revisited");
            Console.WriteLine("PASS Blueprint actual GPU preview A-B-A cache summary pairing and old-combination late receipt; fixed source untouched.");
        }
        private static void Capture(object context, int x, int y, ref ulong tick, int left = -1)
        {
            var host = Get(context, "Blueprint"); var input = Get(context, "Input"); var shell = Get(context, "Shell");
            Call(shell, "CloseAndSubmitPosition"); NativeQuickItemChecks.Sample(input, Array.Empty<Microsoft.Xna.Framework.Input.Keys>());
            var editor = (BlueprintCaptureState)Get(host, "State"); var flow = (BlueprintCaptureFlow)Get(host, "Flow");
            editor.Enter(); editor.Selection.ToggleRectangle(left < 0 ? x : left, y, x, y, 120, 120);
            Require((bool)Call(host, "Save"), "visual real capture accepted");
            ulong counter = tick;
            NativeQuickItemChecks.Until(() => { Call(host, "Update", counter++); Call(host, "Poll"); return flow.Status == BlueprintCaptureStatus.DraftReady || flow.Status == BlueprintCaptureStatus.Failed; });
            tick = counter; var receipt = ((LibraryState)Get(host, "Library")).Snapshot.LastReceipt;
            Require(flow.Status == BlueprintCaptureStatus.DraftReady, "visual real capture completed: " + flow.Message + " / " + receipt?.Kind + " / " + receipt?.FailureCode);
        }
    }
}
