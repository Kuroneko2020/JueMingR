using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using JueMingR.Infrastructure.Settings;
using JueMingR.Platform.Hotkeys;
using JueMingR.Platform.Settings;
using JueMingR.TerrariaHost.F5;
using JueMingR.TerrariaHost.Hotkeys;
using JueMingR.TerrariaHost.Input;
using Microsoft.Xna.Framework.Input;
using Terraria.GameInput;

namespace Terraria
{
    internal static class HotkeyPopupChecks
    {
        private static readonly object font = new object();
        internal static void Run()
        {
            Main.SampleMiddle = Main.SampleX1 = Main.SampleX2 = false;
            int requests = 0;
            var target = new HotkeyAction("test.command", "独立动作", HotkeyContext.Gameplay, () => true, () => requests++);
            var profile = new PlayerInputProfile(); PlayerInput.CurrentProfile = profile;
            var map = profile.InputModes[InputMode.Keyboard].KeyStatus;
            map["SmartCursor"] = new List<string> { "LeftControl" };
            Check(VanillaHotkeyConflicts.Check(target, Parse("LeftControl+K")) != null, "modifier itself conflicts with native SmartCursor");
            Check(VanillaHotkeyConflicts.Check(target, Parse("RightControl+K")) == null, "side identity leaves free RightControl");
            Check(VanillaHotkeyConflicts.Check(target, Parse("F9")) == null && VanillaHotkeyConflicts.Check(target, Parse("RightShift+F9")) != null, "F9 fixed rule requires Shift");
            foreach (string key in new[] { "F7", "F8", "F10", "F11", "Enter", "RightAlt+Enter" }) Check(VanillaHotkeyConflicts.Check(target, Parse(key)) != null, "actual fixed handler " + key);
            map["MapStyle"] = new List<string> { "Tab" };
            Check(VanillaHotkeyConflicts.Check(target, Parse("Tab")) != null && VanillaHotkeyConflicts.Check(target, Parse("RightAlt+Tab")) == null, "Alt Tab excluded by actual native mapping condition");
            Social.SocialAPI.Mode = Social.SocialMode.Steam;
            Check(VanillaHotkeyConflicts.Check(target, Parse("RightShift+Tab")) == null, "Steam Shift Tab mapping exception");
            Social.SocialAPI.Mode = Social.SocialMode.None;
            Check(VanillaHotkeyConflicts.Check(target, Parse("RightShift+Tab")) != null, "non Steam Shift Tab still maps");
            map["DpadRadial1"] = new List<string> { "K" }; Check(VanillaHotkeyConflicts.Check(target, Parse("K")) != null, "Dpad keyboard trigger can change held item");
            map.Clear(); map["MenuUp"] = new List<string> { "K" }; Check(VanillaHotkeyConflicts.Check(target, Parse("K")) == null, "menu-only key is not globally reserved");
            map["LockOn"] = new List<string> { "K" }; LockOnHelper.ForceUsability = true;
            Check(VanillaHotkeyConflicts.Check(target, Parse("K")) != null, "forced native lock-on context"); LockOnHelper.ForceUsability = false;
            map.Clear(); PlayerInput.CurrentProfile = null;
            Check(VanillaHotkeyConflicts.Check(target, Parse("K")) != null, "unavailable active configuration reports incomplete check");
            PlayerInput.CurrentProfile = profile;

            string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "JueMingR.Hotkeys.Popup-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            try
            {
                var registry = new HotkeyRegistry(); registry.Register(target);
                registry.Register(new HotkeyAction("test.other", "另一动作", HotkeyContext.Gameplay, () => true, () => { }));
                using (var owner = new HotkeyBindings(registry, new FilePreferenceStorage(Path.Combine(root, "hotkeys.json"))))
                {
                    Wait(owner, () => owner.Loaded);
                    var input = new HostInputState(() => new IntPtr(1), () => new IntPtr(1));
                    var popup = new HotkeyPopup(owner, registry, input); var anchor = new F5Rect(740, 150, 22, 30);
                    int extraMouse = -1;
                    Action<int[], bool, float, float> step = (keys, left, x, y) =>
                    {
                        input.BeginUpdate();
                        PlayerInput.MouseInfo = new MouseState((int)x, (int)y, 0,
                            left ? ButtonState.Pressed : ButtonState.Released,
                            extraMouse == 258 ? ButtonState.Pressed : ButtonState.Released,
                            extraMouse == 257 ? ButtonState.Pressed : ButtonState.Released,
                            extraMouse == 259 ? ButtonState.Pressed : ButtonState.Released,
                            extraMouse == 260 ? ButtonState.Pressed : ButtonState.Released);
                        input.AfterMapping(); var keyboard = new Keys[keys.Length]; for (int i = 0; i < keys.Length; i++) keyboard[i] = (Keys)keys[i];
                        Main.keyState = new KeyboardState(keyboard); input.AfterKeyboardRefresh();
                        popup.Process(true, 9, x, y, popup.Layout.Matches(800, 600, font)); popup.Prepare(800, 600, font, Measure);
                    };
                    step(new int[0], false, 0, 0);
                    popup.Click("test.command", anchor, 1, 9, 100); Check(!popup.Visible, "single click cannot open binding window");
                    popup.Click("test.command", anchor, 1, 9, 200); popup.Prepare(800, 600, font, Measure);
                    Check(popup.Visible && popup.Layout.Panel.Right <= 788 && popup.Layout.Panel.Bottom <= 588, "double click opens viewport-clamped window");
                    Action<int> click = index => { F5Rect r = popup.Layout.Buttons[index].Rect.Offset(popup.Layout.Panel.X, popup.Layout.Panel.Y); step(new int[0], true, r.X + 3, r.Y + 3); step(new int[0], false, r.X + 3, r.Y + 3); };
                    click(0); step(new int[0], false, 0, 0); Check(popup.Capturing && owner.Get(target.Id) == null, "start activation click never binds Mouse1");
                    step(new[] { 163, 161, 165 }, false, 0, 0); Check(popup.Capturing, "pure modifiers wait for main key");
                    step(new[] { 163, 161, 165, 75 }, false, 0, 0); Wait(owner, () => !owner.Busy);
                    Check(owner.Get(target.Id)?.Text == "RightControl+RightShift+RightAlt+K" && popup.Visible && !popup.Capturing, "three modifiers auto-save through real file and keep popup");
                    Check(input.Hotkeys.IsSuppressed(75), "captured primary retained as physical tail"); step(new int[0], false, 0, 0);
                    // Owner decision: native overlaps warn but still complete the
                    // real capture/file/dispatch path, including modifier tokens.
                    map["SmartCursor"] = new List<string> { "LeftControl" };
                    map["SmartSelect"] = new List<string> { "LeftShift" };
                    map["QuickHeal"] = new List<string> { "J" };
                    map["MouseLeft"] = new List<string> { "Mouse1" };
                    foreach (string text in new[] { "LeftControl+K", "LeftShift+K", "LeftControl+LeftShift+K", "J", "F7", "Mouse1" })
                    {
                        click(0);
                        var chord = Parse(text); var keys = new List<int>();
                        for (int i = 0; i < 6; i++) if (((int)chord.Modifiers & (1 << i)) != 0) keys.Add(HotkeyChord.ModifierCode(i));
                        if (chord.MainKey < 256) keys.Add(chord.MainKey);
                        step(keys.ToArray(), chord.MainKey == 256, 2, 2);
                        Wait(owner, () => !owner.Busy);
                        step(new int[0], false, 2, 2);
                        Check(owner.Get(target.Id)?.Text == text && owner.CompletionSucceeded && popup.Status.Contains("已保存") && popup.Status.Contains("原版"), "native overlap warns and saves through popup: " + text);
                        var saved = HotkeyDocument.Decode(File.ReadAllBytes(Path.Combine(root, "hotkeys.json")));
                        Check(saved.Entries[0].Value == text, "native overlap persisted exact candidate: " + text);
                        int before = requests;
                        step(keys.ToArray(), chord.MainKey == 256, 2, 2);
                        owner.Dispatch(input.Hotkeys, HotkeyContext.SinglePlayer, true);
                        Check(requests == before + 1, "warned binding dispatches after release: " + text);
                        step(new int[0], false, 2, 2);
                    }
                    PlayerInput.CurrentProfile = null;
                    click(0); step(new[] { 75 }, false, 2, 2); Wait(owner, () => !owner.Busy); step(new int[0], false, 2, 2);
                    Check(owner.Get(target.Id)?.Text == "K" && popup.Status.Contains("已保存") && popup.Status.Contains("无法"), "unavailable native profile warns without refusing save");
                    PlayerInput.CurrentProfile = profile; map.Clear();
                    click(0); step(new int[0], false, 0, 0); step(new[] { 74, 75 }, false, 0, 0);
                    Check(!popup.Capturing && popup.Status.Contains("多个") && owner.Get(target.Id).MainKey == 75, "multiple new primaries rejected without enum winner");
                    step(new int[0], false, 0, 0); click(0); step(new int[0], false, 0, 0); step(new[] { 116 }, false, 0, 0);
                    Check(popup.Status.Contains("F5") && popup.Visible && !popup.Capturing, "F5 has explicit reserved feedback");
                    step(new int[0], false, 0, 0); click(0); step(new int[0], false, 0, 0); step(new[] { 27 }, false, 0, 0);
                    Check(popup.Visible && !popup.Capturing && owner.Get(target.Id) != null, "Esc cancels without clearing or closing");
                    step(new int[0], false, 0, 0); click(0); step(new int[0], false, 0, 0); click(1); Wait(owner, () => !owner.Busy);
                    Check(owner.Get(target.Id) == null && popup.Visible && !popup.Capturing, "clear control takes precedence over mouse capture");
                    step(new int[0], false, 0, 0); // Deliver save feedback and reflow before a new gesture.
                    click(0); step(new[] { 8 }, false, 0, 0); Wait(owner, () => !owner.Busy);
                    Check(!popup.Capturing && owner.Get(target.Id)?.MainKey == 8, "first new primary immediately after start-button release is captured");
                    step(new int[0], false, 0, 0);
                    float bodyX = popup.Layout.Panel.X + 18, bodyY = popup.Layout.Panel.Y + 18;
                    step(new int[0], true, bodyX, bodyY); step(new int[0], true, 2, 2);
                    Check(input.Hotkeys.HasSuppressedKeys && !PlayerInput.Triggers.Current.MouseLeft, "popup body press retains its physical tail outside panel");
                    step(new int[0], false, 2, 2);
                    for (int mouse = 256; mouse <= 260; mouse++)
                    {
                        click(0);
                        extraMouse = mouse;
                        step(new int[0], mouse == 256, 2, 2);
                        Wait(owner, () => !owner.Busy);
                        Check(owner.Get(target.Id)?.MainKey == mouse && !popup.Capturing, "physical five-button capture " + mouse);
                        int before = requests;
                        owner.Dispatch(input.Hotkeys, HotkeyContext.SinglePlayer, true);
                        step(new int[0], mouse == 256, 2, 2);
                        owner.Dispatch(input.Hotkeys, HotkeyContext.SinglePlayer, true);
                        Check(requests == before, "captured mouse down and held tail cannot dispatch " + mouse);
                        extraMouse = -1;
                        step(new int[0], false, 2, 2);
                        owner.Dispatch(input.Hotkeys, HotkeyContext.SinglePlayer, true);
                        extraMouse = mouse;
                        step(new int[0], mouse == 256, 2, 2);
                        owner.Dispatch(input.Hotkeys, HotkeyContext.SinglePlayer, true);
                        Check(requests == before + 1, "released mouse re-press dispatches exactly one command " + mouse);
                        extraMouse = -1;
                        step(new int[0], false, 2, 2);
                    }
                    for (int mouse = 258; mouse <= 260; mouse++)
                    {
                        extraMouse = mouse;
                        PlayerInput.Triggers.Current.KeyStatus["QuickHeal"] = true;
                        step(new int[0], false, bodyX, bodyY);
                        Check(!PlayerInput.Triggers.Current.KeyStatus["QuickHeal"], "non-capturing popup consumes first mapped side/middle action " + mouse);
                        extraMouse = -1;
                        step(new int[0], false, 2, 2);
                    }
                    click(2); Check(!popup.Visible, "close control terminates popup");
                }
            }
            finally
            {
                string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!root.StartsWith(parent, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(root).StartsWith("JueMingR.Hotkeys.Popup-", StringComparison.Ordinal)) throw new Exception("Unsafe isolated test cleanup path.");
                Directory.Delete(root, true);
            }
            CheckCompletionOwnership(false); CheckCompletionOwnership(true);
            Console.WriteLine("PASS: hotkey actual-profile conflicts and production popup/input/file checks.");
        }
        private static void CheckCompletionOwnership(bool fail)
        {
            var registry = new HotkeyRegistry();
            registry.Register(new HotkeyAction("test.a", "前目标", HotkeyContext.Gameplay, () => true, () => { }));
            registry.Register(new HotkeyAction("test.b", "后目标", HotkeyContext.Gameplay, () => true, () => { }));
            var storage = new GatedStorage { Fail = fail };
            PlayerInput.CurrentProfile = new PlayerInputProfile();
            PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard].KeyStatus["SmartCursor"] = new List<string> { "LeftControl" };
            using (var owner = new HotkeyBindings(registry, storage))
            {
                try
                {
                    Wait(owner, () => owner.Loaded);
                    var input = new HostInputState(() => new IntPtr(1), () => new IntPtr(1));
                    var popup = new HotkeyPopup(owner, registry, input);
                    Action<Keys[], bool, float, float> frame = (keys, left, x, y) =>
                    {
                        input.BeginUpdate();
                        PlayerInput.MouseInfo = new MouseState((int)x, (int)y, 0, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                        input.AfterMapping(); Main.keyState = new KeyboardState(keys); input.AfterKeyboardRefresh();
                        popup.Process(true, 0, x, y, popup.Layout.Matches(800, 600, font));
                        popup.Prepare(800, 600, font, Measure);
                    };
                    frame(new Keys[0], false, 0, 0);
                    var anchor = new F5Rect(100, 100, 22, 30);
                    popup.Click("test.a", anchor, 1, 0, 100); popup.Click("test.a", anchor, 1, 0, 200);
                    popup.Prepare(800, 600, font, Measure);
                    var button = popup.Layout.Buttons[0].Rect.Offset(popup.Layout.Panel.X, popup.Layout.Panel.Y);
                    frame(new Keys[0], true, button.X + 3, button.Y + 3);
                    frame(new Keys[0], false, button.X + 3, button.Y + 3);
                    frame(new[] { Keys.LeftControl, Keys.K }, false, 0, 0);
                    Check(storage.Entered.WaitOne(5000) && owner.Busy && popup.Status.Contains("SmartCursor"), "real worker accepted warned A before window switch");
                    popup.Close();
                    popup.Click("test.b", anchor, 1, 0, 300); popup.Click("test.b", anchor, 1, 0, 400);
                    popup.Prepare(800, 600, font, Measure);
                    Check(!popup.Status.Contains("已保存"), "B never displays A result");
                    storage.Release.Set(); Wait(owner, () => !owner.Busy);
                    frame(new Keys[0], false, 0, 0);
                    Check((fail ? owner.Get("test.a") == null : owner.Get("test.a")?.Text == "LeftControl+K") && owner.Get("test.b") == null, "submitted A completes after close without editing B");
                    Check(popup.Target == "test.b" && popup.Status.Contains("请选择开始录入") && !popup.Status.Contains("已保存") && !popup.Status.Contains("SmartCursor"), "B becomes ready after A finishes without inheriting A warning/result or stuck saving");
                }
                finally { storage.Release.Set(); }
            }
        }
        private sealed class GatedStorage : IPreferenceStorage
        {
            internal bool Fail;
            internal readonly ManualResetEvent Entered = new ManualResetEvent(false), Release = new ManualResetEvent(false);
            public PreferenceReadResult Read() { return new PreferenceReadResult(PreferenceReadStatus.Missing, null, null, null); }
            public PreferenceWriteResult Write(string identity, byte[] bytes)
            {
                Entered.Set();
                if (!Release.WaitOne(5000)) throw new TimeoutException("Controlled save was not released.");
                return Fail ? new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, null, "controlled failure") : new PreferenceWriteResult(PreferenceWriteStatus.Saved, "saved", null);
            }
            public void Dispose() { Entered.Dispose(); Release.Dispose(); }
        }
        private static F5Size Measure(string text, float scale) { return new F5Size(text.Length * 10 * scale, 24 * scale); }
        private static HotkeyChord Parse(string text) { HotkeyChord c; string r; if (!HotkeyChord.TryParse(text, out c, out r)) throw new Exception(r); return c; }
        private static void Wait(HotkeyBindings owner, Func<bool> condition) { var watch = Stopwatch.StartNew(); while (!condition() && watch.ElapsedMilliseconds < 5000) { owner.Poll(); Thread.Sleep(1); } Check(condition(), "worker completed"); }
        private static void Check(bool value, string reason) { if (!value) throw new InvalidOperationException("Hotkey: " + reason); }
    }
}
