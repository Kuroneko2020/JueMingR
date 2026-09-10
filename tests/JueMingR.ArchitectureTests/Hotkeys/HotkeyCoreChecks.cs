using System;
using System.Collections.Generic;
using JueMingR.Platform.Settings;
using JueMingR.Platform.Hotkeys;
using System.Threading;
using System.Diagnostics;

namespace JueMingR.ArchitectureTests
{
    internal static class HotkeyCoreChecks
    {
        internal static void Check(IList<string> failures)
        {
            string[] valid = { "RightControl+RightShift+RightAlt+K", "LeftControl+RightControl+Back", "OemPlus", "Add", "NumLock", "CapsLock", "Scroll", "F24", "OemClear", "Apps", "BrowserBack", "Mouse1", "Mouse2", "Mouse3", "Mouse4", "Mouse5" };
            string[] invalid = { "Control+K", "Shift+K", "LeftControl+LeftControl+K", "LeftControl+RightControl+LeftShift+RightAlt+K", "LeftControl", "K+J", "K+Mouse1", "WheelUp", "WheelDown", "HorizontalWheel", "Escape", "F5", "LeftAlt+F5", "LeftWindows+K", "Sleep", "ProcessKey", "OemCopy", "NumPadEnter", "Mouse6", "999" };
            HotkeyChord chord; string reason;
            foreach (string text in valid) if (!HotkeyChord.TryParse(text, out chord, out reason)) failures.Add("Hotkeys valid chord rejected: " + text);
            foreach (string text in invalid) if (HotkeyChord.TryParse(text, out chord, out reason)) failures.Add("Hotkeys invalid/unreportable chord accepted: " + text);
            if (Parse("RightAlt+LeftControl+K").Text != "LeftControl+RightAlt+K") failures.Add("Hotkeys: canonical order lost side identity.");
            ExerciseDispatch(failures); ExerciseWarnings(failures); ExerciseSaving(failures); ExerciseProtectedEntries(failures);
        }
        internal static HotkeyChord Parse(string value)
        { HotkeyChord chord; string reason; if (!HotkeyChord.TryParse(value, out chord, out reason)) throw new Exception(value + ": " + reason); return chord; }
        internal static void Wait(HotkeyBindings owner, Func<bool> done)
        { var time = Stopwatch.StartNew(); while (!done() && time.ElapsedMilliseconds < 5000) { owner.Poll(); Thread.Sleep(1); } if (!done()) throw new Exception("Hotkey worker failed to finish controlled test."); }
        private static void ExerciseDispatch(IList<string> failures)
        {
            int bare = 0, ctrl = 0, three = 0, command = 0, vanillaReads = 0;
            var registry = new HotkeyRegistry();
            // Registration order intentionally differs from binding order; the
            // non-toggle command uses the same real dispatch as all other actions.
            registry.Register(new HotkeyAction("test.command", "独立命令", HotkeyContext.Gameplay, () => true, () => command += 7));
            registry.Register(new HotkeyAction("test.three", "三修饰", HotkeyContext.Gameplay, () => true, () => three++));
            registry.Register(new HotkeyAction("test.ctrl", "修饰", HotkeyContext.Gameplay, () => true, () => ctrl++));
            registry.Register(new HotkeyAction("test.bare", "主键", HotkeyContext.Gameplay, () => true, () => bare++));
            using (var owner = new HotkeyBindings(registry, new MemoryStorage()))
            {
                Wait(owner, () => owner.Loaded);
                Func<HotkeyAction, HotkeyChord, string> check = (a, c) => { vanillaReads++; return null; };
                Set(owner, "test.bare", "K", check); Set(owner, "test.ctrl", "RightControl+K", check);
                Set(owner, "test.three", "RightControl+RightShift+RightAlt+K", check); Set(owner, "test.command", "J", check);
                int reads = vanillaReads;
                var input = new HotkeyInput(); var sample = new bool[HotkeyChord.KeyCount];
                Action<bool, int[]> step = (permission, keys) => { Array.Clear(sample, 0, sample.Length); foreach (int key in keys) sample[key] = true; input.Update(sample, true); owner.Dispatch(input, HotkeyContext.SinglePlayer, permission); };
                step(true, new int[0]); step(true, new[] { 75 }); step(true, new[] { 75, 163 }); step(true, new[] { 75, 163, 161, 165 }); step(true, new[] { 75 });
                if (bare != 1 || ctrl != 0 || three != 0) failures.Add("Hotkeys: modifier changes fabricated a primary edge.");
                step(true, new int[0]); step(true, new[] { 75, 163, 87 });
                if (bare != 1 || ctrl != 1) failures.Add("Hotkeys: exact modifiers or unrelated held W matching failed.");
                step(true, new int[0]); step(true, new[] { 75, 163, 161, 165 });
                if (bare != 1 || ctrl != 1 || three != 1) failures.Add("Hotkeys: exact three modifier command overlapped a subset.");
                step(true, new int[0]); step(false, new[] { 75 }); step(true, new[] { 75 });
                if (bare != 1) failures.Add("Hotkeys: text/UI blocked press replayed on permission gain.");
                step(true, new int[0]); step(true, new[] { 75, 74 });
                if (bare != 2 || command != 7) failures.Add("Hotkeys: independent primary edges or non-toggle extension lost dispatch.");
                input.SuppressHeld(); Array.Clear(sample, 0, sample.Length); input.Update(sample, false);
                if (!input.HasSuppressedKeys || !input.IsDown(75)) failures.Add("Hotkeys: synthetic focus loss retired a physical tail.");
                step(true, new[] { 75, 74 }); step(true, new int[0]); step(true, new[] { 74 });
                if (command != 14) failures.Add("Hotkeys: capture tail either replayed or never released.");
                step(true, new int[0]); step(true, new[] { 91, 75 });
                if (bare != 2) failures.Add("Hotkeys: Win chord dispatched gameplay.");
                if (vanillaReads != reads) failures.Add("Hotkeys: runtime monitored vanilla bindings.");
                if (owner.Validate("test.command", Parse("K")) == null) failures.Add("Hotkeys: internal duplicate did not reserve a toggle action.");
                Set(owner, "test.bare", "K", (a, c) => "changed vanilla");
                if (!owner.Message.Contains("changed vanilla")) failures.Add("Hotkeys: idempotent edit lost current vanilla warning.");
            }
        }
        private static void ExerciseWarnings(IList<string> failures)
        {
            var registry = new HotkeyRegistry();
            registry.Register(new HotkeyAction("a", "First action", HotkeyContext.Gameplay, () => true, () => { }));
            registry.Register(new HotkeyAction("b", "Second action", HotkeyContext.Gameplay, () => true, () => { }));
            var storage = new MemoryStorage(); int reads = 0;
            using (var owner = new HotkeyBindings(registry, storage))
            {
                Wait(owner, () => owner.Loaded); Set(owner, "a", "K", (a, c) => null);
                storage.Block.Reset(); long command, ignored; string reason;
                try
                {
                    if (!owner.TrySet("a", Parse("LeftControl+J"), (a, c) => { reads++; return "native overlap A"; }, out command, out reason))
                    { failures.Add("Hotkeys: native warning refused candidate."); return; }
                    if (!owner.Busy || owner.Get("a").Text != "K" || !owner.Message.Contains("native overlap A") || owner.Message.Contains("已保存"))
                        failures.Add("Hotkeys: warning hid pending state or activated early.");
                    if (owner.Feedback.Kind != HotkeyFeedbackKind.Saving || owner.Feedback.Advisory != "native overlap A") failures.Add("Hotkeys: pending feedback lost typed status/advisory.");
                    if (owner.TrySet("b", Parse("L"), (a, c) => { reads++; return "native overlap B"; }, out ignored, out reason) || reads != 1 || !owner.Message.Contains("native overlap A"))
                        failures.Add("Hotkeys: busy rejection overwrote accepted warning or read native profile.");
                }
                finally { storage.Block.Set(); }
                Wait(owner, () => !owner.Busy);
                if (owner.Feedback.Kind != HotkeyFeedbackKind.Saved) failures.Add("Hotkeys: reliable completion did not publish Saved.");
                if (!owner.CompletionSucceeded || owner.CompletionId != command || owner.Get("a").Text != "LeftControl+J" || !owner.Message.Contains("native overlap A"))
                    failures.Add("Hotkeys: successful save lost candidate or warning identity.");
                byte[] saved = storage.Bytes;
                if (owner.TrySet("b", Parse("LeftControl+J"), (a, c) => { reads++; return "native overlap B"; }, out ignored, out reason) || reads != 1 || !reason.Contains("First action") || storage.Bytes != saved)
                    failures.Add("Hotkeys: internal duplicate no longer blocks before warning/read/write.");
                Set(owner, "a", "LeftControl+J", (a, c) => "new native mapping");
                if (!owner.Message.Contains("new native mapping") || owner.Message.Contains("native overlap A")) failures.Add("Hotkeys: idempotent submit retained stale warning.");
                Set(owner, "a", null, (a, c) => { reads++; return "clear must not check"; });
                if (owner.Feedback.Kind != HotkeyFeedbackKind.Cleared) failures.Add("Hotkeys: reliable clear did not publish Cleared.");
                if (reads != 1 || owner.Get("a") != null || owner.Message.Contains("native")) failures.Add("Hotkeys: clearing consulted native profile or retained warning.");
                Set(owner, "a", "J", (a, c) => { throw new InvalidOperationException("unavailable profile"); });
                if (!owner.Message.Contains("无法") || owner.Get("a").Text != "J") failures.Add("Hotkeys: unavailable native profile did not warn and save.");
                Set(owner, "a", "K", null);
                if (!owner.Message.Contains("无法") || owner.Get("a").Text != "K") failures.Add("Hotkeys: absent warning provider blocked or implied complete check.");
                Set(owner, "a", "L", (a, c) => null);
                if (owner.Message.Contains("无法")) failures.Add("Hotkeys: a clean submission inherited previous warning.");
            }
        }
        private static void ExerciseSaving(IList<string> failures)
        {
            var registry = new HotkeyRegistry(); registry.Register(new HotkeyAction("test.one", "一", HotkeyContext.Gameplay, () => true, () => { }));
            var storage = new MemoryStorage();
            using (var owner = new HotkeyBindings(registry, storage))
            {
                Wait(owner, () => owner.Loaded); Set(owner, "test.one", "K", (a, c) => null);
                storage.Block.Reset(); storage.Fail = true; long command; string reason;
                if (!owner.TrySet("test.one", Parse("J"), (a, c) => "native warning", out command, out reason)) throw new Exception(reason);
                if (owner.Get("test.one").Text != "K" || !owner.Busy) failures.Add("Hotkeys: pending write activated candidate early.");
                long ignored;
                if (owner.TrySet("test.one", Parse("L"), (a, c) => null, out ignored, out reason)) failures.Add("Hotkeys: busy worker overwrote accepted command.");
                storage.Block.Set(); Wait(owner, () => !owner.Busy);
                if (owner.Feedback.Kind != HotkeyFeedbackKind.Failed || owner.Feedback.Advisory != null) failures.Add("Hotkeys: failed feedback kept advisory or wrong severity.");
                if (owner.Get("test.one").Text != "K" || owner.CompletionSucceeded || owner.CompletionId != command || owner.CompletionAction != "test.one" || owner.Message.Contains("native warning")) failures.Add("Hotkeys: failed save lost old binding/command identity or kept advisory instead of failure.");
                storage.Fail = false;
                if (owner.Protected || !owner.TrySet("test.one", Parse("L"), (a, c) => null, out command, out reason))
                    failures.Add("Hotkeys: ordinary known precommit failure prevented a new recording/save.");
                else { Wait(owner, () => !owner.Busy); if (owner.Get("test.one").Text != "L") failures.Add("Hotkeys: ordinary failure recovery did not activate successful retry."); }
            }
            storage = new MemoryStorage { Fail = true, Unknown = true };
            using (var owner = new HotkeyBindings(registry, storage))
            {
                Wait(owner, () => owner.Loaded); long command; string reason;
                owner.TrySet("test.one", Parse("J"), (a, c) => "native warning", out command, out reason); Wait(owner, () => !owner.Busy);
                if (owner.Feedback.Kind != HotkeyFeedbackKind.Unconfirmed || owner.Feedback.Advisory != null) failures.Add("Hotkeys: unknown commit lost protected result category.");
                if (!owner.CommitUnconfirmed || !owner.Protected || owner.Get("test.one") != null || owner.Message.Contains("native warning")) failures.Add("Hotkeys: unknown commit pretended success/rollback or hid failure with warning.");
            }
        }
        private static void ExerciseProtectedEntries(IList<string> failures)
        {
            var registry = new HotkeyRegistry();
            foreach (string id in new[] { "a", "b", "c" }) registry.Register(new HotkeyAction(id, id, HotkeyContext.Gameplay, () => true, () => { }));
            var entries = new[] { new KeyValuePair<string,string>("a", "K"), new KeyValuePair<string,string>("b", "K"), new KeyValuePair<string,string>("c", "J"), new KeyValuePair<string,string>("future.action", "Mouse5") };
            using (var owner = new HotkeyBindings(registry, new MemoryStorage { Bytes = HotkeyDocument.Encode(new HotkeyDocument(entries)) }))
            {
                Wait(owner, () => owner.Loaded);
                if (!owner.Protected || owner.Get("a") != null || owner.Get("b") != null || owner.Get("c").Text != "J" || owner.Get("future.action") != null)
                    failures.Add("Hotkeys: duplicate load selected an order-dependent winner or dispatched unknown action.");
            }
            byte[] future = System.Text.Encoding.UTF8.GetBytes("{\"format\":\"JueMingR.Hotkeys\",\"version\":1,\"bindings\":[],\"future\":true}");
            foreach (string[] values in new[] { new[] { "K", "J" }, new[] { "J", "K" }, new[] { "NoSuchKey", "J" } })
            {
                var repeated = new[] { new KeyValuePair<string, string>("a", values[0]), new KeyValuePair<string, string>("a", values[1]), new KeyValuePair<string, string>("b", "J") };
                using (var owner = new HotkeyBindings(registry, new MemoryStorage { Bytes = HotkeyDocument.Encode(new HotkeyDocument(repeated)) }))
                {
                    Wait(owner, () => owner.Loaded);
                    if (!owner.Protected || owner.Get("a") != null || owner.Get("b")?.Text != "J")
                        failures.Add("Hotkeys: invalid duplicate action changed an independent binding according to row order.");
                }
            }
            using (var owner = new HotkeyBindings(registry, new MemoryStorage { Bytes = future }))
            { Wait(owner, () => owner.Loaded); if (!owner.Protected) failures.Add("Hotkeys: unknown fields were silently discarded."); }
            var round = HotkeyDocument.Decode(HotkeyDocument.Encode(new HotkeyDocument(entries).With("c", Parse("L"))));
            if (round.Entries.Count != 4 || round.Entries[3].Key != "future.action" || round.Entries[3].Value != "Mouse5") failures.Add("Hotkeys: unrelated edit lost unknown action.");
            var full = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < 256; i++) full.Add(new KeyValuePair<string, string>("future." + i, "K"));
            using (var owner = new HotkeyBindings(registry, new MemoryStorage { Bytes = HotkeyDocument.Encode(new HotkeyDocument(full)) }))
            {
                Wait(owner, () => owner.Loaded); long command; string reason;
                try { if (owner.TrySet("a", Parse("J"), (a, c) => null, out command, out reason) || String.IsNullOrEmpty(reason)) failures.Add("Hotkeys: full preserved document lacks bounded rejection."); }
                catch { failures.Add("Hotkeys: legal full document threw through UI instead of rejecting candidate."); }
            }
        }
        internal static void Set(HotkeyBindings owner, string id, string text, Func<HotkeyAction, HotkeyChord, string> check)
        { long command; string reason; if (!owner.TrySet(id, text == null ? null : Parse(text), check, out command, out reason)) throw new Exception(reason); Wait(owner, () => !owner.Busy); if (!owner.CompletionSucceeded) throw new Exception(owner.Message); }
        internal sealed class MemoryStorage : IPreferenceStorage
        {
            internal byte[] Bytes; internal bool Fail, Unknown; internal readonly ManualResetEvent Block = new ManualResetEvent(true);
            private int revision;
            public PreferenceReadResult Read() { return new PreferenceReadResult(Bytes == null ? PreferenceReadStatus.Missing : PreferenceReadStatus.Loaded, Bytes, "0", null); }
            public PreferenceWriteResult Write(string identity, byte[] contents)
            {
                if (!Block.WaitOne(5000)) throw new TimeoutException("Controlled write was not released.");
                if (identity != revision.ToString()) return new PreferenceWriteResult(PreferenceWriteStatus.Conflict, null, "conflict", false, true);
                if (Unknown) Bytes = contents;
                if (Fail) return new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, null, "controlled failure", Unknown, Unknown);
                Bytes = contents; return new PreferenceWriteResult(PreferenceWriteStatus.Saved, (++revision).ToString(), null);
            }
            public void Dispose() { Block.Dispose(); }
        }
    }
}
