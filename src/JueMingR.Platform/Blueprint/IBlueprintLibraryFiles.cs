using System;
using System.Collections.Generic;
using System.IO;
using JueMingR.Platform.Settings;

namespace JueMingR.Platform.Blueprint
{
    // One runtime-owned worker owns this port. Only opaque bounded bytes and
    // fixed logical IDs cross it; schema and the meaning of a save stay outside.
    public interface IBlueprintLibraryFiles : IDisposable
    {
        void Open();
        IEnumerable<Guid> EnumerateAssets();
        PreferenceReadResult OpenMetadata(Guid assetId, bool ownedUnpublishedContent = false);
        PreferenceWriteResult WriteMetadata(string expectedIdentity, byte[] contents);
        void CloseMetadata();
        void BeginStage(Guid id);
        void WriteStageBlock(Guid id, long number, byte[] contents, byte[] hash);
        byte[] ReadBlock(Guid id, bool stage, long number);
        Stream OpenIndex(Guid id, bool stage);
        void SealStage(Guid id, byte[] root);
        byte[] ReadRoot(Guid id, bool stage);
        void ValidateLayout(Guid id, bool stage, long blockCount);
        void PromoteStage(Guid stageId, Guid assetId);
        bool CleanupStep(Guid id, bool stage, long blockCount, int budget);
    }
}
