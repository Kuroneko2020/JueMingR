using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.Features.Items;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Items
{
    internal sealed class ItemHotkeys
    {
        private readonly HostItems host;
        private readonly object owner = new object();
        private KeyboardState previous;
        private bool leased, priorBlock, tail, focusTail;
        private long inspectedRevision = -1;
        private object inspectedProfile;
        internal ItemActionKind? Capturing { get; private set; }
        internal string Feedback { get; private set; }
        internal bool OwnsTextToken { get { return ReferenceEquals(Main.CurrentInputTextTakerOverride, owner); } }
        internal ItemHotkeys(HostItems host) { this.host = host; }
        internal void Capture(ItemActionKind action) { Capturing = action; Feedback = "按可用字母、数字或功能键；支持组合键，Esc 取消"; }
        internal void Cancel(string feedback = null) { Capturing = null; if (feedback != null) Feedback = feedback; Release(); }
        internal void BeforeInput(bool active, bool picker = false)
        {
            if (host.Preferences.IsLoaded && (inspectedRevision != host.Preferences.Revision || !ReferenceEquals(inspectedProfile, PlayerInput.CurrentProfile)))
            {
                inspectedRevision = host.Preferences.Revision; inspectedProfile = PlayerInput.CurrentProfile;
                for (int i = 0; i < 3; i++)
                {
                    int binding = host.Preferences.Value.Binding((ItemActionKind)i); string conflict;
                    if (binding != 0 && !AvailableBinding(binding, out conflict)) { Feedback = conflict; break; }
                }
            }
            bool foreign = Main.drawingPlayerChat || Main.editSign || Main.editChest ||
                Main.CurrentInputTextTakerOverride != null && !OwnsTextToken;
            if (active && (Capturing.HasValue || picker) && !foreign)
            {
                if (!leased) { priorBlock = Main.blockInput; leased = true; }
                Main.CurrentInputTextTakerOverride = owner; Main.blockInput = true; PlayerInput.WritingText = true;
            }
            else Release();
            if (tail && !foreign) PlayerInput.WritingText = true;
        }
        internal void Sample(KeyboardState sample, bool active, bool allowToggle, bool focused)
        {
            if (!focused) { Cancel(); focusTail = true; return; }
            if (focusTail)
            {
                previous = sample;
                if (sample.GetPressedKeys().Length == 0) focusTail = false;
                return;
            }
            bool consuming = leased || Capturing.HasValue || tail;
            if (!active) Cancel();
            else if (Capturing.HasValue)
            {
                if (sample.IsKeyDown(Keys.Escape) && previous.IsKeyUp(Keys.Escape) && Modifiers(sample) == 0) { Feedback = "已取消按键录入"; Cancel(); }
                else
                {
                    foreach (Keys key in sample.GetPressedKeys())
                    {
                        if (!previous.IsKeyUp(key) || IsModifier(key)) continue;
                        int binding = (int)key | Modifiers(sample);
                        try
                        {
                            ItemAutomationSettings.ValidateBinding(binding);
                            string conflict;
                            if (!AvailableBinding(binding, out conflict)) { Feedback = conflict; break; }
                            ItemAutomationSettings next = host.Preferences.Value.WithBinding(Capturing.Value, binding);
                            if (host.Change(next)) { Feedback = "主开关键已设置；只切换此功能开启或关闭"; Cancel(); }
                        }
                        catch (ArgumentException) { Feedback = "此键不可用或已被另一项物品功能绑定"; }
                        break;
                    }
                }
            }
            else if (allowToggle && !tail && host.Preferences.IsLoaded && host.Available)
            {
                ItemAutomationSettings settings = host.Preferences.Value;
                for (int n = 0; n < 3; n++)
                {
                    var action = (ItemActionKind)n; int binding = settings.Binding(action);
                    if (binding == 0 || Modifiers(sample) != (binding & ~255)) continue;
                    Keys key = (Keys)(binding & 255);
                    // Always update the baseline below, including modal/focus
                    // suppression. A held key cannot fire on returning to play.
                    if (sample.IsKeyDown(key) && previous.IsKeyUp(key))
                    {
                        if (host.Feature.HasFailed) { Feedback = "物品处理已停止，主开关键本次不可用"; Main.NewText(Feedback, 255, 180, 90); continue; }
                        string conflict = null;
                        if (PlayerInput.CurrentlyRebinding || !AvailableBinding(binding, out conflict))
                        {
                            string reason = PlayerInput.CurrentlyRebinding ? "正在修改原版按键，物品快捷键暂停" : conflict;
                            if (Feedback != reason) { Feedback = reason; Main.NewText(reason, 255, 180, 90); }
                            continue;
                        }
                        bool enabled = !host.Preferences.Value.Enabled(action);
                        host.Change(host.Preferences.Value.WithEnabled(action, enabled));
                        // No current native mapping uses any key of this chord.
                        // Remove only its keys from the cached sample; unrelated
                        // held keys and their original actions remain available.
                        var consumed = new HashSet<Keys>(Chord(binding));
                        Main.keyState = new KeyboardState(Main.keyState.GetPressedKeys().Where(k => !consumed.Contains(k)).ToArray());
                        Main.NewText(ItemsPresentation.Name(action) + (enabled ? "：已开启" : "：已关闭"), 220, 235, 255);
                    }
                }
            }
            if (consuming)
            { tail = sample.GetPressedKeys().Length != 0; Main.keyState = default(KeyboardState); }
            previous = sample;
        }
        private void Release()
        {
            if (!leased) return;
            if (OwnsTextToken) Main.CurrentInputTextTakerOverride = null;
            // A later text owner may take the token. Return only our block lease
            // without erasing that owner's token, matching the Notes contract.
            if (Main.blockInput) Main.blockInput = priorBlock;
            leased = false;
        }
        private static bool IsModifier(Keys key)
        { return key == Keys.LeftControl || key == Keys.RightControl || key == Keys.LeftShift || key == Keys.RightShift || key == Keys.LeftAlt || key == Keys.RightAlt; }
        private static int Modifiers(KeyboardState state)
        { return (state.IsKeyDown(Keys.LeftControl) || state.IsKeyDown(Keys.RightControl) ? 256 : 0) |
            (state.IsKeyDown(Keys.LeftShift) || state.IsKeyDown(Keys.RightShift) ? 512 : 0) |
            (state.IsKeyDown(Keys.LeftAlt) || state.IsKeyDown(Keys.RightAlt) ? 1024 : 0); }
        private static IEnumerable<Keys> Chord(int binding)
        {
            yield return (Keys)(binding & 255);
            if ((binding & 256) != 0) { yield return Keys.LeftControl; yield return Keys.RightControl; }
            if ((binding & 512) != 0) { yield return Keys.LeftShift; yield return Keys.RightShift; }
            if ((binding & 1024) != 0) { yield return Keys.LeftAlt; yield return Keys.RightAlt; }
        }
        private static bool AvailableBinding(int binding, out string reason)
        {
            reason = null; KeyConfiguration configuration;
            Keys primary = (Keys)(binding & 255);
            // These .8 consumers run before normal remappable input processing.
            // Reject them even when absent from the user's KeyStatus dictionary.
            if (primary == Keys.F7 || primary == Keys.F8 || primary == Keys.F10 || primary == Keys.F11 ||
                primary == Keys.F9 && (binding & 512) != 0)
            { reason = "快捷键 " + Label(binding) + " 是原版保留键，请重新绑定"; return false; }
            PlayerInputProfile profile = PlayerInput.CurrentProfile;
            if (profile == null || profile.InputModes == null || !profile.InputModes.TryGetValue(InputMode.Keyboard, out configuration) || configuration?.KeyStatus == null)
            { reason = "原版键位暂不可读取，物品快捷键暂停"; return false; }
            foreach (Keys key in Chord(binding)) foreach (var mapping in configuration.KeyStatus)
                if (mapping.Value != null && mapping.Value.Contains(key.ToString()))
                { reason = "快捷键 " + Label(binding) + " 与原版键位冲突，请重新绑定"; return false; }
            return true;
        }
        internal static string Label(int binding)
        { return binding == 0 ? "未绑定" : ((binding & 256) != 0 ? "Ctrl+" : "") + ((binding & 512) != 0 ? "Shift+" : "") +
            ((binding & 1024) != 0 ? "Alt+" : "") + ((Keys)(binding & 255)).ToString(); }
    }
}
