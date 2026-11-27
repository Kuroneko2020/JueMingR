using System.Collections.Generic;
using JueMingR.Features.Blueprint;

namespace JueMingR.TerrariaHost.Blueprint
{
    internal sealed partial class HostBlueprint
    {
        internal sealed class TaskProjectionRead
        {
            internal BlueprintProjectionIdentity Identity;
            internal BlueprintProjectionBounds Bounds;
            internal BlueprintProjectionWindow Window;
            internal bool Open, Requested;
            internal string Error;
        }

        private readonly Dictionary<BlueprintProjectionIdentity,TaskProjectionRead> taskProjectionReads =
            new Dictionary<BlueprintProjectionIdentity,TaskProjectionRead>();

        internal TaskProjectionRead BeginTaskProjectionRead(BlueprintProjectionPlacement placement,BlueprintProjectionBounds bounds)
        {
            if (!HasSession || Library == null || !projectionScene.Contains(placement)) return null;
            var asset = placement.Identity;
            var identity = new BlueprintProjectionIdentity(asset.AssetId,asset.ContentRevision,asset.ContentHash,
                generation,++projectionGeneration);
            if (!Library.RequestProjectionOpen(identity)) return null;
            var read = new TaskProjectionRead { Identity = identity, Bounds = bounds };
            taskProjectionReads.Add(identity,read);
            return read;
        }

        internal void PollTaskProjectionRead(TaskProjectionRead read)
        {
            if (HasSession && read.Open && !read.Requested && read.Error == null && read.Window == null)
                read.Requested = Library.RequestProjectionWindow(read.Identity,1,read.Bounds);
        }

        internal void ReleaseTaskProjectionRead(TaskProjectionRead read)
        {
            if (read != null && taskProjectionReads.Remove(read.Identity)) Library?.ReleaseProjection(read.Identity);
        }

        private bool AcceptTaskProjectionRead(BlueprintProjectionReceipt receipt)
        {
            if (!taskProjectionReads.TryGetValue(receipt.Identity,out var read)) return false;
            switch (receipt.Kind)
            {
                case BlueprintProjectionReceiptKind.Opened: read.Open = true; break;
                case BlueprintProjectionReceiptKind.Window: read.Window = receipt.Window; break;
                case BlueprintProjectionReceiptKind.Failed: read.Error = receipt.Error ?? "未能读取蓝图区块。"; break;
            }
            return true;
        }
    }
}
