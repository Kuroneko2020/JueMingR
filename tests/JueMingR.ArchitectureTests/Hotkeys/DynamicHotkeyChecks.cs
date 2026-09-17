using System;
using System.Collections.Generic;
using JueMingR.Platform.Hotkeys;

namespace JueMingR.ArchitectureTests
{
    internal static class DynamicHotkeyChecks
    {
        internal static void Check(IList<string> failures)
        {
            int oldCalls = 0, newCalls = 0, coreCalls = 0;
            var registry = new HotkeyRegistry(); var dynamic = registry.CreateDynamicOwner("quick.use.");
            registry.Register(new HotkeyAction("core", "核心", HotkeyContext.Gameplay, () => true, () => coreCalls++));
            var old = new HotkeyAction("quick.use.one", "条目一", HotkeyContext.Gameplay, () => true, () => oldCalls++);
            var storage = new HotkeyCoreChecks.MemoryStorage { Bytes = HotkeyDocument.Encode(new HotkeyDocument(new[] {
                new KeyValuePair<string,string>("quick.use.one", "J"), new KeyValuePair<string,string>("core", "K"),
                new KeyValuePair<string,string>("future.other", "Mouse5") })) };
            using (var bindings = new HotkeyBindings(registry, storage))
            {
                HotkeyCoreChecks.Wait(bindings, () => bindings.Loaded);
                if (bindings.Get(old.Id) != null) failures.Add("Dynamic: orphan activated before domain registration.");
                string reason;
                if (!dynamic.TryReplace(new[] { old }, out reason) || bindings.Get(old.Id)?.Text != "J") failures.Add("Dynamic: reliable domain snapshot did not activate existing binding.");
                try { registry.Register(new HotkeyAction("late", "late", HotkeyContext.Gameplay, () => true, () => { })); failures.Add("Dynamic: core was unfrozen."); }
                catch (InvalidOperationException) { }
                var input = new HotkeyInput(); var keys = new bool[HotkeyChord.KeyCount]; input.Update(keys, true);
                keys[74] = keys[75] = true; input.Update(keys, true); bindings.Dispatch(input, HotkeyContext.SinglePlayer, true);
                if (oldCalls != 1 || coreCalls != 1) failures.Add("Dynamic: shared dispatcher did not execute both actions.");
                storage.Block.Reset(); long save; string error;
                if (!bindings.TrySet(old.Id, HotkeyCoreChecks.Parse("L"), (a,c) => null, out save, out error)) throw new Exception(error);
                dynamic.TryReplace(new HotkeyAction[0], out reason);
                if (old.Invoke(HotkeyContext.SinglePlayer) || bindings.Get(old.Id) != null) failures.Add("Dynamic: retired callback remained live.");
                storage.Block.Set(); HotkeyCoreChecks.Wait(bindings, () => !bindings.Busy);
                if (bindings.Get(old.Id) != null || registry.Find(old.Id) != null) failures.Add("Dynamic: delayed save resurrected removed action.");
                if (!bindings.TryRemoveRetired(dynamic, old.Id, out save, out error)) throw new Exception(error);
                HotkeyCoreChecks.Wait(bindings, () => !bindings.Busy);
                var disk = HotkeyDocument.Decode(storage.Bytes);
                if (disk.Entries.Count != 2 || disk.Entries[1].Key != "future.other") failures.Add("Dynamic: deletion left tombstone or lost unknown binding.");
                if (bindings.TryRemoveRetired(dynamic, "core", out save, out error)) failures.Add("Dynamic: owner deleted core binding.");
                var replacement = new HotkeyAction(old.Id, "替换", HotkeyContext.Gameplay, () => true, () => newCalls++);
                dynamic.TryReplace(new[] { replacement }, out reason);
                HotkeyCoreChecks.Set(bindings, replacement.Id, "J", (a,c) => null);
                input.Update(keys, true); bindings.Dispatch(input, HotkeyContext.SinglePlayer, true);
                if (newCalls != 0) failures.Add("Dynamic: replacement replayed held key.");
                if (old.Invoke(HotkeyContext.SinglePlayer)) failures.Add("Dynamic: old generation revived on ID reuse.");
                long revision = registry.Revision;
                if (dynamic.TryReplace(new[] { replacement, replacement }, out reason) || registry.Revision != revision) failures.Add("Dynamic: invalid snapshot partially published.");
                var full = new List<HotkeyAction>();
                for (int i = 0; i < 256; i++) full.Add(new HotkeyAction("quick.use." + i, "项", HotkeyContext.Gameplay, () => true, () => { }));
                if (dynamic.TryReplace(full, out reason) || registry.Find(old.Id) != replacement) failures.Add("Dynamic: capacity rejection changed active snapshot.");
                var removesLater = new HotkeyAction("quick.use.early", "先删", HotkeyContext.Gameplay, () => true, () => dynamic.TryReplace(new HotkeyAction[0], out reason));
                dynamic.TryReplace(new[] { removesLater, replacement }, out reason);
                HotkeyCoreChecks.Set(bindings, removesLater.Id, "I", (a,c) => null);
                Array.Clear(keys, 0, keys.Length); input.Update(keys, true); keys[73] = keys[74] = keys[75] = true; input.Update(keys, true);
                bindings.Dispatch(input, HotkeyContext.SinglePlayer, true);
                if (newCalls != 0 || coreCalls != 2) failures.Add("Dynamic: dispatch snapshot called a retired action or skipped independent core.");
            }
        }
    }
}
