using System;
using System.Collections.Generic;

namespace JueMingR.Platform.Runtime
{
    public sealed class SingleFeatureRuntime
    {
        private readonly IGameSessionProbe sessionProbe;
        private readonly List<IRuntimeFeature> features = new List<IRuntimeFeature>();
        private bool sessionActive;
        private bool observed;
        private object sessionIdentity;

        public SingleFeatureRuntime(IGameSessionProbe sessionProbe, IRuntimeFeature feature)
        {
            this.sessionProbe = sessionProbe ?? throw new ArgumentNullException(nameof(sessionProbe));
            features.Add(feature ?? throw new ArgumentNullException(nameof(feature)));
        }

        public long Generation { get; private set; }
        public void AddFeature(IRuntimeFeature feature)
        {
            if (observed) throw new InvalidOperationException("Runtime composition is frozen before its first observation.");
            if (feature == null) throw new ArgumentNullException(nameof(feature));
            if (features.Contains(feature)) throw new ArgumentException("Feature already attached.");
            features.Add(feature);
        }

        public bool IsSessionActive
        {
            get { return sessionActive; }
        }

        public void Update(ulong updateTick)
        {
            observed = true;
            bool active;
            object identity;
            try
            {
                active = sessionProbe.IsSessionActive;
                var identified = sessionProbe as IGameSessionIdentityProbe;
                identity = !active ? null : identified == null ? sessionProbe : identified.SessionIdentity;
                if (active && identity == null) throw new InvalidOperationException("Active session has no identity.");
            }
            catch
            {
                InvalidateSession();
                foreach (IRuntimeFeature feature in features) FailFeatureClosed(feature);
                return;
            }

            // Session callbacks occur on edges, before any update in the new state.
            if (!active)
            {
                InvalidateSession();
                return;
            }

            if (sessionActive && !ReferenceEquals(identity, sessionIdentity)) InvalidateSession();

            if (!sessionActive)
            {
                sessionActive = true;
                sessionIdentity = identity;
                Generation = checked(Generation + 1);
                foreach (IRuntimeFeature feature in features)
                {
                    try { feature.OnSessionStarted(); }
                    catch { FailFeatureClosed(feature); }
                }
            }

            foreach (IRuntimeFeature feature in features)
            {
                try { feature.Update(updateTick); }
                catch { FailFeatureClosed(feature); }
            }
        }

        public void InvalidateSession()
        {
            if (!sessionActive) return;
            sessionActive = false; sessionIdentity = null;
            foreach (IRuntimeFeature feature in features)
            {
                try { feature.OnSessionEnded(); }
                catch { FailFeatureClosed(feature); }
            }
        }

        private static void FailFeatureClosed(IRuntimeFeature feature)
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
