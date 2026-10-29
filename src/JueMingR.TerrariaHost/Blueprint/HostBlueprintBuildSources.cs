using System;
using System.Collections.Generic;
using JueMingR.Features.Blueprint;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        private sealed class BuildSourcePart
        {
            internal BlueprintProjectionPlacement Owner;
            internal BlueprintProjectionMatchRange Range;
            internal BlueprintProjectionMask Mask;
            internal BlueprintProjectionSourcePart Part;
            internal int X, Y;
            internal long Catalog;
            internal long NearVersion=-1, NearWake=-1, NearPlayer=-1, NearSupply=-1;
            internal int NearCursor, FrontierCursor;
            internal BlueprintProjectionBounds NearBounds, FrontierBounds;
            internal bool Frontier, NearInitialized, NearComplete, NearRevisit, LocalFirst = true;
#if JMR_BLUEPRINT_DIAGNOSTIC
            internal int DiagnosticResetBefore,DiagnosticResetAfter,DiagnosticResetReasons;
#endif
        }
        // Reuse the immutable transport, not a second world/target table. Four
        // recent parts share the match owner's retained-byte accounting. The
        // per-range ordinal survives eviction; a later source resumes exactly
        // there, even when sampling crossed an Update or a different budget.
        private const int BuildSourcePartLimit = 4;
        // Each retained source gets a bounded local and forward slice from the
        // one shared budget. A hit rotates both source and class before return.
        private const int BuildSourceSlice = 8;
        private const long BuildSourceByteLimit = 4 * BlueprintProjectionSourceReceipt.TargetBytes;
        private static long BuildSourceRetainedBytes(BlueprintProjectionSourcePart part) { return part.EstimatedBytes + 256; }
        private readonly LinkedList<BuildSourcePart> buildSources = new LinkedList<BuildSourcePart>();
        private long buildSourceBytes;
        private int materialBuildSourceBudget, buildSourceCursor;

        private void RetainBuildSource(BlueprintProjectionMatchRead read)
        {
            if (MaterialBuildDemand?.Invoke() != true || read.Batch == null || read.MaterialCatalog == null ||
                BuildSourceRetainedBytes(read.Batch) > BuildSourceByteLimit) return;
            if (read.BuildSourceTickSet && read.BuildSourceTick == projectionMatchTick) return;
            read.BuildSourceTickSet = true; read.BuildSourceTick = projectionMatchTick;
            var retained = FindBuildSource(read.Range);
            if (retained != null && ReferenceEquals(retained.Window, read.Batch.Window)) return;
            RemoveBuildSources(read.Owner.Identity, read.Range);
            while (buildSources.Count >= BuildSourcePartLimit || buildSourceBytes + BuildSourceRetainedBytes(read.Batch) > BuildSourceByteLimit)
            {
                var victim = BuildSourceVictim(read.Batch.Window.Bounds,read.Owner);
                if (victim == null) return;
                RemoveBuildSource(victim);
            }
            buildSources.AddLast(new BuildSourcePart { Owner = read.Owner, Range = read.Range, Mask = read.Owner.Mask,
                Part = read.Batch, X = read.Owner.OriginX, Y = read.Owner.OriginY, Catalog = read.MaterialCatalog.Revision });
            buildSourceBytes += BuildSourceRetainedBytes(read.Batch);
        }
        private void RemoveBuildSource(LinkedListNode<BuildSourcePart> node)
        { if (node != null) { buildSourceBytes -= BuildSourceRetainedBytes(node.Value.Part); buildSources.Remove(node); } }
        private void RemoveBuildSources(BlueprintProjectionIdentity identity = null, BlueprintProjectionMatchRange range = null)
        {
            if(identity!=null && range==null)
            {
                CancelWiringBuildIdentity(identity);
                if(buildSupplyTarget?.Identity.Equals(identity)==true){buildMaterialPreparation?.RequestReturn();buildSupplyTarget=null;}
            }
            for (var node = buildSources.First; node != null;)
            {
                var next = node.Next;
                if ((identity == null || node.Value.Owner.Identity.Equals(identity)) && (range == null || ReferenceEquals(node.Value.Range, range))) RemoveBuildSource(node);
                node = next;
            }
        }
        private BlueprintProjectionSourcePart FindBuildSource(BlueprintProjectionMatchRange range)
        {
            for (var node = buildSources.First; node != null; node = node.Next)
            {
                var value = node.Value;
                if (!ReferenceEquals(value.Range, range)) continue;
                var owner = range.Owner;
                if (!ReferenceEquals(value.Owner, owner) || !ReferenceEquals(value.Mask, owner.Mask) || value.X != owner.OriginX || value.Y != owner.OriginY ||
                    value.Catalog != MaterialCatalog?.Revision || !projectionScene.Contains(owner))
                { RemoveBuildSource(node); return null; }
                return value.Part;
            }
            return null;
        }
        private bool ReuseBuildSources(BlueprintProjectionPlacement owner, List<BlueprintProjectionSourceDemand> demands, BlueprintProjectionMatchWork work)
        {
            if (MaterialBuildDemand?.Invoke() != true) return false;
            foreach (var demand in demands)
            {
                var part = FindBuildSource(owner.Match.Ranges[demand.Range]);
                if (part == null || part.Offset != demand.Offset) { work.Parts.Clear(); return false; }
                work.Parts.Add(part);
            }
            return true;
        }
        private bool ContinueMaterialBuildSource(BlueprintProjectionMatchRange range, out BlueprintBuildTarget target, int stepLimit)
        {
#if JMR_BLUEPRINT_DIAGNOSTIC
            BlueprintBuildTrace.Hit(BuildTraceStage.Forward);
            BlueprintBuildTrace.Count(BuildTraceReason.ForwardVisit);
            if(materialBuildSourceBudget<=0)BlueprintBuildTrace.Count(BuildTraceReason.ForwardZeroBudget);
#endif
            target = null;
            var value = range.Materials;
            if (value == null || value.BuildExhausted)
            {
#if JMR_BLUEPRINT_DIAGNOSTIC
                BlueprintBuildTrace.Count(BuildTraceReason.SourceExhausted);
#endif
                return false;
            }
            var part = FindBuildSource(range);
            if (part == null)
            {
#if JMR_BLUEPRINT_DIAGNOSTIC
                BlueprintBuildTrace.Count(BuildTraceReason.SourceMissing);
#endif
                if (BuildSourceCapacityAvailable()) DemandProjectionMatchRange(range);
                return false;
            }
            long support;
            if (!range.Group.Ready || !ProjectionMatchReadCurrent(range, range.RawVersion) ||
                !projectionChanges.TryGetBuildStamp(range.Watch, range.PartIndex, out support))
            {
#if JMR_BLUEPRINT_DIAGNOSTIC
                BlueprintBuildTrace.Count(BuildTraceReason.SourceNotCurrent);
#endif
                return false;
            }
            while (materialBuildSourceBudget > 0 && stepLimit-- > 0)
            {
                int index = BuildMemberIndex(value.CandidateCursor) - part.Offset;
                if (index < 0) { DemandProjectionMatchRange(range);
#if JMR_BLUEPRINT_DIAGNOSTIC
                    BlueprintBuildTrace.Count(BuildTraceReason.SourceOffset);
#endif
                    return false; }
                if (index >= part.Window.Cells.Count)
                { value.BuildExhausted = part.Complete;
#if JMR_BLUEPRINT_DIAGNOSTIC
                    BlueprintBuildTrace.Count(BuildTraceReason.SourceEnd);
#endif
                    return false; }
                materialBuildSourceBudget--;
#if JMR_BLUEPRINT_DIAGNOSTIC
                BlueprintBuildTrace.Count(BuildTraceReason.ForwardStep);
#endif
#if DEBUG
                ProjectionMatchSourceSteps++;
#endif
                int ordinal = value.CandidateCursor;
                int following;
                if (TryCreateConstructionTargetAndAdvance(range.Owner,range,part,index,ordinal,support,out target,out following))
                {
                    target.LocalProof = true; value.Candidate = target; return true;
                }
                value.CandidateCursor = following;
            }
#if JMR_BLUEPRINT_DIAGNOSTIC
            BlueprintBuildTrace.Count(BuildTraceReason.SourceBudgetEmpty);
#endif
            return false;
        }
        private bool BuildSourceCapacityAvailable()
        {
            // A paused discovery walk keeps its ordinal in the range, never a
            // permanent cache pin. Nearby immutable transport can displace it.
            return true;
        }
        private bool TryRetainedBuildTarget(out BlueprintBuildTarget target)
        {
            target = null;
            // Spend the same 64 source steps on at most four live walks before
            // discovering another locality. A large mixed template must not
            // revisit hundreds of exhausted object ranges for each ordinary
            // candidate. Source discovery keeps its own bounded scene cursor.
            for (int n = 0; n < BuildSourcePartLimit && buildSources.Count > 0 && materialBuildSourceBudget > 0; n++)
            {
                int count = buildSources.Count, at = buildSourceCursor % count;
                var node = buildSources.First;
                for (int i = 0; i < at; i++) node = node.Next;
                buildSourceCursor = (at + 1) % count;
                var source = node.Value;
                bool localFirst = source.LocalFirst; source.LocalFirst = !localFirst;
                if (localFirst)
                {
                    if (TryNearbyBuildTarget(source, BuildSourceSlice, out target)) return true;
                    if (TryMaterialBuildTarget(source.Range, out target, BuildSourceSlice)) return true;
                }
                else
                {
                    if (TryMaterialBuildTarget(source.Range, out target, BuildSourceSlice)) return true;
                    if (TryNearbyBuildTarget(source, BuildSourceSlice, out target)) return true;
                }
            }
            return false;
        }
        private static int NextBuildSourceOrdinal(int ordinal, CaptureCell cell, bool logicalObject)
        {
            return NextBuildOrdinal(ordinal, cell);
        }
    }
}
