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
                // Target .8 reforge uses this same native popup pool and draw
                // path. Advanced accepts the exact sentence without an item
                // quantity suffix; it cannot merge into ordinary pickup text.
                // Native capacity/showItemText rules apply; never replay failure.
                PopupText.NewText(new AdvancedPopupRequest
                {
                    Text = "自动丢弃了" + quantity.ToString(CultureInfo.InvariantCulture) + "个" + name,
                    Color = Color.White,
                    DurationInFrames = 60,
                    Velocity = new Vector2(0, -7)
                }, player.Center);
            }
            catch { }
        }
    }
}
