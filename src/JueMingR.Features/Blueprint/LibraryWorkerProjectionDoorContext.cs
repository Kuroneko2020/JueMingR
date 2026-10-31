using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    internal sealed partial class LibraryWorker
    {
        private sealed class ProjectionSourceTransform
        {
            internal BlueprintProjectionWindow Source;
            internal CaptureCell[] Cells;
            internal bool RowMajor;
            internal Action<BlueprintProjectionWindow> Publish;
            internal readonly Dictionary<int, CaptureObject> Metadata = new Dictionary<int, CaptureObject>();
        }
        private readonly struct ProjectionSourceTransformCell
        {
            internal ProjectionSourceTransformCell(ProjectionSourceTransform target, int index) { Target = target; Index = index; }
            internal ProjectionSourceTransform Target { get; }
            internal int Index { get; }
        }

        private IEnumerable<bool> TransformProjectionSourceViewsSteps(ProjectionOwner owner, ProjectionSourceRequest request,
            List<ProjectionSourceBuild> builds, long working)
        {
            var view = request.View; Action check = () => CheckProjectionSource(owner, request);
            if (view.MirroredHorizontally && (view.Profile == null || !view.Profile.Ready)) throw new System.IO.InvalidDataException("blueprint-mirror-profile-not-ready");
            var transforms = new List<ProjectionSourceTransform>(); var ids = new List<int>(); var distinct = new HashSet<int>();
            var routes = new Dictionary<int, List<ProjectionSourceTransformCell>>();
            var metadataRoutes = new Dictionary<int, List<ProjectionSourceTransform>>();
            long temporary = 1024;
            foreach (var build in builds)
            {
                check();
                var transform = new ProjectionSourceTransform { Source = build.Window, RowMajor = build.ObjectId > 0, Publish = value => build.Window = value };
                transforms.Add(transform); temporary += 256L + transform.Source.Cells.Count * 256L + transform.Source.Objects.Count * 256L;
                UpdateProjectionSourceWorkingBytes(working + temporary);
                foreach (bool ignored in PlanProjectionSourceTransformSteps(transform, view, build.ObjectId > 0, ids, distinct, routes, metadataRoutes, check)) yield return true;
                if (build.DoorContexts != null && build.DoorContexts.Count != 0)
                {
                    var contexts = new BlueprintProjectionWindow[build.DoorContexts.Count];
                    for (int c = 0; c < contexts.Length; c++)
                    {
                        int at = c;
                        var context = new ProjectionSourceTransform { Source = build.DoorContexts[c], RowMajor = true, Publish = value => contexts[at] = value };
                        transforms.Add(context); temporary += 256L + context.Source.Cells.Count * 256L + context.Source.Objects.Count * 256L;
                        UpdateProjectionSourceWorkingBytes(working + temporary);
                        foreach (bool ignored in PlanProjectionSourceTransformSteps(context, view, false, ids, distinct, routes, metadataRoutes, check)) yield return true;
                    }
                    build.DoorContexts = Array.AsReadOnly(contexts);
                }
                UpdateProjectionSourceWorkingBytes(working + temporary); check(); yield return true;
            }
            // ID directories and route-list capacities coexist with every
            // output array, but contain no additional canonical object body.
            temporary += distinct.Count * 512L + ids.Capacity * 4L + transforms.Capacity * 8L;
            UpdateProjectionSourceWorkingBytes(working + temporary);
            int next = 0;
            while (next < ids.Count)
            {
                check();
                using (var batch = PlanProjectionObjectMemberBatch(owner, ids, ref next))
                {
                    foreach (bool ignored in CollectProjectionObjectMembersSteps(owner, batch, check,
                        bytes => UpdateProjectionSourceWorkingBytes(working + temporary + bytes))) yield return true;
                    foreach (var pair in batch.Entries)
                    {
                        check(); var entry = pair.Value; var target = entry.Metadata; string reason;
                        CaptureCell[] mirrored = null;
                        if (view.MirroredHorizontally)
                        {
                            RecordProjectionObjectInterpretation();
                            if (!view.Profile.TryMirrorObject(entry.Metadata, entry.Members, view.Width, out target, out mirrored, out reason))
                                throw new System.IO.InvalidDataException(reason ?? "blueprint-view-source");
                            if (mirrored == null || mirrored.Length != entry.Members.Count) throw new System.IO.InvalidDataException("blueprint-mirror-object-members");
                        }
                        else if (view.Profile != null)
                        {
                            RecordProjectionObjectInterpretation();
                            if (!view.Profile.TryNormalizeObject(entry.Metadata, entry.Members, out target, out reason))
                                throw new System.IO.InvalidDataException(reason ?? "blueprint-view-source");
                        }
                        var members = mirrored == null ? null : new Dictionary<long, CaptureCell>();
                        if (mirrored != null) for (int i = 0; i < mirrored.Length; i++)
                        {
                            var cell = mirrored[i]; members.Add(ProjectionDirectory.Key(view.SourceX(cell.X), cell.Y), cell);
                            if ((i & 31) == 31) { check(); yield return true; }
                        }
                        List<ProjectionSourceTransformCell> consumers;
                        if (routes.TryGetValue(pair.Key, out consumers)) for (int i = 0; i < consumers.Count; i++)
                        {
                            var consumer = consumers[i]; var cell = consumer.Target.Cells[consumer.Index];
                            if (members != null)
                            {
                                CaptureCell transformed;
                                if (!members.TryGetValue(ProjectionDirectory.Key(cell.X, cell.Y), out transformed)) throw new System.IO.InvalidDataException("blueprint-mirror-object-metadata");
                                // Canonical raw proof supplies Tile appearance.
                                // Lane/mask ownership of every same-cell layer
                                // remains exact, including stripped ordinary Tile.
                                transformed.Layers = cell.Layers; transformed.WallType = cell.WallType; transformed.WallPaint = cell.WallPaint;
                                transformed.InvisibleWall = cell.InvisibleWall; transformed.FullbrightWall = cell.FullbrightWall;
                                transformed.Wires = cell.Wires; transformed.LiquidAmount = cell.LiquidAmount; transformed.LiquidKind = cell.LiquidKind;
                                transformed.Actuator = cell.Actuator; consumer.Target.Cells[consumer.Index] = transformed;
                            }
                            if ((i & 31) == 31) { check(); yield return true; }
                        }
                        List<ProjectionSourceTransform> objectConsumers;
                        if (metadataRoutes.TryGetValue(pair.Key, out objectConsumers))
                            foreach (var consumer in objectConsumers) consumer.Metadata.Add(pair.Key, target);
                        check(); yield return true;
                    }
                }
            }
            foreach (var transform in transforms)
            {
                check();
                if (transform.RowMajor) foreach (bool ignored in SortProjectionObjectMembersSteps(transform.Cells, check)) yield return true;
                var objects = new List<CaptureObject>();
                foreach (var obj in transform.Source.Objects) { CaptureObject value; if (transform.Metadata.TryGetValue(obj.Id, out value)) objects.Add(value); }
                var source = transform.Source;
                transform.Publish(new BlueprintProjectionWindow(source.Identity, source.WindowGeneration, source.Summary, view.DisplayBounds(source.Bounds),
                    transform.Cells, objects.ToArray(), true, view.Mask, view, source, !transform.RowMajor));
                check(); yield return true;
            }
            foreach (var build in builds)
            {
                if (build.DoorContexts != null && build.DoorContexts.Count != 0)
                {
                    var contexts = new BlueprintProjectionWindow[build.DoorContexts.Count];
                    for (int i = 0; i < contexts.Length; i++) contexts[i] = build.DoorContexts[i];
                    Array.Sort(contexts, (a, b) => a.Bounds.Top == b.Bounds.Top ? a.Bounds.Left.CompareTo(b.Bounds.Left) : a.Bounds.Top.CompareTo(b.Bounds.Top));
                    build.DoorContexts = Array.AsReadOnly(contexts);
                }
                check(); yield return true;
            }
        }

        private static IEnumerable<bool> PlanProjectionSourceTransformSteps(ProjectionSourceTransform target, BlueprintProjectionView view,
            bool completeObject, List<int> ids, HashSet<int> distinct, Dictionary<int, List<ProjectionSourceTransformCell>> routes,
            Dictionary<int, List<ProjectionSourceTransform>> metadataRoutes, Action check)
        {
            target.Cells = new CaptureCell[target.Source.Cells.Count]; var liveTiles = new Dictionary<int, int>();
            for (int i = 0; i < target.Cells.Length; i++)
            {
                var cell = view.Mask.Apply(target.Source.Cells[i]);
                if (cell.ObjectId > 0)
                {
                    if (distinct.Add(cell.ObjectId)) ids.Add(cell.ObjectId);
                    List<ProjectionSourceTransformCell> values;
                    if (!routes.TryGetValue(cell.ObjectId, out values)) routes.Add(cell.ObjectId, values = new List<ProjectionSourceTransformCell>());
                    values.Add(new ProjectionSourceTransformCell(target, i));
                    if ((cell.Layers & CaptureLayer.Tile) != 0) { int count; liveTiles.TryGetValue(cell.ObjectId, out count); liveTiles[cell.ObjectId] = count + 1; }
                }
                else if (view.MirroredHorizontally)
                { string reason; if (!view.Profile.TryMirrorCell(cell, view.Width, out cell, out reason)) throw new System.IO.InvalidDataException(reason); }
                target.Cells[i] = cell;
                if ((i & 31) == 31) { check(); yield return true; }
            }
            foreach (var obj in target.Source.Objects)
            {
                int count; liveTiles.TryGetValue(obj.Id, out count);
                if (completeObject && count != 0 && count != (long)obj.Width * obj.Height) throw new System.IO.InvalidDataException("blueprint-mirror-object-incomplete");
                if (!routes.ContainsKey(obj.Id) || count == 0) continue;
                List<ProjectionSourceTransform> values;
                if (!metadataRoutes.TryGetValue(obj.Id, out values)) metadataRoutes.Add(obj.Id, values = new List<ProjectionSourceTransform>());
                values.Add(target);
            }
            check(); yield return true;
        }
        // Source transport must be self-contained after ordinary siblings are
        // released and without a viewport Draw. Only the three possible door
        // columns are copied. Other layers keep their original coordinates;
        // Host still applies the current instance mask and qualification.
        private IEnumerable<bool> PlanProjectionDoorContexts(ProjectionOwner owner, ProjectionSourceRequest request,
            ProjectionSourceBuild build, long working)
        {
            var contexts = new List<BlueprintProjectionWindow>(); var seen = new HashSet<int>();
            long contextBytes = 0;
            if (build.ObjectId > 0)
            {
                var obj = build.Metadata; obj.X += owner.Root.Summary.Left; obj.Y += owner.Root.Summary.Top;
                var context = ReadProjectionDoorContext(owner, request, obj);
                if (context != null) { contexts.Add(context); contextBytes += context.EstimatedBytes + BlueprintProjectionSourcePart.SummaryReferenceBytes(context.Summary); }
                UpdateProjectionSourceWorkingBytes(working + contextBytes + 512); yield return true;
            }
            else
            {
                var chunk = ProjectionChunk(owner, build.Block);
                int end = build.Offset;
                for (int index = build.Offset; index < chunk.Count; index++)
                {
                    int before = contexts.Count; long beforeBytes = contextBytes;
                    RecordProjectionObjectMemberAuxiliaryRecord(); var wall = chunk.Cells[index];
                    if ((wall.Layers & CaptureLayer.Wall) != 0)
                    {
                        // A saved hinge is at most one column from a wall in
                        // a legal runtime wing. Exact 3x1 directory queries and
                        // binary searches avoid scanning a sparse block bbox
                        // or retaining a template-wide door appearance index.
                        foreach (long number in owner.Directory.Query(wall.X - 1, wall.Y, wall.X + 2, wall.Y + 1, ProjectionComparison))
                        {
                            CheckProjectionSource(owner, request); var nearby = ProjectionChunk(owner, number);
                            int at = ProjectionDoorCellLowerBound(nearby, wall.X - 1, wall.Y);
                            while (at < nearby.Count && nearby.Cells[at].Y == wall.Y && nearby.Cells[at].X <= wall.X + 1)
                            {
                                RecordProjectionObjectMemberAuxiliaryRecord(); var cell = nearby.Cells[at++];
                                if (cell.ObjectId <= 0 || (cell.Layers & CaptureLayer.Tile) == 0 || cell.TileType != 10 && cell.TileType != 11 || !seen.Add(cell.ObjectId)) continue;
                                var context = ReadProjectionDoorContext(owner, request, ProjectionObject(owner, cell.ObjectId));
                                if (context != null) { contexts.Add(context); contextBytes += context.EstimatedBytes + BlueprintProjectionSourcePart.SummaryReferenceBytes(context.Summary); }
                            }
                        }
                    }
                    UpdateProjectionSourceWorkingBytes(working + contextBytes + 512L + contexts.Capacity * 8L + seen.Count * 64L);
                    long contextEstimate = contexts.Count == 0 ? 0 : 64L + contexts.Count * 8L + contextBytes;
                    long partEstimate = 256L + (index - build.Offset + 1L) * 64L + contextEstimate +
                        BlueprintProjectionSourcePart.SummaryReferenceBytes(owner.Root.Summary) +
                        BlueprintProjectionSourcePart.DescriptorBytes + BlueprintProjectionSourceReceipt.DirectoryBytes;
                    if (index > build.Offset && partEstimate > BlueprintProjectionSourceReceipt.TargetBytes)
                    {
                        // A dense wall/door block is a resumable raw-record
                        // prefix, never an oversized part the Host cannot keep.
                        // This unaccepted cell is the next request's first raw
                        // offset; neither its wall nor its contexts are skipped.
                        contexts.RemoveRange(before, contexts.Count - before); contextBytes = beforeBytes; break;
                    }
                    end = index + 1;
                    if ((index & 31) == 31) { CheckProjectionSource(owner, request); yield return true; }
                }
                build.NextOffset = end; build.Complete = end == chunk.Count;
                build.PlannedCells = end - build.Offset; build.PlannedBytes = 256L + build.PlannedCells * 64L;
            }
            contexts.Sort((a, b) => a.Bounds.Top != b.Bounds.Top ? a.Bounds.Top.CompareTo(b.Bounds.Top) : a.Bounds.Left.CompareTo(b.Bounds.Left));
            build.DoorContexts = contexts.Count == 0 ? (IReadOnlyList<BlueprintProjectionWindow>)Array.Empty<BlueprintProjectionWindow>() : Array.AsReadOnly(contexts.ToArray());
            build.DoorContextBytes = BlueprintProjectionSourcePart.DoorContextBytes(build.DoorContexts);
            build.PlannedBytes = checked(build.PlannedBytes + build.DoorContextBytes);
            UpdateProjectionSourceWorkingBytes(working + build.DoorContextBytes + 512L + contexts.Capacity * 8L + seen.Count * 64L);
            CheckProjectionSource(owner, request); yield return true;
        }

        private BlueprintProjectionWindow ReadProjectionDoorContext(ProjectionOwner owner, ProjectionSourceRequest request, CaptureObject obj)
        {
            if (!obj.Reliable || obj.Type != 10 && obj.Type != 11 || obj.Height != 3 ||
                obj.Type == 10 && obj.Width != 1 || obj.Type == 11 && obj.Width != 2) return null;
            var summary = owner.Root.Summary; var bounds = ProjectionSourceObjectBounds(obj, summary);
            if (bounds.Width != 3 || bounds.Height != 3) return null;
            var cells = new CaptureCell[9];
            for (int i = 0; i < cells.Length; i++) cells[i] = new CaptureCell { X = bounds.Left + i % 3, Y = bounds.Top + i / 3 };
            int left = bounds.Left + summary.Left, top = bounds.Top + summary.Top;
            foreach (long number in owner.Directory.Query(left, top, left + 3, top + 3, ProjectionComparison))
            {
                CheckProjectionSource(owner, request); var chunk = ProjectionChunk(owner, number);
                for (int row = 0; row < 3; row++)
                {
                    int at = ProjectionDoorCellLowerBound(chunk, left, top + row);
                    while (at < chunk.Count && chunk.Cells[at].Y == top + row && chunk.Cells[at].X < left + 3)
                    {
                        RecordProjectionObjectMemberAuxiliaryRecord(); var cell = chunk.Cells[at++]; cell.X -= summary.Left; cell.Y -= summary.Top;
                        cells[(cell.Y - bounds.Top) * 3 + cell.X - bounds.Left] = cell;
                    }
                }
            }
            // Only an exhausted, current, validated source read proves empty
            // neighbours. Cancellation/hash failure publishes no partial area.
            CheckProjectionSource(owner, request); obj.X -= summary.Left; obj.Y -= summary.Top;
            return new BlueprintProjectionWindow(request.Identity, request.Generation, summary, bounds, cells, new[] { obj }, true);
        }
        private int ProjectionDoorCellLowerBound(CaptureChunk chunk, int x, int y)
        {
            int low = 0, high = chunk.Count;
            while (low < high)
            {
                int middle = low + (high - low) / 2; RecordProjectionObjectMemberAuxiliaryRecord(); var cell = chunk.Cells[middle];
                if (cell.Y < y || cell.Y == y && cell.X < x) low = middle + 1; else high = middle;
            }
            return low;
        }
    }
}
