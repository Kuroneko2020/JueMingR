using System;

namespace JueMingR.Platform.Hotkeys
{
    // One physical owner. An ownership veto never edits down[]; only a trusted
    // release retires held keys. Every edge advances before any action is read.
    public sealed class HotkeyInput
    {
        private readonly bool[] down = new bool[HotkeyChord.KeyCount], edges = new bool[HotkeyChord.KeyCount];
        private readonly bool[] suppressed = new bool[HotkeyChord.KeyCount];
        private bool initialized;
        public bool Reliable { get; private set; }
        public HotkeyModifiers Modifiers { get; private set; }
        public bool SystemModifier { get { return down[91] || down[92]; } }
        public bool HasSuppressedKeys { get; private set; }
        public bool IsDown(int key) { return down[key]; }
        public bool IsNew(int key) { return Reliable && edges[key] && !suppressed[key]; }
        public void Update(bool[] sample, bool reliable)
        {
            if (sample == null || sample.Length != down.Length) throw new ArgumentException("Expected one complete physical key sample.");
            Reliable = reliable; HasSuppressedKeys = false;
            for (int i = 0; i < down.Length; i++)
            {
                edges[i] = false;
                if (reliable)
                {
                    edges[i] = initialized && sample[i] && !down[i]; down[i] = sample[i];
                    if (!down[i]) suppressed[i] = false;
                }
                HasSuppressedKeys |= suppressed[i];
            }
            if (!reliable) return;
            initialized = true; Modifiers = HotkeyModifiers.None;
            for (int i = 0; i < 6; i++) if (down[HotkeyChord.ModifierCode(i)]) Modifiers |= (HotkeyModifiers)(1 << i);
        }
        public void SuppressHeld()
        { for (int i = 0; i < down.Length; i++) if (down[i]) { suppressed[i] = true; HasSuppressedKeys = true; } }
        public void SuppressKey(int key)
        { if (key <= 0 || key >= down.Length) throw new ArgumentOutOfRangeException(nameof(key)); if (down[key]) { suppressed[key] = true; HasSuppressedKeys = true; } }
        public bool IsSuppressed(int key) { return suppressed[key]; }
        public int NewPrimary(out int key)
        {
            key = 0; int count = 0;
            for (int i = 1; i < down.Length; i++)
                if (IsNew(i) && HotkeyChord.Modifier(i) == HotkeyModifiers.None && i != 91 && i != 92) { key = i; count++; }
            return count;
        }
    }
}
