using System;
using System.Collections.Generic;
using System.IO;

namespace JueMingR.Features.Blueprint
{
    internal sealed partial class LibraryWorker
    {
        private IEnumerable<bool> ValidateProjectionMirror(BlueprintProjectionCommitRequest request, ProjectionOwner owner)
        {
            var candidate = request.Candidate; var profile = candidate.MirrorProfile; var summary = owner.Root.Summary;
            if (profile == null || !profile.Ready) throw new InvalidDataException("blueprint-mirror-profile-not-ready");
            var visited = new HashSet<int>();
            foreach (var range in owner.SourceRanges)
            {
                CheckProjectionCommit(request); var chunk = ProjectionChunk(owner, range.Block); var ids = new List<int>();
                RecordProjectionObjectMemberBlock();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if ((i & 31) == 0) { CheckProjectionCommit(request); yield return true; }
                    RecordProjectionObjectMemberRecord();
                    var cell = chunk.Cells[i]; cell.X -= summary.Left; cell.Y -= summary.Top; cell = candidate.Mask.Apply(cell);
                    if (!cell.HasContent) continue; string reason;
                    if ((cell.Layers & CaptureLayer.Tile) == 0 || cell.ObjectId <= 0)
                    {
                        CaptureCell ignored;
                        if (!profile.TryMirrorCell(cell, summary.Width, out ignored, out reason)) throw new InvalidDataException(reason ?? "blueprint-mirror-cell");
                    }
                    else if (visited.Add(cell.ObjectId)) ids.Add(cell.ObjectId);
                }
                int next = 0;
                while (next < ids.Count)
                {
                    using (var batch = PlanProjectionObjectMemberBatch(owner, ids, ref next))
                    {
                        foreach (bool step in CollectProjectionObjectMembersSteps(owner, batch, () => CheckProjectionCommit(request))) yield return step;
                        foreach (var entry in batch.Entries.Values)
                        {
                            CheckProjectionCommit(request); var members = entry.Members;
                            // Object Tile is all-or-none in a committed mask;
                            // same-cell non-Tile layers retain independent bits.
                            for (int n = 0; n < members.Count; n++)
                            { members[n] = candidate.Mask.Apply(members[n]); if ((n & 31) == 0) { CheckProjectionCommit(request); yield return true; } }
                            CaptureObject ignored; CaptureCell[] ignoredMembers; string reason;
                            RecordProjectionObjectInterpretation();
                            if (!profile.TryMirrorObject(entry.Metadata, members, summary.Width, out ignored, out ignoredMembers, out reason))
                                throw new InvalidDataException(reason ?? "blueprint-mirror-object");
                            yield return true;
                        }
                    }
                }
                yield return true;
            }
        }
    }
}
