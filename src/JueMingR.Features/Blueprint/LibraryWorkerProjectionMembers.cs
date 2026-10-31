using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace JueMingR.Features.Blueprint
{
    internal sealed partial class LibraryWorker
    {
        private sealed class ProjectionObjectMemberEntry
        {
            internal CaptureObject Metadata;
            internal ProjectionSourceObjectIndex Index;
            internal List<CaptureCell> Members;
        }

        // Bodies belong to one finite caller batch, never to ProjectionOwner.
        // The allowance includes a source list, sort scratch, mirrored values
        // and coordinate routes while the consumer interprets that same body.
        private sealed class ProjectionObjectMemberBatch : IDisposable
        {
            private readonly LibraryWorker worker;
            internal readonly Dictionary<int, ProjectionObjectMemberEntry> Entries = new Dictionary<int, ProjectionObjectMemberEntry>();
            internal long PlannedBytes, EstimatedBytes;
            internal bool Collected;
            internal Action<long> WorkingBytes;
            internal ProjectionObjectMemberBatch(LibraryWorker worker) { this.worker = worker; }
            internal void Observe(long bytes)
            {
                worker.ChargeProjectionObjectMemberWorkingBytes(bytes - EstimatedBytes);
                EstimatedBytes = bytes; WorkingBytes?.Invoke(bytes);
            }
            public void Dispose()
            {
                Observe(0); WorkingBytes = null; Entries.Clear();
            }
        }

        // IDs are already distinct at the caller's operation scope. Admit a
        // byte-bounded prefix before allocating bodies; an existing legal large
        // object is served alone instead of gaining an arbitrary object limit.
        private ProjectionObjectMemberBatch PlanProjectionObjectMemberBatch(ProjectionOwner owner, IReadOnlyList<int> ids,
            ref int next, long targetBytes = BlueprintProjectionSourceReceipt.TargetBytes)
        {
            if (ids == null || next < 0 || next >= ids.Count || targetBytes <= 0) throw new ArgumentException("blueprint-member-batch-plan");
            var batch = new ProjectionObjectMemberBatch(this); long planned = 256;
            while (next < ids.Count)
            {
                int id = ids[next];
                if (batch.Entries.ContainsKey(id)) { next++; continue; }
                ProjectionSourceObjectIndex index;
                if (id <= 0 || !owner.SourceObjects.TryGetValue(id, out index)) throw new InvalidDataException("blueprint-mirror-object-index");
                var obj = ProjectionObject(owner, id);
                long strings = ((long)(obj.Reason ?? "").Length + (obj.MissingPayload ?? "").Length) * 2;
                long payload = checked(384L + index.Members * 64L + strings);
                if (payload > ProjectionWindowLimit) throw new InvalidDataException("blueprint-projection-window-budget");
                long bytes = checked(512L + index.Members * 384L + index.Blocks.Count * 80L + strings);
                if (batch.Entries.Count != 0 && planned + bytes > targetBytes) break;
                obj.X -= owner.Root.Summary.Left; obj.Y -= owner.Root.Summary.Top;
                batch.Entries.Add(id, new ProjectionObjectMemberEntry { Metadata = obj, Index = index });
                planned = checked(planned + bytes); next++;
                if (planned > targetBytes) break;
            }
            batch.PlannedBytes = planned; batch.Observe(planned);
#if DEBUG
            Interlocked.Increment(ref projectionObjectMemberBatches);
#endif
            return batch;
        }

        private IEnumerable<bool> CollectProjectionObjectMembersSteps(ProjectionOwner owner, ProjectionObjectMemberBatch batch,
            Action check, Action<long> workingBytes = null)
        {
            if (batch == null || check == null || batch.Collected) throw new ArgumentException("blueprint-member-batch-collect");
            batch.WorkingBytes = workingBytes; batch.WorkingBytes?.Invoke(batch.EstimatedBytes);
            var selected = new HashSet<long>(); var blocks = new List<long>();
            foreach (var entry in batch.Entries.Values)
            {
                check(); entry.Members = new List<CaptureCell>(entry.Index.Members);
                foreach (long block in entry.Index.Blocks)
                {
                    if (selected.Add(block)) blocks.Add(block);
                    check(); yield return true;
                }
            }
            // A cached block still costs one dispatch scan. Cache hits go first
            // so decoding misses cannot evict an unread member of this batch.
            var ordered = new List<long>(blocks.Count);
            foreach (long block in blocks) if (FindProjectionBlock(owner, block) != null) ordered.Add(block);
            foreach (long block in blocks) if (FindProjectionBlock(owner, block) == null) ordered.Add(block);
            var summary = owner.Root.Summary;
            foreach (long block in ordered)
            {
                check(); var chunk = ProjectionChunk(owner, block); RecordProjectionObjectMemberBlock();
                for (int i = 0; i < chunk.Count; i++)
                {
                    RecordProjectionObjectMemberRecord(); var cell = chunk.Cells[i]; ProjectionObjectMemberEntry entry;
                    if (cell.ObjectId > 0 && (cell.Layers & CaptureLayer.Tile) != 0 && batch.Entries.TryGetValue(cell.ObjectId, out entry))
                    {
                        // Keep every captured layer before lane filtering. The
                        // full raw body is the profile's independent evidence.
                        cell.X -= summary.Left; cell.Y -= summary.Top;
                        if (cell.X < entry.Metadata.X || cell.Y < entry.Metadata.Y ||
                            (long)cell.X >= (long)entry.Metadata.X + entry.Metadata.Width || (long)cell.Y >= (long)entry.Metadata.Y + entry.Metadata.Height)
                            throw new InvalidDataException("blueprint-mirror-object-members");
                        entry.Members.Add(cell);
                    }
                    if ((i & 31) == 31) { ObserveProjectionObjectMemberCheckpoint(); check(); yield return true; }
                }
                ObserveProjectionObjectMemberCheckpoint(); check(); yield return true;
            }
            foreach (var entry in batch.Entries.Values)
            {
                check();
                if (entry.Members.Count != entry.Index.Members) throw new InvalidDataException("blueprint-mirror-object-members");
                foreach (bool ignored in SortProjectionObjectMembersSteps(entry.Members, check)) yield return true;
                check(); yield return true;
            }
            batch.Collected = true;
        }

        private static IEnumerable<bool> SortProjectionObjectMembersSteps(IList<CaptureCell> members, Action check)
        {
            if (members.Count < 2) yield break;
            var scratch = new CaptureCell[members.Count]; int operations = 0;
            // Bottom-up merging keeps even a large legal body cancellable;
            // List.Sort would otherwise hide the whole sort in one MoveNext.
            for (int width = 1; width < members.Count; width = width > members.Count / 2 ? members.Count : width * 2)
            {
                for (int left = 0; left < members.Count;)
                {
                    int middle = (int)Math.Min((long)members.Count, (long)left + width);
                    int right = (int)Math.Min((long)members.Count, (long)middle + width);
                    int a = left, b = middle;
                    for (int n = left; n < right; n++)
                    {
                        bool takeA = b == right || a < middle && CellComparer.Instance.Compare(members[a], members[b]) <= 0;
                        scratch[n] = takeA ? members[a++] : members[b++];
                        if ((++operations & 31) == 0) { check(); yield return true; }
                    }
                    left = right;
                }
                for (int i = 0; i < members.Count; i++)
                {
                    members[i] = scratch[i];
                    if ((++operations & 31) == 0) { check(); yield return true; }
                }
            }
            for (int i = 1; i < members.Count; i++)
            {
                if (members[i - 1].X == members[i].X && members[i - 1].Y == members[i].Y)
                    throw new InvalidDataException("blueprint-mirror-object-members");
                if ((++operations & 31) == 0) { check(); yield return true; }
            }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        private void ObserveProjectionObjectMemberCheckpoint()
        {
#if DEBUG
            projectionObjectMemberCheckpoint?.Invoke();
#endif
        }
        [System.Diagnostics.Conditional("DEBUG")]
        private void RecordProjectionObjectMemberBlock()
        {
#if DEBUG
            Interlocked.Increment(ref projectionObjectMemberBlockScans);
            Interlocked.Increment(ref projectionSourceMemberBlocks);
#endif
        }
        [System.Diagnostics.Conditional("DEBUG")]
        private void RecordProjectionObjectMemberRecord()
        {
#if DEBUG
            Interlocked.Increment(ref projectionObjectMemberRecordVisits);
            Interlocked.Increment(ref projectionSourceMemberRecords);
#endif
        }
        [System.Diagnostics.Conditional("DEBUG")]
        private void RecordProjectionObjectMemberAuxiliaryRecord()
        {
#if DEBUG
            // Context discovery/probes also inspect real records. Keep the old
            // source collector counter's meaning for its no-View consumers,
            // while the new total includes these bounded auxiliary visits.
            Interlocked.Increment(ref projectionObjectMemberRecordVisits);
#endif
        }
        [System.Diagnostics.Conditional("DEBUG")]
        private void RecordProjectionObjectInterpretation()
        {
#if DEBUG
            Interlocked.Increment(ref projectionObjectMemberInterpretations);
#endif
        }
        [System.Diagnostics.Conditional("DEBUG")]
        private void ChargeProjectionObjectMemberWorkingBytes(long bytes)
        {
#if DEBUG
            long current = Interlocked.Add(ref projectionObjectMemberWorkingBytes, bytes);
            if (current > Interlocked.Read(ref projectionObjectMemberWorkingPeakBytes)) Interlocked.Exchange(ref projectionObjectMemberWorkingPeakBytes, current);
#endif
        }
#if DEBUG
        // The isolated native probe can cancel the exact accepted source at a
        // real collector checkpoint. No Release callback or sampler exists.
        private Action projectionObjectMemberCheckpoint = null;
        private long projectionObjectMemberBatches, projectionObjectMemberBlockScans, projectionObjectMemberRecordVisits;
        private long projectionObjectMemberInterpretations, projectionObjectMemberWorkingBytes, projectionObjectMemberWorkingPeakBytes;
        internal BlueprintProjectionMemberBatchStatistics ProjectionMemberBatchStatistics
        {
            get { return new BlueprintProjectionMemberBatchStatistics(Interlocked.Read(ref projectionObjectMemberBatches), Interlocked.Read(ref projectionObjectMemberBlockScans),
                Interlocked.Read(ref projectionObjectMemberRecordVisits), Interlocked.Read(ref projectionObjectMemberInterpretations),
                Interlocked.Read(ref projectionObjectMemberWorkingBytes), Interlocked.Read(ref projectionObjectMemberWorkingPeakBytes)); }
        }
#endif
    }
#if DEBUG
    public readonly struct BlueprintProjectionMemberBatchStatistics
    {
        internal BlueprintProjectionMemberBatchStatistics(long batches, long blockScans, long recordVisits, long interpretations, long workingBytes, long workingPeakBytes)
        { Batches = batches; BlockScans = blockScans; RecordVisits = recordVisits; Interpretations = interpretations; WorkingBytes = workingBytes; WorkingPeakBytes = workingPeakBytes; }
        public long Batches { get; }
        public long BlockScans { get; }
        public long RecordVisits { get; }
        public long Interpretations { get; }
        public long WorkingBytes { get; }
        public long WorkingPeakBytes { get; }
    }
#endif
}
