namespace JueMingR.Features.Journey
{
    // This is a world-session activation, never a persisted preference. The
    // host owns native resources and confirms command success only after the
    // native window opens; temporary qualification is needed while opening it.
    public sealed class JourneyActivation
    {
        public bool Active { get; private set; }
        public bool Failed { get; private set; }
        public long Revision { get; private set; }
        public void Activate() { if (!Active && !Failed) { Active = true; Revision++; } }
        public void End() { if (Active) { Active = false; Revision++; } }
        public void Fail() { End(); Failed = true; }
    }
}
