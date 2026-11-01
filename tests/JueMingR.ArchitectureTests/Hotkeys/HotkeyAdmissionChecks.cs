using System;
using System.Collections.Generic;
using JueMingR.Platform.Hotkeys;

namespace JueMingR.ArchitectureTests
{
    internal static class HotkeyAdmissionChecks
    {
        internal static void Check(IList<string> failures)
        {
            int edit = 0, ordinary = 0, mouse = 0, single = 0;
            bool available = true;
            HotkeyChord received = null;
            var registry = new HotkeyRegistry();
            registry.Register(new HotkeyAction("test.edit", "编辑", HotkeyContext.Gameplay, () => available,
                (HotkeyChord chord) => { edit++; received = chord; }));
            registry.Register(new HotkeyAction("test.ordinary", "普通", HotkeyContext.Gameplay, () => true, () => ordinary++));
            registry.Register(new HotkeyAction("test.mouse", "鼠标", HotkeyContext.Gameplay, () => true, () => mouse++));
            registry.Register(new HotkeyAction("test.single", "单人", HotkeyContext.SinglePlayer, () => true, () => single++));
            using (var bindings = new HotkeyBindings(registry, new HotkeyCoreChecks.MemoryStorage()))
            {
                HotkeyCoreChecks.Wait(bindings, () => bindings.Loaded);
                HotkeyCoreChecks.Set(bindings, "test.edit", "LeftControl+K", null);
                HotkeyCoreChecks.Set(bindings, "test.ordinary", "J", null);
                HotkeyCoreChecks.Set(bindings, "test.mouse", "Mouse1", null);
                HotkeyCoreChecks.Set(bindings, "test.single", "L", null);
                var input = new HotkeyInput(); var sample = new bool[HotkeyChord.KeyCount]; input.Update(sample, true);
                Action<int[]> step = keys => { Array.Clear(sample, 0, sample.Length); foreach (int key in keys) sample[key] = true; input.Update(sample, true); };
                Func<HotkeyAction, HotkeyChord, bool> editOnly = (action, chord) => action.Id == "test.edit";
                step(new[] { 162, 75, 74 }); bindings.Dispatch(input, HotkeyContext.SinglePlayer, false, admit: editOnly);
                if (edit != 1 || ordinary != 0 || received == null || received.Text != "LeftControl+K") failures.Add("Hotkeys admission: a narrow owner failed to admit only its exact immutable matched chord.");
                // A held next sample carries no new primary, including when
                // the shell grants a different owner on that later mapping.
                int afterSameMapping = edit;
                step(new[] { 162, 75 }); bindings.Dispatch(input, HotkeyContext.SinglePlayer, false, admit: editOnly);
                if (edit != afterSameMapping) failures.Add("Hotkeys admission: held primary repeated through a narrow owner.");
                step(Array.Empty<int>()); step(new[] { 162, 164, 75 }); bindings.Dispatch(input, HotkeyContext.SinglePlayer, false, admit: editOnly);
                if (edit != afterSameMapping) failures.Add("Hotkeys admission: an extra modifier bypassed exact matching.");
                step(Array.Empty<int>()); step(new[] { 256, 74 });
                bindings.Dispatch(input, HotkeyContext.SinglePlayer, true, admit: (action, chord) => chord.MainKey != 256);
                if (mouse != 0 || ordinary != 1) failures.Add("Hotkeys admission: a world primary veto either leaked Mouse1 or widened to an unrelated primary.");
                available = false; step(Array.Empty<int>()); step(new[] { 162, 75 }); bindings.Dispatch(input, HotkeyContext.SinglePlayer, false, admit: editOnly);
                if (edit != afterSameMapping || bindings.Validate("test.ordinary", HotkeyCoreChecks.Parse("LeftControl+K")) == null)
                    failures.Add("Hotkeys admission: unavailable actions executed or stopped reserving their exact binding.");
                step(Array.Empty<int>()); step(new[] { 76 }); bindings.Dispatch(input, HotkeyContext.Multiplayer, false, admit: (action, chord) => true);
                if (single != 0) failures.Add("Hotkeys admission: an owner filter bypassed the registered context.");
                input.Update(sample, false); bindings.Dispatch(input, HotkeyContext.SinglePlayer, false, admit: (action, chord) => true);
                if (single != 0) failures.Add("Hotkeys admission: an unreliable sample reached an admitted action.");
            }
        }
    }
}
