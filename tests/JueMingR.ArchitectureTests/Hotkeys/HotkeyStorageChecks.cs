using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;

namespace JueMingR.ArchitectureTests
{
    internal static class HotkeyStorageChecks
    {
        internal static void Check(string repository, IList<string> failures)
        {
            string parent = Path.GetFullPath(Path.Combine(repository, ".local", "研究", "unified-hotkeys-implementation", "storage-tests"));
            string root = Path.Combine(parent, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                string path = Path.Combine(root, "hotkeys.json");
                var registry = new HotkeyRegistry(); registry.Register(new HotkeyAction("test.action", "Test", HotkeyContext.Gameplay, () => true, () => { }));
                using (var owner = new HotkeyBindings(registry, new AtomicFileDocument(path, 65536, true)))
                {
                    HotkeyCoreChecks.Wait(owner, () => owner.Loaded);
                    if (File.Exists(path) || owner.Get("test.action") != null) failures.Add("Hotkeys: missing file load wrote defaults or invented a binding.");
                    HotkeyCoreChecks.Set(owner, "test.action", "RightControl+OemPlus", (a, c) => null);
                    byte[] accepted = File.ReadAllBytes(path);
                    File.WriteAllText(path, "external user edit", Encoding.UTF8); byte[] external = File.ReadAllBytes(path);
                    long command; string reason; owner.TrySet("test.action", HotkeyCoreChecks.Parse("J"), (a, c) => null, out command, out reason);
                    HotkeyCoreChecks.Wait(owner, () => !owner.Busy);
                    if (!owner.Protected || owner.Get("test.action").MainKey != 187 || !Same(external, File.ReadAllBytes(path))) failures.Add("Hotkeys: actual external conflict overwrote user bytes or effective binding.");
                    File.WriteAllBytes(Path.Combine(root, "accepted.json"), accepted);
                }
                string reload = Path.Combine(root, "accepted.json");
                using (var owner = new HotkeyBindings(registry, new AtomicFileDocument(reload, 65536, true)))
                {
                    HotkeyCoreChecks.Wait(owner, () => owner.Loaded);
                    if (owner.Get("test.action")?.Text != "RightControl+OemPlus") failures.Add("Hotkeys: actual file reload lost OEM/modifier identity.");
                    HotkeyCoreChecks.Set(owner, "test.action", null, null);
                }
                using (var owner = new HotkeyBindings(registry, new AtomicFileDocument(reload, 65536, true)))
                { HotkeyCoreChecks.Wait(owner, () => owner.Loaded); if (owner.Get("test.action") != null) failures.Add("Hotkeys: clear was not durable across worker restart."); }
                string[] broken = { "{", "{\"format\":\"JueMingR.Hotkeys\",\"version\":2,\"bindings\":[]}", "{\"format\":\"JueMingR.Hotkeys\",\"version\":1,\"bindings\":[{\"action\":\"test.action\",\"binding\":\"J\",\"future\":1}]}" };
                for (int i = 0; i < broken.Length; i++)
                {
                    string file = Path.Combine(root, "protected" + i + ".json"); File.WriteAllText(file, broken[i], new UTF8Encoding(false)); byte[] original = File.ReadAllBytes(file);
                    using (var owner = new HotkeyBindings(registry, new AtomicFileDocument(file, 65536, true)))
                    { HotkeyCoreChecks.Wait(owner, () => owner.Loaded); if (!owner.Protected || !Same(original, File.ReadAllBytes(file))) failures.Add("Hotkeys: malformed/future actual document lost protection."); }
                }
                string future = Path.Combine(root, "unknown-action.json");
                File.WriteAllBytes(future, HotkeyDocument.Encode(new HotkeyDocument(new[] { new KeyValuePair<string, string>("future.action", "Mouse5") })));
                using (var owner = new HotkeyBindings(registry, new AtomicFileDocument(future, 65536, true)))
                { HotkeyCoreChecks.Wait(owner, () => owner.Loaded); HotkeyCoreChecks.Set(owner, "test.action", "J", (a, c) => null); }
                var round = HotkeyDocument.Decode(File.ReadAllBytes(future));
                if (round.Entries.Count != 2 || round.Entries[0].Key != "future.action" || round.Entries[0].Value != "Mouse5") failures.Add("Hotkeys: actual unrelated save removed unknown action.");
            }
            finally
            {
                if (!Path.GetFullPath(root).StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new Exception("Unsafe hotkey test cleanup.");
                Directory.Delete(root, true);
            }
        }
        private static bool Same(byte[] a, byte[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    }
}
