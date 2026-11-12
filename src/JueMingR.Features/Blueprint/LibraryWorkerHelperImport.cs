using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace JueMingR.Features.Blueprint
{
    internal sealed partial class LibraryWorker
    {
        private IEnumerable<bool> ImportHelperJson(ExchangeOperation operation)
        {
            // This input-format quota is separate from the 1 MiB native block
            // limit. The source byte buffer and sparse record graph live only
            // for this operation; declared empty dimensions allocate no grid.
            if (operation.SourceLength < 2 || operation.SourceLength > 64L * 1024 * 1024) throw HelperBlueprintJson.Bad("文件长度须在64MiB以内");
            foreach (bool step in ReadExchange(operation, (int)operation.SourceLength)) yield return step;
            var document = new HelperBlueprintJson();
            foreach (bool step in document.Read(operation.Read, operation.Request.ImportProfile)) yield return step;
            operation.Read = null;
            operation.Stage = Guid.NewGuid(); operation.Asset = Guid.NewGuid(); files.BeginStage(operation.Stage); yield return true;
            using (var identity = SHA256.Create())
            {
                // Stable semantic identity excludes JSON ordering/whitespace,
                // template titles, source dates, import time, stage and local asset IDs.
                byte[] domain = Encoding.UTF8.GetBytes("JueMingR.HelperBlueprint/1\0" + operation.Request.SupportedTargetVersion + "\0" + document.Width.ToString(CultureInfo.InvariantCulture) + "x" + document.Height.ToString(CultureInfo.InvariantCulture) + "\0");
                identity.TransformBlock(domain, 0, domain.Length, null, 0);
                var cells = new CaptureCell[CaptureChunk.MaximumRecords]; int count = 0;
                foreach (CaptureCell cell in document.Cells.Values)
                {
                    cells[count++] = cell;
                    if (count == cells.Length)
                    { WriteHelperChunk(operation, new CaptureChunk(cells, count), identity); cells = new CaptureCell[CaptureChunk.MaximumRecords]; count = 0; }
                    yield return true;
                }
                if (count != 0) { WriteHelperChunk(operation, new CaptureChunk(cells, count), identity); yield return true; }
                for (int at = 0; at < document.Objects.Count;)
                {
                    int take = Math.Min(CaptureChunk.MaximumRecords, document.Objects.Count - at); var objects = new CaptureObject[take]; document.Objects.CopyTo(at, objects, 0, take);
                    WriteHelperChunk(operation, new CaptureChunk(Array.Empty<CaptureCell>(), 0, objects, take), identity); at += take; yield return true;
                }
                identity.TransformFinalBlock(Array.Empty<byte>(), 0, 0); var revisionBytes = new byte[16]; Array.Copy(identity.Hash, revisionBytes, 16);
                Guid revision = new Guid(revisionBytes); if (revision == Guid.Empty) revisionBytes[0] = 1;
                LibraryRoot root = NewRoot(operation.Stage, operation.Blocks, new CaptureSummary { TargetVersion = operation.Request.SupportedTargetVersion,
                    Width = document.Width, Height = document.Height, ContentCount = document.Cells.Count }, true);
                // Generated revisions are independent of the private stage.
                root.ContentRevision = new Guid(revisionBytes);
                foreach (bool step in PublishImport(operation, root, LibraryCodec.EncodeRoot(root), document.Name)) yield return step;
            }
        }
        private void WriteHelperChunk(ExchangeOperation operation, CaptureChunk chunk, HashAlgorithm identity)
        {
            byte[] bytes = LibraryCodec.EncodeChunk(chunk); identity.TransformBlock(bytes, 0, bytes.Length, null, 0);
            files.WriteStageBlock(operation.Stage, operation.Blocks, bytes, LibraryCodec.Hash(bytes)); operation.Blocks++;
        }
    }
}
