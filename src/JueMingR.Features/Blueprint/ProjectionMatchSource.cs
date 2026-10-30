using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    internal readonly struct BlueprintProjectionSourceDependency
    {
        internal BlueprintProjectionSourceDependency(BlueprintProjectionBounds bounds, int objectId) { Bounds = bounds; ObjectId = objectId; }
        internal BlueprintProjectionBounds Bounds { get; }
        internal int ObjectId { get; }
    }
    // Physical observation ranges and their logical proof ranges both share
    // immutable geometry across instances. Native watches remain per block:
    // exact source positions (8 bytes plus optional 2-byte layers/4-byte object
    // IDs), compact equal-layer runs and cross-block whole-object dependencies.
    // No CaptureCell payload or observed world value is retained in this index.
    public readonly struct BlueprintProjectionSourceRange
    {
        private readonly SourceGeometry geometry;
        private readonly ProofRouting routing;
        internal BlueprintProjectionSourceRange(long block, BlueprintProjectionBounds bounds, long targets, long[] coordinates, ushort[] layers, int[] objectIds, IList<BlueprintProjectionSourceDependency> dependencies, int observationGroup = 0, int objectId = 0)
        { Block = block; Bounds = bounds; TargetLayerCount = targets; ObservationGroup = observationGroup; ObjectId = objectId; geometry = new SourceGeometry(coordinates, layers, objectIds, dependencies); routing = null; }
        private BlueprintProjectionSourceRange(BlueprintProjectionSourceRange source, ProofRouting routing)
        { Block = source.Block; Bounds = source.Bounds; TargetLayerCount = source.TargetLayerCount; ObservationGroup = source.ObservationGroup; ObjectId = source.ObjectId; geometry = source.geometry; this.routing = routing; }
        internal BlueprintProjectionSourceRange WithRouting(BlueprintProjectionSourceRange? ordinary, int[] objectIds, int[] routes)
        { return new BlueprintProjectionSourceRange(this,new ProofRouting(ordinary,objectIds,routes)); }
        internal long Block { get; }
        public int ObjectId { get; }
        public int ObservationGroup { get; }
        public BlueprintProjectionBounds Bounds { get; }
        public long TargetLayerCount { get; }
        public ushort TargetBits { get { return geometry == null ? (ushort)0 : geometry.TargetBits; } }
        public long SourceIndexBytes { get { return geometry == null ? 0 : geometry.EstimatedBytes + (routing == null ? 0 : routing.EstimatedBytes); } }
        public bool Contains(int x, int y) { return Intersects(new BlueprintProjectionBounds(x, y, 1, 1)); }
        public bool Intersects(BlueprintProjectionBounds bounds) { return Intersects(bounds, 0xff, BlueprintProjectionMask.Empty); }
        public bool Intersects(BlueprintProjectionBounds bounds, ushort changedLayers, BlueprintProjectionMask mask)
        { return geometry != null && SourceGeometry.Overlap(Bounds, bounds) && geometry.Intersects(bounds, changedLayers, mask ?? BlueprintProjectionMask.Empty); }
        public bool IntersectsProofs(BlueprintProjectionBounds bounds,ushort changedLayers,BlueprintProjectionMask mask)
        {
            if(geometry==null || bounds.Width<=0 || bounds.Height<=0 || !SourceGeometry.Overlap(Bounds,bounds))return false;
            mask=mask??BlueprintProjectionMask.Empty;if(routing==null)return geometry.Intersects(bounds,changedLayers,mask);
            return routing.Ordinary.HasValue && routing.Ordinary.Value.Intersects(bounds,changedLayers,mask) ||
                (changedLayers&1)!=0 && geometry.HasRoutedObject(bounds,mask,routing.Objects);
        }
        // Called only for a real dirty physical block. Its immutable geometry
        // routes exact coordinate/layer changes to bounded logical proof IDs;
        // it neither reads source/world values nor allocates an event list.
        public void RouteProofs(BlueprintProjectionBounds bounds, ushort changedLayers, BlueprintProjectionMask mask, Action<int> emit)
        {
            if (emit == null || geometry == null || !SourceGeometry.Overlap(Bounds,bounds)) return;
            mask = mask ?? BlueprintProjectionMask.Empty;
            if (routing == null) { if (geometry.Intersects(bounds,changedLayers,mask)) emit(0); return; }
            if (routing.Ordinary.HasValue && routing.Ordinary.Value.Intersects(bounds,changedLayers,mask)) emit(0);
            if ((changedLayers & 1) != 0) geometry.RouteObjects(bounds,mask,routing.Objects,routing.Routes,emit);
        }
        private sealed class ProofRouting
        {
            internal readonly BlueprintProjectionSourceRange? Ordinary;
            internal readonly int[] Objects, Routes;
            internal long EstimatedBytes { get { return 64L + Objects.LongLength*4 + Routes.LongLength*4; } }
            internal ProofRouting(BlueprintProjectionSourceRange? ordinary, int[] objects, int[] routes)
            { Ordinary=ordinary; Objects=objects; Routes=routes; Array.Sort(Objects,Routes); }
        }

        private sealed class SourceGeometry
        {
            private struct Leaf { internal BlueprintProjectionBounds Bounds; internal ushort Layers; internal int ObjectId; }
            private struct Node { internal BlueprintProjectionBounds Bounds; internal int Left, Right, ObjectId; internal ushort Layers; }
            private readonly long[] points;
            private readonly ushort[] pointLayers;
            private readonly int[] pointObjects;
            private readonly ushort uniformLayers;
            private readonly Node[] nodes;
            private int allocated;
            internal ushort TargetBits { get; }
            internal long EstimatedBytes { get { return 128L + points.LongLength * 8L + pointLayers.LongLength * 2L + pointObjects.LongLength * 4L + nodes.LongLength * 32L; } }
            private static long Key(int x, int y) { return ((long)y << 32) | (uint)x; }
            internal SourceGeometry(long[] coordinates, ushort[] layers, int[] objectIds, IList<BlueprintProjectionSourceDependency> dependencies)
            {
                ushort targetBits=0;bool hasObjectTile=false;
                for(int i=0;i<layers.Length;i++){targetBits|=layers[i];hasObjectTile|=objectIds[i]>0 && (layers[i]&1)!=0;}
                var objectBounds=new Dictionary<int,BlueprintProjectionBounds>();
                if(hasObjectTile)
                {
                    var ordinaryCoordinates=new List<long>();var ordinaryLayers=new List<ushort>();
                    for(int i=0;i<coordinates.Length;i++)
                    {
                        ushort ordinary=layers[i];
                        if(objectIds[i]>0 && (ordinary&1)!=0)
                        { IncludeObject(objectBounds,objectIds[i],new BlueprintProjectionBounds((int)(uint)coordinates[i],(int)(coordinates[i]>>32),1,1));ordinary&=0xfe; }
                        if(ordinary!=0){ordinaryCoordinates.Add(coordinates[i]);ordinaryLayers.Add(ordinary);}
                    }
                    coordinates=ordinaryCoordinates.ToArray();layers=ordinaryLayers.ToArray();objectIds=new int[coordinates.Length];
                }
                foreach(var dependency in dependencies)IncludeObject(objectBounds,dependency.ObjectId,dependency.Bounds);
                var compact = new List<Leaf>(); var vertical = new Dictionary<Tuple<int, int, ushort, int>, int>();
                bool uniform = true, objects = false;
                uniformLayers = layers.Length==0?(ushort)0:layers[0];
                for (int i = 0; i < layers.Length; i++) { uniform &= layers[i] == uniformLayers; objects |= objectIds[i] != 0; }
                TargetBits = targetBits;
                // Merge actual adjacent row runs; never walk absent bbox rows.
                for (int i = 0; i < coordinates.Length;)
                {
                    int x = (int)(uint)coordinates[i], y = (int)(coordinates[i] >> 32), right = x + 1, objectId = objectIds[i]; ushort layer = layers[i]; i++;
                    while (i < coordinates.Length && (int)(coordinates[i] >> 32) == y && (int)(uint)coordinates[i] == right && layers[i] == layer && objectIds[i] == objectId) { right++; i++; }
                    var key = Tuple.Create(x, right - x, layer, objectId); int prior;
                    if (vertical.TryGetValue(key, out prior) && compact[prior].Bounds.Bottom == y)
                    { var old = compact[prior]; old.Bounds = new BlueprintProjectionBounds(old.Bounds.Left, old.Bounds.Top, old.Bounds.Width, old.Bounds.Height + 1); compact[prior] = old; }
                    else { vertical[key] = compact.Count; compact.Add(new Leaf { Bounds = new BlueprintProjectionBounds(x, y, right - x, 1), Layers = layer, ObjectId = objectId }); }
                }
                bool useRuns = compact.Count * 64L < coordinates.LongLength * (8L + (uniform ? 0 : 2) + (objects ? 4 : 0));
                points = useRuns ? Array.Empty<long>() : coordinates;
                pointLayers = useRuns || uniform ? Array.Empty<ushort>() : layers;
                pointObjects = useRuns || !objects ? Array.Empty<int>() : objectIds;
                if (!useRuns) compact.Clear();
                // Every complete object's Tile positions and expanded/cross-
                // block dependency are the same proof. Keep one rectangle per
                // ID instead of both sparse points and an overlapping extent:
                // one real store must feed its strict old/new chain once.
                // Other layers at those points remain ordinary geometry.
                foreach(var pair in objectBounds)compact.Add(new Leaf{Bounds=pair.Value,Layers=1,ObjectId=pair.Key});
                compact.Sort((a, b) => a.Layers != b.Layers ? a.Layers.CompareTo(b.Layers) : a.ObjectId != b.ObjectId ? a.ObjectId.CompareTo(b.ObjectId) : a.Bounds.Top != b.Bounds.Top ? a.Bounds.Top.CompareTo(b.Bounds.Top) : a.Bounds.Height != b.Bounds.Height ? a.Bounds.Height.CompareTo(b.Bounds.Height) : a.Bounds.Left.CompareTo(b.Bounds.Left));
                var merged = new List<Leaf>();
                foreach (var next in compact)
                {
                    if (merged.Count != 0)
                    {
                        var last = merged[merged.Count - 1];
                        if (last.Layers == next.Layers && last.ObjectId == next.ObjectId && last.Bounds.Top == next.Bounds.Top && last.Bounds.Height == next.Bounds.Height && next.Bounds.Left <= last.Bounds.Right)
                        { last.Bounds = new BlueprintProjectionBounds(last.Bounds.Left, last.Bounds.Top, Math.Max(last.Bounds.Right, next.Bounds.Right) - last.Bounds.Left, last.Bounds.Height); merged[merged.Count - 1] = last; continue; }
                    }
                    merged.Add(next);
                }
                var rectangles = merged.ToArray(); nodes = new Node[Math.Max(0, rectangles.Length * 2 - 1)];
                if (rectangles.Length != 0) Build(rectangles, 0, rectangles.Length);
            }
            private static void IncludeObject(Dictionary<int,BlueprintProjectionBounds> objects,int id,BlueprintProjectionBounds bounds)
            {
                BlueprintProjectionBounds prior;
                if(objects.TryGetValue(id,out prior))
                {int left=Math.Min(prior.Left,bounds.Left),top=Math.Min(prior.Top,bounds.Top);bounds=new BlueprintProjectionBounds(left,top,Math.Max(prior.Right,bounds.Right)-left,Math.Max(prior.Bottom,bounds.Bottom)-top);}
                objects[id]=bounds;
            }
            private int Build(Leaf[] rectangles, int start, int count)
            {
                int slot = allocated++; var bounds = rectangles[start].Bounds; ushort layers = rectangles[start].Layers;
                for (int i = start + 1; i < start + count; i++)
                { var next = rectangles[i].Bounds; layers |= rectangles[i].Layers; int left = Math.Min(bounds.Left, next.Left), top = Math.Min(bounds.Top, next.Top); bounds = new BlueprintProjectionBounds(left, top, Math.Max(bounds.Right, next.Right) - left, Math.Max(bounds.Bottom, next.Bottom) - top); }
                if (count == 1) { nodes[slot] = new Node { Bounds = bounds, Left = -1, Right = -1, Layers = layers, ObjectId = rectangles[start].ObjectId }; return slot; }
                bool horizontal = bounds.Width >= bounds.Height;
                Array.Sort(rectangles, start, count, Comparer<Leaf>.Create((a, b) => horizontal ? ((long)a.Bounds.Left * 2 + a.Bounds.Width).CompareTo((long)b.Bounds.Left * 2 + b.Bounds.Width) : ((long)a.Bounds.Top * 2 + a.Bounds.Height).CompareTo((long)b.Bounds.Top * 2 + b.Bounds.Height)));
                int half = count / 2, leftChild = Build(rectangles, start, half), rightChild = Build(rectangles, start + half, count - half);
                nodes[slot] = new Node { Bounds = bounds, Left = leftChild, Right = rightChild, Layers = layers }; return slot;
            }
            internal static bool Overlap(BlueprintProjectionBounds a, BlueprintProjectionBounds b)
            { return a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom; }
            private bool IntersectsNode(int index, BlueprintProjectionBounds bounds, ushort layers, BlueprintProjectionMask mask)
            {
                var node = nodes[index]; layers &= node.Layers;
                if (layers == 0 || !Overlap(node.Bounds, bounds)) return false;
                if (node.Left >= 0) return IntersectsNode(node.Left, bounds, layers, mask) || IntersectsNode(node.Right, bounds, layers, mask);
                int left = Math.Max(node.Bounds.Left, bounds.Left), top = Math.Max(node.Bounds.Top, bounds.Top);
                return mask.AnyRemainingLayers(new BlueprintProjectionBounds(left, top, Math.Min(node.Bounds.Right, bounds.Right) - left, Math.Min(node.Bounds.Bottom, bounds.Bottom) - top), node.ObjectId, layers);
            }
            private bool PointIntersects(int i, ushort layers, BlueprintProjectionMask mask)
            { return mask.RemainingLayers((int)(uint)points[i], (int)(points[i] >> 32), pointObjects.Length == 0 ? 0 : pointObjects[i], (ushort)(layers & (pointLayers.Length == 0 ? uniformLayers : pointLayers[i]))) != 0; }
            private static void EmitObject(int id,int[] objects,int[] routes,Action<int> emit)
            { if(id<=0)return; int at=Array.BinarySearch(objects,id); if(at>=0)emit(routes[at]); }
            private bool HasObjectNode(int index,BlueprintProjectionBounds bounds,BlueprintProjectionMask mask,int[] objects)
            {
                var node=nodes[index];if((node.Layers&1)==0 || !Overlap(node.Bounds,bounds))return false;
                if(node.Left>=0)return HasObjectNode(node.Left,bounds,mask,objects)||HasObjectNode(node.Right,bounds,mask,objects);
                if(node.ObjectId<=0 || Array.BinarySearch(objects,node.ObjectId)<0)return false;
                int left=Math.Max(node.Bounds.Left,bounds.Left),top=Math.Max(node.Bounds.Top,bounds.Top);
                return mask.AnyRemainingLayers(new BlueprintProjectionBounds(left,top,Math.Min(node.Bounds.Right,bounds.Right)-left,Math.Min(node.Bounds.Bottom,bounds.Bottom)-top),node.ObjectId,1);
            }
            internal bool HasRoutedObject(BlueprintProjectionBounds bounds,BlueprintProjectionMask mask,int[] objects)
            {
                return objects.Length!=0 && nodes.Length!=0 && HasObjectNode(0,bounds,mask,objects);
            }
            private void RouteObjectNode(int index,BlueprintProjectionBounds bounds,BlueprintProjectionMask mask,int[] objects,int[] routes,Action<int> emit)
            {
                var node=nodes[index]; if((node.Layers&1)==0 || !Overlap(node.Bounds,bounds))return;
                if(node.Left>=0){RouteObjectNode(node.Left,bounds,mask,objects,routes,emit);RouteObjectNode(node.Right,bounds,mask,objects,routes,emit);return;}
                if(node.ObjectId<=0)return; int left=Math.Max(node.Bounds.Left,bounds.Left),top=Math.Max(node.Bounds.Top,bounds.Top);
                if(mask.AnyRemainingLayers(new BlueprintProjectionBounds(left,top,Math.Min(node.Bounds.Right,bounds.Right)-left,Math.Min(node.Bounds.Bottom,bounds.Bottom)-top),node.ObjectId,1))EmitObject(node.ObjectId,objects,routes,emit);
            }
            internal void RouteObjects(BlueprintProjectionBounds bounds,BlueprintProjectionMask mask,int[] objects,int[] routes,Action<int> emit)
            {
                if(objects.Length!=0 && nodes.Length!=0)RouteObjectNode(0,bounds,mask,objects,routes,emit);
            }
            internal bool Intersects(BlueprintProjectionBounds bounds, ushort layers, BlueprintProjectionMask mask)
            {
                if (bounds.Width <= 0 || bounds.Height <= 0 || (layers & TargetBits) == 0) return false;
                if (bounds.Width == 1 && bounds.Height == 1)
                { int at = Array.BinarySearch(points, Key(bounds.Left, bounds.Top)); if (at >= 0 && PointIntersects(at, layers, mask)) return true; }
                if (bounds.Width != 1 || bounds.Height != 1)
                {
                    int low = 0, high = points.Length;
                    long first = Key(0, bounds.Top);
                    while (low < high) { int middle = (low + high) / 2; if (points[middle] < first) low = middle + 1; else high = middle; }
                    for (int i = low; i < points.Length && (points[i] >> 32) < bounds.Bottom; i++)
                        if (bounds.Contains((int)(uint)points[i], (int)(points[i] >> 32)) && PointIntersects(i, layers, mask)) return true;
                }
                return nodes.Length != 0 && IntersectsNode(0, bounds, layers, mask);
            }
        }
    }

    public readonly struct BlueprintProjectionSourceDemand
    {
        public BlueprintProjectionSourceDemand(int range, int objectId = 0, int offset = 0)
        { Range = range; ObjectId = objectId; Offset = offset; }
        public int Range { get; }
        // Zero selects the logical range: ordinary layers or one complete
        // object's Tile. Positive values retain the explicit all-layer API.
        public int ObjectId { get; }
        public int Offset { get; }
    }

    public sealed class BlueprintProjectionSourcePart
    {
        internal const long DescriptorBytes = 128;
        internal BlueprintProjectionSourcePart(BlueprintProjectionSourceDemand demand, int logicalObjectId,
            BlueprintProjectionWindow window, int nextOffset, bool complete, IReadOnlyList<BlueprintProjectionWindow> doorContexts = null)
        {
            Range = demand.Range; ObjectId = demand.ObjectId; LogicalObjectId = logicalObjectId; Offset = demand.Offset;
            Window = window; NextOffset = nextOffset; Complete = complete;
            WindowEstimatedBytes = window == null ? 0 : checked(window.EstimatedBytes + SummaryReferenceBytes(window.Summary));
            DoorContexts = doorContexts ?? Array.Empty<BlueprintProjectionWindow>();
            DoorContextEstimatedBytes = DoorContextBytes(DoorContexts);
        }
        internal static long SummaryReferenceBytes(CaptureSummary summary)
        { return summary == null ? 0 : checked(8L * ((summary.MissingPayloads?.LongLength ?? 0) + (summary.UnreliableReasons?.LongLength ?? 0))); }
        internal static long DoorContextBytes(IReadOnlyList<BlueprintProjectionWindow> contexts)
        {
            if (contexts == null || contexts.Count == 0) return 0;
            long bytes = 64L + contexts.Count * 8L;
            foreach (var context in contexts) bytes = checked(bytes + context.EstimatedBytes + SummaryReferenceBytes(context.Summary));
            return bytes;
        }
        public int Range { get; }
        public int ObjectId { get; }
        public int LogicalObjectId { get; }
        public int Offset { get; }
        public int NextOffset { get; }
        public bool Complete { get; }
        public BlueprintProjectionWindow Window { get; }
        // A door can occupy either wing outside its saved Tile members. Each
        // context contains exactly the canonical 3x3 source area, including
        // explicit empty cells after a complete directory read. These raw
        // neighbours grant no layer selection and never join Window.Cells or
        // its source ordinals. The part owns their existing bounded lifetime.
        public IReadOnlyList<BlueprintProjectionWindow> DoorContexts { get; }
        public long DoorContextEstimatedBytes { get; }
        // Window's legacy estimate covers its fixed headers/payload. Copy
        // clones the summary's two reference arrays; each slot is additional
        // per-window retention. The immutable strings remain shared with root.
        public long WindowEstimatedBytes { get; }
        // A standalone part estimate. A batch owner must charge the receipt,
        // which deduplicates shared windows rather than summing this property.
        public long EstimatedBytes { get { return DescriptorBytes + WindowEstimatedBytes + DoorContextEstimatedBytes; } }
    }

    // A transport has immutable source ownership only. Each part still needs
    // its own Host world stamp and complete proof; finishing or invalidating
    // one proof must not cancel the transport's stable siblings.
    public sealed class BlueprintProjectionSourceReceipt
    {
        public const int MaximumPartCount = 64;
        public const long TargetBytes = 256L * 1024;
        internal const long DirectoryBytes = 128;
        // A legal old 32-MiB single window still fits with its descriptor,
        // directory and cloned summary slots. Validated v1 roots have at most
        // 16 MissingPayloads and no serialized UnreliableReasons. This is a
        // transport bound, not an extension of reliable-object proof capacity.
        public const long MaximumEstimatedBytes = 32L * 1024 * 1024 + DirectoryBytes + BlueprintProjectionSourcePart.DescriptorBytes + 16L * 8;
        internal BlueprintProjectionSourceReceipt(BlueprintProjectionIdentity identity, long generation,
            BlueprintProjectionSourcePart[] parts, string error = null, long capabilityRevision = 0, BlueprintProjectionView view = null)
        {
            Identity = identity; Generation = generation; Parts = Array.AsReadOnly(parts); Error = error; CapabilityRevision = capabilityRevision; View = view;
            long bytes = DirectoryBytes + parts.LongLength * BlueprintProjectionSourcePart.DescriptorBytes;
            var windows = new HashSet<BlueprintProjectionWindow>();
            foreach (var part in parts) if (part.Window != null && windows.Add(part.Window)) bytes = checked(bytes + part.WindowEstimatedBytes + part.DoorContextEstimatedBytes);
            EstimatedBytes = bytes;
        }
        public BlueprintProjectionIdentity Identity { get; }
        public long Generation { get; }
        public long CapabilityRevision { get; }
        public BlueprintProjectionView View { get; }
        public IReadOnlyList<BlueprintProjectionSourcePart> Parts { get; }
        public long EstimatedBytes { get; }
        // Compatibility accessors describe the first requested part, including
        // the original ObjectId request rather than the derived logical ID.
        public int Range { get { return Parts[0].Range; } }
        public int ObjectId { get { return Parts[0].ObjectId; } }
        public int Offset { get { return Parts[0].Offset; } }
        public int NextOffset { get { return Parts[0].NextOffset; } }
        public bool Complete { get { return Parts[0].Complete; } }
        public BlueprintProjectionWindow Window { get { return Parts[0].Window; } }
        public string Error { get; }
    }
#if DEBUG
    // Bounded-work evidence only; no Release sampling, timers or file output.
    public readonly struct BlueprintProjectionSourceStatistics
    {
        internal BlueprintProjectionSourceStatistics(long requests, long receipts, long parts, long blocks, long records,
            long working, long workingPeak, long receiptBytes, long receiptPeak, long pending)
        { Requests = requests; Receipts = receipts; Parts = parts; MemberBlocks = blocks; MemberRecords = records; WorkingBytes = working; WorkingPeakBytes = workingPeak; ReceiptBytes = receiptBytes; ReceiptPeakBytes = receiptPeak; PendingRequests = pending; }
        public long Requests { get; }
        public long Receipts { get; }
        public long Parts { get; }
        public long MemberBlocks { get; }
        public long MemberRecords { get; }
        public long WorkingBytes { get; }
        public long WorkingPeakBytes { get; }
        public long ReceiptBytes { get; }
        public long ReceiptPeakBytes { get; }
        public long PendingRequests { get; }
    }
#endif
}
