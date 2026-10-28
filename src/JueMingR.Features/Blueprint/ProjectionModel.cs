using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    // Origin is deliberately absent: moving the same immutable asset must not
    // invalidate its decoded blocks. Host owns session/selection generations.
    public sealed class BlueprintProjectionIdentity : IEquatable<BlueprintProjectionIdentity>
    {
        public BlueprintProjectionIdentity(Guid assetId, Guid contentRevision, string contentHash, long sessionGeneration, long projectionGeneration)
        {
            if (assetId == Guid.Empty || contentRevision == Guid.Empty || String.IsNullOrEmpty(contentHash)) throw new ArgumentException("Invalid projection identity.");
            TemplateKey = new BlueprintTemplateKey(assetId, contentRevision, contentHash); AssetId = assetId; ContentRevision = contentRevision; ContentHash = contentHash; SessionGeneration = sessionGeneration; ProjectionGeneration = projectionGeneration;
        }
        public BlueprintTemplateKey TemplateKey { get; }
        public Guid AssetId { get; }
        public Guid ContentRevision { get; }
        public string ContentHash { get; }
        public long SessionGeneration { get; }
        public long ProjectionGeneration { get; }
        public bool Equals(BlueprintProjectionIdentity other)
        { return other != null && AssetId == other.AssetId && ContentRevision == other.ContentRevision && ContentHash == other.ContentHash && SessionGeneration == other.SessionGeneration && ProjectionGeneration == other.ProjectionGeneration; }
        public override bool Equals(object value) { return Equals(value as BlueprintProjectionIdentity); }
        public override int GetHashCode() { unchecked { return AssetId.GetHashCode() * 397 ^ ContentRevision.GetHashCode() ^ ContentHash.GetHashCode() ^ SessionGeneration.GetHashCode() ^ ProjectionGeneration.GetHashCode(); } }
    }

    public readonly struct BlueprintProjectionBounds : IEquatable<BlueprintProjectionBounds>
    {
        public BlueprintProjectionBounds(int left, int top, int width, int height)
        {
            if (width < 0 || height < 0 || (long)left + width > Int32.MaxValue || (long)top + height > Int32.MaxValue) throw new ArgumentOutOfRangeException(nameof(width));
            Left = left; Top = top; Width = width; Height = height;
        }
        public int Left { get; }
        public int Top { get; }
        public int Width { get; }
        public int Height { get; }
        public int Right { get { return Left + Width; } }
        public int Bottom { get { return Top + Height; } }
        public bool Contains(int x, int y) { return x >= Left && y >= Top && x < Right && y < Bottom; }
        public BlueprintProjectionBounds Expand(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            return new BlueprintProjectionBounds(checked(Left - amount), checked(Top - amount), checked(Width + amount * 2), checked(Height + amount * 2));
        }
        public bool Equals(BlueprintProjectionBounds other) { return Left == other.Left && Top == other.Top && Width == other.Width && Height == other.Height; }
        public override bool Equals(object value) { return value is BlueprintProjectionBounds && Equals((BlueprintProjectionBounds)value); }
        public override int GetHashCode() { unchecked { return ((Left * 397 ^ Top) * 397 ^ Width) * 397 ^ Height; } }
    }

    public sealed class BlueprintProjectionWindow
    {
        public BlueprintProjectionWindow(BlueprintProjectionIdentity identity, long windowGeneration, CaptureSummary summary, BlueprintProjectionBounds bounds, CaptureCell[] cells, CaptureObject[] objects)
            : this(identity, windowGeneration, summary, bounds, cells, objects, false) { }
        internal BlueprintProjectionWindow(BlueprintProjectionIdentity identity, long windowGeneration, CaptureSummary summary, BlueprintProjectionBounds bounds, CaptureCell[] cells, CaptureObject[] objects, bool owned, BlueprintProjectionMask appliedMask = null)
        {
            AppliedMask = appliedMask;
            Identity = identity ?? throw new ArgumentNullException(nameof(identity)); WindowGeneration = windowGeneration; Summary = LibraryState.Copy(summary) ?? throw new ArgumentNullException(nameof(summary)); Bounds = bounds;
            Cells = Array.AsReadOnly(owned ? cells : (CaptureCell[])cells.Clone()); Objects = Array.AsReadOnly(owned ? objects : (CaptureObject[])objects.Clone());
            long bytes = checked(256L + cells.LongLength * 64L + objects.LongLength * 128L);
            foreach (CaptureObject obj in objects) bytes = checked(bytes + (long)((obj.Reason ?? "").Length + (obj.MissingPayload ?? "").Length) * 2);
            EstimatedBytes = bytes;
        }
        public BlueprintProjectionIdentity Identity { get; }
        public long WindowGeneration { get; }
        // Host-effective snapshots retain the exact immutable mask identity.
        // Raw worker/cache DTOs leave this null and are never changed in place.
        public BlueprintProjectionMask AppliedMask { get; }
        // Summary retains the saved source origin. Cell/object X/Y below have
        // ALREADY had Summary.Left/Top subtracted; raw frames remain unchanged.
        public CaptureSummary Summary { get; }
        public BlueprintProjectionBounds Bounds { get; }
        public IReadOnlyList<CaptureCell> Cells { get; }
        public IReadOnlyList<CaptureObject> Objects { get; }
        // Conservative retained DTO payload estimate; excludes transient query
        // lists and the separately budgeted decoded-block cache/directory.
        public long EstimatedBytes { get; }
    }

    public enum BlueprintProjectionReceiptKind { Opened, Window, Failed, Closed }
#if DEBUG
    // Development-only counters for the bounded-work checks. No Release
    // sampling, logging, timers, or diagnostic file output is installed.
    public readonly struct BlueprintProjectionStatistics
    {
        internal BlueprintProjectionStatistics(long reads, long records, long comparisons, long windowBlocks, long slices, long retired, long cachePeak, long directoryBytes, long directoryBlocks, long cacheBytes, long templates = 0, long references = 0, long pending = 0, long receipts = 0, long working = 0, long sourceIndexBytes = 0)
        { BlockReads = reads; DecodedRecords = records; DirectoryComparisons = comparisons; WindowBlocks = windowBlocks; Slices = slices; RetiredRequests = retired; CachePeakBytes = cachePeak; DirectoryBytes = directoryBytes; DirectoryBlocks = directoryBlocks; CacheBytes = cacheBytes; ActiveTemplates = templates; ActiveReferences = references; PendingRequests = pending; ReceiptBytes = receipts; WindowWorkingBytes = working; SourceIndexBytes = sourceIndexBytes; }
        public long SourceIndexBytes { get; }
        public long ActiveTemplates { get; }
        public long ActiveReferences { get; }
        public long PendingRequests { get; }
        public long ReceiptBytes { get; }
        public long WindowWorkingBytes { get; }
        public long BlockReads { get; }
        public long DecodedRecords { get; }
        public long DirectoryComparisons { get; }
        public long WindowBlocks { get; }
        public long Slices { get; }
        public long RetiredRequests { get; }
        public long CachePeakBytes { get; }
        public long DirectoryBytes { get; }
        public long DirectoryBlocks { get; }
        public long CacheBytes { get; }
    }
#endif
    public sealed class BlueprintProjectionReceipt
    {
        internal BlueprintProjectionReceipt(BlueprintProjectionReceiptKind kind, BlueprintProjectionIdentity identity, long windowGeneration = 0, CaptureSummary summary = null, BlueprintProjectionWindow window = null, string error = null, string failureCode = null, long totalLayerCount = 0, IReadOnlyList<BlueprintProjectionSourceRange> sourceRanges = null, IReadOnlyList<BlueprintProjectionSourceRange> matchRanges = null, bool identityRetired = false, long totalConstructionLayerCount = 0, string name = null)
        { Name = name; MatchRanges = matchRanges; SourceRanges = sourceRanges; TotalLayerCount = totalLayerCount; TotalConstructionLayerCount = totalConstructionLayerCount; Kind = kind; Identity = identity; WindowGeneration = windowGeneration; Summary = LibraryState.Copy(summary); Window = window; Error = error; FailureCode = failureCode; IdentityRetired = identityRetired; }
        public string Name { get; }
        public IReadOnlyList<BlueprintProjectionSourceRange> SourceRanges { get; }
        public IReadOnlyList<BlueprintProjectionSourceRange> MatchRanges { get; }
        public long TotalLayerCount { get; }
        // Display-open's non-liquid upper bound is not a qualification proof.
        // Host must await the separate catalog/mask-bound effective count.
        public long TotalConstructionLayerCount { get; }
        public BlueprintProjectionReceiptKind Kind { get; }
        public BlueprintProjectionIdentity Identity { get; }
        public long WindowGeneration { get; }
        public CaptureSummary Summary { get; }
        public BlueprintProjectionWindow Window { get; }
        public string Error { get; }
        public string FailureCode { get; }
        // FailureCode describes the read; this states the worker ownership fact.
        // A source failure can use the same code as a recoverable viewport
        // budget refusal while already having revoked the shared identity.
        public bool IdentityRetired { get; }
    }
}
