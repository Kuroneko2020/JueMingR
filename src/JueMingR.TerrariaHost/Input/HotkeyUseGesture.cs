using System;
using System.Collections.Generic;
using JueMingR.Platform.Hotkeys;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Input
{
    // Shares the Host's frozen sample. Ownership is of physical press tails,
    // not of an item operation: completion/cancellation must not replay a hold.
    internal sealed class HotkeyUseGesture
    {
        private static readonly string[] actions = { "MouseLeft", "MouseRight", "SmartSelect" };
        private static readonly Dictionary<string, int> codes = BuildCodes();
        private readonly bool[] owned = new bool[HotkeyChord.KeyCount];
        private bool active, previousUse;
        private readonly bool[] gamepad = new bool[3];
        private KeyboardState mappedKeyboard;
        internal bool HasTail { get { return active; } }
        internal bool Enabled { get; set; }
#if DEBUG
        internal long SourceChecks { get; private set; }
#endif
        internal void BeginUpdate()
        {
            previousUse = PlayerInput.Triggers.Current.MouseLeft;
            gamepad[0] = gamepad[1] = gamepad[2] = false;
        }
        internal void ObserveMapping(KeyConfiguration configuration, TriggersSet set, string token, InputMode mode)
        {
            if ((!Enabled && !active) || (mode != InputMode.XBoxGamepad && mode != InputMode.XBoxGamepadUI) || !ReferenceEquals(set, PlayerInput.Triggers.Current)) return;
            // Observe native mapping, including BEFORE the first dispatch. Its
            // LatestInputMode dictionary survives Reset and cannot prove source.
            for (int i = 0; i < actions.Length; i++)
            {
#if DEBUG
                SourceChecks++;
#endif
                gamepad[i] |= configuration.KeyStatus[actions[i]].Contains(token);
            }
        }
        internal void AfterMapping() { mappedKeyboard = Main.keyState; }
        internal void Claim(HotkeyChord chord, HotkeyInput input, bool[] physical)
        {
            if (chord == null || !input.IsNew(chord.MainKey)) return;
            owned[chord.MainKey] = active = true;
            var profile = PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard];
            for (int i = 0; i < 6; i++)
            {
                int key = HotkeyChord.ModifierCode(i);
                if (((int)chord.Modifiers & 1 << i) == 0) continue;
                // A modifier that was already driving an attack remains foreign.
                // Default Shift's SmartSelect may be borrowed by this new chord.
                if (previousUse && !input.IsNew(key) && profile.KeyStatus["MouseLeft"].Contains(HotkeyChord.KeyName(key))) continue;
                owned[key] = true;
            }
            Consume(physical);
        }
        internal void AfterSample(bool[] physical, bool reliable, bool permitted)
        {
            if (!active) return;
            if (permitted) Consume(physical);
            if (!reliable) return; // Focus-generated releases are not proof.
            active = false;
            for (int key = 0; key < owned.Length; key++)
            { owned[key] &= physical[key]; active |= owned[key]; }
        }
        private void Consume(bool[] physical)
        {
            // SteamDeck's UI trigger can set MouseLeft without source metadata.
            // It belongs to native UI, never to a gameplay shortcut's tail.
            if (Main.gameMenu || Main.LocalPlayer.talkNPC != -1 || Main.LocalPlayer.sign != -1 ||
                Terraria.UI.IngameFancyUI.CanCover() ||
                Terraria.UI.Gamepad.UILinkPointNavigator.Available && !PlayerInput.InBuildingMode) return;
            var pack = PlayerInput.Triggers;
            var profile = PlayerInput.CurrentProfile.InputModes[InputMode.Keyboard];
            for (int i = 0; i < actions.Length; i++)
            {
                string action = actions[i];
                bool ours = false, foreign = false;
                foreach (string token in profile.KeyStatus[action])
                {
#if DEBUG
                    SourceChecks++;
#endif
                    int key;
                    if (!codes.TryGetValue(token, out key)) { foreign |= pack.Current.KeyStatus[action]; continue; }
                    ours |= owned[key];
                    foreign |= (key < 256 ? !PlayerInput.WritingText && token != Main.blockKey && mappedKeyboard.IsKeyDown((Keys)key) : physical[key]) && !owned[key];
                }
                if (!ours) continue;
                bool before = pack.Current.KeyStatus[action];
                // Only subtract from the actual mapped action. In particular,
                // never revive an action cleared by focus or modal ownership.
                foreign = before && (foreign || gamepad[i]);
                pack.Current.KeyStatus[action] = foreign;
                pack.JustPressed.KeyStatus[action] = foreign && !pack.Old.KeyStatus[action];
                pack.JustReleased.KeyStatus[action] = !foreign && pack.Old.KeyStatus[action];
                // Main's mouse flags are mapped actions, not raw MouseInfo.
                // Preserve an independently injected flag with no mapped source.
                if (action == "MouseLeft" && (before || foreign)) Main.mouseLeft = foreign;
                if (action == "MouseRight" && (before || foreign)) Main.mouseRight = foreign;
            }
        }
        private static Dictionary<string, int> BuildCodes()
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int key = 0; key < HotkeyChord.KeyCount; key++)
                if (HotkeyChord.KeyName(key) != null) result.Add(HotkeyChord.KeyName(key), key);
            return result;
        }
    }
}
