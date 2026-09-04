namespace JueMingR.Platform.Runtime
{
    public interface IRuntimeFeature
    {
        bool Enabled { get; }

        void OnSessionStarted();

        void OnSessionEnded();

        void Update(ulong updateTick);

        void FailClosed();
    }
}
