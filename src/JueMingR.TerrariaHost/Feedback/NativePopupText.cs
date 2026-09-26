using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Feedback
{
    // Game-thread native pool adapter. Admission never evicts a foreign entry.
    // Each use has a unique string identity: native ResetText reuses the object,
    // including when a successor happens to have exactly the same text.
    internal static class NativePopupText
    {
        internal static bool TryCreate(string text, Color color, int frames, Vector2 velocity, Vector2 position,
            out PopupText popup, out int slot, out string token)
        {
            popup = null; slot = -1; token = null;
            if (!Main.showItemText || Main.netMode == 2 || PopupText.popupText == null) return false;
            bool free = false;
            foreach (var item in PopupText.popupText) if (item != null && !item.active) { free = true; break; }
            if (!free) return false;
            token = new string(text.ToCharArray());
            slot = PopupText.NewText(new AdvancedPopupRequest { Text = token, Color = color, DurationInFrames = frames, Velocity = velocity }, position);
            if (slot < 0 || slot >= PopupText.popupText.Length) return false;
            popup = PopupText.popupText[slot];
            return Owns(popup, slot, token);
        }
        internal static bool Owns(PopupText popup, int slot, string token)
        {
            return token != null && popup != null && PopupText.popupText != null && slot >= 0 && slot < PopupText.popupText.Length &&
                ReferenceEquals(PopupText.popupText[slot], popup) && popup.active && popup.freeAdvanced && popup.context == PopupTextContext.Advanced &&
                ReferenceEquals(popup.name, token) && ReferenceEquals(popup.displayText, token);
        }
        internal static void Release(PopupText popup, int slot, string token)
        { if (Owns(popup, slot, token)) popup.active = false; }
    }
}
