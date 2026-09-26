using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using Terraria;

namespace JueMingR.TerrariaHost.Items
{
    // A synchronous presentation adapter, not a result owner or retry queue.
    // Both name lookup and native display can fail independently of discarding.
    internal sealed class ItemDiscardFeedback
    {
        private readonly Func<bool> enabled;
        internal Action<Player, string> Present { get; set; }
        internal ItemDiscardFeedback(Func<bool> enabled)
        { this.enabled = enabled ?? throw new ArgumentNullException(nameof(enabled)); }

        internal string CaptureName(Item original)
        {
            try
            {
                if (!enabled() || !Main.showItemText || Main.netMode == 2) return null;
                return original.AffixName();
            }
            catch { return null; }
        }

        internal void Complete(Player player, string name, int quantity)
        {
            if (String.IsNullOrEmpty(name) || quantity <= 0) return;
            try
            {
                string text = "自动丢弃了" + quantity.ToString(CultureInfo.InvariantCulture) + "个" + name;
                if (Present != null) { Present(player, text); return; }
                // The native Advanced path keeps the original exact quantity,
                // 60-frame motion and opt-out. A full pool drops display only.
                PopupText popup; int slot; string token;
                Feedback.NativePopupText.TryCreate(text, Color.White, 60, new Vector2(0, -7), player.Center, out popup, out slot, out token);
            }
            catch { }
        }
    }
}
