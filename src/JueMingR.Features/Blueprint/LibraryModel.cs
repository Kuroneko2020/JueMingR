using System;

namespace JueMingR.Features.Blueprint
{
    public enum LibraryCommandKind { BeginStage, AppendChunk, SealDraft, DiscardDraft, Create, List, Preview, Rename, Delete, Objects }
    public enum LibraryStatus { Completed, Saved, Deleted, Failed, CommitUnconfirmed, Protected, Cancelled }
    public sealed class LibraryEntry
    {
        internal LibraryEntry(Guid id, string identity, LibraryMetadata value, bool protect = false)
        { AssetId = id; MetadataIdentity = identity; Name = value.Name; ContentRevision = value.ContentRevision.ToString("N"); ContentHash = value.ContentHash; Width = value.Width; Height = value.Height; ContentCount = value.ContentCount; MissingSummary = value.MissingSummary; IsProtected = protect; }
        private LibraryEntry(LibraryEntry value)
        { AssetId = value.AssetId; MetadataIdentity = value.MetadataIdentity; Name = value.Name; ContentRevision = value.ContentRevision; ContentHash = value.ContentHash; Width = value.Width; Height = value.Height; ContentCount = value.ContentCount; MissingSummary = value.MissingSummary; IsProtected = true; }
        internal LibraryEntry Protect() { return IsProtected ? this : new LibraryEntry(this); }
        public Guid AssetId { get; }
        public string MetadataIdentity { get; }
        public string Name { get; }
        public string ContentRevision { get; }
        public string ContentHash { get; }
        public int Width { get; }
        public int Height { get; }
        public long ContentCount { get; }
        public string MissingSummary { get; }
        public bool IsProtected { get; }
    }
    public sealed class LibraryPage
    {
        internal LibraryPage(long generation, int page, long count, LibraryEntry[] entries, int isolated, bool protect, string error)
        { Generation = generation; PageIndex = page; TotalCount = count; Entries = Array.AsReadOnly(entries); IsolatedCount = isolated; IsProtected = protect; Error = error; }
        public long Generation { get; }
        public int PageIndex { get; }
        public long PageCount { get { return TotalCount / 6 + (TotalCount % 6 == 0 ? 0 : 1); } }
        public long TotalCount { get; }
        public System.Collections.Generic.IReadOnlyList<LibraryEntry> Entries { get; }
        public int IsolatedCount { get; }
        public bool IsProtected { get; }
        public string Error { get; }
        internal LibraryPage Protect(Guid id)
        {
            var values = new LibraryEntry[Entries.Count]; bool found = false;
            for (int i = 0; i < values.Length; i++) { values[i] = Entries[i]; if (values[i].AssetId == id) { values[i] = values[i].Protect(); found = true; } }
            return found ? new LibraryPage(Generation, PageIndex, TotalCount, values, IsolatedCount, IsProtected, Error) : this;
        }
    }
    public sealed class LibraryObjectPage
    {
        internal LibraryObjectPage(long generation, int page, long count, CaptureObject[] items) { Generation = generation; PageIndex = page; TotalCount = count; Items = Array.AsReadOnly(items); }
        public long Generation { get; }
        public int PageIndex { get; }
        public long TotalCount { get; }
        public long PageCount { get { return TotalCount / 6 + (TotalCount % 6 == 0 ? 0 : 1); } }
        public System.Collections.Generic.IReadOnlyList<CaptureObject> Items { get; }
    }
    public sealed class LibraryReceipt
    {
        // Protected is a broad durable-data guard, not proof that a delete
        // reached its metadata commit. Only the mechanical writer can set this
        // narrower fact before the receipt is published to the Host owner.
        public bool DeleteProvenUncommitted { get; internal set; }
        // A reliable rename is a metadata fact independent of the current UI
        // page. Never publish the proposed name on an uncertain commit.
        public string CommittedName { get; internal set; }
        internal LibraryReceipt(long commandId, LibraryCommandKind kind, LibraryStatus status, Guid operation, Guid asset, CaptureToken token, long generation, string error = null, bool protect = false, bool cleanup = false, LibraryPage page = null, BlueprintPreviewImage preview = null, LibraryObjectPage objectPage = null, CaptureSummary summary = null, string failureCode = null, string suggestedName = null, bool operationRetired = false)
        { CommandId = commandId; Kind = kind; Status = status; OperationId = operation; AssetId = asset; Token = token; Generation = generation; Error = error; IsProtected = protect; CleanupPending = cleanup; Page = page; Preview = preview; ObjectPage = objectPage; Summary = LibraryState.Copy(summary); FailureCode = failureCode; SuggestedName = suggestedName; OperationRetired = operationRetired; }
        public long CommandId { get; }
        public LibraryCommandKind Kind { get; }
        public LibraryStatus Status { get; }
        public Guid OperationId { get; }
        public Guid AssetId { get; }
        public CaptureToken Token { get; }
        public long Generation { get; }
        public string Error { get; }
        public string FailureCode { get; }
        public string SuggestedName { get; }
        public bool IsProtected { get; }
        // Retirement releases the active slot, not the protected files. It is
        // independent of whether metadata is known absent or commit is unknown.
        public bool OperationRetired { get; }
        public bool CleanupPending { get; }
        public LibraryPage Page { get; }
        public BlueprintPreviewImage Preview { get; }
        public LibraryObjectPage ObjectPage { get; }
        public CaptureSummary Summary { get; }
        public ContentHandle ContentHandle { get { return new ContentHandle(OperationId); } }
    }
    public sealed class LibrarySnapshot
    {
        internal LibrarySnapshot(long revision, bool busy, LibraryPage page, LibraryReceipt receipt, long processed = 0) { Revision = revision; Busy = busy; Page = page; LastReceipt = receipt; ProcessedItems = processed; }
        public long Revision { get; }
        public bool Busy { get; }
        public LibraryPage Page { get; }
        public LibraryReceipt LastReceipt { get; }
        public long ProcessedItems { get; }
    }
    internal sealed class LibraryMetadata
    {
        internal Guid AssetId, ContentRevision;
        internal string Name, ContentHash, MissingSummary, CleanupSourceIdentity, CleanupIndexIdentity;
        internal int Width, Height;
        internal long ContentCount, BlockCount, CreatedTicks, UpdatedTicks;
        internal bool Deleted;
    }
    internal sealed class LibraryRoot
    {
        internal Guid ContentRevision;
        internal CaptureSummary Summary;
        internal long BlockCount;
        internal byte[] IndexHash;
    }
}
