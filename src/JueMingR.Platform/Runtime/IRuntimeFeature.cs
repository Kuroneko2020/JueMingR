namespace JueMingR.Platform.Runtime
{
    public interface IRuntimeFeature
    {
        bool Enabled { get; }

        void OnSessionStarted();

        void OnSessionEnded();

        void Update(ulong updateTick);

        // The feature owns terminal disable and stale-state cleanup, including across session re-entry.
        void FailClosed();
    }
}
