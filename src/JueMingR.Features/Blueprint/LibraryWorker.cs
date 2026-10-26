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
            internal bool Promoted;
        }
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
                    lock (gate) { while (pending == null && !ProjectionHasWorkLocked() && !MaterialHasWorkLocked() && !stopping) Monitor.Wait(gate); if (pending == null && stopping) break; command = pending; pending = null; }
                    // Accepted library commands have priority over the next
                    // projection slice. Their receipt/operation slot is separate.
                    if (command == null) { if (MaterialTurn()) MaterialStep(); else ProjectionStep(); continue; }
                    LibraryReceipt completed;
                    try
                    {
                        ReleaseRetiredProjection();
                        if (!opened && rootFailure == null) { try { files.Open(); opened = true; } catch (Exception e) { rootFailure = Reason(e); } }
                        if (rootFailure != null) completed = Receipt(command, LibraryStatus.Protected, rootFailure, true);
                        else completed = Execute(command);
                    }
                    catch (OperationCanceledException)
                    {
                        bool retired = RetireFailedStage(command);
                        completed = Receipt(command, LibraryStatus.Cancelled, retired ? "采集已停止，原选区已保留" : "读取已取消", retired, operationRetired: retired);
                    }
                    catch (Exception e)
                    {
                        bool retired = RetireFailedStage(command);
                        bool protect = retired;
                        if (command.Kind == LibraryCommandKind.Preview && !command.Stage) { protectedAssets.Add(command.Id); protect = true; }
                        completed = Receipt(command, protect && command.Kind == LibraryCommandKind.Preview ? LibraryStatus.Protected : LibraryStatus.Failed, retired ? Reason(e) + "；本次采集已结束，可重新采集" : Reason(e), protect, failureCode: Code(e), operationRetired: retired);
                    }
                    finally { files.CloseMetadata(); }
                    lock (gate) result = completed;
                }
                // Accepted create/rename/delete finished before this point.
                // Stop gives cleanup back at a safe boundary; unfinished private
                // material keeps its disk proof rather than delaying shutdown.
                if (stage != null && !stage.Promoted)
                    try { Cleanup(stage.Physical, true, stage.Blocks); } catch { }
            }
            finally { CloseMaterialWorker(); CloseProjectionWorker(); files.Dispose(); }
        }
        private LibraryReceipt Execute(LibraryCommand c)
        {
            if (c.Kind == LibraryCommandKind.Create || c.Kind == LibraryCommandKind.DiscardDraft) RetireMaterialSource(c.Id, true);
            else if (c.Kind == LibraryCommandKind.Delete) RetireMaterialSource(c.Id, false);
            // Capture correlation before any command can retire the old stage.
            // Old handles never acquire the token or ownership of a newer stage.
            if (stage != null && stage.Id == c.Id && c.Kind != LibraryCommandKind.Create && c.Kind != LibraryCommandKind.BeginStage) c.Token = stage.Token;
            switch (c.Kind)
            {
                case LibraryCommandKind.BeginStage:
                    if (stage != null || c.Id == Guid.Empty) return Receipt(c, LibraryStatus.Failed, "已有草稿或无效采集身份");
                    string suggested = SuggestName();
                    files.BeginStage(c.Id); stage = new Stage { Id = c.Id, Physical = c.Id, Token = c.Token }; return Receipt(c, LibraryStatus.Completed, suggestedName: suggested);
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
                    RequireStage(c.Id, null);
                    bool discarded = Cleanup(stage.Physical, true, stage.Blocks); stage = null;
                    return Receipt(c, LibraryStatus.Completed, cleanup: !discarded, operationRetired: true);
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
            if (protectedAssets.Contains(c.Id)) return Receipt(c, LibraryStatus.Protected, "该保存已保护，不能再次提交", true, operationRetired: true);
            RequireStage(c.Id, true);
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
                if (written.Status != PreferenceWriteStatus.Saved)
                {
                    // The promoted payload is retained under this exact ID. A
                    // precommit failure is still known uncommitted, but this
                    // operation cannot reuse its handle or promise draft retry.
                    stage = null; protectedAssets.Add(c.Id);
                    return Receipt(c, written.CommitUnconfirmed ? LibraryStatus.CommitUnconfirmed : LibraryStatus.Failed,
                        written.CommitUnconfirmed ? "无法确认是否保存成功，相关文件已保留" : "本次蓝图未保存，原选区已保留，可重新采集",
                        true, failureCode: written.Error, operationRetired: true);
                }
                // Mechanical commit readback covers metadata; this separate
                // content verification makes Saved cover its immutable payload.
                Validate(c.Id, false, root, null, false); files.CloseMetadata();
                bool cleanup = false; try { cleanup = !Cleanup(stage.Physical, true, stage.Blocks); } catch { cleanup = true; }
                stage = null; return Receipt(c, LibraryStatus.Saved, cleanup: cleanup, summary: filtered, operationRetired: true);
            }
            catch (Exception e)
            {
                // An output block may have failed before sealing. Releasing
                // its writer preserves bytes and does not affect the sealed
                // fixed draft, which may still support a user-requested retry.
                try { files.CloseStageWriter(); } catch { }
                if (promoted || publishedAttempt)
                {
                    stage = null; protectedAssets.Add(c.Id);
                    return Receipt(c, publishedAttempt ? LibraryStatus.CommitUnconfirmed : LibraryStatus.Failed,
                        publishedAttempt ? "无法确认是否保存成功，相关文件已保留" : "本次蓝图未保存，相关文件和原选区已保留，可重新采集",
                        true, failureCode: Code(e), operationRetired: true);
                }
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
            // Cleanup must never contend with our own retained index handle.
            // Host retains the old origin only for a known uncommitted failure.
            RetireProjectionAsset(c.Id);
            LibraryMetadata value; PreferenceReadResult read; LibraryRoot root;
            if (!OpenAsset(c, out value, out read, out root)) return Receipt(c, LibraryStatus.Protected, "蓝图已改变或不可写，原件已保留", true);
            Validate(c.Id, false, root, null, false); value.Deleted = true; value.UpdatedTicks = Math.Max(value.CreatedTicks, DateTime.UtcNow.Ticks);
            value.CleanupSourceIdentity = read.Identity; value.CleanupIndexIdentity = Convert.ToBase64String(root.IndexHash);
            byte[] tombstone = LibraryCodec.EncodeMetadata(value);
            try { files.PrepareCleanup(c.Id, CleanupProof(value, LibraryCodec.HashString(tombstone))); }
            catch (Exception e) { return Receipt(c, LibraryStatus.Protected, "删除恢复材料已保留，未继续改写", true, failureCode: Code(e)); }
            PreferenceWriteResult written;
            try { written = files.WriteMetadata(read.Identity, tombstone); }
            catch (Exception e) { return Receipt(c, LibraryStatus.CommitUnconfirmed, "无法确认删除是否提交，相关文件已保留", true, failureCode: Code(e)); }
            if (written.Status != PreferenceWriteStatus.Saved)
            {
                var rejected = Receipt(c, written.CommitUnconfirmed ? LibraryStatus.CommitUnconfirmed : LibraryStatus.Protected, written.CommitUnconfirmed ? "无法确认删除是否提交，相关文件已保留" : "删除未完成，恢复材料已保留", true, failureCode: written.Error);
                rejected.DeleteProvenUncommitted = !written.CommitUnconfirmed && !written.IsProtected;
                return rejected;
            }
            files.CloseMetadata(); bool cleanup = false;
            try { cleanup = !Cleanup(c.Id, false, value.BlockCount, CleanupProof(value, written.Identity)); } catch { cleanup = true; }
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
                    if (metadata.Deleted)
                    {
                        files.CloseMetadata();
                        if (metadata.CleanupSourceIdentity != null && metadata.CleanupIndexIdentity != null)
                            try { Cleanup(id, false, metadata.BlockCount, CleanupProof(metadata, read.Identity), c); }
                            catch (OperationCanceledException) { throw; } catch { }
                        continue;
                    }
                    ReadAssetRoot(metadata, true);
                    if (c.SelectId == id) selectedIndex = count;
                    if (count >= start && items.Count < 6) items.Add(new LibraryEntry(id, read.Identity, metadata, protectedAssets.Contains(id))); count++;
                }
                catch (OperationCanceledException) { throw; }
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
            if (!BlueprintPreviewBudget.IsValid(c.Width, c.Height, c.Stage)) return Receipt(c, LibraryStatus.Failed, "预览尺寸无效");
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
            var builder = new BlueprintPreviewBuilder(summary.Width, summary.Height, c.Width, c.Height, c.Stage);
            var materialCompanion = TakePreviewMaterials(c, root);
            var materialSummary = materialCompanion == null ? null : new BlueprintMaterialAccumulator();
            foreach (bool ignored in VisitSteps(physical, isStage, root, c, chunk =>
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
            }, materialKey: materialCompanion?.Key, catalog: materialCompanion?.Catalog, materials: materialSummary)) { }
            if (materialCompanion != null) StoreMaterials(materialCompanion.Key, materialSummary.Snapshot(++materialRevision));
            return Receipt(c, LibraryStatus.Completed, preview: builder.Complete(), summary: summary);
        }
        private CaptureSummary Measure(Guid id, bool isStage, LibraryRoot root, LibraryCommand command, bool requireExclusions)
        {
            int left = Int32.MaxValue, top = Int32.MaxValue, right = -1, bottom = -1; long count = 0, unreliable = 0; var missing = new HashSet<string>(StringComparer.Ordinal);
            Visit(id, isStage, root, command.Kind == LibraryCommandKind.Create ? null : command, chunk =>
            {
                for (int i = 0; i < chunk.Count; i++)
                {
                    CaptureCell original = chunk.Cells[i]; if (requireExclusions && original.UnreliableObject && !Excluded(original.ObjectId, true, command)) throw new InvalidDataException("请先排除所有未确认对象");
                    CaptureCell cell = Filter(original, command); if (!cell.HasContent) continue; count++; left = Math.Min(left, cell.X); top = Math.Min(top, cell.Y); right = Math.Max(right, cell.X); bottom = Math.Max(bottom, cell.Y);
                }
                for (int i = 0; i < chunk.ObjectCount; i++)
                {
                    CaptureObject obj = chunk.Objects[i]; bool excluded = Excluded(obj.Id, !obj.Reliable, command);
                    if (!obj.Reliable && !excluded) { unreliable++; if (requireExclusions) throw new InvalidDataException("请先排除所有未确认对象"); }
                    if (!excluded && !String.IsNullOrEmpty(obj.MissingPayload)) { if (missing.Count >= 16 && !missing.Contains(obj.MissingPayload)) throw new InvalidDataException("blueprint-missing-categories"); missing.Add(obj.MissingPayload); }
                }
            });
            // Count remaining unreliable objects in the existing visit; this is
            // a filtered summary, never a mutation of the original capture.
            var names = new string[missing.Count]; missing.CopyTo(names); Array.Sort(names, StringComparer.Ordinal);
            return new CaptureSummary { Left = count == 0 ? 0 : left, Top = count == 0 ? 0 : top, Width = count == 0 ? 0 : checked(right - left + 1), Height = count == 0 ? 0 : checked(bottom - top + 1), ContentCount = count, UnreliableObjectCount = unreliable, StartTick = root.Summary.StartTick, EndTick = root.Summary.EndTick, TargetVersion = root.Summary.TargetVersion, MissingPayloads = names };
        }
        private void RequireStage(Guid id, bool? sealedState)
        {
            if (stage == null || stage.Id != id || sealedState.HasValue && (stage.Root != null) != sealedState.Value) throw new InvalidOperationException("blueprint-stage-unavailable");
        }
        private bool RetireFailedStage(LibraryCommand command)
        {
            bool mutatesDraft = command.Kind == LibraryCommandKind.BeginStage || command.Kind == LibraryCommandKind.AppendChunk || command.Kind == LibraryCommandKind.SealDraft || command.Kind == LibraryCommandKind.DiscardDraft;
            if (!mutatesDraft) return false;
            bool ownsWriter = stage != null && stage.Id == command.Id || stage == null && command.Kind == LibraryCommandKind.BeginStage;
            if (stage != null && stage.Id == command.Id) stage = null;
            // A stale handle is not the owner of the new append writer. Closing
            // only this failed operation also makes retained files inspectable.
            if (ownsWriter) try { files.CloseStageWriter(); } catch { }
            // No recovery dictionary is needed: unmatched handles can never
            // reacquire a live stage. Signed disk materials retain their proof.
            return true;
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
        private Stream OpenVerifiedIndex(Guid id, bool isStage, LibraryRoot root)
        {
            Stream index = files.OpenIndex(id, isStage);
            try { if (index.Length != checked(root.BlockCount * 32L) || !LibraryCodec.Equal(LibraryCodec.Hash(index), root.IndexHash)) throw new InvalidDataException("blueprint-index-hash"); index.Position = 0; return index; }
            catch { index.Dispose(); throw; }
        }
        private void VerifyIndex(Guid id, bool isStage, LibraryRoot root) { using (Stream index = OpenVerifiedIndex(id, isStage, root)) { } }
        private void Validate(Guid id, bool isStage, LibraryRoot root, LibraryCommand command, bool allowPartialSummary)
        { files.ValidateLayout(id, isStage, root.BlockCount); Visit(id, isStage, root, command, chunk => { }, !allowPartialSummary); }
        private void Visit(Guid id, bool isStage, LibraryRoot root, LibraryCommand command, Action<CaptureChunk> visit, bool checkSummary = true)
        { foreach (bool ignored in VisitSteps(id, isStage, root, command, visit, checkSummary)) { } }
        private IEnumerable<bool> VisitSteps(Guid id, bool isStage, LibraryRoot root, LibraryCommand command, Action<CaptureChunk> visit, bool checkSummary = true,
            Stream verifiedIndex = null, Func<long, CaptureChunk> reader = null, BlueprintMaterialKey materialKey = null,
            BlueprintPlacementCatalog catalog = null, BlueprintMaterialAccumulator materials = null)
        {
            long previous = -1, totalCells = 0, unreliable = 0; int previousObject = 0; bool sawObjects = false;
            using (Stream index = verifiedIndex ?? OpenVerifiedIndex(id, isStage, root))
            using (var objects = new ObjectLookup(this, id, isStage, root.BlockCount, index, reader)) for (long number = 0; number < root.BlockCount; number++)
            {
                if (command != null) CheckRead(command); CaptureChunk chunk = reader == null ? ReadChunk(id, isStage, number, index) : reader(number);
                for (int i = 0; i < chunk.Count; i++)
                {
                    CaptureCell cell = chunk.Cells[i]; long key = Key(cell);
                    if (sawObjects || key <= previous || cell.X < root.Summary.Left || cell.Y < root.Summary.Top || (long)cell.X >= (long)root.Summary.Left + root.Summary.Width || (long)cell.Y >= (long)root.Summary.Top + root.Summary.Height) throw new InvalidDataException("blueprint-cell-order-bounds-or-duplicate"); previous = key;
                    if (checkSummary && cell.ObjectId > 0)
                    {
                        CaptureObject obj = objects.Find(cell.ObjectId);
                        if (obj.Type != cell.TileType || obj.Reliable == cell.UnreliableObject || cell.X < obj.X || cell.Y < obj.Y || (long)cell.X >= (long)obj.X + obj.Width || (long)cell.Y >= (long)obj.Y + obj.Height) throw new InvalidDataException("blueprint-object-member-reference");
                    }
                    if (materials != null)
                    {
                        CaptureCell effective = materialKey.Excluded(cell.ObjectId, cell.UnreliableObject) ? cell.WithoutTile() : cell;
                        if (effective.HasContent) BlueprintMaterialSteps.StaticCell(effective, catalog, materials);
                    }
                    if ((i & 31) == 31) yield return true;
                }
                totalCells = checked(totalCells + chunk.Count);
                for (int i = 0; i < chunk.ObjectCount; i++)
                {
                    sawObjects = true; CaptureObject obj = chunk.Objects[i]; if (obj.Id <= previousObject) throw new InvalidDataException("blueprint-object-order-or-duplicate"); previousObject = obj.Id;
                    if (!obj.Reliable) unreliable++; else if (checkSummary) foreach (bool ignored in objects.CheckCompleteSteps(obj)) yield return true;
                    if (materials != null && !materialKey.Excluded(obj.Id, !obj.Reliable)) BlueprintMaterialSteps.StaticObject(obj, objects.FindCell(obj.X, obj.Y), catalog, materials);
                    yield return true;
                }
                visit?.Invoke(chunk); if (materials == null) Interlocked.Increment(ref processed); yield return true;
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
            private readonly Func<long, CaptureChunk> reader;
            private readonly List<Cache> cache = new List<Cache>(8);
            internal ObjectLookup(LibraryWorker owner, Guid id, bool stage, long blocks, Stream index, Func<long, CaptureChunk> reader = null)
            { this.owner = owner; this.id = id; this.stage = stage; this.blocks = blocks; this.index = index; this.reader = reader; }
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
            { foreach (bool ignored in CheckCompleteSteps(obj)) { } }
            internal IEnumerable<bool> CheckCompleteSteps(CaptureObject obj)
            {
                // This is the capture resolver's per-object proof boundary, not
                // a limit on selection/content size. Larger/uncertain groups are
                // represented as unreliable and must be explicitly excluded.
                long area = (long)obj.Width * obj.Height;
                if (area < 1 || area >= CaptureChunk.MaximumRecords) throw new InvalidDataException("blueprint-object-proof-size");
                int visited = 0;
                for (int y = obj.Y; y < (long)obj.Y + obj.Height; y++) for (int x = obj.X; x < (long)obj.X + obj.Width; x++)
                {
                    CaptureCell cell = FindCell(x, y);
                    if (cell.ObjectId != obj.Id || cell.TileType != obj.Type || (cell.Layers & CaptureLayer.Tile) == 0 || cell.UnreliableObject) throw new InvalidDataException("blueprint-incomplete-object-group");
                    if (++visited % 32 == 0) yield return true;
                }
            }
            internal CaptureCell FindCell(int x, int y)
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
                // Concurrent projection opens borrow the one global block cache;
                // the ordinary save validator keeps its original private cache.
                if (reader != null) return reader(number);
                for (int i = 0; i < cache.Count; i++) if (cache[i].Number == number) { Cache hit = cache[i]; cache.RemoveAt(i); cache.Add(hit); return hit.Chunk; }
                CaptureChunk chunk = owner.ReadChunk(id, stage, number, index); if (cache.Count == 8) cache.RemoveAt(0); cache.Add(new Cache { Number = number, Chunk = chunk }); return chunk;
            }
            public void Dispose() { cache.Clear(); }
        }
        private bool Cleanup(Guid id, bool isStage, long blocks, BlueprintCleanupProof proof = null, LibraryCommand read = null)
        {
            if (!isStage) RetireProjectionAsset(id);
            try
            {
                while (true)
                {
                    if (read != null) CheckRead(read);
                    lock (gate) if (stopping) return false;
                    if (files.CleanupStep(id, isStage, blocks, 32, proof)) return true;
                    // A safe batch is a cancellation/stop boundary, including
                    // index proof validation; accepted metadata writes finished
                    // before entering cleanup and cannot be revoked here.
                    Thread.Yield();
                }
            }
            finally { files.CloseCleanup(); }
        }
        private static BlueprintCleanupProof CleanupProof(LibraryMetadata value, string identity)
        { return new BlueprintCleanupProof(identity, value.CleanupSourceIdentity, value.ContentHash, value.CleanupIndexIdentity); }
        private string SuggestName()
        {
            const string name = "新蓝图", prefix = "新蓝图 "; bool occupied = false; string maximum = "1";
            VisitNames(value =>
            {
                if (value == name) occupied = true;
                if (!value.StartsWith(prefix, StringComparison.Ordinal)) return;
                string suffix = value.Substring(prefix.Length); if (suffix.Length == 0 || suffix[0] == '0') return;
                foreach (char digit in suffix) if (digit < '0' || digit > '9') return;
                if (suffix.Length > maximum.Length || suffix.Length == maximum.Length && String.CompareOrdinal(suffix, maximum) > 0) maximum = suffix;
            });
            if (!occupied) return name;
            string candidate = prefix + Increment(maximum), error;
            if (LibraryName.TryValidate(candidate, out error)) return candidate;
            // Only an extreme manual numeric name can exhaust the 48-element
            // suffix length. Search actual holes then, without an unbounded set.
            string number = "2";
            while (true) { candidate = prefix + number; bool exists = false; VisitNames(value => { if (value == candidate) exists = true; }); if (!exists) return candidate; number = Increment(number); }
        }
        private void VisitNames(Action<string> visitor)
        {
            foreach (Guid id in files.EnumerateAssets())
            {
                Interlocked.Increment(ref processed);
                try { PreferenceReadResult read = files.OpenMetadata(id); if (read.Status == PreferenceReadStatus.Loaded) { LibraryMetadata value = LibraryCodec.DecodeMetadata(read.Contents, id); if (!value.Deleted) visitor(value.Name); } }
                catch { } finally { files.CloseMetadata(); }
            }
        }
        private static string Increment(string number)
        { char[] digits = number.ToCharArray(); for (int i = digits.Length - 1; i >= 0; i--) { if (digits[i] != '9') { digits[i]++; return new string(digits); } digits[i] = '0'; } return "1" + new string(digits); }
        private void CheckRead(LibraryCommand command)
        {
            // A read generation never owns a create/rename/delete. Even a bad
            // caller reusing that number cannot turn an accepted write into a
            // cancelled operation halfway through its immutable-content pass.
            if ((command.Kind == LibraryCommandKind.List || command.Kind == LibraryCommandKind.Objects || command.Kind == LibraryCommandKind.Preview) &&
                (Interlocked.Read(ref cancelledRead) == command.Generation || IsStopping())) throw new OperationCanceledException();
        }
        private bool IsStopping() { lock (gate) return stopping; }
        private LibraryReceipt WriteReceipt(LibraryCommand c, PreferenceWriteResult value)
        {
            if (value.IsProtected || value.CommitUnconfirmed) protectedAssets.Add(c.Id);
            LibraryStatus status = value.Status == PreferenceWriteStatus.Saved ? LibraryStatus.Saved : value.CommitUnconfirmed ? LibraryStatus.CommitUnconfirmed : value.IsProtected ? LibraryStatus.Protected : LibraryStatus.Failed;
            string error = status == LibraryStatus.Saved ? null : value.CommitUnconfirmed ? "无法确认是否改名成功，相关文件已保留" : value.IsProtected ? "蓝图文件被占用或已改变，原件已保留" : "改名未保存，原名称已保留";
            return Receipt(c, status, error, value.IsProtected, failureCode: value.Error);
        }
        private LibraryReceipt Receipt(LibraryCommand c, LibraryStatus status, string error = null, bool protect = false, bool cleanup = false, LibraryPage page = null, BlueprintPreviewImage preview = null, LibraryObjectPage objectPage = null, CaptureSummary summary = null, string failureCode = null, string suggestedName = null, bool operationRetired = false)
        {
            if (status == LibraryStatus.CommitUnconfirmed && c.Kind == LibraryCommandKind.Delete) projectionUncertainAssets.Add(c.Id);
            if (protect && !c.Stage && c.Id != Guid.Empty && (c.Kind == LibraryCommandKind.Preview || c.Kind == LibraryCommandKind.Create || c.Kind == LibraryCommandKind.Rename || c.Kind == LibraryCommandKind.Delete)) protectedAssets.Add(c.Id);
            return new LibraryReceipt(c.CommandId, c.Kind, status, c.Id, c.Id, c.Token, c.Generation, error, protect, cleanup, page, preview, objectPage, summary, failureCode, suggestedName, operationRetired)
            { CommittedName = c.Kind == LibraryCommandKind.Rename && status == LibraryStatus.Saved ? c.Name : null };
        }
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
