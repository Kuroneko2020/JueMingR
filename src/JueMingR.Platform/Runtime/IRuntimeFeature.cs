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

    // Optional return-only work after the shared Runtime has withdrawn all new
    // action/session admission. The owner must already hold the responsibility,
    // validate the original native identity, and never start a new operation.
    public interface IRuntimeFailureCleanup
    {
        void UpdateFailureCleanup(ulong updateTick);
    }
}
