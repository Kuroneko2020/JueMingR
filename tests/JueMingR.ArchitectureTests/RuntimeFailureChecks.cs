using System;
using System.Collections.Generic;
using JueMingR.Platform.Runtime;

namespace JueMingR.ArchitectureTests
{
    internal static class RuntimeFailureChecks
    {
        internal static void Check(List<string> failures)
        {
            Run(failures, "terminal shared runtime", Terminal);
            Run(failures, "unobservable session retains existing cleanup", UnknownIdentity);
            Run(failures, "independent failure cleanup preserves errors", IndependentCleanup);
            Run(failures, "local feature failure stays local", LocalFailure);
            Run(failures, "pre-session failure cannot activate", BeforeSession);
        }
        private static void Terminal()
        {
            var probe = new Probe(); var feature = new Feature(); var runtime = new SingleFeatureRuntime(probe, feature);
            runtime.Update(0); long generation = runtime.Generation;
            probe.Throw = true; runtime.Update(1); probe.Throw = false;
            runtime.Update(2); probe.SessionIdentity = new object(); runtime.Update(3);
            if (runtime.IsSessionActive || runtime.Generation != generation || feature.Starts != 1)
                throw new InvalidOperationException("core observation failure must reject later session activation, including a different world");
            if (feature.Ends != 1 || feature.Failures == 0) throw new InvalidOperationException("core failure preserves exact session-end and feature cleanup responsibilities");
        }
        private static void UnknownIdentity()
        {
            var probe = new Probe(); var feature = new Feature(); var runtime = new SingleFeatureRuntime(probe, feature);
            runtime.Update(0); long generation = runtime.Generation;
            probe.Throw = true; runtime.Update(1); runtime.Update(2);
            Require(runtime.HasFailed && !runtime.IsSessionActive && runtime.HasRetainedSession && feature.Ends == 0 && feature.Cleanups == 0,
                "an observation exception revokes admission but proves neither session end nor permission to return");
            probe.Throw = false; runtime.Update(3);
            Require(feature.Cleanups == 1 && feature.Updates == 1 && runtime.Generation == generation,
                "same identity resumes only existing cleanup, never normal business Update");
            probe.SessionIdentity = new object(); runtime.Update(4); runtime.Update(5);
            Require(feature.Ends == 1 && feature.Cleanups == 1 && feature.Starts == 1 && !runtime.HasRetainedSession,
                "confirmed different identity retires the old responsibility once and cannot start a successor");
        }
        private static void IndependentCleanup()
        {
            var first = new Feature { ThrowFail = true, ThrowCleanup = true }; var other = new Feature();
            var runtime = new SingleFeatureRuntime(new Probe(), first); runtime.AddFeature(other); runtime.Update(0);
            runtime.FailClosed(); runtime.FailClosed();
            Require(first.Failures == 1 && other.Failures == 1 && runtime.CleanupFailure != null,
                "one throwing fail callback cannot skip a later owner or hide incomplete cleanup");
            runtime.Update(1); runtime.Update(2);
            Require(first.Cleanups == 1 && other.Cleanups == 2 && first.Updates == 1 && other.Updates == 1,
                "throwing return work is not blindly retried while independent existing cleanup still runs");
        }
        private static void LocalFailure()
        {
            var first = new Feature { ThrowUpdate = true }; var other = new Feature();
            var runtime = new SingleFeatureRuntime(new Probe(), first); runtime.AddFeature(other); runtime.Update(0); runtime.Update(1);
            Require(!runtime.HasFailed && runtime.IsSessionActive && first.Failures == 1 && other.Updates == 2,
                "feature-owned failure cannot become a common terminal failure or stop another feature");
        }
        private static void BeforeSession()
        {
            var probe = new Probe(); var feature = new Feature(); var runtime = new SingleFeatureRuntime(probe, feature);
            runtime.FailClosed(); runtime.Update(0); probe.SessionIdentity = new object(); runtime.Update(1);
            Require(!runtime.IsSessionActive && runtime.Generation == 0 && feature.Starts == 0 && feature.Ends == 0 && feature.Cleanups == 0,
                "failure before first observation creates no session or invented cleanup baseline");
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        private static void Run(List<string> failures, string name, Action check)
        {
            try
            {
                check();
            }
            catch (Exception e) { failures.Add(name + ": " + e.Message); }
        }
        private sealed class Probe : IGameSessionIdentityProbe
        {
            internal bool Throw;
            public bool IsSessionActive { get { if (Throw) throw new InvalidOperationException("controlled core observation fault"); return true; } }
            public object SessionIdentity { get; set; } = new object();
        }
        private sealed class Feature : IRuntimeFeature, IRuntimeFailureCleanup
        {
            internal int Starts, Ends, Failures, Updates, Cleanups;
            internal bool ThrowFail, ThrowUpdate, ThrowCleanup;
            private bool failed;
            public bool Enabled { get { return !failed; } }
            public void OnSessionStarted() { Starts++; }
            public void OnSessionEnded() { Ends++; }
            public void Update(ulong tick) { if (failed) return; Updates++; if (ThrowUpdate) throw new InvalidOperationException("controlled local update failure"); }
            public void FailClosed() { failed = true; Failures++; if (ThrowFail) throw new InvalidOperationException("controlled failure cleanup fault"); }
            public void UpdateFailureCleanup(ulong tick) { Cleanups++; if (ThrowCleanup) throw new InvalidOperationException("controlled return fault"); }
        }
    }
}
