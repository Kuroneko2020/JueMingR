using System;
using System.Collections.Generic;

namespace JueMingR.Platform.Blueprint
{
    // Signed stage proof scans also yield; the existing library remains the
    // only owner and no caller may turn a verified path into a second writer.
    public interface IBlueprintLibraryExchangeSteps
    {
        IEnumerable<int> SealStageSteps(Guid id, byte[] root, int budget);
        // -1 means the next MoveNext may remove the private owner or move its
        // body; ordinary nonnegative proof progress remains cancellable.
        IEnumerable<int> PromoteStageSteps(Guid stageId, Guid assetId, int budget);
    }
}
