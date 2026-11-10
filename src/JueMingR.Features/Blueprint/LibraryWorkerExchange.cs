using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using JueMingR.Platform.Blueprint;
using JueMingR.Platform.Settings;

namespace JueMingR.Features.Blueprint
{
    internal sealed partial class LibraryWorker
    {
        private enum ExportCommitKnowledge { NotSubmitted, InFlight, NotCommitted, Unconfirmed, Committed }
        private sealed class ExchangeOperation
        {
            internal BlueprintExchangeRequest Request;
            internal IEnumerator<bool> Steps;
            internal Guid Stage, Asset;
            internal long Blocks, Position, SourceLength;
            internal bool Promoted, Publishing, Cancelled, Finishing, AwaitingIo;
            internal ExportCommitKnowledge ExportCommit;
            internal BlueprintExchangeReceipt Completion;
            internal readonly byte[] Buffer = new byte[65536];
            internal byte[] Read;
            internal BlueprintExchangeIoReceipt Io;
            internal SHA256 OutputDigest;
            internal long OutputLength;
        }
        private IBlueprintExchangeIo exchangeIo;
        private IPreferenceStorage exchangeCounter;
        private Func<DateTime> exchangeClock;
        private ExchangeOperation exchange;
        private Guid exchangeReservation;
        private BlueprintExchangeReceipt exchangeReceipt;
        private bool exchangeReady, exchangeWasTurn, exchangeCounterProtected, exchangeCounterLoaded;
        private BlueprintExchangePhase exchangePhase;
        private string exchangeCounterIdentity;
        private DateTime exchangeSavedDate;
        private long exchangeSavedSequence;
        private DateTime exchangeHighDate;
        private long exchangeHighSequence;
        internal bool ExchangeBusy { get { lock (gate) return exchangeReservation != Guid.Empty || exchange != null || exchangeReceipt != null; } }
        internal BlueprintExchangePhase ExchangePhase { get { lock (gate) return exchangePhase; } }
        internal void ConfigureExchange(IBlueprintExchangeIo io, IPreferenceStorage counter, Func<DateTime> clock)
        { lock (gate) { if (exchangeIo != null || exchange != null) throw new InvalidOperationException("blueprint-exchange-already-configured"); exchangeIo = io; exchangeCounter = counter; exchangeClock = clock; } }
        private bool ExchangeBlocks(LibraryCommandKind kind)
        { return exchangeReservation != Guid.Empty && kind != LibraryCommandKind.List && kind != LibraryCommandKind.Preview && kind != LibraryCommandKind.Objects; }
        internal bool TryReserveExchange(Guid id)
        {
            lock (gate)
            {
                if (id == Guid.Empty || stopping || exchangeIo == null || exchange != null || exchangeReceipt != null || exchangeReservation != Guid.Empty || occupied || stage != null) return false;
                exchangeReservation = id; return true;
            }
        }
        internal bool TryBeginExchange(BlueprintExchangeRequest request)
        {
            if (request == null || request.RequestId == Guid.Empty) return false;
            lock (gate)
            {
                if (stopping || exchangeIo == null || exchangeCounter == null || exchange != null || exchangeReceipt != null || occupied || stage != null ||
                    exchangeReservation != Guid.Empty && exchangeReservation != request.RequestId ||
                    request.Kind != BlueprintExchangeKind.PrepareExport && (request.Ticket == null || request.Ticket.RequestId != request.RequestId || String.IsNullOrEmpty(request.SupportedTargetVersion))) return false;
                // Freeze caller-owned mutable DTO at admission; no UI selection
                // or later request edit may replace the accepted source/ticket.
                var frozen = new BlueprintExchangeRequest { RequestId = request.RequestId, Kind = request.Kind, TemplateKey = request.TemplateKey, Name = request.Name, SupportedTargetVersion = request.SupportedTargetVersion, Ticket = request.Ticket, Proposal = request.Proposal, CounterCandidate = request.CounterCandidate, ImportProfile = request.ImportProfile };
                exchangeReservation = request.RequestId; exchange = new ExchangeOperation { Request = frozen };
                exchange.Steps = ExchangeSteps(exchange).GetEnumerator(); exchangeReady = true; exchangePhase = BlueprintExchangePhase.Preparing; Monitor.Pulse(gate); return true;
            }
        }
        internal bool TryTakeExchangeReceipt(out BlueprintExchangeReceipt value)
        { lock (gate) { value = exchangeReceipt; if (value == null) return false; exchangeReceipt = null; return true; } }
        internal void CancelExchange(Guid id)
        {
            lock (gate)
            {
                if (exchange != null && exchange.Request.RequestId == id) exchange.Cancelled = true;
                else if (exchangeReservation == id) exchangeReservation = Guid.Empty;
                Monitor.Pulse(gate);
            }
        }
        private bool ExchangeHasWorkLocked() { return exchange != null && exchangeReady; }
        private bool ExchangeTurn()
        {
            lock (gate)
            {
                bool others = ProjectionHasWorkLocked() || MaterialHasWorkLocked();
                bool turn = ExchangeHasWorkLocked() && (!others || !exchangeWasTurn);
                exchangeWasTurn = turn; return turn;
            }
        }
        private void WakeExchange() { lock (gate) { exchangeReady = true; Monitor.Pulse(gate); } }
        private void ExchangeStep()
        {
            ExchangeOperation operation; lock (gate) { operation = exchange; exchangeReady = false; }
            if (operation == null) return;
            try
            {
                if (!opened) { if (rootFailure != null) throw new InvalidOperationException(rootFailure); files.Open(); opened = true; }
                if (operation.Cancelled && !operation.Publishing && !operation.Finishing && !operation.AwaitingIo) throw new OperationCanceledException();
                if (operation.Steps.MoveNext()) { if (operation.Steps.Current) WakeExchange(); return; }
                operation.Steps.Dispose();
                if (!operation.Finishing && operation.Request.Kind != BlueprintExchangeKind.PrepareExport)
                { operation.Finishing = true; operation.Steps = FinishExchange(operation).GetEnumerator(); WakeExchange(); return; }
                lock (gate)
                {
                    exchangeReceipt = operation.Completion; exchange = null; exchangePhase = BlueprintExchangePhase.Completed;
                    if (operation.Request.Kind != BlueprintExchangeKind.PrepareExport || operation.Completion.Status != BlueprintExchangeStatus.Completed) exchangeReservation = Guid.Empty;
                }
            }
            catch (Exception error)
            {
                operation.Steps.Dispose();
                if (operation.Finishing)
                {
                    // Cleanup failure never rewrites a reliable publish fact.
                    lock (gate) { exchangeReceipt = operation.Completion; exchange = null; exchangeReservation = Guid.Empty; }
                    return;
                }
                // Publishing blocks cancellation, but only the CommitTarget
                // acknowledgement knows whether export publication was attempted.
                // Import promotion/metadata retain their existing unknown boundary.
                bool unknown = operation.Request.Kind == BlueprintExchangeKind.Export
                    ? operation.ExportCommit == ExportCommitKnowledge.InFlight || operation.ExportCommit == ExportCommitKnowledge.Unconfirmed
                    : operation.Publishing;
                operation.Completion = ExchangeResult(operation, unknown ? BlueprintExchangeStatus.CommitUnconfirmed : error is OperationCanceledException ? BlueprintExchangeStatus.Cancelled : BlueprintExchangeStatus.Failed,
                    unknown ? "无法确认是否保存成功，相关文件已保留" : error is OperationCanceledException ? "文件操作已取消" : "蓝图文件格式、版本或读写校验失败，原件已保留");
                if (!unknown && error is InvalidDataException && error.Message.StartsWith("小助手蓝图：", StringComparison.Ordinal)) operation.Completion.Message = error.Message + "，原件已保留";
                operation.Finishing = true; operation.Steps = FinishExchange(operation).GetEnumerator(); WakeExchange();
            }
            finally { files.CloseMetadata(); }
        }
        private IEnumerable<bool> Io(ExchangeOperation operation, BlueprintExchangeIoCommand command)
        {
            operation.AwaitingIo = true;
            if (!exchangeIo.TrySubmit(command, WakeExchange)) { operation.AwaitingIo = false; throw new InvalidOperationException("blueprint-exchange-io-busy"); }
            bool exportCommit = operation.Request.Kind == BlueprintExchangeKind.Export && command.Kind == BlueprintExchangeIoKind.CommitTarget;
            if (exportCommit) operation.ExportCommit = ExportCommitKnowledge.InFlight;
            yield return false;
            BlueprintExchangeIoReceipt value;
            if (!exchangeIo.TryTake(out value)) throw new InvalidOperationException("blueprint-exchange-ack-missing");
            operation.AwaitingIo = false; operation.Io = value;
            // Close/read/write receipts must not overwrite the settled commit
            // fact, including reliable publication followed by cleanup failure.
            if (exportCommit) operation.ExportCommit = value.FileCommitted ? ExportCommitKnowledge.Committed : value.CommitUnconfirmed ? ExportCommitKnowledge.Unconfirmed : ExportCommitKnowledge.NotCommitted;
            if (!value.Success && !value.FileCommitted) { if (value.CommitUnconfirmed) operation.Publishing = true; throw new IOException(value.ErrorCode); }
            if (operation.Cancelled && !operation.Publishing && !operation.Finishing) throw new OperationCanceledException();
        }
        private IEnumerable<bool> ReadExchange(ExchangeOperation operation, int count, HashAlgorithm digest = null, long offset = -1)
        {
            long start = offset < 0 ? operation.Position : offset;
            if (count < 0 || start < 0 || checked(start + count) > operation.SourceLength) throw new InvalidDataException("blueprint-exchange-truncated");
            byte[] result = new byte[count]; int at = 0;
            while (at < count)
            {
                int take = Math.Min(65536, count - at);
                foreach (bool step in Io(operation, new BlueprintExchangeIoCommand { Kind = BlueprintExchangeIoKind.Read, Buffer = operation.Buffer, Count = take, Offset = start + at })) yield return step;
                if (operation.Io.Count != take || operation.Io.Length != operation.SourceLength) throw new InvalidDataException("blueprint-exchange-source-changed");
                Array.Copy(operation.Buffer, 0, result, at, take); digest?.TransformBlock(operation.Buffer, 0, take, null, 0); at += take;
            }
            if (offset < 0) operation.Position = checked(start + count); operation.Read = result;
        }
        private IEnumerable<bool> WriteExchange(ExchangeOperation operation, byte[] bytes, HashAlgorithm digest = null)
        {
            for (int at = 0; at < bytes.Length;)
            {
                int take = Math.Min(65536, bytes.Length - at); Array.Copy(bytes, at, operation.Buffer, 0, take); digest?.TransformBlock(operation.Buffer, 0, take, null, 0);
                operation.OutputDigest?.TransformBlock(operation.Buffer, 0, take, null, 0); operation.OutputLength = checked(operation.OutputLength + take);
                foreach (bool step in Io(operation, new BlueprintExchangeIoCommand { Kind = BlueprintExchangeIoKind.Write, Buffer = operation.Buffer, Count = take })) yield return step;
                at += take;
            }
        }
        private IEnumerable<bool> ExchangeSteps(ExchangeOperation operation)
        {
            BlueprintExchangeRequest request = operation.Request;
            if (request.Kind == BlueprintExchangeKind.PrepareExport)
            {
                if (exchangeCounterProtected) throw new InvalidDataException("blueprint-exchange-counter-protected");
                if (!exchangeCounterLoaded)
                {
                    // Read is the storage's initial snapshot. The sequential
                    // owner keeps current identity only from reliable Saved acks.
                    PreferenceReadResult read = exchangeCounter.Read();
                    if (read.Status == PreferenceReadStatus.Loaded)
                    { try { BlueprintExchangeCounter.Decode(read.Contents, out exchangeSavedDate, out exchangeSavedSequence); } catch { exchangeCounterProtected = true; throw; } }
                    else if (read.Status != PreferenceReadStatus.Missing) { exchangeCounterProtected = true; throw new InvalidDataException("blueprint-exchange-counter-unreadable"); }
                    exchangeCounterIdentity = read.Identity; exchangeCounterLoaded = true;
                }
                DateTime date = exchangeClock().Date;
                long candidate = checked(Math.Max(exchangeSavedDate == date ? exchangeSavedSequence : 0, exchangeHighDate == date ? exchangeHighSequence : 0) + 1);
                operation.Completion = ExchangeResult(operation, BlueprintExchangeStatus.Completed, null);
                operation.Completion.Proposal = new BlueprintExportNameProposal { RequestId = request.RequestId, ExportDate = date, CounterCandidate = candidate }; yield break;
            }
            exchangePhase = BlueprintExchangePhase.ReadingOrWriting;
            operation.OutputDigest = SHA256.Create();
            if (request.Kind == BlueprintExchangeKind.Import) foreach (bool step in ImportExchange(operation)) yield return step;
            else foreach (bool step in ExportExchange(operation)) yield return step;
        }
        private IEnumerable<bool> ValidateExchange(ExchangeOperation operation, Guid id, bool isStage, LibraryRoot root)
        {
            var layout = files as IBlueprintLibraryLayoutSteps;
            if (layout == null) throw new InvalidOperationException("blueprint-exchange-incremental-layout-unavailable");
            foreach (int ignored in layout.ValidateLayoutSteps(id, isStage, root.BlockCount, 32)) yield return true;
            using (Stream index = files.OpenIndex(id, isStage))
            {
                if (index.Length != checked(root.BlockCount * 32L)) throw new InvalidDataException("blueprint-index-length");
                using (var hash = SHA256.Create())
                {
                    int read; while ((read = index.Read(operation.Buffer, 0, operation.Buffer.Length)) != 0) { hash.TransformBlock(operation.Buffer, 0, read, null, 0); yield return true; }
                    hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0); if (!LibraryCodec.Equal(hash.Hash, root.IndexHash)) throw new InvalidDataException("blueprint-index-hash");
                }
                index.Position = 0;
                foreach (bool ignored in VisitSteps(id, isStage, root, null, null, true, index)) yield return true;
            }
        }
        private IEnumerable<bool> ImportExchange(ExchangeOperation operation)
        {
            foreach (bool step in Io(operation, new BlueprintExchangeIoCommand { Kind = BlueprintExchangeIoKind.OpenSource, Ticket = operation.Request.Ticket })) yield return step;
            operation.SourceLength = operation.Io.Length;
            foreach (bool step in ReadExchange(operation, (int)Math.Min(16, operation.SourceLength), null, 0)) yield return step;
            byte[] signature = operation.Read;
            bool native = signature.Length >= 8 && signature[0] == 0x4A && signature[1] == 0x4D && signature[2] == 0x52 && signature[3] == 0x4A && signature[4] == 0x4D && signature[5] == 0x42 && signature[6] == 0x50 && signature[7] == 0;
            if (!native) { foreach (bool step in ImportHelperJson(operation)) yield return step; yield break; }
            using (var digest = SHA256.Create())
            {
                foreach (bool step in ReadExchange(operation, 16, digest)) yield return step;
                byte[] head = operation.Read;
                if (head[0] != 0x4A || head[1] != 0x4D || head[2] != 0x52 || head[3] != 0x4A || head[4] != 0x4D || head[5] != 0x42 || head[6] != 0x50 || head[7] != 0 || Int32At(head, 8) != 1) throw new InvalidDataException("blueprint-exchange-format-version");
                uint nameLength = UInt32At(head, 12); if (nameLength < 1 || nameLength > 32768) throw new InvalidDataException("blueprint-exchange-name-length");
                foreach (bool step in ReadExchange(operation, (int)nameLength, digest)) yield return step;
                string name = new UTF8Encoding(false, true).GetString(operation.Read), nameError; if (name.Length == 0 || name[0] == '\uFEFF' || !LibraryName.TryValidate(name, out nameError)) throw new InvalidDataException("blueprint-exchange-name");
                foreach (bool step in ReadExchange(operation, 4, digest)) yield return step;
                uint rootLength = UInt32At(operation.Read, 0); if (rootLength < 1 || rootLength > 65536) throw new InvalidDataException("blueprint-exchange-root-length");
                foreach (bool step in ReadExchange(operation, (int)rootLength, digest)) yield return step;
                byte[] rootBytes = operation.Read; LibraryRoot root = LibraryCodec.DecodeRoot(rootBytes);
                if (!String.Equals(root.Summary.TargetVersion, operation.Request.SupportedTargetVersion, StringComparison.Ordinal) || root.Summary.UnreliableObjectCount != 0) throw new InvalidDataException("blueprint-exchange-target-version-or-unreliable");
                foreach (bool step in ReadExchange(operation, 8, digest)) yield return step;
                long indexLength = Int64At(operation.Read, 0), indexStart = operation.Position;
                if (indexLength != checked(root.BlockCount * 32L) || checked(indexLength + checked(root.BlockCount * 20L) + 32L) > operation.SourceLength - operation.Position) throw new InvalidDataException("blueprint-exchange-index-or-minimum-length");
                using (var indexHash = SHA256.Create())
                {
                    for (long left = indexLength; left > 0;)
                    {
                        int take = (int)Math.Min(65536, left); foreach (bool step in ReadExchange(operation, take, digest)) yield return step;
                        indexHash.TransformBlock(operation.Read, 0, take, null, 0); left -= take;
                    }
                    indexHash.TransformFinalBlock(Array.Empty<byte>(), 0, 0); if (!LibraryCodec.Equal(indexHash.Hash, root.IndexHash)) throw new InvalidDataException("blueprint-exchange-index-hash");
                }
                operation.Stage = Guid.NewGuid(); operation.Asset = Guid.NewGuid(); files.BeginStage(operation.Stage); yield return true;
                for (long number = 0; number < root.BlockCount; number++)
                {
                    foreach (bool step in ReadExchange(operation, 4, digest)) yield return step;
                    uint length = UInt32At(operation.Read, 0); if (length < 16 || length > 1048576 || checked((root.BlockCount - number - 1) * 20L + 32L + length) > operation.SourceLength - operation.Position) throw new InvalidDataException("blueprint-exchange-chunk-length");
                    foreach (bool step in ReadExchange(operation, (int)length, digest)) yield return step;
                    byte[] bytes = operation.Read; byte[] hash = LibraryCodec.Hash(bytes); LibraryCodec.DecodeChunk(bytes);
                    foreach (bool step in ReadExchange(operation, 32, null, checked(indexStart + number * 32))) yield return step;
                    if (!LibraryCodec.Equal(hash, operation.Read)) throw new InvalidDataException("blueprint-exchange-chunk-hash");
                    files.WriteStageBlock(operation.Stage, operation.Blocks, bytes, hash); operation.Blocks++; yield return true;
                }
                digest.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                foreach (bool step in ReadExchange(operation, 32)) yield return step;
                if (!LibraryCodec.Equal(operation.Read, digest.Hash) || operation.Position != operation.SourceLength) throw new InvalidDataException("blueprint-exchange-footer-or-tail");
                foreach (bool step in PublishImport(operation, root, rootBytes, name)) yield return step;
            }
        }
        private IEnumerable<bool> PublishImport(ExchangeOperation operation, LibraryRoot root, byte[] rootBytes, string name)
        {
            var incremental = files as IBlueprintLibraryExchangeSteps;
            if (incremental == null) throw new InvalidOperationException("blueprint-exchange-incremental-stage-unavailable");
            foreach (int ignored in incremental.SealStageSteps(operation.Stage, rootBytes, 32)) yield return true;
            exchangePhase = BlueprintExchangePhase.Validating; yield return true;
            foreach (bool step in ValidateExchange(operation, operation.Stage, true, root)) yield return step;
            string contentHash = LibraryCodec.HashString(rootBytes);
            foreach (Guid candidate in files.EnumerateAssets())
            {
                LibraryMetadata metadata = null;
                try { PreferenceReadResult read = files.OpenMetadata(candidate); if (read.Status == PreferenceReadStatus.Loaded && !protectedAssets.Contains(candidate)) metadata = LibraryCodec.DecodeMetadata(read.Contents, candidate); }
                catch { } finally { files.CloseMetadata(); }
                yield return true;
                if (metadata == null || metadata.Deleted || metadata.ContentHash != contentHash) continue;
                // A corrupt matching metadata claim is not a duplicate. The
                // iterator is stepped here so failures isolate only that item.
                IEnumerator<bool> health = HealthyExchangeCandidate(operation, metadata).GetEnumerator(); bool healthy = true;
                while (true)
                {
                    bool next; try { next = health.MoveNext(); } catch { healthy = false; next = false; }
                    if (!next) break; yield return true;
                }
                health.Dispose();
                if (healthy) { operation.Asset = candidate; operation.Completion = ExchangeResult(operation, BlueprintExchangeStatus.AlreadyExists, "蓝图已存在，已定位到原条目"); yield break; }
            }
            exchangePhase = BlueprintExchangePhase.Publishing;
            // Proof scanning is still cancellable. Only the final atomic
            // promotion/publish step crosses the irreversible boundary.
            IEnumerator<int> promotion = incremental.PromoteStageSteps(operation.Stage, operation.Asset, 32).GetEnumerator();
            try { while (promotion.MoveNext()) { if (promotion.Current < 0) { operation.Publishing = true; operation.Promoted = true; } yield return true; } }
            finally { promotion.Dispose(); }
            operation.Publishing = true; operation.Promoted = true;
            PreferenceReadResult missing = files.OpenMetadata(operation.Asset, true); if (missing.Status != PreferenceReadStatus.Missing) throw new InvalidDataException("blueprint-exchange-publish-conflict");
            long now = DateTime.UtcNow.Ticks;
            var value = new LibraryMetadata { AssetId = operation.Asset, ContentRevision = root.ContentRevision, ContentHash = contentHash, Name = name, Width = root.Summary.Width, Height = root.Summary.Height, ContentCount = root.Summary.ContentCount, BlockCount = root.BlockCount, MissingSummary = String.Join("；", root.Summary.MissingPayloads), CreatedTicks = now, UpdatedTicks = now };
            PreferenceWriteResult written = files.WriteMetadata(missing.Identity, LibraryCodec.EncodeMetadata(value)); files.CloseMetadata();
            if (written.Status != PreferenceWriteStatus.Saved)
            { operation.Publishing = written.CommitUnconfirmed; protectedAssets.Add(operation.Asset); throw new IOException("blueprint-exchange-metadata-not-saved"); }
            operation.Completion = ExchangeResult(operation, BlueprintExchangeStatus.Completed, "蓝图已导入");
        }
        private IEnumerable<bool> HealthyExchangeCandidate(ExchangeOperation operation, LibraryMetadata metadata)
        {
            LibraryRoot root = ReadAssetRoot(metadata);
            foreach (bool step in ValidateExchange(operation, metadata.AssetId, false, root)) yield return step;
        }
        private IEnumerable<bool> ExportExchange(ExchangeOperation operation)
        {
            BlueprintExchangeRequest request = operation.Request; BlueprintTemplateKey key = request.TemplateKey;
            if (key == null || request.Proposal == null || request.Proposal.RequestId != request.RequestId || request.CounterCandidate < request.Proposal.CounterCandidate) throw new InvalidDataException("blueprint-exchange-export-proposal");
            LibraryMetadata metadata;
            PreferenceReadResult read = files.OpenMetadata(key.AssetId);
            if (read.Status != PreferenceReadStatus.Loaded) throw new InvalidDataException("blueprint-exchange-source-metadata");
            metadata = LibraryCodec.DecodeMetadata(read.Contents, key.AssetId); string sourceIdentity = read.Identity; files.CloseMetadata();
            if (metadata.Deleted || protectedAssets.Contains(key.AssetId) || metadata.ContentRevision != key.ContentRevision || metadata.ContentHash != key.ContentHash || metadata.Name != request.Name) throw new InvalidDataException("blueprint-exchange-source-changed");
            byte[] rootBytes = files.ReadRoot(key.AssetId, false); LibraryRoot root = ReadAssetRoot(metadata);
            if (LibraryCodec.HashString(rootBytes) != key.ContentHash) throw new InvalidDataException("blueprint-exchange-source-changed");
            if (!String.Equals(root.Summary.TargetVersion, request.SupportedTargetVersion, StringComparison.Ordinal)) throw new InvalidDataException("blueprint-exchange-target-version");
            exchangePhase = BlueprintExchangePhase.Validating;
            foreach (bool step in ValidateExchange(operation, key.AssetId, false, root)) yield return step;
            foreach (bool step in Io(operation, new BlueprintExchangeIoCommand { Kind = BlueprintExchangeIoKind.BeginTarget, Ticket = request.Ticket })) yield return step;
            exchangePhase = BlueprintExchangePhase.ReadingOrWriting;
            using (var digest = SHA256.Create())
            {
                byte[] name = new UTF8Encoding(false, true).GetBytes(request.Name);
                byte[] header; using (var memory = new MemoryStream()) using (var writer = new BinaryWriter(memory))
                { writer.Write(new byte[] { 0x4A, 0x4D, 0x52, 0x4A, 0x4D, 0x42, 0x50, 0 }); writer.Write(1); writer.Write((uint)name.Length); writer.Write(name); writer.Write((uint)rootBytes.Length); writer.Write(rootBytes); writer.Write(checked(root.BlockCount * 32L)); writer.Flush(); header = memory.ToArray(); }
                foreach (bool step in WriteExchange(operation, header, digest)) yield return step;
                using (Stream index = files.OpenIndex(key.AssetId, false))
                using (var indexHash = SHA256.Create())
                {
                    byte[] buffer = new byte[65536]; int count;
                    while ((count = index.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        indexHash.TransformBlock(buffer, 0, count, null, 0); byte[] segment = count == buffer.Length ? buffer : Slice(buffer, count);
                        foreach (bool step in WriteExchange(operation, segment, digest)) yield return step;
                    }
                    indexHash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    if (!LibraryCodec.Equal(indexHash.Hash, root.IndexHash) || index.Length != checked(root.BlockCount * 32L)) throw new InvalidDataException("blueprint-exchange-source-index-changed");
                }
                for (long number = 0; number < root.BlockCount; number++)
                {
                    byte[] bytes = files.ReadBlock(key.AssetId, false, number);
                    // Recheck the bytes actually exported against the immutable
                    // index, rather than relying on an earlier same-path pass.
                    using (Stream index = files.OpenIndex(key.AssetId, false)) { index.Position = checked(number * 32L); byte[] expected = new byte[32]; ReadExactly(index, expected); if (!LibraryCodec.Equal(LibraryCodec.Hash(bytes), expected)) throw new InvalidDataException("blueprint-exchange-source-changed"); }
                    foreach (bool step in WriteExchange(operation, UInt32Bytes((uint)bytes.Length), digest)) yield return step;
                    foreach (bool step in WriteExchange(operation, bytes, digest)) yield return step;
                }
                digest.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                foreach (bool step in WriteExchange(operation, digest.Hash)) yield return step;
            }
            read = files.OpenMetadata(key.AssetId); files.CloseMetadata(); if (read.Status != PreferenceReadStatus.Loaded || read.Identity != sourceIdentity || !LibraryCodec.Equal(files.ReadRoot(key.AssetId, false), rootBytes)) throw new InvalidDataException("blueprint-exchange-source-changed");
            exchangePhase = BlueprintExchangePhase.Publishing; operation.Publishing = true;
            operation.OutputDigest.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            foreach (bool step in Io(operation, new BlueprintExchangeIoCommand { Kind = BlueprintExchangeIoKind.CommitTarget, ExpectedDigest = operation.OutputDigest.Hash, ExpectedLength = operation.OutputLength })) yield return step;
            operation.Completion = ExchangeResult(operation, BlueprintExchangeStatus.Completed, "蓝图已导出"); operation.Completion.FileCommitted = true;
            bool retainedRecovery = !operation.Io.Success;
            exchangeHighDate = request.Proposal.ExportDate; exchangeHighSequence = request.CounterCandidate;
            try
            {
                PreferenceWriteResult written = exchangeCounter.Write(exchangeCounterIdentity, BlueprintExchangeCounter.Encode(exchangeHighDate, exchangeHighSequence));
                operation.Completion.CounterCommitted = written.Status == PreferenceWriteStatus.Saved;
                if (operation.Completion.CounterCommitted)
                { exchangeCounterIdentity = written.Identity; exchangeSavedDate = exchangeHighDate; exchangeSavedSequence = exchangeHighSequence; }
                else exchangeCounterProtected = true;
            }
            catch { exchangeCounterProtected = true; }
            if (!operation.Completion.CounterCommitted) operation.Completion.Message = "文件已导出，但未能保存编号；下次默认编号可能重新避让";
            if (retainedRecovery) operation.Completion.Message += "；相关恢复文件已保留";
        }
        private IEnumerable<bool> FinishExchange(ExchangeOperation operation)
        {
            if (operation.Stage != Guid.Empty && !operation.Promoted)
            {
                bool done = false;
                while (!done) { try { done = files.CleanupStep(operation.Stage, true, operation.Blocks, 32); } catch { done = true; } yield return true; }
                files.CloseCleanup();
            }
            if (operation.Request.Kind != BlueprintExchangeKind.PrepareExport)
                foreach (bool step in Io(operation, new BlueprintExchangeIoCommand { Kind = BlueprintExchangeIoKind.Close })) yield return step;
            if (operation.Io?.ErrorCode == "blueprint-exchange-cleanup-retained" && operation.Completion != null) operation.Completion.Message += "；未能确认临时文件清理，相关文件已保留";
            operation.Request.Ticket?.Dispose();
            operation.OutputDigest?.Dispose();
        }
        private static BlueprintExchangeReceipt ExchangeResult(ExchangeOperation operation, BlueprintExchangeStatus status, string message)
        { return new BlueprintExchangeReceipt { RequestId = operation.Request.RequestId, Kind = operation.Request.Kind, Status = status, Message = message, AssetId = operation.Asset }; }
        private void CloseExchangeWorker()
        {
            exchange?.Steps?.Dispose();
            // The external lane owns only external paths, never files/stage or
            // counter. A stop timeout therefore cannot dispose a live library IO.
            exchangeIo?.BeginStop(); exchangeCounter?.Dispose();
        }
        private static uint UInt32At(byte[] bytes, int at) { return (uint)(bytes[at] | bytes[at + 1] << 8 | bytes[at + 2] << 16 | bytes[at + 3] << 24); }
        private static int Int32At(byte[] bytes, int at) { return unchecked((int)UInt32At(bytes, at)); }
        private static long Int64At(byte[] bytes, int at) { return unchecked((long)((ulong)UInt32At(bytes, at) | (ulong)UInt32At(bytes, at + 4) << 32)); }
        private static byte[] UInt32Bytes(uint value) { return new[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24) }; }
        private static byte[] Slice(byte[] bytes, int count) { var result = new byte[count]; Array.Copy(bytes, result, count); return result; }
    }
}
