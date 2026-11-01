using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using JueMingR.Platform.Blueprint;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Blueprint
{
    internal sealed partial class LibraryWorker
    {
        private IEnumerator<bool> previewReading;
        private LibraryCommand previewCommand;
        private LibraryReceipt previewReceipt;
        private bool previewWasTurn;
        private void BackgroundStep()
        {
            bool other;
            lock (gate) other = ProjectionHasWorkLocked() || MaterialHasWorkLocked() || ExchangeHasWorkLocked();
            if (previewReading != null && (!other || !previewWasTurn)) { previewWasTurn = true; PreviewStep(); }
            else { previewWasTurn = false; if (ExchangeTurn()) ExchangeStep(); else if (MaterialTurn()) MaterialStep(); else ProjectionStep(); }
        }
        private void PreviewStep()
        {
            try { if (previewReading.MoveNext()) return; }
            catch (OperationCanceledException) { previewReceipt = Receipt(previewCommand, LibraryStatus.Cancelled, "读取已取消"); }
            catch (Exception error)
            {
                protectedAssets.Add(previewCommand.Id);
                previewReceipt = Receipt(previewCommand, LibraryStatus.Protected, Reason(error), true, failureCode: Code(error));
            }
            finally { files.CloseMetadata(); }
            previewReading.Dispose(); previewReading = null; previewCommand = null;
            lock (gate) { result = previewReceipt; previewReceipt = null; }
        }
        // Normal templates have no draft exclusions, so their signed root
        // summary is the preview extent. The same semantic body validator and
        // builder run once, yielding between bounded index/body slices. No new
        // worker, command slot, decoded cache or instance overlay is involved.
        private IEnumerable<bool> ReadTemplatePreview(LibraryCommand command)
        {
            CheckRead(command);
            if (!BlueprintPreviewBudget.IsValid(command.Width, command.Height, false))
            { previewReceipt = Receipt(command, LibraryStatus.Failed, "预览尺寸无效"); yield break; }
            LibraryRoot root;
            PreferenceReadResult read = files.OpenMetadata(command.Id);
            try
            {
                if (read.Status != PreferenceReadStatus.Loaded)
                { previewReceipt = Receipt(command, LibraryStatus.Protected, "蓝图不可读取", true); yield break; }
                var metadata = LibraryCodec.DecodeMetadata(read.Contents, command.Id);
                if (metadata.Deleted || metadata.ContentHash != command.Identity)
                { previewReceipt = Receipt(command, LibraryStatus.Protected, "蓝图内容已改变", true); yield break; }
                root = ReadAssetRoot(metadata);
            }
            finally { files.CloseMetadata(); }
            var summary = root.Summary;
            if (summary.ContentCount == 0) { previewReceipt = Receipt(command, LibraryStatus.Failed, "没有可预览的内容", summary: summary); yield break; }
            var layout = files as IBlueprintLibraryLayoutSteps;
            if (layout != null) foreach (int ignored in layout.ValidateLayoutSteps(command.Id, false, root.BlockCount, 32)) { CheckRead(command); yield return true; }
            else { files.ValidateLayout(command.Id, false, root.BlockCount); yield return true; }
            var builder = new BlueprintPreviewBuilder(summary.Width, summary.Height, command.Width, command.Height, false);
            var companion = TakePreviewMaterials(command, root);
            var materials = companion == null ? null : new BlueprintMaterialAccumulator();
            using (var index = files.OpenIndex(command.Id, false))
            {
                if (index.Length != checked(root.BlockCount * 32L)) throw new InvalidDataException("blueprint-index-length");
                using (var hash = SHA256.Create())
                {
                    var buffer = new byte[32768]; int count;
                    while ((count = index.Read(buffer, 0, buffer.Length)) != 0)
                    { hash.TransformBlock(buffer, 0, count, buffer, 0); CheckRead(command); yield return true; }
                    hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    if (!LibraryCodec.Equal(hash.Hash, root.IndexHash)) throw new InvalidDataException("blueprint-index-hash");
                }
                index.Position = 0;
                foreach (bool ignored in VisitSteps(command.Id, false, root, command, chunk =>
                {
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var cell = chunk.Cells[i]; BlueprintPreviewLayer layers = BlueprintPreviewLayer.None;
                        if ((cell.Layers & CaptureLayer.Wall) != 0) layers |= BlueprintPreviewLayer.Wall;
                        if ((cell.Layers & CaptureLayer.Wire) != 0) layers |= BlueprintPreviewLayer.Wire;
                        if ((cell.Layers & CaptureLayer.Actuator) != 0) layers |= BlueprintPreviewLayer.Actuator;
                        if ((cell.Layers & CaptureLayer.Liquid) != 0) layers |= BlueprintPreviewLayer.Liquid;
                        if ((cell.Layers & CaptureLayer.Tile) != 0) layers |= cell.ObjectId == 0 ? BlueprintPreviewLayer.Tile : BlueprintPreviewLayer.Object;
                        builder.Add(cell.X - summary.Left, cell.Y - summary.Top, layers);
                    }
                }, verifiedIndex: index, materialKey: companion?.Key, catalog: companion?.Catalog, materials: materials))
                { CheckRead(command); yield return true; }
            }
            CheckRead(command);
            if (companion != null) StoreMaterials(companion.Key, materials.Snapshot(++materialRevision));
            previewReceipt = Receipt(command, LibraryStatus.Completed, preview: builder.Complete(), summary: summary);
        }
    }
}
