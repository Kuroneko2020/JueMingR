namespace JueMingR.Platform.Combat
{
    public enum PredictionPlayerConditionKind { None, HeldControls, CurrentObservation }

    // Owned by the immutable publication, not the Source's latest sample.
    // One rolling player timeline is supported; acquiring a different required
    // future remains the existing MissingDependency boundary.
    public sealed class PredictionPlayerCondition
    {
        public static readonly PredictionPlayerCondition None = new PredictionPlayerCondition();
        public PredictionPlayerConditionKind Kind { get; }
        public PredictionPlayerMotion Sample { get; }
        public bool Alive { get; }
        private PredictionPlayerCondition() { }
        public PredictionPlayerCondition(PredictionPlayerConditionKind kind, PredictionPlayerMotion sample,bool alive=true)
        { Kind = kind; Sample = sample; Alive = alive; }
    }
}
