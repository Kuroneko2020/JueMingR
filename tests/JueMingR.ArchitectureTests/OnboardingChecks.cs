using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using JueMingR.Features.Onboarding;
using JueMingR.Infrastructure.Storage;
namespace JueMingR.ArchitectureTests
{
    internal static class OnboardingChecks
    {
        internal static void Check(List<string> failures)
        {
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-onboarding-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var states = new List<OnboardingState>();
            try
            {
                string key = new string('a', 64), other = new string('b', 64);
                Func<OnboardingState> create = () => { var state = new OnboardingState(k => new AtomicFileDocument(Path.Combine(root, k + ".json"), 1024, true)); states.Add(state); return state; };
                var owner = create(); owner.Begin(1); owner.Resolve(key); Wait(owner, () => owner.Ready);
                owner.Poll(); Require(!File.Exists(Path.Combine(root, key + ".json")), "Preparing never claims a presentation.");
                owner.Presented(1, 100); Require(owner.Shown && !owner.Saved, "Actual Draw suppresses immediately but does not claim a durable save.");
                Wait(owner, () => owner.Saved); Wait(owner, () => owner.WorkerCount == 0);
                owner.Begin(2); owner.Resolve(key); Require(!owner.Ready, "Same character, another world is suppressed.");
                var reload = create(); reload.Begin(3); reload.Resolve(key); Wait(reload, () => reload.Saved); Require(!reload.Ready, "Restart preserves the marker.");
                reload.DetachIdentity(); Require(!reload.Ready, "A known seen role stays suppressed during temporary identity loss.");
                reload.Resolve(key); Require(!reload.Ready, "Restoring the same role never reopens its prompt.");
                reload.Begin(30); Require(reload.Ready, "A different admission can still show an unknown-role prompt.");
                owner.Begin(4); owner.Presented(4, 0); owner.Pause(); owner.Presented(4, 100000); Require(owner.Opacity == 1, "Hidden time does not consume the reading period.");
                owner.Resolve(other); Wait(owner, () => owner.Saved); Require(owner.Shown, "Unknown to reliable preserves the same-session receipt.");
                owner.Begin(5); owner.Presented(4, 101000); Require(!owner.Shown, "Old session receipt cannot mark a new role.");
                string future = new string('c', 64), path = Path.Combine(root, future + ".json");
                byte[] bytes = Encoding.UTF8.GetBytes("{\"format\":\"JueMingR.Onboarding\",\"version\":9,\"character\":\"" + future + "\",\"seen\":true}"); File.WriteAllBytes(path, bytes);
                owner.Resolve(future); Wait(owner, () => owner.Ready); owner.Presented(5, 0); for (int i = 0; i < 1000; i++) owner.Poll();
                Require(!owner.Saved && Convert.ToBase64String(File.ReadAllBytes(path)) == Convert.ToBase64String(bytes), "Future format stays protected after actual presentation.");
                Require(owner.Failure.Contains("不支持的版本") && !owner.Failure.Contains("下次启动"), "Future format is distinguishable and cannot be repaired by same-version restart.");
                var many = create();
                for (int i = 1; i <= OnboardingState.MaximumEntries + 3; i++)
                {
                    many.Begin(100 + i); many.Resolve(i.ToString("x64")); Wait(many, () => many.Ready);
                    many.Presented(100 + i, 0); Wait(many, () => many.Saved && many.WorkerCount == 0);
                }
                Require(many.EntryCount <= OnboardingState.MaximumEntries && many.Saved, "Completed cache entries are evicted without disabling later characters.");
                OnboardingFailureChecks.Run();
                Console.WriteLine("PASS: onboarding actual presentation, isolated durable reload, cross-world identity, unknown admission, future protection.");
            }
            catch (Exception error) { failures.Add("Onboarding: " + error.Message); }
            finally { foreach (var state in states) state.Stop(3000); Directory.Delete(root, true); }
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Wait(OnboardingState owner, Func<bool> condition)
        { var clock = Stopwatch.StartNew(); while (!condition() && clock.ElapsedMilliseconds < 3000) { owner.Poll(); Thread.Sleep(1); } Require(condition(), "Bounded worker condition timed out."); }
    }
}
