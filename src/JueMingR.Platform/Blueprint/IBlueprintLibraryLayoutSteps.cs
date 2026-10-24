using System;
using System.Collections.Generic;

namespace JueMingR.Platform.Blueprint
{
    // Optional incremental mechanical port. It has the same strict layout
    // proof as ValidateLayout, but lets the sole worker serve accepted commands
    // between batches without creating a second file-owning thread.
    public interface IBlueprintLibraryLayoutSteps
    {
        IEnumerable<int> ValidateLayoutSteps(Guid id, bool stage, long blockCount, int budget);
    }
}
