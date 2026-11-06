using System;
using System.Collections.Generic;

namespace JueMingR.Platform.Runtime
{
    public sealed class SingleFeatureRuntime
    {
        private readonly IGameSessionProbe sessionProbe;
        private readonly List<IRuntimeFeature> features = new List<IRuntimeFeature>();
        private readonly HashSet<IRuntimeFeature> failedCleanup = new HashSet<IRuntimeFeature>();
        private bool sessionActive;
        private bool observed;
        private object sessionIdentity;

        public SingleFeatureRuntime(IGameSessionProbe sessionProbe, IRuntimeFeature feature)
        {
            this.sessionProbe = sessionProbe ?? throw new ArgumentNullException(nameof(sessionProbe));
            features.Add(feature ?? throw new ArgumentNullException(nameof(feature)));
        }

        public long Generation { get; private set; }
        public bool HasFailed { get; private set; }
        // This is identity retention for an existing return/receipt, never an
        // action permit. The generation is retired only at a real session edge.
        public bool HasRetainedSession { get { return sessionActive; } }
        public Exception CleanupFailure { get; private set; }
        public void AddFeature(IRuntimeFeature feature)
        {
            if (observed) throw new InvalidOperationException("Runtime composition is frozen before its first observation.");
            if (feature == null) throw new ArgumentNullException(nameof(feature));
            if (features.Contains(feature)) throw new ArgumentException("Feature already attached.");
            features.Add(feature);
        }

        public bool IsSessionActive
        {
            get { return sessionActive && !HasFailed; }
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
                FailClosed();
                // Failure to observe is not evidence of a session edge. Do not
                // pump cleanup or retire identity this tick. Feature.FailClosed
                // still revokes intent and returns under its own exact contract.
                return;
            }

            if (HasFailed)
            {
                // A common failure is terminal for this composition, including
                // before its first session and after a later world replacement.
                // Observe only identity edges and explicitly owned finite returns.
                if (!active || sessionActive && !ReferenceEquals(identity, sessionIdentity)) InvalidateSession();
                if (sessionActive)
                    foreach (IRuntimeFeature feature in features)
                    {
                        var cleanup = feature as IRuntimeFailureCleanup;
                        if (cleanup == null || failedCleanup.Contains(feature)) continue;
                        try { cleanup.UpdateFailureCleanup(updateTick); }
                        catch (Exception exception)
                        {
                            failedCleanup.Add(feature);
                            if (CleanupFailure == null) CleanupFailure = exception;
                        }
                    }
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

        public void FailClosed()
        {
            if (HasFailed) return;
            // Withdraw admission before callbacks: a reentrant command cannot
            // start work while another owner is still returning its native loan.
            HasFailed = true;
            observed = true;
            foreach (IRuntimeFeature feature in features) FailFeatureClosed(feature);
        }

        private void FailFeatureClosed(IRuntimeFeature feature)
        {
            try
            {
                feature.FailClosed();
            }
            catch (Exception exception)
            {
                // Other owners still receive cleanup. Preserve the first failure
                // rather than describing an incomplete cleanup as successful.
                if (CleanupFailure == null) CleanupFailure = exception;
            }
        }
    }
}
