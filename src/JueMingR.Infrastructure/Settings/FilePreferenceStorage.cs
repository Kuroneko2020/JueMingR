using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Settings;

namespace JueMingR.Infrastructure.Settings
{
    // Configuration retains its original size/failure contract. Notes shares only
    // the mechanical atomic-file implementation, not preference coalescing.
    public sealed class FilePreferenceStorage : IPreferenceStorage
    {
        private readonly AtomicFileDocument document;
        public FilePreferenceStorage(string fullDocumentPath) { document = new AtomicFileDocument(fullDocumentPath, 65536); }
        public PreferenceReadResult Read() { return document.Read(); }
        public PreferenceWriteResult Write(string expectedIdentity, byte[] contents) { return document.Write(expectedIdentity, contents); }
        public void Dispose() { document.Dispose(); }
    }
}
