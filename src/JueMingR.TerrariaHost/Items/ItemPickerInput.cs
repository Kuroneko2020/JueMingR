using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Items
{
    // The list picker owns only a text lease and the keys held while it closes.
    // It has no preferences, binding registration, chord parsing or commands.
    internal sealed class ItemPickerInput
    {
        private readonly object owner = new object();
        private bool leased, priorBlock, tail;
        internal bool OwnsTextToken { get { return ReferenceEquals(Main.CurrentInputTextTakerOverride, owner); } }
        private bool OtherTextOwner { get { return Main.drawingPlayerChat || Main.editSign || Main.editChest ||
            Main.CurrentInputTextTakerOverride != null && !OwnsTextToken; } }
        internal void BeforeInput(bool pickerActive)
        {
            bool foreign = OtherTextOwner;
            if (pickerActive && !foreign)
            {
                if (!leased) { priorBlock = Main.blockInput; leased = true; }
                Main.CurrentInputTextTakerOverride = owner;
                Main.blockInput = true; PlayerInput.WritingText = true;
            }
            else Release();
            if (tail && !foreign) PlayerInput.WritingText = true;
        }
        internal void Sample(KeyboardState sample, bool focused)
        {
            if (leased && OtherTextOwner) Release();
            if (!leased && !tail) return;
            // Focus-loss samples may synthesize releases. Only a real focused
            // empty sample retires this picker-owned tail, even after hiding.
            tail = !focused || sample.GetPressedKeys().Length != 0;
            if (!OtherTextOwner) Main.keyState = default(KeyboardState);
            if (!focused) Release();
        }
        internal void Release()
        {
            if (!leased) return;
            // Closing can precede the next frozen sample; require a real release.
            tail = true;
            if (OwnsTextToken) Main.CurrentInputTextTakerOverride = null;
            if (Main.blockInput) Main.blockInput = priorBlock;
            leased = false;
        }
    }
}
