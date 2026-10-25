using System;
using JueMingR.Platform.Blueprint;

namespace JueMingR.Features.Blueprint
{
    // The game-thread owner sees immutable pages/receipts, never file handles.
    // Closing a UI or retiring a world cannot cancel an accepted asset mutation.
    public sealed class LibraryState : IDisposable
    {
        private readonly LibraryWorker worker;
        private LibrarySnapshot snapshot = new LibrarySnapshot(0, false, null, null);
        private LibraryReceipt receipt;
        private long nextCommand, revision;
        public LibraryState(IBlueprintLibraryFiles files) { worker = new LibraryWorker(files); }
        public LibrarySnapshot Snapshot { get { return snapshot; } }
        public long AcceptedCommandId { get; private set; }
        public bool TryBeginStage(CaptureToken token) { return Submit(new LibraryCommand { Kind = LibraryCommandKind.BeginStage, Id = token.OperationId, Token = token }); }
        public bool TryAppendChunk(ContentHandle handle, CaptureChunk chunk) { return Submit(new LibraryCommand { Kind = LibraryCommandKind.AppendChunk, Id = handle.Id, Chunk = chunk }); }
        public bool TrySealDraft(ContentHandle handle, CaptureSummary summary) { return Submit(new LibraryCommand { Kind = LibraryCommandKind.SealDraft, Id = handle.Id, Summary = Copy(summary) }); }
        public bool TryDiscardDraft(ContentHandle handle) { return Submit(new LibraryCommand { Kind = LibraryCommandKind.DiscardDraft, Id = handle.Id }); }
        public bool TryCreate(ContentHandle handle, string name, CaptureToken token, bool excludeUnreliable = false, int[] excludedObjectIds = null)
        { return Submit(new LibraryCommand { Kind = LibraryCommandKind.Create, Id = handle.Id, Name = name, Token = token, Exclude = excludeUnreliable, Excluded = Exclusions(excludedObjectIds) }); }
        public bool TryList(long generation, int page, Guid? selectAssetId = null) { return Submit(new LibraryCommand { Kind = LibraryCommandKind.List, Generation = generation, Page = Math.Max(0, page), SelectId = selectAssetId }); }
        public bool TryObjects(long generation, ContentHandle handle, int page) { return Submit(new LibraryCommand { Kind = LibraryCommandKind.Objects, Id = handle.Id, Generation = generation, Page = Math.Max(0, page) }); }
        public bool TryPreview(long generation, ContentHandle handle, int width, int height, bool excludeUnreliable = false, int[] excludedObjectIds = null)
        { return Submit(new LibraryCommand { Kind = LibraryCommandKind.Preview, Id = handle.Id, Stage = true, Generation = generation, Width = width, Height = height, Exclude = excludeUnreliable, Excluded = Exclusions(excludedObjectIds) }); }
        public bool TryPreview(long generation, Guid assetId, string contentHash, int width, int height)
        { return Submit(new LibraryCommand { Kind = LibraryCommandKind.Preview, Id = assetId, Identity = contentHash, Generation = generation, Width = width, Height = height }); }
        public bool TryRename(Guid assetId, string expectedMetadataIdentity, string name) { return Submit(new LibraryCommand { Kind = LibraryCommandKind.Rename, Id = assetId, Identity = expectedMetadataIdentity, Name = name }); }
        public bool TryDelete(Guid assetId, string expectedMetadataIdentity) { return Submit(new LibraryCommand { Kind = LibraryCommandKind.Delete, Id = assetId, Identity = expectedMetadataIdentity }); }
        public void CancelRead(long generation) { worker.CancelRead(generation); }
        // Projection work has its own bounded latest-request/result mailboxes;
        // it never owns the accepted capture/save command slot or its token.
        public bool RequestProjectionOpen(BlueprintProjectionIdentity identity) { return worker.RequestProjectionOpen(identity); }
        public bool RequestProjectionWindow(BlueprintProjectionIdentity identity, long windowGeneration, BlueprintProjectionBounds localBounds) { return worker.RequestProjectionWindow(identity, windowGeneration, localBounds); }
        public void CloseProjection() { worker.CloseProjection(); }
        public void ReleaseProjection(BlueprintProjectionIdentity identity) { worker.ReleaseProjection(identity); }
        public void ClearProjectionWindow(BlueprintProjectionIdentity identity) { worker.ClearProjectionWindow(identity); }
        public bool RequestProjectionCommit(BlueprintProjectionCommitRequest request) { return worker.RequestProjectionCommit(request); }
        public void CancelProjectionCommit(long sessionGeneration, long transactionId) { worker.CancelProjectionCommit(sessionGeneration, transactionId); }
        public bool TryTakeProjectionCommit(out BlueprintProjectionCommitReceipt receipt) { return worker.TryTakeProjectionCommit(out receipt); }
        public bool TryTakeProjectionReceipt(out BlueprintProjectionReceipt value) { return worker.TryTakeProjectionReceipt(out value); }
        public bool RequestProjectionSource(BlueprintProjectionIdentity identity, long generation, int range, int objectId = 0, int offset = 0) { return worker.RequestProjectionSource(identity, generation, range, objectId, offset); }
        public void ClearProjectionSource(BlueprintProjectionIdentity identity) { worker.ClearProjectionSource(identity); }
        public bool TryTakeProjectionSource(out BlueprintProjectionSourceReceipt receipt) { return worker.TryTakeProjectionSource(out receipt); }
#if DEBUG
        public BlueprintProjectionStatistics DebugProjectionStatistics { get { return worker.ProjectionStatistics; } }
#endif
        public void Pump()
        {
            LibraryReceipt value;
            if (receipt == null && worker.TryTake(out value))
            {
                receipt = value;
                LibraryPage page = value.Page ?? snapshot.Page;
                if (value.IsProtected && page != null) page = page.Protect(value.AssetId);
                snapshot = new LibrarySnapshot(++revision, true, page, value, worker.Processed);
            }
            else if (snapshot.Busy != (worker.Busy || receipt != null) || snapshot.ProcessedItems != worker.Processed)
                snapshot = new LibrarySnapshot(++revision, worker.Busy || receipt != null, snapshot.Page, snapshot.LastReceipt, worker.Processed);
        }
        public bool TryTakeReceipt(out LibraryReceipt value)
        {
            value = receipt; if (value == null) return false; receipt = null;
            snapshot = new LibrarySnapshot(++revision, worker.Busy, snapshot.Page, snapshot.LastReceipt, worker.Processed); return true;
        }
        public void BeginStop() { worker.BeginStop(); }
        public bool Stop(int milliseconds) { return worker.Stop(milliseconds); }
        public void Dispose() { if (!Stop(3000)) throw new TimeoutException("Blueprint worker still owns its files."); }
        private bool Submit(LibraryCommand command)
        {
            if (receipt != null) return false; command.CommandId = ++nextCommand;
            if (!worker.TrySubmit(command)) return false;
            AcceptedCommandId = command.CommandId;
            snapshot = new LibrarySnapshot(++revision, true, snapshot.Page, snapshot.LastReceipt, 0); return true;
        }
        private static int[] Exclusions(int[] ids) { if (ids == null || ids.Length == 0) return Array.Empty<int>(); var result = (int[])ids.Clone(); Array.Sort(result); for (int i = 0; i < result.Length; i++) if (result[i] < 1 || i > 0 && result[i] == result[i - 1]) throw new ArgumentException("Invalid excluded object IDs."); return result; }
        internal static CaptureSummary Copy(CaptureSummary source)
        {
            if (source == null) return null;
            return new CaptureSummary { Left = source.Left, Top = source.Top, Width = source.Width, Height = source.Height, ContentCount = source.ContentCount, UnreliableObjectCount = source.UnreliableObjectCount, StartTick = source.StartTick, EndTick = source.EndTick, TargetVersion = source.TargetVersion, MissingPayloads = source.MissingPayloads == null ? Array.Empty<string>() : (string[])source.MissingPayloads.Clone(), UnreliableReasons = source.UnreliableReasons == null ? Array.Empty<string>() : (string[])source.UnreliableReasons.Clone() };
        }
    }
    internal sealed class LibraryCommand
    {
        internal long CommandId, Generation;
        internal LibraryCommandKind Kind;
        internal Guid Id;
        internal Guid? SelectId;
        internal CaptureToken Token;
        internal CaptureChunk Chunk;
        internal CaptureSummary Summary;
        internal string Name, Identity;
        internal int Page, Width, Height;
        internal bool Stage, Exclude;
        internal int[] Excluded = Array.Empty<int>();
    }
}
