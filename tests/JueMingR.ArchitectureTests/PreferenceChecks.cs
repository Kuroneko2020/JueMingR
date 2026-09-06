using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using JueMingR.Features.Biomes;
using JueMingR.Infrastructure.Settings;
using JueMingR.Platform.Settings;

namespace JueMingR.ArchitectureTests
{
    internal static class PreferenceChecks
    {
        internal static void Check(IList<string> failures)
        {
            Run(failures, "settings restart and independent documents", Restart);
            Run(failures, "settings strict JSON and original protection", InvalidDocuments);
            Run(failures, "settings bounded coalescing and stop", Coalescing);
        }

        private static void Restart()
        {
            WithRoot(root =>
            {
                string biome = Path.Combine(root, "config", "features", "biome-display.json");
                string ui = Path.Combine(root, "config", "ui.json");
                using (var setting = new PreferenceDocument<bool>(new FilePreferenceStorage(biome), new BiomePreferenceCodec(), true))
                using (var position = new PreferenceDocument<WindowPosition>(new FilePreferenceStorage(ui), new UiPreferenceCodec(), null))
                {
                    Loaded(setting); Loaded(position);
                    Require(setting.Snapshot.Value && position.Snapshot.Value == null, "missing defaults");
                    Require(setting.Snapshot.Status == PreferenceStatus.Missing, "missing is distinct");
                    Require(!File.Exists(biome) && !File.Exists(ui), "no default rewrite");
                    Require(setting.Set(false), "accept off");
                    Require(position.Set(new WindowPosition(950, 640)), "accept position");
                    Require(setting.Stop(3000) && position.Stop(3000), "flush both");
                }
                using (var setting = new PreferenceDocument<bool>(new FilePreferenceStorage(biome), new BiomePreferenceCodec(), true))
                using (var position = new PreferenceDocument<WindowPosition>(new FilePreferenceStorage(ui), new UiPreferenceCodec(), null))
                {
                    Loaded(setting); Loaded(position);
                    Require(!setting.Snapshot.Value, "new owner reads saved off");
                    Require(position.Snapshot.Value.Equals(new WindowPosition(950, 640)), "new owner reads logical position");
                    Require(setting.Snapshot.Status == PreferenceStatus.Saved, "valid read is reliable");
                }
                File.WriteAllText(biome, "{broken", new UTF8Encoding(false));
                using (var setting = new PreferenceDocument<bool>(new FilePreferenceStorage(biome), new BiomePreferenceCodec(), true))
                using (var position = new PreferenceDocument<WindowPosition>(new FilePreferenceStorage(ui), new UiPreferenceCodec(), null))
                {
                    Loaded(setting); Loaded(position);
                    Require(setting.Snapshot.Status == PreferenceStatus.Invalid, "one corrupt document");
                    Require(position.Snapshot.Value.X == 950, "independent valid file still loaded");
                    setting.Set(false); Require(setting.Stop(3000), "protected stop");
                    Require(File.ReadAllText(biome) == "{broken", "original remains");
                }
            });
        }

        private static void InvalidDocuments()
        {
            var codec = new BiomePreferenceCodec();
            Require(codec.Decode(codec.Encode(false)) == false, "real codec roundtrip");
            string[] invalid = {
                "{}", "{\"format\":\"JueMingR.BiomeDisplay\",\"version\":1}",
                "{\"format\":\"JueMingR.BiomeDisplay\",\"version\":1,\"enabled\":\"false\"}",
                "{\"format\":\"JueMingR.BiomeDisplay\",\"version\":1,\"enabled\":false,\"enabled\":true}",
                "{\"format\":\"JueMingR.BiomeDisplay\",\"version\":1,\"enabled\":false} trailing"
            };
            foreach (string json in invalid) ExpectFormat(codec, json, PreferenceStatus.Invalid);
            ExpectFormat(codec, "{\"format\":\"JueMingR.BiomeDisplay\",\"version\":2,\"enabled\":false}", PreferenceStatus.UnsupportedVersion);
            ExpectFormat(codec, "{\"format\":\"JueMingR.BiomeDisplay\",\"version\":1,\"enabled\":false,\"future\":17}", PreferenceStatus.UnknownFields);
            var ui = new UiPreferenceCodec();
            Require(ui.Decode(ui.Encode(null)) == null, "explicit absent position");
            Require(ui.Decode(ui.Encode(new WindowPosition(100000, -100000))).X == 100000, "offscreen coordinates are legal");
            string[] protectedJson = {
                invalid[1],
                "{\"format\":\"JueMingR.BiomeDisplay\",\"version\":2,\"enabled\":false}",
                "{\"format\":\"JueMingR.BiomeDisplay\",\"version\":1,\"enabled\":false,\"future\":17}"
            };
            foreach (string json in protectedJson)
            {
                WithRoot(root =>
                {
                    string path = Path.Combine(root, "biome.json");
                    File.WriteAllText(path, json, new UTF8Encoding(false));
                    var storage = new CountingStorage(path);
                    using (var setting = new PreferenceDocument<bool>(storage, codec, true))
                    {
                        Loaded(setting);
                        Require(setting.Set(false), "protected file still permits memory selection");
                        Require(setting.Stop(3000), "protected document stopped");
                        Require(File.ReadAllText(path) == json && storage.Writes == 0,
                            "missing field/future/unknown original cannot be rewritten");
                        Require(!File.Exists(path + ".bak"), "bad original never rotates a recovery backup");
                    }
                });
            }
            string[] invalidPositions = {
                "{\"x\":1}", "{\"x\":1.5,\"y\":12}", "{\"x\":2147483648,\"y\":12}",
                "{\"x\":1,\"y\":12,\"scale\":1}", "[]", "true"
            };
            foreach (string value in invalidPositions)
            {
                bool rejected = false;
                try { ui.Decode(Encoding.UTF8.GetBytes("{\"format\":\"JueMingR.Ui\",\"version\":1,\"windowPosition\":" + value + "}")); }
                catch (PreferenceFormatException) { rejected = true; }
                Require(rejected, "invalid/unknown position fields rejected");
            }
        }

        private static void Coalescing()
        {
            WithRoot(root =>
            {
                string path = Path.Combine(root, "biome.json");
                var storage = new CountingStorage(path);
                using (var setting = new PreferenceDocument<bool>(storage, new BiomePreferenceCodec(), true, 200))
                {
                    Loaded(setting);
                    Require(!setting.Set(true), "same selection no request");
                    setting.Set(false); setting.Set(true); setting.Set(false);
                    Require(setting.Stop(3000), "stop drains last value");
                    Require(storage.Writes == 1, "coalesced one physical commit");
                    Require(new BiomePreferenceCodec().Decode(File.ReadAllBytes(path)) == false, "latest value on disk");
                    Require(!setting.Set(true), "stopped owner rejects commands");
                    int writes = storage.Writes;
                    Thread.Sleep(250);
                    Require(storage.Writes == writes, "no writes after stop");
                }
            });
        }

        // Count actual commits only in the tests that ask about coalescing/idle
        // work. No production counter or probe is required by the save contract.
        internal sealed class CountingStorage : IPreferenceStorage
        {
            private readonly FilePreferenceStorage inner;
            private int writes;
            internal CountingStorage(string path) { inner = new FilePreferenceStorage(path); }
            internal int Writes { get { return Volatile.Read(ref writes); } }
            public PreferenceReadResult Read() { return inner.Read(); }
            public PreferenceWriteResult Write(string identity, byte[] contents)
            {
                PreferenceWriteResult result = inner.Write(identity, contents);
                if (result.Status == PreferenceWriteStatus.Saved) Interlocked.Increment(ref writes);
                return result;
            }
            public void Dispose() { inner.Dispose(); }
        }

        private static void ExpectFormat(BiomePreferenceCodec codec, string json, PreferenceStatus status)
        {
            try { codec.Decode(Encoding.UTF8.GetBytes(json)); }
            catch (PreferenceFormatException exception) { Require(exception.Status == status, "format classification"); return; }
            throw new InvalidOperationException("invalid JSON accepted");
        }
        internal static void Loaded<T>(PreferenceDocument<T> value)
        {
            Require(SpinWait.SpinUntil(() => value.Snapshot.IsLoaded, 3000), "load completed within test bound");
        }
        private static void WithRoot(Action<string> action)
        {
            // Explicit injection precedes construction: no product path discovery runs in tests.
            string root = Path.Combine(Path.GetTempPath(), "JueMingR-Preferences-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try { action(root); }
            finally { Directory.Delete(root, true); }
        }
        private static void Run(IList<string> failures, string name, Action test)
        { try { test(); } catch (Exception exception) { failures.Add(name + ": " + exception.Message); } }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
