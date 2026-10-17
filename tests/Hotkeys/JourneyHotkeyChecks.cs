using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using JueMingR.Features.HiddenFeatures;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Hotkeys;

namespace Terraria
{
    internal static class JourneyHotkeyChecks
    {
        internal static void Run()
        {
            CheckSavedBinding(); CheckLockedConflict();
            var registry = new HotkeyRegistry(); var access = new HiddenFeatureAccess(); var owner = new JourneyUiOwner();
            var entry = new JourneyHotkeys(registry, access, owner, () => owner.Toggle());
            Unlock(access); entry.Refresh();
            using (var storage = new JourneyStorage(HotkeyDocument.Empty))
            using (var bindings = new HotkeyBindings(registry, storage))
            {
                Wait(bindings);
                JourneyUiChecks.Require(bindings.Get(JourneyControls.ActionId) == null && !owner.IsActive, "fresh unlocked binding defaults to unbound and OFF");
            }
            Console.WriteLine("PASS: Journey saved binding inert while locked, activation edge, no repeated registration and close readiness.");
        }
        private static void CheckSavedBinding()
        {
            var registry = new HotkeyRegistry(); var access = new HiddenFeatureAccess(); var owner = new JourneyUiOwner();
            var entry = new JourneyHotkeys(registry, access, owner, () => owner.Toggle());
            var document = new HotkeyDocument(new[] { new KeyValuePair<string, string>(JourneyControls.ActionId, "J") });
            using (var storage = new JourneyStorage(document))
            using (var bindings = new HotkeyBindings(registry, storage))
            {
                Wait(bindings);
                var input = new HotkeyInput(); var keys = new bool[HotkeyChord.KeyCount]; input.Update(keys, true);
                keys[74] = true; input.Update(keys, true); bindings.Dispatch(input, HotkeyContext.SinglePlayer, true);
                JourneyUiChecks.Require(owner.ToggleCalls == 0 && bindings.Get(JourneyControls.ActionId) == null && registry.Find(JourneyControls.ActionId) == null && input.IsNew(74), "locked saved key has no dispatch/config/conflict entry and retains ordinary input");
                long lockedRevision = registry.Revision;
                for (int i = 0; i < 100; i++) entry.Refresh();
                JourneyUiChecks.Require(registry.Revision == lockedRevision && storage.Writes == 0, "locked stable updates neither rebuild nor save");
                Unlock(access); entry.Refresh();
                JourneyUiChecks.Require(registry.Revision == lockedRevision + 1 && registry.Find(JourneyControls.ActionId).CanConfigure && bindings.Get(JourneyControls.ActionId).Text == "J", "unlock publishes exactly one action and preserves its saved binding");
                long unlockedRevision = registry.Revision;
                for (int i = 0; i < 100; i++) entry.Refresh();
                JourneyUiChecks.Require(registry.Revision == unlockedRevision && storage.Writes == 0, "unlocked stable updates do not re-register or save");
                keys[74] = false; input.Update(keys, true); keys[74] = true; input.Update(keys, true);
                bindings.Dispatch(input, HotkeyContext.Multiplayer, true);
                JourneyUiChecks.Require(owner.ToggleCalls == 0, "Journey action stays single-player only");
                bindings.Dispatch(input, HotkeyContext.SinglePlayer, true);
                JourneyUiChecks.Require(owner.ToggleCalls == 1 && owner.IsActive && owner.IsMenuOpen, "unlocked saved binding calls real toggle command boundary");
                owner.Ready = false;
                keys[74] = false; input.Update(keys, true); keys[74] = true; input.Update(keys, true);
                bindings.Dispatch(input, HotkeyContext.SinglePlayer, true);
                JourneyUiChecks.Require(owner.ToggleCalls == 2 && !owner.IsActive, "loss of enable readiness never blocks explicit close");
                keys[74] = false; input.Update(keys, true); keys[74] = true; input.Update(keys, true);
                bindings.Dispatch(input, HotkeyContext.SinglePlayer, true);
                JourneyUiChecks.Require(owner.ToggleCalls == 2, "not-ready OFF state cannot enable");
            }
        }
        private static void CheckLockedConflict()
        {
            int calls = 0; var registry = new HotkeyRegistry();
            registry.Register(new HotkeyAction("ordinary.toggle", "普通功能", HotkeyContext.Gameplay, () => true, () => calls++));
            var owner = new JourneyUiOwner(); new JourneyHotkeys(registry, new HiddenFeatureAccess(), owner, () => owner.Toggle());
            using (var storage = new JourneyStorage(new HotkeyDocument(new[] {
                new KeyValuePair<string, string>(JourneyControls.ActionId, "J"), new KeyValuePair<string, string>("ordinary.toggle", "J") })))
            using (var bindings = new HotkeyBindings(registry, storage))
            {
                Wait(bindings); var input = new HotkeyInput(); var keys = new bool[HotkeyChord.KeyCount]; input.Update(keys, true);
                keys[74] = true; input.Update(keys, true); bindings.Dispatch(input, HotkeyContext.SinglePlayer, true);
                JourneyUiChecks.Require(calls == 1 && !bindings.Protected && owner.ToggleCalls == 0, "locked orphan does not reserve or disable the ordinary action sharing its key");
            }
        }
        private static void Unlock(HiddenFeatureAccess access)
        { for (int i = 0; i < 5; i++) access.TryCompleteTitleClick(100 + i * 100, i + 1); }
        private static void Wait(HotkeyBindings bindings)
        {
            var timer = Stopwatch.StartNew();
            while (!bindings.Loaded && timer.ElapsedMilliseconds < 2000) { bindings.Poll(); Thread.Sleep(1); }
            JourneyUiChecks.Require(bindings.Loaded && !bindings.Protected, "isolated in-memory bindings loaded");
        }
        private sealed class JourneyStorage : IPreferenceStorage
        {
            private readonly byte[] bytes;
            internal int Writes;
            internal JourneyStorage(HotkeyDocument document) { bytes = HotkeyDocument.Encode(document); }
            public PreferenceReadResult Read() { return new PreferenceReadResult(PreferenceReadStatus.Loaded, bytes, "journey-ui", null); }
            public PreferenceWriteResult Write(string expectedIdentity, byte[] contents)
            { Writes++; return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, "journey-ui", "Unexpected write in read/registration test"); }
            public void Dispose() { }
        }
    }
}
