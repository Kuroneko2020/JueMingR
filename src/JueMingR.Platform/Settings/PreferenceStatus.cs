using System;

namespace JueMingR.Platform.Settings
{
    public enum PreferenceStatus
    {
        Loading, Missing, Saved, Pending, Invalid, UnsupportedVersion,
        UnknownFields, IoFailure, Conflict, Busy, Stopped
    }

    public sealed class PreferenceFormatException : Exception
    {
        public PreferenceFormatException(PreferenceStatus status, string message) : base(message) { Status = status; }
        public PreferenceStatus Status { get; private set; }
    }

    public sealed class PreferenceSnapshot<T>
    {
        internal PreferenceSnapshot(T value, bool loaded, long revision, PreferenceStatus status,
            bool commitUnconfirmed = false, bool isProtected = false, string error = null)
        { Value = value; IsLoaded = loaded; Revision = revision; Status = status;
            CommitUnconfirmed = commitUnconfirmed; IsProtected = isProtected; Error = error; }
        public T Value { get; private set; }
        public bool IsLoaded { get; private set; }
        public long Revision { get; private set; }
        public PreferenceStatus Status { get; private set; }
        // A failed verification after replacement cannot promise either old or
        // new bytes. Keep this mechanical outcome separate from in-memory intent.
        public bool CommitUnconfirmed { get; private set; }
        public bool IsProtected { get; private set; }
        public string Error { get; private set; }
    }
}
