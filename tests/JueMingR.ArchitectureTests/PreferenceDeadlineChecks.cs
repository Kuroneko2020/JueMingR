using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using JueMingR.Features.Biomes;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    // The oracle is elapsed quiet time after an accepted intent. The two clock
    // delegates describe the same scene; only constructor binding changes when
    // the production deadline migrates from UTC to monotonic milliseconds.
    internal static class PreferenceDeadlineChecks
    {
        internal static void Check(List<string> failures)
        {
            Run(failures, "preference UTC rollback cannot postpone accepted intent", Rollback);
            Run(failures, "preference UTC jump cannot bypass quiet merge", Forward);
            Run(failures, "preference normal stop bypasses quiet deadline", StopPending);
        }

        private static void Rollback()
        {
            var clock = new Clock(); var storage = new MemoryStorage();
            var document = Create(storage, clock);
            try
            {
                Loaded(document); Require(document.Set(false), "loaded writable owner accepts a new preference");
                clock.MoveUtc(-3600000);
                Require(Until(() => document.Snapshot.Status == PreferenceStatus.Saved, 1800),
                    "accepted preference remains Pending after elapsed quiet time when UTC moves backwards");
                Require(storage.Writes == 1 && !new BiomePreferenceCodec().Decode(storage.Bytes), "one accepted false reaches the unchanged codec");
            }
            finally { Require(document.Stop(3000), "preference worker joined before fixture release"); }
        }

        private static void Forward()
        {
            var clock = new Clock(); var storage = new MemoryStorage();
            var document = Create(storage, clock);
            try
            {
                Loaded(document); Require(document.Set(false), "first quiet intent accepted");
                clock.MoveUtc(3600000);
                // Waking through a same-value command is intentionally not used:
                // it would reset the business intent instead of testing its due.
                Thread.Sleep(40);
                Require(storage.Writes == 0, "wall-clock jump cannot complete less than the quiet interval");
                Require(Until(() => document.Snapshot.Status == PreferenceStatus.Saved, 1800), "ordinary elapsed quiet completion");
            }
            finally { Require(document.Stop(3000), "forward scene worker joined"); }
        }

        private static void StopPending()
        {
            var clock = new Clock(); var storage = new MemoryStorage();
            var document = Create(storage, clock);
            try
            {
                Loaded(document); document.Set(false); clock.MoveUtc(-3600000);
                Require(document.Stop(3000), "normal Stop drains accepted request without waiting for its due time");
                Require(storage.Writes == 1 && !new BiomePreferenceCodec().Decode(storage.Bytes), "Stop preserves the accepted bytes");
                Require(!document.Set(true), "stopped owner rejects later intent");
            }
            finally { Require(document.Stop(3000), "stop scene worker joined"); }
        }

        internal static PreferenceDocument<bool> Create(IPreferenceStorage storage, Clock clock)
        {
            var type = typeof(PreferenceDocument<bool>);
            var prefix = new[] { typeof(IPreferenceStorage), typeof(IPreferenceCodec<bool>), typeof(bool), typeof(int) };
            var arguments = new object[] { storage, new BiomePreferenceCodec(), true, 150 };
            return (PreferenceDocument<bool>)ConstructClockOwner(type, prefix, arguments, clock);
        }

        internal static object ConstructClockOwner(Type type, Type[] prefix, object[] arguments, Clock clock)
        {
            foreach (bool monotonic in new[] { true, false })
            {
                var signature = new Type[prefix.Length + 1]; Array.Copy(prefix, signature, prefix.Length);
                signature[prefix.Length] = monotonic ? typeof(Func<long>) : typeof(Func<DateTime>);
                var constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, signature, null);
                if (constructor == null) continue;
                var values = new object[arguments.Length + 1]; Array.Copy(arguments, values, arguments.Length);
                values[arguments.Length] = monotonic ? (object)(Func<long>)(() => clock.MonotonicMilliseconds) : (Func<DateTime>)(() => clock.UtcNow);
                Console.WriteLine("Deadline scene binding=" + (monotonic ? "monotonic-ms" : "UTC-old-semantics"));
                return constructor.Invoke(values);
            }
            // Setup failure cannot be counted as the old business regression.
            throw new InvalidOperationException("Deadline fixture: no supported internal clock constructor.");
        }

        internal sealed class Clock
        {
            private readonly Stopwatch elapsed = Stopwatch.StartNew();
            private readonly long initialUtc = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
            private long utcOffset;
            internal long MonotonicMilliseconds { get { return elapsed.ElapsedMilliseconds; } }
            internal DateTime UtcNow { get { return new DateTime(initialUtc + elapsed.Elapsed.Ticks + Interlocked.Read(ref utcOffset), DateTimeKind.Utc); } }
            internal void MoveUtc(int milliseconds) { Interlocked.Add(ref utcOffset, (long)milliseconds * TimeSpan.TicksPerMillisecond); }
        }

        internal sealed class MemoryStorage : IPreferenceStorage
        {
            private int writes;
            private byte[] bytes;
            internal int Writes { get { return Volatile.Read(ref writes); } }
            internal byte[] Bytes { get { return Volatile.Read(ref bytes); } }
            public PreferenceReadResult Read() { return new PreferenceReadResult(PreferenceReadStatus.Missing, null, null, null); }
            public PreferenceWriteResult Write(string identity, byte[] contents)
            {
                Volatile.Write(ref bytes, (byte[])contents.Clone());
                int revision = Interlocked.Increment(ref writes);
                return new PreferenceWriteResult(PreferenceWriteStatus.Saved, "memory-" + revision, null);
            }
            public void Dispose() { }
        }
        internal static void Loaded(PreferenceDocument<bool> document) { Require(Until(() => document.Snapshot.IsLoaded, 3000), "initial read completed"); }
        internal static bool Until(Func<bool> condition, int milliseconds)
        { var elapsed = Stopwatch.StartNew(); while (!condition() && elapsed.ElapsedMilliseconds < milliseconds) Thread.Sleep(2); return condition(); }
        internal static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        internal static void Run(List<string> failures, string name, Action scene)
        { try { scene(); } catch (Exception e) { failures.Add(name + ": " + e.Message); } }
    }
}
