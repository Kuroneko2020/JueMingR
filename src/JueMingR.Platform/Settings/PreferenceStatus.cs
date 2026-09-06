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
        internal PreferenceSnapshot(T value, bool loaded, long revision, PreferenceStatus status)
        { Value = value; IsLoaded = loaded; Revision = revision; Status = status; }
        public T Value { get; private set; }
        public bool IsLoaded { get; private set; }
        public long Revision { get; private set; }
        public PreferenceStatus Status { get; private set; }
    }
}
