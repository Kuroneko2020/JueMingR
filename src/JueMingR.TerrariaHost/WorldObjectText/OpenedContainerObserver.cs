using System;
using JueMingR.Features.WorldObjectText;
using JueMingR.Platform.WorldObjectText;
using JueMingR.Platform.Runtime;
using JueMingR.Platform.WorldTargets;
using JueMingR.TerrariaHost.World;
using Terraria;

namespace JueMingR.TerrariaHost.WorldObjectText
{
    // Observe committed vanilla state after Update. OpenChest alone misses the
    // normal multiplayer case-33 path, which assigns these fields directly.
    internal sealed class OpenedContainerObserver
    {
        private readonly SingleFeatureRuntime runtime;
        private readonly OpenedPositionHistory history;
        private readonly Func<bool> automatic;
        private readonly Func<int, int, WorldTargetTile> read;
        private Chest held;
        private int heldIndex = -1, heldX, heldY;
        private bool pairKnown;
        internal OpenedContainerObserver(SingleFeatureRuntime runtime, OpenedPositionHistory history, WorldTileObservation world, Func<bool> automatic)
        { this.runtime = runtime; this.history = history; read = world.Read; this.automatic = automatic; }
        internal void Start() { held = null; heldIndex = -1; pairKnown = false; history.BeginSession(runtime.Generation); }
        internal void End() { held = null; heldIndex = -1; pairKnown = false; history.EndSession(); }
        internal void Update(bool loadForOpened)
        {
            if (!runtime.IsSessionActive || Main.gameMenu || Main.dedServ || Main.netMode != 0 && Main.netMode != 1) return;
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) return;
            if (Main.netMode == 1 && (Netplay.Connection == null || Netplay.Connection.State != 10)) { held = null; heldIndex = -1; history.Poll(); return; }
            // Off/Always alone do not cold-read history. The Opened view or an
            // actual observed opening creates the first demand for this pair.
            if (!pairKnown && (loadForOpened || history.HasAny)) ObserveIdentity(player);
            history.Poll();
            if (automatic() || !Main.playerInventory || player.chest < 0 || Main.chest == null || player.chest >= Main.chest.Length)
            { held = null; heldIndex = -1; return; }
            Chest chest = Main.chest[player.chest];
            if (chest == null || chest.x != player.chestX || chest.y != player.chestY) { held = null; heldIndex = -1; return; }
            // Keeping one chest open is scalar/reference comparison only. Full
            // geometry is checked on the actual new-open transition, not per tick.
            if (ReferenceEquals(chest, held) && heldIndex == player.chest && heldX == chest.x && heldY == chest.y) return;
            WorldObject value;
            if (!WorldObjectResolver.TryResolve(chest.x, chest.y, read(chest.x, chest.y), read, out value) || value.Key != WorldObject.PositionKey(chest.x, chest.y) ||
                value.Kind != WorldObjectKind.Chest || value.Type != 21 && value.Type != 467 && value.Type != 88) return;
            held = chest; heldIndex = player.chest; heldX = chest.x; heldY = chest.y;
            history.Opened(chest.x, chest.y);
            if (!pairKnown) ObserveIdentity(player);
        }
        private void ObserveIdentity(Player player)
        {
            var file = Main.ActivePlayerFileData; var world = Main.ActiveWorldFileData;
            if (file == null || !ReferenceEquals(file.Player, player) || file.ServerSideCharacter || Main.ServerSideCharacter ||
                String.IsNullOrEmpty(file.Path) || world == null || world.UniqueId == Guid.Empty ||
                Main.netMode == 1 && (Netplay.Connection == null || Netplay.Connection.State != 10)) return;
            string pair = OpenedPositionCodec.PairKey((file.IsCloudSave ? "cloud:" : "local:") + file.Path, world.UniqueId);
            history.UsePair(pair); pairKnown = true;
        }
    }
}
