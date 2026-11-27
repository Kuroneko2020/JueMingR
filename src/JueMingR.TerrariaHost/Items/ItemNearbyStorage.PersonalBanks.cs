using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using JueMingR.Platform.Items;
using JueMingR.TerrariaHost.World;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Items
{
    internal sealed partial class ItemNearbyStorage
    {
        internal sealed partial class NativeChestStack
        {
            private MethodInfo transferBanks, restoreBankSources;
            private object playerSaveLock;

            private void BindPersonalBanks()
            {
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
                transferBanks = typeof(QuickStacking).GetMethod("Transfer", flags, null,
                    new[] { sourceType, typeof(List<PositionedChest>), typeof(List<int>).MakeByRefType(), typeof(bool) }, null);
                restoreBankSources = typeof(QuickStacking).GetMethod("RestoreToPlayer", flags, null, new[] { typeof(Player), sourceType }, null);
                playerSaveLock = typeof(Player).GetField("IOLock", flags)?.GetValue(null);
                if (transferBanks == null || transferBanks.ReturnType != typeof(void) ||
                    restoreBankSources == null || restoreBankSources.ReturnType != typeof(void) || playerSaveLock == null)
                    throw new MissingMethodException("selective-bank-transfer-abi");
            }

            internal int? ExecuteBanks(Player player, ItemIdentity identity, Item[] items,
                PlayerItemSlotID.SlotReference[] slots, ulong mask, Predicate<Chest> admitted, bool localPlayerSave = false)
            {
                List<PositionedChest> banks = ReachableBanks(player, items[0], admitted);
                if (banks == null || banks.Count == 0) return 0;
                var input = new List<Item>();
                var sourceSlots = new List<PlayerItemSlotID.SlotReference>();
                int original = 0;
                for (int i = 0; i < slots.Length; i++)
                {
                    if ((mask & (1UL << (slots[i].SlotId - PlayerItemSlotID.Inventory0))) == 0) continue;
                    input.Add(items[i]); sourceSlots.Add(slots[i]); original += items[i].stack;
                }
                var targets = new List<Chest>(banks.Count);
                foreach (var bank in banks) targets.Add(bank.chest);
                object native = Prepare(player, input.ToArray(), sourceSlots.ToArray());
                // Saves can read both arrays on a worker. Keep the native
                // bank writes and source-reference restores indivisible to
                // Player.SavePlayer, and leave the game thread free if busy.
                if (localPlayerSave && !Monitor.TryEnter(playerSaveLock)) return null;
                try
                {
                    long targetBefore = Quantity(targets, identity);
                    // Use the native matching and stack limits on just the
                    // admitted slots. Restore also seals any partial swaps.
                    try { transferBanks.Invoke(null, new[] { native, (object)banks, null, false }); }
                    finally { restoreBankSources.Invoke(null, new[] { (object)player, native }); }
                    int remaining = 0;
                    foreach (var slot in sourceSlots)
                    {
                        Item actual = slot.Item;
                        if (actual.IsAir) continue;
                        if (actual.type != identity.Type || actual.prefix != identity.Prefix)
                            throw new InvalidOperationException("bank-source-replaced");
                        remaining += actual.stack;
                    }
                    int moved = original - remaining;
                    if (moved < 0 || Quantity(targets, identity) - targetBefore != moved)
                        throw new InvalidOperationException("bank-transfer-conservation-not-confirmed");
                    return moved;
                }
                finally { if (localPlayerSave) Monitor.Exit(playerSaveLock); }
            }

            private static List<PositionedChest> ReachableBanks(Player player, Item source, Predicate<Chest> admitted)
            {
                var possible = new[] { player.bank, player.bank2, player.bank3, player.bank4 };
                int pending = 0;
                for (int i = 0; i < possible.Length; i++)
                    if (HasMatchingCapacity(possible[i], source) && admitted(possible[i])) pending |= 1 << i;
                if ((pending & 8) != 0)
                {
                    if (player.disableVoidBag >= 0) pending &= ~8;
                    for (int slot = 0; slot < 58 && (pending & 8) != 0; slot++)
                        if (player.inventory[slot].stack > 0 && player.inventory[slot].type == ItemID.ClosedVoidBag) pending &= ~8;
                }
                if (pending == 0) return null;
                var banks = new List<PositionedChest>(4);
                // NearbyChests.GetBanksInRangeOf in 1.4.6.1 uses this tile
                // rectangle and finds projectile entrances first. Its tile
                // branch also reads stale type from inactive/unloaded cells.
                const int radius = 600 / 16 + 2;
                Point center = player.Center.ToTileCoordinates();
                var area = new Rectangle(center.X - radius, center.Y - radius, radius * 2 + 1, radius * 2 + 1);
                if ((pending & 9) != 0)
                    for (int slot = 0; slot < Main.maxProjectiles; slot++)
                    {
                        Projectile provider = Main.projectile[slot];
                        if (provider == null || !provider.active || !provider.TryGetContainerIndex(out int container)) continue;
                        int index = -container - 2;
                        if (index < 0 || index >= possible.Length || (pending & 1 << index) == 0 ||
                            !area.Contains(provider.Hitbox.ClosestPointInRect(player.Center).ToTileCoordinates())) continue;
                        banks.Add(new PositionedChest(possible[index], provider.Center));
                        pending &= ~(1 << index);
                        if ((pending & 9) == 0) break;
                    }
                if (Main.tile == null) return banks;
                for (int x = Math.Max(0, area.Left); x < Math.Min(area.Right, Main.maxTilesX) && pending != 0; x++)
                    for (int y = Math.Max(0, area.Top); y < Math.Min(area.Bottom, Main.maxTilesY) && pending != 0; y++)
                    {
                        Tile tile = Main.tile[x, y];
                        if (tile == null || !tile.active()) continue;
                        int index;
                        switch (tile.type)
                        {
                            case TileID.PiggyBank: index = 0; break;
                            case TileID.Safes: index = 1; break;
                            case TileID.DefendersForge: index = 2; break;
                            case TileID.VoidVault: index = 3; break;
                            default: continue;
                        }
                        if ((pending & 1 << index) == 0 || !WorldTileKnowledge.CellKnown(x, y)) continue;
                        banks.Add(new PositionedChest(possible[index], new Vector2(x * 16 + 16, y * 16 + 16)));
                        pending &= ~(1 << index);
                    }
                return banks;
            }

            private static bool HasMatchingCapacity(Chest bank, Item source)
            {
                bool sameType = false, room = false;
                foreach (Item item in bank.item)
                {
                    if (item.IsAir) room = true;
                    else if (item.type == source.type)
                    {
                        sameType = true;
                        room |= item.stack < item.maxStack && Item.CanStack(source, item);
                    }
                    if (sameType && room) return true;
                }
                return false;
            }
        }
    }
}
