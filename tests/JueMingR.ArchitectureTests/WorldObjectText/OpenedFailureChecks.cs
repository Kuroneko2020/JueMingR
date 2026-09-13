using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using JueMingR.Features.WorldObjectText;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Settings;
using JueMingR.Platform.WorldObjectText;

namespace JueMingR.ArchitectureTests
{
    internal static class OpenedFailureChecks
    {
        internal static void Run()
        {
            foreach (string mode in new[] { "future", "corrupt", "read-failed", "unconfirmed" }) ProtectOriginal(mode);
            PairNoticeIsolation();
        }
        private static void ProtectOriginal(string mode)
        {
            string root = Root(), pair = new string('a', 64), path = Path.Combine(root, "record.json");
            byte[] valid = new OpenedPositionCodec().Encode(pair, new OpenedPositionIndex(new[] { WorldObject.PositionKey(2, 3) }));
            byte[] original = mode == "future" ? Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(valid).Replace("\"version\":1", "\"version\":9")) : mode == "corrupt" ? Encoding.UTF8.GetBytes("{broken") : valid;
            byte[] backup = { 1, 88, 255 }; File.WriteAllBytes(path, original); File.WriteAllBytes(path + ".bak", backup);
            Storage storage = null; var owner = new OpenedPositionHistory(key => storage = new Storage(new AtomicFileDocument(path, OpenedPositionCodec.MaximumBytes, true), mode));
            try
            {
                owner.BeginSession(1); owner.Opened(5, 6); owner.UsePair(pair);
                Until(() => { owner.Poll(); return owner.Loaded && owner.Error != null; });
                Require(owner.Status == (mode == "future" ? PreferenceStatus.UnsupportedVersion : mode == "corrupt" ? PreferenceStatus.Invalid : PreferenceStatus.IoFailure), "owner distinguishes protected " + mode);
                Require(owner.Contains(WorldObject.PositionKey(5, 6)) && owner.CommitUnconfirmed == (mode == "unconfirmed"), "memory fact and uncertain commit stay distinct");
                for (int i = 0; i < 10; i++) { owner.Opened(5, 6); owner.Poll(); }
                Require(owner.Stop(5000), "protected owner stops its worker");
                Require(storage.Writes == (mode == "unconfirmed" ? 1 : 0), "protected source cannot turn into an empty/retried write");
                Require(File.ReadAllBytes(path).SequenceEqual(original) && File.ReadAllBytes(path + ".bak").SequenceEqual(backup), "real original and prior backup bytes survive " + mode);
            }
            finally { owner.Stop(5000); }
        }
        private static void PairNoticeIsolation()
        {
            string root = Root(), a = new string('b', 64), b = new string('c', 64); Storage first = null, second = null;
            var owner = new OpenedPositionHistory(key => key == a ? first = new Storage(new AtomicFileDocument(Path.Combine(root, a + ".json"), OpenedPositionCodec.MaximumBytes, true), "A-failed") : second = new Storage(new AtomicFileDocument(Path.Combine(root, b + ".json"), OpenedPositionCodec.MaximumBytes, true), "B-retry"));
            try
            {
                owner.BeginSession(1); owner.UsePair(a); owner.Opened(10, 10);
                Until(() => { owner.Poll(); return first != null && first.Writes >= 3 && owner.Status == PreferenceStatus.IoFailure; });
                owner.EndSession(); owner.BeginSession(2); owner.UsePair(b); owner.Opened(20, 20);
                Until(() => { owner.Poll(); return second != null && second.Writes >= 2 && owner.Status == PreferenceStatus.Saved; });
                Require(owner.TakeBackgroundFailure() == "test-A-failed", "B recovery cannot erase the unconsumed failed A notice");
                Require(owner.TakeBackgroundFailure() == null && owner.Error == null, "repeated unchanged failure is not republished and active B is healthy");
                Require(!File.Exists(Path.Combine(root, a + ".json")) && new OpenedPositionCodec().Decode(File.ReadAllBytes(Path.Combine(root, b + ".json")), b).Count == 1, "failed old pair never contaminates new pair file");
            }
            finally { owner.Stop(5000); }
        }
        private static string Root() { string value = Path.Combine(Path.GetTempPath(), "JueMingR-opened-failure-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(value); return value; }
        private static void Until(Func<bool> condition) { var time = System.Diagnostics.Stopwatch.StartNew(); while (!condition()) { if (time.ElapsedMilliseconds > 5000) throw new TimeoutException("opened failure case"); Thread.Sleep(1); } }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private sealed class Storage : IPreferenceStorage
        {
            private readonly IPreferenceStorage inner; private readonly string mode; internal int Writes;
            internal Storage(IPreferenceStorage inner, string mode) { this.inner = inner; this.mode = mode; }
            public PreferenceReadResult Read() { return mode == "read-failed" ? new PreferenceReadResult(PreferenceReadStatus.IoFailure, null, null, "test-read-failed") : inner.Read(); }
            public PreferenceWriteResult Write(string identity, byte[] bytes)
            {
                int count = Interlocked.Increment(ref Writes);
                if (mode == "unconfirmed") return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, identity, "test-unconfirmed", true, true);
                if (mode == "A-failed" || mode == "B-retry" && count == 1) return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, identity, "test-" + mode);
                return inner.Write(identity, bytes);
            }
            public void Dispose() { inner.Dispose(); }
        }
    }
}
