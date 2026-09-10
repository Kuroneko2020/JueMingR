using System;
using System.Collections.Generic;
using JueMingR.Platform.Runtime;

namespace JueMingR.ArchitectureTests
{
    internal static class ItemRuntimeChecks
    {
        internal static void Check(IList<string> failures)
        {
            try
            {
                var probe = new Probe(); var first = new Feature(); var second = new Feature();
                var runtime = new SingleFeatureRuntime(probe, first); runtime.AddFeature(second);
                runtime.Update(0);
                Require(runtime.Generation == 1 && first.Starts == 1 && second.Starts == 1, "one shared generation and both initial edges");
                probe.SessionIdentity = new object(); runtime.Update(1);
                Require(runtime.Generation == 2 && first.Ends == 1 && second.Ends == 1 && second.Starts == 2, "identity change without an inactive tick must end old session first");
                first.ThrowUpdate = true; runtime.Update(2);
                Require(first.Failures == 1 && second.Updates == 3, "one feature failure cannot disable unrelated features");
                runtime.InvalidateSession(); probe.IsSessionActive = false; runtime.Update(3);
                Require(first.Ends == 2 && second.Ends == 2, "explicit native boundary ends exactly once");
                probe.IsSessionActive = true; runtime.Update(4);
                Require(runtime.Generation == 3 && second.Starts == 3, "return requires fresh generation");
            }
            catch (Exception e) { failures.Add("shared item session: " + e.Message); }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private sealed class Probe : IGameSessionIdentityProbe
        { public bool IsSessionActive { get; set; } = true; public object SessionIdentity { get; set; } = new object(); }
        private sealed class Feature : IRuntimeFeature
        {
            public bool Enabled { get { return true; } }
            internal int Starts, Ends, Updates, Failures;
            internal bool ThrowUpdate;
            public void OnSessionStarted() { Starts++; }
            public void OnSessionEnded() { Ends++; }
            public void Update(ulong tick) { Updates++; if (ThrowUpdate) throw new InvalidOperationException(); }
            public void FailClosed() { Failures++; }
        }
    }
}
