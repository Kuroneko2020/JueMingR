using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JueMingR.Features.Blueprint;
using JueMingR.Infrastructure.Blueprint;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ObjectData;
using static NativeWorldTextProbe.NativeInformationChecks;

namespace NativeWorldTextProbe
{
    internal static class NativeBlueprintCaptureBoundaryChecks
    {
        internal static void Run(object context, IBlueprintCaptureSource source)
        {
            Directions(source);
            var host = Get(context, "Blueprint"); var shell = Get(context, "Shell");
            var input = Get(context, "Input"); var editor = (BlueprintCaptureState)Get(host, "State");
            var flow = (BlueprintCaptureFlow)Get(host, "Flow"); var library = (LibraryState)Get(host, "Library");
            var originalTiles = Main.tile; int originalWidth = Main.maxTilesX, originalHeight = Main.maxTilesY;
            ulong tick = 50000;
            Action poll = () => { Call(host, "Update", tick++); Call(host, "Poll"); };
            Action finishCancel = () => { Call(host, "CancelCaptureOrDraft"); NativeQuickItemChecks.Until(() => { poll(); return !library.Snapshot.Busy && !(bool)Get(host, "discardPending"); }); Call(shell, "CloseAndSubmitPosition"); };
            try
            {
                Main.maxTilesX = Main.maxTilesY = 180; Main.tile = new Tile[180, 180];
                for (int x = 0; x < 180; x++) for (int y = 0; y < 180; y++) Main.tile[x, y] = new Tile();
                Action begin = () => { Call(shell, "CloseAndSubmitPosition"); NativeQuickItemChecks.Sample(input, Array.Empty<Keys>()); editor.Enter(); editor.Selection.Clear(); editor.Selection.ToggleRectangle(5, 5, 164, 164, 180, 180); Console.WriteLine("SCALE BEGIN status=" + flow.Status + " canSave=" + Get(host, "CanSave") + " controls=" + Get(host, "ControlsEnabled") + " selection=" + editor.Selection.HasSelection + " inputStart=" + Get(input, "CanStartActions") + " busy=" + library.Snapshot.Busy + " discard=" + Get(host, "discardPending")); Require((bool)Call(host, "Save"), "actual Host accepts >16384 selected cells without a selection cap"); };
                for (int scenario = 0; scenario < 2; scenario++)
                {
                    bool solid = scenario == 0;
                    for (int x = 5; x <= 164; x++) for (int y = 5; y <= 164; y++) { Main.tile[x, y].active(solid); Main.tile[x, y].type = 1; }
                    if (!solid) { Main.tile[5, 5].active(true); Main.tile[164, 164].active(true); }
                    begin(); var job = (CaptureJob)Get(host, "capture");
                    // The real F5 focus-loss route must retire text/pointer work
                    // while the already accepted game-thread read remains alive.
                    Call(shell, "CancelForFocusLoss");
                    Call(shell, "ProcessInput");
                    Require(flow.Status == BlueprintCaptureStatus.Starting && !job.Cancelled, "focus loss does not cancel an accepted Host capture");
                    long maxReads = 0; double maxAdvanceMs = 0; var blocks = new HashSet<long>(); var total = Stopwatch.StartNew();
                    NativeQuickItemChecks.Until(() =>
                    {
                        long before = job.TotalReads; var watch = Stopwatch.StartNew(); poll(); long reads = job.TotalReads - before;
                        maxReads = Math.Max(maxReads, reads); if (reads > 0) maxAdvanceMs = Math.Max(maxAdvanceMs, watch.Elapsed.TotalMilliseconds);
                        Require(reads <= 1024, "every actual Host advance respects its 1024 live tile-read ceiling");
                        var receipt = library.Snapshot.LastReceipt; if (receipt != null && receipt.Kind == LibraryCommandKind.AppendChunk) blocks.Add(receipt.CommandId);
                        return flow.Status == BlueprintCaptureStatus.DraftReady || flow.Status == BlueprintCaptureStatus.Failed;
                    });
                    Require(flow.Status == BlueprintCaptureStatus.DraftReady && job.TotalReads == 25600 && flow.Summary.ContentCount == (solid ? 25600 : 2) && flow.Summary.Width == 160 && flow.Summary.Height == 160, "real Host solid/sparse capture preserves distant bounds and all selected content after focus loss");
                    Require(blocks.Count == (solid ? 13 : 1), "bounded 2048-cell handoff count matches actual content without bbox-dense records");
                    Console.WriteLine("CAPTURE SCALE " + (solid ? "solid" : "sparse") + " selected=25600 content=" + flow.Summary.ContentCount + " liveReads=" + job.TotalReads + " maxReadsPerHostAdvance=" + maxReads + " chunks=" + blocks.Count + " maxObservedAdvanceMs=" + maxAdvanceMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " elapsedMs=" + total.ElapsedMilliseconds + "; finite isolated observation, not FPS or memory-peak acceptance");
                    finishCancel();
                }
                begin(); var cancelled = (CaptureJob)Get(host, "capture");
                NativeQuickItemChecks.Until(() => { poll(); return cancelled.TotalReads > 0; });
                Call(shell, "CloseAndSubmitPosition");
                NativeQuickItemChecks.Until(() => { poll(); return !library.Snapshot.Busy && !(bool)Get(host, "discardPending"); });
                long stoppedReads = cancelled.TotalReads; for (int i = 0; i < 8; i++) poll();
                Require(cancelled.Cancelled && cancelled.TotalReads == stoppedReads, "explicit cancel stops further real-world reads and discards only its private stage");
                editor.Exit();
            }
            finally { Main.tile = originalTiles; Main.maxTilesX = originalWidth; Main.maxTilesY = originalHeight; }
        }
        private static void Directions(IBlueprintCaptureSource source)
        {
            // Fixed .8 metadata for tile 388: one column, five rows, two facing
            // variants. Expected enum values come from the original enum,
            // independently of the codec's accepted range.
            const ushort type = 388; var data = TileObjectData.GetTileData(type, 0);
            Require(data.Width == 1 && data.Height == 5 && data.StyleMultiplier == 2, "locked directional metadata representative");
            string directory = Path.Combine(Terraria.Program.SavePath, "direction-library");
            using (var library = new LibraryState(new FileBlueprintLibrary(directory)))
            for (int facing = 0; facing < 2; facing++)
            {
                int x = 70 + facing * 3, y = 40, offset = 0;
                for (int row = 0; row < 5; row++) { var tile = Main.tile[x, y + row]; tile.active(true); tile.type = type; tile.frameX = 0; tile.frameY = (short)(facing * data.CoordinateFullHeight + offset); offset += data.CoordinateHeights[row] + data.CoordinatePadding; }
                var selection = new BlueprintSelection(); selection.ToggleRectangle(x, y + 4, x, y + 4, 120, 120);
                var token = new CaptureToken(Guid.NewGuid(), 1, 2, 3); var handle = new ContentHandle(token.OperationId);
                Require(Complete(library, library.TryBeginStage(token)).Status == LibraryStatus.Completed, "direction stage begins");
                var job = new CaptureJob(source, selection.Freeze(), 0); int direction = -1;
                for (int step = 0; !job.Complete && job.Error == null && step < 1000; step++)
                {
                    job.Advance(2, 20); var chunk = job.PendingChunk; if (chunk == null) continue;
                    if (chunk.ObjectCount > 0) direction = chunk.Objects[0].Direction;
                    var appended = Complete(library, library.TryAppendChunk(handle, chunk));
                    Console.WriteLine("DIRECTION capture=" + facing + " nativeDirection=" + direction + " append=" + appended.Status + " code=" + appended.FailureCode);
                    Require(appended.Status == LibraryStatus.Completed, "real native directional capture chunk enters stage"); job.AcceptChunk();
                }
                Require(job.Complete && direction == facing + 1, "native left/right preserve exact 1/2 enum values");
                Require(Complete(library, library.TrySealDraft(handle, job.Summary(10))).Status == LibraryStatus.Completed, "directional draft seals");
                Require(Complete(library, library.TryCreate(handle, "方向 " + facing, token)).Status == LibraryStatus.Saved, "both real directional variants reach reliable Saved");
            }
        }
        private static LibraryReceipt Complete(LibraryState library, bool accepted)
        {
            Require(accepted, "isolated directional command accepted"); LibraryReceipt receipt = null;
            NativeQuickItemChecks.Until(() => { library.Pump(); return library.TryTakeReceipt(out receipt); }); return receipt;
        }
    }
}
