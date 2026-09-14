using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;

namespace JueMingR.TerrariaHost.Information
{
    // A matching completed native UI pass is necessary to start a drag under
    // vanilla UI. Unknown/new coordinates decline the press, never replay it.
    // These Draw callbacks only observe; Update owns all adjustment transitions.
    internal sealed class InformationPointerObservation
    {
        private bool begun, completed, blocked, inventory;
        private int chest, talkNpc, mapStyle;
        private long tick, session;
        private object player, world;
        private string chat;
        private Vector2 raw, screen, mouseScale;
        private Matrix matrix;
        internal void Invalidate() { begun = completed = false; }
        internal void Begin(long currentTick, long currentSession)
        {
            begun = true; completed = false; tick = currentTick; session = currentSession;
            player = Main.LocalPlayer; world = Main.ActiveWorldFileData;
            raw = new Vector2(PlayerInput.MouseInfo.X, PlayerInput.MouseInfo.Y);
            screen = PlayerInput.OriginalScreenSize; mouseScale = PlayerInput.RawMouseScale; matrix = Main.UIScaleMatrix;
            inventory = Main.playerInventory; chest = Main.LocalPlayer?.chest ?? -1; talkNpc = Main.LocalPlayer?.talkNPC ?? -1;
            chat = Main.npcChatText; mapStyle = Main.mapStyle;
        }
        internal void End()
        {
            if (!begun) return;
            begun = false;
            // Resource bars publish mouseText rather than mouseInterface. This
            // runs after restoring R's own leases, and before the next sample.
            blocked = Main.LocalPlayer == null || Main.LocalPlayer.mouseInterface || Main.mouseText;
            completed = true;
        }
        internal bool CanGrab(long currentTick, long currentSession)
        { return !blocked && Matches(currentTick, currentSession); }
        internal bool NativeOwnsPointer(long currentTick, long currentSession)
        { return blocked && Matches(currentTick, currentSession); }
        private bool Matches(long currentTick, long currentSession)
        {
            return completed && Main.LocalPlayer != null && tick == currentTick && session == currentSession && ReferenceEquals(player, Main.LocalPlayer) &&
                ReferenceEquals(world, Main.ActiveWorldFileData) && raw.X == PlayerInput.MouseInfo.X && raw.Y == PlayerInput.MouseInfo.Y &&
                screen == PlayerInput.OriginalScreenSize && mouseScale == PlayerInput.RawMouseScale && matrix == Main.UIScaleMatrix &&
                inventory == Main.playerInventory && chest == Main.LocalPlayer.chest && talkNpc == Main.LocalPlayer.talkNPC && chat == Main.npcChatText && mapStyle == Main.mapStyle;
        }
    }
}
