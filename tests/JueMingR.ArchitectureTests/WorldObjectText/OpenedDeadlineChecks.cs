using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using JueMingR.Features.WorldObjectText;
using JueMingR.Platform.Settings;
using JueMingR.Platform.WorldObjectText;
using static JueMingR.ArchitectureTests.PreferenceDeadlineChecks;

namespace JueMingR.ArchitectureTests
{
    internal static class OpenedDeadlineChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        internal static void Check(List<string> failures)
        {
            Run(failures, "opened UTC rollback cannot postpone a new fact", Rollback);
            Run(failures, "opened continuous facts keep the 1000ms upper merge interval", Continuous);
            Run(failures, "opened normal UTC continuous facts keep the 1000ms upper merge interval", () => Continuous(false));
            Run(failures, "opened another pair cannot postpone an earlier quiet deadline", EarlierPair);
            Run(failures, "opened released empty domain completes normal Stop", ReleasedEmptyStop);
        }
        private static void Rollback()
        {
            var clock = new Clock(); var storage = new MemoryStorage(); object store = Create(storage, clock);
            try
            {
                object lease = Call(store, "Acquire", new string('d', 64));
                Require(Until(() => (bool)Field(Snapshot(lease), "Loaded"), 3000), "writable pair load completed before accepting fact");
                Require((bool)Call(store, "Add", lease, WorldObject.PositionKey(12, 34)), "new fact accepted"); clock.MoveUtc(-3600000);
                Require(Until(() => storage.Writes != 0, 1800), "accepted opened fact stays pending after elapsed quiet time when UTC rolls back");
                var index = new OpenedPositionCodec().Decode(storage.Bytes, new string('d', 64));
                Require(index.Count == 1 && index.Contains(WorldObject.PositionKey(12, 34)), "unchanged format contains the exact accepted fact");
            }
            finally { Require((bool)Call(store, "Stop", 3000), "opened worker ended before fixture release"); }
        }
        private static void Continuous()
        { Continuous(true); }
        private static void Continuous(bool rollback)
        {
            var clock = new Clock(); var storage = new MemoryStorage(); object store = Create(storage, clock);
            try
            {
                object lease = Call(store, "Acquire", new string('e', 64));
                Require(Until(() => (bool)Field(Snapshot(lease), "Loaded"), 3000), "continuous pair loaded");
                Call(store, "Add", lease, WorldObject.PositionKey(1, 1));
                if (rollback) clock.MoveUtc(-3600000);
                bool wroteWhileChanging = false;
                for (int i = 2; i <= 17; i++)
                {
                    Thread.Sleep(80); Call(store, "Add", lease, WorldObject.PositionKey(i, 1));
                    wroteWhileChanging |= storage.Writes > 0;
                }
                Require(wroteWhileChanging, "continuous accepted facts must commit by the first 1000ms deadline" + (rollback ? " despite UTC rollback" : " with normal UTC"));
                Require((bool)Call(store, "Stop", 3000), "normal stop drains the last continuous batch");
                Require(new OpenedPositionCodec().Decode(storage.Bytes, new string('e', 64)).Count == 17, "Stop drains every later accepted fact without changing format");
            }
            finally { Require((bool)Call(store, "Stop", 3000), "continuous worker ended"); }
        }
        private static void EarlierPair()
        {
            var clock = new Clock(); var storage = new MemoryStorage[8]; var leases = new object[8];
            for (int i = 0; i < storage.Length; i++) storage[i] = new MemoryStorage();
            object store = Create(key => storage[key[0] - '0'], clock);
            try
            {
                for (int i = 0; i < leases.Length; i++)
                    leases[i] = Call(store, "Acquire", new string((char)('0' + i), 64));
                Require(Until(() => Array.TrueForAll(leases, lease => (bool)Field(Snapshot(lease), "Loaded")), 3000), "all eight pair read responsibilities complete");
                Require((bool)Call(store, "Add", leases[0], WorldObject.PositionKey(10, 10)), "earlier pair fact accepted");
                bool earlierCompleted = false;
                for (int i = 1; i <= 8; i++)
                {
                    Thread.Sleep(80);
                    Require((bool)Call(store, "Add", leases[7], WorldObject.PositionKey(i, 20)), "later pair fact accepted");
                    // A has a 150ms quiet deadline. Allow scheduling tolerance,
                    // while B remains continuously busy well beyond this point.
                    if (i <= 5) earlierCompleted |= storage[0].Writes != 0;
                }
                Require(earlierCompleted, "later pair input cannot renew another pair's existing quiet deadline");
                Require((bool)Call(store, "Stop", 3000), "eight-pair stop drains every accepted responsibility");
                Require(new OpenedPositionCodec().Decode(storage[0].Bytes, new string('0', 64)).Count == 1 &&
                    new OpenedPositionCodec().Decode(storage[7].Bytes, new string('7', 64)).Count == 8, "independent pair bytes retain their exact facts");
            }
            finally { Require((bool)Call(store, "Stop", 3000), "eight-pair worker ended"); }
        }
        private static object Create(MemoryStorage storage, Clock clock)
        { return Create(_ => storage, clock); }
        private static object Create(Func<string, IPreferenceStorage> storage, Clock clock)
        {
            Type type = typeof(OpenedPositionHistory).Assembly.GetType("JueMingR.Features.WorldObjectText.OpenedPositionStore", true);
            return ConstructClockOwner(type, new[] { typeof(Func<string, IPreferenceStorage>) }, new object[] { storage }, clock);
        }
        private static void ReleasedEmptyStop()
        {
            var clock = new Clock(); var storage = new MemoryStorage(); object store = Create(storage, clock);
            bool stopped = false;
            try
            {
                object lease = Call(store, "Acquire", new string('a', 64));
                Require(Until(() => (bool)Field(Snapshot(lease), "Loaded"), 3000), "empty pair read completed");
                Call(store, "Release", lease, null, 0, null);
                // This is fixture coordination, not the completion oracle: let
                // the real worker retire its lease and finish disposing storage
                // BEFORE Stop resets its completion signal. No I/O is blocked.
                object gate = Field(store, "gate");
                Require(Until(() => { lock (gate) return ((IDictionary)Field(store, "leases")).Count == 0 && !(bool)Field(store, "running"); }, 3000), "released domain is empty with no worker or I/O owner");
                stopped = (bool)Call(store, "Stop", 1000);
                Require(stopped && storage.Writes == 0, "normal Stop of an already released empty domain must complete without a nonexistent callback or fabricated write");
            }
            finally { if (!stopped) Call(store, "Stop", 3000); }
        }
        private static object Snapshot(object lease) { return lease.GetType().DeclaringType.GetMethod("Snapshot", Flags).Invoke(null, new[] { lease }); }
        private static object Field(object target, string name) { return target.GetType().GetField(name, Flags).GetValue(target); }
        private static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Flags).Invoke(target, args); }
    }
}
