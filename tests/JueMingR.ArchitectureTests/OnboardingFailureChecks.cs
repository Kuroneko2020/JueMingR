using System;
using System.Diagnostics;
using System.Threading;
using JueMingR.Features.Onboarding;
using JueMingR.Platform.Settings;
namespace JueMingR.ArchitectureTests
{
    internal static class OnboardingFailureChecks
    {
        internal static void Run()
        {
            var storage = new Storage(); var owner = new OnboardingState(key => storage);
            try
            {
                owner.Begin(1); owner.Resolve(new string('d', 64)); Wait(owner, () => owner.Ready);
                owner.DetachIdentity(); owner.Presented(1, 0); Wait(owner, () => owner.WorkerCount == 0);
                Require(storage.Writes == 0 && owner.Shown, "Identity loss detaches prior file; unknown Draw never marks it.");
            }
            finally { owner.Stop(3000); }
            owner = new OnboardingState(key => new Storage());
            try
            {
                owner.Begin(4); owner.Presented(4, 0);
                for (int time = 100; time <= 3000; time += 100) owner.Presented(4, time);
                Require(owner.Ready && owner.Opacity == 1, "Three visible seconds retain full opacity.");
                owner.Pause(); owner.Presented(4, 100000); Require(owner.Opacity == 1, "Obscured time never consumes fade.");
                owner.Presented(4, 100200); owner.Presented(4, 100300);
                Require(Math.Abs(owner.Opacity - .5f) < .001f, "The next 600 visible milliseconds fade linearly.");
                owner.Presented(4, 100500); owner.Presented(4, 100600); Require(!owner.Ready && owner.Opacity == 0, "Completed presentation stops drawing.");
            }
            finally { owner.Stop(3000); }
            using (var unblock = new ManualResetEventSlim())
            {
                var held = new System.Collections.Generic.List<Storage>();
                owner = new OnboardingState(key => { var value = new Storage { Unblock = unblock }; held.Add(value); return value; });
                try
                {
                    for (int i = 1; i <= 5; i++) { owner.Begin(i); owner.Resolve(i.ToString("x64")); }
                    Require(owner.WorkerCount == 4 && held.Count == 4 && owner.Ready, "Slow retired reads remain owned and cap concurrent workers at four.");
                    unblock.Set(); Wait(owner, () => owner.WorkerCount <= 1);
                    Require(held.TrueForAll(value => value.Writes == 0), "Retired unread roles never acquire a presentation receipt.");
                }
                finally { unblock.Set(); owner.Stop(3000); }
            }
            storage = new Storage(); owner = new OnboardingState(key => storage);
            try
            {
                owner.Begin(6); owner.Presented(6, 0); owner.Resolve(new string('a', 64));
                owner.Stop(3000);
                Require(storage.Writes == 1 && storage.Disposed, "Accepted unknown-role Draw survives late identity/read and immediate bounded exit.");
            }
            finally { owner.Stop(3000); }
            storage = new Storage { FailWrite = true, Unconfirmed = true }; owner = new OnboardingState(key => storage);
            try
            {
                owner.Begin(7); owner.Resolve(new string('b', 64)); Wait(owner, () => owner.Ready); owner.Presented(7, 0); Wait(owner, () => owner.WorkerCount == 0);
                Require(!owner.Saved && owner.Failure.Contains("无法确认") && !owner.Failure.Contains("原文件已保留"), "Unconfirmed commit is never described as preserved original or success.");
            }
            finally { owner.Stop(3000); }
            storage = new Storage { FailWrite = true }; owner = new OnboardingState(key => storage);
            try
            {
                owner.Begin(2); owner.Resolve(new string('e', 64)); Wait(owner, () => owner.Ready); owner.Presented(2, 0); Wait(owner, () => owner.WorkerCount == 0);
                for (int i = 0; i < 2000; i++) owner.Poll();
                Require(storage.Reads == 1 && storage.Writes == 1 && owner.Shown && !owner.Saved && owner.Failure != null, "Write failure is not success and has no automatic retries.");
                int feedback = 0; owner.TakeFeedback(_ => feedback++); owner.TakeFeedback(_ => feedback++); Require(feedback == 1, "One failure alert remains available exactly once.");
                owner.Begin(3); owner.Resolve(new string('e', 64)); Require(!owner.Ready && storage.Reads == 1, "Failed presented role remains suppressed across worlds.");
            }
            finally { owner.Stop(3000); }
            var codec = new OnboardingMarkerCodec(new string('f', 64));
            foreach (byte[] malformed in new[] { new byte[] { 255 }, System.Text.Encoding.UTF8.GetBytes("{}"), new OnboardingMarkerCodec(new string('a', 64)).Encode(true) })
            { bool rejected = false; try { codec.Decode(malformed); } catch (PreferenceFormatException) { rejected = true; } Require(rejected, "Malformed UTF8/shape/wrong character never becomes writable default."); }
            foreach (string error in new[] { "first-create-conflict", "external-document-change", "document-write-access-denied" })
            {
                storage = new Storage { FailWrite = true, WriteError = error }; owner = new OnboardingState(key => storage);
                try
                {
                    owner.Begin(8); owner.Resolve(new string('c', 64)); Wait(owner, () => owner.Ready); owner.Presented(8, 0); Wait(owner, () => owner.WorkerCount == 0);
                    Require(owner.Failure.Contains(error.EndsWith("denied", StringComparison.Ordinal) ? "权限" : "其它操作更改"), "Known write permission/conflict causes remain distinguishable.");
                }
                finally { owner.Stop(3000); }
            }
            storage = new Storage { Contents = System.Text.Encoding.UTF8.GetBytes("{}") }; owner = new OnboardingState(key => storage);
            try { owner.Begin(9); owner.Resolve(new string('d', 64)); Wait(owner, () => owner.Ready); owner.Presented(9, 0); Wait(owner, () => owner.WorkerCount == 0); Require(owner.Failure.Contains("格式损坏") && storage.Writes == 0, "Corrupt marker remains protected with an actionable cause."); }
            finally { owner.Stop(3000); }
        }
        private static void Require(bool condition, string text) { if (!condition) throw new InvalidOperationException(text); }
        private static void Wait(OnboardingState owner, Func<bool> condition)
        { var clock = Stopwatch.StartNew(); while (!condition() && clock.ElapsedMilliseconds < 3000) { owner.Poll(); Thread.Sleep(1); } Require(condition(), "Failure worker timed out."); }
        private sealed class Storage : IPreferenceStorage
        {
            internal int Reads, Writes; internal bool FailWrite, Unconfirmed, Disposed;
            internal ManualResetEventSlim Unblock;
            internal byte[] Contents; internal string WriteError;
            public PreferenceReadResult Read() { Interlocked.Increment(ref Reads); Unblock?.Wait(); return new PreferenceReadResult(Contents == null ? PreferenceReadStatus.Missing : PreferenceReadStatus.Loaded, Contents, null, null); }
            public PreferenceWriteResult Write(string expected, byte[] bytes)
            { Interlocked.Increment(ref Writes); return new PreferenceWriteResult(FailWrite ? PreferenceWriteStatus.IoFailure : PreferenceWriteStatus.Saved, "saved", FailWrite ? WriteError ?? "write-failed" : null, Unconfirmed, Unconfirmed); }
            public void Dispose() { Disposed = true; }
        }
    }
}
