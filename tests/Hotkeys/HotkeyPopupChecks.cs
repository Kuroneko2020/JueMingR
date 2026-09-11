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
            map["SmartSelect"] = new List<string> { "LeftShift" };
            var both = VanillaHotkeyConflicts.Check(target, Parse("LeftControl+LeftShift+K"));
            Check(both.Message.Contains("SmartCursor") && both.Message.Contains("SmartSelect"), "one submission reports both modifier overlaps");
            map.Remove("SmartSelect");
            Check(VanillaHotkeyConflicts.Check(target, Parse("LeftControl+K")).HasNotice, "modifier itself conflicts with native SmartCursor");
            Check(!VanillaHotkeyConflicts.Check(target, Parse("RightControl+K")).HasNotice, "side identity leaves free RightControl");
            Check(!VanillaHotkeyConflicts.Check(target, Parse("F9")).HasNotice && VanillaHotkeyConflicts.Check(target, Parse("RightShift+F9")).HasNotice, "F9 fixed rule requires Shift");
            foreach (string key in new[] { "F7", "F8", "F10", "F11", "Enter", "RightAlt+Enter" }) Check(VanillaHotkeyConflicts.Check(target, Parse(key)).HasNotice, "actual fixed handler " + key);
            map["MapStyle"] = new List<string> { "Tab" };
            Check(VanillaHotkeyConflicts.Check(target, Parse("Tab")).HasNotice && !VanillaHotkeyConflicts.Check(target, Parse("RightAlt+Tab")).HasNotice, "Alt Tab excluded by actual native mapping condition");
            Social.SocialAPI.Mode = Social.SocialMode.Steam;
            Check(!VanillaHotkeyConflicts.Check(target, Parse("RightShift+Tab")).HasNotice, "Steam Shift Tab mapping exception");
            Social.SocialAPI.Mode = Social.SocialMode.None;
            Check(VanillaHotkeyConflicts.Check(target, Parse("RightShift+Tab")).HasNotice, "non Steam Shift Tab still maps");
            map["DpadRadial1"] = new List<string> { "K" }; Check(VanillaHotkeyConflicts.Check(target, Parse("K")).HasNotice, "Dpad keyboard trigger can change held item");
            map.Clear(); map["MenuUp"] = new List<string> { "K" }; Check(!VanillaHotkeyConflicts.Check(target, Parse("K")).HasNotice, "menu-only key is not globally reserved");
            map["LockOn"] = new List<string> { "K" }; LockOnHelper.ForceUsability = true;
            Check(VanillaHotkeyConflicts.Check(target, Parse("K")).HasNotice, "forced native lock-on context"); LockOnHelper.ForceUsability = false;
            map.Clear(); PlayerInput.CurrentProfile = null;
            Check(VanillaHotkeyConflicts.Check(target, Parse("K")).HasNotice, "unavailable active configuration reports incomplete check");
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
                    Check(popup.Layout.Index(HotkeyPopupCommand.Help) >= 0 && popup.Layout.Index(HotkeyPopupCommand.Close) >= 0, "popup header owns help and close");
                    Check(!popup.Layout.Text.Exists(t => t.Text.Contains("最多三个") || t.Text.Contains("请选择")), "initial popup has no permanent rule wall or false result");
                    Check(popup.Layout.Panel.Width < 400 && popup.Layout.Panel.Height < 180, "empty popup sizes to visible content, without hidden help/result reserve");
                    Action<int> click = index => { F5Rect r = popup.Layout.Buttons[popup.Layout.Index((HotkeyPopupCommand)index)].Rect.Offset(popup.Layout.Panel.X, popup.Layout.Panel.Y); step(new int[0], true, r.X + 3, r.Y + 3); step(new int[0], false, r.X + 3, r.Y + 3); };
                    int languageReads = Localization.Language.Reads;
                    click(3); Check(popup.HelpVisible && !popup.Capturing && owner.Get(target.Id) == null, "help is a hover surface, never a candidate");
                    Check(Localization.Language.Reads == languageReads, "help/open do not query native action labels");
                    click(0); step(new int[0], false, 0, 0); Check(popup.Capturing && owner.Get(target.Id) == null, "start activation click never binds Mouse1");
                    click(3); Check(popup.Capturing && !owner.Busy, "help activation while recording cannot bind Mouse1");
                    var helpRect = popup.Layout.Buttons[popup.Layout.Index(HotkeyPopupCommand.Help)].Rect.Offset(popup.Layout.Panel.X, popup.Layout.Panel.Y);
                    step(new[] { 27 }, false, helpRect.X + 3, helpRect.Y + 3);
                    Check(!popup.Capturing && !owner.Busy, "hovering help must not block keyboard Escape");
                    step(new int[0], false, 0, 0); click(0);
                    step(new[] { 162, 163, 160, 161 }, false, 0, 0);
                    Check(popup.Capturing && popup.Layout.Keycaps.Count == 0 && popup.Status.Contains("多余修饰键"), "invalid modifier progress never creates more than four keycaps");
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
                    long otherCommand; string otherReason;
                    Check(owner.TrySet("test.other", Parse("J"), (a, c) => null, out otherCommand, out otherReason), "prepare internal conflict"); Wait(owner, () => !owner.Busy);
                    click(0); step(new[] { 74 }, false, 2, 2); step(new int[0], false, 2, 2);
                    Check(popup.Feedback.Kind == HotkeyFeedbackKind.Rejected && popup.Status.Contains("另一动作") && owner.Get(target.Id).Text == "K" && popup.Layout.Keycaps[0].Text == "K", "internal rejection shows the actual old binding");
                    var record = popup.Layout.Buttons[popup.Layout.Index(HotkeyPopupCommand.Record)].Rect.Offset(popup.Layout.Panel.X, popup.Layout.Panel.Y);
                    step(new int[0], true, record.X + 3, record.Y + 3);
                    popup.Prepare(800, 600, new object(), Measure);
                    step(new int[0], false, record.X + 3, record.Y + 3);
                    Check(!popup.Capturing && owner.Get(target.Id).Text == "K", "font change cancels an armed record action");
                    click(0); step(new int[0], false, 0, 0); step(new[] { 74, 75 }, false, 0, 0);
                    Check(!popup.Capturing && popup.Status.Contains("多个") && owner.Get(target.Id).MainKey == 75, "multiple new primaries rejected without enum winner");
                    step(new int[0], false, 0, 0); click(0); step(new int[0], false, 0, 0); step(new[] { 116 }, false, 0, 0);
                    Check(popup.Status.Contains("F5") && popup.Visible && !popup.Capturing, "F5 has explicit reserved feedback");
                    step(new int[0], false, 0, 0); click(0); step(new int[0], false, 0, 0); step(new[] { 27 }, false, 0, 0);
                    Check(popup.Visible && !popup.Capturing && owner.Get(target.Id) != null, "Esc cancels without clearing or closing");
                    step(new int[0], false, 0, 0); click(0); step(new int[0], false, 0, 0); Check(popup.Layout.Index(HotkeyPopupCommand.Clear) < 0, "clear is absent while capturing"); click(0); click(1); Wait(owner, () => !owner.Busy);
                    Check(owner.Get(target.Id) == null && popup.Visible && !popup.Capturing, "cancel before clear reliably removes the binding");
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
            CheckCompletionOwnership(false); CheckCompletionOwnership(true); CheckCompletionOwnership(true, true);
            CheckDisplayStates(); CheckCompactDetails(); CheckDetailInput(); HotkeyAdvisoryChecks.Run();
            Console.WriteLine("PASS: hotkey actual-profile conflicts and production popup/input/file checks.");
        }
        private static void CheckCompletionOwnership(bool fail, bool unconfirmed = false)
        {
            var registry = new HotkeyRegistry();
            registry.Register(new HotkeyAction("test.a", "前目标", HotkeyContext.Gameplay, () => true, () => { }));
            registry.Register(new HotkeyAction("test.b", "后目标", HotkeyContext.Gameplay, () => true, () => { }));
            var storage = new GatedStorage { Fail = fail, Unconfirmed = unconfirmed, Initial = unconfirmed ? HotkeyDocument.Encode(new HotkeyDocument(new[] { new KeyValuePair<string, string>("test.a", "K") })) : null };
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
                    var button = popup.Layout.Buttons[popup.Layout.Index(HotkeyPopupCommand.Record)].Rect.Offset(popup.Layout.Panel.X, popup.Layout.Panel.Y);
                    frame(new Keys[0], true, button.X + 3, button.Y + 3);
                    frame(new Keys[0], false, button.X + 3, button.Y + 3);
                    frame(new[] { Keys.LeftControl, Keys.K }, false, 0, 0);
                    Check(storage.Entered.WaitOne(5000) && owner.Busy && popup.Status.Contains("SmartCursor"), "real worker accepted warned A before window switch");
                    Check(popup.Feedback.Kind == HotkeyFeedbackKind.Saving && (unconfirmed ? owner.Get("test.a").Text == "K" : owner.Get("test.a") == null) && popup.Layout.Keycaps.Count == 2, "pending candidate is visible but not effective");
                    var disabled = popup.Layout.Buttons[popup.Layout.Index(HotkeyPopupCommand.Record)].Rect.Offset(popup.Layout.Panel.X, popup.Layout.Panel.Y);
                    Check(popup.Layout.Hit(disabled.X + 3, disabled.Y + 3) == HotkeyPopupCommand.None && popup.Layout.Index(HotkeyPopupCommand.Clear) < 0, "busy modifications are not hit targets");
                    frame(new Keys[0], true, disabled.X + 3, disabled.Y + 3); frame(new Keys[0], false, disabled.X + 3, disabled.Y + 3);
                    Check(owner.Busy && !popup.Capturing && input.Hotkeys.IsSuppressed(256) == false, "disabled click completes without another command");
                    popup.Close();
                    popup.Click("test.b", anchor, 1, 0, 300); popup.Click("test.b", anchor, 1, 0, 400);
                    popup.Prepare(800, 600, font, Measure);
                    Check(!popup.Status.Contains("已保存"), "B never displays A result");
                    storage.Release.Set(); Wait(owner, () => !owner.Busy);
                    frame(new Keys[0], false, 0, 0);
                    Check((fail ? (unconfirmed ? owner.Get("test.a").Text == "K" : owner.Get("test.a") == null) : owner.Get("test.a")?.Text == "LeftControl+K") && owner.Get("test.b") == null, "submitted A completes after close without editing B");
                    Check(popup.Target == "test.b" && popup.Feedback.Kind == (unconfirmed ? HotkeyFeedbackKind.Unconfirmed : HotkeyFeedbackKind.Ready) && !popup.Status.Contains("已保存") && !popup.Status.Contains("SmartCursor") && !popup.Status.Contains("原绑定仍有效"), "B reflects global availability without inheriting A result or retained binding claim");
                    if (unconfirmed) Check(popup.Status.Contains("当前没有有效绑定") && !popup.Layout.Enabled[popup.Layout.Index(HotkeyPopupCommand.Record)], "B shows its own effective state while disk result remains unknown");
                }
                finally { storage.Release.Set(); }
            }
        }
        private sealed class GatedStorage : IPreferenceStorage
        {
            internal bool Fail, Unconfirmed;
            internal int Writes;
            internal byte[] Initial;
            internal readonly ManualResetEvent Entered = new ManualResetEvent(false), Release = new ManualResetEvent(false);
            public PreferenceReadResult Read() { return new PreferenceReadResult(Initial == null ? PreferenceReadStatus.Missing : PreferenceReadStatus.Loaded, Initial, Initial == null ? null : "initial", null); }
            public PreferenceWriteResult Write(string identity, byte[] bytes)
            {
                Writes++;
                Entered.Set();
                if (!Release.WaitOne(5000)) throw new TimeoutException("Controlled save was not released.");
                return Fail ? new PreferenceWriteResult(PreferenceWriteStatus.IoFailure, null, "controlled failure", commitUnconfirmed: Unconfirmed) : new PreferenceWriteResult(PreferenceWriteStatus.Saved, "saved", null);
            }
            public void Dispose() { Entered.Dispose(); Release.Dispose(); }
        }
        private static void CheckDisplayStates()
        {
            Localization.Language.Values["LegacyMenu.160"] = "快捷火把";
            Localization.Language.Values["LegacyMenu.161"] = "智能光标";
            Check(VanillaHotkeyConflicts.DisplayAction("SmartSelect") == "快捷火把" && VanillaHotkeyConflicts.DisplayAction("SmartCursor") == "智能光标", "verified native keys use the active language");
            Check(VanillaHotkeyConflicts.DisplayAction("unknown-source").Contains("unknown-source"), "unavailable label keeps identifiable native token");
            Localization.Language.Values.Clear();
            var layout = new HotkeyPopupLayout(); var anchor = new F5Rect(580, 270, 22, 30);
            var effective = Parse("LeftControl+RightShift+LeftAlt+Add");
            F5Rect primary = default(F5Rect); int reads = 0;
            Func<string, float, F5Size> measure = (s, scale) => { reads++; return Measure(s, scale); };
            foreach (var kind in new[] { HotkeyFeedbackKind.Ready, HotkeyFeedbackKind.Saved, HotkeyFeedbackKind.Rejected, HotkeyFeedbackKind.Failed })
            {
                var view = new HotkeyPopupView("自动堆叠", effective, null, HotkeyModifiers.None,
                    new HotkeyFeedback(kind, kind == HotkeyFeedbackKind.Ready ? null : "本次结果", advisory: kind == HotkeyFeedbackKind.Saved ? HotkeyAdvisoryChecks.Notice(1) : null), true, false, true);
                layout.Build(640, 480, font, anchor, view, measure);
                Check(layout.Keycaps.Count == 4 && layout.Keycaps[3].Text == "Num+", "plus inside one main key is never split into modifiers");
                foreach (var cap in layout.Keycaps)
                {
                    Check(cap.Rect.Right <= layout.Panel.Width - 12 && cap.Rect.Bottom < layout.FooterTop, "whole keycap remains readable");
                    Check(layout.Hit(layout.Panel.X + cap.Rect.X + 3, layout.Panel.Y + cap.Rect.Y + 3) == HotkeyPopupCommand.None, "keycaps have no action");
                }
                var next = layout.Buttons[layout.Index(HotkeyPopupCommand.Record)].Rect.Offset(layout.Panel.X, layout.Panel.Y);
                if (kind == HotkeyFeedbackKind.Saved) Check(next.Y != primary.Y, "visible feedback contributes height instead of reserving an empty footer area");
                primary = next;
                int before = reads, generation = layout.Generation;
                for (int i = 0; i < 1000; i++) layout.Build(640, 480, font, anchor, view, measure);
                Check(reads == before && generation == layout.Generation, "stable projection performs no measurement or layout rebuild");
            }
            foreach (var kind in new[] { HotkeyFeedbackKind.Loading, HotkeyFeedbackKind.Protected, HotkeyFeedbackKind.Unconfirmed, HotkeyFeedbackKind.Cleared })
            {
                bool known = kind == HotkeyFeedbackKind.Cleared;
                var view = new HotkeyPopupView("群系显示", null, null, HotkeyModifiers.None, new HotkeyFeedback(kind, "真实状态"), known, false, known);
                layout.Build(604, 400, font, anchor, view, Measure);
                Check(layout.Keycaps.Count == 0 && layout.Text.Exists(t => t.Text == (known ? "未设置快捷键" : "快捷键状态待核对")), "unknown/protected state cannot masquerade as unbound");
                Check(layout.Panel.Bottom <= 388 && layout.HelpPanel.Bottom <= 388, "minimum supported test viewport contains popup and help");
                if (!known) Check(!layout.Enabled[layout.Index(HotkeyPopupCommand.Record)], "unknown/protected cannot edit");
            }
            var longest = new HotkeyPopupView("自动丢弃", Parse("RightControl+RightShift+RightAlt+MediaPreviousTrack"), null, HotkeyModifiers.None,
                new HotkeyFeedback(HotkeyFeedbackKind.Saved, "已保存", advisory: HotkeyAdvisoryChecks.Notice(24)), true, false, true);
            layout.Build(604, 340, font, anchor, longest, Measure);
            Check(layout.Panel.Bottom <= 328 && layout.Keycaps.Count == 4, "long names and feedback fit the supported viewport");
        }
        private static F5Size Measure(string text, float scale) { return new F5Size(text.Length * 10 * scale, 24 * scale); }
        private static void CheckDetailInput()
        {
            var previous = PlayerInput.CurrentProfile; var registry = new HotkeyRegistry();
            registry.Register(new HotkeyAction("test.details", "完整提醒", HotkeyContext.Gameplay, () => true, () => { }));
            var storage = new GatedStorage(); storage.Release.Set();
            PlayerInput.CurrentProfile = new PlayerInputProfile();
            var map = PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard].KeyStatus; map.Clear();
            foreach (string id in new[] { "Up", "Down", "Left", "Right", "Jump", "Grapple", "SmartCursor", "SmartSelect", "QuickHeal", "QuickMana", "QuickBuff", "Inventory" }) map[id] = new List<string> { "K" };
            try
            {
                using (var owner = new HotkeyBindings(registry, storage))
                {
                    Wait(owner, () => owner.Loaded);
                    var input = new HostInputState(() => new IntPtr(1), () => new IntPtr(1)); var popup = new HotkeyPopup(owner, registry, input);
                    Action<Keys[], bool, float, float, int> step = (keys, left, x, y, wheel) =>
                    {
                        input.BeginUpdate(); PlayerInput.MouseInfo = new MouseState((int)x, (int)y, 0, left ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                        input.AfterMapping(); Main.keyState = new KeyboardState(keys); input.AfterKeyboardRefresh();
                        popup.Process(true, 0, x, y, popup.Layout.Matches(800, 400, font), wheel); popup.Prepare(800, 400, font, Measure);
                    };
                    Action<HotkeyPopupCommand> click = command =>
                    {
                        var r = popup.Layout.Buttons[popup.Layout.Index(command)].Rect.Offset(popup.Layout.Panel.X, popup.Layout.Panel.Y);
                        step(new Keys[0], true, r.X + 4, r.Y + 4, 0); step(new Keys[0], false, r.X + 4, r.Y + 4, 0);
                    };
                    var anchor = new F5Rect(750, 280, 22, 30);
                    popup.Click("test.details", anchor, 1, 0, 100); popup.Click("test.details", anchor, 1, 0, 200); popup.Prepare(800, 400, font, Measure);
                    step(new Keys[0], false, 0, 0, 0); click(HotkeyPopupCommand.Record);
                    step(new[] { Keys.K }, false, 0, 0, 0); Wait(owner, () => !owner.Busy); step(new Keys[0], false, 0, 0, 0);
                    var result = popup.Feedback.Advisory;
                    Check(result.Items.Count == 12 && storage.Writes == 1 && popup.Layout.DetailVisibleLines <= 3, "one accepted submission owns a full report and bounded summary");
                    PlayerInput.CurrentProfile = null;
                    click(HotkeyPopupCommand.Details);
                    Check(popup.DetailsExpanded && popup.Layout.DetailMaxOffset > 0, "full result expands into a bounded scroll viewport");
                    while (popup.DetailOffset < popup.Layout.DetailMaxOffset) click(HotkeyPopupCommand.DetailDown);
                    Check(popup.DetailOffset + popup.Layout.DetailVisibleLines == popup.Layout.DetailText.Count, "actual control gestures reach the final cached line");
                    int offset = popup.DetailOffset;
                    click(HotkeyPopupCommand.Help);
                    var help = popup.Layout.HelpPanel;
                    step(new Keys[0], false, help.X + 4, help.Y + 4, -120);
                    Check(popup.HelpVisible && popup.ConsumeWheel && popup.DetailOffset == offset, "help wheel is consumed without scrolling details behind it");
                    step(new Keys[0], false, 0, 0, 0);
                    var body = popup.Layout.DetailViewport.Offset(popup.Layout.Panel.X, popup.Layout.Panel.Y);
                    step(new Keys[0], false, body.X + 3, body.Y + 3, 120);
                    Check(popup.ConsumeWheel && popup.DetailOffset < offset, "wheel uses the shared popup input to scroll cached lines");
                    click(HotkeyPopupCommand.Details);
                    Check(!popup.DetailsExpanded && ReferenceEquals(result, popup.Feedback.Advisory) && storage.Writes == 1 && owner.Get("test.details").Text == "K", "details/help do not replace submission snapshot or rewrite binding after profile changes");
                    click(HotkeyPopupCommand.Close);
                    popup.Click("test.details", anchor, 1, 0, 300); popup.Click("test.details", anchor, 1, 0, 400); popup.Prepare(800, 400, font, Measure);
                    Check(popup.Feedback.Kind == HotkeyFeedbackKind.Ready && popup.Feedback.Advisory == null && popup.Layout.Panel.Height < 180 && storage.Writes == 1, "reopen neither restores old advice nor checks unavailable profile");
                }
            }
            finally { PlayerInput.CurrentProfile = previous; }
        }
        private static void CheckCompactDetails()
        {
            var layout = new HotkeyPopupLayout(); var report = HotkeyAdvisoryChecks.Notice(24);
            var chord = Parse("LeftControl+LeftShift+K");
            foreach (var viewport in new[] { new F5Size(604, 340), new F5Size(640, 480), new F5Size(800, 600) })
            foreach (bool expanded in new[] { false, true })
            {
                var view = new HotkeyPopupView("群系显示", chord, null, HotkeyModifiers.None, new HotkeyFeedback(HotkeyFeedbackKind.Saved, "已保存", advisory: report), true, false, true, expanded);
                layout.ResetAnchor(); layout.Build(viewport.Width, viewport.Height, font, new F5Rect(viewport.Width - 32, viewport.Height - 90, 22, 30), view, Measure);
                Check(layout.Panel.Bottom <= viewport.Height - 12 && layout.Panel.Right <= viewport.Width - 12, "summary/details stay in the edge viewport");
                Check(layout.DetailText.Count == 24 && layout.DetailVisibleLines > 0 && (expanded || layout.DetailVisibleLines <= 3), "summary is bounded while all source lines remain reachable");
                if (expanded) Check(layout.DetailMaxOffset + layout.DetailVisibleLines == 24, "last detail row is reachable through the bounded range");
                foreach (var b in layout.Buttons) Check(b.Rect.Bottom <= layout.Panel.Height && b.Rect.X >= 0 && b.Rect.Right <= layout.Panel.Width, "all detail and exit controls remain in the window");
                float originalHeight = layout.Panel.Height; int generation = layout.Generation;
                layout.PrepareHelp(Measure);
                Check(layout.Panel.Height == originalHeight && layout.Generation == generation, "visible help does not resize the main popup");
                Check(layout.HelpText.Count >= 6 && layout.HelpPanel.Bottom <= viewport.Height - 12, "all help rules remain available in edge viewport");
                foreach (var b in layout.Buttons)
                {
                    var r = b.Rect.Offset(layout.Panel.X, layout.Panel.Y); var h = layout.HelpPanel;
                    Check(!(r.X < h.Right && r.Right > h.X && r.Y < h.Bottom && r.Bottom > h.Y), "help never covers an actionable or disabled control");
                }
            }
            var ready = new HotkeyPopupView("群系显示", chord, null, HotkeyModifiers.None, new HotkeyFeedback(HotkeyFeedbackKind.Ready), true, false, true);
            layout.Build(800, 600, font, new F5Rect(650, 450, 22, 30), ready, Measure);
            Check(layout.Panel.Width < 400 && layout.Panel.Height < 180 && layout.DetailVisibleLines == 0 && layout.Index(HotkeyPopupCommand.Details) < 0, "reopened binding has only caps and actions, without result/details reserves");
            foreach (var mods in new[] { HotkeyModifiers.None, HotkeyModifiers.LeftControl, HotkeyModifiers.LeftControl | HotkeyModifiers.LeftShift, HotkeyModifiers.RightControl | HotkeyModifiers.RightShift | HotkeyModifiers.RightAlt })
            {
                var view = new HotkeyPopupView("群系显示", chord, null, mods, new HotkeyFeedback(HotkeyFeedbackKind.Capturing), true, true, true);
                layout.Build(800, 600, font, new F5Rect(650, 450, 22, 30), view, Measure);
                if (layout.Keycaps.Count > 0)
                {
                    var first = layout.Keycaps[0].Rect; var last = layout.Keycaps[layout.Keycaps.Count - 1].Rect;
                    Check(Math.Abs(first.X + last.Right - layout.Panel.Width) < .01f, "modifier cap row is centered by actual measured width");
                }
                foreach (var text in layout.Text)
                    if (text.Text.Contains("请按") || text.Text.Contains("请再按") || text.Text.Contains("单键直接"))
                        Check(Math.Abs(text.Rect.X + text.Rect.Right - layout.Panel.Width) < .01f, "capture progress and each instruction line use the same center");
            }
            layout.Build(604, 220, font, new F5Rect(570, 150, 22, 30), ready, Measure);
            Check(layout.Index(HotkeyPopupCommand.Close) >= 0 && layout.Index(HotkeyPopupCommand.Record) < 0 && layout.Panel.Bottom < 220, "existing too-small viewport guard keeps a reachable close control");
            var wide = new HotkeyPopupView("群系显示", Parse("MediaPreviousTrack"), null, HotkeyModifiers.None, new HotkeyFeedback(HotkeyFeedbackKind.Ready), true, false, true);
            layout.Build(604, 400, new object(), new F5Rect(570, 200, 22, 30), wide, (text, scale) => text == "MediaPreviousTrack" ? new F5Size(600, 24) : Measure(text, scale));
            Check(layout.Keycaps.Count == 0 && layout.Index(HotkeyPopupCommand.Close) >= 0 && layout.Text.Exists(t => t.Text.Contains("缩放")), "unfittable single key label uses the existing safe viewport response");
        }
        private static HotkeyChord Parse(string text) { HotkeyChord c; string r; if (!HotkeyChord.TryParse(text, out c, out r)) throw new Exception(r); return c; }
        private static void Wait(HotkeyBindings owner, Func<bool> condition) { var watch = Stopwatch.StartNew(); while (!condition() && watch.ElapsedMilliseconds < 5000) { owner.Poll(); Thread.Sleep(1); } Check(condition(), "worker completed"); }
        private static void Check(bool value, string reason) { if (!value) throw new InvalidOperationException("Hotkey: " + reason); }
    }
}
