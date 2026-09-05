using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using JueMingR.Features.Biomes;
using JueMingR.Infrastructure.Settings;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class PreferenceConcurrencyChecks
    {
        internal static void Check(IList<string> failures)
        {
            Run(failures, "load timeout retires late data", LateRead);
            Run(failures, "old save completion cannot erase new choice", LateWrite);
            Run(failures, "bounded stop retains native operation ownership", StopDuringWrite);
            Run(failures, "fault never changes desired setting", FeatureFault);
        }

        private static void LateRead()
        {
            WithRoot(root =>
            {
                string path = Path.Combine(root, "biome.json");
                File.WriteAllBytes(path, new BiomePreferenceCodec().Encode(true));
                var storage = new GatedStorage(path, true, false);
                var setting = new PreferenceDocument<bool>(storage, new BiomePreferenceCodec(), true, 0);
                try
                {
                    Require(storage.ReadEntered.Wait(3000), "read reached gate");
                    Require(!setting.Set(false), "unresolved read must not accept command");
                    setting.AbandonSlowLoad();
                    Require(setting.Set(false), "protected defaults allow memory choice");
                    storage.ReadRelease.Set();
                    Require(storage.Disposed.Wait(3000), "late read releases storage");
                    Require(!setting.Snapshot.Value && setting.Snapshot.Status == PreferenceStatus.IoFailure,
                        "late true must not replace chosen false or clear protection");
                    Require(storage.WriteCalls == 0 && new BiomePreferenceCodec().Decode(File.ReadAllBytes(path)), "no stale read writeback");
                }
                finally { storage.ReleaseAll(); Require(setting.Stop(3000), "read worker joined"); storage.DisposeEvents(); }
            });
        }

        private static void LateWrite()
        {
            WithRoot(root =>
            {
                string path = Path.Combine(root, "biome.json");
                var storage = new GatedStorage(path, false, true);
                var setting = new PreferenceDocument<bool>(storage, new BiomePreferenceCodec(), true, 0);
                try
                {
                    PreferenceChecks.Loaded(setting);
                    setting.Set(false);
                    Require(storage.WriteEntered.Wait(3000), "first write reached gate");
                    for (int i = 0; i < 101; i++) setting.Set(i % 2 == 0);
                    Require(setting.Snapshot.Value && setting.Snapshot.Status == PreferenceStatus.Pending, "latest true held in memory");
                    Require(storage.WriteCalls == 1, "one in-flight operation despite many commands");
                    storage.WriteRelease.Set();
                    Require(SpinWait.SpinUntil(() => setting.Snapshot.Status == PreferenceStatus.Saved, 3000), "latest write completes");
                    Require(setting.Snapshot.Value && setting.CompletedWriteCount == 2 && storage.WriteCalls == 2,
                        "old completion drains one latest slot");
                    Require(new BiomePreferenceCodec().Decode(File.ReadAllBytes(path)), "new true wins on disk");
                    long completed = setting.CompletedWriteCount;
                    for (int i = 0; i < 100; i++) Require(!setting.Set(true), "same values are inert");
                    Thread.Sleep(200);
                    Require(setting.CompletedWriteCount == completed, "idle does not save");
                }
                finally { storage.ReleaseAll(); Require(setting.Stop(3000), "write worker joined"); storage.DisposeEvents(); }
            });
        }

        private static void StopDuringWrite()
        {
            WithRoot(root =>
            {
                string path = Path.Combine(root, "biome.json");
                var storage = new GatedStorage(path, false, true);
                var setting = new PreferenceDocument<bool>(storage, new BiomePreferenceCodec(), true, 0);
                try
                {
                    PreferenceChecks.Loaded(setting); setting.Set(false);
                    Require(storage.WriteEntered.Wait(3000), "native-like work began");
                    setting.Set(true);
                    Require(!setting.Stop(0), "busy I/O cannot be claimed stopped");
                    Require(!storage.Disposed.IsSet, "storage remains worker-owned");
                    Require(!setting.Set(false), "stopped owner rejects new work");
                    storage.WriteRelease.Set();
                    Require(setting.Stop(3000), "eventually joined before directory cleanup");
                    Require(storage.WriteCalls == 1 && storage.Disposed.IsSet, "pending request cancelled and lease released");
                    Require(!new BiomePreferenceCodec().Decode(File.ReadAllBytes(path)), "only operation already in progress may commit");
                }
                finally { storage.ReleaseAll(); Require(setting.Stop(3000), "cleanup joined"); storage.DisposeEvents(); }
            });
        }

        private static void FeatureFault()
        {
            WithRoot(root =>
            {
                string path = Path.Combine(root, "biome.json");
                using (var setting = new PreferenceDocument<bool>(new FilePreferenceStorage(path), new BiomePreferenceCodec(), true))
                {
                    PreferenceChecks.Loaded(setting);
                    var feature = new BiomeDisplayFeature(new EmptyObservation(), false);
                    Require(!feature.HasFailed, "user off is healthy");
                    feature.SetEnabled(true); Require(feature.Enabled, "off can become on");
                    feature.FailClosed(); feature.SetEnabled(setting.Snapshot.Value);
                    Require(feature.HasFailed && !feature.Enabled && setting.Snapshot.Value, "fault latch does not mutate intent or auto-retry");
                    Require(!File.Exists(path) && setting.CompletedWriteCount == 0, "fault does not save an off preference");
                }
            });
        }

        private sealed class EmptyObservation : JueMingR.Platform.Biomes.IBiomeObservationSource
        {
            public bool TryObserve(out JueMingR.Platform.Biomes.BiomeObservation observation)
            { observation = default(JueMingR.Platform.Biomes.BiomeObservation); return false; }
        }

        private sealed class GatedStorage : IPreferenceStorage
        {
            private readonly FilePreferenceStorage inner;
            private readonly bool gateRead, gateWrite;
            internal readonly ManualResetEventSlim ReadEntered = new ManualResetEventSlim();
            internal readonly ManualResetEventSlim ReadRelease = new ManualResetEventSlim();
            internal readonly ManualResetEventSlim WriteEntered = new ManualResetEventSlim();
            internal readonly ManualResetEventSlim WriteRelease = new ManualResetEventSlim();
            internal readonly ManualResetEventSlim Disposed = new ManualResetEventSlim();
            internal int WriteCalls;
            internal GatedStorage(string path, bool gateRead, bool gateWrite)
            { inner = new FilePreferenceStorage(path); this.gateRead = gateRead; this.gateWrite = gateWrite; }
            public PreferenceReadResult Read()
            {
                ReadEntered.Set();
                if (gateRead && !ReadRelease.Wait(5000)) throw new TimeoutException("test read gate timed out");
                return inner.Read();
            }
            public PreferenceWriteResult Write(string identity, byte[] bytes)
            {
                Interlocked.Increment(ref WriteCalls); WriteEntered.Set();
                if (gateWrite && !WriteRelease.Wait(5000)) throw new TimeoutException("test write gate timed out");
                return inner.Write(identity, bytes);
            }
            public void Dispose() { inner.Dispose(); Disposed.Set(); }
            internal void ReleaseAll() { ReadRelease.Set(); WriteRelease.Set(); }
            internal void DisposeEvents()
            { ReadEntered.Dispose(); ReadRelease.Dispose(); WriteEntered.Dispose(); WriteRelease.Dispose(); Disposed.Dispose(); }
        }
        private static void WithRoot(Action<string> test)
        {
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-PreferenceTiming-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try { test(root); } finally { Directory.Delete(root, true); }
        }
        private static void Run(IList<string> failures, string name, Action test)
        { try { test(); } catch (Exception exception) { failures.Add(name + ": " + exception.Message); } }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
