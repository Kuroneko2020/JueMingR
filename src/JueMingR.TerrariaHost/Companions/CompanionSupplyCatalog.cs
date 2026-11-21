using System;
using System.Collections.Generic;
using System.Linq;
using JueMingR.TerrariaHost.Recovery;
using Terraria;
using Terraria.ID;

namespace JueMingR.TerrariaHost.Companions
{
    internal enum CompanionSupplyKind { Life, Mana, Growth, Buff }

    internal static class CompanionSupplyCatalog
    {
        private static readonly short[] GrowthTypes =
        {
            ItemID.LifeCrystal, ItemID.ManaCrystal, ItemID.LifeFruit, ItemID.DemonHeart,
            ItemID.ArtisanLoaf, ItemID.AegisCrystal, ItemID.AegisFruit, ItemID.ArcaneCrystal,
            ItemID.GalaxyPearl, ItemID.GummyWorm, ItemID.Ambrosia, ItemID.TorchGodsFavor, ItemID.MinecartPowerup
        };
        internal sealed class Option
        {
            internal readonly Item Icon;
            internal readonly int[] Types;
            internal readonly int Effect;

            internal Option(Item icon, int[] types, int effect = 0)
            { Icon = icon; Types = types; Effect = effect; }

            internal string Name => Effect == BuffID.WellFed ? "饱食" :
                Effect > 0 ? Lang.GetBuffName(Effect) : Icon.Name;
        }

        private static readonly IReadOnlyList<Option>[] Catalog = new IReadOnlyList<Option>[4];
        private static int definitionCount;

        // Definitions come from the game's pinned catalog and stay available
        // when an actual player runs out or reaches their growth cap.
        internal static IReadOnlyList<Option> Options(CompanionSupplyKind kind)
        {
            var definitions = ContentSamples.ItemsByType;
            if (definitionCount != definitions.Count)
            {
                definitionCount = definitions.Count;
                Array.Clear(Catalog, 0, Catalog.Length);
            }
            int index = (int)kind;
            if (Catalog[index] == null)
            {
                IEnumerable<Item> items = kind == CompanionSupplyKind.Growth
                    ? GrowthTypes.Where(type => definitions.ContainsKey(type)).Select(type => definitions[type])
                    : definitions.Values.Where(item => Candidate(kind, item)).OrderBy(item => item.type);
                Catalog[index] = kind == CompanionSupplyKind.Buff ? BuffOptions(items) :
                    items.Select(item => new Option(item, new[] { item.type })).ToArray();
            }
            return Catalog[index];
        }

        private static Option[] BuffOptions(IEnumerable<Item> items)
        {
            // All food shares one permission control. Luck potions share one
            // native effect but expose distinct strengths. Saves and QuickBuff
            // continue to authorize concrete item types in both cases.
            var groups = items.GroupBy(item => BuffID.Sets.IsWellFed[item.buffType] ? BuffID.WellFed :
                item.buffType == BuffID.Lucky ? -item.type : item.buffType);
            return groups.OrderBy(group => group.Key == BuffID.WellFed ? 0 : group.Key < 0 ? 1 : 2)
                .ThenBy(group => Math.Abs(group.Key))
                .Select(group => new Option(group.First(), group.Select(item => item.type).ToArray(), Math.Max(0, group.Key)))
                .ToArray();
        }

        internal static bool PotionAllowedByDefault(CompanionSupplyKind kind, int type)
        {
            return (kind == CompanionSupplyKind.Life || kind == CompanionSupplyKind.Mana) &&
                ContentSamples.ItemsByType.TryGetValue(type, out Item item) && Candidate(kind, item);
        }

        internal static bool Candidate(CompanionSupplyKind kind, Item item)
        {
            if (item == null || item.IsAir) return false;
            switch (kind)
            {
                case CompanionSupplyKind.Life: return item.potion && item.healLife > 0;
                case CompanionSupplyKind.Mana: return item.healMana > 0;
                case CompanionSupplyKind.Growth: return IsGrowthType(item.type);
                default:
                    return item.consumable && item.healLife == 0 && item.healMana == 0 && item.createTile < 0 &&
                        item.createWall < 0 && item.makeNPC <= 0 &&
                        RecoveryCatalog.BuffCandidate(item) && !Main.lightPet[item.buffType] &&
                        !Main.vanityPet[item.buffType] && !Main.debuff[item.buffType];
            }
        }

        internal static void CountStock(Player body, CompanionSupplyKind kind, Dictionary<int, int> stock)
        {
            stock.Clear();
            if (body == null) return;
            CountStock(body.inventory, kind == CompanionSupplyKind.Growth ? 50 : 58, stock);
            if (kind != CompanionSupplyKind.Growth && body.useVoidBag())
                CountStock(body.bank4.item, body.bank4.maxItems, stock);
        }

        private static void CountStock(Item[] items, int slots, Dictionary<int, int> stock)
        {
            for (int i = 0; i < slots; i++)
            {
                var item = items[i];
                if (item?.stack > 0)
                {
                    stock.TryGetValue(item.type, out int count);
                    stock[item.type] = count + item.stack;
                }
            }
        }

        internal static bool GrowthEligible(Player body, int type)
        {
            if (body == null || !IsGrowthType(type) || GrowthComplete(body, type)) return false;
            return (type != ItemID.LifeFruit || body.statLifeMax >= 400) &&
                (type != ItemID.DemonHeart || Main.expertMode);
        }

        private static bool IsGrowthType(int type)
        {
            return type > 0 && type <= short.MaxValue && Array.IndexOf(GrowthTypes, (short)type) >= 0;
        }

        internal static bool GrowthComplete(Player body, int type)
        {
            if (body == null) return false;
            switch (type)
            {
                case ItemID.LifeCrystal: return body.statLifeMax >= 400;
                case ItemID.ManaCrystal: return body.statManaMax >= 200;
                case ItemID.LifeFruit: return body.statLifeMax >= 500;
                case ItemID.DemonHeart: return body.extraAccessory;
                case ItemID.ArtisanLoaf: return body.ateArtisanBread;
                case ItemID.AegisCrystal: return body.usedAegisCrystal;
                case ItemID.AegisFruit: return body.usedAegisFruit;
                case ItemID.ArcaneCrystal: return body.usedArcaneCrystal;
                case ItemID.GalaxyPearl: return body.usedGalaxyPearl;
                case ItemID.GummyWorm: return body.usedGummyWorm;
                case ItemID.Ambrosia: return body.usedAmbrosia;
                case ItemID.TorchGodsFavor: return body.unlockedBiomeTorches;
                case ItemID.MinecartPowerup: return body.unlockedSuperCart;
                default: return false;
            }
        }
    }
}
