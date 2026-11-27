using Microsoft.Xna.Framework.Input;

namespace JueMingR.TerrariaHost.Input
{
    internal static class HostKeyboardActions
    {
        internal static bool IsOperationKey(Keys key)
        {
            // Mirror Terraria 1.4.6.1 PlayerInput.GetPressedKeys. The hotkey
            // catalogue excludes other keys (e.g. Win) that still prove a hold.
            return key != Keys.None && key != Keys.Kanji && (key < (Keys)240 || key > Keys.Pa1);
        }

        internal static bool HasHeldOperationKey(KeyboardState frozenSample)
        {
            // Consumption can clear Main.keyState before a tail checks release.
            // Read the frozen XNA bits without allocating a pressed-key array.
            for (int code = 0; code < 256; code++)
            {
                var key = (Keys)code;
                if (IsOperationKey(key) && frozenSample.IsKeyDown(key)) return true;
            }
            return false;
        }
    }
}
