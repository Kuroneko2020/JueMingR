using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.IO;
using System.Threading;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Settings;
using JueMingR.Features.WorldObjectText;
using JueMingR.Platform.WorldObjectText;
using JueMingR.Platform.WorldTargets;

namespace JueMingR.ArchitectureTests
{
    internal static class OpenedPositionChecks
    {
        internal static void Check(IList<string> failures)
        {
            try
            {
                Type codecType = typeof(WorldObjectResolver).Assembly.GetType("JueMingR.Features.WorldObjectText.OpenedPositionCodec");
                Require(codecType != null, "strict opened-position codec is not implemented");
                var codec = Activator.CreateInstance(codecType); string pair = new string('a', 64);
                string json = "{\"format\":\"JueMingR.OpenedContainers\",\"version\":1,\"pair\":\"" + pair + "\",\"positions\":[{\"x\":10,\"y\":20},{\"x\":9000,\"y\":1000}]}";
                MethodInfo decode = codecType.GetMethod("Decode");
                object index = decode.Invoke(codec, new object[] { Encoding.UTF8.GetBytes(json), pair });
                Require((int)index.GetType().GetProperty("Count").GetValue(index) == 2, "coordinates survive strict read");
                var nearby = (IEnumerable)index.GetType().GetMethod("Nearby").Invoke(index, new object[] { new WorldTargetView(0, 0, 100, 100, 1) });
                int count = 0; foreach (long position in nearby) { count++; Require(position == WorldObject.PositionKey(10, 20), "far bucket never enters nearby query"); }
                Require(count == 1, "nearby query returns only applicable position");
                foreach (string invalid in new[] { json.Replace("\"version\":1", "\"version\":2"), json.Replace("\"y\":20", "\"y\":-1"), json.Replace("\"x\":10", "\"name\":\"old\",\"x\":10"), json.Replace(pair, new string('b', 64)), json.Replace("\"x\":9000,\"y\":1000", "\"x\":10,\"y\":20") })
                { bool rejected = false; try { decode.Invoke(codec, new object[] { Encoding.UTF8.GetBytes(invalid), pair }); } catch (TargetInvocationException e) when (e.InnerException is PreferenceFormatException) { rejected = true; } Require(rejected, "corrupt/future/mismatched/duplicate file is protected"); }
                byte[] encoded = (byte[])codecType.GetMethod("Encode").Invoke(codec, new[] { (object)pair, index });
                Require(Encoding.UTF8.GetString(encoded).IndexOf("name", StringComparison.Ordinal) < 0, "position history never persists a name truth");
                DelayedRealFile();
                StopDuringWrite();
                DeferredReentry();
                DeferredRetryRetiresOverlay();
                CapacityNoticeOnce();
                OpenedFailureChecks.Run();
                BoundedQuery();
            }
            catch (Exception e) { failures.Add("Opened positions: " + (e.InnerException ?? e).Message); }
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void BoundedQuery()
        {
            var keys = new List<long>(); for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) keys.Add(WorldObject.PositionKey(x, y));
            var query = new OpenedPositionIndex(keys).Query(new WorldTargetView(63, 63, 1, 1, 1));
            int steps = 0, found = 0;
            while (true)
            {
                var step = query.MoveNext(32); steps++;
                Require(query.WorkUsed <= 32, "out-of-view same-bucket entries consume the raw-visit budget");
                if (step == OpenedQueryStep.Position) { found++; Require(query.Current == WorldObject.PositionKey(63, 63), "only the final matching position is yielded"); }
                if (step == OpenedQueryStep.End) break;
            }
            Require(steps > 100 && found == 1, "the long skipped prefix is resumable rather than hidden in one MoveNext");
        }
        private static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name).Invoke(target, args); }
        private static void DelayedRealFile()
        {
            Type ownerType = typeof(WorldObjectResolver).Assembly.GetType("JueMingR.Features.WorldObjectText.OpenedPositionHistory");
            Require(ownerType != null, "asynchronous opened-position owner is not implemented");
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-opened-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); string first = new string('a', 64), second = new string('b', 64);
            var codec = new OpenedPositionCodec();
            File.WriteAllBytes(Path.Combine(root, first + ".json"), codec.Encode(first,
                new OpenedPositionIndex(new[] { WorldObject.PositionKey(10, 20), WorldObject.PositionKey(9000, 1000) })));
            var entered = new ManualResetEventSlim(); var release = new ManualResetEventSlim();
            Func<string, IPreferenceStorage> factory = key => key == first
                ? (IPreferenceStorage)new SlowRead(new AtomicFileDocument(Path.Combine(root, key + ".json"), OpenedPositionCodec.MaximumBytes, true), entered, release)
                : new AtomicFileDocument(Path.Combine(root, key + ".json"), OpenedPositionCodec.MaximumBytes, true);
            object owner = Activator.CreateInstance(ownerType, new object[] { factory }); bool stopped = false;
            try
            {
                Call(owner, "BeginSession", 1L);
                Require((bool)Call(owner, "Opened", 30, 40), "new local fact is accepted before identity/load");
                Call(owner, "UsePair", first); Call(owner, "Poll");
                Require(entered.Wait(3000), "real background read entered");
                Require((bool)Call(owner, "Opened", 50, 60), "load-in-flight fact is independently accepted");
                Call(owner, "EndSession"); Call(owner, "BeginSession", 2L); Call(owner, "UsePair", second);
                release.Set();
                var time = System.Diagnostics.Stopwatch.StartNew();
                while (!(bool)ownerType.GetProperty("Loaded").GetValue(owner) && time.ElapsedMilliseconds < 5000) { Call(owner, "Poll"); Thread.Sleep(1); }
                Require((bool)ownerType.GetProperty("Loaded").GetValue(owner), "new pair load completes");
                Require(!(bool)Call(owner, "Contains", WorldObject.PositionKey(30, 40)) && !(bool)Call(owner, "Contains", WorldObject.PositionKey(10, 20)), "old completion never populates new world");
                stopped = (bool)Call(owner, "Stop", 5000); Require(stopped, "bounded exit completes accepted old pair work");
                var actual = codec.Decode(File.ReadAllBytes(Path.Combine(root, first + ".json")), first);
                Require(actual.Count == 4 && actual.Contains(WorldObject.PositionKey(50, 60)) && actual.Contains(WorldObject.PositionKey(30, 40)), "old real file preserves loaded and both concurrent local positions");
                Require(File.Exists(Path.Combine(root, first + ".json.bak")), "real atomic replacement retains previous file");
                Require(!File.Exists(Path.Combine(root, second + ".json")), "reading missing history alone never creates an empty document");
            }
            finally
            {
                release.Set(); if (!stopped) stopped = (bool)Call(owner, "Stop", 5000);
                if (stopped) { entered.Dispose(); release.Dispose(); }
                // Only this randomly named test root is eligible for cleanup.
                string resolved = Path.GetFullPath(root), temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (stopped && resolved.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("JueMingR-opened-", StringComparison.Ordinal)) Directory.Delete(resolved, true);
            }
        }
        private sealed class SlowRead : IPreferenceStorage
        {
            private readonly IPreferenceStorage inner; private readonly ManualResetEventSlim entered, release;
            internal SlowRead(IPreferenceStorage inner, ManualResetEventSlim entered, ManualResetEventSlim release) { this.inner = inner; this.entered = entered; this.release = release; }
            public PreferenceReadResult Read() { entered.Set(); if (!release.Wait(5000)) throw new TimeoutException("test read gate"); return inner.Read(); }
            public PreferenceWriteResult Write(string identity, byte[] bytes) { return inner.Write(identity, bytes); }
            public void Dispose() { inner.Dispose(); }
        }
        private static void StopDuringWrite()
        {
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-opened-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string key = new string('c', 64), path = Path.Combine(root, key + ".json");
            var entered = new ManualResetEventSlim(); var release = new ManualResetEventSlim();
            var owner = new OpenedPositionHistory(pair => new SlowWrite(new AtomicFileDocument(path, OpenedPositionCodec.MaximumBytes, true), entered, release));
            bool stopped = false; Thread stopThread = null;
            try
            {
                owner.BeginSession(1); owner.UsePair(key); Until(() => { owner.Poll(); return owner.Loaded; });
                owner.Opened(10, 10); owner.Poll(); Require(entered.Wait(3000), "first real commit is in flight");
                owner.Opened(20, 20); owner.Poll();
                Require(owner.Status == PreferenceStatus.Pending, "an in-flight commit and its later delta are pending");
                stopThread = new Thread(() => stopped = owner.Stop(5000)); stopThread.Start();
                // The Stop thread is blocked while the OS-write surrogate holds
                // its gate. No timing sleep decides which revision is accepted.
                Until(() => (stopThread.ThreadState & ThreadState.WaitSleepJoin) != 0);
                release.Set(); Require(stopThread.Join(5000) && stopped, "exit releases worker ownership");
                var result = new OpenedPositionCodec().Decode(File.ReadAllBytes(path), key);
                Require(result.Count == 2 && result.Contains(WorldObject.PositionKey(20, 20)), "exit must drain B accepted while A was writing");
            }
            finally
            {
                release.Set(); if (stopThread != null && stopThread.IsAlive) stopThread.Join(5000);
                if (!stopped) stopped = owner.Stop(5000);
                if (stopped)
                {
                    entered.Dispose(); release.Dispose();
                    string resolved = Path.GetFullPath(root), temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (resolved.StartsWith(temp, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("JueMingR-opened-", StringComparison.Ordinal)) Directory.Delete(resolved, true);
                }
            }
        }
        private static void Until(Func<bool> condition)
        { var clock = System.Diagnostics.Stopwatch.StartNew(); while (!condition()) { if (clock.ElapsedMilliseconds > 5000) throw new TimeoutException("opened test boundary"); Thread.Sleep(1); } }
        private static void DeferredReentry()
        {
            var entered = new ManualResetEventSlim(); var release = new ManualResetEventSlim();
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-opened-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string pair = new string('d', 64); bool stopped = false;
            var owner = new OpenedPositionHistory(key => new SlowRead(new AtomicFileDocument(Path.Combine(root, key + ".json"), OpenedPositionCodec.MaximumBytes, true), entered, release));
            try
            {
                owner.BeginSession(1);
                for (int i = 0; i < 65; i++) owner.Opened(i, 10);
                owner.UsePair(pair); Require(entered.Wait(3000), "late identity load is held");
                owner.EndSession(); owner.BeginSession(2); owner.UsePair(pair);
                Require(owner.Contains(WorldObject.PositionKey(64, 10)), "same-pair reentry sees the transferred, not-yet-replayed tail");
                int count = 0; foreach (long key in owner.Nearby(new WorldTargetView(0, 0, 100, 100, 1))) count++;
                Require(count == 65, "transferred spatial facts are visible exactly once before disk completion");
                release.Set(); stopped = owner.Stop(5000); Require(stopped, "deferred pair drains");
                Require(new OpenedPositionCodec().Decode(File.ReadAllBytes(Path.Combine(root, pair + ".json")), pair).Count == 65, "all late-identity facts reach the real file");
            }
            finally
            {
                release.Set(); if (!stopped) stopped = owner.Stop(5000);
                string resolved = Path.GetFullPath(root), temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (stopped && resolved.StartsWith(temp, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("JueMingR-opened-", StringComparison.Ordinal))
                { entered.Dispose(); release.Dispose(); Directory.Delete(resolved, true); }
            }
        }
        private static void DeferredRetryRetiresOverlay()
        {
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-opened-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string pair = new string('e', 64), path = Path.Combine(root, pair + ".json");
            var entered = new ManualResetEventSlim(); var release = new ManualResetEventSlim(); RetryWrite storage = null;
            var owner = new OpenedPositionHistory(key => storage = new RetryWrite(new SlowRead(new AtomicFileDocument(path, OpenedPositionCodec.MaximumBytes, true), entered, release), pair));
            try
            {
                owner.BeginSession(1); for (int i = 0; i < 65; i++) owner.Opened(i, 11);
                owner.UsePair(pair); Require(entered.Wait(3000), "deferred retry read gate"); owner.EndSession(); owner.BeginSession(2); owner.UsePair(pair);
                release.Set(); Until(() => { owner.Poll(); return owner.Status == PreferenceStatus.Saved; });
                Until(() => { owner.Poll(); return storage.Failed && owner.Status == PreferenceStatus.Saved; });
                Require(storage.Writes >= 2 && owner.Error == null && owner.TakeBackgroundFailure() == null, "known precommit failure retries and clears only its recoverable notice");
                var session = typeof(OpenedPositionHistory).GetField("current", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
                var lease = session.GetType().GetField("Lease", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
                Require(((Array)lease.GetType().GetField("Overlays", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(lease)).Length == 0, "successful retry retires the frozen overlay, not only its pending keys");
                Require(new OpenedPositionCodec().Decode(File.ReadAllBytes(path), pair).Count == 65, "retry commits every deferred coordinate to a real file");
            }
            finally { release.Set(); owner.Stop(5000); entered.Dispose(); release.Dispose(); }
        }
        private sealed class RetryWrite : IPreferenceStorage
        {
            private readonly IPreferenceStorage inner; private readonly string pair; internal int Writes; internal volatile bool Failed;
            internal RetryWrite(IPreferenceStorage inner, string pair) { this.inner = inner; this.pair = pair; }
            public PreferenceReadResult Read() { return inner.Read(); }
            public PreferenceWriteResult Write(string identity, byte[] bytes)
            { Interlocked.Increment(ref Writes); if (!Failed && new OpenedPositionCodec().Decode(bytes, pair).Count == 65) { Failed = true; return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, identity, "test-known-precommit"); } return inner.Write(identity, bytes); }
            public void Dispose() { inner.Dispose(); }
        }
        private static void CapacityNoticeOnce()
        {
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-opened-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string pair = new string('f', 64); var entered = new ManualResetEventSlim(); var release = new ManualResetEventSlim();
            var owner = new OpenedPositionHistory(key => new SlowRead(new AtomicFileDocument(Path.Combine(root, pair + ".json"), OpenedPositionCodec.MaximumBytes, true), entered, release));
            var store = typeof(OpenedPositionHistory).GetField("store", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
            var count = store.GetType().GetField("queuedUnits", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                owner.BeginSession(1); owner.UsePair(pair); Require(entered.Wait(3000), "capacity test holds worker");
                // Reach the exact admission boundary without allocating a million
                // unrelated fixture coordinates; only the game thread can Add.
                count.SetValue(store, 1048576); owner.Opened(1, 1);
                int notices = 0; for (int i = 0; i < 20; i++) { owner.Poll(); if (owner.TakeBackgroundFailure() != null) notices++; }
                Require(notices == 1, "one blocked fact cannot republish a capacity notice every Poll");
                count.SetValue(store, 0); owner.Poll(); release.Set(); Until(() => { owner.Poll(); return owner.Status == PreferenceStatus.Saved; });
                Require(owner.Contains(WorldObject.PositionKey(1, 1)), "capacity recovery accepts the waiting fact");
            }
            finally { count.SetValue(store, 0); release.Set(); owner.Stop(5000); entered.Dispose(); release.Dispose(); }
        }
        private sealed class SlowWrite : IPreferenceStorage
        {
            private readonly IPreferenceStorage inner; private readonly ManualResetEventSlim entered, release; private int writes;
            internal SlowWrite(IPreferenceStorage inner, ManualResetEventSlim entered, ManualResetEventSlim release) { this.inner = inner; this.entered = entered; this.release = release; }
            public PreferenceReadResult Read() { return inner.Read(); }
            public PreferenceWriteResult Write(string identity, byte[] bytes)
            { if (++writes == 1) { entered.Set(); if (!release.Wait(5000)) throw new TimeoutException("test write gate"); } return inner.Write(identity, bytes); }
            public void Dispose() { inner.Dispose(); }
        }
    }
}
