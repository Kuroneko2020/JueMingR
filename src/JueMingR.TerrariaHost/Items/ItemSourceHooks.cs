using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JueMingR.Platform.Items;
using Terraria;
using Terraria.ID;
using Terraria.UI;

namespace JueMingR.TerrariaHost.Items
{
    internal static class ItemSourceHooks
    {
        private static HostItems host;
        private static Origin current;
        internal static void Install(HostItems owner, Harmony harmony)
        {
            host = owner;
            Patch(harmony, typeof(Player), "PickupItem", new[] { typeof(WorldItem) }, nameof(PickupBefore), nameof(OriginAfter), nameof(OriginFinalizer));
            Patch(harmony, typeof(ItemSlot), "TryOpenContainer", new[] { typeof(Item[]), typeof(int), typeof(int), typeof(Player) }, nameof(OpenBefore), nameof(OriginAfter), nameof(OriginFinalizer));
            Patch(harmony, typeof(ItemSlot), "TryOpenContainer_GrantItems", new[] { typeof(Item), typeof(Player) }, null, nameof(GrantAfter));
            Patch(harmony, typeof(Player), "GetItem", new[] { typeof(Item), typeof(GetItemSettings) }, nameof(GetBefore), nameof(GetAfter));
            Patch(harmony, typeof(MessageBuffer), "GetData", new[] { typeof(int), typeof(int), typeof(int).MakeByRefType() }, nameof(MessageBefore), nameof(MessageAfter));
            Patch(harmony, typeof(Player), "DropItems", new[] { typeof(bool) }, nameof(PlayerBoundary));
            Patch(harmony, typeof(Player), "DropCoins", Type.EmptyTypes, nameof(PlayerBoundary));
            Patch(harmony, typeof(WorldGen), "SaveAndQuit", new[] { typeof(Action) }, nameof(WorldBoundary));
        }
        internal static void Patch(Harmony harmony, Type type, string name, Type[] parameters, string prefix = null, string postfix = null, string finalizer = null)
        {
            MethodInfo original = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, parameters, null);
            if (original == null) throw new MissingMethodException(type.FullName, name);
            harmony.Patch(original, Hook(prefix), Hook(postfix), null, Hook(finalizer));
        }
        private static HarmonyMethod Hook(string name)
        { return name == null ? null : new HarmonyMethod(typeof(ItemSourceHooks).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)); }
        private static void PickupBefore(Player __instance, WorldItem __0, out Origin __state)
        {
            __state = null;
            if (!host.CanObserve || current != null || !ReferenceEquals(__instance, host.World.Player) || __0 == null || __0.inner == null) return;
            __state = new Origin(__instance, __0.inner, __0); current = __state; host.World.CausalDepth++;
        }
        private static void OpenBefore(Item[] __0, int __1, int __2, Player __3, out Origin __state)
        {
            __state = null;
            if (!host.CanObserve || current != null || !ReferenceEquals(__3, host.World.Player) || __0 == null || __2 < 0 || __2 >= __0.Length) return;
            Item item = __0[__2];
            if (item == null || item.type <= 0 || item.type >= ItemID.Sets.OpenableBag.Length || !ItemID.Sets.OpenableBag[item.type]) return;
            __state = new Origin(__3, item, null); current = __state; host.World.CausalDepth++;
            if (ReferenceEquals(__0, __3.inventory)) { host.World.ManualSlot = __2; host.World.ManualItem = item; }
        }
        private static void GrantAfter(Item __0, Player __1, bool __result)
        { if (current != null && current.WorldItem == null && ReferenceEquals(current.Item, __0) && ReferenceEquals(current.Player, __1)) current.Opened = __result; }
        private static void GetBefore(Player __instance, Item __0, out Gain __state)
        {
            __state = default(Gain);
            try
            {
            if (current == null || !ReferenceEquals(current.Player, __instance) || __0 == null || __0.type <= 0 || __0.type >= 71 && __0.type <= 74) return;
            var identity = new ItemIdentity(__0.type, __0.prefix);
            current.Touched.Add(identity);
            __state = new Gain(current, identity, Quantity(__instance, identity));
            }
            catch { host.FailClosed(); }
        }
        private static void GetAfter(Player __instance, Gain __state)
        {
            try
            {
                if (__state.Origin != null && ReferenceEquals(current, __state.Origin) && Quantity(__instance, __state.Identity) > __state.Before)
                    current.Gained.Add(__state.Identity);
            }
            catch { host.FailClosed(); }
        }
        private static void OriginAfter(Origin __state) { Finish(__state, true); }
        private static Exception OriginFinalizer(Exception __exception, Origin __state)
        { if (__exception != null) Finish(__state, false); return __exception; }
        private static void Finish(Origin origin, bool returned)
        {
            if (origin == null || !ReferenceEquals(origin, current)) return;
            current = null; host.World.CausalDepth = Math.Max(0, host.World.CausalDepth - 1);
            // An interrupted original grant may already have produced items.
            // Preserve those items and stop automation; do not reinterpret the
            // partially completed scope as ordinary sell/trash candidates.
            if (!returned) { HoldInterrupted(origin); return; }
            try
            {
            bool completed = returned && (origin.WorldItem != null ? origin.WorldItem.stack < origin.Before :
                origin.Opened && origin.Item.stack == origin.Before - 1);
            if (!completed || !host.CanCapture) return;
            foreach (ItemIdentity identity in origin.Gained) host.Feature.RegisterAcquisition(identity, host.Runtime.Generation, host.Tick);
            }
            catch { host.FailClosed(); }
        }
        private static void HoldInterrupted(Origin origin)
        {
            try
            {
                Player player = host.World.Player; if (!ReferenceEquals(player, origin.Player)) return;
                ulong affected = 0;
                for (int i = 0; i < 58; i++)
                {
                    Item item = player.inventory[i];
                    if (item != null && (ReferenceEquals(item, origin.Item) || host.World.ManualMaterials.Contains(item) ||
                        origin.Touched.Contains(new ItemIdentity(item.type, item.prefix)))) affected |= 1UL << i;
                }
                host.Ownership.HoldInterruptedSource(host.Runtime.Generation, affected);
                host.SourceMessage = "一次手动物品操作中断；受影响槽暂受保护，无关物品仍可处理";
            }
            catch { host.FailClosed(); }
        }
        private static int Quantity(Player player, ItemIdentity identity)
        {
            int quantity = 0;
            for (int i = 0; i < 58; i++)
            {
                if (i >= 50 && i < 54) continue;
                Item item = player.inventory[i];
                if (item != null && item.type == identity.Type && item.prefix == identity.Prefix && item.stack > 0) quantity = checked(quantity + item.stack);
            }
            return quantity;
        }
        private static void MessageBefore(MessageBuffer __instance, int __0, int __1, out ItemNearbyStorage.Receipt __state)
        { __state = default(ItemNearbyStorage.Receipt); try { __state = host.Storage.BeforeMessage(__instance, __0, __1); } catch { host.FailClosed(); } }
        private static void MessageAfter(ItemNearbyStorage.Receipt __state) { try { host.Storage.AfterMessage(__state); } catch { host.FailClosed(); } }
        private static void PlayerBoundary(Player __instance)
        { if (ReferenceEquals(__instance, host.World.Player)) host.Runtime.InvalidateSession(); }
        private static void WorldBoundary() { host.Runtime.InvalidateSession(); }
        internal static void EndSession() { current = null; }
        internal sealed class Origin
        {
            internal readonly Player Player;
            internal readonly Item Item;
            internal readonly WorldItem WorldItem;
            internal readonly int Before;
            internal readonly HashSet<ItemIdentity> Gained = new HashSet<ItemIdentity>();
            internal readonly HashSet<ItemIdentity> Touched = new HashSet<ItemIdentity>();
            internal bool Opened;
            internal Origin(Player player, Item item, WorldItem worldItem) { Player = player; Item = item; WorldItem = worldItem; Before = item.stack; }
        }
        private readonly struct Gain
        {
            internal readonly Origin Origin;
            internal readonly ItemIdentity Identity;
            internal readonly int Before;
            internal Gain(Origin origin, ItemIdentity identity, int before) { Origin = origin; Identity = identity; Before = before; }
        }
    }
}
