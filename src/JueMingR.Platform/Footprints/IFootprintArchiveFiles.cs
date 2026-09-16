using System;
using JueMingR.Platform.Settings;

namespace JueMingR.Platform.Footprints
{
    // All calls belong to one worker. Catalog's lease excludes other writers for
    // this pair, including while a generation's root is closed for deletion.
    public interface IFootprintArchiveFiles : IDisposable
    {
        PreferenceReadResult ReadCatalog();
        PreferenceWriteResult WriteCatalog(string identity, byte[] bytes);
        PreferenceReadResult OpenRoot(string generation);
        PreferenceWriteResult WriteRoot(string identity, byte[] bytes);
        byte[] ReadBlock(long number);
        void CreateBlock(long number, byte[] bytes);
        bool ValidateBlockNamesStep(long blockCount, int budget);
        bool DeleteGenerationStep(string generation, int budget);
    }

    public sealed class FootprintQuery
    {
        public FootprintQuery(string generation, long end, FootprintSample[] samples, bool partial)
        { Generation = generation; End = end; Samples = samples; Partial = partial; }
        public string Generation { get; }
        public long End { get; }
        // Ownership transfers from worker to reader; published arrays never mutate.
        public FootprintSample[] Samples { get; }
        public bool Partial { get; }
    }
}
