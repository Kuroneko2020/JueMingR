using System;
using System.Collections.Generic;

namespace JueMingR.Features.Blueprint
{
    internal sealed partial class LibraryWorker
    {
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
                    var wall = chunk.Cells[index];
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
                                var cell = nearby.Cells[at++];
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
                        var cell = chunk.Cells[at++]; cell.X -= summary.Left; cell.Y -= summary.Top;
                        cells[(cell.Y - bounds.Top) * 3 + cell.X - bounds.Left] = cell;
                    }
                }
            }
            // Only an exhausted, current, validated source read proves empty
            // neighbours. Cancellation/hash failure publishes no partial area.
            CheckProjectionSource(owner, request); obj.X -= summary.Left; obj.Y -= summary.Top;
            return new BlueprintProjectionWindow(request.Identity, request.Generation, summary, bounds, cells, new[] { obj }, true);
        }
        private static int ProjectionDoorCellLowerBound(CaptureChunk chunk, int x, int y)
        {
            int low = 0, high = chunk.Count;
            while (low < high)
            {
                int middle = low + (high - low) / 2; var cell = chunk.Cells[middle];
                if (cell.Y < y || cell.Y == y && cell.X < x) low = middle + 1; else high = middle;
            }
            return low;
        }
    }
}
