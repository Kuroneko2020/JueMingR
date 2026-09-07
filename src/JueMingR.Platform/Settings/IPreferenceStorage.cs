using System;

namespace JueMingR.Platform.Settings
{
    // One sequential owner validates document semantics before passing bytes to Write.
    // The storage lease belongs to this instance and ends at Dispose, not at Read.
    public interface IPreferenceStorage : IDisposable
    {
        PreferenceReadResult Read();
        PreferenceWriteResult Write(string expectedIdentity, byte[] contents);
    }

    public enum PreferenceReadStatus { Missing, Loaded, Busy, IoFailure, TooLarge }
    public enum PreferenceWriteStatus { Saved, Conflict, Busy, IoFailure }

    public sealed class PreferenceReadResult
    {
        private readonly byte[] contents;

        public PreferenceReadResult(PreferenceReadStatus status, byte[] contents, string identity, string error)
        {
            Status = status;
            this.contents = contents == null ? null : (byte[])contents.Clone();
            Identity = identity;
            Error = error;
        }

        public PreferenceReadStatus Status { get; }
        public byte[] Contents { get { return contents == null ? null : (byte[])contents.Clone(); } }
        public string Identity { get; }
        public string Error { get; }
    }

    public sealed class PreferenceWriteResult
    {
        public PreferenceWriteResult(PreferenceWriteStatus status, string identity, string error, bool commitUnconfirmed = false, bool isProtected = false)
        {
            Status = status;
            Identity = identity;
            Error = error;
            CommitUnconfirmed = commitUnconfirmed;
            IsProtected = isProtected;
        }

        public PreferenceWriteStatus Status { get; }
        public bool CommitUnconfirmed { get; }
        public bool IsProtected { get; }
        public string Identity { get; }
        public string Error { get; }
    }
}
