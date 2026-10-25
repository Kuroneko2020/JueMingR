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
        internal int ObjectId { get; }
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
                var compact = new List<Leaf>(); var vertical = new Dictionary<Tuple<int, int, ushort, int>, int>();
                bool uniform = true, objects = false; ushort bits = 0;
                uniformLayers = layers[0];
                for (int i = 0; i < layers.Length; i++) { bits |= layers[i]; uniform &= layers[i] == uniformLayers; objects |= objectIds[i] != 0; }
                TargetBits = bits;
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
                foreach (var dependency in dependencies) compact.Add(new Leaf { Bounds = dependency.Bounds, Layers = 1, ObjectId = dependency.ObjectId });
                // Door expansion and complete-object rectangles frequently
                // share a row interval. Their exact union remains a rectangle.
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
                if(objects.Length==0)return false;
                if(bounds.Width==1 && bounds.Height==1)
                {int at=Array.BinarySearch(points,Key(bounds.Left,bounds.Top));if(at>=0 && pointObjects.Length!=0 && PointIntersects(at,1,mask) && Array.BinarySearch(objects,pointObjects[at])>=0)return true;}
                else
                {
                    int low=0,high=points.Length;long first=Key(0,bounds.Top);
                    while(low<high){int middle=(low+high)/2;if(points[middle]<first)low=middle+1;else high=middle;}
                    for(int i=low;i<points.Length && (points[i]>>32)<bounds.Bottom;i++)
                        if(pointObjects.Length!=0 && bounds.Contains((int)(uint)points[i],(int)(points[i]>>32)) && PointIntersects(i,1,mask) && Array.BinarySearch(objects,pointObjects[i])>=0)return true;
                }
                return nodes.Length!=0 && HasObjectNode(0,bounds,mask,objects);
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
                if(objects.Length==0)return;
                if(bounds.Width==1 && bounds.Height==1)
                {int at=Array.BinarySearch(points,Key(bounds.Left,bounds.Top));if(at>=0 && pointObjects.Length!=0 && PointIntersects(at,1,mask))EmitObject(pointObjects[at],objects,routes,emit);}
                else
                {
                    int low=0,high=points.Length;long first=Key(0,bounds.Top);
                    while(low<high){int middle=(low+high)/2;if(points[middle]<first)low=middle+1;else high=middle;}
                    for(int i=low;i<points.Length && (points[i]>>32)<bounds.Bottom;i++)
                        if(pointObjects.Length!=0 && bounds.Contains((int)(uint)points[i],(int)(points[i]>>32)) && PointIntersects(i,1,mask))EmitObject(pointObjects[i],objects,routes,emit);
                }
                if(nodes.Length!=0)RouteObjectNode(0,bounds,mask,objects,routes,emit);
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

    public sealed class BlueprintProjectionSourceReceipt
    {
        internal BlueprintProjectionSourceReceipt(BlueprintProjectionIdentity identity, long generation, int range, int objectId,
            BlueprintProjectionWindow window, int offset, int nextOffset, bool complete, string error = null)
        { Identity = identity; Generation = generation; Range = range; ObjectId = objectId; Window = window; Offset = offset; NextOffset = nextOffset; Complete = complete; Error = error; }
        public BlueprintProjectionIdentity Identity { get; }
        public long Generation { get; }
        public int Range { get; }
        // Zero returns a bounded ordinary source slice or one object anchor.
        // Positive returns its one complete target workset; the Host counts
        // that object's Tile members once, apart from same-cell other layers.
        public int ObjectId { get; }
        public int Offset { get; }
        public int NextOffset { get; }
        public bool Complete { get; }
        public BlueprintProjectionWindow Window { get; }
        public string Error { get; }
    }
}
