using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JueMingR.Infrastructure.Storage;
using JueMingR.Platform.Hotkeys;

namespace JueMingR.ArchitectureTests
{
    // Exercise the common binding/document boundary with the hidden action
    // absent/present. The original-Terraria UI check owns real host publication.
    internal static class HiddenPathBindingChecks
    {
        private const string PathId = "combat.npc-path", OrdinaryId = "combat.aim", BusyId = "combat.busy";
        internal static void Run()
        {
            ExactReservation(); LockedSaveAndUnlock(); ExistingSharedKey(); CleanUnlockAndReload(); InvalidRecords();
            Console.WriteLine("PASS hidden binding: exact-ID isolation; locked K save/dispatch; preserved disk record; explicit unlock conflict; reload; malformed/duplicate and temporary configuration guards.");
        }
        private static HotkeyRegistry Registry(out DynamicHotkeyOwner hidden, Action ordinary = null)
        {
            var registry = new HotkeyRegistry(); hidden = registry.CreateDynamicActionOwner(PathId);
            registry.Register(new HotkeyAction(OrdinaryId, "辅助瞄准", HotkeyContext.Gameplay, () => true, ordinary ?? (() => { })));
            registry.Register(new HotkeyAction(BusyId, "暂不可配置动作", HotkeyContext.Gameplay, () => false, () => { }, () => false));
            return registry;
        }
        private static void ExactReservation()
        {
            DynamicHotkeyOwner hidden; var registry = Registry(out hidden);
            registry.Register(new HotkeyAction(PathId + ".child", "邻近动作", HotkeyContext.Gameplay, () => true, () => { }));
            Require(hidden.Owns(PathId) && !hidden.Owns(OrdinaryId) && !hidden.Owns(PathId + ".child"), "exact reservation owns one stable ID only");
            Reject(() => registry.CreateDynamicOwner("combat."), "prefix cannot take an exact owner's namespace");
            Reject(() => registry.CreateDynamicActionOwner(PathId), "duplicate exact owner rejected");
            Reject(() => registry.CreateDynamicActionOwner(OrdinaryId), "exact owner cannot take a core ID");
            var prefix = new HotkeyRegistry(); prefix.CreateDynamicOwner("combat.");
            Reject(() => prefix.CreateDynamicActionOwner(PathId), "exact owner cannot take a prefix owner's ID");
            string reason; long revision = registry.Revision;
            Require(!hidden.TryReplace(new[] { new HotkeyAction(OrdinaryId, "越界", HotkeyContext.Gameplay, () => true, () => { }) }, out reason) && registry.Revision == revision,
                "out-of-scope snapshot has no partial publication");
            registry.Freeze();
            Reject(() => registry.CreateDynamicActionOwner("new.action"), "freeze keeps reservation creation closed");
            var action = PathAction(() => { });
            Require(hidden.TryReplace(new[] { action }, out reason), "reserved action can publish after freeze");
            revision = registry.Revision;
            for (int i = 0; i < 100; i++) Require(hidden.TryReplace(new[] { action }, out reason), "same exact snapshot accepted");
            Require(registry.Revision == revision && registry.Find(OrdinaryId) != null && registry.Find(PathId + ".child") != null, "stable snapshot leaves revision and ordinary combat identities unchanged");
        }
        private static void LockedSaveAndUnlock()
        {
            int calls = 0, pathCalls = 0; DynamicHotkeyOwner hidden;
            var registry = Registry(out hidden, () => calls++);
            using (var file = new IsolatedDocument(registry, Document(PathId, "K", BusyId, "L")))
            {
                var bindings = file.Bindings; byte[] original = file.Bytes;
                Require(!bindings.Protected && bindings.Get(PathId) == null && registry.Find(PathId) == null, "locked saved path has no effective target");
                Require(bindings.Get(BusyId) != null && bindings.Validate(OrdinaryId, HotkeyCoreChecks.Parse("L")) != null,
                    "temporary CanConfigure=false still reserves its ordinary binding");
                Require(bindings.Validate(OrdinaryId, HotkeyCoreChecks.Parse("K")) == null, "locked path does not reserve ordinary K");
                Require(file.Bytes.SequenceEqual(original), "load and validation preserve exact original disk bytes");
                HotkeyCoreChecks.Set(bindings, OrdinaryId, "K", (a, c) => null);
                var saved = HotkeyDocument.Decode(file.Bytes);
                Require(saved.Entries.Single(e => e.Key == PathId).Value == "K" && saved.Entries.Single(e => e.Key == OrdinaryId).Value == "K", "ordinary save retains the hidden original record");
                Press(bindings, 75); Require(calls == 1 && pathCalls == 0, "locked K executes the ordinary consumer only");
                byte[] beforeUnlock = file.Bytes; string reason;
                Require(hidden.TryReplace(new[] { PathAction(() => pathCalls++) }, out reason), "unlock publishes original ID");
                Require(bindings.Get(PathId) == null && bindings.Get(OrdinaryId) == null && bindings.Protected && bindings.Error(PathId) != null && bindings.Error(OrdinaryId) != null,
                    "unlock exposes a visible protected conflict and disables both effective bindings");
                Press(bindings, 75); Require(calls == 1 && pathCalls == 0 && file.Bytes.SequenceEqual(beforeUnlock), "unlock neither steals K nor edits conflicting disk records");
            }
        }
        private static void ExistingSharedKey()
        {
            int calls = 0; DynamicHotkeyOwner hidden; var registry = Registry(out hidden, () => calls++);
            using (var file = new IsolatedDocument(registry, Document(PathId, "K", OrdinaryId, "K")))
            {
                Require(!file.Bindings.Protected && file.Bindings.Get(PathId) == null && file.Bindings.Get(OrdinaryId)?.Text == "K", "preexisting shared K is valid while hidden action is locked");
                byte[] bytes = file.Bytes; Press(file.Bindings, 75);
                Require(calls == 1 && file.Bytes.SequenceEqual(bytes), "locked preexisting shared K dispatches without a write");
            }
        }
        private static void CleanUnlockAndReload()
        {
            int calls = 0; DynamicHotkeyOwner hidden; var registry = Registry(out hidden);
            byte[] bytes = Document(PathId, "K");
            using (var file = new IsolatedDocument(registry, bytes))
            {
                var action = PathAction(() => calls++); string reason;
                hidden.TryReplace(new[] { action }, out reason);
                Require(file.Bindings.Get(PathId)?.Text == "K", "uncontested unlock restores old K");
                Press(file.Bindings, 75); Require(calls == 1, "uncontested unlock executes the registered consumer");
                hidden.TryReplace(new HotkeyAction[0], out reason);
                Require(!action.Invoke(HotkeyContext.SinglePlayer) && file.Bindings.Get(PathId) == null && file.Bytes.SequenceEqual(bytes), "retired snapshot cannot dispatch or delete disk preference");
            }
            var next = Registry(out hidden);
            using (var file = new IsolatedDocument(next, bytes))
                Require(file.Bindings.Get(PathId) == null && !file.Bindings.Protected && file.Bytes.SequenceEqual(bytes), "fresh locked registry reload keeps old binding inert");
        }
        private static void InvalidRecords()
        {
            foreach (byte[] bytes in new[] { Document(PathId, "K+L", OrdinaryId, "K"), Document(PathId, "K", PathId, "J", OrdinaryId, "K") })
            {
                DynamicHotkeyOwner hidden; var registry = Registry(out hidden);
                using (var file = new IsolatedDocument(registry, bytes))
                    Require(file.Bindings.Protected && file.Bindings.Get(PathId) == null && file.Bindings.Get(OrdinaryId)?.Text == "K" && file.Bytes.SequenceEqual(bytes),
                        "locked hidden records retain malformed/duplicate protection without disabling an independent valid action");
            }
        }
        private static HotkeyAction PathAction(Action command)
        { return new HotkeyAction(PathId, "寻路预测", HotkeyContext.Gameplay, () => true, command); }
        private static byte[] Document(params string[] fields)
        {
            var entries = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < fields.Length; i += 2) entries.Add(new KeyValuePair<string, string>(fields[i], fields[i + 1]));
            return HotkeyDocument.Encode(new HotkeyDocument(entries));
        }
        private static void Press(HotkeyBindings bindings, int key)
        {
            var input = new HotkeyInput(); var keys = new bool[HotkeyChord.KeyCount]; input.Update(keys, true);
            keys[key] = true; input.Update(keys, true); bindings.Dispatch(input, HotkeyContext.SinglePlayer, true);
        }
        private static void Reject(Action command, string message)
        { try { command(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException("Hidden binding: " + message); }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Hidden binding: " + message); }
        private sealed class IsolatedDocument : IDisposable
        {
            private readonly string root, path;
            internal readonly HotkeyBindings Bindings;
            internal byte[] Bytes { get { return File.ReadAllBytes(path); } }
            internal IsolatedDocument(HotkeyRegistry registry, byte[] bytes)
            {
                root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "JueMingR-HiddenBinding-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root); path = System.IO.Path.Combine(root, "hotkeys.json"); File.WriteAllBytes(path, bytes);
                Bindings = new HotkeyBindings(registry, new AtomicFileDocument(path, 65536, true));
                HotkeyCoreChecks.Wait(Bindings, () => Bindings.Loaded);
            }
            public void Dispose()
            {
                Require(Bindings.Stop(2000), "isolated storage worker stops before cleanup"); Bindings.Dispose();
                Directory.Delete(root, true);
            }
        }
    }
}
