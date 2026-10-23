using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace JueMingR.Features.Blueprint
{
    internal sealed partial class LibraryWorker
    {
        private sealed class RunSource
        {
            internal Guid Id;
            internal long First, Count;
            internal LibraryRoot Root;
        }
        // A cursor retains one decoded <=2048-record block. Sixteen inputs plus
        // one output is a fixed memory bound, independent of the selected area.
        private sealed class RunCursor : IDisposable
        {
            private readonly LibraryWorker owner;
            private readonly RunSource source;
            private readonly bool objects;
            private readonly Stream index;
            private long next;
            private CaptureChunk chunk;
            private int at;
            internal CaptureCell Cell { get { return chunk.Cells[at]; } }
            internal CaptureObject Object { get { return chunk.Objects[at]; } }
            internal bool Valid { get; private set; }
            internal RunCursor(LibraryWorker owner, RunSource source, bool objects)
            { this.owner = owner; this.source = source; this.objects = objects; next = source.First; index = owner.files.OpenIndex(source.Id, true); Load(); }
            internal void Advance() { at++; if (at >= (objects ? chunk.ObjectCount : chunk.Count)) Load(); }
            private void Load()
            {
                Valid = false;
                while (next < source.First + source.Count)
                {
                    chunk = owner.ReadChunk(source.Id, true, next++, index); at = 0;
                    if ((objects ? chunk.ObjectCount : chunk.Count) != 0) { Valid = true; return; }
                }
                chunk = null;
            }
            public void Dispose() { index.Dispose(); chunk = null; }
        }
        private void Normalize(Stage value)
        {
            long runCount = value.Blocks; int pass = 0;
            while (true)
            {
                long outputCount = runCount / 16 + (runCount % 16 == 0 ? 0 : 1);
                for (long group = 0; group < outputCount; group++)
                {
                    var sources = new List<RunSource>(16);
                    for (long i = group * 16; i < Math.Min(runCount, group * 16 + 16); i++)
                    {
                        if (pass == 0) sources.Add(new RunSource { Id = value.Physical, First = i, Count = 1, Root = value.Root });
                        else { Guid id = RunId(value.Id, pass - 1, i); LibraryRoot root = LibraryCodec.DecodeRoot(files.ReadRoot(id, true)); sources.Add(new RunSource { Id = id, First = 0, Count = root.BlockCount, Root = root }); }
                    }
                    Guid output = RunId(value.Id, pass, group); files.BeginStage(output); long written = MergeRuns(output, sources);
                    LibraryRoot outputRoot = NewRoot(output, written, value.Root.Summary, true); files.SealStage(output, LibraryCodec.EncodeRoot(outputRoot)); Validate(output, true, outputRoot, null, true);
                    if (pass != 0) foreach (RunSource source in sources) Cleanup(source.Id, true, source.Count);
                }
                if (pass == 0) Cleanup(value.Physical, true, value.Blocks);
                if (outputCount == 1)
                {
                    value.Physical = RunId(value.Id, pass, 0); value.Root = LibraryCodec.DecodeRoot(files.ReadRoot(value.Physical, true)); value.Blocks = value.Root.BlockCount; return;
                }
                runCount = outputCount; pass = checked(pass + 1);
            }
        }
        private long MergeRuns(Guid output, IList<RunSource> sources)
        {
            long blocks = 0; var cursors = new List<RunCursor>(16);
            try
            {
                foreach (RunSource source in sources) { VerifyIndex(source.Id, true, source.Root); cursors.Add(new RunCursor(this, source, false)); }
                var cells = new CaptureCell[CaptureChunk.MaximumRecords]; int used = 0; long previous = -1;
                while (true)
                {
                    RunCursor selected = null;
                    foreach (RunCursor cursor in cursors) if (cursor.Valid && (selected == null || Key(cursor.Cell) < Key(selected.Cell))) selected = cursor;
                    if (selected == null) break; CaptureCell cell = selected.Cell; long key = Key(cell);
                    if (key <= previous) throw new InvalidDataException("blueprint-duplicate-cell"); previous = key; cells[used++] = cell; selected.Advance();
                    if (used == cells.Length) { WriteMergeBlock(output, ref blocks, new CaptureChunk(cells, used)); cells = new CaptureCell[CaptureChunk.MaximumRecords]; used = 0; }
                }
                if (used != 0) WriteMergeBlock(output, ref blocks, new CaptureChunk(cells, used));
            }
            finally { foreach (RunCursor cursor in cursors) cursor.Dispose(); cursors.Clear(); }
            try
            {
                foreach (RunSource source in sources) cursors.Add(new RunCursor(this, source, true));
                var objects = new CaptureObject[CaptureChunk.MaximumRecords]; int used = 0, previous = 0;
                while (true)
                {
                    RunCursor selected = null;
                    foreach (RunCursor cursor in cursors) if (cursor.Valid && (selected == null || cursor.Object.Id < selected.Object.Id)) selected = cursor;
                    if (selected == null) break; CaptureObject obj = selected.Object;
                    if (obj.Id <= previous) throw new InvalidDataException("blueprint-duplicate-object"); previous = obj.Id; objects[used++] = obj; selected.Advance();
                    if (used == objects.Length) { WriteMergeBlock(output, ref blocks, new CaptureChunk(Array.Empty<CaptureCell>(), 0, objects, used)); objects = new CaptureObject[CaptureChunk.MaximumRecords]; used = 0; }
                }
                if (used != 0) WriteMergeBlock(output, ref blocks, new CaptureChunk(Array.Empty<CaptureCell>(), 0, objects, used));
            }
            finally { foreach (RunCursor cursor in cursors) cursor.Dispose(); }
            return blocks;
        }
        private void WriteMergeBlock(Guid output, ref long number, CaptureChunk chunk)
        { byte[] bytes = LibraryCodec.EncodeChunk(chunk); files.WriteStageBlock(output, number++, bytes, LibraryCodec.Hash(bytes)); InterlockedProgress(); }
        private void InterlockedProgress() { System.Threading.Interlocked.Increment(ref processed); }
        private static Guid RunId(Guid operation, int pass, long run)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(operation.ToByteArray()); writer.Write("JueMingR.BlueprintSortRun1"); writer.Write(pass); writer.Write(run); writer.Flush();
                using (var hash = SHA256.Create()) { byte[] digest = hash.ComputeHash(stream.ToArray()); var bytes = new byte[16]; Array.Copy(digest, bytes, bytes.Length); return new Guid(bytes); }
            }
        }
    }
}
