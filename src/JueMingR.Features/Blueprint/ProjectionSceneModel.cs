using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace JueMingR.Features.Blueprint
{
    public sealed class BlueprintTemplateKey : IEquatable<BlueprintTemplateKey>
    {
        public BlueprintTemplateKey(Guid assetId, Guid contentRevision, string contentHash)
        { AssetId = assetId; ContentRevision = contentRevision; ContentHash = contentHash ?? throw new ArgumentNullException(nameof(contentHash)); }
        public Guid AssetId { get; }
        public Guid ContentRevision { get; }
        public string ContentHash { get; }
        public bool Equals(BlueprintTemplateKey other) { return other != null && AssetId == other.AssetId && ContentRevision == other.ContentRevision && ContentHash == other.ContentHash; }
        public override bool Equals(object value) { return Equals(value as BlueprintTemplateKey); }
        public override int GetHashCode() { unchecked { return AssetId.GetHashCode() * 397 ^ ContentRevision.GetHashCode() ^ ContentHash.GetHashCode(); } }
    }

    // Masks are session facts owned by Host, never mutations of the library.
    // They only grow after a commit: moving/removing a covering instance cannot
    // resurrect an older layer. Object IDs are local to this mask's instance.
    public sealed class BlueprintProjectionMask
    {
        private readonly Dictionary<long, ushort> cells;
        private readonly HashSet<int> objects;
        public static BlueprintProjectionMask Empty { get; } = new BlueprintProjectionMask(new Dictionary<long, ushort>(), new HashSet<int>(), 0);
        internal BlueprintProjectionMask(Dictionary<long, ushort> cells, HashSet<int> objects, long removed)
        { this.cells = cells; this.objects = objects; RemovedLayerCount = removed; }
        public long RemovedLayerCount { get; }
        public int CellCount { get { return cells.Count; } }
        public int ObjectCount { get { return objects.Count; } }
        // The shared source-position index carries only layer bits/object ID.
        // Observation can apply the current immutable mask without recreating
        // a CaptureCell or retaining any template/world payload per instance.
        internal ushort RemainingLayers(int x, int y, int objectId, ushort layers)
        {
            ushort removed; cells.TryGetValue(ProjectionDirectory.Key(x, y), out removed);
            if (objectId > 0 && objects.Contains(objectId)) removed |= 1;
            return (ushort)(layers & ~removed);
        }
        internal bool AnyRemainingLayers(BlueprintProjectionBounds bounds, int objectId, ushort layers)
        {
            if (objectId > 0 && objects.Contains(objectId)) layers &= 0xfe;
            if (layers == 0 || bounds.Width <= 0 || bounds.Height <= 0) return false;
            // A rectangle with more positions than the sparse removal table
            // necessarily contains an untouched position. Otherwise enumerate
            // at most that table's count; ordinary field stores query one cell.
            if ((long)bounds.Width * bounds.Height > cells.Count) return true;
            for (int y = bounds.Top; y < bounds.Bottom; y++) for (int x = bounds.Left; x < bounds.Right; x++)
                if (RemainingLayers(x, y, objectId, layers) != 0) return true;
            return false;
        }
        public CaptureCell Apply(CaptureCell cell)
        {
            if (cells.Count == 0 && objects.Count == 0) return cell;
            ushort removed; cells.TryGetValue(ProjectionDirectory.Key(cell.X, cell.Y), out removed);
            if (cell.ObjectId > 0 && objects.Contains(cell.ObjectId)) removed |= 1;
            if ((removed & 1) != 0) cell = cell.WithoutTile();
            if ((removed & 2) != 0) cell.Layers &= ~CaptureLayer.Wall;
            if ((removed & 4) != 0) cell.Layers &= ~CaptureLayer.Liquid;
            if ((removed & 8) != 0) { cell.Layers &= ~CaptureLayer.Actuator; cell.Actuator = false; }
            cell.Wires &= (byte)~(removed >> 4);
            if (cell.Wires == 0) cell.Layers &= ~CaptureLayer.Wire;
            return cell;
        }
        public BlueprintProjectionWindow Apply(BlueprintProjectionWindow window)
        {
            if (ReferenceEquals(window.AppliedMask, this)) return window;
            var visible = new List<CaptureCell>();
            var referenced = new HashSet<int>();
            foreach (CaptureCell raw in window.Cells)
            {
                CaptureCell cell = Apply(raw); if (!cell.HasContent) continue;
                visible.Add(cell);
                if ((cell.Layers & CaptureLayer.Tile) != 0 && cell.ObjectId > 0) referenced.Add(cell.ObjectId);
            }
            // A halo member may refer to an object whose origin is outside the
            // window. Keep its complete metadata by live Tile reference, not origin.
            var metadata = new List<CaptureObject>(); foreach (CaptureObject obj in window.Objects) if (referenced.Contains(obj.Id)) metadata.Add(obj);
            return new BlueprintProjectionWindow(window.Identity, window.WindowGeneration, window.Summary, window.Bounds, visible.ToArray(), metadata.ToArray(), true, this);
        }
        internal Dictionary<long, ushort> CopyCells() { return new Dictionary<long, ushort>(cells); }
        internal HashSet<int> CopyObjects() { return new HashSet<int>(objects); }
        internal static ushort Layers(CaptureCell cell)
        {
            ushort value = 0;
            if ((cell.Layers & CaptureLayer.Tile) != 0) value |= 1;
            if ((cell.Layers & CaptureLayer.Wall) != 0) value |= 2;
            if ((cell.Layers & CaptureLayer.Liquid) != 0) value |= 4;
            if ((cell.Layers & CaptureLayer.Actuator) != 0) value |= 8;
            if ((cell.Layers & CaptureLayer.Wire) != 0) value |= (ushort)((cell.Wires & 15) << 4);
            return value;
        }
        internal static int Count(ushort bits) { int count = 0; while (bits != 0) { count += bits & 1; bits >>= 1; } return count; }
    }

    public sealed class BlueprintProjectionPlacement
    {
        public BlueprintProjectionPlacement(BlueprintProjectionIdentity identity, CaptureSummary summary, int originX, int originY, long commitOrder, BlueprintProjectionMask mask = null, long totalLayerCount = 0)
        { Identity = identity ?? throw new ArgumentNullException(nameof(identity)); savedSummary = LibraryState.Copy(summary) ?? throw new ArgumentNullException(nameof(summary)); OriginX = originX; OriginY = originY; CommitOrder = commitOrder; Mask = mask ?? BlueprintProjectionMask.Empty; TotalLayerCount = totalLayerCount; }
        public BlueprintProjectionIdentity Identity { get; }
        private readonly CaptureSummary savedSummary;
        public CaptureSummary Summary { get { return LibraryState.Copy(savedSummary); } }
        public int OriginX { get; }
        public int OriginY { get; }
        public long CommitOrder { get; }
        public BlueprintProjectionMask Mask { get; }
        public long TotalLayerCount { get; }
    }

    public sealed class BlueprintProjectionCommitRequest
    {
        public BlueprintProjectionCommitRequest(long sessionGeneration, long transactionId, long sceneRevision, BlueprintProjectionPlacement candidate, IReadOnlyList<BlueprintProjectionPlacement> previous)
        {
            SessionGeneration = sessionGeneration; TransactionId = transactionId; SceneRevision = sceneRevision; Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            var copy = new BlueprintProjectionPlacement[previous.Count]; for (int i = 0; i < copy.Length; i++) copy[i] = previous[i]; Previous = Array.AsReadOnly(copy);
        }
        public long SessionGeneration { get; }
        public long TransactionId { get; }
        public long SceneRevision { get; }
        public BlueprintProjectionPlacement Candidate { get; }
        public IReadOnlyList<BlueprintProjectionPlacement> Previous { get; }
    }
    public sealed class BlueprintProjectionCommitReceipt
    {
        internal BlueprintProjectionCommitReceipt(BlueprintProjectionCommitRequest request, Dictionary<long, BlueprintProjectionMask> masks, List<long> retired, string error = null)
        { Request = request; Masks = new ReadOnlyDictionary<long, BlueprintProjectionMask>(masks); RetiredInstanceIds = retired.AsReadOnly(); Error = error; }
        public BlueprintProjectionCommitRequest Request { get; }
        public IReadOnlyDictionary<long, BlueprintProjectionMask> Masks { get; }
        public IReadOnlyList<long> RetiredInstanceIds { get; }
        public string Error { get; }
        public bool Succeeded { get { return Error == null; } }
    }
}
