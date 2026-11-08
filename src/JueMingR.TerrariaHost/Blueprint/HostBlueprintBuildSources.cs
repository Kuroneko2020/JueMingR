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
        private int materialBuildSourceBudget;

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
        private static int NextBuildSourceOrdinal(int ordinal, CaptureCell cell, bool logicalObject)
        {
            return NextBuildOrdinal(ordinal, cell);
        }
    }
}
