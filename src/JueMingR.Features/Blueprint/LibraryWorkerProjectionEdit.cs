using System;
using System.Collections.Generic;
using System.IO;

namespace JueMingR.Features.Blueprint
{
    internal sealed partial class LibraryWorker
    {
        private List<CaptureCell> ProjectionObjectMembers(ProjectionOwner owner, int id, bool normalized = true)
        {
            ProjectionSourceObjectIndex index;
            if (!owner.SourceObjects.TryGetValue(id, out index)) throw new InvalidDataException("blueprint-mirror-object-index");
            var result = new List<CaptureCell>(index.Members); var summary = owner.Root.Summary;
            foreach (long block in index.Blocks)
            {
                var chunk = ProjectionChunk(owner, block);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var cell = chunk.Cells[i]; if (cell.ObjectId != id || (cell.Layers & CaptureLayer.Tile) == 0) continue;
                    if (normalized) { cell.X -= summary.Left; cell.Y -= summary.Top; }
                    result.Add(cell);
                }
            }
            result.Sort((a, b) => a.Y == b.Y ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y));
            if (result.Count != index.Members) throw new InvalidDataException("blueprint-mirror-object-members");
            return result;
        }
        private IEnumerable<bool> ValidateProjectionMirror(BlueprintProjectionCommitRequest request, ProjectionOwner owner)
        {
            var candidate = request.Candidate; var profile = candidate.MirrorProfile; var summary = owner.Root.Summary;
            if (profile == null || !profile.Ready) throw new InvalidDataException("blueprint-mirror-profile-not-ready");
            var visited = new HashSet<int>();
            foreach (var range in owner.SourceRanges)
            {
                CheckProjectionCommit(request); var chunk = ProjectionChunk(owner, range.Block);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var cell = chunk.Cells[i]; cell.X -= summary.Left; cell.Y -= summary.Top; cell = candidate.Mask.Apply(cell);
                    if (!cell.HasContent) continue; string reason;
                    if ((cell.Layers & CaptureLayer.Tile) == 0 || cell.ObjectId <= 0)
                    {
                        CaptureCell ignored;
                        if (!profile.TryMirrorCell(cell, summary.Width, out ignored, out reason)) throw new InvalidDataException(reason ?? "blueprint-mirror-cell");
                    }
                    else if (visited.Add(cell.ObjectId))
                    {
                        var obj = ProjectionObject(owner, cell.ObjectId); obj.X -= summary.Left; obj.Y -= summary.Top;
                        var members = ProjectionObjectMembers(owner, obj.Id);
                        // Masks cannot split a positive object Tile. Other
                        // layers follow their own cells and are not canonical members.
                        for (int n = 0; n < members.Count; n++) members[n] = candidate.Mask.Apply(members[n]);
                        CaptureObject ignored; CaptureCell[] ignoredMembers;
                        if (!profile.TryMirrorObject(obj, members, summary.Width, out ignored, out ignoredMembers, out reason)) throw new InvalidDataException(reason ?? "blueprint-mirror-object");
                    }
                }
                yield return true;
            }
        }
        private IEnumerable<bool> ComputeProjectionTrim(BlueprintProjectionCommitRequest request, ProjectionOwner owner)
        {
            if (!request.TrimBounds.HasValue) throw new InvalidDataException("blueprint-trim-bounds");
            var placement = request.Candidate; var view = placement.View; var display = request.TrimBounds.Value;
            if (display.Left < 0 || display.Top < 0 || display.Right > view.Width || display.Bottom > view.Height) throw new InvalidDataException("blueprint-trim-bounds");
            var source = view.SourceBounds(display); var summary = owner.Root.Summary;
            var builder = new ProjectionMaskBuilder(placement.Mask); var objects = new HashSet<int>(); var extensions = new List<BlueprintProjectionBounds>();
            foreach (long block in owner.Directory.Query(checked(source.Left + summary.Left), checked(source.Top + summary.Top), checked(source.Right + summary.Left), checked(source.Bottom + summary.Top), ProjectionComparison))
            {
                CheckProjectionCommit(request); var chunk = ProjectionChunk(owner, block);
                for (int i = 0; i < chunk.Count; i++)
                {
                    var cell = chunk.Cells[i]; cell.X -= summary.Left; cell.Y -= summary.Top;
                    if (!source.Contains(cell.X, cell.Y)) continue; cell = builder.View.Apply(cell);
                    ushort bits = BlueprintProjectionMask.Layers(cell);
                    if ((bits & 1) != 0 && cell.ObjectId > 0) { objects.Add(cell.ObjectId); bits &= 0xfe; }
                    builder.AddCell(cell, bits);
                }
                yield return true;
            }
            foreach (int id in objects)
            {
                CheckProjectionCommit(request); if (builder.Objects.Contains(id)) continue;
                var obj = ProjectionObject(owner, id); var members = ProjectionObjectMembers(owner, id);
                long removed = 0;
                foreach (var raw in members) if ((builder.View.Apply(raw).Layers & CaptureLayer.Tile) != 0) removed++;
                if (removed == 0) continue;
                if (removed != (long)obj.Width * obj.Height || !obj.Reliable) throw new InvalidDataException("blueprint-trim-object-incomplete");
                builder.Removed = checked(builder.Removed + removed); builder.RemovedConstruction = checked(builder.RemovedConstruction + removed); builder.Objects.Add(id);
                extensions.Add(new BlueprintProjectionBounds(obj.X - summary.Left, obj.Y - summary.Top, obj.Width, obj.Height));
                yield return true;
            }
            CheckProjectionCommit(request);
            var plan = new BlueprintProjectionTrimPlan(placement.Mask, builder.Mask, display, extensions);
            lock (gate) if (ReferenceEquals(projectionActiveCommit, request))
                projectionCommitReceipt = new BlueprintProjectionCommitReceipt(request, new Dictionary<long, BlueprintProjectionMask>(), new List<long>(), trimPlan: plan);
        }
    }
}
