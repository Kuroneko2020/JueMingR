using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using JueMingR.Features.CoinDeposit;
using JueMingR.Infrastructure.Storage;

namespace JueMingR.ArchitectureTests
{
    internal static class CoinSettingsChecks
    {
        internal static void Check(IList<string> failures)
        {
            var storage = new HotkeyCoreChecks.MemoryStorage();
            using (var settings = new CoinSettings(storage))
            {
                Wait(settings, () => settings.Loaded);
                Require(!settings.Enabled && storage.Bytes == null, "missing preference defaults off without writing", failures);
                storage.Block.Reset(); settings.Set(true);
                Require(settings.Busy && !settings.Enabled, "uncommitted enable stays inactive", failures);
                storage.Block.Set(); Wait(settings, () => !settings.Busy);
                Require(settings.Enabled, "reliable commit enables", failures);
                byte[] accepted = storage.Bytes;
                storage.Fail = true; storage.Block.Reset(); settings.Set(false);
                Require(!settings.Enabled && settings.Busy, "accepted disable immediately stops execution", failures);
                storage.Block.Set(); Wait(settings, () => !settings.Busy);
                Require(!settings.Enabled && settings.Value && !settings.Protected && ReferenceEquals(storage.Bytes, accepted), "known failed disable keeps saved bytes and pauses execution", failures);
                storage.Fail = false; settings.Set(true); Wait(settings, () => !settings.Busy);
                Require(settings.Enabled, "explicit reliable retry can restore operation", failures);
                storage.Fail = storage.Unknown = true; settings.Set(false); Wait(settings, () => !settings.Busy);
                Require(settings.Protected && !settings.Enabled && !settings.Set(true), "unknown commit cannot reactivate or retry", failures);
            }
            using (var restart = new CoinSettings(new HotkeyCoreChecks.MemoryStorage { Bytes = storage.Bytes }))
            {
                Wait(restart, () => restart.Loaded);
                Require(!restart.Enabled, "restart reads actual committed bytes after unknown result", failures);
            }
            string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "JueMingR-coin-preference-tests"));
            string root = Path.Combine(parent, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                string path = Path.Combine(root, "coin-deposit.json");
                using (var settings = new CoinSettings(new AtomicFileDocument(path, 4096, true)))
                {
                    Wait(settings, () => settings.Loaded); settings.Set(true); Wait(settings, () => !settings.Busy);
                    Require(settings.Enabled, "actual atomic preference enabled", failures);
                }
                using (var settings = new CoinSettings(new AtomicFileDocument(path, 4096, true)))
                {
                    Wait(settings, () => settings.Loaded);
                    Require(settings.Enabled, "actual file restart restores preference", failures);
                    File.WriteAllText(path, "external edit", new UTF8Encoding(false)); byte[] original = File.ReadAllBytes(path);
                    settings.Set(false); Wait(settings, () => !settings.Busy);
                    Require(settings.Protected && !settings.Enabled && original.SequenceEqual(File.ReadAllBytes(path)), "external edit is preserved and disables writes", failures);
                }
                string[] broken = { "{", "{\"format\":\"JueMingR.CoinDeposit\",\"version\":2,\"enabled\":true}", "{\"format\":\"JueMingR.CoinDeposit\",\"version\":1,\"enabled\":true,\"future\":1}" };
                for (int i = 0; i < broken.Length; i++)
                {
                    string file = Path.Combine(root, "broken" + i + ".json"); File.WriteAllText(file, broken[i], new UTF8Encoding(false)); byte[] original = File.ReadAllBytes(file);
                    using (var settings = new CoinSettings(new AtomicFileDocument(file, 4096, true)))
                    {
                        Wait(settings, () => settings.Loaded);
                        Require(settings.Protected && !settings.Enabled && !settings.Set(true) && original.SequenceEqual(File.ReadAllBytes(file)), "corrupt/future preference retains original file", failures);
                    }
                }
            }
            finally
            {
                if (!Path.GetFullPath(root).StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe coin fixture cleanup.");
                Directory.Delete(root, true);
            }
        }
        private static void Wait(CoinSettings settings, Func<bool> done)
        {
            var clock = Stopwatch.StartNew();
            while (!done() && clock.ElapsedMilliseconds < 5000) { settings.Poll(); Thread.Sleep(1); }
            if (!done()) throw new TimeoutException("Coin preference worker timeout.");
        }
        private static void Require(bool condition, string text, IList<string> failures)
        { if (!condition) failures.Add("Coin preference: " + text); }
    }
}
