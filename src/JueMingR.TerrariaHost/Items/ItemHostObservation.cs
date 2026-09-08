using System;
using JueMingR.Platform.Items;
using JueMingR.Platform.Runtime;
using System.Collections.Generic;
using Terraria;
using Terraria.GameInput;
using Microsoft.Xna.Framework.Input;

namespace JueMingR.TerrariaHost.Items
{
    // All arrays and Item references stay on the game-thread side of the port.
    internal sealed class ItemHostObservation : IItemObservationSource
    {
        private readonly Func<long> generation;
        private readonly ItemOperationOwnership ownership;
        private readonly Item[] references = new Item[58];
        private readonly long[] instances = new long[58];
        private ItemSlotObservation[] previous;
        private ItemInventoryObservation cached;
        private long nextInstance, revision, shopIdentity;
        private Chest previousShop;
        private NPC previousNpc;
        private Player sessionPlayer;
        private int previousAdmissionGates;
        internal int ManualSlot { get; set; } = -1;
        internal Item ManualItem { get; set; }
        internal readonly HashSet<Item> ManualMaterials = new HashSet<Item>();
        internal int CausalDepth { get; set; }
        internal bool AutomaticOperation { get; set; }
        internal Func<Item, bool> AdditionalProtection { get; set; }
        internal ItemHostObservation(Func<long> generation, ItemOperationOwnership ownership)
        { this.generation = generation; this.ownership = ownership; }
        public long SessionGeneration { get { return generation(); } }
        internal Player Player
        {
            get
            {
                Player current = Main.LocalPlayer;
                return !Main.gameMenu && !Main.dedServ && (Main.netMode == 0 || Main.netMode == 1) && current != null &&
                    current.active && !current.dead && ReferenceEquals(current, sessionPlayer) && current.inventory != null && current.inventory.Length >= 58 ? current : null;
            }
        }
        internal void BeginSession()
        { sessionPlayer = Main.LocalPlayer; previous = null; cached = null; previousShop = null; previousNpc = null; ClearManual(); Array.Clear(references, 0, references.Length); }
        internal void EndSession() { sessionPlayer = null; cached = null; previous = null; ClearManual(); CausalDepth = 0; }
        private void ClearManual() { ManualSlot = -1; ManualItem = null; ManualMaterials.Clear(); }
        internal bool HasManualOperation { get { return ManualSlot >= 0 || ManualMaterials.Count != 0; } }
        internal void InvalidateObservation() { cached = null; }
        internal bool Busy
        { get { return CausalDepth != 0 || AutomaticOperation || Main.LocalPlayerHasPendingInventoryActions(); } }

        public bool TryObserve(out ItemInventoryObservation observation)
        {
            observation = null;
            Player player = Player;
            if (player == null || Busy) return false;
            if (FocusHelper.AllowInputProcessing && PlayerInput.MouseInfo.LeftButton == ButtonState.Released && PlayerInput.MouseInfo.RightButton == ButtonState.Released) ClearManual();
            Chest shop; NPC npc;
            bool available = TryShop(out shop, out npc);
            // These temporary native gates can change without changing a single
            // inventory value. Their release must wake previously rejected work.
            int admissionGates = (player.HasLockedInventory() ? 1 : 0) | (HasManualOperation ? 2 : 0) |
                (player.itemAnimation > 0 || player.itemTime > 0 ? 4 : 0) | (player.tileEntityAnchor.IsInValidUseTileEntity() ? 8 : 0);
            if (!ReferenceEquals(previousShop, shop) || !ReferenceEquals(previousNpc, npc))
            { previousShop = shop; previousNpc = npc; shopIdentity++; }
            bool changed = previous == null || cached == null || cached.Session != SessionGeneration ||
                cached.ShopAvailable != available || cached.ShopIdentity != shopIdentity || previousAdmissionGates != admissionGates;
            bool referencesChanged = false;
            var current = new ItemSlotObservation[58];
            for (int i = 0; i < current.Length; i++)
            {
                Item item = player.inventory[i];
                if (!ReferenceEquals(item, references[i])) { references[i] = item; instances[i] = ++nextInstance; referencesChanged = true; }
                current[i] = item == null ? new ItemSlotObservation(i, default(ItemIdentity), 0, 0, instances[i], true) :
                    new ItemSlotObservation(i, new ItemIdentity(item.type, item.prefix), item.stack, item.maxStack, instances[i], IsProtected(player, item, i));
                if (!changed && !Equal(previous[i], current[i])) changed = true;
            }
            // Native rejected sales can clone all 58 slots without changing a
            // value. Refresh live identities, but do not turn clone churn into a
            // new business revision that repeatedly attempts an unchanged sale.
            if (changed || referencesChanged)
            { if (changed) revision++; previous = current; cached = new ItemInventoryObservation(SessionGeneration, revision, available, shopIdentity, current); }
            previousAdmissionGates = admissionGates; observation = cached; return true;
        }
        internal bool Matches(ItemSlotObservation observed)
        {
            Player player = Player;
            if (player == null || Busy || !observed.IsCandidate || observed.Slot >= player.inventory.Length) return false;
            Item item = player.inventory[observed.Slot];
            return item != null && ReferenceEquals(item, references[observed.Slot]) && observed.Instance == instances[observed.Slot] &&
                item.type == observed.Identity.Type && item.prefix == observed.Identity.Prefix && item.stack == observed.Stack &&
                item.maxStack == observed.MaximumStack && !IsProtected(player, item, observed.Slot);
        }
        internal bool MatchesShop(long identity)
        {
            Chest shop; NPC npc;
            return TryShop(out shop, out npc) && identity == shopIdentity && ReferenceEquals(shop, previousShop) && ReferenceEquals(npc, previousNpc);
        }
        internal bool TryShop(out Chest shop, out NPC npc)
        {
            shop = null; npc = null;
            Player player = Player;
            if (player == null || !Main.playerInventory || Main.instance == null || Main.npcShop <= 0 || Main.instance.shop == null || Main.npcShop >= Main.instance.shop.Length ||
                player.talkNPC < 0 || player.talkNPC >= Main.npc.Length) return false;
            NPC current = Main.npc[player.talkNPC];
            Chest currentShop = Main.instance.shop[Main.npcShop];
            if (current == null || !current.active || !current.friendly || currentShop == null || currentShop.item == null || currentShop.item.Length < 39) return false;
            shop = currentShop; npc = current; return true;
        }
        private bool IsProtected(Player player, Item item, int slot)
        {
            return item.favorited || slot == player.selectedItem || slot == ManualSlot ||
                ManualMaterials.Contains(item) || ReferenceEquals(item, Main.mouseItem) ||
                player.inventoryChestStack[slot] || ownership.IsProtected(slot) ||
                (AdditionalProtection != null && AdditionalProtection(item));
        }
        private static bool Equal(ItemSlotObservation a, ItemSlotObservation b)
        { return a.Identity.Equals(b.Identity) && a.Stack == b.Stack && a.MaximumStack == b.MaximumStack && a.Protected == b.Protected; }
    }

    internal sealed class ItemSessionProbe : IGameSessionIdentityProbe
    {
        private Player player;
        private object world, socket, token;
        private int mode;
        public bool IsSessionActive
        { get { Player current = Main.LocalPlayer; return !Main.gameMenu && !Main.dedServ && (Main.netMode == 0 || Main.netMode == 1) &&
                    current != null && current.active && !current.dead; } }
        public object SessionIdentity
        {
            get
            {
                object currentWorld = Main.ActiveWorldFileData;
                object currentSocket = Main.netMode == 1 ? Netplay.Connection.Socket : null;
                if (token == null || !ReferenceEquals(player, Main.LocalPlayer) || !ReferenceEquals(world, currentWorld) ||
                    !ReferenceEquals(socket, currentSocket) || mode != Main.netMode)
                { player = Main.LocalPlayer; world = currentWorld; socket = currentSocket; mode = Main.netMode; token = new object(); }
                return token;
            }
        }
    }
}
