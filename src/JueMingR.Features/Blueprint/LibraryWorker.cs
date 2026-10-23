using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using JueMingR.Platform.Blueprint;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Blueprint
{
    internal sealed partial class LibraryWorker
    {
        private sealed class Stage
        {
            internal Guid Id, Physical;
            internal CaptureToken Token;
            internal long Blocks, Cells, Objects;
            internal LibraryRoot Root;
            internal bool Protected, Promoted;
        }
        private sealed class ProtectedStageException : InvalidOperationException
        { internal ProtectedStageException() : base("草稿含恢复材料，已进入保护状态") { } }
        private readonly object gate = new object();
        private readonly IBlueprintLibraryFiles files;
        private readonly Thread thread;
        private LibraryCommand pending;
        private LibraryReceipt result;
        private Stage stage;
        private bool occupied, stopping, opened;
        private string rootFailure;
        private long processed, cancelledRead = Int64.MinValue;
        private readonly HashSet<Guid> protectedAssets = new HashSet<Guid>();
        internal LibraryWorker(IBlueprintLibraryFiles files)
        { this.files = files ?? throw new ArgumentNullException(nameof(files)); thread = new Thread(Run) { IsBackground = true, Name = "JueMingR blueprint library" }; thread.Start(); }
        internal bool Busy { get { lock (gate) return occupied; } }
        internal long Processed { get { return Interlocked.Read(ref processed); } }
        internal bool TrySubmit(LibraryCommand command) { lock (gate) { if (occupied || stopping) return false; pending = command; occupied = true; Interlocked.Exchange(ref processed, 0); Monitor.Pulse(gate); return true; } }
        internal bool TryTake(out LibraryReceipt value) { lock (gate) { value = result; if (value == null) return false; result = null; occupied = false; return true; } }
        internal void CancelRead(long generation) { Interlocked.Exchange(ref cancelledRead, generation); }
        internal void BeginStop() { lock (gate) { stopping = true; Monitor.Pulse(gate); } }
        internal bool Stop(int milliseconds) { BeginStop(); return thread.Join(Math.Max(0, milliseconds)); }
        private void Run()
        {
            try
            {
                while (true)
                {
                    LibraryCommand command;
                    lock (gate) { while (pending == null && !stopping) Monitor.Wait(gate); if (pending == null) break; command = pending; pending = null; }
                    LibraryReceipt completed;
                    try
                    {
                        if (!opened && rootFailure == null) { try { files.Open(); opened = true; } catch (Exception e) { rootFailure = Reason(e); } }
                        if (rootFailure != null) completed = Receipt(command, LibraryStatus.Protected, rootFailure, true);
                        else completed = Execute(command);
                    }
                    catch (OperationCanceledException) { completed = Receipt(command, LibraryStatus.Cancelled, "读取已取消"); }
                    catch (ProtectedStageException e) { completed = Receipt(command, LibraryStatus.Protected, e.Message, true); }
                    catch (Exception e)
                    {
                        if (stage != null && stage.Id == command.Id && (command.Kind == LibraryCommandKind.AppendChunk || command.Kind == LibraryCommandKind.SealDraft)) stage.Protected = true;
                        bool protect = stage != null && stage.Id == command.Id && stage.Protected;
                        if (command.Kind == LibraryCommandKind.Preview && !command.Stage) { protectedAssets.Add(command.Id); protect = true; }
                        completed = Receipt(command, protect && command.Kind == LibraryCommandKind.Preview ? LibraryStatus.Protected : LibraryStatus.Failed, Reason(e), protect, failureCode: Code(e));
                    }
                    finally { files.CloseMetadata(); }
                    lock (gate) result = completed;
                }
                // A normal runtime exit releases only its known uncommitted
                // draft. Accepted create/rename/delete completed above first.
                if (stage != null && !stage.Protected && !stage.Promoted)
                    try { Cleanup(stage.Physical, true, stage.Blocks); } catch { }
            }
            finally { files.Dispose(); }
        }
        private LibraryReceipt Execute(LibraryCommand c)
        {
            switch (c.Kind)
            {
                case LibraryCommandKind.BeginStage:
                    if (stage != null || c.Id == Guid.Empty) return Receipt(c, LibraryStatus.Failed, "已有草稿或无效采集身份");
                    files.BeginStage(c.Id); stage = new Stage { Id = c.Id, Physical = c.Id, Token = c.Token }; return Receipt(c, LibraryStatus.Completed);
                case LibraryCommandKind.AppendChunk:
                    RequireStage(c.Id, false); if (c.Chunk == null || c.Chunk.Count + c.Chunk.ObjectCount == 0) throw new InvalidDataException("blueprint-empty-chunk");
                    Array.Sort(c.Chunk.Cells, 0, c.Chunk.Count, CellComparer.Instance); Array.Sort(c.Chunk.Objects, 0, c.Chunk.ObjectCount, ObjectComparer.Instance);
                    byte[] block = LibraryCodec.EncodeChunk(c.Chunk); files.WriteStageBlock(stage.Physical, stage.Blocks, block, LibraryCodec.Hash(block));
                    stage.Blocks++; stage.Cells = checked(stage.Cells + c.Chunk.Count); stage.Objects = checked(stage.Objects + c.Chunk.ObjectCount); return Receipt(c, LibraryStatus.Completed);
                case LibraryCommandKind.SealDraft:
                    RequireStage(c.Id, false); if (stage.Blocks == 0 || c.Summary == null || c.Summary.ContentCount != stage.Cells) throw new InvalidDataException("blueprint-summary-count");
                    stage.Root = NewRoot(stage.Physical, stage.Blocks, c.Summary, true); files.SealStage(stage.Physical, LibraryCodec.EncodeRoot(stage.Root));
                    Normalize(stage); Validate(stage.Physical, true, stage.Root, null, false); return Receipt(c, LibraryStatus.Completed, summary: stage.Root.Summary);
                case LibraryCommandKind.DiscardDraft:
                    RequireStage(c.Id, null); if (stage.Protected || stage.Promoted) return Receipt(c, LibraryStatus.Protected, "草稿含恢复材料，已保留", true);
                    Cleanup(stage.Physical, true, stage.Blocks); stage = null; return Receipt(c, LibraryStatus.Completed);
                case LibraryCommandKind.Create: return Create(c);
                case LibraryCommandKind.List: return List(c);
                case LibraryCommandKind.Objects: return Objects(c);
                case LibraryCommandKind.Preview: return Preview(c);
                case LibraryCommandKind.Rename: return Rename(c);
                case LibraryCommandKind.Delete: return Delete(c);
                default: throw new InvalidOperationException("blueprint-command");
            }
        }
        private LibraryReceipt Create(LibraryCommand c)
        {
            RequireStage(c.Id, true); if (stage.Protected || stage.Promoted || protectedAssets.Contains(c.Id)) return Receipt(c, LibraryStatus.Protected, "该保存已保护，不能再次提交", true);
            string error; if (!LibraryName.TryValidate(c.Name, out error)) return Receipt(c, LibraryStatus.Failed, error);
            if (c.Token.OperationId != stage.Token.OperationId || c.Token.SessionGeneration != stage.Token.SessionGeneration || c.Token.ModeGeneration != stage.Token.ModeGeneration || c.Token.SelectionRevision != stage.Token.SelectionRevision) return Receipt(c, LibraryStatus.Failed, "采集身份已失效");
            CaptureSummary filtered = Measure(stage.Physical, true, stage.Root, c, true);
            if (filtered.ContentCount == 0) return Receipt(c, LibraryStatus.Failed, "排除后没有可保存的内容");
            Guid output = Guid.NewGuid(); long outputBlocks = 0; bool publishedAttempt = false, promoted = false;
            try
            {
                files.BeginStage(output);
                Visit(stage.Physical, true, stage.Root, null, chunk =>
                {
                    var cells = new CaptureCell[chunk.Count]; int count = 0; var objects = new CaptureObject[chunk.ObjectCount]; int objectCount = 0;
                    for (int i = 0; i < chunk.Count; i++) { CaptureCell cell = Filter(chunk.Cells[i], c); if (cell.HasContent) cells[count++] = cell; }
                    for (int i = 0; i < chunk.ObjectCount; i++) { CaptureObject obj = chunk.Objects[i]; if (!Excluded(obj.Id, !obj.Reliable, c)) objects[objectCount++] = obj; }
                    if (count + objectCount != 0) { byte[] bytes = LibraryCodec.EncodeChunk(new CaptureChunk(cells, count, objects, objectCount)); files.WriteStageBlock(output, outputBlocks++, bytes, LibraryCodec.Hash(bytes)); }
                });
                LibraryRoot root = NewRoot(output, outputBlocks, filtered, true); byte[] rootBytes = LibraryCodec.EncodeRoot(root); files.SealStage(output, rootBytes); Validate(output, true, root, null, false);
                // Promotion can create/move the asset directory before throwing;
                // uncertainty at this boundary must also latch this stable ID.
                promoted = true; stage.Promoted = true; files.PromoteStage(output, c.Id);
                PreferenceReadResult read = files.OpenMetadata(c.Id, true); if (read.Status != PreferenceReadStatus.Missing) throw new InvalidDataException("blueprint-create-metadata-conflict");
                long now = DateTime.UtcNow.Ticks;
                var metadata = new LibraryMetadata { AssetId = c.Id, Name = c.Name, ContentRevision = output, ContentHash = LibraryCodec.HashString(rootBytes), Width = filtered.Width, Height = filtered.Height, ContentCount = filtered.ContentCount, BlockCount = outputBlocks, MissingSummary = String.Join("；", filtered.MissingPayloads), CreatedTicks = now, UpdatedTicks = now };
                byte[] encoded = LibraryCodec.EncodeMetadata(metadata); publishedAttempt = true; PreferenceWriteResult written = files.WriteMetadata(read.Identity, encoded);
                if (written.Status != PreferenceWriteStatus.Saved) { stage.Protected = true; protectedAssets.Add(c.Id); return WriteReceipt(c, written); }
                // Mechanical commit readback covers metadata; this separate
                // content verification makes Saved cover its immutable payload.
                Validate(c.Id, false, root, null, false); files.CloseMetadata();
                bool cleanup = false; try { Cleanup(stage.Physical, true, stage.Blocks); } catch { cleanup = true; }
                stage = null; return Receipt(c, LibraryStatus.Saved, cleanup: cleanup, summary: filtered);
            }
            catch (Exception e)
            {
                if (promoted || publishedAttempt) { stage.Protected = true; protectedAssets.Add(c.Id); return Receipt(c, publishedAttempt ? LibraryStatus.CommitUnconfirmed : LibraryStatus.Protected, Reason(e), true, failureCode: Code(e)); }
                // A partial immutable block is retained rather than overwritten.
                // It belongs to this failed output, not the unchanged fixed draft.
                return Receipt(c, LibraryStatus.Failed, Reason(e), failureCode: Code(e));
            }
        }
        private LibraryReceipt Rename(LibraryCommand c)
        {
            string error; if (!LibraryName.TryValidate(c.Name, out error)) return Receipt(c, LibraryStatus.Failed, error);
            LibraryMetadata value; PreferenceReadResult read; LibraryRoot root;
            if (!OpenAsset(c, out value, out read, out root)) return Receipt(c, LibraryStatus.Protected, "蓝图已改变或不可写，原件已保留", true);
            if (value.Name == c.Name) return Receipt(c, LibraryStatus.Saved);
            value.Name = c.Name; value.UpdatedTicks = Math.Max(value.CreatedTicks, DateTime.UtcNow.Ticks);
            return WriteReceipt(c, files.WriteMetadata(read.Identity, LibraryCodec.EncodeMetadata(value)));
        }
        private LibraryReceipt Delete(LibraryCommand c)
        {
            LibraryMetadata value; PreferenceReadResult read; LibraryRoot root;
            if (!OpenAsset(c, out value, out read, out root)) return Receipt(c, LibraryStatus.Protected, "蓝图已改变或不可写，原件已保留", true);
            Validate(c.Id, false, root, null, false); value.Deleted = true; value.UpdatedTicks = Math.Max(value.CreatedTicks, DateTime.UtcNow.Ticks);
            PreferenceWriteResult written = files.WriteMetadata(read.Identity, LibraryCodec.EncodeMetadata(value));
            if (written.Status != PreferenceWriteStatus.Saved) return WriteReceipt(c, written);
            files.CloseMetadata(); bool cleanup = false;
            try { Cleanup(c.Id, false, value.BlockCount); } catch { cleanup = true; }
            return Receipt(c, LibraryStatus.Deleted, cleanup ? "已删除，部分文件清理尚未完成" : null, cleanup: cleanup);
        }
        private bool OpenAsset(LibraryCommand c, out LibraryMetadata value, out PreferenceReadResult read, out LibraryRoot root)
        {
            value = null; read = null; root = null; if (protectedAssets.Contains(c.Id)) return false;
            try
            {
                read = files.OpenMetadata(c.Id); if (read.Status != PreferenceReadStatus.Loaded || read.Identity != c.Identity) { protectedAssets.Add(c.Id); return false; }
                value = LibraryCodec.DecodeMetadata(read.Contents, c.Id); if (value.Deleted) return false; root = ReadAssetRoot(value); return true;
            }
            catch { protectedAssets.Add(c.Id); return false; }
        }
        private LibraryReceipt List(LibraryCommand c)
        {
            var items = new List<LibraryEntry>(6); long count = 0, selectedIndex = -1; int isolated = 0; long start = (long)c.Page * 6;
            foreach (Guid id in files.EnumerateAssets())
            {
                CheckRead(c); Interlocked.Increment(ref processed);
                try
                {
                    PreferenceReadResult read = files.OpenMetadata(id); if (read.Status == PreferenceReadStatus.Missing) continue;
                    if (read.Status != PreferenceReadStatus.Loaded) { isolated++; continue; }
                    LibraryMetadata metadata = LibraryCodec.DecodeMetadata(read.Contents, id);
                    if (metadata.Deleted) { files.CloseMetadata(); try { Cleanup(id, false, metadata.BlockCount); } catch { } continue; }
                    ReadAssetRoot(metadata, true);
                    if (c.SelectId == id) selectedIndex = count;
                    if (count >= start && items.Count < 6) items.Add(new LibraryEntry(id, read.Identity, metadata, protectedAssets.Contains(id))); count++;
                }
                catch { isolated++; }
                finally { files.CloseMetadata(); }
            }
            int page = count == 0 ? 0 : (int)Math.Min(Int32.MaxValue, Math.Min(c.Page, (count - 1) / 6));
            if (selectedIndex >= 0) page = (int)Math.Min(Int32.MaxValue, selectedIndex / 6);
            if (page != c.Page)
            {
                var retry = new LibraryCommand { CommandId = c.CommandId, Kind = c.Kind, Generation = c.Generation, Page = page }; return List(retry);
            }
            var resultPage = new LibraryPage(c.Generation, page, count, items.ToArray(), isolated, false, isolated == 0 ? null : "部分蓝图不可读取，原件已保留");
            return Receipt(c, LibraryStatus.Completed, page: resultPage);
        }
        private LibraryReceipt Objects(LibraryCommand c)
        {
            RequireStage(c.Id, true); var items = new List<CaptureObject>(6); long count = 0, start = (long)c.Page * 6;
            Visit(stage.Physical, true, stage.Root, c, chunk => { for (int i = 0; i < chunk.ObjectCount; i++) if (!chunk.Objects[i].Reliable) { if (count >= start && items.Count < 6) items.Add(chunk.Objects[i]); count++; } });
            int page = count == 0 ? 0 : (int)Math.Min(c.Page, (count - 1) / 6);
            if (page != c.Page) { c.Page = page; return Objects(c); }
            return Receipt(c, LibraryStatus.Completed, objectPage: new LibraryObjectPage(c.Generation, page, count, items.ToArray()));
        }
        private LibraryReceipt Preview(LibraryCommand c)
        {
            if (c.Width < 1 || c.Width > 512 || c.Height < 1 || c.Height > 128) return Receipt(c, LibraryStatus.Failed, "预览尺寸无效");
            Guid physical; LibraryRoot root; bool isStage = c.Stage;
            if (isStage) { RequireStage(c.Id, true); physical = stage.Physical; root = stage.Root; }
            else
            {
                PreferenceReadResult read = files.OpenMetadata(c.Id); if (read.Status != PreferenceReadStatus.Loaded) return Receipt(c, LibraryStatus.Protected, "蓝图不可读取", true);
                LibraryMetadata metadata = LibraryCodec.DecodeMetadata(read.Contents, c.Id); if (metadata.Deleted || metadata.ContentHash != c.Identity) return Receipt(c, LibraryStatus.Protected, "蓝图内容已改变", true);
                physical = c.Id; root = ReadAssetRoot(metadata);
            }
            CaptureSummary summary = Measure(physical, isStage, root, c, false);
            if (summary.ContentCount == 0) return Receipt(c, LibraryStatus.Failed, "没有可预览的内容", summary: summary);
            var builder = new BlueprintPreviewBuilder(summary.Width, summary.Height, c.Width, c.Height);
            Visit(physical, isStage, root, c, chunk =>
            {
                for (int i = 0; i < chunk.Count; i++)
                {
                    CaptureCell cell = Filter(chunk.Cells[i], c); if (!cell.HasContent) continue;
                    BlueprintPreviewLayer layers = BlueprintPreviewLayer.None;
                    if ((cell.Layers & CaptureLayer.Wall) != 0) layers |= BlueprintPreviewLayer.Wall;
                    if ((cell.Layers & CaptureLayer.Wire) != 0) layers |= BlueprintPreviewLayer.Wire;
                    if ((cell.Layers & CaptureLayer.Actuator) != 0) layers |= BlueprintPreviewLayer.Actuator;
                    if ((cell.Layers & CaptureLayer.Liquid) != 0) layers |= BlueprintPreviewLayer.Liquid;
                    if ((cell.Layers & CaptureLayer.Tile) != 0) layers |= cell.ObjectId == 0 ? BlueprintPreviewLayer.Tile : BlueprintPreviewLayer.Object;
                    builder.Add(cell.X - summary.Left, cell.Y - summary.Top, layers);
                }
            });
            return Receipt(c, LibraryStatus.Completed, preview: builder.Complete(), summary: summary);
        }
        private CaptureSummary Measure(Guid id, bool isStage, LibraryRoot root, LibraryCommand command, bool requireExclusions)
        {
            int left = Int32.MaxValue, top = Int32.MaxValue, right = -1, bottom = -1; long count = 0; var missing = new HashSet<string>(StringComparer.Ordinal);
            Visit(id, isStage, root, command.Kind == LibraryCommandKind.Create ? null : command, chunk =>
            {
                for (int i = 0; i < chunk.Count; i++)
                {
                    CaptureCell original = chunk.Cells[i]; if (requireExclusions && original.UnreliableObject && !Excluded(original.ObjectId, true, command)) throw new InvalidDataException("请先排除所有未确认对象");
                    CaptureCell cell = Filter(original, command); if (!cell.HasContent) continue; count++; left = Math.Min(left, cell.X); top = Math.Min(top, cell.Y); right = Math.Max(right, cell.X); bottom = Math.Max(bottom, cell.Y);
                }
                for (int i = 0; i < chunk.ObjectCount; i++) { CaptureObject obj = chunk.Objects[i]; if (requireExclusions && !obj.Reliable && !Excluded(obj.Id, true, command)) throw new InvalidDataException("请先排除所有未确认对象"); if (!Excluded(obj.Id, !obj.Reliable, command) && !String.IsNullOrEmpty(obj.MissingPayload)) { if (missing.Count >= 16 && !missing.Contains(obj.MissingPayload)) throw new InvalidDataException("blueprint-missing-categories"); missing.Add(obj.MissingPayload); } }
            });
            var names = new string[missing.Count]; missing.CopyTo(names); Array.Sort(names, StringComparer.Ordinal);
            return new CaptureSummary { Left = count == 0 ? 0 : left, Top = count == 0 ? 0 : top, Width = count == 0 ? 0 : checked(right - left + 1), Height = count == 0 ? 0 : checked(bottom - top + 1), ContentCount = count, UnreliableObjectCount = requireExclusions ? 0 : root.Summary.UnreliableObjectCount, StartTick = root.Summary.StartTick, EndTick = root.Summary.EndTick, TargetVersion = root.Summary.TargetVersion, MissingPayloads = names };
        }
        private void RequireStage(Guid id, bool? sealedState)
        {
            if (stage == null || stage.Id != id || sealedState.HasValue && (stage.Root != null) != sealedState.Value) throw new InvalidOperationException("blueprint-stage-unavailable");
            if (stage.Protected) throw new ProtectedStageException();
        }
        private LibraryRoot ReadAssetRoot(LibraryMetadata metadata, bool validateLayout = false)
        {
            byte[] bytes = files.ReadRoot(metadata.AssetId, false); if (LibraryCodec.HashString(bytes) != metadata.ContentHash) throw new InvalidDataException("blueprint-content-root-hash");
            LibraryRoot root = LibraryCodec.DecodeRoot(bytes);
            if (root.ContentRevision != metadata.ContentRevision || root.BlockCount != metadata.BlockCount || root.Summary.Width != metadata.Width || root.Summary.Height != metadata.Height || root.Summary.ContentCount != metadata.ContentCount || root.Summary.UnreliableObjectCount != 0 || String.Join("；", root.Summary.MissingPayloads) != metadata.MissingSummary) throw new InvalidDataException("blueprint-metadata-content-mismatch");
            if (validateLayout) { files.ValidateLayout(metadata.AssetId, false, root.BlockCount); VerifyIndex(metadata.AssetId, false, root); } return root;
        }
        private LibraryRoot NewRoot(Guid id, long blocks, CaptureSummary summary, bool isStage)
        { using (Stream index = files.OpenIndex(id, isStage)) return new LibraryRoot { ContentRevision = id, BlockCount = blocks, Summary = summary, IndexHash = LibraryCodec.Hash(index) }; }
        private void VerifyIndex(Guid id, bool isStage, LibraryRoot root)
        { using (Stream index = files.OpenIndex(id, isStage)) if (index.Length != checked(root.BlockCount * 32L) || !LibraryCodec.Equal(LibraryCodec.Hash(index), root.IndexHash)) throw new InvalidDataException("blueprint-index-hash"); }
        private void Validate(Guid id, bool isStage, LibraryRoot root, LibraryCommand command, bool allowPartialSummary)
        { files.ValidateLayout(id, isStage, root.BlockCount); VerifyIndex(id, isStage, root); Visit(id, isStage, root, command, chunk => { }, !allowPartialSummary); }
        private void Visit(Guid id, bool isStage, LibraryRoot root, LibraryCommand command, Action<CaptureChunk> visit, bool checkSummary = true)
        {
            VerifyIndex(id, isStage, root); long previous = -1, totalCells = 0, unreliable = 0; int previousObject = 0; bool sawObjects = false;
            using (var objects = new ObjectLookup(this, id, isStage, root.BlockCount))
            using (Stream index = files.OpenIndex(id, isStage)) for (long number = 0; number < root.BlockCount; number++)
            {
                if (command != null) CheckRead(command); CaptureChunk chunk = ReadChunk(id, isStage, number, index);
                for (int i = 0; i < chunk.Count; i++)
                {
                    CaptureCell cell = chunk.Cells[i]; long key = Key(cell);
                    if (sawObjects || key <= previous || cell.X < root.Summary.Left || cell.Y < root.Summary.Top || (long)cell.X >= (long)root.Summary.Left + root.Summary.Width || (long)cell.Y >= (long)root.Summary.Top + root.Summary.Height) throw new InvalidDataException("blueprint-cell-order-bounds-or-duplicate"); previous = key;
                    if (checkSummary && cell.ObjectId > 0)
                    {
                        CaptureObject obj = objects.Find(cell.ObjectId);
                        if (obj.Type != cell.TileType || obj.Reliable == cell.UnreliableObject || cell.X < obj.X || cell.Y < obj.Y || (long)cell.X >= (long)obj.X + obj.Width || (long)cell.Y >= (long)obj.Y + obj.Height) throw new InvalidDataException("blueprint-object-member-reference");
                    }
                }
                totalCells = checked(totalCells + chunk.Count);
                for (int i = 0; i < chunk.ObjectCount; i++) { sawObjects = true; CaptureObject obj = chunk.Objects[i]; if (obj.Id <= previousObject) throw new InvalidDataException("blueprint-object-order-or-duplicate"); previousObject = obj.Id; if (!obj.Reliable) unreliable++; else if (checkSummary) objects.CheckComplete(obj); }
                visit(chunk); Interlocked.Increment(ref processed);
            }
            if (checkSummary && (totalCells != root.Summary.ContentCount || unreliable != root.Summary.UnreliableObjectCount)) throw new InvalidDataException("blueprint-content-count");
        }
        private CaptureChunk ReadChunk(Guid id, bool isStage, long number, Stream index)
        {
            index.Position = checked(number * 32L); byte[] expected = new byte[32]; ReadExactly(index, expected); byte[] bytes = files.ReadBlock(id, isStage, number);
            if (!LibraryCodec.Equal(LibraryCodec.Hash(bytes), expected)) throw new InvalidDataException("blueprint-chunk-hash"); return LibraryCodec.DecodeChunk(bytes);
        }
        private sealed class ObjectLookup : IDisposable
        {
            private sealed class Cache { internal long Number; internal CaptureChunk Chunk; }
            private readonly LibraryWorker owner;
            private readonly Guid id;
            private readonly bool stage;
            private readonly long blocks;
            private readonly Stream index;
            private readonly List<Cache> cache = new List<Cache>(8);
            internal ObjectLookup(LibraryWorker owner, Guid id, bool stage, long blocks)
            { this.owner = owner; this.id = id; this.stage = stage; this.blocks = blocks; index = owner.files.OpenIndex(id, stage); }
            internal CaptureObject Find(int objectId)
            {
                long low = 0, high = blocks - 1;
                while (low <= high)
                {
                    long number = low + (high - low) / 2; CaptureChunk chunk = Get(number);
                    if (chunk.ObjectCount == 0 || chunk.Objects[chunk.ObjectCount - 1].Id < objectId) { low = number + 1; continue; }
                    if (chunk.Objects[0].Id > objectId) { high = number - 1; continue; }
                    int first = 0, last = chunk.ObjectCount - 1;
                    while (first <= last) { int at = first + (last - first) / 2; int candidate = chunk.Objects[at].Id; if (candidate == objectId) return chunk.Objects[at]; if (candidate < objectId) first = at + 1; else last = at - 1; }
                    break;
                }
                throw new InvalidDataException("blueprint-missing-object-reference");
            }
            internal void CheckComplete(CaptureObject obj)
            {
                // This is the capture resolver's per-object proof boundary, not
                // a limit on selection/content size. Larger/uncertain groups are
                // represented as unreliable and must be explicitly excluded.
                long area = (long)obj.Width * obj.Height;
                if (area < 1 || area >= CaptureChunk.MaximumRecords) throw new InvalidDataException("blueprint-object-proof-size");
                for (int y = obj.Y; y < (long)obj.Y + obj.Height; y++) for (int x = obj.X; x < (long)obj.X + obj.Width; x++)
                {
                    CaptureCell cell = FindCell(x, y);
                    if (cell.ObjectId != obj.Id || cell.TileType != obj.Type || (cell.Layers & CaptureLayer.Tile) == 0 || cell.UnreliableObject) throw new InvalidDataException("blueprint-incomplete-object-group");
                }
            }
            private CaptureCell FindCell(int x, int y)
            {
                long target = ((long)y << 32) | (uint)x, low = 0, high = blocks - 1;
                while (low <= high)
                {
                    long number = low + (high - low) / 2; CaptureChunk chunk = Get(number);
                    if (chunk.Count == 0 || Key(chunk.Cells[0]) > target) { high = number - 1; continue; }
                    if (Key(chunk.Cells[chunk.Count - 1]) < target) { low = number + 1; continue; }
                    int first = 0, last = chunk.Count - 1;
                    while (first <= last) { int at = first + (last - first) / 2; long key = Key(chunk.Cells[at]); if (key == target) return chunk.Cells[at]; if (key < target) first = at + 1; else last = at - 1; }
                    break;
                }
                throw new InvalidDataException("blueprint-missing-object-member");
            }
            private CaptureChunk Get(long number)
            {
                for (int i = 0; i < cache.Count; i++) if (cache[i].Number == number) { Cache hit = cache[i]; cache.RemoveAt(i); cache.Add(hit); return hit.Chunk; }
                CaptureChunk chunk = owner.ReadChunk(id, stage, number, index); if (cache.Count == 8) cache.RemoveAt(0); cache.Add(new Cache { Number = number, Chunk = chunk }); return chunk;
            }
            public void Dispose() { index.Dispose(); cache.Clear(); }
        }
        private void Cleanup(Guid id, bool isStage, long blocks) { while (!files.CleanupStep(id, isStage, blocks, 32)) { } }
        private void CheckRead(LibraryCommand command) { if (Interlocked.Read(ref cancelledRead) == command.Generation) throw new OperationCanceledException(); }
        private LibraryReceipt WriteReceipt(LibraryCommand c, PreferenceWriteResult value)
        {
            if (value.IsProtected || value.CommitUnconfirmed) protectedAssets.Add(c.Id);
            LibraryStatus status = value.Status == PreferenceWriteStatus.Saved ? LibraryStatus.Saved : value.CommitUnconfirmed ? LibraryStatus.CommitUnconfirmed : value.IsProtected ? LibraryStatus.Protected : LibraryStatus.Failed;
            string error = status == LibraryStatus.Saved ? null : value.CommitUnconfirmed ? "无法确认是否保存成功，相关文件已保留" : value.IsProtected ? "蓝图文件被占用或已改变，已进入保护状态" : "蓝图保存失败，草稿和原选区已保留";
            return Receipt(c, status, error, value.IsProtected, failureCode: value.Error);
        }
        private LibraryReceipt Receipt(LibraryCommand c, LibraryStatus status, string error = null, bool protect = false, bool cleanup = false, LibraryPage page = null, BlueprintPreviewImage preview = null, LibraryObjectPage objectPage = null, CaptureSummary summary = null, string failureCode = null)
        { if (protect && !c.Stage && c.Id != Guid.Empty && (c.Kind == LibraryCommandKind.Preview || c.Kind == LibraryCommandKind.Create || c.Kind == LibraryCommandKind.Rename || c.Kind == LibraryCommandKind.Delete)) protectedAssets.Add(c.Id); CaptureToken token = c.Kind == LibraryCommandKind.Create || c.Kind == LibraryCommandKind.BeginStage ? c.Token : stage != null && stage.Id == c.Id ? stage.Token : default(CaptureToken); return new LibraryReceipt(c.CommandId, c.Kind, status, c.Id, c.Id, token, c.Generation, error, protect, cleanup, page, preview, objectPage, summary, failureCode); }
        private static bool Excluded(int id, bool unreliable, LibraryCommand c) { return id > 0 && (unreliable && c.Exclude || Array.BinarySearch(c.Excluded, id) >= 0); }
        private static CaptureCell Filter(CaptureCell cell, LibraryCommand c) { return Excluded(cell.ObjectId, cell.UnreliableObject, c) ? cell.WithoutTile() : cell; }
        private static long Key(CaptureCell cell) { return ((long)cell.Y << 32) | (uint)cell.X; }
        private static void ReadExactly(Stream stream, byte[] bytes) { int at = 0; while (at != bytes.Length) { int read = stream.Read(bytes, at, bytes.Length - at); if (read == 0) throw new EndOfStreamException(); at += read; } }
        private static string Reason(Exception e) { if (e is InvalidDataException || e is InvalidOperationException || e is ArgumentException) return e.Message.StartsWith("blueprint-", StringComparison.Ordinal) ? "蓝图数据或文件校验失败，原件已保留" : e.Message; if (e is PreferenceFormatException) return "蓝图格式、字段或版本不可读取，原件已保留"; if (e is UnauthorizedAccessException) return "没有蓝图目录的读写权限"; if (e is IOException) return "蓝图文件读写失败或被占用，原件已保留"; return "蓝图处理失败，原件已保留"; }
        private static string Code(Exception e) { return e is InvalidDataException || e is InvalidOperationException ? e.Message : e.GetType().Name; }
        private sealed class CellComparer : IComparer<CaptureCell> { internal static readonly CellComparer Instance = new CellComparer(); public int Compare(CaptureCell x, CaptureCell y) { int result = x.Y.CompareTo(y.Y); return result == 0 ? x.X.CompareTo(y.X) : result; } }
        private sealed class ObjectComparer : IComparer<CaptureObject> { internal static readonly ObjectComparer Instance = new ObjectComparer(); public int Compare(CaptureObject x, CaptureObject y) { return x.Id.CompareTo(y.Id); } }
    }
}
