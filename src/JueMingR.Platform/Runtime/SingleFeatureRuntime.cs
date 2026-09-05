using System;

namespace JueMingR.Platform.Runtime
{
    public sealed class SingleFeatureRuntime
    {
        private readonly IGameSessionProbe sessionProbe;
        private readonly IRuntimeFeature feature;
        private bool sessionActive;

        public SingleFeatureRuntime(IGameSessionProbe sessionProbe, IRuntimeFeature feature)
        {
            this.sessionProbe = sessionProbe ?? throw new ArgumentNullException(nameof(sessionProbe));
            this.feature = feature ?? throw new ArgumentNullException(nameof(feature));
        }

        public bool IsSessionActive
        {
            get { return sessionActive; }
        }

        public void Update(ulong updateTick)
        {
            bool active;
            try
            {
                active = sessionProbe.IsSessionActive;
            }
            catch
            {
                sessionActive = false;
                FailFeatureClosed();
                return;
            }

            // Session callbacks occur on edges, before any update in the new state.
            if (!active)
            {
                if (sessionActive)
                {
                    sessionActive = false;
                    try
                    {
                        feature.OnSessionEnded();
                    }
                    catch
                    {
                        FailFeatureClosed();
                    }
                }

                return;
            }

            if (!sessionActive)
            {
                sessionActive = true;
                try
                {
                    feature.OnSessionStarted();
                }
                catch
                {
                    sessionActive = false;
                    FailFeatureClosed();
                    return;
                }
            }

            try
            {
                feature.Update(updateTick);
            }
            catch
            {
                FailFeatureClosed();
            }
        }

        private void FailFeatureClosed()
        {
            // The feature owns terminal failure and cleanup; this runtime has no failure latch.
            try
            {
                feature.FailClosed();
            }
            catch
            {
                // Contain the callback exception only; this does not prove cleanup succeeded.
            }
        }
    }
}
