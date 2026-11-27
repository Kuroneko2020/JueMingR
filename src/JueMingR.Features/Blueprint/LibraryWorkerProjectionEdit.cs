using System;
using System.Collections.Generic;
using System.IO;

namespace JueMingR.Features.Blueprint
{
    internal sealed partial class LibraryWorker
    {
        // Candidate commits and saved mirrored restore prove the same complete
        // source interpretation. Door context adds only proven empty slots to
        // those records and maps them through the same coordinate domain.
        private IEnumerable<bool> ValidateProjectionView(ProjectionOwner owner, BlueprintProjectionView view, Action check)
        {
            var profile = view.Profile; var summary = owner.Root.Summary;
            if (profile == null || !profile.Ready) throw new InvalidDataException("blueprint-mirror-profile-not-ready");
            var visited = new HashSet<int>();
            foreach (var range in owner.SourceRanges)
            {
                check(); var chunk = ProjectionChunk(owner, range.Block); var ids = new List<int>();
                RecordProjectionObjectMemberBlock();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if ((i & 31) == 0) { check(); yield return true; }
                    RecordProjectionObjectMemberRecord();
                    var cell = chunk.Cells[i]; cell.X -= summary.Left; cell.Y -= summary.Top; cell = view.Mask.Apply(cell);
                    if (!cell.HasContent) continue; string reason;
                    if ((cell.Layers & CaptureLayer.Tile) == 0 || cell.ObjectId <= 0)
                    {
                        CaptureCell ignored;
                        if (!view.TryTransformCell(cell, false, out ignored, out reason)) throw new ProjectionInterpretationException("blueprint-view-cell", reason);
                    }
                    else if (visited.Add(cell.ObjectId)) ids.Add(cell.ObjectId);
                }
                int next = 0;
                while (next < ids.Count)
                {
                    using (var batch = PlanProjectionObjectMemberBatch(owner, ids, ref next))
                    {
                        foreach (bool step in CollectProjectionObjectMembersSteps(owner, batch, check)) yield return step;
                        foreach (var entry in batch.Entries.Values)
                        {
                            check(); var members = entry.Members;
                            // Object Tile is all-or-none in a committed mask;
                            // same-cell non-Tile layers retain independent bits.
                            for (int n = 0; n < members.Count; n++)
                            { members[n] = view.Mask.Apply(members[n]); if ((n & 31) == 0) { check(); yield return true; } }
                            CaptureObject ignored; CaptureCell[] ignoredMembers; string reason;
                            RecordProjectionObjectInterpretation();
                            if (!view.TryTransformObject(entry.Metadata, members, out ignored, out ignoredMembers, out reason))
                                throw new ProjectionInterpretationException("blueprint-view-object", reason);
                            yield return true;
                        }
                    }
                }
                yield return true;
            }
        }
    }
}
