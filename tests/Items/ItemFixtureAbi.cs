using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;

// Independent, deliberately small test doubles. They model the documented side
// effects used by the adapters, not Terraria's complete algorithms or authority.
// No original game source is embedded or run by this fixture.
namespace Terraria
{
    public class Item
    {
        public int type, stack, maxStack = 9999, value = 5, ammo, useAmmo, tileWand, shoot, bait;
        public byte prefix;
        public bool favorited, buyOnce;
        public bool PaintOrCoating { get; set; }
        public string DisplayName;
        public static int AffixReads;
        public static Action OnAffixName;
        public string Name { get { return DisplayName ?? "fixture-item-" + type; } }
        public string AffixName()
        { AffixReads++; if (OnAffixName != null) OnAffixName(); return prefix > 0 ? "锋利的" + Name : Name; }
        public GameContent.FlexibleTileWand Flexible;
        public bool IsAir { get { return type == 0 || stack <= 0; } }
        public Item Clone() { return (Item)MemberwiseClone(); }
        public void TurnToAir() { type = 0; stack = 0; prefix = 0; favorited = false; }
        public bool IsNetStateDifferent(Item other) { return type != other.type || stack != other.stack || prefix != other.prefix || favorited != other.favorited; }
        public static bool CanStack(Item a, Item b) { return a.type == b.type && a.prefix == b.prefix; }
        public GameContent.FlexibleTileWand GetFlexibleTileWand() { return Flexible; }
    }
    public sealed class WorldItem
    { public Item inner { get; set; } public int stack { get { return inner.stack; } } }
    public struct GetItemSettings { }
    public sealed partial class NPC : Entity { public bool active = true, friendly = true; }
    public sealed class Anchor { public bool Busy; public bool IsInValidUseTileEntity() { return Busy; } }
    public class Projectile
    { public int type, owner; public bool active, bobber; public readonly float[] localAI = new float[3]; }
    public sealed class Chest
    {
        public bool FixtureLocked, FixtureOccupied;
        public Item[] item = Enumerable.Range(0, 40).Select(_ => new Item()).ToArray();
        public void AddItemToShop(Item source)
        {
            int remaining = Math.Max(0, source.stack - Main.shopSellbackHelper.GetAmount(source));
            for (int i = 0; i < 39 && remaining > 0; i++)
            {
                Item target = item[i];
                if (!target.buyOnce || !Item.CanStack(target, source)) continue;
                int move = Math.Min(remaining, target.maxStack - target.stack); target.stack += move; remaining -= move;
            }
            for (int i = 0; i < 39 && remaining > 0; i++) if (item[i].IsAir)
            { item[i] = source.Clone(); item[i].buyOnce = true; item[i].stack = remaining; return; }
        }
    }
    public partial class Main
    {
        public static bool playerInventory, ServerSideCharacter, PendingInventory, NativeVoidPending;
        public static int cursorOverride, myPlayer;
        public static int npcShop { get; set; }
        public static Item mouseItem = new Item();
        public static NPC[] npc = { new NPC() };
        public static Projectile[] projectile = new Projectile[1000];
        public static GameContent.ItemShopSellbackHelper shopSellbackHelper = new GameContent.ItemShopSellbackHelper();
        public static IO.WorldFileData ActiveWorldFileData = new IO.WorldFileData();
        public Chest[] shop = { new Chest(), new Chest() };
        public static DataStructures.DrawAnimation[] itemAnimations = new DataStructures.DrawAnimation[ItemID.Count];
        public void LoadItem(int type) { }
        public static bool LocalPlayerHasPendingInventoryActions() { return PendingInventory; }
    }
    public static class Lang { public static string GetItemNameValue(int type) { return "测试物品 " + type; } }
    public static class Netplay { public static RemoteServer Connection = new RemoteServer(); }
    public sealed class RemoteServer { public Net.Sockets.ISocket Socket = new Net.Sockets.FixtureSocket(); }
    public partial class Entity { public int whoAmI; public Vector2 position; public Vector2 Center { get; set; } }
    public static class WorldGen
    { [MethodImpl(MethodImplOptions.NoInlining)] public static void SaveAndQuit(Action callback) { callback?.Invoke(); } }
    public sealed partial class Player : Entity
    {
        public readonly Item[] inventory = Enumerable.Range(0, 59).Select(_ => new Item()).ToArray();
        public readonly bool[] inventoryChestStack = new bool[59];
        public Item trashItem = new Item();
        public DataStructures.PlayerInteractionAnchor tileEntityAnchor = new DataStructures.PlayerInteractionAnchor();
        public bool dead, channel, autoPaint, autoActuator, RejectSale, ThrowSaleAfterCredit, ThrowGrant;
        public bool FixtureVoidReceive;
        public int FixtureVoidQuantity;
        public Item FixtureGrantRemainder;
        public int itemAnimation, itemTime, SellCalls, Paints, Actuators, Uses, Places, WireEffects;
        public int selectedItem { get; set; }
        public int talkNPC { get; set; }
        public long Selling = 25, Buying = 125;
        public static int FlexibleWandRandomSeed, FlexibleWandCycleOffset;
        public bool HasLockedInventory() { return Main.PendingInventory || Main.NativeVoidPending || inventoryChestStack.Any(b => b); }
        public void GetItemExpectedPrice(Item item, out long sell, out long buy) { sell = item.value == 0 ? 0 : Selling; buy = Buying; }
        [MethodImpl(MethodImplOptions.NoInlining)] public bool SellItem(Item item, int stack = -1)
        {
            SellCalls++;
            if (RejectSale) { for (int i = 0; i < 58; i++) inventory[i] = inventory[i].Clone(); return false; }
            if (item.value == 0) return false;
            int bought = Math.Min(Main.shopSellbackHelper.Count, item.stack);
            long unit = Math.Max(1, Selling / 5), coins = unit * (item.stack - bought) + Buying * bought;
            inventory[50].type = 71; inventory[50].stack += (int)coins;
            if (ThrowSaleAfterCredit) throw new InvalidOperationException("fixture-transaction-interrupted");
            return true;
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public bool BuyItem(long price, int customCurrency = -1) { inventory[50].stack -= (int)price; return true; }
        [MethodImpl(MethodImplOptions.NoInlining)] public Item GetItem(Item incoming, GetItemSettings settings)
        {
            if (FixtureVoidReceive) { FixtureVoidQuantity += incoming.stack; return new Item(); }
            if (incoming.ammo > 0) incoming = FillAmmo(incoming, settings);
            for (int i = 0; i < 50 && !incoming.IsAir; i++) if (GetItem_FillIntoOccupiedSlot(incoming, settings, incoming, i)) return new Item();
            for (int i = 0; i < 50 && !incoming.IsAir; i++) if (GetItem_FillEmptyInventorySlot(incoming, settings, incoming, i)) return new Item();
            return incoming;
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private bool GetItem_FillIntoOccupiedSlot(Item incoming, GetItemSettings settings, Item remaining, int i)
        {
            if (inventory[i].IsAir || !Item.CanStack(inventory[i], incoming)) return false;
            int quantity = Math.Min(incoming.stack, inventory[i].maxStack - inventory[i].stack);
            inventory[i].stack += quantity; incoming.stack -= quantity; return incoming.stack == 0;
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private bool GetItem_FillEmptyInventorySlot(Item incoming, GetItemSettings settings, Item remaining, int i)
        { if (!inventory[i].IsAir) return false; inventory[i] = incoming.Clone(); incoming.TurnToAir(); return true; }
        [MethodImpl(MethodImplOptions.NoInlining)] public Item FillAmmo(Item incoming, GetItemSettings settings)
        {
            for (int i = 54; i < 58; i++)
            {
                if (inventory[i].type <= 0 || !Item.CanStack(inventory[i], incoming)) continue;
                int amount = Math.Min(incoming.stack, inventory[i].maxStack - inventory[i].stack);
                inventory[i].stack += amount; incoming.stack -= amount;
                if (incoming.stack == 0) return new Item();
            }
            for (int i = 54; i < 58; i++)
            {
                if (inventory[i].type != 0) continue;
                inventory[i] = incoming.Clone(); incoming.TurnToAir(); return new Item();
            }
            return incoming;
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private void PickupItem(WorldItem world) { world.inner = GetItem(world.inner, new GetItemSettings()); }
        public void Pickup(WorldItem item) { PickupItem(item); }
        [MethodImpl(MethodImplOptions.NoInlining)] public void DropItems(bool dropCoins) { inventory[10].TurnToAir(); }
        [MethodImpl(MethodImplOptions.NoInlining)] public void DropCoins() { inventory[50].TurnToAir(); }
        [MethodImpl(MethodImplOptions.NoInlining)] public void DropSelectedItem(int slot, ref Item item) { item.TurnToAir(); }
        [MethodImpl(MethodImplOptions.NoInlining)] private Item PickAmmo_IterateRange(Item held, Item[] items, int[] order, bool cycle)
        { foreach (int slot in order) if (items[slot].stack > 0 && items[slot].ammo == held.useAmmo) return items[slot]; return null; }
        public Item Pick(Item held) { return PickAmmo_IterateRange(held, inventory, new[] { 54, 55, 56, 57, 10, 11 }, false); }
        [MethodImpl(MethodImplOptions.NoInlining)] private bool ItemCheck_TryStartUse(Item held, bool ignored) { Uses++; return true; }
        public bool Use(Item held) { return ItemCheck_TryStartUse(held, false); }
        [MethodImpl(MethodImplOptions.NoInlining)] public bool ConsumeItem(int type, bool reverseOrder = false, bool includeVoidBag = false)
        {
            for (int n = 0; n < 58; n++) { int i = reverseOrder ? 57 - n : n; if (inventory[i].type != type || inventory[i].stack <= 0) continue; inventory[i].stack--; return true; }
            return false;
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public Item FindPaintOrCoating()
        { for (int i = 0; i < 58; i++) if (inventory[i].stack > 0 && inventory[i].PaintOrCoating) return inventory[i]; return null; }
        [MethodImpl(MethodImplOptions.NoInlining)] private bool ItemCheck_CheckFishingBobber_ConsumeBait(Projectile bobber, out int used)
        {
            for (int i = 0; i < 58; i++) if (inventory[i].stack > 0 && inventory[i].type == (int)bobber.localAI[2])
            { used = inventory[i].type; inventory[i].stack--; return true; }
            used = 0; return false;
        }
        [MethodImpl(MethodImplOptions.NoInlining)] private bool PlaceThing_Tiles_CheckWandUsability(bool canUse) { return canUse; }
        [MethodImpl(MethodImplOptions.NoInlining)] private void PlaceThing_Tiles(bool place) { if (place) Places++; }
        public void Place() { PlaceThing_Tiles(true); }
        public int FindItem(int type) { for (int i = 0; i < 58; i++) if (inventory[i].type == type && inventory[i].stack > 0) return i; return -1; }
        [MethodImpl(MethodImplOptions.NoInlining)] private void PlaceThing_Tiles_PlaceIt_AutoPaintAndActuate(Vector3[,] cache, int tile)
        { if (autoPaint && FindPaintOrCoating() != null) Paints++; int slot = FindItem(849); if (autoActuator && slot >= 0) { Actuators++; inventory[slot].stack--; } }
        public void Decorate() { PlaceThing_Tiles_PlaceIt_AutoPaintAndActuate(null, 1); }
        [MethodImpl(MethodImplOptions.NoInlining)] private void ItemCheck_UseWiringTools(Item held) { WireEffects++; }
    }
    public sealed class MessageBuffer
    {
        public byte[] readBuffer = new byte[2048];
        public int whoAmI = 256;
        [MethodImpl(MethodImplOptions.NoInlining)] public void GetData(int start, int length, out int type)
        {
            type = readBuffer[start]; if (type != 5) return;
            int slot = BitConverter.ToInt16(readBuffer, start + 2);
            if (slot < 0 || slot >= 58 || !Main.LocalPlayer.HasLockedInventory()) return;
            Main.LocalPlayer.inventory[slot] = new Item { type = BitConverter.ToInt16(readBuffer, start + 7), stack = BitConverter.ToInt16(readBuffer, start + 4), prefix = readBuffer[start + 6] };
            Main.LocalPlayer.inventoryChestStack[slot] = false;
        }
    }
}
namespace Terraria.UI
{
    public static class ItemSlot
    {
        public static int TrashResearch, ManualClicks, TrashFailure;
        [MethodImpl(MethodImplOptions.NoInlining)] private static bool OverrideLeftClick(Item[] inv, int context, int slot)
        {
            Item item = inv[slot]; Player player = Main.LocalPlayer;
            if (Main.cursorOverride == 10)
            {
                if (player.SellItem(item) || item.value == 0) { Main.instance.shop[Main.npcShop].AddItemToShop(item); item.TurnToAir(); }
                return true;
            }
            if (Main.cursorOverride == 6)
            {
                TrashResearch++;
                if (TrashFailure == 2) return true;
                player.trashItem = item.Clone(); item.TurnToAir();
                if (TrashFailure == 1) throw new InvalidOperationException("fixture-trash-interrupted");
                if (TrashFailure == 3) player.trashItem.stack++;
                return true;
            }
            return false;
        }
        [MethodImpl(MethodImplOptions.NoInlining)] public static void LeftClick(Item[] inv, int context, int slot)
        { if (!OverrideLeftClick(inv, context, slot)) { ManualClicks++; inv[slot].TurnToAir(); } }
        [MethodImpl(MethodImplOptions.NoInlining)] public static void RightClick(Item[] inv, int context, int slot) { TryOpenContainer(inv, context, slot, Main.LocalPlayer); }
        [MethodImpl(MethodImplOptions.NoInlining)] public static void Handle(Item[] inv, int context, int slot, bool allow)
        { if (allow && Main.mouseLeft) LeftClick(inv, context, slot); }
        [MethodImpl(MethodImplOptions.NoInlining)] public static string GetGamepadInstructions(Item[] inv, int context, int slot) { ManualClicks++; return "fixture"; }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void TryOpenContainer(Item[] inv, int context, int slot, Player player)
        { Item item = inv[slot]; if (TryOpenContainer_GrantItems(item, player) && --item.stack == 0) item.TurnToAir(); }
        [MethodImpl(MethodImplOptions.NoInlining)] private static bool TryOpenContainer_GrantItems(Item item, Player player)
        {
            if (item.type <= 0 || !ItemID.Sets.OpenableBag[item.type]) return false;
            if (item.type == 1000 && !player.ConsumeItem(327)) return false;
            player.FixtureGrantRemainder = player.GetItem(new Item { type = 8, stack = 3 }, new GetItemSettings());
            if (player.ThrowGrant) throw new InvalidOperationException("fixture-grant-second-item-failed");
            return true;
        }
    }
    public static class ItemSorting
    {
        public static int Calls;
        [MethodImpl(MethodImplOptions.NoInlining)] public static void SortInventory() { if (!Main.LocalPlayer.HasLockedInventory()) { Calls++; Array.Reverse(Main.LocalPlayer.inventory, 10, 40); } }
    }
}
namespace Terraria.ID
{
    public static class ItemID { public static readonly short Count = 6200; public static class Sets { public static readonly bool[] OpenableBag = new bool[Count]; } }
    public static class PlayerItemSlotID
    {
        public static readonly int Inventory0 = 0;
        public struct SlotReference { public readonly Terraria.Player Player; public readonly int Slot; public SlotReference(Terraria.Player player, int slot) { Player = player; Slot = slot; } }
    }
}
namespace Terraria.GameContent
{
    public sealed class ItemShopSellbackHelper { public int Count; public int GetAmount(Item item) { return Count; } }
    public static partial class TextureAssets
    { public static ReLogic.Content.Asset<Texture2D>[] Item = new ReLogic.Content.Asset<Texture2D>[ItemID.Count]; }
    public struct PositionedChest { public Chest chest; public Vector2 position; }
    public static class NearbyChests
    {
        public static readonly List<PositionedChest> Targets = new List<PositionedChest>();
        public static List<PositionedChest> GetChestsInRangeOf(Vector2 position, float range = 0) { return Targets.Where(c => Vector2.Distance(c.position, position) <= (range == 0 ? 600 : range)).ToList(); }
    }
    public sealed class FlexibleTileWand
    {
        public sealed class PlacementOption { }
        public bool ConsumesAmmoItem = true;
        public bool TryGetPlacementOption(Player player, int seed, int offset, out PlacementOption option, out Item item)
        { option = new PlacementOption(); item = player.inventory[10]; return !item.IsAir; }
    }
    public static class QuickStacking
    {
        internal struct SourceInventory
        {
            public Item[] items; public int numItems; public PlayerItemSlotID.SlotReference[] slots; public bool[] transferBlocked; public Vector2 position;
            internal SourceInventory(int count) { items = new Item[count]; numItems = count; slots = new PlayerItemSlotID.SlotReference[count]; transferBlocked = new bool[count]; position = Vector2.Zero; }
        }
        public static int Calls;
        public static int[] LastSlots;
        [MethodImpl(MethodImplOptions.NoInlining)] public static void QuickStackToNearbyChests(Player player, bool coins) { Calls++; }
        [MethodImpl(MethodImplOptions.NoInlining)] public static void QuickStackToNearbyInventories(Player player, bool smartStack = false) { Calls++; }
        [MethodImpl(MethodImplOptions.NoInlining)] private static void QuickStackToNearbyChests(Player player, SourceInventory source, bool coins)
        {
            Calls++; LastSlots = source.slots.Select(s => s.Slot).ToArray();
            if (Main.netMode == 1) { foreach (var slot in source.slots) player.inventoryChestStack[slot.Slot] = true; return; }
            foreach (PositionedChest chest in NearbyChests.GetChestsInRangeOf(player.position))
                for (int i = 0; i < source.numItems; i++)
                {
                    Item item = source.items[i];
                    if (chest.chest.FixtureLocked || chest.chest.FixtureOccupied || !chest.chest.item.Any(t => !t.IsAir && Item.CanStack(t, item))) continue;
                    foreach (Item target in chest.chest.item)
                    {
                        if (!Item.CanStack(item, target) || target.IsAir) continue;
                        int amount = Math.Min(item.stack, target.maxStack - target.stack); target.stack += amount; item.stack -= amount;
                    }
                    for (int slot = 0; slot < chest.chest.item.Length && item.stack > 0; slot++)
                    {
                        if (!chest.chest.item[slot].IsAir) continue;
                        int amount = Math.Min(item.stack, item.maxStack); chest.chest.item[slot] = item.Clone();
                        chest.chest.item[slot].stack = amount; item.stack -= amount;
                    }
                }
            for (int i = 0; i < source.numItems; i++) if (source.items[i].stack == 0) source.items[i].TurnToAir();
        }
    }
}
namespace Terraria.GameInput
{
    public enum InputMode { Keyboard, KeyboardUI, Mouse, XBoxGamepad, XBoxGamepadUI }
    public sealed class KeyConfiguration { public Dictionary<string, List<string>> KeyStatus = new Dictionary<string, List<string>>(); }
    public sealed class PlayerInputProfile { public Dictionary<InputMode, KeyConfiguration> InputModes = new Dictionary<InputMode, KeyConfiguration> { { InputMode.Keyboard, new KeyConfiguration() }, { InputMode.KeyboardUI, new KeyConfiguration() }, { InputMode.Mouse, new KeyConfiguration() } }; }
    public static partial class PlayerInput { public static PlayerInputProfile CurrentProfile { get; set; } = new PlayerInputProfile(); public static bool CurrentlyRebinding { get; set; } }
}
namespace Terraria.IO { public sealed class WorldFileData { } }
namespace Terraria.Net.Sockets { public interface ISocket { } public sealed class FixtureSocket : ISocket { } }
namespace Terraria.DataStructures
{
    public struct PlayerInteractionAnchor { public bool Busy; public bool IsInValidUseTileEntity() { return Busy; } }
    public class DrawAnimation { public virtual Rectangle GetFrame(Texture2D texture, int frameCounterOverride = -1) { return texture.Bounds; } }
}
